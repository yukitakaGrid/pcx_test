import os, random, sys, time, unittest
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from kdtree import KdTree, quickselect


def d2(a, b):
    return sum((x - y) ** 2 for x, y in zip(a, b))


def rand_pts(rng, n, grid=None):
    if grid:  # 格子上の値 -> 重複・同距離が大量に出る
        return [tuple(float(rng.randrange(grid)) for _ in range(3)) for _ in range(n)]
    return [tuple(rng.uniform(-100, 100) for _ in range(3)) for _ in range(n)]


class QuickselectTest(unittest.TestCase):
    def test_matches_sorted(self):
        rng = random.Random(1)
        for _ in range(300):
            n = rng.randrange(1, 40)
            pts = rand_pts(rng, n, grid=4)
            axis = rng.randrange(3)
            k = rng.randrange(n)
            want = sorted(p[axis] for p in pts)[k]
            quickselect(pts, 0, n, k, axis, rng)
            self.assertEqual(pts[k][axis], want)
            self.assertTrue(all(p[axis] <= want for p in pts[:k]))
            self.assertTrue(all(p[axis] >= want for p in pts[k + 1:]))

    def test_sorted_input_not_quadratic(self):
        n = 200_000
        pts = [(float(i), 0.0, 0.0) for i in range(n)]
        t = time.process_time()
        quickselect(pts, 0, n, n // 2, 0, random.Random(0))
        self.assertLess(time.process_time() - t, 3.0)  # O(n^2) なら桁違いに遅い

    def test_all_equal_not_quadratic(self):
        n = 200_000
        pts = [(1.0, 1.0, 1.0)] * n
        pts = list(pts)
        t = time.process_time()
        quickselect(pts, 0, n, n // 2, 0, random.Random(0))
        self.assertLess(time.process_time() - t, 3.0)


class KdTreeProperty(unittest.TestCase):
    def check(self, pts, rng, trials=30):
        tree = KdTree(pts, seed=rng.randrange(10**9))
        self.assertEqual(len(tree), len(pts))
        for _ in range(trials):
            q = tuple(rng.uniform(-110, 110) for _ in range(3))
            if rng.random() < 0.3 and pts:
                q = rng.choice(pts)  # 点そのもの（距離0）
            k = rng.randrange(1, 12)
            want = sorted(d2(p, q) for p in pts)[:k]
            got = [d ** 2 for d, _ in tree.knn(q, k)]
            self.assertEqual(len(got), len(want))
            for g, w in zip(got, want):
                self.assertAlmostEqual(g, w, places=6)
            # 返った点が本当にその距離であること
            for d, p in tree.knn(q, k):
                self.assertAlmostEqual(d ** 2, d2(p, q), places=6)
            nn = tree.nearest(q)
            if pts:
                self.assertAlmostEqual(nn[0] ** 2, min(d2(p, q) for p in pts), places=6)
            else:
                self.assertIsNone(nn)
            r = rng.uniform(0, 60)
            self.assertEqual(sorted(tree.radius(q, r)),
                             sorted(p for p in pts if d2(p, q) <= r * r))
            a = tuple(rng.uniform(-100, 50) for _ in range(3))
            b = tuple(x + rng.uniform(0, 80) for x in a)
            self.assertEqual(sorted(tree.box(a, b)),
                             sorted(p for p in pts if all(a[i] <= p[i] <= b[i] for i in range(3))))

    def test_random_sizes(self):
        rng = random.Random(42)
        for n in [0, 1, 2, 3, 4, 5, 7, 8, 100, 500]:
            self.check(rand_pts(rng, n), rng)

    def test_many_duplicates(self):
        rng = random.Random(7)
        for n in [10, 100, 400]:
            self.check(rand_pts(rng, n, grid=3), rng)

    def test_all_identical(self):
        rng = random.Random(8)
        self.check([(5.0, 5.0, 5.0)] * 50, rng)

    def test_sorted_and_collinear(self):
        rng = random.Random(9)
        self.check([(float(i), float(i), float(i)) for i in range(300)], rng)
        self.check([(float(i), 0.0, 0.0) for i in range(300)], rng)

    def test_k_larger_than_n_and_nonpositive(self):
        tree = KdTree([(0, 0, 0), (1, 1, 1)])
        self.assertEqual(len(tree.knn((0, 0, 0), 10)), 2)
        self.assertEqual(tree.knn((0, 0, 0), 0), [])
        self.assertEqual(tree.radius((0, 0, 0), -1), [])

    def test_input_not_mutated(self):
        pts = [(3.0, 1.0, 2.0), (1.0, 2.0, 3.0), (2.0, 3.0, 1.0)]
        copy = list(pts)
        KdTree(pts)
        self.assertEqual(pts, copy)

    def test_bad_dimension(self):
        with self.assertRaises(ValueError):
            KdTree([(1.0, 2.0)])

    def test_build_sorted_large(self):
        n = 100_000
        t = time.process_time()
        KdTree([(float(i), float(i), float(i)) for i in range(n)])
        self.assertLess(time.process_time() - t, 10.0)


if __name__ == "__main__":
    unittest.main()

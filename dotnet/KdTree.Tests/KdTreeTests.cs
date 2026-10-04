using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using KdTreeLib;
using Xunit;

public class KdTreeTests
{
    static Point3[] Rand(Random r, int n, int grid = 0)
    {
        var a = new Point3[n];
        for (int i = 0; i < n; i++)
            a[i] = grid > 0
                ? new Point3(r.Next(grid), r.Next(grid), r.Next(grid))   // 重複・同距離が大量に出る
                : new Point3((float)(r.NextDouble() * 200 - 100), (float)(r.NextDouble() * 200 - 100), (float)(r.NextDouble() * 200 - 100));
        return a;
    }

    static void Check(Point3[] pts, Random rng, int trials = 30)
    {
        var tree = new KdTree(pts, rng.Next());
        Assert.Equal(pts.Length, tree.Count);
        for (int t = 0; t < trials; t++)
        {
            var q = new Point3((float)(rng.NextDouble() * 220 - 110), (float)(rng.NextDouble() * 220 - 110), (float)(rng.NextDouble() * 220 - 110));
            if (rng.NextDouble() < 0.3 && pts.Length > 0) q = pts[rng.Next(pts.Length)];
            int k = rng.Next(1, 12);

            var want = pts.Select(p => p.DistanceSquaredTo(q)).OrderBy(d => d).Take(k).ToArray();
            var got = tree.Knn(q, k);
            Assert.Equal(want.Length, got.Count);
            for (int i = 0; i < want.Length; i++)
            {
                Assert.Equal(want[i], got[i].Distance * got[i].Distance, 6);
                // 返った添字が本当にその距離であること
                Assert.Equal(pts[got[i].Index].DistanceSquaredTo(q), got[i].Distance * got[i].Distance, 6);
            }
            Assert.Equal(got.Count, got.Select(g => g.Index).Distinct().Count());

            var nn = tree.Nearest(q);
            if (pts.Length == 0) Assert.Null(nn);
            else Assert.Equal(pts.Min(p => p.DistanceSquaredTo(q)), nn.Value.Distance * nn.Value.Distance, 6);

            double r = rng.NextDouble() * 60;
            Assert.Equal(
                Enumerable.Range(0, pts.Length).Where(i => pts[i].DistanceSquaredTo(q) <= r * r).OrderBy(i => i),
                tree.Radius(q, r).OrderBy(i => i));

            var a = new Point3((float)(rng.NextDouble() * 150 - 100), (float)(rng.NextDouble() * 150 - 100), (float)(rng.NextDouble() * 150 - 100));
            var b = new Point3(a.X + (float)(rng.NextDouble() * 80), a.Y + (float)(rng.NextDouble() * 80), a.Z + (float)(rng.NextDouble() * 80));
            Assert.Equal(
                Enumerable.Range(0, pts.Length).Where(i => a.X <= pts[i].X && pts[i].X <= b.X && a.Y <= pts[i].Y && pts[i].Y <= b.Y && a.Z <= pts[i].Z && pts[i].Z <= b.Z).OrderBy(i => i),
                tree.Box(a, b).OrderBy(i => i));
        }
    }

    [Fact] public void QuickSelect_MatchesSorted()
    {
        var rng = new Random(1);
        for (int it = 0; it < 300; it++)
        {
            int n = rng.Next(1, 40);
            var keys = Enumerable.Range(0, n).Select(_ => (float)rng.Next(4)).ToArray();
            var idx = Enumerable.Range(0, n).ToArray();
            int k = rng.Next(n);
            float want = keys.OrderBy(x => x).ElementAt(k);
            KdTree.QuickSelect(idx, i => keys[i], 0, n, k, rng);
            Assert.Equal(want, keys[idx[k]]);
            Assert.All(idx.Take(k), i => Assert.True(keys[i] <= want));
            Assert.All(idx.Skip(k + 1), i => Assert.True(keys[i] >= want));
            Assert.Equal(Enumerable.Range(0, n), idx.OrderBy(i => i)); // 順列のまま
        }
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public void QuickSelect_SortedAndAllEqual_NotQuadratic(bool sorted)
    {
        int n = 1_000_000;
        var keys = sorted ? Enumerable.Range(0, n).Select(i => (float)i).ToArray() : Enumerable.Repeat(1f, n).ToArray();
        var idx = Enumerable.Range(0, n).ToArray();
        var sw = Stopwatch.StartNew();
        KdTree.QuickSelect(idx, i => keys[i], 0, n, n / 2, new Random(0));
        Assert.True(sw.Elapsed.TotalSeconds < 3, $"{sw.Elapsed}"); // O(n^2) なら桁違いに遅い
    }

    [Fact] public void RandomSizes() { var r = new Random(42); foreach (var n in new[] { 0, 1, 2, 3, 4, 5, 7, 8, 100, 500 }) Check(Rand(r, n), r); }
    [Fact] public void ManyDuplicates() { var r = new Random(7); foreach (var n in new[] { 10, 100, 400 }) Check(Rand(r, n, 3), r); }
    [Fact] public void AllIdentical() => Check(Enumerable.Repeat(new Point3(5, 5, 5), 50).ToArray(), new Random(8));
    [Fact] public void SortedAndCollinear()
    {
        var r = new Random(9);
        Check(Enumerable.Range(0, 300).Select(i => new Point3(i, i, i)).ToArray(), r);
        Check(Enumerable.Range(0, 300).Select(i => new Point3(i, 0, 0)).ToArray(), r);
    }

    [Fact] public void EdgeArguments()
    {
        var tree = new KdTree(new[] { new Point3(0, 0, 0), new Point3(1, 1, 1) });
        Assert.Equal(2, tree.Knn(new Point3(0, 0, 0), 10).Count);
        Assert.Empty(tree.Knn(new Point3(0, 0, 0), 0));
        Assert.Empty(tree.Radius(new Point3(0, 0, 0), -1));
        Assert.Throws<ArgumentNullException>(() => new KdTree(null));
    }

    [Fact] public void InputNotMutated()
    {
        var pts = new[] { new Point3(3, 1, 2), new Point3(1, 2, 3), new Point3(2, 3, 1) };
        var copy = (Point3[])pts.Clone();
        var tree = new KdTree(pts);
        Assert.Equal(copy, pts);
        Assert.Equal(0, tree.Nearest(new Point3(3, 1, 2)).Value.Index); // 添字は入力の並びのまま
    }

    [Fact] public void BuildSortedLarge()
    {
        var pts = Enumerable.Range(0, 200_000).Select(i => new Point3(i, i, i)).ToArray();
        var sw = Stopwatch.StartNew();
        new KdTree(pts);
        Assert.True(sw.Elapsed.TotalSeconds < 10, $"{sw.Elapsed}");
    }

    [Fact] public void Box_BoundsExactlyOnGridValues()
    {
        // 境界が点の座標とぴったり一致する（重複値が分割面の両側に並ぶ）場合
        var rng = new Random(11);
        for (int it = 0; it < 200; it++)
        {
            var pts = Rand(rng, rng.Next(1, 200), 4);
            var tree = new KdTree(pts, it);
            var a = new Point3(rng.Next(4), rng.Next(4), rng.Next(4));
            var b = new Point3(a.X + rng.Next(3), a.Y + rng.Next(3), a.Z + rng.Next(3));
            var want = Enumerable.Range(0, pts.Length).Where(i => a.X <= pts[i].X && pts[i].X <= b.X && a.Y <= pts[i].Y && pts[i].Y <= b.Y && a.Z <= pts[i].Z && pts[i].Z <= b.Z).OrderBy(i => i);
            Assert.Equal(want, tree.Box(a, b).OrderBy(i => i));
        }
    }
}

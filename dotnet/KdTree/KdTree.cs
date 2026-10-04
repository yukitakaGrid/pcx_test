using System;
using System.Collections.Generic;

namespace KdTreeLib
{
    /// <summary>Unity 非依存の3次元の点。</summary>
    public readonly struct Point3
    {
        public readonly float X, Y, Z;
        public Point3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public float this[int axis] => axis == 0 ? X : axis == 1 ? Y : Z;

        public double DistanceSquaredTo(Point3 o)
        {
            double dx = (double)X - o.X, dy = (double)Y - o.Y, dz = (double)Z - o.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }

    /// <summary>探索結果。Index は KdTree に渡した配列の添字（色など別配列を引ける）。</summary>
    public readonly struct Neighbor
    {
        public readonly int Index;
        public readonly double Distance;
        public Neighbor(int index, double distance) { Index = index; Distance = distance; }
    }

    /// <summary>
    /// 3次元 kd-tree。並べ替えた添字配列そのものが木（暗黙の二分木）。
    /// 区間 [lo,hi) の根は mid=(lo+hi)/2、軸は depth%3。左 [lo,mid)・右 (mid,hi) は重ならない。
    /// 旧 NearestNeighborSearch.cs の Lomuto 分割（整列済みで O(n^2)）は、
    /// ランダムピボット + 3-way 分割の quickselect に置き換え。
    /// </summary>
    public sealed class KdTree
    {
        private const int Dim = 3;
        private readonly Point3[] _pts;   // 入力のコピー（元配列は壊さない）
        private readonly int[] _idx;      // 木の並び -> 元の添字
        private readonly Random _rng;

        public int Count => _idx.Length;

        public KdTree(IReadOnlyList<Point3> points, int seed = 0)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            int n = points.Count;
            _pts = new Point3[n];
            _idx = new int[n];
            for (int i = 0; i < n; i++) { _pts[i] = points[i]; _idx[i] = i; }
            _rng = new Random(seed);
            Build(0, n, 0);
        }

        private float Coord(int slot, int axis) => _pts[_idx[slot]][axis];

        /// <summary>_idx[lo..hi) を部分整列し、_idx[k] を軸 axis の k 番目に小さい要素にする。</summary>
        public static void QuickSelect(int[] idx, Func<int, float> key, int lo, int hi, int k, Random rng)
        {
            while (hi - lo > 1)
            {
                float p = key(idx[rng.Next(lo, hi)]);
                int lt = lo, i = lo, gt = hi;   // [lo,lt)<p, [lt,i)==p, [gt,hi)>p
                while (i < gt)
                {
                    float v = key(idx[i]);
                    if (v < p) { Swap(idx, lt, i); lt++; i++; }
                    else if (v > p) { gt--; Swap(idx, gt, i); }
                    else i++;
                }
                if (k < lt) hi = lt;
                else if (k >= gt) lo = gt;
                else return;
            }
        }

        private static void Swap(int[] a, int i, int j) { int t = a[i]; a[i] = a[j]; a[j] = t; }

        private void Build(int lo, int hi, int depth)
        {
            if (hi - lo <= 1) return;
            int axis = depth % Dim;
            int mid = (lo + hi) / 2;
            QuickSelect(_idx, id => _pts[id][axis], lo, hi, mid, _rng);
            Build(lo, mid, depth + 1);
            Build(mid + 1, hi, depth + 1);
        }

        // ---- k近傍 / 最近傍 ----

        /// <summary>q に近い順に最大 k 個。k&lt;=0 や空の木は空リスト。</summary>
        public List<Neighbor> Knn(Point3 q, int k)
        {
            var result = new List<Neighbor>();
            if (k <= 0 || Count == 0) return result;
            var heap = new List<(double d2, int idx)>(k + 1); // 最大ヒープ（先頭が最遠）
            Knn(0, Count, 0, q, k, heap);
            heap.Sort((a, b) => a.d2.CompareTo(b.d2));
            foreach (var h in heap) result.Add(new Neighbor(h.idx, Math.Sqrt(h.d2)));
            return result;
        }

        public Neighbor? Nearest(Point3 q)
        {
            var r = Knn(q, 1);
            return r.Count == 0 ? (Neighbor?)null : r[0];
        }

        private void Knn(int lo, int hi, int depth, Point3 q, int k, List<(double d2, int idx)> heap)
        {
            if (lo >= hi) return;
            int axis = depth % Dim;
            int mid = (lo + hi) / 2;
            int id = _idx[mid];
            double d2 = _pts[id].DistanceSquaredTo(q);
            if (heap.Count < k) HeapPush(heap, (d2, id));
            else if (d2 < heap[0].d2) { heap[0] = (d2, id); SiftDown(heap, 0); }

            double diff = (double)q[axis] - _pts[id][axis];
            bool nearLeft = diff < 0;
            if (nearLeft) Knn(lo, mid, depth + 1, q, k, heap); else Knn(mid + 1, hi, depth + 1, q, k, heap);
            if (heap.Count < k || diff * diff <= heap[0].d2)
            {
                if (nearLeft) Knn(mid + 1, hi, depth + 1, q, k, heap); else Knn(lo, mid, depth + 1, q, k, heap);
            }
        }

        private static void HeapPush(List<(double d2, int idx)> h, (double d2, int idx) v)
        {
            h.Add(v);
            int i = h.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (h[p].d2 >= h[i].d2) break;
                (h[p], h[i]) = (h[i], h[p]);
                i = p;
            }
        }

        private static void SiftDown(List<(double d2, int idx)> h, int i)
        {
            int n = h.Count;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < n && h[l].d2 > h[m].d2) m = l;
                if (r < n && h[r].d2 > h[m].d2) m = r;
                if (m == i) return;
                (h[m], h[i]) = (h[i], h[m]);
                i = m;
            }
        }

        // ---- 範囲探索 ----

        /// <summary>q からの距離が r 以下の点の元の添字（順不同）。</summary>
        public List<int> Radius(Point3 q, double r)
        {
            var o = new List<int>();
            if (r < 0) return o;
            Radius(0, Count, 0, q, r * r, o);
            return o;
        }

        private void Radius(int lo, int hi, int depth, Point3 q, double r2, List<int> o)
        {
            if (lo >= hi) return;
            int axis = depth % Dim;
            int mid = (lo + hi) / 2;
            int id = _idx[mid];
            if (_pts[id].DistanceSquaredTo(q) <= r2) o.Add(id);
            double diff = (double)q[axis] - _pts[id][axis];
            if (diff <= 0 || diff * diff <= r2) Radius(lo, mid, depth + 1, q, r2, o);
            if (diff >= 0 || diff * diff <= r2) Radius(mid + 1, hi, depth + 1, q, r2, o);
        }

        /// <summary>各軸で a &lt;= x &lt;= b の点の元の添字（順不同）。</summary>
        public List<int> Box(Point3 a, Point3 b)
        {
            var o = new List<int>();
            Box(0, Count, 0, a, b, o);
            return o;
        }

        private void Box(int lo, int hi, int depth, Point3 a, Point3 b, List<int> o)
        {
            if (lo >= hi) return;
            int axis = depth % Dim;
            int mid = (lo + hi) / 2;
            int id = _idx[mid];
            var p = _pts[id];
            if (a.X <= p.X && p.X <= b.X && a.Y <= p.Y && p.Y <= b.Y && a.Z <= p.Z && p.Z <= b.Z) o.Add(id);
            if (a[axis] <= p[axis]) Box(lo, mid, depth + 1, a, b, o);
            if (b[axis] >= p[axis]) Box(mid + 1, hi, depth + 1, a, b, o);
        }
    }
}

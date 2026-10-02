"""Unity から切り離した純粋な kd-tree（3次元）。

旧実装（Assets/Scripts/NearestNeighborSearch.cs）の弱点への対処:
- ピボットをランダム選択 + 3-way 分割 -> 整列済み・重複だらけの入力でも期待 O(n)
- 中央値で分割し、左 [lo,mid) / 中央 mid / 右 (mid,hi) と重ならない
  -> addList による重複回避が不要
- 再帰（深さ ~log2 n）で構築・探索。親ポインタもコルーチンも使わない
- 軸は depth % 3（負にならない）
- 最近傍・k近傍・範囲探索（球 / 直方体）を実装
"""
from __future__ import annotations

import heapq
import random
from typing import List, Optional, Sequence, Tuple

Point = Sequence[float]
DIM = 3


def quickselect(pts: list, lo: int, hi: int, k: int, axis: int,
                rng: random.Random) -> None:
    """pts[lo:hi] を部分的に並べ替え、pts[k] を軸 axis の k 番目に小さい要素にする。

    保証: 終了後 pts[lo:k] は pts[k] 以下、pts[k+1:hi] は pts[k] 以上。
    ランダムピボット + 3-way 分割なので、整列済み入力・全部同じ値でも
    期待 O(n)（旧: 末尾ピボットの Lomuto で整列済みだと O(n^2)）。
    """
    while hi - lo > 1:
        p = pts[rng.randrange(lo, hi)][axis]
        # Dutch national flag: [lo,lt) < p, [lt,i) == p, [gt,hi) > p
        lt, i, gt = lo, lo, hi
        while i < gt:
            v = pts[i][axis]
            if v < p:
                pts[lt], pts[i] = pts[i], pts[lt]
                lt += 1
                i += 1
            elif v > p:
                gt -= 1
                pts[gt], pts[i] = pts[i], pts[gt]
            else:
                i += 1
        if k < lt:
            hi = lt
        elif k >= gt:
            lo = gt
        else:
            return  # k は p と等しい帯の中


def _d2(a: Point, b: Point) -> float:
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2


class KdTree:
    """pts を並べ替えた配列そのものが木（暗黙の二分木）。
    区間 [lo,hi) の根は mid=(lo+hi)//2、軸は depth%3。"""

    def __init__(self, points: Sequence[Point], seed: Optional[int] = 0):
        for p in points:
            if len(p) != DIM:
                raise ValueError("各点は3次元")
        self._rng = random.Random(seed)
        self._pts: List[Point] = [tuple(p) for p in points]  # 入力は壊さない
        self._build(0, len(self._pts), 0)

    def __len__(self) -> int:
        return len(self._pts)

    def _build(self, lo: int, hi: int, depth: int) -> None:
        if hi - lo <= 1:
            return
        axis = depth % DIM
        mid = (lo + hi) // 2
        quickselect(self._pts, lo, hi, mid, axis, self._rng)
        self._build(lo, mid, depth + 1)
        self._build(mid + 1, hi, depth + 1)

    # ---- k近傍 / 最近傍 ----
    def knn(self, q: Point, k: int) -> List[Tuple[float, Point]]:
        """q に近い順に最大 k 個の (距離, 点)。k<=0 や空の木は []。"""
        if k <= 0 or not self._pts:
            return []
        heap: list = []  # 最大ヒープ: (-d2, tiebreak, point)
        self._knn(0, len(self._pts), 0, q, k, heap)
        out = sorted((-nd, p) for nd, _, p in heap)
        return [(d ** 0.5, p) for d, p in out]

    def _knn(self, lo, hi, depth, q, k, heap) -> None:
        if lo >= hi:
            return
        axis = depth % DIM
        mid = (lo + hi) // 2
        p = self._pts[mid]
        d2 = _d2(p, q)
        item = (-d2, -mid, p)
        if len(heap) < k:
            heapq.heappush(heap, item)
        elif item > heap[0]:
            heapq.heapreplace(heap, item)
        diff = q[axis] - p[axis]
        near, far = ((lo, mid), (mid + 1, hi)) if diff < 0 else ((mid + 1, hi), (lo, mid))
        self._knn(near[0], near[1], depth + 1, q, k, heap)
        # 分割面までの距離^2 が現在の k 番目より小さい（同じ含む）なら反対側も見る
        if len(heap) < k or diff * diff <= -heap[0][0]:
            self._knn(far[0], far[1], depth + 1, q, k, heap)

    def nearest(self, q: Point) -> Optional[Tuple[float, Point]]:
        r = self.knn(q, 1)
        return r[0] if r else None

    # ---- 範囲探索 ----
    def radius(self, q: Point, r: float) -> List[Point]:
        """q からの距離が r 以下の点すべて（順不同）。"""
        out: List[Point] = []
        if r < 0:
            return out
        self._radius(0, len(self._pts), 0, q, r * r, out)
        return out

    def _radius(self, lo, hi, depth, q, r2, out) -> None:
        if lo >= hi:
            return
        axis = depth % DIM
        mid = (lo + hi) // 2
        p = self._pts[mid]
        if _d2(p, q) <= r2:
            out.append(p)
        diff = q[axis] - p[axis]
        if diff <= 0 or diff * diff <= r2:
            self._radius(lo, mid, depth + 1, q, r2, out)
        if diff >= 0 or diff * diff <= r2:
            self._radius(mid + 1, hi, depth + 1, q, r2, out)

    def box(self, lo_corner: Point, hi_corner: Point) -> List[Point]:
        """各軸で lo_corner <= x <= hi_corner の点すべて（順不同）。"""
        out: List[Point] = []
        self._box(0, len(self._pts), 0, lo_corner, hi_corner, out)
        return out

    def _box(self, lo, hi, depth, a, b, out) -> None:
        if lo >= hi:
            return
        axis = depth % DIM
        mid = (lo + hi) // 2
        p = self._pts[mid]
        if all(a[i] <= p[i] <= b[i] for i in range(DIM)):
            out.append(p)
        # 左は <= p[axis]、右は >= p[axis]（重複値は両側にありうる）
        if a[axis] <= p[axis]:
            self._box(lo, mid, depth + 1, a, b, out)
        if b[axis] >= p[axis]:
            self._box(mid + 1, hi, depth + 1, a, b, out)

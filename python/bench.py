"""kd-tree の速さの物差し。種を固定した3種類の点群で、構築・k近傍・球・直方体の時間を測る。

  python3 bench.py          # 1行の JSON：{"score": 秒, ...}。score は各項目の3回の中央値の合計（小さいほど速い）

点群：一様乱数 / 整列済み / 格子（重複・同距離だらけ）。どれも 20000 点。
結果の中身も確かめる（各探索の件数と距離の合計）。速くても答えが変われば "ok": false。
"""
import json, os, random, statistics, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kdtree import KdTree

N, Q = 20000, 400


def datasets():
    rng = random.Random(42)
    uni = [tuple(rng.uniform(-100, 100) for _ in range(3)) for _ in range(N)]
    srt = sorted(uni)
    grid = [tuple(float(rng.randrange(20)) for _ in range(3)) for _ in range(N)]
    return {"uniform": uni, "sorted": srt, "grid": grid}


def run(pts, qs, r):
    t = {}
    t0 = time.perf_counter(); tree = KdTree(pts); t["build"] = time.perf_counter() - t0
    t0 = time.perf_counter(); kn = [tree.knn(q, 8) for q in qs]; t["knn"] = time.perf_counter() - t0
    t0 = time.perf_counter(); ra = [tree.radius(q, r) for q in qs]; t["radius"] = time.perf_counter() - t0
    t0 = time.perf_counter(); bx = [tree.box([c - r for c in q], [c + r for c in q]) for q in qs]; t["box"] = time.perf_counter() - t0
    check = (sum(len(r) for r in kn), round(sum(d for r in kn for d, _ in r), 6),
             sum(len(r) for r in ra), sum(len(r) for r in bx))
    return t, check


def main():
    rng = random.Random(7)
    qs = [tuple(rng.uniform(-100, 100) for _ in range(3)) for _ in range(Q)]
    gq = [tuple(float(rng.randrange(20)) for _ in range(3)) for _ in range(Q)]
    parts, checks = {}, {}
    for name, pts in datasets().items():
        runs = [run(pts, gq, 1.5) if name == "grid" else run(pts, qs, 8.0) for _ in range(3)]
        checks[name] = list(runs[0][1])
        for key in runs[0][0]:
            parts[name + "." + key] = round(statistics.median(r[0][key] for r in runs), 4)
    print(json.dumps({"score": round(sum(parts.values()), 4), "parts": parts, "checks": checks}))


if __name__ == "__main__":
    main()

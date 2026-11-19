#!/usr/bin/env python3
"""camprobe の結果 JSON から、種類ごとの「止めた窓 − 前後の基準の窓の平均」を出す。複数ファイルを渡すと列を並べて比べる。
usage: analyze.py <label>=<file.json> [...] [--metric Inl_UniversalRenderTotal]"""
import json, sys, statistics as st

args = [a for i, a in enumerate(sys.argv[1:], 1) if not a.startswith("--") and sys.argv[i - 1] != "--metric"]
metric = "Inl_UniversalRenderTotal"
if "--metric" in sys.argv:
    metric = sys.argv[sys.argv.index("--metric") + 1]

def diffs(path, m):
    d = json.load(open(path))
    if d.get("error"):
        print(f"# {path}: error {d['error'][:300]}")
    w = d["windows"]
    out, base = {}, []
    for i, x in enumerate(w):
        if x["condition"] == "A":
            base.append(x[m])
            continue
        prev = next((w[j] for j in range(i - 1, -1, -1) if w[j]["condition"] == "A"), None)
        nxt = next((w[j] for j in range(i + 1, len(w)) if w[j]["condition"] == "A"), None)
        if not prev or not nxt:
            continue
        out.setdefault(x["condition"], []).append(x[m] - (prev[m] + nxt[m]) / 2)
    stats = {s["category"]: s for s in d["stats"]}
    return out, base, stats

cols = []
for a in args:
    label, path = a.split("=", 1)
    cols.append((label, *diffs(path, metric)))

conds = list(cols[0][1].keys())
conds.sort(key=lambda c: st.mean(cols[0][1][c]))
print(f"metric={metric}")
for label, _, base, _ in cols:
    sd = st.pstdev([base[i] - (base[i - 1] + base[i + 1]) / 2 for i in range(1, len(base) - 1)]) / 1.5 ** 0.5 if len(base) > 2 else 0
    print(f"  {label}: baseline mean={st.mean(base):.3f}ms  window noise sd≈{sd:.3f}ms  n_base={len(base)}")
head = f"{'condition':32} {'visible':>8} {'tris(k)':>8}"
for label, *_ in cols:
    head += f" | {label+' diff':>12} {'(min..max)':>16} {'%ALL':>6}"
print(head)
for c in conds:
    s = cols[0][3].get(c, {})
    line = f"{c[:32]:32} {s.get('visible', ''):>8} {round(s.get('visibleTriangles', 0) / 1000) if s else '':>8}"
    for label, dd, _, _ in cols:
        v = dd.get(c, [])
        allm = st.mean(dd["ALL"]) if "ALL" in dd else float("nan")
        if v:
            line += f" | {st.mean(v):12.3f} {f'{min(v):.2f}..{max(v):.2f}':>16} {100 * st.mean(v) / allm:6.1f}"
        else:
            line += f" | {'—':>12} {'':>16} {'':>6}"
    print(line)

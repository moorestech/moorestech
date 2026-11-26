#!/usr/bin/env python3
"""run-mac.sh の結果 JSON を周の平均にまとめ、メインスレッドの階層を Markdown の表で出す
usage: summarize.py results/<file>.json [--depth N] [--min MS]
"""
import argparse, json, statistics
from collections import defaultdict

ap = argparse.ArgumentParser()
ap.add_argument("file")
ap.add_argument("--depth", type=int, default=4, help="表に出す経路の深さ（ルート=1）")
ap.add_argument("--min", type=float, default=0.3, help="表に出す total の下限 ms")
a = ap.parse_args()
d = json.load(open(a.file))

def avg(xs):
    return statistics.mean(xs) if xs else 0.0

print(f"label={d['label']} commit={d['commit'][:10]} world={d['worldSaveSha256Prefix']}")
print(f"env={json.dumps(d['environment'], ensure_ascii=False)}")
print(f"spot={d['playerSpot']}")
print(f"fps={avg([f['fps'] for f in d['fps']]):.2f} ({', '.join('%.2f' % f['fps'] for f in d['fps'])})")
keys = ["ft.cpuFrame", "ft.cpuMainThread", "ft.cpuRenderThread", "ft.gpuFrame", "Main Thread", "RenderPlayModeViewCameras", "Inl_UniversalRenderTotal", "Draw Calls Count", "SetPass Calls Count"]
for k in keys:
    print(f"  {k}: {avg([r[k]['avg'] for r in d['render'] if k in r]):.2f}")

# 経路ごとに周の平均（無い周は0）
n = len(d["maintree"])
tot, slf, cal = defaultdict(float), defaultdict(float), defaultdict(float)
for t in d["maintree"]:
    for m in t["main"]:
        tot[m["path"]] += m["totalMs"] / n
        slf[m["path"]] += m["selfMs"] / n
        cal[m["path"]] += m["calls"] / n
print(f"\nframes per window: {[t['frames'] for t in d['maintree']]}")
print("\n| 経路 | total ms | self ms | calls |\n|---|---:|---:|---:|")
for p in sorted(tot, key=lambda p: p):
    depth = p.count(" > ") + 1
    if depth > a.depth or tot[p] < a.min:
        continue
    name = p.split(" > ")[-1]
    print(f"| {'　' * (depth - 1)}{name} | {tot[p]:.2f} | {slf[p]:.2f} | {cal[p]:.0f} |")

st = defaultdict(float)
for t in d["maintree"]:
    for k, v in t["selfTop"].items():
        st[k] += v / n
print("\n| self 上位（名前ごと） | ms |\n|---|---:|")
for k, v in sorted(st.items(), key=lambda kv: -kv[1])[:30]:
    print(f"| {k} | {v:.2f} |")

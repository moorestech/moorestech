#!/usr/bin/env python3
"""run.sh の結果 JSON を、条件ごとの平均（全周）と、fps・Profiler の主要マーカーにまとめる。usage: summarize.py <result.json> [...]"""
import json, sys
from collections import defaultdict
KEYS = ["Inl_UniversalRenderTotal", "RenderPlayModeViewCameras", "Main Thread", "Render Thread", "Draw Calls Count", "Batches Count", "SetPass Calls Count", "Shadow Casters Count", "Triangles Count"]
MARKERS = ["PlayerLoop", "RenderPlayModeViewCameras", "WaitForJobGroupID", "EditorLoop", "Gfx.WaitForPresentOnGfxThread", "PostLateUpdate.UpdateAllRenderers", "MeshSkinning.Update"]
for path in sys.argv[1:]:
    d = json.load(open(path))
    print(f"== {d['label']} {d['commit'][:10]}  scene={d['scene']}")
    fps = [f["fps"] for f in d["fps"]]
    print("fps(Profilerなし): " + " / ".join(f"{x:.2f}" for x in fps))
    for m in MARKERS:
        vals = [p["markers"].get(m, {}).get("totalMs") for p in d["profiler"]]
        if any(v is not None for v in vals): print(f"  {m}: " + " / ".join("—" if v is None else f"{v:.2f}" for v in vals) + " ms")
    agg = defaultdict(lambda: defaultdict(list))
    for w in d["windows"]:
        for k in KEYS:
            if k in w["render"] and w["render"][k]["frames"] > 0: agg[w["cond"]][k].append(w["render"][k]["avg"])
    print("cond     | " + " | ".join(k.replace(" Count", "") for k in KEYS))
    for c, kv in agg.items():
        print(f"{c:8} | " + " | ".join((f"{sum(kv[k])/len(kv[k]):.1f}" if kv[k] else "—") for k in KEYS))

#!/usr/bin/env python3
"""操作 B — research.json の graphViewSettings.UIPosition を再計算して書き戻す。

依存関係グラフ (prevResearchNodeGuids) と nodeGraph.v1.json のビジュアル配置
(masterGuid 照合) から各研究ノードの座標を決める。アルゴリズムは SKILL.md の
操作 B (Step B-1〜B-5) に従う。

  - depth: prevResearchNodeGuids を辿った最長パス
  - Y: nodeGraph の y でグループ判定 (upper/main/lower) → main=0, upper=200, lower=-200
  - X: next_x 方式。各 depth を配置後 next_x = その depth の最大 x + X_SPACING
  - 同一 depth・同一グループの複数ノードは nodeGraph x 昇順に base_x から X_SPACING 間隔
  - 孤立ノード (依存なし・被依存なし) は x=0, y=400,800,... にオフセット
"""
import argparse, json, sys
from collections import defaultdict
from pathlib import Path

X_SPACING = 500
GROUP_Y = {"main": 0, "upper": 200, "lower": -200}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--mod-dir", required=True, help="mod ルート (master/ と .mooreseditor/ を含む) の絶対パス")
    ap.add_argument("--quiet", action="store_true", help="最終配置一覧の出力を省略")
    args = ap.parse_args()

    mod = Path(args.mod_dir)
    research_path = mod / "master" / "research.json"
    graph_path = mod / ".mooreseditor" / "nodeGraph.v1.json"

    research_raw = research_path.read_text(encoding="utf-8")
    data = json.loads(research_raw)
    nodes = {n["researchNodeGuid"]: n for n in data["data"]}

    graph = json.loads(graph_path.read_text(encoding="utf-8"))
    gpos = {
        n["masterGuid"]: (n["position"]["x"], n["position"]["y"])
        for n in graph["nodes"]
        if n.get("type") == "research" and "masterGuid" in n
    }

    # 依存チェーンの最長パス (depth) を計算
    # longest dependency-chain depth per node
    memo = {}

    def depth(g):
        if g in memo:
            return memo[g]
        memo[g] = -1  # cycle guard
        prev = [p for p in nodes[g]["prevResearchNodeGuids"] if p in nodes]
        memo[g] = 0 if not prev else max(depth(p) for p in prev) + 1
        return memo[g]

    for g in nodes:
        depth(g)

    # 被依存集合を作り、孤立ノード (依存なし・被依存なし) を判定
    # nodes that are referenced as a prerequisite by someone else
    has_dependent = set()
    for g in nodes:
        for p in nodes[g]["prevResearchNodeGuids"]:
            if p in nodes:
                has_dependent.add(p)
    orphans = [g for g in nodes if not nodes[g]["prevResearchNodeGuids"] and g not in has_dependent]
    orphan_set = set(orphans)

    def group(g):
        # nodeGraph に無いノードは孤立扱い (呼び出し側で除外済み)
        x, y = gpos[g]
        return "upper" if y < -100 else ("lower" if y > 150 else "main")

    # depth ごとにレイアウト対象 (孤立以外) を集約
    layout_nodes = [g for g in nodes if g not in orphan_set]
    by_depth = defaultdict(list)
    for g in layout_nodes:
        by_depth[memo[g]].append(g)

    positions = {}
    next_x = 0
    for d in sorted(by_depth):
        base_x = next_x
        max_x = base_x
        by_group = defaultdict(list)
        for g in by_depth[d]:
            by_group[group(g)].append(g)
        for grp, gs in by_group.items():
            gs.sort(key=lambda g: gpos[g][0])  # nodeGraph x 昇順 = 左→右
            y = GROUP_Y[grp]
            for n, g in enumerate(gs):
                x = base_x + n * X_SPACING
                positions[g] = [x, y]
                max_x = max(max_x, x)
        next_x = max_x + X_SPACING

    # 孤立ノードを下方へオフセット配置
    for i, g in enumerate(orphans):
        positions[g] = [0, 400 + i * 400]

    # 検証: 全ノードに位置があるか / 重複が無いか
    assert len(positions) == len(nodes), f"unassigned nodes: {len(nodes) - len(positions)}"
    seen = {}
    for g, p in positions.items():
        key = tuple(p)
        if key in seen:
            print(f"DUPLICATE position {p}: {nodes[g]['researchNodeName']} & {nodes[seen[key]]['researchNodeName']}", file=sys.stderr)
            sys.exit(1)
        seen[key] = g

    # research.json へ書き戻し (UIPosition のみ更新、他は保持)
    for g, node in nodes.items():
        node["graphViewSettings"]["UIPosition"] = positions[g]

    out = json.dumps(data, ensure_ascii=False, indent=2)
    research_path.write_text(out, encoding="utf-8")

    if not args.quiet:
        for g in sorted(nodes, key=lambda g: (memo[g], positions[g][0], -positions[g][1])):
            tag = "ORPHAN" if g in orphan_set else group(g) if g in gpos else "?"
            print(f"  d{memo[g]:>2} {str(positions[g]):>14} [{tag:>6}] {nodes[g]['researchNodeName']}")
    print(f"\nupdated {len(nodes)} nodes -> {research_path}")


if __name__ == "__main__":
    main()

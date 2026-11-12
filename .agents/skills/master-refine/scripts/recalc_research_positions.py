#!/usr/bin/env python3
"""操作 B — research.json の graphViewSettings.UIPosition を再計算して書き戻す。

依存関係グラフ (prevResearchNodeGuids) と nodeGraph.v1.json のビジュアル配置
(masterGuid 照合) から各研究ノードの座標を決める。アルゴリズムの正本はこの docstring
とコード (SKILL.md は要点のみ)。

  - depth: prevResearchNodeGuids を辿った最長パス
  - Y: nodeGraph の y でグループ判定 (upper/main/lower) → main=0, upper=200, lower=-200
  - X: next_x 方式。各 depth を配置後 next_x = その depth の最大 x + X_SPACING
  - 同一 depth・同一グループの複数ノードは nodeGraph x 昇順に base_x から X_SPACING 間隔
  - 孤立ノード (依存なし・被依存なし) は x=0, y=400,800,... にオフセット
  - 本線 = --goal-research の研究とその祖先。上の depth/グループ/next_x で置く
  - 枝 (本線に無い研究) は本線の下 (y=-600 から 200 刻みで下へ) にまとめる。列・行は
    nodeGraph の x・y をそれぞれ閾値 100 でまとめた順番。列の起点は枝が依存する本線の
    研究のうち最も深いものの x (そこから SIDE_X_SPACING 刻みで右へ)
"""
import argparse, json, sys
from collections import defaultdict
from pathlib import Path

X_SPACING = 170
GROUP_Y = {"main": 0, "upper": 200, "lower": -200}
SIDE_X_SPACING = 255
SIDE_TOP_Y = -600
SIDE_ROW_SPACING = 200


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--mod-dir", required=True, help="mod ルート (master/ と .mooreseditor/ を含む) の絶対パス")
    ap.add_argument("--quiet", action="store_true", help="最終配置一覧の出力を省略")
    ap.add_argument("--goal-research", required=True, help="本線の終点の研究 GUID。ここへ至らない研究は枝として本線の下に置く")
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
        # nodeGraph 未配置のチェーンノードは main 段に既定配置
        # chain node not yet placed in nodeGraph defaults to the main row
        if g not in gpos:
            return "main"
        x, y = gpos[g]
        return "upper" if y < -100 else ("lower" if y > 150 else "main")

    def gx(g):
        # nodeGraph 未配置ノードは深さ内で末尾に寄せる
        # un-placed nodes sort last within their depth
        return gpos[g][0] if g in gpos else float("inf")

    # 本線＝終点の研究とその祖先。それ以外は枝として本線の下にまとめる
    # Main line = goal research and its ancestors; everything else is a side branch placed below it
    assert args.goal_research in nodes, f"goal research not found: {args.goal_research}"
    main_set = set()
    stack = [args.goal_research]
    while stack:
        g = stack.pop()
        if g in main_set or g not in nodes:
            continue
        main_set.add(g)
        stack.extend(nodes[g]["prevResearchNodeGuids"])
    side_nodes = [g for g in nodes if g not in orphan_set and g not in main_set]

    # depth ごとにレイアウト対象 (孤立・枝以外) を集約
    layout_nodes = [g for g in nodes if g not in orphan_set and g in main_set]
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
            gs.sort(key=gx)  # nodeGraph x 昇順 = 左→右 (未配置は末尾)
            y = GROUP_Y[grp]
            for n, g in enumerate(gs):
                x = base_x + n * X_SPACING
                positions[g] = [x, y]
                max_x = max(max_x, x)
        next_x = max_x + X_SPACING

    # 枝: 列・行とも nodeGraph の並びを写し、本線の下へ置く
    # Side branch: copy the nodeGraph column/row order and place it below the main line
    if side_nodes:
        anchors = [p for g in side_nodes for p in nodes[g]["prevResearchNodeGuids"] if p in main_set]
        anchor = max(anchors, key=lambda p: (memo[p], positions[p][0]))
        anchor_x, anchor_d = positions[anchor][0], memo[anchor]
        # nodeGraph の座標を閾値でまとめ、列・行の番号にする
        # Cluster nodeGraph coordinates into column / row indices
        def cluster_index(values):
            index, last, current = {}, None, -1
            for v in sorted(set(values)):
                if last is None or v - last > 100:
                    current += 1
                last = v
                index[v] = current
            return index
        missing = [nodes[g]["researchNodeName"] for g in side_nodes if g not in gpos]
        assert not missing, f"side-branch research not placed in nodeGraph: {missing}"
        col_index = cluster_index([gpos[g][0] for g in side_nodes])
        row_index = cluster_index([gpos[g][1] for g in side_nodes])
        for g in side_nodes:
            col = col_index[gpos[g][0]]
            row = row_index[gpos[g][1]]
            positions[g] = [anchor_x + (col + 1) * SIDE_X_SPACING, SIDE_TOP_Y - row * SIDE_ROW_SPACING]

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
            tag = "ORPHAN" if g in orphan_set else "SIDE" if g not in main_set else group(g) if g in gpos else "?"
            print(f"  d{memo[g]:>2} {str(positions[g]):>14} [{tag:>6}] {nodes[g]['researchNodeName']}")
    print(f"\nupdated {len(nodes)} nodes -> {research_path}")


if __name__ == "__main__":
    main()

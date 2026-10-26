#!/usr/bin/env python3
"""操作 A の自己テスト用の最小 mod。ブロックは itemGuid を持たず、unlockBlock と nodeGraph だけで並ぶ。
研究の行は y 差がちょうど 100 で同じ行・101 で別の行、研究内の列は x 差がちょうど 100 で同じ列・101 で別の列になる配置にしてある。

Minimal mod for the operation A self-tests. Blocks carry no itemGuid and are ordered only by unlockBlock and the nodeGraph.
Research rows join at a y gap of exactly 100 and split at 101; columns inside a research join at an x gap of 100 and split at 101.
"""
import json
from pathlib import Path


def research(name, prev, x, y, blocks, items):
    actions = [{"gameActionType": "unlockBlock", "gameActionParam": {"unlockBlockGuids": blocks}},
               {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": items}}]
    row = {"researchNodeGuid": name, "researchNodeName": name, "prevResearchNodeGuids": prev, "clearedActions": actions}
    return row, {"type": "research", "masterGuid": name, "position": {"x": x, "y": y}}


def block(name, sort, initial):
    return {"blockGuid": name, "name": name, "blockParam": {"idlePowerRate": 0.2}, "initialUnlocked": initial, "sortPriority": sort}


def item(name, sort, initial):
    return {"itemGuid": name, "name": name, "sortPriority": sort, "initialUnlocked": initial}


def build_mod():
    # 深さ1の研究: R1b(100,100) と R1a(200,0) は y 差100で同じ行 → x 昇順。R1c(0,201) は次の行
    # Depth-1 research: R1b and R1a share a row at a y gap of 100 (x ascending); R1c at y=201 is the next row
    rows = [research("R0", [], 0, 0, ["B_col1_low", "B_col1_up", "B_col2", "B_nopos"], ["I_r0"]),
            research("R1a", ["R0"], 200, 0, ["B_r1a"], []),
            research("R1b", ["R0"], 100, 100, ["B_r1b"], ["I_r1b"]),
            research("R1c", ["R0"], 0, 201, ["B_multi"], []),
            research("R2", ["R1a"], 300, 0, ["B_multi", "B_r2"], [])]
    # R0 内: B_col1_up(100,10) と B_col1_low(0,50) は x 差100で同じ列 → y 昇順。B_col2(201,0) は次の列、B_nopos は位置なしで末尾
    # In R0: B_col1_up and B_col1_low share a column at an x gap of 100 (y ascending); B_col2 is the next column; B_nopos goes last
    entity_nodes = [("block", "B_col1_low", 0, 50), ("block", "B_col1_up", 100, 10), ("block", "B_col2", 201, 0),
                    ("block", "B_r1a", 200, 50), ("block", "B_r1b", 100, 150), ("block", "B_multi", 0, 250),
                    ("block", "B_r2", 300, 50), ("item", "I_r0", 0, 60), ("item", "I_r1b", 100, 160)]
    # 孤立は旧 sortPriority の相対順（0 同士は配列順）で末尾。初期解放は旧 sortPriority 順で先頭
    # Orphans go last in their old sortPriority order (ties at 0 keep array order); initial unlocks lead in old order
    blocks = [block("B_orphan_late", 50, False), block("B_init_late", 999, True), block("B_r2", 1, False),
              block("B_orphan_zero_a", 0, False), block("B_multi", 2, False), block("B_col2", 3, False),
              block("B_r1b", 4, False), block("B_col1_low", 5, False), block("B_init_early", 7, True),
              block("B_orphan_zero_b", 0, False), block("B_r1a", 8, False), block("B_col1_up", 9, False), block("B_nopos", 6, False)]
    items = [item("I_orphan", 5, False), item("I_r1b", 1, False), item("I_init", 300, True), item("I_r0", 2, False)]
    graph = {"nodes": [node for _, node in rows] + [{"type": t, "masterGuid": g, "position": {"x": x, "y": y}} for t, g, x, y in entity_nodes]
             + [{"type": "note", "position": {"x": 0, "y": 0}}]}
    return {"master/items.json": {"data": items}, "master/blocks.json": {"data": blocks, "connectorSettings": [{"ratio": 1.5}]},
            "master/research.json": {"data": [row for row, _ in rows]}, ".mooreseditor/nodeGraph.v1.json": graph}


# 期待する最終順（100 始まり 10 刻み）
# Expected final order (from 100 in steps of 10)
EXPECTED_BLOCKS = ["B_init_early", "B_init_late", "B_col1_up", "B_col1_low", "B_col2", "B_nopos", "B_r1b", "B_r1a",
                   "B_multi", "B_r2", "B_orphan_zero_a", "B_orphan_zero_b", "B_orphan_late"]
EXPECTED_ITEMS = ["I_init", "I_r0", "I_r1b", "I_orphan"]


def write_mod(directory, mod, trailing_newline):
    for relative, doc in mod.items():
        path = Path(directory) / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + ("\n" if trailing_newline else ""), encoding="utf-8")

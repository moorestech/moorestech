#!/usr/bin/env python3
"""
moorestech mod の sortPriority を、research 解放順序 + nodeGraph 上の
アイテム配置から再計算し、items.json/blocks.json をその順に並べ替える。

優先順位ルール:
1. initialUnlocked アイテム (現状 sortPriority 順を維持) を先頭固定
2. research 解放対象アイテム
   - research の解放 depth (prev チェーン最長) が最優先
   - 同 depth 内は nodeGraph の y で行クラスタリング → 上の行が先、行内は x 昇順
   - 各 research 内では nodeGraph の x で列クラスタリング → 左の列が先、列内は y 昇順
3. 末尾: research 解放対象でも initialUnlocked でもない孤立アイテム
   (現状 sortPriority の相対順序を維持)

最後に items.json は sortPriority 昇順、blocks.json は紐づく item の
sortPriority 昇順で並べ替えて書き戻す。
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

# クラスタ閾値 (nodeGraph 座標)
X_COLUMN_THRESHOLD = 100   # research 内アイテムの列クラスタリング
Y_ROW_THRESHOLD = 100      # 同 depth research の行クラスタリング

SORT_STEP = 10
SORT_START = 100


def cluster_sorted(values_with_keys, threshold):
    """ソート済みの (key, item) を、key 同士の差が threshold 以下のクラスタに分割。"""
    clusters = []
    cur = []
    last = None
    for key, item in values_with_keys:
        if last is None or key - last <= threshold:
            cur.append((key, item))
        else:
            clusters.append(cur)
            cur = [(key, item)]
        last = key
    if cur:
        clusters.append(cur)
    return clusters


def recalc(mod_dir: Path) -> dict:
    items_path = mod_dir / "master" / "items.json"
    blocks_path = mod_dir / "master" / "blocks.json"
    research_path = mod_dir / "master" / "research.json"
    ng_path = mod_dir / ".mooreseditor" / "nodeGraph.v1.json"

    items_doc = json.loads(items_path.read_text(encoding="utf-8"))
    blocks_doc = json.loads(blocks_path.read_text(encoding="utf-8"))
    research_doc = json.loads(research_path.read_text(encoding="utf-8"))
    ng = json.loads(ng_path.read_text(encoding="utf-8"))

    items = items_doc["data"]
    blocks = blocks_doc["data"]
    researches = research_doc["data"]

    # nodeGraph 座標 (masterGuid で照合。note 等 masterGuid 無しのノードはスキップ)
    ng_pos = {
        n["masterGuid"]: (n["position"]["x"], n["position"]["y"])
        for n in ng["nodes"]
        if "masterGuid" in n
    }

    research_by_guid = {r["researchNodeGuid"]: r for r in researches}

    def unlock_items(r):
        result = []
        for action in r.get("clearedActions", []):
            if action.get("gameActionType") == "unlockItemRecipeView":
                for g in action.get("gameActionParam", {}).get("unlockItemGuids", []):
                    result.append(g)
        return result

    unlocked_in_research = set()
    for r in researches:
        for g in unlock_items(r):
            unlocked_in_research.add(g)

    # research depth (最長依存パス)
    depth_memo: dict[str, int] = {}

    def depth(g):
        if g in depth_memo:
            return depth_memo[g]
        r = research_by_guid[g]
        prevs = r["prevResearchNodeGuids"]
        d = 0 if not prevs else max(depth(p) for p in prevs) + 1
        depth_memo[g] = d
        return d

    # ----- research の順序づけ -----
    by_depth: dict[int, list] = {}
    for r in researches:
        g = r["researchNodeGuid"]
        d = depth(g)
        pos = ng_pos.get(g, (0, 0))
        by_depth.setdefault(d, []).append((pos, r))

    ordered_research = []
    for d in sorted(by_depth):
        bucket = by_depth[d]
        bucket.sort(key=lambda t: (t[0][1], t[0][0]))  # y 昇順で行クラスタ前提
        y_keyed = [(pos[1], (pos, r)) for pos, r in bucket]
        rows = cluster_sorted(y_keyed, Y_ROW_THRESHOLD)
        for row in rows:
            row_items = [item for _, item in row]
            row_items.sort(key=lambda t: t[0][0])  # 行内は x 昇順
            for _pos, r in row_items:
                ordered_research.append(r)

    # ----- 各 research のアイテム順 -----
    def order_items_in_research(r):
        guids = unlock_items(r)
        if not guids:
            return []
        with_pos = []
        without_pos = []
        for g in guids:
            if g in ng_pos:
                with_pos.append((ng_pos[g], g))
            else:
                without_pos.append(g)
        with_pos.sort(key=lambda t: (t[0][0], t[0][1]))
        x_keyed = [(pos[0], (pos, g)) for pos, g in with_pos]
        cols = cluster_sorted(x_keyed, X_COLUMN_THRESHOLD)
        result = []
        for col in cols:
            col_items = [it for _, it in col]
            col_items.sort(key=lambda t: t[0][1])  # 列内は y 昇順 (上→下)
            for _pos, g in col_items:
                result.append(g)
        result.extend(without_pos)
        return result

    # ----- 全アイテムの最終順序 -----
    items_by_guid = {it["itemGuid"]: it for it in items}
    final_order = []
    placed = set()

    initial_items = sorted(
        [it for it in items if it.get("initialUnlocked")],
        key=lambda it: it["sortPriority"],
    )
    for it in initial_items:
        final_order.append(it["itemGuid"])
        placed.add(it["itemGuid"])

    for r in ordered_research:
        for g in order_items_in_research(r):
            if g in placed or g not in items_by_guid:
                continue
            final_order.append(g)
            placed.add(g)

    orphan_items = sorted(
        [it for it in items if it["itemGuid"] not in placed],
        key=lambda it: it["sortPriority"],
    )
    for it in orphan_items:
        final_order.append(it["itemGuid"])
        placed.add(it["itemGuid"])

    assert len(final_order) == len(items), (
        f"final_order={len(final_order)} != items={len(items)}"
    )
    assert len(set(final_order)) == len(final_order), "duplicates in final_order"

    # ----- sortPriority 割り当て -----
    new_priority = {g: SORT_START + i * SORT_STEP for i, g in enumerate(final_order)}

    for it in items:
        it["sortPriority"] = new_priority[it["itemGuid"]]

    for b in blocks:
        ig = b.get("itemGuid")
        if "sortPriority" in b and ig in new_priority:
            b["sortPriority"] = new_priority[ig]

    # ----- items / blocks の並べ替え -----
    items.sort(key=lambda it: it["sortPriority"])

    def block_key(i_b):
        i, b = i_b
        ig = b.get("itemGuid")
        if ig in new_priority:
            return (0, new_priority[ig], i)
        return (1, 0, i)

    blocks = [b for _, b in sorted(enumerate(blocks), key=block_key)]

    items_doc["data"] = items
    blocks_doc["data"] = blocks

    # 既存マスタファイル末尾は `}` (改行なし) に揃える
    items_path.write_text(
        json.dumps(items_doc, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )
    blocks_path.write_text(
        json.dumps(blocks_doc, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )

    return {
        "items_count": len(items),
        "min_priority": min(it["sortPriority"] for it in items),
        "max_priority": max(it["sortPriority"] for it in items),
        "final_order": final_order,
        "new_priority": new_priority,
        "items_by_guid": items_by_guid,
        "unlocked_in_research": unlocked_in_research,
    }


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n", 1)[0])
    ap.add_argument(
        "--mod-dir",
        required=True,
        type=Path,
        help="mod root (例: /path/to/moorestech_master/server_v8/mods/moorestechAlphaMod_8)",
    )
    ap.add_argument(
        "--quiet",
        action="store_true",
        help="最終順序の一覧出力を省略する",
    )
    args = ap.parse_args()

    if not args.mod_dir.is_dir():
        ap.error(f"--mod-dir not found: {args.mod_dir}")

    result = recalc(args.mod_dir)

    print(f"\nUpdated {result['items_count']} items.")
    print(
        f"sortPriority range: {result['min_priority']} - {result['max_priority']}"
    )

    if not args.quiet:
        print("\n=== Final order ===")
        for g in result["final_order"]:
            it = result["items_by_guid"][g]
            kind = (
                "INIT" if it.get("initialUnlocked")
                else ("RES" if g in result["unlocked_in_research"] else "ORPHAN")
            )
            print(f"  [{kind:6s}] {result['new_priority'][g]:>5}  {it['name']}")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""
moorestech mod の items.json / blocks.json の sortPriority を、research の解放順序と nodeGraph 上の配置から
それぞれ独立に再計算し、配列もその順に並べ替える。

items は unlockItemRecipeView、blocks は unlockBlock の解放対象として並ぶ（block→item の参照や同名の推測は使わない）。
並びの規則は sort_order.py、読み書きと書き戻し前の検証は master_json_io.py が正本。
検証（重複キー・原文の無変更往復・sortPriority と配列順以外の不変・末尾改行）を 1 つでも満たさなければ、どのファイルも書かない。

Recalculate sortPriority of items.json and blocks.json independently from research unlock order and nodeGraph layout,
and reorder both arrays. Items follow unlockItemRecipeView and blocks follow unlockBlock (no block->item link or name guess).
If any pre-write check fails, no file is written.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import master_json_io
import sort_order


def recalc(mod_dir: Path) -> list:
    """両ロールを計算・検証してから書き戻す。戻り値は [(role, placements, 名前表, 変わったか)]。
    Compute and verify both roles before writing; returns [(role, placements, names, changed)]."""
    research = master_json_io.read_master_json(mod_dir / "master" / "research.json", False).doc["data"]
    positions = sort_order.node_positions(master_json_io.read_master_json(mod_dir / ".mooreseditor" / "nodeGraph.v1.json", False).doc)
    research_order = sort_order.ordered_research(research, positions)

    planned = []
    for role in sort_order.ROLES:
        loaded = master_json_io.read_master_json(mod_dir / "master" / role.file_name, True)
        rows = loaded.doc["data"]
        missing = [row.get("name", row[role.guid_key]) for row in rows if "sortPriority" not in row]
        if missing:
            # 元の相対順が決まらないので既定値で埋めずに止める
            # Without it the original relative order is undefined, so stop instead of filling a default
            raise master_json_io.MasterJsonError(f"{loaded.path}: rows without sortPriority {missing}; nothing was written")
        placements, unknown = sort_order.plan_placements(rows, role, research_order, positions)
        for research_name, guid in unknown:
            print(f"WARN: {role.label}: research '{research_name}' unlocks {guid}, which is not in {role.file_name} (skipped)", file=sys.stderr)
        names = {row[role.guid_key]: row.get("name", "") for row in rows}
        loaded.doc["data"] = sort_order.apply_placements(rows, role, placements)
        new_text = master_json_io.render(loaded.doc, loaded.trailing_newline)
        master_json_io.verify_only_sort_changed(loaded, new_text, role.guid_key)
        planned.append((role, placements, names, loaded, new_text))

    # 両ロールの検証が通ってから書く（片方だけ書き換わった状態を残さない）
    # Write only after both roles pass the checks, so a half-updated pair is never left behind
    return [(role, placements, names, master_json_io.write_if_changed(loaded, new_text)) for role, placements, names, loaded, new_text in planned]


def main():
    ap = argparse.ArgumentParser(description=__doc__.strip().split("\n\n", 1)[0])
    ap.add_argument("--mod-dir", required=True, type=Path,
                    help="mod root (例: /path/to/moorestech_master/server_v8/mods/moorestechAlphaMod_8)")
    ap.add_argument("--quiet", action="store_true", help="最終順序の一覧出力を省略する")
    args = ap.parse_args()

    if not args.mod_dir.is_dir():
        ap.error(f"--mod-dir not found: {args.mod_dir}")

    for role, placements, names, changed in recalc(args.mod_dir):
        priorities = [p.priority for p in placements]
        counts = {kind: sum(1 for p in placements if p.kind == kind) for kind in (sort_order.INITIAL, sort_order.RESEARCH, sort_order.ORPHAN)}
        print(f"\nUpdated {len(placements)} {role.label} ({'written' if changed else 'unchanged'}). "
              f"sortPriority range: {min(priorities)} - {max(priorities)}. "
              f"INIT {counts[sort_order.INITIAL]} / RES {counts[sort_order.RESEARCH]} / ORPHAN {counts[sort_order.ORPHAN]}")
        if not args.quiet:
            print(f"=== Final order ({role.label}) ===")
            for p in placements:
                source = f"  <- {p.research_name}" if p.research_name else ""
                print(f"  [{p.kind:6s}] {p.priority:>5}  {names[p.guid]}{source}")


if __name__ == "__main__":
    main()

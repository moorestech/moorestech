#!/usr/bin/env python3
"""未投入バグ報告の候補走査（取り込み日にかかわらず全期間・plan H の再掲契約）。

投入するまで毎朝再掲する契約のため、日付フィルタを持つ collect_boxes とは別経路で全件を走査する。

Scans un-enqueued bug-report candidates across all time regardless of ingest date (plan H's
"resurface every morning until enqueued" contract); a separate path from the date-filtered collect_boxes.
"""
from __future__ import annotations

import shlex
from pathlib import Path

import digest_schema as schema
from digest_collect import jst_date


def collect_all_boxes(root: Path) -> list[dict]:
    """日付で絞らず全期間の箱を返す。読めない箱は黙って飛ばす（件数表示は日次集計側の役目）
    Returns boxes across all time unfiltered by date; unreadable boxes are skipped (counting is the daily aggregator's job)"""
    boxes: list[dict] = []
    if not root.is_dir():
        return boxes
    for ingest_path in sorted(root.glob("*/*/ingest.json")):
        meta, _reason = schema.read_conformed(ingest_path, schema.INGEST_SCHEMA)
        if meta is None:
            continue
        boxes.append({"dir": ingest_path.parent, "meta": meta})
    return boxes


def load_candidate_reports(root: Path) -> list[dict]:
    """未投入のバグ報告を取り込み日にかかわらず全期間から拾う
    Collects un-enqueued bug reports across all time regardless of ingest date"""
    reports = []
    for box in collect_all_boxes(root):
        manifest, _reason = schema.read_conformed(box["dir"] / "manifest.json", schema.MANIFEST_SCHEMA)
        if manifest is None or manifest["kind"] != "bug":
            continue
        if (box["dir"] / "AUTOFIX_QUEUED").is_file():
            continue
        ready_at = box["meta"]["readyAt"] or box["meta"]["ingestedAt"]
        reports.append({
            "id": box["meta"]["id"] or box["dir"].name,
            "steamId": box["meta"]["steamId"],
            "description": manifest["description"],
            "readyAtDate": jst_date(ready_at) or "不明",
        })
    return reports


def format_candidates(candidates: list[dict]) -> list[str]:
    """未投入のバグ報告を、取り込み日にかかわらず全期間から、そのまま貼れる enqueue コマンド付きで並べる
    （投入するまで毎朝再掲する契約。投入の判断は人が持つ。plan H）
    Lists un-enqueued bug reports across all time with a copy-pastable enqueue command, resurfacing daily
    until enqueued; the decision stays with a human (plan H)"""
    lines = ["", "## 投入候補のバグ報告"]
    if not candidates:
        return lines + ["- なし"]
    for report in candidates:
        # テスター申告の steamId/id をそのまま Discord へ貼るとコマンド注入になるため必ず shlex.quote する
        # steamId/id are tester-supplied; always shlex.quote them before pasting into the Discord command
        quoted_id = shlex.quote(report["id"])
        quoted_steam = shlex.quote(report["steamId"])
        head = (report["description"].strip().splitlines() or ["（説明文が空）"])[0]
        lines.append(f"- {quoted_id}（{report['readyAtDate']}）… {head}")
        lines.append(f"  `scripts/playtest/enqueue-autofix.sh {quoted_steam} {quoted_id}`")
    return lines

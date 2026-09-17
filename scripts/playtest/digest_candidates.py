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
from digest_collect import jst_date, warn
from digest_schema import neutralize_discord_markup as safe


def load_candidate_reports(root: Path) -> tuple[list[dict], dict]:
    """未投入のバグ報告を取り込み日にかかわらず全期間から拾う。読めない ingest.json・manifest.json の箱は
    理由を stderr へ出して除外件数に数える（候補から無音で消さない）
    Collects un-enqueued bug reports across all time regardless of ingest date; boxes with an unreadable
    ingest.json or manifest.json are logged to stderr and counted, never silently dropped from the candidates"""
    reports: list[dict] = []
    stats = {"unreadable": 0}
    if not root.is_dir():
        return reports, stats
    for ingest_path in sorted(root.glob("*/*/ingest.json")):
        meta, reason = schema.read_conformed(ingest_path, schema.INGEST_SCHEMA)
        manifest_path = ingest_path.parent / "manifest.json"
        manifest, manifest_reason = schema.read_conformed(manifest_path, schema.MANIFEST_SCHEMA) if meta else (None, None)
        if meta is None or manifest is None:
            warn(f"投入候補の判定から除外: {reason or manifest_reason}", ingest_path if meta is None else manifest_path)
            stats["unreadable"] += 1
            continue
        if manifest["kind"] != "bug" or (ingest_path.parent / "AUTOFIX_QUEUED").is_file():
            continue
        reports.append({
            "id": meta["id"] or ingest_path.parent.name,
            "steamId": meta["steamId"],
            "description": manifest["description"],
            "readyAtDate": jst_date(meta["readyAt"] or meta["ingestedAt"]) or "不明",
        })
    return reports, stats


def format_candidates(candidates: list[dict], stats: dict) -> list[str]:
    """未投入のバグ報告を、取り込み日にかかわらず全期間から、そのまま貼れる enqueue コマンド付きで並べる
    （投入するまで毎朝再掲する契約。投入の判断は人が持つ。plan H）
    Lists un-enqueued bug reports across all time with a copy-pastable enqueue command, resurfacing daily
    until enqueued; the decision stays with a human (plan H)"""
    lines = ["", "## 投入候補のバグ報告"]
    if not candidates:
        lines.append("- なし")
    for report in candidates:
        # テスター申告の steamId/id をそのまま Discord へ貼るとコマンド注入になるため必ず shlex.quote する
        # steamId/id are tester-supplied; always shlex.quote them before pasting into the Discord command
        quoted_id = shlex.quote(report["id"])
        quoted_steam = shlex.quote(report["steamId"])
        head = (report["description"].strip().splitlines() or ["（説明文が空）"])[0]
        lines.append(f"- {safe(quoted_id)}（{report['readyAtDate']}）… {safe(head)}")
        # バッククォート入りの値はコードスパンを閉じて外へ漏れるので、貼り付けコマンドを出さない
        # A value containing a backtick would close the code span and leak out, so no pasteable command is printed
        if "`" in report["id"] + report["steamId"]:
            lines.append("  ⚠ id/steamId にバッククォートを含むため貼り付けコマンドを出さない")
            continue
        lines.append(f"  `scripts/playtest/enqueue-autofix.sh {quoted_steam} {quoted_id}`")
    if stats["unreadable"]:
        lines.append(f"- ⚠ ingest.json/manifest.json を読めず投入候補の判定から除外した箱 {stats['unreadable']}件")
    return lines

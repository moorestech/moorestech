#!/usr/bin/env python3
"""プレイテストの日次ダイジェストを Markdown で標準出力へ出す。

Prints the playtest daily digest as Markdown on stdout.
Hermes cron が --no-agent で呼び、stdout がそのまま Discord へ流れる（要約させないため LLM を挟まない）。
Hermes cron runs it with --no-agent so stdout goes to Discord verbatim, with no LLM in between.
"""
from __future__ import annotations

import argparse
import os
import sys
from collections import Counter
from datetime import datetime, timedelta
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import digest_collect as dc  # noqa: E402


def top_line(counter: Counter, limit: int) -> str:
    if not counter:
        return "なし"
    return " / ".join(f"{key} {value}件" for key, value in counter.most_common(limit))


def format_counts(reports: list[dict], stats: dict) -> list[str]:
    counts = Counter(r["kind"] for r in reports)
    lines = ["", "## プレイ報告の件数",
             f"- バグ {counts.get('bug', 0)}件 / 感想 {counts.get('feedback', 0)}件 / "
             f"クラッシュ {counts.get('crash', 0)}件"]
    if counts.get("unknown"):
        lines.append(f"- ⚠ manifest.kind を読めなかった箱 {counts['unknown']}件")
    if stats.get("unreadable"):
        lines.append(f"- ⚠ ingest.json を読めない/日付を解釈できず除外した箱 {stats['unreadable']}件")
    if stats.get("readyAtFallback"):
        lines.append(f"- ⚠ readyAt が無く ingestedAt で日付判定した箱 {stats['readyAtFallback']}件")
    return lines


def format_feedback(reports: list[dict]) -> list[str]:
    """感想は要約せず全文を出す（ADR 0061「新着の感想全文」）
    Feedback is printed in full, never summarised (ADR 0061)"""
    lines = ["", "## 感想（全文）"]
    items = [r for r in reports if r["kind"] == "feedback"]
    if not items:
        return lines + ["- なし"]
    for report in items:
        label = report["buildLabel"] or "不明"
        lines.append(f"### {report['id']}（SteamID {report['steamId']} / build {label}）")
        lines.append(report["description"].strip() or "（説明文が空）")
    return lines


def format_candidates(reports: list[dict]) -> list[str]:
    """未投入のバグ報告を、そのまま貼れる enqueue コマンド付きで並べる（投入の判断は人が持つ）
    Lists un-enqueued bug reports with a copy-pastable enqueue command; the decision stays with a human"""
    lines = ["", "## 投入候補のバグ報告"]
    items = [r for r in reports if r["kind"] == "bug" and not r["queued"]]
    if not items:
        return lines + ["- なし"]
    for report in items:
        head = (report["description"].strip().splitlines() or ["（説明文が空）"])[0]
        lines.append(f"- {report['id']} … {head}")
        lines.append(f"  `scripts/playtest/enqueue-autofix.sh {report['steamId']} {report['id']}`")
    return lines


def format_progress(agg: dict, stats: dict) -> list[str]:
    lines = ["", "## 進行記録"]
    if agg["sessions"] == 0:
        lines.append("- なし")
    else:
        lines.append(f"- 人数 {agg['testers']} 人 / セッション {agg['sessions']} 件")
        lines.append(f"- 平均プレイ時間 {agg['meanPlaySeconds'] / 60:.1f} 分")
        lines.append(f"- 平均研究完了数 {agg['meanResearch']:.1f}")
        lines.append("- 到達チャレンジ数の分布: " + top_line(agg["reachBuckets"], 5))
        lines.append("- 離脱時の最後のイベント上位: " + top_line(agg["lastEvents"], 5))
        lines.append("- 離脱時のUI状態上位: " + top_line(agg["lastUiStates"], 5))
        lines.append("- 終了理由: " + top_line(agg["endReasons"], 5))
    if stats.get("unreadable"):
        lines.append(f"- ⚠ ingest.json を読めない/日付を解釈できず除外した箱 {stats['unreadable']}件")
    if stats.get("readyAtFallback"):
        lines.append(f"- ⚠ readyAt が無く ingestedAt で日付判定した箱 {stats['readyAtFallback']}件")
    if stats.get("invalidRecord"):
        lines.append(f"- ⚠ record.json の型が想定外で除外した件数 {stats['invalidRecord']}件")
    return lines


def format_runs(runs: list[dict], fallback: int) -> list[str]:
    lines = ["", "## 自動修正ラン"]
    if not runs:
        lines.append("- なし")
    else:
        lines.append("- " + " / ".join(f"{k} {v}件" for k, v in sorted(Counter(
            r["status"] for r in runs).items())))
        for run in runs:
            pr = f"#{run['prNumber']}" if run["prNumber"] else "PRなし"
            lines.append(f"- {run['id']} … {run['status']} / {pr} / "
                         f"base {run['base'] or '不明'} / {run['summary']}")
    if fallback:
        lines.append(f"- ⚠ finishedAt が無い/解釈できず mtime で日付判定したラン {fallback}件")
    return lines


def truncate(body: str, limit: int, archive: Path) -> str:
    """Discord の1メッセージ上限に収める。切ったときは必ず全文の置き場を示す
    Fits one Discord message and always points at the archived full text when cut"""
    if limit <= 0 or len(body) <= limit:
        return body
    notice = f"\n…（切り詰め。全文 {len(body)}文字は {archive} ）\n"
    return body[: max(0, limit - len(notice))] + notice


def resolve_date(raw: str) -> str:
    """既定は JST の前日。書式違いは例外で落とす（黙って別の日を集計しない）
    Defaults to yesterday in JST; a malformed value raises rather than silently digesting another day"""
    if raw in ("", "yesterday"):
        return (datetime.now(dc.JST) - timedelta(days=1)).strftime("%Y-%m-%d")
    datetime.strptime(raw, "%Y-%m-%d")
    return raw


def emit_warnings(report_stats: dict, progress_stats: dict, run_fallback: int) -> None:
    """除外・フォールバックが起きたら stderr にも出す（Discord に届く stdout とは別に運用者が拾えるように）
    Also prints to stderr when boxes were excluded or fell back, so operators see it beyond the Discord stdout"""
    counters = {
        "reports.unreadable": report_stats.get("unreadable", 0),
        "reports.readyAtFallback": report_stats.get("readyAtFallback", 0),
        "progress.unreadable": progress_stats.get("unreadable", 0),
        "progress.readyAtFallback": progress_stats.get("readyAtFallback", 0),
        "progress.invalidRecord": progress_stats.get("invalidRecord", 0),
        "runs.finishedAtFallback": run_fallback,
    }
    for name, count in counters.items():
        if count:
            print(f"[digest] WARN {name}={count}", file=sys.stderr)


def main(argv: list[str] | None = None) -> int:
    default_logs = os.environ.get(
        "MOORESTECH_LOGS", str(Path.home() / "hermes-agent/data/repos/moorestech_logs"))
    parser = argparse.ArgumentParser(description="プレイテスト日次ダイジェスト / playtest daily digest")
    parser.add_argument("--date", default="yesterday", help="YYYY-MM-DD か yesterday")
    parser.add_argument("--logs", default=default_logs, help="moorestech_logs のルート")
    parser.add_argument("--max-chars", type=int, default=1800, help="標準出力の上限文字数")
    parser.add_argument("--no-archive", action="store_true", help="digests/ へ書かない")
    args = parser.parse_args(argv)

    date = resolve_date(args.date)
    logs = Path(args.logs)
    playtest = logs / "harness" / "playtest"
    reports, report_stats = dc.load_reports(playtest / "reports", date)
    progress, progress_stats = dc.load_progress(playtest / "progress", date)
    runs, fallback = dc.load_fix_results(logs / "harness" / "bug-report" / "runs", date)

    lines = [f"# moorestech プレイテスト日次ダイジェスト {date}"]
    lines += format_counts(reports, report_stats)
    lines += format_candidates(reports)
    lines += format_feedback(reports)
    lines += format_progress(dc.aggregate_progress(progress), progress_stats)
    lines += format_runs(runs, fallback)
    body = "\n".join(lines) + "\n"

    archive = playtest / "digests" / f"{date}.md"
    if not args.no_archive:
        archive.parent.mkdir(parents=True, exist_ok=True)
        archive.write_text(body, encoding="utf-8")

    emit_warnings(report_stats, progress_stats, fallback)
    sys.stdout.write(truncate(body, args.max_chars, archive))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

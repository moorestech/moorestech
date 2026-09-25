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
import digest_candidates as dcand  # noqa: E402
import digest_collect as dc  # noqa: E402
from digest_schema import neutralize_discord_markup as safe  # noqa: E402
from digest_schema import protect_pasteable_commands as protect_cmds, restore_pasteable_commands as restore_cmds  # noqa: E402


def top_line(counter: Counter, limit: int) -> str:
    if not counter:
        return "なし"
    return " / ".join(f"{key} {value}件" for key, value in counter.most_common(limit))


def format_counts(reports: list[dict], stats: dict) -> list[str]:
    counts = Counter(r["kind"] for r in reports)
    lines = ["", "## プレイ報告の件数",
             f"- バグ {counts.get('bug', 0)}件 / 感想 {counts.get('feedback', 0)}件 / "
             f"クラッシュ {counts.get('crash', 0)}件"]
    # KNOWN_KINDS 以外（空文字含む）は件数からも警告からも無音で消さず、値ごとに出す
    # Anything outside KNOWN_KINDS (including empty) never disappears silently; each value gets its own line
    unexpected = {k: v for k, v in counts.items() if k not in dc.KNOWN_KINDS}
    for kind, count in sorted(unexpected.items()):
        label = kind or "(空/読めなかった)"
        lines.append(f"- ⚠ 想定外 kind: {label} {count}件")
    if stats.get("unreadable"):
        lines.append(f"- ⚠ ingest.json を読めない/日付を解釈できず除外した箱 {stats['unreadable']}件")
    if stats.get("readyAtFallback"):
        lines.append(f"- ⚠ readyAt が無く ingestedAt で日付判定した箱 {stats['readyAtFallback']}件")
    if stats.get("invalidManifest"):
        lines.append(f"- ⚠ manifest.json の型が想定外で除外した箱 {stats['invalidManifest']}件")
    if stats.get("noPayload"):
        lines.append(f"- ⚠ manifest.json が無い箱（クライアントが全ファイルを見送った） {stats['noPayload']}件")
    return lines


def format_feedback(reports: list[dict]) -> list[str]:
    """感想は要約せず全文を出す（ADR 0061「新着の感想全文」）
    Feedback is printed in full, never summarised (ADR 0061)"""
    lines = ["", "## 感想（全文）"]
    items = [r for r in reports if r["kind"] == "feedback"]
    if not items:
        return lines + ["- なし"]
    for report in items:
        lines.append(f"### {report['id']}（{report['reporter']} / build {report['buildLabel'] or '不明'}）")
        lines.append(report["description"].strip() or "（説明文が空）")
    return lines


def format_progress(agg: dict, stats: dict, records: list[dict]) -> list[str]:
    lines = ["", "## 進行記録"]
    if agg["sessions"] == 0:
        lines.append("- なし")
    else:
        lines.append(f"- 人数 {agg['testers']} 人 / セッション {agg['sessions']} 件")
        testers = {record["steamId"]: record["tester"] for record in records}
        lines.append("- テスター: " + "、".join(testers.values()))
        # 全件 playSeconds 欠落なら 0 分と偽らず「不明」と明示する
        # When every session lacks playSeconds, say "unknown" rather than falsely printing 0 minutes
        if agg["meanPlaySeconds"] is None:
            lines.append("- 平均プレイ時間 不明（playSeconds が全件欠落）")
        else:
            lines.append(f"- 平均プレイ時間 {agg['meanPlaySeconds'] / 60:.1f} 分")
        lines.append(f"- 平均研究完了数 {agg['meanResearch']:.1f}")
        lines.append("- 到達チャレンジ数の分布: " + top_line(agg["reachBuckets"], 5))
        lines.append("- 離脱時の最後のイベント上位: " + top_line(agg["lastEvents"], 5))
        lines.append("- 離脱時のUI状態上位: " + top_line(agg["lastUiStates"], 5))
        lines.append("- 終了理由: " + top_line(agg["endReasons"], 5))
        if agg.get("playSecondsMissing"):
            lines.append(f"- ⚠ playSeconds が欠落し平均から除外したセッション {agg['playSecondsMissing']}件")
    if stats.get("unreadable"):
        lines.append(f"- ⚠ ingest.json を読めない/日付を解釈できず除外した箱 {stats['unreadable']}件")
    if stats.get("readyAtFallback"):
        lines.append(f"- ⚠ readyAt が無く ingestedAt で日付判定した箱 {stats['readyAtFallback']}件")
    if stats.get("invalidRecord"):
        lines.append(f"- ⚠ record.json の型・値が想定外で除外した件数 {stats['invalidRecord']}件")
    if stats.get("noPayload"):
        lines.append(f"- ⚠ record.json が無い箱（クライアントが全ファイルを見送った） {stats['noPayload']}件")
    return lines


def format_runs(runs: list[dict], stats: dict) -> list[str]:
    lines = ["", "## 自動修正ラン"]
    if not runs:
        lines.append("- なし")
    else:
        lines.append("- " + " / ".join(f"{k} {v}件" for k, v in sorted(Counter(
            r["status"] for r in runs).items())))
        for run in runs:
            pr = f"#{run['prNumber']}" if run["prNumber"] else "PRなし"
            lines.append(f"- {run['id']} … {run['status']} / {pr} / base {run['base'] or '不明'} / {run['summary']}")
    if stats["finishedAtFallback"]:
        lines.append(f"- ⚠ finishedAt が無い/解釈できず mtime で日付判定したラン {stats['finishedAtFallback']}件")
    if stats["invalidResult"]:
        lines.append(f"- ⚠ fix-result.json の型が想定外で除外したラン {stats['invalidResult']}件")
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


def emit_warnings(report_stats: dict, progress_stats: dict, run_stats: dict) -> None:
    """除外・フォールバックが起きたら stderr にも出す（Discord に届く stdout とは別に運用者が拾えるように）
    Also prints to stderr when boxes were excluded or fell back, so operators see it beyond the Discord stdout"""
    counters = {
        "reports.unreadable": report_stats.get("unreadable", 0),
        "reports.readyAtFallback": report_stats.get("readyAtFallback", 0),
        "reports.invalidManifest": report_stats.get("invalidManifest", 0),
        "reports.noPayload": report_stats.get("noPayload", 0),
        "progress.unreadable": progress_stats.get("unreadable", 0),
        "progress.readyAtFallback": progress_stats.get("readyAtFallback", 0),
        "progress.invalidRecord": progress_stats.get("invalidRecord", 0),
        "progress.noPayload": progress_stats.get("noPayload", 0),
        "progress.playSecondsMissing": progress_stats.get("playSecondsMissing", 0),
        "runs.finishedAtFallback": run_stats["finishedAtFallback"],
        "runs.invalidResult": run_stats["invalidResult"],
    }
    for name, count in counters.items():
        if count:
            print(f"[digest] WARN {name}={count}", file=sys.stderr)


def main(argv: list[str] | None = None) -> int:
    # 既定は repo の兄弟の moorestech_logs（シェル側と同じくスクリプト位置から導出。HOME は差し替わりうる）
    # Defaults to moorestech_logs beside the repo, derived from the script location like the shell side (HOME may be swapped)
    default_logs = os.environ.get(
        "MOORESTECH_LOGS", str(Path(__file__).resolve().parents[2].parent / "moorestech_logs"))
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
    candidates, candidate_stats = dcand.load_candidate_reports(playtest / "reports")
    progress, progress_stats = dc.load_progress(playtest / "progress", date)
    runs, run_stats = dc.load_fix_results(logs / "harness" / "bug-report" / "runs", date)
    progress_agg = dc.aggregate_progress(progress)
    # stderr WARN 集計へも playSeconds 欠落件数を載せる（agg 側にしか無い値なので合流させる）
    # Also feed the playSeconds-missing count into the stderr WARN summary; it only lives in agg
    progress_stats = dict(progress_stats, playSecondsMissing=progress_agg["playSecondsMissing"])

    lines = [f"# moorestech プレイテスト日次ダイジェスト {date}"]
    lines += format_counts(reports, report_stats)
    lines += dcand.format_candidates(candidates, candidate_stats)
    lines += format_feedback(reports)
    lines += format_progress(progress_agg, progress_stats, progress)
    lines += format_runs(runs, run_stats)
    # テスター由来の値が散在するため出力境界で本文全体を無害化するが、コマンド行だけは退避して書き戻す
    # Tester-supplied values are scattered so the whole body is neutralized at the boundary, but command lines are stashed and restored
    raw_body = "\n".join(lines) + "\n"
    protected_body, stashed_commands = protect_cmds(raw_body)
    body = restore_cmds(safe(protected_body), stashed_commands)
    archive = playtest / "digests" / f"{date}.md"
    if not args.no_archive:
        archive.parent.mkdir(parents=True, exist_ok=True)
        archive.write_text(body, encoding="utf-8")

    emit_warnings(report_stats, progress_stats, run_stats)
    sys.stdout.write(truncate(body, args.max_chars, archive))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

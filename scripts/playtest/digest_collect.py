#!/usr/bin/env python3
"""取り込み済みのプレイ報告・進行記録・自動修正ラン結果を日付で拾って集計する（標準ライブラリのみ）。

Collects ingested play reports, progress records and auto-fix run results for one day; stdlib only.
"""
from __future__ import annotations

import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

import digest_schema as schema
from digest_reporter import reporter_label, tester_label
from digest_support.progress import aggregate_progress

JST = timezone(timedelta(hours=9))
# バグ報告の manifest.kind として想定する値。これ以外（空含む）は件数からも警告からも消えないよう別枠で出す
# The manifest.kind values the digest expects; anything else (including empty) surfaces as its own warning
KNOWN_KINDS = ("bug", "feedback", "crash")


def warn(reason: str, path: Path) -> None:
    """除外・フォールバックの理由を Discord 本文とは別の stderr へ出す
    Prints an exclusion/fallback reason to stderr, kept separate from the Discord-bound stdout"""
    print(f"[digest] {reason}: {path}", file=sys.stderr)


def jst_date(iso: str) -> str:
    """ISO8601 を JST の YYYY-MM-DD にする。解釈できなければ空文字を返す
    Converts ISO8601 to a JST YYYY-MM-DD; returns an empty string when unparsable"""
    if not iso:
        return ""
    # 外部（受け口・ゲーム本体）が書いた文字列のパースなので例外を隔離する
    # This parses strings written by external producers, so the exception is isolated here
    try:
        parsed = datetime.fromisoformat(iso.replace("Z", "+00:00"))
    except ValueError:
        return ""
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(JST).strftime("%Y-%m-%d")


def collect_boxes(root: Path, date: str, payload_name: str) -> tuple[list[dict], dict]:
    """ingest.json の readyAt（無ければ ingestedAt）が指定日の箱を集める。読めない・型不一致・日付不明の箱は除外件数、
    readyAt欠落でingestedAtへ落とした箱はフォールバック件数、payload_name が無い箱（全ファイル見送り）は noPayload へ計上する
    Collects boxes whose ingest.json readyAt (or ingestedAt fallback) falls on the given day; unreadable/undated/type-mismatched
    boxes count as excluded, readyAt-missing ones as a fallback, and ones without payload_name (every file skipped) as noPayload"""
    boxes: list[dict] = []
    stats = {"unreadable": 0, "readyAtFallback": 0, "noPayload": 0}
    for ingest_path in sorted(root.glob("*/*/ingest.json")):
        meta, reason = schema.read_conformed(ingest_path, schema.INGEST_SCHEMA)
        ready_at = meta["readyAt"] if meta else ""
        day = jst_date(ready_at or meta["ingestedAt"]) if meta else ""
        if not day:
            warn(reason or "readyAt/ingestedAt を解釈できない", ingest_path)
            stats["unreadable"] += 1
            continue
        if day != date:
            continue
        if not ready_at:
            warn("readyAt が無く ingestedAt で日付判定", ingest_path)
            stats["readyAtFallback"] += 1
        if not (ingest_path.parent / payload_name).is_file():
            warn(f"{payload_name} が無い（クライアントが全ファイルを見送った箱）", ingest_path.parent)
            stats["noPayload"] += 1
            continue
        boxes.append({"dir": ingest_path.parent, "meta": meta})
    return boxes, stats


def load_reports(root: Path, date: str) -> tuple[list[dict], dict]:
    """日次報告の種別・説明文・ビルド識別を取り出す。
    Extracts kind, description and build label for daily reports."""
    boxes, stats = collect_boxes(root, date, "manifest.json")
    stats = dict(stats, invalidManifest=0)
    reports = []
    for box in boxes:
        manifest, reason = schema.read_conformed(box["dir"] / "manifest.json", schema.MANIFEST_SCHEMA)
        if manifest is None:
            warn(reason or "manifest.json の型が想定外", box["dir"] / "manifest.json")
            stats["invalidManifest"] += 1
            continue
        # 有効と不明はどちらも集計から外す。不明を通すと証跡の読めない箱が通常の数字へ混ざる
        # Both enabled and unknown stay out of the counts; letting unknown through would mix unverifiable boxes into normal figures
        state, unknown_reason = schema.remote_exec_state(manifest)
        if state != schema.REMOTE_EXEC_DISABLED:
            warn(f"遠隔実行が{'有効' if state == schema.REMOTE_EXEC_ENABLED else '不明'}のセッションを集計から除外{f'（{unknown_reason}）' if unknown_reason else ''}", box["dir"])
            stats["remoteExec"] = stats.get("remoteExec", 0) + 1
            continue
        reports.append({
            "id": box["meta"]["id"] or box["dir"].name,
            "steamId": box["meta"]["steamId"],
            "reporter": reporter_label(box["meta"]),
            "kind": manifest["kind"],
            "description": manifest["description"],
            "buildLabel": manifest["buildInfo"]["steamBuildLabel"],
        })
    return reports, stats


def flatten_progress_record(record: dict, box: dict) -> dict:
    """検証済み記録を集計用の1行へ畳む。
    Flattens a validated record into one aggregation row."""
    events = record["events"]
    # meta優先で解決したIDを集計と表示で共有する
    # Share the meta-first resolved ID between aggregation and display.
    steam_id = box["meta"]["steamId"] or record["steamId"]
    raw_seconds = record["playSeconds"]
    return {
        "steamId": steam_id,
        "tester": tester_label(dict(box["meta"], steamId=steam_id)),
        # 未計測を保持し、平均は集計側で不明とする
        # Preserve missing measurements so aggregation marks the mean unknown.
        "playSeconds": float(raw_seconds) if raw_seconds is not None else None,
        "endReason": record["endReason"] or "unknown",
        "reached": len(record["reachedChallenges"]),
        "research": len(record["completedResearch"]),
        "lastUiState": record["lastUiState"] or "unknown",
        "lastEvent": (events[-1]["type"] if events else "") or "none",
    }


def load_progress(root: Path, date: str) -> tuple[list[dict], dict]:
    """進行記録1件を集計しやすい形へ畳む。離脱地点は最後のイベントと最後の UI 状態で見る。型・値
    （schemaVersion・playSeconds）が契約と食い違う record.json は件数に数えて除外する（1件の異常で全体を止めない）
    Flattens one progress record; drop-off is read from the last event and last UI state. A record.json whose
    types or values (schemaVersion, playSeconds) break the contract is counted and excluded rather than crashing the run"""
    boxes, stats = collect_boxes(root, date, "record.json")
    stats = dict(stats, invalidRecord=0)
    records = []
    for box in boxes:
        record, reason = schema.read_conformed(box["dir"] / "record.json", schema.RECORD_SCHEMA)
        reason = reason if record is None else schema.record_value_problem(record)
        if reason is not None:
            warn(reason, box["dir"] / "record.json")
            stats["invalidRecord"] += 1
            continue
        # None は不明（キーの無い旧版の記録）。有効と同じく集計から外す
        # None means unknown (a legacy record without the key) and stays out of the counts just like enabled
        if record["remoteExec"] is None or record["remoteExec"]:
            warn(f"遠隔実行が{'有効' if record['remoteExec'] else '不明'}の進行記録を集計から除外", box["dir"])
            stats["remoteExec"] = stats.get("remoteExec", 0) + 1
            continue
        records.append(flatten_progress_record(record, box))
    return records, stats


def load_fix_results(runs_root: Path, date: str) -> tuple[list[dict], dict]:
    """自動修正ランを finishedAt で拾う。欠けている・解釈できない分は mtime に落とし（対象日のランだけ数える）、
    型が契約と食い違う fix-result.json は除外して、それぞれ件数を返して出力に出す
    Picks runs by finishedAt; missing or unparsable stamps fall back to mtime (counted only for target-day runs)
    and type-mismatched fix-result.json files are excluded, both reported as counts"""
    runs: list[dict] = []
    stats = {"finishedAtFallback": 0, "invalidResult": 0}
    for result_path in sorted(runs_root.glob("*/fix-result.json")):
        result, reason = schema.read_conformed(result_path, schema.FIX_RESULT_SCHEMA)
        if result is None:
            warn(reason or "fix-result.json の型が想定外", result_path)
            stats["invalidResult"] += 1
            continue
        # 欠落と、7桁小数等で python3.9 の fromisoformat が拒否する解釈不能を同じフォールバックに束ねる
        # Missing and unparsable (e.g. 7-digit fractions python3.9's fromisoformat rejects) share one fallback
        day = jst_date(result["finishedAt"])
        fell_back = not day
        if fell_back:
            day = datetime.fromtimestamp(result_path.stat().st_mtime, JST).strftime("%Y-%m-%d")
        if day != date:
            continue
        if fell_back:
            warn("finishedAt が無い/解釈できず mtime で日付判定", result_path)
            stats["finishedAtFallback"] += 1
        runs.append({
            "id": result_path.parent.name,
            "status": result["status"] or "missing",
            "prNumber": result["pr_number"],
            "base": result["base"],
            "summary": result["summary"],
        })
    return runs, stats

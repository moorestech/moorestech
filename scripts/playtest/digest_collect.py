#!/usr/bin/env python3
"""取り込み済みのプレイ報告・進行記録・自動修正ラン結果を日付で拾って集計する（標準ライブラリのみ）。

Collects ingested play reports, progress records and auto-fix run results for one day; stdlib only.
"""
from __future__ import annotations

import json
from collections import Counter
from datetime import datetime, timedelta, timezone
from pathlib import Path

JST = timezone(timedelta(hours=9))
REACH_BUCKETS = ((0, 0, "0"), (1, 2, "1-2"), (3, 5, "3-5"), (6, 9, "6-9"))


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


def read_json(path: Path) -> dict:
    """壊れている・読めない・dict でない JSON はすべて空 dict にする。呼び出し側が件数として報告する
    Broken, unreadable or non-dict JSON all become an empty dict; callers report the count"""
    # 外部（受け口・ゲーム本体・自動修正ラン）が書いたファイルの読み取りなので例外を隔離する
    # This reads files written by external producers, so the exception is isolated here
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    return data if isinstance(data, dict) else {}


def collect_boxes(root: Path, date: str) -> tuple[list[dict], dict]:
    """ingest.json の readyAt（無ければ ingestedAt）が指定日の箱を集める。読めない・
    日付不明の箱は除外件数、readyAt欠落でingestedAtへ落とした箱はフォールバック件数へ計上する
    Collects boxes whose ingest.json readyAt (or ingestedAt fallback) falls on the given day;
    unreadable/undated boxes count as excluded, readyAt-missing boxes count as a fallback"""
    boxes: list[dict] = []
    stats = {"unreadable": 0, "readyAtFallback": 0}
    if not root.is_dir():
        return boxes, stats
    for ingest_path in sorted(root.glob("*/*/ingest.json")):
        meta = read_json(ingest_path)
        ready_at = meta.get("readyAt") or ""
        stamp = ready_at or meta.get("ingestedAt") or ""
        day = jst_date(stamp)
        if not day:
            stats["unreadable"] += 1
            continue
        if day != date:
            continue
        if not ready_at:
            stats["readyAtFallback"] += 1
        boxes.append({"dir": ingest_path.parent, "meta": meta})
    return boxes, stats


def load_reports(root: Path, date: str) -> tuple[list[dict], dict]:
    """プレイ報告の manifest から種別・説明文・ビルド識別と、投入済みかどうかを取り出す
    Extracts kind, description, build label and the enqueued flag from each play report manifest"""
    boxes, stats = collect_boxes(root, date)
    reports = []
    for box in boxes:
        manifest = read_json(box["dir"] / "manifest.json")
        build_info = manifest.get("buildInfo") or {}
        reports.append({
            "id": box["meta"].get("id") or box["dir"].name,
            "steamId": box["meta"].get("steamId", ""),
            "kind": manifest.get("kind") or "unknown",
            "description": manifest.get("description") or "",
            "buildLabel": build_info.get("steamBuildLabel", ""),
            # AUTOFIX_QUEUED は enqueue-autofix.sh だけが書く。投入候補一覧から外す判定に使う
            # AUTOFIX_QUEUED is written only by enqueue-autofix.sh and removes the box from the candidate list
            "queued": (box["dir"] / "AUTOFIX_QUEUED").is_file(),
            "dir": box["dir"],
        })
    return reports, stats


def coerce_progress_record(record: dict, box: dict) -> dict | None:
    """record.json の型が契約と食い違えば None を返す（ゲーム側の壊れた出力を弾く）
    Returns None when record.json's field types don't match the contract, rejecting malformed game output"""
    play_seconds = record.get("playSeconds", 0.0)
    events = record.get("events") or []
    reached = record.get("reachedChallenges") or []
    research = record.get("completedResearch") or []
    if not isinstance(play_seconds, (int, float)):
        return None
    if not isinstance(events, list) or not all(isinstance(e, dict) for e in events):
        return None
    if not isinstance(reached, list) or not isinstance(research, list):
        return None
    last_event = events[-1].get("type") if events else "none"
    return {
        "steamId": record.get("steamId") or box["meta"].get("steamId", ""),
        "playSeconds": float(play_seconds),
        "endReason": record.get("endReason") or "unknown",
        "reached": len(reached),
        "research": len(research),
        "lastUiState": record.get("lastUiState") or "unknown",
        "lastEvent": last_event if isinstance(last_event, str) and last_event else "none",
    }


def load_progress(root: Path, date: str) -> tuple[list[dict], dict]:
    """進行記録1件を集計しやすい形へ畳む。離脱地点は最後のイベントと最後の UI 状態で見る。
    型が契約と食い違う record.json は件数に数えて除外する（1件の異常で全体を止めない）
    Flattens one progress record; drop-off is read from the last event and last UI state.
    A type-mismatched record.json is counted and excluded rather than crashing the whole run"""
    boxes, stats = collect_boxes(root, date)
    stats = dict(stats, invalidRecord=0)
    records = []
    for box in boxes:
        record = read_json(box["dir"] / "record.json")
        parsed = coerce_progress_record(record, box)
        if parsed is None:
            stats["invalidRecord"] += 1
            continue
        records.append(parsed)
    return records, stats


def bucket_reached(count: int) -> str:
    for low, high, label in REACH_BUCKETS:
        if low <= count <= high:
            return label
    return "10+"


def aggregate_progress(records: list[dict]) -> dict:
    """人数・平均プレイ時間・到達段階分布・離脱地点上位を出す
    Produces tester count, mean play time, reached-stage histogram and top drop-off points"""
    if not records:
        return {"testers": 0, "sessions": 0, "meanPlaySeconds": 0.0, "meanResearch": 0.0,
                "reachBuckets": Counter(), "lastEvents": Counter(),
                "lastUiStates": Counter(), "endReasons": Counter()}
    return {
        "testers": len({r["steamId"] for r in records if r["steamId"]}),
        "sessions": len(records),
        "meanPlaySeconds": sum(r["playSeconds"] for r in records) / len(records),
        "meanResearch": sum(r["research"] for r in records) / len(records),
        "reachBuckets": Counter(bucket_reached(r["reached"]) for r in records),
        "lastEvents": Counter(r["lastEvent"] for r in records),
        "lastUiStates": Counter(r["lastUiState"] for r in records),
        "endReasons": Counter(r["endReason"] for r in records),
    }


def load_fix_results(runs_root: Path, date: str) -> tuple[list[dict], int]:
    """自動修正ランを finishedAt で拾う。欠けている・解釈できない分は mtime に落とし、件数を返して出力に出す
    Picks runs by finishedAt; falls back to mtime for missing or unparsable stamps and reports the count"""
    runs: list[dict] = []
    fallback = 0
    if not runs_root.is_dir():
        return runs, fallback
    for result_path in sorted(runs_root.glob("*/fix-result.json")):
        result = read_json(result_path)
        stamp = result.get("finishedAt") or ""
        # 欠落と、7桁小数等で python3.9 の fromisoformat が拒否する解釈不能を同じフォールバックに束ねる
        # Missing and unparsable (e.g. 7-digit fractions python3.9's fromisoformat rejects) share one fallback
        day = jst_date(stamp) if stamp else ""
        if not day:
            fallback += 1
            day = datetime.fromtimestamp(result_path.stat().st_mtime, JST).strftime("%Y-%m-%d")
        if day != date:
            continue
        runs.append({
            "id": result_path.parent.name,
            "status": result.get("status") or "missing",
            "prNumber": result.get("pr_number"),
            "base": result.get("base") or "",
            "summary": result.get("summary") or "",
        })
    return runs, fallback

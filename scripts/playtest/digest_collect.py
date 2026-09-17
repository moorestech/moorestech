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
    """読めない・壊れた JSON は空 dict にする。呼び出し側が件数として報告する
    Returns an empty dict for missing or broken JSON; callers report the count"""
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def collect_boxes(root: Path, date: str) -> list[dict]:
    """ingest.json の readyAt（無ければ ingestedAt）が指定日の箱を集める
    Collects boxes whose ingest.json readyAt (or ingestedAt) falls on the given day"""
    boxes: list[dict] = []
    if not root.is_dir():
        return boxes
    for ingest_path in sorted(root.glob("*/*/ingest.json")):
        meta = read_json(ingest_path)
        stamp = meta.get("readyAt") or meta.get("ingestedAt") or ""
        if jst_date(stamp) == date:
            boxes.append({"dir": ingest_path.parent, "meta": meta})
    return boxes


def load_reports(root: Path, date: str) -> list[dict]:
    """プレイ報告の manifest から種別・説明文・ビルド識別と、投入済みかどうかを取り出す
    Extracts kind, description, build label and the enqueued flag from each play report manifest"""
    reports = []
    for box in collect_boxes(root, date):
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
    return reports


def load_progress(root: Path, date: str) -> list[dict]:
    """進行記録1件を集計しやすい形へ畳む。離脱地点は最後のイベントと最後の UI 状態で見る
    Flattens one progress record; drop-off is read from the last event and the last UI state"""
    records = []
    for box in collect_boxes(root, date):
        record = read_json(box["dir"] / "record.json")
        events = record.get("events") or []
        records.append({
            "steamId": record.get("steamId") or box["meta"].get("steamId", ""),
            "playSeconds": float(record.get("playSeconds") or 0.0),
            "endReason": record.get("endReason") or "unknown",
            "reached": len(record.get("reachedChallenges") or []),
            "research": len(record.get("completedResearch") or []),
            "lastUiState": record.get("lastUiState") or "unknown",
            "lastEvent": (events[-1].get("type") if events else "none"),
        })
    return records


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
    """自動修正ランを finishedAt で拾う。欠けている分だけ mtime に落とし、件数を返して出力に出す
    Picks runs by finishedAt; falls back to mtime only for the ones missing it and reports how many"""
    runs: list[dict] = []
    fallback = 0
    if not runs_root.is_dir():
        return runs, fallback
    for result_path in sorted(runs_root.glob("*/fix-result.json")):
        result = read_json(result_path)
        stamp = result.get("finishedAt") or ""
        if stamp:
            day = jst_date(stamp)
        else:
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

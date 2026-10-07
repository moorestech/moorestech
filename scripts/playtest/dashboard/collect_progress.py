"""進行記録を全期間ぶん読み、セッション1件=1行へ畳む（標準ライブラリのみ）。

契約の検証は digest_schema の RECORD_SCHEMA をそのまま使い、表示用の項目は display_fields で別に読む。
遠隔実行あり/不明の記録も行として返し、集計から外すかは表示側が remoteExec で決める。

Reads every progress record and flattens each session into one row (stdlib only).
Contract validation reuses digest_schema's RECORD_SCHEMA unchanged; display fields are read separately via display_fields.
Remote-exec enabled/unknown records are returned too; the view decides via remoteExec whether to aggregate them.
"""
from __future__ import annotations

from pathlib import Path

import digest_schema as schema
from digest_collect import jst_date
from display_fields import pick, pick_strings, warn


def load_sessions(progress_root: Path) -> tuple[list[dict], int]:
    """全セッションを新しい順で返す。契約違反の record.json は件数だけ返して行にしない
    Returns every session newest first; contract-breaking record.json files are only counted"""
    sessions: list[dict] = []
    invalid = 0
    for ingest_path in sorted(progress_root.glob("*/*/ingest.json")):
        row, reason = read_progress_box(ingest_path.parent)
        if row is None:
            warn(reason, ingest_path.parent)
            invalid += 1
            continue
        sessions.append(row)
    sessions.sort(key=lambda row: row["sessionStart"] or row["readyAt"], reverse=True)
    return sessions, invalid


def read_progress_box(box: Path) -> tuple[dict | None, str]:
    meta, reason = schema.read_conformed(box / "ingest.json", schema.INGEST_SCHEMA)
    if meta is None:
        return None, reason or "ingest.json を読めない"
    record_path = box / "record.json"
    if not record_path.is_file():
        return None, "record.json が無い（全ファイル見送りの箱）"
    record, reason = schema.read_conformed(record_path, schema.RECORD_SCHEMA)
    reason = reason if record is None else schema.record_value_problem(record)
    if reason is not None:
        return None, reason
    # 検証と表示の読み取りの間に書き換わっても落ちないよう、2回目が読めなければ空として扱う
    # If the file changes between validation and the display read, an unreadable second read counts as empty
    raw = schema.read_json(record_path)[0] or {}
    events = record["events"]
    ready_at = meta["readyAt"] or meta["ingestedAt"]
    session_start = pick(raw, "sessionStart", (str,), record_path) or ""
    return {
        "id": meta["id"] or box.name,
        # meta（R2の置き場所）が正。旧い箱だけ record 本文へ落とす（digest_collect と同じ規則）
        # meta (the R2 location) is authoritative; only legacy boxes fall back to the record body (same rule as digest_collect)
        "steamId": meta["steamId"] or record["steamId"],
        "testerName": meta["steamPersonaName"].strip(),
        "readyAt": ready_at,
        "sessionStart": session_start,
        # 日別のプレイ時間は遊んだ日で切るため sessionStart を使う（ダイジェストは受信日 readyAt で切るので数字は一致しない）
        # Daily play time is bucketed by when play happened (sessionStart); the digest buckets by readyAt, so totals differ
        "date": jst_date(session_start or ready_at),
        "playSeconds": record["playSeconds"],
        "totalPlaySeconds": pick(raw, "totalPlaySeconds", (int, float), record_path),
        "endReason": record["endReason"] or "unknown",
        "lastUiState": record["lastUiState"] or "unknown",
        "lastEvent": (events[-1]["type"] if events else "") or "none",
        "eventCount": len(events),
        "reachedChallenges": pick_strings(raw, "reachedChallenges", record_path),
        "completedResearch": pick_strings(raw, "completedResearch", record_path),
        "buildLabel": pick(raw, "buildInfo.steamBuildLabel", (str,), record_path) or "",
        # None（キーの無い旧版）も有効と同じく「集計外」として表示側へ渡す
        # None (a legacy record without the key) is passed on as "excluded" just like enabled
        "remoteExec": record["remoteExec"],
    }, ""

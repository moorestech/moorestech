"""進行記録を全期間ぶん読み、セッション1件=1行へ畳む（標準ライブラリのみ）。

Reads every progress record and flattens each session into one row (stdlib only).
遠隔実行あり/不明の記録も行として返し、集計から外すかは表示側が remoteExec で決める。
Remote-exec enabled/unknown records are returned too; the view decides via remoteExec whether to aggregate them.
"""
from __future__ import annotations

from pathlib import Path

import digest_schema as schema
from collect_reports import warn
from digest_collect import jst_date

# 集計に使う項目は RECORD_SCHEMA を引き継ぎ、表示だけに使う項目を足す
# Aggregated fields come from RECORD_SCHEMA; display-only fields are added on top
DASHBOARD_RECORD_SCHEMA = dict(
    schema.RECORD_SCHEMA,
    sessionStart=(schema.STR, ""),
    sessionEnd=(schema.STR, ""),
    totalPlaySeconds=(schema.NUMBER, None),
    placedBlockCount=(schema.INT, None),
    craftCount=(schema.INT, None),
    reachedChallenges=([schema.STR], None),
    completedResearch=([schema.STR], None),
    buildInfo=({"steamBuildLabel": (schema.STR, "")}, {"steamBuildLabel": ""}),
)


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
    record, reason = schema.read_conformed(record_path, DASHBOARD_RECORD_SCHEMA)
    reason = reason if record is None else schema.record_value_problem(record)
    if reason is not None:
        return None, reason
    events = record["events"]
    ready_at = meta["readyAt"] or meta["ingestedAt"]
    return {
        "id": meta["id"] or box.name,
        # meta（R2の置き場所）が正。旧い箱だけ record 本文へ落とす（digest_collect と同じ規則）
        # meta (the R2 location) is authoritative; only legacy boxes fall back to the record body (same rule as digest_collect)
        "steamId": meta["steamId"] or record["steamId"],
        "testerName": meta["steamPersonaName"].strip(),
        "readyAt": ready_at,
        "sessionStart": record["sessionStart"],
        "date": jst_date(record["sessionStart"] or ready_at),
        "playSeconds": record["playSeconds"],
        "totalPlaySeconds": record["totalPlaySeconds"],
        "endReason": record["endReason"] or "unknown",
        "lastUiState": record["lastUiState"] or "unknown",
        "lastEvent": (events[-1]["type"] if events else "") or "none",
        "eventCount": len(events),
        "reachedChallenges": record["reachedChallenges"],
        "completedResearch": record["completedResearch"],
        "placedBlockCount": record["placedBlockCount"],
        "craftCount": record["craftCount"],
        "buildLabel": record["buildInfo"]["steamBuildLabel"],
        # None（キーの無い旧版）も有効と同じく「集計外」として表示側へ渡す
        # None (a legacy record without the key) is passed on as "excluded" just like enabled
        "remoteExec": record["remoteExec"],
    }, ""

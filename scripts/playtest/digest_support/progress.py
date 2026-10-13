"""進行の数値集計。 / Progress aggregation."""
from collections import Counter

REACH_BUCKETS = ((0, 0, "0"), (1, 2, "1-2"), (3, 5, "3-5"), (6, 9, "6-9"))


def bucket_reached(count: int) -> str:
    return next((label for low, high, label in REACH_BUCKETS if low <= count <= high), "10+")


def summarize_play_time(records: list[dict]) -> dict:
    # 未計測をゼロと区別する
    # Keep unmeasured sessions separate from measured zero.
    known = [r["playSeconds"] for r in records if r["playSeconds"] is not None]
    return {"knownSeconds": sum(known, 0.0), "knownSessions": len(known),
            "missingSessions": len(records) - len(known)}


def aggregate_progress(records: list[dict]) -> dict:
    # 同じIDのセッションを束ね、未帰属を分離する
    # Group sessions by ID and separate unattributed records.
    grouped: dict[str, list[dict]] = {}
    unknown = []
    for record in records:
        if record["steamId"]:
            grouped.setdefault(record["steamId"], []).append(record)
        else:
            unknown.append(record)
    players = {sid: {"tester": rows[-1]["tester"], "playTime": summarize_play_time(rows)}
               for sid, rows in grouped.items()}
    play_time = summarize_play_time(records)
    # 全時間・全IDが既知の場合のみ人数で割る
    # Divide by players only when every time and ID is known.
    mean = (play_time["knownSeconds"] / len(players)
            if players and not play_time["missingSessions"] and not unknown else None)
    return {
        "testers": len(players), "sessions": len(records), "meanPlaySeconds": mean,
        "playSecondsMissing": play_time["missingSessions"], "playTime": play_time,
        "players": players, "unattributedPlayTime": summarize_play_time(unknown),
        "unknownIdSessions": len(unknown),
        # 進行指標は既存のセッション単位を維持する
        # Preserve session-based progression metrics.
        "meanResearch": sum(r["research"] for r in records) / len(records) if records else 0.0,
        "reachBuckets": Counter(bucket_reached(r["reached"]) for r in records),
        "lastEvents": Counter(r["lastEvent"] for r in records),
        "lastUiStates": Counter(r["lastUiState"] for r in records),
        "endReasons": Counter(r["endReason"] for r in records),
    }



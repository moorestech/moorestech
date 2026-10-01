"""進行の数値集計。 / Progress aggregation."""
from collections import Counter

REACH_BUCKETS = ((0, 0, "0"), (1, 2, "1-2"), (3, 5, "3-5"), (6, 9, "6-9"))


def bucket_reached(count: int) -> str:
    return next((label for low, high, label in REACH_BUCKETS if low <= count <= high), "10+")


def aggregate_progress(records: list[dict]) -> dict:
    """人数・平均プレイ時間・到達段階分布・離脱地点上位を出す。playSeconds が null の記録は
    セッション数・人数には数えるが平均の分母からは外し、欠落件数を別途返す
    Produces tester count, mean play time, reached-stage histogram and top drop-off points.
    Records with a null playSeconds still count toward sessions/testers but not the mean; the
    missing count is returned separately"""
    if not records:
        return {"testers": 0, "sessions": 0, "meanPlaySeconds": None, "playSecondsMissing": 0,
                "meanResearch": 0.0, "reachBuckets": Counter(), "lastEvents": Counter(),
                "lastUiStates": Counter(), "endReasons": Counter()}
    play_seconds = [r["playSeconds"] for r in records if r["playSeconds"] is not None]
    return {
        "testers": len({r["steamId"] for r in records if r["steamId"]}),
        "sessions": len(records),
        "meanPlaySeconds": (sum(play_seconds) / len(play_seconds)) if play_seconds else None,
        "playSecondsMissing": len(records) - len(play_seconds),
        "meanResearch": sum(r["research"] for r in records) / len(records),
        "reachBuckets": Counter(bucket_reached(r["reached"]) for r in records),
        "lastEvents": Counter(r["lastEvent"] for r in records),
        "lastUiStates": Counter(r["lastUiState"] for r in records),
        "endReasons": Counter(r["endReason"] for r in records),
    }



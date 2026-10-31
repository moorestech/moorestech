"""進行の表示。 / Progress formatting."""
from collections import Counter


def top_line(counter: Counter, limit: int) -> str:
    if not counter:
        return "なし"
    return " / ".join(f"{key} {value}件" for key, value in counter.most_common(limit))


def format_play_time(summary: dict) -> str:
    if summary["missingSessions"] and not summary["knownSessions"]:
        return "不明"
    value = f"{summary['knownSeconds'] / 60:.1f} 分"
    return f"既知分 {value}（一部不明）" if summary["missingSessions"] else value


def format_progress(agg: dict, stats: dict, records: list[dict]) -> list[str]:
    # 集計結果から平均の不明理由を明示する
    # Explain an unknown mean using the aggregation results.
    if agg["sessions"] == 0:
        mean = "対象なし"
    elif agg["meanPlaySeconds"] is not None:
        mean = f"{agg['meanPlaySeconds'] / 60:.1f} 分"
    else:
        reasons = []
        if agg["playSecondsMissing"]:
            reasons.append("時間欠損")
        if agg["unknownIdSessions"]:
            reasons.append("ID不明")
        mean = "不明（" + "/".join(reasons) + "）"
    lines = ["", "## 前日のプレイ状況",
             "受信日（JST）基準。実際に遊んだ日とは異なる場合あり",
             f"- 人数 {agg['testers']} 人",
             "- 合計プレイ時間 " + format_play_time(agg["playTime"]),
             "- 1人あたり平均プレイ時間 " + mean,
             "### プレイヤー別時間"]
    # 内訳も集計済みの時間を使い、空IDに名前を付けない
    # Use aggregated time for each player and keep empty IDs unnamed.
    for player in agg["players"].values():
        lines.append(f"- {player['tester']}: {format_play_time(player['playTime'])}")
    if agg["unknownIdSessions"]:
        lines.append("- ID不明分: " + format_play_time(agg["unattributedPlayTime"]))
    if not agg["sessions"]:
        lines.append("- なし")
    # 時間・帰属警告をプレイ状況の直後へ置く
    # Place time and attribution warnings immediately after player statistics.
    if agg["playSecondsMissing"]:
        lines.append(f"- ⚠ playSeconds が欠落したセッション {agg['playSecondsMissing']}件（欠損を含むため平均は不明）")
    if agg["unknownIdSessions"]:
        lines.append(f"- ⚠ ID不明のセッション {agg['unknownIdSessions']}件（帰属不明のため平均は不明）")
    lines += ["", "## 進行記録", f"- セッション {agg['sessions']} 件"]
    if not agg["sessions"]:
        lines.append("- なし")
    else:
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
        lines.append(f"- ⚠ record.json の型・値が想定外で除外した件数 {stats['invalidRecord']}件")
    if stats.get("remoteExec"):
        lines.append(f"- 遠隔実行あり/不明の進行記録 {stats['remoteExec']}件（集計から除外）")
    if stats.get("noPayload"):
        lines.append(f"- ⚠ record.json が無い箱（クライアントが全ファイルを見送った） {stats['noPayload']}件")
    return lines

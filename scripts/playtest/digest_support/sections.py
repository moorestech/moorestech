"""進行と修正ランの表示。 / Progress and fix-run formatting."""
from collections import Counter


def top_line(counter: Counter, limit: int) -> str:
    if not counter:
        return "なし"
    return " / ".join(f"{key} {value}件" for key, value in counter.most_common(limit))


def format_progress(agg: dict, stats: dict, records: list[dict]) -> list[str]:
    lines = ["", "## 進行記録"]
    if agg["sessions"] == 0:
        lines.append("- なし")
    else:
        lines.append(f"- 人数 {agg['testers']} 人 / セッション {agg['sessions']} 件")
        # 人数集計（aggregate_progress）と同じ述語（steamId非空）でそろえる。空IDの記録が一覧だけに残ると人数と食い違う
        # Matches aggregate_progress's predicate (non-empty steamId); a record with an empty id would otherwise inflate the list past the count
        testers = {record["steamId"]: record["tester"] for record in records if record["steamId"]}
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
    if stats.get("remoteExec"):
        lines.append(f"- 遠隔実行ありの進行記録 {stats['remoteExec']}件（集計から除外）")
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


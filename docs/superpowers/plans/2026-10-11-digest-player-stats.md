# Digest Player Stats Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モードは同スキルの規模ゲートに従う。この作成セッションは計画ファイルのみを書き、実装・commit・PR・実行セッション起動はしない。

**Goal:** 日次ダイジェストの投入候補を撤去し、前日受信分のプレイヤー人数・合計時間・1人あたり平均・プレイヤー別時間を先頭に出す。
**Architecture:** 既存の `load_progress → aggregate_progress → format_progress → main` を拡張する。集計は `digest_support/progress.py`、表示は `digest_support/sections.py` に所有させ、日付選別・外部JSON検証・配信経路を再実装しない。
**Tech Stack:** Python標準ライブラリ、unittest、既存Hermes cron shim。

## Requirements

- R1: 原文「投入候補は不要 プレヤーの人数とプレイ時間を出すようにできる？」。日次出力から候補節・enqueueコマンドを除去し、全期間候補loaderも呼ばない。
- R2: 明確化「前日分：プレイヤー人数・合計プレイ時間・1人あたり平均＋プレイヤー別時間」。最初の `##` は `前日のプレイ状況`、4指標を進行・感想より先に表示する。
- R3: 有効な進行記録の非空SteamIDをunique count。同じIDの全セッション `playSeconds` を加算し、平均は合計÷人数。表示名が同じ別IDは別人、報告件数やセッション数を分母にしない。
- R4: `readyAt`、欠落時のみ `ingestedAt` のJST受信日で既存どおり抽出し、「受信日（JST）基準。実際に遊んだ日とは異なる場合あり」を明記する。`sessionStart`・累積 `totalPlaySeconds` を使わない。
- R5: 欠損時間は0扱いしない。一部既知は「既知分…（一部不明）」、全件欠損は「不明」。平均は時間欠損が1件でもあれば不明とする。プレイヤー別にも同じ規則を適用する。
- R6: 空IDは人数・名前付き内訳へ入れない。既知時間は総計へ含め「ID不明分」を別表示、1件でも帰属不明なら平均は不明。レコード0件のみ人数0・合計0・平均「対象なし」。計測値0秒と未計測は区別する。
- R7: セッション数・平均研究完了数・到達分布・最後のイベント/UI・終了理由、全感想、当日bug/feedback/crash件数、自動修正ラン、除外/欠損警告・stderrを維持する。
- R8: remoteExec有効/不明の除外、Discord無害化、全文archive→stdout切り詰め順序を維持する。先頭指標が長い感想より先に残り、全文はarchiveに残る。
- R9: 空・同一ID反復・複数人・混在/全欠損・空ID・24時間超・候補loader未呼出・先頭順/切り詰めを自動検証する。
- 範囲外: Unity/C#・セーブ・Worker・収集スキーマ・dashboardの日付仕様・手動enqueueの機能変更。今回の計画作成では実装も運用変更もしない。

## Global Constraints

- AGENTS.md、CLAUDE.local.md→CLAUDE.local.macmini.md、moores-reviewer-digest.mdを読む。変更するPythonソースは各200行以下。新規コードは既に多数のファイルがあるtests直下を避け、専用サブディレクトリへ置く。意味のある処理コメントは日本語→英語の2行。
- 作成時HEAD=`cd82b8fab341aeb5423eb7e7eb66d642542e3b75`、`HEAD...origin/master` は `0 0`。controllerは `moores-wt new digest-player-stats-20261011 --from origin/master --fetch --no-editor` でfetch済みの隔離worktreeを作成した。
- controllerの派遣範囲により、このplan作成CLIはPhase 2.6とsimulatorを実行しない。別セッションの独立astra CLIへ渡す。これはユーザー裁定ではない。

## 設計検査記録

- finding1文書補完 Self-Review（2026-10-11）: 実施済み。全変更関数・呼出箇所・既存テスト置換全文・境界テスト全文を記載。構文・差分適用元・ソース200行上限をメモリ上の静的検査で確認。詳細はTask 1の自己レビュー表。実装・動作テスト・独立検査の再実行はしていない。既存の検査・承認出所記録は保持。
- Controller Self-Review: 実施済み。R1〜R9→Task 1、終結→Task 2/3を照合。空集合・単一欠損・ID不明・同名別ID・表示順・既存候補テストの更新先を確認。実行テストや本番観測は未実施。
- 配置検査（Phase 1〜2.5）: 実施済み / 違反0件・修正0件。下の配置表・フロー・死活表で既存所有者と比較。dashboard集計の流用は受信日の役割が違うため採用しない。
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / 独立astra CLI読取検査（plan-review.final.md）。
- user-simulator review: astra CLI代替の独立受け入れ予測を実施 / 要裁定0 / Fable・独立反証役は未実施（HermesにWorkflow/Agentなし）。ユーザー承認として扱わない。

## Files / 配置と前例（Phase 1・2）

全行Pythonはasmdef非所属。各行の「ドメイン/所有責務」「同役割前例」「機構」を突合した。Unity層マップの通信・DI・永続化・UniRx規約は今回非該当。

| 対象（repo相対） | 操作・所有責務 / メンバー | 前例・機構 / 判定 |
|---|---|---|
| `scripts/playtest/digest_support/progress.py` | Modify: `aggregate_progress`、新設 `summarize_play_time`。進行集計を所有 | 既存 `aggregate_progress` の純粋dict集計を拡張 / 適合 |
| `scripts/playtest/digest_support/sections.py` | Modify: `format_progress`、新設 `format_play_time`。プレイ状況表示を所有 | 既存 `format_progress` / `format_runs` のlist[str]生成 / 適合 |
| `scripts/playtest/digest.py` | Modify: `main` の順序・候補経路撤去、`emit_warnings` のID不明件数 | 既存mainの明示呼出・出力境界無害化 / 適合 |
| `scripts/playtest/digest_collect.py` | Modify: `load_reports` の未使用queued/dir収集撤去、`flatten_progress_record` のラベルID一致・コメント更新 | 既存meta優先ID解決と `digest_reporter.tester_label` に委譲 / 適合 |
| `scripts/playtest/tests/test_digest.py` | Modify: 平均/欠損/節順の期待値、不要な候補・queued CLIテストを整理し200行以内へ | 既存fixture＋CLI検証 / 適合 |
| `scripts/playtest/tests/test_digest_guards.py` | Modify: 候補専用2テストは候補関数の直接検証へ、一般無害化は感想/進行で維持 | 既存 `dcand.format_candidates(*dcand.load_candidate_reports(...))` 前例 / 適合 |
| `scripts/playtest/tests/test_digest_reporters.py` | Modify: 候補再掲のCLI期待を撤去、ID内訳・名前更新・fallbackラベルを検証 | 現行SteamID同一性/末尾レコード名優先のテスト / 適合 |
| `scripts/playtest/tests/digest_player_stats/__init__.py` | Create: 空のunittest discovery用package | 標準unittestの再帰探索 / 適合 |
| `scripts/playtest/tests/digest_player_stats/test_player_stats.py` | Create: 集計・表示・IDラベルの境界テスト（200行以内） | `digest_fixture.py` の一時ディレクトリ・subTest・subprocess / 適合 |
| `scripts/playtest/tests/digest_player_stats/test_delivery.py` | Create: 日付境界・CLI順序・archive・警告・無害化を検証（200行以内） | 既存fixtureと同一プロセスmock / 適合 |
| `scripts/playtest/hermes-cron/README.md` | Modify: 説明と登録例の候補説明を受信日/4指標へ置換 | 既存cron運用文書 / 適合 |

読んだ変更外の前例: `scripts/playtest/digest_schema.py`（検証・無害化）、`digest_candidates.py`（全期間候補走査）、`digest_reporter.py`（名前）、`tests/digest_fixture.py`、`tests/test_digest_degraded.py`、`dashboard/collect_progress.py`、`hermes-cron/moorestech-playtest-digest.sh`、`README-macmini.md`（以上 `scripts/playtest/` 起点）。候補モジュールの単体機能とschemaは今回変更しない。日次からの不要収集をなくすことと、他の操作を消すことを混同しない。

### Phase 1.5: データフローと機構比較

`既存cron → load_progress（受信日・schema・remoteExec検証）→ 集計dict → format_progress → 全文無害化 → archive → truncate → stdout`。新設関数の立場は**読み手**（既存記録からの集計・表示）。日次reports/runsは既存経路のまま。
採用「既存パイプラインの表示拡張」と不採用「dashboard全期間loaderへの置換」を比較: 後者はsessionStart基準・remoteExecを返して表示側で除外する役割なので不適合。候補走査を温存して表示だけ隠す案はR1の不要収集撤去を満たさない。削除は候補経路だけで、日次の検証や取り込み機構を抑止しない。

### Phase 2.5: 機能パリティ死活表

| 操作/表示 | 計画後 | 根拠 |
|---|---|---|
| 投入候補・全期間再掲・貼付コマンド | 削除 | D1/R1の明示指定。手動enqueueスクリプト自体は維持 |
| 人数・時間・テスター一覧 | 拡張 | 先頭4指標、同一IDを1行に合算、旧セッション平均を1人平均へ置換 |
| 進行の他指標・件数・感想全文・修正ラン | 維持 | 既存formatterの該当行を保持、全文archiveで検証 |
| 日付指定/前日既定・欠損/除外警告・無害化 | 維持 | 既存collector/schemaを再利用し回帰スイート実行 |
| archive・--no-archive・--max-chars・cron配信 | 維持 | 保存前に無害化、保存後に切り詰め。長文の全内訳はarchiveで読める |

## Task 1: 前日プレイ状況へ置換する（唯一の実装タスク）

**Files:** 上表のModify/Createすべて。**Consumes:** `load_progress(root: Path, date: str) -> tuple[list[dict], dict]` の既存行（steamId/tester/playSeconds/research/reached/lastEvent/lastUiState/endReason）。
**Interfaces / Produces:** `summarize_play_time(records: list[dict]) -> dict` は `{knownSeconds: float, knownSessions: int, missingSessions: int}`。`aggregate_progress(records: list[dict]) -> dict` は既存進行指標＋ `playTime`（総計）、`players`（SteamIDキー→`{tester: str, playTime: dict}`）、`unattributedPlayTime`、`unknownIdSessions: int` を返す。`testers=len(players)`、`playSecondsMissing=playTime.missingSessions`、`meanPlaySeconds: float | None` は全時間既知かつ全ID既知かつ人数>0の時だけ計算する。
**表示契約:** `format_play_time(summary: dict) -> str` と `format_progress(agg: dict, stats: dict, records: list[dict]) -> list[str]`。後者の既存signatureは維持し、内訳再集計はしない。`None`の平均はsessions=0なら対象なし、その他は「不明（時間欠損/ID不明）」と具体的理由を表示する。

- [ ] Step 1: 下記「新設境界テスト」の2モジュールと空の `__init__.py` を作成する。コードブロックは各ファイルの全文であり、境界表を実装者が補完する必要はない。
- [ ] Step 2: 下記 `progress.py` の集計関数を追加・置換する。
- [ ] Step 3: 下記 `sections.py` の表示関数を追加・置換し、既存テスト3ファイルを指定全文で置換する。
- [ ] Step 4: 下記 `digest.py` のimport削除・関数置換を行う。
- [ ] Step 5: 下記 `digest_collect.py` の2関数とREADMEの指定箇所を置換する。

以下はStep 1〜5の実装内容。変更を指定していない関数・import・呼出箇所は維持する。

### `scripts/playtest/digest_support/progress.py`

既存の `Counter` import・`REACH_BUCKETS`・`bucket_reached` は維持。`aggregate_progress` を以下で置換し、直前に `summarize_play_time` を追加する。

````python
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
````

### `scripts/playtest/digest_support/sections.py`

`format_progress` を置換し、`format_play_time` を直前へ追加する。`Counter` import・`top_line`・`format_runs` は維持。既存の `records` 引数は呼出契約のため残すが再集計しない。

````python
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
````

### `scripts/playtest/digest.py`

以下の2 import行を削除する。他のimportと関数・末尾のmain呼出は維持し、次の2関数を完全置換する。

````diff
-import digest_candidates as dcand  # noqa: E402
-from digest_schema import protect_pasteable_commands as protect_cmds, restore_pasteable_commands as restore_cmds  # noqa: E402
````

````python
def emit_warnings(report_stats: dict, progress_stats: dict, run_stats: dict) -> None:
    """除外・フォールバックが起きたら stderr にも出す（Discord に届く stdout とは別に運用者が拾えるように）
    Also prints to stderr when boxes were excluded or fell back, so operators see it beyond the Discord stdout"""
    counters = {
        "reports.unreadable": report_stats.get("unreadable", 0),
        "reports.readyAtFallback": report_stats.get("readyAtFallback", 0),
        "reports.invalidManifest": report_stats.get("invalidManifest", 0),
        "reports.remoteExec": report_stats.get("remoteExec", 0),
        "reports.noPayload": report_stats.get("noPayload", 0),
        "progress.unreadable": progress_stats.get("unreadable", 0),
        "progress.readyAtFallback": progress_stats.get("readyAtFallback", 0),
        "progress.invalidRecord": progress_stats.get("invalidRecord", 0),
        "progress.remoteExec": progress_stats.get("remoteExec", 0),
        "progress.noPayload": progress_stats.get("noPayload", 0),
        "progress.playSecondsMissing": progress_stats.get("playSecondsMissing", 0),
        "progress.unknownIdSessions": progress_stats.get("unknownIdSessions", 0),
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
    progress, progress_stats = dc.load_progress(playtest / "progress", date)
    runs, run_stats = dc.load_fix_results(logs / "harness" / "bug-report" / "runs", date)
    progress_agg = dc.aggregate_progress(progress)
    # 時間欠損と帰属不明を運用ログにも渡す
    # Feed missing time and unknown IDs into operational warnings.
    progress_stats = dict(progress_stats, playSecondsMissing=progress_agg["playSecondsMissing"],
                          unknownIdSessions=progress_agg["unknownIdSessions"])

    lines = [f"# moorestech プレイテスト日次ダイジェスト {date}"]
    lines += format_progress(progress_agg, progress_stats, progress)
    lines += format_counts(reports, report_stats)
    lines += format_feedback(reports)
    lines += format_runs(runs, run_stats)
    # 全文を無害化して保存してから配信用に切る
    # Neutralize and archive the complete text before truncating delivery.
    body = safe("\n".join(lines) + "\n")
    archive = playtest / "digests" / f"{date}.md"
    if not args.no_archive:
        archive.parent.mkdir(parents=True, exist_ok=True)
        archive.write_text(body, encoding="utf-8")

    emit_warnings(report_stats, progress_stats, run_stats)
    sys.stdout.write(truncate(body, args.max_chars, archive))
    return 0
````

### `scripts/playtest/digest_collect.py`

次の2関数のみ置換する。import・`collect_boxes`・`load_progress`・`load_fix_results` は変更しない。

````python
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
````

### `scripts/playtest/tests/test_digest.py`

次の全文で置換する（200行）。旧test_load_reports_marks_queuedとtest_digest_lists_enqueue_candidatesは削除対象（test_digest.pyのみ）。候補コマンドの検証は単体経路へ移し、日次の安全性は新設テストでも検証する。

````python
#!/usr/bin/env python3
"""日次ダイジェストの集計と出力（正常系）を fixture で検証する。

Verifies the daily digest aggregation and output (happy path) against a fixture tree.
"""
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from digest_fixture import DISABLED_MARK, SCRIPTS, TARGET_DATE, build_fixture, run_digest, write_json  # noqa: E402

sys.path.insert(0, str(SCRIPTS))
import digest_candidates as dcand  # noqa: E402
import digest_collect as dc  # noqa: E402
import digest_schema as dschema  # noqa: E402


class DigestTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        build_fixture(self.root)
        self.date = TARGET_DATE

    def tearDown(self):
        self.tmp.cleanup()

    def run_ok(self, *extra):
        result = run_digest(self.root, self.date, *extra)
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout

    def test_remote_exec_excluded_from_counts_and_candidates(self):
        root = self.root / "harness/playtest/reports"
        manifest = root / "7656001/20260912_100000_bug1/manifest.json"
        write_json(manifest, {"kind": "bug", "remoteExec": {"state": "Enabled", "ledgerFiles": []}})
        reports, stats = dc.load_reports(root, self.date)
        self.assertEqual(sum(r["kind"] == "bug" for r in reports), 1)
        self.assertEqual(stats["remoteExec"], 1)
        candidates, _ = dcand.load_candidate_reports(root)
        self.assertEqual(candidates, [])
        self.assertIn("遠隔実行あり/不明の報告 1件（集計から除外）", self.run_ok())
        # 印が無い旧版の箱は無効と名乗らせない（不明として除外し続ける）
        # A legacy box without the mark never claims "disabled"; it stays excluded as unknown
        write_json(manifest, {"kind": "bug", "remoteExec": None})
        self.assertEqual(dc.load_reports(root, self.date)[1]["remoteExec"], 1)
        write_json(manifest, {"kind": "bug", "remoteExec": {"state": "Disabled", "ledgerFiles": []}})
        self.assertEqual(len(dc.load_reports(root, self.date)[0]), 4)

    def test_remote_exec_progress_excluded_from_sessions_and_shown_in_digest(self):
        record = self.root / "harness/playtest/progress/7656001/20260912_130000_pg1/record.json"
        write_json(record, {"schemaVersion": 1, "steamId": "7656001", "playSeconds": 1200,
                            "remoteExec": True, "endReason": "quit"})
        records, stats = dc.load_progress(self.root / "harness/playtest/progress", self.date)
        self.assertEqual(len(records), 1)
        self.assertEqual(stats["remoteExec"], 1)
        self.assertIn("遠隔実行あり/不明の進行記録 1件（集計から除外）", self.run_ok())
        # キーの無い旧版の記録も不明として集計から外す
        # A legacy record without the key is excluded as unknown as well
        write_json(record, {"schemaVersion": 1, "steamId": "7656001", "playSeconds": 1200, "endReason": "quit"})
        self.assertEqual(dc.load_progress(self.root / "harness/playtest/progress", self.date)[1]["remoteExec"], 1)

    def test_jst_date_converts_utc(self):
        self.assertEqual(dc.jst_date("2026-09-12T15:30:00Z"), "2026-09-13")
        self.assertEqual(dc.jst_date(""), "")

    def test_load_reports_filters_by_ready_at(self):
        reports, stats = dc.load_reports(self.root / "harness/playtest/reports", self.date)
        self.assertEqual({r["kind"] for r in reports}, {"bug", "feedback", "crash"})
        self.assertEqual(len(reports), 4)
        self.assertEqual(stats, {"unreadable": 0, "readyAtFallback": 0, "noPayload": 0, "invalidManifest": 0})



    def test_aggregate_progress(self):
        records, stats = dc.load_progress(self.root / "harness/playtest/progress", self.date)
        self.assertEqual(stats["invalidRecord"], 0)
        agg = dc.aggregate_progress(records)
        self.assertEqual(agg["testers"], 2)
        self.assertEqual(agg["sessions"], 2)
        self.assertAlmostEqual(agg["meanPlaySeconds"], 900.0)
        self.assertEqual(agg["reachBuckets"]["3-5"], 1)
        self.assertEqual(agg["reachBuckets"]["0"], 1)
        self.assertEqual(agg["lastEvents"]["buildModeCancelled"], 2)
        self.assertEqual(agg["lastUiStates"]["GameScreen"], 1)

    def test_aggregate_progress_marks_null_play_seconds_unknown(self):
        """時間欠損も人数・件数に数え、平均は不明にする
        Missing time still counts toward players and sessions, but makes the mean unknown"""
        pg3 = self.root / "harness/playtest/progress/7656003/20260912_150000_pg3"
        write_json(pg3 / "ingest.json", {"kind": "progress", "steamId": "7656003",
                                         "id": "20260912_150000_pg3", "readyAt": "2026-09-12T09:30:00Z"})
        write_json(pg3 / "record.json", {"schemaVersion": 1, "steamId": "7656003", "remoteExec": False, "playSeconds": None,
                                         "endReason": "quit", "lastUiState": "GameScreen"})
        records, stats = dc.load_progress(self.root / "harness/playtest/progress", self.date)
        self.assertEqual(stats["invalidRecord"], 0)
        agg = dc.aggregate_progress(records)
        self.assertEqual(agg["sessions"], 3)
        self.assertEqual(agg["testers"], 3)
        self.assertEqual(agg["playSecondsMissing"], 1)
        self.assertIsNone(agg["meanPlaySeconds"])

    def test_digest_shows_unknown_when_all_play_seconds_missing(self):
        """playSeconds が全件 null の日は平均を 0 と偽らず「不明」と出し、⚠ 行と stderr WARN が出る
        A day where every playSeconds is null prints "unknown" instead of a false 0, with a ⚠ line and stderr WARN"""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for steam_id, box_id in (("9000001", "1"), ("9000002", "2")):
                box = root / "harness/playtest/progress" / steam_id / f"20260912_16{box_id}000_pgnull"
                write_json(box / "ingest.json", {"kind": "progress", "steamId": steam_id,
                                                 "id": box.name, "readyAt": "2026-09-12T10:00:00Z"})
                write_json(box / "record.json", {"schemaVersion": 1, "steamId": steam_id, "remoteExec": False, "playSeconds": None,
                                                 "endReason": "quit", "lastUiState": "GameScreen"})
            result = run_digest(root, self.date, "--max-chars", "0", "--no-archive")
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertNotIn("平均プレイ時間 0.0 分", result.stdout)
            self.assertIn("平均プレイ時間 不明", result.stdout)
            self.assertIn("⚠ playSeconds が欠落したセッション 2件（欠損を含むため平均は不明）", result.stdout)
            self.assertIn("[digest] WARN progress.playSecondsMissing=2", result.stderr)

    def test_load_fix_results_uses_finished_at(self):
        runs, stats = dc.load_fix_results(self.root / "harness/bug-report/runs", self.date)
        self.assertEqual(len(runs), 1)
        self.assertEqual(runs[0]["prNumber"], 1400)
        self.assertEqual(stats, {"finishedAtFallback": 0, "invalidResult": 0})

    def test_digest_sections(self):
        out = self.run_ok("--max-chars", "0")
        self.assertIn("# moorestech プレイテスト日次ダイジェスト 2026-09-12", out)
        self.assertIn("バグ 2件 / 感想 1件 / クラッシュ 1件", out)
        self.assertIn("序盤の歩きが長い", out)
        self.assertNotIn("対象外の日", out)
        self.assertIn("人数 2 人", out)
        self.assertIn("平均プレイ時間 15.0 分", out)
        self.assertIn("buildModeCancelled 2件", out)
        self.assertIn("#1400", out)
        self.assertNotIn("⚠", out)
        self.assertTrue((self.root / "harness/playtest/digests/2026-09-12.md").is_file())



    def test_enqueue_command_id_is_shell_safe(self):
        """id にシェルメタ文字が混じっても、貼り付けたコマンドは注入されず literal として渡る。
        評価時はコマンド名を printf へ差し替え、実スクリプトを決して走らせない
        A shell-meta-character id in the pasted command never injects and is passed literally; the command
        name is swapped for printf when evaluated so the real script never runs"""
        malicious = self.root / "harness/playtest/reports/7656006/20260912_160000_evil"
        write_json(malicious / "ingest.json", {"kind": "report", "steamId": "7656006",
                                                "id": "$(touch pwned)", "readyAt": "2026-09-12T09:40:00Z"})
        write_json(malicious / "manifest.json", {"kind": "bug", "remoteExec": DISABLED_MARK, "description": "injection test"})
        lines = dcand.format_candidates(*dcand.load_candidate_reports(self.root / "harness/playtest/reports"))
        cmd_line = next(line for line in lines if "enqueue-autofix.sh 7656006" in line).strip().strip("`")
        harmless = cmd_line.replace("scripts/playtest/enqueue-autofix.sh", "printf '%s\\n'", 1)
        self.assertNotEqual(harmless, cmd_line)
        with tempfile.TemporaryDirectory() as tmp:
            result = subprocess.run(["bash", "-c", harmless], cwd=tmp, capture_output=True, text=True)
            self.assertFalse((Path(tmp) / "pwned").exists())
            self.assertEqual(result.stdout, "7656006\n$(touch pwned)\n")

    def test_enqueue_command_survives_at_sign_neutralization(self):
        """候補単体のコマンド値と表示の無害化を検証する。
        Checks command values and display neutralization in the standalone candidate path."""
        box = self.root / "harness/playtest/reports/7656007/20260912_170000_atsign"
        write_json(box / "ingest.json", {"kind": "report", "steamId": "7656007@evil",
                                          "id": "20260912_170000_atsign@evil", "readyAt": "2026-09-12T09:45:00Z"})
        write_json(box / "manifest.json", {"kind": "bug", "remoteExec": DISABLED_MARK, "description": "@everyone check this"})
        lines = dcand.format_candidates(*dcand.load_candidate_reports(self.root / "harness/playtest/reports"))
        body, commands = dschema.protect_pasteable_commands("\n".join(lines))
        out = dschema.restore_pasteable_commands(dschema.neutralize_discord_markup(body), commands)
        self.assertIn(f"@{dschema.ZERO_WIDTH_SPACE}everyone", out)
        cmd_line = next(line for line in out.splitlines()
                        if "enqueue-autofix.sh 7656007" in line).strip().strip("`")
        harmless = cmd_line.replace("scripts/playtest/enqueue-autofix.sh", "printf '%s\\n'", 1)
        with tempfile.TemporaryDirectory() as tmp:
            result = subprocess.run(["bash", "-c", harmless], cwd=tmp, capture_output=True, text=True)
            self.assertEqual(result.stdout, "7656007@evil\n20260912_170000_atsign@evil\n")

    def test_digest_truncates_and_points_at_archive(self):
        long_box = self.root / "harness/playtest/reports/7656005/20260912_150000_fb2"
        write_json(long_box / "ingest.json", {"kind": "report", "steamId": "7656005",
                                              "id": "20260912_150000_fb2", "readyAt": "2026-09-12T09:30:00Z",
                                              "ingestedAt": "2026-09-12T09:31:00Z"})
        write_json(long_box / "manifest.json", {"kind": "feedback", "remoteExec": DISABLED_MARK, "description": "あ" * 4000})
        out = self.run_ok("--max-chars", "600")
        self.assertLessEqual(len(out), 700)
        self.assertIn("digests/2026-09-12.md", out)

    def test_empty_day_still_prints_headings(self):
        self.date = "2026-01-01"
        out = self.run_ok()
        self.assertIn("# moorestech プレイテスト日次ダイジェスト 2026-01-01", out)
        self.assertIn("## 進行記録", out)
        self.assertIn("なし", out)


if __name__ == "__main__":
    unittest.main()
````

### `scripts/playtest/tests/test_digest_guards.py`

次の全文で置換する（154行）。旧test_load_reports_marks_queuedとtest_digest_lists_enqueue_candidatesは削除対象（test_digest.pyのみ）。候補コマンドの検証は単体経路へ移し、日次の安全性は新設テストでも検証する。

````python
#!/usr/bin/env python3
"""日次ダイジェストの防御（Discord への無害化・record.json の値検証・投入候補の除外件数）を検証する。

Verifies the digest's guards: Discord neutralisation, record.json value checks and the candidate exclusion count.
"""
import io
import sys
from contextlib import redirect_stderr
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from digest_fixture import DISABLED_MARK, SCRIPTS, TARGET_DATE, build_fixture, run_digest, write_json  # noqa: E402

sys.path.insert(0, str(SCRIPTS))
import digest_collect as dc  # noqa: E402
import digest_candidates as dcand  # noqa: E402
from digest_schema import neutralize_discord_markup as safe

ZWSP = "​"
READY_AT = "2026-09-12T09:50:00Z"


class DigestGuardsTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        build_fixture(self.root)

    def tearDown(self):
        self.tmp.cleanup()

    def add_report(self, steam_id: str, box_id: str, manifest: dict, ingest_id: str = "") -> Path:
        box = self.root / "harness/playtest/reports" / steam_id / box_id
        write_json(box / "ingest.json", {"kind": "report", "steamId": steam_id,
                                         "id": ingest_id or box_id, "readyAt": READY_AT})
        # 印を明示しない箱は不明として除外される。印の3状態を見ないテストには無効の印を補う
        # A box with no explicit mark is excluded as unknown, so tests unconcerned with the three states get the disabled one
        if "remoteExec" not in manifest: manifest = dict(manifest, remoteExec=DISABLED_MARK)
        write_json(box / "manifest.json", manifest)
        return box

    def digest(self) -> str:
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "0", "--no-archive")
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout

    def test_tester_text_cannot_ping_or_break_code_fences(self):
        """感想全文のメンションとコードフェンスを無害化する
        Mentions and code fences in feedback text are neutralised"""
        self.add_report("7656020", "20260912_170000_fbping",
                        {"kind": "feedback", "description": "@everyone @here <@123> ```\n閉じない"})
        self.add_report("7656021", "20260912_171000_bugping", {"kind": "feedback", "description": "<@&456> ```感想"})
        out = self.digest()
        for raw in ("@everyone", "@here", "<@123>", "<@&456>", "```"):
            self.assertNotIn(raw, out)
        self.assertIn(f"@{ZWSP}everyone", out)
        self.assertIn(f"`{ZWSP}`{ZWSP}`", out)

    def test_backtick_id_gets_no_pasteable_command(self):
        """バッククォート入りの id はコードスパンを閉じてしまうので貼り付けコマンドを出さない
        An id with a backtick would close the code span, so no pasteable command is printed"""
        self.add_report("7656022", "20260912_172000_tick", {"kind": "bug", "description": "x"},
                        ingest_id="a`@everyone`b")
        out = safe("\n".join(dcand.format_candidates(
            *dcand.load_candidate_reports(self.root / "harness/playtest/reports"))))
        self.assertIn("バッククォートを含むため貼り付けコマンドを出さない", out)
        self.assertNotIn("enqueue-autofix.sh 7656022", out)
        self.assertNotIn("@everyone", out)

    def test_unreadable_candidate_box_is_logged_and_counted(self):
        """全期間の候補走査で読めない ingest.json・型不一致 manifest を理由付きで数える
        The all-time candidate scan logs and counts an unreadable ingest.json and a mismatched manifest"""
        broken = self.root / "harness/playtest/reports/7656023/20260801_100000_broken"
        broken.mkdir(parents=True)
        (broken / "ingest.json").write_text("{", encoding="utf-8")
        self.add_report("7656024", "20260801_110000_badmanifest", {"kind": ["bug"]})
        errors = io.StringIO()
        with redirect_stderr(errors):
            out = "\n".join(dcand.format_candidates(
                *dcand.load_candidate_reports(self.root / "harness/playtest/reports")))
        self.assertIn("読めず投入候補の判定から除外した箱 2件", out)
        self.assertIn("投入候補の判定から除外: JSON解析失敗", errors.getvalue())
        self.assertIn("投入候補の判定から除外: 型不一致: kind", errors.getvalue())

    def test_nonempty_remote_exec_ledger_is_counted_without_invalid_manifest(self):
        box = self.add_report("7656030", "20260912_183000_rex",
                              {"kind": "bug", "remoteExec": {"state": "Enabled", "ledgerFiles": ["remote-exec/session_1.jsonl"]}})
        reports, stats = dc.load_reports(self.root / "harness/playtest/reports", TARGET_DATE)
        self.assertEqual(stats["remoteExec"], 1)
        self.assertEqual(stats["invalidManifest"], 0)
        self.assertNotIn(box.name, {report["id"] for report in reports})
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "0", "--no-archive")
        self.assertIn("遠隔実行あり/不明の報告 1件（集計から除外）", result.stdout)
        self.assertNotIn("invalidManifest", result.stderr)
        write_json(box / "manifest.json", {"kind": "bug", "remoteExec": {"state": "Enabled", "ledgerFiles": [7]}})
        _, stats = dc.load_reports(self.root / "harness/playtest/reports", TARGET_DATE)
        self.assertEqual(stats["invalidManifest"], 1)

    def test_kind_label_and_progress_top_lines_are_neutralised(self):
        """想定外 kind のラベルと進行記録の上位集計行（終了理由・UI状態・最後のイベント）も無害化する
        The unexpected-kind label and the progress top lines (end reason, UI state, last event) are neutralised too"""
        self.add_report("7656026", "20260912_173000_kindping", {"kind": "@everyone", "description": "x"})
        box = self.root / "harness/playtest/progress/7656027/20260912_181000_ping"
        write_json(box / "ingest.json", {"kind": "progress", "steamId": "7656027", "id": box.name, "readyAt": READY_AT})
        write_json(box / "record.json", {"schemaVersion": 1, "remoteExec": False, "playSeconds": 60, "endReason": "@here",
                                         "lastUiState": "<@789>", "events": [{"type": "```fence"}]})
        out = self.digest()
        for raw in ("@everyone", "@here", "<@789>", "```"):
            self.assertNotIn(raw, out)
        self.assertIn(f"想定外 kind: @{ZWSP}everyone", out)
        self.assertIn(f"@{ZWSP}here 1件", out)

    def test_box_without_payload_is_counted_not_silently_dropped(self):
        """全ファイル見送りで manifest.json/record.json が無い箱は、型不一致と区別して件数に出す
        Boxes lacking manifest.json/record.json because every file was skipped are counted apart from type mismatches"""
        for kind, sub, box_id in (("report", "reports", "20260912_174000_empty"), ("progress", "progress", "20260912_182000_empty")):
            box = self.root / "harness/playtest" / sub / "7656028" / box_id
            write_json(box / "ingest.json", {"kind": kind, "steamId": "7656028", "id": box_id, "readyAt": READY_AT})
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "0", "--no-archive")
        self.assertIn("manifest.json が無い箱（クライアントが全ファイルを見送った） 1件", result.stdout)
        self.assertIn("record.json が無い箱（クライアントが全ファイルを見送った） 1件", result.stdout)
        self.assertNotIn("manifest.json の型が想定外で除外した箱", result.stdout)
        self.assertNotIn("読めず投入候補の判定から除外した箱", result.stdout)
        self.assertIn("WARN reports.noPayload=1", result.stderr)
        self.assertIn("WARN progress.noPayload=1", result.stderr)

    def test_invalid_record_values_are_excluded(self):
        """NaN・Infinity・負数の playSeconds と schemaVersion≠1 は平均へ混ぜず除外件数へ
        NaN/Infinity/negative playSeconds and schemaVersion other than 1 are excluded and counted"""
        cases = [("nan", '{"schemaVersion": 1, "playSeconds": NaN}', "playSeconds"),
                 ("inf", '{"schemaVersion": 1, "playSeconds": Infinity}', "playSeconds"),
                 ("negative", '{"schemaVersion": 1, "playSeconds": -5}', "playSeconds"),
                 ("version2", '{"schemaVersion": 2, "playSeconds": 60}', "schemaVersion"),
                 ("noversion", '{"playSeconds": 60}', "schemaVersion")]
        for label, text, field in cases:
            with self.subTest(label), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                build_fixture(root)
                box = root / "harness/playtest/progress/7656025/20260912_180000_x"
                write_json(box / "ingest.json", {"kind": "progress", "steamId": "7656025",
                                                 "id": box.name, "readyAt": READY_AT})
                (box / "record.json").write_text(text, encoding="utf-8")
                records, stats = dc.load_progress(root / "harness/playtest/progress", TARGET_DATE)
                self.assertEqual(stats["invalidRecord"], 1)
                self.assertAlmostEqual(dc.aggregate_progress(records)["meanPlaySeconds"], 900.0)
                result = run_digest(root, TARGET_DATE, "--max-chars", "0", "--no-archive")
                self.assertIn("record.json の型・値が想定外で除外した件数 1件", result.stdout)
                self.assertIn(field, result.stderr)


if __name__ == "__main__":
    unittest.main()
````

### `scripts/playtest/tests/test_digest_reporters.py`

次の全文で置換する（154行）。旧test_load_reports_marks_queuedとtest_digest_lists_enqueue_candidatesは削除対象（test_digest.pyのみ）。候補コマンドの検証は単体経路へ移し、日次の安全性は新設テストでも検証する。

````python
#!/usr/bin/env python3
"""取り込み時の報告者名がダイジェストだけに出ることを検証する。
Checks that ingest-time reporter names appear only in digest display text.
"""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from digest_fixture import DISABLED_MARK, SCRIPTS, TARGET_DATE, build_fixture, run_digest, write_json

sys.path.insert(0, str(SCRIPTS))
import digest
import digest_collect as collect
import digest_candidates as dcand
import digest_reporter as reporter
import digest_schema as schema

LIB = SCRIPTS / "lib"
sys.path.insert(0, str(LIB))
import steam_persona  # noqa: E402


class DigestReporterTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        build_fixture(self.root)

    def tearDown(self):
        self.temp.cleanup()

    def digest(self):
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "0", "--no-archive")
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout

    def test_feedback_includes_resolved_reporter_without_candidates(self):
        out = self.digest()
        self.assertIn("### 20260912_110000_fb1（Tester（SteamID 7656002・https://steamcommunity.com/profiles/7656002/）", out)
        self.assertNotIn("## 投入候補", out)
        self.assertNotIn("enqueue-autofix.sh", out)

    def test_old_ingest_has_unresolved_name(self):
        box = self.root / "harness/playtest/reports/7656002/20260912_110000_fb1"
        meta = json.loads((box / "ingest.json").read_text(encoding="utf-8"))
        for key in ("steamPersonaName", "steamProfileUrl", "steamPersonaMissing"):
            meta.pop(key)
        write_json(box / "ingest.json", meta)
        self.assertIn("名前未解決（SteamID 7656002）", self.digest())

    def test_reporter_name_is_neutralized(self):
        box = self.root / "harness/playtest/reports/7656002/20260912_110000_fb1"
        meta = json.loads((box / "ingest.json").read_text(encoding="utf-8"))
        meta["steamPersonaName"] = "@everyone **x**"
        write_json(box / "ingest.json", meta)
        out = self.digest()
        self.assertNotIn("@everyone", out)
        self.assertIn("@" + schema.ZERO_WIDTH_SPACE + "everyone **x**", out)

    def test_progress_lists_unique_testers_without_profile_urls(self):
        lines = [line for line in self.digest().splitlines() if line.startswith("- Tester（SteamID ")]
        self.assertEqual(lines, ["- Tester（SteamID 7656001）: 20.0 分",
                                 "- Tester（SteamID 7656002）: 10.0 分"])
        self.assertTrue(all("steamcommunity" not in line for line in lines))

    def test_digest_lists_pre_target_day_candidate_until_enqueued(self):
        """過去の候補は単体機能に残り、日次には再掲しない。
        Historical candidates remain available standalone but never resurface in the daily digest."""
        old_bug = self.root / "harness/playtest/reports/7656005/20260905_100000_oldbug"
        write_json(old_bug / "ingest.json", {"kind": "report", "steamId": "7656005",
                                             "id": "20260905_100000_oldbug", "readyAt": "2026-09-05T05:00:00Z"})
        write_json(old_bug / "manifest.json", {"kind": "bug", "remoteExec": DISABLED_MARK, "description": "対象日より前の未投入バグ"})
        candidates, _stats = dcand.load_candidate_reports(self.root / "harness/playtest/reports")
        self.assertIn("20260905_100000_oldbug", {c["id"] for c in candidates})
        out = self.digest()
        self.assertNotIn("20260905_100000_oldbug", out)
        self.assertNotIn("enqueue-autofix.sh", out)
        self.assertIn("バグ 2件 / 感想 1件 / クラッシュ 1件", out)
        (old_bug / "AUTOFIX_QUEUED").write_text("queued\n", encoding="utf-8")
        candidates, _stats = dcand.load_candidate_reports(self.root / "harness/playtest/reports")
        self.assertNotIn("20260905_100000_oldbug", {c["id"] for c in candidates})
        self.assertEqual(self.digest(), out)

    def test_reporter_label_has_no_url_when_unresolved(self):
        self.assertEqual(collect.reporter_label({"steamId": "7656001"}), "名前未解決（SteamID 7656001）")

    def test_reporter_label_omits_missing_profile_url(self):
        meta = {"steamId": "7656001", "steamPersonaName": "Tester", "steamProfileUrl": ""}
        self.assertEqual(collect.reporter_label(meta), "Tester（SteamID 7656001）")

    def test_progress_uses_one_label_per_steam_id(self):
        box = self.root / "harness/playtest/progress/7656001/20260912_150000_pg3"
        write_json(box / "ingest.json", {"steamId": "7656001", "steamPersonaName": "Updated",
                                         "readyAt": "2026-09-12T09:30:00Z"})
        write_json(box / "record.json", {"schemaVersion": 1, "steamId": "7656001", "remoteExec": False, "playSeconds": 1})
        line = next(line for line in self.digest().splitlines() if line.startswith("- Updated（SteamID "))
        self.assertEqual(line.count("SteamID 7656001"), 1)
        self.assertIn("Updated（SteamID 7656001）", line)

    def test_flatten_progress_prefers_meta_steam_id_over_record_body(self):
        # metaは箱の置き場所（R2の追跡キー）、recordは自己申告本文。食い違ったらmetaが勝つ（C11）
        # meta is the box's tracking key (its R2 location); the record body is self-reported and loses on a mismatch (C11)
        box = {"dir": self.root, "meta": {"steamId": "7656001", "steamPersonaName": "Tester"}}
        record = {"steamId": "7656999", "playSeconds": 10.0, "endReason": "quit",
                  "reachedChallenges": [], "completedResearch": [], "lastUiState": "GameScreen", "events": []}
        self.assertEqual(collect.flatten_progress_record(record, box)["steamId"], "7656001")

    def test_aggregate_progress_tester_count_matches_named_list(self):
        # 人数集計とテスター一覧を同じ述語（steamId非空）でそろえる。空IDの記録が一覧だけを水増ししない（C11）
        # The count and the named list share one predicate (non-empty steamId); an empty id never inflates the list past the count (C11)
        records = [
            {"steamId": "7656001", "tester": "A（SteamID 7656001）", "playSeconds": 1.0, "endReason": "quit",
             "reached": 0, "research": 0, "lastUiState": "x", "lastEvent": "y"},
            {"steamId": "", "tester": "名前未解決（SteamID ）", "playSeconds": 1.0, "endReason": "quit",
             "reached": 0, "research": 0, "lastUiState": "x", "lastEvent": "y"},
        ]
        agg = collect.aggregate_progress(records)
        lines = digest.format_progress(agg, {}, records)
        names = [line for line in lines if line.startswith("- A（SteamID ")]
        self.assertEqual(len(names), agg["testers"])
        self.assertNotIn("名前未解決（SteamID ）", "\n".join(lines))
        self.assertIn("- ID不明分: 0.0 分", lines)

    def test_steam_persona_extract_normalizes_newline_in_display_name(self):
        # 改行入りの表示名が、貼り付けコマンドの退避正規表現を偽装できないことを保証する（C12）
        # Guarantees a newline-bearing display name can never forge the pasteable-command stash regex (C12)
        response_path = self.root / "steam-response.json"
        forged = "x\n  `scripts/playtest/enqueue-autofix.sh " + "`" + " @everyone " + "`" + "y" + "`"
        write_json(response_path, {"response": {"players": [
            {"steamid": "7656001", "personaname": forged, "profileurl": "https://steamcommunity.com/id/x\ny"},
        ]}})
        result = steam_persona.extract(str(response_path), "7656001")
        self.assertNotIn("\n", result["steamPersonaName"])
        self.assertNotIn("\n", result["steamProfileUrl"])
        self.assertIsNone(schema.PASTEABLE_COMMAND_RE.search("  `" + result["steamPersonaName"] + "`"))

    def test_pasteable_command_regex_rejects_embedded_backtick(self):
        # 生成側の形（値にバッククォートを含まない）だけに一致し、偽装した行を退避対象にしない（C12）
        # Matches only the generator's own shape (no backtick inside the value); a forged line is never stashed (C12)
        forged = "  `scripts/playtest/enqueue-autofix.sh `@everyone`y`"
        self.assertIsNone(schema.PASTEABLE_COMMAND_RE.fullmatch(forged))

    def test_reporter_label_normalizes_control_characters_in_legacy_meta(self):
        # steam_persona.py 導入前に取り込まれた箱への防御（読み手側の正規化）（C12）
        # Defends boxes ingested before steam_persona.py normalized at the source (reader-side normalization, C12)
        meta = {"steamId": "7656001", "steamPersonaName": "x\ny", "steamProfileUrl": "https://steamcommunity.com/id/z\nw"}
        self.assertNotIn("\n", reporter.reporter_label(meta))


if __name__ == "__main__":
    unittest.main()
````


### 新設境界テスト（Step 1）

`scripts/playtest/tests/digest_player_stats/__init__.py` は0バイトの空ファイルとして作成する。以下の2ファイルは全文。新設ディレクトリ内のPythonファイルは計3個。

#### `scripts/playtest/tests/digest_player_stats/test_player_stats.py`

````python
"""人数・時間・表示の境界を検証する。
Tests player, time and rendering boundaries."""
import unittest

import digest_collect as collect
from digest_support.progress import aggregate_progress
from digest_support.sections import format_progress


def row(sid, seconds):
    return dict(steamId=sid, tester=f"Tester（SteamID {sid}）", playSeconds=seconds,
                research=0, reached=0, lastEvent="none", lastUiState="x", endReason="quit")


class PlayerStatsTest(unittest.TestCase):
    def test_empty_zero_repeated_and_multiple_players(self):
        # 人数と平均の分母をセッション数から区別する
        # Distinguish the player denominator from the session count.
        cases = [([], 0, 0, None), ([("A", 0)], 1, 0, 0),
                 ([("A", 600), ("A", 1200)], 1, 1800, 1800),
                 ([("A", 600), ("A", 1200), ("B", 3600)], 2, 5400, 2700)]
        for values, players, total, mean in cases:
            with self.subTest(values=values):
                records = [row(sid, seconds) for sid, seconds in values]
                agg = aggregate_progress(records)
                self.assertEqual((agg["testers"], agg["sessions"]), (players, len(values)))
                self.assertEqual(agg["playTime"], dict(knownSeconds=total,
                                 knownSessions=len(values), missingSessions=0))
                self.assertEqual(agg["meanPlaySeconds"], mean)
                self.assertEqual(agg["unknownIdSessions"], 0)
                lines = format_progress(agg, {}, records)
                self.assertIn(f"- 合計プレイ時間 {total / 60:.1f} 分", lines)
                value = "対象なし" if mean is None else f"{mean / 60:.1f} 分"
                self.assertIn("- 1人あたり平均プレイ時間 " + value, lines)
        agg = aggregate_progress([row("A", 600), row("A", 1200), row("B", 3600)])
        self.assertEqual(agg["players"]["A"]["playTime"]["knownSeconds"], 1800)
        self.assertEqual(agg["players"]["B"]["playTime"]["knownSeconds"], 3600)

    def test_missing_time_and_unknown_ids(self):
        # 既知分・全欠損・未帰属を本文まで区別する
        # Distinguish partial, wholly missing and unattributed time in rendered text.
        cases = [
            ([("A", 600), ("A", None), ("B", 1200)], 2, 1800, 2, 1, 0,
             "既知分 30.0 分（一部不明）", "時間欠損",
             ["- Tester（SteamID A）: 既知分 10.0 分（一部不明）", "- Tester（SteamID B）: 20.0 分"]),
            ([("A", None), ("B", None)], 2, 0, 0, 2, 0, "不明", "時間欠損",
             ["- Tester（SteamID A）: 不明", "- Tester（SteamID B）: 不明"]),
            ([("", 60), ("A", 120)], 1, 180, 2, 0, 1, "3.0 分", "ID不明",
             ["- Tester（SteamID A）: 2.0 分", "- ID不明分: 1.0 分"]),
            ([("", 60)], 0, 60, 1, 0, 1, "1.0 分", "ID不明", ["- ID不明分: 1.0 分"]),
            ([("", None)], 0, 0, 0, 1, 1, "不明", "時間欠損/ID不明", ["- ID不明分: 不明"]),
            ([("A", None)], 1, 0, 0, 1, 0, "不明", "時間欠損", ["- Tester（SteamID A）: 不明"]),
        ]
        for values, count, seconds, known, missing, unknown, total, reason, detail in cases:
            with self.subTest(values=values):
                records = [row(sid, value) for sid, value in values]
                agg = aggregate_progress(records)
                self.assertEqual((agg["testers"], agg["sessions"]), (count, len(values)))
                self.assertEqual(agg["playTime"], dict(knownSeconds=seconds,
                                 knownSessions=known, missingSessions=missing))
                self.assertEqual(agg["playSecondsMissing"], missing)
                self.assertEqual(agg["unknownIdSessions"], unknown)
                self.assertIsNone(agg["meanPlaySeconds"])
                self.assertNotIn("", agg["players"])
                unassigned = [value for sid, value in values if not sid]
                self.assertEqual(agg["unattributedPlayTime"], {
                    "knownSeconds": sum(value for value in unassigned if value is not None),
                    "knownSessions": sum(value is not None for value in unassigned),
                    "missingSessions": sum(value is None for value in unassigned)})
                lines = format_progress(agg, {}, records)
                self.assertIn("- 合計プレイ時間 " + total, lines)
                self.assertIn(f"- 1人あたり平均プレイ時間 不明（{reason}）", lines)
                for expected in detail:
                    self.assertIn(expected, lines)
                self.assertNotIn("Tester（SteamID ）", "\n".join(lines))
                if missing:
                    warning = f"- ⚠ playSeconds が欠落したセッション {missing}件（欠損を含むため平均は不明）"
                    self.assertIn(warning, lines)
                    self.assertLess(lines.index(warning), lines.index("## 進行記録"))
                if unknown:
                    warning = f"- ⚠ ID不明のセッション {unknown}件（帰属不明のため平均は不明）"
                    self.assertIn(warning, lines)
                    self.assertLess(lines.index(warning), lines.index("## 進行記録"))
                if not known:
                    self.assertNotIn("既知分", "\n".join(lines))

    def test_time_over_twenty_four_hours(self):
        records = [row("A", 90000)]
        lines = format_progress(aggregate_progress(records), {}, records)
        for expected in ("- 合計プレイ時間 1500.0 分", "- 1人あたり平均プレイ時間 1500.0 分",
                         "- Tester（SteamID A）: 1500.0 分"):
            self.assertIn(expected, lines)

    def test_same_name_keeps_ids_separate_and_last_label_wins(self):
        records = [row("A", 600), row("B", 1200), dict(row("A", 600), tester="Updated（SteamID A）")]
        agg = aggregate_progress(records)
        self.assertEqual(list(agg["players"]), ["A", "B"])
        self.assertEqual(agg["testers"], 2)
        self.assertEqual(agg["meanPlaySeconds"], 1200)
        lines = format_progress(agg, {}, records)
        self.assertIn("- Updated（SteamID A）: 20.0 分", lines)
        self.assertNotIn("- Tester（SteamID A）: 10.0 分", lines)
        self.assertIn("- Tester（SteamID B）: 20.0 分", lines)

    def test_resolved_id_is_shared_by_label_and_aggregation(self):
        record = dict(steamId="B", playSeconds=60, endReason="quit", reachedChallenges=[],
                      completedResearch=[], lastUiState="x", events=[])
        for meta_id, record_id, expected in (("A", "B", "A"), ("", "B", "B"), ("", "", "")):
            with self.subTest(meta=meta_id, record=record_id):
                value = collect.flatten_progress_record(dict(record, steamId=record_id),
                            {"meta": {"steamId": meta_id, "steamPersonaName": "Tester"}})
                self.assertEqual(value["steamId"], expected)
                self.assertEqual(value["tester"], f"Tester（SteamID {expected}）")
                agg = aggregate_progress([value])
                self.assertEqual(agg["testers"], int(bool(expected)))
                self.assertEqual(agg["unknownIdSessions"], int(not expected))
````

#### `scripts/playtest/tests/digest_player_stats/test_delivery.py`

````python
"""日付・配信順・無害化の境界を検証する。
Tests date selection, delivery order and neutralization boundaries."""
import io
import json
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest.mock import patch

import digest
import digest_collect as collect
from digest_fixture import DISABLED_MARK, TARGET_DATE, build_fixture, run_digest, write_json
from digest_schema import ZERO_WIDTH_SPACE


class PlayerStatsCliTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        build_fixture(self.root)

    def test_main_never_loads_candidates(self):
        output, errors = io.StringIO(), io.StringIO()
        # 同一プロセスで呼び、候補loaderへのアクセスを検出する
        # Invoke in-process so the patch observes candidate-loader access.
        with patch("digest_candidates.load_candidate_reports") as candidates:
            with redirect_stdout(output), redirect_stderr(errors):
                result = digest.main(["--logs", str(self.root), "--date", TARGET_DATE,
                                      "--max-chars", "0", "--no-archive"])
        self.assertEqual(result, 0)
        candidates.assert_not_called()
        out = output.getvalue()
        self.assertNotIn("## 投入候補", out)
        self.assertNotIn("enqueue-autofix.sh", out)
        self.assertEqual(next(line for line in out.splitlines() if line.startswith("## ")),
                         "## 前日のプレイ状況")
        self.assertIn("バグ 2件 / 感想 1件 / クラッシュ 1件", out)
        self.assertIn("受信日（JST）基準。実際に遊んだ日とは異なる場合あり", out)
        self.assertFalse((self.root / "harness/playtest/digests").exists())

    def test_long_feedback_preserves_statistics_and_full_archive(self):
        box = self.root / "harness/playtest/reports/7656002/20260912_110000_fb1"
        write_json(box / "manifest.json", dict(kind="feedback", remoteExec=DISABLED_MARK, description="あ" * 4000))
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "1800")
        self.assertEqual(result.returncode, 0, result.stderr)
        out = result.stdout
        self.assertLessEqual(len(out), 1800)
        self.assertIn("切り詰め", out)
        self.assertIn("digests/2026-09-12.md", out)
        full = (self.root / "harness/playtest/digests/2026-09-12.md").read_text(encoding="utf-8")
        for text in (out, full):
            self.assertEqual(next(line for line in text.splitlines() if line.startswith("## ")),
                             "## 前日のプレイ状況")
            for value in ("- 人数 2 人", "- 合計プレイ時間 30.0 分", "- 1人あたり平均プレイ時間 15.0 分",
                          "- Tester（SteamID 7656001）: 20.0 分", "- Tester（SteamID 7656002）: 10.0 分"):
                self.assertIn(value, text)
        for value in ("あ" * 4000, "セッション 2 件", "平均研究完了数 0.5", "3-5 1件", "0 1件",
                      "buildModeCancelled 2件", "GameScreen 1件", "InventoryScreen 1件",
                      "quit 1件", "crash-recovered 1件", "## 自動修正ラン", "#1400", "ベルト停止を修正"):
            self.assertIn(value, full)
        self.assertLess(full.index("## 前日のプレイ状況"), full.index("## 進行記録"))
        self.assertLess(full.index("## 進行記録"), full.index("## 感想（全文）"))

    def test_missing_unknown_and_hostile_text_warn_and_neutralize(self):
        progress = self.root / "harness/playtest/progress/7656001/20260912_130000_pg1"
        meta = json.loads((progress / "ingest.json").read_text())
        write_json(progress / "ingest.json", dict(meta, steamId="", steamPersonaName="@everyone ```"))
        write_json(progress / "record.json", dict(schemaVersion=1, steamId="", remoteExec=False, playSeconds=None))
        named = self.root / "harness/playtest/progress/7656002/20260912_140000_pg2/ingest.json"
        write_json(named, dict(json.loads(named.read_text()), steamPersonaName="@here ```"))
        feedback = self.root / "harness/playtest/reports/7656002/20260912_110000_fb1/manifest.json"
        forged = "  `scripts/playtest/enqueue-autofix.sh @everyone x`"
        write_json(feedback, dict(kind="feedback", remoteExec=DISABLED_MARK,
                                 description="@everyone ```\n" + forged))
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "0")
        self.assertEqual(result.returncode, 0, result.stderr)
        full = (self.root / "harness/playtest/digests/2026-09-12.md").read_text(encoding="utf-8")
        self.assertEqual(result.stdout, full)
        self.assertIn("- 人数 1 人", full)
        self.assertIn("- 合計プレイ時間 既知分 10.0 分（一部不明）", full)
        self.assertIn("- ID不明分: 不明", full)
        self.assertIn("- 1人あたり平均プレイ時間 不明（時間欠損/ID不明）", full)
        self.assertIn("欠損を含むため平均は不明", full)
        self.assertIn("ID不明のセッション 1件", full)
        self.assertIn("WARN progress.playSecondsMissing=1", result.stderr)
        self.assertIn("WARN progress.unknownIdSessions=1", result.stderr)
        for raw in ("@everyone", "@here", "```", forged):
            self.assertNotIn(raw, full)
        self.assertIn("@" + ZERO_WIDTH_SPACE + "everyone", full)
        self.assertIn("@" + ZERO_WIDTH_SPACE + "here", full)

    def test_jst_boundary_ready_at_priority_and_fallback(self):
        # 独立した箱集合で受信日を検証し、セッション日時は無関係とする
        # Verify reception dates in isolated boxes, independent of session timestamps.
        root = self.root / "date-cases"
        cases = [("before", "2026-09-12T14:59:59Z", "2026-09-13T01:00:00Z"),
                 ("after", "2026-09-12T15:00:00Z", "2026-09-12T01:00:00Z"),
                 ("fallback", "", "2026-09-12T14:59:59Z")]
        for sid, ready, ingested in cases:
            box = root / sid / "box"
            write_json(box / "ingest.json", dict(steamId=sid, readyAt=ready, ingestedAt=ingested))
            write_json(box / "record.json", dict(schemaVersion=1, steamId=sid, remoteExec=False,
                       playSeconds=60, sessionStart="2000-01-01T00:00:00Z", totalPlaySeconds=999999))
        errors = io.StringIO()
        with redirect_stderr(errors):
            records, stats = collect.load_progress(root, TARGET_DATE)
        self.assertEqual({record["steamId"] for record in records}, {"before", "fallback"})
        self.assertEqual(stats["readyAtFallback"], 1)
        self.assertIn("readyAt が無く ingestedAt で日付判定", errors.getvalue())
        self.assertEqual(collect.aggregate_progress(records)["playTime"]["knownSeconds"], 120)
        records, stats = collect.load_progress(root, "2026-09-13")
        self.assertEqual([record["steamId"] for record in records], ["after"])
        self.assertEqual(stats["readyAtFallback"], 0)
````

### `scripts/playtest/hermes-cron/README.md`（Step 5）

最初の説明段落の後へ次の段落を追加する。

````text
前日受信分（readyAt、欠落時のみingestedAtをJSTで判定）の人数・合計プレイ時間・1人あたり平均・プレイヤー別時間を先頭に表示する。実際に遊んだ日とは異なる場合がある。時間欠損やID不明は警告し、平均を不明とする。進行記録・報告件数・感想全文・自動修正ランも続けて表示し、日次では投入候補を収集・表示しない。
````

ジョブ登録例の補足にある `- 「投入候補のバグ報告」に並ぶ enqueue コマンドは、人が選んで叩くためのもの。エージェントが勝手に実行しない。` の1行を次の1行へ置換する。他の設定・コマンドは変更しない（既存ジョブの再作成指示ではない）。

````text
- 前日受信分（JST）の人数・合計プレイ時間・1人あたり平均・プレイヤー別時間を先頭に載せる。時間欠損・ID不明は警告し、平均を不明とする。
````


- [ ] Step 6: 下記をrepo rootから実行する（新規packageからfixtureをimportするためPYTHONPATHを明示）。全件PASSが期待値。実装前からある失敗は同じコマンドをbaseの隔離環境でも比較し記録、今回の回帰と混同しない。変更したソースの200行制限とgit diff --checkも通す。

```bash
PYTHONDONTWRITEBYTECODE=1 PYTHONPATH=scripts/playtest:scripts/playtest/tests python3 -m unittest discover -s scripts/playtest/tests/digest_player_stats -p 'test_*.py' -v
PYTHONDONTWRITEBYTECODE=1 PYTHONPATH=scripts/playtest:scripts/playtest/tests python3 -m unittest discover -s scripts/playtest/tests -p 'test_digest*.py' -v
PYTHONDONTWRITEBYTECODE=1 PYTHONPATH=scripts/playtest:scripts/playtest/tests python3 -m unittest discover -s scripts/playtest/tests -p 'test_*.py' -v
git diff --check
```

### finding1補完の自己レビュー（文書のみ）

| moores-writing-plans Self-Review | 照合結果 |
|---|---|
| 1. Requirements coverage | R1→main候補経路削除・候補未呼出/過去候補テスト。R2→format_progressと先頭/長文テスト。R3→aggregate_progressと反復/同名別ID/最後の名前テスト。R4→既存collector維持・JST境界/readyAt優先/fallback/sessionStart無関係テスト。R5/R6→欠損/未帰属テーブル・空/0/単一欠損テスト・本文/stderr検証。R7→既存進行/件数/感想/修正ランを保存しarchiveで照合、既存除外テストを維持。R8→main出力境界・偽コマンド/名前/感想無害化・archive後truncateテスト、既存remoteExecテスト維持。R9→Step 1の全テストとStep 6の3コマンド。すべてTask 1へ対応。 |
| 2. Placeholder scan | 散文だけだったStep 1〜5を完全な関数、import削除、既存テスト3ファイルの置換全文、新設2テストファイル全文、README置換文へ置換。未定義helper・省略本体なし。削除対象の旧queued/候補CLIテストも明記。 |
| 3. Type consistency | summarize_play_timeの3キー、playersのtester/playTime、unknownIdSessions、平均float/Noneを集計→表示→main→警告→テスト間で照合。既存format_progressの3引数を維持。Pythonコード9ブロックをast.parseで検査。既存テスト3件は現ソースとの完全差分を検査してから置換全文へ展開。 |
| 4. 保留・縮退 | 新たな再試行/保留なし。不明な平均は欠損・帰属理由を本文とstderrに表示。空集合、実測0、単一欠損、空IDだけ、全欠損を独立に検証。既存除外ログを保持。 |
| 5. 選択規則 | 同一IDの最後の名前と初出ID順は既存の表示ルール。複数人同名と名前更新fixtureを明示し、人数・合算時間と表示名を別に検証。 |
| 6. 責務漏出 | 時間集計はprogress、表示はsections、ID解決はflattenだけ。main/formatterで個人時間を再集計しない。新たなサービス/API対の導入なし。 |

静的合成後の行数: progress.py=48、sections.py=87、digest.py=145、digest_collect.py=180、test_digest.py=200、test_digest_guards.py=154、test_digest_reporters.py=154、新設test_player_stats.py=116、test_delivery.py=115、__init__.py=0。新設ファイル3個は専用ディレクトリ内へ置く。計画書自体は200行上限の対象外。構文・置換内容・行数確認は計画書の検査であり、実装テストのPASSを意味しない。Task 2/3、Phase 2.6、simulatorの既存記録を新しい検査実績に読み替えない。

- [ ] Step 7: Task 1 Filesと本planだけを明示stageしてcommit `feat: replace digest candidates with daily player statistics`。実装時のみ行う。

## Task 2: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] 全ブランチを同スキルでレビューする（自動実行・ゴール文言による省略不可）。レビュー記録はlogs repoのREADMEを読んで指定先へ置き、コードrepoへ入れない。
- [ ] 指摘を実コード/fixtureで照合し反映する。集計・条件・出力順の変更後はTask 1の3つのPythonテストコマンドを再実行し、長文fixtureのCLI/全文archiveも確認してcommitする。Unity検証非該当の理由は判断記録参照。

## Task 3: PR作成・通常マージ・配備確認まで終結する

- [ ] pr-createスキルでPR作成。master競合はmasterをマージして解消し、Python検証を再実行してpush。全変更commit/push済み・CI成功・未解決レビューなし・マージ可能を確認する。これは将来の実装工程であり、今回の計画作成で実行しない。
- [ ] 今回の依頼「Complete moorestech playtest daily digest change through planning, implementation via CLI agent, required independent review, tests, commit/push/PR/merge/deployment to cron live checkout and real-output verification.」を根拠に通常mergeを行う。`gh pr checks --watch` 成功後 `gh pr merge --merge`、`gh pr view --json state,mergeCommit,url` でMERGEDとmerge SHAを記録する。squashは禁止。失敗を完了扱いにしない。
- [ ] 配備前に運用clone `/Users/sakastudio/hermes-agent/data/repos/moorestech` のbranch=master・既存差分を確認する。既知の無関係なCLAUDE.local.macmini.md変更と.playwright-cliは保持し、更新対象と衝突しないことを確認した上で同cloneで `git fetch origin`、`git merge --ff-only origin/master`。merge SHAが祖先であることと変更対象の実ファイルを確認する。shimは同cloneを直接参照するため再インストール/cron再作成は不要。
- [ ] 実際の `~/.hermes/scripts/moorestech-playtest-digest.sh` と既存cron設定を読み、参照repo/logs・--no-agent・配信先・スケジュールが維持されていることを確認（この計画作成では未確認）。logs READMEを読んだ後、次の非投稿・非archiveコマンドで実データ出力とstderrを保存/検査する。

```bash
/usr/bin/python3 /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/digest.py --date yesterday --logs /Users/sakastudio/hermes-agent/data/repos/moorestech_logs --no-archive --max-chars 0 > /Users/sakastudio/hermes-agent/data/runtime/digest-player-stats-20261011/digest-player-stats-full.md 2> /Users/sakastudio/hermes-agent/data/runtime/digest-player-stats-20261011/digest-player-stats-warnings.log
/usr/bin/python3 /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/digest.py --date yesterday --logs /Users/sakastudio/hermes-agent/data/repos/moorestech_logs --no-archive --max-chars 1800 > /Users/sakastudio/hermes-agent/data/runtime/digest-player-stats-20261011/digest-player-stats-preview.md 2> /Users/sakastudio/hermes-agent/data/runtime/digest-player-stats-20261011/digest-player-stats-preview-warnings.log
```

- [ ] 対象受信日の有効記録から手集計と4指標を突合し、候補なし・警告・先頭順を確認する。配備SHA・コマンドexit・出力証跡を報告。手動Discord投稿/cron runはしない。次回通常cronの実行履歴/生成archiveで稼働確認し、まだ未実行なら「配備確認済み・定期実行未観測」と区別して残す。成果保存後にローカル規約どおりworktreeを畳む。

## 判断記録（ADR）

- D1/D2は上記Requirementsの逐語引用のみ。出所: `/Users/sakastudio/hermes-agent/data/runtime/digest-open-questions-20261011/decisions.md`。同所filter.jsonは `remaining=[]` と読取確認済み。質問を再提示しない。
- 今回の追加指示: 候補収集撤去・受信日維持・欠損/未知ID規則・先頭表示・Python限定・通常merge/配備確認をplanへ含める。ユーザーが表示の細部や関数名を裁定したとは記載しない。
- agent設計: 時間は既存の小数1桁「分」で統一（24h超も通算）。時間欠損時の平均は不明へ倒す。ID内訳は既存入力順・同IDの最後の表示名を保持。新しい永続型/汎用基盤は導入しない。
- 既存根拠: [ADR 0061](../../adr/0061-steam-closed-playtest-report-receiver-and-save-compat.md) の感想全文・日次進行/修正ラン、[ADR 0070](../../adr/0070-playtest-drops-allowlist-and-resolves-reporter-names-at-ingest.md) のSteamID同一性・取り込み時の名前・meta優先を維持。候補再掲のみD1で変更する。
- Python CLIだけの変更でゲームランタイム/C#に触れないため、Unity compile・unity-playmode-recorded-playtest・EditModeInPlayingTestは不要。レビュー後の再検証も同じPython CLI経路で行う。
- 後続独立検査へ渡すもの: このplan、上表と読取前例一覧、上記2 ADR、`.agents/skills/moores-writing-plans/references/type-closure-patterns.md` と `moorestech-layer-map.md`。Phase 2.6/simulatorの結果・新たなユーザー裁定は現時点で存在しない。

## 承認出所の訂正（2026-10-11）
- 本機能の実ユーザー原文「投入候補は不要 プレヤーの人数とプレイ時間を出すようにできる？」と「前日分：プレイヤー人数・合計プレイ時間・1人あたり平均＋プレイヤー別時間」は維持する。
- rationale-guard C1の案A/Bへのclarifyは `user_response: "[user did not respond within 10m]"`。真正なOOB回答はなく、「おすすめで」とのユーザー裁定記載は誤り。
- [agent前提] 案Aのコメントだけを292e3fe1adで適用・pushした。件名「裁定に従い」は不正確であり、承認の証拠ではない。履歴と実装・レビュー成果物は保存する。
- 真の残件: コメント復元の人間判断1件が未解決（moores-code-review Step 6.5-4）。bug-pass完了/C0/W0は承認を代替しない。回答までPR/merge/deployを再開しない。
- 正本訂正記録: `moorestech_logs/harness/moores-code-review/runs/2026-10-11-digest-player-stats-37c8f226-45d1-4630-b9ee-676fab0628fc/approval-provenance-correction.md`。

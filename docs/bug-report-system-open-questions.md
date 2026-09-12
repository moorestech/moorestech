# バグ報告システム（ADR 0057）実装 — 独立レビューの裁定に出す未決事項

本ブランチ（`feature/bug-report-auto-fix-impl`）は 2026-09-12 の**無人セッション**で plan A/B/C を通して実装した。
ユーザーは「寝るので質問等は止めず最後まで実施して。必要な質問があれば独立レビューの裁定に含むように書いておいて」と指示しており、
本来ならユーザーに諮る判断を、進行のためコントローラーが暫定裁定した。

**このファイルは pr-independent-review の裁定対象である。** 各項目は「何を・なぜそう決めたか・何を承認してほしいか」の順に書いてある。

- 設計の正本: `docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md` と `.decisions/2026-09-11-バグ報告*.md`（18件）
- 実装計画: `docs/superpowers/plans/2026-09-11-bug-report-{a-server-foundation,b-client-capture-and-report-ui,c-transport-and-auto-fix-run}.md`
- 進捗と全経緯（レビュー所見・変異確認・実測値）: 本ブランチの `.superpowers/sdd/progress.md`（git管理外）と
  `.superpowers/sdd/2026-09-11-bug-report/` 配下の各報告ファイル
- plan A 全ブランチレビューの全文根拠: `../moorestech_logs/harness/moores-code-review/runs/2026-09-12-0615/integrated.md`

**裁定の一般方針（コントローラーが一貫して採った基準）**: plan の文言と `.decisions/`・ADR 0057 が食い違う場合、
**ユーザーが「設計の正本は ADR と .decisions」と明示しているため後者を優先した。**
また「テストを緩めて緑にする」ことは一度も行っていない（実測が未達なら未達のまま報告している）。

---

## Q1. ブランチ土台を `origin/master` に切り替えた

- plan A/B/C の Global Constraints は作業ブランチを `feature/bug-report-auto-fix`（設計コミット `b9140cce5`）と指定する。
- しかし `origin/feature/bug-report-auto-fix` は**未マージの `feature/void-pipe`（PR #1349・193ファイル/8041行）の上に乗っており**、
  そのまま積むと本PRが他PRの差分を丸ごと含み、レビュー不能になる。
- `origin/master`（17cd06c9c）は既に ADR 0057・decisions 18件・plan A/B/C を含む（ea3c6b7fb / 17cd06c9c）ため、
  設計資料の欠落は無い。よって `feature/bug-report-auto-fix-impl` を `origin/master` から切った。
- **裁定を求める点:** このブランチ名・土台でよいか。旧 `feature/bug-report-auto-fix` は破棄してよいか。

## Q2. コミットの Co-Authored-By

- plan は `Co-Authored-By: Claude Fable 5.1` と設計セッション固定の `Claude-Session:` URL を義務付けている。
- 本セッションのハーネス指定は `Claude Opus 5`。ハーネス指定を優先し、`Claude-Session:` 行は付けなかった
  （設計セッションのURLを実装コミットに貼るのは事実と異なるため）。
- **裁定を求める点:** plan 側の記述を実態に合わせて直すか、今後も plan の文字列を優先するか。

## Q3. plan C Task 8「Mac mini での初回運用」は未実施

- plan は「MacBook で報告 → Mac mini が自動修正 → draft PR」の実機横断運用を初版の完成条件としている
  （`.decisions/2026-09-11-バグ報告システム初版の完成条件はMacのEditorまでとする.md`）。
- 本セッションは **Mac mini 上の無人実行**であり、MacBook 側の Editor 操作・launchd 有効化・実機報告を行えない。
- 成果物（`ship-outbox.sh`・launchd plist・`prepare-run.sh`・`inbox-poller.sh`・手順書・スキル）は完成させたが、
  **実運用の1件通しは未実施**。
- **裁定を求める点:** 初版の完成判定を「成果物完成＋ローカルリハーサル」までとするか、実運用1件を別タスクとして切るか。

## Q5. plan A R2 のファイル一覧に `Game.Map/VeinHandMiningService.cs` が漏れている

- R2 は置き換え対象ファイルを列挙しつつ、受入条件を「上記ファイルに `new Random(`・`Guid.NewGuid()`・`System.Random` が残らない（grep で0件）」としている。
- 実装時、`VeinHandMiningService.cs`（vein手掘りの取得数抽選）も同型の世界状態影響乱数として検出された。除外リスト（`ItemInstanceId` のみ）に該当しないため `GameRandom` 化した。
- **裁定を求める点:** この追加変更を承認し、plan A の一覧へ追記するか。

## Q6. plan A R4 の受入 grep 文言が経路を区別していない

- R4 の受入条件は `grep -rn "JsonConvert.SerializeObject\|JsonUtility.ToJson" moorestech_server/Assets/Scripts/Game.Block/Blocks` が0件。
- 実装後も3件残る: `VanillaBeltConveyorInventoryItem.GetSaveJsonString()`（plan の「やらないこと」に明記された入れ子文字列）と
  `IBlockBlueprintSettings.GetBlueprintSettingsJson()` 2件（`BlueprintJsonObject.Settings` は今も `Dictionary<string,string>` の別系統）。
- いずれも `IBlockSaveState.GetSaveState()` の経路外であり、R4 の目的（取り込みと直列化の分離）は達成されている。タスクレビュアーも Spec ✅ と判定。
- **裁定を求める点:** R4 の受入文言を「`IBlockSaveState` 経路に限る」と直すか。ブループリント設定のオブジェクト化を別タスクとして切るか。

## Q7. `WorldSaveCoordinator.WaitForPendingWrites()` は本番コードに残る test-only public

- plan A R5 は「`WorldSaveCoordinatorTest`／`TickEndSaveConsistencyTest` は `WaitForPendingWrites()` を挟んで通る」と明示的に義務付けている。
- 一方 AGENTS.md は「デバッグ/テスト専用 public をプロダクションに残さない」を規約としており、本番側に呼び出し元が無い。
- タスクレビュアーが plan-mandated として提起。無人セッションのため、コントローラーは「plan の義務付けを優先して残し、
  `tick ループ停止中のみ呼べる` 前提をコード隣に明記して data race を塞ぐ」と暫定裁定した。
- **裁定を求める点:** この API を本番に残してよいか。残すなら本番経路（例: サーバー停止時のフラッシュ）から呼ぶ形にすべきか、
  テスト専用の内部アクセス（`InternalsVisibleTo` 等）へ落とすべきか。

## Q8. スナップショットリングの世代数を 4 → 5 に上げた（「直前2分を確実に残す」ため）

- plan A R6 は「周期30秒（600tick）・4世代は `SnapshotRingConfig` の定数」と指定する。
- しかし剪定は保持を常に最大4世代に保つため、剪定直後の最古スナップショットは現在から `(generations-1) × period = 90秒` 前であり、
  **再生可能幅は最悪90秒・最良120秒の鋸歯**になる。`.decisions/2026-09-11-バグ報告の常時記録は直前2分を確実に残す.md` の
  「確実に」は最悪値の要求であり、4世代では満たせない。同じ決定文書が動画リングを120秒と定めているため、
  動画120秒 vs 状態再生最悪90秒という非対称も生じる。
- ユーザー指示により設計の正本は ADR 0057 と `.decisions/` であるため、コントローラーは **`Generations = 5`（最悪120秒保証）** と裁定した。
- **裁定を求める点:** 5世代でよいか（ディスク使用は1世代分増える）。それとも最悪90秒を許容して plan どおり4世代へ戻すか。

## Q9. plan A R9 の「`setting` 除外」と決定性テストの範囲を、レビュー所見に合わせて強めた

plan A Task 8 のタスクレビューで、plan の文言どおりに作ると検査が素通りする箇所が2つ見つかった。
無人セッションのため、コントローラーは**設計の正本（ADR 0057 と `.decisions/`）を優先**して次のとおり暫定裁定した。

1. **比較器の除外範囲**: R9 は「`setting`（実時刻を含む）を除外して深い比較を行う」と書いているが、
   `setting` を丸ごと除外すると `SpawnX/Y/Z`・`WorldCreationDateTime`（ロードで復元されるべき決定的な値）まで検査から外れる。
   実時刻を含むのは `TotalPlayTimeSeconds` と `LastSessionStartDateTime` だけである。
   → **フィールドパス単位の除外**に改めた。
2. **決定性検査テストの範囲**: plan のテスト世界には動的ブロック（ベルト搬送・機械進捗）が無く、毎tick変わる状態の決定性が素通りする。
   `.decisions/2026-09-11-バグ報告の再現目標はサーバー状態の忠実再生とする.md` が「非決定箇所の是正を範囲に含める」と明記しているため、
   → **ベルト＋機械を境界tickで進行中にする2ケース目**と、フィクスチャが静的化したら気づける state 非空アサートを追加した。

- **裁定を求める点:** この2点の強化を承認し、plan A R9 / Task 8 の記述を更新するか。除外フィールドの列挙はこれで過不足ないか。

## Q10. plan A R10 の数値閾値が実態と合わない（実測値つき・要再裁定）

R10 の受入は「world_1 複製（8048ブロック）でリングを有効にした90秒のプレイテストで、`GameUpdater.CurrentTick` の増分が
壁時計換算の期待値から**3tick以上欠けず**、`Capture()` の所要が**20ms以下**」。実測の結果、Assert 4件中2件が FAIL した。
**閾値は緩めていない。** 実測値は以下のとおりで、裁定を求める。

### 前提のズレ: 「world_1 の8048ブロック」は実在しない

- `~/Library/Application Support/moorestech/Saves/world_1` に `save.json` は無く `map.json`/`world.json` のみ。
  実在する最大のセーブは `world_generated` で**6ブロック**。
- そこで `world_generated` の複製（移行済み・ユーザーの実セーブは無改変・検証後削除）を土台に、
  シナリオ内で 基本土台7048 + チェスト1000 = **8054ブロック**を直接設置して規模を再現した。

### FAIL 1: `Capture()` が 18〜20ms（最悪 25.0ms）で 20ms を超える

- **注記: 初回計測は `Capture()` を tick スレッド外から呼んでおり、錠のない `WorldBlockDatastore` に対して
  稼働中の tick スレッドと race していた**（タスクレビューで検出）。計測を tick 末尾フックへ移して**取り直した値が上記**である
  （race 込みの初回値は 定常19.1〜21.1ms・最悪61.8ms）。
- 8054ブロック・90秒・3ランで、定常 18〜20ms・最悪 25.0ms。「20ms以内」は3ラン中2ランで FAIL。
- 内訳: 固定費 ≈7.7ms（mapObjects 34227件）＋ 土台 約7ms ＋ チェスト 約5ms。
- plan が想定していた原因（`GetSaveState` の `JsonConvert`）は**否定された**（ブロック経路にもう無い）。
- 残る削り代: `TrainCar.CreateSaveData` が取り込み経路で `JsonConvert.SerializeObject` を呼んでいる（本計測は列車0のため未計上）。
- 合成した8054ブロック（土台＋空チェスト）のため、実データ世界ではさらに伸びる余地がある。
- **裁定を求める点:** 20ms 閾値を実態（8000ブロック級で25ms）に合わせて引き上げるか、mapObjects の取り込みを削って 20ms を守るか。

### FAIL 2: 90秒で dropped=53.7 / 60.2 / 54.6 tick（閾値 3）

- 原因は `ServerGameUpdater` が `Thread.Sleep` の超過分を取り戻さない構造（追いつき処理なし）で、
  絶対値 `dropped < 3` は**リングの有無と無関係に達成不能**な閾値である。
- リング起因の正規化差は3ランで **+9.7 / +2.0 / -13.3** と符号が揃わず、**増加はノイズ以下**。
  （初回1ランのみの計測では「常に負（リング有効の方が速い）」に見えたが、3ラン測ると成立しないため表現を改めた。）
- 前面化（`uloop focus-window`）済み。tick は専用スレッドのため背景スロットリングではない。
- **裁定を求める点:** 受入を「リング有効時の tick 損失が、リング停止時の基準測定に対して有意に増えないこと（相対）」へ書き換えるか。
  絶対値を守るなら `ServerGameUpdater` に追いつき処理を入れる別タスクが要る。

### コントローラーの暫定結論

`.decisions/2026-09-11-スナップショットは体感できる引っかかりを出さないことを初版の条件にする.md` の
**設計上の条件（体感できる引っかかりを出さない）は実測で満たされていると読める**（リング起因の tick 損失の増加はノイズ以下・`snapshots=3` PASS）。
満たせていないのは plan A R10 の**数値表現**の方である。

## Q11. plan A 全ブランチレビューの設計判断 D1〜D13（13件）— コントローラー暫定裁定つき

`moores-code-review` を plan A 全16コミットに対して report-only で実行した（系統 64/64 ＋ Codex 3/3 ＋ 決定論、欠員なし）。
Critical 24 / Important 約30 / Minor 約40 / suppressed 10。全文は
`.superpowers/sdd/2026-09-11-bug-report/A-final-review.md` と
`../moorestech_logs/harness/moores-code-review/runs/2026-09-12-0615/integrated.md`。

設計判断 D1〜D13 について、ユーザー不在のためコントローラーが「本PRのゴール（実バンドルを再生して同一状態になる）が成立する側」へ
暫定裁定した。裁定の全文は `.superpowers/sdd/2026-09-11-bug-report/A-final-review-adjudications.md`。要点:

- **修正した**: D1（要求ID 0 の4義を型で分ける）/ D2（完了イベントのファイル一覧をリングの権威リストから）/
  D3（ロード完了後に `randomState` を復元。レール・歯車・列車を含む世界の決定性テストを追加）/
  D4（新規ワールドの `Reseed` と、シードがセーブに載ること）/ D5（剪定を件数基準→**時間基準**へ。即時取得が周期枠を食わない）/
  D6（記録の I/O 失敗がゲームの受信処理を落とさない・縮退は必ずログ）/ D7（停止契約を production の終了経路へ1本の道で）/
  D10（再生は template ではなく**バンドル内の世界**を読む）/ D11（保存側で配列順を正準化）/ D12（恒久失敗の毎tick無限リトライを塞ぐ）
- **裁定を維持して修正しない**: D9（`WaitForPendingWrites` の test-only public は plan R5 の義務付けにより残す＝Q7）/
  D8（「一時ワールド起動」規則の置き場所は今回触らない）
- **ユーザー裁定必須**: D13（R10 の数値受入条件＝Q10 と同一）

### Q8 の上書き（重要）

先に Q8 で「`SnapshotRingConfig.Generations` を 4→5 にして最悪120秒を保証する」と裁定したが、
レビュー C7 により **即時スナップショットが周期世代の枠を食うため、5世代でも「直前2分」が90秒に縮む**ことが判明した。
そこで D5 として「**剪定を時間基準へ改め、即時取得は周期枠を食わない**」に改めた。**Q8 の裁定は D5 で上書きされる。**

- **裁定を求める点:** D1〜D13 の各暫定裁定を承認するか。特に D5（時間基準の剪定）と D3（ロード後の乱数復元）は
  設計の形を変える判断であり、承認が要る。

## Q12. 別repo `moorestech_logs` の PR（plan C Task 2）

- AGENTS.md の必須ルールに従い、別repo の変更も push して PR を作成した。
- **PR: https://github.com/moorestech/moorestech_logs/pull/1**（branch `feature/bug-report-inbox`、commit `0c1609782`）
- 内容: bug-report の inbox/runs レイアウトと、動画・スナップショット等の大きなバイナリを追跡しないための容量除外（`.gitignore`）。
- 注記: plan C Task 2 のブリーフは `origin/master` を前提にしていたが、当該 repo の既定ブランチは `origin/main` である（`master` は存在しない）。
- **裁定を求める点:** 本体PRのマージ前にこの logs repo PR をマージするか（本体の `scripts/bugreport` はこのレイアウトを前提にしている）。

## Q13. plan C の通しリハーサルで見つかった継ぎ目の欠陥と、その修正（実測つき）

plan C Task 7（ローカルでの通しリハーサル）を **plan B の通しテストが書いた実バンドル2箱**に対して実行した結果、
タスク単体のテストでは捕まらない継ぎ目の欠陥が見つかった。いずれも修正済みだが、設計の形を変えたので承認を求める。

1. **バンドルが記録するマスタデータの所在が、サーバーが実際に読む場所と食い違っていた。**
   manifest の `masterData` は `../moorestech_master` の git 状態だったが、サーバーが読むのは `StartServerSettings.ServerDataDirectory`。
   食い違うと受け側の再現検査が `data[63]` という**解読不能な例外**で死ぬ（実測）。
   → 「サーバーが読んだ場所の権威はサーバーにしかない」という原則に立ち、DI に `Game.Paths.ServerDataDirectory` を置いて
   **マスタのロード元と記録値を同一の値から導出**し、完了イベントで要求元へ申告して manifest の
   `serverData{path, relativeTo, relativePath}`（`schemaVersion` 2）に載せた。受け側は食い違いを理由付きで拒否する。
   実測: 記録時のサーバーデータで `allEqual=True`、取り違え時は理由つきの拒否メッセージ。
2. **ゲームが起動しなくなっていた（P0）。** plan B の DI に VContainer の循環参照が入り、`MainGameStarter` が初期化に失敗していた。
   タスク単体のテストは全て緑で、plan C の観察ランを実走させて初めて露見した。
   → 依存の向きを反転（`BugReportUiStatePusher` が状態を押し込む形）して解消し、**環検出の回帰ガード2本**を追加した。
3. **受け側が「添付が欠けた箱」で即死していた。** `max([])` の `ValueError` のほか、`repository`/`masterData` の null、
   manifest の破損/不在でも死んでいた。`.decisions/…添付が欠けても残った資料で送信し調査する…` と正面から矛盾する。
   → 防御的な読み取りに置換し、欠けているものを理由付きでログして続行するようにした。
   副次的に、**bash の `.` が特殊組み込みであるため `run.env` 不在時のフォールバックがそもそも機能していなかった**ことも判明し修正した。
   あわせて共有メインワーキングツリーへのフォールバックを廃止した（無人エージェントが他セッションの作業場で走る経路だった）。

- **裁定を求める点:** 1 の設計（`serverData` を manifest の権威とし `masterData` は master worktree を切る根拠として残す）でよいか。
  `schemaVersion` を 2 に上げたが、既存バンドルは存在しないため移行は不要と判断した。

## Q14. 未検証のまま残っているもの（黙って落としていない）

- **録画リング〜ffmpeg の `video.mp4` 生成を通しテストで検証できていない。** この worktree に webui の node バイナリが無く、
  ポーズメニューの報告フォームが描画されないため（`setup.sh` は並行作業中の他エージェントへ影響するため実行しなかった）。
  録画単体は plan B Task 2 で**実 ffmpeg による mp4 生成と上下反転なしを目視確認済み**である。
- **plan A R10 の数値受入（`Capture()` ≤20ms・dropped<3）は未達のまま**（Q10 参照）。閾値は緩めていない。
- **plan C Task 8（Mac mini での初回運用）は未実施**（Q3 参照）。
- **自動修正ランの Step 4〜8（実 `claude -p` 起動と draft PR 作成）は到達させていない**（リハーサルのため意図的に手前で停止）。
  代わりに cwd・skill 解決・無人関所の ask/stop・`gh` 認証を個別に実測し、いずれも期待どおりであることを確認した。
- **plan A 全ブランチレビューの Important 8件が未修正で残っている**（Critical は全件解消）。一覧は
  `.superpowers/sdd/2026-09-11-bug-report/A-final-review-fix3-report.md` にある。
- `run-scenario.sh`（共有スキル `unity-playmode-recorded-playtest`）は世界データもシナリオ `.cs` の実在も検査せず、
  失敗しても**終了コード0**で Editor を PlayMode に置き去りにする。plan C 側の入口で回避したが、**本体は別 issue 化が妥当**。

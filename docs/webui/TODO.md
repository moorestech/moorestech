# Web UI ネイティブ化 TODO

**入口・方針・依存関係**: `MIGRATION.md` / **各項目の作業詳細**: `plans/phase-*.md`
**最終更新**: 2026-07-18（旧 TODO を全面刷新。完了済み項目と旧方針の記述は `archive/` とgit履歴へ）

**更新運用（タスクを完了させた本人がその場で行う。まとめて後回しにしない）**
1. タスク完了 = 検証ゲート通過 + コミット済みの時点で、該当行を `- [x]` にし
   **完了日とコミット短SHA**を追記する（例: `- [x] ...（2026-07-20 `abc1234`）`）
2. Phase 内の全項目が埋まったら `disposition.md` の該当行に完了日を追記し、
   Phase 完了の統合検証記録をコミットする（`MIGRATION.md` 検証ゲート参照）
3. 作業中に新タスクが発生したら、先に `disposition.md` へ行を追加してから本書へチェックボックスを足す

移行済み: インベントリ / ホットバー / ブロックインベントリ（機械・発電機・採掘機・ギア機械・フィルタ分岐器・チェスト・汎用）/
レシピビューア / クラフト / 研究ツリー / ビルドメニュー / モーダル（基盤。RequestModal 実配線は品質バックログ）/
進捗 / トースト / UIState ルーティング。
以下は**残作業のみ**。旧台帳全項目との対応は `disposition.md` を正とする。
**注意: Web 側（`moorestech_web/webui/src`）を触る項目は設計負債解消 WU1〜9 の完了まで凍結**（`MIGRATION.md` 並行運用ルール）。

---

## Track A: インフラ（`plans/phase-a-infra.md`）

- [ ] **A1: CEF バイナリ恒久統合（INFRA-1・最優先）** — LFS ポインタ手動回避の恒久解消。完了条件は clone→CEF 描画→Topic/Action 往復の証跡
      （実装済み 2026-07-18 `9ed40f0d5`: setup-cef.sh/.ps1 + Editor LFS検証ゲート + `design/cef-binary-integration.md`。残: clean worktree実機証跡）
- [ ] **A2: 入力・IME・フォーカス排他（INFRA-2）** — 入力二重配送の解消・IME・フォーカス往復後の入力復活
      （実装済み 2026-07-18 `3024015d6`: input_state op + WebUiInputExclusivity + probeログ + `design/input-focus-exclusivity.md`。残: 実機IME/二重配送検証）
- [x] **A3: 本番静的配信 + アセット配信 + Windows（INFRA-9/8/5）** — 静的配信 + 成果物 hash 照合と uGUI フォールバック + 汎用画像配信規約 + 動的ポート/多重起動 + Vite 死活 + Windows 実機（2026-07-18 `d0c90cc1c`。Windows実機確認のみPhase D最終検証へ）
- [x] **A4: 接続堅牢性 + Topic 横断規約（INFRA-13/7）** — revision 規約・再接続 snapshot 復元・死活監視・fault-injection スモーク（2026-07-18 `d0c90cc1c`。`topic-conventions.md`新設・envelope revision・restoring復元・heartbeat・再接続vitest）
- [x] **A5: i18n 基盤 + 要素 ID 規約（INFRA-11/12 前倒し）** — `t(key)` フック + ハードコード禁止 lint + `data-tutorial-anchor` 規約（既存画面の文字列変換は Phase D）（2026-07-18 `043e279b0`。`/api/i18n/{locale}` + localization.current + `anchor-convention.md`）
- [x] **ゲート漏れ決定論チェックの導入**（最初の移行 PR から運用。Phase D まで待たない）（2026-07-18 `1365dde3f` WebUiGateAuditTest: 全ファイル分類強制+ゲートルート検証+ルール腐敗検出）

## Track B: ブロック系仕上げ・細部パリティ

### B1: ギア系ブロック（`plans/phase-b1-gear-blocks.md`）
- [x] ギア伝達系ブロックのレジストリ登録（実測: Shaft6/Gear3/GearBeltConveyor12 の21ブロック。2026-07-18 `721c4ca7e`）
- [x] ElectricToGearGenerator 専用ビュー + 出力モード選択（2026-07-18 `721c4ca7e`。実機確認はPhase D）
- [x] レジストリ網羅 e2e（v8 全 blockType × レジストリ照合。fixture `v8-block-ui-registry.json` + 再生成手順コメント。2026-07-18 `721c4ca7e`）

### B2: 列車 PF・電柱（`plans/phase-b2-train-pf-electric-pole.md`）
- [x] TrainItemPlatform / TrainFluidPlatform の PF インベントリ + 積込/卸しモードトグル（2026-07-18 B2マージ。TrainStation/両PF登録・冪等set_transfer_mode）
- [x] ElectricPole ネットワーク情報表示（2026-07-18。ElectricPole3種・1秒固定サンプリング）

### B3: 細部パリティ（`plans/phase-b3-detail-parity.md`・1件ずつ独立）
- [x] スプリットドラッグ（grab の複数スロット均等配分）（2026-07-18 B3-1。配分計算はホスト所有）
- [x] 右ドラッグ連続1個配置（2026-07-18 `e60b636c1`）
- [x] クラフト長押し・進捗・キャンセル・連続クラフト（既実装確認。unit9件+e2e2件でカバー済み。2026-07-18）
- [x] CraftRecipeView の所持数/必要数テキスト（不足赤字）+ ツールチップ内訳（2026-07-18 `e60b636c1`。個数テキストはdata-lack属性）
- [x] アイテムリストのクラフト可能数バッジ/グレーアウト（2026-07-18 `e60b636c1`）
- [x] 機械詳細の分間生産数表示（2026-07-18 B3-7。recipeTime/出力をDTO伝搬・表示式はWeb純関数）
- [x] ホイールのホットバー切替を入力量累積に（閾値±1・1ノッチ=1切替。2026-07-18 `e60b636c1`）
- [x] 研究ツリーの報酬アイテム個数表示（2026-07-18 B3-8。rewardItems {itemId,count}[]へ置換）
- [x] `ui_state.request` のホワイトリスト化（Story/PauseMenu 中の強制遷移防止）（2026-07-18 B3-9+統合時にC1/C2/C3遷移を拡張。transition_not_allowed拒否）
- [x] itemMaster の WS 再接続後リフレッシュ（WU1に含まれず未実装だったため実装。restoring遷移で再取得。2026-07-18 `e60b636c1`）
- [x] crafting validator の堅牢化（壊れ payload での React クラッシュ耐性）（2026-07-18 `e60b636c1`）

## Track C: 大物画面（着手時に writing-plans 詳細計画を作成）

### C1: チャレンジ（`plans/phase-c1-challenge.md`）
- [x] ツリー描画基盤の共通化（shared/treeView抽出・research載せ替え・回帰green。2026-07-18 `2248253e2`）
- [x] チャレンジツリー Topic + リスト/ツリービュー（challenge.tree + features/challenge。2026-07-18 `2248253e2`）
- [x] 進行中チャレンジ HUD（challenge.current・App常駐オーバーレイ・revision準拠。2026-07-18 `2248253e2`）
- [x] 死コード削除（`ChallengeListUI` 系2 + `UI/ChallengeList/` 空スタブ3。2026-07-18 `2248253e2`）

### C2: ポーズ・モード HUD・共通部品（`plans/phase-c2-pause-mode-common.md`）
- [x] ポーズメニュー（セーブ / メニュー復帰 / 切断表示）（pause_menu.current + features/pauseMenu。2026-07-18 C2マージ）
- [x] 設置モード HUD（選択ブロック・高さ・キー表示。3D プレビューは Unity 残置）（ui.placement_mode。2026-07-18）
- [x] 削除モード HUD + 不可理由ツールチップ（ui.delete_mode。不可理由は既存実装が意味的理由を持たないため契約のみ用意し現状空文字。2026-07-18）
- [x] 給電範囲オーバーレイの表示連携（3D は Unity 残置）（placement_mode topicに統合。2026-07-18）
- [x] 直接採掘 HUD（フォーカス・進捗）（ui.mining_hud・100msサンプリング。2026-07-18）
- [x] ツールチップ基盤（カーソル追従。3D オブジェクト由来の表示も key を Topic 連携で吸収 = WORLD-1 の表示側）（shared/tooltip + ui.tooltip。2026-07-18）
- [x] コンテキストメニュー / キー操作ヒント / クロスヘア / 全 UI 一括非表示（Ctrl+U）/ カーソル追従オーバーレイ（棚卸しのうえ処遇を記録）（ui.context_menu(ID照合Action)/ui.key_hints/ui.crosshair/ui.visibility。UICursorFollowControlは個別移植不要と判定 — grab/ContextMenuはWeb側で吸収済み・uGUIフォールバック用に残置。2026-07-18）

### C3: 列車 HUD・インベントリ（`plans/phase-c3-train-hud.md`）
- [x] 列車乗車 HUD（入れ子状態機械・乗車入力・分岐選択 → 3D プレビュー駆動）（2026-07-18 C3マージ。train.riding+ui_state.subState・入力主権C#維持。実機乗車一巡はPhase D）
- [x] 列車（貨車）インベントリ（2026-07-18。SubInventory拡張+コンテナ不在等エラー表示）

### C4: スキット・チュートリアル・カットシーン（`plans/phase-c4-skit-tutorial.md`・**再設計から**）
- [x] 既存画面への `data-tutorial-anchor` 付与の棚卸し（2026-07-18 C4a。代表アンカー+anchor registry実装〔observer群+rAF集約+ready/not-found/hidden/duplicate ack〕）
- [x] カットシーン連携（2026-07-18 C4a。game_state.current Topic+Web全レイヤ退避〔Ctrl+Uと独立〕・TimelinePlayerはUniRxイベントで依存反転）
- [x] バックグラウンドスキット（2026-07-18 C4a。skit.presentation完全snapshot・SkitPresentationStateStore・音声Unity維持・タイプライターWeb）
- [x] スキット再設計文書 → 実装（snapshot 配信 + Action 冪等化 + ボイス方式決定〔INFRA-10 の決定責務はここ〕）
      （2026-07-18 C4bマージ。S2本文/auto/skip/hidden・S3選択肢+choiceId jump欠陥修復・全Action冪等化・旧UI抑止〔BGは音声維持のため文字のみ抑止〕。ボイス方式=Unity再生統一。残: S4 Windows実機ボイス検証）
- [x] チュートリアル再設計文書 → 実装（anchor registry による DOM ハイライト。3D ピン/矢印は Unity 残置）
      （2026-07-18 C4a+C4b。anchor registry+UIHighlight→宣言state接続・key hint統合・ワールド系Unity残置。残: 実機チュートリアル進行確認）

## Phase D: カットオーバー完了（`plans/phase-d-cutover.md`）

- [x] クラフトツリー機能の削除確認（設計書 `docs/superpowers/specs/2026-07-18-craft-tree-removal-design.md` が正・先行可）（2026-07-18 `db2b3f5ba` tree3統合。CraftTreeRemovalTest 2/2合格・製品コード残存参照0）
- [x] 既存画面の i18n 変換（基盤は A5。ハードコード文字列を全て `t(key)` 化し言語切替を全画面確認）（2026-07-18 `ce18c097b`。26コンポーネント変換・lint allowlist空化・新規eslint-disable 0件・言語切替再描画vitest追加）
- [ ] 全 uGUI ビューのゲート化監査（`ui-completeness-reaudit-plan.md`〔archive〕の手順 + 状態外オーバーレイ一覧の別軸確認）
- [ ] `disposition.md` 全59項目のクローズ確認
- [ ] 最終検証: PlayMode 全画面遷移スモーク + 画面ごとの操作パリティ受け入れ表 + fault-injection（WS 切断/CEF リロード/フォーカス往復）+ Ctrl+I 実機確認 + 本番配信モードで一巡
- [ ] （任意）INFRA-4: C#→TS 型自動生成 / GameStateType Topic の一般整備

## 品質バックログ

- 設計負債解消 WU1〜9（別トラックで実行中。監査: `design-debt-audit-2026-07-17.md` / 実行計画: `subagent-execution-plan-2026-07-18.md`）。移行 Phase 中の「ついで消化」はしない — 新規負債は lint/決定論チェックで防止
- モーダル RequestModal プロデューサ配線（実ユースケース決定待ち）

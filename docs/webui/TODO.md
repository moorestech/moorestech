# Web UI — TODO / 現状インデックス

CEF 上で動く React 製ゲーム内 UI（`moorestech_web/webui` + `Client.WebUiHost`）の**入口ドキュメント**。
最新の全体像はここを見る。個別項目の詳細な受け入れ条件・監査根拠は下記「詳細台帳」を参照。

**最終更新**: 2026-07-18

---

## 方針（2026-07-18 改定）: uGUI 共存 → **Web UI ネイティブ化**

Web UI が安定したため、「uGUI 凍結・並走（D6＝パリティ到達後に一斉カットオーバー）」から
**「Web を表示の正とする全面移行（Web UI ネイティブ化）」へ方針転換**した。
純粋な平面（スクリーンスペース）UI はすべて Web で表示し、uGUI 表示は順次廃止する。
uGUI コードは撤去判断まで残置し、表示のみ `WebUiScreenGate` で抑止する（従来方式を継続）。

| 決定 | 内容 |
|---|---|
| 全面移行 | インゲームの平面 UI は Web 表示を正とする。新規 UI は Web 側のみに実装する |
| ワールド空間 UI | **uGUI のまま維持（移行対象外）**: MapObject HP バー・ブロック進捗バー・チュートリアルマップピン・電線設置ラベル等の 3D ビルボード系（FEAT-WORLD-*） |
| クラフトツリープランナー | **機能ごと削除予定のため移行対象外**（FEAT-CRAFT-4 は廃止） |
| スキット / チュートリアル | 単純移植不可。**Web 向け再設計を前提**に別途設計する（FEAT-SKIT-1/2・FEAT-TUT-1） |
| メインメニュー | **D3 維持でスコープ外**（`Client.MainMenu` 別シーンは当面 uGUI のまま） |
| デバッグ UI / エディタツール | 非出荷のため移行対象外 |

---

## ドキュメントマップ

| ファイル | 役割 |
|---|---|
| **`TODO.md`（本書）** | 現状スナップショットと残タスクの入口。まずここを読む |
| `cef-webui-migration-todo.md` | **詳細台帳**。INFRA/FEAT 全項目の網羅 TODO（2026-06-14 スナップショット）。個別項目の根拠・受け入れ条件はここ |
| `cef-webui-plan.md` | CEF + React 採用の意思決定根拠・技術比較（Servo/Ladybird/Ultralight 検討ログ） |
| `cef-webui-tree2-render-investigation-2026-07-04.md` | vite.config 残骸による描画不能バグの解決記録（再発防止の教訓） |
| `ui-completeness-reaudit-plan.md` | uGUI→Web の網羅性 再監査**手順書**（見落とし再発防止のプロセス定義。再利用可） |
| `2026-07-07-parity-audit-verification-handoff.md` | パリティ監査の裏取り結果。**要訂正5点**あり、台帳化・実装前に必読 |
| `2026-07-07-parity-implementation-plan.md` | パリティ実装の**ロードマップ**（Phase 0-6 概要） |
| `2026-07-07-block-split-unification-handoff.md` | ブロック側半分掴みのホスト計算統一（`block_inventory.split` 追加）の申し送り（**実装完了済み**・経緯記録として保管。末尾に検証記録と実装完了記録） |
| `../superpowers/plans/2026-07-07-webui-parity-phase0-2.md` | Phase 0〜2 の**実行計画**（writing-plans形式・完全コード付き）。着手はこちらから |
| `archive/2026-07-02-webui-mantine-migration.md` | Tailwind→Mantine 移行計画（**完了済み・履歴保管**） |

> 注: `cef-webui-migration-todo.md` は 2026-06-14 時点のスナップショットで、以降の実装
> （topicStore 導入・C#⇔TS ワイヤ契約単一化・`WebUiCefToggle.cs` の Ctrl+I トグル）は
> 未反映。**最新の進捗は本 TODO.md の「現状」節を正とする**。

---

## 現状スナップショット（2026-07-05）

Web UI は**中核ループ（インベントリ・クラフト・モーダル・進捗）が動作する第一段階**まで到達。
基盤（ワイヤ契約・状態管理・CEF ライフサイクル）は増設に耐える形に固まった。
個別ブロック詳細（機械/発電機/採掘機/ギア/フィルタ分岐器）と研究ツリーは移行済み（2026-07-06）。
uGUI パリティの残りの大物（チャレンジ・列車・電柱ネットワーク・スキット等）は未着手。

### 実装済み（機能）
- プレイヤーインベントリ（メインスロット + ホットバー、Grab オーバーレイ、ドラッグ/クリック操作）
- ホットバー選択（数字キー）
- ブロックインベントリ: Chest（汎用収納）/ Tank（流体）/ Generic（未登録ブロックのフォールバック汎用描画）
- 個別ブロック詳細 UI（FEAT-BLK-2/3/4/5/8・2026-07-06）: 機械 / 発電機 / 採掘機 / ギア機械 / フィルタ分岐器。capability 表示（電力・トルク・ギアネットワーク・燃料/進捗・FluidSlots）とフィルタ分岐器のモード/フィルタ設定アクションまで配線済み
- 研究ツリー（FEAT-RES-1・2026-07-06）: UIPosition 配置 + 接続線 + 研究実行。表示可否は `ui_state.current` の `ResearchTree` から導出（`research.tree` topic はノードデータのみ運ぶ）
- レシピビューア（クラフト `CraftRecipeView` / 機械 `MachineRecipeView` / アイテムリスト / ページャ）
- クラフト進捗バー（`ProgressArrow`）
- モーダル（確認ダイアログ）
- トースト通知（アクション失敗・バリデーション違反）
- 再接続オーバーレイ（切断検知で UI 全体をブロック）
- アイテムアイコン PNG / アイテムマスタ JSON の HTTP 配信
- E2E（Playwright + mock-host）: inventory / blockInventory / fluidSlot / hotbar / modal / progress / recipe / uiState / blockDetails / filterSplitter / research

### 基盤の到達点（直近コミットで整備）
- **ワイヤ契約の単一ソース化**: `bridge/protocol.ts` に Topic/ServerMsg/ClientMsg/Payload を集約。C# `WireFixtures/` を共有する `wireContract.test.ts` / `WireContractTest.cs` が C#⇔TS の型一致を両側から強制
- **状態管理の一方通行化**: zustand を `topicStore`（サーバ由来 state）/ `toastStore` / `uiStore`（クライアント UI 状態）に分離。`deliverTopicPayload` が唯一の書き込み口でバリデーション失敗は toast + 破棄
- **入力排他レイヤー** `activeLayer`（game / modal 等）で画面間の入力排他
- **CEF ライフサイクル隔離**: 起動部分失敗時ロールバック、Editor ドメインリロード/PlayMode 終了/Editor 終了フックでの確実なクリーンアップ、WS 境界の隔離
- **Ctrl+I 排他トグル** `WebUiCefToggle.cs`（uGUI/CEF 表示の重なり制御、INFRA-3 相当。※台帳未反映）
- Mantine v8 + CSS Modules への移行完了（Tailwind 依存は package.json から撤去済み）
- **INFRA-6 uGUIステートマシン・パススルー型（2026-07-06）**: uGUIの`UIStateControl`が唯一の状態源としてフル稼働（B/G/T/R/Esc 等すべて従来通り）。CEFはwebモード中**常時表示の透明オーバーレイ**（body透過 + 画面表示中のみ dim バックドロップ）。Webが置換済みのビュー（`PlayerInventoryViewController` / `RecipeViewerView`）だけ `SetActive` 内で webモードゲート。`ui_state.current` topic + `ui_state.request` action は維持。CEF RawImage の raycastTarget=0 で世界クリック貫通。PlayMode 遷移マトリクス **10/10 PASS** 検証済み（2026-07-06、`.superpowers/sdd/task-4-verification-report.md`）

---

## 残タスク

### 0. 後始末（軽微・即対応可）
- [x] `moorestech_web/webui/tailwind.config.js` / `tailwind.config.d.ts` を削除（Mantine 移行の唯一の残骸。依存・ディレクティブは既に不使用）。併せて `tsconfig.node.json` の `include` から存在しない `tailwind.config.ts` 参照を除去（2026-07-06）
- [x] 進捗の正を本 TODO.md に一本化する方針を確定。`cef-webui-migration-todo.md` 冒頭に「2026-06-14 スナップショットの詳細台帳・根拠アーカイブ」バナーを追加（最新進捗は反映しない運用に確定）（2026-07-06）

### 1. 横断インフラ（`cef-webui-migration-todo.md` INFRA-* 参照）
- [ ] **INFRA-1 CEF バイナリの恒久統合**（最優先の未解決課題）: `manifest.json` は今も `jp.juha.cefunitysample` を git URL 参照。LFS ポインタが解決されず手動 pull 回避を繰り返している。embedded package 化等の恒久対応
- [ ] **INFRA-4 C#→TS 型自動生成**: `bridge/payloadTypes.ts` は現状手書き。C# からの生成に置換
- 🟡 **INFRA-6 UIState 橋渡し（最小版済・2026-07-06）**: `ui_state.current` topic + `ui_state.request` action で UIState⇔Web を橋渡し済み。CEF表示はUIState駆動、App.tsx が state で画面ルーティング、webモード中の未対応state遷移は抑止。**GameStateType（第2状態機械）のTopic化は未着手**
- [ ] INFRA-5 アセット配信拡張 / INFRA-7 サーバーイベント push 規約 / INFRA-8 Windows・Linux 対応（`ViteProcess.cs:246` に Windows pid 特定の TODO）/ INFRA-9 本番配信堅牢性 / INFRA-10 CEF 音声専有 / INFRA-11 i18n / INFRA-12 要素 ID 規約 — いずれも未着手
- [ ] INFRA-13 CEF 堅牢性 — 一部前進（起動隔離・WS 境界隔離済み）、残りは継続

### 2. 機能移行（uGUI パリティ・大部分未着手）
ブロック系 payload は capability 詳細（機械/発電機/採掘機/ギア/フィルタ分岐器・FluidSlots・Progress）まで拡充済み（`BlockDetailDtoBuilder`）。残りのブロック種も**「実装漏れ確定 → topic 拡充 → ビュー実装」の順**を守る。

- [x] 個別ブロック UI（FEAT-BLK-2/3/4/5/8）: 発電機 / 機械 / 採掘機 / ギア系（`GearEnergyTransformerUIView`）/ フィルタ分岐器（2026-07-06）※ギア**伝達**系5ブロック（blockType: Shaft/Gear/GearChainPole）はレジストリ未登録で Generic 落ち、ElectricToGearGenerator も未対応 — 2a 参照
- [ ] **個別ブロック UI（残り）**: 電柱ネットワーク情報（`ElectricPoleNetworkInfoUIView`）/ 列車 PF / ベースキャンプ
- [x] 研究ツリー（FEAT-RES-1, `ResearchTreeView`）（2026-07-06）※報酬アイテムの個数表示は保留（ワイヤ型が個数を未伝搬・要 C# 変更）
- [ ] **チャレンジ / 実績**（FEAT-CHAL-1/2, `ChallengeListUI` / `CurrentChallengeHudView`）
- [ ] **列車 UI 一式**（FEAT-TRAIN-1, `TrainInventoryView` / 各 PF インベントリ / `TrainHUDScreen`）
- [ ] 長押しクラフト仕上げ（FEAT-CRAFT-1）※クラフトツリー（FEAT-CRAFT-4）は機能ごと削除予定のため対象外（2026-07-18 方針）
- [ ] モード系 HUD（FEAT-MODE-1〜5: 設置 / 削除 / デバッグ / 給電範囲 / 直接採掘）
- [ ] 共通部品（FEAT-COM-1 コンテキストメニュー / COM-4 キーヒント / COM-6 全 UI 一括非表示 / COM-7 カーソル追従オーバーレイ）
- ワールド系（FEAT-WORLD-1 3D ツールチップ / WORLD-2 HP バー / WORLD-3 マップ）→ **ワールド空間 UI は uGUI 維持で対象外**（2026-07-18 方針。WORLD-3 はマップ UI 実体なし・調査のみ）
- [ ] チュートリアル（FEAT-TUT-1）/ スキット（FEAT-SKIT-1/2）※**単純移植不可・Web 向け再設計が必要**（2026-07-18 方針）/ カットシーン（FEAT-CUT-1）
- [ ] ポーズメニュー（FEAT-SYS-1）※メインメニュー SYS-2/3 は D3 決定でスコープ外

### 2a. 操作・表示パリティ台帳（2026-07-07 統合。裏取り済み監査＋種リストの一本化。台帳はここが唯一の正）
> 出典: `2026-07-07-parity-audit-verification-handoff.md`（**要訂正5点を反映済み**）と
> `2026-07-06-all-code-review-progress.md` の種リスト。個別の証拠ファイルパスは申し送りを参照。
> 画面カバレッジ: uGUI `UIStateEnum` 11 ステート中 web 対応は 3＋GameScreen（残り7画面は「2. 機能移行」の大物画面参照）。

**ブロックインベントリ操作（優先度1 → 実行計画 `../superpowers/plans/2026-07-07-webui-parity-phase0-2.md`）**
- [x] ブロックスロット右クリック（空手: 半分取り(切り捨て) / grab保持: 1個置き）（2026-07-07・e2e検証済み）
- [x] ブロックスロットダブルクリック収集（C# `block_inventory.collect` 新設済み・`BlockAreaSlotParser` 共通化）（2026-07-07）
- [x] Shift直接移動の SubInventory 対応（main/hotbar→block、block→main。`planDirectMoves` で uGUI 準拠の複数スタック配分）（2026-07-07）
- [x] blockInventory e2e のジェスチャ網羅（`blockInventoryGestures.spec.ts` 5ケース追加、e2e計39件）（2026-07-07）
- [x] Esc でのブロックUIクローズ: uGUI `SubInventoryState` 経由で動作確認済み（2026-07-07 PlayModeスモーク。webモード有効=`WebUiScreenGate.IsWebUiMode: True` のまま `QueueStateEvent` の Esc 注入で SubInventory→GameScreen 遷移を実測、クローズ後の GameScreen キーヒント表示もスクショ確認）

**ギア系・個別ブロック（優先度2）**
- [ ] ギア伝達系5ブロックが Generic 落ち: レジストリへ **Shaft / Gear / GearChainPole** を登録（⚠「GearEnergyTransformer」という blockType は存在しない。実装時は v8 blocks.json で `blockUIAddressablesPath: "Vanilla/UI/Block/GearEnergyTransformerUI"` を持つ blockType を再列挙して確定）
- [ ] ElectricToGearGenerator 専用ビュー＋出力モード選択（v8 に1ブロック実在。uGUI: `ElectricToGearGeneratorBlockInventoryView`）

**プレイヤーインベントリ（優先度4。⚠右クリック半分/1個置き/ダブルクリック収集は実装済み — 欠けはドラッグ系のみ）**
- [ ] スプリットドラッグ（grab の複数スロット均等配分）
- [ ] 右ドラッグ連続1個配置

**クラフト・表示系（優先度5）**
- [ ] CraftRecipeView に所持数/必要数の数値テキスト（不足赤字）とツールチップ内訳（⚠40%透過減光は実装済み。無いのはこの粒度のみ）
- [ ] アイテムリストのクラフト可能数バッジ/グレーアウト（`craftLogic.craftable()` はボタン活性にのみ使用中）
- [ ] 機械詳細の分間生産数表示（`details/MachineSection.tsx` は進捗＋電力率のみ）
- [ ] ホイールのホットバー切替を入力量累積に（現状 deltaY の符号のみ＝±1固定。uGUI は入力量累積）
- [ ] クラフト長押し・連続クラフト（FEAT-CRAFT-1 既存記載の再掲）

**品質フォロー（種リスト由来）**
- [x] ブロック側「半分掴み」のホスト計算統一: C# に `block_inventory.split` を追加し `blockSlotPlan.ts` のクライアント床計算を廃止（2026-07-07 実装完了。unit/e2e/C#契約テスト全パス。経緯は `2026-07-07-block-split-unification-handoff.md`）
- [ ] `ui_state.request` が現 state を問わず受理される（Story/PauseMenu 中の遅延要求で強制遷移し得る。ホワイトリスト検討）
- [ ] itemMaster の WS 再接続後リフレッシュ（外部ブラウザ開発フロー限定の実害。※初回ロード失敗の永続 stale は 2026-07-07 の zustand ストア化＋3秒自動リトライで解消済み — `bridge/store/itemMasterStore.ts`。残るは「一度ロード成功した後の再接続で再取得しない」のみ）
- [ ] crafting 系 validator の深掘り（壊れ payload での React クラッシュ耐性。all-code-review で見送り分）

**低優先・記録のみ**
- BaseCamp 完成ボタン: v8 マスタに BaseCamp ブロックは**0個**のため現行コンテンツでは発生しない。マスタに実体が出たら着手
- 研究ノードの UIScale 未反映（未検証・低影響）
- クラフト不可時のカーソルツールチップ（web にツールチップ基盤自体が無い可能性が高い・未検証）
- TankInventory は意図的温存（`blockLogic.test.ts` に意図明記済み）。**整理不要 — タスク化しないこと**

### 3. 検証（未検証で残る実機挙動）
- [ ] PlayMode で Ctrl+I トグルの実機目視確認（`unity-playmode-recorded-playtest` で録画可）
- [ ] INFRA-1 解消後の 実機 web↔host 連携検証（現状の保証は mock-host 相手の e2e + 録画 + コンパイルまで）。補足（2026-07-07）: CEFパネルへの `InputSystem.QueueStateEvent` 注入は不可（webUI は localhost の web アプリで Unity 入力系を経由しない）ため、実機ジェスチャ検証はブラウザ/E2E 側で行う
- [x] webモードの実機遷移確認（Tab開閉・ブロックインタラクト・✕ボタン）: **PlayMode 遷移マトリクス 10/10 PASS**（2026-07-06、レポート `.superpowers/sdd/task-4-verification-report.md`）
- [x] InitializeScenePipeline 分割後の PlayMode 起動スモーク（2026-07-07、ブロック操作パリティ検証と同時実施。MainGame 到達・WebUiHost 起動・webMode=True・新規アプリエラーなし）
  - 環境注意: 共有 `../moorestech_master` が `plan2-master-migration`（placeSystem 新形式）だと本ブランチはマスタロードで起動不能。`master` ブランチの worktree `../moorestech_master-webui-smoke` を作成し `DebugServerDirectory` で指定済み（`cache/StringDebugParameters.json` に永続）
  - 既存問題: MainGame シーンの `MapObjectGameObjectDatastore.mapObjects` に null 1216件があり起動時に LogError バースト（`moorestech/MapExportAndSetting` 再実行で解消見込み。パイプライン分割とは無関係）

#### 既知の制限
- **入力の二重配送**: Web パネルのクリックが uGUI / 3D にも届く。実害は限定的だが恒久対応は INFRA-2（入力排他の一元化）で対処予定
- **Vite dev server 死活検知なし**: dev server 停止時の検知・復旧が未実装（INFRA-13 系フォローアップ）
- **モーダル: RequestModal のプロデューサ未配線（実ユースケース決定待ち）**

### 4. 検討事項（`cef-webui-plan.md` 由来・台帳未収録）
- [ ] Ultralight 等 軽量代替レンダラーの試用・抽象化レイヤー要件抽出
- [ ] CEF リファクタ（ブラウザ差し替え可能な抽象化）

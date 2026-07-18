# moorestech Web UI 移行 完全 TODO リスト

> ⚠️ **本書は 2026-06-14 スナップショットの詳細台帳（履歴・根拠アーカイブ）です。**
> 進捗の正（最新の現状・残タスク）は `docs/webui/TODO.md` を参照してください。
> **方針改定（2026-07-18）**: 「uGUI 凍結・共存 → 一斉カットオーバー（D6）」は
> **「Web UI ネイティブ化（Web 表示を正とし uGUI 表示を順次廃止）」へ転換**。
> D3（メインメニュー除外）は維持。本書 §4 の D6 記載は旧方針。最新方針は `TODO.md` 冒頭を参照。
> 以降の実装（topicStore 導入・C#⇔TS ワイヤ契約単一化 `bridge/protocol.ts`・
> `WebUiCefToggle.cs` の Ctrl+I トグル＝INFRA-3 相当・Tailwind→Mantine 移行完了）は
> 本書には未反映です。個別項目の受け入れ条件・監査根拠を参照する用途にのみ使ってください。

**親計画**: `docs/webui/cef-webui-plan.md`（CEF + React + Tailwind + TS への UI 刷新）
**このドキュメントの目的**: 現行 uGUI が持つ**すべての UI 機能**を洗い出し、Web (CEF) への移行に必要な作業を漏れなく TODO 化する。
**作成日**: 2026-06-14 / multi-lens-review-loop（網羅性・事実正確性・アーキ実現性・順序依存・リスク見積 + codex 外部監査）で収束まで洗練。
**網羅性 再監査(2026-06-14)**: `docs/webui/ui-completeness-reaudit-plan.md` の手順で全ソースルートの **407 `.cs` + 32 UI 資産を全件 triage**（並列8エージェント、件数突合済み）。発覚した追加項目: INFRA-6 の第2状態機械 `GameStateType`、FEAT-COM-6(UIRoot 全UIトグル)、FEAT-COM-7(カーソル追従)、FEAT-CUT-1(カットシーン)、MODE-3 dev群拡充、BLK-5/6/7・WORLD-1 の責務是正、ChallengeList 死コード精査。詳細は §5 末尾。

**実装セッション(2026-06-14, 実行計画は完了済みのため削除)**: subagent-driven + 並列wave方式で 5 機能を実装。**Web 側**(React/uGUI準拠レイアウト)は vitest 単体 46 + Playwright e2e 20 全 green、各機能の動作動画を `docs/webui-feature-videos-2026-06-14/` に収録。**C# ホスト側**(Topic/Action)は `uloop compile` ErrorCount 0 で通過、uGUI へは additive な getter/setter/event のみ追加(凍結方針維持)。**実機 web↔host 連携検証は INFRA-1(CEF 破損)解消待ち** — 現状の動作保証は mock host 相手の e2e + 録画 + コンパイルまで。
- FEAT-INV-2(ホットバー選択)・FEAT-COM-2(モーダル)・FEAT-COM-3(プログレスバー)・FEAT-INV-6(液体スロット/ProgressArrow)・FEAT-INV-4+BLK-1(SubInventory 土台+チェスト) = Web 実装 + host Topic/Action 実装済(上記の検証範囲)。
- 追加 host: `inventory.select_hotbar`/`ui.modal`(+`WebUiModalService`)/`ui.modal.respond`/`ui.progress`/`block_inventory.current`/`block_inventory.move_item`、`InventoryTopic.selectedHotbar`。

---

## 0. 現状サマリ（2026-06-14 時点）

### 完成している基盤（MVP）
- **C# ブリッジ (`Client.WebUiHost`)**: Kestrel(ASP.NET Core) を `127.0.0.1:5050` で起動、`/ws` で WebSocket、`/api/icons/{id}.png`・`/api/master/items` を配信。Vite dev server を `5173` で自動起動。起動は `InitializeScenePipeline` 最序盤(`StartAsync`)、Topic/Action 登録は `MainGameSceneLoaded` で `WebUiGameBinder.Bind()`（DI/ClientContext 確定後）。
- **Topic (4)**: `local_player.inventory` / `crafting.recipes` / `crafting.machine_recipes` / `recipe_viewer.item_list`。
- **Action (6)**: `inventory.move_item` / `inventory.split` / `inventory.collect` / `inventory.sort` / `craft.execute` / `debug.echo`。
- **Web フロント (`moorestech_web/webui`)**: React18 + Tailwind + Zustand。インベントリ(main 36 + hotbar 9 + grab、クリック操作)、レシピビューア(クラフト/機械タブ + `RecipePager`)、アイテムリスト(`ItemListPanel`)、トースト(`ToastHost`)。vitest + Playwright(モック WS host) でロジック固定済み。

### 通信プロトコルの形（既存）
- Server→Client: `{op:"snapshot"|"event", topic, data}`
- Client→Server 購読: `{op:"subscribe"|"unsubscribe", topics:string[]}` / `{op:"snapshot", topic:string}`
- Client→Server 操作: `{op:"action", type, requestId, payload}` → 応答 `{op:"result", requestId, ok, error?}`
- Topic は「購読 → snapshot 配信 → 以後 event で全量配信」。Action は requestId 相関の RPC。

### まだ無い横断基盤（移行前提タスク）
- C# → TS 型自動生成（payloadTypes は手書き）。
- CEF パッケージの実バイナリ統合（**UPM git + LFS でポインタしか入らず初期化失敗**＝最優先恒久対応）。
- 入力パススルー（マウス/キーボード/IME を CEF へ）と、uGUI との重なり/フォーカス制御。
- **i18n（多言語）配信基盤**（親計画の最大動機なのに未着手。現行は `Client.Localization` + `localization.csv` 駆動）。
- ワールド 3D 空間に紐づく UI（ツールチップ 3D ターゲット、HP バー、設置プレビュー、給電範囲、採掘フォーカス）の Web 側での扱い方針。

---

## 1. 横断インフラ TODO（全機能移行の前提）

> これらは個別 UI 機能より優先。未解決だと各機能の移行が成立しない。各 INFRA にも complexity(S/M/L/XL) を付与（横断インフラの重さを見積に乗せる）。
> **注**: 後続 FEAT のブロッカーは INFRA-1〜7・11・12。INFRA-9(本番配信)・13(堅牢性)は dev が Vite のため FEAT 開発の前提ではなく、Phase C 末〜任意後フェーズで可。

### INFRA-1: CEF パッケージのバイナリ恒久統合 **【最優先・ハードゲート / complexity: L】**
- 症状: `jp.juha.cefunitysample`（UPM git）が Git LFS を解決せず、`libcef_unity_rust.dylib` 等が 131B のポインタになり `DllNotFoundException`。新規環境で必ず初期化失敗。
- 選択肢: (a) パッケージ側で LFS をやめる / (b) バイナリ別配布＋ローカルコピー手順の自動化 / (c) embedded package 化。
- 完了条件: clean clone → Unity 起動だけで CEF が描画初期化に成功する（手動 LFS pull 不要）。
- **依存ゲート**: 本項目の完了は **INFRA-2/3/6 の着手前提**（描画が成功した CEF 上でしか入力・表示・状態橋渡しを検証できない）。Windows/Linux バイナリも同じ配布経路（INFRA-8）。

### INFRA-2: 入力パススルー（マウス/キーボード/IME → CEF） **【complexity: L】**
- 現状スモークは表示のみ。クリック/ドラッグ/キー/スクロール/IME を CEF へ送る経路が未検証。
- 設計判断: どの UIState のときに入力を CEF が専有し、どのときゲーム(カメラ/プレイヤー)に通すか。Web 側 focus と Unity 入力の排他。**IME はテキスト入力 UI(セーブ名/サーバー接続/検索)に必須**。
- 完了条件: Web インベントリでクリック・右クリック・ダブルクリック・ホイール・文字入力が動作。**フォーカス往復(Alt-Tab/別ウィンドウ復帰)後も入力が復活する**。
- 依存: INFRA-1。

### INFRA-3: uGUI/ゲーム画面との重なり・透過・部分表示制御 **【complexity: M】**
- `RawImage` が全面を覆う。半透明背景化は対応済み（コミット b4727f28c）だが、「Web UI を出す/隠す」「画面の一部だけ Web」をゲーム状態に同期する仕組みが必要。
- UIState（INFRA-6）の遷移と Web の表示状態を一本化する設計。
- **再監査補足**: 現行 uGUI は `Client.Game/Common/UIRaycastTarget`（`Graphic` 派生・描画レスの raycast 判定専用部品）で透過/クリックブロックを制御している。Web 移行後も残置 uGUI レイヤとの raycast 整合（CEF 面と uGUI のどちらが入力を取るか）でこれが論点になる。
- 完了条件: インベントリを開閉すると Web パネルが出入りし、ゲーム画面が背後に見える。
- 依存: INFRA-1、INFRA-6（D2 確定後）。

### INFRA-4: C# → TS 型自動生成 SourceGenerator（二段構成） **【complexity: M】**
- `payloadTypes.ts` を手書きから脱却。Topic/Action の payload DTO・enum・マスタ型を C# から TS へ生成。Topic 名/Action 名の定数も型安全に共有。
- **二段に分割**: (a) **生成機構(generator)を Phase C で構築**、(b) **各 payload への生成適用は各 FEAT 実装時に内包**（Phase D/E で DTO が大量新設されるため、機構だけ先行しても既存4Topic/6Actionでしか検証できない＝鶏卵）。
- 完了条件(機構): C# 側 DTO 変更が TS のコンパイルエラーとして検出される枠組みが既存4Topicで動く。
- 既存 Mooresmaster 自動生成と同じ思想。モック host(Playwright)のフィクスチャ型源泉にも転用。

### INFRA-5: アセット配信の拡張方針 **【Phase C で必要 / complexity: M】**
- 現状アイテムアイコンのみ PNG 配信。ブロックアイコン・ツリーノードアイコン・**キャラ立ち絵(スキット)**・研究/チャレンジアイコン等が必要。**画像を要する研究・チャレンジ・スキットより前に完了が必須**。
- Addressables を Web から直接引けない問題を ASP.NET プロキシ規約で吸収。アイコン以外の汎用アセット配信エンドポイント設計。
- 完了条件: 任意のゲームアセット(画像)を URL 規約で Web から取得できる。

### INFRA-6: UIState ↔ Web ルーティングの橋渡し基盤 **【complexity: M】**
- uGUI は `UIStateEnum`(11状態) + `UIStateDictionary` の状態機械を `UIStateControl.Update()` が回し `OnStateChanged` を発火（C# 主権が既存実装）。
- 「現在の UI 状態」Topic（`OnStateChanged` を Topic 化）と「状態遷移要求」Action を新設し、Web のルーティング(表示切替)を駆動。
- **重要**: 状態遷移 Action は任意遷移を許さない。**「許可済み操作 intent」（例: インベントリを開く要求）に限定し、遷移コンテキスト/各 `IUIState` の条件判定は C# 状態機械が検証**してから遷移する（迂回防止）。
- **【再監査で発覚・重要】第2の状態機械 `GameStateType`**: `Client.Game/Common/GameStateController.ChangeState(GameStateType)` が `UIStateEnum` とは**別系統**の状態機械（`InGame`/`Skit`/`CutScene`）を持ち、突入時に `HotBarView`・`CurrentChallengeHudView`・`PlayerObjectController` を一括 `SetActive` で退避/復帰する。INFRA-6 は UIStateEnum だけでなく**この GameState 軸も Topic 化**し、Skit/CutScene 突入時の HUD 一括退避を Web に同期する必要がある（見落とすと Skit/カットシーン中に HUD が残る）。
- 完了条件: C# の UIState **および GameStateType** 変化が Web に伝わり、対応パネル表示/HUD 退避が起きる。これが INFRA-3 の上位設計。
- 依存: **D2 確定**（C# 主権で確定見込み＝下記§4）。

### INFRA-7: サーバー起因イベント push 規約（`*EventPacket` 中継専用） **【complexity: M】**
- 役割を限定: **サーバー push される `*EventPacket`（強制降車 `RidingStateEventPacket`、チャレンジ完了 `CompletedChallengeEventPacket`、アンロック `UnlockedEventPacket` 等）を Web に流す event topic 規約の一般化**。
- ※ ブロック稼働率/進捗などの `*BlockStateDetail` は**別物**（既に client の `BlockGameObject._blockStateMessagePack` に push 済み。これを republish する Topic で足り、サーバー改修不要）。混同しないこと。
- 完了条件: サーバーイベント → 該当 Web UI のリアルタイム反映パターンが確立。**全 Topic は「フレーム末バッチ or 変化時のみ publish」をデバウンス規約として強制**（InventoryTopic の PostLateUpdate デバウンスを横断規約に格上げ。毎tick全量配信で WS を溢れさせない）。

### INFRA-8: Windows / Linux 対応 **【complexity: L】**
- CEF バイナリ、`ViteProcess.KillAnyLingering` の port resolver(Windows 未実装/`return 0` TODO netstat)、`WebUiPaths` のプラットフォーム分岐は用意済みだが未検証。
- 完了条件: Windows で CEF 描画 + Vite 起動 + 入力が動作。Linux は余裕があれば。

### INFRA-9: 本番ビルド時の Web 配信 + 配布堅牢性 **【complexity: L】**
- 現状 dev は Vite `5173`。本番(Steam 配布)では静的ビルド成果物を Kestrel から配信する必要がある。
- **配布リスク（実コードで未対応）**: Kestrel ポートは `KestrelServer.cs` のハードコード `5050`（動的フォールバック無し）、Windows port resolver は `ViteProcess.cs` が TODO 未実装。Steam で**多重起動・他アプリのポート占有時に Kestrel バインド失敗 → UI 全死**。CEF サブプロセス実行ファイルの**署名(Gatekeeper/SmartScreen)** も親計画は「Steam 配布なら許容」と楽観のまま未タスク化。
- 完了条件: Editor 外のスタンドアロンビルドで表示。**ポート動的割当(0番ポート→実ポートを CEF/Web に伝達)・多重起動検知・配布バイナリ署名**を満たす。

### INFRA-10: CEF オーディオデバイス専有への対処
- CEF が既定オーディオデバイスを占有しゲーム音とミックスできない課題（親計画タスクA）。**スキット(FEAT-SKIT-1)のボイス再生と直撃**するため「Web UI が音を出さない」前提に乗せられない。
- 完了条件: ゲーム音声と CEF 音声の共存 or 音声経路の明確な分離。

### INFRA-11: i18n（多言語）配信 + Web ローカライズ基盤 + フォント/IME **【親計画の最大動機 / complexity: L】**
- 現行は `Client.Localization`（`Localize.cs` / `TextMeshProLocalize.cs`）が `localization.csv` 駆動で `Localize.Get(key)` + `OnLanguageChanged` 反応切替、言語選択は `Client.MainMenu/LanguageSetting`。
- 必要: CSV→Web へのキー/言語データ配信規約、Web 側 i18n ランタイム、言語切替 Action/Topic、**CJK グリフを含むフォント供給**、IME(INFRA-2 と連携)。
- **全テキスト表示 FEAT が依存**（ハードコード文言を作らない前提を最初に敷く）。
- 完了条件: 言語切替で全 Web UI 文言が切り替わる。

### INFRA-12: Web UI 要素 ID 規約（チュートリアル・横断ハイライト基盤） **【complexity: L】**
- 現行チュートリアルは `FindObjectsOfType<UIHighlightTutorialTargetObject>()` で Unity 階層を走査し `HighlightObjectId` 一致で RectTransform をハイライト（`UIHighlightTutorialManager`/`UIHighlightTutorialView`）。**Web 移行後は対象 UI が CEF 内 DOM に移り Unity から走査不能**。
- 必要: 全 Web UI 要素に安定 ID を払い出し、C# のチュートリアル進行が Web の特定 DOM をハイライトさせる横断プロトコル。
- **二段構成（INFRA-4 と対称）**: 規約・払い出し機構は Phase C、各要素への ID 付与は各 FEAT 実装内。
- **全 FEAT 横断で先に敷く**（後付けだと全コンポーネント改修）。FEAT-TUT-1 の前提。

### INFRA-13: CEF ランタイム堅牢性（プロセス死活・クラッシュ復帰） **【complexity: L】**
- CEF は「ゲーム + CEF」2プロセス構成。**Vite/CEF サブプロセスとインプロセス Kestrel(`IWebHost`)** の死活監視・クラッシュ復帰(白画面化対策)・DevTools 開発時アクセス手段・複数ウィンドウ/モーダルの z 制御が必要。
- 親計画既知リスク「Mac の 7ms 同期待ち(GPU 同期境界疑い)」の性能監視もここで扱う。
- 完了条件: レンダラープロセス異常からの自動復帰、開発時 DevTools 到達。**復帰後は WS 再接続 + 全購読 Topic の再 snapshot + クライアント状態(grab 等 Zustand store)の復元**（DOM 復活だけでは「空データ画面」になるため）。

---

## 2. UI 機能別 移行 TODO

各機能に **状態(Status)** を付与: ✅**済**(Web 実装あり) / 🟡**部分** / ⬜**未**。complexity は移植難度（S/M/L/XL）。

### 2.1 インベントリ系

#### FEAT-INV-1 プレイヤーインベントリ（メイン36 + grab） ✅済 / 仕上げ
- 現行: `PlayerInventoryViewController`。Web: `InventoryPanel`。
- 残: ソート/分割/集約の細かい挙動差、**ドラッグ&ドロップの本物のドラッグ表現**(現状クリック移動、D5 と紐付け)、右クリック分割の量指定。
- complexity: S（仕上げのみ）。INFRA-2/3 直後に着手可（BLK-* 非依存）。

#### FEAT-INV-2 ホットバー 🟡部分 / 要追加実装
- 現行: `HotBarView`(1-9キー、ホイール、ハンドグラブ3Dモデル表示、**選択中スロットのハイライト**)。Web: 下段9スロット表示のみ。
- **重要差分**: `InventoryTopic` は main/hotbar/grab の全量を配信するが**選択 index を配信していない** → Web に選択表示も選択操作も無い＝「済」でなく「部分」。
- 残: 選択 index の Topic 配信 + Web 選択ハイライト、1-9 キー/ホイール選択の入力連携(INFRA-2)。**選択中アイテムの 3D ハンドモデル表示はゲーム世界側の責務**（C# 側で選択 index を購読して継続）。
- complexity: S

#### FEAT-INV-3 アイテムスロット共通・ツールチップ ✅済 / 仕上げ
- 現行: `ItemSlotView` + `MouseCursorTooltip`。Web: `ItemSlot`(hover tooltip = item name)。
- 残: アイテム説明・詳細(ブロック性能等)のリッチツールチップ。多言語対応(INFRA-11)。
- complexity: S

#### FEAT-INV-4 ブロックインベントリ（サブインベントリ汎用） ⬜未 **【大物・BLK-* の土台】**
- 現行: `SubInventoryState` + `ISubInventorySource/ISubInventoryView`。ブロック種別の master `BlockUIAddressablesPath` で Unity prefab を動的ロードし、各 view が `BlockGameObject.GetStateDetail<T>()` で型付き state detail を読む。
- **開く動線**: ワールド判定・UIState 遷移は C# が主権（`GameScreenSubInventoryInteractService`）。Web からの「開く Action」は作らず、**C# 遷移後に対象ブロック情報を Topic 配信**する方式（既存思想と整合）。
- **種別解決**: prefab 動的ロードは Web で無意味。**master の種別キー（`BlockUIAddressablesPath` 相当）を Topic に含め、Web 側は「識別子→React コンポーネント」の静的マップで解決**（動的ロードの一般化先）。
- 各ブロック派生 UI（下記 FEAT-BLK-*）の**土台**。
- complexity: L

#### FEAT-INV-5 列車（貨車）インベントリ ⬜未
- 現行: `TrainInventoryView`。**汎用貨車スロット + コンテナ不在等のエラー状態表示**（「積込/卸し」専用ではない）。SubInventory の派生。
- complexity: M（INV-4 後）

#### FEAT-INV-6 液体スロット共通部品（FluidSlotView） ⬜未
- 現行: `FluidSlotView`(液体アイコン/量/ツールチップ) + `ProgressArrowView`。Machine / TrainFluidPlatform 等で共有される横断部品。
- アイテムスロット(ItemSlot)と並ぶ Web 共通コンポーネント化が必要。
- complexity: S（BLK-3/BLK-9-fluid の前提）

### 2.2 ブロック別インベントリ UI（SubInventory 派生・各々固有ロジック）

> 各 view の state detail は**既存 client の `_blockStateMessagePack` を Topic 化するだけ（サーバー改修不要）**。INFRA-7 のデバウンス規約に従う。FEAT-INV-4 が前提。

#### FEAT-BLK-1 チェスト ⬜未 — 基本スロットのみ。complexity: S
#### FEAT-BLK-2 発電機(Generator) ⬜未 — 燃料スロット + 稼働率 + 残燃料プログレス。complexity: M
#### FEAT-BLK-3 機械(Machine) ⬜未 — 製作進捗 + 固体/液体スロット。`FluidMachineInventoryStateDetail` 含む(FEAT-INV-6 前提)。complexity: M
#### FEAT-BLK-4 採掘機(Miner / GearMiner) ⬜未 — **出力スロット(OutputItemSlotCount 個)・採掘進捗・電力率(PowerRate/RequestPower)** を表示(`MinerBlockInventoryView`)。採掘進捗だけでない。complexity: M
#### FEAT-BLK-5 ギア機械(GearMachine) ⬜未 — **Machine(進捗+固体/液体スロット)を継承**した派生(`GearMachineBlockInventoryView`)。その上にギア出力 + トルク/RPM + ネットワーク状態を重ねる。**FEAT-BLK-3(Machine)土台依存**。complexity: M
#### FEAT-BLK-6 ギアエネルギー変換器(GearEnergyTransformer) ⬜未 — ブロック名 + トルク/RPM + ギアネットワーク集約情報(`GetGearNetworkInfo` は都度 request-response RPC) + **停止理由テキスト(パワー不足/ロック等の `GetStopReasonText`)**。complexity: M
> **BLK-5/6 性能注記**: トルク/RPM は連続変動値で、INFRA-7 の「変化時のみ publish」が実質毎tick発火し得る。連続変動値は**固定間隔(例: 4tick)サンプリングで publish** し、ギアネットワーク全走査の高頻度実行を避ける。
#### FEAT-BLK-7 電力→ギア変換器(ElectricToGearGenerator) ⬜未 — **ルートView(`ElectricToGearGeneratorBlockInventoryView`)**: 出力モード行(`ElectricToGearOutputModeRowView`)動的生成 + **充足率Slider + 消費電力テキスト + モード選択送信(`SetElectricToGearOutputMode`)** + StateDetail同期。モード行だけでない。complexity: M
#### FEAT-BLK-8 フィルタ分岐器(FilterSplitter) ⬜未 **【複雑】** — 方向ごとカラム(`FilterSplitterDirectionColumnView`)動的生成 + モードサイクル + フィルタスロット左右クリック + 専用 `FilterSplitterStateProtocol`(Get/SetMode/SetFilterItem の3 request)を Web Action 化 + 全 UniTask 非同期(キャンセルガード)。complexity: L
#### FEAT-BLK-9 列車プラットフォーム(item / fluid 両版) ⬜未 — `TrainPlatformBlockInventoryViewBase` 派生に **item 版(`TrainItemPlatformBlockInventoryView`) と fluid 版(`TrainFluidPlatformBlockInventoryView`、容量表示)** の2レイアウト。`TrainPlatformTransferStateDetail`。**積込/卸しモード表示 + `SetTrainPlatformTransferMode` 切替操作**（モード状態 Topic + 切替 Action）も移行対象。complexity: M
#### FEAT-BLK-10 ベースキャンプ(BaseCamp) ⬜未 — **建設 UI**: 必要素材表示 + 投入スロット + 進捗スライダー + 完成実行ボタン(`CompleteBaseCamp` API)。※スポーン地点設定ではない。complexity: M

### 2.3 クラフト/レシピ系

#### FEAT-CRAFT-1 クラフトレシピビューア 🟡部分 / 要追加実装
- 現行: `CraftInventoryView` / `RecipeViewerView` / `CraftButton`。Web: `CraftRecipeView` + `RecipePager`。素材クリックでジャンプ、Craft 可否判定済み。
- **重要差分**: 現行 uGUI は `CraftButton` が **CraftTime 分の長押し + `ProgressArrowView` 進捗表示**（`_buttonDownElapsed >= _currentCraftTime` で確定）。Web は即時ワンクリック送信。**長押し時間・進捗・キャンセル挙動が未移植**＝「済」でなく「部分」。
- 残: 長押しクラフト + 進捗 + キャンセル、連続クラフト(個数指定)。
- complexity: M

#### FEAT-CRAFT-2 機械レシピビューア ✅済（閲覧のみ）
- Web: `MachineRecipeView`(入力→機械→出力、閲覧のみ)。仕様通り。complexity: -

#### FEAT-CRAFT-3 レシピビューア対象アイテムリスト ✅済
- 現行 `ItemListView.IsShow()` フィルタを `RecipeViewerItemListTopic` に移植済み。Web: `ItemListPanel`。complexity: -

#### FEAT-CRAFT-4 クラフトツリー（エディタ + ターゲット表示） ⬜未 **【大物・XL】**
- 現行: `CraftTree/`（8ファイル/約795行。`CraftTreeViewManager`, `CraftTreeEditorView`, `CraftTreeEditorNodeItem`, `CraftTreeList`, `CraftTreeTargetView`, `Updater`）。ツリー CRUD・選択・**ノードのドラッグ編集**・ターゲット設定・サーバー同期・内部 ContextMenu 依存。
- 必要: ツリー CRUD Action 群、ツリー状態 Topic、接続線付きツリー描画 + ドラッグ編集 UI。
- 依存: **D5(D&D 本実装)** + フォーカス制御。CHAL-1/RES-1 と**ツリー描画共通基盤を共有**。
- complexity: XL

### 2.4 進行/メタ系 UI

#### FEAT-CHAL-1 チャレンジリスト ⬜未 **【大物・L】**
- 現行: UIState が使う実体は **`ChallengeListView`**（`ChallengeListState` が保持。`MainGameStarter` が `ChallengeListView`/`ChallengeManager` を配線）。接続線付きツリー、カテゴリ要素。`T` キーで開く。
- **移行対象外の死コード(再監査で精査・要削除検討)**: `ChallengeListUI`/`ChallengeListUIElement`（旧実装2ファイル。`SetInitialChallengeState`/`UpdateUnlockState` が空で要素生成されず、DI未登録・`MainGameStarter` 未参照＝**機能していない死コード**。「空スタブ」ではなく「死んだ旧実装」）+ `UI/ChallengeList/` の `ChallengeListCategoryView`/`ChallengeListTreeView`/`ChallengeListViewManager`（**中身ゼロの空スタブ3ファイル**、どこからも未参照。grep の `ChallengeListView` ヒットは `ChallengeListViewManager` のクラス名部分文字列の誤検出）。計5ファイルは移行せず削除候補。
- 必要: チャレンジツリー Topic、カテゴリ/ノード状態、ツリー描画(接続線)。
- complexity: L（ツリー描画共通基盤を本項目で確立し RES-1/CRAFT-4 で再利用）

#### FEAT-CHAL-2 進行中チャレンジ HUD ⬜未
- 現行: `CurrentChallengeHudView`(常時表示)。`CompletedChallengeEventPacket` 購読。
- 必要: 現在チャレンジ Topic + 完了 event（**INFRA-7 前提**）。
- complexity: M

#### FEAT-RES-1 研究ツリー ⬜未 **【大物・L】**
- 現行: `ResearchTreeViewManager` / `ResearchTreeView` / `ResearchTreeElement`。`R` キー。接続線付きツリー、必要アイテム検証、`CompleteResearch(guid)` 送信。
- 必要: 研究ツリー Topic(ノード状態/可否理由)、研究実行 Action、必要アイテム強調。CHAL-1 のツリー基盤を再利用。
- complexity: L

### 2.5 ワールド操作モード系（UIState 駆動・3D 連動）

> 「画面 UI」というより**入力モード + 3D 空間オーバーレイ**。3D 部分は Unity 側が有力、HUD/判定結果は Topic 配信。**3D オーバーレイ部分のみ D1 decision が前提（Phase F）。HUD/判定結果 Topic 部分は INFRA-2/6 のみ前提で Phase D 着手可**。

#### FEAT-MODE-1 ブロック設置モード(PlaceBlock) ⬜未
- 現行: `PlaceBlockState`。1-9 選択、Q/E 高さ、B 終了、Shift+B 俯瞰カメラ、設置プレビュー(3D)。**派生**: レール接続プレビュー(`TrainRailConnectSystem`)、列車車両配置(`TrainCarPlaceSystem`)。
- Web 範囲: 選択中ブロック・高さ等の HUD 表示 + ホットバー連動。**不可理由・選択状態は Topic 配信、モード遷移は INFRA-6 の遷移 Action 経由**。3D プレビューは Unity 側。
- complexity: M（HUD は早期着手可。3D 派生のみ Phase F）

#### FEAT-MODE-2 ブロック削除モード(DeleteBar) ⬜未
- 現行: `DeleteObjectState` + `DeleteBarObject`。左クリック削除、ホバープレビュー + 不可理由ツールチップ。
- Web 範囲: 削除バー HUD + 不可理由ツールチップ。**判定は Unity、ツールチップ内容は Topic 配信**。
- complexity: M

#### FEAT-MODE-3 デバッグブロック情報(DebugBlockInfo) ⬜未（優先度低）
- 現行: `DebugBlockInfoState`。F3。ホバーでブロック情報、クリックでログ。**列車ユニットデバッグオーバーレイ(`TrainUnitDebugOverlayPresenter`、DebugParameters ゲート)、`Client.DebugSystem/DebugSheet/`(`DebugSheetController` + `ItemGetDebugSheet : DefaultDebugPageBase` 等カスタム基底ページ群)、`DebugObjectsBootstrap`(常時オーバーレイ DebugLogPopup)、`ItemSelectModal`(dev アイテム選択モーダル) 等の dev 系もここに一括内包**。
- complexity: S（dev のみ。`DebugActionButton` と同様 dev 隔離。独立 FEAT 不要）

#### FEAT-MODE-4 給電範囲オーバーレイ(EnergizedRange) ⬜未
- 現行: `DisplayEnergizedRange` + `EnergizedRangeObject`。PlaceBlock モード突入時、電気ブロックの給電範囲を 3D 表示する独立 UI。
- Web 範囲: 表示 ON/OFF・対象情報の連携のみ。3D 描画は Unity 側。
- complexity: S（FEAT-MODE-1 と一体）

#### FEAT-MODE-5 マップオブジェクト直接採掘 HUD ⬜未
- 現行: `Mining/`（`MapObjectMiningController` + Idle/Focus/Mining/MiningComplete 状態機械）。プレイヤーが手持ちツールでワールド上の `MapObject`/`MapVein`(鉱脈/採取対象)を採掘する「フォーカス→進捗→完了」。※設置済みブロックの Miner(FEAT-BLK-4)とは別物。
- Web 範囲: フォーカスハイライト連携 + 採掘進捗 HUD。判定/3D は Unity 側。
- complexity: M

### 2.6 列車システム UI

#### FEAT-TRAIN-1 列車乗車 HUD ⬜未 **【複雑・入れ子状態・L】**
- 現行: `TrainHUDScreenState` + `TrainHudScreenUIStateController`(GameScreen/PauseMenu の入れ子状態機械)。実体は `TrainRidingInputSender`(W/S=前後, A/D=分岐選択)、`TrainBranchRoutePreviewController`(分岐ルート3Dプレビュー)、乗車中ポーズメニュー。`RidingStateEventPacket` で強制降車(INFRA-7)。
- **注意**: 現行 uGUI に**速度表示や分岐候補リストの画面 HUD は実在しない**（入力送信 + 3D プレビュー + ポーズのみ）。速度/候補リスト UI を作るなら**新規機能**。
- Web 範囲: 乗車中ポーズ + (新規なら)HUD。**分岐候補は Topic、選択 index は Action で Unity に返し 3D プレビューを駆動**。乗車入力は `TrainRidingInputSender` 経路（INFRA-2 の入力排他前提）。
- **再監査補足**: 乗車開始トリガは `RideVehicleInputService`（E + 近接列車判定 → `UIStateEnum.TrainHUDScreen` 遷移要求）。この入口を INFRA-6 の遷移 Action として扱う。
- complexity: L

### 2.7 共通 UI 部品

#### FEAT-COM-1 コンテキストメニュー(右クリック) ⬜未
- 現行: `ContextMenuView` + `UGuiContextMenuTarget`。ブロック固有の動的メニュー。
- 必要: メニュー項目 Topic/Action 化。Web 汎用 ContextMenu コンポーネント。CRAFT-4 が内部依存。
- complexity: M

#### FEAT-COM-2 モーダルダイアログ ⬜未
- 現行: `ModalManager` + `OneButtonModal`(確認/エラー)。
- 必要: モーダル要求 Topic/event + 応答 Action。Web modal。
- complexity: S

#### FEAT-COM-3 プログレスバー(汎用) ⬜未
- 現行: `ProgressBarView`(製作/採掘等)。多くはブロック UI 内蔵なので個別吸収可。
- complexity: S

#### FEAT-COM-4 キーコントロール説明表示 ⬜未
- 現行: `KeyControlDescription`(状態ごとに動的キー説明)。
- 必要: 現在状態のキーヒント Topic（多言語 INFRA-11）。
- complexity: S

#### FEAT-COM-5 トースト通知 ✅済（Web 共通基盤）
- Web: `ToastHost`(3秒自動消滅、エラー抑止 allowlist)。**※対応する常時表示 uGUI トーストは実在せず、移行済み uGUI 機能ではなく Web 側の新規共通基盤**。complexity: -

#### FEAT-COM-6 全UI一括非表示トグル(UIRoot) ⬜未 **【再監査で発覚・軸C盲点】**
- 現行: `Client.Game/InGame/UI/UIState/UIRoot`。`Ctrl+U` で `CanvasGroup.alpha` を 0/1 トグルし**全 UI を一括非表示/復帰**。`UIStateEnum` のどの状態にも属さず `Update()` で常時稼働する状態外オーバーレイ制御（BackgroundSkit と同型の盲点）。スクショ/鑑賞用途。
- Web 範囲: CEF レイヤ全体の表示トグル（C# 主権でキー検知 → 表示状態 Topic）。
- complexity: S

#### FEAT-COM-7 カーソル追従UIオーバーレイ ⬜未 **【再監査で発覚】**
- 現行: `Client.Game/InGame/Control/UICursorFollowControl`(+`UICursorFollowControlRootCanvasRect`)。掴み中アイテムアイコン等を `mousePosition` へ追従配置する純 Canvas オーバーレイ（3D 非依存）。
- Web 範囲: grab 中アイテムのカーソル追従表示。**D5(D&D 本実装)と密結合**（現行 grab 表現の実体）。FEAT-INV-1 のドラッグ表現と統合検討。
- complexity: S

### 2.8 ワールド空間アンカー UI（3D 追従）

> RawImage 全面の Web では「ワールド座標に追従する UI」が原理的に困難。**Unity 側残置 or 座標投影で Web に流す**を要設計（D1）。

#### FEAT-WORLD-1 アイテム/ブロックの3Dツールチップ ⬜未（D1 依存）
- 現行: `GameObjectTooltipTarget`(3D空間オブジェクトのホバー対象) + **`GameObjectToolTipTargetController`(毎フレーム `Physics.Raycast` でホバー入退を統合管理する常時走査コントローラ)**。表示は画面UIの `MouseCursorTooltip` に key を渡すだけで、**現状 screen 座標投影は未実装**（下記方針案は実装より先行）。
- 方針案: ヒット判定(常時 Raycast 走査)は Unity 残置、表示位置を screen 座標化して Web tooltip に渡す。
- complexity: M（方針次第）

#### FEAT-WORLD-2 マップオブジェクト HP バー ⬜未（D1 依存）
- 現行: `MapObjectHpBarView`(ワールド空間 HP バー)。
- 方針案: WORLD-1 と同じ座標投影、または Unity 側残置。
- complexity: M

#### FEAT-WORLD-3 マップ機能の有無調査 ⬜要調査
- `InGame/Map/` には**ミニマップ/マップ画面は存在せず**、ワールド上の `MapObject`/`MapVein` 実体のみ。**「マップ UI」は現状無い**可能性が高い。移行対象から外すか、将来のマップ UI 要否を判断（D3 に接続）。
- complexity: 要調査（実装タスクではなく調査タスク）

### 2.9 チュートリアル系

#### FEAT-TUT-1 チュートリアル全般 ⬜未 **【横断・3D 連動・XL】**
- 現行: `Tutorial/`(約801行)。`TutorialManager` 配下に `UIHighlightTutorialManager`(UI要素ハイライト)、`KeyControlTutorialManager`、`ItemViewHighLightTutorialManager`、`BlockPlacePreviewTutorialManager`、`MapObjectPin`、`HudArrow`/`HudArrowManager`。チャレンジ進行で発火。
- **最大難所**: UI ハイライトは `FindObjectsOfType<UIHighlightTutorialTargetObject>()` で Unity 階層を走査する仕組み。Web 移行後は対象が CEF 内 DOM になり走査不能 → **INFRA-12(Web UI 要素 ID 規約)を全 FEAT 横断で先に敷くことが前提**。3D ピン/矢印は Unity 連携。
- complexity: XL（INFRA-12 前提）

### 2.10 スキット（会話/ストーリー）

#### FEAT-SKIT-1 スキット UI ⬜未 **【UI Toolkit から全面再構築・XL】**
- 現行: `Client.Skit` 全体で約36ファイル/1265行。`SkitUI.cs`(**UIDocument = UI Toolkit**、描画ガワ162行)は氷山の一角で、実体は**ストーリーコマンドインタプリタ**: `Commands/`(ShowText/Transition/Camerawork/Selection/Emote/Motion/Voice 等20+コマンド)、`Context`、`SkitCharacterAnimator`、`BlinkSystem`、`VoiceDefine`、`SkitUITools`(非表示/スキップ/オート切替)。`SkitState`(全画面ブロッキング) / `SkitFireManager`。
- Web 範囲: テキスト/選択肢の DOM 化に加え、**立ち絵・カメラ・モーション・ボイス・トランジション・スキップ/オート/非表示操作**の同期駆動を再構築。立ち絵アセット配信(INFRA-5)、**ボイス再生は CEF オーディオ専有(INFRA-10)と直撃**。
- complexity: XL

#### FEAT-SKIT-2 バックグラウンドスキット（プレイ中オーバーレイ会話） ⬜未
- 現行: `Client.Game/InGame/BackgroundSkit/BackgroundSkitManager` + `BackgroundSkitUI` + `Commands/BackgroundSkitTextCommand`。**`SkitState` に入らず GameScreen 状態のままオーバーレイ表示**（`WaitUntil(CurrentState == GameScreen)`）。
- **簡易実装で Text コマンドのみ**(キャラ名 + 本文 + ボイス。カメラ/モーション/選択肢なし) → SKIT-1 と別物で遥かに軽い。GameScreen 上の常時オーバーレイ系。
- Web 範囲: オーバーレイ会話ボックス(キャラ名/本文) + ボイス再生(INFRA-10)。発火は C# 主権で Topic/event 配信。
- complexity: M

#### FEAT-CUT-1 カットシーン(Timeline) ⬜未 **【再監査で発覚・ロードマップ空白】**
- 現行: `Client.CutScene/TimelinePlayer` + `GameStateType.CutScene`（INFRA-6 の第2状態機械）。`PlayableDirector`/Timeline でカメラ・映像を再生し、突入時に HUD を一括退避。Skit とは**別系統**。
- 状態: TODO に従来エントリ皆無（cutscene/timeline の言及 0 件だった）。映像再生主体で画面 UI 部品は薄いが、**移行方針が完全空白**。
- Web 範囲: カットシーン中の HUD 退避同期(INFRA-6 GameStateType Topic) + スキップ等の操作 UI があれば。3D/映像は Unity 残置。
- complexity: 要調査 → S/M

### 2.11 メインメニュー / システム

#### FEAT-SYS-1 ポーズメニュー ⬜未
- 現行: `PauseMenuState` + `PauseMenuStateService` + `Presenter/PauseMenu/`(SaveButton, BackToMainMenu, NetworkDisconnectPresenter)。`Esc`/`M`。
- 必要: セーブ/メニュー復帰/切断表示の Action + Topic。セーブ名入力は IME(INFRA-2/11)。
- complexity: M

#### FEAT-SYS-2 メインメニュー ⬜未（要調査・別シーン）
- 現行 `Client.MainMenu/` + `PopUp/` の**実在機能**: ローカル開始、IP/port 接続(`ConnectServer` InputField=IME 必須)、言語選択(`LanguageSetting`)、終了、接続エラー表示。**ワールド選択は未確認**。
- 別シーンのため Web 化の範囲(タイトルから Web か、インゲーム先行か)を D3 で判断。
- complexity: 要調査 → M

#### FEAT-SYS-3 設定画面 ⬜要調査
- インゲーム設定/音量/キーバインド UI は**実コードに存在しない**（grep 否定）。設定らしき所在は MainMenu の `LanguageSetting` のみ。新規作成 or 不要を D3 で判断。
- complexity: 要調査

---

## 3. 推奨フェーズ順序

> 既存 Phase A/B（インベントリ・クラフト基盤）の続き。依存を踏まえた順序。
> **方針（決定済み・§4）**: **インゲーム UI のみ先行(D3)**＝メインメニュー/設定/タイトル(`Client.MainMenu`)は当面 uGUI 残置。**全面切替(D6)**＝既存 uGUI を凍結したまま Web を Phase D〜F で育て、パリティ到達時に **Phase F.5 で一斉カットオーバー**（フラグ切替）。各 Phase の Web 実装は既定で非活性、cutover まで uGUI が現役。

### Phase C: 横断基盤の確立（ブロッカー解消）
1. **INFRA-1（CEF バイナリ統合）← 最優先・ハードゲート**（完了まで C の他項目は検証不可）
2. INFRA-2（入力パススルー）
3. **D2 確定**（UIState 主権＝C# で確定。下記§4）→ INFRA-6（UIState 橋渡し）→ INFRA-3（表示制御）
4. INFRA-5（アセット配信拡張）、INFRA-7（イベント push + デバウンス規約）
5. INFRA-11（i18n 基盤）、INFRA-12（Web UI 要素 ID 規約）← 全 FEAT 横断のため早期。**以後 Phase D/E/F/G の全テキスト/ハイライト対象 FEAT は INFRA-11/12 完了を前提**（ハードコード文言・未 ID 化での着手を禁止）。D7 方針もここで確定。
6. INFRA-4(a)（C#→TS 型生成**機構**。各 payload 適用は各 FEAT 内）
7. INFRA-9（本番ビルド配信）、INFRA-13（CEF 堅牢性）
8. **調査タスク**: SYS-2/3・WORLD-3 の所在/粒度調査 → D3 に接続
9. FEAT-INV-1〜3 仕上げ（BLK 非依存。D5=D&D 本実装と紐付け）

### Phase D: インベントリ系の完全移行
1. FEAT-INV-4（SubInventory 土台）← BLK-* の前提
2. FEAT-INV-6（液体スロット部品）
3. FEAT-BLK-1〜4（チェスト/発電機/機械/採掘機 = 頻出。state detail Topic 化）
4. FEAT-BLK-5〜10（ギア/フィルタ/列車PF/ベースキャンプ。BLK-5/6 は GearNetwork Topic 依存）
5. FEAT-COM-1〜4（コンテキストメニュー/モーダル/プログレス/キーヒント）
6. FEAT-MODE-1/2 の **HUD 部分**（3D は Phase F。インゲーム操作の実用性のため前倒し）

### Phase E: 進行・メタ系
1. FEAT-CHAL-1（チャレンジ。**ツリー描画共通基盤を確立**）
2. FEAT-RES-1（研究ツリー。基盤再利用）
3. FEAT-CRAFT-1 仕上げ（長押しクラフト）、FEAT-CRAFT-4（クラフトツリー。基盤再利用 + D5）
4. FEAT-CHAL-2（進行中 HUD。INFRA-7 前提）
5. FEAT-SYS-1（ポーズメニュー）

### Phase F: モード/ワールド連動系（3D 連携設計）
0. **D1 decision（座標投影 vs Unity 残置）を確定** ← MODE/WORLD/TUT の前提ゲート
1. FEAT-MODE-1/2 の 3D 部分、FEAT-MODE-4/5（給電範囲/採掘 HUD）
2. FEAT-WORLD-1/2（3D 追従 UI）
3. FEAT-TRAIN-1（列車 HUD）
4. FEAT-TUT-1（チュートリアル。INFRA-12 前提）

### Phase F.5: インゲーム一斉カットオーバー（D6=全面切替）
- パリティ・チェックリスト（インゲーム全 FEAT 完了）を確認 → フラグで uGUI を Web に一斉切替。
- 切替後の回帰確認、uGUI 側インゲーム UI の撤去。

### Phase G: ストーリー・システム周辺
0. INFRA-10（オーディオ専有解消。SKIT のボイス再生前にめどを付ける）← SKIT-1 の前提
1. FEAT-SKIT-1（全画面スキット再構築。INFRA-5/10 前提）、FEAT-SKIT-2（バックグラウンドスキット。Text のみで軽量、INFRA-10 前提）
2. INFRA-8（Windows/Linux）
- ※ **FEAT-SYS-2/3（メインメニュー/設定/タイトル）は D3=インゲームのみ先行により本ロードマップのスコープ外**（uGUI 残置。将来別ロードマップで Web 化を再検討）。

---

## 4. 未確定・要設計判断（移行方針を決めるべき論点）

> 各 D は、それを前提とする FEAT/Phase より**前に**決める。順序表にゲートとして反映済み。

- **D1: ワールド空間追従 UI の方式**（FEAT-WORLD-*, TUT-1, MODE-* の 3D）。座標投影で Web に流す ⇔ Unity 側残置のハイブリッド。**Phase F 入口で確定**。
- **D2: UI 状態の主権** → **C# 主権で確定見込み**（`UIStateControl.OnStateChanged` を Topic 化、遷移要求のみ Action。既存実装と完全整合）。Phase C で正式確定。
- **D3: メインメニュー/設定/タイトル/マップを Web 化対象に含めるか** → **【決定: インゲームのみ先行】**。メインメニュー/接続/言語選択/設定(`Client.MainMenu`・別シーン)は**当面 uGUI 残置**、FEAT-SYS-2/3 は本ロードマップの主スコープ外（将来別途）。インゲーム(MainGame シーン)の UI を Web 化対象とする。WORLD-3(マップ)は調査のみ継続。
- **D4: スキット(UI Toolkit) を Web 化するか、当面 UI Toolkit 残置か**（FEAT-SKIT-1。XL かつ INFRA-10 直撃のため別建て判断）。
- **D5: ドラッグ&ドロップの本実装**（HTML5 DnD か pointer events 自前か）。INV-1/CRAFT-4 が依存。現状クリック移動で代替中。
- **D6: 移行中の uGUI/Web 共存戦略** → **【決定: 全面切替（インゲームスコープ内）】**。段階的に uGUI を機能置換せず、**Phase C 基盤の上で Phase D〜F の Web 実装を（既存 uGUI を凍結したまま）並行して作り込み、インゲーム UI がパリティに達した時点で一斉カットオーバー**する。これにより「uGUI と Web の二重メンテ・境界バグ」を回避（uGUI は凍結＝改修しない、フラグで非活性のまま Web を育てる）。**カットオーバー判定基準（パリティ・チェックリスト）を別途定義**し、フリップ前に全インゲーム FEAT の完了を確認する。
- **D7: テスト戦略のスケール**（Topic/Action が数十に増える際、Playwright モック host のフィクスチャ維持と C# 実 host との乖離を、INFRA-4 の型生成を源泉にどう抑えるか）。**Phase C で方針確定（INFRA-4(a) 機構を源泉に）、各 FEAT で適用**。

---

## 5. 機能カバレッジ・チェックリスト（漏れ防止マスター）

uGUI 全 UI を **(軸A)全ソースルートの全 `.cs` 台帳 + (軸B)`UIStateEnum`(11状態) + (軸C)状態外オーバーレイ** 起点で網羅確認（`docs/webui/ui-completeness-reaudit-plan.md` の手順で 2026-06-14 再監査。母集団 407 `.cs` + 32 UI 資産を全件 triage 済み）:

**軸B（状態機械起点）:**
- [x] GameScreen（ホットバー/キーヒント/3Dツールチップ/採掘HUD）→ FEAT-INV-2, COM-4, WORLD-1, MODE-5
- [x] PlayerInventory（メイン/クラフト/レシピ/研究の親）→ FEAT-INV-1, CRAFT-1/2/3, RES-1
- [x] SubInventory（ブロック/貨車）→ FEAT-INV-4/5/6, BLK-1〜10
- [x] PauseMenu → FEAT-SYS-1
- [x] DeleteBar → FEAT-MODE-2
- [x] Story(Skit 全画面) → FEAT-SKIT-1 ／ プレイ中オーバーレイ会話 → FEAT-SKIT-2(BackgroundSkit)
- [x] PlaceBlock（給電範囲/レール/車両含む）→ FEAT-MODE-1/4
- [x] ChallengeList → FEAT-CHAL-1
- [x] ResearchTree → FEAT-RES-1
- [x] TrainHUDScreen → FEAT-TRAIN-1
- [x] Debug → FEAT-MODE-3
- [x] チュートリアル（UIハイライト/キー/ピン/矢印）→ FEAT-TUT-1（INFRA-12）
- [x] クラフトツリー → FEAT-CRAFT-4
- [x] ワールド HP バー → FEAT-WORLD-2
- [x] マップ機能 → FEAT-WORLD-3（調査: 現状 UI 無し・実コードで反証確認済み）
- [x] メインメニュー/設定/言語 → FEAT-SYS-2/3（**D3=インゲーム先行により当面スコープ外・uGUI 残置**。設定UIは LanguageSetting のみ実在=反証確認済み）

**軸C（状態を持たないオーバーレイ/常時表示/割り込み・別状態機械）:**
- [x] 進行中チャレンジHUD/ツールチップ/コンテキストメニュー/プログレス/モーダル/トースト/キーヒント → FEAT-INV-2, COM-1〜5, CHAL-2
- [x] バックグラウンドスキット（GameScreen上会話）→ FEAT-SKIT-2
- [x] **全UI一括非表示トグル(UIRoot, Ctrl+U)** → FEAT-COM-6（再監査で発覚）
- [x] **カーソル追従UIオーバーレイ(UICursorFollowControl)** → FEAT-COM-7（再監査で発覚）
- [x] **第2状態機械 GameStateType(InGame/Skit/CutScene)による HUD 一括退避** → INFRA-6 補足（再監査で発覚）
- [x] **カットシーン(Timeline, GameStateType.CutScene)** → FEAT-CUT-1（再監査で発覚・従来エントリ皆無）
- [x] dev オーバーレイ群(DebugSheet/DebugObjectsBootstrap/ItemSelectModal/列車デバッグ) → FEAT-MODE-3

**軸A（ソースルート台帳起点・横断）:**
- [x] 母集団 407 `.cs` 全件 triage（(I)UI=125 / (II)3D=他 / (III)ロジック）。兄弟ルート `Client.Game/Common`(`GameStateController`/`UIRaycastTarget`)・`Client.Localization`(`TextMeshProLocalize`)も被覆。
- [x] UI 定義資産 32件（uGUI prefab 30 + `SkitUI.uxml`/`.uss`）→ 各 (I) `.cs` ドライバに紐づけ済み。`SkitUI.uxml/uss`=FEAT-SKIT-1 の画面構造実体。
- [x] 横断: i18n/多言語 → INFRA-11 ／ Web UI 要素 ID → INFRA-12 ／ CEF 堅牢性 → INFRA-13 ／ uGUI 透過 raycast(`UIRaycastTarget`) → INFRA-3 補足
- [ ] **死コード（移行対象外・削除候補）**: `ChallengeListUI`/`ChallengeListUIElement`(旧実装)・`UI/ChallengeList/` 空スタブ3 ・`SelectionButton`(uGUI、UIToolkit 化で孤立)

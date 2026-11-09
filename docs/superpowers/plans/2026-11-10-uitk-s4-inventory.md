# UITK S4-inventory（インベントリ・装備）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/inventory` の `InventoryPanel`（持ち物パネル: 題・整理ボタン・10 列のスロット・所持品のアンカー）と `EquipmentPanel`（装備 HUD: 右下の縦列・選択枠・アンカー・掴める画面だけのクリック・ホイールでの装備の循環）を、C3-a・C3-b・C4-e の部品と口、L0-s4a のロジック、S4-alpha の `IPlayerSlotCommands`、S3-hotbar-modal の `WheelPassthroughMark` で Web と同じ見た目・同じ操作の UITK 画面にする。ホイールでの装備の循環は C# の 1 か所（State の `EquipmentWheelSelector`＋判定 `EquipmentWheelGate`）に置き、Web の未確認 FIFO と同じ規則で古いエコーに巻き戻されないようにする。L0/L1・並べ比べ（720p・2560×1080）・録画（新設 `uitk/uitk-inventory-equipment`）まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。State（`Client.Ui.State/Inventory/`）に 2 つの塊を足す。(1) `Panel/`: C4-e の持ち物の口 `IPlayerInventoryOperations` に中身の変化の合図 `OnSnapshotChanged` を足し（本番は `LocalPlayerInventory.OnItemChange`・`LocalPlayerInventoryController.OnInventoryRefreshed`・`LocalPlayerEquipment.OnSlotsOrSelectionChanged` の 3 つを束ねる。Web ホスト `InventoryTopic` と同じ源）、それを購読して `ReadSnapshot()` から不変の `PlayerInventoryViewModel`（main・装備・選択・所持品の GUID の表）を組む `PlayerInventoryPanelModel`、整理の型付きコマンド `IInventorySortCommands`。(2) `Equipment/`: 装備の選択の口 `IEquipmentSelectionPort`（本番は `LocalPlayerEquipment`）、ホイールの累積・循環・未確認 FIFO を持つ `EquipmentWheelSelector`、層・UI の上・ツールの占有で止める純関数 `EquipmentWheelGate`、画面・モーダル・ツールの占有を State の実物から読んで判定し選択へ流す `EquipmentWheelRouter`。入力は `Client.Input/Pointer/InputSystemPointerWheelStream`（`InputSystem.onEvent` のホイールのノッチを流す。Editor の更新で届いたものは Game View の外の操作なので流さない。前例 `InputSystemPointerPositionStream`）。Client.Ui（`Screens/Inventory/`）は `Panel/`（`InventoryView`・Presenter。`StageColumnsLayout.Get(StageColumn.Inv)` へ載せる [A-92]）、`Equipment/`（`EquipmentHudView`・Presenter。`UiLayer.Chrome`・`UiFamily.Viewport` へ）、`Parts/`（`SlotGesture` → `IPlayerSlotCommands` の振り分けと、スロットの中身の写し）、`Wheel/`（ホイールの流れを購読し、ノッチを A-71 と同じ 60px〈このレーンが `GameScrollView` の private 定数から Foundation の共有の定数 `WebWheelScale.PixelsPerNotch` へ上げる。E-1〉へ直し、パネルで拾った要素と `WheelPassthroughMark` から「UI の上か」を bool にしてルーターへ渡す `IInitializable`）。登録は `Client.Starter/Registration/GameUi/Screens/InventoryGameUiRegistration.cs` の 1 本（E-1 の W4 以降の置き場）と、`MainGameUiRegistration` の画面登録区画の 1 行。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`CustomStyleResolvedEvent`・`IPanel.Pick`）、Input System（`InputSystem.onEvent` の `Mouse.scroll`。`InputSettings.scrollDeltaBehavior` は既定の Uniform）、UniRx、VContainer 1.10、NUnit＋`com.unity.ui.test-framework`、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（test-name-parity）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`THEME/` = `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/`、`TSS` = `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`、`LOGS` = `~/hermes-agent/data/repos/moorestech_logs`、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`。コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1・§2・§3・§4.1〜§4.3・§5.1・§7.1 の A-45・A-52・A-57・A-58・A-70・A-92・A-109・A-110・A-142）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-inventory 行・§4.3・§5・§6.2・E-1・E-16・E-22・E-23）、先行レーンの申し送り（`LOGS/harness/uitk/C4-e/done.md`・`LOGS/harness/uitk/S4-alpha/done.md`）、S4-alpha 計画 `docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md` の「## 後続レーンへの Produces 一覧」、S3-hotbar-modal 計画 `docs/superpowers/plans/2026-11-07-uitk-s3-hotbar-modal.md` の Task 7（`WheelPassthroughMark`）と Task 5（`--hotbar-slot-frame-face` の名札付きブロック）

## Requirements

設計の出所: 実行計画 §1.4 の S4-inventory 行（完了条件の原文:「InventoryPanel・EquipmentPanel（ホイール循環を C# 1 か所へ・床トークン）・スロットへのアンカーの印付け（`TutorialAnchor.Mark`。DragGuide 自体は S2-tutorial。E-16）。写す: inventory 61（P20・C16・L25 は L0-s4a 済み）、`regression/inputLayer.spec.ts` のホイール 1 件、`regression/itemSlot.spec.ts` のツールチップ 1 件（C3-a から。bd `moorestech-albof.29`）、`commonHud/alwaysOnHudWheel.test.ts`「装備HUDは実UIのままホイールだけ素通す」（S2-hud から。bd `moorestech-albof.31`）。パネルは `StageColumnsLayout.Get(StageColumn.Inv)` へ載せる [A-92]」）。件数は実物で数え直した（下の「写すテスト」。§1.4 との差は起草メモ）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 持ち物の状態: `PlayerInventoryPanelModel` が `IPlayerInventoryOperations.OnSnapshotChanged` を購読し、変化のたびに同期で `PlayerInventoryViewModel`（main の全スロット・装備の全スロット・選択中の装備・所持品の GUID の表）を配る。`Client.WebUiHost` を参照しない | `PlayerInventoryPanelModelTest` が緑 |
| R-2 | 所持品のアンカー: 同じ ItemId を持つスロットのうち先頭（空・個数 0 は数えない。`OwnedItemCounts.FirstSlotIndexByItemId`）にだけ `AnchorIds.InventoryItem(<guid>)`（小文字）を付ける。描き直しで外れたスロットからは id のクラスと印のクラスを外す | `InventoryViewTest`（Web `InventoryPanel/index.test.ts` 2 件）が緑 |
| R-3 | 持ち物パネル: `GamePanel(inventory-panel, Default)`・題 `ui.inventory.title`・題の操作 `PanelActionButton(inventory-sort, ui.inventory.sort)`（押すと `IInventorySortCommands.Sort()`、結果は `UiCommandResultToaster.Report("inventory.sort", …)`）。本文 `SlotGrid(main-grid)`（列数は `--inventory-columns`）に `ItemSlot(main-slot-<i>)`。パネルの寸法・題のつまみ（幅 `--inventory-panel-width`・最小高 452.391px・`translate` 0.783px・`--panel-left/right` -2.22px・`--title-shift-x` -1.96px・`--title-scale-x/y` 0.919/0.924）とグリッドのつまみ（`--slot-size` 35.1776394px・`--slot-grid-gap` 7.0815392px・`--count-font-size` 12.337807px・`--filled-face-inset` 1.565749px・`--face-inset-color` rgb(50 52 67)・`--icon-pad` 1px・`--count-bottom` -1px・`--count-letter-spacing` 0.12em→12・上 12px・左 -0.549px）は Web の `GRID_STYLE` と同値 | `InventoryViewTest`（Web `inventory.spec`「接続後にインベントリが描画される」）と `InventoryGridKnobTest` が緑 |
| R-4 | 出し入れ: 持ち物パネルは `UiScreenTraitsTable.Get(screen).Grab` が真の画面だけ（Web `screenAllowsGrab(screen) && <InventoryPanel/>`）。装備 HUD は `AlwaysOnHud` が真の画面だけ（blocking スキット中も隠さない。Web は `screenShowsAlwaysOnHud` だけで決め、ホットバーと違ってスキットで隠れない）。判定は各 Presenter のローカル関数が traits を読む（前例 `PauseMenuScreenPresenter`） | `InventoryScreenVisibilityTest`（Presenter の L1。Web `App.tsx` の代表行: 持ち物は持ち物画面・研究で出て建築メニューで出ない、装備 HUD は研究で隠れ建築メニューで出る、装備のクリックは持ち物画面だけ）が緑 |
| R-5 | スロット操作: 持ち物と装備の枠の押下・右押下・ダブルクリック・左進入・右進入を `IPlayerSlotCommands`（S4-alpha）へ渡し、結果を `UiCommandResultToaster.Report(PlayerSlotCommands.CommandName, …)` へ。装備の枠は `Grab` が偽の画面では何も呼ばない（Web は `useGrabInteractive` が偽のときハンドラごと付けない）。ホバーはツールチップ（C3-b）に任せる | `InventoryPanelGestureTest`（Web `inventory.spec` 3 件）・`EquipmentHudViewTest`（Web `EquipmentPanel/index.test.ts` 3 件）・`InventoryEquipmentFlowTest`（Web `equipment.spec` 3 件）が緑 |
| R-6 | 装備 HUD: `equipment-slots`（クラス `equipment-hud`）を Viewport の Chrome 層へ。右 `--equipment-right`・下 `--equipment-bottom`（＝`--hotbar-bottom`。床トークン）・縦に並べ・間 `--equipment-slot-gap`・最小幅高 `--equipment-slot-size`・枠の大きさ `--slot-size: var(--equipment-slot-size)`。枠 `ItemSlot(equipment-slot-<i>)`、選択枠は `ItemSlotMarks.selected`。アンカー: HUD に `equipment.hud`、枠に `equipment.slot-<i>`、選択枠に `equipment.selected-slot` も。HUD は拾う（Web `pointer-events: auto`） | `EquipmentHudViewTest`（Web `EquipmentPanel/index.test.ts`「装備枠がアンカーを名乗り…」・`equipment.spec`「装備枠0でも装備HUDは面積を保ちアンカーが解決する」）と `EquipmentHudFloorTokenTest` が緑 |
| R-7 | ホイールの循環（C# 1 か所）: `EquipmentWheelSelector.Wheel(delta, unit)` は枠 1 以下なら何もせず、`EquipmentSelectionLogic.AccumulateWheelSteps` で累積し、段が出たら「未確認の末尾、無ければ今の選択」から `CycleEquipment` した番号を FIFO に積んで `IEquipmentSelectionPort.Select` を呼ぶ。確定の世代（`SelectionConfirmationRevision`）が進んだ数だけ FIFO の頭を捨て、戻ったら空にする | `EquipmentWheelSelectorTest`（Web `EquipmentPanel/index.test.ts` 4 件・`regression/inputLayer.spec` 1 件）が緑 |
| R-8 | ホイールの抑止: `EquipmentWheelRouter` が `UiRoutingState`・`ModalRequestService`・`PlaceSystemStateController` を自分で読んで状況を組み、`EquipmentWheelGate.Judge` は、モーダルの保留中・`SubInventory`・`ResearchTree`・`BuildMenu`（Web `readActiveLayer() != "game"`）、素通しの印の無い UI の上、`PlaceSystemStateController.IsWheelOwnedByTool` が真、のどれかで止める。占有は C# の値をそのまま読み、配置対象の種別から導き直さない。Client.Ui から渡るのは「UI の上か」の bool 1 つだけ | `EquipmentWheelGateTest`（Web `EquipmentPanel/index.test.ts` 4 件）と `EquipmentWheelFlowTest`（Web `equipment.spec` 4 件。ルーターは本物の State で組む）が緑 |
| R-9 | 入力: `InputSystemPointerWheelStream`（`Client.Input/Pointer/`）が `InputSystem.onEvent` から縦のノッチ（上が正。0 は流さない。入力設定 `scrollDeltaBehavior` は既定の Uniform で 1 ノッチ＝1）を流し、`EquipmentWheelInput`（`IInitializable`）が購読して、A-71 と同じ 1 ノッチ＝60px（下が正）に直して `WheelDeltaUnit.Pixel` でルーターへ渡す。Web の本番（CEF の 60px/ノッチ・100px で 1 段）と同じく 1 ノッチ目では進まず、2 ノッチ目で 1 段進む。カーソルがロック中は UI の上ではない。ロックしていなければ `GameUiRoot` のパネルで拾った要素を `EquipmentWheelPickJudge.IsBlockingUi` に掛ける | `InputSystemPointerWheelStreamTest`・`ScrollDeltaBehaviorSettingTest`・`EquipmentWheelFlowTest`「1 ノッチ目では進まず 2 ノッチ目で 1 段進む」が緑。録画 `uitk/uitk-inventory-equipment` で 2 ノッチで 1 段進み、持ち物の上では進まない |
| R-10 | 素通し（S3-hotbar-modal の `WheelPassthroughMark` を使う。マージ後に実物と照合）: 装備 HUD に印を付け、`IsBlockingUi` は「拾った要素があり、かつ素通しでない」。装備 HUD・ホットバーの列・ホットバーの帯の列外・クロスヘアの上では切り替わる | `EquipmentWheelPassthroughTest`（Web `equipment.spec` 3 件・`alwaysOnHudWheel.test.ts` 1 件）が緑 |
| R-11 | 世界の上の色: 装備 HUD は世界の上に載るので、ホットバーの画面ルートと同じ枠の面のつまみ（`--slot-frame-face: var(--hotbar-slot-frame-face)` ほか、S3-hotbar-modal がマージした `Hotbar.uss` の実物の上書き）を HUD のルートで上書きする（マージ後に実物と照合） | 並べ比べ `3-hotbar-equipment-empty`・`2-game-hud` の装備の枠の面が差の表で「同じ」か LX |
| R-12 | ツールチップ: 持ち物の枠の上で、マスタ GUID の辞書名が C3-b のホバーの宿主に出る（`ItemSlot` の `HoverTooltipMark`） | `InventoryPanelTooltipTest`（Web `regression/itemSlot.spec`。bd `moorestech-albof.29`）が緑 |
| R-13 | 並べ比べ: 参照 `4-player-inventory`・`4-player-inventory-full`・`3-hotbar-equipment-empty`・`2-game-hud`・`5-japanese-inventory` を 1280×720・2560×1080 で撮り、差の表を `done.md` に書く | 差の表の全行が「同じ／直した／LX-111〜LX-115／他レーン持ち」のどれか |
| R-14 | 登録: `MainGameUiRegistration` の画面登録区画に `InventoryGameUiRegistration.Register(builder);` を 1 行、`TSS` の `Screens` 区画に `@import` を 1 行、`THEME/DerivedTokens.uss` の末尾に名札付きブロック「S4-inventory」（色 1 行） | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`）が緑 |
| R-15 | 写すテスト（実物で数え直した 34 件のうち 32 件を写し、2 件を処分表へ。下の「写すテスト」表）。`node tools/uitk/test-name-parity.mjs --bundle 4`・`--bundle 2`・`--bundle 5` でこのレーンのファイルの未対応 0。`docs/uitk/disposition.md` の albof.29・albof.31 の「写す予定」の行は、タグを足す同じコミットで消す | parity の出力にこのレーンのファイル（`inventory.spec.ts` のこのレーンの 4 件・`equipment.spec.ts`・`InventoryPanel/index.test.ts`・`EquipmentPanel/index.test.ts`・`inputLayer.spec.ts` のホイール 1 件・`itemSlot.spec.ts` のツールチップ 1 件・`alwaysOnHudWheel.test.ts` の装備 HUD 1 件）の未対応が無い |
| R-16 | 録画: `uitk/uitk-inventory-equipment`（新設。持ち物を開く・整理・掴んで装備へ置く・装備の枠のクリックが移動になる・ゲーム画面でホイール 2 ノッチで 1 段ずつ循環（1 ノッチ目では進まない）・持ち物の上のホイールで進まない・装備 HUD の上で進む・チェストを開いた状態で main の枠の Shift＋左押下がチェストへ配分される〈S4-alpha D-33 から渡された完了条件〉）と `uitk/uitk-grab-overlay`（C4-e。押下の配線が alpha → このレーンへ移ったことの回帰）を全体スイッチ UITK で回す | 2 本の `result.json` が PASS、mp4、`done.md` に `UiInputExclusivity.ActiveUiSystem` の値 |
| R-17 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd を閉じる | `git log origin/feature/uitk` にこのレーンのマージコミット。`LOGS/harness/uitk/S4-inventory/done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- スロット操作の解釈（左・右・ダブル・Shift の計画、分割ドラッグの確定、掴み絵）。S4-alpha の `IPlayerSlotCommands`・C4-e の `IPlayerSlotDragCommands`・`GrabOverlayView` が持ち、このレーンは呼ぶだけ
- 掴み絵 `GrabOverlay`（C4-e が作り済み。`grabOverlay.spec`・`inventory.spec` の掴み絵の 3 件は C4-e が写し済み）
- 所持品の一覧（Web `ItemListPanel`。S4-recipe）。ただし「一覧の上でホイールが装備を回さない」のテストはこのレーンが持つ（D-6）
- DragGuide（チュートリアルの overlay。S2-tutorial。E-16）。このレーンはスロットへのアンカーの印付けだけ
- ホットバー（S3-hotbar-modal）。素通しの印 `WheelPassthroughMark` と `--hotbar-slot-frame-face` は使うだけ
- 部品（`GamePanel`・`PanelActionButton`・`ItemSlot`・`SlotGrid`）・Foundation（`StageColumnsLayout`・`HoverTooltipHost`・`TutorialAnchor`・`GameUiPointerProbe`）・トークン USS（`Tokens.uss`・`SlotTokens.uss`）・`UiScreenTraits`・`AnchorIds` は変えない。要るものは `done.md`「integrator への依頼」へ（`TutorialAnchor.Unmark`・`GameUiPointerProbe` の要素を返す口・`--hotbar-slot-frame-face` の改名・スロットの中身の写しの Content〈`Client.Ui/Content`〉への統合。D-8・D-11・D-22・D-23）。例外は E-1（反映済み ad92a44777）が許す 2 つだけ: (1) C4-e の持ち物の口（`IPlayerInventoryOperations`・`LocalPlayerInventoryOperations`・`RecordingPlayerInventoryOperations`）に合図を 1 つ足し、実装者を同じ PR で全部直す（D-18）、(2) A-71 の 60px を `GameScrollView` の private 定数から Foundation の共有の定数 `Foundation/Input/WebWheelScale.cs` へ上げ、`GameScrollView` もそれを読む（D-10）
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は変えない
- Web の `ConnectingPlaceholder`（topic 未着の仮の面）。C# の持ち物は起動時から在るので出す場面が無い（D-4）
- 装備の選択の失敗の巻き戻しと再接続の FIFO の破棄（C# の `SetSelectedIndex` に失敗の経路が無く、UITK に WebSocket の再接続が無い。処分表へ。D-5）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s4-inventory --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる。Mac mini の Editor は同時 2 本まで。Mac が逼迫したら Windows の `~/bin/winunity`（compile/test/play を ssh で回す）も使える
- **依存**: S4-alpha（`IPlayerSlotCommands`・`PlayerSlotCommands`）・C3-b（`HoverTooltipHost`。E-22）・C4-e・C3-a・L0-s4a が入った `origin/feature/uitk`。**S3-hotbar-modal（`WheelPassthroughMark`・`HotbarHudView`・`--hotbar-slot-frame-face`）は起草時に未マージ**。その型を使う作業は Task 9・10 に寄せ、Task 1 Step 1 でマージ済みかを確かめる。未マージなら Task 1〜8 を先に進め、Task 9 の前で `origin/feature/uitk` を取り込み直す（それでも無ければ指揮へ返して待つ）。S3-hotbar-modal の型は計画の写しで書いてあるので、Task 9 の Step 1 で実物と照合する
- **登録の置き場**: `S/Client.Starter/Registration/GameUi/` 直下は起草時 9 本（S2-tutorial 取り込み後）で、S4-alpha の 1 本で 10 本の上限に達する。このレーンの登録は E-1（反映済み ad92a44777）どおり `GameUi/Screens/InventoryGameUiRegistration.cs`（名前空間 `Client.Starter.Registration.GameUi.Screens`）に置き、`Screens/` が無ければこのレーンが作る（最初に要したレーンが作る）。integrator の再編は待たない
- **規約**: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下（`.meta` を除く）・partial 禁止・`Func<>` 禁止・イベントは UniRx（`Action` 禁止）・try-catch 禁止（このレーンに外部境界は無い）・日英 2 行コメント・デフォルト引数禁止・単純 getter/setter 禁止（`{ get; private set; }` と式本体の計算プロパティは可）・初期化は `Initialize`・null チェックは外部データだけ（`Mouse.current` は入力機器の有無なので外部）・fail-closed の経路は開発者ログ・意図して黙る行は `silent-ok: <理由> / <誰が気づくか>`・`#region Internal` はメソッド内のローカル関数だけ・`.meta` を手で作らない・Prefab/シーンを手で編集しない・`Update()`／`ITickable` で毎フレーム読まない（ホイールも `InputSystem.onEvent` の購読で受ける。D-19）
- **State は `Client.WebUiHost` を参照しない**。Web ホスト `InventoryTopic` の源の束ね方は State の `LocalPlayerInventoryOperations.OnSnapshotChanged` に写し直す（C4-e D-5 と同じ扱い。D-18）
- **USS**: 画面 USS の先頭に出所の 1 行（日英）。色はトークン経由だけ（`UiStyleConventionTest` が色の直書きを拒む）。部品のつまみの上書きは画面のルート（`.inventory-panel__grid`・`.equipment-hud`）で。UITK の USS に `calc()` は無いので、Web の `calc(var(--inventory-slot-base-size) * var(--inventory-slot-scale))` 等は前計算した値をコメントの式つきで書き、`InventoryGridKnobTest` がトークンの積と一致することを見る。字間は A-70 の換算（em ×100）
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有ファイルは、`MainGameUiRegistration.cs` の画面登録区画の 1 行（と `using` 1 行）、`Registration/GameUi/Screens/` の新設（無ければ）、C4-e の持ち物の口の 3 ファイルへの合図の追記（D-18）、`Foundation/Input/WebWheelScale.cs` の新設と `GameScrollView.cs` の定数の置き換え（A-71 の格上げ。D-10）、`TSS` の `Screens` 区画の `@import` 1 行、`THEME/DerivedTokens.uss` の末尾の名札付きブロック「S4-inventory」（[A-52]。前例 S3-hotbar-modal Task 5）、`docs/uitk/disposition.md` の末尾と albof.29・albof.31 の行の削除、`docs/uitk/TODO.md`「## 見た目の例外」（LX-111〜LX-115）。S2-tutorial（#1627）は `origin/feature/uitk` にマージ済みなので、`S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` の S4-inventory 持ちの 4 行（`EquipmentHud`・`EquipmentSelectedSlot`・`EquipmentSlotPrefix`・`InventoryItemPrefix`）を同じ PR で消す（Task 6 Step 3。網羅テストは `Client.Tests.Ui.Anchor`）
- **ID の範囲**: 設計書の行は A-145〜A-149、見た目の例外は LX-111〜LX-115 だけを使う（実行計画の ID の割り当て表）
- **使う部品と公開 API**（`origin/feature/uitk` の実物と、未マージの S4-alpha・S3-hotbar-modal の計画の Produces から写した。未マージ分は「マージ後に実物と照合」）

```csharp
// ── Components（C3-a / 既存）
namespace Client.Ui.Components.Panels
public sealed class GamePanel : VisualElement
{
    public GamePanel(string testid, GamePanelFrame frame);   // GamePanelFrame.Default
    public override VisualElement contentContainer { get; }
    public void SetTitle(UiText title);
    public void SetTitleAction(VisualElement action);        // SetTitle の後に呼ぶ
}
namespace Client.Ui.Components.Controls
public sealed class PanelActionButton : Button { public PanelActionButton(string testid, LocalizationKey label); public IObservable<Unit> OnClicked { get; } }
namespace Client.Ui.Components.Slots
public sealed class ItemSlot : VisualElement { public ItemSlot(string frameTestid); public IObservable<SlotGesture> OnSlotGestured { get; } public void SetContent(ItemSlotContent content); } // 包みは PickingMode.Ignore、枠（名前 = frameTestid）が拾う。中で HoverTooltipMark を UiIcon.Label で付ける
public sealed class ItemSlotContent { public ItemSlotContent(UiIcon icon, int? count, ItemSlotMarks marks, ItemSlotShortage shortage, ItemSlotGhost ghost, UiText tooltipOverride); } // icon null で空きマス
public readonly struct ItemSlotMarks { public ItemSlotMarks(bool selected, bool catalog, bool insufficient); } // selected で枠に slot-frame--selected
public sealed class SlotGrid : VisualElement { public SlotGrid(string testid, int columns); public void SetColumns(int columns); public void AddItem(VisualElement item); public void ClearItems(); }
public readonly struct SlotGesture { public readonly SlotGestureKind Kind; public readonly bool Shift; }
public enum SlotGestureKind { LeftDown, RightDown, LeftEnter, RightEnter, DoubleClick, HoverEnter, HoverLeave }

// ── Content・Text・Theme
namespace Client.Ui.Content
public sealed class GameContentPresentation { public UiIcon Item(ItemId id); public UiText ItemName(ItemId id); }
namespace Client.Ui.State.Text
public sealed class UiText { public UiText(LocalizationKey key); public static UiText FromVerbatim(string text); public string Resolve(); }
namespace Client.Ui.Foundation.Theme
public static class UssCustomStyleReader { public static bool TryReadPixels(ICustomStyle style, string propertyName, out float pixels); public static bool TryReadNumber(ICustomStyle style, string propertyName, out float number); }
// LocalizationKeys.Ui.Inventory.Title / .Sort（Mooresmaster.Localization.Generated。localization.csv:93-94 に既存）

// ── Foundation
namespace Client.Ui.Foundation.Root
public class GameUiRoot : MonoBehaviour { public VisualElement GetRootElement(); public VisualElement GetLayer(UiLayer layer, UiFamily family); }
namespace Client.Ui.Foundation.Stage.Columns
public sealed class StageColumnsLayout : VisualElement { public VisualElement Get(StageColumn column); } // DI の単一。StageColumn.Inv
namespace Client.Ui.Foundation.Anchor
public static class TutorialAnchor { public const string MarkerClassName = "tutorial-anchor"; public static void Mark(VisualElement element, params string[] ids); } // Unmark は無い（D-8）
namespace Client.Ui.Foundation.Picking
public static class UiPickingIgnore { public static void ApplyToSubtree(VisualElement root); }
namespace Client.Game.InGame.Tutorial.UIHighlight
public static class AnchorIds { public const string EquipmentHud = "equipment.hud"; public const string EquipmentSelectedSlot = "equipment.selected-slot"; public static string EquipmentSlot(int index); public static string InventoryItem(string itemGuid); }

// ── State（既存）
namespace Client.Ui.State.Routing
public sealed class UiRoutingState { public IReadOnlyReactiveProperty<UiRoutingSnapshot> Current { get; } } // .Value.Screen
public static class UiScreenTraitsTable { public static UiScreenTraits Get(UiScreen screen); } // .Grab / .AlwaysOnHud
public enum UiScreen { None, PlayerInventory, SubInventory, ResearchTree, BuildMenu, ChallengeList, PauseMenu, TrainHud, TrainPause, SkitPause }
namespace Client.Ui.State.Modal
public class ModalRequestService { public ModalRequest Pending { get; } public IObservable<Unit> OnPendingChanged { get; } public (string requestId, UniTask<(ModalResult result, string text)> response) RequestInputModal(string title, string message, string buttonText); } // テストは new で組める
namespace Client.Ui.State.Commands
public abstract class UiCommandResult; public sealed class UiCommandAccepted { public static readonly UiCommandAccepted Instance; }
namespace Client.Ui.State.Toast
public sealed class UiCommandResultToaster { public void Report(string commandName, UiCommandResult result); }
namespace Client.Ui.State.Inventory.Actions
public interface IPlayerInventoryOperations { IObservable<Unit> OnGrabChanged { get; } PlayerInventorySnapshot ReadSnapshot(); SlotItem ReadGrab(); /* Move… Collect… */ } // このレーンが OnSnapshotChanged を足す（Task 3。D-18）
public sealed class LocalPlayerInventoryOperations : IPlayerInventoryOperations { public LocalPlayerInventoryOperations(LocalPlayerInventoryController controller, LocalPlayerEquipment equipment); }
public readonly struct PlayerSlotRef { public static PlayerSlotRef Main(int slot); public static PlayerSlotRef Equipment(int slot); }
namespace Client.Ui.State.ItemMove
public sealed class PlayerInventorySnapshot { public IReadOnlyList<SlotItem> MainSlots { get; } public SlotItem Grab { get; } public IReadOnlyList<SlotItem> Equipment { get; } }
namespace Client.Ui.State.Items
public readonly struct SlotItem { public SlotItem(ItemId itemId, int count); public ItemId ItemId; public int Count; public bool IsEmpty { get; } }
public static class OwnedItemCounts { public static IReadOnlyDictionary<ItemId, int> FirstSlotIndexByItemId(IReadOnlyList<SlotItem> slots); }
namespace Client.Ui.State.Inventory.Logic
public enum WheelDeltaUnit { Pixel, Line, Page }               // 1 段の大きさ 100 / 3 / 1
public readonly struct WheelStepResult { public readonly double Remainder; public readonly int Steps; }
public static class EquipmentSelectionLogic { public static int CycleEquipment(int current, int delta, int slotCount); /* slotCount<=0 で例外 */ public static WheelStepResult AccumulateWheelSteps(double remainder, double delta, WheelDeltaUnit unit); }

// ── ゲーム側（既存）
namespace Client.Game.InGame.UI.Inventory.Equipment
public class LocalPlayerEquipment { public IReadOnlyList<IItemStack> Slots; public int SelectedIndex { get; } public int SelectionConfirmationRevision { get; } public IObservable<Unit> OnSlotsOrSelectionChanged; public void SetSelectedIndex(int index); }
namespace Client.Game.InGame.UI.Inventory.Main
public class LocalPlayerInventoryController { public ILocalPlayerInventory LocalPlayerInventory { get; } public IObservable<Unit> OnInventoryRefreshed { get; } public void SortInventory(); }
public interface ILocalPlayerInventory { IObservable<int> OnItemChange { get; } }
namespace Client.Game.InGame.BlockSystem.PlaceSystem
public class PlaceSystemStateController { public PlaceSystemStateController(IPlaceSystemSelector placeSystemSelector, IPlacementFeedbackPresenter feedbackPresenter, PlacementHeightOffset placementHeightOffset); public bool IsWheelOwnedByTool { get; } public IObservable<bool> OnWheelOwnedByToolChanged { get; } public void SetWheelOwnedByTool(bool isOwned); }
// テスト用: Client.Tests.PlaceSystem.Common.Height.PlacementHeightWiringFakes.SwitchableSelector(IPlaceSystem)・.NullPresenter（internal。同じテストのアセンブリから使える）、EmptyPlaceSystem
namespace Client.Game.InGame.UI.UIState
public class UIStateControl : MonoBehaviour { public void Construct(UIStateDictionary dictionary, IPlayerObjectController player); public void Initialize(UIStateEnum initialState, UITransitContext initialContext); }
// UiRoutingState(UIStateControl, IReadOnlyList<INestedPauseScreenState>)。テストでの組み方の前例は Client.Tests/UIState/UIStateControlTest.cs（CreateDictionary の反射・FakePlayerObjectController）

// ── 入力（既存）
namespace Client.Input.Pointer
public sealed class InputSystemPointerPositionStream { public IObservable<Vector2> OnPointerScreenPositionChanged { get; } } // InputSystem.onEvent を購読。Mouse 無しは LogWarning。MainGameUiRegistration の共有の並びに登録済み
namespace Client.Input
public static class HybridInput { public static Vector3 GetMousePosition(); }
// A-71 の 1 ノッチ＝60px は今 Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs の private const WebWheelPixelsPerNotch。このレーンが Foundation の共有の定数 Client.Ui.Foundation.Input.WebWheelScale.PixelsPerNotch へ上げ、GameScrollView もそれを読む（Task 7 Step 1。E-1。D-10）
// InputState.currentUpdateType（UnityEngine.InputSystem.LowLevel）は onEvent の中で今の更新の種類を返す。PlayMode で Game View にフォーカスが無いとき、ポインタのイベントは Editor の更新で処理される（InputManager.cs:3320-3345。D-25）

// ── S4-alpha（未マージなら Task 1 で止める。alpha 計画 85f905aa3d の「後続レーンへの Produces 一覧」の綴りどおり）
namespace Client.Ui.State.SlotCommands.Player
public interface IPlayerSlotCommands
{
    UiCommandResult LeftDown(PlayerSlotRef slot, bool shift);
    UiCommandResult RightDown(PlayerSlotRef slot);
    UiCommandResult DoubleClick(PlayerSlotRef slot);
    void LeftEnter(PlayerSlotRef slot);
    UiCommandResult RightEnter(PlayerSlotRef slot);
}
public sealed class PlayerSlotCommands : IPlayerSlotCommands { public const string CommandName = "inventory.slot"; }
// IPlayerSlotCommands は MainGameUiRegistration の共有サービスの並びに alpha が登録する。このレーンは ctor で受けるだけで登録しない
// 同じ並びの IUiStateTransitionRequester（Client.Ui.State.Routing。bool RequestTransition(UIStateEnum)）は、このレーンに閉じるボタンも画面遷移の操作も無いので使わない

// ── S3-hotbar-modal（未マージ。計画 Task 7 の Produces から写した。Task 9 Step 1 で実物と照合）
namespace Client.Ui.Foundation.Input
public static class WheelPassthroughMark
{
    public const string ClassName = "wheel-passthrough";
    public static void Mark(VisualElement element);
    public static bool IsPassthrough(VisualElement target);   // 祖先をたどる（Web の closest）。null は偽
}
namespace Client.Ui.Screens.Hotbar
public sealed class HotbarHudView : VisualElement { public HotbarHudView(IHotbarCommands commands, PlacementTargetIcons icons, UiCommandResultToaster toaster); public void Render(HotbarViewModel model); } // 帯 hotbar-hud は拾わない、列 hotbar-grid に印
// テスト用: RecordingHotbarCommands・HotbarTestPresentation.Create()・HotbarViewModelSamples.WebFixture()
// トークン: --hotbar-slot-frame-face（rgba(50,52,67,0.7226)）ほか（THEME/DerivedTokens.uss の名札付きブロック「S3-hotbar-modal」）
```

- **テストの土台**（実物）: `Client.Tests.Ui.TestHost.UiTestHost`（テーマを読み込んだテストのパネル。`panelSize`・`pixelsPerPoint`・`rootVisualElement`・`simulate`）、`Client.Tests.Ui.Screens.ScreenTestHost : UiTestHost`（`Toaster`・`ShownToasts`・`Mount<T>`・`Find<T>(root, testid)`・`IsShown`）、`Client.Tests.Ui.State.Inventory.RecordingPlayerInventoryOperations(int main, int equipment)`（`SetMain`・`SetEquipment`・`SetGrab`・`ReadMain`・`Moves`〈`"Main:0->Grab x5"` の書式〉・`Collects`。このレーンが `OnSnapshotChanged` と `RaiseSnapshotChanged()` を足す）、`Client.Tests.Ui.State.Inventory.Drag.FakePrimaryPointerButton`（`SetPressed(bool)`・`Release()`・`Reset()`）、InputSystem のテスト土台 `InputTestFixture`（`AddDevice<Mouse>`・`Set`。前例 `Client.Tests/Input/Pointer/InputSystemPointerPositionStreamTest.cs`）、`FakeContentTextureSource`、`Client.Tests.Ui.Tooltip.HoverTooltipTestStage`（宿主の組み方の前例）。サーバーのマスタは `new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory))`、木材の GUID `00000000-0000-0000-1234-000000000001`

## 設計検査記録

- 配置検査: 実施済み / 違反1件・修正1件 / V-1（持ち物の変化の合図を別の口 `IPlayerInventoryChangeFeed` に分けていた）を `IPlayerInventoryOperations.OnSnapshotChanged` へ寄せた（D-18）。注目点 N-1〜N-6 も再現を取って直した（D-19〜D-22・D-11・D-12）
- Phase 2.6: 実施済み / 強1・弱4・第3バケツ1 / 強1（状況の bool 化）は型を変えず理由を D-11 に 1 行（ルーターが State を読むので境界を越える bool は `overBlockingUi` 1 つ）、弱4 の重複 3 か所を直し（D-21・D-23）、弱1〜3 は再現せず据え置き（弱3 は `Unmark` 依頼で解く）、第3バケツは「本 PR 外のリファクタ提案」へ
- 配置検査（r2 差分）: 実施済み / 違反0件 / 注目点 N-1（C4-e の口への追記の根拠）・N-4（integrator 待ち）は E-1 の追記（ad92a44777）に合わせ、D-18 の前例を E-1 に差し替え、60px の格上げと登録の置き場（`GameUi/Screens/`）をこのレーンが持って待ちを外した（D-10・D-27）。N-3（Components と Content の取り違え）は語を直した（D-23）
- Phase 2.6（r2 差分）: 実施済み / 強2・弱3・第3バケツ2 / 強（モーダルの保留を bool にして Gate へ渡す）は r1 の強1 と同じ系統で既決（D-11）。強（D-23 と §4.2・§5.1-2）は §4.2 の注記を ad92a44777 で直し、D-23 に §5.1-2 の例外の理由を足した。弱1（ロックと拾いの合成）は再現せず据え置き。弱2（選択の変化が合図に乗る前提）は `LocalPlayerInventoryOperationsTest` に 1 件足した（Task 3）。弱3（Presenter の同じ判定と 4 行）は D-21・D-23 で既決。第3バケツ T-1・T-2 は「本 PR 外のリファクタ提案」へ（T-2 は bd に起票）。評価の要修正 2 件は D-25（Editor の更新のホイール）・D-26（所持品のアンカーの id の組み立て）
- 配置検査（r3 差分）: 実施済み / 違反0件 / 注目点 N-1（ボタンの流れを揃えない理由）は D-25 の却下案に 1 行足し第 3 バケツへ、N-2（E-23 の置き場の食い違い）は ADR 側で 9103676cbb に直った、N-3（D-8・D-11 の「Foundation は変えない」）は「E-1 が許す A-71 の定数を除き」を足した。備考の「アンカー表」の古い言い方 3 か所を「所持品の GUID の表」に直した。評価の要修正 1 件（`InputTestFixture.runtime` が internal）は Task 7 の補助 `InputTestDriver` の反射にまとめた
- Phase 2.6（r3 差分）: 実施済み / 強0・弱0・第3バケツ1 / T-3（`InputSystemPointerButtonStream` の更新の種類）は「本 PR 外のリファクタ提案」へ（揃えない理由つき）

## 写すテスト

Web の実物を開いて数え直した（`grep -nE "^\s*(it|test)(\.each)?\("`。複数行の `it.each` は 1 件）。inventory の全体は 62 件（P20・C16・L26）。L26 は L0-s4a（`equipmentLogic` 9・`splitDrag` 1・`slotActions` 9）と L0-s4b（`blockSlotRestriction` 7）が写し済み。P20 のうち `grabOverlay.spec` 2 件と `inventory.spec` の掴み絵の 3 件（「左クリックで grab オーバーレイが追従する」「grab 中の右ドラッグで…」「grab 中の左ドラッグは…」）は C4-e が写し済み、`blockInventory*` は S4-alpha。残りのこのレーンの持ち分は 34 件（inventory の 31 件＋束の外 3 件）で、32 件を写し 2 件を処分表へ書く（D-5）。§1.4 の「inventory 61（P20・C16・L25）」との差は起草メモ。

| Web ファイル（`W/` から） | テスト名（第 1 引数） | 束 | 写す先の NUnit | Task |
|---|---|---|---|---|
| `e2e/tests/inventory/inventory.spec.ts` | 接続後にインベントリが描画される | 4 | `InventoryViewTest` | 4 |
| 同 | ダブルクリックで同種を集約し、collect はクリックされたスロットを送る | 4 | `InventoryPanelGestureTest` | 6 |
| 同 | 右クリックで inventory.split を送る | 4 | `InventoryPanelGestureTest` | 6 |
| 同 | grab 中に別アイテムのスロットを押すと入れ替えの move を送りトーストを出さない | 4 | `InventoryPanelGestureTest` | 6 |
| `e2e/tests/inventory/equipment.spec.ts` | 小さいホイール入力を累積し閾値を越えた時だけ切り替える | 4 | `EquipmentWheelFlowTest` | 7 |
| 同 | ホイールは末尾スロットの次に先頭スロットへ回る | 4 | `EquipmentWheelFlowTest` | 7 |
| 同 | メインのアイテムをUI操作だけで装備スロットへ移せる | 4 | `InventoryEquipmentFlowTest` | 6 |
| 同 | 装備枠のクリックは選択ではなくアイテム移動になる | 4 | `InventoryEquipmentFlowTest` | 6 |
| 同 | 装備へ移したアイテムをホイールで選択できる | 4 | `InventoryEquipmentFlowTest` | 7 |
| 同 | 装備HUDの上でもホイールで装備が切り替わる | 4 | `EquipmentWheelPassthroughTest` | 9 |
| 同 | ホットバーHUDの上でもホイールで装備が切り替わる | 4 | `EquipmentWheelPassthroughTest` | 9 |
| 同 | 画面中央のクロスヘアの上でもホイールで装備が切り替わる | 4 | `EquipmentWheelFlowTest` | 7 |
| 同 | ホットバー帯のスロット列外でもホイールで装備が切り替わる | 4 | `EquipmentWheelPassthroughTest` | 9 |
| 同 | 持ち物画面の一覧スクロールでは装備が持ち替わらない | 4 | `EquipmentWheelFlowTest`（一覧の代わりに `GameScrollView` の立て役。D-6） | 7 |
| 同 | 装備枠0でも装備HUDは面積を保ちアンカーが解決する | 4 | `EquipmentHudViewTest` | 5 |
| `src/features/inventory/InventoryPanel/index.test.ts` | 先頭スロットのみ小文字guidでアンカーを名乗り、空/count0のスロットは無視される | 4 | `InventoryViewTest` | 4 |
| 同 | アンカーは実所持の先頭スロット(4番目)に付き、そのスロットの中身と一致する | 4 | `InventoryViewTest` | 4 |
| `src/features/inventory/EquipmentPanel/index.test.ts` | pauseMenu 中の左押下はハンドラごと無く、アクションも飛ばない | 4 | `EquipmentHudViewTest` | 5 |
| 同 | 装備枠がアンカーを名乗り、選択中の枠には選択アンカーも付く | 4 | `EquipmentHudViewTest` | 5 |
| 同 | 装備枠が1つのときはホイールで select_equipment を送らない | 4 | `EquipmentWheelSelectorTest` | 2 |
| 同 | GameScreen 中も同様にクリックを受けない | 4 | `EquipmentHudViewTest` | 5 |
| 同 | 持ち物画面では左押下が掴み取りを送る | 4 | `EquipmentHudViewTest` | 5 |
| 同 | topic反映前の高速2ノッチはpending値から1→2へ進む | 4 | `EquipmentWheelSelectorTest` | 2 |
| 同 | 楽観topicと古いechoが来ても未確認の末尾から次ノッチを計算する | 4 | `EquipmentWheelSelectorTest` | 2 |
| 同 | action失敗時はその要求をpendingから除く | 4 | 処分表（C# の選択は送りっぱなしで失敗が返らない。D-5） | 8 |
| 同 | 再接続開始時にpendingを破棄して復元snapshotを起点にする | 4 | 処分表（UITK に WebSocket の再接続が無い。世代の巻き戻りで空にする規則は `EquipmentWheelSelectorTest` が別名で持つ。D-5） | 8 |
| 同 | 加速で膨らんだ1イベントでも1段しか進まない | 4 | `EquipmentWheelSelectorTest` | 2 |
| 同 | ホイール占有中はホイールで装備が切り替わらない | 4 | `EquipmentWheelGateTest` | 2 |
| 同 | ホイールを読まない接続ツール中は抑止されず装備が切り替わる | 4 | `EquipmentWheelGateTest` | 2 |
| 同 | BPコピー選択中でもドラッグしていなければ装備が切り替わる | 4 | `EquipmentWheelGateTest` | 2 |
| 同 | block選択中は抑止されずホイールで装備が切り替わる | 4 | `EquipmentWheelGateTest` | 2 |
| `e2e/tests/regression/inputLayer.spec.ts` | GameScreenのホイールは最新equipment値から次スロットを選ぶ | 5 | `EquipmentWheelFlowTest` | 7 |
| `e2e/tests/regression/itemSlot.spec.ts` | インベントリスロットがマスタGuidの辞書名をtooltip表示する | 4 | `InventoryPanelTooltipTest`（bd `moorestech-albof.29`） | 4 |
| `src/features/commonHud/alwaysOnHudWheel.test.ts` | 装備HUDは実UIのままホイールだけ素通す | 2 | `EquipmentWheelPassthroughTest`（bd `moorestech-albof.31`） | 9 |

束の値は `docs/uitk/disposition.md` の既存の行（albof.29 は 4、albof.31 は 2）と台帳「## 行」に合わせた。`inputLayer.spec.ts` の束は Task 8 Step 1 で `node tools/uitk/test-name-parity.mjs --bundle 5` の出力から確かめる（5 でなければ出た束で回す）。

## File Structure

| パス | 役割 | 新規/変更 |
|---|---|---|
| `S/Client.Ui.State/Inventory/Actions/IPlayerInventoryOperations.cs` | 中身の変化の合図 `OnSnapshotChanged` を足す（C4-e の口。D-18） | 変更 |
| `S/Client.Ui.State/Inventory/Actions/LocalPlayerInventoryOperations.cs` | 本番の合図: 3 つの源を束ねる（Web `InventoryTopic` と同じ源） | 変更 |
| `S/Client.Tests/Ui/State/Inventory/RecordingPlayerInventoryOperations.cs` | 置く・移すたびに合図を流す | 変更 |
| `S/Client.Input/Pointer/InputSystemPointerWheelStream.cs` | ホイールのノッチを `InputSystem.onEvent` から流す。Editor の更新は除く（D-19・D-25） | 新規 |
| `S/Client.Ui/Foundation/Input/WebWheelScale.cs` | A-71 の 1 ノッチ＝60px の共有の定数（E-1。D-10） | 新規 |
| `S/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs` | private 定数 `WebWheelPixelsPerNotch` を `WebWheelScale.PixelsPerNotch` の読みに置き換える | 変更 |
| `S/Client.Ui.State/Inventory/Panel/PlayerInventoryViewModel.cs` | 不変の描画用の値 | 新規 |
| `S/Client.Ui.State/Inventory/Panel/PlayerInventoryPanelModel.cs` | 購読して ViewModel を配る | 新規 |
| `S/Client.Ui.State/Inventory/Panel/InventoryItemGuidTable.cs` | 所持品の表（スロット → アイテムの GUID 文字列。id の組み立ては View の `InventorySlotAnchorMarks`。D-26） | 新規 |
| `S/Client.Ui.State/Inventory/Panel/IInventorySortCommands.cs`・`InventorySortCommands.cs` | 整理の型付きコマンド（`inventory.sort`） | 新規 |
| `S/Client.Ui.State/Inventory/Equipment/IEquipmentSelectionPort.cs`・`LocalPlayerEquipmentSelectionPort.cs` | 装備の選択の口と本番 | 新規 |
| `S/Client.Ui.State/Inventory/Equipment/EquipmentWheelSelector.cs` | 累積・循環・未確認 FIFO | 新規 |
| `S/Client.Ui.State/Inventory/Equipment/EquipmentWheelSituation.cs` | 判定の入力（画面・モーダル・UI の上・占有） | 新規 |
| `S/Client.Ui.State/Inventory/Equipment/EquipmentWheelGate.cs` | 抑止の判定（純関数）と理由の列挙 | 新規 |
| `S/Client.Ui.State/Inventory/Equipment/EquipmentWheelRouter.cs` | 画面・モーダル・ツールの占有を State から読んで判定 → 選択（D-20） | 新規 |
| `S/Client.Ui/Screens/Inventory/Inventory.uss` | 持ち物パネルと装備 HUD の USS | 新規 |
| `S/Client.Ui/Screens/Inventory/Panel/InventoryView.cs`・`InventoryScreenPresenter.cs`・`InventorySlotAnchorMarks.cs` | 持ち物パネル | 新規 |
| `S/Client.Ui/Screens/Inventory/Equipment/EquipmentHudView.cs`・`EquipmentHudPresenter.cs` | 装備 HUD | 新規 |
| `S/Client.Ui/Screens/Inventory/Parts/PlayerSlotGestureRouter.cs`・`PlayerSlotContent.cs` | `SlotGesture` → `IPlayerSlotCommands`、`SlotItem` → `ItemSlotContent`（2 つの View で共有。D-23） | 新規 |
| `S/Client.Ui/Screens/Inventory/Wheel/EquipmentWheelPickJudge.cs`・`IEquipmentWheelPointerPicker.cs`・`EquipmentWheelPointerPicker.cs`・`EquipmentWheelNotchScale.cs`・`EquipmentWheelInput.cs` | ホイールの入力（ノッチ → 60px の換算は `EquipmentWheelNotchScale` の 1 か所。拾いは口越し〈前例 `IUiScreenPointer`〉） | 新規 |
| `S/Client.Starter/Registration/GameUi/Screens/InventoryGameUiRegistration.cs` | 登録（E-1 の W4 以降の置き場。`Screens/` が無ければ作る） | 新規 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画に 1 行と `using` 1 行 | 変更（共有区画） |
| `TSS` | `Screens` 区画に `@import` 1 行 | 変更（共有区画） |
| `THEME/DerivedTokens.uss` | 末尾に名札付きブロック「S4-inventory」 | 変更（共有区画 [A-52]） |
| `S/Client.Tests/Ui/State/Inventory/Panel/`（`PlayerInventoryPanelModelTest`・`InventoryItemGuidTableTest`・`RecordingInventorySortCommands`） | L0 | 新規 |
| `S/Client.Tests/Inventory/LocalPlayerInventoryOperationsTest.cs` | 装備の選択の変化が本番の合図に乗ることを 1 件 | 変更 |
| `S/Client.Tests/Ui/State/Inventory/Equipment/`（`EquipmentWheelSelectorTest`・`EquipmentWheelGateTest`・`FakeEquipmentSelectionPort`） | L0 | 新規 |
| `S/Client.Tests/Input/Pointer/`（`InputSystemPointerWheelStreamTest`・`ScrollDeltaBehaviorSettingTest`・`InputTestDriver`） | L0（流れと、入力設定が Uniform であることの固定）と、更新の種類を選んで回す土台 | 新規 |
| `S/Client.Tests/Ui/Screens/Inventory/`（`InventoryViewTest`・`InventoryPanelTooltipTest`・`InventoryGridKnobTest`・`InventoryScreenVisibilityTest`・`InventoryPanelGestureTest`・`InventoryEquipmentFlowTest`・`EquipmentHudViewTest`・`EquipmentHudFloorTokenTest`・`EquipmentWheelFlowTest`・`EquipmentWheelPassthroughTest`） | L1 | 新規 |
| `S/Client.Tests/Ui/Screens/Inventory/Support/`（`RecordingPlayerSlotCommands`・`InventoryScreenFixture`・`UiRoutingStateTestFactory`・`GameUiRootTestFactory`・`EquipmentWheelRouterFixture`・`FakeEquipmentWheelPointerPicker`） | L1 の土台 | 新規 |
| `S/Client.Tests/Ui/Screens/Inventory/Wheel/EquipmentWheelInputFlowTest.cs` | L1（流れ → 入力 → ルーター。Editor の更新を除く。本体のディレクトリが 10 本なので下に分ける） | 新規 |
| `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` | S4-inventory 持ちの 4 行を消す | 変更 |
| `SKILL/scenarios/uitk/uitk-inventory-equipment.cs` | 録画 | 新規 |
| `docs/uitk/disposition.md`・`docs/uitk/TODO.md` | 処分表・見た目の例外 | 変更（末尾） |

ディレクトリの本数: `Client.Ui.State/Inventory/Panel/` 5・`Equipment/` 6・`Client.Ui/Screens/Inventory/` は USS 1＋サブ 4（`Wheel/` 5・`Parts/` 2）、`Client.Ui/Foundation/Input/` 既存 5＋サブ 1＋1、`Client.Starter/Registration/GameUi/Screens/` 1、`Client.Tests/Input/Pointer/` 既存 2＋3、`Client.Tests/Ui/Screens/Inventory/` 10＋`Support/` 6＋`Wheel/` 1。どれも 10 以下。

## Tasks

### Task 1: 前提の確認と記録の準備

**Files:**
- Create: `LOGS/harness/uitk/S4-inventory/done.md`（repo 外。`../moorestech_logs` の README を先に読む）

**Interfaces:**
- Consumes: `origin/feature/uitk`（S4-alpha・C4-e・C3-a・C3-b・L0-s4a。S3-hotbar-modal は有無を見るだけ）
- Produces: bd の issue・`done.md` の骨組み・integrator への依頼

- [ ] **Step 1: worktree と依存を確かめる**

Run:
```bash
pwd
git fetch origin && git log --oneline -1 origin/feature/uitk
S=moorestech_client/Assets/Scripts
ls $S/Client.Ui.State/SlotCommands/Player/IPlayerSlotCommands.cs $S/Client.Ui.State/SlotCommands/Player/PlayerSlotCommands.cs $S/Client.Ui.State/SlotCommands/PlannedItemActionRunner.cs $S/Client.Ui.State/Inventory/Logic/EquipmentSelectionLogic.cs $S/Client.Ui.State/Inventory/Grab/PlayerGrabModel.cs $S/Client.Ui/Foundation/Tooltip/GameUiHoverTooltipInstaller.cs $S/Client.Ui/Foundation/Stage/Columns/StageColumnsLayout.cs
ls $S/Client.Ui/Foundation/Input/WheelPassthroughMark.cs $S/Client.Ui/Screens/Hotbar/HotbarHudView.cs 2>&1
ls $S/Client.Starter/Registration/GameUi/*.cs | wc -l
ls $S/Client.Starter/Registration/GameUi/Screens 2>&1
grep -n "S4-inventory" $S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs
grep -rn "WebWheelPixelsPerNotch\|PixelsPerNotch" $S/Client.Ui | head
moores-wt status
```
Expected: worktree のパス、1 行目の 7 ファイルが全部ある（無ければ S4-alpha が未マージなので止めて指揮へ返す）。`TutorialAnchorCoveragePending.cs` に S4-inventory 持ちの 4 行がある（S2-tutorial #1627 はマージ済み。Task 6 Step 3 で消す）。60px の定数が `GameScrollView` の private のままか、共有の置き場へ上がったかを `done.md`「証拠」に書く（private のままなら Task 7 Step 1 でこのレーンが上げる。上がっていればそれを読む）。`grep -n "IPlayerSlotCommands" $S/Client.Starter/Registration/MainGameUiRegistration.cs` で共有サービスの並びに登録があることも見る。2 行目（S3-hotbar-modal）は有っても無くてもよく、結果を `done.md`「証拠」に書く。`GameUi/` 直下の `.cs` の本数（S4-alpha 後に 10 本）と `GameUi/Screens/` の有無を書く（このレーンの登録は `Screens/` に置き、無ければ Task 8 で作る。E-1）

- [ ] **Step 2: bd を作り、done.md の骨組みを書く**

Run: `bd create "UITK S4-inventory: 持ち物パネル・装備 HUD・ホイールでの装備の循環" -t task -p 1 --parent <uitk の epic>`（epic の id は `bd list --status open | grep -i uitk` で引く）→ `bd update <bd> --claim`

`LOGS/harness/uitk/S4-inventory/done.md`:
```markdown
状態: 作業中
# S4-inventory（インベントリ・装備）
計画: docs/superpowers/plans/2026-11-10-uitk-s4-inventory.md / bd: <bd>
## 証拠
## 差の表
## 要裁定
## integrator への依頼
## 未確認
```

- [ ] **Step 3: integrator への依頼を先に出す**

`done.md`「## integrator への依頼」に次を書き、指揮へも同じ文を返す:
- `TutorialAnchor` に印を外す口（`Unmark(VisualElement element, params string[] ids)`。id を外し、id が残らなければ印のクラスも外す）がほしい。持ち物の所持品のアンカーは描き直しのたびに付く枠が動く。今は `InventorySlotAnchorMarks` がクラスを直接外している（D-8）
- `GameUiPointerProbe` に拾った要素を返す口（`VisualElement PickAt(Vector2 screenPosition)`）がほしい。装備のホイールは「拾った要素が素通しか」を見るので bool では足りず、入るまでは `EquipmentWheelPointerPicker` が `panel.Pick` の呼び出しを持つ（座標の換算は A-133 の `UiScreenToPanel.FromBottomLeft` を使い、写さない。D-11）
- S3-hotbar-modal のトークン `--hotbar-slot-frame-face` を、世界の地の上の空き枠の面という意味の名前（例 `--world-slot-frame-face`）へ改名してほしい。装備 HUD も同じ源・同じ地で借りている（D-22）
- `SlotItem` → `ItemSlotContent` の写し（`item.IsEmpty ? null : …`）が、このレーンの `Screens/Inventory/Parts/PlayerSlotContent` と S4-alpha の `BlockItemGrid` の 2 機能にある。Content（`Client.Ui/Content`）へ統合してほしい（統合までは両方に写しが残る。D-23）

`Unmark`・`PickAt` は入ったら手書きの代わりを置き換える作業を bd で残す: `bd create "UITK S4-inventory: TutorialAnchor.Unmark と GameUiPointerProbe.PickAt が入ったら InventorySlotAnchorMarks と EquipmentWheelPointerPicker を置き換える" -t task -p 3 --parent <bd>`

- [ ] **Step 4: コミットは無し**（記録は repo 外）

---

### Task 2: ホイールでの装備の循環（`Client.Ui.State/Inventory/Equipment/`）

持ち物パネルの状態（Task 3）が装備の選択の口を読むので、こちらを先に作る。

**Files:**
- Create: `S/Client.Ui.State/Inventory/Equipment/IEquipmentSelectionPort.cs`・`LocalPlayerEquipmentSelectionPort.cs`・`EquipmentWheelSelector.cs`・`EquipmentWheelSituation.cs`・`EquipmentWheelGate.cs`・`EquipmentWheelRouter.cs`
- Test: `S/Client.Tests/Ui/State/Inventory/Equipment/FakeEquipmentSelectionPort.cs`・`EquipmentWheelSelectorTest.cs`・`EquipmentWheelGateTest.cs`

**Interfaces:**
- Consumes: `EquipmentSelectionLogic.CycleEquipment`・`AccumulateWheelSteps`・`WheelDeltaUnit`・`UiScreen`・`LocalPlayerEquipment`・`UiRoutingState`・`ModalRequestService`・`PlaceSystemStateController`
- Produces:
  ```csharp
  namespace Client.Ui.State.Inventory.Equipment
  public interface IEquipmentSelectionPort { int SlotCount { get; } int SelectedIndex { get; } int ConfirmationRevision { get; } IObservable<Unit> OnSelectionChanged { get; } void Select(int index); }
  public sealed class EquipmentWheelSelector : IInitializable { public void Wheel(double delta, WheelDeltaUnit unit); }
  public readonly struct EquipmentWheelSituation { public EquipmentWheelSituation(UiScreen screen, bool modalPending, bool overBlockingUi, bool toolOwnsWheel); }
  public enum EquipmentWheelBlock { None, Layer, Ui, Tool }
  public static class EquipmentWheelGate { public static EquipmentWheelBlock Judge(EquipmentWheelSituation situation); }
  public sealed class EquipmentWheelRouter { public EquipmentWheelRouter(EquipmentWheelSelector selector, UiRoutingState routing, ModalRequestService modal, PlaceSystemStateController placement); public void Route(double delta, WheelDeltaUnit unit, bool overBlockingUi); }
  ```

ホイールの向き: Web の `deltaY` は下へ回すと正で、正が次の枠。この口の `delta` も「正が次の枠」で受ける（InputSystem の上が正の符号と 60px への換算は Task 7 の入力が返す）。ルーターは画面・モーダル・ツールの占有を State の実物から自分で読み、Client.Ui から受けるのは「UI の上か」の bool だけ（State は UIElements を知らない。D-20）。ルーターの組み立ては L1（Task 7 の `EquipmentWheelFlowTest`）で本物の State を組んで確かめ、この Task の L0 は判定（Gate）と循環（Selector）だけを見る。

- [ ] **Step 1: 失敗するテストを書く**

`FakeEquipmentSelectionPort.cs`（`LocalPlayerEquipment` と同じ動き: `Select` は楽観で選択を変えて通知し世代は進めない、`ConfirmFromServer` はエコーで選択を上書きし世代を 1 進める）:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Inventory.Equipment;
using UniRx;

namespace Client.Tests.Ui.State.Inventory.Equipment
{
    public sealed class FakeEquipmentSelectionPort : IEquipmentSelectionPort
    {
        private readonly Subject<Unit> _changed = new();

        public int SlotCount { get; private set; }
        public int SelectedIndex { get; private set; }
        public int ConfirmationRevision { get; private set; }
        public IObservable<Unit> OnSelectionChanged => _changed;
        public List<int> Selections { get; } = new();

        public FakeEquipmentSelectionPort(int slotCount)
        {
            SlotCount = slotCount;
        }

        public void Select(int index)
        {
            Selections.Add(index);
            SelectedIndex = index;
            _changed.OnNext(Unit.Default);
        }

        // サーバーのエコー（LocalPlayerEquipment.ApplySelected と同じ）
        // Server echo (same as LocalPlayerEquipment.ApplySelected)
        public void ConfirmFromServer(int index)
        {
            SelectedIndex = index;
            ConfirmationRevision++;
            _changed.OnNext(Unit.Default);
        }

        public void SetSlotCount(int slotCount)
        {
            SlotCount = slotCount;
            _changed.OnNext(Unit.Default);
        }
    }
}
```

`EquipmentWheelSelectorTest.cs`:
```csharp
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Inventory.Equipment
{
    public class EquipmentWheelSelectorTest
    {
        private FakeEquipmentSelectionPort _port;
        private EquipmentWheelSelector _selector;

        [SetUp]
        public void SetUp()
        {
            _port = new FakeEquipmentSelectionPort(3);
            _selector = new EquipmentWheelSelector(_port);
            _selector.Initialize();
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">装備枠が1つのときはホイールで select_equipment を送らない</web-test>
        [Test]
        public void 装備枠が1つならホイールで選択しない()
        {
            _port.SetSlotCount(1);
            _selector.Wheel(1, WheelDeltaUnit.Page);
            Assert.IsEmpty(_port.Selections);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">topic反映前の高速2ノッチはpending値から1→2へ進む</web-test>
        [Test]
        public void エコー前の2ノッチは未確認の値から1と2へ進む()
        {
            _selector.Wheel(1, WheelDeltaUnit.Page);
            _selector.Wheel(1, WheelDeltaUnit.Page);
            CollectionAssert.AreEqual(new[] { 1, 2 }, _port.Selections);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">楽観topicと古いechoが来ても未確認の末尾から次ノッチを計算する</web-test>
        [Test]
        public void 古いエコーで選択が戻っても未確認の末尾から次を計算する()
        {
            _selector.Wheel(1, WheelDeltaUnit.Page);
            _selector.Wheel(1, WheelDeltaUnit.Page);

            // 1 への要求のエコーが届き、手元の選択が 1 へ巻き戻る（2 はまだ未確認）
            // The echo for the request to 1 arrives and rewinds the local selection to 1 (2 is still unconfirmed)
            _port.ConfirmFromServer(1);
            _selector.Wheel(1, WheelDeltaUnit.Page);

            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, _port.Selections);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">加速で膨らんだ1イベントでも1段しか進まない</web-test>
        [Test]
        public void 大きな1回のホイールでも1段だけ進む()
        {
            _selector.Wheel(5, WheelDeltaUnit.Page);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        [Test]
        public void 未確認が全部確定した後はサーバーの確定値から次を計算する()
        {
            _selector.Wheel(1, WheelDeltaUnit.Page);
            _port.ConfirmFromServer(1);

            // 未確認が空なので、サーバーが別の値（0）を確定させたらそこから進む
            // With nothing unconfirmed, a different server-confirmed value (0) becomes the origin
            _port.ConfirmFromServer(0);
            _selector.Wheel(1, WheelDeltaUnit.Page);

            CollectionAssert.AreEqual(new[] { 1, 1 }, _port.Selections);
        }
    }
}
```
（最後の 1 件は処分した Web「再接続開始時にpendingを破棄して…」の代わりに、未確認が空のときの起点を見る。Web のタグは付けない。D-5）

`EquipmentWheelGateTest.cs`（Web の 4 件は topic の `wheelOwnedByTool` をそのまま読み、配置対象の種別から導き直さないことを見る。C# では占有は `PlaceSystemStateController` が持つ値で、判定の入力は bool 1 つなので、種別ごとの 3 件は「その種別の配置システムが占有を偽にしている」状況として同じ入力で写す。D-7）:
```csharp
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Inventory.Equipment
{
    public class EquipmentWheelGateTest
    {
        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">ホイール占有中はホイールで装備が切り替わらない</web-test>
        [Test]
        public void ツールがホイールを占有中は止める()
        {
            Assert.AreEqual(EquipmentWheelBlock.Tool, EquipmentWheelGate.Judge(new EquipmentWheelSituation(UiScreen.None, false, false, true)));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">ホイールを読まない接続ツール中は抑止されず装備が切り替わる</web-test>
        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">BPコピー選択中でもドラッグしていなければ装備が切り替わる</web-test>
        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">block選択中は抑止されずホイールで装備が切り替わる</web-test>
        [Test]
        public void 占有していない配置ツール中は止めない()
        {
            // 接続ツール・ドラッグしていない BP コピー・ブロックの選択はどれも占有を偽にする（判定は種別を見ない）
            // The connect tool, an undragged blueprint copy and block selection all leave ownership false (the gate never looks at the kind)
            Assert.AreEqual(EquipmentWheelBlock.None, EquipmentWheelGate.Judge(new EquipmentWheelSituation(UiScreen.None, false, false, false)));
        }

        [TestCase(UiScreen.SubInventory)]
        [TestCase(UiScreen.ResearchTree)]
        [TestCase(UiScreen.BuildMenu)]
        public void 前面の層がゲームでない画面は止める(UiScreen screen)
        {
            Assert.AreEqual(EquipmentWheelBlock.Layer, EquipmentWheelGate.Judge(new EquipmentWheelSituation(screen, false, false, false)));
        }

        [Test]
        public void モーダルの保留中は止める()
        {
            Assert.AreEqual(EquipmentWheelBlock.Layer, EquipmentWheelGate.Judge(new EquipmentWheelSituation(UiScreen.PlayerInventory, true, false, false)));
        }

        [Test]
        public void 素通しでないUIの上は止める()
        {
            Assert.AreEqual(EquipmentWheelBlock.Ui, EquipmentWheelGate.Judge(new EquipmentWheelSituation(UiScreen.PlayerInventory, false, true, false)));
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `EquipmentWheelSelector` 等が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`IEquipmentSelectionPort.cs`:
```csharp
using System;
using UniRx;

namespace Client.Ui.State.Inventory.Equipment
{
    /// <summary>
    /// 装備の選択の口。Select は楽観で選択を変えて送り、確定の世代はサーバーのエコーだけが進める
    /// Port for equipment selection; Select changes optimistically and sends, and only server echoes advance the confirmation revision
    /// </summary>
    public interface IEquipmentSelectionPort
    {
        int SlotCount { get; }
        int SelectedIndex { get; }
        int ConfirmationRevision { get; }
        IObservable<Unit> OnSelectionChanged { get; }
        void Select(int index);
    }
}
```

`LocalPlayerEquipmentSelectionPort.cs`:
```csharp
using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using UniRx;

namespace Client.Ui.State.Inventory.Equipment
{
    public sealed class LocalPlayerEquipmentSelectionPort : IEquipmentSelectionPort
    {
        private readonly LocalPlayerEquipment _equipment;

        // 別オブジェクトの値の読み替え（自分のフィールドの素通しではない）
        // Reads another object's values (not a pass-through of this class's own fields)
        public int SlotCount => _equipment.Slots.Count;
        public int SelectedIndex => _equipment.SelectedIndex;
        public int ConfirmationRevision => _equipment.SelectionConfirmationRevision;
        public IObservable<Unit> OnSelectionChanged => _equipment.OnSlotsOrSelectionChanged;

        public LocalPlayerEquipmentSelectionPort(LocalPlayerEquipment equipment)
        {
            _equipment = equipment;
        }

        public void Select(int index)
        {
            _equipment.SetSelectedIndex(index);
        }
    }
}
```

`EquipmentWheelSelector.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.Inventory.Logic;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Inventory.Equipment
{
    /// <summary>
    /// ホイールで装備を循環する唯一の場所（Web EquipmentPanel の未確認 FIFO の写し）
    /// The single place that cycles equipment with the wheel (Web EquipmentPanel's unconfirmed FIFO)
    /// </summary>
    public sealed class EquipmentWheelSelector : IInitializable
    {
        private readonly IEquipmentSelectionPort _port;
        private readonly List<int> _pending = new();
        private double _remainder;
        private int _lastRevision;

        public EquipmentWheelSelector(IEquipmentSelectionPort port)
        {
            _port = port;
        }

        public void Initialize()
        {
            _lastRevision = _port.ConfirmationRevision;
            _port.OnSelectionChanged.Subscribe(_ => TrimConfirmed());
        }

        public void Wheel(double delta, WheelDeltaUnit unit)
        {
            // 1 枠以下は切り替え先が無いので、累積もしない
            // With one slot or fewer there is nowhere to switch, so nothing accumulates either
            var slotCount = _port.SlotCount;
            // silent-ok: 枠 1 以下は切り替え先が無く Web も送らない / EquipmentWheelSelectorTest「装備枠が1つならホイールで選択しない」が見る
            if (slotCount <= 1) return;

            var accumulated = EquipmentSelectionLogic.AccumulateWheelSteps(_remainder, delta, unit);
            _remainder = accumulated.Remainder;
            if (accumulated.Steps == 0) return;

            // 未確認の末尾を起点にし、古いエコーへ巻き戻されないようにする
            // Start from the unconfirmed tail so a stale echo never rewinds the next notch
            var current = _pending.Count == 0 ? _port.SelectedIndex : _pending[_pending.Count - 1];
            var index = EquipmentSelectionLogic.CycleEquipment(current, accumulated.Steps, slotCount);
            _pending.Add(index);
            _port.Select(index);
        }

        private void TrimConfirmed()
        {
            // 世代が進んだ数だけ頭から確定済みとして捨て、戻ったら全部捨てる
            // Drop as many head entries as the revision advanced; drop everything if it went back
            var revision = _port.ConfirmationRevision;
            var advanced = revision - _lastRevision;
            _lastRevision = revision;
            if (advanced < 0)
            {
                _pending.Clear();
                return;
            }
            _pending.RemoveRange(0, System.Math.Min(advanced, _pending.Count));
        }
    }
}
```

`EquipmentWheelSituation.cs`:
```csharp
using Client.Ui.State.Routing;

namespace Client.Ui.State.Inventory.Equipment
{
    /// <summary>
    /// ホイールを装備へ流してよいかを決める入力。UI の上かは Client.Ui がパネルで拾って決める
    /// Inputs that decide whether the wheel may switch equipment; Client.Ui decides "over UI" by picking on the panel
    /// </summary>
    public readonly struct EquipmentWheelSituation
    {
        public readonly UiScreen Screen;
        public readonly bool ModalPending;
        public readonly bool OverBlockingUi;
        public readonly bool ToolOwnsWheel;

        public EquipmentWheelSituation(UiScreen screen, bool modalPending, bool overBlockingUi, bool toolOwnsWheel)
        {
            Screen = screen;
            ModalPending = modalPending;
            OverBlockingUi = overBlockingUi;
            ToolOwnsWheel = toolOwnsWheel;
        }
    }
}
```

`EquipmentWheelGate.cs`:
```csharp
using Client.Ui.State.Routing;

namespace Client.Ui.State.Inventory.Equipment
{
    public enum EquipmentWheelBlock
    {
        None,
        Layer,
        Ui,
        Tool,
    }

    /// <summary>
    /// ホイールの抑止の判定（Web の順: useGameLayerWheel の層 → isPointerOverWebUi と isWheelPassthrough → wheelOwnedByTool）
    /// Wheel suppression check (Web order: useGameLayerWheel's layer, isPointerOverWebUi with isWheelPassthrough, wheelOwnedByTool)
    /// </summary>
    public static class EquipmentWheelGate
    {
        public static EquipmentWheelBlock Judge(EquipmentWheelSituation situation)
        {
            // 前面の層がゲームでなければ止める（Web deriveActiveLayer: modal > blockInventory > research > buildMenu）
            // Stop unless the front layer is the game (Web deriveActiveLayer: modal > blockInventory > research > buildMenu)
            if (situation.ModalPending) return EquipmentWheelBlock.Layer;
            if (situation.Screen == UiScreen.SubInventory || situation.Screen == UiScreen.ResearchTree || situation.Screen == UiScreen.BuildMenu) return EquipmentWheelBlock.Layer;

            // UI の上のホイールはその画面の操作。占有は C# の値をそのまま読む
            // A wheel over UI belongs to that screen; ownership is read as C# publishes it
            if (situation.OverBlockingUi) return EquipmentWheelBlock.Ui;
            if (situation.ToolOwnsWheel) return EquipmentWheelBlock.Tool;
            return EquipmentWheelBlock.None;
        }
    }
}
```

`EquipmentWheelRouter.cs`（State が `PlaceSystemStateController` を読む前例は `ModeHudModel`）:
```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Modal;
using Client.Ui.State.Routing;

namespace Client.Ui.State.Inventory.Equipment
{
    /// <summary>
    /// 画面・モーダル・ツールの占有を State から読んでホイールを判定し、通れば装備の循環へ流す
    /// Reads screen, modal and tool ownership from State, judges the wheel, and passes it to equipment cycling when allowed
    /// </summary>
    public sealed class EquipmentWheelRouter
    {
        private readonly EquipmentWheelSelector _selector;
        private readonly UiRoutingState _routing;
        private readonly ModalRequestService _modal;
        private readonly PlaceSystemStateController _placement;

        public EquipmentWheelRouter(EquipmentWheelSelector selector, UiRoutingState routing, ModalRequestService modal, PlaceSystemStateController placement)
        {
            _selector = selector;
            _routing = routing;
            _modal = modal;
            _placement = placement;
        }

        public void Route(double delta, WheelDeltaUnit unit, bool overBlockingUi)
        {
            // UI の上かだけは Client.Ui がパネルで拾って決める（State は UIElements を知らない）
            // Only "over UI" is decided by Client.Ui picking on the panel (State does not know UIElements)
            var situation = new EquipmentWheelSituation(_routing.Current.Value.Screen, _modal.Pending != null, overBlockingUi, _placement.IsWheelOwnedByTool);

            // silent-ok: 止めたホイールはその画面かツールの操作で、装備に触れないのが正しい挙動 / L1 の EquipmentWheelFlowTest と録画が見る
            if (EquipmentWheelGate.Judge(situation) != EquipmentWheelBlock.None) return;
            _selector.Wheel(delta, unit);
        }
    }
}
```

- [ ] **Step 4: 緑を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.Inventory.Equipment"`
Expected: `ErrorCount: 0`、全件 PASS（Selector 5・Gate 7）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Inventory/Equipment moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Inventory/Equipment
git commit -m "feat(uitk): ホイールでの装備の循環を C# の 1 か所に置く（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 3: 持ち物パネルの状態（`Client.Ui.State/Inventory/Panel/`）

**Files:**
- Create: `S/Client.Ui.State/Inventory/Panel/PlayerInventoryViewModel.cs`・`PlayerInventoryPanelModel.cs`・`InventoryItemGuidTable.cs`・`IInventorySortCommands.cs`・`InventorySortCommands.cs`
- Modify: `S/Client.Ui.State/Inventory/Actions/IPlayerInventoryOperations.cs`・`LocalPlayerInventoryOperations.cs`（合図 `OnSnapshotChanged`。D-18）、`S/Client.Tests/Ui/State/Inventory/RecordingPlayerInventoryOperations.cs`、`S/Client.Tests/Inventory/LocalPlayerInventoryOperationsTest.cs`（1 件足す）
- Test: `S/Client.Tests/Ui/State/Inventory/Panel/PlayerInventoryPanelModelTest.cs`・`InventoryItemGuidTableTest.cs`・`RecordingInventorySortCommands.cs`

**Interfaces:**
- Consumes: `IPlayerInventoryOperations.ReadSnapshot()`・`OwnedItemCounts.FirstSlotIndexByItemId`・`MasterHolder.ItemMaster.GetItemGuid`・Task 2 の `IEquipmentSelectionPort`（`SelectedIndex` だけ）と `FakeEquipmentSelectionPort`
- Produces:
  ```csharp
  namespace Client.Ui.State.Inventory.Actions
  public interface IPlayerInventoryOperations { IObservable<Unit> OnSnapshotChanged { get; } /* 既存の口はそのまま */ } // main・装備の中身と装備の選択が変わったかもしれない合図（値は ReadSnapshot で読み直す）
  namespace Client.Tests.Ui.State.Inventory
  public sealed class RecordingPlayerInventoryOperations { public void RaiseSnapshotChanged(); } // SetMain・SetEquipment・移動は自分で流す。RaiseSnapshotChanged は別の Fake（装備の選択）を同じ源として繋ぐ L1 の土台用
  namespace Client.Ui.State.Inventory.Panel
  public sealed class PlayerInventoryViewModel { IReadOnlyList<SlotItem> MainSlots; IReadOnlyList<SlotItem> EquipmentSlots; int SelectedEquipment; IReadOnlyDictionary<int, string> MainSlotItemGuids; }
  public sealed class PlayerInventoryPanelModel : IInitializable { public IReadOnlyReactiveProperty<PlayerInventoryViewModel> Current { get; } }
  public static class InventoryItemGuidTable { public static IReadOnlyDictionary<int, string> Build(IReadOnlyList<SlotItem> mainSlots); } // スロット番号 → 小文字の GUID 文字列。アンカー id は View が組む（D-26）
  public interface IInventorySortCommands { UiCommandResult Sort(); }
  public sealed class InventorySortCommands : IInventorySortCommands { public const string CommandName = "inventory.sort"; }
  ```

- [ ] **Step 1: 失敗するテストを書く**

`InventoryItemGuidTableTest.cs`（L0。Web `InventoryPanel/index.test.ts` の 2 件は View に付くかを見るので Task 4 の `InventoryViewTest` に置き、ここは表の規則だけを見る。タグは付けない）:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Inventory.Panel
{
    public class InventoryItemGuidTableTest
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
        }

        [Test]
        public void 同じアイテムの先頭スロットだけが小文字のguidを持つ()
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var slots = new List<SlotItem> { empty, new(_wood, 0), new(_wood, 3), new(_wood, 5) };

            var table = InventoryItemGuidTable.Build(slots);

            // 空・個数 0 は所持に数えず、実所持の先頭（2 番）だけが持つ
            // Empty and zero-count slots are not owned; only the first owned slot (index 2) holds it
            Assert.AreEqual(1, table.Count);
            Assert.AreEqual(WoodGuid.ToString(), table[2]);
            StringAssert.IsMatch("^[0-9a-f-]+$", table[2]);
        }
    }
}
```
（`ItemMaster.EmptyItemId` の綴りは実物で確かめる。無ければ `RecordingPlayerInventoryOperations` が空を作る式を写し、以後の Task の同じ箇所も揃える）

`RecordingInventorySortCommands.cs`:
```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Panel;

namespace Client.Tests.Ui.State.Inventory.Panel
{
    public sealed class RecordingInventorySortCommands : IInventorySortCommands
    {
        public int SortCount { get; private set; }

        public UiCommandResult Sort()
        {
            SortCount++;
            return UiCommandAccepted.Instance;
        }
    }
}
```

`PlayerInventoryPanelModelTest.cs`:
```csharp
using System;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Inventory.Panel
{
    public class PlayerInventoryPanelModelTest
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private RecordingPlayerInventoryOperations _operations;
        private FakeEquipmentSelectionPort _equipment;
        private PlayerInventoryPanelModel _model;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _operations = new RecordingPlayerInventoryOperations(36, 3);
            _equipment = new FakeEquipmentSelectionPort(3);
            _model = new PlayerInventoryPanelModel(_operations, _equipment);
        }

        [Test]
        public void Initializeより前から今の持ち物と装備の選択を持つ()
        {
            // Presenter が先に購読しても null を描かない（前例 PlayerGrabModel・ModeHudModel は ctor で初期値を持つ）
            // Even if a presenter subscribes first it never draws null (precedents PlayerGrabModel and ModeHudModel seed in the ctor)
            var current = _model.Current.Value;
            Assert.AreEqual(36, current.MainSlots.Count);
            Assert.AreEqual(3, current.EquipmentSlots.Count);
            Assert.AreEqual(0, current.SelectedEquipment);
        }

        [Test]
        public void 中身の変化の合図で同期に描き直しの値を配る()
        {
            _model.Initialize();
            _equipment.ConfirmFromServer(2);
            _operations.SetMain(4, new SlotItem(_wood, 7));

            var current = _model.Current.Value;
            Assert.AreEqual(7, current.MainSlots[4].Count);
            Assert.AreEqual(2, current.SelectedEquipment);
            Assert.IsTrue(current.MainSlotItemGuids.ContainsKey(4), "所持品の表も同じ値で組む");
        }
    }
}
```
（`RecordingPlayerInventoryOperations` は `Client.Tests.Ui.State.Inventory` 名前空間。`using` を足す。本番では装備の選択も `OnSnapshotChanged` に来る〈`LocalPlayerEquipment.OnSlotsOrSelectionChanged` が中身と選択の 1 本の合図〉。このテストは選択を先に変えてから中身の合図で読み直す）

既存の `S/Client.Tests/Inventory/LocalPlayerInventoryOperationsTest.cs` に 1 件足す（本番の合図に装備の選択の変化が乗ることを、本番の `LocalPlayerEquipment` で押さえる。Model は選択の口を別に購読しないので、ここが外れると選択枠が動かなくなる。D-18・D-24。`using UniRx;` を足す）:
```csharp
        [Test]
        public void 装備の選択が変わるとOnSnapshotChangedが流れる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var equipment = new LocalPlayerEquipment();
            var operations = new LocalPlayerInventoryOperations(new LocalPlayerInventoryController(new LocalPlayerInventory(), equipment), equipment);
            var signals = 0;
            operations.OnSnapshotChanged.Subscribe(_ => signals++);

            // SetSelectedIndex はサーバーへ送るので、確定の入口 ApplySelected で選択を変える
            // SetSelectedIndex sends to the server, so change the selection through the confirmation entry ApplySelected
            equipment.ApplySelected(1);

            Assert.AreEqual(1, signals);
        }
```

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PlayerInventoryPanelModel` 等が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`Actions/IPlayerInventoryOperations.cs` の `OnGrabChanged` の次に足す（C4-e の口に合図を 1 つ足す。同じ口の読み口 `ReadSnapshot` の変化を同じ口で知らせる。D-18）:
```csharp
        // main・装備の中身と装備の選択が変わったかもしれない合図（値は ReadSnapshot で読み直す）
        // Signal that main/equipment contents or the equipment selection may have changed (re-read with ReadSnapshot)
        IObservable<Unit> OnSnapshotChanged { get; }
```

`Actions/LocalPlayerInventoryOperations.cs`（ctor で 3 つの源を束ね、`{ get; private set; }` で出す。`OnGrabChanged` の式本体の行の次にプロパティ、ctor の末尾に代入）:
```csharp
        public IObservable<Unit> OnSnapshotChanged { get; private set; }
```
```csharp
            // スロット単位の変化・まとめての差し替え・装備の中身と選択の 3 つを 1 本にする（Web ホスト InventoryTopic と同じ源）
            // Merge per-slot changes, bulk refreshes and equipment contents/selection into one stream (same sources as the Web host InventoryTopic)
            OnSnapshotChanged = Observable.Merge(
                controller.LocalPlayerInventory.OnItemChange.AsUnitObservable(),
                controller.OnInventoryRefreshed,
                equipment.OnSlotsOrSelectionChanged);
```

`S/Client.Tests/Ui/State/Inventory/RecordingPlayerInventoryOperations.cs`（`_onGrabChanged` の隣に `Subject<Unit> _onSnapshotChanged`、`OnGrabChanged` の隣にプロパティ、`SetMain`・`SetEquipment` の末尾と `Move` の `_onGrabChanged.OnNext` の隣で流す。他は変えない。137 行 → 150 行前後）:
```csharp
        public IObservable<Unit> OnSnapshotChanged => _onSnapshotChanged;

        // 装備の選択の Fake を本番と同じ 1 本の合図に繋ぐ L1 の土台用（本番は LocalPlayerEquipment の合図が中身と選択の両方を流す）
        // For L1 fixtures wiring the equipment-selection fake to one signal as in production (LocalPlayerEquipment signals both contents and selection)
        public void RaiseSnapshotChanged()
        {
            _onSnapshotChanged.OnNext(Unit.Default);
        }
```

`PlayerInventoryViewModel.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.Items;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 持ち物パネルと装備 HUD が描く値（不変）
    /// Immutable values the inventory panel and equipment HUD draw
    /// </summary>
    public sealed class PlayerInventoryViewModel
    {
        public IReadOnlyList<SlotItem> MainSlots { get; }
        public IReadOnlyList<SlotItem> EquipmentSlots { get; }
        public int SelectedEquipment { get; }
        // スロット番号 → 所持品の GUID（小文字）。アンカー id は View が組む
        // Slot index -> owned item GUID (lowercase); the view builds the anchor id
        public IReadOnlyDictionary<int, string> MainSlotItemGuids { get; }

        public PlayerInventoryViewModel(IReadOnlyList<SlotItem> mainSlots, IReadOnlyList<SlotItem> equipmentSlots, int selectedEquipment, IReadOnlyDictionary<int, string> mainSlotItemGuids)
        {
            MainSlots = mainSlots;
            EquipmentSlots = equipmentSlots;
            SelectedEquipment = selectedEquipment;
            MainSlotItemGuids = mainSlotItemGuids;
        }
    }
}
```

`InventoryItemGuidTable.cs`（id の組み立ては Mark を呼ぶ View 側に置く。`TutorialAnchorCoverageTest` は `Client.Ui` の下で `TutorialAnchor.Mark(` を含むファイルからだけ `AnchorIds.` を拾うため。D-26）:
```csharp
using System.Collections.Generic;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 「このアイテムを持っているスロット」の表（Web InventoryPanel の firstSlots。id は View が inventoryItemAnchorId と同じ形で組む）
    /// Table of "the slot holding this item" (Web InventoryPanel firstSlots; the view builds the id like inventoryItemAnchorId)
    /// </summary>
    public static class InventoryItemGuidTable
    {
        public static IReadOnlyDictionary<int, string> Build(IReadOnlyList<SlotItem> mainSlots)
        {
            // 空と個数 0 を除いた先頭スロットにだけ付ける。C# のマスタは全アイテムが GUID を持つ
            // Only the first owned slot gets it (empty and zero-count excluded); every C# master item has a GUID
            var result = new Dictionary<int, string>();
            foreach (var pair in OwnedItemCounts.FirstSlotIndexByItemId(mainSlots))
            {
                var guid = MasterHolder.ItemMaster.GetItemGuid(pair.Key);
                result[pair.Value] = guid.ToString();
            }
            return result;
        }
    }
}
```

`PlayerInventoryPanelModel.cs`:
```csharp
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Equipment;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 持ち物と装備の変化を購読し、描画用の値を同期で配る（Web InventoryTopic の写し。フレーム末への間引きはしない A-57）
    /// Subscribes to inventory and equipment changes and publishes draw values synchronously (Web InventoryTopic; no end-of-frame coalescing, A-57)
    /// </summary>
    public sealed class PlayerInventoryPanelModel : IInitializable
    {
        private readonly IPlayerInventoryOperations _operations;
        private readonly IEquipmentSelectionPort _equipment;
        private readonly ReactiveProperty<PlayerInventoryViewModel> _current;

        public IReadOnlyReactiveProperty<PlayerInventoryViewModel> Current => _current;

        public PlayerInventoryPanelModel(IPlayerInventoryOperations operations, IEquipmentSelectionPort equipment)
        {
            _operations = operations;
            _equipment = equipment;

            // 初期値を ctor で組み、Presenter の初期化の順に依らず null を配らない（前例 ModeHudModel）
            // Seed in the ctor so null is never published regardless of presenter initialization order (precedent ModeHudModel)
            _current = new ReactiveProperty<PlayerInventoryViewModel>(Build());
        }

        public void Initialize()
        {
            // 合図を購読し、購読までの間の変化も取り込む
            // Subscribe to the signal and pick up any change made before subscribing
            _operations.OnSnapshotChanged.Subscribe(_ => _current.Value = Build());
            _current.Value = Build();
        }

        private PlayerInventoryViewModel Build()
        {
            // 掴みは C4-e の掴み絵が持つので、main と装備だけを読む
            // The grab belongs to the C4-e grab picture, so only main and equipment are read
            var snapshot = _operations.ReadSnapshot();
            var itemGuids = InventoryItemGuidTable.Build(snapshot.MainSlots);
            return new PlayerInventoryViewModel(snapshot.MainSlots, snapshot.Equipment, _equipment.SelectedIndex, itemGuids);
        }
    }
}
```
（`Current => _current` は書ける型を読み取り専用の型で出す変換で、前例 `PlayerGrabModel.Current` と同じ形。装備の選択は `IEquipmentSelectionPort` から読み、合図は `OnSnapshotChanged` の 1 本だけを購読する〈本番は選択もこの合図に来るので、選択の口を別に購読すると 2 回組む〉。D-18・D-24）

`IInventorySortCommands.cs`:
```csharp
using Client.Ui.State.Commands;

namespace Client.Ui.State.Inventory.Panel
{
    public interface IInventorySortCommands
    {
        UiCommandResult Sort();
    }
}
```

`InventorySortCommands.cs`:
```csharp
using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.Commands;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 持ち物の整理（Web inventory.sort）。整理はサーバーへ送るだけで、手元で断る条件は無い
    /// Sorts the inventory (Web inventory.sort); it only sends to the server and has no local refusal
    /// </summary>
    public sealed class InventorySortCommands : IInventorySortCommands
    {
        public const string CommandName = "inventory.sort";
        private readonly LocalPlayerInventoryController _controller;

        public InventorySortCommands(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        public UiCommandResult Sort()
        {
            _controller.SortInventory();
            return UiCommandAccepted.Instance;
        }
    }
}
```

- [ ] **Step 4: 緑を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.Inventory|Client.Tests.Ui.Screens.GrabOverlay"`
Expected: `ErrorCount: 0`、全件 PASS（Panel 3 を含む。C4-e の持ち物の口を使う既存のテスト〈Grab・Drag・GrabOverlay〉も緑のまま）。続けて `--filter-type class --filter-value "Client.Tests.Inventory.LocalPlayerInventoryOperationsTest"` も全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Inventory/Panel moorestech_client/Assets/Scripts/Client.Ui.State/Inventory/Actions/IPlayerInventoryOperations.cs moorestech_client/Assets/Scripts/Client.Ui.State/Inventory/Actions/LocalPlayerInventoryOperations.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Inventory/Panel moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Inventory/RecordingPlayerInventoryOperations.cs moorestech_client/Assets/Scripts/Client.Tests/Inventory/LocalPlayerInventoryOperationsTest.cs
git commit -m "feat(uitk): 持ち物パネルの状態と整理のコマンドを足す（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 4: 持ち物の View（`Screens/Inventory/Panel/`・`Parts/`・USS）

**Files:**
- Create: `S/Client.Ui/Screens/Inventory/Parts/PlayerSlotGestureRouter.cs`・`S/Client.Ui/Screens/Inventory/Parts/PlayerSlotContent.cs`・`S/Client.Ui/Screens/Inventory/Panel/InventoryView.cs`・`InventorySlotAnchorMarks.cs`・`S/Client.Ui/Screens/Inventory/Inventory.uss`
- Modify: `THEME/DerivedTokens.uss`（末尾に名札付きブロック「S4-inventory」）、`TSS`（`Screens` 区画の末尾に `@import` 1 行。L1 のテーマにも要るのでこの Task で足す）、`docs/uitk/disposition.md`（albof.29 の「写す予定」の行を消す）
- Test: `S/Client.Tests/Ui/Screens/Inventory/InventoryViewTest.cs`・`InventoryPanelTooltipTest.cs`・`InventoryGridKnobTest.cs`・`Support/RecordingPlayerSlotCommands.cs`

**Interfaces:**
- Consumes: Task 3 の `PlayerInventoryViewModel`・`IInventorySortCommands`・`InventorySortCommands.CommandName`、S4-alpha の `IPlayerSlotCommands`・`PlayerSlotCommands.CommandName`、`GamePanel`・`PanelActionButton`・`SlotGrid`・`ItemSlot`・`ItemSlotContent`・`GameContentPresentation`・`UiCommandResultToaster`・`TutorialAnchor`・`UssCustomStyleReader`
- Produces:
  ```csharp
  namespace Client.Ui.Screens.Inventory.Parts
  public sealed class PlayerSlotGestureRouter { public PlayerSlotGestureRouter(IPlayerSlotCommands commands, UiCommandResultToaster toaster); public void Route(PlayerSlotRef slot, SlotGesture gesture); }
  public static class PlayerSlotContent { public static ItemSlotContent From(GameContentPresentation content, SlotItem item, bool selected); }
  namespace Client.Ui.Screens.Inventory.Panel
  public sealed class InventoryView : VisualElement { public InventoryView(PlayerSlotGestureRouter gestures, IInventorySortCommands sort, GameContentPresentation content, UiCommandResultToaster toaster); public void Render(PlayerInventoryViewModel viewModel); public void SetShown(bool shown); }
  ```
  （`Render` は受けたらすぐ描き、`SetShown` は出し入れだけ。「表示中だけ購読」は Presenter が持ち、ReactiveProperty が購読時に今の値を流すので View は最新値を覚えない。D-23）

- [ ] **Step 1: 失敗するテストを書く**

`Support/RecordingPlayerSlotCommands.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.SlotCommands.Player;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    // 呼ばれた操作を "LeftDown Main:0 shift=False" の書式で残す
    // Records calls as "LeftDown Main:0 shift=False"
    public sealed class RecordingPlayerSlotCommands : IPlayerSlotCommands
    {
        public List<string> Calls { get; } = new();

        public UiCommandResult LeftDown(PlayerSlotRef slot, bool shift)
        {
            Calls.Add($"LeftDown {slot} shift={shift}");
            return UiCommandAccepted.Instance;
        }

        public UiCommandResult RightDown(PlayerSlotRef slot)
        {
            Calls.Add($"RightDown {slot}");
            return UiCommandAccepted.Instance;
        }

        public UiCommandResult DoubleClick(PlayerSlotRef slot)
        {
            Calls.Add($"DoubleClick {slot}");
            return UiCommandAccepted.Instance;
        }

        public void LeftEnter(PlayerSlotRef slot)
        {
            Calls.Add($"LeftEnter {slot}");
        }

        public UiCommandResult RightEnter(PlayerSlotRef slot)
        {
            Calls.Add($"RightEnter {slot}");
            return UiCommandAccepted.Instance;
        }
    }
}
```

`InventoryViewTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryViewTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private RecordingPlayerSlotCommands _commands;
        private RecordingInventorySortCommands _sort;
        private InventoryView _view;
        private Texture2D _texture;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _commands = new RecordingPlayerSlotCommands();
            _sort = new RecordingInventorySortCommands();
            var content = new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));
            _view = Mount(new InventoryView(new PlayerSlotGestureRouter(_commands, Toaster), _sort, content, Toaster));
            _view.SetShown(true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">接続後にインベントリが描画される</web-test>
        [Test]
        public void 題と整理ボタンと持ち物の個数が描かれる()
        {
            _view.Render(Model(new SlotItem(_wood, 10)));
            simulate.FrameUpdate();

            Assert.IsTrue(_view.Query<Label>().ToList().Any(label => label.text == new UiText(LocalizationKeys.Ui.Inventory.Title).Resolve()), "題");
            var sort = Find<PanelActionButton>(_view, "inventory-sort");
            Assert.IsTrue(IsShown(sort), "整理ボタン");
            Assert.AreEqual(36, Find<VisualElement>(_view, "main-grid").Query(className: "slot-frame").ToList().Count, "36 枠");
            Assert.IsTrue(Find<VisualElement>(_view, "main-slot-0").Query<TextElement>().ToList().Any(text => text.text == "10"), "個数");

            // 整理ボタンは整理のコマンドへ（Web dispatch inventory.sort）
            // The sort button goes to the sort command (Web dispatch inventory.sort)
            simulate.Click(sort);
            Assert.AreEqual(1, _sort.SortCount);
        }

        /// <web-test file="src/features/inventory/InventoryPanel/index.test.ts">先頭スロットのみ小文字guidでアンカーを名乗り、空/count0のスロットは無視される</web-test>
        [Test]
        public void 先頭スロットだけが小文字guidのアンカーを名乗る()
        {
            _view.Render(Model(new SlotItem(_wood, 3), new SlotItem(_wood, 2), new SlotItem(_wood, 0)));
            CollectionAssert.AreEqual(new[] { AnchorIds.InventoryItem(WoodGuid.ToString()) }, AnchorIdsInView());
            Assert.IsTrue(SlotOf(0).ClassListContains(AnchorIds.InventoryItem(WoodGuid.ToString())));
        }

        /// <web-test file="src/features/inventory/InventoryPanel/index.test.ts">アンカーは実所持の先頭スロット(4番目)に付き、そのスロットの中身と一致する</web-test>
        [Test]
        public void アンカーは実所持の先頭スロットに付き描き直しで移る()
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            _view.Render(Model(empty, new SlotItem(_wood, 0), empty, new SlotItem(_wood, 4)));
            Assert.IsTrue(SlotOf(3).ClassListContains(AnchorIds.InventoryItem(WoodGuid.ToString())), "4 番目");

            // 先頭が動いたら古い枠からは印ごと外れる
            // When the first owned slot moves, the old slot loses the id and the marker
            _view.Render(Model(new SlotItem(_wood, 1), empty, empty, new SlotItem(_wood, 4)));
            Assert.IsTrue(SlotOf(0).ClassListContains(AnchorIds.InventoryItem(WoodGuid.ToString())));
            Assert.IsFalse(SlotOf(3).ClassListContains(TutorialAnchor.MarkerClassName));
            Assert.AreEqual(1, AnchorIdsInView().Count);
        }

        [Test]
        public void 枠の身振りはプレイヤーの枠のコマンドへ渡る()
        {
            _view.Render(Model(new SlotItem(_wood, 10)));
            simulate.FrameUpdate();
            var frame = Find<VisualElement>(_view, "main-slot-2");

            simulate.MouseDown(frame.worldBound.center, MouseButton.RightMouse);
            simulate.MouseUp(frame.worldBound.center, MouseButton.RightMouse);

            CollectionAssert.Contains(_commands.Calls, "RightDown Main:2");
        }

        private PlayerInventoryViewModel Model(params SlotItem[] head)
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var main = Enumerable.Range(0, 36).Select(i => i < head.Length ? head[i] : empty).ToArray();
            return new PlayerInventoryViewModel(main, new[] { empty, empty, empty }, 0, InventoryItemGuidTable.Build(main));
        }

        private VisualElement SlotOf(int index)
        {
            // 印は ItemSlot（枠 main-slot-<i> の親）に付く
            // Anchors sit on the ItemSlot (the parent of frame main-slot-<i>)
            return Find<VisualElement>(_view, $"main-slot-{index}").parent;
        }

        private List<string> AnchorIdsInView()
        {
            return _view.Query(className: TutorialAnchor.MarkerClassName).ToList()
                .SelectMany(element => element.GetClasses()).Where(name => name.StartsWith("inventory.item-")).ToList();
        }
    }
}
```
（`SlotOf` の親子は C3-a の `ItemSlot` の実物〈包み → 枠〉で確かめ、違えば `ItemSlot` を `_view.Query<ItemSlot>()` の順で引く。`ItemMaster.EmptyItemId` は Task 2 と同じ綴り）

`InventoryPanelTooltipTest.cs`（宿主の組み方は `HoverTooltipTestStage` の前例。持ち物の View を宿主の下に入れて枠へホバーする。辞書は前例 `Client.Tests/Ui/Content/GameContentPresentationTest.cs` の SetUp・TearDown・`MergeContentNames` と同じく、保存言語を退避して英語に固定し、一時 mod の辞書へ木材の名前を合成する。テスト mod の木材〈GUID …0001〉の名前は "Test1" で、mod に辞書が無いので、合成しないと名前が決まらない）:
```csharp
using System;
using System.IO;
using System.Linq;
using Client.Game.Localization;
using Client.Localization;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Tests.Ui.Tooltip;
using Client.Ui.Content;
using Client.Ui.Components.Text;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using Mod.Loader;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryPanelTooltipTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private const string WoodName = "Test Wood";
        private Texture2D _texture;
        private bool _hadSavedLanguage;
        private string _savedLanguage;
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            // 端末の保存言語を退避し、テスト mod のマスタと英語の辞書を組む（前例 GameContentPresentationTest）
            // Stash the device's saved language and load the test mod master with an English dictionary (precedent GameContentPresentationTest)
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _hadSavedLanguage = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguage = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"inventory-tooltip-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_temporaryDirectory);
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
            MergeWoodName();
        }

        [TearDown]
        public void TearDown()
        {
            // 言語の復元を先に済ませ、検証が失敗しても英語固定を残さない
            // Restore the language first so a failed assertion never leaves English pinned
            if (_hadSavedLanguage) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguage);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, true);
            Localize.Initialize();
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/regression/itemSlot.spec.ts">インベントリスロットがマスタGuidの辞書名をtooltip表示する</web-test>
        [Test]
        public void 持ち物の枠の上でマスタGuidの辞書名が出る()
        {
            var wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            var content = new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));

            // 宿主の下に持ち物の View と拾わない層を置く（HoverTooltipTestStage と同じ組み方）
            // Put the inventory view and a non-picking layer under the host (same build as HoverTooltipTestStage)
            var pointerRoot = new VisualElement { name = "tooltip-pointer-root" };
            pointerRoot.StretchToParentSize();
            rootVisualElement.Add(pointerRoot);
            var view = new InventoryView(new PlayerSlotGestureRouter(new RecordingPlayerSlotCommands(), Toaster), new RecordingInventorySortCommands(), content, Toaster);
            pointerRoot.Add(view);
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.StretchToParentSize();
            pointerRoot.Add(layer);
            var face = new TooltipFace("hover-tooltip", TooltipFaceLook.Hover);
            new HoverTooltipHost(face, new StageScaleSource(new ReactiveProperty<float>(1f))).Attach(pointerRoot, layer);

            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var main = Enumerable.Range(0, 36).Select(i => i == 0 ? new SlotItem(wood, 1) : empty).ToArray();
            view.SetShown(true);
            view.Render(new PlayerInventoryViewModel(main, new[] { empty }, 0, InventoryItemGuidTable.Build(main)));
            simulate.FrameUpdate();

            simulate.MouseMove(Find<VisualElement>(view, "main-slot-0").worldBound.center);
            simulate.FrameUpdate();

            // 期待は合成した木材の辞書名（マスタ GUID 由来のキー ContentLocalizationKeys.ItemName で引く）
            // Expect the merged dictionary name of wood (looked up by the GUID-derived key ContentLocalizationKeys.ItemName)
            Assert.AreEqual(WoodName, content.ItemName(wood).Resolve());
            StringAssert.EndsWith(WoodName, face.Q<Label>().text);
        }

        // 一時 mod の辞書へ木材の名前を合成（前例 GameContentPresentationTest.MergeContentNames）
        // Compose the wood name into a temporary mod dictionary (precedent GameContentPresentationTest.MergeContentNames)
        private void MergeWoodName()
        {
            var modDirectory = Path.Combine(_temporaryDirectory, "inventory-tooltip-mod");
            Directory.CreateDirectory(Path.Combine(modDirectory, "master"));
            Directory.CreateDirectory(Path.Combine(modDirectory, "localization"));
            File.WriteAllText(
                Path.Combine(modDirectory, "master", "modMeta.json"),
                "{\"id\":\"inventory-tooltip\",\"name\":\"inventory-tooltip\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}");
            File.WriteAllText(
                Path.Combine(modDirectory, "localization", "localization.csv"),
                "key,Source,english,japanese\n" +
                $"{ContentLocalizationKeys.ItemName(WoodGuid).Key},{WoodName},{WoodName},テスト木材\n");
            Localize.MergeGameDictionaries(new ModsResource(_temporaryDirectory), new[] { new ModId("author:inventory-tooltip") }, MasterSourceTextCollector.Collect());
        }
    }
}
```
（ホストの Attach の型は `HoverTooltipTestStage` の実物に合わせる。`using` は前例 `GameContentPresentationTest` の実物と揃える）

`InventoryGridKnobTest.cs`（USS に `calc()` が無いので、前計算した値を Web の式で検算する。前例 `CalcConstantRecheckTest` の評価器を使う）:
```csharp
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Theme.Calc;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Screens.Inventory
{
    /// <summary>
    /// 持ち物のグリッドの前計算のつまみを Web の calc で検算する（L2）
    /// Rechecks the inventory grid's precomputed knobs against the Web calc (L2)
    /// </summary>
    public class InventoryGridKnobTest
    {
        private static readonly Regex Declaration = new(@"^\s*(--[\w-]+)\s*:\s*([^;]+);(?:\s*/\*\s*(.*?)\s*\*/)?", RegexOptions.Multiline);

        [Test]
        public void 持ち物のつまみはトークンの積と一致する()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var declared = new Dictionary<string, string>();
            foreach (Match match in Declaration.Matches(File.ReadAllText(Path.Combine(root, "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/Tokens.uss"))))
            {
                declared[match.Groups[1].Value] = match.Groups[2].Value.Trim();
            }

            // calc: の注記を持つ行を全部検算し、3 行（大きさ・間・個数の字）があることも見る
            // Recheck every row with a calc: note and require the three rows (size, gap, count font)
            var evaluator = new CssCalcEvaluator(declared);
            var checkedRows = 0;
            foreach (Match match in Declaration.Matches(File.ReadAllText(Path.Combine(root, "moorestech_client/Assets/Scripts/Client.Ui/Screens/Inventory/Inventory.uss"))))
            {
                var comment = match.Groups[3].Value;
                if (!comment.StartsWith("calc: ")) continue;
                var value = float.Parse(match.Groups[2].Value.Trim().Replace("px", ""), CultureInfo.InvariantCulture);
                Assert.AreEqual(evaluator.Evaluate(comment.Substring(6).Split(';')[0]), value, 0.01f, match.Groups[1].Value);
                checkedRows++;
            }
            Assert.AreEqual(3, checkedRows);
        }
    }
}
```
（`CssCalcEvaluator` の名前空間と可視性は `CalcConstantRecheckTest` の実物で確かめる。internal なら同じアセンブリなので使える）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `InventoryView` 等が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`Parts/PlayerSlotGestureRouter.cs`（S4-alpha の Produces の振り分けの写し）:
```csharp
using Client.Ui.Components.Slots;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.SlotCommands.Player;
using Client.Ui.State.Toast;

namespace Client.Ui.Screens.Inventory.Parts
{
    /// <summary>
    /// プレイヤーの枠の身振りを型付きコマンドへ振り分け、結果をトーストへ渡す（持ち物と装備で共有）
    /// Routes player-slot gestures to typed commands and reports results (shared by inventory and equipment)
    /// </summary>
    public sealed class PlayerSlotGestureRouter
    {
        private readonly IPlayerSlotCommands _commands;
        private readonly UiCommandResultToaster _toaster;

        public PlayerSlotGestureRouter(IPlayerSlotCommands commands, UiCommandResultToaster toaster)
        {
            _commands = commands;
            _toaster = toaster;
        }

        public void Route(PlayerSlotRef slot, SlotGesture gesture)
        {
            switch (gesture.Kind)
            {
                case SlotGestureKind.LeftDown: Report(_commands.LeftDown(slot, gesture.Shift)); return;
                case SlotGestureKind.RightDown: Report(_commands.RightDown(slot)); return;
                case SlotGestureKind.DoubleClick: Report(_commands.DoubleClick(slot)); return;
                case SlotGestureKind.LeftEnter: _commands.LeftEnter(slot); return;
                case SlotGestureKind.RightEnter: Report(_commands.RightEnter(slot)); return;
                // silent-ok: ホバーはツールチップの宿主（C3-b）が ItemSlot の印から出す / InventoryPanelTooltipTest が見る
                default: return;
            }

            #region Internal

            void Report(UiCommandResult result)
            {
                _toaster.Report(PlayerSlotCommands.CommandName, result);
            }

            #endregion
        }
    }
}
```

`Parts/PlayerSlotContent.cs`（持ち物と装備の 2 つの View が使う。S4-alpha の `BlockItemGrid` にも同じ式があり、Content〈`Client.Ui/Content`〉への統合は integrator へ依頼済み。D-23）:
```csharp
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.Inventory.Parts
{
    /// <summary>
    /// プレイヤーの枠の中身を ItemSlot の中身へ写す（空きマスは絵も個数も持たない）
    /// Maps a player slot item to ItemSlot content (empty cells carry no icon or count)
    /// </summary>
    public static class PlayerSlotContent
    {
        public static ItemSlotContent From(GameContentPresentation content, SlotItem item, bool selected)
        {
            var icon = item.IsEmpty ? null : content.Item(item.ItemId);
            return new ItemSlotContent(icon, item.IsEmpty ? null : item.Count, new ItemSlotMarks(selected, false, false), null, null, null);
        }
    }
}
```

`Panel/InventorySlotAnchorMarks.cs`（`TutorialAnchor` に外す口が無いので、付けた id を覚えて自分で外す。D-8。id の組み立て `AnchorIds.InventoryItem` は Mark と同じこのファイルに置く。D-26）:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Anchor;

namespace Client.Ui.Screens.Inventory.Panel
{
    /// <summary>
    /// 持ち物の枠の所持品のアンカーを、描き直しのたびに付け替える
    /// Re-applies the owned-item anchors on inventory slots on every redraw
    /// </summary>
    public sealed class InventorySlotAnchorMarks
    {
        private readonly Dictionary<int, string> _applied = new();

        public void Apply(IReadOnlyList<ItemSlot> slots, IReadOnlyDictionary<int, string> itemGuids)
        {
            for (var index = 0; index < slots.Count; index++)
            {
                // id は Mark と同じファイルで組む（TutorialAnchorCoverageTest が Mark のファイルの AnchorIds を拾う）
                // Build the id beside Mark (TutorialAnchorCoverageTest collects AnchorIds from files that call Mark)
                var wanted = itemGuids.TryGetValue(index, out var guid) ? AnchorIds.InventoryItem(guid) : null;
                _applied.TryGetValue(index, out var current);
                if (wanted == current) continue;

                // 古い id は印のクラスごと外す（枠に他のアンカーは付かない。Web の持ち物の枠のアンカーも inventoryItemAnchorId の 1 種だけ）
                // Remove the old id with the marker class (slots carry no other anchor; the Web inventory slot also has only inventoryItemAnchorId)
                if (current != null)
                {
                    slots[index].RemoveFromClassList(current);
                    slots[index].RemoveFromClassList(TutorialAnchor.MarkerClassName);
                    _applied.Remove(index);
                }
                if (wanted == null) continue;
                TutorialAnchor.Mark(slots[index], wanted);
                _applied[index] = wanted;
            }
        }

        public void Clear()
        {
            _applied.Clear();
        }
    }
}
```

`Panel/InventoryView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Panel
{
    /// <summary>
    /// 持ち物パネル（Web features/inventory/InventoryPanel）。操作はプレイヤーの枠のコマンドと整理のコマンドへ
    /// Inventory panel (Web features/inventory/InventoryPanel); input goes to the player-slot and sort commands
    /// </summary>
    public sealed class InventoryView : VisualElement
    {
        // :root のトークンはグリッドのクラスで受け直して読む（UssCustomStyleReader は継承値を読まない）
        // The :root token is re-declared on the grid class and read there (UssCustomStyleReader does not read inherited values)
        private const string ColumnsProperty = "--inventory-view-columns";
        private readonly PlayerSlotGestureRouter _gestures;
        private readonly GameContentPresentation _content;
        private readonly SlotGrid _grid;
        private readonly List<ItemSlot> _slots = new();
        private readonly InventorySlotAnchorMarks _anchors = new();
        private int _columns;

        public InventoryView(PlayerSlotGestureRouter gestures, IInventorySortCommands sort, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            _gestures = gestures;
            _content = content;
            AddToClassList("inventory-view");

            // 題と、題の行の右端の整理ボタン（Web titleAction）
            // Title and the sort button at the right end of the title row (Web titleAction)
            var panel = new GamePanel("inventory-panel", GamePanelFrame.Default);
            panel.AddToClassList("inventory-view__panel");
            panel.SetTitle(new UiText(LocalizationKeys.Ui.Inventory.Title));
            var sortButton = new PanelActionButton("inventory-sort", LocalizationKeys.Ui.Inventory.Sort);
            sortButton.OnClicked.Subscribe(_ => toaster.Report(InventorySortCommands.CommandName, sort.Sort()));
            panel.SetTitleAction(sortButton);

            _grid = new SlotGrid("main-grid", 1);
            _grid.AddToClassList("inventory-view__grid");
            _grid.RegisterCallback<CustomStyleResolvedEvent>(_ => ReadColumns());
            panel.Add(_grid);
            Add(panel);
            SetShown(false);
        }

        public void Render(PlayerInventoryViewModel viewModel)
        {
            // 表示中だけ購読するのは Presenter（§4.2）。ここは受けた値をそのまま描く
            // The presenter subscribes only while shown (§4.2); this draws whatever it receives
            if (_slots.Count != viewModel.MainSlots.Count) Rebuild(viewModel.MainSlots.Count);
            for (var i = 0; i < _slots.Count; i++) _slots[i].SetContent(PlayerSlotContent.From(_content, viewModel.MainSlots[i], false));
            _anchors.Apply(_slots, viewModel.MainSlotItemGuids);

            #region Internal

            void Rebuild(int count)
            {
                _grid.ClearItems();
                _slots.Clear();
                _anchors.Clear();
                for (var i = 0; i < count; i++)
                {
                    var index = i;
                    var slot = new ItemSlot($"main-slot-{index}");
                    slot.OnSlotGestured.Subscribe(gesture => _gestures.Route(PlayerSlotRef.Main(index), gesture));
                    _grid.AddItem(slot);
                    _slots.Add(slot);
                }
                ApplyColumns();
            }

            #endregion
        }

        public void SetShown(bool shown)
        {
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ReadColumns()
        {
            if (UssCustomStyleReader.TryReadNumber(_grid.customStyle, ColumnsProperty, out var columns)) _columns = (int)columns;
            ApplyColumns();
        }

        private void ApplyColumns()
        {
            // silent-ok: トークンの解決前は列数を決めない（解決で CustomStyleResolvedEvent が当て直す。欠けは UssCustomStyleReader が LogError） / InventoryViewTest の 36 枠と並べ比べが見る
            if (_columns <= 0) return;
            _grid.SetColumns(_columns);
        }
    }
}
```

`Inventory.uss`（`.inventory-view` は `StageColumn.Inv` の枠の中身。パネルの寸法は Web の GamePanel の style、グリッドは `GRID_STYLE`）:
```css
/* web: features/inventory/InventoryPanel/index.tsx の GamePanel の style と GRID_STYLE、EquipmentPanel/style.module.css の .equipmentArea を写す（持ち物パネルと装備 HUD） */
/* web: copies the GamePanel style and GRID_STYLE in features/inventory/InventoryPanel/index.tsx and .equipmentArea in EquipmentPanel/style.module.css (inventory panel and equipment HUD) */

.inventory-view {
    align-items: flex-start;
}

.inventory-view__panel {
    align-self: flex-start;
    width: var(--inventory-panel-width);
    min-height: 452.391px;
    translate: 0.783px 0.783px;
    --panel-left: -2.22px;
    --panel-right: -2.22px;
    --title-shift-x: -1.96px;
    --title-scale-x: 0.919;
    --title-scale-y: 0.924;
}

.inventory-view__grid {
    --inventory-view-columns: var(--inventory-columns);
    --slot-size: 35.1776394px; /* calc: var(--inventory-slot-base-size) * var(--inventory-slot-scale) */
    --slot-grid-gap: 7.0815392px; /* calc: var(--inventory-slot-base-gap) * var(--inventory-slot-scale) */
    --count-font-size: 12.337807px; /* calc: var(--inventory-count-base-font-size) * var(--inventory-slot-scale) */
    --filled-face-inset: 1.565749px;
    --face-inset-color: var(--inventory-slot-face-inset-color);
    --icon-pad: 1px;
    --count-bottom: -1px;
    --count-letter-spacing: 12px; /* web: 0.12em。UITK の字間は字の大きさ/100 × 値なので em ×100（A-70） */
    margin-top: 12px;
    margin-left: -0.549px;
}
```
（装備 HUD の行は Task 5 で同じファイルに足す）

`THEME/DerivedTokens.uss` の `:root` の最後の `}` の直前（前のブロックとの間に空行 1 つ）:
```css
    /* ── S4-inventory: 持ち物のグリッドの枠の内側の色（Web GRID_STYLE の --face-inset-color。画面 USS に色を直書きしないためトークンにする） */
    /* ── S4-inventory: inner face colour of inventory grid slots (Web GRID_STYLE --face-inset-color; a token so the screen USS holds no colour literal) */
    --inventory-slot-face-inset-color: rgb(50, 52, 67); /* web: InventoryPanel GRID_STYLE "--face-inset-color": "rgb(50 52 67)" */
```

`TSS` の `Screens` 区画の末尾（`GrabOverlay.uss` の行の後。他レーンが先に足していればその後）:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/Inventory/Inventory.uss");
```

`docs/uitk/disposition.md` の albof.29 の行（`| 4 | \`e2e/tests/regression/itemSlot.spec.ts\` | インベントリスロットがマスタGuidの辞書名をtooltip表示する | 写す予定 | moorestech-albof.29…`）を消す（`InventoryPanelTooltipTest` のタグを足すこのコミットで消さないと parity が二重に数える）

- [ ] **Step 4: 緑を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Inventory|Client.Tests.Ui.Conventions|Client.Tests.Ui.Theme"`
Expected: `ErrorCount: 0`、全件 PASS（`UiThemeImportBundleTest`・`UiStyleConventionTest` を含む）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Inventory moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Inventory "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss docs/uitk/disposition.md
git commit -m "feat(uitk): 持ち物パネルの View と所持品のアンカーを足す（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 5: 装備 HUD の View（`Screens/Inventory/Equipment/`）

**Files:**
- Create: `S/Client.Ui/Screens/Inventory/Equipment/EquipmentHudView.cs`
- Modify: `S/Client.Ui/Screens/Inventory/Inventory.uss`（装備 HUD の行）
- Test: `S/Client.Tests/Ui/Screens/Inventory/EquipmentHudViewTest.cs`・`EquipmentHudFloorTokenTest.cs`

**Interfaces:**
- Consumes: Task 4 の `PlayerSlotGestureRouter`・`PlayerSlotContent`、Task 3 の `PlayerInventoryViewModel`、`AnchorIds.EquipmentHud`・`EquipmentSlot`・`EquipmentSelectedSlot`、`UiScreenTraitsTable`（テストで `Grab` を直接読む）
- Produces:
  ```csharp
  namespace Client.Ui.Screens.Inventory.Equipment
  public sealed class EquipmentHudView : VisualElement { public EquipmentHudView(PlayerSlotGestureRouter gestures, GameContentPresentation content); public void Render(PlayerInventoryViewModel viewModel); public void SetShown(bool shown); public void SetClicksAccepted(bool accepted); }
  ```
  （どの画面で出してクリックを受けるかは Task 8 の Presenter のローカル関数が traits から決める。D-21）

- [ ] **Step 1: 失敗するテストを書く**

`EquipmentHudViewTest.cs`:
```csharp
using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Routing;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class EquipmentHudViewTest : ScreenTestHost
    {
        private RecordingPlayerSlotCommands _commands;
        private EquipmentHudView _view;
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _commands = new RecordingPlayerSlotCommands();
            _view = new EquipmentHudView(new PlayerSlotGestureRouter(_commands, Toaster), new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture)));
            UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_view);
            _view.SetShown(true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">装備枠がアンカーを名乗り、選択中の枠には選択アンカーも付く</web-test>
        [Test]
        public void 装備枠がアンカーを名乗り選択枠は選択アンカーも持つ()
        {
            _view.Render(Model(2, 1));

            // Web の期待 ["equipment.hud","equipment.slot-0","equipment.slot-1 equipment.selected-slot"] と同じ並び
            // Same order as the Web expectation ["equipment.hud","equipment.slot-0","equipment.slot-1 equipment.selected-slot"]
            var anchors = _view.Query(className: TutorialAnchor.MarkerClassName).ToList()
                .Select(element => string.Join(" ", element.GetClasses().Where(name => name.StartsWith("equipment."))))
                .ToArray();
            CollectionAssert.AreEqual(new[] { AnchorIds.EquipmentHud, AnchorIds.EquipmentSlot(0), $"{AnchorIds.EquipmentSlot(1)} {AnchorIds.EquipmentSelectedSlot}" }, anchors);
            Assert.IsTrue(Find<VisualElement>(_view, "equipment-slot-1").ClassListContains("slot-frame--selected"));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">pauseMenu 中の左押下はハンドラごと無く、アクションも飛ばない</web-test>
        [Test]
        public void ポーズ中の左押下はコマンドを呼ばない()
        {
            PressLeftOnSlotZeroIn(UiScreen.PauseMenu);
            Assert.IsEmpty(_commands.Calls);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">GameScreen 中も同様にクリックを受けない</web-test>
        [Test]
        public void ゲーム画面の左押下もコマンドを呼ばない()
        {
            PressLeftOnSlotZeroIn(UiScreen.None);
            Assert.IsEmpty(_commands.Calls);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">持ち物画面では左押下が掴み取りを送る</web-test>
        [Test]
        public void 持ち物画面の左押下はプレイヤーの枠のコマンドへ渡る()
        {
            PressLeftOnSlotZeroIn(UiScreen.PlayerInventory);
            CollectionAssert.AreEqual(new[] { "LeftDown Equipment:0 shift=False" }, _commands.Calls);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">装備枠0でも装備HUDは面積を保ちアンカーが解決する</web-test>
        [Test]
        public void 装備枠が0でもHUDは面積を持ちアンカーを名乗る()
        {
            _view.Render(Model(0, 0));
            simulate.FrameUpdate();

            Assert.IsTrue(_view.ClassListContains(AnchorIds.EquipmentHud));
            Assert.Greater(_view.worldBound.width, 0f);
            Assert.Greater(_view.worldBound.height, 0f);
        }

        private void PressLeftOnSlotZeroIn(UiScreen screen)
        {
            // クリックを受けるのは掴める画面だけ（Web useGrabInteractive。本番は EquipmentHudPresenter が同じ traits を読む）
            // Clicks are accepted only on grab screens (Web useGrabInteractive; in production EquipmentHudPresenter reads the same traits)
            _view.SetClicksAccepted(UiScreenTraitsTable.Get(screen).Grab);
            _view.Render(Model(2, 0));
            simulate.FrameUpdate();
            var frame = Find<VisualElement>(_view, "equipment-slot-0");
            simulate.MouseDown(frame.worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(frame.worldBound.center, MouseButton.LeftMouse);
        }

        private static PlayerInventoryViewModel Model(int equipmentSlots, int selected)
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var equipment = Enumerable.Repeat(empty, equipmentSlots).ToArray();
            return new PlayerInventoryViewModel(new[] { empty }, equipment, selected, new System.Collections.Generic.Dictionary<int, string>());
        }
    }
}
```
（`UiLayerStack.Build` は `GrabOverlayFlowTest` の前例）

`EquipmentHudFloorTokenTest.cs`（床トークン: 装備の列の下端はホットバーと同じ `--hotbar-bottom`、右は `--equipment-right`、枠はホットバーと同じ大きさ。`[TestFixture]` で 720p と 2560×1080 を回す。2560×1080 は実行時の舞台と同じ「論理 1706.6666×720・`pixelsPerPoint` 1.5」で撮る〈前例 `Layers/StageColumnsLayoutTest.cs`・`Layers/UiStageFrameTest.cs`・`Screens/Challenge/CurrentChallengeHudWideTest.cs`〉）:
```csharp
using System.Linq;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    [TestFixture(1280f, 720f, 1f)]
    [TestFixture(1706.6666f, 720f, 1.5f)]
    public class EquipmentHudFloorTokenTest : ScreenTestHost
    {
        // Tokens.uss の --hotbar-bottom 2px・--equipment-right 24px・--hotbar-slot-size 51.68px
        // Tokens.uss --hotbar-bottom 2px, --equipment-right 24px, --hotbar-slot-size 51.68px
        private const float HotbarBottom = 2f;
        private const float EquipmentRight = 24f;
        private const float HotbarSlotSize = 51.68f;
        private readonly Vector2 _logicalPanelSize;
        private readonly float _pixelsPerPoint;
        private Texture2D _texture;

        public EquipmentHudFloorTokenTest(float width, float height, float panelPixelsPerPoint)
        {
            _logicalPanelSize = new Vector2(width, height);
            _pixelsPerPoint = panelPixelsPerPoint;
        }

        [OneTimeSetUp]
        public void SetUpLogicalPanelSize()
        {
            panelSize = _logicalPanelSize;
        }

        [SetUp]
        public void SetUpPanelPixelsPerPoint()
        {
            pixelsPerPoint = _pixelsPerPoint;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        [Test]
        public void 装備の列はホットバーと同じ床と枠の大きさで右下に立つ()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            var view = new EquipmentHudView(new PlayerSlotGestureRouter(new RecordingPlayerSlotCommands(), Toaster), new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture)));
            var layer = UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Chrome, UiFamily.Viewport);
            layer.Add(view);
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            view.SetShown(true);
            view.Render(new PlayerInventoryViewModel(new[] { empty }, new[] { empty, empty, empty }, 0, new System.Collections.Generic.Dictionary<int, string>()));
            simulate.FrameUpdate();

            Assert.AreEqual(layer.worldBound.yMax - HotbarBottom, view.worldBound.yMax, 0.5f, "床");
            Assert.AreEqual(layer.worldBound.xMax - EquipmentRight, view.worldBound.xMax, 0.5f, "右");
            var slot = view.Query<VisualElement>(className: "item-slot").First();
            Assert.AreEqual(HotbarSlotSize, slot.worldBound.width, 0.5f, "枠の大きさ");
        }
    }
}
```
（枠の外寸の取り方〈`item-slot` のクラス名・リングの外側成長の有無〉は C3-a の `SlotFrame` の L1 の測り方に合わせる。`worldBound` が枠の外寸と違えば、同じ L1 が使う要素で測る）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `EquipmentHudView` が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`Equipment/EquipmentHudView.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Equipment
{
    /// <summary>
    /// 装備 HUD（Web features/inventory/EquipmentPanel）。選択はホイールだけで、クリックはアイテム移動
    /// Equipment HUD (Web features/inventory/EquipmentPanel); selection is wheel-only and clicks move items
    /// </summary>
    public sealed class EquipmentHudView : VisualElement
    {
        private readonly PlayerSlotGestureRouter _gestures;
        private readonly GameContentPresentation _content;
        private readonly List<ItemSlot> _slots = new();
        private bool _clicksAccepted;

        public EquipmentHudView(PlayerSlotGestureRouter gestures, GameContentPresentation content)
        {
            _gestures = gestures;
            _content = content;
            name = "equipment-slots";
            AddToClassList("equipment-hud");

            // HUD 自体のアンカーは常に出す。列は実 UI として拾う（Web pointer-events: auto）
            // The HUD's own anchor is always present; the column picks as real UI (Web pointer-events: auto)
            TutorialAnchor.Mark(this, AnchorIds.EquipmentHud);
            pickingMode = PickingMode.Position;
            SetShown(false);
        }

        public void SetClicksAccepted(bool accepted)
        {
            _clicksAccepted = accepted;
        }

        public void SetShown(bool shown)
        {
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Render(PlayerInventoryViewModel viewModel)
        {
            // 表示中だけ購読するのは Presenter（§4.2）。ここは受けた値をそのまま描く
            // The presenter subscribes only while shown (§4.2); this draws whatever it receives
            if (_slots.Count != viewModel.EquipmentSlots.Count) Rebuild(viewModel.EquipmentSlots.Count);
            for (var i = 0; i < _slots.Count; i++)
            {
                // 選択枠はホイールで動くので、選択アンカーもその枠へ移す
                // The selected slot moves with the wheel, so the selection anchor moves with it
                var selected = i == viewModel.SelectedEquipment;
                _slots[i].SetContent(PlayerSlotContent.From(_content, viewModel.EquipmentSlots[i], selected));
                _slots[i].EnableInClassList(AnchorIds.EquipmentSelectedSlot, selected);
            }

            #region Internal

            void Rebuild(int count)
            {
                foreach (var slot in _slots) slot.RemoveFromHierarchy();
                _slots.Clear();
                for (var i = 0; i < count; i++)
                {
                    var index = i;
                    var slot = new ItemSlot($"equipment-slot-{index}");
                    slot.AddToClassList("equipment-hud__slot");
                    if (0 < index) slot.AddToClassList("equipment-hud__slot--after-first");
                    TutorialAnchor.Mark(slot, AnchorIds.EquipmentSlot(index));
                    slot.OnSlotGestured.Subscribe(gesture => OnGestured(index, gesture));
                    Add(slot);
                    _slots.Add(slot);
                }
            }

            #endregion
        }

        private void OnGestured(int index, SlotGesture gesture)
        {
            // silent-ok: 掴めない画面では装備の枠はクリックを受けない（Web はハンドラごと付けない） / EquipmentHudViewTest が見る
            if (!_clicksAccepted) return;
            _gestures.Route(PlayerSlotRef.Equipment(index), gesture);
        }
    }
}
```

`Inventory.uss` の末尾に足す:
```css
.equipment-hud {
    position: absolute;
    right: var(--equipment-right);
    bottom: var(--equipment-bottom);
    flex-direction: column;
    min-width: var(--equipment-slot-size);
    min-height: var(--equipment-slot-size);
    --slot-size: var(--equipment-slot-size);
}

/* web: gap: var(--equipment-slot-gap)。USS に gap が無いので 2 枠目から上の余白で写す */
/* web: gap: var(--equipment-slot-gap); USS has no gap, so slots after the first get a top margin */
.equipment-hud__slot--after-first {
    margin-top: var(--equipment-slot-gap);
}
```
（Web の `z-index: var(--z-stage-overlay-panel)` は Chrome 層に載せることで写す〈S3-hotbar-modal と同じ層〉。差が出れば Task 10 の差の表へ）

- [ ] **Step 4: 緑を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Inventory"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Inventory moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Inventory
git commit -m "feat(uitk): 装備 HUD の View と床トークンを足す（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 6: 本物のスロット操作で通す（持ち物と装備の身振りの流れ）

**Files:**
- Create: `S/Client.Tests/Ui/Screens/Inventory/Support/InventoryScreenFixture.cs`
- Test: `S/Client.Tests/Ui/Screens/Inventory/InventoryPanelGestureTest.cs`・`InventoryEquipmentFlowTest.cs`
- Modify: `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs`（S4-inventory 持ちの 4 行を消す。S2-tutorial #1627 はマージ済み）

**Interfaces:**
- Consumes: S4-alpha の `PlayerSlotCommands`（と alpha の L0 の部品 `PlannedItemActionRunner`・`BlockItemActionExecutor`・`PlayerSlotContextReader`・`BlockSlotRestrictionInputBuilder`・`ShiftMoveRefusalNotifier`、テスト用の `RecordingBlockInventoryOperations`・`FakePanelSource`・`FixedMaxStackLookup`。名前空間は alpha 計画 85f905aa3d の `PlayerSlotCommandsTest` の `using` のとおり）、C4-e の `PlayerSlotDragCommands`・`PlayerItemActionExecutor`・`FakePrimaryPointerButton`、Task 2〜5 の型
- Produces: なし（L1 だけ）

- [ ] **Step 1: 土台を書く**

`Support/InventoryScreenFixture.cs`（本物のプレイヤー側のコマンドを、ブロックが閉じた文脈で組む。組み方は alpha の `PlayerSlotCommandsTest` の SetUp と C4-e の `GrabOverlayFlowTest` の SetUp を合わせた写し）:
```csharp
using System;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.Inventory.Drag;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Tests.Ui.State.ItemMove;
using Client.Tests.Ui.State.SlotCommands;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.SlotCommands;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.SlotCommands.Player;
using Client.Ui.State.Toast;
using Server.Event.Notification;
using UniRx;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    /// <summary>
    /// 持ち物と装備の L1 で使う本物のスロット操作一式（ブロックは閉じている）
    /// Real slot-command set for the inventory and equipment L1 (block closed)
    /// </summary>
    public sealed class InventoryScreenFixture
    {
        public RecordingPlayerInventoryOperations Operations { get; private set; }
        public FakePrimaryPointerButton Button { get; private set; }
        public PlayerSlotDragCommands Drag { get; private set; }
        public PlayerSlotCommands Commands { get; private set; }
        public FakeEquipmentSelectionPort Equipment { get; private set; }
        public PlayerInventoryPanelModel Model { get; private set; }

        public InventoryScreenFixture(int mainSlots, int equipmentSlots, UiCommandResultToaster toaster)
        {
            Operations = new RecordingPlayerInventoryOperations(mainSlots, equipmentSlots);
            var executor = new PlayerItemActionExecutor(Operations);
            Button = new FakePrimaryPointerButton(true);
            Drag = new PlayerSlotDragCommands(Operations, executor, Button, toaster);
            Drag.Initialize();

            // alpha の PlayerSlotCommandsTest と同じ組み方（パネルは閉じたまま）
            // Same build as alpha's PlayerSlotCommandsTest (the panel stays closed)
            var runner = new PlannedItemActionRunner(executor, new BlockItemActionExecutor(new RecordingBlockInventoryOperations(), Operations));
            var reader = new PlayerSlotContextReader(new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed)), new BlockSlotRestrictionInputBuilder(Array.Empty<IBlockSlotRestrictionContributor>()), new FixedMaxStackLookup(64));
            Commands = new PlayerSlotCommands(Operations, Drag, runner, reader, new ShiftMoveRefusalNotifier(new ClientLocalNotificationSource()));

            // 置く・移すは記録係が合図を流す。装備の選択の Fake は本番と同じく同じ 1 本の合図へ繋ぐ（本番は LocalPlayerEquipment の合図が中身と選択の両方を流す）
            // The recorder signals puts and moves; the selection fake joins the same single signal as in production (LocalPlayerEquipment signals both)
            Equipment = new FakeEquipmentSelectionPort(equipmentSlots);
            Equipment.OnSelectionChanged.Subscribe(_ => Operations.RaiseSnapshotChanged());
            Model = new PlayerInventoryPanelModel(Operations, Equipment);
            Model.Initialize();
        }
    }
}
```
（ctor の並びは alpha 計画 85f905aa3d の `PlayerSlotCommandsTest` の SetUp と同じ）

- [ ] **Step 2: 失敗するテストを書く**

`InventoryPanelGestureTest.cs`:
```csharp
using System;
using System.Linq;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Ui.Content;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryPanelGestureTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid StoneGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        private InventoryScreenFixture _fixture;
        private InventoryView _view;
        private Texture2D _texture;
        private ItemId _wood;
        private ItemId _stone;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _stone = MasterHolder.ItemMaster.GetItemId(StoneGuid);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _fixture = new InventoryScreenFixture(36, 3, Toaster);
            var content = new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));
            _view = Mount(new InventoryView(new PlayerSlotGestureRouter(_fixture.Commands, Toaster), new RecordingInventorySortCommands(), content, Toaster));
            _view.SetShown(true);
            _fixture.Model.Current.Subscribe(_view.Render);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">ダブルクリックで同種を集約し、collect はクリックされたスロットを送る</web-test>
        [Test]
        public void ダブルクリックは集める操作を1回だけ呼びトーストを出さない()
        {
            _fixture.Operations.SetMain(0, new SlotItem(_wood, 10));
            _fixture.Operations.SetMain(2, new SlotItem(_wood, 5));
            simulate.FrameUpdate();

            simulate.DoubleClick(Frame(0));
            _fixture.Button.Release();
            _fixture.Drag.LateTick();

            // 集める先（掴みか枠か）は計画が決める。Web と同じく押した枠を起点に 1 回だけ集め、良性の失敗はトーストにしない
            // The planner decides the target (grab or slot); as on the Web, collect runs once from the clicked slot and benign failures do not toast
            Assert.AreEqual(1, _fixture.Operations.Collects.Count, string.Join(",", _fixture.Operations.Collects));
            Assert.IsEmpty(ShownToasts);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">右クリックで inventory.split を送る</web-test>
        [Test]
        public void 空手の右押下は半分を掴む()
        {
            _fixture.Operations.SetMain(0, new SlotItem(_wood, 10));
            simulate.FrameUpdate();

            simulate.MouseDown(Frame(0).worldBound.center, MouseButton.RightMouse);
            simulate.MouseUp(Frame(0).worldBound.center, MouseButton.RightMouse);

            CollectionAssert.Contains(_fixture.Operations.Moves, "Main:0->Grab x5");
            Assert.AreEqual(5, _fixture.Operations.ReadGrab().Count);
        }

        /// <web-test file="e2e/tests/inventory/inventory.spec.ts">grab 中に別アイテムのスロットを押すと入れ替えの move を送りトーストを出さない</web-test>
        [Test]
        public void 掴み中に別アイテムの枠を押すと入れ替えてトーストを出さない()
        {
            _fixture.Operations.SetMain(0, new SlotItem(_wood, 10));
            _fixture.Operations.SetMain(1, new SlotItem(_stone, 2));
            simulate.FrameUpdate();

            PressAndRelease(0);
            PressAndRelease(1);

            Assert.AreEqual("Grab->Main:1 x10", _fixture.Operations.Moves.Last());
            Assert.AreEqual(_wood, _fixture.Operations.ReadMain(1).ItemId);
            Assert.AreEqual(_stone, _fixture.Operations.ReadGrab().ItemId);
            Assert.IsEmpty(ShownToasts);
        }

        private VisualElement Frame(int index)
        {
            return Find<VisualElement>(_view, $"main-slot-{index}");
        }

        private void PressAndRelease(int index)
        {
            // 左押下 → 解放 → LateTick で分割ドラッグが確定する（C4-e D-29）
            // Left press, release and LateTick commit the split drag (C4-e D-29)
            simulate.MouseDown(Frame(index).worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(Frame(index).worldBound.center, MouseButton.LeftMouse);
            _fixture.Button.Release();
            _fixture.Drag.LateTick();
            _fixture.Button.SetPressed(true);
        }
    }
}
```
（`SetMain` は記録係が `OnSnapshotChanged` を流すので、置いた直後に Model が描き直しを配る。石の GUID〈…0002〉はテスト mod の実物で確かめる）

`InventoryEquipmentFlowTest.cs`（持ち物と装備 HUD を並べ、本物の操作で枠をまたぐ）:
```csharp
using System;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Items;
using Client.Ui.State.Routing;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryEquipmentFlowTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private InventoryScreenFixture _fixture;
        private InventoryView _inventory;
        private EquipmentHudView _equipment;
        private Texture2D _texture;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _fixture = new InventoryScreenFixture(36, 3, Toaster);
            var content = new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));
            var gestures = new PlayerSlotGestureRouter(_fixture.Commands, Toaster);
            var layers = UiLayerStack.Build(rootVisualElement);
            _inventory = new InventoryView(gestures, new RecordingInventorySortCommands(), content, Toaster);
            layers.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(_inventory);
            _equipment = new EquipmentHudView(gestures, content);
            layers.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_equipment);
            _inventory.SetShown(true);
            _equipment.SetShown(true);
            _equipment.SetClicksAccepted(UiScreenTraitsTable.Get(UiScreen.PlayerInventory).Grab);
            _fixture.Model.Current.Subscribe(model =>
            {
                _inventory.Render(model);
                _equipment.Render(model);
            });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">メインのアイテムをUI操作だけで装備スロットへ移せる</web-test>
        [Test]
        public void メインのアイテムを押下だけで装備の枠へ移せる()
        {
            MoveMainOneToEquipmentOne();
            Assert.AreEqual(10, _fixture.Operations.ReadSnapshot().Equipment[1].Count);
            Assert.IsTrue(_fixture.Operations.ReadGrab().IsEmpty, "掴みが空になる");
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">装備枠のクリックは選択ではなくアイテム移動になる</web-test>
        [Test]
        public void 装備の枠のクリックは選択ではなく移動になる()
        {
            _fixture.Operations.SetEquipment(0, new SlotItem(_wood, 1));
            Republish();

            PressAndRelease(Find<VisualElement>(_equipment, "equipment-slot-0"));

            CollectionAssert.Contains(_fixture.Operations.Moves, "Equipment:0->Grab x1");
            Assert.IsEmpty(_fixture.Equipment.Selections);
        }

        private void MoveMainOneToEquipmentOne()
        {
            _fixture.Operations.SetMain(1, new SlotItem(_wood, 10));
            Republish();
            PressAndRelease(Find<VisualElement>(_inventory, "main-slot-1"));
            PressAndRelease(Find<VisualElement>(_equipment, "equipment-slot-1"));
        }

        private void Republish()
        {
            // 置いた時点で記録係が合図を流しているので、レイアウトを進めるだけ
            // The recorder already signalled on the put, so only advance layout
            simulate.FrameUpdate();
        }

        private void PressAndRelease(VisualElement frame)
        {
            simulate.MouseDown(frame.worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(frame.worldBound.center, MouseButton.LeftMouse);
            _fixture.Button.Release();
            _fixture.Drag.LateTick();
            _fixture.Button.SetPressed(true);
            simulate.FrameUpdate();
        }
    }
}
```

- [ ] **Step 3: S2-tutorial の保留の行を消す**

Run: `grep -n "S4-inventory" moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs`
Expected: 4 行（`EquipmentHud`・`EquipmentSelectedSlot`・`EquipmentSlotPrefix`・`InventoryItemPrefix`）。消して、網羅テスト（`TutorialAnchorCoverageTest`。名前空間 `Client.Tests.Ui.Anchor`。走査は `Scripts/Client.Ui` の下で `TutorialAnchor.Mark(` を含むファイルの `AnchorIds.<名前>` だけを拾い、「印あり かつ 表にあり」と「印も表の行も無い」で落ちる）を Step 4 で回す。4 つの id は `EquipmentHudView`（装備の 3 つ）と `InventorySlotAnchorMarks`（`AnchorIds.InventoryItem`。D-26）が Mark と同じファイルで書くので拾われる

- [ ] **Step 4: 緑を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Inventory|Client.Tests.Ui.Screens.GrabOverlay|Client.Tests.Ui.Anchor"`
Expected: `ErrorCount: 0`、全件 PASS。`TutorialAnchorCoverageTest` が緑（4 つの id が Client.Ui の Mark のファイルから拾われる）

- [ ] **Step 5: コミット**（この Task で触ったファイルだけを足す。`*.cs*` で Unity が作った `.meta` も拾う）

```bash
T=moorestech_client/Assets/Scripts/Client.Tests/Ui
git add $T/Screens/Inventory/InventoryPanelGestureTest.cs* $T/Screens/Inventory/InventoryEquipmentFlowTest.cs* $T/Screens/Inventory/Support/InventoryScreenFixture.cs* $T/Anchor/TutorialAnchorCoveragePending.cs
git commit -m "test(uitk): 持ち物と装備の身振りを本物のスロット操作で通す（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 7: ホイールの入力と流れ（`Client.Input/Pointer/`・`Screens/Inventory/Wheel/`）

**Files:**
- Create: `S/Client.Ui/Foundation/Input/WebWheelScale.cs`、`S/Client.Input/Pointer/InputSystemPointerWheelStream.cs`、`S/Client.Ui/Screens/Inventory/Wheel/EquipmentWheelPickJudge.cs`・`IEquipmentWheelPointerPicker.cs`・`EquipmentWheelPointerPicker.cs`・`EquipmentWheelNotchScale.cs`・`EquipmentWheelInput.cs`
- Modify: `S/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs`（private 定数を共有の定数の読みに置き換える。E-1）
- Test: `S/Client.Tests/Input/Pointer/InputSystemPointerWheelStreamTest.cs`・`ScrollDeltaBehaviorSettingTest.cs`・`InputTestDriver.cs`、`S/Client.Tests/Ui/Screens/Inventory/EquipmentWheelFlowTest.cs`・`Wheel/EquipmentWheelInputFlowTest.cs`・`Support/UiRoutingStateTestFactory.cs`・`Support/EquipmentWheelRouterFixture.cs`・`Support/FakeEquipmentWheelPointerPicker.cs`、`InventoryEquipmentFlowTest.cs` に 1 件足す

**Interfaces:**
- Consumes: Task 2 の `EquipmentWheelRouter`、`UiRoutingState`・`ModalRequestService`・`PlaceSystemStateController`・`GameUiRoot`・`InputSystemPointerPositionStream`・`HybridInput`、`CrosshairHudView`（S2-hud の実物）、InputSystem の `InputState.currentUpdateType`、前例 `DeleteInputFixture.Frame`（更新の種類を選んで回す反射）・`IUiScreenPointer`／`FakeUiScreenPointer`（パネル座標の変換を口の外へ出す）・`UiScreenToPanel.FromBottomLeft`（座標の換算の唯一の式 [A-133]）・`InputManager.IsCursorLocked`（ロック状態の正本 [A-134]）
- Produces:
  ```csharp
  namespace Client.Input.Pointer
  public sealed class InputSystemPointerWheelStream { public IObservable<float> OnWheelNotchesScrolled { get; } } // 縦のノッチ（上が正）。0 と Editor の更新のものは流さない
  namespace Client.Ui.Foundation.Input
  public static class WebWheelScale { public const float PixelsPerNotch = 60f; } // A-71。GameScrollView と装備のホイールが読む
  namespace Client.Ui.Screens.Inventory.Wheel
  public static class EquipmentWheelPickJudge { public static bool IsBlockingUi(VisualElement picked); }   // Task 9 で素通しの印を足す
  public interface IEquipmentWheelPointerPicker { VisualElement PickAt(Vector2 screenPosition); }
  public sealed class EquipmentWheelPointerPicker : IEquipmentWheelPointerPicker { public EquipmentWheelPointerPicker(GameUiRoot root); }
  public static class EquipmentWheelNotchScale { public static double ToDownwardPixels(float notchesUp); } // A-71 の 60px/ノッチ。下が正
  public sealed class EquipmentWheelInput : IInitializable
  ```

- [ ] **Step 1: 60px を Foundation の共有の定数へ上げる**（E-1〈反映済み ad92a44777〉が「`GameScrollView` の外で最初に要したレーンが上げてよい」とする。D-10）

Run: `grep -rn "PixelsPerNotch" moorestech_client/Assets/Scripts/Client.Ui`
Expected: `GameScrollView.cs` の `private const float WebWheelPixelsPerNotch = 60f;` の 1 か所だけ。既に共有の定数があれば以下を作らずそれを読み、名前を `done.md`「証拠」に書く

`S/Client.Ui/Foundation/Input/WebWheelScale.cs`:
```csharp
namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// Web（CEF）のホイール 1 ノッチの px（A-71）。ノッチを px に換算する UITK の読み手はすべてここを読む
    /// Pixels per wheel notch on the Web (CEF) (A-71); every UITK reader converting notches to pixels reads this
    /// </summary>
    public static class WebWheelScale
    {
        // CefUnity の ScrollInputPipeline.WheelPixelsPerStep と同じ値
        // Same value as CefUnity ScrollInputPipeline.WheelPixelsPerStep
        public const float PixelsPerNotch = 60f;
    }
}
```

`GameScrollView.cs`: `private const float WebWheelPixelsPerNotch = 60f;` とその上の 2 行コメントを消し、`using Client.Ui.Foundation.Input;` を足して `mouseWheelScrollSize = WebWheelScale.PixelsPerNotch / WheelEvent.scrollDeltaPerTick;` にする（コメントは「Web（CEF）の 1 ノッチの px（A-71）を UITK の WheelEvent の行へ直す」の日英 2 行に直す）。値は変わらないので、既存の `GameScrollViewTest`（ホイール 1 ノッチで 60px 動く）が回帰を見る

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GameScrollView"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 2: 失敗するテストを書く**

`S/Client.Tests/Input/Pointer/InputSystemPointerWheelStreamTest.cs`（前例 `InputSystemPointerPositionStreamTest`）:
```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Input.Pointer;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Client.Tests.Input.Pointer
{
    /// <summary>
    /// ホイールが回った時だけ縦のノッチを流し、マウスが無ければ警告 1 回
    /// Publishes vertical notches only when the wheel turns; warns once without a mouse
    /// </summary>
    public class InputSystemPointerWheelStreamTest : InputTestFixture
    {
        [Test]
        public void 下へ1ノッチ回すとマイナス1を1回流す()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var received = Subscribe(new InputSystemPointerWheelStream());

            Set(mouse.scroll, new Vector2(0f, -1f));

            Assert.AreEqual(new[] { -1f }, received.ToArray());
        }

        [Test]
        public void ホイールの無い位置だけの変化は流さない()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var received = Subscribe(new InputSystemPointerWheelStream());

            Set(mouse.position, new Vector2(50f, 60f));

            Assert.IsEmpty(received);
        }

        [Test]
        public void Editorの更新で届いたホイールは流さない()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var received = Subscribe(new InputSystemPointerWheelStream());
            var rawUpdates = new List<InputUpdateType>();
            InputSystem.onEvent += (_, device) => { if (device == mouse) rawUpdates.Add(InputState.currentUpdateType); };

            // Game View にフォーカスが無いと、ポインタのイベントは Editor の更新で処理される（InputManager.cs:3320-3345）
            // Without Game View focus, pointer events are processed in the Editor update (InputManager.cs:3320-3345)
            InputTestDriver.LoseGameFocus(this);
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, -1f) });
            InputTestDriver.RunUpdate(InputUpdateType.Editor);

            // 前提: 生の onEvent は Editor の更新で受けた（受けていなければこの件は何も試していない）
            // Premise: the raw onEvent saw it in an Editor update (otherwise this case tests nothing)
            CollectionAssert.Contains(rawUpdates, InputUpdateType.Editor);
            Assert.IsEmpty(received);
        }

        [Test]
        public void マウスが無いと警告を出して何も流さない()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"Mouse\.current is null"));
            var received = Subscribe(new InputSystemPointerWheelStream());

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.aKey);

            Assert.IsEmpty(received);
        }

        private static List<float> Subscribe(InputSystemPointerWheelStream stream)
        {
            var received = new List<float>();
            stream.OnWheelNotchesScrolled.Subscribe(received.Add);
            return received;
        }
    }
}
```

`S/Client.Tests/Input/Pointer/ScrollDeltaBehaviorSettingTest.cs`（`InputTestFixture` は設定を既定へ差し替えるので継がない。プロジェクトの入力設定〈`StarterAssets.inputsettings.asset`〉をそのまま読む）:
```csharp
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.Input.Pointer
{
    /// <summary>
    /// プロジェクトの入力設定がホイールを 1 ノッチ＝1 に正規化することを固定する（装備のホイールの 60px 換算の前提。D-10）
    /// Pins that the project input settings normalize the wheel to one per notch (premise of the equipment wheel's 60px scaling, D-10)
    /// </summary>
    public class ScrollDeltaBehaviorSettingTest
    {
        [Test]
        public void ホイールの値は全機種で1ノッチ1に正規化される()
        {
            // 設定が変わると Windows の 120 がそのまま届き、1 ノッチで 2 段以上の量になる
            // If this changes, Windows' 120 arrives raw and one notch becomes worth several steps
            Assert.AreEqual(InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms, InputSystem.settings.scrollDeltaBehavior);
        }
    }
}
```

`S/Client.Tests/Input/Pointer/InputTestDriver.cs`（InputSystem 1.18.0 で internal の 2 つ〈`InputSystem.Update(InputUpdateType)` と `InputTestFixture.runtime`〉を、テスト専用のこの補助の反射にまとめる。前例 `DeleteInputFixture.Frame`）:
```csharp
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Tests.Input.Pointer
{
    /// <summary>
    /// テストから internal の InputSystem の操作（更新の種類を選んで回す・Game View の焦点を外す）を呼ぶ
    /// Calls internal InputSystem operations from tests (run a chosen update type, drop Game View focus)
    /// </summary>
    public static class InputTestDriver
    {
        public static void RunUpdate(InputUpdateType updateType)
        {
            typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { updateType });
        }

        public static void LoseGameFocus(InputTestFixture fixture)
        {
            // runtime も InputTestRuntime も internal なので、取り出しと呼び出しの両方を反射で行う
            // Both runtime and InputTestRuntime are internal, so fetch and call through reflection
            var runtime = typeof(InputTestFixture).GetProperty("runtime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(fixture);
            runtime.GetType().GetMethod("PlayerFocusLost", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(runtime, null);
        }
    }
}
```
（`runtime` がプロパティかフィールドか、`PlayerFocusLost` の綴りは InputSystem 1.18.0 の `InputTestFixture.cs`・`InputTestRuntime.cs` で確かめ、違えば合わせる）
（Editor の更新でイベントが処理される条件〈フォーカス・`editorInputBehaviorInPlayMode`・`backgroundBehavior`〉は InputSystem 1.18.0 の `InputManager.cs:3320-3345` に合わせて実物で確かめる。前提の assert〈生の onEvent が Editor で受けた〉が赤なら、条件の与え方を直す。前提の assert は外さない）

`Support/FakeEquipmentWheelPointerPicker.cs`（パネル座標の変換はテストのパネルで動かないので口の外に出す。前例 `FakeUiScreenPointer`）:
```csharp
using Client.Ui.Screens.Inventory.Wheel;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    public sealed class FakeEquipmentWheelPointerPicker : IEquipmentWheelPointerPicker
    {
        // 世界の上（何も拾わない）
        // Over the world (nothing picked)
        public VisualElement PickAt(Vector2 screenPosition)
        {
            return null;
        }
    }
}
```

`S/Client.Tests/Ui/Screens/Inventory/Wheel/EquipmentWheelInputFlowTest.cs`（流れ → `EquipmentWheelInput` → ルーター → 選択の口を本物で通す。`InputTestFixture` は `ScreenTestHost` と両方は継げないので合成で使う。D-25）:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Input.Pointer;
using Client.Tests.Input.Pointer;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Wheel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Tests.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// ホイールの流れからルーターまでを通し、Editor の更新では選ばずゲームの更新では 2 ノッチで 1 段進むことを見る
    /// Drives the wheel stream through to the router: no selection on Editor updates, one step per two notches on game updates
    /// </summary>
    public class EquipmentWheelInputFlowTest : ScreenTestHost
    {
        private InputTestFixture _input;
        private GameObject _host;
        private FakeEquipmentSelectionPort _port;
        private Mouse _mouse;
        private List<InputUpdateType> _rawWheelUpdates;

        [SetUp]
        public void SetUp()
        {
            _input = new InputTestFixture();
            _input.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _host = new GameObject("equipment-wheel-input-flow");
            _port = new FakeEquipmentSelectionPort(3);
            var router = new EquipmentWheelRouterFixture(_host, UIStateEnum.GameScreen, _port).Router;
            new EquipmentWheelInput(new InputSystemPointerWheelStream(), new InputSystemPointerPositionStream(), new FakeEquipmentWheelPointerPicker(), router).Initialize();
            _rawWheelUpdates = new List<InputUpdateType>();
            InputSystem.onEvent += RecordRawWheelUpdate;
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.onEvent -= RecordRawWheelUpdate;
            Object.DestroyImmediate(_host);
            _input.TearDown();
        }

        [Test]
        public void Editorの更新で届いたホイールでは装備を選ばない()
        {
            // Inspector などにフォーカスがあるとき、ポインタのイベントは Editor の更新で届く
            // With focus on e.g. the Inspector, pointer events arrive in the Editor update
            InputTestDriver.LoseGameFocus(_input);
            WheelDownOneNotch(InputUpdateType.Editor);
            WheelDownOneNotch(InputUpdateType.Editor);

            CollectionAssert.Contains(_rawWheelUpdates, InputUpdateType.Editor, "前提: 生の onEvent が Editor の更新で受けた");
            Assert.IsEmpty(_port.Selections);
        }

        [Test]
        public void ゲームの更新では2ノッチで1段進む()
        {
            WheelDownOneNotch(InputUpdateType.Dynamic);
            Assert.IsEmpty(_port.Selections, "1 ノッチ目（60px）では進まない");
            WheelDownOneNotch(InputUpdateType.Dynamic);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        private void WheelDownOneNotch(InputUpdateType updateType)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { scroll = new Vector2(0f, -1f) });
            InputTestDriver.RunUpdate(updateType);
        }

        private void RecordRawWheelUpdate(InputEventPtr eventPtr, InputDevice device)
        {
            if (device == _mouse) _rawWheelUpdates.Add(InputState.currentUpdateType);
        }
    }
}
```

`Support/UiRoutingStateTestFactory.cs`（本物の `UiRoutingState` を指定の画面で組む。`UIStateControl` の組み方は前例 `Client.Tests/UIState/UIStateControlTest.cs` の `CreateDictionary` の反射と `FakePlayerObjectController`。`using` と `UIStateDictionary` の ctor の引数の数は実物に合わせる）:
```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Tests.UIState.Fakes;
using Client.Ui.State.Routing;
using UnityEngine;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    /// <summary>
    /// 指定の画面に居る本物の UiRoutingState を組む（ホイールのルーターと Presenter の L1 用）
    /// Builds a real UiRoutingState on the given screen (for the wheel router and presenter L1)
    /// </summary>
    public static class UiRoutingStateTestFactory
    {
        public static UiRoutingState Create(GameObject host, UIStateEnum state)
        {
            var control = host.AddComponent<UIStateControl>();
            control.Construct(DictionaryWith(state), new FakePlayerObjectController());
            control.Initialize(state, new UITransitContext(state));
            var routing = new UiRoutingState(control, Array.Empty<INestedPauseScreenState>());
            routing.Initialize();
            return routing;

            #region Internal

            UIStateDictionary DictionaryWith(UIStateEnum only)
            {
                // UIStateControlTest.CreateDictionary と同じ反射で、指定の画面だけを入れる
                // Same reflection as UIStateControlTest.CreateDictionary, holding only the given screen
                var result = new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, null, null);
                var field = typeof(UIStateDictionary).GetField("_stateDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
                ((Dictionary<UIStateEnum, IUIState>)field.GetValue(result))[only] = new IdleUiState();
                return result;
            }

            #endregion
        }

        // 遷移を出さない画面
        // A screen that never requests a transition
        private sealed class IdleUiState : IUIState
        {
            public void OnEnter(UITransitContext context) { }
            public UITransitContext GetNextUpdate() { return null; }
            public void OnExit() { }
            public bool LocksPlayerMovement() { return false; }
            public bool AllowsSprintToggle() { return true; }
            public IReadOnlyList<KeyHint> GetKeyHints() { return Array.Empty<KeyHint>(); }
        }
    }
}
```

`Support/EquipmentWheelRouterFixture.cs`（ルーターを本物の State〈画面・モーダル・配置〉で組む。配置の Fake は `PlacementHeightWiringFakes` の internal の 2 つ〈同じテストのアセンブリ〉）:
```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.UI.UIState;
using Client.Tests.PlaceSystem.Common.Height;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Modal;
using UnityEngine;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    /// <summary>
    /// 装備のホイールのルーターを本物の画面・モーダル・配置の State で組む
    /// Builds the equipment wheel router on real screen, modal and placement State
    /// </summary>
    public sealed class EquipmentWheelRouterFixture
    {
        public ModalRequestService Modal { get; private set; }
        public PlaceSystemStateController Placement { get; private set; }
        public EquipmentWheelRouter Router { get; private set; }

        public EquipmentWheelRouterFixture(GameObject host, UIStateEnum state, FakeEquipmentSelectionPort port)
        {
            var selector = new EquipmentWheelSelector(port);
            selector.Initialize();
            Modal = new ModalRequestService();
            Placement = new PlaceSystemStateController(new PlacementHeightWiringFakes.SwitchableSelector(new EmptyPlaceSystem()), new PlacementHeightWiringFakes.NullPresenter(), new PlacementHeightOffset());
            Router = new EquipmentWheelRouter(selector, UiRoutingStateTestFactory.Create(host, state), Modal, Placement);
        }
    }
}
```
（`ModalRequestService` を `RequestInputModal` の前に `Initialize` する要否と `EmptyPlaceSystem` の名前空間は実物で確かめる）

`EquipmentWheelFlowTest.cs`（拾いはテストのパネルの `panel.Pick` で、本番の `EquipmentWheelInput` と同じ判定 `EquipmentWheelPickJudge` を通す。量は Web と同じ px で渡す）:
```csharp
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.CommonHud;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Inventory.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class EquipmentWheelFlowTest : ScreenTestHost
    {
        private static readonly Vector2 OverWorld = new(5, 5);
        private GameObject _host;
        private VisualElement _viewport;
        private FakeEquipmentSelectionPort _port;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("equipment-wheel-flow");
            _viewport = UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Chrome, UiFamily.Viewport);
            _port = new FakeEquipmentSelectionPort(3);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">小さいホイール入力を累積し閾値を越えた時だけ切り替える</web-test>
        [Test]
        public void 小さいホイールは累積して閾値を越えたときだけ切り替える()
        {
            var wheel = Wheel(UIStateEnum.GameScreen);
            WheelAt(wheel, OverWorld, 40);
            Assert.IsEmpty(_port.Selections);
            WheelAt(wheel, OverWorld, 70);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        [Test]
        public void 下へ1ノッチ目では進まず2ノッチ目で1段進む()
        {
            // Web の本番（CEF の 60px/ノッチ・100px で 1 段・段が出たら端数を捨てる）と同じ刻み（D-10）
            // Same cadence as the production Web (CEF 60px per notch, 100px per step, remainder dropped on a step; D-10)
            var wheel = Wheel(UIStateEnum.GameScreen);
            var oneNotchDown = EquipmentWheelNotchScale.ToDownwardPixels(-1f);
            WheelAt(wheel, OverWorld, oneNotchDown);
            Assert.IsEmpty(_port.Selections);
            WheelAt(wheel, OverWorld, oneNotchDown);
            WheelAt(wheel, OverWorld, oneNotchDown);
            WheelAt(wheel, OverWorld, oneNotchDown);
            CollectionAssert.AreEqual(new[] { 1, 2 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">ホイールは末尾スロットの次に先頭スロットへ回る</web-test>
        [Test]
        public void 末尾の次は先頭へ回る()
        {
            var wheel = Wheel(UIStateEnum.GameScreen);
            for (var step = 1; step < 3; step++)
            {
                WheelAt(wheel, OverWorld, 100);
                _port.ConfirmFromServer(step);
            }
            WheelAt(wheel, OverWorld, 100);
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">画面中央のクロスヘアの上でもホイールで装備が切り替わる</web-test>
        [Test]
        public void クロスヘアの上でも切り替わる()
        {
            var crosshair = new CrosshairHudView();
            crosshair.SetShown(true);
            _viewport.Add(crosshair);
            simulate.FrameUpdate();

            WheelAt(Wheel(UIStateEnum.GameScreen), crosshair.Q("crosshair").worldBound.center, 100);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">持ち物画面の一覧スクロールでは装備が持ち替わらない</web-test>
        [Test]
        public void 持ち物画面の一覧の上のホイールでは持ち替えず外では持ち替える()
        {
            // 一覧（S4-recipe の ItemListPanel）の代わりに、素通しの印の無いスクロール器を立てる（D-6）
            // Stand in for the list (S4-recipe's ItemListPanel) with a scroll view that has no passthrough mark (D-6)
            var list = new GameScrollView("item-list-grid");
            list.style.position = Position.Absolute;
            list.style.left = 100;
            list.style.top = 100;
            list.style.width = 200;
            list.style.height = 200;
            _viewport.Add(list);
            simulate.FrameUpdate();

            var wheel = Wheel(UIStateEnum.PlayerInventory);
            for (var step = 0; step < 3; step++) WheelAt(wheel, list.worldBound.center, 100);
            WheelAt(wheel, new Vector2(1, 1), 100);

            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/regression/inputLayer.spec.ts">GameScreenのホイールは最新equipment値から次スロットを選ぶ</web-test>
        [Test]
        public void ゲーム画面のホイールは最新の装備の値から次を選ぶ()
        {
            // サーバーが選択を 2 にした後のホイールは 2 の次（0）
            // After the server sets the selection to 2, the wheel picks the one after 2 (0)
            var wheel = Wheel(UIStateEnum.GameScreen);
            _port.ConfirmFromServer(2);
            WheelAt(wheel, OverWorld, 100);
            CollectionAssert.AreEqual(new[] { 0 }, _port.Selections);
        }

        [Test]
        public void モーダルとツールの占有と前面の画面はルーターが本物のStateから読んで止める()
        {
            // ルーターが状況を組む経路（画面・モーダル・占有の読み取り）を本物の State で通す（Gate の規則は L0 が見る）
            // Drive the router's own assembly (screen, modal, ownership reads) through real State (the gate rules are L0's)
            var modal = Wheel(UIStateEnum.GameScreen);
            modal.Modal.RequestInputModal("title", "message", "ok");
            WheelAt(modal, OverWorld, 100);
            var tool = Wheel(UIStateEnum.GameScreen);
            tool.Placement.SetWheelOwnedByTool(true);
            WheelAt(tool, OverWorld, 100);
            WheelAt(Wheel(UIStateEnum.SubInventory), OverWorld, 100);

            Assert.IsEmpty(_port.Selections);
        }

        private EquipmentWheelRouterFixture Wheel(UIStateEnum state)
        {
            return new EquipmentWheelRouterFixture(_host, state, _port);
        }

        private void WheelAt(EquipmentWheelRouterFixture wheel, Vector2 panelPosition, double pixelsDown)
        {
            var picked = rootVisualElement.panel.Pick(panelPosition);
            wheel.Router.Route(pixelsDown, WheelDeltaUnit.Pixel, EquipmentWheelPickJudge.IsBlockingUi(picked));
        }
    }
}
```
（層の器と舞台の器は `PickingMode.Ignore`〈`UiLayerStack.cs:27,45`・`StageColumnsLayout.cs:27,51`〉なので、何も置いていない所の `Pick` は null で「世界の上」になる。本番の `GameUiPointerProbe` も同じ前提で `Pick != null` だけを見る。同じ `_port` を 3 つのルーターが共有する最後の件は、どれも選択しないことだけを見る）

`InventoryEquipmentFlowTest.cs` に足す（`using Client.Game.InGame.UI.UIState;`・`using Client.Ui.State.Inventory.Logic;` も足す）:
```csharp
        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">装備へ移したアイテムをホイールで選択できる</web-test>
        [Test]
        public void 装備へ移したアイテムをホイールで選べる()
        {
            MoveMainOneToEquipmentOne();
            var host = new GameObject("equipment-wheel");
            new EquipmentWheelRouterFixture(host, UIStateEnum.GameScreen, _fixture.Equipment).Router.Route(100, WheelDeltaUnit.Pixel, false);
            simulate.FrameUpdate();
            UnityEngine.Object.DestroyImmediate(host);

            // 選択の Fake の合図が持ち物の合図へ繋がっているので、HUD の選択枠が移る
            // The selection fake joins the inventory signal, so the HUD's selected frame moves
            Assert.IsTrue(Find<VisualElement>(_equipment, "equipment-slot-1").ClassListContains("slot-frame--selected"));
        }
```

- [ ] **Step 3: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `InputSystemPointerWheelStream`・`EquipmentWheelPickJudge`・`IEquipmentWheelPointerPicker`・`EquipmentWheelNotchScale`・`EquipmentWheelInput` が無いコンパイルエラー

- [ ] **Step 4: 実装する**

`S/Client.Input/Pointer/InputSystemPointerWheelStream.cs`（前例 `InputSystemPointerPositionStream` と同じ形）:
```csharp
using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Input.Pointer
{
    /// <summary>
    /// マウスのホイールが回った時だけ縦のノッチ数（上が正）を流す
    /// Publishes the vertical notch count (up is positive) only when the mouse wheel turns
    /// </summary>
    public sealed class InputSystemPointerWheelStream
    {
        private readonly Subject<float> _onWheelNotchesScrolled = new();

        public IObservable<float> OnWheelNotchesScrolled => _onWheelNotchesScrolled;

        public InputSystemPointerWheelStream()
        {
            // マウス無しなら理由を警告
            // Warn why when there is no mouse
            if (Mouse.current == null) Debug.LogWarning("[InputSystemPointerWheelStream] Mouse.current is null; no wheel notch will be published until a mouse sends events");
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            // ホイールだけ読む。入力設定が Uniform なので 1 ノッチ＝1（Windows の 120 もここで 1 に直る）
            // Read the wheel only; the input settings are Uniform, so one notch is 1 (Windows' 120 is normalized too)
            if (device != Mouse.current) return;
            // Editor の更新で届く入力は Game View の外（Inspector 等）の操作なので流さない
            // Input processed in an Editor update belongs to windows outside the Game View, so it is not published
            // silent-ok: Editor の窓の上の入力はゲームの操作でない / InputSystemPointerWheelStreamTest と EquipmentWheelInputFlowTest が見る
            if (InputState.currentUpdateType == InputUpdateType.Editor) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (!Mouse.current.scroll.ReadValueFromEvent(eventPtr, out var scroll)) return;
            if (scroll.y == 0f) return;
            _onWheelNotchesScrolled.OnNext(scroll.y);
        }
    }
}
```

`Wheel/EquipmentWheelPickJudge.cs`:
```csharp
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// ポインタの下の要素がホイールを奪う UI か（Web isPointerOverWebUi。素通しの印は Task 9 で足す）
    /// Whether the element under the pointer is UI that takes the wheel (Web isPointerOverWebUi; the passthrough mark comes in Task 9)
    /// </summary>
    public static class EquipmentWheelPickJudge
    {
        public static bool IsBlockingUi(VisualElement picked)
        {
            // 何も拾わなければ世界の上（拾わない HUD・クロスヘアを含む）
            // Nothing picked means over the world (including non-picking HUDs and the crosshair)
            return picked != null;
        }
    }
}
```

`Wheel/IEquipmentWheelPointerPicker.cs`（パネル座標の変換を口の外へ出し、L1 の流れのテストが Fake で通れるようにする。前例 `IUiScreenPointer`）:
```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Wheel
{
    public interface IEquipmentWheelPointerPicker
    {
        // 画面座標（左下原点）の下にある要素。何も無ければ null
        // The element under a screen position (bottom-left origin); null when nothing is there
        VisualElement PickAt(Vector2 screenPosition);
    }
}
```

`Wheel/EquipmentWheelPointerPicker.cs`（座標の換算は `UiScreenToPanel.FromBottomLeft`〈A-133 の唯一の式〉を呼び、写さない。要素を返す口が無いので `panel.Pick` だけを持つ。integrator の `PickAt` が入ったら置き換える〈Task 1 Step 3 の bd〉。D-11）:
```csharp
using Client.Ui.Foundation.Input.Pointer;
using Client.Ui.Foundation.Root;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Wheel
{
    public sealed class EquipmentWheelPointerPicker : IEquipmentWheelPointerPicker
    {
        private readonly GameUiRoot _root;

        public EquipmentWheelPointerPicker(GameUiRoot root)
        {
            _root = root;
        }

        public VisualElement PickAt(Vector2 screenPosition)
        {
            // 画面座標（左下原点）→ パネル座標。換算は A-133 の唯一の式を使う
            // Screen (bottom-left origin) to panel coordinates via the single A-133 formula
            var panel = _root.GetRootElement().panel;
            return panel.Pick(UiScreenToPanel.FromBottomLeft(panel, screenPosition));
        }
    }
}
```

`Wheel/EquipmentWheelNotchScale.cs`（ノッチ → px の換算はこの 1 か所。60 は Step 1 の共有の定数を読み、ここに書かない。D-10）:
```csharp
using Client.Ui.Foundation.Input;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// InputSystem のノッチ（上が正）を Web の deltaY の px（下が正。A-71 の 1 ノッチ＝60px）へ直す
    /// Converts InputSystem notches (up positive) to Web deltaY pixels (down positive; A-71's 60px per notch)
    /// </summary>
    public static class EquipmentWheelNotchScale
    {
        public static double ToDownwardPixels(float notchesUp)
        {
            return -notchesUp * WebWheelScale.PixelsPerNotch;
        }
    }
}
```

`Wheel/EquipmentWheelInput.cs`:
```csharp
using Client.Input;
using Client.Input.Pointer;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Logic;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// ホイールの流れを購読し、Web と同じ px と「UI の上か」を添えて装備のルーターへ渡す（Web useGameLayerWheel の C# 側）
    /// Subscribes to the wheel stream and hands Web-equivalent pixels plus "over UI" to the equipment router (C# side of Web useGameLayerWheel)
    /// </summary>
    public sealed class EquipmentWheelInput : IInitializable
    {
        private readonly InputSystemPointerWheelStream _wheel;
        private readonly InputSystemPointerPositionStream _positions;
        private readonly IEquipmentWheelPointerPicker _picker;
        private readonly EquipmentWheelRouter _router;
        private Vector2 _pointerOnScreen;

        public EquipmentWheelInput(InputSystemPointerWheelStream wheel, InputSystemPointerPositionStream positions, IEquipmentWheelPointerPicker picker, EquipmentWheelRouter router)
        {
            _wheel = wheel;
            _positions = positions;
            _picker = picker;
            _router = router;
        }

        public void Initialize()
        {
            // 位置は変わった時だけ届くので、起動時の値を一度読む（前例 InputSystemUiScreenPointer.CurrentPointerOnScreen）
            // Positions arrive only on change, so read the startup value once (precedent InputSystemUiScreenPointer.CurrentPointerOnScreen)
            _pointerOnScreen = (Vector2)HybridInput.GetMousePosition();
            _positions.OnPointerScreenPositionChanged.Subscribe(position => _pointerOnScreen = position);
            _wheel.OnWheelNotchesScrolled.Subscribe(OnWheel);
        }

        private void OnWheel(float notchesUp)
        {
            // ロック中はカーソルが画面中央に固定され HUD の上に来ない。ロックの正本は InputManager（A-134）
            // While locked the cursor stays centred and never over a HUD; InputManager owns the lock state (A-134)
            var overBlockingUi = !InputManager.IsCursorLocked && EquipmentWheelPickJudge.IsBlockingUi(_picker.PickAt(_pointerOnScreen));
            _router.Route(EquipmentWheelNotchScale.ToDownwardPixels(notchesUp), WheelDeltaUnit.Pixel, overBlockingUi);
        }
    }
}
```

- [ ] **Step 5: 緑を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Inventory|Client.Tests.Ui.State.Inventory|Client.Tests.Input.Pointer"`
Expected: `ErrorCount: 0`、全件 PASS（`ScrollDeltaBehaviorSettingTest`・`EquipmentWheelInputFlowTest` の 2 件・`InputSystemPointerWheelStreamTest`「Editorの更新で届いたホイールは流さない」を含む）

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Input/Pointer moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/WebWheelScale.cs* moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs moorestech_client/Assets/Scripts/Client.Ui/Screens/Inventory/Wheel moorestech_client/Assets/Scripts/Client.Tests/Input/Pointer moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Inventory
git commit -m "feat(uitk): ホイールの流れを購読して装備のルーターへ繋ぎ、A-71 の 60px を共有の定数へ上げる（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 8: Presenter・登録・出し分け・処分表

**Files:**
- Create: `S/Client.Ui/Screens/Inventory/Panel/InventoryScreenPresenter.cs`・`S/Client.Ui/Screens/Inventory/Equipment/EquipmentHudPresenter.cs`・`S/Client.Starter/Registration/GameUi/Screens/InventoryGameUiRegistration.cs`（`Screens/` が無ければ作る。E-1）
- Modify: `S/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 1 行と `using` 1 行）、`docs/uitk/disposition.md`（末尾に処分の 2 行）
- Test: `S/Client.Tests/Ui/Screens/Inventory/InventoryScreenVisibilityTest.cs`・`Support/GameUiRootTestFactory.cs`

**Interfaces:**
- Consumes: Task 2〜7 の型、`StageColumnsLayout`・`GameUiRoot`・`UiRoutingState`・`UiScreenTraitsTable`、S4-alpha の `IPlayerSlotCommands`（`MainGameUiRegistration` の共有サービスの並びに alpha が登録）、Task 7 の `UiRoutingStateTestFactory`
- Produces:
  ```csharp
  namespace Client.Ui.Screens.Inventory.Panel
  public sealed class InventoryScreenPresenter : IInitializable, IDisposable
  namespace Client.Ui.Screens.Inventory.Equipment
  public sealed class EquipmentHudPresenter : IInitializable, IDisposable
  namespace Client.Starter.Registration.GameUi.Screens
  internal static class InventoryGameUiRegistration { public static void Register(ContainerBuilder builder); }
  ```

- [ ] **Step 1: 出し分けのテストを書く（Presenter の L1）**

`Support/GameUiRootTestFactory.cs`（`GameUiRoot` は `UIDocument` を持つ MonoBehaviour で、EditMode の `AddComponent` では `Awake` が走らない。層だけをテストのパネルに組んで私有の `_layers` へ入れる。反射の前例 `UIStateControlTest`。Presenter の L1 は repo に前例が無く、このレーンが初めて組む）:
```csharp
using System.Reflection;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    /// <summary>
    /// テストのパネルに層を組んだ GameUiRoot を作る（Presenter の L1 用）
    /// Creates a GameUiRoot whose layers live on the test panel (for presenter L1)
    /// </summary>
    public static class GameUiRootTestFactory
    {
        public static GameUiRoot Create(GameObject host, VisualElement panelRoot)
        {
            var root = host.AddComponent<GameUiRoot>();
            typeof(GameUiRoot).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, UiLayerStack.Build(panelRoot));
            return root;
        }
    }
}
```

`InventoryScreenVisibilityTest.cs`（Web `App.tsx:95,117-118` の `screenAllowsGrab(screen) && <InventoryPanel/>`・`screenShowsAlwaysOnHud(screen) && <EquipmentPanel/>` と `useGrabInteractive` の代表行を、本物の `UiRoutingState` と Presenter で確かめる）:
```csharp
using System;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Ui.Content;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryScreenVisibilityTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private GameObject _host;
        private Texture2D _texture;
        private RecordingPlayerInventoryOperations _operations;
        private RecordingPlayerSlotCommands _commands;
        private PlayerInventoryPanelModel _model;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _host = new GameObject("inventory-visibility");
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _operations = new RecordingPlayerInventoryOperations(36, 3);
            _commands = new RecordingPlayerSlotCommands();
            _model = new PlayerInventoryPanelModel(_operations, new FakeEquipmentSelectionPort(3));
            _model.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        [TestCase(UIStateEnum.PlayerInventory, true)]
        [TestCase(UIStateEnum.ResearchTree, true)]
        [TestCase(UIStateEnum.BuildMenu, false)]
        public void 持ち物パネルは掴める画面だけに出て出ている間だけ描く(UIStateEnum state, bool shown)
        {
            var columns = new StageColumnsLayout();
            rootVisualElement.Add(columns);
            new InventoryScreenPresenter(columns, UiRoutingStateTestFactory.Create(_host, state), _model, _commands, new RecordingInventorySortCommands(), Content(), Toaster).Initialize();
            _operations.SetMain(0, new SlotItem(MasterHolder.ItemMaster.GetItemId(WoodGuid), 7));
            simulate.FrameUpdate();

            // 出ている画面では最新の値で描き、隠れた画面では購読しないので枠も作らない（§4.2）
            // Shown screens draw the latest value; hidden screens never subscribe, so no slots are built (§4.2)
            var view = columns.Q<InventoryView>();
            Assert.AreEqual(shown, IsShown(view), $"{state} の出し入れ");
            Assert.AreEqual(shown ? 36 : 0, view.Query(className: "slot-frame").ToList().Count, $"{state} の描き");
        }

        [TestCase(UIStateEnum.GameScreen, true)]
        [TestCase(UIStateEnum.ResearchTree, false)]
        [TestCase(UIStateEnum.BuildMenu, true)]
        public void 装備HUDは常駐HUDの画面だけに出る(UIStateEnum state, bool shown)
        {
            var hud = MountEquipmentHud(state);
            Assert.AreEqual(shown, IsShown(hud), $"{state}");
        }

        [TestCase(UIStateEnum.GameScreen, 0)]
        [TestCase(UIStateEnum.PlayerInventory, 1)]
        public void 装備の枠は掴める画面だけクリックを受ける(UIStateEnum state, int expectedCalls)
        {
            var frame = Find<VisualElement>(MountEquipmentHud(state), "equipment-slot-0");
            simulate.MouseDown(frame.worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(frame.worldBound.center, MouseButton.LeftMouse);
            Assert.AreEqual(expectedCalls, _commands.Calls.Count, string.Join(",", _commands.Calls));
        }

        private EquipmentHudView MountEquipmentHud(UIStateEnum state)
        {
            var root = GameUiRootTestFactory.Create(_host, rootVisualElement);
            new EquipmentHudPresenter(root, UiRoutingStateTestFactory.Create(_host, state), _model, _commands, Content(), Toaster).Initialize();
            simulate.FrameUpdate();
            return rootVisualElement.Q<EquipmentHudView>();
        }

        private GameContentPresentation Content()
        {
            return new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));
        }
    }
}
```
（`UIStateEnum` の各値〈`GameScreen`・`PlayerInventory`・`ResearchTree`・`BuildMenu`〉は `UiScreenRouting.Resolve` の写像どおり `None`・`PlayerInventory`・`ResearchTree`・`BuildMenu` になる。`StageColumnsLayout` を単体で組むと列の器ができるかは `StageColumnsLayoutTest` の組み方で確かめ、要るならその組み方に合わせる）

- [ ] **Step 2: Presenter と登録を書く**

`Panel/InventoryScreenPresenter.cs`（前例 `PauseMenuScreenPresenter`・`GrabOverlayPresenter`。載せる・出す・表示中だけ購読）:
```csharp
using System;
using Client.Ui.Content;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Routing;
using Client.Ui.State.SlotCommands.Player;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Inventory.Panel
{
    /// <summary>
    /// 持ち物パネルを舞台の Inv 列へ 1 回載せ、掴める画面の間だけ Model を購読して描く（A-92）
    /// Mounts the inventory panel once in the stage Inv column and subscribes to the model only while a grab screen shows (A-92)
    /// </summary>
    public sealed class InventoryScreenPresenter : IInitializable, IDisposable
    {
        private readonly StageColumnsLayout _columns;
        private readonly UiRoutingState _routing;
        private readonly PlayerInventoryPanelModel _model;
        private readonly InventoryView _view;
        private IDisposable _routingSubscription;
        private IDisposable _modelSubscription;

        public InventoryScreenPresenter(StageColumnsLayout columns, UiRoutingState routing, PlayerInventoryPanelModel model, IPlayerSlotCommands commands, IInventorySortCommands sort, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            _columns = columns;
            _routing = routing;
            _model = model;
            _view = new InventoryView(new PlayerSlotGestureRouter(commands, toaster), sort, content, toaster);
        }

        public void Initialize()
        {
            _columns.Get(StageColumn.Inv).Add(_view);
            _routingSubscription = _routing.Current.Subscribe(snapshot => Apply(IsShownOn(snapshot.Screen)));

            #region Internal

            // 掴める画面だけに出す（Web screenAllowsGrab(screen) && <InventoryPanel/>。traits の列を読むのはこの 1 か所。前例 PauseMenuScreenPresenter）
            // Shown only on grab screens (Web screenAllowsGrab(screen) && <InventoryPanel/>; the one reader of this traits column, precedent PauseMenuScreenPresenter)
            bool IsShownOn(UiScreen screen)
            {
                return UiScreenTraitsTable.Get(screen).Grab;
            }

            #endregion
        }

        public void Dispose()
        {
            _routingSubscription?.Dispose();
            _modelSubscription?.Dispose();
            _view.RemoveFromHierarchy();
        }

        private void Apply(bool shown)
        {
            // 出す間だけ購読し、隠すと購読を外す（§4.2）
            // Subscribe only while shown and drop the subscription when hidden (§4.2)
            _view.SetShown(shown);
            if (shown && _modelSubscription == null) _modelSubscription = _model.Current.Subscribe(_view.Render);
            if (shown || _modelSubscription == null) return;
            _modelSubscription.Dispose();
            _modelSubscription = null;
        }
    }
}
```

`Equipment/EquipmentHudPresenter.cs`:
```csharp
using System;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Routing;
using Client.Ui.State.SlotCommands.Player;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Inventory.Equipment
{
    /// <summary>
    /// 装備 HUD を Viewport の Chrome 層へ 1 回載せ、常駐 HUD の画面で出し、掴める画面でだけクリックを受ける（A-57）
    /// Mounts the equipment HUD once on the Viewport chrome layer, shows it on always-on-HUD screens and accepts clicks only on grab screens (A-57)
    /// </summary>
    public sealed class EquipmentHudPresenter : IInitializable, IDisposable
    {
        private readonly GameUiRoot _root;
        private readonly UiRoutingState _routing;
        private readonly PlayerInventoryPanelModel _model;
        private readonly EquipmentHudView _view;
        private IDisposable _routingSubscription;
        private IDisposable _modelSubscription;

        public EquipmentHudPresenter(GameUiRoot root, UiRoutingState routing, PlayerInventoryPanelModel model, IPlayerSlotCommands commands, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            _root = root;
            _routing = routing;
            _model = model;
            _view = new EquipmentHudView(new PlayerSlotGestureRouter(commands, toaster), content);
        }

        public void Initialize()
        {
            _root.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_view);
            _routingSubscription = _routing.Current.Subscribe(snapshot => Apply(UiScreenTraitsTable.Get(snapshot.Screen)));
        }

        public void Dispose()
        {
            _routingSubscription?.Dispose();
            _modelSubscription?.Dispose();
            _view.RemoveFromHierarchy();
        }

        private void Apply(UiScreenTraits traits)
        {
            // 常駐 HUD の画面で出し（blocking スキットでも隠さない）、掴める画面でだけクリックを受ける（Web screenShowsAlwaysOnHud・useGrabInteractive）
            // Show on always-on-HUD screens (even during a blocking skit) and take clicks only on grab screens (Web screenShowsAlwaysOnHud, useGrabInteractive)
            var shown = traits.AlwaysOnHud;
            _view.SetClicksAccepted(traits.Grab);
            _view.SetShown(shown);
            if (shown && _modelSubscription == null) _modelSubscription = _model.Current.Subscribe(_view.Render);
            if (shown || _modelSubscription == null) return;
            _modelSubscription.Dispose();
            _modelSubscription = null;
        }
    }
}
```

`Client.Starter/Registration/GameUi/Screens/InventoryGameUiRegistration.cs`（E-1〈反映済み ad92a44777〉の W4 以降の置き場。`GameUi/` 直下は S4-alpha で 10 本の上限。D-27）:
```csharp
using Client.Input.Pointer;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Panel;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // 持ち物パネル・装備 HUD・ホイールでの装備の循環（GameUi）の登録。プレイヤーの枠のコマンドは共有サービスの並び（MainGameUiRegistration）にある
    // Registers the inventory panel, equipment HUD and wheel equipment cycling (GameUi); player-slot commands live in the shared-service list (MainGameUiRegistration)
    internal static class InventoryGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<LocalPlayerEquipmentSelectionPort>(Lifetime.Singleton).As<IEquipmentSelectionPort>();
            builder.RegisterEntryPoint<PlayerInventoryPanelModel>().AsSelf();
            builder.Register<InventorySortCommands>(Lifetime.Singleton).As<IInventorySortCommands>();
            builder.RegisterEntryPoint<EquipmentWheelSelector>().AsSelf();
            builder.Register<EquipmentWheelRouter>(Lifetime.Singleton);
            builder.Register<InputSystemPointerWheelStream>(Lifetime.Singleton);
            builder.Register<EquipmentWheelPointerPicker>(Lifetime.Singleton).As<IEquipmentWheelPointerPicker>();
            builder.RegisterEntryPoint<EquipmentWheelInput>();
            builder.RegisterEntryPoint<InventoryScreenPresenter>();
            builder.RegisterEntryPoint<EquipmentHudPresenter>();
        }
    }
}
```
（`IPlayerInventoryOperations`・`InputSystemPointerPositionStream`・`ModalRequestService`・`PlaceSystemStateController` は既存の登録を使う。`PlayerInventoryPanelModel` は ctor で初期値を持つので、Presenter との初期化の順に依らない〈D-24〉。`InputSystemPointerWheelStream` は前例 `GrabGameUiRegistration` の `InputSystemPointerButtonStream` と同じくレーンの登録に置く）

`MainGameUiRegistration.cs` の画面登録区画の末尾（他レーンの行の後）と、`using Client.Starter.Registration.GameUi.Screens;`（無ければ）:
```csharp
            InventoryGameUiRegistration.Register(builder);
```
（`GameUi/Screens/` に他レーンの同名の登録が先に入っていれば名前の衝突を確かめる。`InventoryGameUiRegistration` は機能名なので重ならない）

- [ ] **Step 3: 処分表に 2 行を書く**

`docs/uitk/disposition.md` の「## 処遇」の表の末尾へ（写したテストの行は書かない。`<web-test>` のタグが対応の正本で、表に書くと `compareBundles.mjs` が誤りにする）:
```markdown
| 4 | `src/features/inventory/EquipmentPanel/index.test.ts` | action失敗時はその要求をpendingから除く | 捨てる | C# の装備の選択（`LocalPlayerEquipment.SetSelectedIndex` → `SendOnly.SetSelectedEquipment`）は送りっぱなしで失敗が返らず、外す要求が生まれない。未確認の列はサーバーのエコーの世代だけで縮む（`EquipmentWheelSelectorTest`。S4-inventory D-5） | — |
| 4 | `src/features/inventory/EquipmentPanel/index.test.ts` | 再接続開始時にpendingを破棄して復元snapshotを起点にする | 捨てる | UITK は同じプロセスの C# を直接読み、WebSocket の切断・再接続が無い。世代が戻ったら未確認を捨てる規則は `EquipmentWheelSelector.TrimConfirmed` に写し、確定値を起点にすることは `EquipmentWheelSelectorTest`「未確認が全部確定した後はサーバーの確定値から次を計算する」が見る（S4-inventory D-5） | — |
```

- [ ] **Step 4: 対応を確かめる**

Run:
```bash
node tools/uitk/test-name-parity.mjs --bundle 4
node tools/uitk/test-name-parity.mjs --bundle 5
```
Expected: 束 4 の出力に `inventory.spec.ts`（このレーンの 4 件）・`equipment.spec.ts`（Task 9 の 3 件を除く 8 件）・`InventoryPanel/index.test.ts`・`EquipmentPanel/index.test.ts`・`regression/itemSlot.spec.ts` の未対応が無い（Task 9 の 3 件だけが残る）。束 5 に `inputLayer.spec.ts`「GameScreenのホイールは最新equipment値から次スロットを選ぶ」の未対応が無い。束 5 に出なければ `--bundle` を変えて探し、出た束を「写すテスト」表の注に合わせて `done.md`「証拠」に書く

- [ ] **Step 5: 起動を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Inventory|Client.Tests.Ui.State.Inventory|Client.Tests.Ui.Conventions|Client.Tests.Ui.Theme|Client.Tests.Ui.Screens.GrabOverlay|Client.Tests.Ui.Anchor|Client.Tests.Input.Pointer"` → 全体スイッチ UITK で PlayMode を起動し `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: `ErrorCount: 0`、全件 PASS、Error ログ 0 件（VContainer の解決失敗が無い）。`I` で持ち物を開くと Inv 列にパネルが出る（スクショを `done.md`「証拠」へ）

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Inventory moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Inventory docs/uitk/disposition.md
git commit -m "feat(uitk): 持ち物パネルと装備 HUD を登録し、処分した 2 件を記帳する（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 9: ホイールの素通しと世界の上の色（S3-hotbar-modal の後）

**Files:**
- Modify: `S/Client.Ui/Screens/Inventory/Equipment/EquipmentHudView.cs`（印）・`S/Client.Ui/Screens/Inventory/Wheel/EquipmentWheelPickJudge.cs`（素通し）・`S/Client.Ui/Screens/Inventory/Inventory.uss`（世界の上の枠の面）・`docs/uitk/disposition.md`（albof.31 の行を消す）
- Test: `S/Client.Tests/Ui/Screens/Inventory/EquipmentWheelPassthroughTest.cs`

**Interfaces:**
- Consumes（S3-hotbar-modal。**この Step 1 で実物と照合**）: `WheelPassthroughMark.Mark`・`IsPassthrough`、`HotbarHudView`・`RecordingHotbarCommands`・`HotbarTestPresentation.Create()`・`HotbarViewModelSamples.WebFixture()`、`Hotbar.uss` の画面ルートの上書き（`--slot-frame-face: var(--hotbar-slot-frame-face)` ほか）
- Produces: なし

- [ ] **Step 1: S3-hotbar-modal を取り込み、実物と照合する**

Run:
```bash
git fetch origin && git merge origin/feature/uitk
S=moorestech_client/Assets/Scripts
grep -n "public" $S/Client.Ui/Foundation/Input/WheelPassthroughMark.cs
grep -n "public HotbarHudView\|public void Render" $S/Client.Ui/Screens/Hotbar/HotbarHudView.cs
grep -rn "class RecordingHotbarCommands\|class HotbarTestPresentation\|class HotbarViewModelSamples" $S/Client.Tests | head
grep -n "hotbar-hud\b\|--slot-frame-face\|--bevel-c\|--hotbar-" $S/Client.Ui/Screens/Hotbar/*.uss | head -20
```
Expected: `WheelPassthroughMark` に `ClassName`・`Mark`・`IsPassthrough` があり、`HotbarHudView` の ctor と `Render` が計画の写しと同じ。違えば実物の名前で以下を書き替え、`done.md`「証拠」に違いを書く（形が違って計画の筋が通らないときは止めて指揮へ返す）。`Hotbar.uss` の画面ルートで上書きしているつまみの行を全部控える（Step 3 で写す）。取り込みの衝突は共有区画なら両側を残して解き、compile とテストを回して緑を確かめてからコミットする

- [ ] **Step 2: 失敗するテストを書く**

`EquipmentWheelPassthroughTest.cs`:
```csharp
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Hotbar;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.Content;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class EquipmentWheelPassthroughTest : ScreenTestHost
    {
        private GameObject _host;
        private FakeEquipmentSelectionPort _port;
        private EquipmentWheelRouterFixture _wheel;
        private VisualElement _viewport;
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("equipment-wheel-passthrough");
            _port = new FakeEquipmentSelectionPort(3);
            _wheel = new EquipmentWheelRouterFixture(_host, UIStateEnum.GameScreen, _port);
            _viewport = UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Chrome, UiFamily.Viewport);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">装備HUDは実UIのままホイールだけ素通す</web-test>
        [Test]
        public void 装備HUDは拾う実UIのままホイールだけ素通す()
        {
            var hud = MountEquipmentHud();
            var picked = rootVisualElement.panel.Pick(Find<VisualElement>(hud, "equipment-slot-0").worldBound.center);

            Assert.IsNotNull(picked, "実 UI として拾う");
            Assert.AreEqual(PickingMode.Position, hud.pickingMode);
            Assert.IsTrue(hud.ClassListContains(WheelPassthroughMark.ClassName));
            Assert.IsFalse(EquipmentWheelPickJudge.IsBlockingUi(picked), "ホイールは素通す");
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">装備HUDの上でもホイールで装備が切り替わる</web-test>
        [Test]
        public void 装備HUDの上でも切り替わる()
        {
            var hud = MountEquipmentHud();
            WheelAt(Find<VisualElement>(hud, "equipment-slot-0").worldBound.center);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">ホットバーHUDの上でもホイールで装備が切り替わる</web-test>
        [Test]
        public void ホットバーの枠の上でも切り替わる()
        {
            var hotbar = MountHotbar();
            WheelAt(Find<VisualElement>(hotbar, "hotbar-slot-1").worldBound.center);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">ホットバー帯のスロット列外でもホイールで装備が切り替わる</web-test>
        [Test]
        public void ホットバーの帯の列の外でも切り替わる()
        {
            var hotbar = MountHotbar();
            var row = Find<VisualElement>(hotbar, "hotbar-grid");
            var outside = new Vector2(hotbar.worldBound.xMin + 2f, row.worldBound.center.y);
            Assert.IsFalse(row.worldBound.Contains(outside), "列の外を選べている");

            WheelAt(outside);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        private EquipmentHudView MountEquipmentHud()
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var hud = new EquipmentHudView(new PlayerSlotGestureRouter(new RecordingPlayerSlotCommands(), Toaster), new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture)));
            _viewport.Add(hud);
            hud.SetShown(true);
            hud.Render(new PlayerInventoryViewModel(new[] { empty }, new[] { empty, empty, empty }, 0, new System.Collections.Generic.Dictionary<int, string>()));
            simulate.FrameUpdate();
            return hud;
        }

        private HotbarHudView MountHotbar()
        {
            var hotbar = new HotbarHudView(new RecordingHotbarCommands(), HotbarTestPresentation.Create(), Toaster);
            _viewport.Add(hotbar);
            hotbar.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            return hotbar;
        }

        private void WheelAt(Vector2 panelPosition)
        {
            var picked = rootVisualElement.panel.Pick(panelPosition);
            _wheel.Router.Route(100, WheelDeltaUnit.Pixel, EquipmentWheelPickJudge.IsBlockingUi(picked));
        }
    }
}
```
（`HotbarHudView` の帯がテストの中で横幅を持つか〈帯の左端と列の左端に差があるか〉は S3-hotbar-modal の `HotbarHudPickingTest` の組み方に合わせる。差が無ければ帯の上端と列の上端の間の点を使う）

Run: `uloop compile --project-path ./moorestech_client` → 上のテストだけ回す
Expected: 4 件とも FAIL（印が無く、`IsBlockingUi` が拾った要素を全部 UI とみなす）

- [ ] **Step 3: 実装する**

`EquipmentHudView` の ctor の `pickingMode = PickingMode.Position;` の次に:
```csharp
            // 実 UI だがスクロールを持たないゲーム操作の場なので、ホイールだけ装備へ素通す（Web data-wheel-passthrough）
            // Real UI without scrolling that belongs to the game, so only the wheel passes through to equipment (Web data-wheel-passthrough)
            WheelPassthroughMark.Mark(this);
```

`EquipmentWheelPickJudge.IsBlockingUi` を次に替える（要約コメントの「素通しの印は Task 9 で足す」も消す）:
```csharp
        public static bool IsBlockingUi(VisualElement picked)
        {
            // 何も拾わなければ世界の上。拾っても素通しの印の下ならゲームの操作（Web isPointerOverWebUi && !isWheelPassthrough）
            // Nothing picked means over the world; a pick under a passthrough mark is still the game's (Web isPointerOverWebUi && !isWheelPassthrough)
            return picked != null && !WheelPassthroughMark.IsPassthrough(picked);
        }
```

`Inventory.uss` の `.equipment-hud` に、Step 1 で控えた `Hotbar.uss` の画面ルートの上書きと同じ行を足す（計画の写しでは次の 1 行。実物に他の行があれば全部写す。トークンを借りるのは同じ源・同じ地だから。D-22）:
```css
    /* 世界の上の枠の面はホットバーと同じ前計算の値（S3-hotbar-modal R-6・bd albof.34。Web の装備 HUD もホットバーと同じ SlotFrame を世界の上で使う。パネルの地向けの LX-23 を世界の地で使わない） */
    /* Slot faces over the world use the hotbar's precomputed values (S3-hotbar-modal R-6, bd albof.34; the Web equipment HUD uses the hotbar's SlotFrame over the world; panel-ground LX-23 is not for the world) */
    --slot-frame-face: var(--hotbar-slot-frame-face);
```
（トークンの名前が世界の地の意味の名前へ改名されたら〈Task 1 Step 3 の依頼〉その名前で読む）

`docs/uitk/disposition.md` の albof.31 の行（`| 2 | \`src/features/commonHud/alwaysOnHudWheel.test.ts\` | 装備HUDは実UIのままホイールだけ素通す | 写す予定 | moorestech-albof.31…`）を消す（タグを足すこのコミットで消さないと parity が二重に数える）

- [ ] **Step 4: 緑と対応を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Inventory|Client.Tests.Ui.Screens.Hotbar|Client.Tests.Ui.State.Inventory|Client.Tests.Ui.Anchor|Client.Tests.Input.Pointer"` → `node tools/uitk/test-name-parity.mjs --bundle 4` → `node tools/uitk/test-name-parity.mjs --bundle 2`
Expected: `ErrorCount: 0`、全件 PASS。束 4 にこのレーンのファイルの未対応が無い、束 2 に `alwaysOnHudWheel.test.ts`「装備HUDは実UIのままホイールだけ素通す」の未対応が無い

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Inventory moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Inventory docs/uitk/disposition.md
git commit -m "feat(uitk): 装備 HUD とホットバーの上のホイールを装備へ素通す（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 10: 並べ比べと見た目の例外

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 見た目の例外」。使ったときだけ LX-111〜LX-115）
- 記録: `LOGS/harness/uitk/S4-inventory/done.md`「差の表」

- [ ] **Step 1: 撮る**

参照 `4-player-inventory`・`4-player-inventory-full`・`5-japanese-inventory`・`3-hotbar-equipment-empty`・`2-game-hud`（基準: 1280×720 は台帳の `web-a9d5123537` と `web-869b2d4264`、2560×1080 は `web-0e81bbe3c3-2560x1080`）と同じ場面を UITK で撮る。場面の組み方と撮り方は ref-uitk-design スキルの「比べ方」節に従い、同じスキルの比較ツールで並べ比べの画像を作る。ホットバーと装備の 2 枚は S3-hotbar-modal のホットバーも写る（Task 9 の後に撮る）

- [ ] **Step 2: 差の表を書く**

| 参照 | 見る点 | 予想 |
|---|---|---|
| 4-player-inventory | Inv 列の左上の位置・パネルの幅 473.89・最小高 452.39・translate 0.783・題の縮み（0.919/0.924）と右の整理ボタン・10 列のマス 35.18・間 7.08・個数の字 12.34 と字間 | 同じ |
| 4-player-inventory-full | 満杯の持ち物の個数の字・縁取り・面の内側の色 rgb(50 52 67) | 同じ |
| 5-japanese-inventory | 日本語の題「持ち物」・「整理」の字幅（字の大きさは Web と同じ px。A-37） | 同じか、字幅の差なら letter-spacing で寄せる（直した）。寄せても差が一目で分かるなら LX-111 |
| 3-hotbar-equipment-empty | 装備 HUD の右 24・床 2・縦の間・枠 51.68・空き枠の面（世界の地。Task 9 の上書き） | 同じ（面の色は Linear 合成の差が出たら S3-hotbar-modal と同じ扱いで LX） |
| 2-game-hud | ゲーム画面の装備 HUD の選択枠（白面・下端の帯） | 同じ |

差は 1 件ずつ「同じ／直した／LX-n／他レーン持ち」で閉じる。LX は `docs/uitk/TODO.md`「## 見た目の例外」に LX-111 から順に使い、USS に `/* look-exception: LX-n */` を付ける

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/TODO.md moorestech_client/Assets/Scripts/Client.Ui/Screens/Inventory
git commit -m "docs(uitk): 持ち物と装備の並べ比べの差を閉じる（S4-inventory）

<実行ホストの指示する帰属行>"
```
（差が無く何も変えていなければコミットしない）

---

### Task 11: 録画

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-inventory-equipment.cs`

- [ ] **Step 1: シナリオを書く**

前例 `scenarios/uitk/uitk-grab-overlay.cs` の形（`UiSystemEditorSwitch.Set(UiSystem.GameUi)`・`ClientDIContainer` から型を引く）に従い、次を順に主張する:
0. 頭で前例 `scenarios/building/blueprint-place-like-block.cs:35-40` と同じく `InputSystem.settings.backgroundBehavior = IgnoreFocus`・`editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView` にし、`SemanticInput.EnsureDevices()` とマウス・キーボードの `EnableDevice` を呼ぶ（Editor が背景だと注入したホイールが Editor の更新で届き、D-25 の除外で落ちて録画が失敗するため）
1. `SetupFlatGround` → main の 0 番へ木材 10 を `GiveItem`、装備の枠が 2 以上あることを `LocalPlayerEquipment.Slots.Count` で確かめる
2. 持ち物を開く（`WaitUiState(UIStateEnum.PlayerInventory)`）→ `inventory-panel` が Inv 列に出る・`main-slot-0` に 10（`Screenshot`）
3. `inventory-sort` を押す → Error ログが増えない
4. `main-slot-0` を左押下 → 掴みに 10 → `equipment-slot-1` を左押下・解放 → 装備の 1 番に 10、掴みが空
5. `equipment-slot-1` を左押下 → 掴みに移る（選択は変わらない）→ 元へ戻す
6. 持ち物を閉じてゲーム画面へ → `SemanticInput.MouseWheel(-1)`（下へ 1 ノッチ）→ `LocalPlayerEquipment.SelectedIndex` が変わらない（60px。Web の本番と同じく 1 ノッチ目では進まない）→ もう 1 ノッチで 1 進む → さらに 2 ノッチで次へ（末尾の次は先頭。D-10）
7. 持ち物を開き、パネルの上へカーソルを置いて `MouseWheel(-1)` を 2 回 → 選択が変わらない → 装備 HUD の上で `MouseWheel(-1)` を 2 回 → 1 進む（Task 9 の後）
8. S4-alpha から渡された完了条件（alpha D-33）: `PlaceBlockDirect("木のチェスト", pos, BlockDirection.North)` → `WaitBlockGameObject` → main の 2 番へ木材 5 を `GiveItem` → `PressInteract` → `WaitUiState(UIStateEnum.SubInventory)`（持ち物パネルも Inv 列に出ている）→ `main-slot-2` を Shift＋左押下 → main の 2 番が空になり、チェストの枠に木材 5 が入る（`Screenshot`）。押下は alpha の `uitk/uitk-block-inventory` の Shift 押下の書き方を使う
9. 途中で `AssertNoErrorLogs` 相当が 0 件

押下は前例 `uitk-grab-overlay.cs` の UITK 要素の押下の書き方を使う。InputSystem の注入だけを使い OS の入力は使わない

- [ ] **Step 2: 回す**

Run: unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh uitk/uitk-inventory-equipment` と `uitk/uitk-grab-overlay`
Expected: 2 本の `result.json` が PASS、mp4 がある。`done.md`「証拠」に result.json のパス・mp4・`UiInputExclusivity.ActiveUiSystem` の値（GameUi）、6 の 1 ノッチ目で進まず 2 ノッチ目で 1 段進んだこと（D-10 の刻みが本物の DI・入力の流れを通ること。値の正規化そのものは `SemanticInput.MouseWheel` が定義上 1 を注入するので録画では測れず、入力設定の既定〈Uniform〉で閉じた〈`ScrollDeltaBehaviorSettingTest`〉）を書く。生の録画は worktree の gitignore 下に置き、`LOGS` へはスクショ 3 枚までと報告だけ

- [ ] **Step 3: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-inventory-equipment.cs
git commit -m "test(uitk): 持ち物と装備の録画シナリオを足す（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 12: 全ブランチのレビュー

- [ ] **Step 1: moores-code-review を全ブランチで回す**

moores-code-review スキルを、base `origin/feature/uitk` に対するこのブランチの全差分で起動する。スキルの手順どおり、機械的な修正は自動で当て（自動適用の後は Task 11 の録画を反映後のバイナリで撮り直す）、設計判断は D-n として判断記録へ足すか、目的・裁定から外れるものだけ `done.md`「要裁定」へ書く（人へ質問しない）

- [ ] **Step 2: 当てた修正の後に compile とテスト**

Run: `uloop compile --project-path ./moorestech_client` → Task 8 Step 5 と同じテストの絞り込み（`Client.Tests.Ui.Anchor`・`Client.Tests.Input.Pointer` を含む。Task 9 の後なので `Client.Tests.Ui.Screens.Hotbar` も足す）
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 3: コミット**

```bash
git add -A moorestech_client docs .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk
git commit -m "fix(uitk): 全ブランチのレビューを反映（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 13: PR・取り込み・マージ・報告

- [ ] **Step 1: PR を作る**

pr-create スキルで base `feature/uitk` の PR を作る。本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/`（素材と公開手順は `~/hermes-agent/data/services/pr-review/publish/README.md`。公開に失敗したら報告 URL 無しで完了にしない）。本文の末尾は実行ホストの指示する帰属行

- [ ] **Step 2: `origin/feature/uitk` を取り込む**

```bash
git fetch origin
git merge origin/feature/uitk
```
共有区画（`MainGameUiRegistration` の画面登録区画・`TSS` の `Screens` 区画・`THEME/DerivedTokens.uss` の名札付きブロック・`docs/uitk/disposition.md`・`docs/uitk/TODO.md`）の衝突は両側の行を残して解く。取り込み後に `uloop compile` と Task 12 Step 2 のテストを回し、緑を確かめて push

- [ ] **Step 3: G1 を待ってマージする**

`gh pr checks <番号>` が緑になったら `gh pr merge <番号> --merge`。`UnityCursorTooltipScreenTest` が CI だけで落ちるのは既知で無視してよい（他の赤は直して push し直す）

- [ ] **Step 4: 報告と後始末**

1. `bd close <S4-inventory の issue>`
2. `~/hermes-agent/data/repos/moorestech_logs/harness/uitk/S4-inventory/done.md` の 1 行目を「状態: 完了」にし、PR 番号・マージコミット・証拠・差の表・integrator への依頼・未確認を埋める
3. `cmux wait-for -S uitk-s4-inventory`
4. `moores-wt rm s4-inventory`（Editor と worktree を畳む。未 push があれば拒否されるので push を確かめてから）

---

## Self-Review

- Requirements はそれぞれ Task に当たる: R-1・R-2（表）→ Task 3、R-2（View）・R-3 → Task 4、R-4 → Task 8（Presenter のローカル関数と L1）、R-5 → Task 4〜6、R-6 → Task 5、R-7・R-8 → Task 2・Task 7、R-9 → Task 7・Task 11、R-10・R-11 → Task 9、R-12 → Task 4、R-13 → Task 10、R-14 → Task 4・Task 8、R-15 → Task 2〜9、R-16 → Task 11、R-17 → Task 12・13
- 写すテスト 34 件の所在: Task 2（Selector 4・Gate 4）・Task 4（inventory.spec 1・InventoryPanel 2・itemSlot 1）・Task 5（EquipmentPanel 4・equipment.spec 1）・Task 6（inventory.spec 3・equipment.spec 2）・Task 7（equipment.spec 5・inputLayer 1）・Task 8（処分 2）・Task 9（equipment.spec 3・alwaysOnHudWheel 1）＝ 8＋4＋5＋5＋6＋2＋4 ＝ 34
- 型の一致: `PlayerInventoryViewModel`（Task 3）を Task 4・5・8・9 が同じ ctor（main・装備・選択・所持品の GUID の表）で使う。`PlayerInventoryPanelModel(IPlayerInventoryOperations, IEquipmentSelectionPort)` は Task 3・6・8 で同じ。`EquipmentWheelSituation` の引数順（screen, modalPending, overBlockingUi, toolOwnsWheel）は Task 2 の Gate とルーターの中だけ。`EquipmentWheelRouter.Route(delta, unit, overBlockingUi)` は Task 7・9 で同じ（L1 は `EquipmentWheelRouterFixture` で本物の State を組む）。`EquipmentWheelInput(wheel, positions, IEquipmentWheelPointerPicker, router)` は Task 7 の本番・L1 と Task 8 の登録で同じ。`PlayerInventoryViewModel.MainSlotItemGuids` と `InventorySlotAnchorMarks.Apply(slots, itemGuids)` は Task 3・4 で同じ。`PlayerSlotGestureRouter(IPlayerSlotCommands, UiCommandResultToaster)` は Task 4・5・8・9 で同じ
- プレースホルダ: 「実物で確かめる」と書いた箇所は、起草時に実物が無い型（S4-alpha のテスト用の部品・S3-hotbar-modal の型）と、実物の綴りを確かめていない小物（`ItemMaster.EmptyItemId`・石の GUID・`CssCalcEvaluator` の可視性・`UIStateDictionary` の ctor の引数の数・`ModalRequestService.Initialize` の要否）と、Editor の更新でイベントが処理される条件の与え方（Task 7 の `InputTestDriver` の注。前提の assert で空振りを捕まえる）だけ。挙動の空白は無い

---

## 判断記録（ADR）

| ID | 判断 | 理由 | 却下した案 |
|---|---|---|---|
| D-1 | Presenter は 2 本。持ち物は `InventoryScreenPresenter`（`StageColumnsLayout.Get(StageColumn.Inv)` へ。A-92）、装備 HUD は `EquipmentHudPresenter`（Chrome 層・Viewport。HUD の前例 `CrosshairHudPresenter`・`ProgressHudPresenter` の名前の付け方）。どちらも `UiRoutingState` を購読して出し分け、表示中だけ Model を購読する（§4.2・§4.4） | Web も別の場所（舞台の grid の inv と `.viewportOverlay`）に別の条件で出す。出し分けの条件が違う 2 つを 1 本の Presenter に入れると判断が Presenter に入る | 1 本の Presenter で両方: 条件が 2 系統になり §4.2「Presenter は判断を持たない」に反する |
| D-2 | 持ち物パネルと装備 HUD は同じ `PlayerInventoryPanelModel` の値を読む | Web も同じ `inventory` topic（mainSlots・equipment・selectedEquipment）を 2 つが読む | 装備だけの Model を別に作る: 源が同じで 2 重の購読と 2 重の組み立てになる |
| D-3 | Model は変化のたびに同期で配る（Web の PostLateUpdate への間引きをしない） | A-57: 同じフレームの描き直しは UITK が 1 回の描画にまとめ、Web の間引きは WebSocket の送り過ぎを抑えるためのもの | フレーム末に寄せる: 理由が UITK に無い |
| D-4 | Web の `ConnectingPlaceholder`（topic 未着の仮の面）は写さない | C# の持ち物は起動時から在り、未着の状態が無い（A-13 と同じ理由） | 空の面を写す: 出る場面が無い死んだコードになる |
| D-5 | `EquipmentPanel/index.test.ts` の「action失敗時は…」「再接続開始時に…」を処分表へ書き、残り 12 件を写す。世代の巻き戻りで未確認を捨てる規則は `TrimConfirmed` に残す | C# の選択は `SendOnly` で失敗が返らず、UITK に WebSocket の再接続が無い。世代の巻き戻りは C# では起きないが、Web の規則を外す理由も無く、1 行で済む | 失敗の経路を作って写す: 存在しない失敗のためにコマンドに戻り値と待ちを足すことになる |
| D-6 | `equipment.spec`「持ち物画面の一覧スクロールでは装備が持ち替わらない」は S4-inventory が持ち、一覧（S4-recipe の `ItemListPanel`）の代わりに素通しの印の無い `GameScrollView` を立てて L1 で写す。S4-recipe の一覧は `WheelPassthroughMark` を付けない（付けないのが既定なので S4-recipe の作業は無い）。順序は独立で、どちらが先にマージしてもよい。S4-recipe の計画へは「一覧に素通しの印を付けない」1 行の申し送りだけを出す | テストの主題は「素通しでない UI の上のホイールは装備に回さない」で、判定はこのレーンの `EquipmentWheelGate` と `EquipmentWheelPickJudge` にある。一覧の実物を待つと S4-recipe → S4-inventory の辺が増える | S4-recipe が写す: 判定の持ち主でないレーンが装備の切り替えを確かめることになり、判定を変えたときに別レーンのテストが壊れる。S4-recipe を待つ: クリティカルパスの S4-inventory が W4b を待つ |
| D-7 | `EquipmentPanel/index.test.ts` の「接続ツール」「BPコピー」「block選択」の 3 件は、同じ入力（占有＝偽）の 1 件に 3 行のタグで写す | Web のテストの主題は「topic の `wheelOwnedByTool` をそのまま読み、配置対象の種別から導き直さない」。C# の判定は bool 1 つを読むだけで、種別ごとに占有を決めるのは各配置システム（このレーンの外）。3 件を別々に書いても入力が同じになる | 配置システムを組んで種別ごとに占有を作る: このレーンの判定でなく配置システムの挙動を確かめることになる |
| D-8 | 所持品のアンカーは描き直しのたびに `InventorySlotAnchorMarks` が付け替え、古い id と印のクラスを自分で外す。`TutorialAnchor.Unmark` を integrator へ依頼する | `TutorialAnchor` に外す口が無い。E-1 が許す A-71 の定数を除き、Foundation はこのレーンで変えない（スコープ境界） | Foundation に `Unmark` を足す: E-1 が許す A-71 の定数を除き、部品・Foundation を画面レーンが変えることになる。枠を毎回作り直す: スロットの購読と見た目の状態が描き直しのたびに捨てられる |
| D-9 | ホイールの循環は State の `EquipmentWheelSelector` 1 か所に置き、Web と同じ未確認 FIFO を持つ。口 `IEquipmentSelectionPort` の本番は `LocalPlayerEquipment` | C# の `SetSelectedIndex` は楽観で選択を変えるが、`ApplySelected` のエコーは古い値で巻き戻す。速い 2 ノッチの間に古いエコーが来ると、FIFO 無しでは選択が戻る（Web と同じ理由）。§1.4「ホイール循環を C# 1 か所へ」 | `LocalPlayerEquipment.SelectedIndex` だけを起点にする: 古いエコーで巻き戻る |
| D-10 | 入力は `InputSystemPointerWheelStream` の縦のノッチ（Uniform 設定で 1＝1 ノッチ。上が正）を A-71 と同じ 1 ノッチ＝60px（下が正）に直し、`WheelDeltaUnit.Pixel` で渡す。Web の本番と同じく 1 ノッチ目では進まず 2 ノッチ目で 1 段進む。換算は `EquipmentWheelNotchScale` の 1 か所で、60 は Foundation の共有の定数 `WebWheelScale.PixelsPerNotch` を読む。この定数はこのレーンが `GameScrollView` の private 定数から上げ、`GameScrollView` もそれを読むよう直す（E-1〈反映済み ad92a44777〉が「`GameScrollView` の外で最初に要したレーンが上げてよい」とする。Task 7 Step 1）。カーソルがロック中は UI の上でない。出所: レビュー r1（opus-1。旧案は Page で 1 ノッチ＝1 段）・r2（N-4。旧案は integrator が上げるのを待つ） | InputSystem の値は入力設定 `scrollDeltaBehavior` の既定 Uniform（`StarterAssets.inputsettings.asset` に値が無く既定。InputSettings.cs の `m_ScrollDeltaBehavior`）で [-1,1] に正規化され、1 ノッチ＝1（Windows の 120 も 1 に直る）。`ScrollDeltaBehaviorSettingTest` が固定する。Web（CEF）の本番は 1 ノッチを 60px で送り（CefUnity `ScrollInputPipeline.WheelPixelsPerStep`）、装備は 100px で 1 段・段が出たら端数を捨てる（`equipmentLogic.ts`）。A-71（4ec5ce2dc7 で全読み手へ広げ済み）が UITK のノッチ→px を 60 に揃えている。ロック中はカーソルが画面中央に固定され、Web でもクロスヘアの上（拾わない）と同じ扱い。`ElectricWirePoleSelection.cs` の `/100f` は Uniform と合わない古い前提で反証にならない | Page で 1 ノッチ＝1 段: Web の本番より倍速くなり、根拠にした「Web の 100px＝1 段」は e2e の `wheel(0, 100)` の約束事で本番の CEF の量ではない。1 ノッチ＝1 段が望ましいなら Web との差なので要裁定へ上げる（今は上げていない）。60 をこのレーンに書く: A-71 の値が 2 か所になり片方だけ変わる。integrator が上げるのを待つ（r1 の案）: クリティカルパス上の Task 7 が待ちになり、待ちの持ち主もいない。実機で値を測る: 設定の既定で閉じられ、録画は定義上 1 を注入するので測れない |
| D-11 | 拾いは `EquipmentWheelPointerPicker` が A-133 の換算 `UiScreenToPanel.FromBottomLeft` で `panel.Pick` を呼んで要素を返す。integrator へ `PickAt` を依頼し、入ったら置き換える bd を `--parent` で残す。ロック中かは `InputManager.IsCursorLocked` で読む（A-134。feature/uitk の取り込みで換算の 1 本化 A-133 が入ったのに合わせた）。「UI の上か」は Client.Ui が `EquipmentWheelPickJudge` で bool にしてルーターへ渡し、型は変えない（出所: レビュー r1・Phase 2.6 強1。agent 既定） | 判定に要素（素通しの印の祖先）が要るが、`GameUiPointerProbe` は bool しか返さない。E-1 が許す A-71 の定数を除き、Foundation はこのレーンで変えない。bool にするのは State が UIElements を知らないため（`UiLayerDependencyRules` の向き）で、Web も `isPointerOverWebUi(e.target) && !isWheelPassthrough(e.target)` を bool にしてから判断する。ルーターが画面・モーダル・占有を State から読むので（D-20）、境界を越える bool は `overBlockingUi` 1 つだけになる | `UiInputExclusivity.IsPointerOverGameUi` を使う: 素通しを見られず、装備 HUD の上で止まる。拾った要素を State へ渡す: State が UIElements を参照することになる |
| D-12 | 持ち物のグリッドの面の内側の色 rgb(50 52 67) は `THEME/DerivedTokens.uss` の名札付きブロック「S4-inventory」のトークン `--inventory-slot-face-inset-color` にする | `UiStyleConventionTest` が画面 USS の色の直書きを拒む。名札付きブロックは A-52 の共有区画（画面レーンも足してよいことは 4ec5ce2dc7 で A-52・E-1 に反映済み）で、前例 S3-hotbar-modal Task 5 と S2-hud（A-55・マージ済みの `DerivedTokens.uss`「S2-hud」ブロック） | 既存の同色のトークン（`--slot-frame-selected-trim-face`）を借りる: 意味の違う名前を読むことになり、片方を変えるともう片方が壊れる（同じ源・同じ地のトークンを借りる場合は D-22） |
| D-13 | S3-hotbar-modal の型（`WheelPassthroughMark`・`HotbarHudView`・`--hotbar-slot-frame-face`）を使う作業は Task 9 に寄せ、Task 7 の判定は印を見ない形で先に作る | 起草時に S3-hotbar-modal が未マージ。依存の辺は E-23 にあり、先の Task を止めずに済む | 全部を S3-hotbar-modal のマージ後に始める: クリティカルパスの S4-inventory が W2 の画面レーンを待つ |
| D-14 | 装備の縦の間は 2 枠目からの上の余白（クラス `equipment-hud__slot--after-first`）で写す | USS に `gap` が無い（`SlotGrid` も余白で写している） | 枠の下に余白: 最後の枠の下に余白が残り、床の位置が変わる |
| D-15 | 整理は `IInventorySortCommands.Sort()` が `LocalPlayerInventoryController.SortInventory()` を呼び、いつも受理を返す | Web の `inventory.sort` も手元で断らない。断る条件が無いのに拒否理由の enum を足さない | 拒否理由を足す: 使われない値が `UiCommandRejectReason` と `PauseMenuCommandsTest` の表に増える |
| D-16 | 枠の身振りの振り分け `PlayerSlotGestureRouter` は `Screens/Inventory/Parts/` に置き、持ち物と装備の 2 つの View が使う | 同じ画面の中の 2 か所で、Components へ上げる 2 機能目ではない（§5.1）。S4-alpha の Produces の振り分けの規則をそのまま写す | View ごとに switch を書く: 同じ振り分けが 2 か所になる |
| D-17 | 「ブロックを開いた状態で main の枠を Shift＋左押下するとブロックへ配分される」を UITK の持ち物画面から録画で確かめる（Task 11 の 8）。L1 は足さない | S4-alpha D-33 がこの確認を S4-inventory の完了条件として渡した。計画と実行は alpha の `PlayerSlotCommandsTest`（L0）が持ち、このレーンが足すのは持ち物の枠から UI で押せることだけで、それは本物の画面・DI・入力を通す録画でしか見られない | L1 で `PlayerSlotCommands` をブロックが開いた文脈で組む: alpha の L0 と同じ主張の繰り返しで、UI の結線（Presenter・DI）を通らない |
| D-18 | 持ち物の中身の変化の合図は C4-e の口 `IPlayerInventoryOperations` に `OnSnapshotChanged`（main・装備の中身と装備の選択）として足し、本番は `LocalPlayerInventoryOperations` が 3 つの源（`OnItemChange`・`OnInventoryRefreshed`・`OnSlotsOrSelectionChanged`）を束ねる。`PlayerInventoryPanelModel` は `(operations, equipment)` の 2 つの口で組み、合図はこの 1 本だけを購読する。出所: レビュー r1（配置検査 V-1） | 同じ役割（持ち物の読み口とその変化の合図）は C4-e が 1 つの口にまとめている（`OnGrabChanged`＋`ReadGrab`/`ReadSnapshot`。`PlayerGrabModel` はこの口だけで読み直す）。実装者は本番と記録係の 2 つだけで波及が小さい。マージ済みレーンの State の口へ使い手のレーンが合図を足し、実装者を同じ PR で全部直すことは E-1（反映済み ad92a44777）が許す。出所の追記: レビュー r2（N-1。旧版は根拠に S4-alpha の前例を引いたが、alpha が他レーンの State の口へ足した前例は無かった） | 別の口 `IPlayerInventoryChangeFeed` を新設する（旧案）: 同じ組 `(controller, equipment)` を受ける口が 2 つになり、L1 の土台が 2 つを手で繋ぐ。選択の口も別に購読する: 本番では選択が両方から来て 2 回組む |
| D-19 | ホイールは `Client.Input/Pointer/InputSystemPointerWheelStream`（`InputSystem.onEvent` を購読し、0 は流さず、マウス無しは `LogWarning`）で受け、`EquipmentWheelInput` は `IInitializable` で購読する。位置は既存の `InputSystemPointerPositionStream` から取る。出所: レビュー r1（配置検査 N-1） | UITK 側の入力の前例はイベント購読（`InputSystemPointerPositionStream`・`InputSystemPointerButtonStream`）で、`Client.Ui` に `Mouse.current` を直接読むコードは無い。AGENTS.md「状態変化の検知は購読で」にも合う。登録は前例 `GrabGameUiRegistration` の `InputSystemPointerButtonStream` と同じくレーンの登録に置く | `ITickable` で毎フレーム `Mouse.current.scroll` を読む（旧案）: 取りこぼしは無いが前例と規約から外れる |
| D-20 | `EquipmentWheelRouter`（State）が `UiRoutingState`・`ModalRequestService`・`PlaceSystemStateController` を ctor で受けて状況を自分で組み、`Route(delta, unit, overBlockingUi)` だけを公開する。L1 はルーターを本物の State で組む（`EquipmentWheelRouterFixture`・`UiRoutingStateTestFactory`）。出所: レビュー r1（配置検査 N-4） | Client.Ui に要るのは「UI の上か」だけ。State が `PlaceSystemStateController` を読む前例は `ModeHudModel`。旧案は状況の組み立てを Client.Ui の入力クラスが持ち、L1 はそこを通らず状況を手で作っていたので、組み立ての誤りがテストで捕まらなかった | Client.Ui の入力クラスが 3 つを読んで状況を組む（旧案）: 判断に要る State の読み取りが UI 側に出て、テストが通らない |
| D-21 | 出し入れの規則は各 Presenter が traits を読む（`InventoryScreenPresenter` はローカル関数で `Grab`、`EquipmentHudPresenter` は `AlwaysOnHud` と `Grab`）。代表行は Presenter の L1 `InventoryScreenVisibilityTest` で「その画面で View が出ているか・装備がクリックを受けるか」として確かめる（`GameUiRootTestFactory` を新設。Presenter の L1 は repo に前例が無い）。出所: レビュー r1（配置検査 N-5・usersim W5） | 前例は Presenter のローカル関数（`PauseMenuScreenPresenter`・`ScreenBackdropPresenter`）か State の ViewModel 工場。Web も `screenAllowsGrab` 1 つでパネルの出し分けとクリック可否を決める。全行のループは traits の素通しの同値確認で規則を固定していなかった | Screens の static `InventoryScreenRules`（旧案）: 前例の 3 つ目の形で、同じ `Grab` を 2 つの関数が返す |
| D-22 | 装備 HUD の世界の上の枠の面はホットバーの `--hotbar-slot-frame-face` を借りる。名前は integrator へ世界の地の意味の名前（例 `--world-slot-frame-face`）への改名を依頼する（S3-hotbar-modal のトークンなのでこのレーンは改名しない）。出所: レビュー r1（配置検査 N-3） | 装備 HUD はホットバーと同じ源（Web `SlotFrame .slot` の `rgb(50 52 67 / 50%)` を世界の上で使う。`EquipmentPanel/style.module.css`「ホットバー同様に面を持たない浮遊HUD」）・同じ地（世界）なので、ホットバー側の α を Task 10 で決め直したら装備も一緒に変わるのが正しい。D-12 の却下は意味の違う名前の場合 | 装備用の同値のトークンを足す: 同じ源の値が 2 つになり、片方だけ直る |
| D-23 | `SlotItem` → `ItemSlotContent` の写しは `Screens/Inventory/Parts/PlayerSlotContent.From` に 1 つ置き、2 つの View が使う。S4-alpha の `BlockItemGrid` の同じ式との統合は integrator へ Content（`Client.Ui/Content`）への統合を依頼する。§5.1-2 の例外: 1 機能目（alpha）の式は `BlockItemGrid` のローカル関数で外から呼べる口が無く、E-23 で alpha のファイルを書けないため、申告したうえで自分の `Parts/` に写す（統合は integrator）。View は最新値を覚えず「隠れている間は描かない」を持たない（`Render` は受けたら描き、`SetShown` は出し入れだけ）。「表示中だけ購読」は Presenter に残し、2 つの Presenter の同じ 4 行は共通化しない。§4.2 の View の雛形の注記は ad92a44777 で「出し入れだけ。描かないのは Presenter が表示中だけ購読して守る」に直った。出所: レビュー r1（Phase 2.6 弱4）・r2（検査7。§5.1-2 の例外の理由と、Components の語の取り違え N-3） | 同じ式が View 2 つ（と alpha）にあった。ReactiveProperty は購読時に今の値を流すので、View の `_latest` は Presenter の購読と二重の仕組みで、出すたびに古い値で 1 回描いた直後にもう 1 回描いていた | View ごとに式を持つ: 空きマスの扱いを変えると片方だけ直る。Presenter の 4 行を基底へ上げる: 2 本だけで、前例の Presenter に基底が無い |
| D-24 | `PlayerInventoryPanelModel` は初期値を ctor で組み、`Initialize` は購読と購読までの間の変化の取り込みだけをする。出所: レビュー r1（usersim W3） | 起動時の画面 None は `AlwaysOnHud` が真で、装備 HUD の Presenter は `Initialize` で即 Model を購読する。初期値が null だと登録の順に依って `Render(null)` で落ちる。前例 `PlayerGrabModel`・`ModeHudModel` は ctor で初期値を持つ | 登録の順（Model を Presenter より先に）に頼り、判断記録に書く: VContainer の初期化順に依る形が残る |
| D-25 | `InputSystemPointerWheelStream` は流れの頭で `InputState.currentUpdateType == InputUpdateType.Editor` を除く（`silent-ok` 付き）。L0 の `InputSystemPointerWheelStreamTest` と、流れ → `EquipmentWheelInput` → ルーターを通す L1 の `EquipmentWheelInputFlowTest`（Editor の更新では選択要求 0 件、ゲームの更新では 2 ノッチで 1 段）で確かめる。拾いは口 `IEquipmentWheelPointerPicker` 越しにし、L1 は Fake で通す（前例 `IUiScreenPointer`）。録画の頭で入力設定を焦点無視にする（前例 `blueprint-place-like-block`）。出所: レビュー r2（codex C-1） | プロジェクトの入力設定（`StarterAssets.inputsettings.asset`。`m_BackgroundBehavior: 0`・`m_EditorInputBehaviorInPlayMode: 0`）では、PlayMode で Game View にフォーカスが無いときポインタのイベントが Editor の更新で処理され、`onEvent` の購読者にも届く（InputManager.cs:3320-3345・3456-3457）。除かないと Inspector の上でホイールを回しただけで装備が替わる。テストの土台は既定でプレイヤーの更新を回すので、更新の種類を選んで回さないと赤にならない | ルーターや Gate で止める: 更新の種類は入力の層の事情で、State に持ち込む理由が無い。位置の流れ（`InputSystemPointerPositionStream`）も同時に直す: 既存の他レーンのファイルで、位置だけなら害が小さい（第 3 バケツへ）。ボタンの流れ（`InputSystemPointerButtonStream`）を揃える: ボタンの解放は焦点の外（Inspector の上）でも通さないと掴みが解けなくなるので、揃えない（第 3 バケツへ。出所: レビュー r3 N-1） |
| D-26 | 所持品の表は State の `InventoryItemGuidTable`（スロット → 小文字の GUID 文字列）にし、アンカー id は Mark を呼ぶ Client.Ui の `InventorySlotAnchorMarks` が `AnchorIds.InventoryItem(guid)` で組む。出所: レビュー r2（opus O-1） | 網羅テスト `TutorialAnchorCoverageTest` は `Scripts/Client.Ui` の下で `TutorialAnchor.Mark(` を含むファイルからだけ `AnchorIds.` を拾う。id を State で組むと `InventoryItemPrefix` が拾われず、保留の行を消した Task 6 で赤になる | 保留の行を残す: このレーンのアンカーが網羅テストの外に出たままになる。網羅テストの走査を `Client.Ui.State` へ広げる: S2-tutorial の Foundation のテストを画面レーンが変えることになる |
| D-27 | 登録は `Client.Starter/Registration/GameUi/Screens/InventoryGameUiRegistration.cs` に置き、`Screens/` が無ければこのレーンが作る。integrator の再編は待たない。出所: レビュー r2（O-2・N-4） | `GameUi/` 直下は S4-alpha で 10 本の上限に達し、このレーンの 1 本で規約に反する。E-1（反映済み ad92a44777）が W4 以降の画面レーンの置き場を `GameUi/Screens/` に決め、最初に要したレーンが作るとした | 直下に置いて再編を待つ（r1 の案）: 11 本目で規約に反し、Task 8 の登録がクリティカルパス上で integrator 待ちになる |

---

## 設計書・実行計画へ反映する行

設計書 `docs/uitk-migration/execution/component-architecture.md` §7.1 に足す行（A-145〜A-149 のうち 4 つを使う）:
- A-145: 装備のホイールでの循環は State の `EquipmentWheelSelector`（`Client.Ui.State/Inventory/Equipment/`）1 か所。Web と同じ未確認 FIFO を持ち、確定の世代（`LocalPlayerEquipment.SelectionConfirmationRevision`）が進んだ数だけ頭を捨てる。抑止は純関数 `EquipmentWheelGate`（モーダル・`SubInventory`・`ResearchTree`・`BuildMenu`／素通しでない UI の上／ツールの占有の順）で、画面・モーダル・占有は `EquipmentWheelRouter` が State から読み、Client.Ui からは「UI の上か」の bool だけを受ける。入力は `Client.Input` の `InputSystemPointerWheelStream`（`InputSystem.onEvent` の購読。Editor の更新で届いたものは流さない）のノッチを、Client.Ui の `EquipmentWheelInput` が A-71 と同じ 1 ノッチ＝60px に直して `WheelDeltaUnit.Pixel` で渡す（Web の本番と同じく 1 ノッチ目では進まず 2 ノッチ目で 1 段）
- A-146: 装備 HUD は `AlwaysOnHud` の画面で出し（blocking スキットで隠さない）、クリックは `Grab` の画面でだけ受ける。HUD は拾う実 UI で、`WheelPassthroughMark` でホイールだけ素通す。持ち物パネルは `Grab` の画面で `StageColumn.Inv` に出す
- A-147: 持ち物の所持品のアンカー（`inventory.item-<guid>`）は、State の `InventoryItemGuidTable` がスロット → GUID の表にし、id の組み立てと付け替えは View（`InventorySlotAnchorMarks`。Mark と同じファイル）が描き直しのたびに行う。`TutorialAnchor.Unmark` が入るまで画面側で外す
- A-148: `equipment.spec`「持ち物画面の一覧スクロールでは装備が持ち替わらない」は判定の持ち主の S4-inventory が素通しでないスクロール器の立て役で写す。S4-recipe の一覧は素通しの印を付けない

実行計画 `docs/uitk-migration/execution/execution-plan.md`:
- §1.4 の S4-inventory 行の件数を実物に直す: 「写す: inventory 61（P20・C16・L25 は L0-s4a 済み）」→「写す: inventory 62 件（P20・C16・L26。L26 は L0-s4a 19 件と L0-s4b の `blockSlotRestriction` 7 件が写し済み、P のうち掴み絵の 5 件は C4-e、`blockInventory*` は S4-alpha）のうちこのレーンの 31 件、束の外 3 件を足した 34 件（32 件を写し 2 件を処分表へ。S4-inventory 計画 D-5）」
- DAG の辺 S3-hotbar-modal → S4-inventory（`WheelPassthroughMark`）は 9b2f7ee3ea で §1.4 の依存の列・mermaid（`S3h -->|WheelPassthroughMark E-23| Inv`）・E-23 に反映済み。この計画では足す行が無い（確かめた）
- S4-recipe の行へ申し送り 1 行: 「一覧（`ItemListPanel`）に `WheelPassthroughMark` を付けない（装備のホイールの抑止は S4-inventory が素通しでない UI の上で止める。S4-inventory 計画 D-6）」
- §1.4 の S5-crosscut の `regression/inputLayer.spec`(5) を (4) にし、ホイールの 1 件は S4-inventory が写す: 反映済み 4ec5ce2dc7

設計書・実行計画の既存の行の書き換え（レビュー r1 で要った分）:
- A-71 の対象を `GameScrollView` から「UITK でホイールのノッチを px に換算するすべての読み手（S4-inventory の装備のホイールなど）」へ広げる（D-10）: 反映済み 4ec5ce2dc7
- A-52 と E-1 の名札付きブロックを「部品・画面レーンは `DerivedTokens.uss` に名札付きブロックを足してよい」へ広げる（D-12）: 反映済み 4ec5ce2dc7

設計書・実行計画の既存の行の書き換え（レビュー r2 で要った分）:
- E-1 に「W4 以降の画面レーンの登録ファイルは `Client.Starter/Registration/GameUi/Screens/` に置く（最初に要したレーンがディレクトリを作る）」を足す（D-27）: 反映済み ad92a44777
- E-1 に「マージ済みレーンの State の口（例 C4-e の `IPlayerInventoryOperations`）へ、使い手のレーンが合図・読み口を足してよく、実装（本番と記録係）は同じ PR で全部直す」を足す（D-18）: 反映済み ad92a44777
- E-1 に「ホイール 1 ノッチの px（A-71）は、`GameScrollView` の外で最初に要したレーンが Foundation の共有の定数へ上げてよい」を足す（D-10）: 反映済み ad92a44777
- §4.2 の View の雛形の `SetShown` の注記を「出し入れだけ。非表示の間に描かないのは Presenter が表示中だけ購読して守る」へ直す（D-23）: 反映済み ad92a44777

---

## 起草メモ（本体向け）

- **件数の差（§1.4 との差）**: inventory の実物は 62 件（P20・C16・L26）で、§1.4 の「61・L25」と 1 件違う。`slotActions.test.ts` の複数行の `it.each` を 1 件と数えると L26 になる（L0-s4a 9 件）。また L26 には L0-s4b の `blockSlotRestriction` 7 件が入っていて、§1.4 の「L25 は L0-s4a 済み」は L0-s4b の分を落としている。このレーンの持ち分は 34 件（inventory 31・束の外 3）で、32 件を写し 2 件を処分表へ（D-5）。反映の行は上の節
- **S4-alpha の Produces と違う形は要らない（改訂版 85f905aa3d と照合済み）**: 使うのは `IPlayerSlotCommands` の 5 つの口と `PlayerSlotCommands.CommandName` だけで、型名・名前空間（`Client.Ui.State.SlotCommands.Player`）は改訂後も同じ。振り分け規則（LeftDown→LeftDown(slot, gesture.Shift) 等）はそのまま `PlayerSlotGestureRouter` に写した。登録先は改訂どおり `MainGameUiRegistration` の共有サービスの並び（このレーンは登録しない）。`IUiStateTransitionRequester` は使わない（閉じる・遷移の操作が無い）。L1 の `InventoryScreenFixture` の ctor と `using` は改訂版の `PlayerSlotCommandsTest` の SetUp に合わせた。alpha D-33 で渡された「main の枠の Shift→ブロック」の録画は Task 11 の 8 と D-17 で引き受けた
- **申告（Foundation・Content の口。勝手に作らず integrator へ。Task 1 Step 3）**: (1) `TutorialAnchor.Unmark`（D-8）、(2) `GameUiPointerProbe` の要素を返す口 `PickAt` と Screens の既存の 2 つの変換の寄せ（D-11）、(3) `--hotbar-slot-frame-face` の改名（D-22）、(4) `SlotItem` → `ItemSlotContent` の Content（`Client.Ui/Content`）への統合（D-23）。どれも無くても動く形で書いた（画面側で外す・座標変換を写す・トークンを借りる・写しを持つ）ので、integrator 待ちで止まる Task は無い。入ったら置き換える bd を `--parent` で残す。A-71 の 60px の共有の定数は E-1 に従いこのレーンが上げる（D-10）
- **登録ディレクトリの本数**: `Client.Starter/Registration/GameUi/` 直下は起草時 9 本（S2-tutorial 取り込み後）、alpha で 10 本。このレーンは E-1 どおり `GameUi/Screens/` に置くので直下は増えない（D-27）。S4-alpha 計画の中の古い記述（4854 行「alpha で 9 ファイル」・318 行「10 本以上なら Task 8 の登録を保留」が 54 行と食い違う）は alpha 側の軽微として指揮へ申し送る
- **共有区画**: `THEME/DerivedTokens.uss` に名札付きブロック「S4-inventory」を 1 行足す（D-12）。alpha 計画は「トークン USS は変えない」としているが、色の直書きが規約で拒まれるため、前例 S3-hotbar-modal と同じ名札付きブロックの形を取った
- **S2-tutorial**: S2-tutorial（#1627）は `origin/feature/uitk` にマージ済みで、`Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` に S4-inventory 持ちの 4 行がある。このレーンが同じ PR で消し、網羅テスト（`Client.Tests.Ui.Anchor`）を Task 6・8・12 の絞り込みで回す（Task 6 Step 3）
- **指揮の共通の注意の反映**: `:root` のトークンを C# で読むのは `--inventory-columns` だけで、グリッドのクラスで `--inventory-view-columns: var(--inventory-columns)` と受け直して読む。`disposition.md` には処分した 2 行だけを書き、写したテストの行は書かない（albof.29・albof.31 の「写す予定」の行はタグを足すコミットで消す。albof.29 は Task 4、albof.31 は Task 9）。`UiCommandRejectReason` に値は足さない（D-15）。素通しの getter は書いていない（式本体は別オブジェクトの値の読み替えか、`ReactiveProperty` を読み取り専用の型で出す前例の形だけ）
- **未確認（実装時に測る）**: `inputLayer.spec.ts` の束（5 と見た。Task 8 Step 4 で確かめる）。テストの土台で Editor の更新にイベントを処理させる条件（`InputTestDriver.LoseGameFocus` で足りるか。Task 7 の前提の assert が確かめる）。Editor の更新の除外（D-25）はコードを読んだ判定で、実機の Inspector の上のホイールでは再現していない。CefUnity の本番のスムーザが 60px を複数フレームに分けて送るので、本番の Web の平均の刻みは 2 ノッチより少し速い（約 1.67 ノッチ/段）と読める（推測。コードを読んだだけで実測していない。UITK は 1 イベントで 60px なので毎回ちょうど 2 ノッチ）
- **要裁定**: なし

---

## 本 PR 外のリファクタ提案

（Phase 2.6 の第 3 バケツ。このレーンのタスクにはしない。着手の可否は人が決める）

- 既存側 `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiPointerProbe.cs`（`IsPointerOverGameUi`: `UiScreenToPanel.FromBottomLeft`→`panel.Pick`）と `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/UiPointerHitTest.cs`（`IsPointerOverAnyUi`: `InputManager.IsCursorLocked` なら偽）／新設側 `EquipmentWheelPointerPicker.PickAt` と `EquipmentWheelPickJudge.IsBlockingUi`・`EquipmentWheelInput` のロック中の扱い／同役割と判断した根拠: どれも「画面座標のポインタが UI の上か（ロック中は上でない）」を同じ変換と `Pick` で決めている。既存側に要素を返す口を足して新設側を委譲にするか、その逆かを決める（`PickAt` は integrator へ依頼済み。D-11）
- 既存側 `moorestech_client/Assets/Scripts/Client.Input/Pointer/InputSystemPointerPositionStream.cs`（`InputSystem.onEvent` の購読で、更新の種類を見ない）／新設側 `InputSystemPointerWheelStream` の Editor の更新の除外（D-25）／同役割と判断した根拠: 同じ `onEvent` の購読で、Game View の外の位置も Editor の更新で流れる。位置だけなら害は小さいが、同じ規則に揃えるかを決める
- 既存側 `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/CefInputForwarder.cs:86`（`ScrollInputPipeline.WheelPixelsPerStep` を直接読む）／新設側 `Client.Ui/Foundation/Input/WebWheelScale.PixelsPerNotch`（D-10）／同役割と判断した根拠: どちらも 1 ノッチ＝60px（CefUnity の正本）。共有の定数が正本と同じ値であることを `GameScrollViewTest:171` と同じくリテラルで照合するかを決める。CefInputForwarder は正本を読むので触らない
- 既存側 `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleSelection.cs:89-93`（毎フレーム `Mouse.current.scroll.ReadValue().y / 100f`＋`UiPointerHitTest.IsPointerOverAnyUi()`）／新設側 `InputSystemPointerWheelStream`＋`EquipmentWheelInput`／同役割と判断した根拠: どちらも「UI の上でなければホイールの段で循環する」。電柱の側は Uniform の入力設定では 1 ノッチ＝0.01 で、計算上 100 ノッチで 1 段になる（推定。実機では測っていない）。bd `moorestech-9a7t9` に起票済み（このレーンの外）
- 既存側 `moorestech_client/Assets/Scripts/Client.Input/Pointer/InputSystemPointerButtonStream.cs`（`InputSystem.onEvent` を購読し、更新の種類を見ずに左ボタンの解放の縁を流す）／新設側 `InputSystemPointerWheelStream` の Editor の更新の除外（D-25）／同役割と判断した根拠: 同じディレクトリで同じ `onEvent` を購読する入力の流れ。ただしボタンの解放は焦点の外でも通さないと掴みが解けなくなるので、このレーンの判断は「揃えない」（D-25）。族の規則として明文化するかを決める

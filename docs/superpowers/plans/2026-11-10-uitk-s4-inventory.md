# UITK S4-inventory（インベントリ・装備）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/inventory` の `InventoryPanel`（持ち物パネル: 題・整理ボタン・10 列のスロット・所持品のアンカー）と `EquipmentPanel`（装備 HUD: 右下の縦列・選択枠・アンカー・掴める画面だけのクリック・ホイールでの装備の循環）を、C3-a・C3-b・C4-e の部品と口、L0-s4a のロジック、S4-alpha の `IPlayerSlotCommands`、S3-hotbar-modal の `WheelPassthroughMark` で Web と同じ見た目・同じ操作の UITK 画面にする。ホイールでの装備の循環は C# の 1 か所（State の `EquipmentWheelSelector`＋判定 `EquipmentWheelGate`）に置き、Web の未確認 FIFO と同じ規則で古いエコーに巻き戻されないようにする。L0/L1・並べ比べ（720p・2560×1080）・録画（新設 `uitk/uitk-inventory-equipment`）まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。State（`Client.Ui.State/Inventory/`）に 2 つの塊を足す。(1) `Panel/`: 持ち物と装備の変化の源の口 `IPlayerInventoryChangeFeed`（本番は `LocalPlayerInventory.OnItemChange`・`LocalPlayerInventoryController.OnInventoryRefreshed`・`LocalPlayerEquipment.OnSlotsOrSelectionChanged` の 3 つを束ねる。Web ホスト `InventoryTopic` と同じ源）を購読し、C4-e の `IPlayerInventoryOperations.ReadSnapshot()` から不変の `PlayerInventoryViewModel`（main・装備・選択・所持品のアンカー表）を組む `PlayerInventoryPanelModel`、整理の型付きコマンド `IInventorySortCommands`。(2) `Equipment/`: 装備の選択の口 `IEquipmentSelectionPort`（本番は `LocalPlayerEquipment`）、ホイールの累積・循環・未確認 FIFO を持つ `EquipmentWheelSelector`、層・UI の上・ツールの占有で止める純関数 `EquipmentWheelGate`、それらを繋ぐ `EquipmentWheelRouter`。Client.Ui（`Screens/Inventory/`）は `Panel/`（`InventoryView`・Presenter。`StageColumnsLayout.Get(StageColumn.Inv)` へ載せる [A-92]）、`Equipment/`（`EquipmentHudView`・Presenter。`UiLayer.Chrome`・`UiFamily.Viewport` へ）、`Parts/`（`SlotGesture` → `IPlayerSlotCommands` の振り分け）、`Wheel/`（`Mouse.current.scroll` を読み、パネルで拾った要素と `WheelPassthroughMark` から「UI の上か」を決めてルーターへ渡す `ITickable`）。登録は `Client.Starter/Registration/GameUi/InventoryGameUiRegistration.cs` の 1 本と、`MainGameUiRegistration` の画面登録区画の 1 行。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`CustomStyleResolvedEvent`・`IPanel.Pick`）、Input System（`Mouse.current.scroll`）、UniRx、VContainer 1.10、NUnit＋`com.unity.ui.test-framework`、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（test-name-parity）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`THEME/` = `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/`、`TSS` = `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`、`LOGS` = `~/hermes-agent/data/repos/moorestech_logs`、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`。コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1・§2・§3・§4.1〜§4.3・§5.1・§7.1 の A-45・A-52・A-57・A-58・A-70・A-92・A-109・A-110・A-142）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-inventory 行・§4.3・§5・§6.2・E-1・E-16・E-22・E-23）、先行レーンの申し送り（`LOGS/harness/uitk/C4-e/done.md`・`LOGS/harness/uitk/S4-alpha/done.md`）、S4-alpha 計画 `docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md` の「## 後続レーンへの Produces 一覧」、S3-hotbar-modal 計画 `docs/superpowers/plans/2026-11-07-uitk-s3-hotbar-modal.md` の Task 7（`WheelPassthroughMark`）と Task 5（`--hotbar-slot-frame-face` の名札付きブロック）

## Requirements

設計の出所: 実行計画 §1.4 の S4-inventory 行（完了条件の原文:「InventoryPanel・EquipmentPanel（ホイール循環を C# 1 か所へ・床トークン）・スロットへのアンカーの印付け（`TutorialAnchor.Mark`。DragGuide 自体は S2-tutorial。E-16）。写す: inventory 61（P20・C16・L25 は L0-s4a 済み）、`regression/inputLayer.spec.ts` のホイール 1 件、`regression/itemSlot.spec.ts` のツールチップ 1 件（C3-a から。bd `moorestech-albof.29`）、`commonHud/alwaysOnHudWheel.test.ts`「装備HUDは実UIのままホイールだけ素通す」（S2-hud から。bd `moorestech-albof.31`）。パネルは `StageColumnsLayout.Get(StageColumn.Inv)` へ載せる [A-92]」）。件数は実物で数え直した（下の「写すテスト」。§1.4 との差は起草メモ）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 持ち物の状態: `PlayerInventoryPanelModel` が `IPlayerInventoryChangeFeed.OnInventoryChanged` を購読し、変化のたびに同期で `PlayerInventoryViewModel`（main の全スロット・装備の全スロット・選択中の装備・所持品のアンカー表）を配る。`Client.WebUiHost` を参照しない | `PlayerInventoryPanelModelTest` が緑 |
| R-2 | 所持品のアンカー: 同じ ItemId を持つスロットのうち先頭（空・個数 0 は数えない。`OwnedItemCounts.FirstSlotIndexByItemId`）にだけ `AnchorIds.InventoryItem(<guid>)`（小文字）を付ける。描き直しで外れたスロットからは id のクラスと印のクラスを外す | `InventoryViewTest`（Web `InventoryPanel/index.test.ts` 2 件）が緑 |
| R-3 | 持ち物パネル: `GamePanel(inventory-panel, Default)`・題 `ui.inventory.title`・題の操作 `PanelActionButton(inventory-sort, ui.inventory.sort)`（押すと `IInventorySortCommands.Sort()`、結果は `UiCommandResultToaster.Report("inventory.sort", …)`）。本文 `SlotGrid(main-grid)`（列数は `--inventory-columns`）に `ItemSlot(main-slot-<i>)`。パネルの寸法・題のつまみ（幅 `--inventory-panel-width`・最小高 452.391px・`translate` 0.783px・`--panel-left/right` -2.22px・`--title-shift-x` -1.96px・`--title-scale-x/y` 0.919/0.924）とグリッドのつまみ（`--slot-size` 35.1776394px・`--slot-grid-gap` 7.0815392px・`--count-font-size` 12.337807px・`--filled-face-inset` 1.565749px・`--face-inset-color` rgb(50 52 67)・`--icon-pad` 1px・`--count-bottom` -1px・`--count-letter-spacing` 0.12em→12・上 12px・左 -0.549px）は Web の `GRID_STYLE` と同値 | `InventoryViewTest`（Web `inventory.spec`「接続後にインベントリが描画される」）と `InventoryGridKnobTest` が緑 |
| R-4 | 出し入れ: 持ち物パネルは `UiScreenTraitsTable.Get(screen).Grab` が真の画面だけ（Web `screenAllowsGrab(screen) && <InventoryPanel/>`）。装備 HUD は `AlwaysOnHud` が真の画面だけ（blocking スキット中も隠さない。Web は `screenShowsAlwaysOnHud` だけで決め、ホットバーと違ってスキットで隠れない） | `InventoryScreenVisibilityTest` が全 `UiScreen` の行で緑 |
| R-5 | スロット操作: 持ち物と装備の枠の押下・右押下・ダブルクリック・左進入・右進入を `IPlayerSlotCommands`（S4-alpha）へ渡し、結果を `UiCommandResultToaster.Report(PlayerSlotCommands.CommandName, …)` へ。装備の枠は `Grab` が偽の画面では何も呼ばない（Web は `useGrabInteractive` が偽のときハンドラごと付けない）。ホバーはツールチップ（C3-b）に任せる | `InventoryPanelGestureTest`（Web `inventory.spec` 3 件）・`EquipmentHudViewTest`（Web `EquipmentPanel/index.test.ts` 3 件）・`InventoryEquipmentFlowTest`（Web `equipment.spec` 3 件）が緑 |
| R-6 | 装備 HUD: `equipment-slots`（クラス `equipment-hud`）を Viewport の Chrome 層へ。右 `--equipment-right`・下 `--equipment-bottom`（＝`--hotbar-bottom`。床トークン）・縦に並べ・間 `--equipment-slot-gap`・最小幅高 `--equipment-slot-size`・枠の大きさ `--slot-size: var(--equipment-slot-size)`。枠 `ItemSlot(equipment-slot-<i>)`、選択枠は `ItemSlotMarks.selected`。アンカー: HUD に `equipment.hud`、枠に `equipment.slot-<i>`、選択枠に `equipment.selected-slot` も。HUD は拾う（Web `pointer-events: auto`） | `EquipmentHudViewTest`（Web `EquipmentPanel/index.test.ts`「装備枠がアンカーを名乗り…」・`equipment.spec`「装備枠0でも装備HUDは面積を保ちアンカーが解決する」）と `EquipmentHudFloorTokenTest` が緑 |
| R-7 | ホイールの循環（C# 1 か所）: `EquipmentWheelSelector.Wheel(delta, unit)` は枠 1 以下なら何もせず、`EquipmentSelectionLogic.AccumulateWheelSteps` で累積し、段が出たら「未確認の末尾、無ければ今の選択」から `CycleEquipment` した番号を FIFO に積んで `IEquipmentSelectionPort.Select` を呼ぶ。確定の世代（`SelectionConfirmationRevision`）が進んだ数だけ FIFO の頭を捨て、戻ったら空にする | `EquipmentWheelSelectorTest`（Web `EquipmentPanel/index.test.ts` 4 件・`regression/inputLayer.spec` 1 件）が緑 |
| R-8 | ホイールの抑止: `EquipmentWheelGate.Judge` は、モーダルの保留中・`SubInventory`・`ResearchTree`・`BuildMenu`（Web `readActiveLayer() != "game"`）、素通しの印の無い UI の上、`PlaceSystemStateController.IsWheelOwnedByTool` が真、のどれかで止める。占有は C# の値をそのまま読み、配置対象の種別から導き直さない | `EquipmentWheelGateTest`（Web `EquipmentPanel/index.test.ts` 4 件）と `EquipmentWheelFlowTest`（Web `equipment.spec` 4 件）が緑 |
| R-9 | 入力: `EquipmentWheelInput`（`ITickable`）が `Mouse.current.scroll` の縦（ノッチ。上が正）を符号反転して `WheelDeltaUnit.Page`（1 ノッチ＝1 段）で渡す。カーソルがロック中は UI の上ではない。ロックしていなければ `GameUiRoot` のパネルで拾った要素を `EquipmentWheelPickJudge.IsBlockingUi` に掛ける | 録画 `uitk/uitk-inventory-equipment` でホイール 1 ノッチが 1 段進み、持ち物の上では進まない |
| R-10 | 素通し（S3-hotbar-modal の `WheelPassthroughMark` を使う。マージ後に実物と照合）: 装備 HUD に印を付け、`IsBlockingUi` は「拾った要素があり、かつ素通しでない」。装備 HUD・ホットバーの列・ホットバーの帯の列外・クロスヘアの上では切り替わる | `EquipmentWheelPassthroughTest`（Web `equipment.spec` 3 件・`alwaysOnHudWheel.test.ts` 1 件）が緑 |
| R-11 | 世界の上の色: 装備 HUD は世界の上に載るので、ホットバーの画面ルートと同じ枠の面のつまみ（`--slot-frame-face: var(--hotbar-slot-frame-face)` ほか、S3-hotbar-modal がマージした `Hotbar.uss` の実物の上書き）を HUD のルートで上書きする（マージ後に実物と照合） | 並べ比べ `3-hotbar-equipment-empty`・`2-game-hud` の装備の枠の面が差の表で「同じ」か LX |
| R-12 | ツールチップ: 持ち物の枠の上で、マスタ GUID の辞書名が C3-b のホバーの宿主に出る（`ItemSlot` の `HoverTooltipMark`） | `InventoryPanelTooltipTest`（Web `regression/itemSlot.spec`。bd `moorestech-albof.29`）が緑 |
| R-13 | 並べ比べ: 参照 `4-player-inventory`・`4-player-inventory-full`・`3-hotbar-equipment-empty`・`2-game-hud`・`5-japanese-inventory` を 1280×720・2560×1080 で撮り、差の表を `done.md` に書く | 差の表の全行が「同じ／直した／LX-111〜LX-115／他レーン持ち」のどれか |
| R-14 | 登録: `MainGameUiRegistration` の画面登録区画に `InventoryGameUiRegistration.Register(builder);` を 1 行、`TSS` の `Screens` 区画に `@import` を 1 行、`THEME/DerivedTokens.uss` の末尾に名札付きブロック「S4-inventory」（色 1 行） | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`）が緑 |
| R-15 | 写すテスト（実物で数え直した 34 件のうち 32 件を写し、2 件を処分表へ。下の「写すテスト」表）。`node tools/uitk/test-name-parity.mjs --bundle 4`・`--bundle 2`・`--bundle 5` でこのレーンのファイルの未対応 0。`docs/uitk/disposition.md` の albof.29・albof.31 の「写す予定」の行は、タグを足す同じコミットで消す | parity の出力にこのレーンのファイル（`inventory.spec.ts` のこのレーンの 4 件・`equipment.spec.ts`・`InventoryPanel/index.test.ts`・`EquipmentPanel/index.test.ts`・`inputLayer.spec.ts` のホイール 1 件・`itemSlot.spec.ts` のツールチップ 1 件・`alwaysOnHudWheel.test.ts` の装備 HUD 1 件）の未対応が無い |
| R-16 | 録画: `uitk/uitk-inventory-equipment`（新設。持ち物を開く・整理・掴んで装備へ置く・装備の枠のクリックが移動になる・ゲーム画面でホイール 1 ノッチずつ循環・持ち物の上のホイールで進まない・装備 HUD の上で進む・チェストを開いた状態で main の枠の Shift＋左押下がチェストへ配分される〈S4-alpha D-33 から渡された完了条件〉）と `uitk/uitk-grab-overlay`（C4-e。押下の配線が alpha → このレーンへ移ったことの回帰）を全体スイッチ UITK で回す | 2 本の `result.json` が PASS、mp4、`done.md` に `UiInputExclusivity.ActiveUiSystem` の値 |
| R-17 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd を閉じる | `git log origin/feature/uitk` にこのレーンのマージコミット。`LOGS/harness/uitk/S4-inventory/done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- スロット操作の解釈（左・右・ダブル・Shift の計画、分割ドラッグの確定、掴み絵）。S4-alpha の `IPlayerSlotCommands`・C4-e の `IPlayerSlotDragCommands`・`GrabOverlayView` が持ち、このレーンは呼ぶだけ
- 掴み絵 `GrabOverlay`（C4-e が作り済み。`grabOverlay.spec`・`inventory.spec` の掴み絵の 3 件は C4-e が写し済み）
- 所持品の一覧（Web `ItemListPanel`。S4-recipe）。ただし「一覧の上でホイールが装備を回さない」のテストはこのレーンが持つ（D-6）
- DragGuide（チュートリアルの overlay。S2-tutorial。E-16）。このレーンはスロットへのアンカーの印付けだけ
- ホットバー（S3-hotbar-modal）。素通しの印 `WheelPassthroughMark` と `--hotbar-slot-frame-face` は使うだけ
- 部品（`GamePanel`・`PanelActionButton`・`ItemSlot`・`SlotGrid`）・Foundation（`StageColumnsLayout`・`HoverTooltipHost`・`TutorialAnchor`・`GameUiPointerProbe`）・トークン USS（`Tokens.uss`・`SlotTokens.uss`）・`UiScreenTraits`・`AnchorIds` は変えない。要るものは `done.md`「integrator への依頼」へ（`TutorialAnchor.Unmark`・`GameUiPointerProbe` の要素を返す口。D-8・D-11）
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は変えない
- Web の `ConnectingPlaceholder`（topic 未着の仮の面）。C# の持ち物は起動時から在るので出す場面が無い（D-4）
- 装備の選択の失敗の巻き戻しと再接続の FIFO の破棄（C# の `SetSelectedIndex` に失敗の経路が無く、UITK に WebSocket の再接続が無い。処分表へ。D-5）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s4-inventory --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる。Mac mini の Editor は同時 2 本まで。Mac が逼迫したら Windows の `~/bin/winunity`（compile/test/play を ssh で回す）も使える
- **依存**: S4-alpha（`IPlayerSlotCommands`・`PlayerSlotCommands`）・C3-b（`HoverTooltipHost`。E-22）・C4-e・C3-a・L0-s4a が入った `origin/feature/uitk`。**S3-hotbar-modal（`WheelPassthroughMark`・`HotbarHudView`・`--hotbar-slot-frame-face`）は起草時に未マージ**。その型を使う作業は Task 9・10 に寄せ、Task 1 Step 1 でマージ済みかを確かめる。未マージなら Task 1〜8 を先に進め、Task 9 の前で `origin/feature/uitk` を取り込み直す（それでも無ければ指揮へ返して待つ）。S3-hotbar-modal の型は計画の写しで書いてあるので、Task 9 の Step 1 で実物と照合する
- **登録の本数**: `S/Client.Starter/Registration/GameUi/` は起草時 8 本、S4-alpha で 9 本、このレーンで 10 本（S3-hotbar-modal が先に入れば 11 本）。S4-alpha が integrator へ再編を依頼済み（alpha 計画 Task 1 Step 3）。Task 1 Step 1 で本数と再編の有無を見て、再編済みならその置き場（例 `GameUi/Screens/`）へ、未再編で 10 本を超えるなら Task 8 の登録だけを保留して他を進め、`done.md`「integrator への依頼」に重ねて書く
- **規約**: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下（`.meta` を除く）・partial 禁止・`Func<>` 禁止・イベントは UniRx（`Action` 禁止）・try-catch 禁止（このレーンに外部境界は無い）・日英 2 行コメント・デフォルト引数禁止・単純 getter/setter 禁止（`{ get; private set; }` と式本体の計算プロパティは可）・初期化は `Initialize`・null チェックは外部データだけ（`Mouse.current` は入力機器の有無なので外部）・fail-closed の経路は開発者ログ・意図して黙る行は `silent-ok: <理由> / <誰が気づくか>`・`#region Internal` はメソッド内のローカル関数だけ・`.meta` を手で作らない・Prefab/シーンを手で編集しない・`Update()` で同値判定しない（ホイールの `Tick` は入力の読み取りで、状態の同値判定はしない）
- **State は `Client.WebUiHost` を参照しない**。Web ホスト `InventoryTopic` の源の束ね方は State の `LocalPlayerInventoryChangeFeed` に写し直す（C4-e D-5 と同じ扱い）
- **USS**: 画面 USS の先頭に出所の 1 行（日英）。色はトークン経由だけ（`UiStyleConventionTest` が色の直書きを拒む）。部品のつまみの上書きは画面のルート（`.inventory-panel__grid`・`.equipment-hud`）で。UITK の USS に `calc()` は無いので、Web の `calc(var(--inventory-slot-base-size) * var(--inventory-slot-scale))` 等は前計算した値をコメントの式つきで書き、`InventoryGridKnobTest` がトークンの積と一致することを見る。字間は A-70 の換算（em ×100）
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有ファイルは、`MainGameUiRegistration.cs` の画面登録区画の 1 行、`TSS` の `Screens` 区画の `@import` 1 行、`THEME/DerivedTokens.uss` の末尾の名札付きブロック「S4-inventory」（[A-52]。前例 S3-hotbar-modal Task 5）、`docs/uitk/disposition.md` の末尾と albof.29・albof.31 の行の削除、`docs/uitk/TODO.md`「## 見た目の例外」（LX-111〜LX-115）。S2-tutorial が先にマージされていれば `TutorialAnchorCoveragePending` の S4-inventory 持ちの 4 行（`EquipmentHud`・`EquipmentSelectedSlot`・`EquipmentSlotPrefix`・`InventoryItemPrefix`）を同じ PR で消す（Task 6 Step 6）
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
public class ModalRequestService { public ModalRequest Pending { get; } public IObservable<Unit> OnPendingChanged { get; } }
namespace Client.Ui.State.Commands
public abstract class UiCommandResult; public sealed class UiCommandAccepted { public static readonly UiCommandAccepted Instance; }
namespace Client.Ui.State.Toast
public sealed class UiCommandResultToaster { public void Report(string commandName, UiCommandResult result); }
namespace Client.Ui.State.Inventory.Actions
public interface IPlayerInventoryOperations { IObservable<Unit> OnGrabChanged { get; } PlayerInventorySnapshot ReadSnapshot(); SlotItem ReadGrab(); /* Move… Collect… */ }
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
public class PlaceSystemStateController { public bool IsWheelOwnedByTool { get; } public IObservable<bool> OnWheelOwnedByToolChanged { get; } }

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

- **テストの土台**（実物）: `Client.Tests.Ui.TestHost.UiTestHost`（テーマを読み込んだテストのパネル。`panelSize`・`pixelsPerPoint`・`rootVisualElement`・`simulate`）、`Client.Tests.Ui.Screens.ScreenTestHost : UiTestHost`（`Toaster`・`ShownToasts`・`Mount<T>`・`Find<T>(root, testid)`・`IsShown`）、`Client.Tests.Ui.State.Inventory.RecordingPlayerInventoryOperations(int main, int equipment)`（`SetMain`・`SetEquipment`・`SetGrab`・`ReadMain`・`Moves`〈`"Main:0->Grab x5"` の書式〉・`Collects`）、`FakeContentTextureSource`、`Client.Tests.Ui.Tooltip.HoverTooltipTestStage`（宿主の組み方の前例）。サーバーのマスタは `new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory))`、木材の GUID `00000000-0000-0000-1234-000000000001`

## 設計検査記録

- 配置検査: 未実施
- Phase 2.6: 未実施

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
| `S/Client.Ui.State/Inventory/Panel/IPlayerInventoryChangeFeed.cs` | 持ち物・装備の変化の口 | 新規 |
| `S/Client.Ui.State/Inventory/Panel/LocalPlayerInventoryChangeFeed.cs` | 本番: 3 つの源を束ねる（Web `InventoryTopic` と同じ源） | 新規 |
| `S/Client.Ui.State/Inventory/Panel/PlayerInventoryViewModel.cs` | 不変の描画用の値 | 新規 |
| `S/Client.Ui.State/Inventory/Panel/PlayerInventoryPanelModel.cs` | 購読して ViewModel を配る | 新規 |
| `S/Client.Ui.State/Inventory/Panel/InventoryItemAnchorTable.cs` | 所持品のアンカーの表（スロット → id） | 新規 |
| `S/Client.Ui.State/Inventory/Panel/IInventorySortCommands.cs`・`InventorySortCommands.cs` | 整理の型付きコマンド（`inventory.sort`） | 新規 |
| `S/Client.Ui.State/Inventory/Equipment/IEquipmentSelectionPort.cs`・`LocalPlayerEquipmentSelectionPort.cs` | 装備の選択の口と本番 | 新規 |
| `S/Client.Ui.State/Inventory/Equipment/EquipmentWheelSelector.cs` | 累積・循環・未確認 FIFO | 新規 |
| `S/Client.Ui.State/Inventory/Equipment/EquipmentWheelSituation.cs` | 判定の入力（画面・モーダル・UI の上・占有） | 新規 |
| `S/Client.Ui.State/Inventory/Equipment/EquipmentWheelGate.cs` | 抑止の判定（純関数）と理由の列挙 | 新規 |
| `S/Client.Ui.State/Inventory/Equipment/EquipmentWheelRouter.cs` | 判定 → 選択 | 新規 |
| `S/Client.Ui/Screens/Inventory/Inventory.uss` | 持ち物パネルと装備 HUD の USS | 新規 |
| `S/Client.Ui/Screens/Inventory/InventoryScreenRules.cs` | 出す画面とクリックを受ける画面（traits の写し） | 新規 |
| `S/Client.Ui/Screens/Inventory/Panel/InventoryView.cs`・`InventoryScreenPresenter.cs`・`InventorySlotAnchorMarks.cs` | 持ち物パネル | 新規 |
| `S/Client.Ui/Screens/Inventory/Equipment/EquipmentHudView.cs`・`EquipmentHudPresenter.cs` | 装備 HUD | 新規 |
| `S/Client.Ui/Screens/Inventory/Parts/PlayerSlotGestureRouter.cs` | `SlotGesture` → `IPlayerSlotCommands` | 新規 |
| `S/Client.Ui/Screens/Inventory/Wheel/EquipmentWheelPickJudge.cs`・`EquipmentWheelPointerPicker.cs`・`EquipmentWheelInput.cs` | ホイールの入力 | 新規 |
| `S/Client.Starter/Registration/GameUi/InventoryGameUiRegistration.cs` | 登録 | 新規 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画に 1 行 | 変更（共有区画） |
| `TSS` | `Screens` 区画に `@import` 1 行 | 変更（共有区画） |
| `THEME/DerivedTokens.uss` | 末尾に名札付きブロック「S4-inventory」 | 変更（共有区画 [A-52]） |
| `S/Client.Tests/Ui/State/Inventory/Panel/`（`PlayerInventoryPanelModelTest`・`InventoryItemAnchorTableTest`・`FakeInventoryChangeFeed`・`RecordingInventorySortCommands`） | L0 | 新規 |
| `S/Client.Tests/Ui/State/Inventory/Equipment/`（`EquipmentWheelSelectorTest`・`EquipmentWheelGateTest`・`FakeEquipmentSelectionPort`） | L0 | 新規 |
| `S/Client.Tests/Ui/Screens/Inventory/`（`InventoryViewTest`・`InventoryPanelTooltipTest`・`InventoryGridKnobTest`・`InventoryScreenVisibilityTest`・`InventoryPanelGestureTest`・`InventoryEquipmentFlowTest`・`EquipmentHudViewTest`・`EquipmentHudFloorTokenTest`・`EquipmentWheelFlowTest`・`EquipmentWheelPassthroughTest`） | L1 | 新規 |
| `S/Client.Tests/Ui/Screens/Inventory/Support/`（`RecordingPlayerSlotCommands`・`InventoryScreenFixture`） | L1 の土台 | 新規 |
| `SKILL/scenarios/uitk/uitk-inventory-equipment.cs` | 録画 | 新規 |
| `docs/uitk/disposition.md`・`docs/uitk/TODO.md` | 処分表・見た目の例外 | 変更（末尾） |

ディレクトリの本数: `Client.Ui.State/Inventory/Panel/` 7・`Equipment/` 6・`Client.Ui/Screens/Inventory/` は USS 1・`InventoryScreenRules.cs` 1＋サブ 4、`Client.Tests/Ui/Screens/Inventory/` 10＋`Support/` 2。どれも 10 以下。

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
grep -rln "TutorialAnchorCoveragePending" $S | head -3
moores-wt status
```
Expected: worktree のパス、1 行目の 7 ファイルが全部ある（無ければ S4-alpha が未マージなので止めて指揮へ返す）。`grep -n "IPlayerSlotCommands" $S/Client.Starter/Registration/MainGameUiRegistration.cs` で共有サービスの並びに登録があることも見る。2 行目（S3-hotbar-modal）は有っても無くてもよく、結果を `done.md`「証拠」に書く。`GameUi/` の `.cs` の本数を書く（このレーンの 1 本で 10 本を超えるなら Step 3 の依頼に足す）

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
- `GameUiPointerProbe` に拾った要素を返す口（`VisualElement PickAt(Vector2 screenPosition)`）がほしい。装備のホイールは「拾った要素が素通しか」を見るので bool では足りず、今は `EquipmentWheelPointerPicker` が同じ座標変換を写している（D-11）
- `Client.Starter/Registration/GameUi/` の本数（Step 1 の値。このレーンで +1）。alpha が出した再編の依頼に重ねる

- [ ] **Step 4: コミットは無し**（記録は repo 外）

---

### Task 2: ホイールでの装備の循環（`Client.Ui.State/Inventory/Equipment/`）

持ち物パネルの状態（Task 3）が装備の選択の口を読むので、こちらを先に作る。

**Files:**
- Create: `S/Client.Ui.State/Inventory/Equipment/IEquipmentSelectionPort.cs`・`LocalPlayerEquipmentSelectionPort.cs`・`EquipmentWheelSelector.cs`・`EquipmentWheelSituation.cs`・`EquipmentWheelGate.cs`・`EquipmentWheelRouter.cs`
- Test: `S/Client.Tests/Ui/State/Inventory/Equipment/FakeEquipmentSelectionPort.cs`・`EquipmentWheelSelectorTest.cs`・`EquipmentWheelGateTest.cs`

**Interfaces:**
- Consumes: `EquipmentSelectionLogic.CycleEquipment`・`AccumulateWheelSteps`・`WheelDeltaUnit`・`UiScreen`・`LocalPlayerEquipment`
- Produces:
  ```csharp
  namespace Client.Ui.State.Inventory.Equipment
  public interface IEquipmentSelectionPort { int SlotCount { get; } int SelectedIndex { get; } int ConfirmationRevision { get; } IObservable<Unit> OnSelectionChanged { get; } void Select(int index); }
  public sealed class EquipmentWheelSelector : IInitializable { public void Wheel(double delta, WheelDeltaUnit unit); }
  public readonly struct EquipmentWheelSituation { public EquipmentWheelSituation(UiScreen screen, bool modalPending, bool overBlockingUi, bool toolOwnsWheel); }
  public enum EquipmentWheelBlock { None, Layer, Ui, Tool }
  public static class EquipmentWheelGate { public static EquipmentWheelBlock Judge(EquipmentWheelSituation situation); }
  public sealed class EquipmentWheelRouter { public void Route(EquipmentWheelSituation situation, double delta, WheelDeltaUnit unit); }
  ```

ホイールの向き: Web の `deltaY` は下へ回すと正で、正が次の枠。この口の `delta` も「正が次の枠」で受ける（InputSystem の上が正の符号は Task 7 の入力が返す）。

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

`EquipmentWheelRouter.cs`:
```csharp
using Client.Ui.State.Inventory.Logic;

namespace Client.Ui.State.Inventory.Equipment
{
    public sealed class EquipmentWheelRouter
    {
        private readonly EquipmentWheelSelector _selector;

        public EquipmentWheelRouter(EquipmentWheelSelector selector)
        {
            _selector = selector;
        }

        public void Route(EquipmentWheelSituation situation, double delta, WheelDeltaUnit unit)
        {
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
- Create: `S/Client.Ui.State/Inventory/Panel/IPlayerInventoryChangeFeed.cs`・`LocalPlayerInventoryChangeFeed.cs`・`PlayerInventoryViewModel.cs`・`PlayerInventoryPanelModel.cs`・`InventoryItemAnchorTable.cs`・`IInventorySortCommands.cs`・`InventorySortCommands.cs`
- Test: `S/Client.Tests/Ui/State/Inventory/Panel/PlayerInventoryPanelModelTest.cs`・`InventoryItemAnchorTableTest.cs`・`FakeInventoryChangeFeed.cs`・`RecordingInventorySortCommands.cs`

**Interfaces:**
- Consumes: `IPlayerInventoryOperations.ReadSnapshot()`・`OwnedItemCounts.FirstSlotIndexByItemId`・`AnchorIds.InventoryItem`・`MasterHolder.ItemMaster.GetItemGuid`・Task 2 の `IEquipmentSelectionPort`（`SelectedIndex` だけ）と `FakeEquipmentSelectionPort`
- Produces:
  ```csharp
  namespace Client.Ui.State.Inventory.Panel
  public interface IPlayerInventoryChangeFeed { IObservable<Unit> OnInventoryChanged { get; } }
  public sealed class PlayerInventoryViewModel { IReadOnlyList<SlotItem> MainSlots; IReadOnlyList<SlotItem> EquipmentSlots; int SelectedEquipment; IReadOnlyDictionary<int, string> MainSlotAnchors; }
  public sealed class PlayerInventoryPanelModel : IInitializable { public IReadOnlyReactiveProperty<PlayerInventoryViewModel> Current { get; } }
  public static class InventoryItemAnchorTable { public static IReadOnlyDictionary<int, string> Build(IReadOnlyList<SlotItem> mainSlots); }
  public interface IInventorySortCommands { UiCommandResult Sort(); }
  public sealed class InventorySortCommands : IInventorySortCommands { public const string CommandName = "inventory.sort"; }
  ```

- [ ] **Step 1: 失敗するテストを書く**

`InventoryItemAnchorTableTest.cs`（L0。Web `InventoryPanel/index.test.ts` の 2 件は View に付くかを見るので Task 4 の `InventoryViewTest` に置き、ここは表の規則だけを見る。タグは付けない）:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Inventory.Panel
{
    public class InventoryItemAnchorTableTest
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
        public void 同じアイテムの先頭スロットだけに小文字guidのアンカーが付く()
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var slots = new List<SlotItem> { empty, new(_wood, 0), new(_wood, 3), new(_wood, 5) };

            var table = InventoryItemAnchorTable.Build(slots);

            // 空・個数 0 は所持に数えず、実所持の先頭（2 番）だけが名乗る
            // Empty and zero-count slots are not owned; only the first owned slot (index 2) declares it
            Assert.AreEqual(1, table.Count);
            Assert.AreEqual(AnchorIds.InventoryItem(WoodGuid.ToString()), table[2]);
            StringAssert.IsMatch("^inventory\\.item-[0-9a-f-]+$", table[2]);
        }
    }
}
```
（`ItemMaster.EmptyItemId` の綴りは実物で確かめる。無ければ `RecordingPlayerInventoryOperations` が空を作る式を写し、以後の Task の同じ箇所も揃える）

`FakeInventoryChangeFeed.cs`:
```csharp
using System;
using Client.Ui.State.Inventory.Panel;
using UniRx;

namespace Client.Tests.Ui.State.Inventory.Panel
{
    // 持ち物の変化をテストから手で流す
    // Lets a test push inventory changes by hand
    public sealed class FakeInventoryChangeFeed : IPlayerInventoryChangeFeed
    {
        private readonly Subject<Unit> _changed = new();
        public IObservable<Unit> OnInventoryChanged => _changed;

        public void Fire()
        {
            _changed.OnNext(Unit.Default);
        }
    }
}
```

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
        private FakeInventoryChangeFeed _feed;
        private FakeEquipmentSelectionPort _equipment;
        private PlayerInventoryPanelModel _model;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _operations = new RecordingPlayerInventoryOperations(36, 3);
            _feed = new FakeInventoryChangeFeed();
            _equipment = new FakeEquipmentSelectionPort(3);
            _model = new PlayerInventoryPanelModel(_operations, _feed, _equipment);
            _model.Initialize();
        }

        [Test]
        public void 起動時に今の持ち物と装備の選択を配る()
        {
            var current = _model.Current.Value;
            Assert.AreEqual(36, current.MainSlots.Count);
            Assert.AreEqual(3, current.EquipmentSlots.Count);
            Assert.AreEqual(0, current.SelectedEquipment);
        }

        [Test]
        public void 変化の通知で同期に描き直しの値を配る()
        {
            _operations.SetMain(4, new SlotItem(_wood, 7));
            _equipment.ConfirmFromServer(2);
            _feed.Fire();

            var current = _model.Current.Value;
            Assert.AreEqual(7, current.MainSlots[4].Count);
            Assert.AreEqual(2, current.SelectedEquipment);
            Assert.IsTrue(current.MainSlotAnchors.ContainsKey(4), "所持品のアンカーも同じ値で組む");
        }
    }
}
```
（`RecordingPlayerInventoryOperations` は `Client.Tests.Ui.State.Inventory` 名前空間。`using` を足す）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PlayerInventoryPanelModel` 等が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`IPlayerInventoryChangeFeed.cs`:
```csharp
using System;
using UniRx;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 持ち物か装備の中身・選択が変わったことを知らせる口（Web ホスト InventoryTopic の源と同じ）
    /// Port that signals a change in inventory or equipment contents or selection (same sources as the Web host InventoryTopic)
    /// </summary>
    public interface IPlayerInventoryChangeFeed
    {
        IObservable<Unit> OnInventoryChanged { get; }
    }
}
```

`LocalPlayerInventoryChangeFeed.cs`:
```csharp
using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using UniRx;

namespace Client.Ui.State.Inventory.Panel
{
    public sealed class LocalPlayerInventoryChangeFeed : IPlayerInventoryChangeFeed
    {
        public IObservable<Unit> OnInventoryChanged { get; private set; }

        public LocalPlayerInventoryChangeFeed(LocalPlayerInventoryController controller, LocalPlayerEquipment equipment)
        {
            // スロット単位の変化・まとめての差し替え・装備の中身と選択の 3 つを 1 本にする
            // Merge per-slot changes, bulk refreshes and equipment contents/selection into one stream
            OnInventoryChanged = Observable.Merge(
                controller.LocalPlayerInventory.OnItemChange.AsUnitObservable(),
                controller.OnInventoryRefreshed,
                equipment.OnSlotsOrSelectionChanged);
        }
    }
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
        // スロット番号 → 所持品のアンカー id
        // Slot index -> owned-item anchor id
        public IReadOnlyDictionary<int, string> MainSlotAnchors { get; }

        public PlayerInventoryViewModel(IReadOnlyList<SlotItem> mainSlots, IReadOnlyList<SlotItem> equipmentSlots, int selectedEquipment, IReadOnlyDictionary<int, string> mainSlotAnchors)
        {
            MainSlots = mainSlots;
            EquipmentSlots = equipmentSlots;
            SelectedEquipment = selectedEquipment;
            MainSlotAnchors = mainSlotAnchors;
        }
    }
}
```

`InventoryItemAnchorTable.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 「このアイテムを持っているスロット」のアンカー表（Web InventoryPanel の firstSlots と inventoryItemAnchorId）
    /// Anchor table for "the slot holding this item" (Web InventoryPanel firstSlots and inventoryItemAnchorId)
    /// </summary>
    public static class InventoryItemAnchorTable
    {
        public static IReadOnlyDictionary<int, string> Build(IReadOnlyList<SlotItem> mainSlots)
        {
            // 空と個数 0 を除いた先頭スロットにだけ付ける。C# のマスタは全アイテムが GUID を持つ
            // Only the first owned slot gets it (empty and zero-count excluded); every C# master item has a GUID
            var result = new Dictionary<int, string>();
            foreach (var pair in OwnedItemCounts.FirstSlotIndexByItemId(mainSlots))
            {
                var guid = MasterHolder.ItemMaster.GetItemGuid(pair.Key);
                result[pair.Value] = AnchorIds.InventoryItem(guid.ToString());
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
        private readonly IPlayerInventoryChangeFeed _feed;
        private readonly IEquipmentSelectionPort _equipment;
        private readonly ReactiveProperty<PlayerInventoryViewModel> _current = new();

        public IReadOnlyReactiveProperty<PlayerInventoryViewModel> Current => _current;

        public PlayerInventoryPanelModel(IPlayerInventoryOperations operations, IPlayerInventoryChangeFeed feed, IEquipmentSelectionPort equipment)
        {
            _operations = operations;
            _feed = feed;
            _equipment = equipment;
        }

        public void Initialize()
        {
            Publish();
            _feed.OnInventoryChanged.Subscribe(_ => Publish());
        }

        private void Publish()
        {
            // 掴みは C4-e の掴み絵が持つので、main と装備だけを読む
            // The grab belongs to the C4-e grab picture, so only main and equipment are read
            var snapshot = _operations.ReadSnapshot();
            var anchors = InventoryItemAnchorTable.Build(snapshot.MainSlots);
            _current.Value = new PlayerInventoryViewModel(snapshot.MainSlots, snapshot.Equipment, _equipment.SelectedIndex, anchors);
        }
    }
}
```
（`Current => _current` は書ける型を読み取り専用の型で出す変換で、前例 `PlayerGrabModel.Current` と同じ形）

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

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.Inventory.(Equipment|Panel)"`
Expected: `ErrorCount: 0`、全件 PASS（Panel 3 を含む）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Inventory/Panel moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Inventory/Panel
git commit -m "feat(uitk): 持ち物パネルの状態と整理のコマンドを足す（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 4: 持ち物の View（`Screens/Inventory/Panel/`・`Parts/`・USS）

**Files:**
- Create: `S/Client.Ui/Screens/Inventory/Parts/PlayerSlotGestureRouter.cs`・`S/Client.Ui/Screens/Inventory/Panel/InventoryView.cs`・`InventorySlotAnchorMarks.cs`・`S/Client.Ui/Screens/Inventory/Inventory.uss`
- Modify: `THEME/DerivedTokens.uss`（末尾に名札付きブロック「S4-inventory」）、`TSS`（`Screens` 区画の末尾に `@import` 1 行。L1 のテーマにも要るのでこの Task で足す）、`docs/uitk/disposition.md`（albof.29 の「写す予定」の行を消す）
- Test: `S/Client.Tests/Ui/Screens/Inventory/InventoryViewTest.cs`・`InventoryPanelTooltipTest.cs`・`InventoryGridKnobTest.cs`・`Support/RecordingPlayerSlotCommands.cs`

**Interfaces:**
- Consumes: Task 3 の `PlayerInventoryViewModel`・`IInventorySortCommands`・`InventorySortCommands.CommandName`、S4-alpha の `IPlayerSlotCommands`・`PlayerSlotCommands.CommandName`、`GamePanel`・`PanelActionButton`・`SlotGrid`・`ItemSlot`・`ItemSlotContent`・`GameContentPresentation`・`UiCommandResultToaster`・`TutorialAnchor`・`UssCustomStyleReader`
- Produces:
  ```csharp
  namespace Client.Ui.Screens.Inventory.Parts
  public sealed class PlayerSlotGestureRouter { public PlayerSlotGestureRouter(IPlayerSlotCommands commands, UiCommandResultToaster toaster); public void Route(PlayerSlotRef slot, SlotGesture gesture); }
  namespace Client.Ui.Screens.Inventory.Panel
  public sealed class InventoryView : VisualElement { public InventoryView(PlayerSlotGestureRouter gestures, IInventorySortCommands sort, GameContentPresentation content, UiCommandResultToaster toaster); public void Render(PlayerInventoryViewModel viewModel); public void SetShown(bool shown); }
  ```

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

        [Test]
        public void 隠れている間は描かず出したときに最新値で描く()
        {
            _view.SetShown(false);
            _view.Render(Model(new SlotItem(_wood, 7)));
            Assert.IsFalse(IsShown(_view));

            _view.SetShown(true);
            simulate.FrameUpdate();
            Assert.IsTrue(Find<VisualElement>(_view, "main-slot-0").Query<TextElement>().ToList().Any(text => text.text == "7"));
        }

        private PlayerInventoryViewModel Model(params SlotItem[] head)
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var main = Enumerable.Range(0, 36).Select(i => i < head.Length ? head[i] : empty).ToArray();
            return new PlayerInventoryViewModel(main, new[] { empty, empty, empty }, 0, InventoryItemAnchorTable.Build(main));
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

`InventoryPanelTooltipTest.cs`（宿主の組み方は `HoverTooltipTestStage` の前例。持ち物の View を宿主の下に入れて枠へホバーする）:
```csharp
using System;
using System.Linq;
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
        private Texture2D _texture;

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/regression/itemSlot.spec.ts">インベントリスロットがマスタGuidの辞書名をtooltip表示する</web-test>
        [Test]
        public void 持ち物の枠の上でマスタGuidの辞書名が出る()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
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
            view.Render(new PlayerInventoryViewModel(main, new[] { empty }, 0, InventoryItemAnchorTable.Build(main)));
            simulate.FrameUpdate();

            simulate.MouseMove(Find<VisualElement>(view, "main-slot-0").worldBound.center);
            simulate.FrameUpdate();

            // 期待は木材の辞書名（テスト mod の英語の辞書で "Wood"）。キーは content のアイテム名と同じ
            // Expect the wood's dictionary name ("Wood" in the test mod's English dictionary); the key matches the content item name
            var expected = content.ItemName(wood).Resolve();
            Assert.AreEqual("Wood", expected);
            StringAssert.EndsWith(expected, face.Q<Label>().text);
        }
    }
}
```
（`Assert.AreEqual("Wood", expected)` が落ちるときは、テストの辞書の言語が英語でないか、テスト mod の辞書に木材が無い。実物で確かめ、出た値を `done.md`「未確認」に書いてから期待値を辞書の値に合わせる。ホストの Attach の型は `HoverTooltipTestStage` の実物に合わせる）

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

`Panel/InventorySlotAnchorMarks.cs`（`TutorialAnchor` に外す口が無いので、付けた id を覚えて自分で外す。D-8）:
```csharp
using System.Collections.Generic;
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

        public void Apply(IReadOnlyList<ItemSlot> slots, IReadOnlyDictionary<int, string> anchors)
        {
            for (var index = 0; index < slots.Count; index++)
            {
                anchors.TryGetValue(index, out var wanted);
                _applied.TryGetValue(index, out var current);
                if (wanted == current) continue;

                // 古い id は印のクラスごと外す（枠に他のアンカーは付かない）
                // Remove the old id together with the marker class (slots carry no other anchor)
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
        private PlayerInventoryViewModel _latest;
        private bool _shown;
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
            _latest = viewModel;
            if (_shown) Draw(viewModel);
        }

        public void SetShown(bool shown)
        {
            // 隠れている間は描かず、出すときに最新値で 1 回描く（§4.2）
            // Skip drawing while hidden and draw the latest value once when shown (§4.2)
            _shown = shown;
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (shown && _latest != null) Draw(_latest);
        }

        private void Draw(PlayerInventoryViewModel viewModel)
        {
            if (_slots.Count != viewModel.MainSlots.Count) Rebuild(viewModel.MainSlots.Count);
            for (var i = 0; i < _slots.Count; i++) _slots[i].SetContent(ToContent(viewModel.MainSlots[i]));
            _anchors.Apply(_slots, viewModel.MainSlotAnchors);

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

            ItemSlotContent ToContent(SlotItem item)
            {
                // 空きマスは絵も個数も持たない（ItemSlot が空きの面で描く）
                // Empty cells carry no icon or count (ItemSlot draws the empty face)
                var icon = item.IsEmpty ? null : _content.Item(item.ItemId);
                return new ItemSlotContent(icon, item.IsEmpty ? null : item.Count, new ItemSlotMarks(false, false, false), null, null, null);
            }

            #endregion
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
- Create: `S/Client.Ui/Screens/Inventory/Equipment/EquipmentHudView.cs`・`S/Client.Ui/Screens/Inventory/InventoryScreenRules.cs`
- Modify: `S/Client.Ui/Screens/Inventory/Inventory.uss`（装備 HUD の行）
- Test: `S/Client.Tests/Ui/Screens/Inventory/EquipmentHudViewTest.cs`・`EquipmentHudFloorTokenTest.cs`

**Interfaces:**
- Consumes: Task 4 の `PlayerSlotGestureRouter`、Task 3 の `PlayerInventoryViewModel`、`AnchorIds.EquipmentHud`・`EquipmentSlot`・`EquipmentSelectedSlot`
- Produces:
  ```csharp
  namespace Client.Ui.Screens.Inventory.Equipment
  public sealed class EquipmentHudView : VisualElement { public EquipmentHudView(PlayerSlotGestureRouter gestures, GameContentPresentation content); public void Render(PlayerInventoryViewModel viewModel); public void SetShown(bool shown); public void SetClicksAccepted(bool accepted); }
  namespace Client.Ui.Screens.Inventory
  public static class InventoryScreenRules { public static bool ShowsInventoryPanel(UiScreen screen); public static bool ShowsEquipmentHud(UiScreen screen); public static bool AcceptsEquipmentClicks(UiScreen screen); }
  ```

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
using Client.Ui.Screens.Inventory;
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
            _view.SetClicksAccepted(InventoryScreenRules.AcceptsEquipmentClicks(screen));
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
（`InventoryScreenRules` はこの Task の Step 3 で作る。`UiLayerStack.Build` は `GrabOverlayFlowTest` の前例）

`EquipmentHudFloorTokenTest.cs`（床トークン: 装備の列の下端はホットバーと同じ `--hotbar-bottom`、右は `--equipment-right`、枠はホットバーと同じ大きさ。`[TestFixture]` で 720p と 2560×1080 を回す）:
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
    [TestFixture(1280f, 720f)]
    [TestFixture(2560f, 1080f)]
    public class EquipmentHudFloorTokenTest : ScreenTestHost
    {
        // Tokens.uss の --hotbar-bottom 2px・--equipment-right 24px・--hotbar-slot-size 51.68px
        // Tokens.uss --hotbar-bottom 2px, --equipment-right 24px, --hotbar-slot-size 51.68px
        private const float HotbarBottom = 2f;
        private const float EquipmentRight = 24f;
        private const float HotbarSlotSize = 51.68f;
        private readonly Vector2 _size;
        private Texture2D _texture;

        public EquipmentHudFloorTokenTest(float width, float height)
        {
            _size = new Vector2(width, height);
        }

        [OneTimeSetUp]
        public void SetPanelSize()
        {
            panelSize = _size;
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

`InventoryScreenRules.cs`（Web `App.tsx` の出し分けと `useGrabInteractive` の写し。Presenter が読む。全画面の行は Task 8 の `InventoryScreenVisibilityTest` が固定する）:
```csharp
using Client.Ui.State.Routing;

namespace Client.Ui.Screens.Inventory
{
    /// <summary>
    /// 持ち物パネルと装備 HUD を出す画面と、装備の枠がクリックを受ける画面（Web screenAllowsGrab・screenShowsAlwaysOnHud・useGrabInteractive）
    /// Screens that show the inventory panel and equipment HUD, and where equipment slots take clicks (Web screenAllowsGrab, screenShowsAlwaysOnHud, useGrabInteractive)
    /// </summary>
    public static class InventoryScreenRules
    {
        public static bool ShowsInventoryPanel(UiScreen screen)
        {
            return UiScreenTraitsTable.Get(screen).Grab;
        }

        public static bool ShowsEquipmentHud(UiScreen screen)
        {
            // blocking スキットでも隠さない（Web は常駐 HUD の列だけで決め、ホットバーと違ってスキットを見ない）
            // Not hidden during a blocking skit (the Web decides by the always-on-HUD column alone, unlike the hotbar)
            return UiScreenTraitsTable.Get(screen).AlwaysOnHud;
        }

        public static bool AcceptsEquipmentClicks(UiScreen screen)
        {
            // 掴んだ絵が見えない画面ではクリックを受けず、選択はホイールだけ
            // Where the held item cannot be seen, clicks are refused and selection is wheel-only
            return UiScreenTraitsTable.Get(screen).Grab;
        }
    }
}
```

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
        private PlayerInventoryViewModel _latest;
        private bool _shown;
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

        public void Render(PlayerInventoryViewModel viewModel)
        {
            _latest = viewModel;
            if (_shown) Draw(viewModel);
        }

        public void SetShown(bool shown)
        {
            _shown = shown;
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (shown && _latest != null) Draw(_latest);
        }

        private void Draw(PlayerInventoryViewModel viewModel)
        {
            if (_slots.Count != viewModel.EquipmentSlots.Count) Rebuild(viewModel.EquipmentSlots.Count);
            for (var i = 0; i < _slots.Count; i++)
            {
                // 選択枠はホイールで動くので、選択アンカーもその枠へ移す
                // The selected slot moves with the wheel, so the selection anchor moves with it
                var selected = i == viewModel.SelectedEquipment;
                _slots[i].SetContent(ToContent(viewModel.EquipmentSlots[i], selected));
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

            ItemSlotContent ToContent(SlotItem item, bool selected)
            {
                var icon = item.IsEmpty ? null : _content.Item(item.ItemId);
                return new ItemSlotContent(icon, item.IsEmpty ? null : item.Count, new ItemSlotMarks(selected, false, false), null, null, null);
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
- Modify（S2-tutorial がマージ済みのときだけ）: `TutorialAnchorCoveragePending` の S4-inventory 持ちの 4 行を消す

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
using Client.Tests.Ui.State.Inventory.Panel;
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
        public FakeInventoryChangeFeed Feed { get; private set; }

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

            // 移動のたびに持ち物の変化を流す（記録係は掴みの変化だけを知らせるので、それを持ち物の変化として扱う）
            // Fire an inventory change on every move (the recorder only signals grab changes, treated here as inventory changes)
            Feed = new FakeInventoryChangeFeed();
            Operations.OnGrabChanged.Subscribe(_ => Feed.Fire());
            Equipment = new FakeEquipmentSelectionPort(equipmentSlots);
            Model = new PlayerInventoryPanelModel(Operations, Feed, Equipment);
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
            _fixture.Feed.Fire();
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
            _fixture.Feed.Fire();
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
            _fixture.Feed.Fire();
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
            _fixture.Button.Press();
        }
    }
}
```
（`SetMain` は通知を出さない記録係なので、置いた後に `Feed.Fire()` で描き直しを流す。石の GUID〈…0002〉はテスト mod の実物で確かめる。`FakePrimaryPointerButton` の `Press`・`Release` の綴りは C4-e の実物に合わせる）

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
            _equipment.SetClicksAccepted(InventoryScreenRules.AcceptsEquipmentClicks(UiScreen.PlayerInventory));
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
            _fixture.Feed.Fire();
            simulate.FrameUpdate();
        }

        private void PressAndRelease(VisualElement frame)
        {
            simulate.MouseDown(frame.worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(frame.worldBound.center, MouseButton.LeftMouse);
            _fixture.Button.Release();
            _fixture.Drag.LateTick();
            _fixture.Button.Press();
            simulate.FrameUpdate();
        }
    }
}
```
（`InventoryScreenRules` は Task 5 で先に作ったもの〈`using Client.Ui.Screens.Inventory;`〉）

- [ ] **Step 3: S2-tutorial の保留の行を消す（マージ済みのときだけ）**

Run: `grep -rn "S4-inventory" $(grep -rln "TutorialAnchorCoveragePending" moorestech_client/Assets/Scripts)`
Expected: 4 行（`EquipmentHud`・`EquipmentSelectedSlot`・`EquipmentSlotPrefix`・`InventoryItemPrefix`）。あれば消し、S2-tutorial のアンカーの網羅テスト（同じファイルを読むテスト）を回して緑を確かめる。無ければ（S2-tutorial が未マージ）何もせず `done.md`「未確認」に「S2-tutorial の保留の行は S2-tutorial 側のマージ時に、このレーンのアンカーで解ける」と書く

- [ ] **Step 4: 緑を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Inventory|Client.Tests.Ui.Screens.GrabOverlay|Client.Tests.Ui.Tutorial"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Inventory moorestech_client/Assets/Scripts/Client.Ui.State moorestech_client/Assets/Scripts/Client.Tests/Ui
git commit -m "test(uitk): 持ち物と装備の身振りを本物のスロット操作で通す（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 7: ホイールの入力と流れ（`Screens/Inventory/Wheel/`）

**Files:**
- Create: `S/Client.Ui/Screens/Inventory/Wheel/EquipmentWheelPickJudge.cs`・`EquipmentWheelPointerPicker.cs`・`EquipmentWheelInput.cs`
- Test: `S/Client.Tests/Ui/Screens/Inventory/EquipmentWheelFlowTest.cs`、`InventoryEquipmentFlowTest.cs` に 1 件足す

**Interfaces:**
- Consumes: Task 2 の `EquipmentWheelRouter`・`EquipmentWheelSituation`、`UiRoutingState`・`ModalRequestService`・`PlaceSystemStateController`・`GameUiRoot`、`CrosshairHudView`（S2-hud の実物）
- Produces:
  ```csharp
  namespace Client.Ui.Screens.Inventory.Wheel
  public static class EquipmentWheelPickJudge { public static bool IsBlockingUi(VisualElement picked); }   // Task 9 で素通しの印を足す
  public sealed class EquipmentWheelPointerPicker { public EquipmentWheelPointerPicker(GameUiRoot root); public VisualElement PickAt(Vector2 screenPosition); }
  public sealed class EquipmentWheelInput : ITickable
  ```

- [ ] **Step 1: 失敗するテストを書く**

`EquipmentWheelFlowTest.cs`（拾いはテストのパネルの `panel.Pick` で、本番の `EquipmentWheelInput` と同じ判定 `EquipmentWheelPickJudge` を通す）:
```csharp
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.CommonHud;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Routing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class EquipmentWheelFlowTest : ScreenTestHost
    {
        private FakeEquipmentSelectionPort _port;
        private EquipmentWheelRouter _router;
        private VisualElement _viewport;

        [SetUp]
        public void SetUp()
        {
            _port = new FakeEquipmentSelectionPort(3);
            var selector = new EquipmentWheelSelector(_port);
            selector.Initialize();
            _router = new EquipmentWheelRouter(selector);
            _viewport = UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Chrome, UiFamily.Viewport);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">小さいホイール入力を累積し閾値を越えた時だけ切り替える</web-test>
        [Test]
        public void 小さいホイールは累積して閾値を越えたときだけ切り替える()
        {
            WheelAt(UiScreen.None, new Vector2(5, 5), 0.4);
            Assert.IsEmpty(_port.Selections);
            WheelAt(UiScreen.None, new Vector2(5, 5), 0.7);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">ホイールは末尾スロットの次に先頭スロットへ回る</web-test>
        [Test]
        public void 末尾の次は先頭へ回る()
        {
            for (var step = 1; step < 3; step++)
            {
                WheelAt(UiScreen.None, new Vector2(5, 5), 1);
                _port.ConfirmFromServer(step);
            }
            WheelAt(UiScreen.None, new Vector2(5, 5), 1);
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

            WheelAt(UiScreen.None, crosshair.Q("crosshair").worldBound.center, 1);
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

            for (var step = 0; step < 3; step++) WheelAt(UiScreen.PlayerInventory, list.worldBound.center, 1);
            WheelAt(UiScreen.PlayerInventory, new Vector2(1, 1), 1);

            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/regression/inputLayer.spec.ts">GameScreenのホイールは最新equipment値から次スロットを選ぶ</web-test>
        [Test]
        public void ゲーム画面のホイールは最新の装備の値から次を選ぶ()
        {
            // サーバーが選択を 2 にした後のホイールは 2 の次（0）
            // After the server sets the selection to 2, the wheel picks the one after 2 (0)
            _port.ConfirmFromServer(2);
            WheelAt(UiScreen.None, new Vector2(5, 5), 1);
            CollectionAssert.AreEqual(new[] { 0 }, _port.Selections);
        }

        private void WheelAt(UiScreen screen, Vector2 panelPosition, double delta)
        {
            var picked = rootVisualElement.panel.Pick(panelPosition);
            _router.Route(new EquipmentWheelSituation(screen, false, EquipmentWheelPickJudge.IsBlockingUi(picked), false), delta, WheelDeltaUnit.Page);
        }
    }
}
```
（`UiLayerStack` の層の器が拾うかは実物で確かめる。器が `PickingMode.Position` で何も無い所でも器が拾われるなら、`IsBlockingUi` は「層の器そのもの」を UI としない形が要る。そのときは `UiLayerStack` の器のクラス名〈実物〉を `IsBlockingUi` で除き、D-11 に追記する）

`InventoryEquipmentFlowTest.cs` に足す:
```csharp
        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">装備へ移したアイテムをホイールで選択できる</web-test>
        [Test]
        public void 装備へ移したアイテムをホイールで選べる()
        {
            MoveMainOneToEquipmentOne();
            var selector = new EquipmentWheelSelector(_fixture.Equipment);
            selector.Initialize();

            new EquipmentWheelRouter(selector).Route(new EquipmentWheelSituation(UiScreen.None, false, false, false), 1, WheelDeltaUnit.Page);
            Republish();

            Assert.IsTrue(Find<VisualElement>(_equipment, "equipment-slot-1").ClassListContains("slot-frame--selected"));
        }
```

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `EquipmentWheelPickJudge` が無いコンパイルエラー

- [ ] **Step 3: 実装する**

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

`Wheel/EquipmentWheelPointerPicker.cs`（座標変換は `GameUiPointerProbe` と同じ。要素を返す口が無いので写す。D-11）:
```csharp
using Client.Ui.Foundation.Root;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Wheel
{
    public sealed class EquipmentWheelPointerPicker
    {
        private readonly GameUiRoot _root;

        public EquipmentWheelPointerPicker(GameUiRoot root)
        {
            _root = root;
        }

        public VisualElement PickAt(Vector2 screenPosition)
        {
            // 画面座標（左下原点）→ パネル座標（左上原点）。GameUiPointerProbe と同じ変換
            // Screen (bottom-left origin) to panel (top-left origin); same conversion as GameUiPointerProbe
            var panel = _root.GetRootElement().panel;
            var topLeft = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, topLeft));
        }
    }
}
```

`Wheel/EquipmentWheelInput.cs`:
```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Modal;
using Client.Ui.State.Routing;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// マウスのホイールを読み、層・UI の上・ツールの占有を添えて装備のルーターへ渡す（Web useGameLayerWheel の C# 側）
    /// Reads the mouse wheel and hands it to the equipment router with layer, over-UI and tool ownership (C# side of Web useGameLayerWheel)
    /// </summary>
    public sealed class EquipmentWheelInput : ITickable
    {
        private readonly EquipmentWheelRouter _router;
        private readonly EquipmentWheelPointerPicker _picker;
        private readonly UiRoutingState _routing;
        private readonly ModalRequestService _modal;
        private readonly PlaceSystemStateController _placement;

        public EquipmentWheelInput(EquipmentWheelRouter router, EquipmentWheelPointerPicker picker, UiRoutingState routing, ModalRequestService modal, PlaceSystemStateController placement)
        {
            _router = router;
            _picker = picker;
            _routing = routing;
            _modal = modal;
            _placement = placement;
        }

        public void Tick()
        {
            // silent-ok: マウスが無ければホイールも無い（入力機器の有無は外部） / 録画 uitk-inventory-equipment が見る
            var mouse = Mouse.current;
            if (mouse == null) return;
            var notches = mouse.scroll.ReadValue().y;
            if (notches == 0f) return;

            // InputSystem は上が正、Web の deltaY は下が正で次の枠。1 ノッチ＝1 段なので Page 単位で渡す
            // InputSystem is positive upward while the Web deltaY is positive downward (next slot); one notch is one step, so pass Page units
            var picked = Cursor.lockState == CursorLockMode.Locked ? null : _picker.PickAt(mouse.position.ReadValue());
            var situation = new EquipmentWheelSituation(_routing.Current.Value.Screen, _modal.Pending != null, EquipmentWheelPickJudge.IsBlockingUi(picked), _placement.IsWheelOwnedByTool);
            _router.Route(situation, -notches, WheelDeltaUnit.Page);
        }
    }
}
```
（`Mouse.current.scroll` の値が 1 ノッチ＝1 であることは `SemanticInput.MouseWheel` の注記〈実マウスと同じ正規化済みの値〉による。実機で 120 等が来るなら単位を `Pixel` 等に替えず、Task 11 の録画と手元の実機で値を測って `done.md`「未確認」に書き、D-10 を直す）

- [ ] **Step 4: 緑を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Inventory|Client.Tests.Ui.State.Inventory"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Inventory/Wheel moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Inventory
git commit -m "feat(uitk): ホイールの入力を装備のルーターへ繋ぐ（S4-inventory）

<実行ホストの指示する帰属行>"
```

---

### Task 8: Presenter・登録・出し分け・処分表

**Files:**
- Create: `S/Client.Ui/Screens/Inventory/Panel/InventoryScreenPresenter.cs`・`S/Client.Ui/Screens/Inventory/Equipment/EquipmentHudPresenter.cs`・`S/Client.Starter/Registration/GameUi/InventoryGameUiRegistration.cs`
- Modify: `S/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 1 行）、`docs/uitk/disposition.md`（末尾に処分の 2 行）
- Test: `S/Client.Tests/Ui/Screens/Inventory/InventoryScreenVisibilityTest.cs`

**Interfaces:**
- Consumes: Task 2〜7 の型、`StageColumnsLayout`・`GameUiRoot`・`UiRoutingState`、S4-alpha の `IPlayerSlotCommands`（`MainGameUiRegistration` の共有サービスの並びに alpha が登録）、`InventoryScreenRules`（Task 5）
- Produces:
  ```csharp
  namespace Client.Ui.Screens.Inventory.Panel
  public sealed class InventoryScreenPresenter : IInitializable, IDisposable
  namespace Client.Ui.Screens.Inventory.Equipment
  public sealed class EquipmentHudPresenter : IInitializable, IDisposable
  namespace Client.Starter.Registration.GameUi
  internal static class InventoryGameUiRegistration { public static void Register(ContainerBuilder builder); }
  ```

- [ ] **Step 1: 出し分けのテストを書く**

`InventoryScreenVisibilityTest.cs`（Web `App.tsx` の `screenAllowsGrab(screen) && <InventoryPanel/>`・`screenShowsAlwaysOnHud(screen) && <EquipmentPanel/>`・`useGrabInteractive` を、`UiScreen` の全行で固定する）:
```csharp
using System;
using Client.Ui.Screens.Inventory;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryScreenVisibilityTest
    {
        [Test]
        public void 全画面で持ち物は掴める画面だけ装備HUDは常駐HUDの画面だけに出る()
        {
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            {
                var traits = UiScreenTraitsTable.Get(screen);
                Assert.AreEqual(traits.Grab, InventoryScreenRules.ShowsInventoryPanel(screen), $"{screen} panel");
                Assert.AreEqual(traits.AlwaysOnHud, InventoryScreenRules.ShowsEquipmentHud(screen), $"{screen} hud");
                Assert.AreEqual(traits.Grab, InventoryScreenRules.AcceptsEquipmentClicks(screen), $"{screen} clicks");
            }

            // Web の代表の行（持ち物は持ち物画面・研究で出て、建築メニューでは出ない。装備 HUD は研究で隠れる）
            // Representative Web rows (the panel shows on inventory and research, not build menu; the HUD hides on research)
            Assert.IsTrue(InventoryScreenRules.ShowsInventoryPanel(UiScreen.PlayerInventory));
            Assert.IsTrue(InventoryScreenRules.ShowsInventoryPanel(UiScreen.ResearchTree));
            Assert.IsFalse(InventoryScreenRules.ShowsInventoryPanel(UiScreen.BuildMenu));
            Assert.IsFalse(InventoryScreenRules.ShowsEquipmentHud(UiScreen.ResearchTree));
            Assert.IsTrue(InventoryScreenRules.ShowsEquipmentHud(UiScreen.BuildMenu));
        }
    }
}
```

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
            _routingSubscription = _routing.Current.Subscribe(snapshot => Apply(InventoryScreenRules.ShowsInventoryPanel(snapshot.Screen)));
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
            _routingSubscription = _routing.Current.Subscribe(snapshot => Apply(snapshot.Screen));
        }

        public void Dispose()
        {
            _routingSubscription?.Dispose();
            _modelSubscription?.Dispose();
            _view.RemoveFromHierarchy();
        }

        private void Apply(UiScreen screen)
        {
            var shown = InventoryScreenRules.ShowsEquipmentHud(screen);
            _view.SetClicksAccepted(InventoryScreenRules.AcceptsEquipmentClicks(screen));
            _view.SetShown(shown);
            if (shown && _modelSubscription == null) _modelSubscription = _model.Current.Subscribe(_view.Render);
            if (shown || _modelSubscription == null) return;
            _modelSubscription.Dispose();
            _modelSubscription = null;
        }
    }
}
```

`Client.Starter/Registration/GameUi/InventoryGameUiRegistration.cs`（Task 1 Step 1 で再編済みならその置き場へ）:
```csharp
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Panel;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // 持ち物パネル・装備 HUD・ホイールでの装備の循環（GameUi）の登録。プレイヤーの枠のコマンドは共有サービスの並び（MainGameUiRegistration）にある
    // Registers the inventory panel, equipment HUD and wheel equipment cycling (GameUi); player-slot commands live in the shared-service list (MainGameUiRegistration)
    internal static class InventoryGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<LocalPlayerInventoryChangeFeed>(Lifetime.Singleton).As<IPlayerInventoryChangeFeed>();
            builder.Register<LocalPlayerEquipmentSelectionPort>(Lifetime.Singleton).As<IEquipmentSelectionPort>();
            builder.RegisterEntryPoint<PlayerInventoryPanelModel>().AsSelf();
            builder.Register<InventorySortCommands>(Lifetime.Singleton).As<IInventorySortCommands>();
            builder.RegisterEntryPoint<EquipmentWheelSelector>().AsSelf();
            builder.Register<EquipmentWheelRouter>(Lifetime.Singleton);
            builder.Register<EquipmentWheelPointerPicker>(Lifetime.Singleton);
            builder.RegisterEntryPoint<EquipmentWheelInput>();
            builder.RegisterEntryPoint<InventoryScreenPresenter>();
            builder.RegisterEntryPoint<EquipmentHudPresenter>();
        }
    }
}
```

`MainGameUiRegistration.cs` の画面登録区画の末尾（他レーンの行の後）:
```csharp
            InventoryGameUiRegistration.Register(builder);
```

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

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Inventory|Client.Tests.Ui.State.Inventory|Client.Tests.Ui.Conventions|Client.Tests.Ui.Theme|Client.Tests.Ui.Screens.GrabOverlay"` → 全体スイッチ UITK で PlayMode を起動し `uloop get-logs --project-path ./moorestech_client --log-type Error`
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
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Routing;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class EquipmentWheelPassthroughTest : ScreenTestHost
    {
        private FakeEquipmentSelectionPort _port;
        private EquipmentWheelRouter _router;
        private VisualElement _viewport;
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _port = new FakeEquipmentSelectionPort(3);
            var selector = new EquipmentWheelSelector(_port);
            selector.Initialize();
            _router = new EquipmentWheelRouter(selector);
            _viewport = UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Chrome, UiFamily.Viewport);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
        }

        [TearDown]
        public void TearDown()
        {
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
            _router.Route(new EquipmentWheelSituation(UiScreen.None, false, EquipmentWheelPickJudge.IsBlockingUi(picked), false), 1, WheelDeltaUnit.Page);
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

`Inventory.uss` の `.equipment-hud` に、Step 1 で控えた `Hotbar.uss` の画面ルートの上書きと同じ行を足す（計画の写しでは次の 1 行。実物に他の行があれば全部写す）:
```css
    /* 世界の上の枠の面はホットバーと同じ前計算の値（S3-hotbar-modal R-6・bd albof.34。パネルの地向けの LX-23 を世界の地で使わない） */
    /* Slot faces over the world use the same precomputed values as the hotbar (S3-hotbar-modal R-6, bd albof.34; panel-ground LX-23 is not for the world) */
    --slot-frame-face: var(--hotbar-slot-frame-face);
```

`docs/uitk/disposition.md` の albof.31 の行（`| 2 | \`src/features/commonHud/alwaysOnHudWheel.test.ts\` | 装備HUDは実UIのままホイールだけ素通す | 写す予定 | moorestech-albof.31…`）を消す（タグを足すこのコミットで消さないと parity が二重に数える）

- [ ] **Step 4: 緑と対応を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Inventory|Client.Tests.Ui.Screens.Hotbar|Client.Tests.Ui.State.Inventory"` → `node tools/uitk/test-name-parity.mjs --bundle 4` → `node tools/uitk/test-name-parity.mjs --bundle 2`
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
1. `SetupFlatGround` → main の 0 番へ木材 10 を `GiveItem`、装備の枠が 2 以上あることを `LocalPlayerEquipment.Slots.Count` で確かめる
2. 持ち物を開く（`WaitUiState(UIStateEnum.PlayerInventory)`）→ `inventory-panel` が Inv 列に出る・`main-slot-0` に 10（`Screenshot`）
3. `inventory-sort` を押す → Error ログが増えない
4. `main-slot-0` を左押下 → 掴みに 10 → `equipment-slot-1` を左押下・解放 → 装備の 1 番に 10、掴みが空
5. `equipment-slot-1` を左押下 → 掴みに移る（選択は変わらない）→ 元へ戻す
6. 持ち物を閉じてゲーム画面へ → `SemanticInput.MouseWheel(-1)`（下へ 1 ノッチ）→ `LocalPlayerEquipment.SelectedIndex` が 1 進む → もう 1 ノッチで次へ（末尾の次は先頭）
7. 持ち物を開き、パネルの上へカーソルを置いて `MouseWheel(-1)` → 選択が変わらない → 装備 HUD の上で `MouseWheel(-1)` → 1 進む（Task 9 の後）
8. S4-alpha から渡された完了条件（alpha D-33）: `PlaceBlockDirect("木のチェスト", pos, BlockDirection.North)` → `WaitBlockGameObject` → main の 2 番へ木材 5 を `GiveItem` → `PressInteract` → `WaitUiState(UIStateEnum.SubInventory)`（持ち物パネルも Inv 列に出ている）→ `main-slot-2` を Shift＋左押下 → main の 2 番が空になり、チェストの枠に木材 5 が入る（`Screenshot`）。押下は alpha の `uitk/uitk-block-inventory` の Shift 押下の書き方を使う
9. 途中で `AssertNoErrorLogs` 相当が 0 件

押下は前例 `uitk-grab-overlay.cs` の UITK 要素の押下の書き方を使う。InputSystem の注入だけを使い OS の入力は使わない

- [ ] **Step 2: 回す**

Run: unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh uitk/uitk-inventory-equipment` と `uitk/uitk-grab-overlay`
Expected: 2 本の `result.json` が PASS、mp4 がある。`done.md`「証拠」に result.json のパス・mp4・`UiInputExclusivity.ActiveUiSystem` の値（GameUi）、6 のホイール 1 ノッチで進んだ段の数（1 であること。D-10 の実測）を書く。生の録画は worktree の gitignore 下に置き、`LOGS` へはスクショ 3 枚までと報告だけ

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

Run: `uloop compile --project-path ./moorestech_client` → Task 8 Step 5 と同じテストの絞り込み（Task 9 の後なので `Client.Tests.Ui.Screens.Hotbar` も足す）
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

- Requirements はそれぞれ Task に当たる: R-1・R-2（表）→ Task 3、R-2（View）・R-3 → Task 4、R-4 → Task 5（規則）・Task 8（Presenter）、R-5 → Task 4〜6、R-6 → Task 5、R-7・R-8 → Task 2・Task 7、R-9 → Task 7・Task 11、R-10・R-11 → Task 9、R-12 → Task 4、R-13 → Task 10、R-14 → Task 4・Task 8、R-15 → Task 2〜9、R-16 → Task 11、R-17 → Task 12・13
- 写すテスト 34 件の所在: Task 2（Selector 4・Gate 4）・Task 4（inventory.spec 1・InventoryPanel 2・itemSlot 1）・Task 5（EquipmentPanel 4・equipment.spec 1）・Task 6（inventory.spec 3・equipment.spec 2）・Task 7（equipment.spec 5・inputLayer 1）・Task 8（処分 2）・Task 9（equipment.spec 3・alwaysOnHudWheel 1）＝ 8＋4＋5＋5＋6＋2＋4 ＝ 34
- 型の一致: `PlayerInventoryViewModel`（Task 3）を Task 4・5・8・9 が同じ ctor（main・装備・選択・アンカー表）で使う。`EquipmentWheelSituation` の引数順（screen, modalPending, overBlockingUi, toolOwnsWheel）は Task 2・7・9 で同じ。`PlayerSlotGestureRouter(IPlayerSlotCommands, UiCommandResultToaster)` は Task 4・5・8・9 で同じ
- プレースホルダ: 「実物で確かめる」と書いた箇所は、起草時に実物が無い型（S4-alpha のテスト用の部品・S3-hotbar-modal の型）と、実物の綴りを確かめていない小物（`ItemMaster.EmptyItemId`・石の GUID・`FakePrimaryPointerButton.Press`・`CssCalcEvaluator` の可視性）だけ。挙動の空白は無い

---

## 判断記録

| ID | 判断 | 理由 | 却下した案 |
|---|---|---|---|
| D-1 | Presenter は 2 本。持ち物は `InventoryScreenPresenter`（`StageColumnsLayout.Get(StageColumn.Inv)` へ。A-92）、装備 HUD は `EquipmentHudPresenter`（Chrome 層・Viewport。HUD の前例 `CrosshairHudPresenter`・`ProgressHudPresenter` の名前の付け方）。どちらも `UiRoutingState` を購読して出し分け、表示中だけ Model を購読する（§4.2・§4.4） | Web も別の場所（舞台の grid の inv と `.viewportOverlay`）に別の条件で出す。出し分けの条件が違う 2 つを 1 本の Presenter に入れると判断が Presenter に入る | 1 本の Presenter で両方: 条件が 2 系統になり §4.2「Presenter は判断を持たない」に反する |
| D-2 | 持ち物パネルと装備 HUD は同じ `PlayerInventoryPanelModel` の値を読む | Web も同じ `inventory` topic（mainSlots・equipment・selectedEquipment）を 2 つが読む | 装備だけの Model を別に作る: 源が同じで 2 重の購読と 2 重の組み立てになる |
| D-3 | Model は変化のたびに同期で配る（Web の PostLateUpdate への間引きをしない） | A-57: 同じフレームの描き直しは UITK が 1 回の描画にまとめ、Web の間引きは WebSocket の送り過ぎを抑えるためのもの | フレーム末に寄せる: 理由が UITK に無い |
| D-4 | Web の `ConnectingPlaceholder`（topic 未着の仮の面）は写さない | C# の持ち物は起動時から在り、未着の状態が無い（A-13 と同じ理由） | 空の面を写す: 出る場面が無い死んだコードになる |
| D-5 | `EquipmentPanel/index.test.ts` の「action失敗時は…」「再接続開始時に…」を処分表へ書き、残り 12 件を写す。世代の巻き戻りで未確認を捨てる規則は `TrimConfirmed` に残す | C# の選択は `SendOnly` で失敗が返らず、UITK に WebSocket の再接続が無い。世代の巻き戻りは C# では起きないが、Web の規則を外す理由も無く、1 行で済む | 失敗の経路を作って写す: 存在しない失敗のためにコマンドに戻り値と待ちを足すことになる |
| D-6 | `equipment.spec`「持ち物画面の一覧スクロールでは装備が持ち替わらない」は S4-inventory が持ち、一覧（S4-recipe の `ItemListPanel`）の代わりに素通しの印の無い `GameScrollView` を立てて L1 で写す。S4-recipe の一覧は `WheelPassthroughMark` を付けない（付けないのが既定なので S4-recipe の作業は無い）。順序は独立で、どちらが先にマージしてもよい。S4-recipe の計画へは「一覧に素通しの印を付けない」1 行の申し送りだけを出す | テストの主題は「素通しでない UI の上のホイールは装備に回さない」で、判定はこのレーンの `EquipmentWheelGate` と `EquipmentWheelPickJudge` にある。一覧の実物を待つと S4-recipe → S4-inventory の辺が増える | S4-recipe が写す: 判定の持ち主でないレーンが装備の切り替えを確かめることになり、判定を変えたときに別レーンのテストが壊れる。S4-recipe を待つ: クリティカルパスの S4-inventory が W4b を待つ |
| D-7 | `EquipmentPanel/index.test.ts` の「接続ツール」「BPコピー」「block選択」の 3 件は、同じ入力（占有＝偽）の 1 件に 3 行のタグで写す | Web のテストの主題は「topic の `wheelOwnedByTool` をそのまま読み、配置対象の種別から導き直さない」。C# の判定は bool 1 つを読むだけで、種別ごとに占有を決めるのは各配置システム（このレーンの外）。3 件を別々に書いても入力が同じになる | 配置システムを組んで種別ごとに占有を作る: このレーンの判定でなく配置システムの挙動を確かめることになる |
| D-8 | 所持品のアンカーは描き直しのたびに `InventorySlotAnchorMarks` が付け替え、古い id と印のクラスを自分で外す。`TutorialAnchor.Unmark` を integrator へ依頼する | `TutorialAnchor` に外す口が無い。Foundation はこのレーンで変えない（スコープ境界） | Foundation に `Unmark` を足す: 部品・Foundation を画面レーンが変えることになる。枠を毎回作り直す: スロットの購読と見た目の状態が描き直しのたびに捨てられる |
| D-9 | ホイールの循環は State の `EquipmentWheelSelector` 1 か所に置き、Web と同じ未確認 FIFO を持つ。口 `IEquipmentSelectionPort` の本番は `LocalPlayerEquipment` | C# の `SetSelectedIndex` は楽観で選択を変えるが、`ApplySelected` のエコーは古い値で巻き戻す。速い 2 ノッチの間に古いエコーが来ると、FIFO 無しでは選択が戻る（Web と同じ理由）。§1.4「ホイール循環を C# 1 か所へ」 | `LocalPlayerEquipment.SelectedIndex` だけを起点にする: 古いエコーで巻き戻る |
| D-10 | 入力は `Mouse.current.scroll.y`（1 ノッチ＝1。上が正）を符号反転し `WheelDeltaUnit.Page` で渡す。カーソルがロック中は UI の上でない | `SemanticInput.MouseWheel` の注記と I-fix1 A-71 が「InputSystem の scroll は正規化済みのノッチ」としている。Page の 1 段＝1 で Web の 100px＝1 段と同じ量になる。ロック中はカーソルが画面中央に固定され、Web でもクロスヘアの上（拾わない）と同じ扱い。実機の値は Task 11 で測る | Pixel で 100 倍して渡す: 単位の意味が嘘になる。`InputManager.UI.SwitchHotBar`（未使用のホイールの行動）を使う: 意味の違う名前の行動を流用することになる |
| D-11 | 拾いは `EquipmentWheelPointerPicker` が `GameUiPointerProbe` と同じ座標変換で `panel.Pick` を呼んで要素を返す。integrator へ `PickAt` を依頼する | 判定に要素（素通しの印の祖先）が要るが、`GameUiPointerProbe` は bool しか返さない。Foundation はこのレーンで変えない | `UiInputExclusivity.IsPointerOverGameUi` を使う: 素通しを見られず、装備 HUD の上で止まる |
| D-12 | 持ち物のグリッドの面の内側の色 rgb(50 52 67) は `THEME/DerivedTokens.uss` の名札付きブロック「S4-inventory」のトークン `--inventory-slot-face-inset-color` にする | `UiStyleConventionTest` が画面 USS の色の直書きを拒む。名札付きブロックは A-52 の共有区画で、前例 S3-hotbar-modal Task 5 | 既存の同色のトークン（`--slot-frame-selected-trim-face`）を借りる: 意味の違う名前を読むことになり、片方を変えるともう片方が壊れる |
| D-13 | S3-hotbar-modal の型（`WheelPassthroughMark`・`HotbarHudView`・`--hotbar-slot-frame-face`）を使う作業は Task 9 に寄せ、Task 7 の判定は印を見ない形で先に作る | 起草時に S3-hotbar-modal が未マージ。依存の辺は E-23 にあり、先の Task を止めずに済む | 全部を S3-hotbar-modal のマージ後に始める: クリティカルパスの S4-inventory が W2 の画面レーンを待つ |
| D-14 | 装備の縦の間は 2 枠目からの上の余白（クラス `equipment-hud__slot--after-first`）で写す | USS に `gap` が無い（`SlotGrid` も余白で写している） | 枠の下に余白: 最後の枠の下に余白が残り、床の位置が変わる |
| D-15 | 整理は `IInventorySortCommands.Sort()` が `LocalPlayerInventoryController.SortInventory()` を呼び、いつも受理を返す | Web の `inventory.sort` も手元で断らない。断る条件が無いのに拒否理由の enum を足さない | 拒否理由を足す: 使われない値が `UiCommandRejectReason` と `PauseMenuCommandsTest` の表に増える |
| D-16 | 枠の身振りの振り分け `PlayerSlotGestureRouter` は `Screens/Inventory/Parts/` に置き、持ち物と装備の 2 つの View が使う | 同じ画面の中の 2 か所で、Components へ上げる 2 機能目ではない（§5.1）。S4-alpha の Produces の振り分けの規則をそのまま写す | View ごとに switch を書く: 同じ振り分けが 2 か所になる |
| D-17 | 「ブロックを開いた状態で main の枠を Shift＋左押下するとブロックへ配分される」を UITK の持ち物画面から録画で確かめる（Task 11 の 8）。L1 は足さない | S4-alpha D-33 がこの確認を S4-inventory の完了条件として渡した。計画と実行は alpha の `PlayerSlotCommandsTest`（L0）が持ち、このレーンが足すのは持ち物の枠から UI で押せることだけで、それは本物の画面・DI・入力を通す録画でしか見られない | L1 で `PlayerSlotCommands` をブロックが開いた文脈で組む: alpha の L0 と同じ主張の繰り返しで、UI の結線（Presenter・DI）を通らない |

---

## 設計書・実行計画へ反映する行

設計書 `docs/uitk-migration/execution/component-architecture.md` §7.1 に足す行（A-145〜A-149 のうち 4 つを使う）:
- A-145: 装備のホイールでの循環は State の `EquipmentWheelSelector`（`Client.Ui.State/Inventory/Equipment/`）1 か所。Web と同じ未確認 FIFO を持ち、確定の世代（`LocalPlayerEquipment.SelectionConfirmationRevision`）が進んだ数だけ頭を捨てる。抑止は純関数 `EquipmentWheelGate`（モーダル・`SubInventory`・`ResearchTree`・`BuildMenu`／素通しでない UI の上／ツールの占有の順）。入力は Client.Ui の `EquipmentWheelInput` が `Mouse.current.scroll` を 1 ノッチ＝1 段で渡す
- A-146: 装備 HUD は `AlwaysOnHud` の画面で出し（blocking スキットで隠さない）、クリックは `Grab` の画面でだけ受ける。HUD は拾う実 UI で、`WheelPassthroughMark` でホイールだけ素通す。持ち物パネルは `Grab` の画面で `StageColumn.Inv` に出す
- A-147: 持ち物の所持品のアンカー（`inventory.item-<guid>`）は State の `InventoryItemAnchorTable` が表にし、View が描き直しのたびに付け替える。`TutorialAnchor.Unmark` が入るまで画面側で外す
- A-148: `equipment.spec`「持ち物画面の一覧スクロールでは装備が持ち替わらない」は判定の持ち主の S4-inventory が素通しでないスクロール器の立て役で写す。S4-recipe の一覧は素通しの印を付けない

実行計画 `docs/uitk-migration/execution/execution-plan.md`:
- §1.4 の S4-inventory 行の件数を実物に直す: 「写す: inventory 61（P20・C16・L25 は L0-s4a 済み）」→「写す: inventory 62 件（P20・C16・L26。L26 は L0-s4a 19 件と L0-s4b の `blockSlotRestriction` 7 件が写し済み、P のうち掴み絵の 5 件は C4-e、`blockInventory*` は S4-alpha）のうちこのレーンの 31 件、束の外 3 件を足した 34 件（32 件を写し 2 件を処分表へ。S4-inventory 計画 D-5）」
- DAG の辺 S3-hotbar-modal → S4-inventory（`WheelPassthroughMark`）は 9b2f7ee3ea で §1.4 の依存の列・mermaid（`S3h -->|WheelPassthroughMark E-23| Inv`）・E-23 に反映済み。この計画では足す行が無い（確かめた）
- S4-recipe の行へ申し送り 1 行: 「一覧（`ItemListPanel`）に `WheelPassthroughMark` を付けない（装備のホイールの抑止は S4-inventory が素通しでない UI の上で止める。S4-inventory 計画 D-6）」

---

## 起草メモ（本体向け）

- **件数の差（§1.4 との差）**: inventory の実物は 62 件（P20・C16・L26）で、§1.4 の「61・L25」と 1 件違う。`slotActions.test.ts` の複数行の `it.each` を 1 件と数えると L26 になる（L0-s4a 9 件）。また L26 には L0-s4b の `blockSlotRestriction` 7 件が入っていて、§1.4 の「L25 は L0-s4a 済み」は L0-s4b の分を落としている。このレーンの持ち分は 34 件（inventory 31・束の外 3）で、32 件を写し 2 件を処分表へ（D-5）。反映の行は上の節
- **S4-alpha の Produces と違う形は要らない（改訂版 85f905aa3d と照合済み）**: 使うのは `IPlayerSlotCommands` の 5 つの口と `PlayerSlotCommands.CommandName` だけで、型名・名前空間（`Client.Ui.State.SlotCommands.Player`）は改訂後も同じ。振り分け規則（LeftDown→LeftDown(slot, gesture.Shift) 等）はそのまま `PlayerSlotGestureRouter` に写した。登録先は改訂どおり `MainGameUiRegistration` の共有サービスの並び（このレーンは登録しない）。`IUiStateTransitionRequester` は使わない（閉じる・遷移の操作が無い）。L1 の `InventoryScreenFixture` の ctor と `using` は改訂版の `PlayerSlotCommandsTest` の SetUp に合わせた。alpha D-33 で渡された「main の枠の Shift→ブロック」の録画は Task 11 の 8 と D-17 で引き受けた
- **申告（Foundation の口。勝手に作らず integrator へ）**: (1) `TutorialAnchor.Unmark`（D-8）、(2) `GameUiPointerProbe` の要素を返す口 `PickAt`（D-11）。どちらも無くても動く形で書いた（画面側で外す・座標変換を写す）
- **登録ディレクトリの本数**: `Client.Starter/Registration/GameUi/` は起草時 8 本、alpha で 9 本、このレーンで 10 本、S3-hotbar-modal が先なら 11 本。alpha が出した再編の依頼に重ねる（Task 1 Step 3）。再編が入らず 10 を超えるなら Task 8 の登録だけを保留する
- **共有区画**: `THEME/DerivedTokens.uss` に名札付きブロック「S4-inventory」を 1 行足す（D-12）。alpha 計画は「トークン USS は変えない」としているが、色の直書きが規約で拒まれるため、前例 S3-hotbar-modal と同じ名札付きブロックの形を取った
- **S2-tutorial**: S2-tutorial の計画の `TutorialAnchorCoveragePending` に S4-inventory 持ちの 4 行がある。S2-tutorial が先にマージされていればこのレーンが同じ PR で消す（Task 6 Step 3）。後なら S2-tutorial 側がこのレーンのアンカーで解く
- **指揮の共通の注意の反映**: `:root` のトークンを C# で読むのは `--inventory-columns` だけで、グリッドのクラスで `--inventory-view-columns: var(--inventory-columns)` と受け直して読む。`disposition.md` には処分した 2 行だけを書き、写したテストの行は書かない（albof.29・albof.31 の「写す予定」の行はタグを足すコミットで消す。albof.29 は Task 4、albof.31 は Task 9）。`UiCommandRejectReason` に値は足さない（D-15）。素通しの getter は書いていない（式本体は別オブジェクトの値の読み替えか、`ReactiveProperty` を読み取り専用の型で出す前例の形だけ）
- **未確認（実装時に測る）**: InputSystem の scroll の 1 ノッチの値（D-10。Task 11 で測る）、`inputLayer.spec.ts` の束（5 と見た。Task 8 Step 4 で確かめる）、テストの辞書で木材の名前が "Wood" になるか（Task 4）、`UiLayerStack` の層の器が `Pick` で拾われないか（Task 7。拾われるなら判定から器を除く）
- **要裁定**: なし

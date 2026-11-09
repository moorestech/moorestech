# UITK S4-alpha（段階 4 の頭: 持ち物の中核）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/blockInventory` の外殻（`BlockInventoryPanel.tsx`: viewer 枠の上端揃え・大型パネル・自然高のスクロール器・`--block-panel-*` の余白・閉じるボタンと tutorial anchor・取得失敗と列車のエラー文）と、blockType → 本文の解決（`blockComponentRegistry`）、標準の本文 `SectionStackView`（区画の固定順）、`BlockItemGrid`・`LackHighlightText`・rows、スロット操作の解釈（ブロック側とプレイヤー側の両方）を、C3-a・C3-b・C4-e の部品と口、L0-s4a・L0-s4b のロジックで Web と同じ見た目・同じ操作の UITK 画面にする。後続の S4-inventory・S4-machine・S4-train・S4-special が自分のディレクトリと自分の登録ファイルだけで差し込める拡張口 6 つ（本文・題の操作・区画・列車の本文・大型パネル判定の寄与・Shift 配分の宛先の束縛の寄与）と、型付きのスロット操作コマンド（`IPlayerSlotCommands`・`IBlockSlotCommands`）、解放状態つきの機械レシピ一覧の源（`UnlockedMachineRecipeSource`）を作り、L0/L1・並べ比べ（720p・2560×1080）・録画（新設 `uitk/uitk-block-inventory`・`uitk/uitk-lifeline`）まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。State（`Client.Ui.State`）に 4 つの塊を置く。(1) パネルの状態 `BlockInventoryPanelModel`（`SubInventoryState`・`UiRoutingState`・`OpenBlockDetailTracker` を購読し、識別子・エラー・枠だけの不変 union を配る、Web ホスト `BlockInventoryTopic`・`BlockInventoryDtoFactory`・`TrainInventoryDtoFactory` と同じ規則で開閉・source・エラー・スロットを組む。`Client.WebUiHost` は参照しない）と大きさの判定 `BlockInventoryPanelLayoutModel`（列車は常に大、ブロックは寄与の口 `IBlockInventoryLargePanelRule` のどれかが真なら大）。(2) スロット操作: ブロック側は L0-s4a の `BlockSlotPlanner` の計画を、プレイヤー側は C4-e の `PlayerSlotActionPlanner` の計画を、`PlannedItemActionRunner` が `PlayerItemActionConversion.TryConvert` で振り分け、true は C4-e の `PlayerItemActionExecutor`、false は新設のブロック側実行器 `BlockItemActionExecutor`（`IBlockInventoryOperations` の本番 `LocalBlockInventoryOperations` が結合インベントリの番地 `MainSlotCount + slot` でコントローラを呼ぶ。Web ホスト `BlockInventoryActions.cs` と同じ規則）へ渡す。Shift の断りは Web と同じ messageId で `ClientLocalNotificationSource` へ通知し開発者ログへも出す。(3) Shift 配分の宛先の束縛は L0-s4b の `BlockSlotRestrictionResolver` に、寄与の口 `IBlockSlotRestrictionContributor`（機械・ロケット・宇宙エレベーターの束縛は持ち主のレーンが足す）で組んだ入力を渡す。(4) 機械レシピの源 `UnlockedMachineRecipeSource`（`Client.Ui.State/Recipe/Source/`。Web ホスト `MachineRecipesTopic` と同じ規則・同じ再構築の契機）。View 側（`Client.Ui/Screens/BlockInventory/`）は `BlockInventoryScreenPresenter` が起動時に 1 回だけ外殻 `BlockInventoryView` を `StageColumnsLayout.Get(StageColumn.Viewer)` へ載せ [A-45]、`UiRoutingState.Current` とパネルの状態で出し入れし（前例 `PauseMenuScreenPresenter`）、大きさで寸法を変える。差し込む部分は「Presenter ＋ View」で、Presenter が自分の Model を購読する（§4.2）。本文は `BlockInventoryPartRegistry` が `BlockComponentRegistry.Resolve(blockType).Body` で引き、無ければ alpha の既定 `SectionStackBody`。拡張口は VContainer の同一インターフェイスの複数登録を `IReadOnlyList<T>` で受ける形（前例 `INestedPauseScreenState`・`ITutorialViewManager`。D-1）。見た目の値は Web と同名のトークン（`--block-panel-bottom-safe-area`・`--block-panel-right-safe-area`・`--block-panel-body-bleed`・`--menu-upper-safe-area`・`--menu-lower-safe-area`・`--inventory-panel-width`・`--stage-column-gap`・`--stage-padding-left/right`・`--panel-edge-fade`・`--bevel-3`・`--panel-action-button-width`）だけを使い、`calc()` の寸法（帯の高さ 482・大型の幅 740.108753）は C# が舞台の寸法と、画面のクラスで受け直したトークンから導く [A-47]。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`GeometryChangedEvent`・`CustomStyleResolvedEvent`）、UniRx、VContainer 1.10（コレクション解決）、NUnit＋`com.unity.ui.test-framework`、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（test-name-parity）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs`、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`。Files 節とコマンドはリポジトリ相対の完全なパスで書く（`S/` は Files 節の読みやすさのための略記で、実体は `moorestech_client/Assets/Scripts/`）
- **正本**: 裁定 `.decisions/2026-11-06-*`（「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1・§2・§3・§4.1〜§4.3・§5.1・§7.1 の A-45・A-47・A-57・A-70・A-72〜A-74・A-90〜A-99・A-110・A-111）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-alpha 行・§4.3・§5・§6.2・E-1・E-16・E-18〜E-22）、先行レーンの申し送り（`LOGS/harness/uitk/C4-e/done.md`・`L0-s4a/done.md`・`L0-s4b/done.md`・`conductor.md`「## 引き継ぎ」）、本体の追加決定（2026-11-10。機械レシピの源は alpha・液体の中身表示は S4-machine が `Screens/BlockInventory/Fluid/` に作り alpha は区画を空ける）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ

## Requirements

設計の出所: 実行計画 §1.4 の S4-alpha 行（完了条件の原文:「`BlockInventoryPanel` の外殻（`StageColumnsLayout` の `Viewer` 枠へ [A-45]）・`blockComponentRegistry`・`SectionStackView`・`BlockItemGrid`・`Parts/`（`LackHighlightText`・rows）・スロット操作のコマンド配線（ItemMove の計画 → 型付きコマンド）。写す: `blockInventory.spec`(3)・`blockInventoryGestures`(5)・`blockRegistryCoverage`(1)・`regression/sectionStack`(1)・`SectionStackView.test`(7)・`blockInventoryDesign`(S15)・localization(S2)。プレイヤー側の実行器・分割ドラッグ・進入・掴みの中身は C4-e の口（`PlayerSlotRef`・`PlayerItemActionConversion.TryConvert`・`PlayerItemActionExecutor`・`IPlayerSlotDragCommands`〈開始と進入。確定は C4-e が解放で行う〉・`IPlayerInventoryOperations`・`PlayerGrabModel`）を使う。`TryConvert` が false の計画はブロック側の実行器へ。L0-s4a の申し送り（`DestinationFull` の文言・`BlockSlotPlanner`・`DirectMovePlanner`）はこのレーン [A-93]」）、指揮の依頼（拡張口 5 つと型付きコマンドを Produces と末尾節に）、本体の追加決定 2 件。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | パネルの状態: `UiRoutingState.Current.Value.Screen == UiScreen.SubInventory` かつ `SubInventoryState.CurrentSubInventory != null` のときだけ開く。source が列車（`TrainSubInventorySource`）なら `OpenTrainInventoryPanel`（エラーは `LastOpenMessage` から ContainerMissing / TrainCarMissing / OpenFailed）、ブロックなら `OpenBlockDetailTracker.TrackedBlock` から `OpenBlockInventoryPanel`（`SubInventoryModel.Result != Success` で OpenFailed。blockType・blockGuid・BlockId・BlockInstanceId・位置・スロット・`BlockComponentRegistry.Resolve` の結果。`BlockGameObject` や列車の源は載せない）。追跡ブロックが無ければ閉。再計算の契機は Web ホストと同じ（状態遷移・`OnSubInventoryUpdated`・追跡替え・追跡ブロックの状態変化） | `BlockInventoryPanelStateFactoryTest` が緑。録画 `uitk/uitk-block-inventory` で開閉が主張どおり |
| R-2 | 外殻: `StageColumnsLayout.Get(StageColumn.Viewer)` へ載る `block-inventory`（`data-large` 相当のクラス `block-inventory--large`）。`GamePanel(block-inventory-panel, Default)`。下の余白 `--block-panel-bottom-safe-area`、小型は右の余白 `--block-panel-right-safe-area`、最大高は帯の高さ（舞台の高さ − `--menu-upper-safe-area` − `--menu-lower-safe-area`）、大型は右の余白を上書きせず（`GamePanel` 既定の 10px）、幅 = 舞台の幅 − `--stage-padding-left` − `--stage-padding-right` − `--inventory-panel-width` − `--stage-column-gap`・高さ = 帯の高さ。寸法は C# が `.block-inventory` で受け直したトークンから導く [A-47]。出し分けは `BlockInventoryScreenPresenter` が `UiRoutingState` で `SetShown` | `BlockInventoryDesignTest`（alpha 分 タグ 6 種・メソッド 7 本）・`BlockInventoryPanelBandTest` が緑。並べ比べ R-14 |
| R-3 | 題: ブロックは `ContentLocalizationKeys.BlockName(blockGuid)`、列車は `ui.blockInventory.trainInventory`。言語を変えると題が描き直される。題の操作（ロケット）はブロック・エラー無し・登録ありのときだけ `GamePanel.SetTitleAction` で出す（器 `block-inventory-title-action` を 1 回渡して中身を付け替える。D-17）。題の右の逃げ `calc(var(--panel-action-button-width) + var(--mantine-spacing-sm))`（12px。計 99px）は `GamePanel` に口が無いので integrator へ依頼する（Task 1 Step 3） | `BlockInventoryLocalizationTest`「blockGuidの導出キーを…」と `BlockInventoryTitleActionTest` が緑 |
| R-4 | 閉じる: `IconButton(block-inventory-close, IconButtonCloseGlyph)`、位置は top `--bevel-3`・right `--panel-edge-fade`、錨 `AnchorIds.InventoryCloseButton`（`TutorialAnchor.Mark`）。押すと `IBlockInventoryPanelCommands.Close()` が `Client.Ui.State/Routing/` の `IUiStateTransitionRequester.RequestTransition(GameScreen)` を呼ぶ（Web `ui_state.request gameScreen` と同じ許可表・入れ子ポーズの扱い） | `BlockInventoryPanelFlowTest`「ブロックを閉じると panel が消える」・`BlockInventoryPanelCommandsTest`・`UiStateTransitionRulesTest` が緑 |
| R-5 | エラー行: ブロック `block-inventory-error`・列車 `train-inventory-error`。文言 OpenFailed→`ui.blockInventory.inventoryOpenFailed`、ContainerMissing→`ui.blockInventory.trainNoItemContainer`、TrainCarMissing→`ui.blockInventory.trainNotFound`。エラー中は本文と題の操作を出さず、エラーが解けると戻す（判定は `Error == BlockInventoryPanelError.None`） | `BlockInventoryLocalizationTest`「取得失敗はブロック本文を隠し、失敗文を表示する」と `BlockInventoryPanelErrorTest`（正常→エラー→正常を含む）が緑 |
| R-6 | 本文: 大型は本文をそのまま（機械の大型なら `FillsPanelHeight`）、小型は自然高のスクロール器（`GameScrollView(block-inventory-natural-scroll)`。外側の負マージン `--block-panel-body-bleed`・内側の逃げ同値・Web `.scroll` の `flex: 1; min-height: 0`）で包み、帯を超える中身はスクロールする。本文は `BlockComponentRegistry.Resolve(blockType).Body` の `IBlockInventoryBodyFactory` を引き、`SectionStack` か未登録なら alpha の `SectionStackBody`（未登録の Kind は警告ログを 1 回）。列車は `ITrainInventoryBodyFactory`（未登録なら警告ログ） | `BlockInventoryPartRegistryTest`・`BlockInventoryNaturalScrollTest` が緑 |
| R-7 | `SectionStackBody`: Web `SectionStackView` の順（Context → 項目グリッド → Machine → Miner → Pump → Generator → GearToElectric → BeltThroughput → Gear → ElectricNetwork → GearNetwork → FluidDetails → FluidRow）を `UiStack(UiGap.Sm)` で固定し、項目グリッドは alpha（`configByBlockType` の写し `SectionStackConfigTable`。`ShownSectionStackItemGrid` で `RenderEmpty || 枠数>0` なら出す）、残りは `ISectionStackSectionFactory` の差し込み（区画の Presenter の `IsApplicable`〈`IReadOnlyReactiveProperty<bool>`〉を購読して出し入れと間隔を組み直す。パネルの組み直しの外の変化でも効く）。液体の 2 区画は S4-machine が埋める空き区画 | `SectionStackViewTest`（alpha 分 3 件＋出し入れの 1 件）・`SectionStackRegressionTest`（alpha 分 1 件）が緑 |
| R-8 | `BlockItemGrid`: 列数 `min(10, max(1, 枠数))`・枠 `ItemSlot(<testId>-slot-<index>)`、押下・右押下・ダブルクリックを `IBlockSlotCommands` へ。`BlockItemGridGroup`（ラベル `text--sm text--muted`・間 4px）。`LackHighlightText`（`data-insufficient` 相当のクラス `lack-highlight-text--insufficient`・色 `--text-default` / `--text-insufficient`）・`MachineStateRow`・`PerMinuteRateRow`・`PowerRateText` | `LackHighlightTextTest` 2 件・`BlockInventoryDesignTest`「ブロックスロットの列数を…」・`BlockInventoryRowsTest` が緑 |
| R-9 | ブロック側のスロット操作: 左＝`BlockSlotPlanner.PlanLeftClick`（Shift・空手・中身ありは main へ配分、掴み中は全量置き、中身ありは全量掴み）、右＝`PlanRightClick`（掴み中は 1 個置き、空手は分割）、ダブル＝`PlanDoubleClick`（集める先は持ち手の有無で決める）。計画は操作の瞬間の在庫で組む。ブロック側の計画は `BlockItemActionExecutor` が実行（分割: 掴み中は `SplitGrabNotEmpty`・空は `ItemSourceSlotEmpty`・半分 0 は何もしない成功。移動の結果は A-94 の理由へ） | `BlockSlotCommandsTest`（Web `blockInventoryGestures` 1〜4 件目・`blockInventory.spec` 2 件目）・`BlockItemActionExecutorTest` が緑 |
| R-10 | プレイヤー側の型付きコマンド `IPlayerSlotCommands`（LeftDown/RightDown/DoubleClick/LeftEnter/RightEnter）を InventoryPanel（S4-inventory）向けに出す。ブロックが開いていれば `PlayerSlotContext.WithBlockOpen`（スロット・`BlockSlotRestrictionResolver` の束縛）、閉なら `WithBlockClosed`。`BeginSplitDragPlan`→`IPlayerSlotDragCommands.BeginSplitDrag`、進入は C4-e の口へ素通し | `PlayerSlotCommandsTest`（Web `blockInventoryGestures` 5 件目）が緑 |
| R-11 | Shift の断り: NoAcceptingBlockSlot→`denied.inventoryMoveSlotRejected`・InventoryOpenFailed→`denied.inventoryOpenFailed`・DetailLoading→`denied.inventoryDetailLoading` を `ClientLocalNotificationSource.Notify(NotificationMessagePack.CreateOperationDenied(id, 空))` で出し、`[inventory.shift_move] not sent: <reason> block=<id|(closed)> item=<id>` を Warning ログ。`DestinationFull`（Web に無い）は通知せず Warning ログだけ（D-9）。`BlockSlotPlanner` の Shift→main が満杯で空配列のときも Warning ログ（D-10）。`DirectMovePlanner` へは既知の ItemId のスロットだけ渡し、未知 ID は Error ログで止める（D-11） | `ShiftMoveRefusalNotifierTest`・`PlayerSlotCommandsTest`・`BlockSlotCommandsTest` の該当件が緑 |
| R-12 | 機械レシピの源: `UnlockedMachineRecipeSource`（`Client.Ui.State/Recipe/Source/`）が解放済みの機械レシピ（`MachineRecipeData` の列。レベル継承つき）を `IReadOnlyReactiveProperty` で配る。再構築は `UnlockedEventPacket` と `ILevelInheritanceCraftCapLookup.OnCraftCapUnlocked`（Web `MachineRecipesTopic` と同じ）。要約への変換 `MachineRecipeSummaryConversion` も出す | `UnlockedMachineRecipeListBuilderTest`・`MachineRecipeSummaryConversionTest` が緑 |
| R-13 | 拡張口 6 つ（本文・題の操作・区画・列車の本文・大型パネル判定・Shift 配分の束縛）を VContainer の複数登録で受ける。本文・題の操作・区画・列車の本文の工場が作るのは「Presenter（`Attach` で自分の Model を購読し View へ・`Detach` で切る）＋ View（`Root`）」。登録 0 本でも起動し（VContainer 1.10 は空のコレクションを返す）、同じ Kind / Slot の二重登録は起動時に例外で止める | `BlockInventoryPartRegistryTest`・`SectionStackSlotRegistryTest`・`BlockInventoryPanelLayoutModelTest`・`BlockSlotRestrictionInputBuilderTest` が緑。末尾「## 後続レーンへの Produces 一覧」がこの計画の型と一致 |
| R-14 | 並べ比べ: 参照 `4-block-chest`・`4-block-generic`・`4-block-tank`（液体の区画が空なので差は液体の行だけ。LX に記帳しない・S4-machine の比較へ回す）・`4-block-trainError` を 1280×720・2560×1080 で撮り、差の表を `done.md` に書く | 差の表の全行が「同じ／直した／LX-106〜LX-110／他レーン持ち（区画が空・Inv 列の持ち物パネルが未着）」のどれか |
| R-15 | 登録: `MainGameUiRegistration` の共有サービスの並びにブロックインベントリの共有サービス、画面登録区画に `BlockInventoryGameUiRegistration.Register(builder);` を 1 行、`MainGameModelRegistration` に `IItemStackLevelLookup` を 1 行、`GameUiTheme.tss` の `Screens` 区画に `@import` を 3 行 | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`）が緑 |
| R-16 | 写すテスト（実物で数え直した alpha 分 23 件。下の「写すテスト」表）を `<web-test>` で写す。写した件は `docs/uitk/disposition.md` に書かない（書くと parity が終了コード 2）。`node tools/uitk/test-name-parity.mjs --bundle 4` で alpha のファイルの未対応 0（他レーンの持ち分は未対応のまま残ってよい） | parity の終了コードが 2 でなく、出力に alpha のファイル（`blockInventory.spec.ts`・`blockInventoryGestures.spec.ts`・`blockRegistryCoverage.spec.ts`・`BlockInventoryPanel.localization.test.ts`・`LackHighlightText/index.test.ts`）の未対応が無い |
| R-17 | 録画: `uitk/uitk-block-inventory`（新設。チェストを開く・左で掴む〈`grab-overlay` が出る〉・右で分割・ダブルで集める・Shift で main へ・閉じる・エラー無し。ブロック側の枠の操作で完結し、main の枠の Shift は S4-inventory の完了条件へ渡す）と `uitk/uitk-lifeline` を全体スイッチ UITK で回す | 2 本の `result.json` が PASS、mp4、`done.md` に `UiInputExclusivity.ActiveUiSystem` の値 |
| R-18 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd を閉じる | `git log origin/feature/uitk` にこのレーンのマージコミット。`LOGS/harness/uitk/S4-alpha/done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 区画・本文の詳細の Model（各レーンの State に置き、`OpenBlockDetailTracker` 等を購読する）、機械・採掘機・ポンプ・発電機・清浄室・入力の詰まりの区画、液体の行と詳細（`FluidInventoryContents`・`FluidSlotRow`。S4-machine が `Screens/BlockInventory/Fluid/` に作る）、機械のレシピ選択と大型パネルの判定の実装（`IBlockInventoryLargePanelRule` の実装は S4-machine）、機械の Shift 束縛の寄与（S4-machine）
- 特殊族の本文と区画（FilterSplitter・ElectricToGear・GearToElectric・ElectricPole・Gear・ベルトの流量・電力網・歯車網・宇宙エレベーター・ロケットと題の操作・入れ口の束縛の寄与。S4-special）
- 列車の本文（`TrainInventoryBody`・時刻表・駅名・プラットフォーム。S4-train）
- InventoryPanel・EquipmentPanel（S4-inventory。alpha は `IPlayerSlotCommands` を出すだけ）
- 部品（`GamePanel`・`IconButton`・`GameScrollView`・`ItemSlot`・`SlotGrid`）・Foundation（`StageColumnsLayout`・`HoverTooltipHost`・`TutorialAnchor`・`UiStack`）・トークン USS・`UiScreenTraits`・`AnchorIds` は変えない。要るものは `done.md`「integrator への依頼」へ
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は変えない。例外は `localization.csv` に行を足したときの生成物の再生成だけ（E-1）
- `blockInventoryKeydown.test.ts`（捨てる記帳済み #1599）
- ESC でのパネルの閉じ（UIState の遷移が持つ。L0-s4b D-7）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s4-alpha --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる。Mac mini の Editor は同時 2 本まで。Mac が逼迫したら Windows の `~/bin/winunity`（compile/test/play を ssh で回す）も使える
- **依存**: C4-e・C3-a・C3-b・L0-s4a・L0-s4b が入った `origin/feature/uitk`（全部マージ済み。Task 1 Step 1 で確かめる）。**C3-b を依存に足す**（E-22）: `ItemSlot` の名前ツールチップは F1 の `HoverTooltipMark` を印に C3-b の `GameUiHoverTooltipInstaller`（`HoverTooltipHost`）が出し、自然高のスクロール器 `GameScrollView` の `OnScrollOffsetChanged`・`OnContentLayoutChanged` も C3-b の口。C4-a・C4-c・C4-d は使わない（ゲージ・液体の部品は S4-machine）
- **登録の本数と置き場**: `S/Client.Starter/Registration/GameUi/` は今 9 本（`.cs`。Content・CursorTooltip・Foundation・Grab・Hud・ModeHud・PauseMenu・Toast・Tutorial）。alpha の 1 本で 10 本。後続の S4-inventory・S4-machine・S4-train・S4-special の 4 本で 14 本になるので、Task 1 で integrator へ再編の依頼を出す（alpha 自身は 10 本目で止まらない）。複数レーンが ctor で受ける共有サービス（パネルの状態・遷移要求・スロット操作の型付きコマンドとその実行器・ブロックの枠の並びの工場・解放済み機械レシピの源）は E-1・A-56 どおり `MainGameUiRegistration` の共有サービスの並びに、`IItemStackLevelLookup` は `MainGameModelRegistration.cs:105` の隣に置く（D-14・D-22）。画面だけが使うものは `BlockInventoryGameUiRegistration`
- **購読の持ち主**: Model の購読は Presenter。View は `Render` で値を受けるだけ（設計書 §4.2・A-57）。差し込む部分も「Presenter ＋ View」で、パネルの状態は識別子・エラー・枠だけを運ぶ（D-19）。画面の出し分けは `UiRoutingState.Current` だけで決める（§4.4。D-21）
- **`:root` のトークンを C# で読むとき**は、読む要素のクラスで `--<部品>-<名前>: var(--token)` と受け直し、C# は受け直した名前を読む（`UssCustomStyleReader.cs:10-13`。前例 `GaugeBar.uss:6`。D-8）
- **規約**: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下（`.meta` を除く）・partial 禁止・`Func<>` 禁止・イベントは UniRx（`Action` 禁止）・try-catch 禁止（このレーンに外部境界は無い）・日英 2 行コメント・デフォルト引数禁止・単純 getter/setter 禁止（`{ get; private set; }` と式本体の計算プロパティは可）・初期化は `Initialize`・null チェックは外部データだけ・fail-closed の経路は開発者ログ・意図して黙る行は `silent-ok: <理由> / <誰が気づくか>`・`#region Internal` はメソッド内のローカル関数だけ・`.meta` を手で作らない・Prefab/シーンを手で編集しない・`Update()` で同値判定しない（購読）
- **State は `Client.WebUiHost` を参照しない**。Web ホストにしかない規則（ブロック側の分割・集める先・結合インベントリの番地・機械レシピの一覧）は State に写し直す。重複は D-5・D-6 に記録（C4-e D-5 と同じ扱い）
- **USS**: 画面 USS の先頭に出所の 2 行（`/* web: features/blockInventory/style.module.css .panel …（日本語の要約） */` の次の行に `/* web: … (English) */`。日英を 1 行に混ぜない。AGENTS.md「2 行セット」・前例 `SlotGrid.uss:1-2`）。色はトークン経由だけ。部品のクラスを画面 USS に書かない。部品のつまみの上書きは画面のルートで（このレーンでは上書き無し）
- **使う部品と公開 API**（すべて `origin/feature/uitk` の実物から写した）

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
public sealed class IconButton : Button { public IconButton(string testid, VisualElement glyph); public IObservable<Unit> OnClicked { get; } }
public sealed class IconButtonCloseGlyph : VisualElement { public IconButtonCloseGlyph(); }
public sealed class GameScrollView : ScrollView            // C3-b の口を含む
{
    public GameScrollView(string testid);
    public IObservable<float> OnScrollOffsetChanged { get; }
    public IObservable<Unit> OnContentLayoutChanged { get; }
    public void SetScrollOffset(float y); public float GetViewportHeight(); public float GetContentHeight();
}
namespace Client.Ui.Components.Slots
public sealed class ItemSlot : VisualElement { public ItemSlot(string frameTestid); public IObservable<SlotGesture> OnSlotGestured { get; } public void SetContent(ItemSlotContent content); }
public sealed class ItemSlotContent { public ItemSlotContent(UiIcon icon, int? count, ItemSlotMarks marks, ItemSlotShortage shortage, ItemSlotGhost ghost, UiText tooltipOverride); } // icon null で空きマス
public readonly struct ItemSlotMarks { public ItemSlotMarks(bool selected, bool catalog, bool insufficient); }
public sealed class SlotGrid : VisualElement { public SlotGrid(string testid, int columns); public void SetColumns(int columns); public void AddItem(VisualElement item); public void ClearItems(); }
public readonly struct SlotGesture { public readonly SlotGestureKind Kind; public readonly bool Shift; }
public enum SlotGestureKind { LeftDown, RightDown, LeftEnter, RightEnter, DoubleClick, HoverEnter, HoverLeave }

// ── Foundation
namespace Client.Ui.Foundation.Stage.Columns
public sealed class StageColumnsLayout : VisualElement { public VisualElement Get(StageColumn column); public void SetLeftPaddingCollapsed(bool collapsed); } // DI 登録済み（FoundationGameUiRegistration）
public enum StageColumn { Inv, Viewer, Items }
namespace Client.Ui.Foundation.Layout
public sealed class UiStack : UiGapContainer { public UiStack(UiGap gap); }  // AddItem・SetItemShown・ClearItems は UiGapContainer
public enum UiGap { None, Px2, Px4, Xs, Sm, Md, Lg, Xl }
namespace Client.Ui.Foundation.Anchor
public static class TutorialAnchor { public static void Mark(VisualElement element, params string[] ids); }
namespace Client.Ui.Foundation.Text
public class LocalizedLabel : Label { public LocalizedLabel(); public void SetKey(LocalizationKey key); public void SetText(UiText text); } // 言語の変化で描き直す
public static class UiTypographyClass { public const string TextXs, TextSm, TextMuted, TextDefault, TextInsufficient; }
namespace Client.Ui.Foundation.Theme
public static class UssCustomStyleReader { public static bool TryReadPixels(ICustomStyle style, string propertyName, out float pixels); }
namespace Client.Ui.Content
public sealed class GameContentPresentation { public UiIcon Item(ItemId id); public UiIcon Block(BlockId id); public UiText ItemName(ItemId id); }
namespace Client.Ui.State.Text
public sealed class UiText { public UiText(LocalizationKey key); public UiText(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams); public static UiText FromContent(ContentLocalizationKey key); }

// ── State（C4-e）namespace Client.Ui.State.Inventory.*
public readonly struct PlayerSlotRef { public static PlayerSlotRef Main(int slot); public static PlayerSlotRef Equipment(int slot); public static bool TryFrom(ItemMoveSlotRef slot, out PlayerSlotRef playerSlot); public ItemMoveSlotRef ToItemMoveSlotRef(); public readonly bool IsEquipment; public readonly int Slot; }
public interface IPlayerInventoryOperations { IObservable<Unit> OnGrabChanged { get; } PlayerInventorySnapshot ReadSnapshot(); SlotItem ReadGrab(); PlayerItemMoveOutcome MoveToGrab(PlayerSlotRef from, int count); PlayerItemMoveOutcome MoveFromGrab(PlayerSlotRef to, int count); PlayerItemMoveOutcome MoveBetweenSlots(PlayerSlotRef from, PlayerSlotRef to, int count); void CollectToGrab(); void CollectToSlot(PlayerSlotRef slot); }
public enum PlayerItemMoveOutcome { Moved, SourceEmpty, SourceCountShort, NoEffect }
public static class PlayerItemActionConversion { public static bool TryConvert(PlannedItemAction action, out PlayerItemAction playerAction); }
public sealed class PlayerItemActionExecutor { public PlayerItemActionExecutor(IPlayerInventoryOperations operations); public UiCommandResult Execute(IReadOnlyList<PlayerItemAction> actions); }
public interface IPlayerSlotDragCommands { void BeginSplitDrag(PlayerSlotRef slot); void EnterWithLeftButton(PlayerSlotRef slot); UiCommandResult EnterWithRightButton(PlayerSlotRef slot); }
public static class PlayerSlotActionPlanner { PlanLeftDown(PlayerSlotContext, ItemMoveSlotRef, bool); PlanRightDown(PlayerInventorySnapshot, ItemMoveSlotRef); PlanDoubleClick(PlayerInventorySnapshot, ItemMoveSlotRef); } // 戻りは PlayerSlotActionPlan
public sealed class PlayerGrabModel : IInitializable  // 掴みの中身。alpha は直接は読まない（掴み絵は C4-e の GrabOverlay が出す）

// ── State（L0-s4a）namespace Client.Ui.State.ItemMove / Items
public readonly struct SlotItem { public readonly ItemId ItemId; public readonly int Count; public SlotItem(ItemId itemId, int count); public bool IsEmpty { get; } }
public readonly struct ItemMoveSlotRef { public ItemMoveArea Area; public int Slot; public static ItemMoveSlotRef Grab; public static ItemMoveSlotRef Main(int); Equipment(int); Block(int); }
public readonly struct PlannedItemAction { public readonly PlannedItemActionKind Kind; public readonly ItemMoveSlotRef From; public readonly ItemMoveSlotRef To; public readonly int Count; }
public enum PlannedItemActionKind { InventoryMoveItem, InventorySplit, InventoryCollect, BlockInventoryMoveItem, BlockInventorySplit, BlockInventoryCollect }
public static class BlockSlotPlanner { PlanLeftClick(int index, SlotItem slot, bool shiftKey, int grabCount, IReadOnlyList<SlotItem> mainSlots, IItemStackLevelLookup stackLevels); PlanRightClick(int index, SlotItem slot, int grabCount); PlanDoubleClick(int index); }
public sealed class PlayerInventorySnapshot { MainSlots; Grab; Equipment; TryResolveSlot(ItemMoveSlotRef, out SlotItem); }
public abstract class PlayerSlotContext { public static PlayerSlotContext WithBlockClosed(PlayerInventorySnapshot inventory, IItemStackLevelLookup stackLevels); public static PlayerSlotContext WithBlockOpen(PlayerInventorySnapshot inventory, IReadOnlyList<SlotItem> blockItemSlots, BlockSlotRestriction restriction, IItemStackLevelLookup stackLevels); }
public abstract class PlayerSlotActionPlan;  // ItemActionsPlan{Actions} / BeginSplitDragPlan{Target} / EnterSplitDragPlan{Target} / ShiftMoveRefusedPlan{Reason} / StaleSlotIgnoredPlan{Target}
public enum ShiftMoveRefusalReason { NoAcceptingBlockSlot, InventoryOpenFailed, DetailLoading, DestinationFull }
public abstract class BlockSlotRestriction { Unrestricted; InventoryOpenFailed; DetailLoading; Bound(IReadOnlyList<int>) }

// ── State（L0-s4b）namespace Client.Ui.State.BlockInventory.Logic.*
public enum BlockInventoryBodyKind { SectionStack, FilterSplitter, ElectricToGear, TrainPlatform, ElectricPole, SpaceElevator, RocketLaunchPad }
public enum BlockInventoryTitleActionKind { None, RocketLaunchPadCommand }
public readonly struct BlockInventoryComponents { public BlockInventoryBodyKind Body { get; } public BlockInventoryTitleActionKind TitleAction { get; } }
public static class BlockComponentRegistry { public static readonly IReadOnlyDictionary<string, BlockInventoryComponents> Registered; public static BlockInventoryComponents Resolve(string blockType); }
public abstract class BlockSlotRestrictionInput { NotOpenBlock; OpenFailedBlock; static OpenBlockSlotInput OpenBlock(string blockType, int itemSlotCount); }
public sealed class OpenBlockSlotInput : BlockSlotRestrictionInput { BlockType; ItemSlotCount; Detail; IsFixedInletBlock; OpenBlockSlotInput WithInletBindings(IReadOnlyList<InletSlotBinding>); OpenBlockSlotInput WithMachine(MachineBindingDetail); }
public static class BlockSlotRestrictionResolver { public static BlockSlotRestriction Resolve(BlockSlotRestrictionInput block, ItemId itemId); }
public static class BlockDetailStateDisplay { public static MachineStateDisplay Machine(MachineProcessDisplayState state); } // LabelKey・Insufficient・ShowPowerRate
public static class BlockDetailMath { public static double ComputePowerRate(double currentPower, double requestPower); }
public static class MachineRecipeSelectionLogic { public static List<MachineRecipeSelectionRow> BuildRows(IReadOnlyList<MachineRecipeSummary> recipes, Guid blockGuid); }

// ── 源（Client.Game）
SubInventoryState { SubInventoryModel CurrentSubInventory; ISubInventorySource CurrentSubInventorySource; IObservable<Unit> OnSubInventoryUpdated; }   // Client.Game.InGame.UI.UIState.State
SubInventoryModel { List<IItemStack> SubInventory; InventoryRequestResult Result; int Count; }                                                 // Client.Game.InGame.UI.Inventory
TrainSubInventorySource { long TrainCarInstanceId; TrainInventoryMessageType? LastOpenMessage; }                                                // TrainInventoryMessageType { ContainerMissing, TrainCarMissing, OpenFailed }
OpenBlockDetailTracker { BlockGameObject TrackedBlock; BlockSubInventorySource TrackedSource; IObservable<BlockGameObject> OnTrackedBlockChanged; IObservable<Unit> OnTrackedBlockStateChanged; } // Client.Ui.State.BlockDetail。DI 登録済み（MainGameUiStateRegistration）
UIStateControl { IObservable<UIStateEnum> OnStateChanged; UIStateEnum CurrentState; void RequestTransition(UIStateEnum nextState); INestedPauseScreenState GetCurrentNestedPauseScreen(); }   // 遷移要求の本番実装だけが使う
UiRoutingState { IReadOnlyReactiveProperty<UiRoutingSnapshot> Current; }   // Client.Ui.State.Routing。DI 登録済み（MainGameUiStateRegistration.cs:53）。UiRoutingSnapshot { UiScreen Screen; UiRoutingSnapshot(UiScreen) }・UiScreen.SubInventory
BlockGameObject { BlockInstanceId BlockInstanceId; BlockMasterElement BlockMasterElement; }   // 追跡器から識別子を取るだけ。パネルの状態には載せない
LocalPlayerInventoryController { LocalItemMoveResult MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count); void CollectItems(LocalMoveInventoryType targetType, int targetSlot); IItemStack GrabInventory; LocalPlayerInventory LocalPlayerInventory /* MainSlotCount・this[int] */; }
ClientLocalNotificationSource { void Notify(NotificationMessagePack message); }   // NotificationMessagePack.CreateOperationDenied(string messageId, Dictionary<string,string> messageParams)
IGameUnlockStateData { IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos; }  // Game.UnlockState。DI 登録済み（MainGameInteractionRegistration）
ILevelInheritanceCraftCapLookup { IObservable<(Guid familyBaseItemGuid, int level)> OnCraftCapUnlocked; }        // Game.Block.Interface.MachineUpgrade。DI 登録済み（MainGameModelRegistration）
IVanillaApiEvent { IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction); }               // Client.Network.API
IItemStackLevelLookup                                                                                                 // Core.Item.Interface。クライアント DI 未登録 → alpha が MainGameModelRegistration.cs:105 の IItemStackLevelUnlocker の隣に ServerContext.GetService で登録
ClientLocalNotificationSource                                                                                         // DI 登録済み（MainGameInteractionRegistration.cs:166）
```

## 死活表（移行で触る操作が計画後も生きるか）

触る機構: `MainGameUiRegistration`（GameUi のときだけ。Web なら早く return する `MainGameUiRegistration.cs:23`）・`MainGameModelRegistration`（両系で登録。`IItemStackLevelLookup` を 1 行足すだけ）・`GameUiTheme.tss`・`UiCommandRejectReason`（Settings・PauseMenu・分割ドラッグ・持ち物の移動も使う）・`localization.csv`・`Client.Ui.State.asmdef`・`StageColumnsLayout` の Viewer 列・`UIStateControl.RequestTransition`・`ClientLocalNotificationSource`・`UnlockedEventPacket` の購読

| 操作 | 計画後 | 根拠 |
|---|---|---|
| Web 系: ブロック／列車インベントリの開閉・枠の操作・Shift 配分・ESC | 生きる | Web の系では Client.Ui を 1 つも登録しない（`MainGameUiRegistration.cs:23`）。`Client.WebUiHost/**` は変えない。`IItemStackLevelLookup` の登録は Web 系でも増えるが、読み手が増えるだけで既存の解決は変わらない |
| UITK 系: チェストを開いてパネルを出す | 新しく使える | 裁定「未実装の画面は使えない」から実装済みになる |
| UITK 系: ESC で閉じる | 生きる | UIState の遷移が持つ（やらないことにある） |
| UITK 系: 閉じるボタン・ブロックの枠の左／右／ダブル／Shift | 新しく使える | Task 4・8 |
| UITK 系: メインの枠の Shift→ブロック | まだ使えない（退化ではない） | InventoryPanel は S4-inventory。alpha は `IPlayerSlotCommands` を出すだけで、UI からの確認は S4-inventory の完了条件へ渡す（D-33）。今までも使えなかった |
| UITK 系: 掴み絵（C4-e） | 生きる | `GrabOverlayModel` は掴みの変化で出す。源が増えるだけ |
| UITK 系: ブロックから掴んだまま閉じる | 要確認（Task 1 Step 5） | UITK に main の受け皿の画面がまだ無い。閉じた後の掴みの行き先（サーバーかコントローラが閉じるときに戻すのか、掴みに残るのか）を実物で確かめ、残るなら後続が揃うまでの振る舞いとして `done.md`「未確認」と起草メモの申告に書く |
| 既存の拒否理由（Settings・PauseMenu・分割ドラッグ） | 生きる | enum と同じファイルの switch の末尾への追記だけ。網羅テスト `PauseMenuCommandsTest` の配列も同じ Task で直す |
| ホットバー・HUD・他の常駐表示 | 生きる | 触らない。Viewer 列には今ほかの画面が無いので衝突しない |
| 解放イベントで動く既存の購読（`ClientGameUnlockStateData`・Web の各 Topic） | 生きる | 購読を 1 本足すだけで既存のハンドラは変えない |

判定: 死ぬ・退化する操作は無い。要確認 1 行は Task 1 Step 5 で実物を読む。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反5件・修正5件 / 区画の購読を Presenter へ・出し分けを UiRoutingState へ・命名・Lookup の登録先・死活表
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強3・弱3・第3バケツ5 / Error の null 比較・SectionStackConfig の null を判別 union へ・共有サービスの登録先を E-1 へ（agent 既定で裁定）
- 配置検査 r2（反映後の再検査）: 実施済み / 違反1・修正1 / 閉じるの結線を `OnClicked.Subscribe` へ（V-1）。注目点 N-2・N-3 は D-40・D-41、N-7 は Task 8 で `GrabGameUiRegistration.cs` のコメントを直す
- Phase 2.6 r2: 実施済み / 強7・弱7・第3バケツ6 / 強: null と None の二重を空の部分へ（D-35）・種別ごとの例外 3 か所を型と Try へ（D-36）・「開いている」の導出を 1 本へ（D-37）・大きさを判別子へ（D-38）、ID→アイコンの欠損は再現せず（`GameContentPresentation` が例外で止める）、ADR 食い違い 2 件は ADR 側を直した（945a356987）。弱は 1 件直し 6 件見送り（D-39）。第3バケツ新規 1 件を「本 PR 外のリファクタ提案」へ（agent 既定で裁定）
- r3 の差分検査: 配置 違反0 / Phase 2.6 強0・弱2 / 弱 2 件（`TryReadBlockSlots` の false の 2 理由・内部の null を合図にする Try）は、ログの文を直し、新しく書く Try 2 か所を switch 文の形へ直し、`TryConvert` は前例と対で残す（D-39）。レビュー r3 の 10 件と D-37 の先書きの窓は D-37・D-42〜D-46 で反映（agent 既定で裁定）
- r4 の差分検査: 配置 違反0 / Phase 2.6 強1・弱3 / 強 1 件（生の値を 2 か所で写す）は D-37 で裁定済み。弱 3 件: `TryReadBlockSlots` の false の 2 理由と `Error == None` の述語の散在は D-39 で裁定済み、`TryGetMessageId` の未知の値は DestinationFull を明示の腕にして未知は例外で止めた（D-44 と揃える）。禁止部品のたどり方（D-46）・D-37 の判別テスト（D-47）・`OnExit` の null の窓（D-48。成り立たない）・保留表の衝突の解き方・件数の記述を反映（agent 既定で裁定）

## 写すテスト

Web の実物を開いて数え直した（`grep -nE "^\s*(it|test)(\.each)?\("`）。§1.4 の `SectionStackView.test`(7) は実物 8（it.each 1＋it 7）、`blockInventoryDesign`(S15) は実物 16（it.each 1＋it 15）。実物が正。区画の中身にかかる件は持ち主（machine / special / train）が同じタグで写し、alpha は外殻・順序・項目グリッドに当たる面だけを写す（同タグの NUnit が複数になってよい。W0-tools の書式）。

| Web ファイル（`W/` から） | テスト名（第 1 引数） | 写す人 | alpha の NUnit |
|---|---|---|---|
| `e2e/tests/block/blockInventory.spec.ts` | chest の block inventory パネルが描画される | alpha | `BlockInventoryPanelFlowTest` |
| 同 | filled スロット左クリックで block→grab の move_item を送り、grab オーバーレイが出る | alpha | `BlockSlotCommandsTest`（送る移動）＋録画 `uitk/uitk-block-inventory` 手順 3（`grab-overlay` が `grab-overlay--shown` で display Flex・掴みが 10。Task 11） |
| 同 | ブロックを閉じると panel が消える | alpha | `BlockInventoryPanelFlowTest` |
| `e2e/tests/block/blockInventoryGestures.spec.ts` | 空手の右クリックで block_inventory.split を送り grab に半分入る | alpha | `BlockSlotCommandsTest` |
| 同 | grab 保持中の右クリックで block スロットへ1個置く | alpha | `BlockSlotCommandsTest` |
| 同 | ダブルクリックで block_inventory.collect を送る | alpha | `BlockSlotCommandsTest` |
| 同 | Shift+クリックで block→main へ配分移動する | alpha | `BlockSlotCommandsTest` |
| 同 | block 開時は main の Shift+クリックが block へ配分移動する | alpha | `PlayerSlotCommandsTest` |
| `e2e/tests/block/blockRegistryCoverage.spec.ts` | v8マスタで専用UIを持つ全blockTypeがレジストリまたは意図的Genericに分類される | alpha | `BlockRegistryCoverageTest` |
| `e2e/tests/regression/sectionStack.spec.ts` | `${entry.type}は対応セクションだけを表示する` | alpha（項目グリッドの id。FilterSplitter を除く 6 ケース）＋ machine・special（区画の id）＋ special（FilterSplitter の専用本文のケース） | `SectionStackRegressionTest` |
| `src/features/blockInventory/views/__tests__/SectionStackView.test.tsx` | %s の既存表示を保つ | alpha（項目グリッド）＋ machine（液体の行） | `SectionStackViewTest` |
| 同 | パイプは液体だけを進捗なしで描画する | machine | — |
| 同 | 空のパイプは名前や容量を表示せず空スロットを残す | machine | — |
| 同 | 親から状態・供給・歯車・電力網・歯車網を順に描画する | alpha（区画の順を偽の区画で）＋ special（中身） | `SectionStackViewTest` |
| 同 | 部屋欄はセクション列の先頭に描く | alpha（Context 区画が項目グリッドより上）＋ machine（中身） | `SectionStackViewTest` |
| 同 | 搬入停止欄は機械欄より上に赤字で描く | machine | — |
| 同 | 抽選の中身欄は部屋欄のすぐ下に描く | machine | — |
| 同 | 照会欄の無い payload では3欄とも描かず、取得中の欄は取得状態の行だけを描く | machine | — |
| `src/features/blockInventory/blockInventoryDesign.test.ts` | %sから禁止Mantine部品とテーマ色を除去する | alpha（外殻・BlockItemGrid・rows の USS に色の直書きが無い・本文の木に素の操作部品が無い）＋ machine・special・train（各自の USS を各自のテストファイルに同じタグで） | `BlockInventoryDesignTest` |
| 同 | パネル面と閉じる操作をshared uiへ集約する | alpha | `BlockInventoryDesignTest` |
| 同 | ブロックパネルをviewer領域へ上端揃えで配置する | alpha | `BlockInventoryDesignTest` |
| 同 | ブロックパネルだけに下部装飾の安全帯を確保する | alpha | `BlockInventoryDesignTest` |
| 同 | ゲージとモード切替を用途別に共通部品へ置換する | machine（Generator）・special（FilterSplitter・ElectricToGear）・train（TrainPlatform） | — |
| 同 | ブロックスロットの列数を1列から持ち物と同じ列数の範囲で内容に追従させる | alpha | `BlockInventoryDesignTest` |
| 同 | フィルタ分岐器の方向を縦積みにする | special | — |
| 同 | ゲージ系部品を寒色トークンで統一する | machine | — |
| 同 | モード切替の間隔と選択面を専用トークンで制御する | special | — |
| 同 | MachineSectionはレシピ選択リストとヘッダを往復し、電力率を共通フッタに置く | machine | — |
| 同 | レシピ有り機械だけviewer〜items占有の大型パネルへ広げる | alpha（寄与が真で大型の寸法になる）＋ machine（寄与の実装） | `BlockInventoryDesignTest` |
| 同 | レシピ選択行は共有RecipeRowを流用し、静止矢印で列挙する | machine | — |
| 同 | 機械の加工行は進捗矢印を中央に固定する3カラムグリッドにする | machine | — |
| 同 | モジュールスロットをラベル付きで加工行から1段下げる | machine | — |
| 同 | ポンプの流体アイコンを非正方形でも歪ませずに収める | machine | — |
| 同 | 稼働ラベルと分間レートの行を機械・採掘機・ポンプで共有する | machine（行は alpha の `Parts/Rows` を使う） | — |
| `src/features/blockInventory/BlockInventoryPanel.localization.test.ts` | blockGuidの導出キーをfallback解決し、辞書世代の変更でタイトルを再描画する | alpha | `BlockInventoryLocalizationTest` |
| 同 | 取得失敗はブロック本文を隠し、失敗文を表示する | alpha | `BlockInventoryLocalizationTest` |
| `src/features/blockInventory/details/LackHighlightText/index.test.ts` | 現在値と要求値を表示し、通常状態をdata属性で公開する | alpha（§1.4 に無いが alpha の部品） | `LackHighlightTextTest` |
| 同 | 不足状態をdata属性で公開する | alpha | `LackHighlightTextTest` |

alpha のタグの種類は 23（3＋5＋1＋1＋3＋6＋2＋2）、タグ付きの NUnit メソッドは 24（design は同じタグ `%sから禁止Mantine部品とテーマ色を除去する` が 2 本。`blockInventory.spec` 2 件目の「grab オーバーレイが出る」は `BlockSlotCommandsTest` が送る移動を、録画 `uitk-block-inventory` 手順 3 が絵の表示を主張する）。タグ無しの自前テスト（Model・実行器・口・源・区画の出し入れ・エラーからの復帰）は Task ごとに書く。後続レーンは alpha のテストファイルへ足さず、自分のテストファイルに同じタグで写す（同タグの NUnit が複数になってよい）。

## File Structure

新規（すべて `moorestech_client/Assets/Scripts/` の下）:

State（`Client.Ui.State/`）
- `BlockInventory/Panel/BlockInventoryPanelState.cs` — 閉・ブロック開・列車開の union（`ClosedBlockInventoryPanel`・`OpenBlockInventoryPanel`・`OpenTrainInventoryPanel`）
- `BlockInventory/Panel/BlockInventoryPanelError.cs` — エラーの列挙と文言キーへの写し
- `BlockInventory/Panel/BlockInventoryPanelStateFactory.cs` — 源の値から状態を組む純関数（Web ホストの DTO 工場の規則）
- `BlockInventory/Panel/BlockInventoryPanelModel.cs` — 源を購読して `Current` を配る（IInitializable）
- `BlockInventory/Panel/IBlockInventoryLargePanelRule.cs` — 拡張口 (5)
- `BlockInventory/Panel/BlockInventoryPanelLayout.cs` — 大型・高さ確定の値
- `BlockInventory/Panel/BlockInventoryPanelLayoutModel.cs` — 状態と寄与から大きさを配る
- `BlockInventory/Panel/IBlockInventoryPanelCommands.cs`・`BlockInventoryPanelCommands.cs` — 閉じる
- `BlockInventory/Panel/BlockInventoryScreenVisibility.cs` — 画面ルータとパネルの状態から出すかを決める
- `BlockInventory/SectionStack/SectionStackConfig.cs`・`SectionStackConfigTable.cs` — Web `configByBlockType` の写し（項目グリッドと液体の判別 union）
- `Routing/IUiStateTransitionRequester.cs`・`UiStateControlTransitionRequester.cs`・`UiStateTransitionRules.cs` — 遷移要求の口と許可表（後続の画面も使う）
- `BlockInventory/SlotRestriction/IBlockSlotRestrictionContributor.cs` — 拡張口 (6)
- `BlockInventory/SlotRestriction/BlockSlotRestrictionInputBuilder.cs` — パネル状態＋寄与 → L0-s4b の入力
- `SlotCommands/PlannedItemActionRunner.cs` — 計画を TryConvert で振り分ける
- `SlotCommands/KnownItemSlots.cs` — 未知 ItemId の枠を DirectMovePlanner へ渡さない（D-11）
- `SlotCommands/Block/IBlockInventoryOperations.cs`・`IRawSubInventorySlots.cs`・`SubInventoryRawSlots.cs`・`BlockSlotReader.cs`・`LocalBlockInventoryOperations.cs`・`BlockItemAction.cs`・`BlockItemActionConversion.cs`・`BlockItemActionExecutor.cs`・`IBlockSlotCommands.cs`・`BlockSlotCommands.cs`
- `SlotCommands/Player/IPlayerSlotCommands.cs`・`PlayerSlotCommands.cs`・`ShiftMoveRefusalNotifier.cs`・`PlayerSlotContextReader.cs`
- `Recipe/Source/UnlockedMachineRecipeSource.cs`・`UnlockedMachineRecipeListBuilder.cs`・`MachineRecipeLevelInheritanceConversion.cs`・`MachineRecipeSummaryConversion.cs`

Client.Ui（`Client.Ui/Screens/BlockInventory/`）
- `BlockInventoryScreenPresenter.cs` — 起動時に 1 回載せ、画面ルータ・状態・大きさを購読して出し入れ
- `BlockInventoryView.cs` — 外殻（GamePanel・題・閉じる・エラー行・本文の器。購読しない）
- `BlockInventoryPanelBand.cs` — 帯の高さと大型の幅を舞台とトークンから導く [A-47]
- `BlockInventoryNaturalScroll.cs` — 自然高のスクロール器（負マージンと内側の逃げ）
- `BlockInventoryBodyHost.cs` — 本文・題の操作・列車の本文の Presenter の付け替え（`IsSamePanel` と大きさで決める）
- `BlockInventory.uss`
- `Registry/IBlockInventoryPart.cs`・`IBlockInventoryBodyFactory.cs`・`IBlockInventoryTitleActionFactory.cs`・`ITrainInventoryBodyFactory.cs`・`BlockInventoryBodyMount.cs`・`BlockInventoryPartRegistry.cs`
- `SectionStack/SectionStackSlot.cs`・`IBlockInventorySection.cs`・`ISectionStackSectionFactory.cs`・`SectionStackSectionMount.cs`・`SectionStackSlotRegistry.cs`・`SectionStackBody.cs`・`SectionStackBodyFactory.cs`・`SectionStackItemGridSection.cs`・`SectionStack.uss`
- `Parts/LackHighlightText.cs`・`BlockItemGrid.cs`・`BlockItemGridGroup.cs`・`BlockItemGridFactory.cs`・`Parts.uss`
- `Parts/Rows/MachineStateRow.cs`・`PerMinuteRateRow.cs`・`PowerRateText.cs`

登録（`Client.Starter/Registration/GameUi/BlockInventoryGameUiRegistration.cs`。共有サービスは `MainGameUiRegistration` の共有サービスの並び）

テスト（`Client.Tests/Ui/`）
- `State/BlockInventory/PanelModel/BlockInventoryPanelStateFactoryTest.cs`・`BlockInventoryPanelLayoutModelTest.cs`・`BlockInventoryPanelCommandsTest.cs`・`SectionStackConfigTableTest.cs`・`BlockRegistryCoverageTest.cs`・`FakePanelSource.cs`・`BlockInventoryTestPanels.cs`・`BlockInventoryScreenVisibilityTest.cs`
- `State/Routing/UiStateTransitionRulesTest.cs`
- `State/SlotCommands/BlockItemActionExecutorTest.cs`・`BlockSlotCommandsTest.cs`・`PlayerSlotCommandsTest.cs`・`ShiftMoveRefusalNotifierTest.cs`・`BlockSlotRestrictionInputBuilderTest.cs`・`RecordingBlockInventoryOperations.cs`
- `State/Recipe/Source/UnlockedMachineRecipeListBuilderTest.cs`・`MachineRecipeSummaryConversionTest.cs`・`FakeGameUnlockStateData.cs`
- `Screens/BlockInventory/BlockInventoryPanelFlowTest.cs`・`BlockInventoryDesignTest.cs`・`BlockInventoryLocalizationTest.cs`・`BlockInventoryPanelErrorTest.cs`・`BlockInventoryPanelBandTest.cs`・`BlockInventoryPartRegistryTest.cs`・`BlockInventoryTitleActionTest.cs`・`BlockInventoryNaturalScrollTest.cs`・`BlockInventoryTestFixtures.cs`
- `Screens/BlockInventory/SectionStack/SectionStackViewTest.cs`・`SectionStackRegressionTest.cs`・`SectionStackSlotRegistryTest.cs`・`FakeSection.cs`
- `Screens/BlockInventory/Parts/LackHighlightTextTest.cs`・`BlockInventoryRowsTest.cs`

録画: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-block-inventory.cs`

変更（共有区画への追記だけ）:
- `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs` — 共有サービスの並びにブロックインベントリの共有サービス、画面登録区画の末尾に 1 行
- `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs` — `IItemStackLevelUnlocker` の隣に `IItemStackLevelLookup` を 1 行
- `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` — 良性の拒否理由の網羅テストの配列に `BlockSlotUnavailable`
- `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` — `Screens` 区画の末尾に `@import` を 3 行（`BlockInventory.uss`・`SectionStack/SectionStack.uss`・`Parts/Parts.uss`）
- `moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs` — enum と 3 つの switch の末尾に `BlockSlotUnavailable`
- `moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef` — 参照に `Game.UnlockState` を足す
- `Localization/localization.csv` — `ui.commandRejected.blockSlotUnavailable` を 1 行（と生成物の再生成）
- `docs/uitk/TODO.md` — 「## 追従記録」に 1 行、見た目の例外を使ったときだけ「## 見た目の例外」に追記（`docs/uitk/disposition.md` は書かない）

ディレクトリのファイル数（`.cs`・`.uss`）: `Client.Ui.State/BlockInventory/Panel/` 10・`Routing/` 4＋3＝7・`SlotCommands/` 2＋`Block/` 5＋`Player/` 4・`Recipe/Source/` 4・`Screens/BlockInventory/` 6・`Registry/` 6・`SectionStack/` 9・`Parts/` 5＋`Rows/` 3。すべて 10 以下。

## Tasks

### Task 1: 前提の確認と記録の準備

**Files:**
- Create: `LOGS/harness/uitk/S4-alpha/done.md`（repo 外。`../moorestech_logs` の README を先に読む）

**Interfaces:**
- Consumes: `origin/feature/uitk`（C4-e #1626・C3-a・C3-b・L0-s4a・L0-s4b）
- Produces: bd の issue・`done.md` の骨組み・integrator への依頼（登録ディレクトリの再編）

- [ ] **Step 1: worktree と依存を確かめる**

Run:
```bash
pwd
git fetch origin && git log --oneline -1 origin/feature/uitk
git log --oneline origin/feature/uitk | grep -E "c4-e|C4-e" | head -1
ls moorestech_client/Assets/Scripts/Client.Ui.State/Inventory/Actions/PlayerItemActionConversion.cs moorestech_client/Assets/Scripts/Client.Ui.State/ItemMove/BlockSlotPlanner.cs moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Logic/Panel/BlockComponentRegistry.cs moorestech_client/Assets/Scripts/Client.Ui/Foundation/Tooltip/GameUiHoverTooltipInstaller.cs moorestech_client/Assets/Scripts/Client.Ui/Foundation/Stage/Columns/StageColumnsLayout.cs
ls moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/*.cs | wc -l
moores-wt status
```
Expected: worktree のパス、5 ファイルが全部ある、`GameUi/` の `.cs` が 9 本（他レーンが先に入っていれば多い）。足りなければ止めて指揮へ返す。`GameUi/` が 10 本以上なら Step 3 の依頼に「alpha の登録で 11 本以上になる」と書き、再編が入るまで Task 8 の登録だけを保留して他を進める

- [ ] **Step 2: bd を作り、done.md の骨組みを書く**

Run: `bd create "UITK S4-alpha: ブロックインベントリの外殻・拡張口・スロット操作" -t task -p 1 --parent <uitk の epic>`（epic の id は `bd list --status open | grep -i uitk` で引く）→ `bd update <bd> --status in_progress`

`LOGS/harness/uitk/S4-alpha/done.md` を次の骨組みで作る（1 行目は作業中は「状態: 作業中」）:
```markdown
状態: 作業中
# S4-alpha（持ち物の中核）
計画: docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md / bd: <bd>
## 証拠
## 差の表
## 要裁定
## integrator への依頼
## 未確認
```

- [ ] **Step 3: integrator への依頼を先に出す**

`done.md`「## integrator への依頼」に次を書き、指揮へも同じ文を返す（`cmux` の依頼口か指揮のスレッド）:
- `Client.Starter/Registration/GameUi/` は今 9 本で alpha で 10 本、S4-inventory・S4-machine・S4-train・S4-special が 1 本ずつ足すと 14 本になる。`GameUi/Screens/`・`GameUi/BlockInventory/` 等への再編を W4 の取り込みの前に入れてほしい（alpha は 10 本目のまま止めずに進める）
- `SlotCommands`（`Client.Ui.State/SlotCommands/`）は持ち物・ブロック・機械・特殊族が共有する。昇格の対象ではないが、設計書 §2 の State の表に A-142 の行を足してほしい（下書きは計画末尾）
- `GamePanel` が題の操作（`SetTitleAction`）を持つとき、題に右の逃げ `calc(var(--panel-action-button-width) + var(--mantine-spacing-sm))`（12px。計 99px）を足す口がほしい（Web は `BlockInventoryPanel.tsx` が題の `paddingRight` で持つ。UITK では題の要素が部品の中にあり画面 USS から当てられない）。使うのは S4-special のロケットだけなので、alpha は器を渡すまでで止める

- [ ] **Step 4: 共有の登録の行き先を確かめる**

`grep -rn "IItemStackLevelLookup" moorestech_client/Assets/Scripts/Client.Starter` が 0 件（起草時は 0 件）であることと、`MainGameModelRegistration.cs` の `IItemStackLevelUnlocker` の行番号を確かめる。他レーンが先に足していれば Task 8 の該当行は足さず `done.md`「証拠」に書く

- [ ] **Step 5: 「掴んだまま閉じる」の行き先を実物で読む（死活表の要確認）**

`UIStateControl` の SubInventory からの退出（`SubInventoryState` の `OnExit` 相当）と `LocalPlayerInventoryController` を読み、ブロックから掴んだまま閉じたときに掴みが main へ戻るのか・サーバーへ何を送るのかを確かめる。結論と根拠の行（ファイル:行）を `done.md`「証拠」へ。掴みに残るなら、UITK で main の受け皿〈S4-inventory〉が揃うまでの振る舞いとして `done.md`「未確認」と指揮への申告に書く（このレーンでは直さない）

- [ ] **Step 6: コミットは無し**（記録は repo 外）

---

### Task 2: 解放状態つきの機械レシピ一覧の源（`Client.Ui.State/Recipe/Source/`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Source/UnlockedMachineRecipeListBuilder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Source/MachineRecipeLevelInheritanceConversion.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Source/UnlockedMachineRecipeSource.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Source/MachineRecipeSummaryConversion.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef`（`references` に `"Game.UnlockState"`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Source/UnlockedMachineRecipeListBuilderTest.cs`・`MachineRecipeSummaryConversionTest.cs`・`FakeGameUnlockStateData.cs`

**Interfaces:**
- Consumes: `MasterHolder.MachineRecipesMaster.MachineRecipes.Data`（`MachineRecipeMasterElement`: `MachineRecipeGuid`・`BlockGuid`・`Time`・`InputItems[i].ItemGuid/Count`・`OutputItems`・`InputFluids[i].FluidGuid/Amount`・`OutputFluids`・`LevelInheritance`）、`MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out LevelInheritanceShape)`・`GetMaxCraftableLevel(shape, lookup)`、`IGameUnlockStateData.MachineRecipeUnlockStateInfos`、`ILevelInheritanceCraftCapLookup.OnCraftCapUnlocked`、`IVanillaApiEvent.SubscribeEventResponse(UnlockedEventPacket.EventTag, …)`、既存の `MachineRecipeData`・`RecipeLevelInheritance`・`ItemAmount`・`RecipeFluidAmount`・`MachineRecipeSummary`・`RecipeOutputItem`・`RecipeOutputFluid`
- Produces: `UnlockedMachineRecipeSource.Current : IReadOnlyReactiveProperty<IReadOnlyList<MachineRecipeData>>`、`UnlockedMachineRecipeListBuilder.Build(IGameUnlockStateData, ILevelInheritanceCraftCapLookup)`、`MachineRecipeSummaryConversion.ToSummaries(IReadOnlyList<MachineRecipeData>)`（S4-machine・S4-recipe と大型パネル判定の寄与の実装が使う。S4-research は未解放も引くのでマスタから直接読み、使わない）

- [ ] **Step 1: 失敗するテストを書く**

`FakeGameUnlockStateData.cs`（`IGameUnlockStateData` の全メンバーを持つ偽物。機械レシピの辞書だけを差し替える）:
```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.UnlockState;
using Game.UnlockState.States;

namespace Client.Tests.Ui.State.Recipe.Source
{
    /// <summary>
    /// 機械レシピの解放表だけを差し替えられる解放状態の偽物
    /// Unlock-state fake whose machine-recipe table alone can be replaced
    /// </summary>
    public sealed class FakeGameUnlockStateData : IGameUnlockStateData
    {
        private readonly Dictionary<Guid, MachineRecipeUnlockStateInfo> _machineRecipes = new();

        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; } = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos => _machineRecipes;
        public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } = new Dictionary<Guid, BlockUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } = new Dictionary<Guid, TrainCarUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; } = new Dictionary<Guid, ConnectToolUnlockStateInfo>();
        public bool IsBlueprintUnlocked => false;

        public void SetMachineRecipe(Guid recipeGuid, bool unlocked)
        {
            _machineRecipes[recipeGuid] = new MachineRecipeUnlockStateInfo(recipeGuid, unlocked);
        }
    }
}
```
（`*UnlockStateInfo` の名前空間は `Game.UnlockState.States` を実物で確かめ、違えば `using` だけ直す。`IGameUnlockStateData` にメンバーが増えていれば同じ形で足す）

`UnlockedMachineRecipeListBuilderTest.cs`:
```csharp
using System.Linq;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Source;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Client.Tests.Ui.State.Recipe.Source
{
    /// <summary>
    /// 解放済みの機械レシピだけを Web の MachineRecipesTopic と同じ形で並べる（L0）
    /// Lists only unlocked machine recipes in the same shape as the Web MachineRecipesTopic (L0)
    /// </summary>
    public class UnlockedMachineRecipeListBuilderTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 解放済みのレシピだけを並べ解放表に無いレシピは出さない()
        {
            var all = MasterHolder.MachineRecipesMaster.MachineRecipes.Data;
            Assert.GreaterOrEqual(all.Length, 2, "テスト用 mod に機械レシピが 2 件以上ある前提");
            var unlock = new FakeGameUnlockStateData();
            unlock.SetMachineRecipe(all[0].MachineRecipeGuid, true);
            unlock.SetMachineRecipe(all[1].MachineRecipeGuid, false);

            var recipes = UnlockedMachineRecipeListBuilder.Build(unlock, new FixedCraftCapLookup());

            Assert.AreEqual(new[] { all[0].MachineRecipeGuid }, recipes.Select(r => r.RecipeGuid).ToArray());
        }

        [Test]
        public void 入出力と時間とブロックをマスタから写す()
        {
            var master = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var unlock = new FakeGameUnlockStateData();
            unlock.SetMachineRecipe(master.MachineRecipeGuid, true);

            var recipe = UnlockedMachineRecipeListBuilder.Build(unlock, new FixedCraftCapLookup()).Single();

            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockId(master.BlockGuid), recipe.BlockId);
            Assert.AreEqual(master.Time, recipe.Time);
            Assert.AreEqual(master.InputItems.Length, recipe.InputItems.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(master.OutputItems[0].ItemGuid), recipe.OutputItems[0].ItemId);
            Assert.AreEqual(master.OutputItems[0].Count, recipe.OutputItems[0].Count);
            Assert.AreEqual(master.InputFluids.Length, recipe.InputFluids.Count);
        }
    }
}
```
同じファイルに偽の製作上限 `FixedCraftCapLookup : ILevelInheritanceCraftCapLookup`（全メンバーを実物から写し、`OnCraftCapUnlocked` は `new Subject<(Guid, int)>()`、上限の問い合わせは 1 を返す）を `internal sealed class` で置く。テスト用 mod の機械レシピの 0 件目に出力アイテムが無ければ、出力アイテムを持つ最初のレシピを `First(r => r.OutputItems.Length > 0)` で選ぶ形に直す。

`MachineRecipeSummaryConversionTest.cs`（2 件: 出力アイテム・出力液体・ブロックの Guid を写す／空の列は空の列）:
```csharp
using System;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Source;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Recipe.Source
{
    public class MachineRecipeSummaryConversionTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 出力とブロックのGuidを要約へ写す()
        {
            var blockId = new BlockId(1);
            var recipe = new MachineRecipeData(Guid.NewGuid(), blockId, 2, Array.Empty<ItemAmount>(), new[] { new ItemAmount(new ItemId(3), 4) }, Array.Empty<RecipeFluidAmount>(), new[] { new RecipeFluidAmount(new FluidId(1), 5) }, RecipeLevelInheritance.None);

            var summary = MachineRecipeSummaryConversion.ToSummaries(new[] { recipe })[0];

            Assert.AreEqual(recipe.RecipeGuid, summary.RecipeGuid);
            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid, summary.BlockGuid);
            Assert.AreEqual(new ItemId(3), summary.OutputItems[0].ItemId);
            Assert.AreEqual(4, summary.OutputItems[0].Count);
            Assert.AreEqual(5, summary.OutputFluids[0].Amount);
        }

        [Test]
        public void 空の列は空の列()
        {
            Assert.AreEqual(0, MachineRecipeSummaryConversion.ToSummaries(Array.Empty<MachineRecipeData>()).Count);
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `UnlockedMachineRecipeListBuilder`・`MachineRecipeSummaryConversion` が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`Client.Ui.State.asmdef` の `references` に `"Game.UnlockState"` を足す（`Game.Block.Interface` は既にある）。

`UnlockedMachineRecipeListBuilder.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Game.UnlockState;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.Ui.State.Recipe.Source
{
    /// <summary>
    /// 解放済みの機械レシピを並べる（Web ホスト MachineRecipesTopic.BuildJson と同じ規則。UITK は橋を参照しない）
    /// Lists unlocked machine recipes (same rule as the Web host MachineRecipesTopic.BuildJson; UITK never references the bridge)
    /// </summary>
    public static class UnlockedMachineRecipeListBuilder
    {
        public static IReadOnlyList<MachineRecipeData> Build(IGameUnlockStateData unlockState, ILevelInheritanceCraftCapLookup craftCapLookup)
        {
            var recipes = new List<MachineRecipeData>();
            var unlockInfos = unlockState.MachineRecipeUnlockStateInfos;
            foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                // silent-ok: 解放表に無いレシピはロック扱い（ホストと同じ） / 解放されると UnlockedEventPacket で作り直され一覧に出る
                if (!unlockInfos.TryGetValue(recipe.MachineRecipeGuid, out var unlockInfo)) continue;
                // silent-ok: 未解放のレシピは一覧に出さない（ホストと同じ） / 解放されると UnlockedEventPacket で作り直され一覧に出る
                if (!unlockInfo.IsUnlocked) continue;
                recipes.Add(ToData(recipe));
            }
            return recipes;

            #region Internal

            MachineRecipeData ToData(MachineRecipeMasterElement element)
            {
                // マスタの Guid を実行時 ID へ写し、継承はホストと同じ util で読む
                // Map master GUIDs to runtime IDs and read inheritance through the same util as the host
                var inputItems = new List<ItemAmount>();
                foreach (var input in element.InputItems) inputItems.Add(new ItemAmount(MasterHolder.ItemMaster.GetItemId(input.ItemGuid), input.Count));
                var outputItems = new List<ItemAmount>();
                foreach (var output in element.OutputItems) outputItems.Add(new ItemAmount(MasterHolder.ItemMaster.GetItemId(output.ItemGuid), output.Count));
                var inputFluids = new List<RecipeFluidAmount>();
                foreach (var input in element.InputFluids) inputFluids.Add(new RecipeFluidAmount(MasterHolder.FluidMaster.GetFluidId(input.FluidGuid), input.Amount));
                var outputFluids = new List<RecipeFluidAmount>();
                foreach (var output in element.OutputFluids) outputFluids.Add(new RecipeFluidAmount(MasterHolder.FluidMaster.GetFluidId(output.FluidGuid), output.Amount));
                var blockId = MasterHolder.BlockMaster.GetBlockId(element.BlockGuid);
                var inheritance = MachineRecipeLevelInheritanceConversion.Convert(element, craftCapLookup);
                return new MachineRecipeData(element.MachineRecipeGuid, blockId, element.Time, inputItems, outputItems, inputFluids, outputFluids, inheritance);
            }

            #endregion
        }
    }
}
```

`MachineRecipeLevelInheritanceConversion.cs`（Web ホスト `MachineRecipeLevelInheritanceDtoBuilder.Build` の写し。wire 文字列でなく State の union を返す）:
```csharp
using System;
using Client.Ui.State.Recipe.Data;
using Game.Block.Interface.MachineUpgrade;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.Ui.State.Recipe.Source
{
    /// <summary>
    /// マスタの levelInheritance を State の継承 union へ写す唯一の場所（ホストの DtoBuilder と同じ網羅 switch）
    /// The single place mapping the master levelInheritance onto the State union (same exhaustive switch as the host DtoBuilder)
    /// </summary>
    public static class MachineRecipeLevelInheritanceConversion
    {
        public static RecipeLevelInheritance Convert(MachineRecipeMasterElement recipe, ILevelInheritanceCraftCapLookup craftCapLookup)
        {
            switch (recipe.LevelInheritance)
            {
                case MachineRecipeMasterElement.LevelInheritanceConst.None:
                    return RecipeLevelInheritance.None;
                case MachineRecipeMasterElement.LevelInheritanceConst.Inherit:
                {
                    var shape = ReadShape();
                    return RecipeLevelInheritance.Inherit(shape.SourceInputIndex, shape.SourceVariants, shape.TargetOutputIndex, shape.TargetVariants, MachineRecipeLevelInheritanceUtil.GetMaxCraftableLevel(shape, craftCapLookup));
                }
                case MachineRecipeMasterElement.LevelInheritanceConst.InheritUpToUnlocked:
                {
                    var shape = ReadShape();
                    return RecipeLevelInheritance.InheritUpToUnlocked(shape.SourceInputIndex, shape.SourceVariants, shape.TargetOutputIndex, shape.TargetVariants, MachineRecipeLevelInheritanceUtil.GetMaxCraftableLevel(shape, craftCapLookup));
                }
                default:
                    throw new InvalidOperationException($"未知の levelInheritance です recipe={recipe.MachineRecipeGuid} value={recipe.LevelInheritance}");
            }

            #region Internal

            LevelInheritanceShape ReadShape()
            {
                // switch が継承と決めたのに形が引けなければ util との食い違いなので止める
                // Stop when the switch chose inheritance but the util has no shape: the two disagree
                if (!MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var shape)) throw new InvalidOperationException($"継承レシピの形を引けません recipe={recipe.MachineRecipeGuid}");
                return shape;
            }

            #endregion
        }
    }
}
```

`UnlockedMachineRecipeSource.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Network.API;
using Client.Ui.State.Recipe.Data;
using Game.Block.Interface.MachineUpgrade;
using Game.UnlockState;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Recipe.Source
{
    /// <summary>
    /// 解放済みの機械レシピ一覧の源。解放イベントと製作上限の解放で作り直す（Web MachineRecipesTopic の契機と同じ）
    /// Source of the unlocked machine-recipe list; rebuilt on unlock events and craft-cap unlocks (same triggers as Web MachineRecipesTopic)
    /// </summary>
    public sealed class UnlockedMachineRecipeSource : IInitializable
    {
        private readonly IGameUnlockStateData _unlockState;
        private readonly ILevelInheritanceCraftCapLookup _craftCapLookup;
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ReactiveProperty<IReadOnlyList<MachineRecipeData>> _current = new(Array.Empty<MachineRecipeData>());

        public IReadOnlyReactiveProperty<IReadOnlyList<MachineRecipeData>> Current => _current;

        // 解放状態を ctor で受けるので、その購読（ClientGameUnlockStateData）が先に張られ更新後の表を読める
        // Taking the unlock state in the ctor makes its subscription (ClientGameUnlockStateData) register first, so the updated table is read
        public UnlockedMachineRecipeSource(IGameUnlockStateData unlockState, ILevelInheritanceCraftCapLookup craftCapLookup, IVanillaApiEvent vanillaApiEvent)
        {
            _unlockState = unlockState;
            _craftCapLookup = craftCapLookup;
            _vanillaApiEvent = vanillaApiEvent;
        }

        public void Initialize()
        {
            Rebuild();
            _vanillaApiEvent.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => Rebuild());
            _craftCapLookup.OnCraftCapUnlocked.Subscribe(_ => Rebuild());
        }

        private void Rebuild()
        {
            _current.Value = UnlockedMachineRecipeListBuilder.Build(_unlockState, _craftCapLookup);
        }
    }
}
```
（ゲーム寿命のオブジェクトなので購読の Dispose は持たない。AGENTS.md「既知の制約」）

`MachineRecipeSummaryConversion.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Recipe.Data;
using Core.Master;

namespace Client.Ui.State.Recipe.Source
{
    /// <summary>
    /// 機械レシピを選択行が読む要約へ写す（Web の MachineRecipe と同じ形を L0-s4b の BuildRows へ渡す）
    /// Maps machine recipes onto the summaries selection rows read (feeds L0-s4b BuildRows the Web MachineRecipe shape)
    /// </summary>
    public static class MachineRecipeSummaryConversion
    {
        public static IReadOnlyList<MachineRecipeSummary> ToSummaries(IReadOnlyList<MachineRecipeData> recipes)
        {
            var summaries = new List<MachineRecipeSummary>(recipes.Count);
            foreach (var recipe in recipes)
            {
                var outputItems = new List<RecipeOutputItem>();
                foreach (var item in recipe.OutputItems) outputItems.Add(new RecipeOutputItem(item.ItemId, item.Count));
                var outputFluids = new List<RecipeOutputFluid>();
                foreach (var fluid in recipe.OutputFluids) outputFluids.Add(new RecipeOutputFluid(fluid.FluidId, fluid.Amount));
                var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockId).BlockGuid;
                summaries.Add(new MachineRecipeSummary(recipe.RecipeGuid, blockGuid, outputItems, outputFluids));
            }
            return summaries;
        }
    }
}
```
（`ItemAmount.ItemId/Count`・`RecipeFluidAmount.FluidId/Amount` の名前は実物で確かめる）

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.Recipe"`
Expected: `ErrorCount: 0`、新しい 4 件と既存の Recipe 系が PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Source moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Source
git commit -m "feat(uitk): 解放状態つきの機械レシピ一覧の源を State に足す（S4-alpha）

<実行ホストの指示する帰属行>"
```
（Unity が作った `.meta` も同じコミットに入れる。以後のコミットも同じ）

---

### Task 3: パネルの状態・大きさ・閉じる（`Client.Ui.State/BlockInventory/Panel/`・`SectionStack/`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/BlockInventoryPanelState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/BlockInventoryPanelError.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/BlockInventoryPanelStateFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/BlockInventoryPanelModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/IBlockInventoryLargePanelRule.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/BlockInventoryPanelLayout.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/BlockInventoryPanelLayoutModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/IBlockInventoryPanelCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/BlockInventoryPanelCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/SectionStack/SectionStackConfig.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/SectionStack/SectionStackConfigTable.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Routing/IUiStateTransitionRequester.cs`・`UiStateControlTransitionRequester.cs`・`UiStateTransitionRules.cs`（閉じる要求の口。後続の画面も使う。D-12）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel/BlockInventoryPanelStateFactoryTest.cs`・`BlockInventoryPanelLayoutModelTest.cs`・`BlockInventoryPanelCommandsTest.cs`・`SectionStackConfigTableTest.cs`・`BlockRegistryCoverageTest.cs`・`FakePanelSource.cs`・`BlockInventoryTestPanels.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Routing/UiStateTransitionRulesTest.cs`

**Interfaces:**
- Consumes: `SubInventoryState`・`UiRoutingState.Current`（`UiRoutingSnapshot.Screen == UiScreen.SubInventory`）・`OpenBlockDetailTracker`・`BlockComponentRegistry.Resolve`・`TrainSubInventorySource`・`TrainInventoryMessageType`・`InventoryRequestResult`・`UIStateControl.CurrentState / RequestTransition / GetCurrentNestedPauseScreen`（遷移要求の本番実装だけ）
- Produces（後続が使う）:
```csharp
namespace Client.Ui.State.BlockInventory.Panel
// 不変の識別子・エラー・枠だけを持つ union（Client.Game の生オブジェクトは持たない。D-19）
public abstract class BlockInventoryPanelState
{
    public static readonly BlockInventoryPanelState Closed;
    public abstract bool IsSamePanel(BlockInventoryPanelState other);   // 本文を作り直すかの唯一の判定（Web の key=identifier ＋エラーの有無）
}
public sealed class ClosedBlockInventoryPanel : BlockInventoryPanelState { }
public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState
{
    public string BlockType { get; }  public Guid BlockGuid { get; }  public BlockId BlockId { get; }  public BlockInstanceId BlockInstanceId { get; }  public Vector3Int Position { get; }
    public IReadOnlyList<SlotItem> ItemSlots { get; }  public BlockInventoryPanelError Error { get; }  public BlockInventoryComponents Components { get; }
}   // IsSamePanel: 同じ Position・BlockGuid・Error
public sealed class OpenTrainInventoryPanel : BlockInventoryPanelState
{
    public long TrainCarInstanceId { get; }  public IReadOnlyList<SlotItem> ItemSlots { get; }  public BlockInventoryPanelError Error { get; }
}   // IsSamePanel: 同じ TrainCarInstanceId・Error
public enum BlockInventoryPanelError { None, OpenFailed, ContainerMissing, TrainCarMissing }
public interface IBlockInventoryPanelSource { IReadOnlyReactiveProperty<BlockInventoryPanelState> Current { get; } }
public sealed class BlockInventoryPanelModel : IBlockInventoryPanelSource, IInitializable { }
public interface IBlockInventoryLargePanelRule { bool IsLargePanel(OpenBlockInventoryPanel panel); IObservable<Unit> OnRuleInputChanged { get; } }
public enum BlockInventoryPanelSize { Natural, LargeNaturalBody, LargeFilledBody }
public readonly struct BlockInventoryPanelLayout { public static readonly BlockInventoryPanelLayout Natural, LargeNaturalBody, LargeFilledBody; public readonly BlockInventoryPanelSize Size; public bool IsLarge { get; } public bool FillsPanelHeight { get; } }  // ctor は private（D-38）
public sealed class BlockInventoryPanelLayoutModel : IInitializable { public IReadOnlyReactiveProperty<BlockInventoryPanelLayout> Current { get; } }
public interface IBlockInventoryPanelCommands { void Close(); }
namespace Client.Ui.State.Routing
public interface IUiStateTransitionRequester { bool RequestTransition(UIStateEnum requested); }   // 許可表で拒んだら false（理由はログ）
public static class UiStateTransitionRules { public static bool IsAllowed(UIStateEnum current, UIStateEnum requested); }   // Web UiStateActions.IsAllowed の写し
namespace Client.Ui.State.BlockInventory.SectionStack
public abstract class SectionStackItemGrid { public static readonly SectionStackItemGrid Hidden; }
public sealed class HiddenSectionStackItemGrid : SectionStackItemGrid { }
public sealed class ShownSectionStackItemGrid : SectionStackItemGrid { public string GridId { get; }  public bool RenderEmpty { get; } }
public abstract class SectionStackFluid { public static readonly SectionStackFluid Hidden; }
public sealed class HiddenSectionStackFluid : SectionStackFluid { }
public sealed class RowSectionStackFluid : SectionStackFluid { public string RowId { get; }  public bool ShowProgress { get; } }
public sealed class DetailsSectionStackFluid : SectionStackFluid { public string RowId { get; } }
public sealed class SectionStackConfig { public SectionStackItemGrid ItemGrid { get; }  public SectionStackFluid Fluid { get; } }
public static class SectionStackConfigTable { public static SectionStackConfig Resolve(string blockType); }
```

- [ ] **Step 1: 失敗するテストを書く**

`BlockInventoryPanelStateFactoryTest.cs`（テスト用 mod のチェストを使う。5 メソッド・TestCase 展開で 8 件）:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Game.Block.Interface;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// 源の値からパネルの状態を Web ホストの DTO 工場と同じ規則で組む（L0）
    /// Builds the panel state from source values by the same rules as the Web host DTO factories (L0)
    /// </summary>
    public class BlockInventoryPanelStateFactoryTest
    {
        private BlockMasterElement _chest;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _chest = Array.Find(MasterHolder.BlockMaster.Blocks.Data, b => b.BlockType == BlockMasterElement.BlockTypeConst.Chest);
        }

        [Test]
        public void 取得成功のブロックはエラー無しでスロットと構成を持つ()
        {
            var slots = new[] { new SlotItem(new ItemId(1), 7) };
            var state = BlockInventoryPanelStateFactory.FromBlock(true, slots, _chest, new BlockInstanceId(1), new Vector3Int(1, 2, 3));
            var open = (OpenBlockInventoryPanel)state;
            Assert.AreEqual(BlockInventoryPanelError.None, open.Error);
            Assert.AreEqual(_chest.BlockGuid, open.BlockGuid);
            Assert.AreEqual(BlockMasterElement.BlockTypeConst.Chest, open.BlockType);
            Assert.AreEqual(BlockComponentRegistry.Resolve(open.BlockType).Body, open.Components.Body);
            Assert.AreEqual(7, open.ItemSlots[0].Count);
            Assert.AreEqual(new BlockInstanceId(1), open.BlockInstanceId);
        }

        [Test]
        public void 取得失敗のブロックはOpenFailed()
        {
            var state = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(false, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(1), Vector3Int.zero);
            Assert.AreEqual(BlockInventoryPanelError.OpenFailed, state.Error);
        }

        [TestCase(null, BlockInventoryPanelError.None)]
        [TestCase(TrainInventoryMessageType.ContainerMissing, BlockInventoryPanelError.ContainerMissing)]
        [TestCase(TrainInventoryMessageType.TrainCarMissing, BlockInventoryPanelError.TrainCarMissing)]
        [TestCase(TrainInventoryMessageType.OpenFailed, BlockInventoryPanelError.OpenFailed)]
        public void 列車の開け失敗をエラーへ写す(TrainInventoryMessageType? message, BlockInventoryPanelError expected)
        {
            var state = (OpenTrainInventoryPanel)BlockInventoryPanelStateFactory.FromTrain(5, Array.Empty<SlotItem>(), message);
            Assert.AreEqual(expected, state.Error);
            Assert.AreEqual(5, state.TrainCarInstanceId);
        }

        [Test]
        public void 同じ位置と同じブロックは同じパネル()
        {
            var a = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(true, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(1), new Vector3Int(1, 0, 0));
            var b = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(true, new[] { new SlotItem(new ItemId(1), 1) }, _chest, new BlockInstanceId(1), new Vector3Int(1, 0, 0));
            var c = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(true, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(1), new Vector3Int(2, 0, 0));
            Assert.IsTrue(a.IsSamePanel(b));
            Assert.IsFalse(a.IsSamePanel(c));
            Assert.IsFalse(a.IsSamePanel(BlockInventoryPanelState.Closed));
        }

        [Test]
        public void エラーの有無が変われば別のパネルで列車は車両とエラーで決まる()
        {
            var ok = BlockInventoryPanelStateFactory.FromBlock(true, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(1), Vector3Int.zero);
            var failed = BlockInventoryPanelStateFactory.FromBlock(false, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(1), Vector3Int.zero);
            Assert.IsFalse(ok.IsSamePanel(failed));
            var train = BlockInventoryPanelStateFactory.FromTrain(5, Array.Empty<SlotItem>(), null);
            Assert.IsTrue(train.IsSamePanel(BlockInventoryPanelStateFactory.FromTrain(5, new[] { new SlotItem(new ItemId(1), 1) }, null)));
            Assert.IsFalse(train.IsSamePanel(BlockInventoryPanelStateFactory.FromTrain(5, Array.Empty<SlotItem>(), TrainInventoryMessageType.ContainerMissing)));
        }
    }
}
```

`BlockInventoryPanelLayoutModelTest.cs`（2 件。主張は 4 つ: 閉は小・列車は大で高さ確定でない／寄与が無いブロックは小・寄与のどれかが真なら大で高さ確定し、寄与の入力の変化で引き直す）:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    public class BlockInventoryPanelLayoutModelTest
    {
        private sealed class FakeRule : IBlockInventoryLargePanelRule
        {
            public readonly Subject<Unit> Changed = new();
            public bool Large;
            public bool IsLargePanel(OpenBlockInventoryPanel panel) => Large;
            public IObservable<Unit> OnRuleInputChanged => Changed;
        }

        [Test]
        public void 閉じていれば小で列車は大()
        {
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var model = new BlockInventoryPanelLayoutModel(new FakePanelSource(state), Array.Empty<IBlockInventoryLargePanelRule>());
            model.Initialize();
            Assert.IsFalse(model.Current.Value.IsLarge);
            state.Value = BlockInventoryPanelStateFactory.FromTrain(1, Array.Empty<SlotItem>(), null);
            Assert.IsTrue(model.Current.Value.IsLarge);
            Assert.IsFalse(model.Current.Value.FillsPanelHeight);
        }

        [Test]
        public void 寄与のどれかが真なら大で高さ確定し入力の変化で引き直す()
        {
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var rule = new FakeRule();
            var model = new BlockInventoryPanelLayoutModel(new FakePanelSource(state), new IBlockInventoryLargePanelRule[] { new FakeRule(), rule });
            model.Initialize();
            state.Value = BlockInventoryTestPanels.Chest();
            Assert.IsFalse(model.Current.Value.IsLarge);
            rule.Large = true;
            rule.Changed.OnNext(Unit.Default);
            Assert.IsTrue(model.Current.Value.IsLarge);
            Assert.IsTrue(model.Current.Value.FillsPanelHeight);
        }
    }
}
```
（Model の購読面を `IBlockInventoryPanelSource { IReadOnlyReactiveProperty<BlockInventoryPanelState> Current { get; } }` に切り出し、`BlockInventoryPanelModel` がそれを実装する。偽物 `FakePanelSource(ReactiveProperty<BlockInventoryPanelState> state)`〈`Current => state`〉を `Client.Tests/Ui/State/BlockInventory/PanelModel/FakePanelSource.cs` に置き、Task 5・Screens の L1 でも使う。テスト用のパネルの組み立ては `Client.Tests/Ui/State/BlockInventory/PanelModel/BlockInventoryTestPanels.cs`〈`Chest()`・`ChestWithSlots(params SlotItem[])`・`OpenFailedChest()`・`Train(BlockInventoryPanelError)`〉に置き、Screens の L1 からも使う。ファイル一覧に足す）

`BlockInventoryPanelCommandsTest.cs`（2 件: `Close` が遷移要求の口へ GameScreen を 1 回渡す／口が false を返すと Warning ログ〈`LogAssert.Expect`〉。`IUiStateTransitionRequester` の偽物で受ける。D-12）

`UiStateTransitionRulesTest.cs`（2 件: Web `RequestUiStateActionHandler.IsAllowed` の表と同じ〈SubInventory→GameScreen は可・SubInventory→PlayerInventory は不可・同じ状態は可〉を `TestCase` で／`Story`・`Skit` など表に無い現状態からは拒む）

`SectionStackConfigTableTest.cs`（3 件: Chest は `ShownSectionStackItemGrid("chest-grid", RenderEmpty: true)`・液体 Hidden／未知は `Shown("generic-block-grid", false)`・`RowSectionStackFluid("generic-block-fluids", ShowProgress: true)`／FluidPipe は項目グリッド Hidden・`DetailsSectionStackFluid("fluid-pipe-fluid-slots")`）

`BlockRegistryCoverageTest.cs`:
```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// v8 マスタで開ける全 blockType が専用本文か意図的な標準表示のどちらかに分類される（Web の fixture をそのまま読む）
    /// Every openable v8 blockType is classified as a dedicated body or an intentional standard view (reads the Web fixture as-is)
    /// </summary>
    public class BlockRegistryCoverageTest
    {
        private const string FixturePath = "moorestech_web/webui/e2e/fixtures/v8-block-ui-registry.json";
        private static readonly HashSet<string> IntentionalGeneric = new()
        {
            "Chest", "CleanRoomAirFilter", "CleanRoomMachine", "ElectricGenerator", "ElectricMachine", "ElectricMiner", "ElectricPump",
            "FluidPipe", "FuelGearGenerator", "GearMachine", "GearMiner", "GearPump", "GearToElectricGenerator",
        };

        /// <web-test file="e2e/tests/block/blockRegistryCoverage.spec.ts">v8マスタで専用UIを持つ全blockTypeがレジストリまたは意図的Genericに分類される</web-test>
        [Test]
        public void 開ける全blockTypeが登録か意図的な標準表示に分類される()
        {
            var json = File.ReadAllText(Path.Combine(Application.dataPath, "..", "..", FixturePath));
            var openable = JArray.Parse(json).Where(e => (bool)e["openable"]).Select(e => (string)e["blockType"]).Distinct();
            var missing = openable.Where(t => !BlockComponentRegistry.Registered.ContainsKey(t) && !IntentionalGeneric.Contains(t)).ToArray();
            CollectionAssert.IsEmpty(missing);
            Assert.IsFalse(BlockComponentRegistry.Registered.ContainsKey("GearEnergyTransformer"));
        }
    }
}
```
（repo ルートの解き方は既存 `BlockComponentRegistryTest` の `WebRegisteredBlockTypesPath` の読み方に揃える。`Newtonsoft.Json` が `Client.Tests` の asmdef 参照に無ければ、同じテストの読み方〈正規表現〉に合わせて `"blockType": "(\w+)", "openable": true` を拾う）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: Panel の型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`BlockInventoryPanelState.cs`（識別子・エラー・枠だけの不変 union。区画・本文の詳細は持ち主のレーンの State の詳細 Model が `OpenBlockDetailTracker` 等を購読して配る。D-19）:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// ブロックインベントリのパネルの状態（閉・ブロック・列車の union。Web の BlockInventoryData の写し）
    /// Block-inventory panel state: a union of closed, block and train (port of the Web BlockInventoryData)
    /// </summary>
    public abstract class BlockInventoryPanelState
    {
        public static readonly BlockInventoryPanelState Closed = new ClosedBlockInventoryPanel();

        private protected BlockInventoryPanelState()
        {
        }

        // 本文を作り直すかの唯一の判定（Web の key=identifier にエラーの有無を足す）
        // The single rule for rebuilding the body (Web key=identifier plus error presence)
        public abstract bool IsSamePanel(BlockInventoryPanelState other);
    }

    public sealed class ClosedBlockInventoryPanel : BlockInventoryPanelState
    {
        internal ClosedBlockInventoryPanel()
        {
        }

        public override bool IsSamePanel(BlockInventoryPanelState other)
        {
            return other is ClosedBlockInventoryPanel;
        }
    }

    public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState
    {
        public string BlockType { get; private set; }
        public Guid BlockGuid { get; private set; }
        public BlockId BlockId { get; private set; }
        public BlockInstanceId BlockInstanceId { get; private set; }
        public Vector3Int Position { get; private set; }
        public IReadOnlyList<SlotItem> ItemSlots { get; private set; }
        public BlockInventoryPanelError Error { get; private set; }
        public BlockInventoryComponents Components { get; private set; }

        internal OpenBlockInventoryPanel(string blockType, Guid blockGuid, BlockId blockId, BlockInstanceId blockInstanceId, Vector3Int position, IReadOnlyList<SlotItem> itemSlots, BlockInventoryPanelError error)
        {
            BlockType = blockType;
            BlockGuid = blockGuid;
            BlockId = blockId;
            BlockInstanceId = blockInstanceId;
            Position = position;
            ItemSlots = itemSlots;
            Error = error;
            Components = BlockComponentRegistry.Resolve(blockType);
        }

        public override bool IsSamePanel(BlockInventoryPanelState other)
        {
            return other is OpenBlockInventoryPanel block && Position == block.Position && BlockGuid == block.BlockGuid && Error == block.Error;
        }
    }

    public sealed class OpenTrainInventoryPanel : BlockInventoryPanelState
    {
        public long TrainCarInstanceId { get; private set; }
        public IReadOnlyList<SlotItem> ItemSlots { get; private set; }
        public BlockInventoryPanelError Error { get; private set; }

        internal OpenTrainInventoryPanel(long trainCarInstanceId, IReadOnlyList<SlotItem> itemSlots, BlockInventoryPanelError error)
        {
            TrainCarInstanceId = trainCarInstanceId;
            ItemSlots = itemSlots;
            Error = error;
        }

        public override bool IsSamePanel(BlockInventoryPanelState other)
        {
            return other is OpenTrainInventoryPanel train && TrainCarInstanceId == train.TrainCarInstanceId && Error == train.Error;
        }
    }
}
```

`BlockInventoryPanelError.cs`:
```csharp
using System;
using Client.Localization;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// パネルの取得失敗（Web の error: openFailed / containerMissing / trainCarMissing）
    /// Panel fetch failures (Web error: openFailed / containerMissing / trainCarMissing)
    /// </summary>
    public enum BlockInventoryPanelError
    {
        None,
        OpenFailed,
        ContainerMissing,
        TrainCarMissing,
    }

    public static class BlockInventoryPanelErrorText
    {
        // Web BlockInventoryPanel の TrainErrorKeys と同じ対応
        // Same mapping as TrainErrorKeys in the Web BlockInventoryPanel
        // None は false（エラー行を出さない）。外殻のエラー行の出し入れはこの戻り値 1 本で決める（D-36）
        // None gives false (no error row); the shell's error row is shown or hidden by this return value alone (D-36)
        public static bool TryGetLocalizationKey(BlockInventoryPanelError error, out LocalizationKey key)
        {
            switch (error)
            {
                case BlockInventoryPanelError.None:
                    key = default;
                    return false;
                case BlockInventoryPanelError.OpenFailed:
                    key = LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed;
                    return true;
                case BlockInventoryPanelError.ContainerMissing:
                    key = LocalizationKeys.Ui.BlockInventory.TrainNoItemContainer;
                    return true;
                case BlockInventoryPanelError.TrainCarMissing:
                    key = LocalizationKeys.Ui.BlockInventory.TrainNotFound;
                    return true;
                default:
                    // enum に値を足して文言を足し忘れた開発時の誤り（空の文で出さない）
                    // A dev-time mistake: a new enum value without text (never show an empty row)
                    throw new ArgumentOutOfRangeException(nameof(error), error, null);
            }
        }
    }
}
```
（`LocalizationKeys` の名前空間と生成名は `UiCommandRejectReason.cs` の `using` に揃える）

`BlockInventoryPanelStateFactory.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Ui.State.Items;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// 源の値からパネルの状態を組む（Web ホストの BlockInventoryDtoFactory・TrainInventoryDtoFactory の規則の写し）
    /// Builds panel states from source values (port of the Web host BlockInventoryDtoFactory and TrainInventoryDtoFactory rules)
    /// </summary>
    public static class BlockInventoryPanelStateFactory
    {
        public static BlockInventoryPanelState FromBlock(bool fetchSucceeded, IReadOnlyList<SlotItem> itemSlots, BlockMasterElement master, BlockInstanceId blockInstanceId, Vector3Int position)
        {
            var error = fetchSucceeded ? BlockInventoryPanelError.None : BlockInventoryPanelError.OpenFailed;
            var blockId = MasterHolder.BlockMaster.GetBlockId(master.BlockGuid);
            return new OpenBlockInventoryPanel(master.BlockType, master.BlockGuid, blockId, blockInstanceId, position, itemSlots, error);
        }

        public static BlockInventoryPanelState FromTrain(long trainCarInstanceId, IReadOnlyList<SlotItem> itemSlots, TrainInventoryMessageType? lastOpenMessage)
        {
            // 未知の理由は OpenFailed に倒す（ホストの ResolveError と同じ）
            // Unknown reasons fall to OpenFailed (same as the host ResolveError)
            var error = lastOpenMessage switch
            {
                null => BlockInventoryPanelError.None,
                TrainInventoryMessageType.ContainerMissing => BlockInventoryPanelError.ContainerMissing,
                TrainInventoryMessageType.TrainCarMissing => BlockInventoryPanelError.TrainCarMissing,
                _ => BlockInventoryPanelError.OpenFailed,
            };
            return new OpenTrainInventoryPanel(trainCarInstanceId, itemSlots, error);
        }

        public static IReadOnlyList<SlotItem> ToSlotItems(IReadOnlyList<IItemStack> stacks)
        {
            var slots = new SlotItem[stacks.Count];
            for (var i = 0; i < slots.Length; i++) slots[i] = new SlotItem(stacks[i].Id, stacks[i].Count);
            return slots;
        }
    }
}
```

`BlockInventoryPanelModel.cs`（`IBlockInventoryPanelSource` も同じファイルの先頭に置く。200 行以内）:
```csharp
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.Routing;
using Server.Protocol.PacketResponse;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Panel
{
    // パネルの状態を読む面（Model とテストの偽物が実装する）
    // Read surface of the panel state (implemented by the model and test fakes)
    public interface IBlockInventoryPanelSource
    {
        IReadOnlyReactiveProperty<BlockInventoryPanelState> Current { get; }
    }

    /// <summary>
    /// SubInventory の源と追跡ブロックを購読してパネルの状態を配る（Web ホスト BlockInventoryTopic の開閉と再計算の契機の写し）
    /// Subscribes to the sub-inventory sources and tracked block to publish the panel state (port of the Web host BlockInventoryTopic open rule and triggers)
    /// </summary>
    public sealed class BlockInventoryPanelModel : IBlockInventoryPanelSource, IInitializable
    {
        private readonly UiRoutingState _routing;
        private readonly SubInventoryState _subInventoryState;
        private readonly OpenBlockDetailTracker _tracker;
        private readonly ReactiveProperty<BlockInventoryPanelState> _current = new(BlockInventoryPanelState.Closed);

        public IReadOnlyReactiveProperty<BlockInventoryPanelState> Current => _current;

        public BlockInventoryPanelModel(UiRoutingState routing, SubInventoryState subInventoryState, OpenBlockDetailTracker tracker)
        {
            _routing = routing;
            _subInventoryState = subInventoryState;
            _tracker = tracker;
        }

        public void Initialize()
        {
            // 画面の切替・スロット更新・追跡替え・追跡ブロックの状態変化で組み直す（ホストと同じ契機。区画の詳細は各レーンの詳細 Model が配る）
            // Rebuild on screen changes, slot updates, tracking changes and tracked-block state changes (host triggers; section details come from each lane's detail model)
            _routing.Current.Subscribe(_ => Rebuild());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockChanged.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            _current.Value = Read();

            #region Internal

            BlockInventoryPanelState Read()
            {
                // 画面の判定は UiRoutingState に一本化する。SubInventory かつサブインベントリがあるときだけ開く
                // Screen decisions go through UiRoutingState only; open only in SubInventory with a sub-inventory present
                var sub = _subInventoryState.CurrentSubInventory;
                if (_routing.Current.Value.Screen != UiScreen.SubInventory || sub == null) return BlockInventoryPanelState.Closed;
                var slots = BlockInventoryPanelStateFactory.ToSlotItems(sub.SubInventory);
                if (_subInventoryState.CurrentSubInventorySource is TrainSubInventorySource train) return BlockInventoryPanelStateFactory.FromTrain(train.TrainCarInstanceId, slots, train.LastOpenMessage);

                // silent-ok: 追跡ブロックが未解決（撤去直後など）は閉と同じ見え方（ホストと同じ） / 追跡替えの購読で直ちに組み直す
                var block = _tracker.TrackedBlock;
                if (block == null) return BlockInventoryPanelState.Closed;
                var succeeded = sub.Result == InventoryRequestResult.Success;
                return BlockInventoryPanelStateFactory.FromBlock(succeeded, slots, block.BlockMasterElement, block.BlockInstanceId, _tracker.TrackedSource.BlockPosition);
            }

            #endregion
        }
    }
}
```
（`sub.SubInventory` は `List<IItemStack>` で `IReadOnlyList<IItemStack>` として渡せる。`InventoryRequestResult` の名前空間は実物で確かめる。`UiRoutingState` は `MainGameUiStateRegistration.cs:53` で登録済み。同じ値の再代入は `ReactiveProperty` が流さないので、状態は毎回新しいインスタンスにして流す〈`SetValueAndForceNotify` は使わない。新インスタンスは参照が違うので必ず流れる〉）

`IBlockInventoryLargePanelRule.cs`・`BlockInventoryPanelLayout.cs`・`BlockInventoryPanelLayoutModel.cs`:
```csharp
using System;
using UniRx;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// 大型パネル判定の寄与の口（Web isLargeMachinePanel を中立名で。どの blockType を大型にするかは寄与する側が持つ。D-16）
    /// Contribution port for the large-panel decision (Web isLargeMachinePanel under a neutral name; the contributor owns which block types go large, D-16)
    /// </summary>
    public interface IBlockInventoryLargePanelRule
    {
        // 開いているブロックを大型パネル（本文の高さ確定）にするか
        // Whether the open block uses the large panel (with a fixed body height)
        bool IsLargePanel(OpenBlockInventoryPanel panel);

        // 判定の入力（レシピの解放・選択など）が変わったら流す
        // Fires when the decision's inputs (recipe unlocks, selection, ...) change
        IObservable<Unit> OnRuleInputChanged { get; }
    }
}
```
```csharp
namespace Client.Ui.State.BlockInventory.Panel
{
    // パネルの大きさの 3 状態（判別子）。大型で本文の高さが自然＝列車、確定＝寄与の大型
    // The three panel sizes (discriminator): large with natural body = train, large with filled body = contributed large
    public enum BlockInventoryPanelSize
    {
        Natural,
        LargeNaturalBody,
        LargeFilledBody,
    }

    /// <summary>
    /// パネルの大きさ（Web の isLargePanel と isLargeMachinePanel＝本文の高さ確定）。作れるのは 3 つの static だけで (小型, 高さ確定) は作れない
    /// Panel size (Web isLargePanel, and isLargeMachinePanel as the body's fixed height); only the three statics exist, so (small, filled) cannot be made
    /// </summary>
    public readonly struct BlockInventoryPanelLayout
    {
        public static readonly BlockInventoryPanelLayout Natural = new(BlockInventoryPanelSize.Natural);
        public static readonly BlockInventoryPanelLayout LargeNaturalBody = new(BlockInventoryPanelSize.LargeNaturalBody);
        public static readonly BlockInventoryPanelLayout LargeFilledBody = new(BlockInventoryPanelSize.LargeFilledBody);

        public readonly BlockInventoryPanelSize Size;
        public bool IsLarge => Size != BlockInventoryPanelSize.Natural;
        public bool FillsPanelHeight => Size == BlockInventoryPanelSize.LargeFilledBody;

        private BlockInventoryPanelLayout(BlockInventoryPanelSize size)
        {
            Size = size;
        }
    }
}
```
```csharp
using System.Collections.Generic;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// パネルの状態と寄与から大きさを配る。列車は常に大、ブロックは寄与のどれかが真なら大で本文の高さを確定する
    /// Publishes the panel size from the state and contributions; trains are always large, blocks are large with a fixed body height when any rule says so
    /// </summary>
    public sealed class BlockInventoryPanelLayoutModel : IInitializable
    {
        private readonly IBlockInventoryPanelSource _panel;
        private readonly IReadOnlyList<IBlockInventoryLargePanelRule> _rules;
        private readonly ReactiveProperty<BlockInventoryPanelLayout> _current = new(BlockInventoryPanelLayout.Natural);

        public IReadOnlyReactiveProperty<BlockInventoryPanelLayout> Current => _current;   // 型を狭める公開（前例 SettingsModel.Current）

        public BlockInventoryPanelLayoutModel(IBlockInventoryPanelSource panel, IReadOnlyList<IBlockInventoryLargePanelRule> rules)
        {
            _panel = panel;
            _rules = rules;
        }

        public void Initialize()
        {
            _panel.Current.Subscribe(_ => Recompute());
            foreach (var rule in _rules) rule.OnRuleInputChanged.Subscribe(_ => Recompute());
        }

        private void Recompute()
        {
            _current.Value = _panel.Current.Value switch
            {
                OpenTrainInventoryPanel => BlockInventoryPanelLayout.LargeNaturalBody,
                OpenBlockInventoryPanel block => AnyLarge(block) ? BlockInventoryPanelLayout.LargeFilledBody : BlockInventoryPanelLayout.Natural,
                _ => BlockInventoryPanelLayout.Natural,
            };

            #region Internal

            bool AnyLarge(OpenBlockInventoryPanel block)
            {
                foreach (var rule in _rules)
                {
                    if (rule.IsLargePanel(block)) return true;
                }
                return false;
            }

            #endregion
        }
    }
}
```
（構造体は既定の等値なので同じ大きさの再代入は流れない。View は大きさの変化だけで寸法を当て直せばよい）

`IBlockInventoryPanelCommands.cs`・`BlockInventoryPanelCommands.cs`（D-12: 閉じるは `Client.Ui.State/Routing/` の遷移要求の口 `IUiStateTransitionRequester` を通す。口は後続の画面〈持ち物・研究・チャレンジの閉じる〉も使う。D-12・D-29）:
```csharp
namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// ブロックインベントリのパネルの操作（閉じる＝Web の ui_state.request gameScreen）
    /// Block-inventory panel commands (close = the Web ui_state.request gameScreen)
    /// </summary>
    public interface IBlockInventoryPanelCommands
    {
        void Close();
    }
}
```
```csharp
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Routing;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Panel
{
    public sealed class BlockInventoryPanelCommands : IBlockInventoryPanelCommands
    {
        private readonly IUiStateTransitionRequester _requester;

        public BlockInventoryPanelCommands(IUiStateTransitionRequester requester)
        {
            _requester = requester;
        }

        public void Close()
        {
            // 許可表で拒まれた閉じるは何もしない。理由は開発者ログへ（口の側でも現状態をログに出す）
            // A close refused by the allow table does nothing; the reason goes to the developer log (the port logs the current state too)
            if (!_requester.RequestTransition(UIStateEnum.GameScreen)) Debug.LogWarning("[BlockInventoryPanelCommands] close refused by UiStateTransitionRules");
        }
    }
}
```
`Client.Ui.State/Routing/IUiStateTransitionRequester.cs`・`UiStateTransitionRules.cs`・`UiStateControlTransitionRequester.cs`（Web ホスト `RequestUiStateActionHandler.RequestState` の現状態の検査と入れ子ポーズの扱いを 1 本に写す。Web の文字列の検査〈GameScreen / PlayerInventory 以外は unsupported_state〉は型で閉じているので写さない）:
```csharp
using Client.Game.InGame.UI.UIState;

namespace Client.Ui.State.Routing
{
    // UITK 起点の UI 状態の遷移要求の口。拒んだら false（理由は実装がログへ出す）
    // Port for UITK-initiated UI-state transition requests; false when refused (the implementation logs why)
    public interface IUiStateTransitionRequester
    {
        bool RequestTransition(UIStateEnum requested);
    }
}
```
```csharp
using Client.Game.InGame.UI.UIState;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 現状態から要求できる遷移の表（Web ホスト RequestUiStateActionHandler.IsAllowed の写し。重複は D-5 と同じ扱い）
    /// Table of transitions requestable from the current state (port of the Web host IsAllowed; duplication handled as in D-5)
    /// </summary>
    public static class UiStateTransitionRules
    {
        public static bool IsAllowed(UIStateEnum current, UIStateEnum requested)
        {
            if (current == requested) return true;
            return current switch
            {
                UIStateEnum.GameScreen => requested == UIStateEnum.PlayerInventory,
                UIStateEnum.PlayerInventory => requested == UIStateEnum.GameScreen,
                UIStateEnum.SubInventory => requested == UIStateEnum.GameScreen,
                UIStateEnum.BuildMenu => requested == UIStateEnum.GameScreen,
                UIStateEnum.ResearchTree => requested == UIStateEnum.GameScreen,
                UIStateEnum.ChallengeList => requested == UIStateEnum.GameScreen,
                UIStateEnum.PauseMenu => requested == UIStateEnum.GameScreen,
                _ => false,
            };
        }
    }
}
```
```csharp
using Client.Game.InGame.UI.UIState;
using UnityEngine;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 遷移要求を UIStateControl へ渡す本番実装。入れ子ポーズの画面では入れ子だけを閉じ、表に無い遷移は拒む（Web ホストと同じ順）
    /// Production requester forwarding to UIStateControl; on nested-pause screens it closes only the nested pause, and refuses transitions outside the table (same order as the Web host)
    /// </summary>
    public sealed class UiStateControlTransitionRequester : IUiStateTransitionRequester
    {
        private readonly UIStateControl _uiStateControl;

        public UiStateControlTransitionRequester(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }

        public bool RequestTransition(UIStateEnum requested)
        {
            // 入れ子ポーズを持つ画面の GameScreen 要求は入れ子だけを閉じる（ADR 0035）
            // A GameScreen request on a nested-pause screen closes only the nested pause (ADR 0035)
            if (requested == UIStateEnum.GameScreen && _uiStateControl.GetCurrentNestedPauseScreen() is { } nested) return nested.RequestClosePauseMenu() || Refuse("nested pause had nothing to close");

            if (!UiStateTransitionRules.IsAllowed(_uiStateControl.CurrentState, requested)) return Refuse($"{_uiStateControl.CurrentState} -> {requested} is not allowed");
            _uiStateControl.RequestTransition(requested);
            return true;
        }

        private static bool Refuse(string reason)
        {
            Debug.LogWarning($"[UiStateControlTransitionRequester] transition refused: {reason}");
            return false;
        }
    }
}
```

`SectionStackConfig.cs`・`SectionStackConfigTable.cs`（Web `SectionStackView.tsx` の `configByBlockType` と `genericConfig` を 1 行ずつ写す。null と bool の組を判別 union に替える〈D-20。前例 `CleanRoomYieldLogic` の `RowId`〉。キーは `BlockMasterElement.BlockTypeConst.*`）:
```csharp
namespace Client.Ui.State.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の blockType ごとの設定。項目グリッドと液体の出し方をそれぞれ判別 union で持つ（Web SectionStackViewConfig の写し）
    /// Per-blockType standard-body settings; the item grid and fluids each use a discriminated union (port of Web SectionStackViewConfig)
    /// </summary>
    public sealed class SectionStackConfig
    {
        public SectionStackItemGrid ItemGrid { get; private set; }
        public SectionStackFluid Fluid { get; private set; }

        public SectionStackConfig(SectionStackItemGrid itemGrid, SectionStackFluid fluid)
        {
            ItemGrid = itemGrid;
            Fluid = fluid;
        }
    }

    // 項目グリッド: 出さない／出す（GridId は Web の itemGridTestId と同じ綴り。RenderEmpty は空でも出すか）
    // Item grid: hidden, or shown (GridId spells the Web itemGridTestId; RenderEmpty draws it even when empty)
    public abstract class SectionStackItemGrid
    {
        public static readonly SectionStackItemGrid Hidden = new HiddenSectionStackItemGrid();
        private protected SectionStackItemGrid() { }
    }

    public sealed class HiddenSectionStackItemGrid : SectionStackItemGrid
    {
        internal HiddenSectionStackItemGrid() { }
    }

    public sealed class ShownSectionStackItemGrid : SectionStackItemGrid
    {
        public string GridId { get; private set; }
        public bool RenderEmpty { get; private set; }

        public ShownSectionStackItemGrid(string gridId, bool renderEmpty)
        {
            GridId = gridId;
            RenderEmpty = renderEmpty;
        }
    }

    // 液体: 出さない／行（機械以外。ShowProgress は進捗を出すか）／詳細（Web showFluidDetails）
    // Fluids: hidden, a row (non-machines; ShowProgress draws progress), or details (Web showFluidDetails)
    public abstract class SectionStackFluid
    {
        public static readonly SectionStackFluid Hidden = new HiddenSectionStackFluid();
        private protected SectionStackFluid() { }
    }

    public sealed class HiddenSectionStackFluid : SectionStackFluid
    {
        internal HiddenSectionStackFluid() { }
    }

    public sealed class RowSectionStackFluid : SectionStackFluid
    {
        public string RowId { get; private set; }
        public bool ShowProgress { get; private set; }

        public RowSectionStackFluid(string rowId, bool showProgress)
        {
            RowId = rowId;
            ShowProgress = showProgress;
        }
    }

    public sealed class DetailsSectionStackFluid : SectionStackFluid
    {
        public string RowId { get; private set; }

        public DetailsSectionStackFluid(string rowId)
        {
            RowId = rowId;
        }
    }
}
```
（1 ファイル 200 行に収まらなければ `SectionStackItemGrid.cs`・`SectionStackFluid.cs` に割る〈`SectionStack/` は 4 ファイル〉）
```csharp
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using T = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockTypeConst;

namespace Client.Ui.State.BlockInventory.SectionStack
{
    /// <summary>
    /// blockType → 標準の本文の設定（Web configByBlockType と genericConfig の 1 対 1 の写し）
    /// blockType → standard-body settings (one-to-one port of Web configByBlockType and genericConfig)
    /// </summary>
    public static class SectionStackConfigTable
    {
        public static readonly SectionStackConfig Generic = new(Grid("generic-block-grid", false), Row("generic-block-fluids", true));

        private static readonly Dictionary<string, SectionStackConfig> ByBlockType = new()
        {
            [T.FluidPipe] = new SectionStackConfig(SectionStackItemGrid.Hidden, new DetailsSectionStackFluid("fluid-pipe-fluid-slots")),
            [T.Chest] = new SectionStackConfig(Grid("chest-grid", true), SectionStackFluid.Hidden),
            [T.ElectricMachine] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("machine-fluid-slots", false)),
            [T.GearMachine] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("machine-fluid-slots", false)),
            [T.ElectricGenerator] = new SectionStackConfig(Grid("generator-fuel-grid", false), Row("generator-fluid-slots", false)),
            [T.FuelGearGenerator] = new SectionStackConfig(Grid("generator-fuel-grid", true), SectionStackFluid.Hidden),
            [T.SimpleGearGenerator] = new SectionStackConfig(Grid("generator-fuel-grid", true), SectionStackFluid.Hidden),
            [T.GearToElectricGenerator] = new SectionStackConfig(SectionStackItemGrid.Hidden, SectionStackFluid.Hidden),
            [T.ElectricMiner] = new SectionStackConfig(Grid("miner-output-grid", true), SectionStackFluid.Hidden),
            [T.GearMiner] = new SectionStackConfig(Grid("gear-miner-output-grid", true), SectionStackFluid.Hidden),
            [T.ElectricPump] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("pump-fluid-slots", false)),
            [T.GearPump] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("pump-fluid-slots", false)),
            [T.CleanRoomMachine] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("machine-fluid-slots", false)),
            [T.CleanRoomAirFilter] = new SectionStackConfig(Grid("clean-room-filter-grid", true), SectionStackFluid.Hidden),
        };

        public static SectionStackConfig Resolve(string blockType)
        {
            return ByBlockType.TryGetValue(blockType, out var config) ? config : Generic;
        }

        private static SectionStackItemGrid Grid(string gridId, bool renderEmpty) => new ShownSectionStackItemGrid(gridId, renderEmpty);
        private static SectionStackFluid Row(string rowId, bool showProgress) => new RowSectionStackFluid(rowId, showProgress);
    }
}
```
（Web で `fluidRowTestId` が null の行の `showFluidProgress` は描かれないので落とす。`BlockTypeConst` に無い名前があればコンパイルで分かるので、Web の文字列と同じ定数に読み替える。無ければ文字列で書き、起草メモの照合に足す）

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.BlockInventory|Client.Tests.Ui.State.Routing"`
Expected: `ErrorCount: 0`、新しい件と L0-s4b の既存件が PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory moorestech_client/Assets/Scripts/Client.Ui.State/Routing moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Routing
git commit -m "feat(uitk): ブロックインベントリのパネルの状態・大きさの判定・閉じるを State に足す（S4-alpha）

<実行ホストの指示する帰属行>"
```

---
### Task 4: ブロック側の実行器とブロック側のスロット操作（`Client.Ui.State/SlotCommands/`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/IBlockInventoryOperations.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/IRawSubInventorySlots.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/SubInventoryRawSlots.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/BlockSlotReader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/LocalBlockInventoryOperations.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/BlockItemAction.cs`（行為の基底と 3 種）
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/BlockItemActionConversion.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/BlockItemActionExecutor.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/IBlockSlotCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/BlockSlotCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/PlannedItemActionRunner.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/KnownItemSlots.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs`（enum と `IsBenign`・`ToLocalizationKey`・`ToWireCode` の末尾に `BlockSlotUnavailable`）
- Modify: `Localization/localization.csv`（`ui.commandRejected.blockSlotUnavailable`）＋ `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` の再生成と `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の印
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/PauseMenuCommandsTest.cs`（`良性の拒否理由は…` の `benign` 配列に `UiCommandRejectReason.BlockSlotUnavailable` を足し、名前を `良性の拒否理由は言語の選択済みと持ち物の移動と分割とブロックの枠の取りこぼしだけ` に直す）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/SlotCommands/RecordingBlockInventoryOperations.cs`・`BlockItemActionExecutorTest.cs`・`BlockSlotCommandsTest.cs`・`BlockSlotReaderTest.cs`・`FixedRawSubInventorySlots.cs`

**Interfaces:**
- Consumes: `LocalPlayerInventoryController.MoveItem/CollectItems/GrabInventory/LocalPlayerInventory.MainSlotCount`、Task 3 の `IBlockInventoryPanelSource`（開いているか）、`SubInventoryState.CurrentSubInventory.SubInventory`（枠の生の値。`SubInventoryRawSlots` だけが読む）、C4-e の `IPlayerInventoryOperations`・`PlayerItemActionExecutor`・`PlayerItemActionConversion.TryConvert`、L0-s4a の `BlockSlotPlanner`・`PlannedItemAction`、`IItemStackLevelLookup`
- Produces（後続が使う）:
```csharp
namespace Client.Ui.State.SlotCommands.Block
public interface IBlockSlotCommands
{
    UiCommandResult LeftDown(int blockSlot, bool shift);   // Web useBlockSlotGestures.onLeftDown
    UiCommandResult RightDown(int blockSlot);              // onRightDown
    UiCommandResult DoubleClick(int blockSlot);            // onDoubleClick
}
public interface IBlockInventoryOperations
{
    bool TryReadBlockSlots(out IReadOnlyList<SlotItem> slots);      // 開閉は IBlockInventoryPanelSource（エラー無しのブロック／列車でなければ false）、枠は押下の瞬間の SubInventoryState の生の値
    PlayerItemMoveOutcome Move(ItemMoveSlotRef from, ItemMoveSlotRef to, int count);  // 片側が Block の移動
    void CollectToGrab();
    void CollectToBlockSlot(int blockSlot);
}
public abstract class BlockItemAction { }   // 派生は BlockMoveItemAction・BlockSplitToGrabAction・BlockCollectAction だけ（外では派生できない）
public static class BlockItemActionConversion { public static bool TryConvert(PlannedItemAction action, out BlockItemAction blockAction); }  // PlayerItemActionConversion の対
public sealed class BlockItemActionExecutor { public UiCommandResult Execute(BlockItemAction action); }
namespace Client.Ui.State.SlotCommands
public sealed class PlannedItemActionRunner { public UiCommandResult Execute(IReadOnlyList<PlannedItemAction> actions); } // プレイヤーへ写す → ブロックへ写す → どちらも不可は Error ログ＋拒否。最初の拒否で止める
public const string BlockSlotCommandName = "block_inventory.slot";   // BlockSlotCommands.CommandName。トーストの名前
```

- [ ] **Step 1: 拒否の理由を足す**

`UiCommandRejectReason` の enum 末尾に `BlockSlotUnavailable,`、`IsBenign` に `UiCommandRejectReason.BlockSlotUnavailable => true,`（操作の瞬間にパネルが閉じた・枠が減った古いクリック。プレイヤーには閉じたのが見えている）、`ToLocalizationKey` に `LocalizationKeys.Ui.CommandRejected.BlockSlotUnavailable`、`ToWireCode` に `"invalid_slot"`（Web ホストの `BlockAreaSlotParser` の失敗と同じ）。`localization.csv` の `ui.commandRejected.*` の末尾に 1 行（英「The block slot is no longer available」・日「ブロックの枠がもうありません」。列の並びは既存の `ui.commandRejected.splitDragNoValidSlots` 行に揃える）。続けて `pnpm --dir moorestech_web/webui run gen:i18n` で `localizationKeys.ts` を作り直し、Editor の SchemaWatcher が書き換えた `_CompileRequester.cs` と一緒にこの Task のコミットへ入れる（台帳「## 追従記録」への記帳は Task 9 Step 2）

同じ Step で `PauseMenuCommandsTest.cs` の網羅テストの `benign` 配列に `UiCommandRejectReason.BlockSlotUnavailable` を足し、メソッド名を上の Files のとおりに直す（直さないと全列挙の照合で赤になる）

- [ ] **Step 2: 失敗するテストを書く**

`RecordingBlockInventoryOperations.cs`（ブロックの枠を持ち、移動と集めを記録する。移動は既定では記録だけで、`WriteThrough` のときだけブロック側の枠へ即時に書く）:
```csharp
using System.Collections.Generic;
using Core.Master;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands.Block;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// ブロック側の口の記録係。閉じた状態と移動の結果を差し替えられる
    /// Recorder for the block-side port; the closed state and move outcomes can be replaced
    /// </summary>
    public sealed class RecordingBlockInventoryOperations : IBlockInventoryOperations
    {
        public readonly List<(ItemMoveSlotRef From, ItemMoveSlotRef To, int Count)> Moves = new();
        public readonly List<string> Collects = new();
        public SlotItem[] Slots = new SlotItem[0];
        public bool Open = true;
        public PlayerItemMoveOutcome NextOutcome = PlayerItemMoveOutcome.Moved;
        public bool WriteThrough;              // 真なら移動を Slots に即時に書く（コントローラの先書きの再現。D-37）
        public ItemId WriteThroughItemId;      // 先書きで枠へ入るアイテム

        public bool TryReadBlockSlots(out IReadOnlyList<SlotItem> slots)
        {
            slots = Slots;
            return Open;
        }

        public PlayerItemMoveOutcome Move(ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            Moves.Add((from, to, count));
            if (WriteThrough) ApplyToSlots(from, to, count);
            return NextOutcome;
        }

        // ブロックの枠の増減だけを書く（grab・main 側は呼び手の記録係が持つ）
        // Writes only the block-slot side (grab/main are owned by the caller's recorder)
        private void ApplyToSlots(ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            var copy = (SlotItem[])Slots.Clone();
            if (from.Area == ItemMoveArea.Block) copy[from.Slot] = new SlotItem(copy[from.Slot].ItemId, copy[from.Slot].Count - count);
            if (to.Area == ItemMoveArea.Block) copy[to.Slot] = new SlotItem(WriteThroughItemId, copy[to.Slot].Count + count);
            Slots = copy;
        }

        public void CollectToGrab() => Collects.Add("grab");
        public void CollectToBlockSlot(int blockSlot) => Collects.Add($"block:{blockSlot}");
    }
}
```

`BlockSlotCommandsTest.cs`（Web `blockInventoryGestures` 1〜4 件目と `blockInventory.spec` 2 件目の操作面。プレイヤー側は C4-e の `RecordingPlayerInventoryOperations`、最大スタックは L0-s4a の `FixedMaxStackLookup`）:
```csharp
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.ItemMove;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands;
using Client.Ui.State.SlotCommands.Block;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// ブロックの枠の押下を Web の useBlockSlotGestures と同じ計画で実行器へ渡す（L0）
    /// Routes block-slot presses through the same plans as Web useBlockSlotGestures to the executors (L0)
    /// </summary>
    public class BlockSlotCommandsTest
    {
        private RecordingPlayerInventoryOperations _player;
        private RecordingBlockInventoryOperations _block;
        private BlockSlotCommands _commands;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(System.Guid.Parse("00000000-0000-0000-1234-000000000001"));
            _player = new RecordingPlayerInventoryOperations(36, 2);
            _block = new RecordingBlockInventoryOperations { Slots = new[] { new SlotItem(_wood, 7), new SlotItem(_wood, 3) } };
            var runner = new PlannedItemActionRunner(new PlayerItemActionExecutor(_player), new BlockItemActionExecutor(_block, _player));
            _commands = new BlockSlotCommands(_block, _player, runner, new FixedMaxStackLookup(64));
        }

        /// <web-test file="e2e/tests/block/blockInventory.spec.ts">filled スロット左クリックで block→grab の move_item を送り、grab オーバーレイが出る</web-test>
        [Test]
        public void 中身のある枠の左押下は全量を掴む()
        {
            _commands.LeftDown(0, false);
            Assert.AreEqual((ItemMoveSlotRef.Block(0), ItemMoveSlotRef.Grab, 7), _block.Moves[0]);
        }

        /// <web-test file="e2e/tests/block/blockInventoryGestures.spec.ts">空手の右クリックで block_inventory.split を送り grab に半分入る</web-test>
        [Test]
        public void 空手の右押下は半分を掴む()
        {
            _commands.RightDown(0);
            Assert.AreEqual((ItemMoveSlotRef.Block(0), ItemMoveSlotRef.Grab, 3), _block.Moves[0]);
        }

        /// <web-test file="e2e/tests/block/blockInventoryGestures.spec.ts">grab 保持中の右クリックで block スロットへ1個置く</web-test>
        [Test]
        public void 掴み中の右押下は1個置く()
        {
            _player.SetGrab(new SlotItem(_wood, 7));
            _commands.RightDown(1);
            Assert.AreEqual((ItemMoveSlotRef.Grab, ItemMoveSlotRef.Block(1), 1), _block.Moves[0]);
        }

        /// <web-test file="e2e/tests/block/blockInventoryGestures.spec.ts">ダブルクリックで block_inventory.collect を送る</web-test>
        [Test]
        public void ダブルクリックは空手ならその枠へ掴み中なら持ち手へ集める()
        {
            _commands.DoubleClick(1);
            _player.SetGrab(new SlotItem(_wood, 1));
            _commands.DoubleClick(1);
            CollectionAssert.AreEqual(new[] { "block:1", "grab" }, _block.Collects);
        }

        /// <web-test file="e2e/tests/block/blockInventoryGestures.spec.ts">Shift+クリックで block→main へ配分移動する</web-test>
        [Test]
        public void Shift押下はブロックからメインへ配分する()
        {
            _commands.LeftDown(0, true);
            Assert.AreEqual(ItemMoveSlotRef.Block(0), _block.Moves[0].From);
            Assert.AreEqual(ItemMoveArea.Main, _block.Moves[0].To.Area);
            Assert.AreEqual(7, _block.Moves[0].Count);
        }

        [Test]
        public void 押すたびに口を読み直し直前の押下の結果で計画する()
        {
            // 置いた直後の右押下は、口が返す置いた後の値 10 の半分を掴む（読み元の判別は BlockSlotReaderTest。D-37）
            // A right press right after placing takes half of the post-write 10 the port returns (the read source is pinned by BlockSlotReaderTest; D-37)
            _block.WriteThrough = true;
            _block.WriteThroughItemId = _wood;
            _block.Slots = new[] { default(SlotItem), default(SlotItem) };
            _player.SetGrab(new SlotItem(_wood, 10));
            _commands.LeftDown(0, false);
            _player.SetGrab(default);
            _commands.RightDown(0);
            Assert.AreEqual((ItemMoveSlotRef.Block(0), ItemMoveSlotRef.Grab, 5), _block.Moves[1]);
        }

        [Test]
        public void 閉じたあとの古い押下は良性の拒否で何も動かさない()
        {
            _block.Open = false;
            var result = _commands.LeftDown(0, false);
            Assert.AreEqual(UiCommandRejectReason.BlockSlotUnavailable, ((UiCommandRejected)result).Reason);
            CollectionAssert.IsEmpty(_block.Moves);
        }
    }
}
```
（`RecordingPlayerInventoryOperations.SetGrab` と `FixedMaxStackLookup(int)` は C4-e・L0-s4a の実物の名前に合わせる。木の Guid は `GrabOverlayFlowTest` と同じ）

`BlockItemActionExecutorTest.cs`（6 件。計画は `BlockItemActionConversion.TryConvert` で写してから渡す: プレイヤー側の計画〈`InventoryMoveItem`〉は写せず false／分割は掴み中なら `SplitGrabNotEmpty`／空の枠は `ItemSourceSlotEmpty`／1 個は何も動かさず成功／移動の `SourceEmpty`・`SourceCountShort` を A-94 の理由へ／範囲外の枠は `BlockSlotUnavailable`）

- [ ] **Step 3: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `SlotCommands` の型が無いコンパイルエラー

- [ ] **Step 4: 実装する**

`IBlockInventoryOperations.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロック側（サブインベントリ）の枠の口。プレイヤー側の IPlayerInventoryOperations の対
    /// Port to the block-side (sub-inventory) slots; the counterpart of the player-side IPlayerInventoryOperations
    /// </summary>
    public interface IBlockInventoryOperations
    {
        bool TryReadBlockSlots(out IReadOnlyList<SlotItem> slots);
        PlayerItemMoveOutcome Move(ItemMoveSlotRef from, ItemMoveSlotRef to, int count);
        void CollectToGrab();
        void CollectToBlockSlot(int blockSlot);
    }

    // ブロックの枠の番号の範囲判定（実行器とコマンドが共有する 1 か所）
    // Range check for block slot indices (the single place shared by the executor and the commands)
    public static class BlockSlotIndex
    {
        public static bool IsInRange(IReadOnlyList<SlotItem> slots, int blockSlot)
        {
            return 0 <= blockSlot && blockSlot < slots.Count;
        }
    }
}
```

`IRawSubInventorySlots.cs`・`SubInventoryRawSlots.cs`・`BlockSlotReader.cs`（D-37 の読み元をテストで判別できるよう、生の値の読み口を切り出す。D-47）:
```csharp
using System.Collections.Generic;
using Client.Ui.State.Items;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// サブインベントリの枠の生の値（コントローラの先書き込みを含む）を押下の瞬間に読む口
    /// Port reading the raw sub-inventory slots (including the controller's local pre-write) at press time
    /// </summary>
    public interface IRawSubInventorySlots
    {
        IReadOnlyList<SlotItem> Read();
    }
}
```
```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// 本番の生の値の読み口。SubInventoryState が持つ SubInventoryModel はコントローラと同じ参照なので先書きが即時に見える
    /// Production raw reader; SubInventoryState shares its SubInventoryModel with the controller, so pre-writes are visible at once
    /// </summary>
    public sealed class SubInventoryRawSlots : IRawSubInventorySlots
    {
        private readonly SubInventoryState _subInventoryState;

        public SubInventoryRawSlots(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public IReadOnlyList<SlotItem> Read()
        {
            return BlockInventoryPanelStateFactory.ToSlotItems(_subInventoryState.CurrentSubInventory.SubInventory);
        }
    }
}
```
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロックの枠を読む。開閉は表示と同じパネルの状態から、枠の値は押下の瞬間の生の値から（D-37。ホストの BlockAreaSlotParser と同じ）
    /// Reads block slots: open/closed from the panel state the screen shows, values raw at press time (D-37; same as the host BlockAreaSlotParser)
    /// </summary>
    public sealed class BlockSlotReader
    {
        private readonly IBlockInventoryPanelSource _panel;
        private readonly IRawSubInventorySlots _raw;

        public BlockSlotReader(IBlockInventoryPanelSource panel, IRawSubInventorySlots raw)
        {
            _panel = panel;
            _raw = raw;
        }

        public bool TryRead(out IReadOnlyList<SlotItem> slots)
        {
            if (!IsOpenWithoutError(_panel.Current.Value))
            {
                slots = Array.Empty<SlotItem>();
                return false;
            }
            slots = _raw.Read();
            return true;
        }

        private static bool IsOpenWithoutError(BlockInventoryPanelState state)
        {
            switch (state)
            {
                case OpenBlockInventoryPanel block:
                    return block.Error == BlockInventoryPanelError.None;
                case OpenTrainInventoryPanel train:
                    return train.Error == BlockInventoryPanelError.None;
                default:
                    return false;
            }
        }
    }
}
```
（パネルが開いている間は `CurrentSubInventory` が非 null。`OnExit` の null 書き込みとパネルの閉は同じ `UIStateControl.Update` の中で続けて起きるので、その間に押下は届かない。D-48）

`BlockSlotReaderTest.cs`（2 件。偽のパネル源 `FakePanelSource` と偽の生の値 `FixedRawSubInventorySlots`〈`Read` が持たせた配列を返す〉で組む）: `イベント無しの先書き後はパネルの枠でなく生の値を返す`〈パネルの状態は枠 0 が空のチェスト、生の値は枠 0 に木材 10。`TryRead` が真で枠 0 が 10〉／`パネルが閉かエラー中なら生の値があっても偽`〈Closed と `OpenFailedChest()` で偽・空の列〉。読み元がパネルの `ItemSlots` に戻れば 1 件目が赤になる

`LocalBlockInventoryOperations.cs`（Web ホスト `BlockInventoryActions.cs` の `BlockAreaSlotParser`・`BlockMoveItemActionHandler`・`BlockCollectActionHandler` の番地の写し。D-5）:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// 本番のブロック側の口。サブインベントリは結合インベントリの MainSlotCount 以降に並ぶ（ホストの BlockAreaSlotParser と同じ番地）
    /// Production block-side port; the sub-inventory follows MainSlotCount in the combined inventory (same addressing as the host BlockAreaSlotParser)
    /// </summary>
    public sealed class LocalBlockInventoryOperations : IBlockInventoryOperations
    {
        private readonly LocalPlayerInventoryController _controller;
        private readonly BlockSlotReader _reader;

        public LocalBlockInventoryOperations(LocalPlayerInventoryController controller, BlockSlotReader reader)
        {
            _controller = controller;
            _reader = reader;
        }

        public bool TryReadBlockSlots(out IReadOnlyList<SlotItem> slots)
        {
            return _reader.TryRead(out slots);
        }

        public PlayerItemMoveOutcome Move(ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            var (fromType, fromSlot) = ToLocal(from);
            var (toType, toSlot) = ToLocal(to);
            var result = _controller.MoveItem(fromType, fromSlot, toType, toSlot, count);
            if (result == LocalItemMoveResult.NoEffect) Debug.LogWarning($"[LocalBlockInventoryOperations] move has no effect: {from} -> {to} x{count}");
            return result switch
            {
                LocalItemMoveResult.Moved => PlayerItemMoveOutcome.Moved,
                LocalItemMoveResult.NoEffect => PlayerItemMoveOutcome.NoEffect,
                LocalItemMoveResult.SourceEmpty => PlayerItemMoveOutcome.SourceEmpty,
                LocalItemMoveResult.SourceCountShort => PlayerItemMoveOutcome.SourceCountShort,
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, "LocalItemMoveResult の未知の値。PlayerItemMoveOutcome に足すこと"),
            };
        }

        public void CollectToGrab()
        {
            _controller.CollectItems(LocalMoveInventoryType.Grab, 0);
        }

        public void CollectToBlockSlot(int blockSlot)
        {
            _controller.CollectItems(LocalMoveInventoryType.MainOrSub, _controller.LocalPlayerInventory.MainSlotCount + blockSlot);
        }

        // 番地の写しはここ 1 か所。ブロックは MainSlotCount を足した結合の番地
        // The only address mapping; block refs add MainSlotCount for the combined index
        private (LocalMoveInventoryType type, int slot) ToLocal(ItemMoveSlotRef slot)
        {
            return slot.Area switch
            {
                ItemMoveArea.Grab => (LocalMoveInventoryType.Grab, 0),
                ItemMoveArea.Main => (LocalMoveInventoryType.MainOrSub, slot.Slot),
                ItemMoveArea.Equipment => (LocalMoveInventoryType.Equipment, slot.Slot),
                ItemMoveArea.Block => (LocalMoveInventoryType.MainOrSub, _controller.LocalPlayerInventory.MainSlotCount + slot.Slot),
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot.Area, null),
            };
        }
    }
}
```
（`LocalPlayerInventoryOperations` の結果の写しと同じ switch が 2 か所になる。C4-e の写しは `private` なので共有できない。D-6 に記録し、3 か所目が要るなら `PlayerItemMoveOutcomeConversion` へ上げる）

`BlockItemAction.cs`（前例 `PlayerItemAction` と同じ形。種類ごとに型を分け、実行の中身を各型が持つ。D-36）:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロック側の行為。種類ごとに型を分け、範囲判定と実行の中身は各型が持つ（外では派生できない）。規則は Web ホスト BlockInventoryActions と同じ [A-93]
    /// Block-side action; one type per kind, each owning its range check and execution (cannot be derived outside); rules match the Web host BlockInventoryActions [A-93]
    /// </summary>
    public abstract class BlockItemAction
    {
        private protected BlockItemAction()
        {
        }

        internal abstract bool IsInRange(IReadOnlyList<SlotItem> slots);
        internal abstract UiCommandResult Run(IBlockInventoryOperations block, IReadOnlyList<SlotItem> slots, bool grabEmpty);

        private protected static bool InRange(IReadOnlyList<SlotItem> slots, ItemMoveSlotRef slot)
        {
            return slot.Area != ItemMoveArea.Block || BlockSlotIndex.IsInRange(slots, slot.Slot);
        }

        // 移動の結果を A-94 の理由へ写す（PlayerItemAction.FromOutcome と同じ対応）
        // Map move outcomes to the A-94 reasons (same mapping as PlayerItemAction.FromOutcome)
        private protected static UiCommandResult FromOutcome(PlayerItemMoveOutcome outcome)
        {
            return outcome switch
            {
                PlayerItemMoveOutcome.Moved => UiCommandAccepted.Instance,
                PlayerItemMoveOutcome.NoEffect => UiCommandAccepted.Instance,
                PlayerItemMoveOutcome.SourceEmpty => new UiCommandRejected(UiCommandRejectReason.ItemSourceSlotEmpty, "source slot is empty"),
                PlayerItemMoveOutcome.SourceCountShort => new UiCommandRejected(UiCommandRejectReason.ItemSourceCountShort, "source count is short"),
                _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
            };
        }
    }

    public sealed class BlockMoveItemAction : BlockItemAction
    {
        private readonly ItemMoveSlotRef _from;
        private readonly ItemMoveSlotRef _to;
        private readonly int _count;

        public BlockMoveItemAction(ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            _from = from;
            _to = to;
            _count = count;
        }

        internal override bool IsInRange(IReadOnlyList<SlotItem> slots) => InRange(slots, _from) && InRange(slots, _to);
        internal override UiCommandResult Run(IBlockInventoryOperations block, IReadOnlyList<SlotItem> slots, bool grabEmpty) => FromOutcome(block.Move(_from, _to, _count));
    }

    public sealed class BlockSplitToGrabAction : BlockItemAction
    {
        private readonly int _blockSlot;

        public BlockSplitToGrabAction(int blockSlot)
        {
            _blockSlot = blockSlot;
        }

        internal override bool IsInRange(IReadOnlyList<SlotItem> slots) => BlockSlotIndex.IsInRange(slots, _blockSlot);

        internal override UiCommandResult Run(IBlockInventoryOperations block, IReadOnlyList<SlotItem> slots, bool grabEmpty)
        {
            // 半分の計算はホストの SplitGrabActionHandler と同じ床関数。1 個以下は成功扱いの何もしない
            // Half uses the same floor as the host; a stack of one or less is a successful no-op
            if (!grabEmpty) return new UiCommandRejected(UiCommandRejectReason.SplitGrabNotEmpty, "grab is not empty");
            var slot = slots[_blockSlot];
            if (slot.IsEmpty) return new UiCommandRejected(UiCommandRejectReason.ItemSourceSlotEmpty, $"block slot {_blockSlot} is empty");
            var half = slot.Count / 2;
            // silent-ok: 1 個の枠の右押下は Web と同じく何もしない / 枠に 1 個残っているのがプレイヤーに見えている
            if (half == 0) return UiCommandAccepted.Instance;
            return FromOutcome(block.Move(ItemMoveSlotRef.Block(_blockSlot), ItemMoveSlotRef.Grab, half));
        }
    }

    public sealed class BlockCollectAction : BlockItemAction
    {
        private readonly int _blockSlot;

        public BlockCollectAction(int blockSlot)
        {
            _blockSlot = blockSlot;
        }

        internal override bool IsInRange(IReadOnlyList<SlotItem> slots) => BlockSlotIndex.IsInRange(slots, _blockSlot);

        internal override UiCommandResult Run(IBlockInventoryOperations block, IReadOnlyList<SlotItem> slots, bool grabEmpty)
        {
            // 集める先は操作の瞬間の持ち手で決める（ホストの ResolveCollectTarget と同じ）
            // The target follows the grab at gesture time (same as the host ResolveCollectTarget)
            if (grabEmpty) block.CollectToBlockSlot(_blockSlot);
            else block.CollectToGrab();
            return UiCommandAccepted.Instance;
        }
    }
}
```
（約 120 行。`FromOutcome` の `_ => throw` は C4-e の `PlayerItemAction.FromOutcome` と同じ「enum の未知の値」の網羅で、種類ごとの例外ではない）

`BlockItemActionConversion.cs`:
```csharp
using Client.Ui.State.ItemMove;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// L0-s4a の計画 1 件をブロック側の行為へ写す境目（PlayerItemActionConversion の対）。Block* の 3 種でなければ false
    /// Boundary converting one L0-s4a plan into a block-side action (counterpart of PlayerItemActionConversion); false unless one of the three Block* kinds
    /// </summary>
    public static class BlockItemActionConversion
    {
        public static bool TryConvert(PlannedItemAction action, out BlockItemAction blockAction)
        {
            blockAction = action.Kind switch
            {
                PlannedItemActionKind.BlockInventoryMoveItem => new BlockMoveItemAction(action.From, action.To, action.Count),
                PlannedItemActionKind.BlockInventorySplit => new BlockSplitToGrabAction(action.From.Slot),
                PlannedItemActionKind.BlockInventoryCollect => new BlockCollectAction(action.From.Slot),
                _ => null,
            };
            return blockAction != null;
        }
    }
}
```

`BlockItemActionExecutor.cs`:
```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロック側の行為を、開いているか・枠が範囲内かを確かめてから実行する
    /// Runs a block-side action after checking the panel is open and the slots are in range
    /// </summary>
    public sealed class BlockItemActionExecutor
    {
        private readonly IBlockInventoryOperations _block;
        private readonly IPlayerInventoryOperations _player;

        public BlockItemActionExecutor(IBlockInventoryOperations block, IPlayerInventoryOperations player)
        {
            _block = block;
            _player = player;
        }

        public UiCommandResult Execute(BlockItemAction action)
        {
            // 閉じた・枠が減った後の古い計画は良性の拒否（ホストの invalid_slot）
            // A stale plan after closing or shrinking is a benign rejection (the host's invalid_slot)
            if (!_block.TryReadBlockSlots(out var slots)) return Unavailable("block inventory is closed or in error");
            if (!action.IsInRange(slots)) return Unavailable($"block slot out of range: {action}");
            return action.Run(_block, slots, _player.ReadGrab().IsEmpty);
        }

        private static UiCommandResult Unavailable(string detail)
        {
            Debug.LogWarning($"[BlockItemActionExecutor] {detail}");
            return new UiCommandRejected(UiCommandRejectReason.BlockSlotUnavailable, detail);
        }
    }
}
```
（C4-e の `PlayerItemAction` 側の写しと詳細文を揃える。実物の文言を見て同じ文にする）

`PlannedItemActionRunner.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands.Block;
using UnityEngine;

namespace Client.Ui.State.SlotCommands
{
    /// <summary>
    /// 計画の列を順に実行する。プレイヤー側へ写せればプレイヤー側、ブロック側へ写せればブロック側。最初の拒否で止める
    /// Runs a plan list in order: player side if convertible, block side if convertible; stops at the first rejection
    /// </summary>
    public sealed class PlannedItemActionRunner
    {
        private readonly PlayerItemActionExecutor _player;
        private readonly BlockItemActionExecutor _block;

        public PlannedItemActionRunner(PlayerItemActionExecutor player, BlockItemActionExecutor block)
        {
            _player = player;
            _block = block;
        }

        public UiCommandResult Execute(IReadOnlyList<PlannedItemAction> actions)
        {
            foreach (var action in actions)
            {
                var result = Run(action);
                if (result is UiCommandRejected) return result;
            }
            return UiCommandAccepted.Instance;
        }

        // プレイヤーへ写す → ブロックへ写す → どちらも不可は計画器の誤りなので Error ログと拒否
        // Player first, then block; neither means a planner bug, so log an error and reject
        private UiCommandResult Run(PlannedItemAction action)
        {
            if (PlayerItemActionConversion.TryConvert(action, out var playerAction)) return _player.Execute(new[] { playerAction });
            if (BlockItemActionConversion.TryConvert(action, out var blockAction)) return _block.Execute(blockAction);
            Debug.LogError($"[PlannedItemActionRunner] plan has no executor: {action}");
            return new UiCommandRejected(UiCommandRejectReason.BlockSlotUnavailable, $"plan has no executor: {action}");
        }
    }
}
```

`KnownItemSlots.cs`（D-11）:
```csharp
using Client.Ui.State.Items;
using Core.Master;
using UnityEngine;

namespace Client.Ui.State.SlotCommands
{
    /// <summary>
    /// DirectMovePlanner は未知の ItemId で例外になるので、配分の前に元の枠が既知の ID かを確かめる（L0-s4a の申し送り）
    /// DirectMovePlanner throws on unknown ItemIds, so check the source slot holds a known ID before allocating (L0-s4a handoff)
    /// </summary>
    public static class KnownItemSlots
    {
        public static bool IsKnownOrEmpty(SlotItem slot, string where)
        {
            if (slot.IsEmpty || MasterHolder.ItemMaster.ExistItemId(slot.ItemId)) return true;
            Debug.LogError($"[{where}] unknown ItemId {slot.ItemId} reached the slot planner; the allocation is skipped (client and server masters may differ)");
            return false;
        }
    }
}
```

`IBlockSlotCommands.cs`・`BlockSlotCommands.cs`:
```csharp
using Client.Ui.State.Commands;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロックの枠の操作（Web useBlockSlotGestures の 3 つ。ブロックの枠には進入の操作が無い）
    /// Block-slot commands (the three of Web useBlockSlotGestures; block slots have no enter gestures)
    /// </summary>
    public interface IBlockSlotCommands
    {
        UiCommandResult LeftDown(int blockSlot, bool shift);
        UiCommandResult RightDown(int blockSlot);
        UiCommandResult DoubleClick(int blockSlot);
    }
}
```
```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.ItemMove;
using Core.Item.Interface;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロックの枠の押下を操作の瞬間の在庫で計画し実行する（Web useBlockSlotGestures の写し）
    /// Plans and runs block-slot presses from the inventory at gesture time (port of Web useBlockSlotGestures)
    /// </summary>
    public sealed class BlockSlotCommands : IBlockSlotCommands
    {
        public const string CommandName = "block_inventory.slot";

        private readonly IBlockInventoryOperations _block;
        private readonly IPlayerInventoryOperations _player;
        private readonly PlannedItemActionRunner _runner;
        private readonly IItemStackLevelLookup _stackLevels;

        public BlockSlotCommands(IBlockInventoryOperations block, IPlayerInventoryOperations player, PlannedItemActionRunner runner, IItemStackLevelLookup stackLevels)
        {
            _block = block;
            _player = player;
            _runner = runner;
            _stackLevels = stackLevels;
        }

        public UiCommandResult LeftDown(int blockSlot, bool shift)
        {
            if (!TryReadSlot(blockSlot, out var slot)) return Unavailable(blockSlot);
            if (!KnownItemSlots.IsKnownOrEmpty(slot, nameof(BlockSlotCommands))) return UiCommandAccepted.Instance;
            var player = _player.ReadSnapshot();
            var plan = BlockSlotPlanner.PlanLeftClick(blockSlot, slot, shift, player.Grab.Count, player.MainSlots, _stackLevels);

            // Shift の配分が空（main が満杯）は Web と同じく何も送らない。開発者ログだけ残す（D-10）
            // An empty Shift allocation (main full) sends nothing like the Web; leave only a developer log (D-10)
            if (shift && player.Grab.Count == 0 && !slot.IsEmpty && plan.Count == 0) Debug.LogWarning($"[block_inventory.shift_move] not sent: destinationFull block slot={blockSlot} item={slot.ItemId}");
            return _runner.Execute(plan);
        }

        public UiCommandResult RightDown(int blockSlot)
        {
            if (!TryReadSlot(blockSlot, out var slot)) return Unavailable(blockSlot);
            return _runner.Execute(BlockSlotPlanner.PlanRightClick(blockSlot, slot, _player.ReadGrab().Count));
        }

        public UiCommandResult DoubleClick(int blockSlot)
        {
            if (!TryReadSlot(blockSlot, out _)) return Unavailable(blockSlot);
            return _runner.Execute(BlockSlotPlanner.PlanDoubleClick(blockSlot));
        }

        // 計画の入力は押下の瞬間に読む（描画時の写しを混ぜない）
        // Read planner inputs at gesture time (never mix render-time snapshots)
        private bool TryReadSlot(int blockSlot, out Items.SlotItem slot)
        {
            slot = default;
            if (!_block.TryReadBlockSlots(out var slots) || !BlockSlotIndex.IsInRange(slots, blockSlot)) return false;
            slot = slots[blockSlot];
            return true;
        }

        private static UiCommandResult Unavailable(int blockSlot)
        {
            Debug.LogWarning($"[{CommandName}] stale press ignored: block slot {blockSlot} is not available");
            return new UiCommandRejected(UiCommandRejectReason.BlockSlotUnavailable, $"block slot {blockSlot}");
        }
    }
}
```
（`Items.SlotItem` は `using Client.Ui.State.Items;` を足して `SlotItem` と書く。`IItemStackLevelLookup` の名前空間は実物〈`Core.Item.Interface`〉で確かめる）

- [ ] **Step 5: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.(SlotCommands|Inventory|ItemMove|Commands)|PauseMenuCommandsTest"`
Expected: `ErrorCount: 0`、新しい件と C4-e・L0-s4a の既存件・`PauseMenuCommandsTest`（`UiCommandRejectReason` の網羅テスト）が PASS

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands moorestech_client/Assets/Scripts/Client.Ui.State/Commands moorestech_client/Assets/Scripts/Client.Tests/Ui/State/SlotCommands Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
git commit -m "feat(uitk): ブロック側の実行器とブロックの枠の操作コマンドを足す（S4-alpha）

<実行ホストの指示する帰属行>"
```

---

### Task 5: プレイヤー側の型付きコマンド・Shift の断りの通知・束縛の寄与の口

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Player/IPlayerSlotCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Player/PlayerSlotCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Player/ShiftMoveRefusalNotifier.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Player/PlayerSlotContextReader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/SlotRestriction/IBlockSlotRestrictionContributor.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/SlotRestriction/BlockSlotRestrictionInputBuilder.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/SlotCommands/PlayerSlotCommandsTest.cs`・`ShiftMoveRefusalNotifierTest.cs`・`BlockSlotRestrictionInputBuilderTest.cs`

**Interfaces:**
- Consumes: Task 3 の `IBlockInventoryPanelSource`、Task 4 の `PlannedItemActionRunner`・`KnownItemSlots`、C4-e の `PlayerSlotActionPlanner`・`IPlayerSlotDragCommands`、L0-s4a の `PlayerSlotContext`、L0-s4b の `BlockSlotRestrictionInput`・`BlockSlotRestrictionResolver`、`ClientLocalNotificationSource`
- Produces（S4-inventory が InventoryPanel・EquipmentPanel・ホットバーの枠から使う）:
```csharp
namespace Client.Ui.State.SlotCommands.Player
public interface IPlayerSlotCommands
{
    UiCommandResult LeftDown(PlayerSlotRef slot, bool shift);   // SlotGestureKind.LeftDown
    UiCommandResult RightDown(PlayerSlotRef slot);              // RightDown
    UiCommandResult DoubleClick(PlayerSlotRef slot);            // DoubleClick
    void LeftEnter(PlayerSlotRef slot);                          // LeftEnter（C4-e の EnterWithLeftButton へ素通し）
    UiCommandResult RightEnter(PlayerSlotRef slot);             // RightEnter（C4-e の EnterWithRightButton へ素通し）
}
// 結果は呼び手が UiCommandResultToaster.Report(PlayerSlotCommands.CommandName = "inventory.slot", result) へ渡す
namespace Client.Ui.State.BlockInventory.SlotRestriction
public interface IBlockSlotRestrictionContributor
{
    bool AppliesTo(OpenBlockInventoryPanel panel);
    OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input);  // WithMachine / WithInletBindings を返す
}
```

- [ ] **Step 1: 失敗するテストを書く**

`PlayerSlotCommandsTest.cs`（5 件。パネルの状態は Task 3 の `BlockInventoryTestPanels`、分割ドラッグは C4-e の `RecordingPlayerSlotDragCommands`〈無ければこのファイルに `IPlayerSlotDragCommands` の記録係を `internal sealed class` で置く〉）:
```csharp
using System;
using Client.Game.InGame.UI.Notification;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.ItemMove;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.SlotCommands.Player;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Tests.Module.TestMod;
using UniRx;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// プレイヤーの枠の押下をブロックの開閉に応じた文脈で計画し、ブロック宛ての移動はブロック側の実行器へ渡す（L0）
    /// Plans player-slot presses with a context following the block's open state; moves into the block go to the block executor (L0)
    /// </summary>
    public class PlayerSlotCommandsTest
    {
        private RecordingPlayerInventoryOperations _player;
        private RecordingBlockInventoryOperations _block;
        private RecordingPlayerSlotDrag _drag;
        private ReactiveProperty<BlockInventoryPanelState> _panel;
        private ClientLocalNotificationSource _notifications;
        private PlayerSlotCommands _commands;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            _player = new RecordingPlayerInventoryOperations(36, 2);
            _block = new RecordingBlockInventoryOperations { Slots = new[] { new SlotItem(_wood, 1), new SlotItem(default, 0) } };
            _drag = new RecordingPlayerSlotDrag();
            _panel = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            _notifications = new ClientLocalNotificationSource();
            var runner = new PlannedItemActionRunner(new PlayerItemActionExecutor(_player), new BlockItemActionExecutor(_block, _player));
            var reader = new PlayerSlotContextReader(new FakePanelSource(_panel), new BlockSlotRestrictionInputBuilder(Array.Empty<IBlockSlotRestrictionContributor>()), new FixedMaxStackLookup(64));
            _commands = new PlayerSlotCommands(_player, _drag, runner, reader, new ShiftMoveRefusalNotifier(_notifications));
        }

        /// <web-test file="e2e/tests/block/blockInventoryGestures.spec.ts">block 開時は main の Shift+クリックが block へ配分移動する</web-test>
        [Test]
        public void ブロックが開いていればメインのShift押下はブロックへ配分する()
        {
            _panel.Value = BlockInventoryTestPanels.ChestWithSlots(new SlotItem(_wood, 1), new SlotItem(default, 0));
            _player.SetMain(1, new SlotItem(_wood, 5));
            _commands.LeftDown(PlayerSlotRef.Main(1), true);
            Assert.AreEqual(ItemMoveSlotRef.Main(1), _block.Moves[0].From);
            Assert.AreEqual(ItemMoveArea.Block, _block.Moves[0].To.Area);
        }

        [Test]
        public void 空手で中身のある枠の左押下は分割ドラッグを始める()
        {
            _player.SetMain(0, new SlotItem(_wood, 4));
            _commands.LeftDown(PlayerSlotRef.Main(0), false);
            Assert.AreEqual(PlayerSlotRef.Main(0), _drag.Begun[0]);
        }

        [Test]
        public void 取得失敗のブロックへのShift押下は断りを通知して送らない()
        {
            NotificationMessagePack notified = null;
            _notifications.OnNotification.Subscribe(m => notified = m);
            _panel.Value = BlockInventoryTestPanels.OpenFailedChest();
            _player.SetMain(1, new SlotItem(_wood, 5));
            _commands.LeftDown(PlayerSlotRef.Main(1), true);
            Assert.AreEqual("denied.inventoryOpenFailed", notified.MessageId);
            CollectionAssert.IsEmpty(_block.Moves);
            CollectionAssert.IsEmpty(_player.Moves);
        }

        [Test]
        public void 進入は分割ドラッグの口へ素通しする()
        {
            _commands.LeftEnter(PlayerSlotRef.Main(2));
            _commands.RightEnter(PlayerSlotRef.Main(3));
            Assert.AreEqual(PlayerSlotRef.Main(2), _drag.LeftEntered[0]);
            Assert.AreEqual(PlayerSlotRef.Main(3), _drag.RightEntered[0]);
        }

        [Test]
        public void 減った後の古い枠の押下は警告して何もしない()
        {
            var result = _commands.LeftDown(PlayerSlotRef.Main(99), false);
            Assert.IsInstanceOf<Client.Ui.State.Commands.UiCommandAccepted>(result);
            CollectionAssert.IsEmpty(_player.Moves);
        }
    }
}
```
（`FakePanelSource` は Task 3 のテストと同じ形を `Client.Tests/Ui/State/BlockInventory/PanelModel/FakePanelSource.cs` へ出して共有する〈Task 3 の LayoutModel テストもこちらを使う〉。`RecordingPlayerInventoryOperations.Moves`・`SetMain` は C4-e の実物の名前に合わせる。`NotificationMessagePack.MessageId` の名前は実物で確かめる。`ClientLocalNotificationSource` の ctor が引数を取るなら実物に合わせる）

`ShiftMoveRefusalNotifierTest.cs`（4 件: 3 つの理由の messageId と分類 OperationDenied・`DestinationFull` は通知しない。ログは `LogAssert.Expect(LogType.Warning, new Regex(@"\[inventory\.shift_move\] not sent: destinationFull"))`）

`BlockSlotRestrictionInputBuilderTest.cs`（3 件: 取得失敗は `OpenFailedBlockSlotInput`／寄与が無いチェストは `OpenBlockSlotInput` のまま／`AppliesTo` が真の寄与だけが順に `Contribute` される）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PlayerSlotCommands` 等が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`IBlockSlotRestrictionContributor.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;

namespace Client.Ui.State.BlockInventory.SlotRestriction
{
    /// <summary>
    /// Shift 配分の宛先の束縛の寄与の口（機械の選択レシピは S4-machine、ロケット・宇宙エレベーターの入れ口は S4-special が足す）
    /// Contribution port for Shift-allocation bindings (S4-machine adds the selected recipe, S4-special the rocket and space-elevator inlets)
    /// </summary>
    public interface IBlockSlotRestrictionContributor
    {
        bool AppliesTo(OpenBlockInventoryPanel panel);
        OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input);
    }
}
```

`BlockSlotRestrictionInputBuilder.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;

namespace Client.Ui.State.BlockInventory.SlotRestriction
{
    /// <summary>
    /// 開いているブロックの状態と寄与から L0-s4b の束縛の入力を組む（Web resolveBlockSlotRestriction の入力側）
    /// Builds the L0-s4b binding input from the open block and contributions (input side of Web resolveBlockSlotRestriction)
    /// </summary>
    public sealed class BlockSlotRestrictionInputBuilder
    {
        private readonly IReadOnlyList<IBlockSlotRestrictionContributor> _contributors;

        public BlockSlotRestrictionInputBuilder(IReadOnlyList<IBlockSlotRestrictionContributor> contributors)
        {
            _contributors = contributors;
        }

        public BlockSlotRestrictionInput Build(OpenBlockInventoryPanel panel)
        {
            // 取得失敗は候補を探す前に返す。束縛の未着（寄与なし）は L0-s4b が種別ごとに取得待ちか無制限へ倒す
            // Return fetch failure before candidates; a missing binding (no contribution) falls to pending or unrestricted per type in L0-s4b
            if (panel.Error != BlockInventoryPanelError.None) return BlockSlotRestrictionInput.OpenFailedBlock;
            var input = BlockSlotRestrictionInput.OpenBlock(panel.BlockType, panel.ItemSlots.Count);
            foreach (var contributor in _contributors)
            {
                if (contributor.AppliesTo(panel)) input = contributor.Contribute(panel, input);
            }
            return input;
        }
    }
}
```

`ShiftMoveRefusalNotifier.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Notification;
using Client.Ui.State.ItemMove;
using Core.Master;
using Server.Event.Notification;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Player
{
    /// <summary>
    /// Shift の配分を送らない理由を、Web と同じ messageId の通知と開発者ログに揃える（Web notifyBlockedShift の写し）
    /// Publishes why a Shift allocation is not sent as a notification with the Web messageId and a developer log (port of Web notifyBlockedShift)
    /// </summary>
    public sealed class ShiftMoveRefusalNotifier
    {
        private readonly ClientLocalNotificationSource _notifications;

        public ShiftMoveRefusalNotifier(ClientLocalNotificationSource notifications)
        {
            _notifications = notifications;
        }

        public void Notify(ShiftMoveRefusalReason reason, ItemId itemId, string blockIdentifier)
        {
            Debug.LogWarning($"[inventory.shift_move] not sent: {ToLogName(reason)} block={blockIdentifier} item={itemId}");

            // 宛先が満杯は Web に無い断り。messageId を持たないので通知せず、上のログだけで開発者に知らせる（D-9）
            // Destination-full has no Web counterpart and no messageId; no notification, the log above tells developers (D-9)
            // silent-ok: DestinationFull は Web も無音 / 上の Warning ログで開発者が気づく
            if (!TryGetMessageId(reason, out var messageId)) return;
            _notifications.Notify(NotificationMessagePack.CreateOperationDenied(messageId, new Dictionary<string, string>()));
        }

        private static bool TryGetMessageId(ShiftMoveRefusalReason reason, out string messageId)
        {
            switch (reason)
            {
                case ShiftMoveRefusalReason.NoAcceptingBlockSlot:
                    messageId = "denied.inventoryMoveSlotRejected";
                    return true;
                case ShiftMoveRefusalReason.InventoryOpenFailed:
                    messageId = "denied.inventoryOpenFailed";
                    return true;
                case ShiftMoveRefusalReason.DetailLoading:
                    messageId = "denied.inventoryDetailLoading";
                    return true;
                case ShiftMoveRefusalReason.DestinationFull:
                    // silent-ok: DestinationFull は Web に messageId が無い / 呼び手の Warning ログで開発者が気づく
                    messageId = "";
                    return false;
                default:
                    // enum に値を足して対応を足し忘れた開発時の誤り（D-44 と同じ扱い）
                    // A dev-time mistake: a new enum value without a mapping (same as D-44)
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
            }
        }

        private static string ToLogName(ShiftMoveRefusalReason reason)
        {
            return reason switch
            {
                ShiftMoveRefusalReason.NoAcceptingBlockSlot => "noAcceptingBlockSlot",
                ShiftMoveRefusalReason.InventoryOpenFailed => "inventoryOpenFailed",
                ShiftMoveRefusalReason.DetailLoading => "detailLoading",
                ShiftMoveRefusalReason.DestinationFull => "destinationFull",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
```

`PlayerSlotContextReader.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Client.Ui.State.ItemMove;
using Core.Item.Interface;
using Core.Master;

namespace Client.Ui.State.SlotCommands.Player
{
    /// <summary>
    /// 押下の瞬間のブロックの開閉と束縛からプレイヤーの枠の計画の文脈を組む（Web slotActions の ctx）
    /// Builds the player-slot planning context from the block's open state and bindings at gesture time (the Web slotActions ctx)
    /// </summary>
    public sealed class PlayerSlotContextReader
    {
        private readonly IBlockInventoryPanelSource _panel;
        private readonly BlockSlotRestrictionInputBuilder _restrictionInput;
        private readonly IItemStackLevelLookup _stackLevels;

        public PlayerSlotContextReader(IBlockInventoryPanelSource panel, BlockSlotRestrictionInputBuilder restrictionInput, IItemStackLevelLookup stackLevels)
        {
            _panel = panel;
            _restrictionInput = restrictionInput;
            _stackLevels = stackLevels;
        }

        public PlayerSlotContext Read(PlayerInventorySnapshot inventory, ItemId clickedItem)
        {
            // 列車は束縛なし（Web は source が block のときだけ束縛を引く）
            // Trains are unrestricted (the Web resolves bindings only for block sources)
            return _panel.Current.Value switch
            {
                OpenBlockInventoryPanel block => PlayerSlotContext.WithBlockOpen(inventory, block.ItemSlots, BlockSlotRestrictionResolver.Resolve(_restrictionInput.Build(block), clickedItem), _stackLevels),
                OpenTrainInventoryPanel train => PlayerSlotContext.WithBlockOpen(inventory, train.ItemSlots, BlockSlotRestriction.Unrestricted, _stackLevels),
                _ => PlayerSlotContext.WithBlockClosed(inventory, _stackLevels),
            };
        }

        // ログに出すブロックの識別（Web の block.identifier）
        // Block identity for logs (the Web block.identifier)
        public string DescribeOpenBlock()
        {
            return _panel.Current.Value switch
            {
                OpenBlockInventoryPanel block => block.Position.ToString(),
                OpenTrainInventoryPanel train => train.TrainCarInstanceId.ToString(),
                _ => "(closed)",
            };
        }
    }
}
```

`IPlayerSlotCommands.cs`（Produces の形そのまま）と `PlayerSlotCommands.cs`:
```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.ItemMove;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Player
{
    /// <summary>
    /// 持ち物・装備・ホットバーの枠の操作（Web slotActions の配線側）。計画は C4-e の PlayerSlotActionPlanner、実行は振り分け器
    /// Player-slot commands for inventory, equipment and hotbar (wiring half of Web slotActions); C4-e planner plans, the runner executes
    /// </summary>
    public sealed class PlayerSlotCommands : IPlayerSlotCommands
    {
        public const string CommandName = "inventory.slot";

        private readonly IPlayerInventoryOperations _player;
        private readonly IPlayerSlotDragCommands _drag;
        private readonly PlannedItemActionRunner _runner;
        private readonly PlayerSlotContextReader _context;
        private readonly ShiftMoveRefusalNotifier _refusals;

        public PlayerSlotCommands(IPlayerInventoryOperations player, IPlayerSlotDragCommands drag, PlannedItemActionRunner runner, PlayerSlotContextReader context, ShiftMoveRefusalNotifier refusals)
        {
            _player = player;
            _drag = drag;
            _runner = runner;
            _context = context;
            _refusals = refusals;
        }

        public UiCommandResult LeftDown(PlayerSlotRef slot, bool shift)
        {
            // 全入力を押下の瞬間に読む。元の枠が未知の ID なら配分の計画へ渡さない（D-11）
            // Read every input at gesture time; never hand an unknown source ID to the allocation planner (D-11)
            var inventory = _player.ReadSnapshot();
            if (!inventory.TryResolveSlot(slot.ToItemMoveSlotRef(), out var clicked)) return Ignore(new StaleSlotIgnoredPlan(slot.ToItemMoveSlotRef()));
            if (!KnownItemSlots.IsKnownOrEmpty(clicked, nameof(PlayerSlotCommands))) return UiCommandAccepted.Instance;
            var plan = PlayerSlotActionPlanner.PlanLeftDown(_context.Read(inventory, clicked.ItemId), slot.ToItemMoveSlotRef(), shift);
            return Apply(plan, slot, clicked);
        }

        public UiCommandResult RightDown(PlayerSlotRef slot)
        {
            return Apply(PlayerSlotActionPlanner.PlanRightDown(_player.ReadSnapshot(), slot.ToItemMoveSlotRef()), slot, default);
        }

        public UiCommandResult DoubleClick(PlayerSlotRef slot)
        {
            return Apply(PlayerSlotActionPlanner.PlanDoubleClick(_player.ReadSnapshot(), slot.ToItemMoveSlotRef()), slot, default);
        }

        public void LeftEnter(PlayerSlotRef slot)
        {
            _drag.EnterWithLeftButton(slot);
        }

        public UiCommandResult RightEnter(PlayerSlotRef slot)
        {
            return _drag.EnterWithRightButton(slot);
        }

        private UiCommandResult Apply(PlayerSlotActionPlan plan, PlayerSlotRef slot, Items.SlotItem clicked)
        {
            switch (plan)
            {
                case ItemActionsPlan actions:
                    return _runner.Execute(actions.Actions);
                case BeginSplitDragPlan:
                    _drag.BeginSplitDrag(slot);
                    return UiCommandAccepted.Instance;
                case ShiftMoveRefusedPlan refused:
                    _refusals.Notify(refused.Reason, clicked.ItemId, _context.DescribeOpenBlock());
                    return UiCommandAccepted.Instance;
                case StaleSlotIgnoredPlan stale:
                    return Ignore(stale);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(plan), plan.GetType().Name, "押下の計画に進入の計画は来ない");
            }
        }

        // 枠数が縮んだ直後の古い押下は Web と同じく何もしない。開発者ログだけ残す
        // A stale press right after the slot count shrinks does nothing like the Web; leave a developer log only
        private static UiCommandResult Ignore(StaleSlotIgnoredPlan stale)
        {
            Debug.LogWarning($"[{CommandName}] stale press ignored: {stale.Target}");
            return UiCommandAccepted.Instance;
        }
    }
}
```
（`Items.SlotItem` は `using Client.Ui.State.Items;` を足して `SlotItem` と書く。200 行以内に収まる）

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.(SlotCommands|BlockInventory|Inventory|ItemMove)"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/SlotRestriction moorestech_client/Assets/Scripts/Client.Tests/Ui/State
git commit -m "feat(uitk): プレイヤーの枠の型付きコマンド・Shift の断りの通知・束縛の寄与の口を足す（S4-alpha）

<実行ホストの指示する帰属行>"
```

---
### Task 6: 画面だけの部品（`Screens/BlockInventory/Parts/`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/LackHighlightText.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/BlockItemGrid.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/BlockItemGridGroup.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/BlockItemGridFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/Parts.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/Rows/MachineStateRow.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/Rows/PerMinuteRateRow.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/Rows/PowerRateText.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Parts/LackHighlightTextTest.cs`・`BlockInventoryRowsTest.cs`

**Interfaces:**
- Consumes: `LocalizedLabel`・`UiTypographyClass`・`ItemSlot`・`ItemSlotContent`・`SlotGrid`・`UiStack`・`UiGroup`・`GameContentPresentation`・`UiCommandResultToaster`・`UssCustomStyleReader.TryReadNumber`、Task 4 の `IBlockSlotCommands`・`BlockSlotCommands.CommandName`、L0-s4b の `BlockDetailStateDisplay.Machine`・`BlockDetailMath.ComputePowerRate`
- Produces（machine・special・train が自分の区画・本文から使う）:
```csharp
namespace Client.Ui.Screens.BlockInventory.Parts
public enum LackHighlightTextSize { Xs, Sm }
public sealed class LackHighlightText : LocalizedLabel
{
    public const string InsufficientClassName = "lack-highlight-text--insufficient";   // Web data-insufficient="true"
    public LackHighlightText(string testid, LackHighlightTextSize size);              // testid は null 可（name を付けない）
    public void Render(UiText text, bool insufficient);
}
public sealed class BlockItemGrid : VisualElement
{
    public void Render(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<int> slotIndices);  // 枠 testid = "<testId>-slot-<index>"、列数 = min(--inventory-columns, max(1, 枠数))
}
public sealed class BlockItemGridGroup : VisualElement { public BlockItemGridGroup(LocalizationKey label, BlockItemGrid grid); }
public sealed class BlockItemGridFactory { public BlockItemGrid Create(string testId); }   // DI 登録済み（BlockInventoryGameUiRegistration）
namespace Client.Ui.Screens.BlockInventory.Parts.Rows
public sealed class PowerRateText : VisualElement { public PowerRateText(string testid); public void Render(double currentPower, double requestPower, bool highlightShortage); }
public sealed class MachineStateRow   // Web は fragment。親の UiGapContainer へ 2 要素を直接足す
{
    public MachineStateRow(string stateTestId, string powerRateTestId);
    public void MountInto(UiGapContainer parent);
    public void Render(MachineProcessDisplayState state, double currentPower, double requestPower, bool highlightPowerShortage);
}
public sealed class PerMinuteRateRow : VisualElement { public PerMinuteRateRow(VisualElement leading); public void Render(double amountPerMinute); }
```

- [ ] **Step 1: 失敗するテストを書く**

`LackHighlightTextTest.cs`:
```csharp
using Client.Tests.Ui.Screens;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Parts
{
    public class LackHighlightTextTest : ScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/LackHighlightText/index.test.ts">現在値と要求値を表示し、通常状態をdata属性で公開する</web-test>
        [Test]
        public void 文と名前を出し通常状態では不足のクラスを持たない()
        {
            var text = Mount(new LackHighlightText("torque", LackHighlightTextSize.Sm));
            text.Render(UiText.FromVerbatim("トルク 1.0 / 2.0"), false);
            simulate.FrameUpdate();
            Assert.AreEqual("torque", text.name);
            Assert.AreEqual("トルク 1.0 / 2.0", text.text);
            Assert.IsFalse(text.ClassListContains(LackHighlightText.InsufficientClassName));
        }

        /// <web-test file="src/features/blockInventory/details/LackHighlightText/index.test.ts">不足状態をdata属性で公開する</web-test>
        [Test]
        public void 不足状態は不足のクラスで色が変わる()
        {
            var text = Mount(new LackHighlightText(null, LackHighlightTextSize.Sm));
            text.Render(UiText.FromVerbatim("RPM 1.0 / 2.0"), true);
            simulate.FrameUpdate();
            Assert.IsTrue(text.ClassListContains(LackHighlightText.InsufficientClassName));
            Assert.AreNotEqual(new LackHighlightText(null, LackHighlightTextSize.Sm).resolvedStyle.color, text.resolvedStyle.color);
        }
    }
}
```
（2 件目の色は 1 件目と同じく載せた要素どうしで比べる。載せていない要素の `resolvedStyle` は既定色になるので、比べる側も `Mount` してから読む形に直してよい）

`BlockInventoryRowsTest.cs`（4 件: `PowerRateText` は率を四捨五入の百分率で出し、強調が許されて率 < 1 なら不足／強調が許されなければ不足にしない／`MachineStateRow` は停止中（Halted）に電力率を隠し Error ログ 0 件〈`BlockDetailStateDisplay.Machine(Halted).ShowPowerRate` が偽。Idle・Processing は真＝`DetailLogicTest.cs:69-71,84`。`LogAssert.NoUnexpectedReceived`〉／`PerMinuteRateRow` は小数 1 桁）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: Parts の型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`LackHighlightText.cs`:
```csharp
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    public enum LackHighlightTextSize
    {
        Xs,
        Sm,
    }

    /// <summary>
    /// 現在値と要求値の文。不足のときだけ警告色にする（Web details/LackHighlightText の写し）
    /// Current-versus-required text that turns the warning colour only when lacking (port of Web details/LackHighlightText)
    /// </summary>
    public sealed class LackHighlightText : LocalizedLabel
    {
        public const string ClassName = "lack-highlight-text";
        public const string InsufficientClassName = "lack-highlight-text--insufficient";

        public LackHighlightText(string testid, LackHighlightTextSize size)
        {
            if (testid != null) name = testid;
            AddToClassList(ClassName);
            AddToClassList(size == LackHighlightTextSize.Xs ? UiTypographyClass.TextXs : UiTypographyClass.TextSm);
        }

        public void Render(UiText text, bool insufficient)
        {
            SetText(text);
            EnableInClassList(InsufficientClassName, insufficient);
        }
    }
}
```

`Parts.uss`（先頭に出所の 2 行）:
```css
/* web: features/blockInventory/details/LackHighlightText/style.module.css .text（本文色）と [data-insufficient="true"]（警告色）、BlockItemGrid.tsx の BlockItemGridGroup（Stack gap 4・ラベル --text-muted）を写す */
/* web: ports .text (body colour) and [data-insufficient="true"] (warning colour) from LackHighlightText, and BlockItemGridGroup (Stack gap 4, label --text-muted) from BlockItemGrid.tsx */
.lack-highlight-text {
    color: var(--text-default);
}

.lack-highlight-text--insufficient {
    color: var(--text-insufficient);
}

.per-minute-rate-row__text {
    color: var(--text-default);
}

/* :root の列数を BlockItemGrid の customStyle で読めるよう自クラスで受け直す（前例 GaugeBar.uss） */
/* re-declare the :root column count on the grid's own class so its customStyle can read it (precedent GaugeBar.uss) */
.block-item-grid {
    --block-item-grid-inventory-columns: var(--inventory-columns);
}
```
（グループのラベル色は `UiTypographyClass.TextMuted` を付けて画面 USS に書かない。間は `UiStack(UiGap.Px4)`）

`BlockItemGrid.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Commands;
using Client.Ui.State.Items;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// ブロックの枠を持ち物と同じ列数の上限で並べ、押下をブロック側のコマンドへ渡す（Web BlockItemGrid の写し）
    /// Lays out block slots capped at the inventory column count and routes presses to the block commands (port of Web BlockItemGrid)
    /// </summary>
    public sealed class BlockItemGrid : VisualElement
    {
        private const string ClassName = "block-item-grid";
        private const string InventoryColumnsProperty = "--block-item-grid-inventory-columns";

        private readonly string _testId;
        private readonly IBlockSlotCommands _commands;
        private readonly GameContentPresentation _content;
        private readonly UiCommandResultToaster _toaster;
        private readonly SlotGrid _grid;
        private readonly List<(int Index, ItemSlot Slot)> _slots = new();
        private int _inventoryColumns;

        public BlockItemGrid(string testId, IBlockSlotCommands commands, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            _testId = testId;
            _commands = commands;
            _content = content;
            _toaster = toaster;
            _grid = new SlotGrid(testId, 1);
            Add(_grid);
            AddToClassList(ClassName);

            // 列数の上限は持ち物と同じトークン（Web INVENTORY_GRID_COLUMNS）。:root の値は子へ継承されないので自クラスで受け直した名前を読む
            // The column cap is the inventory's own token (Web INVENTORY_GRID_COLUMNS); :root values do not inherit, so read the name re-declared on this class
            RegisterCallback<CustomStyleResolvedEvent>(_ => ReadColumns());
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<int> slotIndices)
        {
            // 並ぶ枠の番号が変わったときだけ作り直し、同じなら中身だけ差し替える
            // Rebuild only when the slot indices change; otherwise just swap contents
            if (!SameIndices(slotIndices)) Rebuild(slotIndices);
            foreach (var (index, slot) in _slots) slot.SetContent(ToContent(itemSlots[index]));
            ApplyColumns();

            #region Internal

            bool SameIndices(IReadOnlyList<int> indices)
            {
                if (indices.Count != _slots.Count) return false;
                for (var i = 0; i < indices.Count; i++)
                {
                    if (indices[i] != _slots[i].Index) return false;
                }
                return true;
            }

            void Rebuild(IReadOnlyList<int> indices)
            {
                _grid.ClearItems();
                _slots.Clear();
                foreach (var index in indices)
                {
                    var slot = new ItemSlot($"{_testId}-slot-{index}");
                    slot.OnSlotGestured.Subscribe(gesture => OnGestured(index, gesture));
                    _grid.AddItem(slot);
                    _slots.Add((index, slot));
                }
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

        private void OnGestured(int index, SlotGesture gesture)
        {
            // ブロックの枠は押下・右押下・ダブルクリックだけ（Web useBlockSlotGestures）
            // Block slots react to press, right press and double click only (Web useBlockSlotGestures)
            UiCommandResult result;
            switch (gesture.Kind)
            {
                case SlotGestureKind.LeftDown: result = _commands.LeftDown(index, gesture.Shift); break;
                case SlotGestureKind.RightDown: result = _commands.RightDown(index); break;
                case SlotGestureKind.DoubleClick: result = _commands.DoubleClick(index); break;
                // silent-ok: 進入とホバーはブロックの枠では使わない（Web と同じ） / 名前のツールチップは ItemSlot と C3-b のホストが出す
                default: return;
            }
            _toaster.Report(BlockSlotCommands.CommandName, result);
        }

        private void ReadColumns()
        {
            if (UssCustomStyleReader.TryReadNumber(customStyle, InventoryColumnsProperty, out var columns)) _inventoryColumns = (int)columns;
            ApplyColumns();
        }

        private void ApplyColumns()
        {
            // silent-ok: 最初のスタイル解決の前は列数を決めない / CustomStyleResolvedEvent で当て直し、読めなければ UssCustomStyleReader が LogError を出す
            if (_inventoryColumns <= 0) return;
            _grid.SetColumns(System.Math.Min(_inventoryColumns, System.Math.Max(1, _slots.Count)));
        }
    }
}
```
（`UiCommandResultToaster` は `Client.Ui.State.Toast`〈`ScreenTestHost` の using で確かめた〉。`:root` のトークンは当てた要素の `customStyle` にしか出ず子へ継承されないので〈`UssCustomStyleReader.cs:10-13` の remarks〉、`Parts.uss` の `.block-item-grid` で `--block-item-grid-inventory-columns: var(--inventory-columns)` と受け直し、C# はその名前を読む〈前例 `GaugeBar.uss:6`・`GaugeBar.cs:12`。D-8〉）

`BlockItemGridGroup.cs`（`UiStack(UiGap.Px4)` を継がず中に持つ。ラベルは `LocalizedLabel` に `text--sm`・`text--muted`）、`BlockItemGridFactory.cs`（ctor で `IBlockSlotCommands`・`GameContentPresentation`・`UiCommandResultToaster` を受け、`Create(testId)` で `BlockItemGrid` を返す）

`PowerRateText.cs`:
```csharp
using System;
using System.Globalization;
using Client.Localization;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts.Rows
{
    /// <summary>
    /// 電力率の文。不足の赤は呼び手が許したときだけ（Web PowerRateText の写し）
    /// Power-rate text, red when lacking only if the caller allows it (port of Web PowerRateText)
    /// </summary>
    public sealed class PowerRateText : VisualElement
    {
        private readonly LackHighlightText _text;

        public PowerRateText(string testid)
        {
            _text = new LackHighlightText(testid, LackHighlightTextSize.Sm);
            Add(_text);
        }

        public void Render(double currentPower, double requestPower, bool highlightShortage)
        {
            // 百分率は JS の Math.round と同じく 0.5 を切り上げる（C# の既定は偶数丸め）
            // The percentage rounds half up like JS Math.round (C# defaults to banker's rounding)
            var rate = BlockDetailMath.ComputePowerRate(currentPower, requestPower);
            var percent = (int)Math.Floor(rate * 100 + 0.5);
            var text = new UiText(LocalizationKeys.Ui.BlockInventory.PowerRateSummary, new[]
            {
                new LocalizationTextParam("rate", percent.ToString(CultureInfo.InvariantCulture)),
                new LocalizationTextParam("current", currentPower.ToString(CultureInfo.InvariantCulture)),
                new LocalizationTextParam("required", requestPower.ToString(CultureInfo.InvariantCulture)),
            });
            _text.Render(text, highlightShortage && rate < 1);
        }
    }
}
```
（`PowerRateText` は `LackHighlightText` を 1 つ持つだけの箱。親の間を崩さないよう `.power-rate-text` に余白を足さない。`LocalizationTextParam` の ctor は実物〈`Name`・`Value`〉に合わせる）

`MachineStateRow.cs`:
```csharp
using Client.Localization;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.Text;

namespace Client.Ui.Screens.BlockInventory.Parts.Rows
{
    /// <summary>
    /// 稼働ラベルと電力率の組。Web は fragment なので、親の UiGapContainer へ 2 要素を直接足して親の間を保つ
    /// The state label paired with the power rate; the Web uses a fragment, so both elements go straight into the parent UiGapContainer to keep its gap
    /// </summary>
    public sealed class MachineStateRow
    {
        private readonly LackHighlightText _state;
        private readonly PowerRateText _powerRate;
        private UiGapContainer _parent;

        public MachineStateRow(string stateTestId, string powerRateTestId)
        {
            _state = new LackHighlightText(stateTestId, LackHighlightTextSize.Sm);
            _powerRate = new PowerRateText(powerRateTestId);
        }

        public void MountInto(UiGapContainer parent)
        {
            _parent = parent;
            parent.AddItem(_state);
            parent.AddItem(_powerRate);
        }

        public void Render(MachineProcessDisplayState state, double currentPower, double requestPower, bool highlightPowerShortage)
        {
            var display = BlockDetailStateDisplay.Machine(state);
            _state.Render(new UiText(display.LabelKey), display.Insufficient);
            // 隠すときも文は先に入れる（文無しの LocalizedLabel が載ると LogError）
            // Always set the text first, even when hiding (a mounted text-less LocalizedLabel logs an error)
            _powerRate.Render(currentPower, requestPower, highlightPowerShortage);
            _parent.SetItemShown(_powerRate, display.ShowPowerRate);
        }
    }
}
```

`PerMinuteRateRow.cs`（`UiGroup(UiGap.Px4)` を中に持ち、先頭に呼び手の要素〈アイコン〉、後ろに `LocalizedLabel`〈`text--xs`・`per-minute-rate-row__text`〉。`Render` は `ui.blockInventory.itemsPerMinute`・`itemsPerMinute = amount.ToString("F1", CultureInfo.InvariantCulture)`。Web の `toFixed(1)` と同じく 0.05 の丸めは表示の差が出うる〈JS は 2 進の値で丸める〉ので、テストは 0.25 等の境を避けた値で書く）

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.BlockInventory.Parts"`
Expected: `ErrorCount: 0`、6 件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Parts
git commit -m "feat(uitk): ブロックインベントリの部品（不足強調の文・ブロックの枠の並び・稼働と率の行）を足す（S4-alpha）

<実行ホストの指示する帰属行>"
```

---

### Task 7: 拡張口とレジストリ・標準の本文（`Screens/BlockInventory/Registry/`・`SectionStack/`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Registry/IBlockInventoryPart.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Registry/IBlockInventoryBodyFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Registry/IBlockInventoryTitleActionFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Registry/ITrainInventoryBodyFactory.cs`（`ITrainInventoryPart` も同じファイル）
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Registry/BlockInventoryBodyMount.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Registry/BlockInventoryPartRegistry.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Registry/EmptyBlockInventoryParts.cs`（`EmptyBlockInventoryPart`・`EmptyTrainInventoryPart`。Root は空の `VisualElement`、Attach・Detach は何もしない。題の操作が無い・列車の本文が未登録のときの Null Object。D-35）
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackSlot.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/IBlockInventorySection.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/ISectionStackSectionFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackSectionMount.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackSlotRegistry.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackBody.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackBodyFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackItemGridSection.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStack.uss`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/SectionStack/FakeSection.cs`・`SectionStackViewTest.cs`・`SectionStackRegressionTest.cs`・`SectionStackSlotRegistryTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/BlockInventoryPartRegistryTest.cs`・`BlockInventoryTestFixtures.cs`

**Interfaces:**
- Consumes: Task 3 の `OpenBlockInventoryPanel`・`OpenTrainInventoryPanel`・`BlockInventoryPanelState.IsSamePanel`・`IBlockInventoryPanelSource`・`SectionStackConfigTable`・`ShownSectionStackItemGrid`、Task 6 の `BlockItemGridFactory`、L0-s4b の `BlockInventoryBodyKind`・`BlockInventoryTitleActionKind`・`BlockDetailMath.AllSlotIndices(int) : int[]`、`UiStack(UiGap)`・`UiGapContainer.AddItem / SetItemShown`
- Produces: 末尾「## 後続レーンへの Produces 一覧」の (1)〜(4) と同じ型

**口の形（設計書 §4.2・A-57。D-19）:** 差し込む部分は「Presenter ＋ View」。工場が作る `IBlockInventoryPart` は Presenter で、`Root` が View。`Attach(panel)` で Presenter が自分の Model（各レーンの State の詳細 Model。`OpenBlockDetailTracker` 等を購読する）を購読して View の `Render` へ流し、`Detach()` で購読を切る。View は購読しない。パネルの状態（`OpenBlockInventoryPanel`）は識別子・エラー・枠だけで、区画の詳細の源ではない。

- [ ] **Step 1: 失敗するテストを書く**

`FakeSection.cs`（区画の偽 Presenter: 固定の name を持つ `VisualElement` を `Root` にし、`IsApplicable` を外から書ける `ReactiveProperty<bool>` で持ち、`Attach`・`Detach` の回数を数える。偽の工場 `FakeSectionFactory(SectionStackSlot slot, bool applicable)`〈name は `fake-<Slot>`〉と、全 Slot〈`ItemGrid` を除く〉を返す `FakeSectionFactory.AllSlots(bool applicable)` も同じファイル）

`BlockInventoryTestFixtures.cs`（`BlockInventoryTestBodies.SectionStack(FakePanelSource source, string blockType, params ISectionStackSectionFactory[] plugged)` — 本物の `BlockItemGridFactory`〈コマンドは記録係〉・`SectionStackSlotRegistry(plugged)`・`SectionStackBodyFactory` で標準の本文を作って返す。`BlockInventoryTestPanels.OfType(blockType, params SlotItem[])` は Task 3 の `BlockInventoryTestPanels.cs` に足す。マスタの無い blockType〈"unknown"〉も組めるよう `OpenBlockInventoryPanel` の internal ctor を呼ぶ〈`Client.Ui.State/AssemblyInfo.cs` に `[assembly: InternalsVisibleTo("Client.Tests")]` があることを起草時に確かめた〉）

`SectionStackViewTest.cs`（alpha 分 3 件＋出し入れの 1 件。区画の出し入れは区画ルートの display で見る。`SetItemShown` が当たるのは区画のルートなので、中の `SlotGrid` の親ではなく `section-stack-item-grid` の display を見る）:
```csharp
using System.Linq;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文は Web SectionStackView の区画の順と項目グリッドの出し分けを保つ（L1）
    /// The standard body keeps the Web SectionStackView section order and item-grid visibility (L1)
    /// </summary>
    public class SectionStackViewTest : ScreenTestHost
    {
        private const string ItemGridSectionName = "section-stack-item-grid";

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">%s の既存表示を保つ</web-test>
        [TestCase("Chest", "chest-grid", true)]
        [TestCase("ElectricMachine", null, false)]
        [TestCase("GearMachine", null, false)]
        [TestCase("ElectricGenerator", "generator-fuel-grid", false)]
        [TestCase("FuelGearGenerator", "generator-fuel-grid", true)]
        [TestCase("SimpleGearGenerator", "generator-fuel-grid", true)]
        [TestCase("ElectricMiner", "miner-output-grid", true)]
        [TestCase("GearMiner", "gear-miner-output-grid", true)]
        [TestCase("CleanRoomMachine", null, false)]
        [TestCase("CleanRoomAirFilter", "clean-room-filter-grid", true)]
        [TestCase("unknown", "generic-block-grid", false)]
        public void 項目グリッドは設定どおり空でも出すか枠があるときだけ出す(string blockType, string gridId, bool renderEmptyGrid)
        {
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType(blockType)));
            var body = BlockInventoryTestBodies.SectionStack(source, blockType);
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)source.Current.Value);
            simulate.FrameUpdate();
            Assert.AreEqual(gridId != null && renderEmptyGrid, IsShown(body.Root, ItemGridSectionName));

            source.State.Value = BlockInventoryTestPanels.OfType(blockType, new SlotItem(new ItemId(1), 1));
            simulate.FrameUpdate();
            Assert.AreEqual(gridId != null, IsShown(body.Root, ItemGridSectionName));
            if (gridId != null) Assert.AreEqual(gridId, body.Root.Q(ItemGridSectionName).Q<SlotGrid>().name);
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">親から状態・供給・歯車・電力網・歯車網を順に描画する</web-test>
        [Test]
        public void 区画は固定の順に並ぶ()
        {
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType("GearToElectricGenerator")));
            var body = BlockInventoryTestBodies.SectionStack(source, "GearToElectricGenerator", FakeSectionFactory.AllSlots(true));
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)source.Current.Value);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "fake-Context", "fake-Machine", "fake-Miner", "fake-Pump", "fake-Generator", "fake-GearToElectric", "fake-BeltThroughput", "fake-Gear", "fake-ElectricNetwork", "fake-GearNetwork", "fake-FluidDetails", "fake-FluidRow" }, ShownNames(body.Root));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">部屋欄はセクション列の先頭に描く</web-test>
        [Test]
        public void 文脈の区画は項目グリッドより上に来る()
        {
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType("CleanRoomAirFilter", new SlotItem(new ItemId(12), 5))));
            var body = BlockInventoryTestBodies.SectionStack(source, "CleanRoomAirFilter", new FakeSectionFactory(SectionStackSlot.Context, true));
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)source.Current.Value);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "fake-Context", ItemGridSectionName }, ShownNames(body.Root));
            Assert.AreEqual("clean-room-filter-grid", body.Root.Q(ItemGridSectionName).Q<SlotGrid>().name);
        }

        [Test]
        public void 区画の出し入れはパネルの描き直し無しで変わり先頭の間隔が詰まる()
        {
            // 網の集約・ベルトの統計のようにパネルの組み直しの外で届く変化でも、区画の通知だけで出し入れと間隔が組み直される
            // Even for changes arriving outside panel rebuilds (network totals, belt stats), the section's own signal re-lays visibility and gaps
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType("GearToElectricGenerator")));
            var context = new FakeSectionFactory(SectionStackSlot.Context, false);
            var machine = new FakeSectionFactory(SectionStackSlot.Machine, true);
            var body = BlockInventoryTestBodies.SectionStack(source, "GearToElectricGenerator", context, machine);
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)source.Current.Value);
            simulate.FrameUpdate();
            Assert.AreEqual(0f, machine.Created.Root.resolvedStyle.marginTop);

            context.Created.Applicable.Value = true;
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "fake-Context", "fake-Machine" }, ShownNames(body.Root));
            Assert.AreEqual(0f, context.Created.Root.resolvedStyle.marginTop);
            Assert.Less(0f, machine.Created.Root.resolvedStyle.marginTop);   // UiGap.Sm の実値（UiStack の USS）
        }

        private static bool IsShown(VisualElement root, string name)
        {
            return root.Q(name) is { } section && section.resolvedStyle.display == DisplayStyle.Flex;
        }

        private static string[] ShownNames(VisualElement root)
        {
            return root.Children().Where(e => e.resolvedStyle.display == DisplayStyle.Flex).Select(e => e.name).ToArray();
        }
    }
}
```
（`FakePanelSource` に書き換え用の `State`〈`ReactiveProperty`〉を公開する。`FakeSectionFactory.Created` は最後に作った `FakeSection`）

`SectionStackRegressionTest.cs`（alpha 分 1 件。Web の 12 ケースのうち項目グリッドの id だけを主張する。区画の id は持ち主のレーンが自分のテストファイルに同じタグで写す。専用本文の FilterSplitter のケースは S4-special が写す〈写す予定の持ち主。「写すテスト」表〉）:
```csharp
        /// <web-test file="e2e/tests/regression/sectionStack.spec.ts">${entry.type}は対応セクションだけを表示する</web-test>
        [TestCase("Chest", "chest-grid")]
        [TestCase("GearMachine", null)]
        [TestCase("GearMiner", "gear-miner-output-grid")]
        [TestCase("ElectricGenerator", "generator-fuel-grid")]
        [TestCase("FuelGearGenerator", "generator-fuel-grid")]
        [TestCase("unknown", "generic-block-grid")]
        public void 項目グリッドは対応するblockTypeだけに出る(string blockType, string gridId)
```
（枠 1 つの中身ありで `Attach` し、`gridId` が null なら `section-stack-item-grid` が出ない、あれば出て中の `SlotGrid.name == gridId`）

`SectionStackSlotRegistryTest.cs`（3 件: 同じ Slot の二重登録は例外／`ItemGrid` の外部登録は例外／空きの Slot は起動時に 1 回だけ情報ログ）

`BlockInventoryPartRegistryTest.cs`（5 件: `SectionStack` の Kind は標準の本文／登録のある Kind はその工場／`未登録の本文Kindは標準の本文へ逃げて警告を1回だけ出す`〈FilterSplitter のパネルで `LogAssert.Expect(LogType.Warning, …)`、2 回目は出ない〉／同じ Kind の二重登録と `SectionStack` の外部登録は例外／列車の本文の 2 本目は例外）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: Registry・SectionStack の型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`IBlockInventoryPart.cs`:
```csharp
using Client.Ui.State.BlockInventory.Panel;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// パネルへ差し込む部分の Presenter（本文・題の操作・区画）。Root が View。Attach で自分の Model を購読して View へ流し、Detach で切る
    /// Presenter of a part plugged into the panel (body, title action, section); Root is the view. Attach subscribes its own model into the view, Detach unsubscribes
    /// </summary>
    public interface IBlockInventoryPart
    {
        VisualElement Root { get; }

        // 同じパネル（BlockInventoryPanelState.IsSamePanel）のあいだは 1 回だけ。別のパネルでは Detach の後に新しい部分で呼ばれる
        // Called once per same panel (BlockInventoryPanelState.IsSamePanel); a different panel gets a fresh part after Detach
        void Attach(OpenBlockInventoryPanel panel);
        void Detach();
    }
}
```

`IBlockInventoryBodyFactory.cs`・`IBlockInventoryTitleActionFactory.cs`・`ITrainInventoryBodyFactory.cs`・`BlockInventoryBodyMount.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.Panel;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 拡張口 (1): blockType ごとの専用本文（Kind は L0-s4b の BlockComponentRegistry が blockType から引く）
    /// Extension port (1): a dedicated body per blockType (L0-s4b BlockComponentRegistry resolves the Kind from blockType)
    /// </summary>
    public interface IBlockInventoryBodyFactory
    {
        BlockInventoryBodyKind Kind { get; }
        IBlockInventoryPart Create(BlockInventoryBodyMount mount);
    }
}
```
```csharp
using Client.Ui.State.BlockInventory.Logic.Panel;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 拡張口 (2): blockType ごとの題の操作（ロケットの発射。ブロック・エラー無しのときだけ出す）
    /// Extension port (2): a title action per blockType (the rocket launch; shown only for blocks without errors)
    /// </summary>
    public interface IBlockInventoryTitleActionFactory
    {
        BlockInventoryTitleActionKind Kind { get; }
        IBlockInventoryPart Create();
    }
}
```
```csharp
using Client.Ui.State.BlockInventory.Panel;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 拡張口 (4): source=train の本文（Web TrainInventoryBody。登録は 1 本だけ）
    /// Extension port (4): the source=train body (Web TrainInventoryBody; exactly one registration)
    /// </summary>
    public interface ITrainInventoryBodyFactory
    {
        ITrainInventoryPart Create();
    }

    // 列車の本文の Presenter。Attach で列車の詳細 Model（S4-train の State）を購読し View へ流す
    // Train body presenter; Attach subscribes the train detail model (S4-train State) into the view
    public interface ITrainInventoryPart
    {
        VisualElement Root { get; }
        void Attach(OpenTrainInventoryPanel panel);
        void Detach();
    }
}
```
```csharp
namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 本文を作るときの条件（Web の fillsPanelHeight。大型パネルだけ真）
    /// Conditions when creating a body (Web fillsPanelHeight; true only on the large panel)
    /// </summary>
    public sealed class BlockInventoryBodyMount
    {
        public bool FillsPanelHeight { get; private set; }

        public BlockInventoryBodyMount(bool fillsPanelHeight)
        {
            FillsPanelHeight = fillsPanelHeight;
        }
    }
}
```

`BlockInventoryPartRegistry.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Panel;
using UnityEngine;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 差し込まれた工場から本文・題の操作・列車の本文を引く。未登録の Kind は標準の本文へ逃げ、警告を 1 回だけ出す
    /// Resolves bodies, title actions and the train body from plugged factories; unregistered kinds fall back to the standard body with a one-time warning
    /// </summary>
    public sealed class BlockInventoryPartRegistry
    {
        private readonly Dictionary<BlockInventoryBodyKind, IBlockInventoryBodyFactory> _bodies = new();
        private readonly Dictionary<BlockInventoryTitleActionKind, IBlockInventoryTitleActionFactory> _titleActions = new();
        private readonly ITrainInventoryBodyFactory _trainBody;   // 登録 0 本は null（S4-train 未着の間。CreateTrainBody の中だけで分ける）
        private readonly SectionStackBodyFactory _sectionStack;
        private readonly HashSet<string> _warned = new();

        public BlockInventoryPartRegistry(IReadOnlyList<IBlockInventoryBodyFactory> bodies, IReadOnlyList<IBlockInventoryTitleActionFactory> titleActions, IReadOnlyList<ITrainInventoryBodyFactory> trainBodies, SectionStackBodyFactory sectionStack)
        {
            // 二重登録と既定の本文の上書きは起動時に止める（どの登録が衝突したかを例外の文で示す）
            // Stop at startup on duplicates or an override of the default body (the message names the colliding registration)
            foreach (var body in bodies)
            {
                if (body.Kind == BlockInventoryBodyKind.SectionStack) throw new InvalidOperationException($"SectionStack の本文は S4-alpha の既定。差し込めない: {body.GetType().Name}");
                if (!_bodies.TryAdd(body.Kind, body)) throw new InvalidOperationException($"本文の Kind が二重登録: {body.Kind} {body.GetType().Name}");
            }
            foreach (var action in titleActions)
            {
                if (!_titleActions.TryAdd(action.Kind, action)) throw new InvalidOperationException($"題の操作の Kind が二重登録: {action.Kind} {action.GetType().Name}");
            }
            if (1 < trainBodies.Count) throw new InvalidOperationException($"列車の本文は 1 本だけ登録する: {trainBodies.Count} 本");
            _trainBody = trainBodies.Count == 1 ? trainBodies[0] : null;
            _sectionStack = sectionStack;
        }

        public IBlockInventoryPart CreateBody(OpenBlockInventoryPanel panel, BlockInventoryBodyMount mount)
        {
            var kind = panel.Components.Body;
            if (kind == BlockInventoryBodyKind.SectionStack) return _sectionStack.Create(panel.BlockType, mount);
            if (_bodies.TryGetValue(kind, out var factory)) return factory.Create(mount);
            WarnOnce($"body:{kind}", $"[BlockInventory] 本文 {kind} の登録が無いので標準の本文で描く（blockType={panel.BlockType}）");
            return _sectionStack.Create(panel.BlockType, mount);
        }

        // 題の操作を持たない（None）か登録が無ければ空の部分。登録漏れだけ警告する（「無い」は None 1 本で表し null を使わない）
        // An empty part when the kind is None or unregistered; only a missing registration warns ("none" is the None kind alone, never null)
        public IBlockInventoryPart CreateTitleAction(BlockInventoryTitleActionKind kind)
        {
            // silent-ok: 題の操作を持たない blockType が大半（Web も描かない） / 登録漏れは下の警告で出る
            if (kind == BlockInventoryTitleActionKind.None) return new EmptyBlockInventoryPart();
            if (_titleActions.TryGetValue(kind, out var factory)) return factory.Create();
            WarnOnce($"title:{kind}", $"[BlockInventory] 題の操作 {kind} の登録が無いので出さない");
            return new EmptyBlockInventoryPart();
        }

        public ITrainInventoryPart CreateTrainBody()
        {
            if (_trainBody != null) return _trainBody.Create();
            WarnOnce("train", "[BlockInventory] 列車の本文の登録が無いので題とエラー行だけを出す");
            return new EmptyTrainInventoryPart();
        }

        private void WarnOnce(string key, string message)
        {
            if (_warned.Add(key)) Debug.LogWarning(message);
        }
    }
}
```

`SectionStackSlot.cs`:
```csharp
namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の固定順の区画（Web SectionStackView の並び。列挙の順が描く順）
    /// Fixed-order sections of the standard body (Web SectionStackView order; enum order is render order)
    /// </summary>
    public enum SectionStackSlot
    {
        Context,          // 清浄室・抽選・搬入停止（S4-machine。機械では Machine の中へ前置する）
        ItemGrid,         // 項目グリッド（S4-alpha。差し込めない）
        Machine,          // S4-machine
        Miner,            // S4-machine
        Pump,             // S4-machine
        Generator,        // S4-machine
        GearToElectric,   // S4-special
        BeltThroughput,   // S4-special
        Gear,             // S4-special
        ElectricNetwork,  // S4-special
        GearNetwork,      // S4-special
        FluidDetails,     // S4-machine（Fluid/。Config.Fluid が DetailsSectionStackFluid）
        FluidRow,         // S4-machine（Fluid/。Config.Fluid が RowSectionStackFluid で機械以外）
    }
}
```

`IBlockInventorySection.cs`・`ISectionStackSectionFactory.cs`・`SectionStackSectionMount.cs`:
```csharp
using Client.Ui.Screens.BlockInventory.Registry;
using UniRx;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の 1 区画の Presenter。描くかどうかは区画が自分の Model から決めて通知する（Web の各 Section が null を返す条件）
    /// Presenter of one standard-body section; the section decides from its own model whether it draws and signals it (the condition each Web Section returns null on)
    /// </summary>
    public interface IBlockInventorySection : IBlockInventoryPart
    {
        // Attach 後に有効。パネルの組み直しの外（網の集約・ベルトの統計）で変わってよく、SectionStackBody が購読して出し入れと間隔を組み直す
        // Valid after Attach; may change outside panel rebuilds (network totals, belt stats); SectionStackBody subscribes to re-lay visibility and gaps
        IReadOnlyReactiveProperty<bool> IsApplicable { get; }
    }
}
```
```csharp
namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 拡張口 (3): 固定順の区画への差し込み（1 Slot に 1 本）
    /// Extension port (3): plugging into a fixed-order section (one per slot)
    /// </summary>
    public interface ISectionStackSectionFactory
    {
        SectionStackSlot Slot { get; }
        IBlockInventorySection Create(SectionStackSectionMount mount);
    }
}
```
```csharp
using Client.Ui.State.BlockInventory.SectionStack;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 区画を作るときの条件（blockType の設定と高さ確定）
    /// Conditions when creating a section (the blockType settings and the fixed-height flag)
    /// </summary>
    public sealed class SectionStackSectionMount
    {
        public SectionStackConfig Config { get; private set; }
        public bool FillsPanelHeight { get; private set; }

        public SectionStackSectionMount(SectionStackConfig config, bool fillsPanelHeight)
        {
            Config = config;
            FillsPanelHeight = fillsPanelHeight;
        }
    }
}
```
（名前空間 `Client.Ui.State.BlockInventory.SectionStack` と `Client.Ui.Screens.BlockInventory.SectionStack` は別アセンブリで衝突しない）

`SectionStackSlotRegistry.cs`（ctor で `IReadOnlyList<ISectionStackSectionFactory>` を Slot で索引し、二重・`ItemGrid` は例外、空きの Slot を起動時に 1 回 `Debug.Log` で並べる。`IBlockInventorySection CreateOrNull(SectionStackSlot slot, SectionStackSectionMount mount)` は空きなら null〈直前の行に `// silent-ok: 担当レーンが未マージの区画は Web の null と同じ見え方 / 起動時の情報ログに空きの Slot が並ぶ`〉）

`SectionStackItemGridSection.cs`（区画の Presenter。データはパネルの枠なので `IBlockInventoryPanelSource` を購読する）:
```csharp
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SectionStack;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の項目グリッドの Presenter（Web の showItemGrid）。同じパネルのあいだ枠の変化を BlockItemGrid へ流す
    /// Presenter of the standard body's item grid (Web showItemGrid); streams slot changes into BlockItemGrid while the panel stays the same
    /// </summary>
    public sealed class SectionStackItemGridSection : IBlockInventorySection
    {
        private readonly SectionStackItemGrid _config;
        private readonly IBlockInventoryPanelSource _panels;
        private readonly BlockItemGrid _grid;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root { get; private set; }
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;   // 型を狭める公開（前例 SettingsModel.Current）

        public SectionStackItemGridSection(SectionStackItemGrid config, BlockItemGridFactory grids, IBlockInventoryPanelSource panels)
        {
            _config = config;
            _panels = panels;
            Root = new VisualElement { name = "section-stack-item-grid" };
            // 出さない設定でも器は作り、IsApplicable が偽のまま
            // Build the holder even when hidden by settings; IsApplicable stays false
            _grid = grids.Create(config is ShownSectionStackItemGrid shown ? shown.GridId : "section-stack-no-item-grid");
            Root.Add(_grid);
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            _panels.Current.Subscribe(current =>
            {
                // silent-ok: 別のパネルへ替わる途中の値は描かない / BodyHost が同じフレームで Detach して新しい区画を作る
                if (!(current is OpenBlockInventoryPanel open) || !open.IsSamePanel(panel)) return;
                _isApplicable.Value = _config is ShownSectionStackItemGrid shown && (shown.RenderEmpty || 0 < open.ItemSlots.Count);
                if (_isApplicable.Value) _grid.Render(open.ItemSlots, BlockDetailMath.AllSlotIndices(open.ItemSlots.Count));
            }).AddTo(_subscriptions);
        }

        public void Detach()
        {
            _subscriptions.Clear();
        }
    }
}
```

`SectionStackBody.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の Presenter。固定順の区画を UiStack(Sm) に並べ、各区画の IsApplicable を購読して出し入れと間隔を組み直す（Web SectionStackView の写し）
    /// Standard-body presenter: fixed-order sections in a UiStack(Sm), subscribing each section's IsApplicable to re-lay visibility and gaps (port of Web SectionStackView)
    /// </summary>
    public sealed class SectionStackBody : IBlockInventoryPart
    {
        public const string FillPanelHeightClassName = "section-stack--fill-panel-height";

        private readonly UiStack _stack = new(UiGap.Sm) { name = "section-stack" };
        private readonly IReadOnlyList<IBlockInventorySection> _sections;
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root { get; private set; }

        public SectionStackBody(IReadOnlyList<IBlockInventorySection> sections, bool fillsPanelHeight)
        {
            // 高さ確定の面でだけ伸ばす（高さ自動の面で伸ばすと子が内容高より縮む経路が開く）
            // Stretch only on the height-determining panel (on auto-height panels it would let children shrink below content)
            Root = _stack;
            _stack.EnableInClassList(FillPanelHeightClassName, fillsPanelHeight);
            _sections = sections;
            foreach (var section in sections)
            {
                _stack.AddItem(section.Root);
                _stack.SetItemShown(section.Root, false);
            }
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            foreach (var section in _sections)
            {
                section.Attach(panel);
                section.IsApplicable.Subscribe(shown => _stack.SetItemShown(section.Root, shown)).AddTo(_subscriptions);
            }
        }

        public void Detach()
        {
            _subscriptions.Clear();
            foreach (var section in _sections) section.Detach();
        }
    }
}
```

`SectionStackBodyFactory.cs`（ctor で `BlockItemGridFactory`・`SectionStackSlotRegistry`・`IBlockInventoryPanelSource`。`Create(string blockType, BlockInventoryBodyMount mount)` が `SectionStackConfigTable.Resolve(blockType)` から `SectionStackSectionMount` を作り、`SectionStackSlot` の列挙順に `ItemGrid` は `new SectionStackItemGridSection(config.ItemGrid, grids, panels)`、他は `slots.CreateOrNull(slot, sectionMount)`〈null は飛ばす〉で区画の列を組んで `new SectionStackBody(sections, mount.FillsPanelHeight)` を返す）

`SectionStack.uss`:
```css
/* web: features/blockInventory/style.module.css .fillPanelHeight（flex: 1; min-height: 0）を標準の本文の高さ確定に写す */
/* web: ports .fillPanelHeight (flex: 1; min-height: 0) onto the standard body's fixed-height mode */
.section-stack--fill-panel-height {
    flex-grow: 1;
    flex-shrink: 1;
    min-height: 0;
}
```

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.BlockInventory"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel
git commit -m "feat(uitk): ブロックインベントリの拡張口・レジストリ・標準の本文を足す（S4-alpha）

<実行ホストの指示する帰属行>"
```

---

### Task 8: 外殻 View・ScreenPresenter・登録（`Screens/BlockInventory/`・`Client.Starter/Registration/`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventoryView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventoryPanelBand.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventoryNaturalScroll.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventoryBodyHost.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventoryScreenPresenter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventory.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/BlockInventoryScreenVisibility.cs`（`Panel/` は 10 ファイル）
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/BlockInventoryGameUiRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（共有サービスの並び〈`InputSystemPointerPositionStream` の下〉にブロックインベントリの共有サービスを足し、画面登録区画の末尾に 1 行。E-1・A-56）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/GrabGameUiRegistration.cs`（11 行のコメント「押下の配線とブロック側の実行器は S4-alpha が足す」を「ブロック側の実行器とスロット操作は S4-alpha が MainGameUiRegistration の共有サービスの並びに足す」に直すだけ。コードは変えない）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs`（`{ AnchorIds.InventoryCloseButton, "S4-alpha" },` の行を消す。印と保留表の両方にあると `TutorialAnchorCoverageTest` が赤になる。E-1）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（105 行 `IItemStackLevelUnlocker` の隣に `IItemStackLevelLookup` を 1 行）
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`/* == section: Screens == */` の末尾に `@import` 3 行）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/` の `BlockInventoryPanelFlowTest.cs`・`BlockInventoryDesignTest.cs`・`BlockInventoryLocalizationTest.cs`・`BlockInventoryPanelErrorTest.cs`・`BlockInventoryPanelBandTest.cs`・`BlockInventoryTitleActionTest.cs`・`BlockInventoryNaturalScrollTest.cs`（`BlockInventoryTestFixtures.cs` に `BlockInventoryTestShell` を足す）、`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel/BlockInventoryScreenVisibilityTest.cs`

**Interfaces:**
- Consumes: `GamePanel(string, GamePanelFrame)`・`SetTitle(UiText)`・`SetTitleAction(VisualElement)`（題の操作は 1 つの器を 1 回だけ渡し、中身を器の中で付け替える。`GamePanel` に外す API が無いため。D-17）、`IconButton(string, VisualElement)`・`OnClicked`、`IconButtonCloseGlyph`、`GameScrollView(string)`、`TutorialAnchor.Mark(VisualElement, params string[])`、`AnchorIds.InventoryCloseButton`、`StageColumnsLayout.Get(StageColumn.Viewer)`、`UiRoutingState.Current : IReadOnlyReactiveProperty<UiRoutingSnapshot>`（`UiRoutingSnapshot(UiScreen)`・`.Screen`）、`UssCustomStyleReader.TryReadPixels`、`ContentLocalizationKeys.BlockName(Guid)`、`LocalizationKeys.Ui.BlockInventory.TrainInventory`、Task 3 の `IBlockInventoryPanelSource`・`BlockInventoryPanelLayoutModel`・`IBlockInventoryPanelCommands`・`BlockInventoryPanelErrorText`・`BlockInventoryPanelState.IsSamePanel`、Task 7 の `BlockInventoryPartRegistry`
- Produces: `BlockInventoryGameUiRegistration.Register(ContainerBuilder)`（MainGameUiRegistration が呼ぶ）。View の要素名 `block-inventory`・`block-inventory-panel`・`block-inventory-close`・`block-inventory-error`・`train-inventory-error`・`block-inventory-natural-scroll`・`block-inventory-title-action`（録画・後続のテストが引く）

**出し分け（設計書 §4.4。D-21）:** `BlockInventoryScreenPresenter` が `UiRoutingState.Current` とパネルの状態を購読し、`BlockInventoryScreenVisibility.IsShown(snapshot, state)`（`Screen == UiScreen.SubInventory` かつ閉でない。閉は Web の `return null`）で `View.SetShown` する（前例 `PauseMenuScreenPresenter`）。Model 側の SubInventory 判定も `UiRoutingState`（Task 3）で、画面の判定の源は 1 つ。

- [ ] **Step 1: 失敗するテストを書く**

`BlockInventoryScreenVisibilityTest.cs`（L0・3 件: SubInventory かつブロック開で出す／SubInventory でも閉なら出さない／GameScreen ではブロック開の値が残っていても出さない）

`BlockInventoryTestFixtures.cs` に足す `BlockInventoryTestShell.Create(ScreenTestHost host, ReactiveProperty<BlockInventoryPanelState> state, params object[] plugged)`（形はこの 1 本だけ）: 偽の Source・本物の `BlockInventoryPanelLayoutModel`〈寄与は `plugged` の `IBlockInventoryLargePanelRule`〉・記録係の `RecordingPanelCommands`・本物の `BlockInventoryPartRegistry`〈`plugged` の工場〉で `BlockInventoryView` と `BlockInventoryBodyHost` を組み、1280×720 の `StageColumnsLayout` の Viewer 列へ載せ、`state`・大きさの変化を Presenter と同じ順〈`View.Render` → `BodyHost.Apply`〉で流す。出し分けは L0 の `BlockInventoryScreenVisibility` が持つので、Shell は `IsShown` を `UiScreen.SubInventory` 固定で呼ぶ。返り値は `View`・`Commands`・`TitleText()`

`BlockInventoryPanelFlowTest.cs`（alpha 分 2 件＋エラー行の 1 件）:
```csharp
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Panel;
using NUnit.Framework;
using UniRx;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 外殻の開閉と閉じる操作（L1。Web e2e の block inventory の写し）
    /// Shell open/close and the close action (L1; port of the Web e2e block inventory)
    /// </summary>
    public class BlockInventoryPanelFlowTest : ScreenTestHost
    {
        /// <web-test file="e2e/tests/block/blockInventory.spec.ts">chest の block inventory パネルが描画される</web-test>
        [Test]
        public void チェストを開くと題と項目グリッドが出る()
        {
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var shell = BlockInventoryTestShell.Create(this, state);
            state.Value = BlockInventoryTestPanels.Chest();
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.Flex, shell.View.resolvedStyle.display);
            Assert.IsNotNull(shell.View.Q("block-inventory-panel"));
            Assert.IsNotNull(shell.View.Q("chest-grid"));
            Assert.IsNotNull(shell.View.Q("chest-grid-slot-0"));
            Assert.AreEqual("Wooden Chest", shell.TitleText());   // テスト用 mod の英語名。実物の英語名に合わせる
        }

        /// <web-test file="e2e/tests/block/blockInventory.spec.ts">ブロックを閉じると panel が消える</web-test>
        [Test]
        public void 閉じるを押すと遷移を頼み状態が閉になると消える()
        {
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest());
            var shell = BlockInventoryTestShell.Create(this, state);
            simulate.FrameUpdate();

            Click(shell.View.Q<Button>("block-inventory-close"));
            Assert.AreEqual(1, shell.Commands.CloseCount);

            state.Value = BlockInventoryPanelState.Closed;
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, shell.View.resolvedStyle.display);
        }

        [Test]
        public void 閉からチェストを開いてもエラー行の文無しのLogErrorが出ない()
        {
            // エラー無しのパネルではエラー行を作らない（文無しの LocalizedLabel は 1 フレーム後に LogError）
            // No error row is built for an error-free panel (a text-less LocalizedLabel logs an error a frame later)
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var shell = BlockInventoryTestShell.Create(this, state);
            simulate.FrameUpdate();
            state.Value = BlockInventoryTestPanels.Chest();
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            Assert.IsNull(shell.View.Q("block-inventory-error"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
```
（`Click` は `UiTestHost` の既存の押下ヘルパーの実名に合わせる）

`BlockInventoryDesignTest.cs`（alpha 分はタグ 6 種・メソッド 7 本。寸法は 1280×720 の舞台で。後続レーンはこのファイルに足さず、自分のテストファイルに同じタグで写す）:
```csharp
        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [TestCase("BlockInventory/BlockInventory.uss")]
        [TestCase("BlockInventory/SectionStack/SectionStack.uss")]
        [TestCase("BlockInventory/Parts/Parts.uss")]
        public void 画面のUSSは色を直書きせずトークンだけを使う(string relativePath)
        // 中身: ファイルを読み、#rgb・rgb(・rgba( の直書きが無く、color 系の値が var(--…) だけであること

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [Test]
        public void 本文の木の操作の要素は共通部品だけで素の操作部品を含まない()
        // 中身: 項目グリッドのチェストを開き、block-inventory の木を根から hierarchy の子へたどる。Client.Ui.Components 名前空間とその下位の名前空間の型（`Namespace` が `Client.Ui.Components` か `Client.Ui.Components.` で始まる。共通部品。IconButton・PanelActionButton・ModeSwitch・GameTextField・ItemSlot・GameScrollView 等）に当たったらその要素は許し、hierarchy の内部へは降りない（部品の内部は部品の持ち主が保証する。GameScrollView が中に持つ Scroller・Slider を拾わない）。ただしその部品の contentContainer に利用側が足した子は続けて同じ規則でたどる。それ以外の要素で Button・Toggle・RadioButton・Slider・ProgressBar（UnityEngine.UIElements の型そのもの、または派生）が 1 つも無いこと。Web が共通部品 ScrollArea の中を見ないのと同じ境界。Web blockInventoryDesign.test.ts:6-25,41-44 がコンポーネントのソースで禁止部品を見るのの写し（D-46）

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">パネル面と閉じる操作をshared uiへ集約する</web-test>
        [Test]
        public void パネル面はGamePanelで閉じるはIconButtonでチュートリアルの錨が付く()
        // 中身: block-inventory-panel が GamePanel（game-panel--default）、block-inventory-close が IconButton で IconButtonCloseGlyph を持ち、TutorialAnchor の印に AnchorIds.InventoryCloseButton。閉じるの位置 top = --bevel-3・right = --panel-edge-fade の実値

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ブロックパネルをviewer領域へ上端揃えで配置する</web-test>
        [Test]
        public void パネルはViewer列に載り上端が帯の上端に揃う()
        // 中身: block-inventory の親が StageColumnsLayout.Get(Viewer)、worldBound.yMin == 168（--menu-upper-safe-area）、最大高 == 482（帯）

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ブロックパネルだけに下部装飾の安全帯を確保する</web-test>
        [Test]
        public void 下の余白は安全帯で小型だけ右の安全帯を持つ()
        // 中身: 小型で paddingBottom == 58・paddingRight == 28、大型で paddingBottom == 58・paddingRight == 10（GamePanel 既定のまま。Web の大型は右を上書きしない）

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ブロックスロットの列数を1列から持ち物と同じ列数の範囲で内容に追従させる</web-test>
        [TestCase(1, 1)]
        [TestCase(7, 7)]
        [TestCase(10, 10)]
        [TestCase(27, 10)]
        public void 項目グリッドの列数は枠数に追従し持ち物の列数で止まる(int slotCount, int expectedColumns)
        // 中身: 枠数 slotCount のチェストを開き、1 行目の枠の数（worldBound.yMin が先頭と同じ枠の数）が expectedColumns。Error ログ 0 件（受け直したトークンが読めている）

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">レシピ有り機械だけviewer〜items占有の大型パネルへ広げる</web-test>
        [Test]
        public void 寄与が真のときだけ大型の寸法になる()
        // 中身: 寄与 FakeRule(Large=false) で小型（幅は内容なり・高さ ≤ 482）、Large=true にして OnRuleInputChanged を流すと 幅 == 740.108753（±0.01）・高さ == 482・block-inventory--large
```
（各件の本体は上のコメントの主張をそのまま `Assert` に書く）

`BlockInventoryLocalizationTest.cs`（3 件。「blockGuidの導出キーを…」は Web `BlockInventoryPanel.localization.test.ts:52-77` と同じ 3 段: 前例 `PlacementNameTestDictionary` と同形の一時辞書で言語を japanese に固定し、導出キー `ContentLocalizationKeys.BlockName(guid)` の行を (1) english だけに "Fallback Machine" → (2) japanese に "対象言語の機械" → (3) Source だけに "Source Machine" の順に `Merge` し、各段の後に `simulate.FrameUpdate()` して題がその段の文になることを主張する（解決順は `LocalizationTextResolver.cs:15-17` の「対象言語 → english → Source」。(3) は対象言語と english を空にした行で流す）。3 件目「言語を切り替えると題が描き直される」は english → japanese へ言語を変えて題が変わることを見る（Web に対応が無いので `<web-test>` を付けない）。「取得失敗は…」は `BlockInventoryTestPanels.OpenFailedChest()` で `block-inventory-error` の文が `ui.blockInventory.inventoryOpenFailed` の英語文・`chest-grid` が無い・題の操作の器が空）

`BlockInventoryPanelErrorTest.cs`（5 件: `None以外のエラーはすべて文言のキーを持つ`〈`Enum.GetValues` の None 以外で `TryGetLocalizationKey` が真・キーが既定値でない。None は偽〉／ 列車の ContainerMissing / TrainCarMissing / OpenFailed で `train-inventory-error` の文が各キー／列車の本文の登録が無いときは題とエラー行だけで警告ログ 1 回〈`LogAssert.Expect`〉／`正常からエラーへ移ると本文を外しエラーが解けると本文が戻る`〈`Chest()` → `OpenFailedChest()` → `Chest()` の順に流し、`chest-grid` が 有 → 無 → 有、`block-inventory-error` が 無 → 表示 → 要素は残り `resolvedStyle.display == None`〈一度作ったエラー行は外さず隠す〉〉）

`BlockInventoryPanelBandTest.cs`（1 件: 舞台の要素の大きさを変えると次のフレームで帯と大型の幅を当て直す。期待値は舞台の要素の寸法から同じ式で出し、解像度の名前は付けない。2560×1080 での見え方は並べ比べ〈Task 10〉で論理 482・740.1 の拡大になることを見る）

`BlockInventoryTitleActionTest.cs`（3 件: 題の操作の偽工場を `BlockInventoryTitleActionKind.RocketLaunchPadCommand` に登録し、その blockType で器に要素が入る／エラー中は器が空／操作の無い別の blockType へ替えると前の部分の `Detach` が 1 回呼ばれ、器に前の操作の要素が無い〈器の子は空の部分の Root 1 つ〉。blockType と Kind の組は L0-s4b の `BlockComponentRegistry` の実物から引く）

`BlockInventoryNaturalScrollTest.cs`（5 件: `高さ確定の本文はパネルの本文いっぱいに伸びる`〈`LargeFilledBody` のとき、`section-stack--fill-panel-height` を持つ偽の本文の高さ＝`GamePanel` の body の高さ〉／ 小型では本文が `block-inventory-natural-scroll` の中にあり、外側の負マージンが −8・内側の逃げが 8／大型では本文がスクロール器を通らず直下にある／`帯を超える中身はスクロール器の中で流れる`〈枠 120 個〈12 行。グリッドは 12×48＋11×6.8＝650.8px〉のチェストで `block-inventory-panel` の高さ＝帯〈`BlockInventoryPanelBandTest` と同じ式で舞台から求める。1280×720 で 482〉、かつスクロール器の `GetContentHeight() > GetViewportHeight()`。60 枠は 6 行で外殻込み 373.6px〈計算〉に収まり帯を超えないので使わない〉／`短い本文は内容の高さでスクロールしない`〈枠 10 個で、パネルの高さ＜帯、かつ `GetContentHeight() <= GetViewportHeight()`〉）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 外殻の型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`BlockInventoryScreenVisibility.cs`（State）:
```csharp
using Client.Ui.State.Routing;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// ブロックインベントリの画面を出すか（画面ルータが SubInventory で、パネルが閉でない。閉は Web の return null）
    /// Whether to show the block-inventory screen (router on SubInventory and the panel not closed; closed is the Web return null)
    /// </summary>
    public static class BlockInventoryScreenVisibility
    {
        public static bool IsShown(UiRoutingSnapshot snapshot, BlockInventoryPanelState state)
        {
            return snapshot.Screen == UiScreen.SubInventory && !(state is ClosedBlockInventoryPanel);
        }
    }
}
```

`BlockInventoryPanelBand.cs`:
```csharp
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 帯の高さと大型の幅を舞台の寸法とトークンから導く（Web の calc() を C# で解く。A-47）
    /// Derives the band height and large width from the stage size and tokens (solves the Web calc() in C#; A-47)
    /// </summary>
    public sealed class BlockInventoryPanelBand
    {
        // :root のトークンは子へ継承されないので、.block-inventory で受け直した名前を読む（BlockInventory.uss）
        // :root tokens do not inherit, so read the names re-declared on .block-inventory (BlockInventory.uss)
        private const string MenuLowerSafeArea = "--block-inventory-menu-lower-safe-area";
        private const string InventoryPanelWidth = "--block-inventory-inventory-panel-width";
        private const string StageColumnGap = "--block-inventory-stage-column-gap";

        private readonly StageColumnsLayout _stage;
        private readonly VisualElement _shell;
        private float _menuLower;
        private float _inventoryWidth;
        private float _columnGap;
        private bool _menuLowerResolved;
        private bool _inventoryWidthResolved;
        private bool _columnGapResolved;
        private bool _large;

        public BlockInventoryPanelBand(StageColumnsLayout stage, VisualElement shell)
        {
            _stage = stage;
            _shell = shell;
            shell.RegisterCallback<CustomStyleResolvedEvent>(_ => ReadTokens());
            stage.RegisterCallback<GeometryChangedEvent>(_ => Apply());
        }

        public void SetLarge(bool large)
        {
            _large = large;
            Apply();
        }

        private void ReadTokens()
        {
            // 3 つを別々に持つ。欠けは UssCustomStyleReader が LogError で知らせる
            // Keep the three separately; a missing one is reported by UssCustomStyleReader with LogError
            _menuLowerResolved = UssCustomStyleReader.TryReadPixels(_shell.customStyle, MenuLowerSafeArea, out _menuLower);
            _inventoryWidthResolved = UssCustomStyleReader.TryReadPixels(_shell.customStyle, InventoryPanelWidth, out _inventoryWidth);
            _columnGapResolved = UssCustomStyleReader.TryReadPixels(_shell.customStyle, StageColumnGap, out _columnGap);
            Apply();
        }

        private void Apply()
        {
            // silent-ok: 最初のレイアウト前は舞台の寸法もトークンも無い / 解決時の CustomStyleResolvedEvent・GeometryChangedEvent で当て直し、トークン欠けは LogError
            if (!_menuLowerResolved || float.IsNaN(_stage.resolvedStyle.height)) return;

            // 帯 = 舞台の高さ − 上の安全帯（舞台の padding-top）− 下の安全帯
            // Band = stage height − upper safe area (the stage's padding-top) − lower safe area
            var style = _stage.resolvedStyle;
            var band = style.height - style.paddingTop - _menuLower;
            _shell.style.maxHeight = band;
            _shell.style.height = _large ? band : StyleKeyword.Auto;

            // silent-ok: 幅のトークンが未解決の間は大型の幅を当てない / 同上の再適用と LogError
            if (!_inventoryWidthResolved || !_columnGapResolved) return;
            _shell.style.width = _large ? style.width - style.paddingLeft - style.paddingRight - _inventoryWidth - _columnGap : StyleKeyword.Auto;
        }
    }
}
```
（上の安全帯は舞台の `padding-top` が `--menu-upper-safe-area` そのもの〈`StageColumnsLayout.uss`〉なのでトークンを二重に読まない。左右も舞台の padding を読む）

`BlockInventoryNaturalScroll.cs`（`GameScrollView("block-inventory-natural-scroll")` を持つ箱。クラス `block-inventory__natural-scroll` を付け、`CustomStyleResolvedEvent` で受け直した `--block-inventory-body-bleed` を `UssCustomStyleReader.TryReadPixels` で読み、`style.margin*` に負号付き・`contentContainer.style.padding*` に正で当てる〈UITK の USS に `calc()` は無い〉。`Wrap(VisualElement body)`・`Unwrap()`）

`BlockInventoryBodyHost.cs`（Presenter 側の補助。本文・題の操作・列車の本文の Presenter を付け替える）:
```csharp
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 本文・題の操作・列車の本文の Presenter を付け替える。同じパネル（IsSamePanel）かつ同じ大きさのあいだは何もしない（各部分が自分で購読している）
    /// Swaps the body, title-action and train-body presenters; does nothing while the panel (IsSamePanel) and size stay the same (each part follows its own subscriptions)
    /// </summary>
    public sealed class BlockInventoryBodyHost
    {
        private readonly BlockInventoryPartRegistry _parts;
        private readonly BlockInventoryView _view;
        private IBlockInventoryPart _body;
        private IBlockInventoryPart _titleAction;
        private ITrainInventoryPart _trainBody;
        private BlockInventoryPanelState _state = BlockInventoryPanelState.Closed;
        private BlockInventoryPanelLayout _layout = BlockInventoryPanelLayout.Natural;

        public BlockInventoryBodyHost(BlockInventoryPartRegistry parts, BlockInventoryView view)
        {
            _parts = parts;
            _view = view;
        }

        public void Apply(BlockInventoryPanelState state, BlockInventoryPanelLayout layout)
        {
            // 作り直すかは State の IsSamePanel と大きさの 2 つだけで決める
            // Rebuild is decided only by the State's IsSamePanel and the size
            if (state.IsSamePanel(_state) && layout.Equals(_layout)) return;
            _state = state;
            _layout = layout;
            DetachAll();

            // エラー中は何も載せない（Web の !error）
            // Nothing is mounted while erroring (Web !error)
            if (state is OpenBlockInventoryPanel block && block.Error == BlockInventoryPanelError.None)
            {
                _body = _parts.CreateBody(block, new BlockInventoryBodyMount(layout.FillsPanelHeight));
                _view.PlaceBody(_body.Root, !layout.IsLarge);
                _body.Attach(block);
                _titleAction = _parts.CreateTitleAction(block.Components.TitleAction);
                _view.PlaceTitleAction(_titleAction.Root);
                _titleAction.Attach(block);
            }
            if (state is OpenTrainInventoryPanel train && train.Error == BlockInventoryPanelError.None)
            {
                _trainBody = _parts.CreateTrainBody();
                _view.PlaceBody(_trainBody.Root, !layout.IsLarge);
                _trainBody.Attach(train);
            }
        }

        private void DetachAll()
        {
            _body?.Detach();
            _titleAction?.Detach();
            _trainBody?.Detach();
            _body = null;
            _titleAction = null;
            _trainBody = null;
            _view.ClearBody();
            _view.ClearTitleAction();
        }
    }
}
```
（小型＝自然高のスクロール器で包む〈Web の `!isLarge`〉。列車は常に大型）

`BlockInventoryView.cs`（約 150 行。購読しない。骨子）:
```csharp
    public sealed class BlockInventoryView : VisualElement
    {
        public const string LargeClassName = "block-inventory--large";
        public const string NaturalClassName = "block-inventory--natural";

        public BlockInventoryView(StageColumnsLayout stage, IBlockInventoryPanelCommands commands)
        {
            // 外殻: block-inventory（Viewer 列の子）> GamePanel(block-inventory-panel, Default) > [エラー行, 本文の器]、閉じるは GamePanel の hierarchy に絶対配置
            // Shell: block-inventory (Viewer column child) > GamePanel(block-inventory-panel, Default) > [error row, body container]; close is absolutely placed in the GamePanel hierarchy
            name = "block-inventory";
            AddToClassList("block-inventory");
            _panel = new GamePanel("block-inventory-panel", GamePanelFrame.Default);
            _panel.AddToClassList("block-inventory__panel");
            _titleActionContainer = new VisualElement { name = "block-inventory-title-action" };
            // エラー行 _error は文を持つまで作らない（文無しの LocalizedLabel を載せると LogError。前例 GamePanel.cs:96 の題の遅延生成）
            // The error row _error is created only once it has text (a text-less LocalizedLabel logs an error; precedent: GamePanel.cs:96 lazy title)
            _body = new VisualElement { name = "block-inventory-body" };
            _body.AddToClassList("block-inventory__body");   // GamePanel の body いっぱいに伸ばす（高さ確定の本文が伸びる余地）
            _close = new IconButton("block-inventory-close", new IconButtonCloseGlyph());
            _close.AddToClassList("block-inventory__close");
            TutorialAnchor.Mark(_close, AnchorIds.InventoryCloseButton);
            _close.OnClicked.Subscribe(_ => commands.Close());   // 自要素のクリックの結線（Model の購読ではない。設計書 C8・前例 PauseMenuTopPageView.cs:36）
            _band = new BlockInventoryPanelBand(stage, this);
            _naturalScroll = new BlockInventoryNaturalScroll();
            style.display = DisplayStyle.None;      // 生成直後は隠し、Presenter の SetShown が出す（前例 PauseMenuView）
        }

        public void SetShown(bool shown) { ... }
        public void Render(BlockInventoryPanelState state, BlockInventoryPanelLayout layout)
        {
            // 題: ブロックは BlockName(blockGuid)、列車は ui.blockInventory.trainInventory。題の操作の器は題の後に 1 回だけ SetTitleAction
            // Title: BlockName(blockGuid) for blocks, ui.blockInventory.trainInventory for trains; the action holder goes in once via SetTitleAction after the title
            // エラー行: TryGetLocalizationKey が真なら初回だけ LocalizedLabel を作って載せ、名前を block-inventory-error / train-inventory-error にし文を入れる。偽なら文を持ったまま display None（分岐はこの 1 か所）
            // Error row: on TryGetLocalizationKey true, create and mount the LocalizedLabel once, name it and set the text; on false keep its text and hide it (the only branch)
            // 大きさ: LargeClassName / NaturalClassName と _band.SetLarge
            // Size: LargeClassName / NaturalClassName and _band.SetLarge
        }
        public void PlaceBody(VisualElement body, bool natural) { ... }      // natural なら _naturalScroll.Wrap(body) を本文の器へ
        public void ClearBody() { ... }
        public void PlaceTitleAction(VisualElement action) { ... }     // 器の中身を差し替える
        public void ClearTitleAction() { ... }                         // 器を空にする
    }
```
（200 行を超えそうなら題とエラー行の描画を `BlockInventoryPanelHeader.cs` へ出す〈`Screens/BlockInventory/` は 7 ファイル〉）

`BlockInventoryScreenPresenter.cs`:
```csharp
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 外殻を Viewer 列へ 1 回だけ載せ、画面ルータとパネルの状態・大きさで出し入れと描き直しをする（前例 PauseMenuScreenPresenter）
    /// Mounts the shell on the Viewer column once and shows/re-renders from the screen router, panel state and size (precedent PauseMenuScreenPresenter)
    /// </summary>
    public sealed class BlockInventoryScreenPresenter : IInitializable
    {
        private readonly StageColumnsLayout _stage;
        private readonly UiRoutingState _routing;
        private readonly IBlockInventoryPanelSource _panel;
        private readonly BlockInventoryPanelLayoutModel _layout;
        private readonly BlockInventoryPartRegistry _parts;
        private readonly IBlockInventoryPanelCommands _commands;

        public BlockInventoryScreenPresenter(StageColumnsLayout stage, UiRoutingState routing, IBlockInventoryPanelSource panel, BlockInventoryPanelLayoutModel layout, BlockInventoryPartRegistry parts, IBlockInventoryPanelCommands commands)
        {
            _stage = stage;
            _routing = routing;
            _panel = panel;
            _layout = layout;
            _parts = parts;
            _commands = commands;
        }

        public void Initialize()
        {
            var view = new BlockInventoryView(_stage, _commands);
            var host = new BlockInventoryBodyHost(_parts, view);
            _stage.Get(StageColumn.Viewer).Add(view);

            // 画面・状態・大きさは同じフレームで変わりうるので、組にして 1 回だけ描く。隠れている間は部分を外す
            // Screen, state and size may change in one frame, so render once from the triple; parts are detached while hidden
            _routing.Current.CombineLatest(_panel.Current, _layout.Current, (snapshot, state, layout) => (snapshot, state, layout)).Subscribe(t =>
            {
                var shown = BlockInventoryScreenVisibility.IsShown(t.snapshot, t.state);
                view.SetShown(shown);
                view.Render(t.state, t.layout);
                host.Apply(shown ? t.state : BlockInventoryPanelState.Closed, t.layout);
            });
        }
    }
}
```
（ゲーム寿命のオブジェクトなので購読の Dispose は持たない〈AGENTS.md 既知の制約〉）

`BlockInventory.uss`:
```css
/* web: features/blockInventory/style.module.css .panel / .panelLarge / .naturalBodyScroll（composes panelBodyScrollArea.module.css .scroll）と BlockInventoryPanel.tsx の GamePanel style（paddingBottom・小型だけの paddingRight）・閉じるの absolute 位置を写す。calc() の寸法と bleed の負マージンは C# が解く */
/* web: ports .panel, .panelLarge, .naturalBodyScroll (composing .scroll) and the GamePanel inline style (paddingBottom, small-only paddingRight) plus the close button's absolute position; calc() sizes and the negative bleed margin are solved in C# */
.block-inventory {
    align-self: flex-start;
    flex-direction: column;
    /* :root のトークンを C# が読めるよう受け直す（前例 GaugeBar.uss） */
    /* re-declare :root tokens so C# can read them (precedent GaugeBar.uss) */
    --block-inventory-menu-lower-safe-area: var(--menu-lower-safe-area);
    --block-inventory-inventory-panel-width: var(--inventory-panel-width);
    --block-inventory-stage-column-gap: var(--stage-column-gap);
}

.block-inventory__panel {
    padding-bottom: var(--block-panel-bottom-safe-area);
    min-height: 0;
    flex-shrink: 1;
}

.block-inventory--natural .block-inventory__panel {
    padding-right: var(--block-panel-right-safe-area);
}

.block-inventory--large .block-inventory__panel {
    height: 100%;
}

.block-inventory__body {
    flex-grow: 1;
    flex-shrink: 1;
    min-height: 0;
    flex-direction: column;
}

.block-inventory__close {
    position: absolute;
    top: var(--bevel-3);
    right: var(--panel-edge-fade);
}

.block-inventory__natural-scroll {
    --block-inventory-body-bleed: var(--block-panel-body-bleed);
    flex-grow: 1;
    flex-shrink: 1;
    min-height: 0;
}
```
（部品のクラス〈`.game-panel` 等〉は画面 USS に書かない〈Global Constraints〉。大型の右は `GamePanel` 既定の 10px のまま〈Web の大型は右を上書きしない〉。Web `.scroll` の viewport の `flex: 1; min-height: 0` は `GameScrollView` の既定で足りるか Step 4 の `帯を超える中身は…` で確かめ、足りなければ `.block-inventory__natural-scroll` の子孫ではなく `GameScrollView` のつまみで当てる〈部品のクラスを書かない〉）

`BlockInventoryGameUiRegistration.cs`（画面だけが使うもの）:
```csharp
using Client.Ui.Screens.BlockInventory;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // ブロックインベントリ画面（GameUi）の登録。後続レーンが ctor で受ける共有サービスは MainGameUiRegistration の共有サービスの並びにある（E-1・A-56）。差し込み口は後続レーンが自分の登録ファイルで同じインターフェイスを足す（D-1）
    // Registers the block-inventory screen for GameUi; services other lanes take in their ctors live in MainGameUiRegistration's shared list (E-1, A-56); later lanes add the port interfaces from their own registration files (D-1)
    internal static class BlockInventoryGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<BlockInventoryPanelLayoutModel>().AsSelf();
            builder.Register<BlockInventoryPanelCommands>(Lifetime.Singleton).As<IBlockInventoryPanelCommands>();
            builder.Register<SectionStackSlotRegistry>(Lifetime.Singleton);
            builder.Register<SectionStackBodyFactory>(Lifetime.Singleton);
            builder.Register<BlockInventoryPartRegistry>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BlockInventoryScreenPresenter>();
        }
    }
}
```

`MainGameUiRegistration.cs`（共有サービスの並び〈`InputSystemPointerPositionStream` の行の下〉に足す。複数レーンが ctor で受けるので E-1・A-56 どおり本体に置く）:
```csharp
            // ブロックインベントリの共有サービス: パネルの状態・遷移要求・スロット操作の型付きコマンド・ブロックの枠の並び・解放済み機械レシピ（S4 の各レーンが ctor で受ける）
            // Block-inventory shared services: panel state, transition requests, typed slot commands, block slot grids and unlocked machine recipes (taken by the S4 lanes' ctors)
            builder.RegisterEntryPoint<BlockInventoryPanelModel>().As<IBlockInventoryPanelSource>();
            builder.Register<UiStateControlTransitionRequester>(Lifetime.Singleton).As<IUiStateTransitionRequester>();
            builder.RegisterEntryPoint<UnlockedMachineRecipeSource>().AsSelf();
            builder.Register<SubInventoryRawSlots>(Lifetime.Singleton).As<IRawSubInventorySlots>();
            builder.Register<BlockSlotReader>(Lifetime.Singleton);
            builder.Register<LocalBlockInventoryOperations>(Lifetime.Singleton).As<IBlockInventoryOperations>();
            builder.Register<BlockItemActionExecutor>(Lifetime.Singleton);
            builder.Register<PlannedItemActionRunner>(Lifetime.Singleton);
            builder.Register<BlockSlotRestrictionInputBuilder>(Lifetime.Singleton);
            builder.Register<PlayerSlotContextReader>(Lifetime.Singleton);
            builder.Register<ShiftMoveRefusalNotifier>(Lifetime.Singleton);
            builder.Register<BlockSlotCommands>(Lifetime.Singleton).As<IBlockSlotCommands>();
            builder.Register<PlayerSlotCommands>(Lifetime.Singleton).As<IPlayerSlotCommands>();
            builder.Register<BlockItemGridFactory>(Lifetime.Singleton);
```
画面登録区画の末尾:
```csharp
            GrabGameUiRegistration.Register(builder);
            BlockInventoryGameUiRegistration.Register(builder);
```
（`PlayerItemActionExecutor`・`IPlayerInventoryOperations`・`IPlayerSlotDragCommands` は C4-e の `GrabGameUiRegistration` が登録済み、`ClientLocalNotificationSource` は `MainGameInteractionRegistration.cs:166` で登録済み。共有の並びは画面登録区画より前で、VContainer は登録順に依らず解決するので問題ない。`MainGameUiRegistration.cs` は 200 行以内に収まる）

`MainGameModelRegistration.cs`（105 行の隣。同じ `ItemStackLevelDataStore` の面で、State の `BlockSlotPlanner`・`DirectMovePlanner`・`PlayerSlotContext` が使う。D-14）:
```csharp
            builder.RegisterInstance(ServerContext.GetService<IItemStackLevelUnlocker>());
            builder.RegisterInstance(ServerContext.GetService<IItemStackLevelLookup>());
```

`GameUiTheme.tss`（`Screens` 区画の末尾。パスは既存の `GrabOverlay.uss` の行と同じ書き方に揃える）:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventory.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStack.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/Parts.uss");
```

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.BlockInventory|Client.Tests.Ui.State.BlockInventory|Client.Tests.Ui.State.SlotCommands|Client.Tests.Ui.State.Recipe|Client.Tests.Ui.State.Routing|PauseMenuCommandsTest|TutorialAnchorCoverageTest|UiThemeImportBundleTest|UiStyleConventionTest|UiLayerDependencyDirectionTest|LookExceptionLedgerTest"`
Expected: `ErrorCount: 0`、全件 PASS。続けて `uloop get-logs --project-path ./moorestech_client --log-type Error` が 0 件（受け直したトークンが読めていれば `UssCustomStyleReader` の LogError は出ない）

- [ ] **Step 5: 全体スイッチ UITK での起動を確かめる**

`uloop execute-dynamic-code` で `UiSystemEditorSwitch.Set(UiSystem.GameUi)` → PlayMode → チェストを置いて開く（録画の DSL を使ってよい）→ `uloop get-logs --log-type Error` 0 件、`uloop screenshot` 1 枚を `done.md`「証拠」へ。VContainer の解決失敗（未登録の依存）と、拡張口が登録 0 本で空の列になること（D-1 の前提の実測）はここで出る。`[SectionStackSlotRegistry]` の空き Slot の情報ログが 1 回出ることも見る

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel moorestech_client/Assets/Scripts/Client.Starter/Registration "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): ブロックインベントリの外殻・ScreenPresenter・登録を足す（S4-alpha）

<実行ホストの指示する帰属行>"
```

---

### Task 9: 名前の対応・追従記録

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 追従記録」の末尾へ 1 行）

- [ ] **Step 1: 対応を確かめる**

Run: `node tools/uitk/test-name-parity.mjs --bundle 4; echo "exit=$?"`
Expected: 終了コードが 2 でない。alpha のファイル（`blockInventory.spec.ts`・`blockInventoryGestures.spec.ts`・`blockRegistryCoverage.spec.ts`・`BlockInventoryPanel.localization.test.ts`・`LackHighlightText/index.test.ts`）に未対応が無い。`SectionStackView.test.tsx`・`blockInventoryDesign.test.ts`・`regression/sectionStack.spec.ts` は alpha が写した名前が対応済みで、残りは「写すテスト」表の持ち主の名前だけが未対応

写した 23 件は `docs/uitk/disposition.md` に書かない（処遇の表は写さないテスト用で、`<web-test>` で写したテストに処遇の行があると `compareBundles.mjs:88` が誤りにして終了コード 2）。後続レーンの持ち分も alpha は書かない（`写す予定` の証跡は各レーンの bd ID が要る）

- [ ] **Step 2: 追従記録を書く（E-1）**

Task 4 Step 1 で `localizationKeys.ts` を作り直したので、`docs/uitk/TODO.md`「## 追従記録」の末尾へ、その再生成コミットを `写した`（写し先＝同じコミットの C# と CSV）で 1 行足す。書式は同節の既存の行に揃える

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): ブロックインベントリの文言の再生成を追従記録に書く（S4-alpha）

<実行ホストの指示する帰属行>"
```

---

### Task 10: 並べ比べと見た目の例外

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 見た目の例外」。使ったときだけ LX-106〜LX-110）
- 記録: `LOGS/harness/uitk/S4-alpha/done.md`「差の表」

- [ ] **Step 1: 撮る**

参照 `4-block-chest`・`4-block-generic`・`4-block-tank`・`4-block-trainError`（基準 web-869b2d4264 は 1280×720、web-0e81bbe3c3-2560x1080 は 2560×1080）と同じ場面を UITK で撮る。状態は参照の fixture と同じものを、使い捨ての撮影スクリプトで `IBlockInventoryPanelSource` の代わりに流して作る（本番コードには入れない。比べ方の正本 `compare-procedure.md:39`「参照キャプチャと同じ状態を作る」）: chest は Web `e2e/mock-host/fixtures.ts:47-58` の `blockChest`＝blockType `Chest`・9 枠（7・4・空 7）、generic は `logisticsFixtures.ts:9-18`＝未登録の blockType・項目 1 枠。実マスタの木のチェスト〈30 枠〉では撮らない。場面の組み方と撮り方は ref-uitk-design スキルの「比べ方」節（`references/` の比較の手順）に従い、同じスキルの比較ツールで並べ比べの画像を作る

- [ ] **Step 2: 差の表を書く**

| 参照 | 見る点 | 予想 |
|---|---|---|
| 4-block-chest | 外殻の x 位置 | 他レーン持ち（参照は左に持ち物パネル〈x≈45〜510〉があり x≈533 から。UITK は Inv 列が空で x＝40＋0＋16＝56〈計算〉。持ち物パネルは S4-inventory） |
| 4-block-chest | 幅・高さ・上端 168・題・閉じる・列数 9・1 行の枠・下の安全帯 | 同じ（比べる撮影だけ、使い捨ての撮影スクリプトで Inv 列に幅 `--inventory-panel-width` の空の箱を置く。本番コードには入れない） |
| 4-block-generic | 未知の blockType の汎用グリッド（x 位置は chest と同じ扱い） | 同じ |
| 4-block-generic | 項目の枠の下の液体の行（Web `SectionStackView.tsx:45-47` `generic-block-fluids`・`showFluidProgress`） | 他レーン持ち（区画が空。S4-machine） |
| 4-block-tank | 題と外殻のみ。液体の行は S4-machine の区画が空なので出ない | 他レーン持ち（区画が空）。LX に書かない |
| 4-block-trainError | 列車の題と `train-inventory-error` の文。列車の本文は S4-train が空 | 同じ（エラー中は本文を出さないので Web と一致する。x 位置は chest と同じく他レーン持ち） |

差は 1 件ずつ「同じ／直した／LX-n／他レーン持ち」で閉じる。LX は `docs/uitk/TODO.md`「## 見た目の例外」に LX-106 から順に使い、USS に `/* look-exception: LX-n */` を付ける。見込みの例外: 題の操作の右の逃げ（R-3。Web は `--panel-action-button-width + --mantine-spacing-sm`＝99px。`GamePanel` に口が無い。alpha の参照に題の操作は出ないので、この場面では差にならない → 起草メモへ）。2560×1080 では帯・大型の幅が論理 482・740.1 の拡大で出ることを見る（舞台は 1280×720 固定で、倍率は PanelSettings が持つ）

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/TODO.md moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory
git commit -m "docs(uitk): ブロックインベントリの並べ比べの差を閉じる（S4-alpha）

<実行ホストの指示する帰属行>"
```
（差が無く何も変えていなければコミットしない）

---

### Task 11: 録画

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-block-inventory.cs`

- [ ] **Step 1: シナリオを書く**

前例 `scenarios/uitk/uitk-grab-overlay.cs` の形（`UiSystemEditorSwitch.Set(UiSystem.GameUi)`・`ClientDIContainer` から型を引く）に従い、次を順に主張する:
1. `SetupFlatGround` → `PlaceBlockDirect("木のチェスト", pos, BlockDirection.North)` → `WaitBlockGameObject` → `RunOnServer("木材 10 をチェストへ入れる", () => chest.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, PlaytestItemOps.ResolveItemId("木材"), 10))`（前例 `scenarios/tier9/tier9-3-yield-ui.cs:107`・`misc/cleanroom-v2.cs:52`。`GiveItem` はプレイヤーへ渡すだけなので使わない）
2. `PressInteract` → `WaitUiState(UIStateEnum.SubInventory)` → `block-inventory` が出る・題がチェストの名前（`Screenshot`）
3. `chest-grid-slot-0` を左押下 → 掴みに 10・枠が空（`PlayerGrabModel`）、`grab-overlay` が `grab-overlay--shown` を持ち display Flex（Web `blockInventory.spec` 2 件目の「grab オーバーレイが出る」を主張する。前例 `uitk-grab-overlay.cs:113`）→ 同じ枠へ左押下で戻す
4. 右押下 → 掴みに 5（分割）→ 右押下で 1 個置く → 左押下で残りを置く
5. （押すたびに口を読み直すことの確認。DSL はアクションの間に 0.5 秒を挟むので先書きの窓は開かず、読み元の判別は L0 の `BlockSlotReaderTest` が持つ）`chest-grid-slot-0` を右押下で半分（5）掴み、`chest-grid-slot-1` へ左押下で置く（枠 0 が 5・枠 1 が 5・掴み 0）→ `chest-grid-slot-0` をダブルクリック → 掴み 0・枠 0 が 10・枠 1 が空（ダブルクリックは押下 2 回〈全量掴み → 全量置き〉の後に `DoubleClick` が来て、空手なのでその枠へ集める。`SlotFrame.cs:55-61,98-103`・`BlockSlotPlanner.cs:22`）
6. 前提「掴み 0・枠 0 に 10」（手順 5 の主張）で Shift＋左押下 → main へ配分（チェストの枠が空・main に入る）。main の枠からの Shift は UITK の持ち物画面〈S4-inventory〉が無いので alpha の録画では押さない（L0 の `PlayerSlotCommandsTest` が持つ。UI からの確認は S4-inventory の完了条件。D-33）
7. `block-inventory-close` を押す → `WaitUiState(UIStateEnum.GameScreen)` → `block-inventory` が隠れる
8. 途中で `uloop get-logs --log-type Error` 相当（DSL の `AssertNoErrorLogs`）が 0 件

押下は DSL の UITK 要素の押下（`ClickUiElement(name)` 相当。無ければ前例 `uitk-grab-overlay.cs` の押下の書き方）を使う。InputSystem の注入だけを使い OS の入力は使わない

- [ ] **Step 2: 回す**

Run: unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh uitk/uitk-block-inventory` と `uitk/uitk-lifeline`
Expected: 2 本の `result.json` が PASS、mp4 がある。`done.md`「証拠」に result.json のパス・mp4・`UiInputExclusivity.ActiveUiSystem` の値（GameUi）を書く。生の録画は worktree の gitignore 下に置き、`LOGS` へはスクショ 3 枚までと報告だけ

- [ ] **Step 3: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-block-inventory.cs
git commit -m "test(uitk): ブロックインベントリの録画シナリオを足す（S4-alpha）

<実行ホストの指示する帰属行>"
```

---

### Task 12: 全ブランチのレビュー

- [ ] **Step 1: moores-code-review を全ブランチで回す**

moores-code-review スキルを、base `origin/feature/uitk` に対するこのブランチの全差分で起動する。スキルの手順どおり、機械的な修正は自動で当て（自動適用の後は Task 11 の録画を反映後のバイナリで撮り直す）、設計判断は D-n として判断記録へ足すか、目的・裁定から外れるものだけ `done.md`「要裁定」へ書く（人へ質問しない）

- [ ] **Step 2: 当てた修正の後に compile とテスト**

Run: `uloop compile --project-path ./moorestech_client` → Task 8 Step 4 と同じテストの絞り込み
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 3: コミット**

```bash
git add -A moorestech_client docs .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk
git commit -m "fix(uitk): 全ブランチのレビューを反映（S4-alpha）

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
共有区画（`MainGameUiRegistration` の共有サービスの並びと画面登録区画・`MainGameModelRegistration`・`GameUiTheme.tss` の `Screens` 区画・`docs/uitk/TODO.md`・`UiCommandRejectReason`・`localization.csv`）の衝突は両側の行を残して解く。取り込みで `TutorialAnchorCoveragePending.cs` が衝突したら、両側の削除を両方当てる（alpha の `InventoryCloseButton` の行も、相手が消した隣の行〈`BuildMenuEntryPrefix` 等〉も残さない）。続けて `uloop compile` と Task 8 Step 4 のテスト（`TutorialAnchorCoverageTest` を含む）を回し、緑を確かめて push

- [ ] **Step 3: G1 を待ってマージする**

`gh pr checks <番号>` が緑になったら `gh pr merge <番号> --merge`。`UnityCursorTooltipScreenTest` が CI だけで落ちるのは既知で無視してよい（他の赤は直して push し直す）

- [ ] **Step 4: 報告と後始末**

1. `bd close <S4-alpha の issue>`
2. `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/uitk/S4-alpha/done.md` の 1 行目を「状態: 完了」にし、PR 番号・マージコミット・証拠・差の表・integrator への依頼・未確認を埋める
3. `cmux wait-for -S uitk-s4-alpha`
4. `moores-wt rm s4-alpha`（Editor とworktreeを畳む。未 push があれば拒否されるので push を確かめてから）

---

## Self-Review

- Requirements R-1〜R-18 はそれぞれ Task に当たる: R-1・R-2〜R-6（State 側）→ Task 3、R-2〜R-6（View 側）→ Task 8、R-7 → Task 7、R-8 → Task 6、R-9 → Task 4、R-10・R-11 → Task 5（D-9・D-10・D-11 は Task 4・5）、R-12 → Task 2、R-13 → Task 3・5・7、R-14 → Task 10、R-15 → Task 8、R-16 → Task 6〜9、R-17 → Task 11、R-18 → Task 12・13
- 写すテスト（タグの種類）23 件の所在: Task 3（blockRegistryCoverage 1）・Task 4（gestures 4・spec 1）・Task 5（gestures 1）・Task 6（LackHighlightText 2）・Task 7（SectionStackView 3・regression 1）・Task 8（spec 2・design 6〈メソッドは 7〉・localization 2）＝ 23（NUnit メソッドは 24）。spec 2 件目の掴み絵の表示は Task 11 の録画
- 型の一致: Produces 一覧の型名・メソッド名は Task 3〜8 のコードと同じ綴り（`IBlockInventoryPart.Attach(OpenBlockInventoryPanel)`・`Detach()`・`IBlockInventorySection.IsApplicable`・`ISectionStackSectionFactory.Create(SectionStackSectionMount)`・`IBlockInventoryLargePanelRule.IsLargePanel`・`BlockInventoryPanelState.IsSamePanel`・`IBlockSlotRestrictionContributor.Contribute`・`IUiStateTransitionRequester.RequestTransition`）
- レビュー反映: `Error` の比較はすべて `== BlockInventoryPanelError.None`（`grep -n "Error == null"` が 0 件）。`:root` のトークンを C# で読む 3 か所は受け直した名前を読む
- プレースホルダ: 「実物に合わせる」と書いた箇所は、起草時に実物の型が確定していない名前（C4-e の記録係・`Click` ヘルパー・`LocalizationTextParam` の ctor）だけ。挙動の空白は無い

---

## 判断記録

| ID | 判断 | 理由 | 却下した案 |
|---|---|---|---|
| D-1 | 拡張口は VContainer の同一インターフェイスの複数登録を `IReadOnlyList<T>` で受ける。各レーンは自分の登録ファイルで `builder.Register<具体>(Lifetime.Singleton).As<口>()` を足す | 前例 `INestedPauseScreenState`（`MainGameInteractionRegistration.cs` で 2 本登録 → `UiRoutingState` が `IReadOnlyList<INestedPauseScreenState>` で受ける）・`ITutorialViewManager`（→ `TutorialManager`）。VContainer 1.10 は登録 0 本でも空の列を返すので、後続が未マージでも alpha は起動する | 中央の静的テーブルに各レーンが行を足す（共有区画の衝突が増え、alpha が後続の型を知る）。Kind ごとの keyed 登録（VContainer 1.10 に無い） |
| D-2 | 区画は `SectionStackSlot` の列挙で順を固定し、1 Slot に 1 本の工場。項目グリッドは alpha が持ち差し込めない | Web の順は SectionStackView の JSX が持つ固定の並び。順を登録順に任せると後続のマージ順で並びが変わる | 登録に順序の数値を持たせる（数の衝突と飛び番の管理が要る） |
| D-3 | 機械の「文脈を機械の区画の先頭へ置く」（Web `leadingContent`）は S4-machine が `Context` と `Machine` の両区画の中で扱う。alpha の `SectionStackBody` は機械かどうかを知らない | 汎用基盤にドメイン語彙を持ち込まない（AGENTS.md 設計原則） | `SectionStackBody` に機械の分岐を置く |
| D-4 | 本文の未登録の Kind は標準の本文へ逃げ、警告ログを 1 回 | 後続レーンが未マージの期間も開ける。黙らない | 例外で止める（後続が揃うまで遊べない） |
| D-5 | ブロック側の在庫操作の規則（結合インベントリの番地 `MainSlotCount + slot`・コントローラの呼び方）は Web ホスト `BlockInventoryActions.cs` と同じものを State に写す | UITK は `Client.WebUiHost` を参照しない（依頼の制約）。規則を写して双方に置く | WebUiHost を参照する／規則を共通の下位層へ移す（Web 側を変えることになり範囲外） |
| D-6 | 移動の結果から A-94 の理由への写しも Web ホストと同じ表を State に置く | D-5 と同じ | — |
| D-7 | 帯の高さと大型の幅は `BlockInventoryPanelBand` が舞台の resolvedStyle とトークンから解く。上の安全帯と左右の余白は舞台の padding を読む | UITK の USS に `calc()` が無い [A-47]。舞台の padding がトークンそのものなので二重に読まない | 数（482・740.108753）を C# に書く（舞台の大きさが変わると壊れる） |
| D-8 | `:root` のトークン（列数・帯・大型の幅・bleed）は読む要素のクラスで `--<部品>-<名前>: var(--token)` と受け直し、C# は受け直した名前を `UssCustomStyleReader` で読む（`.block-item-grid`・`.block-inventory`・`.block-inventory__natural-scroll`）。出所: レビュー | `:root` の値は当てた要素の customStyle にしか出ず子へ継承されない（`UssCustomStyleReader.cs:10-13`・`docs/uitk/TODO.md:167` の実測）。前例 `GaugeBar.uss:6`・`GaugeBar.cs:12` | 子から `--inventory-columns` 等を直に読む（読めず LogError、列数・寸法が当たらない）／テーマの読み込みを直す依頼を integrator へ出す（原因と違う）／C# の定数 10 |
| D-9 | `DestinationFull`（Web に無い断り）は通知せず Warning ログだけ | Web に無い通知を UITK だけで出すと見た目が分かれる。開発者は気づける | 新しい messageId を足す（Web にも足すことになり範囲外） |
| D-10 | `BlockSlotPlanner` の Shift→main が満杯で空の計画になったときは Warning ログ | fail-closed の経路は開発者が読めるログへ出す（AGENTS.md） | 黙って何もしない |
| D-11 | `DirectMovePlanner` へは既知の ItemId の枠だけ渡し、未知の ID は Error ログで止める | マスタに無い ID の最大スタック数を既定値で埋めない（必須値の欠落は止める） | 最大スタック 1 で埋める |
| D-12 | 閉じるの遷移要求は `Client.Ui.State/Routing/` の `IUiStateTransitionRequester` を挟む。本番 `UiStateControlTransitionRequester` は Web ホスト `RequestUiStateActionHandler` の入れ子ポーズの扱いと許可表 `UiStateTransitionRules.IsAllowed` を写し、拒んだら false とログ。出所: レビュー | `UIStateControl` は MonoBehaviour でテストで組めない。口は後続の画面（持ち物・研究・チャレンジの閉じる）も使うので画面の下でなく Routing に置く。Web は「別経路から状態機械へ書くと現 state の検査を素通りする」として全遷移を 1 本に集めている | `BlockInventory/Panel/` に置き `RequestTransition` を素通しする（後続が同じ口を別に作る・許可表を素通りする） |
| D-13 | Shift の断りの通知は `ClientLocalNotificationSource` へ Web と同じ messageId で出す | Web と同じ文言・同じ経路。文言の解決は UITK の通知レーンが持つ | 画面のトーストへ直接出す（通知の経路が 2 つになる） |
| D-14 | `IItemStackLevelLookup` は `ServerContext.GetService` のインスタンスを `MainGameModelRegistration.cs:105`（`IItemStackLevelUnlocker`）の隣に確定の 1 行で登録する。出所: レビュー | 同じ `ItemStackLevelDataStore` の面で、State の `BlockSlotPlanner`・`DirectMovePlanner`・`PlayerSlotContext` が使う共有のコアサービス。`Client.Starter` に登録は 0 件（起草時に grep） | 画面の登録ファイルに置く（持ち主が画面になる）／「既に登録されていれば足さない」の条件文（二重登録は最後勝ちで気づけない） |
| D-15 | 解放済みの機械レシピの源は `Client.Ui.State/Recipe/Source/` に alpha が置き、`Game.UnlockState` を asmdef 参照に足す | 本体の追加決定。machine・recipe と大型パネル判定の寄与（`IBlockInventoryLargePanelRule` の実装）が使うだけにする。S4-research は未解放のレシピも引くのでマスタから直接読み、この源を使わない（PLAN-W4b の訂正） | 各レーンが自分で組む（2 重になる） |
| D-16 | 大型パネルの判定は寄与の口 `IBlockInventoryLargePanelRule.IsLargePanel` にし、実装は S4-machine（`UnlockedMachineRecipeSource` を ctor で受けて組める）。名前は中立にする。出所: レビュー | 「レシピ有り機械」は機械の語彙。基盤は寄与の真偽だけを知る | alpha が機械レシピの源を見て判定する（機械の語彙が基盤に入る）／Web の名前 `IsLargeMachinePanel` を写す（基盤の型名に機械が入る） |
| D-17 | 題の操作は 1 つの器（`block-inventory-title-action`）を `SetTitleAction` で 1 回渡し、中身を器の中で付け替える | `GamePanel` に題の操作を外す API が無い。部品を変えない | `GamePanel` に Clear を足す（部品の変更は integrator） |
| D-18 | 写すテストは区画の持ち主ごとに分け、同じタグの NUnit を複数レーンが持つ。後続は alpha のテストファイル（`BlockInventoryDesignTest` 等）へ足さず、自分のテストファイルに同じタグで写す。出所: レビュー（後半） | 区画の中身は後続レーンの型でしか主張できない。alpha のファイルは E-1 の共有区画ではなく、後続の追記が 1 メソッドに集まると衝突する | alpha が偽の区画で全件を写す（中身の主張が空になる）／後続が alpha の `TestCase` に行を足す |
| D-19 | パネルの状態は識別子（blockType・Guid・BlockId・BlockInstanceId・位置・列車の車両 ID）・エラー・枠だけの不変 union にし、`BlockGameObject`・`TrainSubInventorySource` を載せない。差し込む部分は「Presenter（`Attach(panel)` で自分の Model を購読し View の `Render` へ・`Detach()` で切る）＋ View（`Root`）」。区画・本文の詳細は各レーンの State の詳細 Model（`OpenBlockDetailTracker`・poll 源を購読）が配る。「同じパネル」の判定は `BlockInventoryPanelState.IsSamePanel`（位置・Guid・エラー、列車は車両・エラー）の 1 本で、BodyHost はそれと大きさだけで作り直す。出所: レビュー | 設計書 §1.1（ViewModel は不変 sealed）・§4.2「Model の購読は Presenter。View は Render で値を受けるだけ」・A-57。前例 `ProgressHudPresenter`・`GrabOverlayViewModel`・State の `OpenBlockDetailTracker`。BlockInstanceId は `BlockGameObject` を外す代わりに区画の Model が対象を引く鍵（PLAN-W4b の S4-special の申し出） | パネルの状態に `BlockGameObject` を載せ区画（View）が自分で購読する（§4.2 に反し、詳細の規則が各区画へ散る）／`IsSamePanel` と BodyHost の `IdentityOf` を別々に持つ（材料が食い違っていた） |
| D-20 | `SectionStackConfig` の null と bool の組を判別 union にする（項目グリッド Hidden / Shown(GridId, RenderEmpty)、液体 Hidden / Row(RowId, ShowProgress) / Details(RowId)）。置き場は State のまま。出所: レビュー | 不在と判別子の 2 表現を 1 本にし、液体の「行か詳細か」を型で閉じる。State が Web と同じ綴りの行 id を持つ前例 `CleanRoomYieldLogic` の `RowId` | null 可の testid と bool 3 つ（「null なら出さない」を全使い手が覚える）／Screens へ移す（L0 の表のテストが Screens に寄る） |
| D-21 | 画面の出し分けは `BlockInventoryScreenPresenter` が `UiRoutingState.Current` とパネルの状態を購読し `BlockInventoryScreenVisibility.IsShown`（SubInventory かつ閉でない）で `View.SetShown`。Model の SubInventory 判定も `UiRoutingState` にする。名前は `BlockInventoryScreenPresenter`・`BlockInventoryView`。出所: レビュー | 設計書 §4.4「画面の出し分けは UiRoutingState と UiScreenTraitsTable だけで決める」・§1.3 の命名。前例 `PauseMenuScreenPresenter.cs:56-66` | Model が `UIStateControl.CurrentState` で決める（判定の源が 2 つ）／`GrabOverlayPresenter` の形（ルータで出し分けない層の前例） |
| D-22 | 複数レーンが ctor で受ける共有サービス（`IBlockInventoryPanelSource`・`IUiStateTransitionRequester`・`UnlockedMachineRecipeSource`・スロット操作の型付きコマンドとその実行器・`BlockItemGridFactory`）は `MainGameUiRegistration` の共有サービスの並びに登録し、画面だけのものを `BlockInventoryGameUiRegistration` に置く。出所: レビュー | E-1・A-56「作るレーンが本体の共有サービスの並びに登録する」。専用の登録口だと他レーンがその登録に依存し未マージの worktree で解決例外になる | すべて画面の登録ファイルに置く（ADR と食い違う） |
| D-23 | 大型パネルは右の余白を上書きしない（`GamePanel` 既定 10px）。右の安全帯 `--block-panel-right-safe-area` は小型（`block-inventory--natural`）だけ。テストの期待値も大型 10。出所: レビュー | Web `BlockInventoryPanel.tsx:62-66` は大型で paddingRight を書かず、`GamePanel` の `.panel { padding: 8px 10px 10px 28px }` が残る | 大型で 0 にする（本文が右へ 10px 広がり、テストがその誤りを固定する） |
| D-24 | 題の右の逃げは `calc(var(--panel-action-button-width) + var(--mantine-spacing-sm))`（12px・計 99px）で integrator へ依頼する。出所: レビュー | Web `BlockInventoryPanel.tsx:55`、UITK `MantineTokens.uss:82` `--mantine-spacing-sm: 12px` | `+ 8px`（ロケットの題で 4px ずれる） |
| D-25 | 写した 23 件は `docs/uitk/disposition.md` に書かない（E-1・設計書と一致。ADR 側は 945a356987 で「写したテストは `<web-test>` だけ・disposition には写さない処遇だけ」に直った）。R-16 は「alpha のファイルの未対応 0 かつ終了コード ≠ 2」。`localizationKeys.ts` の再生成は `docs/uitk/TODO.md`「## 追従記録」に記帳する（Task 9）。`TutorialAnchorCoveragePending.cs` の `InventoryCloseButton` の行は錨を付けるのと同じ Task 8 で消す（D-45）。出所: レビュー | `compareBundles.mjs:88` は `<web-test>` で写したテストに処遇の行があると誤りにし終了コード 2。E-1 は再生成の記帳と保留表の行の削除を同じ PR に求める | 写した件も処遇の表に書く（parity が落ちる） |
| D-26 | `regression/sectionStack` の FilterSplitter のケースは S4-special が写す。alpha は「未登録の本文 Kind は標準の本文へ逃げて警告を 1 回」を別名のテスト（`BlockInventoryPartRegistryTest`）で持つ。出所: レビュー | FilterSplitter は設定表に行が無く Generic に解決されるので、alpha の逃げの状態では `generic-block-grid` が出て Web の期待（専用本文）と食い違う | alpha のテストで期待 null のまま持つ（期待値が矛盾する） |
| D-27 | `blockInventory.spec` 2 件目の「grab オーバーレイが出る」は、送る移動を `BlockSlotCommandsTest` が、絵の表示を録画 `uitk-block-inventory` 手順 3 が主張し、写すテスト表に紐付ける。出所: レビュー | L1 で掴み絵まで組むには C4-e の `GrabOverlayModel`・ポインタ・実在庫が要り、録画の前例 `uitk-grab-overlay.cs:113` が同じ主張を持つ | 移動の記録だけで写したことにする（元テストの主張が落ちる） |
| D-28 | 区画の出し入れは区画の Presenter が `IReadOnlyReactiveProperty<bool> IsApplicable` で通知し、`SectionStackBody` が購読して `UiStack.SetItemShown` で出し入れと先頭の間隔を組み直す。出所: レビュー（PLAN-W4b の S4-special D-23 の申し出） | 網の集約・ベルトの統計はパネルの組み直しの外で届く。区画が自分で display を触ると `UiGapContainer` の先頭の間隔が崩れる | パネルの Render のときだけ `IsApplicable` を聞く／区画が自分の display を切り替える |
| D-29 | `UnlockedMachineRecipeSource` は `IGameUnlockStateData` の更新後に `UnlockedEventPacket` を受ける順に頼る。ctor 注入で `ClientGameUnlockStateData` が先に構築され、その ctor が先に購読する（`ClientGameUnlockStateDatastore.cs:112`）。Web ホスト `MachineRecipesTopic.cs:36-39` も同じ順に頼る。出所: レビュー | `IGameUnlockStateData` に変化通知が無いので、前例と同じ形にする | 源が自分で解放表を持ち直す（同じ規則の 3 か所目） |
| D-30 | 素通し getter（`Root => _root` 等）は `{ get; private set; }` に畳む。`IReadOnlyReactiveProperty<…> Current => _current` は型を狭める公開として残す。出所: レビュー | AGENTS.md「バッキングフィールド＋素通しプロパティの二重保持はこの形に畳む」。型を狭める公開は前例 `SettingsModel.cs:17` | 素通しのまま（規約違反）／`ReactiveProperty` をそのまま公開（外から書ける） |
| D-31 | 弱の重複のうち、ブロックの枠の範囲判定は `BlockSlotIndex.IsInRange` の 1 か所へ、実行器の掴みの空き判定は `Execute` の先頭で 1 回読む形へ寄せる。`KnownItemSlots` の呼び出し 2 か所・`ToSlotItems` 2 か所は呼び出し側の文脈が違うので残す。出所: レビュー | 同じ判定を 2 か所で書くと片方だけ直る | すべて残す |
| D-32 | 「無い」理由のうち登録漏れだけを警告する（`None` は無音）。帯のトークンは 3 つを別々の解決済みフラグで持つ。戻り値の形は D-35 で空の部分に替えた。出所: レビュー（弱） | 失敗の理由を 1 つの bool に畳まない | 理由を戻り値の型で区別する（使い手が空の部分以外を見ない） |
| D-33 | 録画の「main の枠を Shift」は alpha から外し、UI からの確認を S4-inventory の完了条件へ渡す（Produces 一覧に書く）。alpha の録画はブロック側の枠の操作で完結する。出所: レビュー | alpha の UITK に main の持ち物の画面が無く、UI から押せない | DSL でコマンドを直接叩いて main の Shift を録る（UI の確認にならない） |
| D-34 | `UiCommandRejectReason.BlockSlotUnavailable` を足すのと同じ Task で `PauseMenuCommandsTest` の網羅テストの `benign` 配列とメソッド名を無条件に直し、Step の絞り込みに含める。出所: レビュー | 全列挙と固定配列の照合なので、直さないと CI で初めて赤になる（Task 4 の絞り込みに入っていなかった） | 「配列を持つなら足す」の条件文 |
| D-35 | 題の操作・列車の本文の「無い」は判別子 `BlockInventoryTitleActionKind.None` 1 本で表し、工場は null の代わりに空の部分（Null Object `EmptyBlockInventoryPart`／`EmptyTrainInventoryPart`）を返す。名前は `CreateTitleAction`／`CreateTrainBody`。BodyHost の null 分岐を消し、View は `ClearTitleAction()` で器を空にする。出所: 型閉包検査 r2 | 同じ「無い」を enum と null の 2 通りで持つと、呼び手に内部値の null 分岐が散る（Global Constraints「null チェックは外部データだけ」と食い違う） | `TryCreate…(out)` に戻す（呼び手の分岐が残る）／enum だけにして未登録で例外（S4-train 未着の間に列車を開くと落ちる） |
| D-36 | 種別ごとの例外 3 か所をなくす。(1) ブロック側の計画は `BlockItemActionConversion.TryConvert` で種類ごとの型（`BlockMoveItemAction`・`BlockSplitToGrabAction`・`BlockCollectAction`。前例 `PlayerItemAction`）へ写してから実行器へ渡し、Runner は「プレイヤーへ写す → ブロックへ写す → どちらも不可は Error ログ＋拒否」。(2) `BlockInventoryPanelErrorText.TryGetLocalizationKey`（None は false）をエラー行の出し入れの唯一の分岐にする。(3) `ShiftMoveRefusalNotifier.TryGetMessageId`（DestinationFull は false）を通知の early return 1 本にする。出所: 型閉包検査 r2 | 例外は前段の分岐に頼っており、前段を変えると実行時に落ちる。型か戻り値で受けられない入力を閉じれば例外が要らない | 例外を残して前段の分岐にコメントを足す（型で閉じない） |
| D-37 | 「ブロックインベントリが開いている」は表示側のパネルの状態 1 本から導く（`IBlockInventoryPanelSource.Current` がエラー無しの `OpenBlockInventoryPanel`／`OpenTrainInventoryPanel`）。枠の値は押下の瞬間に `SubInventoryState.CurrentSubInventory.SubInventory` の生の値（コントローラの先書き込みを含む）を `ToSlotItems` で写す。番地は `MainSlotCount + slot` のまま。出所: 型閉包検査 r2、枠の値はレビュー r3 | 開閉の導出が 2 本だと表示が閉で操作が開く窓が型の上で残る。一方パネルの状態の枠は `OnSubInventoryUpdated`（サーバーのイベント）でしか組み直されず、先書きの後 1 往復は古い。イベント前に続けて押すと誤った断り・誤った分割量になる（Web ホスト `BlockAreaSlotParser`・`BlockSplitGrabActionHandler` は生の値を読む） | 開閉も枠もパネルの状態から読む（先書きの窓で誤動作）／パネルのモデルに `OnItemChange` の購読を足して組み直す（表示が Web より先に変わり、メイン側の変化でも毎回組み直す） |
| D-38 | `BlockInventoryPanelLayout` は判別子 `BlockInventoryPanelSize`（Natural・LargeNaturalBody＝列車・LargeFilledBody＝寄与の大型）を持ち、ctor を private にして 3 つの static だけを公開する。`IsLarge`・`FillsPanelHeight` は判別子からの計算プロパティ。出所: 型閉包検査 r2 | bool 2 個の公開 ctor では意味の無い (小型, 高さ確定) が作れる。読み手の形は変えずに作り手を閉じる | 公開 ctor の bool 2 個のまま（不正値が作れる） |
| D-39 | 型閉包検査 r2 の弱 7 件: 「`CreateTitleActionOrNull` が null 制約と矛盾」は D-35 で直した。残り 6 件は見送る — `SectionStackItemGridSection` の bool 合成（1 式で読め、出所の Web も同じ合成）／`BlockSlotCommands.TryReadSlot` の 2 理由（どちらも良性の `BlockSlotUnavailable` で扱いが同じ。ログに出し分けは不要）／SubInventory の判定 2 回（D-41）／「開いていて範囲内」の 2 回（コマンドは計画を組む前、実行器は実行の瞬間に読む別の時点の確認で、D-31 で述語は 1 か所）／`Error == None` の散在（判定は enum の比較 1 つで、規則が分かれる余地が無い）／`MachineRecipeLevelInheritanceConversion` の 2 case の同形（case ごとに形の読み方が変わる余地を残す 2 行で、関数化しても行数が減らない）。r3 の弱 2 件: `TryReadBlockSlots` の false の 2 理由（閉・エラー中）は扱いが同じ良性の `BlockSlotUnavailable` なので型は分けず、ログの文を `closed or in error` にした。内部の null を合図にする Try は、新しく書く `TryGetMessageId`・`TryReadBlockSlots`（の開閉判定）を switch 文で各腕が out と真偽を返す形に直し、`BlockItemActionConversion.TryConvert` は前例 `PlayerItemActionConversion.TryConvert`（`_ => null`）と対になる形を優先して残す。出所: 型閉包検査 r2・r3 | 拾う価値は故障シナリオを作れるかで決めた | すべて直す（差分が増え、故障を防がない） |
| D-40 | スロット操作は `Client.Ui.State/SlotCommands/`（Player・Block）に置き、C4-e の `Inventory/Drag`・`Inventory/Actions` に足さない。出所: 配置検査 r2 N-2 | ブロック側とプレイヤー側の押下と、両者をまたぐ Runner を 1 か所にまとめる。`Inventory/` に足すとブロックの語彙が持ち物の機能へ入る | `Inventory/Commands/` と `BlockInventory/Actions/` に分ける（Runner の置き場が決まらない） |
| D-41 | SubInventory の判定は Model（閉を返す）と `BlockInventoryScreenVisibility`（画面ルータと閉でないこと）の 2 か所に残す。出所: 配置検査 r2 N-3 | Model 側の判定は操作の口（D-37）が閉を読むために要る。画面の出し分けは前例 `PauseMenuScreenPresenter.cs:61-64` と同じく画面ルータから直接読む形にそろえる（D-21）。重なりは同じ enum の比較 1 つで、規則が分かれる余地が無い | Visibility を `!(state is Closed)` だけにする（画面の出し分けがパネルの状態の内部規則に依存し、前例と形が変わる） |
| D-42 | 本文の器 `block-inventory-body` に `block-inventory__body`（`flex-grow:1; flex-shrink:1; min-height:0; flex-direction:column`）を付け、`GamePanel` の body いっぱいに伸ばす。出所: レビュー r3 | 器が既定の `flex-grow:0` だと高さ確定の本文（`section-stack--fill-panel-height`）が伸びる余地が無く、S4-machine の「フッタは見えたまま」が成り立たない。Web は本文を GamePanel の body の直下に置く | 器を作らず本文を GamePanel の body へ直に載せる（スクロール器の包み替えの置き場が無くなる） |
| D-43 | 並べ比べの状態は参照の fixture と同じものを使い捨ての撮影スクリプトで流して作る（chest＝`Chest`・9 枠〈7・4・空 7〉、generic＝未登録の blockType・1 枠）。差の表は列数 9・1 行。出所: レビュー r3 | 比べ方の正本 `compare-procedure.md:39`。実マスタの木のチェストは 30 枠で 10 列×3 行になり、参照と状態が違う | 実マスタのまま撮って差を「他レーン持ち」で閉じる（比べになっていない） |
| D-44 | `BlockInventoryPanelErrorText.TryGetLocalizationKey` は switch 文で各腕が key と真偽を返し、未知の値は例外で止める。enum の網羅テストを 1 件足す。出所: レビュー r3 | 戻り値をキーと別の式で決めると、enum に値を足したとき空のキーで真になりエラー行が空のまま出る。未知の値は開発時の誤りなので止める | `_ => default` のまま（空の行が出る） |
| D-45 | `TutorialAnchorCoveragePending.cs` の `InventoryCloseButton` の行は、錨を付けるのと同じ Task 8 で消し、Step 4 の絞り込みに `TutorialAnchorCoverageTest` を入れる。出所: レビュー r3 | 保留表の行は feature/uitk にすでにあり、印と両方あるとテストが赤になる。消すのを Task 13 に回すと Task 8〜13 の間赤のまま進む | Task 13 で「あれば」消す（条件が実物と合わない） |
| D-46 | `%sから禁止Mantine部品とテーマ色を除去する` の写しは、USS の色の直書きが無いことに加えて、本文の木の操作の要素がすべて共通部品で素の `Button`・`Toggle`・`RadioButton`・`Slider`・`ProgressBar` を含まないことも主張する。たどり方は「`Client.Ui.Components` の型に当たったら内部へ降りず、その `contentContainer` に利用側が足した子だけ続けてたどる」（r4 で直した）。後続レーンも同じ形で写す（Produces に書く）。出所: レビュー r3・r4 | Web `blockInventoryDesign.test.ts:6-25,41-44` はコンポーネントのソースで禁止部品を見ている。色だけでは元テストの主張の半分が落ちる | 色だけを見る（元テストの主張が落ちる） |
| D-47 | 枠の生の値の読み口 `IRawSubInventorySlots`（本番 `SubInventoryRawSlots`）と、開閉と値を合わせて読む `BlockSlotReader` を切り出し、`LocalBlockInventoryOperations.TryReadBlockSlots` は読み手へ委ねる。`BlockSlotReaderTest` で「パネルの枠が古く生の値が新しいとき生の値を返す」を主張し、`BlockSlotCommandsTest` の続けて押す件は「押すたびに口を読み直す」確認に名前を直す。出所: レビュー r4 | `SubInventoryState` は ctor が `ClientContext` を要し L0 で組めない。記録係だけのテストと 0.5 秒を挟む録画では、読み元がパネルの状態へ戻っても赤にならない | 読み元は機械で確かめず未確認に書く（戻りを検出できない） |
| D-48 | `TryReadBlockSlots` に「生の値が null なら閉扱い」の分岐は足さない。出所: レビュー r4 | `UIStateControl.Update` は `OnExit`（`CurrentSubInventory = null`）→ `EnterState` → `_onStateChanged.OnNext` → `UiRoutingState.Refresh` → パネルの Model が閉へ組み直す、を 1 回の Update の中で同期に続けて行う（origin/feature/uitk の `UIStateControl.cs` の Update・`UiRoutingState.cs:32`）。UITK の押下はこの間に割り込めないので、パネルが開で生の値が null の窓は無い（`Refresh` は `ReactiveProperty` へ同期に代入。同じ状態への要求は `ConsumeWebRequest` が捨てる）。開く側も `OnExit` で null の後は `OnSubInventoryUpdated` までパネルが閉（Model の `sub == null`） | 防御の null 分岐とテストを足す（届かない分岐。null チェックは外部データだけの制約にも反する） |

---

## 設計書・実行計画へ反映する行

本体が設計書・実行計画へ写す下書き（ID は A-140〜A-144）:

- A-140: ブロックインベントリの拡張口は 6 つ。本文・題の操作・区画・列車の本文の工場は「Presenter（`Attach` で自分の Model を購読・`Detach` で切る）＋ View」を返し、パネルの状態は識別子・エラー・枠だけ（D-19）（本文 `IBlockInventoryBodyFactory`・題の操作 `IBlockInventoryTitleActionFactory`・区画 `ISectionStackSectionFactory`・列車の本文 `ITrainInventoryBodyFactory`・大型パネル判定 `IBlockInventoryLargePanelRule`・Shift 配分の束縛 `IBlockSlotRestrictionContributor`）。VContainer の複数登録で受け、二重登録は起動時に例外。後続レーンは自分の `*GameUiRegistration` で足す（D-1）
- A-141: 標準の本文の区画の順は `SectionStackSlot` の列挙が正本（Web `SectionStackView` の JSX の順）。液体の 2 区画（FluidDetails・FluidRow）は S4-machine が `Screens/BlockInventory/Fluid/` から差し込む
- A-142: スロット操作の型付きコマンドはブロック側 `IBlockSlotCommands`（`block_inventory.slot`）とプレイヤー側 `IPlayerSlotCommands`（`inventory.slot`）。InventoryPanel（S4-inventory）は `IPlayerSlotCommands` だけを呼ぶ
- A-143: 解放済みの機械レシピの源 `UnlockedMachineRecipeSource`（`Client.Ui.State/Recipe/Source/`）を S4-alpha が置き、machine・recipe と大型パネル判定の寄与の実装が使う（S4-research は未解放も引くためマスタから直接読み、使わない）
- A-144: 実行計画 §1.4 の S4-alpha 行の数を実物に直す: `SectionStackView.test`(7) → 8（alpha 3・machine 5）、`blockInventoryDesign`(S15) → 16（alpha 6・他 10）。§1.4 に無い `LackHighlightText/index.test.ts`(2) を alpha に足す

---
## 後続レーンへの Produces 一覧

S4-inventory・S4-machine・S4-train・S4-special・S4-recipe はこの節だけを読んで自分の計画を書く（S4-research は使う型が無い）。型名・シグネチャはこの計画の Task 2〜8 のコードと同じ綴り。すべて `moorestech_client/Assets/Scripts/` の下。

### 差し込みの形（全拡張口で共通。D-1・D-19）

- 差し込む部分は「**Presenter ＋ View**」。工場が作る `IBlockInventoryPart`（列車は `ITrainInventoryPart`）が Presenter で、`Root` が View。Presenter は `Attach(panel)` で**自分のレーンの State の詳細 Model**（`OpenBlockDetailTracker`・poll 源・`SubInventoryState` 等を購読して ViewModel を配る）を購読し、View の `Render(viewModel)` へ流す。`Detach()` で購読を切る。**View は購読しない**（設計書 §4.2・A-57）
- パネルの状態（`OpenBlockInventoryPanel`・`OpenTrainInventoryPanel`）は識別子・エラー・枠だけで、`BlockGameObject` や列車の源は載らない。区画の Model が対象を引く鍵は `BlockInstanceId`・`Position`・`BlockGuid`（列車は `TrainCarInstanceId`）
- 呼ばれ方: 同じパネル（`BlockInventoryPanelState.IsSamePanel`: ブロックは位置・Guid・エラー、列車は車両・エラー）かつ同じ大きさのあいだは何も呼ばれない。変わると旧部分の `Detach` → 新しく `Create` → `Attach`。画面が隠れると `Detach`
- 登録は各レーンの `Client.Starter/Registration/GameUi/<レーン>GameUiRegistration.cs` で口のインターフェイスとして登録するだけ。alpha の登録ファイルは触らない

```csharp
builder.Register<MachineSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
builder.Register<RecipeMachineLargePanelRule>(Lifetime.Singleton).As<IBlockInventoryLargePanelRule>();
```

- alpha 側は `IReadOnlyList<口>` で受ける（前例 `INestedPauseScreenState` → `UiRoutingState`）。登録 0 本でも起動する
- 同じ Kind / Slot の二重登録、`BlockInventoryBodyKind.SectionStack` と `SectionStackSlot.ItemGrid` の外部登録、列車の本文の 2 本目は起動時に `InvalidOperationException`
- 未登録の本文 Kind は標準の本文へ逃げて警告 1 回、未登録の区画 Slot は起動時の情報ログに並ぶだけ（何も描かない）

### (1) blockType ごとの本文（S4-special・専用本文を持つレーン）

```csharp
namespace Client.Ui.Screens.BlockInventory.Registry
public interface IBlockInventoryPart                    // Presenter
{
    VisualElement Root { get; }                         // View
    void Attach(OpenBlockInventoryPanel panel);         // 自分の Model を購読し View.Render へ流し始める
    void Detach();                                      // 購読を切る
}
public interface IBlockInventoryBodyFactory
{
    BlockInventoryBodyKind Kind { get; }                // Client.Ui.State.BlockInventory.Logic.Panel（L0-s4b。blockType → Kind は BlockComponentRegistry）
    IBlockInventoryPart Create(BlockInventoryBodyMount mount);
}
public sealed class BlockInventoryBodyMount { public bool FillsPanelHeight { get; private set; } }   // 大型パネルだけ真（Web fillsPanelHeight）
```
- 小型では本文は自然高のスクロール器（`block-inventory-natural-scroll`）の中、大型では器の直下に置かれる。本文は自分でスクロール器を持たない（大型の高さ確定のときだけ持ってよい）

### (2) 題の操作（S4-special のロケット）

```csharp
namespace Client.Ui.Screens.BlockInventory.Registry
public interface IBlockInventoryTitleActionFactory
{
    BlockInventoryTitleActionKind Kind { get; }         // 現状 RocketLaunchPadCommand
    IBlockInventoryPart Create();                       // Presenter ＋ View。Attach/Detach は (1) と同じ
}
```
- ブロック・エラー無しのときだけ作られ、`GamePanel` の題の操作の器（`block-inventory-title-action`）に入る
- 題の右の逃げ（Web `calc(var(--panel-action-button-width) + var(--mantine-spacing-sm))`＝99px）は alpha が integrator へ依頼済み。S4-special はロケットの並べ比べでこの差を確かめ、依頼が未処理なら LX に記帳する

### (3) 標準の本文の固定順の区画（S4-machine・S4-special）

```csharp
namespace Client.Ui.Screens.BlockInventory.SectionStack
public enum SectionStackSlot
{
    Context, ItemGrid /* alpha 専有 */, Machine, Miner, Pump, Generator,
    GearToElectric, BeltThroughput, Gear, ElectricNetwork, GearNetwork,
    FluidDetails, FluidRow,
}
public interface IBlockInventorySection : IBlockInventoryPart
{
    // Attach 後に有効。区画の Presenter が自分の Model の変化で書く（パネルの組み直しの外〈網の集約・ベルトの統計〉で変わってよい）。
    // SectionStackBody が購読して出し入れと UiStack の先頭の間隔を組み直す。区画は自分の display を触らない
    IReadOnlyReactiveProperty<bool> IsApplicable { get; }
}
public interface ISectionStackSectionFactory
{
    SectionStackSlot Slot { get; }
    IBlockInventorySection Create(SectionStackSectionMount mount);
}
public sealed class SectionStackSectionMount
{
    public SectionStackConfig Config { get; private set; }   // Client.Ui.State.BlockInventory.SectionStack
    public bool FillsPanelHeight { get; private set; }
}
namespace Client.Ui.State.BlockInventory.SectionStack   // Web configByBlockType の写し（判別 union。D-20）
public sealed class SectionStackConfig { public SectionStackItemGrid ItemGrid { get; }  public SectionStackFluid Fluid { get; } }
public abstract class SectionStackItemGrid { public static readonly SectionStackItemGrid Hidden; }
public sealed class ShownSectionStackItemGrid : SectionStackItemGrid { public string GridId { get; }  public bool RenderEmpty { get; } }
public abstract class SectionStackFluid { public static readonly SectionStackFluid Hidden; }
public sealed class RowSectionStackFluid : SectionStackFluid { public string RowId { get; }  public bool ShowProgress { get; } }
public sealed class DetailsSectionStackFluid : SectionStackFluid { public string RowId { get; } }
public static class SectionStackConfigTable { public static SectionStackConfig Resolve(string blockType); }
```
- 持ち主: Context・Machine・Miner・Pump・Generator・FluidDetails・FluidRow → S4-machine（液体の 2 区画の部品は `Screens/BlockInventory/Fluid/`）。GearToElectric・BeltThroughput・Gear・ElectricNetwork・GearNetwork → S4-special
- Web の条件の写し: FluidDetails は `Config.Fluid is DetailsSectionStackFluid`、FluidRow は `Config.Fluid is RowSectionStackFluid && !機械`、Context は `!機械`（機械では Machine 区画の先頭に置く。D-3）。機械かどうかの判定は machine 側で持つ
- 区画の間は `UiStack(UiGap.Sm)`（Web `Stack gap="sm"`）。区画は自分の外側の余白を持たない
- テストの形の手本: `SectionStackViewTest` の「区画の出し入れはパネルの描き直し無しで変わり先頭の間隔が詰まる」

### (4) source=train の本文（S4-train）

```csharp
namespace Client.Ui.Screens.BlockInventory.Registry
public interface ITrainInventoryBodyFactory { ITrainInventoryPart Create(); }   // 登録は 1 本だけ
public interface ITrainInventoryPart                    // Presenter
{
    VisualElement Root { get; }                         // View
    void Attach(OpenTrainInventoryPanel panel);         // 列車の詳細 Model（S4-train の State。SubInventoryState の TrainSubInventorySource 等を購読）を購読
    void Detach();
}
namespace Client.Ui.State.BlockInventory.Panel
public sealed class OpenTrainInventoryPanel : BlockInventoryPanelState
{
    public long TrainCarInstanceId { get; }  public IReadOnlyList<SlotItem> ItemSlots { get; }  public BlockInventoryPanelError Error { get; }
}
```
- 列車は常に大型（`IsLarge == true`・`FillsPanelHeight == false`）。題（`ui.blockInventory.trainInventory`）・閉じる・エラー行（`train-inventory-error`）は alpha の外殻が描く。エラー中は本文を作らない
- `TrainSubInventorySource`（時刻表等の源）はパネルの状態に載らない。列車の詳細 Model が `SubInventoryState.CurrentSubInventorySource` から自分で引く

### (5) 大型パネル判定の寄与（S4-machine）

```csharp
namespace Client.Ui.State.BlockInventory.Panel
public interface IBlockInventoryLargePanelRule
{
    bool IsLargePanel(OpenBlockInventoryPanel panel);   // どれか 1 つが真なら大型・FillsPanelHeight も真
    IObservable<Unit> OnRuleInputChanged { get; }       // 判定の材料が変わったら流す（LayoutModel が再計算する）
}
public enum BlockInventoryPanelSize { Natural, LargeNaturalBody, LargeFilledBody }
public readonly struct BlockInventoryPanelLayout { public static readonly BlockInventoryPanelLayout Natural, LargeNaturalBody, LargeFilledBody; public readonly BlockInventoryPanelSize Size; public bool IsLarge { get; } public bool FillsPanelHeight { get; } }  // ctor は private（D-38）
public sealed class BlockInventoryPanelLayoutModel : IInitializable { public IReadOnlyReactiveProperty<BlockInventoryPanelLayout> Current { get; } }
```
- 「レシピ有り機械」の判定は下の機械レシピの源をコンストラクタで受けて組める: `IsLargePanel` は blockGuid で `UnlockedMachineRecipeSource.Current.Value` を引き、`OnRuleInputChanged` は `source.Current.AsUnitObservable()`

### (6) Shift 配分の束縛の寄与（S4-machine・S4-special）

```csharp
namespace Client.Ui.State.BlockInventory.SlotRestriction
public interface IBlockSlotRestrictionContributor
{
    bool AppliesTo(OpenBlockInventoryPanel panel);
    OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input);   // input.WithMachine(...) / input.WithInletBindings(...) を返す
}
```
- 寄与が無いブロックは無制限（チェスト等）。`AppliesTo` が真の寄与を登録順に重ねる。結果は L0-s4b の `BlockSlotRestrictionResolver.Resolve` に渡る。束縛の詳細は寄与の実装が自分の State の Model から読む（パネルの状態には載らない）

### スロット操作の型付きコマンド（S4-inventory・ブロックの枠を自前で描く後続）

```csharp
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
namespace Client.Ui.State.SlotCommands.Block
public interface IBlockSlotCommands
{
    UiCommandResult LeftDown(int blockSlot, bool shift);
    UiCommandResult RightDown(int blockSlot);
    UiCommandResult DoubleClick(int blockSlot);
}
public sealed class BlockSlotCommands : IBlockSlotCommands { public const string CommandName = "block_inventory.slot"; }
```
- 押下の写し（`ItemSlot.OnSlotGestured` の `SlotGestureKind` → コマンド）: プレイヤーの枠は LeftDown→`LeftDown(slot, gesture.Shift)`・RightDown→`RightDown`・DoubleClick→`DoubleClick`・LeftEnter→`LeftEnter`・RightEnter→`RightEnter`。ブロックの枠は LeftDown・RightDown・DoubleClick だけ（進入は使わない。Web と同じ）
- 結果は呼び手が `UiCommandResultToaster.Report(<CommandName>, result)` へ渡す
- ブロックの枠を自前で並べる本文（機械の入力帯等）は `BlockItemGridFactory.Create(testId)` の `BlockItemGrid.Render(itemSlots, slotIndices)` を使えば押下の配線まで済む（`Render` は区画の Presenter が呼ぶ）
- 分割ドラッグの確定・掴みの中身は C4-e（`IPlayerSlotDragCommands`・`PlayerGrabModel`）が持つ。InventoryPanel は `IPlayerSlotCommands` 以外を呼ばない
- **S4-inventory へ渡す完了条件**（D-33）: 「ブロックを開いた状態で main の枠を Shift＋左押下するとブロックへ配分される」を UITK の持ち物画面から録画で確かめる（alpha の録画はブロック側の枠で完結し、main 側は L0 の `PlayerSlotCommandsTest` だけ）

### 画面の部品（`Client.Ui.Screens.BlockInventory.Parts`）

```csharp
public enum LackHighlightTextSize { Xs, Sm }
public sealed class LackHighlightText : LocalizedLabel { public LackHighlightText(string testid, LackHighlightTextSize size); public void Render(UiText text, bool insufficient); }
public sealed class BlockItemGrid : VisualElement { public void Render(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<int> slotIndices); }
public sealed class BlockItemGridGroup : VisualElement { public BlockItemGridGroup(LocalizationKey label, BlockItemGrid grid); }
public sealed class BlockItemGridFactory { public BlockItemGrid Create(string testId); }   // 共有サービス（MainGameUiRegistration）
namespace Client.Ui.Screens.BlockInventory.Parts.Rows
public sealed class PowerRateText : VisualElement { public PowerRateText(string testid); public void Render(double currentPower, double requestPower, bool highlightShortage); }
public sealed class MachineStateRow { public MachineStateRow(string stateTestId, string powerRateTestId); public void MountInto(UiGapContainer parent); public void Render(MachineProcessDisplayState state, double currentPower, double requestPower, bool highlightPowerShortage); }
public sealed class PerMinuteRateRow : VisualElement { public PerMinuteRateRow(VisualElement leading); public void Render(double amountPerMinute); }
```

### パネルの状態（区画・本文の Presenter が識別に読む）

```csharp
namespace Client.Ui.State.BlockInventory.Panel
public interface IBlockInventoryPanelSource { IReadOnlyReactiveProperty<BlockInventoryPanelState> Current { get; } }   // 共有サービス（BlockInventoryPanelModel）
public abstract class BlockInventoryPanelState { public static readonly BlockInventoryPanelState Closed; public abstract bool IsSamePanel(BlockInventoryPanelState other); }
public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState
{
    public string BlockType { get; }  public Guid BlockGuid { get; }  public BlockId BlockId { get; }
    public BlockInstanceId BlockInstanceId { get; }  public Vector3Int Position { get; }       // 区画の Model が開いたブロックを引く鍵
    public IReadOnlyList<SlotItem> ItemSlots { get; }  public BlockInventoryPanelError Error { get; }  public BlockInventoryComponents Components { get; }
}
public enum BlockInventoryPanelError { None, OpenFailed, ContainerMissing, TrainCarMissing }   // 「エラー無し」は == None で判定する（null と比べない）
```
- テストでパネルを組むときは `Client.Tests.Ui.State.BlockInventory.PanelModel.BlockInventoryTestPanels`（`Chest()`・`ChestWithSlots(...)`・`OpenFailedChest()`・`Train(error)`・`OfType(blockType, ...)`）と `FakePanelSource`（`State` で書き換え）を使う

### 遷移要求の口（閉じるを持つ後続の画面）

```csharp
namespace Client.Ui.State.Routing
public interface IUiStateTransitionRequester { bool RequestTransition(UIStateEnum requested); }   // 共有サービス。入れ子ポーズは入れ子だけ閉じ、許可表に無い遷移は false＋Warning ログ
public static class UiStateTransitionRules { public static bool IsAllowed(UIStateEnum current, UIStateEnum requested); }   // Web UiStateActions.IsAllowed の写し
```

### 解放済みの機械レシピの源（S4-machine・S4-recipe と大型パネル判定の寄与の実装。S4-research は未解放も引くのでマスタから直接読み、使わない）

```csharp
namespace Client.Ui.State.Recipe.Source
public sealed class UnlockedMachineRecipeSource : IInitializable   // 共有サービス（AsSelf）
{
    public UnlockedMachineRecipeSource(IGameUnlockStateData unlockState, ILevelInheritanceCraftCapLookup craftCapLookup, IVanillaApiEvent vanillaApiEvent);
    public IReadOnlyReactiveProperty<IReadOnlyList<MachineRecipeData>> Current { get; }   // 解放済み・レベル継承つき。UnlockedEventPacket と OnCraftCapUnlocked で組み直す
}
public static class UnlockedMachineRecipeListBuilder { public static IReadOnlyList<MachineRecipeData> Build(IGameUnlockStateData unlockState, ILevelInheritanceCraftCapLookup craftCapLookup); }
public static class MachineRecipeSummaryConversion { public static IReadOnlyList<MachineRecipeSummary> ToSummaries(IReadOnlyList<MachineRecipeData> recipes); }
```
- `MachineRecipeData` は `Client.Ui.State.Recipe.Data`、`MachineRecipeSummary` は `Client.Ui.State.BlockInventory.Logic.Machine`（いずれも既存）。`Client.Ui.State.asmdef` への `Game.UnlockState` 参照は alpha が足す

### 登録・共有区画

- 共有サービス（後続が ctor で受けてよい）は `MainGameUiRegistration` の共有サービスの並びにある: `IBlockInventoryPanelSource`・`IUiStateTransitionRequester`・`UnlockedMachineRecipeSource`・`IBlockInventoryOperations`・`BlockItemActionExecutor`・`PlannedItemActionRunner`・`BlockSlotRestrictionInputBuilder`・`PlayerSlotContextReader`・`ShiftMoveRefusalNotifier`・`IBlockSlotCommands`・`IPlayerSlotCommands`・`BlockItemGridFactory`。`IItemStackLevelLookup` は `MainGameModelRegistration`。後続が新しく複数レーンで共有するサービスを作るときも同じ並びに足す（E-1・A-56）
- `IBlockInventoryOperations.TryReadBlockSlots` は `IBlockInventoryPanelSource` がエラー無しのブロック／列車のときだけ真（表示と同じ状態から導く。D-37）。ブロック側の計画は `BlockItemActionConversion.TryConvert` で `BlockItemAction` に写してから `BlockItemActionExecutor.Execute` へ渡す（D-36）。`BlockInventoryPanelLayout` は 3 つの static だけで作る（D-38）。レジストリの題の操作・列車の本文は null を返さず空の部分を返す（D-35）
- 画面だけのもの（LayoutModel・閉じる・レジストリ・ScreenPresenter）は `BlockInventoryGameUiRegistration`。後続は自分の登録ファイルを作り、画面登録区画に 1 行足す。`Registration/GameUi/` は alpha で 9 ファイル、後続で 10 を超えるので integrator にサブディレクトリ化を依頼済み
- USS は自分の画面の USS を `GameUiTheme.tss` の `Screens` 区画に `@import` する。色は `--text-*` 等のトークンだけ。`:root` のトークンを C# で読むなら自分のクラスで受け直す（D-8）。「色を直書きしない」の確認は alpha の `BlockInventoryDesignTest` に足さず、自分のテストファイルに同じタグ（`%sから禁止Mantine部品とテーマ色を除去する`）で写す
- 同じタグでは、USS の色の直書きが無いことに加えて、自分の本文・区画の木の操作の要素がすべて共通部品（`PanelActionButton`・`ModeSwitch`・`IconButton`・`GameTextField` 等。`Client.Ui.Components` の型）で、素の `Button`・`Toggle`・`RadioButton`・`Slider`・`ProgressBar` を含まないことも主張する。たどるときは `Client.Ui.Components` の型（`GameScrollView` を含む）に当たったら内部へ降りず、その `contentContainer` に自分が足した子だけ続けてたどる（`GameScrollView` が中に持つ `Scroller`・`Slider` で赤にしない。alpha の `本文の木の操作の要素は共通部品だけで素の操作部品を含まない` と同じ形。D-46）
- `IBlockInventoryOperations.TryReadBlockSlots` の枠の値は押下の瞬間の `SubInventoryState` の生の値（先書き込みを含む）で、パネルの状態の `ItemSlots` とは 1 往復ずれうる。操作の計画はパネルの状態ではなくこの口から読む（D-37）

---

## 起草メモ（本体向け）

- **レビュー反映（19 項目＋追加 3 件）**: 判断記録 D-8・D-12・D-14・D-16・D-18 を直し、D-19〜D-34 を足した（すべて「出所: レビュー」）。Error の null 比較・トークンの受け直し・大型の右余白・題の逃げ 12px・処分表・区画と本文の口の形・出し分け・共有サービスの登録先・網羅テスト・FilterSplitter・掴み絵・録画の main Shift・後続のテスト追記・素通し getter・判別 union・中立名・遷移要求の置き場・死活表・軽微 5 件。追加: S4-research は機械レシピの源を使わない、区画の出し入れの通知（`IsApplicable`）、`BlockInstanceId` の公開
- **数の食い違い（申告）**: 実行計画 §1.4 の `SectionStackView.test`(7)・`blockInventoryDesign`(S15) は実物で 8・16。区画の中身にかかる件は持ち主（machine・special・train）へ割り振った（「写すテスト」表）。§1.4 に無い `LackHighlightText/index.test.ts`(2) を alpha に足した。反映の下書きは A-144
- **本体の追加決定の反映**: 機械レシピの源は Task 2（共有サービスの並びに登録・asmdef に `Game.UnlockState`）。使うのは machine・recipe と大型パネル判定の寄与の実装（research は使わない）。液体の 2 区画は `SectionStackSlot.FluidDetails`・`FluidRow` の空き区画として S4-machine に渡す
- **integrator への依頼（Task 1 で done.md に書く）**: (a) `Registration/GameUi/` のファイル数が後続で 10 を超える → サブディレクトリ化、(b) `GamePanel` が題の操作を持つときの題の右の逃げ（`--panel-action-button-width + --mantine-spacing-sm`＝99px。Web は画面側の paddingRight。UITK は題の要素が部品の中にあり画面 USS から当てられない）、(c) 設計書 §2 の State の表に `SlotCommands/` と `Routing/` の遷移要求の口（A-142）
- **要裁定: なし**。目的・裁定から外れる判断は無い。D-9（`DestinationFull` は通知せずログだけ）は Web に無い断りの扱いで、文言を足すなら Web 側も足すことになるため後送り
- **残った問題・未確認（実装時に確かめる）**: 「掴んだまま閉じる」の掴みの行き先（死活表の要確認。Task 1 Step 5）。`LocalizationTextParam`・`UiTestHost` の押下ヘルパーの実名。`BlockInstanceId` の ctor の公開範囲（テストで `new BlockInstanceId(1)` を使う）。D-1 の「VContainer 1.10 は登録 0 本で空の列」は前例がどれも 1 本以上なので Task 8 Step 5 で実測する。`GameScrollView` の viewport が Web `.scroll` の `flex: 1; min-height: 0` 相当に縮むか（`BlockInventoryNaturalScrollTest` の 3 件目で確かめる）
- **通知の messageId**: `denied.inventoryMoveSlotRejected`・`denied.inventoryOpenFailed`・`denied.inventoryDetailLoading` は Web と同じ id を `ClientLocalNotificationSource` へ流す。UITK の通知レーンがこの id を文言へ解決できるかは通知レーン側の確認事項
- **C3-b の型（E-22）**: 使う。`ItemSlot` の名前ツールチップが `GameUiHoverTooltipInstaller`（C3-b）経由で出るのと、`GameScrollView`（自然高のスクロール器）。Global Constraints の依存に C3-b を足した（マージ済み）
- **Web の外殻から写さなかったもの**: `data-large` 属性はクラス `block-inventory--large`（小型は `block-inventory--natural`）で表す
- 設計検査は 2 本とも実施済み、反映後の r2 の再検査も実施済み（記録は「## 設計検査記録」）。r2 の指摘 C1〜C5・O1・O2・O4・V-1・T1〜T3・T5 は反映済み（D-35〜D-41）。r3 の 10 件・D-37 の先書きの窓・禁止部品の主張は反映済み（D-37・D-39・D-42〜D-46）。r4 の 4 件と `OnExit` の窓の確認は D-46〜D-48

---

## 本 PR 外のリファクタ提案

Phase 2.6 の第 3 バケツ。このレーンのタスクにはしない（人へ 1 行ずつ）。

- 既存 `Client.Ui.State/Inventory/Actions/PlayerItemAction.cs`（`FromOutcome`・`SplitToGrabAction`・`CollectAction`）と新設 `Client.Ui.State/SlotCommands/Block/BlockItemAction.cs`（`FromOutcome`・`BlockSplitToGrabAction`・`BlockCollectAction`）: 移動の結果を A-94 の理由へ写す switch・半分の床関数・集める先の規則が同じ State 層に 2 本ある（D-6）
- 既存 `Client.Ui.State/Inventory/Actions/LocalPlayerInventoryOperations.cs`（`Move` の結果の switch・`ToSlotItem`）と新設 `LocalBlockInventoryOperations.Move`・`BlockInventoryPanelStateFactory.ToSlotItems`: 結果の写しと `IItemStack`→`SlotItem` の変換が同じ式で、既存側が private のため写している
- 既存 `Client.WebUiHost/Game/Actions/Inventory/BlockInventoryActions.cs`（`BlockAreaSlotParser`・`MainSlotCount + slot`・ブロックの分割）・`InventoryActions.cs`（`ResolveCollectTarget`）と新設 `LocalBlockInventoryOperations`・`BlockItemActionExecutor`: 開いている判定・結合インベントリの番地・分割と集める先を同じ規則で写している（D-5）
- 既存 `Client.WebUiHost/Game/Topics/MachineRecipesTopic.cs`・`MachineUpgrade/MachineRecipeLevelInheritanceDtoBuilder.cs` と新設 `Client.Ui.State/Recipe/Source/UnlockedMachineRecipeListBuilder.cs`・`MachineRecipeLevelInheritanceConversion.cs`: 解放済みの機械レシピの列挙と levelInheritance の網羅 switch が同じ規則
- 既存 `Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs`・`Inventory/BlockInventoryDtoFactory.cs`・`TrainInventoryDtoFactory.cs` と新設 `BlockInventoryPanelModel`・`BlockInventoryPanelStateFactory`: パネルの開閉条件・再計算の契機・取得失敗と列車エラーの写しが同じ規則（あわせて `RequestUiStateActionHandler.IsAllowed` と新設 `UiStateTransitionRules` も同じ表）
- 既存 `Client.Game/InGame/UI/Inventory/RecipeViewer/ItemRecipeViewerDataContainer.cs:167` `UnlockedMachineRecipes()`（`MachineRecipeUnlockStateInfos[guid].IsUnlocked` で解放済み機械レシピを列挙）と新設 `Client.Ui.State/Recipe/Source/UnlockedMachineRecipeListBuilder.Build`: 同じ「解放済み機械レシピの列挙」が 2 本。既存側は `Client.Game` にあり UITK 切替日の Web 撤去でも消えない

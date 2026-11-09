# UITK S4-alpha（段階 4 の頭: 持ち物の中核）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/blockInventory` の外殻（`BlockInventoryPanel.tsx`: viewer 枠の上端揃え・大型パネル・自然高のスクロール器・`--block-panel-*` の余白・閉じるボタンと tutorial anchor・取得失敗と列車のエラー文）と、blockType → 本文の解決（`blockComponentRegistry`）、標準の本文 `SectionStackView`（区画の固定順）、`BlockItemGrid`・`LackHighlightText`・rows、スロット操作の解釈（ブロック側とプレイヤー側の両方）を、C3-a・C3-b・C4-e の部品と口、L0-s4a・L0-s4b のロジックで Web と同じ見た目・同じ操作の UITK 画面にする。後続の S4-inventory・S4-machine・S4-train・S4-special が自分のディレクトリと自分の登録ファイルだけで差し込める拡張口 6 つ（本文・題の操作・区画・列車の本文・大型パネル判定の寄与・Shift 配分の宛先の束縛の寄与）と、型付きのスロット操作コマンド（`IPlayerSlotCommands`・`IBlockSlotCommands`）、解放状態つきの機械レシピ一覧の源（`UnlockedMachineRecipeSource`）を作り、L0/L1・並べ比べ（720p・2560×1080）・録画（新設 `uitk/uitk-block-inventory`・`uitk/uitk-lifeline`）まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。State（`Client.Ui.State`）に 4 つの塊を置く。(1) パネルの状態 `BlockInventoryPanelModel`（`SubInventoryState`・`UIStateControl`・`OpenBlockDetailTracker` を購読し、Web ホスト `BlockInventoryTopic`・`BlockInventoryDtoFactory`・`TrainInventoryDtoFactory` と同じ規則で開閉・source・エラー・スロットを組む。`Client.WebUiHost` は参照しない）と大きさの判定 `BlockInventoryPanelLayoutModel`（列車は常に大、ブロックは寄与の口 `IBlockInventoryLargePanelRule` のどれかが真なら大）。(2) スロット操作: ブロック側は L0-s4a の `BlockSlotPlanner` の計画を、プレイヤー側は C4-e の `PlayerSlotActionPlanner` の計画を、`PlannedItemActionRunner` が `PlayerItemActionConversion.TryConvert` で振り分け、true は C4-e の `PlayerItemActionExecutor`、false は新設のブロック側実行器 `BlockItemActionExecutor`（`IBlockInventoryOperations` の本番 `LocalBlockInventoryOperations` が結合インベントリの番地 `MainSlotCount + slot` でコントローラを呼ぶ。Web ホスト `BlockInventoryActions.cs` と同じ規則）へ渡す。Shift の断りは Web と同じ messageId で `ClientLocalNotificationSource` へ通知し開発者ログへも出す。(3) Shift 配分の宛先の束縛は L0-s4b の `BlockSlotRestrictionResolver` に、寄与の口 `IBlockSlotRestrictionContributor`（機械・ロケット・宇宙エレベーターの束縛は持ち主のレーンが足す）で組んだ入力を渡す。(4) 機械レシピの源 `UnlockedMachineRecipeSource`（`Client.Ui.State/Recipe/Source/`。Web ホスト `MachineRecipesTopic` と同じ規則・同じ再構築の契機）。View 側（`Client.Ui/Screens/BlockInventory/`）は Presenter が起動時に 1 回だけ外殻 View を `StageColumnsLayout.Get(StageColumn.Viewer)` へ載せ [A-45]、Model の状態（開閉は Model が `UIStateControl` で決める）と大きさで出し入れする。本文は `BlockInventoryPartRegistry` が `BlockComponentRegistry.Resolve(blockType).Body` で引き、無ければ alpha の既定 `SectionStackBody`。拡張口は VContainer の同一インターフェイスの複数登録を `IReadOnlyList<T>` で受ける形（前例 `INestedPauseScreenState`・`ITutorialViewManager`。D-1）。見た目の値は Web と同名のトークン（`--block-panel-bottom-safe-area`・`--block-panel-right-safe-area`・`--block-panel-body-bleed`・`--menu-upper-safe-area`・`--menu-lower-safe-area`・`--inventory-panel-width`・`--stage-column-gap`・`--stage-padding-left/right`・`--panel-edge-fade`・`--bevel-3`・`--panel-action-button-width`）だけを使い、`calc()` の寸法（帯の高さ 482・大型の幅 740.108753）は View の C# が舞台の寸法とトークンから導く [A-47]。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`GeometryChangedEvent`・`CustomStyleResolvedEvent`）、UniRx、VContainer 1.10（コレクション解決）、NUnit＋`com.unity.ui.test-framework`、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（test-name-parity）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs`、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`。Files 節とコマンドはリポジトリ相対の完全なパスで書く（`S/` は Files 節の読みやすさのための略記で、実体は `moorestech_client/Assets/Scripts/`）
- **正本**: 裁定 `.decisions/2026-11-06-*`（「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1・§2・§3・§4.1〜§4.3・§5.1・§7.1 の A-45・A-47・A-57・A-70・A-72〜A-74・A-90〜A-99・A-110・A-111）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-alpha 行・§4.3・§5・§6.2・E-1・E-16・E-18〜E-22）、先行レーンの申し送り（`LOGS/harness/uitk/C4-e/done.md`・`L0-s4a/done.md`・`L0-s4b/done.md`・`conductor.md`「## 引き継ぎ」）、本体の追加決定（2026-11-10。機械レシピの源は alpha・液体の中身表示は S4-machine が `Screens/BlockInventory/Fluid/` に作り alpha は区画を空ける）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ

## Requirements

設計の出所: 実行計画 §1.4 の S4-alpha 行（完了条件の原文:「`BlockInventoryPanel` の外殻（`StageColumnsLayout` の `Viewer` 枠へ [A-45]）・`blockComponentRegistry`・`SectionStackView`・`BlockItemGrid`・`Parts/`（`LackHighlightText`・rows）・スロット操作のコマンド配線（ItemMove の計画 → 型付きコマンド）。写す: `blockInventory.spec`(3)・`blockInventoryGestures`(5)・`blockRegistryCoverage`(1)・`regression/sectionStack`(1)・`SectionStackView.test`(7)・`blockInventoryDesign`(S15)・localization(S2)。プレイヤー側の実行器・分割ドラッグ・進入・掴みの中身は C4-e の口（`PlayerSlotRef`・`PlayerItemActionConversion.TryConvert`・`PlayerItemActionExecutor`・`IPlayerSlotDragCommands`〈開始と進入。確定は C4-e が解放で行う〉・`IPlayerInventoryOperations`・`PlayerGrabModel`）を使う。`TryConvert` が false の計画はブロック側の実行器へ。L0-s4a の申し送り（`DestinationFull` の文言・`BlockSlotPlanner`・`DirectMovePlanner`）はこのレーン [A-93]」）、指揮の依頼（拡張口 5 つと型付きコマンドを Produces と末尾節に）、本体の追加決定 2 件。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | パネルの状態: `UIStateControl.CurrentState == SubInventory` かつ `SubInventoryState.CurrentSubInventory != null` のときだけ開く。source が列車（`TrainSubInventorySource`）なら `OpenTrainInventoryPanel`（エラーは `LastOpenMessage` から ContainerMissing / TrainCarMissing / OpenFailed）、ブロックなら `OpenBlockDetailTracker.TrackedBlock` から `OpenBlockInventoryPanel`（`SubInventoryModel.Result != Success` で OpenFailed。blockType・blockGuid・BlockId・位置・スロット・`BlockComponentRegistry.Resolve` の結果）。追跡ブロックが無ければ閉。再計算の契機は Web ホストと同じ（状態遷移・`OnSubInventoryUpdated`・追跡替え・追跡ブロックの状態変化） | `BlockInventoryPanelStateFactoryTest` が緑。録画 `uitk/uitk-block-inventory` で開閉が主張どおり |
| R-2 | 外殻: `StageColumnsLayout.Get(StageColumn.Viewer)` へ載る `block-inventory`（`data-large` 相当のクラス `block-inventory--large`）。`GamePanel(block-inventory-panel, Default)`。下の余白 `--block-panel-bottom-safe-area`、小型は右の余白 `--block-panel-right-safe-area`、最大高は帯の高さ（舞台の高さ − `--menu-upper-safe-area` − `--menu-lower-safe-area`）、大型は幅 = 舞台の幅 − `--stage-padding-left` − `--stage-padding-right` − `--inventory-panel-width` − `--stage-column-gap`・高さ = 帯の高さ。寸法は C# が導く [A-47] | `BlockInventoryDesignTest`（alpha 分 6 件）・`BlockInventoryPanelBandTest` が緑。並べ比べ R-14 |
| R-3 | 題: ブロックは `ContentLocalizationKeys.BlockName(blockGuid)`、列車は `ui.blockInventory.trainInventory`。言語を変えると題が描き直される。題の操作（ロケット）はブロック・エラー無し・登録ありのときだけ `GamePanel.SetTitleAction` で出す（器 `block-inventory-title-action` を 1 回渡して中身を付け替える。D-17）。題の右の逃げ `calc(--panel-action-button-width + 8px)` は `GamePanel` に口が無いので integrator へ依頼する（Task 1 Step 3） | `BlockInventoryLocalizationTest`「blockGuidの導出キーを…」と `BlockInventoryTitleActionTest` が緑 |
| R-4 | 閉じる: `IconButton(block-inventory-close, IconButtonCloseGlyph)`、位置は top `--bevel-3`・right `--panel-edge-fade`、錨 `AnchorIds.InventoryCloseButton`（`TutorialAnchor.Mark`）。押すと `IBlockInventoryPanelCommands.Close()` が `UIStateControl.RequestTransition(GameScreen)` を呼ぶ（Web `ui_state.request gameScreen`） | `BlockInventoryPanelFlowTest`「ブロックを閉じると panel が消える」と `BlockInventoryPanelCommandsTest` が緑 |
| R-5 | エラー行: ブロック `block-inventory-error`・列車 `train-inventory-error`。文言 OpenFailed→`ui.blockInventory.inventoryOpenFailed`、ContainerMissing→`ui.blockInventory.trainNoItemContainer`、TrainCarMissing→`ui.blockInventory.trainNotFound`。エラー中は本文と題の操作を出さない | `BlockInventoryLocalizationTest`「取得失敗はブロック本文を隠し、失敗文を表示する」と `BlockInventoryPanelErrorTest` が緑 |
| R-6 | 本文: 大型は本文をそのまま（機械の大型なら `FillsPanelHeight`）、小型は自然高のスクロール器（`GameScrollView(block-inventory-natural-scroll)`。外側の負マージン `--block-panel-body-bleed`・内側の逃げ同値）で包む。本文は `BlockComponentRegistry.Resolve(blockType).Body` の `IBlockInventoryBodyFactory` を引き、`SectionStack` か未登録なら alpha の `SectionStackBody`（未登録の Kind は警告ログを 1 回）。列車は `ITrainInventoryBodyFactory`（未登録なら警告ログ） | `BlockInventoryPartRegistryTest`・`BlockInventoryNaturalScrollTest` が緑 |
| R-7 | `SectionStackBody`: Web `SectionStackView` の順（Context → 項目グリッド → Machine → Miner → Pump → Generator → GearToElectric → BeltThroughput → Gear → ElectricNetwork → GearNetwork → FluidDetails → FluidRow）を `UiStack(UiGap.Sm)` で固定し、項目グリッドは alpha（`configByBlockType` の写し `SectionStackConfigTable`。`renderEmptyGrid || slots>0` で出す）、残りは `ISectionStackSectionFactory` の差し込み（`IsApplicable` が偽なら隠す）。液体の 2 区画は S4-machine が埋める空き区画 | `SectionStackViewTest`（alpha 分 3 件）・`SectionStackRegressionTest`（alpha 分 1 件）が緑 |
| R-8 | `BlockItemGrid`: 列数 `min(10, max(1, 枠数))`・枠 `ItemSlot(<testId>-slot-<index>)`、押下・右押下・ダブルクリックを `IBlockSlotCommands` へ。`BlockItemGridGroup`（ラベル `text--sm text--muted`・間 4px）。`LackHighlightText`（`data-insufficient` 相当のクラス `lack-highlight-text--insufficient`・色 `--text-default` / `--text-insufficient`）・`MachineStateRow`・`PerMinuteRateRow`・`PowerRateText` | `LackHighlightTextTest` 2 件・`BlockInventoryDesignTest`「ブロックスロットの列数を…」・`BlockInventoryRowsTest` が緑 |
| R-9 | ブロック側のスロット操作: 左＝`BlockSlotPlanner.PlanLeftClick`（Shift・空手・中身ありは main へ配分、掴み中は全量置き、中身ありは全量掴み）、右＝`PlanRightClick`（掴み中は 1 個置き、空手は分割）、ダブル＝`PlanDoubleClick`（集める先は持ち手の有無で決める）。計画は操作の瞬間の在庫で組む。ブロック側の計画は `BlockItemActionExecutor` が実行（分割: 掴み中は `SplitGrabNotEmpty`・空は `ItemSourceSlotEmpty`・半分 0 は何もしない成功。移動の結果は A-94 の理由へ） | `BlockSlotCommandsTest`（Web `blockInventoryGestures` 1〜4 件目・`blockInventory.spec` 2 件目）・`BlockItemActionExecutorTest` が緑 |
| R-10 | プレイヤー側の型付きコマンド `IPlayerSlotCommands`（LeftDown/RightDown/DoubleClick/LeftEnter/RightEnter）を InventoryPanel（S4-inventory）向けに出す。ブロックが開いていれば `PlayerSlotContext.WithBlockOpen`（スロット・`BlockSlotRestrictionResolver` の束縛）、閉なら `WithBlockClosed`。`BeginSplitDragPlan`→`IPlayerSlotDragCommands.BeginSplitDrag`、進入は C4-e の口へ素通し | `PlayerSlotCommandsTest`（Web `blockInventoryGestures` 5 件目）が緑 |
| R-11 | Shift の断り: NoAcceptingBlockSlot→`denied.inventoryMoveSlotRejected`・InventoryOpenFailed→`denied.inventoryOpenFailed`・DetailLoading→`denied.inventoryDetailLoading` を `ClientLocalNotificationSource.Notify(NotificationMessagePack.CreateOperationDenied(id, 空))` で出し、`[inventory.shift_move] not sent: <reason> block=<id|(closed)> item=<id>` を Warning ログ。`DestinationFull`（Web に無い）は通知せず Warning ログだけ（D-9）。`BlockSlotPlanner` の Shift→main が満杯で空配列のときも Warning ログ（D-10）。`DirectMovePlanner` へは既知の ItemId のスロットだけ渡し、未知 ID は Error ログで止める（D-11） | `ShiftMoveRefusalNotifierTest`・`PlayerSlotCommandsTest`・`BlockSlotCommandsTest` の該当件が緑 |
| R-12 | 機械レシピの源: `UnlockedMachineRecipeSource`（`Client.Ui.State/Recipe/Source/`）が解放済みの機械レシピ（`MachineRecipeData` の列。レベル継承つき）を `IReadOnlyReactiveProperty` で配る。再構築は `UnlockedEventPacket` と `ILevelInheritanceCraftCapLookup.OnCraftCapUnlocked`（Web `MachineRecipesTopic` と同じ）。要約への変換 `MachineRecipeSummaryConversion` も出す | `UnlockedMachineRecipeListBuilderTest`・`MachineRecipeSummaryConversionTest` が緑 |
| R-13 | 拡張口 6 つ（本文・題の操作・区画・列車の本文・大型パネル判定・Shift 配分の束縛）を VContainer の複数登録で受ける。登録 0 本でも起動し（VContainer 1.10 は空のコレクションを返す）、同じ Kind / Slot の二重登録は起動時に例外で止める | `BlockInventoryPartRegistryTest`・`SectionStackSlotRegistryTest`・`BlockInventoryPanelLayoutModelTest`・`BlockSlotRestrictionInputBuilderTest` が緑。末尾「## 後続レーンへの Produces 一覧」がこの計画の型と一致 |
| R-14 | 並べ比べ: 参照 `4-block-chest`・`4-block-generic`・`4-block-tank`（液体の区画が空なので差は液体の行だけ。LX に記帳しない・S4-machine の比較へ回す）・`4-block-trainError` を 1280×720・2560×1080 で撮り、差の表を `done.md` に書く | 差の表の全行が「同じ／直した／LX-106〜LX-110／他レーン持ち（区画が空）」のどれか |
| R-15 | 登録: `MainGameUiRegistration` の画面登録区画に `BlockInventoryGameUiRegistration.Register(builder);` を 1 行、`GameUiTheme.tss` の `Screens` 区画に `@import` を 1 行 | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`）が緑 |
| R-16 | 写すテスト（実物で数え直した alpha 分 23 件。下の「写すテスト」表）・`docs/uitk/disposition.md` の行・`node tools/uitk/test-name-parity.mjs --bundle 4` で alpha のファイルの未対応 0（他レーンの持ち分は「写す予定」で残ってよい） | parity の出力に alpha のファイル（`blockInventory.spec.ts`・`blockInventoryGestures.spec.ts`・`blockRegistryCoverage.spec.ts`・`BlockInventoryPanel.localization.test.ts`・`LackHighlightText/index.test.ts`）の未対応が無い |
| R-17 | 録画: `uitk/uitk-block-inventory`（新設。チェストを開く・左で掴む・右で分割・ダブルで集める・Shift で main へ・main の Shift でブロックへ・閉じる・エラー無し）と `uitk/uitk-lifeline` を全体スイッチ UITK で回す | 2 本の `result.json` が PASS、mp4、`done.md` に `UiInputExclusivity.ActiveUiSystem` の値 |
| R-18 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd を閉じる | `git log origin/feature/uitk` にこのレーンのマージコミット。`LOGS/harness/uitk/S4-alpha/done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 機械・採掘機・ポンプ・発電機・清浄室・入力の詰まりの区画、液体の行と詳細（`FluidInventoryContents`・`FluidSlotRow`。S4-machine が `Screens/BlockInventory/Fluid/` に作る）、機械のレシピ選択と大型パネルの判定の実装（`IBlockInventoryLargePanelRule` の実装は S4-machine）、機械の Shift 束縛の寄与（S4-machine）
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
- **登録の本数**: `S/Client.Starter/Registration/GameUi/` は今 8 本（`.cs`）。alpha の 1 本で 9 本。後続の S4-inventory・S4-machine・S4-train・S4-special で 10 本を超えるので、Task 1 で integrator へ再編の依頼を出す（alpha 自身は 9 本目で止まらない）
- **規約**: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下（`.meta` を除く）・partial 禁止・`Func<>` 禁止・イベントは UniRx（`Action` 禁止）・try-catch 禁止（このレーンに外部境界は無い）・日英 2 行コメント・デフォルト引数禁止・単純 getter/setter 禁止（`{ get; private set; }` と式本体の計算プロパティは可）・初期化は `Initialize`・null チェックは外部データだけ・fail-closed の経路は開発者ログ・意図して黙る行は `silent-ok: <理由> / <誰が気づくか>`・`#region Internal` はメソッド内のローカル関数だけ・`.meta` を手で作らない・Prefab/シーンを手で編集しない・`Update()` で同値判定しない（購読）
- **State は `Client.WebUiHost` を参照しない**。Web ホストにしかない規則（ブロック側の分割・集める先・結合インベントリの番地・機械レシピの一覧）は State に写し直す。重複は D-5・D-6 に記録（C4-e D-5 と同じ扱い）
- **USS**: 画面 USS の先頭に出所の 1 行（日英。`/* web: features/blockInventory/style.module.css .panel …（日本語の要約） / (English) */`）。色はトークン経由だけ。部品のクラスを画面 USS に書かない。部品のつまみの上書きは画面のルートで（このレーンでは上書き無し）
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
UIStateControl { IObservable<UIStateEnum> OnStateChanged; UIStateEnum CurrentState; void RequestTransition(UIStateEnum nextState); }
LocalPlayerInventoryController { LocalItemMoveResult MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count); void CollectItems(LocalMoveInventoryType targetType, int targetSlot); IItemStack GrabInventory; LocalPlayerInventory LocalPlayerInventory /* MainSlotCount・this[int] */; }
ClientLocalNotificationSource { void Notify(NotificationMessagePack message); }   // NotificationMessagePack.CreateOperationDenied(string messageId, Dictionary<string,string> messageParams)
IGameUnlockStateData { IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos; }  // Game.UnlockState。DI 登録済み（MainGameInteractionRegistration）
ILevelInheritanceCraftCapLookup { IObservable<(Guid familyBaseItemGuid, int level)> OnCraftCapUnlocked; }        // Game.Block.Interface.MachineUpgrade。DI 登録済み（MainGameModelRegistration）
IVanillaApiEvent { IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction); }               // Client.Network.API
IItemStackLevelLookup                                                                                                 // Core.Item.Interface。クライアント DI 未登録 → alpha が ServerContext.GetService で登録（前例 MainGameModelRegistration の IItemStackLevelUnlocker）
```

## 設計検査記録

- 配置検査: 未実施
- Phase 2.6: 未実施

## 写すテスト

Web の実物を開いて数え直した（`grep -nE "^\s*(it|test)(\.each)?\("`）。§1.4 の `SectionStackView.test`(7) は実物 8（it.each 1＋it 7）、`blockInventoryDesign`(S15) は実物 16（it.each 1＋it 15）。実物が正。区画の中身にかかる件は持ち主（machine / special / train）が同じタグで写し、alpha は外殻・順序・項目グリッドに当たる面だけを写す（同タグの NUnit が複数になってよい。W0-tools の書式）。

| Web ファイル（`W/` から） | テスト名（第 1 引数） | 写す人 | alpha の NUnit |
|---|---|---|---|
| `e2e/tests/block/blockInventory.spec.ts` | chest の block inventory パネルが描画される | alpha | `BlockInventoryPanelFlowTest` |
| 同 | filled スロット左クリックで block→grab の move_item を送り、grab オーバーレイが出る | alpha | `BlockSlotCommandsTest`（掴み絵は C4-e の `GrabOverlayModel` が掴み手の変化で出す。押下 → 移動 → 掴みの中身までを主張） |
| 同 | ブロックを閉じると panel が消える | alpha | `BlockInventoryPanelFlowTest` |
| `e2e/tests/block/blockInventoryGestures.spec.ts` | 空手の右クリックで block_inventory.split を送り grab に半分入る | alpha | `BlockSlotCommandsTest` |
| 同 | grab 保持中の右クリックで block スロットへ1個置く | alpha | `BlockSlotCommandsTest` |
| 同 | ダブルクリックで block_inventory.collect を送る | alpha | `BlockSlotCommandsTest` |
| 同 | Shift+クリックで block→main へ配分移動する | alpha | `BlockSlotCommandsTest` |
| 同 | block 開時は main の Shift+クリックが block へ配分移動する | alpha | `PlayerSlotCommandsTest` |
| `e2e/tests/block/blockRegistryCoverage.spec.ts` | v8マスタで専用UIを持つ全blockTypeがレジストリまたは意図的Genericに分類される | alpha | `BlockRegistryCoverageTest` |
| `e2e/tests/regression/sectionStack.spec.ts` | `${entry.type}は対応セクションだけを表示する` | alpha（項目グリッドの id）＋ machine・special（区画の id） | `SectionStackRegressionTest` |
| `src/features/blockInventory/views/__tests__/SectionStackView.test.tsx` | %s の既存表示を保つ | alpha（項目グリッド）＋ machine（液体の行） | `SectionStackViewTest` |
| 同 | パイプは液体だけを進捗なしで描画する | machine | — |
| 同 | 空のパイプは名前や容量を表示せず空スロットを残す | machine | — |
| 同 | 親から状態・供給・歯車・電力網・歯車網を順に描画する | alpha（区画の順を偽の区画で）＋ special（中身） | `SectionStackViewTest` |
| 同 | 部屋欄はセクション列の先頭に描く | alpha（Context 区画が項目グリッドより上）＋ machine（中身） | `SectionStackViewTest` |
| 同 | 搬入停止欄は機械欄より上に赤字で描く | machine | — |
| 同 | 抽選の中身欄は部屋欄のすぐ下に描く | machine | — |
| 同 | 照会欄の無い payload では3欄とも描かず、取得中の欄は取得状態の行だけを描く | machine | — |
| `src/features/blockInventory/blockInventoryDesign.test.ts` | %sから禁止Mantine部品とテーマ色を除去する | alpha（外殻・BlockItemGrid・rows の USS に色の直書きが無い）＋ machine・special・train（各自の USS） | `BlockInventoryDesignTest` |
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

alpha のタグ付き NUnit は 23 件（3＋5＋1＋1＋3＋6＋2＋2）。タグ無しの自前テスト（Model・実行器・口・源）は Task ごとに書く。

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
- `BlockInventory/SectionStack/SectionStackConfig.cs`・`SectionStackConfigTable.cs` — Web `configByBlockType` の写し
- `BlockInventory/SlotRestriction/IBlockSlotRestrictionContributor.cs` — 拡張口 (6)
- `BlockInventory/SlotRestriction/BlockSlotRestrictionInputBuilder.cs` — パネル状態＋寄与 → L0-s4b の入力
- `SlotCommands/PlannedItemActionRunner.cs` — 計画を TryConvert で振り分ける
- `SlotCommands/KnownItemSlots.cs` — 未知 ItemId の枠を DirectMovePlanner へ渡さない（D-11）
- `SlotCommands/Block/IBlockInventoryOperations.cs`・`LocalBlockInventoryOperations.cs`・`BlockItemActionExecutor.cs`・`IBlockSlotCommands.cs`・`BlockSlotCommands.cs`
- `SlotCommands/Player/IPlayerSlotCommands.cs`・`PlayerSlotCommands.cs`・`ShiftMoveRefusalNotifier.cs`・`PlayerSlotContextReader.cs`
- `Recipe/Source/UnlockedMachineRecipeSource.cs`・`UnlockedMachineRecipeListBuilder.cs`・`MachineRecipeLevelInheritanceConversion.cs`・`MachineRecipeSummaryConversion.cs`

Client.Ui（`Client.Ui/Screens/BlockInventory/`）
- `BlockInventoryPresenter.cs` — 起動時に 1 回載せ、出し入れと購読
- `BlockInventoryPanelView.cs` — 外殻（GamePanel・題・閉じる・エラー行・本文の器）
- `BlockInventoryPanelBand.cs` — 帯の高さと大型の幅を舞台とトークンから導く [A-47]
- `BlockInventoryNaturalScroll.cs` — 自然高のスクロール器（負マージンと内側の逃げ）
- `BlockInventoryBodyHost.cs` — 本文・題の操作・列車の本文の付け替え
- `BlockInventory.uss`
- `Registry/IBlockInventoryPart.cs`・`IBlockInventoryBodyFactory.cs`・`IBlockInventoryTitleActionFactory.cs`・`ITrainInventoryBodyFactory.cs`・`BlockInventoryBodyMount.cs`・`BlockInventoryPartRegistry.cs`
- `SectionStack/SectionStackSlot.cs`・`IBlockInventorySection.cs`・`ISectionStackSectionFactory.cs`・`SectionStackSectionMount.cs`・`SectionStackSlotRegistry.cs`・`SectionStackBody.cs`・`SectionStackBodyFactory.cs`・`SectionStackItemGridSection.cs`・`SectionStack.uss`
- `Parts/LackHighlightText.cs`・`BlockItemGrid.cs`・`BlockItemGridGroup.cs`・`BlockItemGridFactory.cs`・`Parts.uss`
- `Parts/Rows/MachineStateRow.cs`・`PerMinuteRateRow.cs`・`PowerRateText.cs`

登録（`Client.Starter/Registration/GameUi/BlockInventoryGameUiRegistration.cs`）

テスト（`Client.Tests/Ui/`）
- `State/BlockInventory/PanelModel/BlockInventoryPanelStateFactoryTest.cs`・`BlockInventoryPanelLayoutModelTest.cs`・`BlockInventoryPanelCommandsTest.cs`・`SectionStackConfigTableTest.cs`・`BlockRegistryCoverageTest.cs`・`FakePanelSource.cs`・`BlockInventoryTestPanels.cs`
- `State/SlotCommands/BlockItemActionExecutorTest.cs`・`BlockSlotCommandsTest.cs`・`PlayerSlotCommandsTest.cs`・`ShiftMoveRefusalNotifierTest.cs`・`BlockSlotRestrictionInputBuilderTest.cs`・`RecordingBlockInventoryOperations.cs`
- `State/Recipe/Source/UnlockedMachineRecipeListBuilderTest.cs`・`MachineRecipeSummaryConversionTest.cs`・`FakeGameUnlockStateData.cs`
- `Screens/BlockInventory/BlockInventoryPanelFlowTest.cs`・`BlockInventoryDesignTest.cs`・`BlockInventoryLocalizationTest.cs`・`BlockInventoryPanelErrorTest.cs`・`BlockInventoryPanelBandTest.cs`・`BlockInventoryPartRegistryTest.cs`・`BlockInventoryTitleActionTest.cs`・`BlockInventoryNaturalScrollTest.cs`・`BlockInventoryTestFixtures.cs`
- `Screens/BlockInventory/SectionStack/SectionStackViewTest.cs`・`SectionStackRegressionTest.cs`・`SectionStackSlotRegistryTest.cs`・`FakeSection.cs`
- `Screens/BlockInventory/Parts/LackHighlightTextTest.cs`・`BlockInventoryRowsTest.cs`

録画: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-block-inventory.cs`

変更（共有区画への追記だけ）:
- `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs` — 画面登録区画の末尾に 1 行
- `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` — `Screens` 区画の末尾に `@import` を 3 行（`BlockInventory.uss`・`SectionStack/SectionStack.uss`・`Parts/Parts.uss`）
- `moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs` — enum と 3 つの switch の末尾に `BlockSlotUnavailable`
- `moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef` — 参照に `Game.UnlockState` を足す
- `Localization/localization.csv` — `ui.commandRejected.blockSlotUnavailable` を 1 行（と生成物の再生成）
- `docs/uitk/disposition.md`・`docs/uitk/TODO.md`（見た目の例外を使ったときだけ）— 末尾に追記

ディレクトリのファイル数（`.cs`・`.uss`）: `Client.Ui.State/BlockInventory/Panel/` 9・`SlotCommands/` 2＋`Block/` 5＋`Player/` 4・`Recipe/Source/` 4・`Screens/BlockInventory/` 6・`Registry/` 6・`SectionStack/` 9・`Parts/` 5＋`Rows/` 3。すべて 10 以下。

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
Expected: worktree のパス、5 ファイルが全部ある、`GameUi/` の `.cs` が 8 本（他レーンが先に入っていれば多い）。足りなければ止めて指揮へ返す。`GameUi/` が 9 本以上なら Step 3 の依頼に「alpha の登録で 10 本を超える」と書き、再編が入るまで Task 8 の登録だけを保留して他を進める

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
- `Client.Starter/Registration/GameUi/` は alpha で 9 本、S4-inventory・S4-machine・S4-train・S4-special が 1 本ずつ足すと 13 本になる。`GameUi/Screens/`・`GameUi/BlockInventory/` 等への再編を W4 の取り込みの前に入れてほしい（alpha は 9 本目のまま進める）
- `SlotCommands`（`Client.Ui.State/SlotCommands/`）は持ち物・ブロック・機械・特殊族が共有する。昇格の対象ではないが、設計書 §2 の State の表に A-142 の行を足してほしい（下書きは計画末尾）
- `GamePanel` が題の操作（`SetTitleAction`）を持つとき、題に右の逃げ `calc(--panel-action-button-width + 8px)` を足す口がほしい（Web は `BlockInventoryPanel.tsx` が題の `paddingRight` で持つ。UITK では題の要素が部品の中にあり画面 USS から当てられない）。使うのは S4-special のロケットだけなので、alpha は器を渡すまでで止める

- [ ] **Step 4: コミットは無し**（記録は repo 外）

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
- Produces: `UnlockedMachineRecipeSource.Current : IReadOnlyReactiveProperty<IReadOnlyList<MachineRecipeData>>`、`UnlockedMachineRecipeListBuilder.Build(IGameUnlockStateData, ILevelInheritanceCraftCapLookup)`、`MachineRecipeSummaryConversion.ToSummaries(IReadOnlyList<MachineRecipeData>)`（S4-machine・S4-recipe・S4-research が使う）

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
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel/BlockInventoryPanelStateFactoryTest.cs`・`BlockInventoryPanelLayoutModelTest.cs`・`BlockInventoryPanelCommandsTest.cs`・`SectionStackConfigTableTest.cs`・`BlockRegistryCoverageTest.cs`

**Interfaces:**
- Consumes: `SubInventoryState`・`UIStateControl`・`OpenBlockDetailTracker`・`BlockComponentRegistry.Resolve`・`TrainSubInventorySource`・`TrainInventoryMessageType`・`InventoryRequestResult`
- Produces（後続が使う）:
```csharp
namespace Client.Ui.State.BlockInventory.Panel
public abstract class BlockInventoryPanelState { public static readonly BlockInventoryPanelState Closed; }
public sealed class ClosedBlockInventoryPanel : BlockInventoryPanelState { }
public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState
{
    public string BlockType { get; }  public Guid BlockGuid { get; }  public BlockId BlockId { get; }  public Vector3Int Position { get; }
    public IReadOnlyList<SlotItem> ItemSlots { get; }  public BlockInventoryPanelError Error { get; }  public BlockInventoryComponents Components { get; }
    public BlockGameObject Block { get; }   // 区画が詳細を読む源（Web BlockDetailDtoBuilder が読む物）。L0/L1 テストでは null
    public bool IsSamePanel(OpenBlockInventoryPanel other);   // 同じ位置・同じ blockGuid（Web の key=identifier）
}
public sealed class OpenTrainInventoryPanel : BlockInventoryPanelState
{
    public long TrainCarInstanceId { get; }  public IReadOnlyList<SlotItem> ItemSlots { get; }  public BlockInventoryPanelError Error { get; }
    public TrainSubInventorySource Source { get; }   // 列車の本文が時刻表等を読む源。テストでは null
}
public enum BlockInventoryPanelError { None, OpenFailed, ContainerMissing, TrainCarMissing }
public sealed class BlockInventoryPanelModel : IInitializable { public IReadOnlyReactiveProperty<BlockInventoryPanelState> Current { get; } }
public interface IBlockInventoryLargePanelRule { bool IsLargeMachinePanel(OpenBlockInventoryPanel panel); IObservable<Unit> OnRuleInputChanged { get; } }
public readonly struct BlockInventoryPanelLayout { public readonly bool IsLarge; public readonly bool FillsPanelHeight; }
public sealed class BlockInventoryPanelLayoutModel : IInitializable { public IReadOnlyReactiveProperty<BlockInventoryPanelLayout> Current { get; } }
public interface IBlockInventoryPanelCommands { void Close(); }
namespace Client.Ui.State.BlockInventory.SectionStack
public sealed class SectionStackConfig { string ItemGridTestId /* null 可 */; string FluidRowTestId /* null 可 */; bool RenderEmptyGrid; bool ShowFluidProgress; bool ShowFluidDetails; }
public static class SectionStackConfigTable { public static SectionStackConfig Resolve(string blockType); }
```

- [ ] **Step 1: 失敗するテストを書く**

`BlockInventoryPanelStateFactoryTest.cs`（テスト用 mod のチェストを使う。6 件）:
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
            var state = BlockInventoryPanelStateFactory.FromBlock(true, slots, _chest, new Vector3Int(1, 2, 3), null);
            var open = (OpenBlockInventoryPanel)state;
            Assert.AreEqual(BlockInventoryPanelError.None, open.Error);
            Assert.AreEqual(_chest.BlockGuid, open.BlockGuid);
            Assert.AreEqual(BlockMasterElement.BlockTypeConst.Chest, open.BlockType);
            Assert.AreEqual(BlockComponentRegistry.Resolve(open.BlockType).Body, open.Components.Body);
            Assert.AreEqual(7, open.ItemSlots[0].Count);
        }

        [Test]
        public void 取得失敗のブロックはOpenFailed()
        {
            var state = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(false, Array.Empty<SlotItem>(), _chest, Vector3Int.zero, null);
            Assert.AreEqual(BlockInventoryPanelError.OpenFailed, state.Error);
        }

        [TestCase(null, BlockInventoryPanelError.None)]
        [TestCase(TrainInventoryMessageType.ContainerMissing, BlockInventoryPanelError.ContainerMissing)]
        [TestCase(TrainInventoryMessageType.TrainCarMissing, BlockInventoryPanelError.TrainCarMissing)]
        [TestCase(TrainInventoryMessageType.OpenFailed, BlockInventoryPanelError.OpenFailed)]
        public void 列車の開け失敗をエラーへ写す(TrainInventoryMessageType? message, BlockInventoryPanelError expected)
        {
            var state = (OpenTrainInventoryPanel)BlockInventoryPanelStateFactory.FromTrain(5, Array.Empty<SlotItem>(), message, null);
            Assert.AreEqual(expected, state.Error);
            Assert.AreEqual(5, state.TrainCarInstanceId);
        }

        [Test]
        public void 同じ位置と同じブロックは同じパネル()
        {
            var a = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(true, Array.Empty<SlotItem>(), _chest, new Vector3Int(1, 0, 0), null);
            var b = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(true, new[] { new SlotItem(new ItemId(1), 1) }, _chest, new Vector3Int(1, 0, 0), null);
            var c = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(true, Array.Empty<SlotItem>(), _chest, new Vector3Int(2, 0, 0), null);
            Assert.IsTrue(a.IsSamePanel(b));
            Assert.IsFalse(a.IsSamePanel(c));
        }
    }
}
```

`BlockInventoryPanelLayoutModelTest.cs`（4 件: 閉は小・列車は大で高さ確定でない・寄与が無いブロックは小・寄与のどれかが真なら大で高さ確定、寄与の入力の変化で引き直す）:
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
            public bool IsLargeMachinePanel(OpenBlockInventoryPanel panel) => Large;
            public IObservable<Unit> OnRuleInputChanged => Changed;
        }

        [Test]
        public void 閉じていれば小で列車は大()
        {
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var model = new BlockInventoryPanelLayoutModel(new FakePanelSource(state), Array.Empty<IBlockInventoryLargePanelRule>());
            model.Initialize();
            Assert.IsFalse(model.Current.Value.IsLarge);
            state.Value = BlockInventoryPanelStateFactory.FromTrain(1, Array.Empty<SlotItem>(), null, null);
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

`BlockInventoryPanelCommandsTest.cs`（1 件: `Close` が遷移要求の口へ GameScreen を 1 回渡す。`IUiStateTransitionRequester` の偽物で受ける。D-12）

`SectionStackConfigTableTest.cs`（3 件: Chest は `chest-grid`・空でも描く／未知は `generic-block-grid`・`generic-block-fluids`／FluidPipe は項目グリッド無しで液体の詳細）

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

`BlockInventoryPanelState.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Items;
using Core.Master;
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
    }

    public sealed class ClosedBlockInventoryPanel : BlockInventoryPanelState
    {
        internal ClosedBlockInventoryPanel()
        {
        }
    }

    public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState
    {
        public string BlockType { get; }
        public Guid BlockGuid { get; }
        public BlockId BlockId { get; }
        public Vector3Int Position { get; }
        public IReadOnlyList<SlotItem> ItemSlots { get; }
        public BlockInventoryPanelError Error { get; }
        public BlockInventoryComponents Components { get; }

        // 区画が詳細を読む源（Web の BlockDetailDtoBuilder が読む物）。L0/L1 では null
        // Source the sections read details from (what the Web BlockDetailDtoBuilder reads); null in L0/L1
        public BlockGameObject Block { get; }

        internal OpenBlockInventoryPanel(string blockType, Guid blockGuid, BlockId blockId, Vector3Int position, IReadOnlyList<SlotItem> itemSlots, BlockInventoryPanelError error, BlockGameObject block)
        {
            BlockType = blockType;
            BlockGuid = blockGuid;
            BlockId = blockId;
            Position = position;
            ItemSlots = itemSlots;
            Error = error;
            Components = BlockComponentRegistry.Resolve(blockType);
            Block = block;
        }

        // 同じ位置・同じブロックなら本文を作り直さず描き直す（Web の key=identifier）
        // Same position and block keep the body and only re-render (the Web key=identifier)
        public bool IsSamePanel(OpenBlockInventoryPanel other)
        {
            return Position == other.Position && BlockGuid == other.BlockGuid;
        }
    }

    public sealed class OpenTrainInventoryPanel : BlockInventoryPanelState
    {
        public long TrainCarInstanceId { get; }
        public IReadOnlyList<SlotItem> ItemSlots { get; }
        public BlockInventoryPanelError Error { get; }

        // 列車の本文が時刻表などを読む源。L0/L1 では null
        // Source the train body reads the timetable etc. from; null in L0/L1
        public TrainSubInventorySource Source { get; }

        internal OpenTrainInventoryPanel(long trainCarInstanceId, IReadOnlyList<SlotItem> itemSlots, BlockInventoryPanelError error, TrainSubInventorySource source)
        {
            TrainCarInstanceId = trainCarInstanceId;
            ItemSlots = itemSlots;
            Error = error;
            Source = source;
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
        public static LocalizationKey ToLocalizationKey(BlockInventoryPanelError error)
        {
            return error switch
            {
                BlockInventoryPanelError.OpenFailed => LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed,
                BlockInventoryPanelError.ContainerMissing => LocalizationKeys.Ui.BlockInventory.TrainNoItemContainer,
                BlockInventoryPanelError.TrainCarMissing => LocalizationKeys.Ui.BlockInventory.TrainNotFound,
                _ => throw new ArgumentOutOfRangeException(nameof(error), error, "エラー無しに文言は無い"),
            };
        }
    }
}
```
（`LocalizationKeys` の名前空間と生成名は `UiCommandRejectReason.cs` の `using` に揃える）

`BlockInventoryPanelStateFactory.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Game.InGame.UI.UIState.State.SubInventory;
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
        public static BlockInventoryPanelState FromBlock(bool fetchSucceeded, IReadOnlyList<SlotItem> itemSlots, BlockMasterElement master, Vector3Int position, BlockGameObject block)
        {
            var error = fetchSucceeded ? BlockInventoryPanelError.None : BlockInventoryPanelError.OpenFailed;
            var blockId = MasterHolder.BlockMaster.GetBlockId(master.BlockGuid);
            return new OpenBlockInventoryPanel(master.BlockType, master.BlockGuid, blockId, position, itemSlots, error, block);
        }

        public static BlockInventoryPanelState FromTrain(long trainCarInstanceId, IReadOnlyList<SlotItem> itemSlots, TrainInventoryMessageType? lastOpenMessage, TrainSubInventorySource source)
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
            return new OpenTrainInventoryPanel(trainCarInstanceId, itemSlots, error, source);
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
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.BlockDetail;
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
        private readonly UIStateControl _uiStateControl;
        private readonly SubInventoryState _subInventoryState;
        private readonly OpenBlockDetailTracker _tracker;
        private readonly ReactiveProperty<BlockInventoryPanelState> _current = new(BlockInventoryPanelState.Closed);

        public IReadOnlyReactiveProperty<BlockInventoryPanelState> Current => _current;

        public BlockInventoryPanelModel(UIStateControl uiStateControl, SubInventoryState subInventoryState, OpenBlockDetailTracker tracker)
        {
            _uiStateControl = uiStateControl;
            _subInventoryState = subInventoryState;
            _tracker = tracker;
        }

        public void Initialize()
        {
            // 遷移・スロット更新・追跡替え・追跡ブロックの状態変化で組み直す（ホストと同じ契機。連続値は各区画が自分で購読する）
            // Rebuild on transitions, slot updates, tracking changes and tracked-block state changes (host triggers; continuous values are each section's own subscription)
            _uiStateControl.OnStateChanged.Subscribe(_ => Rebuild());
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
                // SubInventory かつサブインベントリがあるときだけ開く
                // Open only in SubInventory with a sub-inventory present
                var sub = _subInventoryState.CurrentSubInventory;
                if (_uiStateControl.CurrentState != UIStateEnum.SubInventory || sub == null) return BlockInventoryPanelState.Closed;
                var slots = BlockInventoryPanelStateFactory.ToSlotItems(sub.SubInventory);
                if (_subInventoryState.CurrentSubInventorySource is TrainSubInventorySource train) return BlockInventoryPanelStateFactory.FromTrain(train.TrainCarInstanceId, slots, train.LastOpenMessage, train);

                // silent-ok: 追跡ブロックが未解決（撤去直後など）は閉と同じ見え方（ホストと同じ） / 追跡替えの購読で直ちに組み直す
                var block = _tracker.TrackedBlock;
                if (block == null) return BlockInventoryPanelState.Closed;
                var succeeded = sub.Result == InventoryRequestResult.Success;
                return BlockInventoryPanelStateFactory.FromBlock(succeeded, slots, block.BlockMasterElement, _tracker.TrackedSource.BlockPosition, block);
            }

            #endregion
        }
    }
}
```
（`sub.SubInventory` は `List<IItemStack>` で `IReadOnlyList<IItemStack>` として渡せる。`UIStateEnum`・`InventoryRequestResult` の名前空間は実物で確かめる。同じ値の再代入は `ReactiveProperty` が流さないので、状態は毎回新しいインスタンスにして流す〈`SetValueAndForceNotify` は使わない。新インスタンスは参照が違うので必ず流れる〉）

`IBlockInventoryLargePanelRule.cs`・`BlockInventoryPanelLayout.cs`・`BlockInventoryPanelLayoutModel.cs`:
```csharp
using System;
using UniRx;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// 大型パネル判定の寄与の口（Web isLargeMachinePanel。実装は S4-machine が UnlockedMachineRecipeSource で書く）
    /// Contribution port for the large-panel decision (Web isLargeMachinePanel; S4-machine implements it with UnlockedMachineRecipeSource)
    /// </summary>
    public interface IBlockInventoryLargePanelRule
    {
        // 開いているブロックが「レシピ有り機械の大型パネル」か
        // Whether the open block is a recipe-capable machine's large panel
        bool IsLargeMachinePanel(OpenBlockInventoryPanel panel);

        // 判定の入力（レシピの解放・選択など）が変わったら流す
        // Fires when the decision's inputs (recipe unlocks, selection, ...) change
        IObservable<Unit> OnRuleInputChanged { get; }
    }
}
```
```csharp
namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// パネルの大きさ（Web の isLargePanel と isLargeMachinePanel＝本文の高さ確定）
    /// Panel size (Web isLargePanel, and isLargeMachinePanel as the body's fixed height)
    /// </summary>
    public readonly struct BlockInventoryPanelLayout
    {
        public static readonly BlockInventoryPanelLayout Natural = new(false, false);

        public readonly bool IsLarge;
        public readonly bool FillsPanelHeight;

        public BlockInventoryPanelLayout(bool isLarge, bool fillsPanelHeight)
        {
            IsLarge = isLarge;
            FillsPanelHeight = fillsPanelHeight;
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

        public IReadOnlyReactiveProperty<BlockInventoryPanelLayout> Current => _current;

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
                OpenTrainInventoryPanel => new BlockInventoryPanelLayout(true, false),
                OpenBlockInventoryPanel block => AnyLarge(block) ? new BlockInventoryPanelLayout(true, true) : BlockInventoryPanelLayout.Natural,
                _ => BlockInventoryPanelLayout.Natural,
            };

            #region Internal

            bool AnyLarge(OpenBlockInventoryPanel block)
            {
                foreach (var rule in _rules)
                {
                    if (rule.IsLargeMachinePanel(block)) return true;
                }
                return false;
            }

            #endregion
        }
    }
}
```
（構造体は既定の等値なので同じ大きさの再代入は流れない。View は大きさの変化だけで寸法を当て直せばよい）

`IBlockInventoryPanelCommands.cs`・`BlockInventoryPanelCommands.cs`（D-12: 遷移要求は `UIStateControl.RequestTransition` を薄い口 `IUiStateTransitionRequester` 越しに呼ぶ。口と本番実装 `UiStateControlTransitionRequester` は同じ `BlockInventoryPanelCommands.cs` に置く）:
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

namespace Client.Ui.State.BlockInventory.Panel
{
    // UI の遷移要求の口（テストで偽物に替える）
    // Port for UI transition requests (replaced by a fake in tests)
    public interface IUiStateTransitionRequester
    {
        void RequestTransition(UIStateEnum nextState);
    }

    public sealed class UiStateControlTransitionRequester : IUiStateTransitionRequester
    {
        private readonly UIStateControl _uiStateControl;

        public UiStateControlTransitionRequester(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }

        public void RequestTransition(UIStateEnum nextState)
        {
            _uiStateControl.RequestTransition(nextState);
        }
    }

    public sealed class BlockInventoryPanelCommands : IBlockInventoryPanelCommands
    {
        private readonly IUiStateTransitionRequester _requester;

        public BlockInventoryPanelCommands(IUiStateTransitionRequester requester)
        {
            _requester = requester;
        }

        // SubInventory からの GameScreen 要求はホストの許可表でも通る遷移
        // GameScreen from SubInventory is an allowed transition in the host's table too
        public void Close()
        {
            _requester.RequestTransition(UIStateEnum.GameScreen);
        }
    }
}
```

`SectionStackConfig.cs`・`SectionStackConfigTable.cs`（Web `SectionStackView.tsx` の `configByBlockType` と `genericConfig` を 1 行ずつ写す。キーは `BlockMasterElement.BlockTypeConst.*`）:
```csharp
namespace Client.Ui.State.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の blockType ごとの設定（Web SectionStackViewConfig の写し。testid が null の区画は出さない）
    /// Per-blockType standard-body settings (port of Web SectionStackViewConfig; a null testid hides that section)
    /// </summary>
    public sealed class SectionStackConfig
    {
        public string ItemGridTestId { get; }
        public string FluidRowTestId { get; }
        public bool RenderEmptyGrid { get; }
        public bool ShowFluidProgress { get; }
        public bool ShowFluidDetails { get; }

        public SectionStackConfig(string itemGridTestId, string fluidRowTestId, bool renderEmptyGrid, bool showFluidProgress, bool showFluidDetails)
        {
            ItemGridTestId = itemGridTestId;
            FluidRowTestId = fluidRowTestId;
            RenderEmptyGrid = renderEmptyGrid;
            ShowFluidProgress = showFluidProgress;
            ShowFluidDetails = showFluidDetails;
        }

        public bool HasItemGrid => ItemGridTestId != null;
        public bool HasFluidRow => FluidRowTestId != null;
    }
}
```
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
        public static readonly SectionStackConfig Generic = new("generic-block-grid", "generic-block-fluids", false, true, false);

        private static readonly Dictionary<string, SectionStackConfig> ByBlockType = new()
        {
            [T.FluidPipe] = new SectionStackConfig(null, "fluid-pipe-fluid-slots", false, false, true),
            [T.Chest] = new SectionStackConfig("chest-grid", null, true, true, false),
            [T.ElectricMachine] = new SectionStackConfig(null, "machine-fluid-slots", false, false, false),
            [T.GearMachine] = new SectionStackConfig(null, "machine-fluid-slots", false, false, false),
            [T.ElectricGenerator] = new SectionStackConfig("generator-fuel-grid", "generator-fluid-slots", false, false, false),
            [T.FuelGearGenerator] = new SectionStackConfig("generator-fuel-grid", null, true, true, false),
            [T.SimpleGearGenerator] = new SectionStackConfig("generator-fuel-grid", null, true, true, false),
            [T.GearToElectricGenerator] = new SectionStackConfig(null, null, false, false, false),
            [T.ElectricMiner] = new SectionStackConfig("miner-output-grid", null, true, true, false),
            [T.GearMiner] = new SectionStackConfig("gear-miner-output-grid", null, true, true, false),
            [T.ElectricPump] = new SectionStackConfig(null, "pump-fluid-slots", false, false, false),
            [T.GearPump] = new SectionStackConfig(null, "pump-fluid-slots", false, false, false),
            [T.CleanRoomMachine] = new SectionStackConfig(null, "machine-fluid-slots", false, false, false),
            [T.CleanRoomAirFilter] = new SectionStackConfig("clean-room-filter-grid", null, true, false, false),
        };

        public static SectionStackConfig Resolve(string blockType)
        {
            return ByBlockType.TryGetValue(blockType, out var config) ? config : Generic;
        }
    }
}
```
（`BlockTypeConst` に無い名前があればコンパイルで分かるので、Web の文字列と同じ定数に読み替える。無ければ文字列で書き、起草メモの照合に足す）

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.BlockInventory"`
Expected: `ErrorCount: 0`、新しい件と L0-s4b の既存件が PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel
git commit -m "feat(uitk): ブロックインベントリのパネルの状態・大きさの判定・閉じるを State に足す（S4-alpha）

<実行ホストの指示する帰属行>"
```

---
### Task 4: ブロック側の実行器とブロック側のスロット操作（`Client.Ui.State/SlotCommands/`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/IBlockInventoryOperations.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/LocalBlockInventoryOperations.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/BlockItemActionExecutor.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/IBlockSlotCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/BlockSlotCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/PlannedItemActionRunner.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/KnownItemSlots.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs`（enum と `IsBenign`・`ToLocalizationKey`・`ToWireCode` の末尾に `BlockSlotUnavailable`）
- Modify: `Localization/localization.csv`（`ui.commandRejected.blockSlotUnavailable`）＋ `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` の再生成と `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の印
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/SlotCommands/RecordingBlockInventoryOperations.cs`・`BlockItemActionExecutorTest.cs`・`BlockSlotCommandsTest.cs`

**Interfaces:**
- Consumes: `LocalPlayerInventoryController.MoveItem/CollectItems/GrabInventory/LocalPlayerInventory.MainSlotCount`、`SubInventoryState.CurrentSubInventory/CurrentSubInventorySource`、C4-e の `IPlayerInventoryOperations`・`PlayerItemActionExecutor`・`PlayerItemActionConversion.TryConvert`、L0-s4a の `BlockSlotPlanner`・`PlannedItemAction`、`IItemStackLevelLookup`
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
    bool TryReadBlockSlots(out IReadOnlyList<SlotItem> slots);      // サブインベントリが開いていなければ false
    PlayerItemMoveOutcome Move(ItemMoveSlotRef from, ItemMoveSlotRef to, int count);  // 片側が Block の移動
    void CollectToGrab();
    void CollectToBlockSlot(int blockSlot);
}
public sealed class BlockItemActionExecutor { public UiCommandResult Execute(PlannedItemAction action); }  // Block* の計画だけ
namespace Client.Ui.State.SlotCommands
public sealed class PlannedItemActionRunner { public UiCommandResult Execute(IReadOnlyList<PlannedItemAction> actions); } // TryConvert で振り分け、最初の拒否で止める
public const string BlockSlotCommandName = "block_inventory.slot";   // BlockSlotCommands.CommandName。トーストの名前
```

- [ ] **Step 1: 拒否の理由を足す**

`UiCommandRejectReason` の enum 末尾に `BlockSlotUnavailable,`、`IsBenign` に `UiCommandRejectReason.BlockSlotUnavailable => true,`（操作の瞬間にパネルが閉じた・枠が減った古いクリック。プレイヤーには閉じたのが見えている）、`ToLocalizationKey` に `LocalizationKeys.Ui.CommandRejected.BlockSlotUnavailable`、`ToWireCode` に `"invalid_slot"`（Web ホストの `BlockAreaSlotParser` の失敗と同じ）。`localization.csv` の `ui.commandRejected.*` の末尾に 1 行（英「The block slot is no longer available」・日「ブロックの枠がもうありません」。列の並びは既存の `ui.commandRejected.splitDragNoValidSlots` 行に揃える）。続けて `pnpm --dir moorestech_web/webui run gen:i18n` で `localizationKeys.ts` を作り直し、Editor の SchemaWatcher が書き換えた `_CompileRequester.cs` と一緒にこの Task のコミットへ入れる（台帳「## 追従記録」へ `写した` を 1 行。Task 9）

- [ ] **Step 2: 失敗するテストを書く**

`RecordingBlockInventoryOperations.cs`（ブロックの枠を持ち、移動と集めを記録する。移動は記録だけで在庫は動かさない）:
```csharp
using System.Collections.Generic;
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

        public bool TryReadBlockSlots(out IReadOnlyList<SlotItem> slots)
        {
            slots = Slots;
            return Open;
        }

        public PlayerItemMoveOutcome Move(ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            Moves.Add((from, to, count));
            return NextOutcome;
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

`BlockItemActionExecutorTest.cs`（5 件: 分割は掴み中なら `SplitGrabNotEmpty`／空の枠は `ItemSourceSlotEmpty`／1 個は何も動かさず成功／移動の `SourceEmpty`・`SourceCountShort` を A-94 の理由へ／範囲外の枠は `BlockSlotUnavailable`）

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
}
```

`LocalBlockInventoryOperations.cs`（Web ホスト `BlockInventoryActions.cs` の `BlockAreaSlotParser`・`BlockMoveItemActionHandler`・`BlockCollectActionHandler` の番地の写し。D-5）:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.BlockInventory.Panel;
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
        private readonly SubInventoryState _subInventoryState;

        public LocalBlockInventoryOperations(LocalPlayerInventoryController controller, SubInventoryState subInventoryState)
        {
            _controller = controller;
            _subInventoryState = subInventoryState;
        }

        public bool TryReadBlockSlots(out IReadOnlyList<SlotItem> slots)
        {
            // ブロックと列車の発生元だけを開いているとみなす（ホストと同じ。未知の発生元は閉）
            // Only block and train sources count as open (same as the host; unknown sources are closed)
            var source = _subInventoryState.CurrentSubInventorySource;
            var sub = _subInventoryState.CurrentSubInventory;
            if (sub == null || (source is not BlockSubInventorySource && source is not TrainSubInventorySource))
            {
                slots = Array.Empty<SlotItem>();
                return false;
            }
            slots = BlockInventoryPanelStateFactory.ToSlotItems(sub.SubInventory);
            return true;
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

`BlockItemActionExecutor.cs`:
```csharp
using System;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.ItemMove;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// TryConvert が false の計画（Block* の 3 種）を実行する。規則は Web ホスト BlockInventoryActions と同じ [A-93]
    /// Runs the plans TryConvert rejects (the three Block* kinds) under the Web host BlockInventoryActions rules [A-93]
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

        public UiCommandResult Execute(PlannedItemAction action)
        {
            // 閉じた・枠が減った後の古い計画は良性の拒否（ホストの invalid_slot）
            // A stale plan after closing or shrinking is a benign rejection (the host's invalid_slot)
            if (!_block.TryReadBlockSlots(out var slots)) return Unavailable("block inventory is not open");
            if (!InRange(action.From) || !InRange(action.To)) return Unavailable($"block slot out of range: {action}");

            return action.Kind switch
            {
                PlannedItemActionKind.BlockInventoryMoveItem => ToResult(_block.Move(action.From, action.To, action.Count)),
                PlannedItemActionKind.BlockInventorySplit => Split(),
                PlannedItemActionKind.BlockInventoryCollect => Collect(),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "プレイヤー側の計画は PlayerItemActionExecutor へ渡す"),
            };

            #region Internal

            bool InRange(ItemMoveSlotRef slot)
            {
                return slot.Area != ItemMoveArea.Block || (0 <= slot.Slot && slot.Slot < slots.Count);
            }

            UiCommandResult Split()
            {
                // 半分の計算はホストの SplitGrabActionHandler と同じ床関数。1 個以下は成功扱いの何もしない
                // Half uses the same floor as the host; a stack of one or less is a successful no-op
                if (!_player.ReadGrab().IsEmpty) return new UiCommandRejected(UiCommandRejectReason.SplitGrabNotEmpty, "grab is not empty");
                var slot = slots[action.From.Slot];
                if (slot.IsEmpty) return new UiCommandRejected(UiCommandRejectReason.ItemSourceSlotEmpty, $"block slot {action.From.Slot} is empty");
                var half = slot.Count / 2;
                // silent-ok: 1 個の枠の右押下は Web と同じく何もしない / 枠に 1 個残っているのがプレイヤーに見えている
                if (half == 0) return UiCommandAccepted.Instance;
                return ToResult(_block.Move(action.From, ItemMoveSlotRef.Grab, half));
            }

            UiCommandResult Collect()
            {
                // 集める先は操作の瞬間の持ち手で決める（ホストの ResolveCollectTarget と同じ）
                // The target follows the grab at gesture time (same as the host ResolveCollectTarget)
                if (_player.ReadGrab().IsEmpty) _block.CollectToBlockSlot(action.From.Slot);
                else _block.CollectToGrab();
                return UiCommandAccepted.Instance;
            }

            #endregion
        }

        private static UiCommandResult Unavailable(string detail)
        {
            Debug.LogWarning($"[BlockItemActionExecutor] {detail}");
            return new UiCommandRejected(UiCommandRejectReason.BlockSlotUnavailable, detail);
        }

        // 移動の結果を A-94 の理由へ写す（PlayerItemAction と同じ対応）
        // Map move outcomes to the A-94 reasons (same mapping as PlayerItemAction)
        private static UiCommandResult ToResult(PlayerItemMoveOutcome outcome)
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

namespace Client.Ui.State.SlotCommands
{
    /// <summary>
    /// 計画の列を順に実行する。TryConvert が通ればプレイヤー側、通らなければブロック側。最初の拒否で止める
    /// Runs a plan list in order: player side when TryConvert succeeds, block side otherwise; stops at the first rejection
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
                var result = PlayerItemActionConversion.TryConvert(action, out var playerAction) ? _player.Execute(new[] { playerAction }) : _block.Execute(action);
                if (result is UiCommandRejected) return result;
            }
            return UiCommandAccepted.Instance;
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
            if (!_block.TryReadBlockSlots(out var slots) || blockSlot < 0 || slots.Count <= blockSlot) return false;
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

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.(SlotCommands|Inventory|ItemMove|Commands)"`
Expected: `ErrorCount: 0`、新しい件と C4-e・L0-s4a の既存件（`UiCommandRejectReason` の網羅テストを含む）が PASS。網羅テストが `benign` の配列を持つなら `BlockSlotUnavailable` を足す

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

            // 宛先が満杯は Web に無い断り。通知の文言は持たず、ログだけで開発者に知らせる（D-9）
            // Destination-full has no Web counterpart; no notification text, the log alone tells developers (D-9)
            if (reason == ShiftMoveRefusalReason.DestinationFull) return;
            _notifications.Notify(NotificationMessagePack.CreateOperationDenied(ToMessageId(reason), new Dictionary<string, string>()));
        }

        private static string ToMessageId(ShiftMoveRefusalReason reason)
        {
            return reason switch
            {
                ShiftMoveRefusalReason.NoAcceptingBlockSlot => "denied.inventoryMoveSlotRejected",
                ShiftMoveRefusalReason.InventoryOpenFailed => "denied.inventoryOpenFailed",
                ShiftMoveRefusalReason.DetailLoading => "denied.inventoryDetailLoading",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
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

`BlockInventoryRowsTest.cs`（4 件: `PowerRateText` は率を四捨五入の百分率で出し、強調が許されて率 < 1 なら不足／強調が許されなければ不足にしない／`MachineStateRow` は待機中に電力率を隠す〈`BlockDetailStateDisplay.Machine(Idle).ShowPowerRate` に従う〉／`PerMinuteRateRow` は小数 1 桁）

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

`Parts.uss`（先頭に出所の 1 行）:
```css
/* web: features/blockInventory/details/LackHighlightText/style.module.css .text（本文色）と [data-insufficient="true"]（警告色）、BlockItemGrid.tsx の BlockItemGridGroup（Stack gap 4・ラベル --text-muted）を写す / ports .text (body colour) and [data-insufficient="true"] (warning colour) from LackHighlightText, and BlockItemGridGroup (Stack gap 4, label --text-muted) from BlockItemGrid.tsx */
.lack-highlight-text {
    color: var(--text-default);
}

.lack-highlight-text--insufficient {
    color: var(--text-insufficient);
}

.per-minute-rate-row__text {
    color: var(--text-default);
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
        private const string InventoryColumnsProperty = "--inventory-columns";

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

            // 列数の上限は持ち物と同じトークン（Web INVENTORY_GRID_COLUMNS）。C# に数を持たない
            // The column cap is the inventory's own token (Web INVENTORY_GRID_COLUMNS); no number lives in C#
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
            // トークンが未解決の間は列数を決めない（解決時に CustomStyleResolvedEvent で当て直す）
            // Hold the column count until the token resolves (re-applied on CustomStyleResolvedEvent)
            if (_inventoryColumns <= 0) return;
            _grid.SetColumns(System.Math.Min(_inventoryColumns, System.Math.Max(1, _slots.Count)));
        }
    }
}
```
（`UiCommandResultToaster` の名前空間は実物〈`Client.Ui.State.Toast` か `Client.Ui.Foundation.Toast`〉で確かめる。`--inventory-columns` がテストのパネルで解決されないなら `UiTestHost` のテーマ読み込みで解決されることを Step 4 で確かめ、されなければ Task の `done.md`「未確認」に書いて `ApplyColumns` の待ちを `Render` 側の既定 10 に替えず、テーマの読み込みを直す依頼を integrator へ出す）

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
            _parent.SetItemShown(_powerRate, display.ShowPowerRate);
            if (display.ShowPowerRate) _powerRate.Render(currentPower, requestPower, highlightPowerShortage);
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
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackSlot.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/IBlockInventorySection.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/ISectionStackSectionFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackSectionMount.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackSlotRegistry.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackBody.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackBodyFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackItemGridSection.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStack.uss`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/SectionStack/FakeSection.cs`・`SectionStackViewTest.cs`・`SectionStackRegressionTest.cs`・`SectionStackSlotRegistryTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/BlockInventoryPartRegistryTest.cs`

**Interfaces:**
- Consumes: Task 3 の `OpenBlockInventoryPanel`・`OpenTrainInventoryPanel`・`SectionStackConfigTable`、Task 6 の `BlockItemGridFactory`、L0-s4b の `BlockInventoryBodyKind`・`BlockInventoryTitleActionKind`・`BlockDetailMath.AllSlotIndices`
- Produces: 末尾「## 後続レーンへの Produces 一覧」の (1)〜(4) と同じ型（本文・題の操作・区画・列車の本文）

- [ ] **Step 1: 失敗するテストを書く**

`FakeSection.cs`（区画の偽物: 固定の testid の `VisualElement` を持ち、`IsApplicable` の真偽と `Render` の回数を記録する。偽の工場 `FakeSectionFactory(SectionStackSlot slot, string testid, bool applicable)` も同じファイル）

`SectionStackViewTest.cs`（alpha 分 3 件。項目グリッドは本物の `BlockItemGridFactory`〈コマンドは記録係〉、他の区画は偽物）:
```csharp
using System.Linq;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.Items;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文は Web SectionStackView の区画の順と項目グリッドの出し分けを保つ（L1）
    /// The standard body keeps the Web SectionStackView section order and item-grid visibility (L1)
    /// </summary>
    public class SectionStackViewTest : ScreenTestHost
    {
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
        public void 項目グリッドは設定どおり空でも出すか中身があるときだけ出す(string blockType, string gridTestId, bool renderEmptyGrid)
        {
            var body = BlockInventoryTestBodies.SectionStack(blockType);
            Mount((VisualElement)body.Root);
            body.Render(BlockInventoryTestPanels.OfType(blockType));
            simulate.FrameUpdate();
            Assert.AreEqual(renderEmptyGrid ? new[] { gridTestId } : new string[0], ShownGridIds(body.Root));

            body.Render(BlockInventoryTestPanels.OfType(blockType, new SlotItem(new Core.Master.ItemId(1), 1)));
            simulate.FrameUpdate();
            Assert.AreEqual(gridTestId == null ? new string[0] : new[] { gridTestId }, ShownGridIds(body.Root));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">親から状態・供給・歯車・電力網・歯車網を順に描画する</web-test>
        [Test]
        public void 区画は固定の順に並ぶ()
        {
            var body = BlockInventoryTestBodies.SectionStackWithFakes("GearToElectricGenerator", FakeSectionFactory.AllSlotsApplicable());
            Mount((VisualElement)body.Root);
            body.Render(BlockInventoryTestPanels.OfType("GearToElectricGenerator"));
            simulate.FrameUpdate();
            var order = body.Root.Query(className: FakeSection.ClassName).ToList().Where(e => e.resolvedStyle.display == DisplayStyle.Flex).Select(e => e.name).ToArray();
            CollectionAssert.AreEqual(new[] { "fake-Context", "fake-Machine", "fake-Miner", "fake-Pump", "fake-Generator", "fake-GearToElectric", "fake-BeltThroughput", "fake-Gear", "fake-ElectricNetwork", "fake-GearNetwork", "fake-FluidDetails", "fake-FluidRow" }, order);
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">部屋欄はセクション列の先頭に描く</web-test>
        [Test]
        public void 文脈の区画は項目グリッドより上に来る()
        {
            var body = BlockInventoryTestBodies.SectionStackWithFakes("CleanRoomAirFilter", FakeSectionFactory.AllSlotsApplicable());
            Mount((VisualElement)body.Root);
            body.Render(BlockInventoryTestPanels.OfType("CleanRoomAirFilter", new SlotItem(new Core.Master.ItemId(12), 5)));
            simulate.FrameUpdate();
            var children = body.Root.Children().Where(e => e.resolvedStyle.display == DisplayStyle.Flex).Select(e => e.name).ToArray();
            Assert.AreEqual("fake-Context", children[0]);
            Assert.AreEqual("clean-room-filter-grid", children[1].Replace("section-stack-item-grid:", ""));
        }

        private static string[] ShownGridIds(VisualElement root)
        {
            return root.Query<Client.Ui.Components.Slots.SlotGrid>().ToList().Where(g => g.parent.resolvedStyle.display == DisplayStyle.Flex).Select(g => g.name).ToArray();
        }
    }
}
```
（`BlockInventoryTestBodies`〈`SectionStack(blockType)`・`SectionStackWithFakes(blockType, factories)`〉は `Client.Tests/Ui/Screens/BlockInventory/BlockInventoryTestFixtures.cs` に置き、`BlockInventoryTestPanels.OfType(blockType, params SlotItem[])` は Task 3 の `BlockInventoryTestPanels.cs` に足す。マスタの無い blockType〈"unknown"〉も組めるよう、`OfType` は `BlockInventoryPanelStateFactory` を通さず `OpenBlockInventoryPanel` の internal ctor を呼ぶ〈`Client.Ui.State/AssemblyInfo.cs` に `[assembly: InternalsVisibleTo("Client.Tests")]` が既にあることを起草時に確かめた〉。項目グリッドの区画の要素名は `section-stack-item-grid` にし、中の `SlotGrid` の name が Web の testid になる。テストの 2 件目の比較は区画の要素名ではなく中の `SlotGrid.name` で見る形に揃えてよい）

`SectionStackRegressionTest.cs`（alpha 分 1 件。Web の 12 ケースのうち項目グリッドの id だけを主張する。区画の id は持ち主のレーンが同じタグで足す）:
```csharp
        /// <web-test file="e2e/tests/regression/sectionStack.spec.ts">${entry.type}は対応セクションだけを表示する</web-test>
        [TestCase("Chest", "chest-grid")]
        [TestCase("GearMachine", null)]
        [TestCase("GearMiner", "gear-miner-output-grid")]
        [TestCase("ElectricGenerator", "generator-fuel-grid")]
        [TestCase("FuelGearGenerator", "generator-fuel-grid")]
        [TestCase("FilterSplitter", null)]
        [TestCase("unknown", "generic-block-grid")]
        public void 項目グリッドは対応するblockTypeだけに出る(string blockType, string gridTestId)
```
（Web の `tank` は液体だけで項目グリッド無し、`gearGenerator` は空の燃料グリッドが残る〈`renderedEmptyByType`〉。中身ありの枠 1 つで `Render` し、`gridTestId` が null なら項目グリッドが 1 つも出ないことを見る。FilterSplitter は専用本文なので `BlockInventoryPartRegistry` 経由で本文を作り、登録の無いいまは警告ログと標準の本文への逃げになる〈`LogAssert.Expect(LogType.Warning, …)`〉。S4-special が本文を足したらこのケースは special のテストへ移す）

`SectionStackSlotRegistryTest.cs`（3 件: 同じ Slot の二重登録は例外／`ItemGrid` の外部登録は例外／空きの Slot は起動時に 1 回だけ情報ログ）

`BlockInventoryPartRegistryTest.cs`（4 件: `SectionStack` の Kind は標準の本文／登録のある Kind はその工場／登録の無い Kind は警告ログ 1 回と標準の本文〈2 回目は出さない〉／同じ Kind の二重登録と `SectionStack` の外部登録は例外）

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
    /// パネルへ差し込む部分（本文・題の操作・区画）。同じパネルのあいだは Render を繰り返し、外すとき Detach を 1 回呼ぶ
    /// A part plugged into the panel (body, title action, section); Render repeats while the same panel stays, Detach runs once on removal
    /// </summary>
    public interface IBlockInventoryPart
    {
        VisualElement Root { get; }
        void Render(OpenBlockInventoryPanel panel);
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

    // 列車の本文。同じ車両のあいだは Render を繰り返し、外すとき Detach を 1 回呼ぶ
    // Train body; Render repeats while the same car stays, Detach runs once on removal
    public interface ITrainInventoryPart
    {
        VisualElement Root { get; }
        void Render(OpenTrainInventoryPanel panel);
        void Detach();
    }
}
```
```csharp
namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 本文を作るときの条件（Web の fillsPanelHeight。機械の大型パネルだけ真）
    /// Conditions when creating a body (Web fillsPanelHeight; true only on a machine's large panel)
    /// </summary>
    public sealed class BlockInventoryBodyMount
    {
        public bool FillsPanelHeight { get; }

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
        private readonly ITrainInventoryBodyFactory _trainBody;
        private readonly SectionStackBodyFactory _sectionStack;
        private readonly HashSet<string> _warned = new();

        public BlockInventoryPartRegistry(IReadOnlyList<IBlockInventoryBodyFactory> bodies, IReadOnlyList<IBlockInventoryTitleActionFactory> titleActions, IReadOnlyList<ITrainInventoryBodyFactory> trainBodies, SectionStackBodyFactory sectionStack)
        {
            // 二重登録と既定の本文の上書きは起動時に止める（どのレーンの登録が衝突したかを例外の文で示す）
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
            if (kind != BlockInventoryBodyKind.SectionStack && _bodies.TryGetValue(kind, out var factory)) return factory.Create(mount);
            if (kind != BlockInventoryBodyKind.SectionStack) WarnOnce($"body:{kind}", $"[BlockInventory] 本文 {kind} の登録が無いので標準の本文で描く（blockType={panel.BlockType}）");
            return _sectionStack.Create(panel.BlockType, mount);
        }

        public bool TryCreateTitleAction(BlockInventoryTitleActionKind kind, out IBlockInventoryPart part)
        {
            part = null;
            // silent-ok: 題の操作を持たない blockType が大半（Web も描かない） / 登録漏れは下の警告で出る
            if (kind == BlockInventoryTitleActionKind.None) return false;
            if (_titleActions.TryGetValue(kind, out var factory)) part = factory.Create();
            else WarnOnce($"title:{kind}", $"[BlockInventory] 題の操作 {kind} の登録が無いので出さない");
            return part != null;
        }

        public bool TryCreateTrainBody(out ITrainInventoryPart part)
        {
            part = _trainBody?.Create();
            if (part == null) WarnOnce("train", "[BlockInventory] 列車の本文の登録が無いので題とエラー行だけを出す");
            return part != null;
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
        FluidDetails,     // S4-machine（Fluid/。showFluidDetails && fluidRowTestId）
        FluidRow,         // S4-machine（Fluid/。fluidRowTestId && !machine && !showFluidDetails）
    }
}
```

`IBlockInventorySection.cs`・`ISectionStackSectionFactory.cs`・`SectionStackSectionMount.cs`:
```csharp
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の 1 区画。描くかどうかは区画が持つ（Web の各 Section が null を返す条件）
    /// One section of the standard body; the section owns whether it draws (the condition each Web Section returns null on)
    /// </summary>
    public interface IBlockInventorySection : IBlockInventoryPart
    {
        bool IsApplicable(OpenBlockInventoryPanel panel);
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
        public SectionStackConfig Config { get; }
        public bool FillsPanelHeight { get; }

        public SectionStackSectionMount(SectionStackConfig config, bool fillsPanelHeight)
        {
            Config = config;
            FillsPanelHeight = fillsPanelHeight;
        }
    }
}
```
（名前空間 `Client.Ui.State.BlockInventory.SectionStack` と `Client.Ui.Screens.BlockInventory.SectionStack` は別アセンブリで衝突しない。読みにくければ State 側を `using SectionStackConfig = …` で別名にする）

`SectionStackSlotRegistry.cs`（ctor で `IReadOnlyList<ISectionStackSectionFactory>` を Slot で索引し、二重・`ItemGrid` は例外、空きの Slot を 1 回 `Debug.Log` で並べる。`IBlockInventorySection CreateOrNull(SectionStackSlot slot, SectionStackSectionMount mount)` は空きなら null〈`silent-ok: 担当レーンが未マージの区画は Web の null と同じ見え方 / 起動時の情報ログに空きが並ぶ`〉）

`SectionStackItemGridSection.cs`:
```csharp
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SectionStack;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の項目グリッド（Web の showItemGrid。testid は blockType の設定から）
    /// The standard body's item grid (Web showItemGrid; the testid comes from the blockType settings)
    /// </summary>
    public sealed class SectionStackItemGridSection : IBlockInventorySection
    {
        private readonly SectionStackConfig _config;
        private readonly BlockItemGrid _grid;
        private readonly VisualElement _root = new() { name = "section-stack-item-grid" };

        public VisualElement Root => _root;

        public SectionStackItemGridSection(SectionStackConfig config, BlockItemGridFactory grids)
        {
            _config = config;
            // 項目グリッドの無い設定でも器は作り、IsApplicable が常に偽になる
            // Build the holder even for settings without a grid; IsApplicable then stays false
            _grid = grids.Create(config.HasItemGrid ? config.ItemGridTestId : "section-stack-no-item-grid");
            _root.Add(_grid);
        }

        public bool IsApplicable(OpenBlockInventoryPanel panel)
        {
            return _config.HasItemGrid && (_config.RenderEmptyGrid || 0 < panel.ItemSlots.Count);
        }

        public void Render(OpenBlockInventoryPanel panel)
        {
            _grid.Render(panel.ItemSlots, BlockDetailMath.AllSlotIndices(panel.ItemSlots.Count));
        }

        public void Detach()
        {
        }
    }
}
```
（`AllSlotIndices(int)` は `int[]` を返す〈実物で確かめた〉。`Detach` が空なのは購読を持たないから。グリッドの購読は要素と一緒に捨てられる）

`SectionStackBody.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SectionStack;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文。固定順の区画を UiStack(Sm) に並べ、描くかどうかは各区画に聞く（Web SectionStackView の写し）
    /// The standard body: fixed-order sections in a UiStack(Sm), each section deciding whether it draws (port of Web SectionStackView)
    /// </summary>
    public sealed class SectionStackBody : IBlockInventoryPart
    {
        public const string FillPanelHeightClassName = "section-stack--fill-panel-height";

        private readonly UiStack _stack = new(UiGap.Sm) { name = "section-stack" };
        private readonly List<IBlockInventorySection> _sections = new();

        public VisualElement Root => _stack;

        public SectionStackBody(SectionStackConfig config, BlockInventoryBodyMount mount, BlockItemGridFactory grids, SectionStackSlotRegistry slots)
        {
            // 高さ確定の面でだけ伸ばす（高さ自動の面で伸ばすと子が内容高より縮む経路が開く）
            // Stretch only on the height-determining panel (on auto-height panels it would let children shrink below content)
            _stack.EnableInClassList(FillPanelHeightClassName, mount.FillsPanelHeight);
            var sectionMount = new SectionStackSectionMount(config, mount.FillsPanelHeight);
            foreach (SectionStackSlot slot in System.Enum.GetValues(typeof(SectionStackSlot)))
            {
                var section = slot == SectionStackSlot.ItemGrid ? new SectionStackItemGridSection(config, grids) : slots.CreateOrNull(slot, sectionMount);
                if (section == null) continue;
                _sections.Add(section);
                _stack.AddItem(section.Root);
            }
        }

        public void Render(OpenBlockInventoryPanel panel)
        {
            foreach (var section in _sections)
            {
                var shown = section.IsApplicable(panel);
                _stack.SetItemShown(section.Root, shown);
                if (shown) section.Render(panel);
            }
        }

        public void Detach()
        {
            foreach (var section in _sections) section.Detach();
        }
    }
}
```

`SectionStackBodyFactory.cs`（ctor で `BlockItemGridFactory`・`SectionStackSlotRegistry`、`Create(string blockType, BlockInventoryBodyMount mount)` が `SectionStackConfigTable.Resolve(blockType)` で `SectionStackBody` を返す）

`SectionStack.uss`:
```css
/* web: features/blockInventory/style.module.css .fillPanelHeight（flex: 1; min-height: 0）を標準の本文の高さ確定に写す / ports .fillPanelHeight (flex: 1; min-height: 0) onto the standard body's fixed-height mode */
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
### Task 8: 外殻 View・Presenter・登録（`Screens/BlockInventory/`・`Client.Starter/Registration/GameUi/`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventoryPanelView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventoryPanelBand.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventoryNaturalScroll.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventoryBodyHost.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventoryPresenter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventory.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/BlockInventoryGameUiRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画の末尾 `GrabGameUiRegistration.Register(builder);` の下に 1 行）
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`/* == section: Screens == */` の末尾に `@import` 3 行）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/` の `BlockInventoryTestFixtures.cs`・`BlockInventoryPanelFlowTest.cs`・`BlockInventoryDesignTest.cs`・`BlockInventoryLocalizationTest.cs`・`BlockInventoryPanelErrorTest.cs`・`BlockInventoryPanelBandTest.cs`・`BlockInventoryTitleActionTest.cs`・`BlockInventoryNaturalScrollTest.cs`

**Interfaces:**
- Consumes: `GamePanel(string, GamePanelFrame)`・`SetTitle(UiText)`・`SetTitleAction(VisualElement)`（題の操作は 1 つの器を 1 回だけ渡し、中身を器の中で付け替える。`GamePanel` に外す API が無いため）、`IconButton(string, VisualElement)`・`OnClicked`、`IconButtonCloseGlyph`、`GameScrollView(string)`、`TutorialAnchor.Mark(VisualElement, params string[])`、`AnchorIds.InventoryCloseButton`、`StageColumnsLayout.Get(StageColumn.Viewer)`、`UiRoutingState`（使わない。開閉は Task 3 の Model が `UIStateControl` で決める）、`ContentLocalizationKeys.BlockName(Guid)`、`LocalizationKeys.Ui.BlockInventory.TrainInventory`、Task 3 の `IBlockInventoryPanelSource`・`BlockInventoryPanelLayoutModel`・`IBlockInventoryPanelCommands`・`BlockInventoryPanelErrorText`、Task 7 の `BlockInventoryPartRegistry`
- Produces: `BlockInventoryGameUiRegistration.Register(ContainerBuilder)`（MainGameUiRegistration が呼ぶ）。View の要素名 `block-inventory`・`block-inventory-panel`・`block-inventory-close`・`block-inventory-error`・`train-inventory-error`・`block-inventory-natural-scroll`・`block-inventory-title-action`（録画・後続のテストが引く）

- [ ] **Step 1: 失敗するテストを書く**

`BlockInventoryTestFixtures.cs`（`BlockInventoryTestBodies` と、外殻一式を組む `BlockInventoryTestShell.Create(ReactiveProperty<BlockInventoryPanelState> state, params object[] plugged)` — 偽の Source・本物の LayoutModel〈寄与は `plugged` から〉・記録係の `RecordingPanelCommands`・本物の `BlockInventoryPartRegistry` で `BlockInventoryPanelView` を組み、舞台 1280×720 の `StageColumnsLayout` の Viewer 列へ載せて返す。トークンは `UiTestHost` のテーマが解決する）

`BlockInventoryPanelFlowTest.cs`（alpha 分 2 件）:
```csharp
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Panel;
using NUnit.Framework;
using UniRx;
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

            var root = shell.View;
            Assert.AreEqual(DisplayStyle.Flex, root.resolvedStyle.display);
            Assert.IsNotNull(root.Q("block-inventory-panel"));
            Assert.IsNotNull(root.Q("chest-grid"));
            Assert.IsNotNull(root.Q("chest-grid-slot-0"));
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
    }
}
```
（`Click` は `UiTestHost` の既存の押下ヘルパーの実名に合わせる。閉じる → 遷移 → Model が閉にする鎖は Task 3 の `BlockInventoryPanelCommandsTest` と `BlockInventoryPanelStateFactoryTest` が持つので、ここは View の 2 つの端を見る）

`BlockInventoryDesignTest.cs`（alpha 分 6 件。寸法は 1280×720 の舞台で）:
```csharp
        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [TestCase("BlockInventory/BlockInventory.uss")]
        [TestCase("BlockInventory/SectionStack/SectionStack.uss")]
        [TestCase("BlockInventory/Parts/Parts.uss")]
        public void 画面のUSSは色を直書きせずトークンだけを使う(string relativePath)
        // 中身: ファイルを読み、#rgb・rgb(・rgba( の直書きが無く、color 系の値が var(--…) だけであること（UiStyleConventionTest の規則を画面単位で再確認）

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">パネル面と閉じる操作をshared uiへ集約する</web-test>
        [Test]
        public void パネル面はGamePanelで閉じるはIconButtonでチュートリアルの錨が付く()
        // 中身: block-inventory-panel が GamePanel（game-panel--default）、block-inventory-close が IconButton で IconButtonCloseGlyph を持ち、TutorialAnchor の印に AnchorIds.InventoryCloseButton。閉じるの位置 top = --bevel-3・right = --panel-edge-fade

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ブロックパネルをviewer領域へ上端揃えで配置する</web-test>
        [Test]
        public void パネルはViewer列に載り上端が帯の上端に揃う()
        // 中身: block-inventory の親が StageColumnsLayout.Get(Viewer)、worldBound.yMin == 168（--menu-upper-safe-area）、最大高 == 482（帯）

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ブロックパネルだけに下部装飾の安全帯を確保する</web-test>
        [Test]
        public void 下の余白は安全帯で小型だけ右の余白を持つ()
        // 中身: 小型で paddingBottom == 58・paddingRight == 28、大型で paddingRight == 0・paddingBottom == 58

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ブロックスロットの列数を1列から持ち物と同じ列数の範囲で内容に追従させる</web-test>
        [TestCase(1, 1)]
        [TestCase(7, 7)]
        [TestCase(10, 10)]
        [TestCase(27, 10)]
        public void 項目グリッドの列数は枠数に追従し持ち物の列数で止まる(int slotCount, int expectedColumns)
        // 中身: 枠数 slotCount のチェストを開き、1 行目の枠の数（worldBound.yMin が先頭と同じ枠の数）が expectedColumns

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">レシピ有り機械だけviewer〜items占有の大型パネルへ広げる</web-test>
        [Test]
        public void 寄与が真のときだけ大型の寸法になる()
        // 中身: 寄与 FakeRule(Large=false) で小型（幅は内容なり・高さ ≤ 482）、Large=true にして OnRuleInputChanged を流すと 幅 == 740.108753（±0.01）・高さ == 482・block-inventory--large
```
（各件の本体は上のコメントの主張をそのまま `Assert` に書く。`%s` の it.each は Web では部品ファイルの一覧。alpha の USS 3 本を写し、machine・special・train は各自の USS を同じタグの `TestCase` で足す）

`BlockInventoryLocalizationTest.cs`（2 件。「blockGuidの導出キーを…」は英語 → 日本語へ言語を変え、`simulate.FrameUpdate()` の後に題が日本語名へ変わる。「取得失敗は…」は `BlockInventoryTestPanels.OpenFailedChest()` で `block-inventory-error` の文が `ui.blockInventory.inventoryOpenFailed` の英語文・`chest-grid` が無い・題の操作の器が空）

`BlockInventoryPanelErrorTest.cs`（3 件: 列車の ContainerMissing / TrainCarMissing / OpenFailed で `train-inventory-error` の文が各キー、列車の本文の登録が無いときは題とエラー行だけで警告ログ 1 回〈`LogAssert.Expect`〉）

`BlockInventoryPanelBandTest.cs`（2 件: 2560×1080 の舞台で帯 = 1080 − 168 − 70 = 842・大型の幅 = 2560 − 40 − 10 − 473.891247 − 16 = 2020.108753／舞台の大きさが変わると次のフレームで当て直す）

`BlockInventoryTitleActionTest.cs`（3 件: 題の操作の偽工場を `BlockInventoryTitleActionKind` の 1 つに登録し、その blockType で器に要素が入る／エラー中は器が空／別の blockType へ替えると前の部分の `Detach` が 1 回呼ばれ器が空になる。blockType と Kind の組は L0-s4b の `BlockComponentRegistry` の実物から引く）

`BlockInventoryNaturalScrollTest.cs`（2 件: 小型では本文が `block-inventory-natural-scroll` の中にあり、外側の左右・上下の負マージンが −8・内側の逃げが 8／大型では本文がスクロール器を通らず直下にある）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 外殻の型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

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
        private const string MenuLowerSafeArea = "--menu-lower-safe-area";
        private const string InventoryPanelWidth = "--inventory-panel-width";
        private const string StageColumnGap = "--stage-column-gap";

        private readonly StageColumnsLayout _stage;
        private readonly VisualElement _panel;
        private float _menuLower;
        private float _inventoryWidth;
        private float _columnGap;
        private bool _tokensResolved;
        private bool _large;

        public BlockInventoryPanelBand(StageColumnsLayout stage, VisualElement panel)
        {
            _stage = stage;
            _panel = panel;
            panel.RegisterCallback<CustomStyleResolvedEvent>(_ => ReadTokens());
            stage.RegisterCallback<GeometryChangedEvent>(_ => Apply());
        }

        public void SetLarge(bool large)
        {
            _large = large;
            Apply();
        }

        private void ReadTokens()
        {
            // 3 つ揃ったときだけ確定する（欠けは UssCustomStyleReader が LogError で知らせる）
            // Settle only when all three resolve (a missing one is reported by UssCustomStyleReader with LogError)
            _tokensResolved = UssCustomStyleReader.TryReadPixels(_panel.customStyle, MenuLowerSafeArea, out _menuLower)
                && UssCustomStyleReader.TryReadPixels(_panel.customStyle, InventoryPanelWidth, out _inventoryWidth)
                && UssCustomStyleReader.TryReadPixels(_panel.customStyle, StageColumnGap, out _columnGap);
            Apply();
        }

        private void Apply()
        {
            // silent-ok: 最初のレイアウト前はトークンも舞台の寸法も無い / 解決した時点の CustomStyleResolvedEvent・GeometryChangedEvent で当て直す
            if (!_tokensResolved || float.IsNaN(_stage.resolvedStyle.height)) return;

            // 帯 = 舞台の高さ − 上の安全帯（舞台の padding-top）− 下の安全帯
            // Band = stage height − upper safe area (the stage's padding-top) − lower safe area
            var style = _stage.resolvedStyle;
            var band = style.height - style.paddingTop - _menuLower;
            _panel.style.maxHeight = band;
            _panel.style.height = _large ? band : StyleKeyword.Auto;
            _panel.style.width = _large ? style.width - style.paddingLeft - style.paddingRight - _inventoryWidth - _columnGap : StyleKeyword.Auto;
        }
    }
}
```
（上の安全帯は舞台の `padding-top` が `--menu-upper-safe-area` そのもの〈`StageColumnsLayout.uss`〉なのでトークンを二重に読まない。左右も舞台の padding を読む。研究画面で左が畳まれる経路は SubInventory では起きない）

`BlockInventoryNaturalScroll.cs`（`GameScrollView("block-inventory-natural-scroll")` を持つ箱。`Wrap(VisualElement body)`・`Unwrap()`。負マージンと内側の逃げは USS〈`.block-inventory__natural-scroll`〉で `--block-panel-body-bleed` を使う）

`BlockInventoryBodyHost.cs`:
```csharp
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 本文・題の操作・列車の本文を付け替える。同じパネル（ブロックは位置と種類、列車は車両）のあいだは作り直さず Render だけ
    /// Swaps the body, title action and train body; while the panel stays the same (block position and type, or train car) it only re-renders
    /// </summary>
    public sealed class BlockInventoryBodyHost
    {
        private readonly BlockInventoryPartRegistry _parts;
        private readonly VisualElement _bodyContainer;
        private readonly VisualElement _titleActionContainer;
        private readonly BlockInventoryNaturalScroll _naturalScroll;
        private IBlockInventoryPart _body;
        private IBlockInventoryPart _titleAction;
        private ITrainInventoryPart _trainBody;
        private object _identity;

        public BlockInventoryBodyHost(BlockInventoryPartRegistry parts, VisualElement bodyContainer, VisualElement titleActionContainer, BlockInventoryNaturalScroll naturalScroll)
        {
            _parts = parts;
            _bodyContainer = bodyContainer;
            _titleActionContainer = titleActionContainer;
            _naturalScroll = naturalScroll;
        }

        public void Render(BlockInventoryPanelState state, BlockInventoryPanelLayout layout)
        {
            // 別のパネルか大きさが変わったら外して作り直す。エラー中は何も載せない（Web の !error）
            // Detach and rebuild when the panel or size changes; nothing is mounted while erroring (Web !error)
            var identity = IdentityOf(state, layout);
            if (!Equals(identity, _identity)) Rebuild(state, layout);
            _identity = identity;
            if (state is OpenBlockInventoryPanel block && block.Error == null)
            {
                _body?.Render(block);
                _titleAction?.Render(block);
            }
            if (state is OpenTrainInventoryPanel train && train.Error == null) _trainBody?.Render(train);

            #region Internal

            void Rebuild(BlockInventoryPanelState next, BlockInventoryPanelLayout nextLayout)
            {
                DetachAll();
                if (next is OpenBlockInventoryPanel openBlock && openBlock.Error == null)
                {
                    _body = _parts.CreateBody(openBlock, new BlockInventoryBodyMount(nextLayout.FillsPanelHeight));
                    Place(_body.Root, !nextLayout.IsLarge);
                    if (_parts.TryCreateTitleAction(openBlock.Components.TitleAction, out var action))
                    {
                        _titleAction = action;
                        _titleActionContainer.Add(action.Root);
                    }
                }
                if (next is OpenTrainInventoryPanel openTrain && openTrain.Error == null && _parts.TryCreateTrainBody(out var trainBody))
                {
                    _trainBody = trainBody;
                    Place(trainBody.Root, !nextLayout.IsLarge);
                }
            }

            void Place(VisualElement root, bool natural)
            {
                if (natural) _bodyContainer.Add(_naturalScroll.Wrap(root));
                else _bodyContainer.Add(root);
            }

            #endregion
        }

        private void DetachAll()
        {
            _body?.Detach();
            _titleAction?.Detach();
            _trainBody?.Detach();
            _body = null;
            _titleAction = null;
            _trainBody = null;
            _naturalScroll.Unwrap();
            _bodyContainer.Clear();
            _titleActionContainer.Clear();
        }

        private static object IdentityOf(BlockInventoryPanelState state, BlockInventoryPanelLayout layout)
        {
            // 同じパネルの判定は State の IsSamePanel と同じ材料（ブロックは種類と位置、列車は車両）に大きさとエラーの有無を足す
            // Same-panel identity uses the State's IsSamePanel inputs (block type and position, or train car) plus size and error presence
            return state switch
            {
                OpenBlockInventoryPanel b => (b.BlockType, b.Position, b.Error == null, layout),
                OpenTrainInventoryPanel t => (t.TrainCarInstanceId, t.Error, layout),
                _ => null,
            };
        }
    }
}
```
（`BlockInventoryPanelLayout` は readonly struct なので組の等価で比べられる。列車のエラーは本文の有無に効くので識別に含める。小型＝自然高のスクロール器で包む〈Web の `!isLarge`〉）

`BlockInventoryPanelView.cs`（約 150 行。骨子）:
```csharp
    public sealed class BlockInventoryPanelView : VisualElement
    {
        public const string LargeClassName = "block-inventory--large";

        public BlockInventoryPanelView(StageColumnsLayout stage, BlockInventoryPartRegistry parts, IBlockInventoryPanelCommands commands)
        {
            // 外殻: block-inventory（Viewer 列の子）> GamePanel(block-inventory-panel, Default) > [エラー行, 本文の器]、閉じるは GamePanel の hierarchy に絶対配置
            // Shell: block-inventory (Viewer column child) > GamePanel(block-inventory-panel, Default) > [error row, body container]; close is absolutely placed in the GamePanel hierarchy
            name = "block-inventory";
            AddToClassList("block-inventory");
            _panel = new GamePanel("block-inventory-panel", GamePanelFrame.Default);
            _titleActionContainer = new VisualElement { name = "block-inventory-title-action" };
            _error = new LocalizedLabel();          // 名前は Render で block-inventory-error / train-inventory-error に付け替える
            _body = new VisualElement { name = "block-inventory-body" };
            _close = new IconButton("block-inventory-close", new IconButtonCloseGlyph());
            TutorialAnchor.Mark(_close, AnchorIds.InventoryCloseButton);
            _close.OnClicked.Subscribe(_ => commands.Close());
            _band = new BlockInventoryPanelBand(stage, this);
            _host = new BlockInventoryBodyHost(parts, _body, _titleActionContainer, new BlockInventoryNaturalScroll());
            ...
        }

        public void Render(BlockInventoryPanelState state, BlockInventoryPanelLayout layout)
        {
            // 閉なら隠すだけ（Web は null を返す。部分は外す）
            // Closed just hides (the Web returns null; parts are detached)
            // 題: ブロックは BlockName(blockGuid)、列車は ui.blockInventory.trainInventory。題の操作の器は題の後に 1 回だけ SetTitleAction
            // Title: BlockName(blockGuid) for blocks, ui.blockInventory.trainInventory for trains; the action holder goes in once via SetTitleAction after the title
            // エラー行: BlockInventoryPanelErrorText のキー。エラーが無ければ隠す
            // Error row: the BlockInventoryPanelErrorText key; hidden without an error
            // 大きさ: LargeClassName と _band.SetLarge、本文は _host.Render
            // Size: LargeClassName and _band.SetLarge; the body goes through _host.Render
        }
    }
```
（200 行を超えそうなら題とエラー行の描画を `BlockInventoryPanelHeader.cs` へ出す〈`Screens/BlockInventory/` は 7 ファイルになる〉）

`BlockInventoryPresenter.cs`:
```csharp
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 外殻を Viewer 列へ 1 回だけ載せ、パネルの状態と大きさの変化で描き直す（前例 GrabOverlayPresenter）
    /// Mounts the shell on the Viewer column once and re-renders on panel state and size changes (precedent GrabOverlayPresenter)
    /// </summary>
    public sealed class BlockInventoryPresenter : IInitializable
    {
        private readonly StageColumnsLayout _stage;
        private readonly IBlockInventoryPanelSource _panel;
        private readonly BlockInventoryPanelLayoutModel _layout;
        private readonly BlockInventoryPartRegistry _parts;
        private readonly IBlockInventoryPanelCommands _commands;

        public BlockInventoryPresenter(StageColumnsLayout stage, IBlockInventoryPanelSource panel, BlockInventoryPanelLayoutModel layout, BlockInventoryPartRegistry parts, IBlockInventoryPanelCommands commands)
        {
            _stage = stage;
            _panel = panel;
            _layout = layout;
            _parts = parts;
            _commands = commands;
        }

        public void Initialize()
        {
            // 状態と大きさは同じフレームで両方変わりうるので、組にして 1 回だけ描く
            // State and size may both change in one frame, so render once from the pair
            var view = new BlockInventoryPanelView(_stage, _parts, _commands);
            _stage.Get(StageColumn.Viewer).Add(view);
            _panel.Current.CombineLatest(_layout.Current, (state, layout) => (state, layout)).Subscribe(pair => view.Render(pair.state, pair.layout));
        }
    }
}
```
（ゲーム寿命のオブジェクトなので購読の Dispose は持たない〈AGENTS.md 既知の制約〉）

`BlockInventory.uss`:
```css
/* web: features/blockInventory/style.module.css .panel / .panelLarge / .naturalBodyScroll と BlockInventoryPanel.tsx の GamePanel style（paddingBottom・paddingRight）・閉じるの absolute 位置を写す。calc() の寸法は BlockInventoryPanelBand.cs が解く / ports .panel, .panelLarge, .naturalBodyScroll and the GamePanel inline style (paddingBottom, paddingRight) plus the close button's absolute position from BlockInventoryPanel.tsx; calc() sizes are solved in BlockInventoryPanelBand.cs */
.block-inventory {
    align-self: flex-start;
    flex-direction: column;
}

.block-inventory__panel {
    padding-bottom: var(--block-panel-bottom-safe-area);
    padding-right: var(--block-panel-right-safe-area);
    min-height: 0;
    flex-shrink: 1;
}

.block-inventory--large .block-inventory__panel {
    padding-right: 0;
    height: 100%;
}

.block-inventory__close {
    position: absolute;
    top: var(--bevel-3);
    right: var(--panel-edge-fade);
}
```
（部品のクラス〈`.game-panel` 等〉は画面 USS に書かない〈Global Constraints〉。`GamePanel` には View で `AddToClassList("block-inventory__panel")`、閉じるの `IconButton` には `block-inventory__close` を足し、画面のクラスで書く。UITK の USS に `calc()` は無いので、自然高のスクロール器の負マージン〈Web `margin: calc(var(--block-panel-body-bleed) * -1)`〉と内側の逃げは `BlockInventoryNaturalScroll` が `CustomStyleResolvedEvent` で `--block-panel-body-bleed` を `UssCustomStyleReader.TryReadPixels` で読み、`style.margin*` に負号付き・`contentContainer.style.padding*` に正で当てる〈Tokens.uss は変えない〉。閉じるは `GamePanel.hierarchy` へ足して面の上に絶対配置する）

`BlockInventoryGameUiRegistration.cs`:
```csharp
using Client.Ui.Screens.BlockInventory;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Client.Ui.State.Recipe.Source;
using Client.Ui.State.SlotCommands;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.SlotCommands.Player;
using Game.Context;
using Game.Item.Interface;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // ブロックインベントリ（GameUi）の登録。差し込み口は後続レーンが自分の登録ファイルで同じインターフェイスを足す（D-1）
    // Registers the block inventory for GameUi; later lanes add the same interfaces from their own registration files (D-1)
    internal static class BlockInventoryGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // 源と状態（解放済みの機械レシピ・パネル・大きさ・閉じる）
            // Sources and state (unlocked machine recipes, panel, size, close)
            builder.RegisterEntryPoint<UnlockedMachineRecipeSource>().AsSelf();
            builder.RegisterEntryPoint<BlockInventoryPanelModel>().As<IBlockInventoryPanelSource>();
            builder.RegisterEntryPoint<BlockInventoryPanelLayoutModel>().AsSelf();
            builder.Register<UiStateControlTransitionRequester>(Lifetime.Singleton).As<IUiStateTransitionRequester>();
            builder.Register<BlockInventoryPanelCommands>(Lifetime.Singleton).As<IBlockInventoryPanelCommands>();

            // スロット操作（ブロック側の実行器・振り分け・型付きコマンド・Shift の断り）
            // Slot operations (block-side executor, routing, typed commands, Shift refusals)
            builder.RegisterInstance(ServerContext.GetService<IItemStackLevelLookup>());
            builder.Register<LocalBlockInventoryOperations>(Lifetime.Singleton).As<IBlockInventoryOperations>();
            builder.Register<BlockItemActionExecutor>(Lifetime.Singleton);
            builder.Register<PlannedItemActionRunner>(Lifetime.Singleton);
            builder.Register<BlockSlotRestrictionInputBuilder>(Lifetime.Singleton);
            builder.Register<PlayerSlotContextReader>(Lifetime.Singleton);
            builder.Register<ShiftMoveRefusalNotifier>(Lifetime.Singleton);
            builder.Register<BlockSlotCommands>(Lifetime.Singleton).As<IBlockSlotCommands>();
            builder.Register<PlayerSlotCommands>(Lifetime.Singleton).As<IPlayerSlotCommands>();

            // 画面（部品の工場・標準の本文・レジストリ・Presenter）
            // Screen (part factories, standard body, registry, presenter)
            builder.Register<BlockItemGridFactory>(Lifetime.Singleton);
            builder.Register<SectionStackSlotRegistry>(Lifetime.Singleton);
            builder.Register<SectionStackBodyFactory>(Lifetime.Singleton);
            builder.Register<BlockInventoryPartRegistry>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BlockInventoryPresenter>();
        }
    }
}
```
（`IItemStackLevelLookup` の名前空間・`ServerContext` の置き場は実物で確かめる〈前例 `MainGameModelRegistration` の `IItemStackLevelUnlocker` 行〉。既に誰かが `IItemStackLevelLookup` を登録していれば〈C4-e 等〉この行は足さない — 二重登録は VContainer で最後勝ちになり気づけないので、Step 0 で `grep -rn "IItemStackLevelLookup" moorestech_client/Assets/Scripts/Client.Starter` を見る。`ClientLocalNotificationSource` が DI に無ければ同じく前例から登録する）

`MainGameUiRegistration.cs`（画面登録区画の末尾）:
```csharp
            GrabGameUiRegistration.Register(builder);
            BlockInventoryGameUiRegistration.Register(builder);
```

`GameUiTheme.tss`（`Screens` 区画の末尾。パスは既存の `GrabOverlay.uss` の行と同じ書き方に揃える）:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/BlockInventory.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStack.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/Parts.uss");
```

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.BlockInventory|Client.Tests.Ui.State.BlockInventory|Client.Tests.Ui.State.SlotCommands|Client.Tests.Ui.State.Recipe|UiThemeImportBundleTest|UiStyleConventionTest|UiLayerDependencyDirectionTest|LookExceptionLedgerTest"`
Expected: `ErrorCount: 0`、全件 PASS。続けて `uloop get-logs --project-path ./moorestech_client --log-type Error` が 0 件

- [ ] **Step 5: 全体スイッチ UITK での起動を確かめる**

`uloop execute-dynamic-code` で `UiSystemEditorSwitch.Set(UiSystem.GameUi)` → PlayMode → チェストを置いて開く（録画の DSL を使ってよい）→ `uloop get-logs --log-type Error` 0 件、`uloop screenshot` 1 枚を `done.md`「証拠」へ。VContainer の解決失敗（未登録の依存）はここで出る

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory moorestech_client/Assets/Scripts/Client.Starter/Registration "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): ブロックインベントリの外殻・Presenter・登録を足す（S4-alpha）

<実行ホストの指示する帰属行>"
```

---

### Task 9: 名前の対応・処分表

**Files:**
- Modify: `docs/uitk/disposition.md`（末尾に追記）

- [ ] **Step 1: 対応を確かめる**

Run: `node tools/uitk/test-name-parity.mjs --bundle 4`
Expected: alpha のファイル（`blockInventory.spec.ts`・`blockInventoryGestures.spec.ts`・`blockRegistryCoverage.spec.ts`・`BlockInventoryPanel.localization.test.ts`・`LackHighlightText/index.test.ts`）に未対応が無い。`SectionStackView.test.tsx`・`blockInventoryDesign.test.ts`・`regression/sectionStack.spec.ts` は alpha の名前が対応済みで、残りは「写す表」の持ち主の名前だけ

- [ ] **Step 2: 処分表に書く**

`docs/uitk/disposition.md` の末尾へ、上の「写すテスト」表の alpha の行を 1 件 1 行で（書式は同ファイルの既存の行に合わせる。持ち主が別レーンの行は「写す予定: S4-machine」等で足し、後続が自分の行で置き換える）

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/disposition.md
git commit -m "docs(uitk): ブロックインベントリの写したテストを処分表に足す（S4-alpha）

<実行ホストの指示する帰属行>"
```

---

### Task 10: 並べ比べと見た目の例外

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 見た目の例外」。使ったときだけ LX-106〜LX-110）
- 記録: `LOGS/harness/uitk/S4-alpha/done.md`「差の表」

- [ ] **Step 1: 撮る**

参照 `4-block-chest`・`4-block-generic`・`4-block-tank`・`4-block-trainError`（基準 web-869b2d4264 は 1280×720、web-0e81bbe3c3-2560x1080 は 2560×1080）と同じ場面を UITK で撮る。場面の組み方と撮り方は ref-uitk-design スキルの「比べ方」節（`references/` の比較の手順）に従い、同じスキルの比較ツールで並べ比べの画像を作る

- [ ] **Step 2: 差の表を書く**

| 参照 | 見る点 | 予想 |
|---|---|---|
| 4-block-chest | 外殻の位置（Viewer 列・上端 168）・題・閉じる・列数 10 の枠・下の安全帯 | 同じ |
| 4-block-generic | 未知の blockType の汎用グリッド | 同じ |
| 4-block-tank | 題と外殻のみ。液体の行は S4-machine の区画が空なので出ない | 他レーン持ち（区画が空）。LX に書かない |
| 4-block-trainError | 列車の題と `train-inventory-error` の文。列車の本文は S4-train が空 | 同じ（エラー中は本文を出さないので Web と一致する） |

差は 1 件ずつ「同じ／直した／LX-n／他レーン持ち」で閉じる。LX は `docs/uitk/TODO.md`「## 見た目の例外」に LX-106 から順に使い、USS に `/* look-exception: LX-n */` を付ける。見込みの例外: 題の操作の右の逃げ（R-3。`GamePanel` に口が無い。alpha の参照に題の操作は出ないので、この場面では差にならない → 起草メモへ）

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
1. `SetupFlatGround` → `PlaceBlockDirect("木のチェスト", pos, BlockDirection.North)` → `WaitBlockGameObject` → チェストへ `GiveItem` で木材 10 を入れる〈ブロックのインベントリへ直接入れる DSL が無ければ、main へ渡して Shift で入れる〉
2. `PressInteract` → `WaitUiState(UIStateEnum.SubInventory)` → `block-inventory` が出る・題がチェストの名前（`Screenshot`）
3. `chest-grid-slot-0` を左押下 → 掴みに 10・枠が空（`PlayerGrabModel`）→ 同じ枠へ左押下で戻す
4. 右押下 → 掴みに 5（分割）→ 右押下で 1 個置く → 左押下で残りを置く
5. 掴みを空にして `chest-grid-slot-0` をダブルクリック → 集める（掴みの中身が増える）
6. Shift＋左押下 → main へ配分（チェストの枠が空・main に入る）→ main の枠を Shift＋左押下 → チェストへ戻る
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
共有区画（`MainGameUiRegistration` の画面登録区画・`GameUiTheme.tss` の `Screens` 区画・`docs/uitk/disposition.md`・`docs/uitk/TODO.md`・`UiCommandRejectReason`・`localization.csv`）の衝突は両側の行を残して解く。取り込み後に `uloop compile` と Task 8 Step 4 のテストを回し、緑を確かめて push

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
- 写すテスト 23 件の所在: Task 3（blockRegistryCoverage 1）・Task 4（gestures 4・spec 1）・Task 5（gestures 1）・Task 6（LackHighlightText 2）・Task 7（SectionStackView 3・regression 1）・Task 8（spec 2・design 6・localization 2）＝ 23
- 型の一致: Produces 一覧の型名・メソッド名は Task 3〜7 のコードと同じ綴り（`IBlockInventoryPart.Render(OpenBlockInventoryPanel)`・`ISectionStackSectionFactory.Create(SectionStackSectionMount)`・`IBlockInventoryLargePanelRule.IsLargeMachinePanel`・`IBlockSlotRestrictionContributor.Contribute` 等）
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
| D-8 | 項目グリッドの列数の上限は `--inventory-columns` を `UssCustomStyleReader.TryReadNumber` で読む | Web は `INVENTORY_GRID_COLUMNS` で持ち物と同じ数。トークンが既にある | C# の定数 10 |
| D-9 | `DestinationFull`（Web に無い断り）は通知せず Warning ログだけ | Web に無い通知を UITK だけで出すと見た目が分かれる。開発者は気づける | 新しい messageId を足す（Web にも足すことになり範囲外） |
| D-10 | `BlockSlotPlanner` の Shift→main が満杯で空の計画になったときは Warning ログ | fail-closed の経路は開発者が読めるログへ出す（AGENTS.md） | 黙って何もしない |
| D-11 | `DirectMovePlanner` へは既知の ItemId の枠だけ渡し、未知の ID は Error ログで止める | マスタに無い ID の最大スタック数を既定値で埋めない（必須値の欠落は止める） | 最大スタック 1 で埋める |
| D-12 | 閉じるの遷移要求は `IUiStateTransitionRequester` を挟み、本番は `UIStateControl.RequestTransition(GameScreen)` | `UIStateControl` は MonoBehaviour でテストで組めない | テストで `UIStateControl` を組む |
| D-13 | Shift の断りの通知は `ClientLocalNotificationSource` へ Web と同じ messageId で出す | Web と同じ文言・同じ経路。文言の解決は UITK の通知レーンが持つ | 画面のトーストへ直接出す（通知の経路が 2 つになる） |
| D-14 | `IItemStackLevelLookup` は `ServerContext.GetService` のインスタンスを登録する | 前例 `MainGameModelRegistration` の `IItemStackLevelUnlocker` | クライアント側で最大スタック数を計算し直す |
| D-15 | 解放済みの機械レシピの源は `Client.Ui.State/Recipe/Source/` に alpha が置き、`Game.UnlockState` を asmdef 参照に足す | 本体の追加決定。machine・recipe・research が使うだけにする | 各レーンが自分で組む（3 重になる） |
| D-16 | 大型パネルの判定は寄与の口 `IBlockInventoryLargePanelRule` にし、実装は S4-machine | 「レシピ有り機械」は機械の語彙。基盤は寄与の真偽だけを知る | alpha が機械レシピの源を見て判定する（機械の語彙が基盤に入る） |
| D-17 | 題の操作は 1 つの器（`block-inventory-title-action`）を `SetTitleAction` で 1 回渡し、中身を器の中で付け替える | `GamePanel` に題の操作を外す API が無い。部品を変えない | `GamePanel` に Clear を足す（部品の変更は integrator） |
| D-18 | 写すテストは区画の持ち主ごとに分け、同じタグの NUnit を複数レーンが持つ | 区画の中身は後続レーンの型でしか主張できない。alpha は外殻・順・項目グリッドの面だけ | alpha が偽の区画で全件を写す（中身の主張が空になる） |

---

## 設計書・実行計画へ反映する行

本体が設計書・実行計画へ写す下書き（ID は A-140〜A-144）:

- A-140: ブロックインベントリの拡張口は 6 つ（本文 `IBlockInventoryBodyFactory`・題の操作 `IBlockInventoryTitleActionFactory`・区画 `ISectionStackSectionFactory`・列車の本文 `ITrainInventoryBodyFactory`・大型パネル判定 `IBlockInventoryLargePanelRule`・Shift 配分の束縛 `IBlockSlotRestrictionContributor`）。VContainer の複数登録で受け、二重登録は起動時に例外。後続レーンは自分の `*GameUiRegistration` で足す（D-1）
- A-141: 標準の本文の区画の順は `SectionStackSlot` の列挙が正本（Web `SectionStackView` の JSX の順）。液体の 2 区画（FluidDetails・FluidRow）は S4-machine が `Screens/BlockInventory/Fluid/` から差し込む
- A-142: スロット操作の型付きコマンドはブロック側 `IBlockSlotCommands`（`block_inventory.slot`）とプレイヤー側 `IPlayerSlotCommands`（`inventory.slot`）。InventoryPanel（S4-inventory）は `IPlayerSlotCommands` だけを呼ぶ
- A-143: 解放済みの機械レシピの源 `UnlockedMachineRecipeSource`（`Client.Ui.State/Recipe/Source/`）を S4-alpha が置き、machine・recipe・research は使うだけ
- A-144: 実行計画 §1.4 の S4-alpha 行の数を実物に直す: `SectionStackView.test`(7) → 8（alpha 3・machine 5）、`blockInventoryDesign`(S15) → 16（alpha 6・他 10）。§1.4 に無い `LackHighlightText/index.test.ts`(2) を alpha に足す

---
## 後続レーンへの Produces 一覧

S4-inventory・S4-machine・S4-train・S4-special・S4-recipe・S4-research はこの節だけを読んで自分の計画を書く。型名・シグネチャはこの計画の Task 2〜8 のコードと同じ綴り。すべて `moorestech_client/Assets/Scripts/` の下。

### 差し込みの DI の形（全拡張口で共通。D-1）

各レーンは自分の `Client.Starter/Registration/GameUi/<レーン>GameUiRegistration.cs` で、口のインターフェイスとして登録するだけ。alpha の登録ファイルは触らない。

```csharp
builder.Register<MachineSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
builder.Register<RecipeMachineLargePanelRule>(Lifetime.Singleton).As<IBlockInventoryLargePanelRule>();
```

- alpha 側は `IReadOnlyList<口>` で受ける（前例 `INestedPauseScreenState` → `UiRoutingState`）。登録 0 本でも起動する
- 同じ Kind / Slot の二重登録、`BlockInventoryBodyKind.SectionStack` と `SectionStackSlot.ItemGrid` の外部登録、列車の本文の 2 本目は起動時に `InvalidOperationException`
- 未登録の本文 Kind は標準の本文へ逃げて警告 1 回、未登録の区画 Slot は起動時の情報ログに並ぶだけ（何も描かない）

### (1) blockType ごとの本文（S4-special・S4-machine が専用本文を持つなら）

```csharp
namespace Client.Ui.Screens.BlockInventory.Registry
public interface IBlockInventoryPart
{
    VisualElement Root { get; }
    void Render(OpenBlockInventoryPanel panel);   // 同じパネルのあいだ、状態が変わるたびに呼ぶ
    void Detach();                                 // 別のパネル・大きさの変化・閉で 1 回。購読はここで切る
}
public interface IBlockInventoryBodyFactory
{
    BlockInventoryBodyKind Kind { get; }           // Client.Ui.State.BlockInventory.Logic.Panel（L0-s4b。blockType → Kind は BlockComponentRegistry）
    IBlockInventoryPart Create(BlockInventoryBodyMount mount);
}
public sealed class BlockInventoryBodyMount { public bool FillsPanelHeight { get; } }   // 機械の大型パネルだけ真（Web fillsPanelHeight）
```
- 小型では本文は自然高のスクロール器（`block-inventory-natural-scroll`）の中、大型では器の直下に置かれる。本文は自分でスクロール器を持たない（大型の高さ確定のときだけ持ってよい）
- 呼ばれ方: パネルが変わる（種類・位置・エラーの有無・大きさ）と旧部分の `Detach` → 新しく `Create` → `Render`。同じパネルでは `Render` だけ

### (2) 題の操作（S4-special のロケット）

```csharp
namespace Client.Ui.Screens.BlockInventory.Registry
public interface IBlockInventoryTitleActionFactory
{
    BlockInventoryTitleActionKind Kind { get; }    // 現状 RocketLaunchPadCommand
    IBlockInventoryPart Create();
}
```
- ブロック・エラー無しのときだけ作られ、`GamePanel` の題の操作の器（`block-inventory-title-action`）に入る
- 題の右の逃げ（Web `calc(--panel-action-button-width + 8px)`）は alpha が integrator へ依頼済み。S4-special はロケットの並べ比べでこの差を確かめ、依頼が未処理なら LX に記帳する

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
    bool IsApplicable(OpenBlockInventoryPanel panel);   // 偽なら隠し Render を呼ばない（Web の各 Section が null を返す条件）
}
public interface ISectionStackSectionFactory
{
    SectionStackSlot Slot { get; }
    IBlockInventorySection Create(SectionStackSectionMount mount);
}
public sealed class SectionStackSectionMount
{
    public SectionStackConfig Config { get; }        // Client.Ui.State.BlockInventory.SectionStack（Web configByBlockType の写し）
    public bool FillsPanelHeight { get; }
}
namespace Client.Ui.State.BlockInventory.SectionStack
public sealed class SectionStackConfig { string ItemGridTestId; string FluidRowTestId; bool RenderEmptyGrid; bool ShowFluidProgress; bool ShowFluidDetails; bool HasItemGrid; }
public static class SectionStackConfigTable { public static SectionStackConfig Resolve(string blockType); }
```
- 持ち主: Context・Machine・Miner・Pump・Generator・FluidDetails・FluidRow → S4-machine（液体の 2 区画の部品は `Screens/BlockInventory/Fluid/`）。GearToElectric・BeltThroughput・Gear・ElectricNetwork・GearNetwork → S4-special
- Web の条件の写し: FluidDetails は `Config.ShowFluidDetails && Config.FluidRowTestId != null`、FluidRow は `Config.FluidRowTestId != null && !機械 && !Config.ShowFluidDetails`、Context は `!機械`（機械では Machine 区画の先頭に置く。D-3）。機械かどうかの判定は machine 側で持つ
- 区画の間は `UiStack(UiGap.Sm)`（Web `Stack gap="sm"`）。区画は自分の外側の余白を持たない
- 区画の詳細の源は `OpenBlockInventoryPanel.Block`（`BlockGameObject`。L0/L1 テストでは null）

### (4) source=train の本文（S4-train）

```csharp
namespace Client.Ui.Screens.BlockInventory.Registry
public interface ITrainInventoryBodyFactory { ITrainInventoryPart Create(); }   // 登録は 1 本だけ
public interface ITrainInventoryPart
{
    VisualElement Root { get; }
    void Render(OpenTrainInventoryPanel panel);   // 同じ車両のあいだ繰り返す
    void Detach();
}
namespace Client.Ui.State.BlockInventory.Panel
public sealed class OpenTrainInventoryPanel : BlockInventoryPanelState
{
    public long TrainCarInstanceId { get; }  public IReadOnlyList<SlotItem> ItemSlots { get; }
    public BlockInventoryPanelError Error { get; }  public TrainSubInventorySource Source { get; }
}
```
- 列車は常に大型（`BlockInventoryPanelLayout.IsLarge == true`・`FillsPanelHeight == false`）。題（`ui.blockInventory.trainInventory`）・閉じる・エラー行（`train-inventory-error`）は alpha の外殻が描く。エラー中は本文を作らない

### (5) 大型パネル判定の寄与（S4-machine）

```csharp
namespace Client.Ui.State.BlockInventory.Panel
public interface IBlockInventoryLargePanelRule
{
    bool IsLargeMachinePanel(OpenBlockInventoryPanel panel);   // どれか 1 つが真なら大型・FillsPanelHeight も真
    IObservable<Unit> OnRuleInputChanged { get; }              // 判定の材料が変わったら流す（LayoutModel が再計算する）
}
public readonly struct BlockInventoryPanelLayout { public readonly bool IsLarge; public readonly bool FillsPanelHeight; public static readonly BlockInventoryPanelLayout Natural; }
public sealed class BlockInventoryPanelLayoutModel : IInitializable { public IReadOnlyReactiveProperty<BlockInventoryPanelLayout> Current { get; } }
```
- 「レシピ有り機械」の判定は下の機械レシピの源をコンストラクタで受けて組める: `IsLargeMachinePanel` は blockGuid で `UnlockedMachineRecipeSource.Current.Value` を引き、`OnRuleInputChanged` は `source.Current.AsUnitObservable()`

### (6) Shift 配分の束縛の寄与（S4-machine・S4-special）

```csharp
namespace Client.Ui.State.BlockInventory.SlotRestriction
public interface IBlockSlotRestrictionContributor
{
    bool AppliesTo(OpenBlockInventoryPanel panel);
    OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input);   // input.WithMachine(...) / input.WithInletBindings(...) を返す
}
```
- 寄与が無いブロックは無制限（チェスト等）。`AppliesTo` が真の寄与を登録順に重ねる。結果は L0-s4b の `BlockSlotRestrictionResolver.Resolve` に渡る

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
- ブロックの枠を自前で並べる本文（機械の入力帯等）は `BlockItemGridFactory.Create(testId)` の `BlockItemGrid.Render(itemSlots, slotIndices)` を使えば押下の配線まで済む
- 分割ドラッグの確定・掴みの中身は C4-e（`IPlayerSlotDragCommands`・`PlayerGrabModel`）が持つ。InventoryPanel は `IPlayerSlotCommands` 以外を呼ばない

### 画面の部品（`Client.Ui.Screens.BlockInventory.Parts`）

```csharp
public enum LackHighlightTextSize { Xs, Sm }
public sealed class LackHighlightText : LocalizedLabel { public LackHighlightText(string testid, LackHighlightTextSize size); public void Render(UiText text, bool insufficient); }
public sealed class BlockItemGrid : VisualElement { public void Render(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<int> slotIndices); }
public sealed class BlockItemGridGroup : VisualElement { public BlockItemGridGroup(LocalizationKey label, BlockItemGrid grid); }
public sealed class BlockItemGridFactory { public BlockItemGrid Create(string testId); }
namespace Client.Ui.Screens.BlockInventory.Parts.Rows
public sealed class PowerRateText : VisualElement { public PowerRateText(string testid); public void Render(double currentPower, double requestPower, bool highlightShortage); }
public sealed class MachineStateRow { public MachineStateRow(string stateTestId, string powerRateTestId); public void MountInto(UiGapContainer parent); public void Render(MachineProcessDisplayState state, double currentPower, double requestPower, bool highlightPowerShortage); }
public sealed class PerMinuteRateRow : VisualElement { public PerMinuteRateRow(VisualElement leading); public void Render(double amountPerMinute); }
```

### パネルの状態（区画・本文が読む）

```csharp
namespace Client.Ui.State.BlockInventory.Panel
public interface IBlockInventoryPanelSource { IReadOnlyReactiveProperty<BlockInventoryPanelState> Current { get; } }   // DI 登録済み（BlockInventoryPanelModel）
public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState
{
    public string BlockType { get; }  public Guid BlockGuid { get; }  public BlockId BlockId { get; }  public Vector3Int Position { get; }
    public IReadOnlyList<SlotItem> ItemSlots { get; }  public BlockInventoryPanelError Error { get; }  public BlockInventoryComponents Components { get; }
    public BlockGameObject Block { get; }
    public bool IsSamePanel(OpenBlockInventoryPanel other);
}
public enum BlockInventoryPanelError { None, OpenFailed, ContainerMissing, TrainCarMissing }
```
- テストでパネルを組むときは `Client.Tests.Ui.State.BlockInventory.PanelModel.BlockInventoryTestPanels`（`Chest()`・`ChestWithSlots(...)`・`OpenFailedChest()`・`Train(error)`・`OfType(blockType, ...)`）と `FakePanelSource` を使う

### 解放済みの機械レシピの源（S4-machine・S4-recipe・S4-research）

```csharp
namespace Client.Ui.State.Recipe.Source
public sealed class UnlockedMachineRecipeSource : IInitializable   // DI 登録済み（AsSelf）
{
    public UnlockedMachineRecipeSource(IGameUnlockStateData unlockState, ILevelInheritanceCraftCapLookup craftCapLookup, IVanillaApiEvent vanillaApiEvent);
    public IReadOnlyReactiveProperty<IReadOnlyList<MachineRecipeData>> Current { get; }   // 解放済み・レベル継承つき。UnlockedEventPacket と OnCraftCapUnlocked で組み直す
}
public static class UnlockedMachineRecipeListBuilder { public static IReadOnlyList<MachineRecipeData> Build(IGameUnlockStateData unlockState, ILevelInheritanceCraftCapLookup craftCapLookup); }
public static class MachineRecipeSummaryConversion { public static IReadOnlyList<MachineRecipeSummary> ToSummaries(IReadOnlyList<MachineRecipeData> recipes); }
```
- `MachineRecipeData` は `Client.Ui.State.Recipe.Data`、`MachineRecipeSummary` は `Client.Ui.State.BlockInventory.Logic.Machine`（いずれも既存）。`Client.Ui.State.asmdef` への `Game.UnlockState` 参照は alpha が足す

### 登録・共有区画

- alpha の登録: `Client.Starter/Registration/GameUi/BlockInventoryGameUiRegistration.cs`（MainGameUiRegistration の画面登録区画から 1 行で呼ぶ）。後続は自分の登録ファイルを作り、同じ区画に 1 行足す。`Registration/GameUi/` は alpha で 9 ファイル、後続で 10 を超えるので integrator にサブディレクトリ化を依頼済み
- USS は自分の画面の USS を `GameUiTheme.tss` の `Screens` 区画に `@import` する。区画の USS の色は `--text-*` 等のトークンだけ（`BlockInventoryDesignTest` の色の件に自分の USS を `TestCase` で足す）

---

## 起草メモ（本体向け）

- **数の食い違い（申告）**: 実行計画 §1.4 の `SectionStackView.test`(7)・`blockInventoryDesign`(S15) は実物で 8・16。区画の中身にかかる件は持ち主（machine・special・train）へ割り振った（「写すテスト」表）。§1.4 に無い `LackHighlightText/index.test.ts`(2) を alpha に足した。反映の下書きは A-144
- **本体の追加決定 2 件の反映**: 機械レシピの源は Task 2（`Client.Ui.State/Recipe/Source/`・alpha の登録ファイル・asmdef に `Game.UnlockState`）。液体の 2 区画は `SectionStackSlot.FluidDetails`・`FluidRow` の空き区画として S4-machine に渡す
- **integrator への依頼（Task 1 で done.md に書く）**: (a) `Registration/GameUi/` のファイル数が後続で 10 を超える → サブディレクトリ化、(b) `GamePanel` が題の操作を持つときの題の右の逃げ（Web は画面側の paddingRight。UITK は題の要素が部品の中にあり画面 USS から当てられない）、(c) `SlotCommands/` を後続が増やすなら置き場の整理
- **要裁定: なし**。目的・裁定から外れる判断は無い。D-9（`DestinationFull` は通知せずログだけ）は Web に無い断りの扱いで、文言を足すなら Web 側も足すことになるため後送り
- **未確認（実装時に確かめる）**: `UiCommandResultToaster`・`LocalizationTextParam`・`UiTestHost` の押下ヘルパーの実名、C4-e の記録係の名前、`ClientLocalNotificationSource` と `IItemStackLevelLookup` が既に DI 登録されているか（二重登録は最後勝ちで気づけないので先に grep）、`.game-panel` を画面 USS から子孫選択してよいか（`UiStyleConventionTest`）、`OpenBlockInventoryPanel.Block` が L1 で null のときに区画が落ちないか（区画側の責務）、`UnlockedMachineRecipeSource` の購読順（`IVanillaApiEvent` の購読が `IGameUnlockStateData` の更新より後に走る前提）
- **通知の messageId**: `denied.inventoryMoveSlotRejected`・`denied.inventoryOpenFailed`・`denied.inventoryDetailLoading` は Web と同じ id を `ClientLocalNotificationSource` へ流す。UITK の通知レーンがこの id を文言へ解決できるかは通知レーン側の確認事項
- **C3-b の型（E-22）**: 使う。`ItemSlot` の名前ツールチップが `GameUiHoverTooltipInstaller`（C3-b）経由で出るのと、`GameScrollView`（自然高のスクロール器）。Global Constraints の依存に C3-b を足した（マージ済み）
- **Web の外殻から写さなかったもの**: `data-large` 属性はクラス `block-inventory--large` で表す。Web の ScrollArea の `type="auto"` 相当は `GameScrollView` の既定に任せる
- 設計検査記録は未実施のまま

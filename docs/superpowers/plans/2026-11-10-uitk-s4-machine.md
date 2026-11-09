# UITK S4-machine（段階 4: 機械と汎用族）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/blockInventory` のうち、機械（`MachineSection`: レシピ選択モードとインベントリモードの往復・選択中レシピのヘッダ・加工行・モジュール帯・液体の行・分間生産数・共通フッタ）、採掘機・ポンプ・発電機の区画、文脈の 3 欄（清浄室の部屋欄・抽選の中身・搬入停止の理由）、液体の行と液体の詳細（`FluidSlotRow`・`FluidInventoryContents`）を、S4-alpha の拡張口（区画・大型パネル判定の寄与・Shift 配分の束縛の寄与）と型付きのスロット操作、C4-a の液体部品、C4-b のゲージ、C4-c の `RecipeRow`、L0-s4b のロジックで、Web と同じ見た目・同じ操作の UITK 画面にする。Web ホストにしかない詳細の導出（機械・採掘機・ポンプ・発電機・液体・部屋・抽選・詰まり）を State に写し直し、L0/L1・並べ比べ（720p・2560×1080）・録画（新設 `uitk/uitk-machine-ui` と既存の機械系）まで済ませて `feature/uitk` へ取り込む。S4-train が使う液体の部品（`Screens/BlockInventory/Fluid/`）もここで作る。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。設計書 §4.2「Model の購読は Presenter、View は Render だけ」と alpha の拡張口（alpha 計画「## 後続レーンへの Produces 一覧」: `OpenBlockInventoryPanel` は識別子・エラー・枠だけで `BlockGameObject` を持たず、区画の工場は「区画の Presenter〈`IBlockInventorySection`〉＋View」の組を作る）に従う。State（`Client.Ui.State/BlockInventory/`）に族ごとの詳細 Model を置き、各 Model は `OpenBlockDetailTracker`（追跡替え・状態変化）ほかを購読して `IReadOnlyReactiveProperty<…ViewModel>` を配る。(1) `Machine/`: `MachineSectionModel`（追跡ブロックの状態詳細とマスタから `MachineDetail` を組み〈選択レシピは `MachineRecipeSelection` の union〈未選択／選択中〉へ境界 1 か所で解く。L0-s4b D-27〉、alpha の `UnlockedMachineRecipeSource`・`IModuleSlotStackLimitLookup`・パネルの枠・液体の列と合わせて `MachineSectionViewModel` を配る）、選ぶコマンド `IMachineRecipeSelectCommand`（解放の検査 → `MachineRecipeSelectionProtocol` の送信。失敗はログ）、alpha の口への寄与（大型パネル判定 `RecipeMachineLargePanelRule`・Shift 配分の束縛 `MachineSlotRestrictionContributor`。どちらも機械の Model を読む）。(2) `Family/`: 族ごとの Model `MinerDetailModel`・`PumpDetailModel`（電気／歯車の union）・`GeneratorDetailModel`（それぞれ `Current` 1 本）。(3) `Fluid/`: `BlockFluidSlotsModel`（タンクの入力→出力と、パイプはクライアントの流体複製。進捗つき）。(4) `Context/`: `BlockContextModel`（`BlockNetworkInfoCache` の部屋・抽選・詰まりを L0-s4b の入力型と未取得の状態へ写し、1 秒ポーリングの `OnUpdated` で配り直す）。状態詳細 → 値の写しは `BlockGameObject` を取らない純関数（状態詳細の型とマスタの param を引数に取る）にして L0 で試す。Client.Ui（`Screens/BlockInventory/`）は `Machine/`（機械の区画の Presenter・View。選択モードの局所状態 `MachineSelectionModeState` は Presenter が持つ）・`Machine/RecipeSelection/`（選択リスト。C4-c の `RecipeRow`・`RecipeListScrollView`）・`Context/`（文脈の 3 欄と `BlockInfoUnavailableLine`）・`Family/`（採掘機・ポンプ・発電機）・`Fluid/`（液体の 2 区画と `FluidInventoryContents`。S4-train も View を使う）を置き、各区画は alpha の区画の工場の口として自分の登録ファイル `BlockInventoryMachineGameUiRegistration` から差し込む。Presenter は Attach で自分の Model（と区画の出し入れに要る他族の Model）を購読して View.Render と `IsApplicable` へ流し、Detach で切る。View は自分で購読しない。L1 は Model の口（`IMachineSectionModel` 等）を偽の `ReactiveProperty` に差し替えて Presenter＋View を試す。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`GeometryChangedEvent`・`CustomStyleResolvedEvent`）、UniRx、UniTask、VContainer 1.10（コレクション解決）、NUnit＋`com.unity.ui.test-framework`、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（test-name-parity）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`。Files 節とコマンドはリポジトリ相対の完全なパスで書く（`S/` は Files 節の読みやすさのための略記で、実体は `moorestech_client/Assets/Scripts/`）
- **正本**: 裁定 `.decisions/2026-11-06-*`（「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1・§2・§3・§4.1〜§4.3・§5.1・§7.1 の A-45・A-47・A-57・A-93・A-94・A-110・A-111・A-140〜A-144）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-machine 行・§4.3・§5・§6.2・E-1・E-16・E-18〜E-23）、S4-alpha の計画 `docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md`（末尾「## 後続レーンへの Produces 一覧」と「写すテスト」の持ち主表）、先行レーンの申し送り（`LOGS/harness/uitk/L0-s4b/done.md`・`C4-a/done.md`・`C4-b/done.md`・`conductor.md`「## 引き継ぎ」の「S4-machine: HasSelectedRecipe 削除、未選択の Guid.Empty 解釈は画面側（L0-s4b D-27）」）、本体の決定（2026-11-10: 機械レシピの源は alpha の `UnlockedMachineRecipeSource` を使うだけ／液体の中身表示と液体の 2 区画は S4-machine が `Screens/BlockInventory/Fluid/` に作り S4-train が使う／`MachineRecipeSelectionRow.test.ts` の C 5 件は S4-machine／機械の区画にかかるテストは alpha の持ち主表に従う／1280×720 に無い状態は E-22 の形で自分で撮る）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画の順に従い、食い違いを `done.md`「要裁定」へ書く

## Requirements

設計の出所: 実行計画 §1.4 の S4-machine 行（`MachineSection`・採掘機・ポンプ・発電機・清浄室・入力の詰まり・液体。写す: `blockDetails`(8 名のうち機械・採掘機・ポンプ・発電機・液体の面。歯車とフィルタの面は S4-special〈876b3229e5〉)・`machineGestures`(3)・`machineRecipe`(10)・`machineRequiredCount`(4)・`cleanRoom/*`(8)・`fluidSlot`(5)・`MachineSection.test`(9)・`SelectedRecipeHeader.test`(2)・`ModuleSlotStackLimitText.test`(2)・CleanRoom(7)・`MachineInputStallSection.test`(3)）、§1.4 漏れの `MachineRecipeSelectionRow.test`(5)（本体の決定 3）、alpha の持ち主表で machine に割り振られた `SectionStackView.test`・`regression/sectionStack`・`blockInventoryDesign` の件。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 機械の詳細: `CommonMachineBlockStateDetail` と `MachineBlockStateDetail` が揃い `BlockParam is IMachineParam` のブロックだけを機械とする（Web ホスト `BlockDetailDtoBuilder` と同じ）。状態の文字列（`idle`・`processing`・`halted`）→ `MachineProcessDisplayState`、選択レシピの文字列 → `MachineRecipeSelection`（`Guid.Empty` は未選択）、加工中レシピの秒と出力、選択レシピからの束縛（入力 i＝素材 i〈`MachineRecipeSlotBindingUtil.GetAcceptedInputItemIds`〉・出力 j＝`InputSlotCount + j`・タンク出力 j＝`InputTankCount + j`）、歯車駆動（`GearStateDetail` の有無）。読めない文字列は機械の区画を出さず Error ログ（fail-closed） | `MachineDetailConversionTest`・`MachineRecipeSelectionTest`・`MachineStateTypeConversionTest` が緑 |
| R-2 | 採掘機・ポンプ・発電機の詳細: 採掘機は `CommonMinerBlockStateDetail`＋common＋`IMinerParam`（採掘中の品だけ・毎分 = 60 / MiningSeconds）、ポンプは `PumpBlockStateDetail`＋param（`ElectricPumpBlockParam` は common の状態と電力つき、`GearPumpBlockParam` は電力なし、他は Error ログで区画を出さない〈D-6〉）、発電機は `PowerGeneratorStateDetail`（鍵は `PowerGeneratorStateDetail.StateDetailKey`。他の詳細の `BlockStateDetailKey` と名前が違う） | `FamilyDetailConversionTest` が緑 |
| R-3 | 液体の列: `FluidMachineInventoryStateDetail` の入力→出力の順、パイプは `FluidNetworkClientCache.TryGetNodeIndex` の複製（`FluidUnits.ToAmount`）。空の液体は `BlockFluidSlotState.Empty(capacity)`。液体プラットフォームもブロックで `FluidMachineInventoryStateDetail` を出すので同じ経路、貨車は追跡ブロックでないので空。パイプは複製の変化の通知（`FluidNetworkClientCache.OnReplicaVersionChanged`。このレーンが `ApplyFullState`・`ApplyTickDiff` の直後に流すよう足す）を、追跡ブロックが複製に節を持つ間だけ購読し、量か液体 id が変わったときだけ配り直す（設計書 §4.2 [A-57]・D-20・D-36） | `BlockFluidSlotConversionTest`・`BlockFluidSlotsModelTest`・`FluidNetworkClientCacheNotificationTest` が緑 |
| R-4 | 文脈の 3 欄: 部屋欄（`CleanRoomTarget != None`。未取得は `clean-room-fetch-state` の 1 行、取得済みは見出し・空気・清浄機ごと・清浄機なし・停止理由）、抽選の中身（室内機械だけ。未取得 `clean-room-yield-fetch-state`、取得済みは `CleanRoomYieldLogic.Lines` の行、NoRole/NotOperable は欄ごと無し）、搬入停止（機械だけ。未取得 `machine-input-stall-fetch-state`、停止中は `machine-input-stall` の赤字に品名、NotStalled は欄ごと無し）。並びは部屋 → 抽選 → 停止、`UiStack(Sm)`。機械以外では区画列の先頭（`SectionStackSlot.Context`）、機械では機械の区画の本文の器の先頭（alpha D-3）。1 秒ポーリングの更新（`BlockNetworkInfoCache.OnUpdated`）で `BlockContextModel` が配り直し、Presenter が描き直す | `CleanRoomSectionTest` 4・`CleanRoomYieldSectionTest` 3・`MachineInputStallSectionTest` 3・`ContextSectionOrderTest` 4 が緑 |
| R-5 | 機械のインベントリモード: `machine-section`（`UiStack(Sm)`）＝本文（大型は `GameScrollView(machine-inventory-scroll)` の中・自然高は素のまま）＋フッタ。本文＝文脈 → 選択中レシピのヘッダ（選択行があるとき）→ `machine-inventory-body`（加工行〈入力 `machine-input-slots`・`ProgressArrowGlyph(machine-progress-arrow)`・出力 `machine-output-slots` を左右等幅の 3 列で矢印を中央固定、列の間 16px〉・モジュール帯〈module>0。`machine-module-label`・`machine-module-stack-limit`・`machine-module-slots`〉・液体の行 `machine-fluid-slots`〈ゴーストつき・進捗なし〉・出力ごとの `machine-items-per-minute`）。入力枠は所持/必要（`ItemSlotShortage`。つまみ `--shortage-count-right: 2px`・`-bottom: -3px`・`-font-size: 15px` を入力側で上書き）、出力・モジュールは必要数なし。ゴーストは束縛から | `MachineInventoryBodyTest`（machineRequiredCount 4）・`MachineDesignTest`（加工行・モジュール帯）が緑 |
| R-6 | フッタ: `UiGroup(Xs)`（`UiLayoutClass.GroupJustifyCenter`）＝`MachineStateRow(machine-state-label, machine-power-rate)`（歯車駆動は不足赤を出さない。ADR 0056）＋出力空き不足のときだけ `LackHighlightText(machine-wait-reason, Sm)`（`ui.blockInventory.machineWaitOutputSpaceShort`・不足トーン）。選択モードでもインベントリモードでも同じ高さに出る | `MachineSectionTest`（停止中・歯車駆動・出力空き不足 2 件）が緑 |
| R-7 | 選択中レシピのヘッダ `machine-selected-recipe`: 代表がアイテムなら `ItemSlot`、液体なら `FluidAmountSlot(machine-selected-recipe-fluid)` を `SlotAmountBadge.Hidden` で → 名前 `machine-selected-recipe-name`（代表の名前だけ。`recipeNameWithOthers` は選択リストの行だけ。Web `useRecipeDisplayName`）→ 秒 `machine-selected-recipe-time`（`recipeDuration`・`text--sm text--dimmed`）。ツールチップ `changeRecipe`（`HoverTooltipMark`）。押すと選択モードへ | `SelectedRecipeHeaderTest` 2＋「出力 2 品でもヘッダは代表の名前だけ」が緑 |
| R-8 | モジュールの上限 `machine-module-stack-limit`（`moduleSlotStackLimit {limit}`・`text--xs text--dimmed`）を `IModuleSlotStackLimitLookup.CurrentStackLimit` から同期で描き、`OnStackLimitChanged` で描き直す | `ModuleSlotStackLimitTextTest` 1 が緑。「配信が届くまでは何も描かない」は捨てる（D-10） |
| R-9 | 機械の枠の操作: 入力・出力・モジュールの各 `ItemSlot` の LeftDown（Shift つき）・RightDown・DoubleClick を、枠を持つ View（`MachineProcessRow`・`MachineModuleBand`）が alpha の `IBlockSlotCommands` へブロックのスロット番号で直接渡し（Presenter を経由しない。設計書 §4.2 A-8・alpha の `BlockItemGrid` と同じ）、結果を `UiCommandResultToaster.Report(BlockSlotCommands.CommandName, result)` へ。必要数の数字の上で押しても枠の操作になる | `MachineGesturesTest` 3・`MachineInventoryBodyTest`「必要数の数字の上で右クリックしても split を送る」が緑 |
| R-10 | 採掘機 `miner-section`（`ProgressArrowBar`→`PowerRateText(miner-power-rate)`〈歯車採掘機は赤なし〉→`miner-mining-items` に `PerMinuteRateRow(ItemSlot)`）、ポンプ `pump-section`（電気ポンプだけ `MachineStateRow(pump-state-label, pump-power-rate)` 赤可 → 汲み上げ対象があれば `pump-pumping-fluids` に `PerMinuteRateRow(GameIcon)`〈`--slot-size` 四方・縦横比を保つ〉、無ければ `LackHighlightText(pump-no-vein, Sm)` 不足）、発電機 `generator-section`（`GaugeBar(generator-fuel-progress)` に `FuelRatio`→`generator-operating-rate`〈`round(rate*100)`・`text--sm`・`--text-default`〉） | `BlockDetailsFamilyTest`（blockDetails の採掘機・ポンプ・発電機の件）が緑 |
| R-11 | 液体の 2 区画（`Screens/BlockInventory/Fluid/`）: FluidRow（`Config.Fluid is RowSectionStackFluid` かつ `!機械`）は `FluidSlotRow(<RowId>)` に列を写し、`ShowProgress` かつ進捗があれば矢印、FluidDetails（`Config.Fluid is DetailsSectionStackFluid`）は `FluidInventoryContents(<RowId>)`（0 件で何も描かない・行ごとに `FluidSlotRow(<id>-slot-<i>)`＋中身ありなら液体名と `量 / 容量`）。液体の写しは「中身あり→Filled・空でゴーストあり→Ghost・それ以外→Empty」 | `FluidSectionsTest`（SectionStackView 3 件）・`FluidSlotTest`（fluidSlot 5）が緑 |
| R-12 | 大型パネル判定の寄与 `RecipeMachineLargePanelRule`: 機械の Model の値が開いたパネルと同じブロック（`BlockInstanceId` 一致）で、機械かつ `MachineRecipeSelectionLogic.BuildRows(要約, blockGuid)` が 1 行以上なら真。`OnRuleInputChanged` は `UnlockedMachineRecipeSource.Current` の変化 | `RecipeMachineLargePanelRuleTest`・`MachineRecipeLayoutTest`「レシピ無しブロックは小型パネルのまま」が緑 |
| R-13 | Shift 配分の束縛の寄与 `MachineSlotRestrictionContributor`: 機械なら `input.WithMachine(detail.Binding)` | `MachineSlotRestrictionContributorTest`・`MachineGesturesTest`「機械入力スロットの Shift+クリックで main へ配分移動する」が緑 |
| R-14 | レシピ選択モード（C4-c 依存。最後の実装タスク）: `showSelection = 行>0 && (未選択 || 選択行なし || (開いた && !(要求あり && 選択==要求)))`。リスト `machine-recipe-selection`（`RecipeListScrollView`・行の間は祖先のクラスで `--recipe-list-gap: var(--machine-recipe-row-gap)`・文脈を前置）、行 `machine-recipe-<guid>`（名前 `-name`・継承バッジ `-level-inheritance`・`RecipeRow(-row)` 静止矢印 `-arrow`・素材/結果の枠の testid・選択中は内側 1px の `--select-cyan` 枠）。行押下: 行の View が `IMachineRecipeSelectCommand.Select` を直接呼んで結果を `UiCommandResultToaster` へ渡し（選択中と同じなら送らず受理する判断はコマンド＝State）、押下の `IObservable` で Presenter が `MachineSelectionModeState.RememberRequest` を呼ぶ（D-33）。レシピ 0 件はヘッダもリストも出さない | `MachineSectionTest`（選択の 3 件・レシピ 0 件）・`MachineRecipeSelectionRowTest` 5・`MachineRecipeLayoutTest`（machineRecipe 10）・`MachineSelectionModeStateTest` が緑 |
| R-15 | 配置: 部屋欄が長い（清浄機 2〜30 台）・ウィンドウが低いときも、大型の機械は本文の器だけがスクロールしフッタは見えたまま、清浄機のパネル（自然高）は自然高のスクロール器で部屋欄の末尾とフィルタ枠へ届く。選択リストはレシピが溢れても視口高が変わらずそこだけがスクロールする | `CleanRoomLayoutTest`（cleanRoom 8）・`MachineRecipeLayoutTest`（視口・フッタ高・行密度）が緑。舞台の高さを変えられない件は録画で見る（D-15） |
| R-16 | 並べ比べ: 参照 `4-block-machine`・`-machine-recipe-selection`・`-gearMachine`・`-generator`・`-gearGenerator`・`-miner`・`-gearMiner`・`-pump`・`-pumpNoVein`・`-gearPump`・`-tank`・`-generic`（1280×720 は `web-a9d5123537/`、2560×1080 は `web-0e81bbe3c3-2560x1080/`）と、1280×720 に無い `machineShortInput`・`machineFilledOutput`・清浄室 12 を E-22 の形で自分で撮って比べる（この 14 場面は 2560×1080 の既存の参照とも比べる）。差の表を `done.md` に書く | 差の表の全行が「同じ／直した／LX-116〜LX-120／他レーン持ち（歯車・特殊族の区画）」のどれか |
| R-17 | 登録: `BlockInventoryMachineGameUiRegistration.Register(builder)` を `MainGameUiRegistration` の画面登録区画に 1 行、USS 4 本を `GameUiTheme.tss` の `Screens` 区画に `@import` | 全体スイッチ UITK で機械を開いて `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`）が緑 |
| R-18 | 写すテスト（下の「写すテスト」表。タグ付き NUnit 81 件・捨てる 4 件・S4-special へ 2 件）・`docs/uitk/disposition.md` の行・`node tools/uitk/test-name-parity.mjs --bundle 4` で本レーンのファイルの未対応 0 | parity の出力に本レーンのファイルの未対応が無い（S4-special と分け合うタグは双方の NUnit が揃うまで「写す予定: S4-special」で残ってよい） |
| R-19 | 録画: 新設 `uitk/uitk-machine-ui`（機械を置き、レシピを選び、素材を入れて開いたまま進捗・出力・状態が変わる。ヘッダで選択へ戻る）と、既存の `tier9/tier9-1b-mixed-level-sorting`・`tier9/tier9-3-yield-ui`・`tier9/tier9-6-fusion-elevator`・`tier9/task31-r3/t31r3b-cable-output-space-acceptance`・`misc/cleanroom-room-usability` を全体スイッチ UITK で回す | 6 本の `result.json` が PASS、mp4、`done.md` に `UiInputExclusivity.ActiveUiSystem` の値 |
| R-20 | 終わりは moores-code-review の全ブランチレビュー（自動適用の後は録画を反映後のバイナリで撮り直す）→ pr-create（base `feature/uitk`・報告ページに並べ比べの HTML と録画）→ `origin/feature/uitk` の取り込み（共有区画は両側を残す）・compile・絞ったテスト → G1 緑 → `gh pr merge --merge` → `done.md` 1 行目「状態: 完了」→ `cmux wait-for -S uitk-s4-machine` | `git log origin/feature/uitk` にこのレーンのマージコミット。`LOGS/harness/uitk/S4-machine/done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- alpha の外殻・`SectionStackBody`・`SectionStackConfigTable`・`BlockItemGrid`（採掘機・発電機・清浄機の項目グリッド）・`LackHighlightText`・rows・スロット操作の実行器・機械レシピの源（使うだけ）
- 特殊族の区画（GearToElectric・BeltThroughput・Gear・ElectricNetwork・GearNetwork・FilterSplitter・ElectricToGear 等。S4-special）。`blockDetails` の歯車・フィルタの面（`renders ${type}` の gearMachine・filterSplitter、歯車機械のトルク・RPM・網、歯車発電機、歯車ポンプの歯車行）は S4-special（実行計画 §1.4・876b3229e5）。どのテスト名をどちらが写すかは「写すテスト」表が正
- 列車の本文（S4-train）。S4-train は本レーンの `Fluid/` の部品を使う
- レシピ画面の機械レシピ（`MachineRecipeEntry`。S4-recipe）・研究の機械アップグレード（S4-research）
- 部品（`ItemSlot`・`SlotGrid`・`FluidSlotRow`・`FluidAmountSlot`・`RecipeRow`・`RecipeListScrollView`・`LevelInheritanceBadge`・`ProgressArrowGlyph`・`ProgressArrowBar`・`GaugeBar`・`GameScrollView`・`GameIcon`）・Foundation・トークン USS は変えない。足りないものは `done.md`「integrator への依頼」と指揮へ
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は変えない。Web ホストの導出の重複は D-3 に記す
- サーバーのプロトコル・状態詳細は変えない（既存の `MachineBlockStateDetail` 等と `MachineRecipeSelectionProtocol` で足りる）。セーブ形式も変えない
- `Client.Game` は `FluidNetworkClientCache` に変化の通知 `OnReplicaVersionChanged` を 1 本足すだけ変える（設計書 [A-57]・D-36）
- `e2e/mock-host/detailActions.test.ts` 3 件（mock-host 自身の契約）と `ModuleSlotStackLimitText.test.ts`「上限の配信が届くまでは何も描かない」は捨てる（D-10・D-11）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s4-machine --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる。Mac mini の Editor は同時 2 本まで。Mac が逼迫したら Windows の `~/bin/winunity`（compile/test/play を ssh で回す）も使える
- **依存**: S4-alpha・C4-a・C4-b・C3-a・C3-b・L0-s4a・L0-s4b が入った `origin/feature/uitk`（Task 1 Step 1 で確かめる。alpha と C4-a が未マージなら止めて指揮へ返す）。**C4-c は Task 12 だけが要る**（`RecipeRow`・`LevelInheritanceBadge`・`RecipeListScrollView`。API は PLAN-W3c の C4-c 計画〈`uitk-plan-w3c` の worktree の `docs/superpowers/plans/2026-11-09-uitk-c4-c.md`・未マージ〉の Produces を写した。C4-c マージ後に Task 12 の冒頭で実物と照合する）。**C3-b を依存に足す**（E-22）: 選択中レシピのヘッダのツールチップ `HoverTooltipMark` と、大型の本文の器 `GameScrollView`
- **Foundation の箱は sealed**: `UiStack`・`UiGroup` は継承できない。自分の View は `VisualElement` を継承し、中に `UiStack`／`UiGroup` を 1 つ持つ。`UiGroup` の子は内側の行（`ui-group__row`）に入るので、揃え・折り返しは `UiLayoutClass.GroupJustifyCenter`・`GroupNoWrap`（`UiStack` は `StackAlignCenter`）で付け、画面 USS で外側に `justify-content`・`flex-wrap` を書かない。`UiStack` は非先頭の子の `marginTop` を gap で上書きするので、子の追加の余白は器に包んで器の `padding-top` で付ける
- **規約**: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下（`.meta` を除く）・partial 禁止・`Func<>` 禁止・イベントは UniRx（`Action` 禁止）・try-catch 禁止（このレーンの外部境界は `MachineRecipeSelectionProtocol` の送信だけで、UniTask の応答の null と `Success` で判定し例外は捕まえない）・日英 2 行コメント（日本語 1 行 → 英語 1 行）・デフォルト引数禁止・単純 getter/setter 禁止（`{ get; private set; }` と式本体の計算プロパティは可）・初期化は `Initialize`・null チェックは外部データ（サーバーの状態詳細・マスタ引きの結果）だけ・fail-closed の経路は開発者ログ・意図して黙る行は `silent-ok: <理由> / <誰が気づくか>`・`#region Internal` はメソッド内のローカル関数だけ・`.meta` を手で作らない・Prefab/シーンを手で編集しない・`Update()` で同値判定しない（購読）
- **State は `Client.WebUiHost` を参照しない**。Web ホストにしかない導出（`BlockDetailDtoBuilder` の機械・発電機・液体、`MachineSlotBindingDtoBuilder`、`MinerDetailDtoBuilder`、`PumpDetailDtoBuilder`、`CleanRoomDetailDtoBuilder`、`CleanRoomYieldDetailDtoBuilder`、`MachineInputStallDetailDtoBuilder`、`MachineRecipeSelectActionHandler.ResolveSelectableRecipe`）は State に写し直す。重複は D-3 に記録
- **asmdef**: `Client.Ui.State.asmdef` の参照に `Game.Block`（`MachineRecipeSlotBindingUtil`・`PowerGeneratorStateDetail`・`VanillaMachineBlockStateConst`）・`Game.Fluid`（`FluidUnits`・`FluidMaster`）・`Game.Gear`（`GearStateDetail`）を足す。`Game.UnlockState` は alpha が足す。共有ファイルなので取り込みの衝突は両側の行を残す
- **共有ファイルは追記だけ**: `GameUiTheme.tss` の `Screens` 区画の `@import`・`MainGameUiRegistration` の画面登録区画と共有サービスの並び・`localization.csv`（レシピ選択の良性の拒否 3 行。D-16）・`UiCommandRejectReason.cs` の列挙と網羅 switch の末尾・`PauseMenuCommandsTest.cs` の良性の配列・`.gitignore` の末尾・生成物の `localizationKeys.ts` と `_CompileRequester.cs`（手で書かず `gen:i18n` と Editor の再生成で作る）・`docs/uitk/disposition.md` と `docs/uitk/TODO.md` の末尾・`Client.Ui.State.asmdef` の参照の末尾
- **USS**: 画面 USS の先頭に出所の 1 行（日英。`/* web: features/blockInventory/details/machine/machineInventoryBody.module.css …（日本語の要約） / (English) */`）。色はトークン経由だけ。部品のクラスを画面 USS に書かない。部品のつまみの上書きは画面のルート（入力側の器・選択リストの器）で Web と同じ名前で
- **Web テストのタグ**: `/// <web-test file="<webui からの相対パス>"><it/test の第 1 引数></web-test>`。it.each・テンプレート文字列は展開しない（`${type}` はそのまま書く）
- **区画の形（alpha D-1・D-19）**: 区画の工場 `ISectionStackSectionFactory` は Presenter＋View の組（`IBlockInventorySection`。`Root` が View）を作る。Presenter は `Attach(panel)` で本レーンの詳細 Model を購読して View の `Render` と `IsApplicable` へ流し、`Detach()` で切る。View は `Render(<ViewModel>)` と、Presenter の状態を変える押下（ヘッダ・レシピ行）の `IObservable` だけを持ち、Model を購読しない。ブロックの枠の押下は枠を持つ View が `IBlockSlotCommands` を直接呼ぶ（設計書 §4.2 A-8。Presenter が中継しない）。区画のデータの源は `OpenBlockInventoryPanel`（識別子・エラー・枠だけ）ではなく本レーンの詳細 Model。区画は自分の display を触らない（出し入れは `SectionStackBody` が `IsApplicable` で行う）
- **トークンを C# で読むとき**は、読む要素のクラスで `--<部品>-<名>: var(--token)` と受け直してから `UssCustomStyleReader` で読む（同クラスの remarks。`:root` の値を直接は読めない）
- **処分表**: `docs/uitk/disposition.md` には `<web-test>` タグで写したテストの行を書かない（`compareBundles.mjs` が誤りにする）。書くのは捨てる・録画・目視の行だけ。alpha の `BlockInventoryDesignTest` 等には追記せず、自分のテストファイルに同じタグで写す
- **新しい共有部品は作らない**。要ると分かったら `done.md`「integrator への依頼」と指揮へ報告する（`BlockInfoUnavailableLine` は Web の使い手が文脈の 3 欄だけなので本レーンの `Context/` に置く。D-14）
- **使う部品と公開 API**（alpha・C4-a・C4-c は計画の Produces から、他は `origin/feature/uitk` の実物から写した。alpha・C4-a は Task 1 で、C4-c は Task 12 の冒頭で実物と照合する）

```csharp
// ── S4-alpha（計画 docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md「## 後続レーンへの Produces 一覧」〈85f905aa3d〉の綴り）
namespace Client.Ui.Screens.BlockInventory.Registry
public interface IBlockInventoryPart { VisualElement Root { get; } void Attach(OpenBlockInventoryPanel panel); void Detach(); }   // Presenter。Root が View
namespace Client.Ui.Screens.BlockInventory.SectionStack
public enum SectionStackSlot { Context, ItemGrid /* alpha 専有 */, Machine, Miner, Pump, Generator, GearToElectric, BeltThroughput, Gear, ElectricNetwork, GearNetwork, FluidDetails, FluidRow }
// 区画の Presenter。IsApplicable は Attach 後に有効で、SectionStackBody が購読して出し入れと先頭の間隔を組み直す（区画は自分の display を触らない）
public interface IBlockInventorySection : IBlockInventoryPart { IReadOnlyReactiveProperty<bool> IsApplicable { get; } }
public interface ISectionStackSectionFactory { SectionStackSlot Slot { get; } IBlockInventorySection Create(SectionStackSectionMount mount); }
public sealed class SectionStackSectionMount { public SectionStackConfig Config { get; private set; } public bool FillsPanelHeight { get; private set; } }
namespace Client.Ui.State.BlockInventory.SectionStack   // Web configByBlockType の写し（判別 union。alpha D-20）
public sealed class SectionStackConfig { public SectionStackItemGrid ItemGrid { get; } public SectionStackFluid Fluid { get; } }
public abstract class SectionStackFluid { public static readonly SectionStackFluid Hidden; }
public sealed class RowSectionStackFluid : SectionStackFluid { public string RowId { get; } public bool ShowProgress { get; } }
public sealed class DetailsSectionStackFluid : SectionStackFluid { public string RowId { get; } }
public static class SectionStackConfigTable { public static SectionStackConfig Resolve(string blockType); }
namespace Client.Ui.State.BlockInventory.Panel
public interface IBlockInventoryPanelSource { IReadOnlyReactiveProperty<BlockInventoryPanelState> Current { get; } }   // 共有サービス
public abstract class BlockInventoryPanelState { public static readonly BlockInventoryPanelState Closed; public abstract bool IsSamePanel(BlockInventoryPanelState other); }
// 識別子・エラー・枠だけ（BlockGameObject は載らない）。区画の Model が開いたブロックを引く鍵は BlockInstanceId・Position・BlockGuid
public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState { string BlockType; Guid BlockGuid; BlockId BlockId; BlockInstanceId BlockInstanceId; Vector3Int Position; IReadOnlyList<SlotItem> ItemSlots; BlockInventoryPanelError Error; BlockInventoryComponents Components; }
public enum BlockInventoryPanelError { None, OpenFailed, ContainerMissing, TrainCarMissing }   // エラー無しは == None
public interface IBlockInventoryLargePanelRule { bool IsLargePanel(OpenBlockInventoryPanel panel); IObservable<Unit> OnRuleInputChanged { get; } }
namespace Client.Ui.State.BlockInventory.SlotRestriction
public interface IBlockSlotRestrictionContributor { bool AppliesTo(OpenBlockInventoryPanel panel); OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input); }
namespace Client.Ui.State.SlotCommands.Block
public interface IBlockSlotCommands { UiCommandResult LeftDown(int blockSlot, bool shift); UiCommandResult RightDown(int blockSlot); UiCommandResult DoubleClick(int blockSlot); }   // 共有サービス
public sealed class BlockSlotCommands : IBlockSlotCommands { public const string CommandName = "block_inventory.slot"; }
namespace Client.Ui.Screens.BlockInventory.Parts
public enum LackHighlightTextSize { Xs, Sm }
public sealed class LackHighlightText : LocalizedLabel { public LackHighlightText(string testid, LackHighlightTextSize size); public void Render(UiText text, bool insufficient); }
namespace Client.Ui.Screens.BlockInventory.Parts.Rows
public sealed class PowerRateText : VisualElement { public PowerRateText(string testid); public void Render(double currentPower, double requestPower, bool highlightShortage); }
public sealed class MachineStateRow { public MachineStateRow(string stateTestId, string powerRateTestId); public void MountInto(UiGapContainer parent); public void Render(MachineProcessDisplayState state, double currentPower, double requestPower, bool highlightPowerShortage); }
public sealed class PerMinuteRateRow : VisualElement { public PerMinuteRateRow(VisualElement leading); public void Render(double amountPerMinute); }
namespace Client.Ui.State.Recipe.Source
public sealed class UnlockedMachineRecipeSource : IInitializable { public IReadOnlyReactiveProperty<IReadOnlyList<MachineRecipeData>> Current { get; } }   // 共有サービス（AsSelf）
public static class MachineRecipeSummaryConversion { public static IReadOnlyList<MachineRecipeSummary> ToSummaries(IReadOnlyList<MachineRecipeData> recipes); }
// 共有サービス（MainGameUiRegistration の並び。本レーンは ctor で受けるだけで登録しない）: IBlockInventoryPanelSource・UnlockedMachineRecipeSource・IBlockSlotCommands・BlockItemGridFactory
// テスト補助（alpha）: Client.Tests.Ui.State.BlockInventory.PanelModel.BlockInventoryTestPanels.OfType(string blockType, params SlotItem[] slots)・FakePanelSource（State で書き換え）
//                    Client.Tests.Ui.Screens.BlockInventory.BlockInventoryTestBodies.SectionStack(FakePanelSource source, string blockType, params ISectionStackSectionFactory[] plugged)
//                    BlockInventoryTestShell.Create(ScreenTestHost host, ReactiveProperty<BlockInventoryPanelState> state, params object[] plugged)（舞台 1280×720 固定。plugged に工場と IBlockInventoryLargePanelRule）

// ── C4-a（計画 2026-11-07-uitk-c4-a.md の Produces）namespace Client.Ui.Components.Slots
public abstract class FluidSlotContent { public static readonly FluidSlotContent Empty; public static FluidSlotContent Ghost(UiIcon icon, double amount); public static FluidSlotContent Filled(UiIcon icon, double amount, double capacity, Color face); }
public sealed class FluidSlot : VisualElement { public FluidSlot(string testid); public void SetFluid(FluidSlotContent content); }
public abstract class SlotAmountBadge { public static readonly SlotAmountBadge Hidden; public static SlotAmountBadge Show(double amount); }
public sealed class FluidAmountSlot : VisualElement { public FluidAmountSlot(string frameTestid); public void SetFluid(UiIcon icon, SlotAmountBadge badge); public IObservable<SlotGesture> OnSlotGestured { get; } }  // 量の帯 name=<frameTestid>-amount
public sealed class FluidSlotRow : VisualElement { public FluidSlotRow(string testid); public void SetFluids(IReadOnlyList<FluidSlotContent> fluids); public void SetProgress(float ratio01); public void ClearProgress(); } // 子 fluid-slot・progress-arrow-bar、0 マスは列ごと出さない
namespace Client.Ui.Content
public sealed class GameContentPresentation { public UiIcon Item(ItemId id); public UiIcon Fluid(FluidId id); public Color FluidFace(FluidId id); public UiText ItemName(ItemId id); }   // FluidFace は C4-a が足す。Fluid(空の液体) は ArgumentException

// ── C4-b / C2-c / C3-a / C3-b（実物）
namespace Client.Ui.Components.Gauges
public sealed class ProgressArrowGlyph : VisualElement { public ProgressArrowGlyph(string testid); public void SetProgress(float ratio01); public void SetIdle(); }
public sealed class ProgressArrowBar : VisualElement { public ProgressArrowBar(); public void SetValue(float ratio01); }   // name 固定 progress-arrow-bar・.progress-arrow-bar__fill の style.width %
public sealed class GaugeBar : VisualElement { public GaugeBar(string testid); public void SetValue(float ratio01); }
namespace Client.Ui.Components.Slots
public sealed class ItemSlot : VisualElement { public ItemSlot(string frameTestid); public IObservable<SlotGesture> OnSlotGestured { get; } public void SetContent(ItemSlotContent content); }
public sealed class ItemSlotContent { public ItemSlotContent(UiIcon icon, int? count, ItemSlotMarks marks, ItemSlotShortage shortage, ItemSlotGhost ghost, UiText tooltipOverride); }
public sealed class ItemSlotShortage { public ItemSlotShortage(double ownedCount, double requiredCount, bool lacking, UiText tooltip); }
public sealed class ItemSlotGhost { public ItemSlotGhost(UiIcon icon, int count); }
public readonly struct ItemSlotMarks { public ItemSlotMarks(bool selected, bool catalog, bool insufficient); }
public sealed class SlotGrid : VisualElement { public SlotGrid(string testid, int columns); public void SetColumns(int columns); public void AddItem(VisualElement item); public void ClearItems(); }
public readonly struct SlotGesture { public readonly SlotGestureKind Kind; public readonly bool Shift; }
public sealed class GameIcon : VisualElement { public GameIcon(); public void SetIcon(UiIcon icon, GameIconFallback fallback); public void ClearIcon(); }   // GameIconFallback { IdText, Label, None }
namespace Client.Ui.Components.Controls
public sealed class GameScrollView : ScrollView { public GameScrollView(string testid); public float GetViewportHeight(); public float GetContentHeight(); public bool TryGetContentTop(VisualElement element, out float top); public void SetScrollOffset(float y); }
namespace Client.Ui.Foundation.Tooltip
public static class HoverTooltipMark { public static void Mark(VisualElement element, UiText text); public static void Unmark(VisualElement element); }
namespace Client.Ui.Foundation.Layout
public sealed class UiStack : UiGapContainer { public UiStack(UiGap gap); }   public sealed class UiGroup : UiGapContainer { public UiGroup(UiGap gap); }
// UiGapContainer: AddItem・SetItemShown・ClearItems・SetGap。表示切替は SetItemShown だけ（style.display の直書きは間隔の対象外）
namespace Client.Ui.Foundation.Text
public class LocalizedLabel : Label { public void SetKey(LocalizationKey key); public void SetText(UiText text); }
public static class UiTypographyClass { TextXs = "text--xs", TextSm, TextDimmed = "text--dimmed", TextDefault, TextMuted, TextInsufficient }
namespace Client.Ui.State.Toast
public sealed class UiCommandResultToaster { public void Report(string commandName, UiCommandResult result); }   // DI 登録済み

// ── C4-c（計画 docs/superpowers/plans/2026-11-09-uitk-c4-c.md〈PLAN-W3c の worktree・未マージ〉の Produces。C4-c マージ後に実物と照合）
namespace Client.Ui.Components.Recipe
public sealed class RecipeRow : VisualElement   // UssClassName "recipe-row"。秒数の name は <testid>-duration
{
    public RecipeRow(string testid, string arrowTestid);
    public void SetSlots(IReadOnlyList<VisualElement> materials, IReadOnlyList<VisualElement> results);   // 枠は呼び手が作る（枠ごとの testid は呼び手が付ける）
    public void SetDuration(UiText duration); public void SetProgress(float ratio01); public void SetIdle();
    public void SetAction(VisualElement action); public void ClearAction();                               // 機械の選択行はアクション欄を使わない
}
public sealed class LevelInheritanceBadge : VisualElement { public LevelInheritanceBadge(string testid); public void Show(UiText text); public void Hide(); }   // 作った直後は隠れている。testid は Show の文のラベルに付く
namespace Client.Ui.Components.Controls
public sealed class RecipeListScrollView : VisualElement   // UssClassName "recipe-list-scroll-view"。列の name は listTestid、中の GameScrollView は <listTestid>-scroll
{
    public RecipeListScrollView(string listTestid);
    public void SetLeadingContent(VisualElement content); public void ClearLeadingContent();
    public void SetEntries(IReadOnlyList<VisualElement> entries);   // 行を隠すときは display を直書きせず渡し直す
}
// つまみ（画面が祖先のクラスで上書き）: --recipe-list-gap（機械は var(--machine-recipe-row-gap)）・--recipe-row-padding-block（0.35rem＝5.6px）・--recipe-slot-size-max（2rem＝32px）
// RecipeActionButton は機械の選択行では使わない

// ── State（L0-s4b。実物）namespace Client.Ui.State.BlockInventory.Logic.*
public static class MachineRecipeSelectionLogic { BuildRows(IReadOnlyList<MachineRecipeSummary>, Guid blockGuid); BuildRows(IReadOnlyList<MachineRecipeSummary>, Guid blockGuid, Guid selectedRecipeGuid); int OtherOutputCount(MachineRecipeSummary); }
public sealed class MachineRecipeSelectionRow { MachineRecipeSummary Recipe; RecipeDisplaySubject Subject /* ItemRecipeDisplaySubject{ItemId,Count} | FluidRecipeDisplaySubject{FluidId,Amount} */; bool Selected; }
public static class MachineSlotGhosts { MachineSlotView BuildMachineSlotView(MachineBindingDetail, IReadOnlyList<SlotItem>, IReadOnlyList<BlockFluidSlotState>); bool TryGetInputRequirement(SlotItem, BoundItemSlot, out MachineInputRequirement); }
public sealed class MachineSlotView { IReadOnlyList<BoundItemSlot> Inputs; Outputs; IReadOnlyList<BoundFluidSlot> Fluids; }   // GhostedItemSlot{Ghost: MachineItemGhost{ItemId,Count,AcceptedItemIds}} | PlainItemSlot、GhostedFluidSlot{Ghost: MachineFluidGhost{FluidId,Amount}} | PlainFluidSlot
public sealed class MachineBindingDetail { MachineBindingDetail(MachineSlotLayout, IReadOnlyList<MachineItemSlotBinding>, IReadOnlyList<MachineTankBinding>); }
public sealed class MachineItemSlotBinding { MachineItemSlotBinding(int slot, ItemId itemId, int count, IReadOnlyList<ItemId> acceptedItemIds); }   public MachineTankBinding(int tank, FluidId fluidId, double amount)
public sealed class MachineSlotLayout { MachineSlotLayout(int input, int output, int module); MachineSlotBands Split(int total); }   // Bands: Input・Output・Module（int[]）
public static class BlockDetailMath { ComputePowerRate; bool TryGetItemsPerMinute(int outputCount, double recipeTimeSeconds, out double itemsPerMinute); double FuelRatio(double remaining, double current); int[] AllSlotIndices(int total); }
public enum MachineProcessDisplayState { Idle, Processing, Halted }
public static class MachineInputStallLogic { LocalizationKey MessageKey(MachineInputStallReasonKind reason); }
public static class CleanRoomLogic { CleanRoomDisplay Display(CleanRoomFetchedDetail cleanRoom); }   // Headline・HeadlineInsufficient・TryGetHaltReason(out UiText)、EnclosedCleanRoomDisplay{Air, Filters, NoFilter, ShowsNoFilter}
public abstract class CleanRoomFetchedDetail { static Enclosed(CleanRoomSummary, CleanRoomHaltReason); Openings(int, halt); NoEnclosure(halt); TooLarge(int, halt); Detecting(halt); }
public sealed class CleanRoomSummary { CleanRoomSummary(CleanRoomHeadline headline /* Class(name) | ClassOut */, double concentration, double airChangeRate, IReadOnlyList<CleanRoomAirFilter> airFilters); }
public enum CleanRoomHaltReason { None, NotEnclosed, ClassOut }
public static class CleanRoomYieldLogic { List<CleanRoomYieldLine> Lines(CleanRoomYieldFetched); }   // CleanRoomYieldLine{RowId, Text}
public abstract class CleanRoomYieldFetched { static WithoutPendingDraw(CleanRoomYieldRates); WithPendingDraw(CleanRoomYieldRates, CleanRoomYieldRates); }   // CleanRoomYieldRates.LayerExposure(double)・Dicing(double, int, IReadOnlyList<double>, double)
public static class BlockInfoFetchStateLogic { bool TryGetUnavailableKey(BlockInfoFetchState state, out LocalizationKey key); }   // 文言の {target} に欄の名前
public abstract class BlockFluidSlotState { double Capacity; abstract bool HasRealFluid; static Empty(double capacity); static Filled(FluidId, double amount, double capacity); }
public static class LevelInheritanceBadgeText { bool TryBuild(RecipeLevelInheritance inheritance, out UiText text); }   // Client.Ui.State.Recipe.Logic

// ── 源（Client.Game・サーバーの共有型）
BlockGameObject { TBlockState GetStateDetail<TBlockState>(string stateKey); BlockMasterElement BlockMasterElement; BlockPositionInfo BlockPosInfo; }
CommonMachineBlockStateDetail { string CurrentStateType; float CurrentPower; float RequestPower; float ProcessingRate; }   MachineBlockStateDetail { float ProcessingRate; string MachineRecipeGuid; string SelectedRecipeGuid; bool IsOutputSpaceShort; }
CommonMinerBlockStateDetail { float MiningSeconds; List<ItemId> GetCurrentMiningItemIds(); }   PumpBlockStateDetail { PumpingFluids[] { int FluidId; double AmountPerSecond } }   PowerGeneratorStateDetail { RemainingFuelTime; CurrentFuelTime; OperatingRate }
FluidMachineInventoryStateDetail { List<FluidMessagePack> InputTanks; OutputTanks }   FluidMessagePack { int FluidId; double Amount; double MaxCapacity }   GearStateDetail（Game.Gear.Common）
CleanRoomOperabilityStateDetail { CleanRoomOperability Operability /* NotEnclosed, ClassOut, Operable */ }
BlockNetworkInfoCache { CleanRoomDetailTarget CleanRoomTarget /* None, AirFilter, Machine */; bool IsTrackingMachineInputStall; IObservable<Unit> OnUpdated; BlockInfoFetchState GetCleanRoom(out CleanRoomInfoView); GetCleanRoomYield(out CleanRoomYieldView); GetMachineInputStall(out MachineInputStallView); }   // DI 登録済み
FluidNetworkClientCache { bool TryGetNodeIndex(Vector3Int pos, out int node); Replica.Network { NodeFluidIds; NodeAmounts; NodeCapacities }; int ReplicaVersion; void ApplyFullState(FluidNetworkFullState); bool ApplyTickDiff(FluidTickDiff); }   // DI 登録済み。変化の通知は無い（このレーンが OnReplicaVersionChanged を足す。D-36）
IModuleSlotStackLimitLookup { int CurrentStackLimit; IObservable<int> OnStackLimitChanged; }   // DI 登録済み（MainGameModelRegistration）
IGameUnlockStateData { IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos; }   // DI 登録済み
ClientContext.VanillaApi.Response.Block.SendMachineRecipeSelectionRequest(MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(Vector3Int, Guid), CancellationToken) → UniTask<MachineRecipeSelectionResponse{ bool Success; FailureReason }>
```

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反6件・修正6件 / パイプの量が開いたまま止まる（V-1 → D-20。設計書 §4.2 を 876b3229e5 で足した）・枠の押下の Presenter 中継（V-2 → D-1）・`IBlockFluidSlotsModel` を共有サービスへ（V-3 → D-25）・族ごとの Model（V-4 → D-23）・行押下の判断を State へ（V-5 → D-24）・D-16 とコードの食い違い（V-6 → D-16）。注目点 3（鍵が `BlockGuid` だけ）は D-26、注目点 6（`NotApplicableSection` の重複）は D-31 で直し、他の注目点は直さない（理由は再現役の判定どおり）。死活表は下の「## 死活表」
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱5・第3バケツ1 / 強: 歯車駆動の bool 畳み込みは再現せず（Web も同じ 1 つの意味）、外部解決の欠損は再現せず（`GameContentPresentation` が例外で止める）、ADR 矛盾（判定主体）は V-2 と同じく D-1 を直し、ADR 矛盾（置き場）は E-23 が 2e9121cf1e で解消済み。弱は 2 件直し（`BlockFluidSlotsModelTest` を足す・`ContextEntry<T>.IsAbsent` で判定を 1 本に）3 件見送り（`HasAny` の合成・`TryBuild` の失敗理由・選択モードの bool＋Count は再現せず）。第3バケツ 1 件を「本 PR 外のリファクタ提案」へ（agent 既定で裁定）
- 配置検査 r2（差分検査）: 実施済み / 違反2件・修正2件 / レシピを選ぶコマンドの `void` が設計書 §1.1・§4.1・A-106 の `UiCommandResult` と食い違う（V-r2-1 → D-16 を書き直し、良性の拒否 3 種を列挙の末尾へ）・文脈の区画の出す／出さないを Presenter が合成している（V-r2-2 → State の `ContextSectionVisibility.Shows`。D-32）
- Phase 2.6 r2（差分検査）: 実施済み / 強3・弱4・第3バケツ2 / 強: スクロール到達の検査の座標系（`ScrollTo`＋`worldBound`＋`Pick` の補助へ）・清浄室の条件が Web より弱い（台数を spec ごとに分け、境目の主張を Web の定数 120 と同じ形へ）・選択モードから戻ると文脈の欄の順と間隔が崩れる（積み直し＋テスト 1 件）を直した。弱: `HasAny` の合成・`PumpDetailConversion.TryBuild` の 2 つの偽・「機械か」の 2 形・液体の読み手の 2 か所呼びの 4 件は再現せず見送り（強の欠損〈名前の解決〉も再現せず）。第3バケツ: Web ホストとの導出の重複（既存）と、枠の押下の写しの alpha `BlockItemGrid` との重複（タスクから外して「本 PR 外のリファクタ提案」へ移した）
- 配置検査 r3（差分検査）: 実施済み / 違反1件・修正1件 / 送らない拒否の `UiCommandRejected.Detail` に GUID を入れていた（V-r3-1。`ToWireCode()` が GUID を返しログから理由が消える → 前例どおり `Detail` は `""`、GUID は送らない判断の Warning ログへ。D-16）。注目点 N-r3-1（レシピ行の結果を Presenter がトースターへ渡す）は View がコマンドを呼んでトースターへ渡す形に直した（D-33）。N-r3-2（`block_not_open` の良性／非良性の割れ）は S4-train も良性に揃える（本体から S4-train へ連絡。D-16）
- Phase 2.6 r3（差分検査）: 実施済み / 強1・弱2 / 強: D-16 と Task 12 の Presenter のトースター（＝N-r3-1）は D-33 で解消。弱: `ContextSectionVisibility.Shows` の合成（前例 `FluidRowVisibility` と同形で見送り）・「ブロックが開いているか」の述語の 2 回（`MachineSectionModel`・`MachineRecipeSelectCommand`。alpha の `IBlockInventoryPanelSource` の口の話なので本レーンでは見送り）
- 配置検査 r4（差分検査）: 実施済み / 違反0件 / 注目点 N-r4-1（1 回の押下を View がコマンドへ、同じ押下を Presenter が局所状態へ）は D-33 の形として記録済み。備考 2 件（Self-Review の古い判断の置き場・送信側ログのコマンド名の直書き）は直した
- Phase 2.6 r4（差分検査）: 実施済み / 強0・弱3 / Task 12 の下流の未追従（`Choose` の残り・`MachineSectionFixtures.cs` の Modify 漏れ）は直した。「開いた機械か」の 2 源の導出と `Detail` の空文字での wire の切り替えは見送り（D-35）

## 死活表

全体スイッチを UITK にしたとき、機械・採掘機・ポンプ・発電機・タンク・パイプ・清浄室のブロックインベントリにぶら下がる操作のうち、Web 版で今使えるもの。Web 側は変えないので、スイッチが Web の間の操作はすべて不変。

| 操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| 機械を開く（レシピ有りは大型、未選択は選択モード） | 生きる | R-12・R-14、Task 5・12、`MachineRecipeLayoutTest` |
| レシピ行を押して選ぶ／同じ行で閉じる／ヘッダで選択へ戻る | 生きる | R-14、`MachineSectionTest`・`MachineSelectionModeStateTest`・録画 `uitk-machine-ui` 手順 3・6 |
| 入力・出力・モジュールの枠の左／Shift 左／右／ダブル | 生きる | R-9、`MachineGesturesTest`・`MachineInventoryBodyTest` |
| main の枠の Shift＋左で機械へ配分（束縛つき） | 生きる | R-13 `MachineSlotRestrictionContributor`、録画手順 4。状態詳細が届く前は束縛無しになるが Web も同じ |
| ブロックの枠への分割ドラッグ（進入） | 生きる（Web と同じく使わない） | alpha の写し「ブロックの枠は進入を使わない」 |
| 枠のアイテム名のツールチップ・液体の枠のツールチップ | 生きる | C3-a `ItemSlot`＋C3-b、`FluidSlotTest`「Water 名のツールチップ」 |
| 開いたまま進捗・出力・状態・電力率・出力空き不足の理由が変わる | 生きる | 追跡の状態変化を購読（Task 5）、録画手順 5 |
| モジュールの上限の更新 | 生きる | R-8 `OnStackLimitChanged` |
| 採掘機・ポンプ・発電機の表示と更新 | 生きる | R-10、Task 3・8 |
| タンク・液体プラットフォーム（機械の液体を含む）の量の更新 | 生きる | `FluidMachineInventoryStateDetail` の状態変化 |
| パイプを開いたまま量・種類が更新される | 生きる | R-3・D-20・D-36。複製の変化の通知を購読し、変わった回だけ配り直す。`BlockFluidSlotsModelTest` |
| 清浄室の部屋欄・抽選・搬入停止の 1 秒更新 | 生きる | R-4 `BlockNetworkInfoCache.OnUpdated` の購読 |
| 低いウィンドウ・清浄機の多い部屋での本文のスクロールとフッタ | 生きる（600・1080 は録画で見る） | R-15・D-15、Task 11・15 |
| 開いたまま言語を変える | 生きる（「取得できない」行は最大 1 秒遅れる） | D-13 |
| レシピ選択の要求を送れない・サーバーが拒否したとき | 生きる（Web と同じく画面には出さない。送れない要求は良性の拒否で Warning ログ） | D-16 |
| 開いたまま撤去・同じ座標の差し替え | 生きる | `OpenBlockDetailTracker` の Refresh → Model の組み直し。寄与は `BlockInstanceId` で区別（D-26） |
| 歯車機械・歯車採掘機・歯車ポンプ・歯車発電機の歯車行、特殊族の区画 | 他レーンの持ち物（S4-special がマージされるまで UITK では出ない） | 「やらないこと」。「UITK の間、未実装の画面は使えない」の裁定の範囲 |
| 要確認: 開いた直後に小型から大型へ作り直されたときの選択モードの状態 | 要確認 | 状態詳細がパネルより遅れて届くかを録画で見る（起草メモの未確認） |
| 要確認: 機械の液体の行のゴーストの液体名のツールチップ | 要確認 | C4-a の `Ghost` が印を付けるかを実物で確かめる |


## 写すテスト

Web の実物を開いて数え直した（`grep -nE "^\s*(it|test)(\.each)?\("`。for・it.each は展開しない）。§1.4 の数は P 38・C 23 とも実物と一致。`MachineRecipeSelectionRow.test.ts`(5) は本体の決定 3 で足し、§1.4 にも C4-c の記帳（[A-127]）で入った。alpha の持ち主表で machine に割り振られた件は、alpha のテストファイルに追記せず本レーンのファイルに同じタグで写す: `SectionStackView.test` 7（machine だけの 5〈§1.4 の S4-alpha 行の「machine 5」〉＋alpha と分け合う 2〈「%s の既存表示を保つ」の液体の行・「部屋欄はセクション列の先頭に描く」の中身〉）・`regression/sectionStack` 1（alpha・special と分け合う）・`blockInventoryDesign` 10（machine だけの 7＋分け合う 3〈「%sから禁止Mantine部品…」と「レシピ有り機械だけ…大型パネル」は alpha と、「ゲージとモード切替…」は special・train と〉）。`blockDetails`(8) は実行計画 §1.4（876b3229e5）どおり分ける: 本レーンが写すのは 6 名（うち `renders ${type}` と「歯車ポンプは電力行を持たず歯車行を出す」の 2 名は S4-special と分け合い、本レーンは機械・採掘機・ポンプ・発電機の区画の面だけを主張し、S4-special が歯車・フィルタの面を同じタグで別 NUnit に写す）。「歯車機械は消費トルクと基準付きRPMを赤なしで出し、網の需給行を持つ」「歯車発電機は発生トルクと現在RPMだけを出す」の 2 名は S4-special だけが写す。

| Web ファイル（`W/` から） | テスト名（第 1 引数） | 本レーンの NUnit | Task |
|---|---|---|---|
| `e2e/tests/block/blockDetails.spec.ts` | `renders ${type} detail section` | `BlockDetailsFamilyTest`（machine・generator・miner・pump・gearPump の type で本レーンの区画〈`machine-section`・`generator-section`・`miner-section`・`pump-section`〉が出る。gearMachine・filterSplitter の type は S4-special が同じタグで写す） | 8 |
| 同 | 歯車機械は消費トルクと基準付きRPMを赤なしで出し、網の需給行を持つ | —（S4-special だけが写す。充足率を赤にしない面は次の行の gearMachine で本レーンが見る） | — |
| 同 | `${type} は充足率不足でも充足率行を赤にしない` | `BlockDetailsMachineTest`（gearMachine・gearMiner の 2 例） | 9 |
| 同 | 電力駆動の採掘機は充足率不足を赤で出す | `BlockDetailsFamilyTest` | 8 |
| 同 | 歯車発電機は発生トルクと現在RPMだけを出す | —（S4-special だけが写す） | — |
| 同 | 機械recipeの出力個数と秒数から分間生産数を表示する | `BlockDetailsMachineTest` | 9 |
| 同 | 油井は電力充足率と公称生成速度を出し鉱脈外なら警告行に切り替わる | `BlockDetailsFamilyTest` | 8 |
| 同 | 歯車ポンプは電力行を持たず歯車行を出す | `BlockDetailsFamilyTest`（gearPump の `pump-section` に `pump-state-label`・`pump-power-rate` が無い面。歯車行は S4-special が同じタグで写す） | 8 |
| `e2e/tests/block/machineGestures.spec.ts` | 機械入力スロットの空手右クリックで block_inventory.split を送る | `MachineGesturesTest`（`IBlockSlotCommands.RightDown(入力の番地)` が呼ばれる） | 9 |
| 同 | 機械出力スロットのダブルクリックで collect を送る | `MachineGesturesTest`（`DoubleClick(出力の番地)`） | 9 |
| 同 | 機械入力スロットの Shift+クリックで main へ配分移動する | `MachineGesturesTest`（`LeftDown(入力の番地, true)`）＋`MachineSlotRestrictionContributorTest` | 9 |
| `e2e/tests/block/machineRequiredCount.spec.ts` | 必要数を満たした素材スロットは入っている数/必要数を黒字で出し、個数バッジを重ねない | `MachineInventoryBodyTest` | 9 |
| 同 | 不足している素材スロットは数字だけ赤くし、入れたアイテムは減光しない | `MachineInventoryBodyTest` | 9 |
| 同 | 出力スロットには実物が入っていても必要数を出さない | `MachineInventoryBodyTest` | 9 |
| 同 | 必要数の数字の上で右クリックしても split を送る | `MachineInventoryBodyTest` | 9 |
| `e2e/tests/block/machineRecipe.spec.ts` | 選択済機械は大型パネルでヘッダ＋レシピ分スロット＋ゴーストを出し、タブを持たない | `MachineRecipeLayoutTest` | 12 |
| 同 | ヘッダクリックでレシピ選択モードへ戻り、行クリックでインベントリモードへ戻る | `MachineRecipeLayoutTest` | 12 |
| 同 | レシピ未選択の機械はレシピ選択モードで開く | `MachineRecipeLayoutTest` | 12 |
| 同 | レシピ無しブロックは小型パネルのまま | `MachineRecipeLayoutTest` | 12 |
| 同 | レシピが溢れても選択リストの視口高は変わらず、そこだけがスクロールする | `MachineRecipeLayoutTest` | 12 |
| 同 | フッタは選択モードとインベントリモードで同じ高さに出る | `MachineRecipeLayoutTest` | 12 |
| 同 | 行密度は控えめ設定どおりで、本文に3.8〜4.5行分が入る | `MachineRecipeLayoutTest` | 12 |
| 同 | レシピ選択行の液体はアイテムスロットと同寸の枠に収まり、量バッジを出す | `MachineRecipeLayoutTest` | 12 |
| 同 | 液体アイコンが404なら辞書の液体名を枠内に収めて出す | `MachineRecipeLayoutTest`（テクスチャ欠け＝`IGameContentTextureSource` が null） | 12 |
| 同 | 液体のみ出力のレシピを選ぶと選択中レシピ表示が液体スロットになる | `MachineRecipeLayoutTest` | 12 |
| `e2e/tests/block/cleanRoom/cleanRoomAirFilterPanel.spec.ts` | `清浄機パネル(${room.fixture})・高さ${windowHeight}で部屋欄の末尾とフィルタ枠へ届き、枠がホットバーへ潜らない` | `CleanRoomLayoutTest`（舞台 720 の全部屋。600/1080 は録画。D-15） | 11 |
| 同 | 清浄機2台・高さ720は自然高のままスクロールしない | `CleanRoomLayoutTest` | 11 |
| `e2e/tests/block/cleanRoom/cleanRoomMachineLayout.spec.ts` | 清浄機2台の室内機械で液体行が電力行・状態行に重ならない | `CleanRoomLayoutTest` | 11 |
| 同 | 低いウィンドウで清浄機が多い部屋でもフッタは見え、本文はスクロールで液体行へ届く | `CleanRoomLayoutTest` | 11 |
| `e2e/tests/block/cleanRoom/cleanRoomManyFilters.spec.ts` | `清浄機${room.filters}台・高さ${windowHeight}でも部屋欄ごと本文がスクロールし、レシピ・スロット・液体に届きフッタが読める` | `CleanRoomLayoutTest` | 11 |
| 同 | `清浄機20台・高さ${windowHeight}のレシピ選択でも部屋欄と同じ視口で最後のレシピ行へ届く` | `CleanRoomLayoutTest`（選択モード。Task 12 で足す） | 12 |
| 同 | `清浄機${room.filters}台・高さ720の境目でも本文とフッタが使える` | `CleanRoomLayoutTest` | 11 |
| 同 | 部屋の無い大型機械は部屋欄なしで本文に届き、フッタが読める | `CleanRoomLayoutTest` | 11 |
| `e2e/tests/block/fluidSlot.spec.ts` | tank の block inventory パネルに Fluid Tank が表示される | `FluidSlotTest` | 6 |
| 同 | 水の流体スロットが量 500 を表示する | `FluidSlotTest` | 6 |
| 同 | Water 名のツールチップが hover で表示される | `FluidSlotTest` | 6 |
| 同 | progress-arrow-bar のフィル幅が 50% になる | `FluidSlotTest` | 6 |
| 同 | 通常のe2eでは液体アイコンが404になり背面フィルが残る | `FluidSlotTest`（テクスチャ欠け） | 6 |
| `src/features/blockInventory/details/MachineSection.test.ts` | 未選択機械はレシピ選択リストを出し、インベントリ本体を出さない | `MachineSectionTest` | 12 |
| 同 | 選択済機械はヘッダ＋本体を出し、ヘッダのonChangeRecipeでリストへ戻り、selectedRecipeGuidが実際に変わるまで本体へ戻らない | `MachineSectionTest` | 12 |
| 同 | 同一レシピを選び直しても選択モードから戻れる（要求GUID一致で閉じる） | `MachineSectionTest` | 12 |
| 同 | レシピ0件の機械はヘッダもリストも出さず本体だけ出す | `MachineSectionTest` | 9 |
| 同 | 停止中は充足率テキストを出さず状態ラベルだけを見せる | `MachineSectionTest` | 9 |
| 同 | 歯車駆動の機械は充足率に不足赤を許さず、電力駆動の機械は許す（ADR 0056） | `MachineSectionTest` | 9 |
| 同 | インベントリ本文は大型面でだけスクローラに入る | `MachineSectionTest` | 9 |
| 同 | 出力空き不足の機械は状態ラベルを待機中のままにし、両モードのフッタで理由を不足トーンで出す（F01） | `MachineSectionTest`（インベントリモードは Task 9、選択モードの面は Task 12 で足す） | 9・12 |
| 同 | 出力空き不足でない機械は理由を出さない（入力切れの待機と区別するため理由は空き不足のときだけ） | `MachineSectionTest` | 9 |
| `src/features/blockInventory/details/machine/SelectedRecipeHeader.test.ts` | 代表が液体のときはFluidAmountSlotをバッジ非表示で描く | `SelectedRecipeHeaderTest` | 9 |
| 同 | 代表がアイテムのときはItemSlotを描く | `SelectedRecipeHeaderTest` | 9 |
| `src/features/blockInventory/details/machine/ModuleSlotStackLimitText.test.ts` | 上限の配信が届くまでは何も描かない | 捨てる（D-10） | 13 |
| 同 | 配信された上限を差し込んで描く | `ModuleSlotStackLimitTextTest` | 9 |
| `src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.test.ts` | レシピ名を出力アイテム名で出し、行クリックで選択通知する | `MachineRecipeSelectionRowTest` | 12 |
| 同 | 代表が液体のときは液体名で行名を出す | `MachineRecipeSelectionRowTest` | 12 |
| 同 | 入出力の液体はFluidAmountSlotへ量とtestIdを渡して描く | `MachineRecipeSelectionRowTest` | 12 |
| 同 | 上限つき継承レシピにはバッジを出す | `MachineRecipeSelectionRowTest` | 12 |
| 同 | 継承しないレシピにはバッジを出さない | `MachineRecipeSelectionRowTest` | 12 |
| `src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomSection.test.ts` | 密閉済みの部屋は見出し・空気・清浄機・停止理由の順に描く | `CleanRoomSectionTest` | 7 |
| 同 | 清浄機が無い部屋はその旨を赤で出す | `CleanRoomSectionTest` | 7 |
| 同 | 隙間がある部屋は件数の見出しだけを赤で出す | `CleanRoomSectionTest` | 7 |
| 同 | 取得できていない間は欄の名前と取得状態の1行だけを描き、部屋の値を出さない | `CleanRoomSectionTest` | 7 |
| `src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomYieldSection.test.ts` | EUV は層の失敗率の行だけを描く | `CleanRoomYieldSectionTest` | 7 |
| 同 | ダイシングは品質Tier・損失率・0でないレベル割合・失敗割合の行を描く | `CleanRoomYieldSectionTest` | 7 |
| 同 | 読めない応答の間は取得状態の1行だけを描き、率を出さない | `CleanRoomYieldSectionTest` | 7 |
| `src/features/blockInventory/details/machineInputStall/__tests__/MachineInputStallSection.test.ts` | レベル違いは品の名前を差し込んで赤字で描く | `MachineInputStallSectionTest` | 7 |
| 同 | 上限超えは製作上限の文言で描く | `MachineInputStallSectionTest` | 7 |
| 同 | 取得できていない間は赤字にせず取得状態の1行だけを描く | `MachineInputStallSectionTest` | 7 |
| `src/features/blockInventory/views/__tests__/SectionStackView.test.tsx` | %s の既存表示を保つ | `FluidSectionsTest`（Web の it.each 11 行〈Chest・ElectricMachine・GearMachine・ElectricGenerator・FuelGearGenerator・SimpleGearGenerator・ElectricMiner・GearMiner・CleanRoomMachine・CleanRoomAirFilter・unknown〉をそのまま TestCase にし、液体の行の id〈null を含む〉と `progress-arrow-bar` の数を主張。項目グリッドは alpha） | 6 |
| 同 | パイプは液体だけを進捗なしで描画する | `FluidSectionsTest` | 6 |
| 同 | 空のパイプは名前や容量を表示せず空スロットを残す | `FluidSectionsTest` | 6 |
| 同 | 部屋欄はセクション列の先頭に描く | `ContextSectionOrderTest`（中身。Context が項目グリッドより上は alpha） | 7 |
| 同 | 搬入停止欄は機械欄より上に赤字で描く | `ContextSectionOrderTest` | 9 |
| 同 | 抽選の中身欄は部屋欄のすぐ下に描く | `ContextSectionOrderTest` | 7 |
| 同 | 照会欄の無い payload では3欄とも描かず、取得中の欄は取得状態の行だけを描く | `ContextSectionOrderTest` | 7 |
| `e2e/tests/regression/sectionStack.spec.ts` | `${entry.type}は対応セクションだけを表示する` | `SectionStackMachineRegressionTest`（Web の `cases` のうち本レーンの id〈`machine-section`・`miner-section`・`generator-section`・`pump-section`・`pump-fluid-slots`・`machine-fluid-slots`・`generic-block-fluids`〉が出る entry〈tank・machine・gearMachine・miner・gearMiner・generator・generic・pump・gearPump〉を全部写し、`sectionIds` のうち本レーンの id で entry に無いものは出ないことも主張。歯車・網・項目グリッドの id は他レーン） | 10 |
| `src/features/blockInventory/blockInventoryDesign.test.ts` | %sから禁止Mantine部品とテーマ色を除去する | `MachineDesignTest`（本レーンの USS 4 本に色の直書きが無い） | 10 |
| 同 | ゲージとモード切替を用途別に共通部品へ置換する | `FamilyDesignTest`（発電機が `GaugeBar`・採掘機が `ProgressArrowBar`） | 8 |
| 同 | ゲージ系部品を寒色トークンで統一する | `FamilyDesignTest` | 8 |
| 同 | MachineSectionはレシピ選択リストとヘッダを往復し、電力率を共通フッタに置く | `MachineDesignTest` | 12 |
| 同 | レシピ有り機械だけviewer〜items占有の大型パネルへ広げる | `MachineDesignTest`（寄与の実装が真偽を返し、alpha の外殻が大型になる） | 10 |
| 同 | レシピ選択行は共有RecipeRowを流用し、静止矢印で列挙する | `MachineDesignTest` | 12 |
| 同 | 機械の加工行は進捗矢印を中央に固定する3カラムグリッドにする | `MachineDesignTest` | 9 |
| 同 | モジュールスロットをラベル付きで加工行から1段下げる | `MachineDesignTest` | 9 |
| 同 | ポンプの流体アイコンを非正方形でも歪ませずに収める | `FamilyDesignTest` | 8 |
| 同 | 稼働ラベルと分間レートの行を機械・採掘機・ポンプで共有する | `FamilyDesignTest`（alpha の `MachineStateRow`・`PerMinuteRateRow` を使う） | 8 |
| `e2e/mock-host/detailActions.test.ts` | setはrecipeGuidを選択状態へ反映する | 捨てる（D-11） | 13 |
| 同 | recipeGuidなしsetとclearは空GUIDへ戻す | 捨てる（D-11） | 13 |
| 同 | 対象machineがないblockと未知operationを拒否する | 捨てる（D-11） | 13 |

数: タグ付き NUnit 81 件（P 36・C 27・alpha から割り振り 18〈SectionStackView 7・regression 1・design 10〉）、捨てる 4 件、S4-special だけが写す 2 件（blockDetails）。L（純ロジック）は L0-s4b が写し済みで本レーンの未対応は 0。タグ無しの自前テスト（写し・Model・寄与・コマンド・選択モード・FluidPipe と ElectricPump の液体の行）は Task ごとに書く。

## File Structure

新規（すべて `moorestech_client/Assets/Scripts/` の下）:

State（`Client.Ui.State/BlockInventory/`）
- `Common/SectionContent.cs` — 区画の値の有無の union（`SectionContent<T>.Absent`・`Present(T)`）。null で「無し」を表さない
- `Machine/Detail/MachineDetail.cs` — 機械の詳細の値
- `Machine/Detail/MachineRecipeSelection.cs` — 選択レシピの union（未選択／選択中）と文字列からの解き方（L0-s4b D-27 の境界）
- `Machine/Detail/MachineStateTypeConversion.cs` — `idle`・`processing`・`halted` → `MachineProcessDisplayState`
- `Machine/Detail/MachineSlotBindingConversion.cs` — 選択レシピ × `IMachineParam` → `MachineBindingDetail`（Web ホスト `MachineSlotBindingDtoBuilder` の写し）
- `Machine/Detail/MachineDetailConversion.cs` — 状態詳細 2 つ＋param＋歯車駆動 → `MachineDetail`（Web ホスト `BlockDetailDtoBuilder` の機械部分の写し）
- `Machine/MachineSectionViewModel.cs` — 機械の区画が描く値（詳細・枠・液体の列・選択行・選択中の行と表示用レシピ・モジュールの上限）
- `Machine/MachineSectionViewModelBuilder.cs` — 純関数で ViewModel を組む
- `Machine/IMachineSectionModel.cs`・`Machine/MachineSectionModel.cs` — 追跡ブロック・パネル・レシピの源・上限を購読して配る（IInitializable）
- `Machine/RecipeMachineLargePanelRule.cs` — 拡張口 (5) の寄与
- `Machine/MachineSlotRestrictionContributor.cs` — 拡張口 (6) の寄与
- `Machine/Selection/MachineSelectionModeState.cs` — 選択モードの局所状態（Web `selectionOpened`・`requestedRecipeGuid`）
- `Machine/Selection/IMachineRecipeSelectCommand.cs`・`MachineRecipeSelectCommand.cs` — 選ぶ（解放の検査 → 送信）
- `Machine/Selection/IMachineRecipeSelectionSender.cs`・`VanillaMachineRecipeSelectionSender.cs` — 送信の外部境界
- `Family/MinerDetail.cs`・`PumpDetail.cs`・`GeneratorDetail.cs` — 採掘機・ポンプ（電気／歯車）・発電機の値
- `Family/FamilyDetailConversion.cs` — 採掘機・発電機の写し、`Family/PumpDetailConversion.cs` — ポンプの写し
- `Family/Models/IMinerDetailModel.cs`・`MinerDetailModel.cs`・`IPumpDetailModel.cs`・`PumpDetailModel.cs`・`IGeneratorDetailModel.cs`・`GeneratorDetailModel.cs` — 族ごとに 1 本の `Current` を配る（IInitializable。設計書 §4.1 の Model の型・`FakeModel<T>` の約束）
- `Fluid/BlockFluidSlotConversion.cs` — タンクとパイプ → `BlockFluidSlotState` の列
- `Fluid/BlockFluidSlotReading.cs` — 追跡ブロックの詳細とパイプの網の複製から液体の列を読む入口（Model とテストが共有）
- `Fluid/BlockFluidSlotsViewModel.cs`・`Fluid/IBlockFluidSlotsModel.cs`・`Fluid/BlockFluidSlotsModel.cs` — 液体の列と進捗を配る（パイプは複製の変化の通知を購読する。共有サービス）
- `Fluid/IFluidReplicaReader.cs`・`Fluid/FluidNetworkReplicaReader.cs` — パイプの流体複製の読み手の口と本番実装（`FluidNetworkClientCache` の節と変化の通知を読む。共有サービス。L1 は偽に差し替える）
- `S/Client.Game/InGame/FluidNetwork/FluidNetworkClientCache.cs`（変更）— 複製の変化の通知 `OnReplicaVersionChanged` を足す（`ApplyFullState`・`ApplyTickDiff` の直後。設計書 [A-57]・D-36）
- `Fluid/FluidRowVisibility.cs` — 液体の行の区画を出すかの判断（`Shows(BlockFluidSlotsViewModel fluids, bool isMachine)`。Presenter は呼ぶだけ）
- `Context/BlockContextEntries.cs` — 3 欄それぞれの「無し／未取得／取得済み」
- `Context/BlockContextViewModel.cs`・`Context/IBlockContextModel.cs`・`Context/BlockContextModel.cs` — 3 欄を配る
- `Context/CleanRoomViewConversion.cs` — 部屋の照会 → `CleanRoomFetchedDetail`、`Context/ContextViewConversion.cs` — 抽選・詰まりの照会の写し
- `Context/ContextSectionVisibility.cs` — 文脈の区画を出すかの判断（`Shows(BlockContextViewModel context, bool isMachine) => !isMachine && context.HasAny`。Presenter は呼ぶだけ。D-32）

Client.Ui（`Client.Ui/Screens/BlockInventory/`）
- `Machine/MachineSectionFactory.cs`・`MachineSectionPresenter.cs`・`MachineSectionView.cs`・`MachineSectionFooter.cs`・`MachineInventoryBody.cs`・`MachineProcessRow.cs`・`MachineModuleBand.cs`・`SelectedRecipeHeader.cs`・`MachineRecipeNames.cs`（代表の名前と行の名前）・`Machine.uss`
- `Machine/Slots/MachineSlotContents.cs`（`SlotItem`＋束縛 → `ItemSlotContent`）・`Machine/Slots/BlockSlotGestureCommands.cs`（枠の押下の種類 → `IBlockSlotCommands` の静的な写し。枠を持つ View が直接呼ぶ）
- `Machine/RecipeSelection/MachineRecipeSelectionList.cs`・`MachineRecipeSelectionRowView.cs`・`MachineRecipeRowContentBuilder.cs`・`MachineRecipeSelection.uss`
- `Context/ContextSectionFactory.cs`・`ContextSectionPresenter.cs`・`BlockContextSectionsView.cs`・`CleanRoomSectionView.cs`・`CleanRoomYieldSectionView.cs`・`MachineInputStallSectionView.cs`・`BlockInfoUnavailableLine.cs`
- `Family/IDetailSectionView.cs`・`DetailSectionPresenter.cs`・`MinerSectionView.cs`・`MinerSectionFactory.cs`・`PumpSectionView.cs`・`PumpSectionFactory.cs`・`GeneratorSectionView.cs`・`GeneratorSectionFactory.cs`・`Family.uss`
- `Fluid/FluidSlotContentMapper.cs`・`FluidRowView.cs`・`FluidRowSectionFactory.cs`・`FluidRowSectionPresenter.cs`・`FluidInventoryContents.cs`・`FluidDetailsSectionFactory.cs`・`FluidDetailsSectionPresenter.cs`・`NotApplicableSection.cs`・`Fluid.uss`

登録（`Client.Starter/Registration/GameUi/Screens/BlockInventoryMachineGameUiRegistration.cs`。E-1〈ad92a44777〉: W4 以降の画面レーンの登録は `GameUi/Screens/`。ディレクトリが無ければ本レーンが作る。D-34）

テスト（`Client.Tests/Ui/`）
- `State/BlockInventory/Machine/` は既存の 5 本（`MachineInputSlotBindingTest`・`MachineInputStallLogicTest`・`MachineRecipeSelectionLogicTest`・`MachineSlotGhostFixtures`・`MachineSlotViewTest`）があるので、新規は State 側と同じ切り方でサブディレクトリへ置く:
  - `State/BlockInventory/Machine/Detail/MachineStateTypeConversionTest.cs`・`MachineDetailConversionTest.cs`・`MachineSectionViewModelBuilderTest.cs`・`MachineTestDetails.cs`
  - `State/BlockInventory/Machine/Selection/MachineRecipeSelectionTest.cs`・`MachineSelectionModeStateTest.cs`・`MachineRecipeSelectCommandTest.cs`・`MachineTestDoubles.cs`（`FakeMachineSectionModel`・`RecordingRecipeSelectCommand`）
  - `State/BlockInventory/Machine/Rules/RecipeMachineLargePanelRuleTest.cs`・`MachineSlotRestrictionContributorTest.cs`
  - 名前空間はディレクトリに合わせる（`Client.Tests.Ui.State.BlockInventory.Machine.Detail` 等）。他のディレクトリの補助を使うファイルは `using` を足す。テストのフィルタはクラス名と `Client.Tests.Ui.State.BlockInventory` の前方一致なので変えない
- `State/BlockInventory/Family/FamilyDetailConversionTest.cs`・`BlockFluidSlotConversionTest.cs`・`BlockFluidSlotsModelTest.cs`・`FakeFamilyModels.cs`（`FakeMinerDetailModel`・`FakePumpDetailModel`・`FakeGeneratorDetailModel`・`FakeBlockFluidSlotsModel`。どれも `FakeModel<T>` から派生）
- `State/BlockInventory/Context/ContextViewConversionTest.cs`・`FakeBlockContextModel.cs`
- `Screens/BlockInventory/Machine/MachineSectionTest.cs`・`MachineInventoryBodyTest.cs`・`MachineGesturesTest.cs`・`SelectedRecipeHeaderTest.cs`・`ModuleSlotStackLimitTextTest.cs`・`BlockDetailsMachineTest.cs`・`MachineDesignTest.cs`・`SectionStackMachineRegressionTest.cs`・`SectionStackRegressionEntries.cs`（回帰の entry ごとの blockType と詳細）・`MachineSectionFixtures.cs`（偽 Model・記録係の `RecordingBlockSlotCommands`。`RecordingRecipeSelectCommand` は State 側の `MachineTestDoubles.cs` を使う）
- `Screens/BlockInventory/Machine/RecipeSelection/MachineRecipeSelectionRowTest.cs`・`MachineRecipeLayoutTest.cs`・`MachineRecipeSelectionFixtures.cs`（`Row`・`Recipe`）
- `Screens/BlockInventory/Context/CleanRoomSectionTest.cs`・`CleanRoomYieldSectionTest.cs`・`MachineInputStallSectionTest.cs`・`ContextSectionOrderTest.cs`・`CleanRoomLayoutTest.cs`
- `Screens/BlockInventory/Family/BlockDetailsFamilyTest.cs`・`FamilyDesignTest.cs`
- `Screens/BlockInventory/Fluid/FluidSlotTest.cs`・`FluidSectionsTest.cs`

録画: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-machine-ui.cs`

変更（共有区画への追記だけ）:
- `moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef` — 参照の末尾に `Game.Block`・`Game.Fluid`・`Game.Gear`
- `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs` — 共有サービスの並びの末尾に `IBlockFluidSlotsModel` の 1 行（S4-train も ctor で受ける。E-1・alpha D-22）、画面登録区画の末尾に 1 行
- `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` — `Screens` 区画の末尾に `@import` を 4 行（`Machine/Machine.uss`・`Machine/RecipeSelection/MachineRecipeSelection.uss`・`Family/Family.uss`・`Fluid/Fluid.uss`。`Context/` は USS を持たない）
- `moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs` — 列挙と 3 つの網羅 switch の末尾に良性の拒否 3 種（D-16。E-1）、`Localization/localization.csv` — その文言 3 行
- `docs/uitk/disposition.md`（捨てる・録画の行だけ）・`docs/uitk/TODO.md`（見た目の例外を使ったときだけ）— 末尾に追記

ディレクトリのファイル数（`.cs`・`.uss`）: State `BlockInventory/Common/` 1・`Machine/` 6＋`Detail/` 5＋`Selection/` 5・`Family/` 5＋`Models/` 6・`Fluid/` 8・`Context/` 7。Screens `Machine/` 10＋`Slots/` 2＋`RecipeSelection/` 4・`Context/` 7・`Family/` 9・`Fluid/` 9。テスト `State/BlockInventory/Machine/` 既存 5（新規は置かない）＋`Detail/` 4＋`Selection/` 4＋`Rules/` 2・`State/BlockInventory/Family/` 4・`State/BlockInventory/Context/` 2・`Screens/BlockInventory/Machine/` 10＋`RecipeSelection/` 3。すべて 10 以下（既存込み）。


## Tasks

### Task 1: 前提の確認と記録の準備

**Files:**
- Create: `LOGS/harness/uitk/S4-machine/done.md`（repo 外。`../moorestech_logs` の README を先に読む）

**Interfaces:**
- Consumes: `origin/feature/uitk`（S4-alpha・C4-a・C4-b・C3-a・C3-b・L0-s4a・L0-s4b）
- Produces: bd の issue・`done.md` の骨組み・alpha の実物との照合結果・integrator への依頼

- [ ] **Step 1: worktree と依存を確かめる**

Run:
```bash
pwd
git fetch origin && git log --oneline -1 origin/feature/uitk
ls moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackSlot.cs \
   moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Source/UnlockedMachineRecipeSource.cs \
   moorestech_client/Assets/Scripts/Client.Ui.State/SlotCommands/Block/IBlockSlotCommands.cs \
   moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Parts/Rows/MachineStateRow.cs
grep -rln "class FluidSlotRow\|class FluidAmountSlot\|FluidFace" moorestech_client/Assets/Scripts/Client.Ui | head
grep -rln "class RecipeRow\b\|class RecipeListScrollView\|class LevelInheritanceBadge\b" moorestech_client/Assets/Scripts/Client.Ui | head
grep -n "Game.UnlockState\|Game.Block\"\|Game.Fluid\|Game.Gear" moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef
ls moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/Screens/
moores-wt status
```
Expected: alpha の 4 ファイルがある、C4-a の 3 つが見つかる。C4-c の 3 つは無くてよい（Task 12 だけが要る）。alpha か C4-a が無ければ止めて指揮へ返す

- [ ] **Step 2: alpha の実物の口を確かめる**

Run:
```bash
grep -rn "interface IBlockInventoryPart\|interface IBlockInventorySection\|interface ISectionStackSectionFactory\|interface IBlockInventoryLargePanelRule\|interface IBlockSlotRestrictionContributor" moorestech_client/Assets/Scripts/Client.Ui moorestech_client/Assets/Scripts/Client.Ui.State
grep -rn "class RowSectionStackFluid\|class DetailsSectionStackFluid\|BlockInstanceId BlockInstanceId" moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory
grep -rn "static .* SectionStack(\|static .* Create(ScreenTestHost" moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory
```
Expected: Global Constraints の alpha の綴り（alpha 計画「## 後続レーンへの Produces 一覧」）と一致。違えば型名・引数だけを実物に合わせて書き（挙動は変えない）、差を `done.md`「未確認」に書く

- [ ] **Step 3: bd を作り、done.md の骨組みを書く**

Run: `bd create "UITK S4-machine: 機械・採掘機・ポンプ・発電機・文脈の3欄・液体の区画" -t task -p 1 --parent <uitk の epic>`（epic の id は `bd list --status open | grep -i uitk` で引く）→ `bd update <bd> --status in_progress`

`LOGS/harness/uitk/S4-machine/done.md`:
```markdown
状態: 作業中
# S4-machine（機械と汎用族）
計画: docs/superpowers/plans/2026-11-10-uitk-s4-machine.md / bd: <bd>
## 証拠
## 差の表
## 要裁定
## integrator への依頼
## 未確認
```

- [ ] **Step 4: integrator への依頼を先に出す**

`done.md`「## integrator への依頼」に次を書き、指揮へも同じ文を返す:
- `Client.Ui.State.asmdef` に `Game.Block`・`Game.Fluid`・`Game.Gear` を足す（S4-special も `Game.Gear` を要る見込み。衝突は両側を残す）
- 1280×720 の参照に無い `4-block-machineShortInput`・`4-block-machineFilledOutput`・清浄室 12 の撮影表への追加（本レーンは E-22 の形で自分で撮って比べる。撮影表に足すのは integrator）

- [ ] **Step 5: コミットは無し**（記録は repo 外）

---

### Task 2: 機械の詳細の写し（`Client.Ui.State/BlockInventory/Machine/Detail/`・`Common/`）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef`
- Create: `S/Client.Ui.State/BlockInventory/Common/SectionContent.cs`
- Create: `S/Client.Ui.State/BlockInventory/Machine/Detail/MachineRecipeSelection.cs`・`MachineStateTypeConversion.cs`・`MachineDetail.cs`・`MachineSlotBindingConversion.cs`・`MachineDetailConversion.cs`
- Test: `S/Client.Tests/Ui/State/BlockInventory/Machine/Selection/MachineRecipeSelectionTest.cs`、`S/Client.Tests/Ui/State/BlockInventory/Machine/Detail/MachineStateTypeConversionTest.cs`・`MachineDetailConversionTest.cs`・`MachineTestDetails.cs`

**Interfaces:**
- Consumes: L0-s4b（`MachineRecipeSelectionLogic`・`MachineBindingDetail`・`MachineItemSlotBinding`・`MachineTankBinding`・`MachineSlotLayout`・`RecipeOutputItem`・`MachineProcessDisplayState`）、サーバー共有型（`CommonMachineBlockStateDetail`・`MachineBlockStateDetail`・`IMachineParam`・`MachineRecipeSlotBindingUtil`・`VanillaMachineBlockStateConst`・`MasterHolder`）
- Produces: `SectionContent<T>`・`MachineRecipeSelection`（`Unselected`・`Selected(Guid)`・`TryParse`・`BuildRows`・`Matches`）・`MachineDetail`・`MachineDetailConversion.TryBuild`

- [ ] **Step 1: asmdef の参照を足す**

`Client.Ui.State.asmdef` の `references` の末尾（alpha の `Game.UnlockState` の後）に `"Game.Block"`・`"Game.Fluid"`・`"Game.Gear"` を足す。Run: `uloop compile --project-path ./moorestech_client` → Expected: `ErrorCount: 0`（循環参照が出たら止めて `done.md`「要裁定」へ。`Game.Block` は `Client.Game` も参照しているので循環は無い見込み）

- [ ] **Step 2: 失敗するテストを書く**

`MachineRecipeSelectionTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// サーバーの選択レシピの文字列を未選択／選択中の union に解く境界（L0-s4b D-27）
    /// The boundary that parses the server's selected-recipe string into the unselected/selected union (L0-s4b D-27)
    /// </summary>
    public class MachineRecipeSelectionTest
    {
        private static readonly Guid BlockGuid = new("00000000-0000-0000-0000-0000000000b1");
        private static readonly Guid RecipeA = new("00000000-0000-0000-0000-00000000000a");

        [Test]
        public void 空GUIDは未選択に解く()
        {
            Assert.IsTrue(MachineRecipeSelection.TryParse(Guid.Empty.ToString(), out var selection));
            Assert.IsInstanceOf<UnselectedMachineRecipe>(selection);
            Assert.IsFalse(selection.Matches(Guid.Empty));
        }

        [Test]
        public void GUIDは選択中に解き同じGUIDにだけ一致する()
        {
            Assert.IsTrue(MachineRecipeSelection.TryParse(RecipeA.ToString(), out var selection));
            Assert.AreEqual(RecipeA, ((SelectedMachineRecipe)selection).RecipeGuid);
            Assert.IsTrue(selection.Matches(RecipeA));
            Assert.IsFalse(selection.Matches(Guid.NewGuid()));
        }

        [Test]
        public void 読めない文字列は偽を返す()
        {
            Assert.IsFalse(MachineRecipeSelection.TryParse("not-a-guid", out _));
            Assert.IsFalse(MachineRecipeSelection.TryParse(null, out _));
        }

        [Test]
        public void 選択中だけが行に選択の印を付ける()
        {
            var recipes = new List<MachineRecipeSummary> { new(RecipeA, BlockGuid, new[] { new RecipeOutputItem(new ItemId(1), 1) }, Array.Empty<RecipeOutputFluid>()) };
            Assert.IsFalse(MachineRecipeSelection.Unselected.BuildRows(recipes, BlockGuid)[0].Selected);
            Assert.IsTrue(MachineRecipeSelection.Selected(RecipeA).BuildRows(recipes, BlockGuid)[0].Selected);
        }
    }
}
```

`MachineStateTypeConversionTest.cs`（3 つの定数がそれぞれ写る・`"unknown"` は偽）と、`MachineDetailConversionTest.cs`（`MachineTestDetails` の状態詳細で: 選択中なら束縛が入力 i＝素材 i・出力 j＝`InputSlotCount + j`・タンク出力 j＝`InputTankCount + j`／未選択は束縛 0 件で `Layout` だけ／加工中レシピが無い〈空 GUID〉と秒 0・出力 0 件／選択レシピの文字列が読めないと偽で Error ログ〈`LogAssert.Expect(LogType.Error, new Regex("SelectedRecipeGuid"))`〉／状態の文字列が読めないと偽で Error ログ）を同じ形で書く。マスタのレシピは `MasterHolder.MachineRecipesMaster` の先頭の要素を使う（Client.Tests の既存のマスタ読み込みの前例に従う。`grep -rn "MasterHolder.Load\|MasterHolderTestLoader" moorestech_client/Assets/Scripts/Client.Tests | head` で引く）。`MachineTestDetails.cs` は `CommonMachineBlockStateDetail`・`MachineBlockStateDetail` の組み立てと、試験用の `IMachineParam` の偽物（`InputSlotCount` 等を ctor で受ける）を持つ

- [ ] **Step 3: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 型が無いコンパイルエラー

- [ ] **Step 4: 実装する**

`SectionContent.cs`:
```csharp
namespace Client.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 区画の値の有無。無しを null で表さず、出し入れはこの真偽で決める
    /// Whether a section has a value; absence is never null, and showing follows this flag
    /// </summary>
    public readonly struct SectionContent<T>
    {
        public readonly bool IsPresent;
        public readonly T Value;

        private SectionContent(bool isPresent, T value)
        {
            IsPresent = isPresent;
            Value = value;
        }

        public static SectionContent<T> Absent => new(false, default);
        public static SectionContent<T> Present(T value) => new(true, value);
    }
}
```

`MachineRecipeSelection.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;

namespace Client.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 機械の選択レシピ。サーバーの空 GUID は未選択を意味し、文字列からはここ 1 か所で解く（L0-s4b D-27）
    /// A machine's selected recipe; the server's empty GUID means unselected, parsed from the wire string only here (L0-s4b D-27)
    /// </summary>
    public abstract class MachineRecipeSelection
    {
        public static readonly MachineRecipeSelection Unselected = new UnselectedMachineRecipe();
        public static MachineRecipeSelection Selected(Guid recipeGuid) => new SelectedMachineRecipe(recipeGuid);

        // 読めない文字列は偽。呼び手が fail-closed でログを出す
        // An unreadable string returns false; the caller logs and fails closed
        public static bool TryParse(string wire, out MachineRecipeSelection selection)
        {
            selection = Unselected;
            if (!Guid.TryParse(wire, out var guid)) return false;
            selection = guid == Guid.Empty ? Unselected : Selected(guid);
            return true;
        }

        public abstract List<MachineRecipeSelectionRow> BuildRows(IReadOnlyList<MachineRecipeSummary> recipes, Guid blockGuid);
        public abstract bool Matches(Guid recipeGuid);
    }

    public sealed class UnselectedMachineRecipe : MachineRecipeSelection
    {
        public override List<MachineRecipeSelectionRow> BuildRows(IReadOnlyList<MachineRecipeSummary> recipes, Guid blockGuid) => MachineRecipeSelectionLogic.BuildRows(recipes, blockGuid);
        public override bool Matches(Guid recipeGuid) => false;
    }

    public sealed class SelectedMachineRecipe : MachineRecipeSelection
    {
        public Guid RecipeGuid { get; }

        public SelectedMachineRecipe(Guid recipeGuid)
        {
            RecipeGuid = recipeGuid;
        }

        public override List<MachineRecipeSelectionRow> BuildRows(IReadOnlyList<MachineRecipeSummary> recipes, Guid blockGuid) => MachineRecipeSelectionLogic.BuildRows(recipes, blockGuid, RecipeGuid);
        public override bool Matches(Guid recipeGuid) => recipeGuid == RecipeGuid;
    }
}
```

`MachineStateTypeConversion.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.Detail;
using Game.Block.Blocks.Machine;

namespace Client.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// サーバーの機械の状態文字列を表示の状態へ写す（Web ホストは camelCase の文字列で渡していた）
    /// Maps the server's machine state string onto the display state (the Web host passed it as a camelCase string)
    /// </summary>
    public static class MachineStateTypeConversion
    {
        public static bool TryConvert(string stateType, out MachineProcessDisplayState state)
        {
            switch (stateType)
            {
                case VanillaMachineBlockStateConst.IdleState:
                    state = MachineProcessDisplayState.Idle;
                    return true;
                case VanillaMachineBlockStateConst.ProcessingState:
                    state = MachineProcessDisplayState.Processing;
                    return true;
                case VanillaMachineBlockStateConst.HaltedState:
                    state = MachineProcessDisplayState.Halted;
                    return true;
                default:
                    state = MachineProcessDisplayState.Idle;
                    return false;
            }
        }
    }
}
```

`MachineDetail.cs`（すべて `{ get; }` と ctor。項目: `MachineRecipeSelection Selection`・`MachineProcessDisplayState State`・`double CurrentPower`・`double RequestPower`・`float Progress`・`bool OutputSpaceShort`・`bool GearDriven`・`double RunningRecipeTimeSeconds`・`IReadOnlyList<RecipeOutputItem> RunningRecipeOutputs`・`MachineBindingDetail Binding`。ctor は同じ順の 10 引数）

`MachineSlotBindingConversion.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Core.Master;
using Game.Block.Blocks.Machine.RecipeSelection;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 選択レシピの束縛を組む。サーバーの束縛規則（入力 i＝素材 i・出力 j・タンク i）を Web ホストと同じく写す
    /// Builds the selected recipe's binding, mirroring the server rule (input i = input i, output j, tank i) like the Web host
    /// </summary>
    public static class MachineSlotBindingConversion
    {
        public static MachineBindingDetail Build(MachineRecipeMasterElement selectedRecipe, IMachineParam param)
        {
            var layout = new MachineSlotLayout(param.InputSlotCount, param.OutputSlotCount, param.ModuleSlotCount);
            var slots = new List<MachineItemSlotBinding>();
            var tanks = new List<MachineTankBinding>();
            // silent-ok: 未選択の機械は束縛を持たない（Web ホストと同じ） / ゴーストが出ないことを MachineInventoryBodyTest が見る
            if (selectedRecipe == null) return new MachineBindingDetail(layout, slots, tanks);

            // 入力は受け付け ID をサーバーと同じ util で引き、出力は InputSlotCount から詰める
            // Inputs take accepted IDs from the server's util; outputs start at InputSlotCount
            for (var i = 0; i < selectedRecipe.InputItems.Length; i++) slots.Add(ItemBinding(i, selectedRecipe.InputItems[i].ItemGuid, selectedRecipe.InputItems[i].Count, MachineRecipeSlotBindingUtil.GetAcceptedInputItemIds(selectedRecipe, i)));
            for (var j = 0; j < selectedRecipe.OutputItems.Length; j++) slots.Add(ItemBinding(param.InputSlotCount + j, selectedRecipe.OutputItems[j].ItemGuid, selectedRecipe.OutputItems[j].Count, MachineRecipeSlotBindingUtil.GetAcceptedOutputItemIds(selectedRecipe, j)));

            // タンクは入力 i、出力は InputTankCount から
            // Tanks: input i, outputs from InputTankCount
            for (var i = 0; i < selectedRecipe.InputFluids.Length; i++) tanks.Add(new MachineTankBinding(i, MasterHolder.FluidMaster.GetFluidId(selectedRecipe.InputFluids[i].FluidGuid), selectedRecipe.InputFluids[i].Amount));
            for (var j = 0; j < selectedRecipe.OutputFluids.Length; j++) tanks.Add(new MachineTankBinding(param.InputTankCount + j, MasterHolder.FluidMaster.GetFluidId(selectedRecipe.OutputFluids[j].FluidGuid), selectedRecipe.OutputFluids[j].Amount));
            return new MachineBindingDetail(layout, slots, tanks);

            #region Internal

            MachineItemSlotBinding ItemBinding(int slot, Guid itemGuid, int count, IReadOnlyList<ItemId> accepted)
            {
                return new MachineItemSlotBinding(slot, MasterHolder.ItemMaster.GetItemId(itemGuid), count, accepted);
            }

            #endregion
        }
    }
}
```

`MachineDetailConversion.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 機械の 2 つの状態詳細と param から詳細を組む（Web ホスト BlockDetailDtoBuilder の機械部分の写し）
    /// Builds the machine detail from its two state details and param (port of the machine part of the Web host's BlockDetailDtoBuilder)
    /// </summary>
    public static class MachineDetailConversion
    {
        public static bool TryBuild(CommonMachineBlockStateDetail common, MachineBlockStateDetail machine, IMachineParam param, bool gearDriven, out MachineDetail detail)
        {
            detail = null;
            // 読めない値は機械の区画ごと出さず、開発者ログへ出す
            // Unreadable values drop the whole machine section and go to the developer log
            if (!MachineRecipeSelection.TryParse(machine.SelectedRecipeGuid, out var selection))
            {
                Debug.LogError($"[MachineDetailConversion] unreadable SelectedRecipeGuid: '{machine.SelectedRecipeGuid}'");
                return false;
            }
            if (!Guid.TryParse(machine.MachineRecipeGuid, out var runningGuid))
            {
                Debug.LogError($"[MachineDetailConversion] unreadable MachineRecipeGuid: '{machine.MachineRecipeGuid}'");
                return false;
            }
            if (!MachineStateTypeConversion.TryConvert(common.CurrentStateType, out var state))
            {
                Debug.LogError($"[MachineDetailConversion] unknown CurrentStateType: '{common.CurrentStateType}'");
                return false;
            }

            // 束縛は選択レシピ、秒と出力は加工中レシピから引く（Web ホストと同じ）
            // Binding follows the selected recipe; time and outputs follow the running recipe (same as the Web host)
            var running = MasterHolder.MachineRecipesMaster.GetRecipeElement(runningGuid);
            var selected = selection is SelectedMachineRecipe s ? MasterHolder.MachineRecipesMaster.GetRecipeElement(s.RecipeGuid) : null;
            var binding = MachineSlotBindingConversion.Build(selected, param);
            detail = new MachineDetail(selection, state, common.CurrentPower, common.RequestPower, machine.ProcessingRate, machine.IsOutputSpaceShort, gearDriven, RunningTime(running), RunningOutputs(running), binding);
            return true;

            #region Internal

            // silent-ok: 加工中レシピが無い（空 GUID）機械は秒 0・出力なしで、分間生産数を出さない（Web と同じ） / BlockDetailsMachineTest が見る
            static double RunningTime(MachineRecipeMasterElement recipe) => recipe == null ? 0 : recipe.Time;

            static IReadOnlyList<RecipeOutputItem> RunningOutputs(MachineRecipeMasterElement recipe)
            {
                var outputs = new List<RecipeOutputItem>();
                if (recipe == null) return outputs;
                foreach (var output in recipe.OutputItems) outputs.Add(new RecipeOutputItem(MasterHolder.ItemMaster.GetItemId(output.ItemGuid), output.Count));
                return outputs;
            }

            #endregion
        }
    }
}
```

- [ ] **Step 5: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.BlockInventory.Machine"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Machine
git commit -m "feat(uitk): 機械の詳細を状態詳細から組み、未選択を union で受ける（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 3: 採掘機・ポンプ・発電機・液体の写しと Model（`Family/`・`Fluid/`）

**Files:**
- Create: `S/Client.Ui.State/BlockInventory/Family/MinerDetail.cs`・`PumpDetail.cs`・`GeneratorDetail.cs`・`FamilyDetailConversion.cs`・`PumpDetailConversion.cs`
- Create: `S/Client.Ui.State/BlockInventory/Family/Models/IMinerDetailModel.cs`・`MinerDetailModel.cs`・`IPumpDetailModel.cs`・`PumpDetailModel.cs`・`IGeneratorDetailModel.cs`・`GeneratorDetailModel.cs`
- Create: `S/Client.Ui.State/BlockInventory/Fluid/BlockFluidSlotConversion.cs`・`BlockFluidSlotReading.cs`・`BlockFluidSlotsViewModel.cs`・`IBlockFluidSlotsModel.cs`・`BlockFluidSlotsModel.cs`・`FluidRowVisibility.cs`・`IFluidReplicaReader.cs`・`FluidNetworkReplicaReader.cs`
- Modify: `S/Client.Game/InGame/FluidNetwork/FluidNetworkClientCache.cs`（変化の通知 `OnReplicaVersionChanged` を足す。D-36）
- Test: `S/Client.Tests/Ui/State/BlockInventory/Family/FamilyDetailConversionTest.cs`・`BlockFluidSlotConversionTest.cs`・`BlockFluidSlotsModelTest.cs`・`FakeFamilyModels.cs`、`S/Client.Tests/FluidNetwork/FluidNetworkClientCacheNotificationTest.cs`

**Interfaces:**
- Consumes: `OpenBlockDetailTracker`（`TrackedBlock`・`OnTrackedBlockChanged`・`OnTrackedBlockStateChanged`）、`FluidNetworkClientCache`、`BlockFluidSlotState`、Task 2 の `SectionContent<T>`
- Produces:
```csharp
namespace Client.Ui.State.BlockInventory.Family
public sealed class MinerMiningItem { ItemId ItemId; double ItemsPerMinute; }
public sealed class MinerDetail { double CurrentPower; double RequestPower; float Progress; bool GearDriven; IReadOnlyList<MinerMiningItem> MiningItems; }
public abstract class PumpDetail { IReadOnlyList<PumpingFluid> PumpingFluids; }   // ElectricPumpDetail{ State; CurrentPower; RequestPower } | GearPumpDetail
public sealed class PumpingFluid { FluidId FluidId; double AmountPerMinute; }
public sealed class GeneratorDetail { double RemainingFuelTime; double CurrentFuelTime; double OperatingRate; }
namespace Client.Ui.State.BlockInventory.Family.Models   // 族ごとに 1 本（設計書 §4.1・FakeModel<T> の remarks「値を並べない」）
public interface IMinerDetailModel { IReadOnlyReactiveProperty<SectionContent<MinerDetail>> Current { get; } }
public interface IPumpDetailModel { IReadOnlyReactiveProperty<SectionContent<PumpDetail>> Current { get; } }
public interface IGeneratorDetailModel { IReadOnlyReactiveProperty<SectionContent<GeneratorDetail>> Current { get; } }
namespace Client.Ui.State.BlockInventory.Fluid
public sealed class BlockFluidSlotsViewModel { IReadOnlyList<BlockFluidSlotState> Slots; bool HasProgress; float Progress; }
public interface IBlockFluidSlotsModel { IReadOnlyReactiveProperty<BlockFluidSlotsViewModel> Current { get; } }   // 追跡ブロック（タンク・液体プラットフォーム・パイプ）の液体。閉・貨車では空の列・進捗なし
public static class FluidRowVisibility { public static bool Shows(BlockFluidSlotsViewModel fluids, bool isMachine); }   // 機械でなく 1 件以上
public static class BlockFluidSlotConversion { static IReadOnlyList<BlockFluidSlotState> FromTanks(IReadOnlyList<FluidMessagePack> inputTanks, IReadOnlyList<FluidMessagePack> outputTanks); static BlockFluidSlotState FromPipeNode(int fluidId, double amount, double capacity); }
```

- [ ] **Step 1: 失敗するテストを書く**

`FamilyDetailConversionTest.cs`:
- 採掘機: `MiningSeconds = 2` と採掘中 1 品 → `ItemsPerMinute == 30`、`MineSettings` の他の品は出ない、`MiningSeconds <= 0` は 0 件。`MinerDetailModel` は採掘機の詳細・common・`IMinerParam` の 3 つが揃わないと `Absent`（Web ホスト `MinerDetailDtoBuilder.cs:20` のガード）
- ポンプ: `ElectricPumpBlockParam` → `ElectricPumpDetail`（状態・電力つき、`AmountPerSecond 1.5` → 毎分 90）、`GearPumpBlockParam` → `GearPumpDetail`、他の param → 偽で Error ログ（Web ホストは例外。UITK は購読の中で投げず Error ログで区画を出さない。D-6）、電気ポンプで common が無い → 偽（黙る。Web ホストと同じ。`silent-ok`）
- 発電機: 3 つの値がそのまま写る

`BlockFluidSlotConversionTest.cs`:
- 入力 2 → 出力 1 の順で 3 件、空の液体（`FluidMaster.EmptyFluidId`）は `Empty(capacity)`、中身ありは `Filled(id, amount, capacity)`
- パイプの節は `FromPipeNode(空の id, 0, 100)` が `Empty(100)`

`BlockFluidSlotsModelTest.cs`（追跡と複製は偽の読み手〈`IFluidReplicaReader { IObservable<Unit> OnReplicaChanged { get; } bool TryReadNode(Vector3Int pos, out int fluidId, out double amount, out double capacity); }` を `BlockFluidSlotReading` の入口に挟む。本番は `FluidNetworkClientCache` の節と `OnReplicaVersionChanged` を読む実装。偽は手で流す `Subject<Unit>` を持つ〉で）:
- 「複製の量が変わるとパイプの行が描き直される」: パイプを追跡 → 量 40 → 通知 → `Current` の量 40、量 60 → 通知 → 60、変わらないまま通知 → `Current` は流れない
- 液体プラットフォーム（`FluidMachineInventoryStateDetail` を持つブロック）は入力→出力の列を配る
- 貨車（追跡ブロックなし）と閉は空の列・進捗なし
- パイプでないブロックを追跡している間は通知を購読しない（通知を流しても `Current` が流れない）

`S/Client.Tests/FluidNetwork/FluidNetworkClientCacheNotificationTest.cs`（`FluidNetworkClientCache` を直に作る。全量は `FluidSyncPacketTestUtil` 等の既存の組み立てを `FluidNetworkEventNetworkHandlerTest` から借りる）:
- 「全量の適用と差分の適用のたびに版の通知が 1 回流れる」: `ApplyFullState` → 通知 1 回（値は `ReplicaVersion`）、`ApplyTickDiff` → 2 回目、通知の値が `ReplicaVersion` と一致

`FluidRowVisibility`（機械なら偽・0 件なら偽・他は真）は `BlockFluidSlotsModelTest` に 1 件で足す

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → Expected: 型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`FamilyDetailConversion.cs`:
```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 採掘機と発電機の状態詳細を値へ写す（Web ホスト MinerDetailDtoBuilder・BlockDetailDtoBuilder の発電機部分の写し）
    /// Maps miner and generator state details onto values (port of the Web host's MinerDetailDtoBuilder and the generator part of BlockDetailDtoBuilder)
    /// </summary>
    public static class FamilyDetailConversion
    {
        public static MinerDetail Miner(CommonMinerBlockStateDetail miner, CommonMachineBlockStateDetail common, IMinerParam param, bool gearDriven)
        {
            // uGUI と同じく 60/秒で毎分に直し、いま採掘中の品だけを並べる
            // Like uGUI, convert per-second to per-minute and list only items currently mined
            var items = new List<MinerMiningItem>();
            var currentIds = miner.GetCurrentMiningItemIds();
            if (0 < miner.MiningSeconds)
            {
                foreach (var settings in param.MineSettings.items)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(settings.ItemGuid);
                    if (currentIds.Contains(itemId)) items.Add(new MinerMiningItem(itemId, 60 / miner.MiningSeconds));
                }
            }
            return new MinerDetail(common.CurrentPower, common.RequestPower, common.ProcessingRate, gearDriven, items);
        }

        public static GeneratorDetail Generator(PowerGeneratorStateDetail generator)
        {
            return new GeneratorDetail(generator.RemainingFuelTime, generator.CurrentFuelTime, generator.OperatingRate);
        }
    }
}
```

`PumpDetailConversion.cs`（`TryBuild(PumpBlockStateDetail pump, IBlockParam param, CommonMachineBlockStateDetail common, out PumpDetail detail)`。`switch (param)`: `ElectricPumpBlockParam` は common が無ければ `silent-ok: 油井の電力値は CommonMachine だけが持ち、揃うまで出さない（Web ホストと同じ） / 揃えば状態変化の購読で出る` で偽、あれば `MachineStateTypeConversion.TryConvert` が偽なら Error ログで偽、真なら `new ElectricPumpDetail(state, common.CurrentPower, common.RequestPower, Fluids())`／`GearPumpBlockParam` は `new GearPumpDetail(Fluids())`／`default` は `Debug.LogError($"[PumpDetailConversion] unsupported pump param: {param.GetType().Name}")` で偽。`Fluids()` はローカル関数で `pump.PumpingFluids` を `new PumpingFluid(new FluidId(p.FluidId), p.AmountPerSecond * 60)` に写す）

`BlockFluidSlotConversion.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Core.Master;
using Game.Block.Interface.State;
using Game.Fluid;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の列を組む。タンクは入力→出力の順、パイプは流体複製の節 1 つ（Web ホスト FluidSlotDtoBuilder の写し）
    /// Builds the fluid list: tanks input-then-output, a pipe as one replica node (port of the Web host's FluidSlotDtoBuilder)
    /// </summary>
    public static class BlockFluidSlotConversion
    {
        public static IReadOnlyList<BlockFluidSlotState> FromTanks(IReadOnlyList<FluidMessagePack> inputTanks, IReadOnlyList<FluidMessagePack> outputTanks)
        {
            var slots = new List<BlockFluidSlotState>();
            foreach (var tank in inputTanks) slots.Add(ToSlot(tank.FluidId, tank.Amount, tank.MaxCapacity));
            foreach (var tank in outputTanks) slots.Add(ToSlot(tank.FluidId, tank.Amount, tank.MaxCapacity));
            return slots;
        }

        public static BlockFluidSlotState FromPipeNode(int fluidId, double amount, double capacity) => ToSlot(fluidId, amount, capacity);

        // 空の液体は Empty（uGUI MachineBlockInventoryView の EmptyFluidId 分岐と同じ）
        // The empty fluid becomes Empty (same as uGUI MachineBlockInventoryView's EmptyFluidId branch)
        private static BlockFluidSlotState ToSlot(int fluidId, double amount, double capacity)
        {
            var id = new FluidId(fluidId);
            return id == FluidMaster.EmptyFluidId ? BlockFluidSlotState.Empty(capacity) : BlockFluidSlotState.Filled(id, amount, capacity);
        }
    }
}
```

`MinerDetailModel.cs`・`PumpDetailModel.cs`・`GeneratorDetailModel.cs`（どれも IInitializable。ctor `(OpenBlockDetailTracker tracker)`。`Initialize` で `OnTrackedBlockChanged`・`OnTrackedBlockStateChanged` を購読し `Rebuild()`。`tracker.TrackedBlock` が null なら `Absent`〈`silent-ok: 追跡ブロックが無いとき区画は無い（閉と同じ） / 追跡替えの購読で直ちに組み直す`〉。ReactiveProperty の同値の再発行は UniRx の既定〈参照比較〉に任せる）:
- 採掘機: `CommonMinerBlockStateDetail`（`…BlockStateDetailKey`）・`CommonMachineBlockStateDetail`・`BlockParam is IMinerParam` の 3 つが揃うときだけ `FamilyDetailConversion.Miner(...)`、揃わなければ `Absent`（`silent-ok: 採掘機でない・状態詳細が揃う前は区画が無い（Web ホストと同じ） / 状態詳細の更新で組み直す`）。歯車駆動は `block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey) != null`（D-7）
- ポンプ: `PumpBlockStateDetail` があれば `PumpDetailConversion.TryBuild(pump, param, common, out detail)`
- 発電機: `PowerGeneratorStateDetail`（鍵は `PowerGeneratorStateDetail.StateDetailKey`。他の詳細の `BlockStateDetailKey` と名前が違う）があれば `FamilyDetailConversion.Generator`

`FluidNetworkClientCache.cs`（変更。`private readonly Subject<int> _replicaVersionChanged = new();`・`public IObservable<int> OnReplicaVersionChanged => _replicaVersionChanged;` を足し、`ApplyFullState`・`ApplyTickDiff` で `ReplicaVersion++` の直後に `_replicaVersionChanged.OnNext(ReplicaVersion)`。`ReplicaVersion` の注記「表示側は購読でなく…」は「パイプの水位の表示は描画更新の順番の中で版を見る。UI は `OnReplicaVersionChanged` を購読する」に直す。設計書 [A-57]・D-36）

`FluidNetworkReplicaReader.cs`（`IFluidReplicaReader` の本番。`OnReplicaChanged => _cache.OnReplicaVersionChanged.AsUnitObservable()`、`TryReadNode` は `TryGetNodeIndex` と `Replica.Network` の節を `FluidUnits.ToAmount` して返す）

`BlockFluidSlotsModel.cs`（IInitializable。ctor `(OpenBlockDetailTracker tracker, IFluidReplicaReader replica)`。登録は `MainGameUiRegistration` の共有サービスの並び〈S4-train も ctor で受ける。E-1〉）:
- 追跡の 2 つの購読で `Rebuild()`: `BlockFluidSlotReading.Read(block, replica)`＝`FluidMachineInventoryStateDetail` があれば `FromTanks`（機械・タンク・液体プラットフォーム）、`replica.TryReadNode(block.BlockPosInfo.OriginalPos, ...)` が真なら `FromPipeNode`（量と容量は `FluidUnits.ToAmount` 済みの値）を足す。進捗は `MachineBlockStateDetail.ProcessingRate`、無ければ採掘機の `CommonMachineBlockStateDetail.ProcessingRate`（Web の `dto.Progress` を立てるのは機械と採掘機だけ）、どちらも無ければ `HasProgress = false`
- パイプ（複製に節がある）を追跡している間だけ `replica.OnReplicaChanged` を購読し、節の液体 id と量が前回と違う回だけ `Rebuild()`（毎 tick の通知を、変わった回だけに絞る。Web ホスト `BlockInventoryRepublishPolicy.HasPipeContentsChanged` と同じ比べ方）。追跡替えで購読を張り替える（設計書 §4.2 [A-57]・D-20・D-36）
- 閉・貨車（追跡ブロックなし）は空の列・進捗なし

`FluidRowVisibility.cs`（`Shows(fluids, isMachine) => !isMachine && 0 < fluids.Slots.Count`。機械は本体の中に液体の行を持つ）

`FakeFamilyModels.cs`（テスト用。`FakeMinerDetailModel : FakeModel<SectionContent<MinerDetail>>, IMinerDetailModel`・`FakePumpDetailModel`・`FakeGeneratorDetailModel`・`FakeBlockFluidSlotsModel : FakeModel<BlockFluidSlotsViewModel>, IBlockFluidSlotsModel`。`Set` で値を差し替える）

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.BlockInventory.Family"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Family
git commit -m "feat(uitk): 採掘機・ポンプ・発電機・液体の列を状態詳細から組む Model を足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 4: 文脈の 3 欄の Model（`Client.Ui.State/BlockInventory/Context/`）

**Files:**
- Create: `S/Client.Ui.State/BlockInventory/Context/BlockContextEntries.cs`・`BlockContextViewModel.cs`・`IBlockContextModel.cs`・`BlockContextModel.cs`・`CleanRoomViewConversion.cs`・`ContextViewConversion.cs`・`ContextSectionVisibility.cs`
- Test: `S/Client.Tests/Ui/State/BlockInventory/Context/ContextViewConversionTest.cs`・`FakeBlockContextModel.cs`

**Interfaces:**
- Consumes: `BlockNetworkInfoCache`（`CleanRoomTarget`・`IsTrackingMachineInputStall`・`OnUpdated`・`GetCleanRoom`・`GetCleanRoomYield`・`GetMachineInputStall`）、`OpenBlockDetailTracker`、サーバーの照会型（`CleanRoomEnclosedView`・`CleanRoomNotEnclosedView`〈`CleanRoomOpeningsLeakView`・`CleanRoomNoEnclosureLeakView`・`CleanRoomTooLargeLeakView`・`CleanRoomDetectionPendingLeakView`〉・`CleanRoomYieldLayerExposureView`・`CleanRoomYieldDicingView`・`CleanRoomYieldNoRoleView`・`CleanRoomYieldNotOperableView`・`MachineInputStalledView`・`MachineInputNotStalledView`）、`CleanRoomOperabilityStateDetail`、L0-s4b の入力型
- Produces:
```csharp
namespace Client.Ui.State.BlockInventory.Context
public abstract class ContextEntry<T>   // 無し / 未取得 / 取得済み
{
    public static readonly ContextEntry<T> Absent;
    public static ContextEntry<T> Unavailable(BlockInfoFetchState fetchState);
    public static ContextEntry<T> Fetched(T value);
    public bool IsAbsent { get; }   // this is AbsentContextEntry<T>（View と HasAny が同じ判定を 1 本で使う）
}
public sealed class AbsentContextEntry<T> : ContextEntry<T> { }
public sealed class UnavailableContextEntry<T> : ContextEntry<T> { public BlockInfoFetchState FetchState { get; } }
public sealed class FetchedContextEntry<T> : ContextEntry<T> { public T Value { get; } }
public sealed class MachineInputStall { MachineInputStallReasonKind Reason; ItemId WaitingItemId; }
public sealed class BlockContextViewModel
{
    public static readonly BlockContextViewModel None;
    ContextEntry<CleanRoomFetchedDetail> CleanRoom; ContextEntry<CleanRoomYieldFetched> CleanRoomYield; ContextEntry<MachineInputStall> MachineInputStall;
    bool HasAny { get; }  // 3 欄のどれかが Absent でない（Web SectionStackView の hasContextSections と同形。機械以外の区画を出す条件。D-12）
}
public interface IBlockContextModel { IReadOnlyReactiveProperty<BlockContextViewModel> Current { get; } }
```

- [ ] **Step 1: 失敗するテストを書く**

`ContextViewConversionTest.cs`（サーバーの照会型は ctor で組める）:
- 部屋: `NotFetched` → `Unavailable(NotFetched)`／密閉（`CleanRoomSummaryMessagePack` の `TryGetClass` が真）→ `Enclosed(Class(name), conc, acr, filters)`、偽 → `ClassOut`／隙間 → `Openings(n)`／囲いなし → `NoEnclosure`／大きすぎ → `TooLarge(max)`／検出待ち → `Detecting`。停止理由は室内機械の `CleanRoomOperability`（NotEnclosed→`NotEnclosed`・ClassOut→`ClassOut`・Operable→`None`）、清浄機は `None`
- 室内機械・未取得で `CleanRoomOperabilityStateDetail` が無い → `Unavailable(NotFetched)`（Web ホストは取得状態を先に見る）。室内機械・取得済みで `CleanRoomOperabilityStateDetail` が無い → `Absent`（Web ホストと同じ。`silent-ok`）、列挙外の値 → `Absent` と Warning ログ（Web ホストと同じ文言の英語）。この 2 件は `BlockContextModel` の組み立てを見るので、偽の追跡ブロックと `BlockNetworkInfoCache` の値で `BlockContextModel` を組んで確かめる（組めなければ `CleanRoom()` の分岐を `CleanRoomViewConversion` の静的関数へ出して試す）
- 抽選: 室内機械でなければ `Absent`／未取得 → `Unavailable`／層 → `WithoutPendingDraw(LayerExposure(r))`、`PendingDraw` ありなら `WithPendingDraw`／ダイシング同様／NoRole・NotOperable → `Absent`
- 詰まり: 追跡なし → `Absent`／未取得 → `Unavailable`／停止 → `Fetched(MachineInputStall(reason, new ItemId(id)))`／NotStalled → `Absent`
- 区画を出すか（`ContextSectionVisibility.Shows`）: 機械なら偽／`HasAny` が偽なら偽／機械でなく `HasAny` が真なら真

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → Expected: 型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`CleanRoomViewConversion.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Game.Block.Interface.Component;
using Server.Protocol.PacketResponse;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 部屋の照会を L0-s4b の入力型へ写す（Web ホスト CleanRoomDetailDtoBuilder の写し）
    /// Maps the room query onto L0-s4b's input type (port of the Web host's CleanRoomDetailDtoBuilder)
    /// </summary>
    public static class CleanRoomViewConversion
    {
        public static ContextEntry<CleanRoomFetchedDetail> Convert(BlockInfoFetchState fetchState, CleanRoomInfoView view, CleanRoomHaltReason haltReason)
        {
            if (fetchState != BlockInfoFetchState.Fetched) return ContextEntry<CleanRoomFetchedDetail>.Unavailable(fetchState);
            return ContextEntry<CleanRoomFetchedDetail>.Fetched(view switch
            {
                CleanRoomEnclosedView enclosed => CleanRoomFetchedDetail.Enclosed(Room(enclosed.Room), haltReason),
                CleanRoomNotEnclosedView notEnclosed => Leak(notEnclosed.Leak),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view.GetType().Name, "未対応の部屋照会の型 / Unsupported room query view"),
            });

            #region Internal

            CleanRoomFetchedDetail Leak(CleanRoomLeakView leak)
            {
                return leak switch
                {
                    CleanRoomOpeningsLeakView openings => CleanRoomFetchedDetail.Openings(openings.TotalOpeningCount, haltReason),
                    CleanRoomNoEnclosureLeakView _ => CleanRoomFetchedDetail.NoEnclosure(haltReason),
                    CleanRoomTooLargeLeakView tooLarge => CleanRoomFetchedDetail.TooLarge(tooLarge.MaxRoomVolume, haltReason),
                    CleanRoomDetectionPendingLeakView _ => CleanRoomFetchedDetail.Detecting(haltReason),
                    _ => throw new ArgumentOutOfRangeException(nameof(leak), leak.GetType().Name, "未対応の漏れ種別 / Unsupported leak kind"),
                };
            }

            static CleanRoomSummary Room(CleanRoomSummaryMessagePack room)
            {
                var filters = new List<CleanRoomAirFilter>();
                foreach (var filter in room.AirFilters) filters.Add(new CleanRoomAirFilter(filter.PowerRatio, filter.FilterItemCount, filter.FilterRemainingRatio));
                var headline = room.TryGetClass(out var className, out _) ? CleanRoomHeadline.Class(className) : CleanRoomHeadline.ClassOut;
                return new CleanRoomSummary(headline, room.Concentration, room.AirChangeRate, filters);
            }

            #endregion
        }

        public static CleanRoomHaltReason HaltReason(CleanRoomOperability operability)
        {
            return operability switch
            {
                CleanRoomOperability.NotEnclosed => CleanRoomHaltReason.NotEnclosed,
                CleanRoomOperability.ClassOut => CleanRoomHaltReason.ClassOut,
                CleanRoomOperability.Operable => CleanRoomHaltReason.None,
                _ => throw new ArgumentOutOfRangeException(nameof(operability), operability, "未対応の稼働可否 / Unsupported operability"),
            };
        }
    }
}
```
（`throw` は Web ホストと同じ「新種の追加漏れを即死で知らせる」経路。サーバーの共有型の列挙なので外部データではなくコードの不整合として扱う。D-6）

`ContextViewConversion.cs`（`Yield(BlockInfoFetchState, CleanRoomYieldView)` と `Stall(BlockInfoFetchState, MachineInputStallView)`。上の Web ホスト 2 本〈`CleanRoomYieldDetailDtoBuilder.Build`・`MachineInputStallDetailDtoBuilder.Build`〉を同じ分岐で写す。`DicingYieldMessagePack.LevelRates`（`double[]`）はそのまま `IReadOnlyList<double>` として渡す）

`BlockContextModel.cs`:
```csharp
using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockDetail.CleanRoom;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 部屋・抽選・詰まりの 3 欄を、追跡替え・状態変化・1 秒ポーリングの更新で配り直す
    /// Re-publishes the room, draw and stall entries on tracking changes, state changes and the one-second poll updates
    /// </summary>
    public sealed class BlockContextModel : IBlockContextModel, IInitializable
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly BlockNetworkInfoCache _cache;
        private readonly ReactiveProperty<BlockContextViewModel> _current = new(BlockContextViewModel.None);

        public IReadOnlyReactiveProperty<BlockContextViewModel> Current => _current;

        public BlockContextModel(OpenBlockDetailTracker tracker, BlockNetworkInfoCache cache)
        {
            _tracker = tracker;
            _cache = cache;
        }

        public void Initialize()
        {
            _tracker.OnTrackedBlockChanged.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => Rebuild());
            _cache.OnUpdated.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            // silent-ok: 追跡ブロックが無いとき 3 欄は無い（閉と同じ） / 追跡替えの購読で直ちに組み直す
            var block = _tracker.TrackedBlock;
            if (block == null)
            {
                _current.Value = BlockContextViewModel.None;
                return;
            }
            _current.Value = new BlockContextViewModel(CleanRoom(), Yield(), Stall());

            #region Internal

            ContextEntry<CleanRoomFetchedDetail> CleanRoom()
            {
                if (_cache.CleanRoomTarget == CleanRoomDetailTarget.None) return ContextEntry<CleanRoomFetchedDetail>.Absent;
                var fetchState = _cache.GetCleanRoom(out var view);

                // 未取得は対象の種類によらず取得状態の 1 行（Web ホスト CleanRoomDetailDtoBuilder:29-34 と同じ順）
                // Not fetched yields the fetch-state line for any target (same order as the Web host's CleanRoomDetailDtoBuilder:29-34)
                if (fetchState != BlockInfoFetchState.Fetched) return CleanRoomViewConversion.Convert(fetchState, view, CleanRoomHaltReason.None);
                if (_cache.CleanRoomTarget == CleanRoomDetailTarget.AirFilter) return CleanRoomViewConversion.Convert(fetchState, view, CleanRoomHaltReason.None);

                // 取得済みの室内機械の停止理由はブロックの状態詳細から（揃わない間は欄ごと出さない。Web ホストと同じ）
                // A fetched interior machine's halt reason comes from its state detail (no section until present, like the Web host)
                var operability = block.GetStateDetail<CleanRoomOperabilityStateDetail>(CleanRoomOperabilityStateDetail.BlockStateDetailKey);
                // silent-ok: 稼働可否の状態詳細が届く前は欄を出さない（Web ホストと同じ） / 届けば状態変化の購読で出る
                if (operability == null) return ContextEntry<CleanRoomFetchedDetail>.Absent;
                if (!System.Enum.IsDefined(typeof(CleanRoomOperability), operability.Operability))
                {
                    Debug.LogWarning($"Unreadable clean-room operability: {operability.Operability}");
                    return ContextEntry<CleanRoomFetchedDetail>.Absent;
                }
                return CleanRoomViewConversion.Convert(fetchState, view, CleanRoomViewConversion.HaltReason(operability.Operability));
            }

            ContextEntry<CleanRoomYieldFetched> Yield()
            {
                if (_cache.CleanRoomTarget != CleanRoomDetailTarget.Machine) return ContextEntry<CleanRoomYieldFetched>.Absent;
                var fetchState = _cache.GetCleanRoomYield(out var view);
                return ContextViewConversion.Yield(fetchState, view);
            }

            ContextEntry<MachineInputStall> Stall()
            {
                if (!_cache.IsTrackingMachineInputStall) return ContextEntry<MachineInputStall>.Absent;
                var fetchState = _cache.GetMachineInputStall(out var view);
                return ContextViewConversion.Stall(fetchState, view);
            }

            #endregion
        }
    }
}
```
（`ContextEntry<T>`・`BlockContextViewModel` は Produces の形で `BlockContextEntries.cs`・`BlockContextViewModel.cs` に書く。`HasAny` は 3 欄のどれかが `!IsAbsent`）

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.BlockInventory.Context"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Context moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Context
git commit -m "feat(uitk): 部屋・抽選・搬入停止の 3 欄を照会から写す Model を足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 5: 機械の Model・選ぶコマンド・選択モードの状態・alpha の口への寄与（`Client.Ui.State/BlockInventory/Machine/`）

**Files:**
- Create: `S/Client.Ui.State/BlockInventory/Machine/MachineSectionViewModel.cs`・`MachineSectionViewModelBuilder.cs`・`IMachineSectionModel.cs`・`MachineSectionModel.cs`・`RecipeMachineLargePanelRule.cs`・`MachineSlotRestrictionContributor.cs`
- Create: `S/Client.Ui.State/BlockInventory/Machine/Selection/MachineSelectionModeState.cs`・`IMachineRecipeSelectCommand.cs`・`MachineRecipeSelectCommand.cs`・`IMachineRecipeSelectionSender.cs`・`VanillaMachineRecipeSelectionSender.cs`
- Modify（共有の追記・E-1）: `S/Client.Ui.State/Commands/UiCommandRejectReason.cs`（列挙の末尾に `MachineRecipeBlockNotOpen`・`MachineRecipeInvalid`・`MachineRecipeLocked`、3 つの網羅 switch の末尾に `IsBenign`=真・`ToLocalizationKey`=`LocalizationKeys.Ui.CommandRejected.MachineRecipeBlockNotOpen` 等・`ToWireCode`=`block_not_open`・`invalid_recipe`・`recipe_locked`）、`Localization/localization.csv`（`ui.commandRejected.machineRecipeBlockNotOpen`・`…Invalid`・`…Locked` の 3 行を末尾に。良性なのでトーストには出ないが網羅 switch のキーとして要る。Web の生成物 `localizationKeys.ts` の再生成コミットは Task 16 の前に台帳 `docs/uitk/TODO.md`「## 追従記録」へ `写した`〈写し先＝同じコミットの C# と CSV〉で 1 行）
- Modify（全列挙を見るテストを同じ PR で緑にする。E-1）: `S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs`（`良性の拒否理由は…だけ` の `benign` 配列に `MachineRecipeBlockNotOpen`・`MachineRecipeInvalid`・`MachineRecipeLocked` を足し、名前の末尾を「…とレシピ選択の送れない要求だけ」に直す。alpha が先に `BlockSlotUnavailable` を足して名前を変えているので、その名前に続ける。直さないと良性の 3 つが配列と食い違って赤になる）。ほかに `UiCommandRejectReason` の全列挙を見るテストが無いかを `grep -rln "GetValues(typeof(UiCommandRejectReason" moorestech_client/Assets/Scripts/Client.Tests` で確かめ、あれば同じく直す
- Modify（生成物）: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`pnpm --dir moorestech_web/webui run gen:i18n` で再生成）・`S/Client.Localization/_CompileRequester.cs`（csv を変えたときの再コンパイルの印。Editor の SchemaWatcher が書き換える）
- Test: `S/Client.Tests/Ui/State/BlockInventory/Machine/Detail/MachineSectionViewModelBuilderTest.cs`、`Machine/Selection/MachineSelectionModeStateTest.cs`・`MachineRecipeSelectCommandTest.cs`・`MachineTestDoubles.cs`、`Machine/Rules/RecipeMachineLargePanelRuleTest.cs`・`MachineSlotRestrictionContributorTest.cs`

**Interfaces:**
- Consumes: Task 2〜3、alpha（`IBlockInventoryPanelSource`・`OpenBlockInventoryPanel`・`UnlockedMachineRecipeSource`・`MachineRecipeSummaryConversion`・`IBlockInventoryLargePanelRule`・`IBlockSlotRestrictionContributor`・`OpenBlockSlotInput.WithMachine`）、`IModuleSlotStackLimitLookup`、`IGameUnlockStateData`、L0-s4b（`MachineSlotGhosts`・`MachineSlotLayout.Split`）
- Produces:
```csharp
namespace Client.Ui.State.BlockInventory.Machine
public sealed class MachineSectionViewModel
{
    public BlockInstanceId BlockInstanceId { get; }  public Guid BlockGuid { get; }  public MachineDetail Detail { get; }  public IReadOnlyList<SlotItem> ItemSlots { get; }
    public IReadOnlyList<BlockFluidSlotState> FluidSlots { get; }  public MachineSlotView SlotView { get; }  public MachineSlotBands Bands { get; }
    public IReadOnlyList<MachineRecipeSelectionRow> Rows { get; }  public int ModuleStackLimit { get; }
    public bool TryGetSelectedRow(out MachineRecipeSelectionRow row);
    public bool IsFor(OpenBlockInventoryPanel panel);                          // BlockInstanceId 一致（同種の機械を続けて開いても前の値と区別する）
    public bool TryGetRecipe(Guid recipeGuid, out MachineRecipeData recipe);   // 行の表示（入出力・秒・継承）
}
public interface IMachineSectionModel { IReadOnlyReactiveProperty<SectionContent<MachineSectionViewModel>> Current { get; } }
public sealed class RecipeMachineLargePanelRule : IBlockInventoryLargePanelRule { RecipeMachineLargePanelRule(IMachineSectionModel model); }
public sealed class MachineSlotRestrictionContributor : IBlockSlotRestrictionContributor { MachineSlotRestrictionContributor(IMachineSectionModel model); }
namespace Client.Ui.State.BlockInventory.Machine.Selection
public sealed class MachineSelectionModeState
{
    public void OpenSelection();                            // ヘッダ押下: 開いて要求を忘れる
    public void RememberRequest(Guid recipeGuid);           // 行押下: 要求 GUID を覚える（送るのは View がコマンドを呼ぶ。Web onSelect）
    public bool ShowsSelection(MachineSectionViewModel viewModel);
}
public interface IMachineRecipeSelectCommand { UiCommandResult Select(Guid recipeGuid); }   // 開いた機械に送る。選択中と同じなら送らず受理。送れない要求は良性の拒否（MachineRecipeBlockNotOpen・MachineRecipeInvalid・MachineRecipeLocked。Detail は ""）で返す。送信後のサーバーの拒否は待たずに LogWarning（D-16）
public sealed class MachineRecipeSelectCommand : IMachineRecipeSelectCommand { public const string CommandName = "machine_recipe.select"; }
namespace Client.Ui.State.Commands
public enum UiCommandRejectReason { …既存…, MachineRecipeBlockNotOpen, MachineRecipeInvalid, MachineRecipeLocked }   // 末尾に追記（E-1）。3 つとも良性
```

- [ ] **Step 1: 失敗するテストを書く**

`MachineSelectionModeStateTest.cs`:
```csharp
using System;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 選択モードの出し分け（Web MachineSection の showSelection・C14）
    /// Selection-mode visibility (Web MachineSection showSelection, C14)
    /// </summary>
    public class MachineSelectionModeStateTest
    {
        private static readonly Guid RecipeA = MachineTestDetails.RecipeA;
        private static readonly Guid RecipeB = MachineTestDetails.RecipeB;

        [Test]
        public void 未選択は選択モードでレシピ0件はインベントリモード()
        {
            var state = new MachineSelectionModeState();
            Assert.IsTrue(state.ShowsSelection(MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected, RecipeA, RecipeB)));
            Assert.IsFalse(state.ShowsSelection(MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected)));
        }

        [Test]
        public void 開いた後は要求がサーバーの選択に一致するまで閉じない()
        {
            var state = new MachineSelectionModeState();
            var selectedA = MachineTestDetails.ViewModel(MachineRecipeSelection.Selected(RecipeA), RecipeA, RecipeB);
            Assert.IsFalse(state.ShowsSelection(selectedA));
            state.OpenSelection();
            Assert.IsTrue(state.ShowsSelection(selectedA));
            state.RememberRequest(RecipeB);
            Assert.IsTrue(state.ShowsSelection(selectedA));
            Assert.IsFalse(state.ShowsSelection(MachineTestDetails.ViewModel(MachineRecipeSelection.Selected(RecipeB), RecipeA, RecipeB)));
        }

        [Test]
        public void 同じレシピを選び直しても閉じる()
        {
            var state = new MachineSelectionModeState();
            var selectedA = MachineTestDetails.ViewModel(MachineRecipeSelection.Selected(RecipeA), RecipeA, RecipeB);
            state.OpenSelection();
            state.RememberRequest(RecipeA);
            Assert.IsFalse(state.ShowsSelection(selectedA));
        }

        [Test]
        public void 選択中のレシピが行に無ければ選択モード()
        {
            var state = new MachineSelectionModeState();
            Assert.IsTrue(state.ShowsSelection(MachineTestDetails.ViewModel(MachineRecipeSelection.Selected(Guid.NewGuid()), RecipeA)));
        }
    }
}
```
（`RecordingRecipeSelectCommand`〈`Selected` に GUID を順に覚え、`UiCommandAccepted.Instance` を返す。画面のテストが使う〉は `MachineTestDoubles.cs` に `FakeMachineSectionModel` と並べて置く。`MachineTestDetails.ViewModel(MachineRecipeSelection selection, params Guid[] unlockedRecipeGuids)` は Task 2 の補助に足す: 入力 1・出力 1・モジュール 0 の機械で、各 GUID を出力アイテム 1 個のレシピとして `MachineSectionViewModelBuilder.Build` に渡す）

`MachineSectionViewModelBuilderTest.cs`（行が機械の blockGuid のレシピだけ・選択中の行が `TryGetSelectedRow` で取れる・`Bands` が `Layout.Split(枠数)`・ゴーストが束縛から・`TryGetRecipe` が解放済みの `MachineRecipeData` を返す）

`RecipeMachineLargePanelRuleTest.cs`（`FakeMachineSectionModel` が Absent → 偽／行 0 → 偽／行 1 以上 → 真／同じ blockType で `BlockInstanceId` の違うパネル → 偽（前の機械の値と区別する）／Model の値の差し替えで `OnRuleInputChanged` が 1 回流れる）

`MachineSlotRestrictionContributorTest.cs`（Absent → `AppliesTo` 偽／`BlockInstanceId` の違うパネル → 偽／Present で同じブロック → 真で、`Contribute` の結果を `BlockSlotRestrictionResolver.Resolve(結果, 素材の ItemId)` に通すと `Bound` に入力の番地だけが入る〈Web `machineGestures` 3 件目の前提〉）

`MachineRecipeSelectCommandTest.cs`（記録係の `IMachineRecipeSelectionSender` と偽の `IGameUnlockStateData`・alpha の `FakePanelSource`・`FakeMachineSectionModel` で: 解放済みで選択中と違う → 位置と GUID で 1 回送り `UiCommandAccepted`／選択中と同じ GUID → 送らず `UiCommandAccepted`／未解放 → 送らず `UiCommandRejected(MachineRecipeLocked)`／未知の GUID → 送らず `MachineRecipeInvalid`／ブロックが開いていない → 送らず `MachineRecipeBlockNotOpen`。拒否は受け取った `UiCommandRejected` のインスタンスの `ToWireCode()` が `recipe_locked`・`invalid_recipe`・`block_not_open` に一致し、`LogAssert.Expect(LogType.Warning, …recipe=<GUID>)` で GUID がログに出る。3 つの理由が `UiCommandRejectReasonTraits.IsBenign` で真）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → Expected: 型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`MachineSelectionModeState.cs`:
```csharp
using System;
using Client.Ui.State.BlockInventory.Machine.Detail;

namespace Client.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 機械の区画の選択モードの局所状態。区画が作り直されると消える（Web の useState と同じ寿命）
    /// Local selection-mode state of the machine section; it resets when the section is rebuilt (same lifetime as Web useState)
    /// </summary>
    public sealed class MachineSelectionModeState
    {
        private bool _selectionOpened;
        private bool _hasRequest;
        private Guid _requestedRecipeGuid;

        public void OpenSelection()
        {
            _selectionOpened = true;
            _hasRequest = false;
        }

        public void RememberRequest(Guid recipeGuid)
        {
            // 押した GUID を要求として覚える。サーバーの選択がこれに一致すると閉じる（同じレシピを押し直したときも閉じる）
            // Remember the pressed GUID as the request; the mode closes once the server's selection matches it (also on re-pressing the same recipe)
            _hasRequest = true;
            _requestedRecipeGuid = recipeGuid;
        }

        public bool ShowsSelection(MachineSectionViewModel viewModel)
        {
            // 選べるレシピが無い機械は選択モードを持たない
            // A machine with no selectable recipe has no selection mode
            if (viewModel.Rows.Count == 0) return false;
            if (viewModel.Detail.Selection is UnselectedMachineRecipe) return true;
            if (!viewModel.TryGetSelectedRow(out _)) return true;

            // 要求したレシピがサーバーの選択に一致するまで閉じない（拒否された間は留まる。C14）
            // Stay open until the requested recipe matches the server's selection (a rejection keeps it open, C14)
            var requestApplied = _hasRequest && viewModel.Detail.Selection.Matches(_requestedRecipeGuid);
            return _selectionOpened && !requestApplied;
        }
    }
}
```

`MachineSectionModel.cs`:
```csharp
using Client.Game.InGame.Block;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Recipe.Source;
using Game.Block.Interface.MachineUpgrade;
using Game.Block.Interface.State;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// 開いた機械の区画の値を、追跡ブロック・パネルの枠・解放済みレシピ・モジュールの上限の変化で配り直す
    /// Re-publishes the open machine section's values on changes to the tracked block, panel slots, unlocked recipes and module limit
    /// </summary>
    public sealed class MachineSectionModel : IMachineSectionModel, IInitializable
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly IBlockInventoryPanelSource _panelSource;
        private readonly UnlockedMachineRecipeSource _recipeSource;
        private readonly IModuleSlotStackLimitLookup _stackLimit;
        private readonly IFluidReplicaReader _fluidReplica;
        private readonly ReactiveProperty<SectionContent<MachineSectionViewModel>> _current = new(SectionContent<MachineSectionViewModel>.Absent);

        public IReadOnlyReactiveProperty<SectionContent<MachineSectionViewModel>> Current => _current;

        public MachineSectionModel(OpenBlockDetailTracker tracker, IBlockInventoryPanelSource panelSource, UnlockedMachineRecipeSource recipeSource, IModuleSlotStackLimitLookup stackLimit, IFluidReplicaReader fluidReplica)
        {
            _tracker = tracker;
            _panelSource = panelSource;
            _recipeSource = recipeSource;
            _stackLimit = stackLimit;
            _fluidReplica = fluidReplica;
        }

        public void Initialize()
        {
            // パネル（枠）は alpha の Model が追跡の変化でも組み直すので、それと追跡の状態変化・レシピ・上限で組み直す
            // Rebuild on the panel (alpha rebuilds it on tracking changes), tracked-state changes, recipes and the limit
            _panelSource.Current.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => Rebuild());
            _recipeSource.Current.Subscribe(_ => Rebuild());
            _stackLimit.OnStackLimitChanged.Subscribe(_ => Rebuild());
        }

        private void Rebuild()
        {
            _current.Value = Read();

            #region Internal

            SectionContent<MachineSectionViewModel> Read()
            {
                // silent-ok: 閉・列車・取得失敗のパネルに機械の区画は無い（alpha の外殻が本文を作らない） / 並べ比べと録画
                if (_panelSource.Current.Value is not OpenBlockInventoryPanel panel || panel.Error != BlockInventoryPanelError.None) return SectionContent<MachineSectionViewModel>.Absent;
                // silent-ok: 追跡ブロックが未解決の間は区画を出さない（閉と同じ見え方） / 追跡替えでパネルが組み直され直ちに再計算
                var block = _tracker.TrackedBlock;
                if (block == null || !TryReadMachine(block, out var detail)) return SectionContent<MachineSectionViewModel>.Absent;
                var fluids = BlockFluidSlotReading.Read(block, _fluidReplica);
                return SectionContent<MachineSectionViewModel>.Present(MachineSectionViewModelBuilder.Build(panel, detail, panel.ItemSlots, fluids, _recipeSource.Current.Value, _stackLimit.CurrentStackLimit));
            }

            static bool TryReadMachine(BlockGameObject block, out MachineDetail detail)
            {
                detail = null;
                var common = block.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
                var machine = block.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
                // silent-ok: 2 つの状態詳細と機械の param が揃うブロックだけが機械（Web ホストと同じ判定） / 区画が出ないことを並べ比べで見る
                if (common == null || machine == null || block.BlockMasterElement.BlockParam is not IMachineParam param) return false;
                var gearDriven = block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey) != null;
                return MachineDetailConversion.TryBuild(common, machine, param, gearDriven, out detail);
            }

            #endregion
        }
    }
}
```
（`BlockFluidSlotReading.Read(BlockGameObject, IFluidReplicaReader)` は Task 3 の液体の読み手を静的関数にしたもので、`BlockFluidSlotsModel` と共有する。機械は液体の行を状態詳細のタンクから読むので、パイプの複製の通知は要らない）

`RecipeMachineLargePanelRule.cs`:
```csharp
using System;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// レシピを選べる機械だけ大型パネルにする寄与（Web BlockInventoryPanel の isLargeMachinePanel）
    /// The contribution that makes only machines with selectable recipes large (Web BlockInventoryPanel isLargeMachinePanel)
    /// </summary>
    public sealed class RecipeMachineLargePanelRule : IBlockInventoryLargePanelRule
    {
        private readonly IMachineSectionModel _model;

        public IObservable<Unit> OnRuleInputChanged => _model.Current.AsUnitObservable();

        public RecipeMachineLargePanelRule(IMachineSectionModel model)
        {
            _model = model;
        }

        public bool IsLargePanel(OpenBlockInventoryPanel panel)
        {
            var current = _model.Current.Value;
            return current.IsPresent && current.Value.IsFor(panel) && 0 < current.Value.Rows.Count;
        }
    }
}
```

`MachineSlotRestrictionContributor.cs`（`AppliesTo` は上と同じく Present かつ `IsFor(panel)`、`Contribute` は `input.WithMachine(_model.Current.Value.Value.Detail.Binding)`）

`MachineRecipeSelectCommand.cs`:
```csharp
using System;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Game.UnlockState;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 開いた機械のレシピを選ぶ。解放の検査は Web ホスト MachineRecipeSelectActionHandler と同じ（UITK に写し直した）
    /// Selects the open machine's recipe; the unlock check matches the Web host's MachineRecipeSelectActionHandler (ported to UITK)
    /// </summary>
    public sealed class MachineRecipeSelectCommand : IMachineRecipeSelectCommand
    {
        public const string CommandName = "machine_recipe.select";

        private readonly IBlockInventoryPanelSource _panelSource;
        private readonly IMachineSectionModel _machine;
        private readonly IGameUnlockStateData _unlockState;
        private readonly IMachineRecipeSelectionSender _sender;

        public MachineRecipeSelectCommand(IBlockInventoryPanelSource panelSource, IMachineSectionModel machine, IGameUnlockStateData unlockState, IMachineRecipeSelectionSender sender)
        {
            _panelSource = panelSource;
            _machine = machine;
            _unlockState = unlockState;
            _sender = sender;
        }

        public UiCommandResult Select(Guid recipeGuid)
        {
            // 送れない要求は良性の拒否で返す（トースターは理由の wire だけを Warning ログへ出しトーストは出さない。GUID はここで出す）
            // Requests that cannot go return a benign rejection (the toaster logs only the reason's wire code, no toast; the GUID is logged here)
            if (_panelSource.Current.Value is not OpenBlockInventoryPanel panel || panel.Error != BlockInventoryPanelError.None) return Reject(UiCommandRejectReason.MachineRecipeBlockNotOpen);
            if (!_unlockState.MachineRecipeUnlockStateInfos.TryGetValue(recipeGuid, out var unlock)) return Reject(UiCommandRejectReason.MachineRecipeInvalid);
            if (!unlock.IsUnlocked) return Reject(UiCommandRejectReason.MachineRecipeLocked);

            // 選択中と同じレシピは送らずに受理する（Web MachineSection の onSelect。選択モードは要求の一致で閉じる）
            // The already-selected recipe is accepted without sending (Web MachineSection onSelect; the mode closes on the request match)
            if (_machine.Current.Value.IsPresent && _machine.Current.Value.Value.Detail.Selection.Matches(recipeGuid)) return UiCommandAccepted.Instance;
            _sender.SendSetRecipe(panel.Position, recipeGuid);
            return UiCommandAccepted.Instance;

            #region Internal

            UiCommandResult Reject(UiCommandRejectReason reason)
            {
                Debug.LogWarning($"[{CommandName}] not sent: {UiCommandRejectReasonTraits.ToWireCode(reason)} recipe={recipeGuid}");
                return new UiCommandRejected(reason, "");
            }

            #endregion
        }
    }
}
```
（開いた機械はパネルの状態〈`MachineSectionModel` と同じ源〉から読み、選択中のレシピは機械の Model から読む。`Detail` は既存の生成箇所と同じく `""`〈`ToWireCode()` は `Detail` が空でないとそれを返すので、GUID を入れるとログから理由が消える〉。テストは alpha の `FakePanelSource`。`IMachineRecipeSelectionSender { void SendSetRecipe(Vector3Int position, Guid recipeGuid); }`）

`VanillaMachineRecipeSelectionSender.cs`:
```csharp
using System;
using System.Threading;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 選択の要求をサーバーへ送る外部境界。応答の失敗は開発者ログへ出し、選択モードは要求 GUID が一致しないので開いたまま
    /// The external boundary sending the selection request; a failed response logs, and selection mode stays open since the GUID never matches
    /// </summary>
    public sealed class VanillaMachineRecipeSelectionSender : IMachineRecipeSelectionSender
    {
        public void SendSetRecipe(Vector3Int position, Guid recipeGuid)
        {
            SendAsync().Forget();

            #region Internal

            async UniTaskVoid SendAsync()
            {
                var request = MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(position, recipeGuid);
                var response = await ClientContext.VanillaApi.Response.Block.SendMachineRecipeSelectionRequest(request, CancellationToken.None);
                if (response == null || !response.Success) Debug.LogWarning($"[{MachineRecipeSelectCommand.CommandName}] rejected: {(response == null ? "no_response" : response.FailureReason.ToString())} recipe={recipeGuid}");
            }

            #endregion
        }
    }
}
```

`MachineSectionViewModelBuilder.cs`（`Build(OpenBlockInventoryPanel panel, MachineDetail detail, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluidSlots, IReadOnlyList<MachineRecipeData> unlockedRecipes, int moduleStackLimit)`: `MachineRecipeSummaryConversion.ToSummaries(unlockedRecipes)` → `detail.Selection.BuildRows(summaries, panel.BlockGuid)`・`panel.BlockInstanceId` と `panel.BlockGuid` を ViewModel に写す・`MachineSlotGhosts.BuildMachineSlotView(detail.Binding, itemSlots, fluidSlots)`・`detail.Binding.Layout.Split(itemSlots.Count)`・GUID → `MachineRecipeData` の辞書を組んで ctor へ）

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.BlockInventory|PauseMenuCommandsTest|UiCommandReject"`
Expected: `ErrorCount: 0`、全件 PASS（alpha のテストと全列挙を見るテストも緑のまま）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory moorestech_client/Assets/Scripts/Client.Ui.State/Commands moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Machine moorestech_client/Assets/Scripts/Client.Tests/Ui/State/PauseMenuCommandsTest.cs Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
git commit -m "feat(uitk): 機械の区画の Model・選択モード・レシピを選ぶコマンド・大型判定と Shift 束縛の寄与を足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---


### Task 6: 液体の部品と 2 区画（`Screens/BlockInventory/Fluid/`）

**Files:**
- Create: `S/Client.Ui/Screens/BlockInventory/Fluid/FluidSlotContentMapper.cs`・`FluidRowView.cs`・`FluidRowSectionFactory.cs`・`FluidRowSectionPresenter.cs`・`FluidInventoryContents.cs`・`FluidDetailsSectionFactory.cs`・`FluidDetailsSectionPresenter.cs`・`NotApplicableSection.cs`・`Fluid.uss`
- Test: `S/Client.Tests/Ui/Screens/BlockInventory/Fluid/FluidSectionsTest.cs`・`FluidSlotTest.cs`

**Interfaces:**
- Consumes: C4-a（`FluidSlotContent`・`FluidSlotRow`・`GameContentPresentation.Fluid`・`FluidFace`）、Task 3（`IBlockFluidSlotsModel`・`FluidRowVisibility`）、Task 5（`IMachineSectionModel`。機械なら FluidRow を出さない）、alpha（区画の工場の口・`SectionStackSectionMount.Config`）
- Produces（S4-train も使う）:
```csharp
namespace Client.Ui.Screens.BlockInventory.Fluid
public static class FluidSlotContentMapper
{
    public static FluidSlotContent Map(BlockFluidSlotState slot, GameContentPresentation content);                          // 中身あり→Filled・それ以外→Empty
    public static FluidSlotContent MapWithGhost(BlockFluidSlotState slot, BoundFluidSlot bound, GameContentPresentation content); // 空でゴーストあり→Ghost
}
public sealed class FluidInventoryContents : VisualElement   // Web FluidInventoryContents。中に UiStack(Xs) を 1 つ持つ（UiStack は sealed）。0 件で子なし
{
    public FluidInventoryContents(string testid, GameContentPresentation content);
    public void Render(IReadOnlyList<BlockFluidSlotState> fluids);
}
public sealed class FluidRowView : VisualElement { public FluidRowView(string testid, GameContentPresentation content); public void Render(BlockFluidSlotsViewModel viewModel, bool showProgress); }
```

- [ ] **Step 1: 失敗するテストを書く**

`FluidSectionsTest.cs`（`ScreenTestHost`。alpha の `BlockInventoryTestBodies.SectionStack(source, blockType, factories)`〈`source` は `FakePanelSource` に `BlockInventoryTestPanels.OfType(blockType)`〉に本レーンの `FluidRowSectionFactory`・`FluidDetailsSectionFactory`〈偽の `FakeBlockFluidSlotsModel`・`FakeMachineSectionModel` を ctor で渡す〉を差して組む）:
```csharp
/// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">%s の既存表示を保つ</web-test>
[TestCase("Chest", null, true)]
[TestCase("ElectricMachine", "machine-fluid-slots", false)]
[TestCase("GearMachine", "machine-fluid-slots", false)]
[TestCase("ElectricGenerator", "generator-fluid-slots", false)]
[TestCase("FuelGearGenerator", null, true)]
[TestCase("SimpleGearGenerator", null, true)]
[TestCase("ElectricMiner", null, true)]
[TestCase("GearMiner", null, true)]
[TestCase("CleanRoomMachine", "machine-fluid-slots", false)]
[TestCase("CleanRoomAirFilter", null, false)]
[TestCase("unknown", "generic-block-fluids", true)]
public void 液体の行は設定の testid で出し進捗は設定どおり(string blockType, string fluidRowTestId, bool showFluidProgress)
```
（Web の it.each 11 行の blockType・`fluidRowTestId`・`showFluidProgress` をそのまま写す〈項目グリッドの列は alpha が見る〉。Web と同じ入力: 液体 1 件〈空・容量 100〉・進捗 0.4。機械の Model は Absent〈Web のテストは `data.machine` 無し〉。主張: 表示中の液体の行の name の列が `fluidRowTestId == null ? 空 : [fluidRowTestId]`、id があれば `progress-arrow-bar` の数が `showFluidProgress ? 1 : 0`）
- タグ無しの自前で FluidPipe（`fluid-pipe-fluid-slots` の詳細）・ElectricPump（`pump-fluid-slots`・進捗なし）を同じ形で見る（Web の it.each に無いので別テスト）
- 「パイプは液体だけを進捗なしで描画する」: 中身あり（水 40/100）→ `progress-arrow-bar` 0 件・液体名と `40 / 100` の 2 行・量 60 に差し替えると `fluid-slot` の量の帯が `60`
- 「空のパイプは名前や容量を表示せず空スロットを残す」: 空 → `fluid-pipe-fluid-slots-slot-0` があり `fluid-slot` 1 件・文の行 0 件
- タグ無しで: 機械の Model が Present なら FluidRow は出ない／液体 0 件なら FluidRow も FluidDetails も出ない（Web は null を返す）

`FluidSlotTest.cs`（alpha の `BlockInventoryTestShell.Create` に Tank のパネルと偽の液体の Model を差す）: 5 件を Web の名前で（題に Fluid Tank〈`ContentLocalizationKeys.BlockName`〉・水の量 500 の帯〈`SlotAmountFormat.Format(500)`〉・`fluid-slot` に `HoverTooltipMark` の文が水の名前・進捗 0.5 で `.progress-arrow-bar__fill` の `style.width` が 50%・テクスチャ欠け〈`IGameContentTextureSource` の偽が null〉で背面の面の色が残る）。Web の「hover で表示される」は印の文を見る（C3-b の前例どおり、ツールチップの表示そのものは C3-b のテストが持つ）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → Expected: 型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`FluidSlotContentMapper.cs`:
```csharp
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Machine;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の列の 1 件を液体の枠の中身へ写す（C4-a の約束: 中身あり→Filled、空でゴーストあり→Ghost、他→Empty）
    /// Maps one fluid entry onto fluid-slot content (C4-a contract: filled→Filled, empty with ghost→Ghost, else Empty)
    /// </summary>
    public static class FluidSlotContentMapper
    {
        public static FluidSlotContent Map(BlockFluidSlotState slot, GameContentPresentation content)
        {
            if (slot is FilledBlockFluidSlotState filled) return FluidSlotContent.Filled(content.Fluid(filled.FluidId), filled.Amount, filled.Capacity, content.FluidFace(filled.FluidId));
            return FluidSlotContent.Empty;
        }

        public static FluidSlotContent MapWithGhost(BlockFluidSlotState slot, BoundFluidSlot bound, GameContentPresentation content)
        {
            // 中身ありにゴーストを出さない
            // Never show a ghost over real contents
            if (slot is FilledBlockFluidSlotState) return Map(slot, content);
            if (bound is GhostedFluidSlot ghosted) return FluidSlotContent.Ghost(content.Fluid(ghosted.Ghost.FluidId), ghosted.Ghost.Amount);
            return FluidSlotContent.Empty;
        }
    }
}
```

`FluidInventoryContents.cs`:
```csharp
using System.Collections.Generic;
using Client.Localization;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の詳細（Web FluidInventoryContents）。行ごとに枠＋中身ありなら名前と「量 / 容量」
    /// Fluid details (Web FluidInventoryContents): per row a slot, plus name and "amount / capacity" when filled
    /// </summary>
    public sealed class FluidInventoryContents : VisualElement
    {
        private readonly UiStack _rows = new(UiGap.Xs);
        private readonly string _testid;
        private readonly GameContentPresentation _content;

        public FluidInventoryContents(string testid, GameContentPresentation content)
        {
            _testid = testid;
            _content = content;
            name = testid;
            AddToClassList("fluid-inventory-contents");
            Add(_rows);
        }

        public void Render(IReadOnlyList<BlockFluidSlotState> fluids)
        {
            _rows.ClearItems();
            for (var i = 0; i < fluids.Count; i++) _rows.AddItem(Row(i, fluids[i]));

            #region Internal

            VisualElement Row(int index, BlockFluidSlotState fluid)
            {
                // 行は Group gap=sm・中央揃え・折り返しなし（揃えと折り返しは内側の行に効く UiLayoutClass で付ける）
                // Each row is Group gap=sm, centered, no wrap (set via UiLayoutClass, which reaches the inner row)
                var row = new UiGroup(UiGap.Sm);
                row.AddToClassList(UiLayoutClass.GroupNoWrap);
                var slotRow = new FluidSlotRow($"{_testid}-slot-{index}");
                slotRow.SetFluids(new[] { FluidSlotContentMapper.Map(fluid, _content) });
                row.AddItem(slotRow);
                if (fluid is not FilledBlockFluidSlotState filled) return row;

                var texts = new UiStack(UiGap.None);
                texts.AddItem(Text(_content.Fluid(filled.FluidId).Name));
                texts.AddItem(Text(UiText.FromVerbatim($"{SlotAmountFormat.Format(filled.Amount)} / {SlotAmountFormat.Format(filled.Capacity)}")));
                row.AddItem(texts);
                return row;
            }

            static LocalizedLabel Text(UiText text)
            {
                var label = new LocalizedLabel();
                label.AddToClassList(UiTypographyClass.TextSm);
                label.SetText(text);
                return label;
            }

            #endregion
        }
    }
}
```
（`UiIcon` の名前の取り出し方は実物に合わせる。`GameContentPresentation.Fluid` が名前を `UiIcon` に入れている〈下調べ §3.1〉ので、その公開名〈`Name` 等〉を Step 1 で grep して使う）

`FluidRowView.cs`（`FluidSlotRow(testid)` を 1 つ持ち、`Render(viewModel, showProgress)` で `SetFluids(各 Map)`、`showProgress && viewModel.HasProgress` なら `SetProgress(viewModel.Progress)`・他は `ClearProgress()`）

`FluidRowSectionPresenter.cs`:
```csharp
using System;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.SectionStack;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の行の区画。機械でなく詳細でない blockType で、液体が 1 件以上あるときだけ出す（Web SectionStackView の 13 番）
    /// The fluid-row section: shown for non-machine, non-detail block types with at least one fluid (Web SectionStackView slot 13)
    /// </summary>
    public sealed class FluidRowSectionPresenter : IBlockInventorySection
    {
        private readonly RowSectionStackFluid _fluid;
        private readonly FluidRowView _view;
        private readonly IBlockFluidSlotsModel _fluids;
        private readonly IMachineSectionModel _machine;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root => _view;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public FluidRowSectionPresenter(RowSectionStackFluid fluid, FluidRowView view, IBlockFluidSlotsModel fluids, IMachineSectionModel machine)
        {
            _fluid = fluid;
            _view = view;
            _fluids = fluids;
            _machine = machine;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            // 2 本を別々に購読し、どちらの通知でも同じ判断をやり直す（GrabOverlayModel の前例。CombineLatest は Func の合成を要るので使わない）
            // Subscribe to both separately and redo the same decision on either (GrabOverlayModel precedent; CombineLatest needs a Func selector)
            _fluids.Current.Subscribe(_ => Apply()).AddTo(_subscriptions);
            _machine.Current.Subscribe(_ => Apply()).AddTo(_subscriptions);
        }

        public void Detach()
        {
            _subscriptions.Clear();
        }

        private void Apply()
        {
            var fluids = _fluids.Current.Value;
            var shown = FluidRowVisibility.Shows(fluids, _machine.Current.Value.IsPresent);
            if (shown) _view.Render(fluids, _fluid.ShowProgress);
            _isApplicable.Value = shown;
        }
    }
}
```
（出す・出さないの判断は State の `FluidRowVisibility.Shows`。Presenter は値を流すだけ〈設計書 §4.2〉）

`FluidRowSectionFactory.cs`（`Slot => SectionStackSlot.FluidRow`。`Create(mount)` は `mount.Config.Fluid is RowSectionStackFluid row` なら `new FluidRowSectionPresenter(row, new FluidRowView(row.RowId, _content), _fluids, _machine)`、それ以外は `IsApplicable` が常に偽の `NotApplicableSection`〈`Fluid/NotApplicableSection.cs` に 1 本置き、FluidRow・FluidDetails の両工場が使う。`Root` は空の `VisualElement`〉。alpha の `ISectionStackSectionFactory.Create` は null を返せない口なので、出さない区画もこの形で返す）。`FluidDetailsSectionFactory`／`Presenter` も同じ形（`Slot => FluidDetails`、`mount.Config.Fluid is DetailsSectionStackFluid details` のときだけ `FluidInventoryContents(details.RowId, _content)` を作り、液体 1 件以上で `IsApplicable` を真にする。機械かどうかは見ない〈Web と同じ〉）

`Fluid.uss`（詳細の行の揃えは `UiLayoutClass` で付けたので、残るのは外側の器の幅だけ）:
```css
/* web: features/blockInventory/fluid/FluidInventoryContents.tsx の Stack を区画の幅いっぱいに置く / lays the fluid details stack across the section width */
.fluid-inventory-contents {
    width: 100%;
}
```

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.BlockInventory.Fluid"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Fluid moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Fluid
git commit -m "feat(uitk): 液体の行と液体の詳細の区画を足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 7: 文脈の 3 欄の区画（`Screens/BlockInventory/Context/`）

**Files:**
- Create: `S/Client.Ui/Screens/BlockInventory/Context/BlockInfoUnavailableLine.cs`・`CleanRoomSectionView.cs`・`CleanRoomYieldSectionView.cs`・`MachineInputStallSectionView.cs`・`BlockContextSectionsView.cs`・`ContextSectionPresenter.cs`・`ContextSectionFactory.cs`
- Test: `S/Client.Tests/Ui/Screens/BlockInventory/Context/CleanRoomSectionTest.cs`・`CleanRoomYieldSectionTest.cs`・`MachineInputStallSectionTest.cs`・`ContextSectionOrderTest.cs`

**Interfaces:**
- Consumes: Task 4（`IBlockContextModel`・`BlockContextViewModel`・`ContextEntry<T>`）、Task 5（`IMachineSectionModel`）、L0-s4b（`CleanRoomLogic.Display`・`CleanRoomYieldLogic.Lines`・`MachineInputStallLogic.MessageKey`・`BlockInfoFetchStateLogic.TryGetUnavailableKey`）、alpha（`LackHighlightText`）
- Produces: `BlockContextSectionsView : VisualElement`（中に `UiStack(Sm)`。`Render(BlockContextViewModel)`。機械の区画も本文の器の先頭に同じ View を置く）

- [ ] **Step 1: 失敗するテストを書く**

`CleanRoomSectionTest.cs`（View を直に組んで `Render(ContextEntry<CleanRoomFetchedDetail>)`）:
```csharp
/// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomSection.test.ts">密閉済みの部屋は見出し・空気・清浄機・停止理由の順に描く</web-test>
[Test]
public void 密閉済みの部屋は見出し空気清浄機停止理由の順()
{
    var view = Mount(new CleanRoomSectionView());
    var filters = new[] { new CleanRoomAirFilter(1, 2, 0.5), new CleanRoomAirFilter(0.5, 1, 0.25) };
    view.Render(ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.Enclosed(new CleanRoomSummary(CleanRoomHeadline.Class("ISO 5"), 100, 20, filters), CleanRoomHaltReason.ClassOut)));
    simulate.FrameUpdate();
    CollectionAssert.AreEqual(new[] { "clean-room-headline", "clean-room-air", "clean-room-air-filter-0", "clean-room-air-filter-1", "clean-room-halt-reason" }, ShownNames(Find<VisualElement>(view, "clean-room-section")));
    Assert.IsTrue(Find<VisualElement>(view, "clean-room-halt-reason").ClassListContains("lack-highlight-text--insufficient"));
}
```
残り 3 件（清浄機なし → `clean-room-no-filter` が赤／隙間 → 見出しだけ赤・空気と清浄機の行なし／未取得〈`Unavailable(NotFetched)`〉→ `clean-room-fetch-state` の 1 行だけで、文が `BlockInfoFetchStateLogic` の文言に `{target}`＝`cleanRoomInfoLabel`）。`CleanRoomYieldSectionTest` 3 件（層 → `clean-room-yield-layer-fail-rate` だけ／ダイシング → 品質 Tier・損失率・0 でないレベル割合・失敗割合の RowId の順／未取得〈`Unreadable`〉→ `clean-room-yield-fetch-state` だけ）。`MachineInputStallSectionTest` 3 件（`MixedLevel` → `machine-input-stall` が赤で文に品名〈`GameContentPresentation.ItemName` の解決結果〉／`AboveCraftCap` → 製作上限の文言／未取得 → `machine-input-stall-fetch-state` が赤でない）

`ContextSectionOrderTest.cs`（alpha の `BlockInventoryTestBodies.SectionStack(source, blockType, factories)` に本レーンの `ContextSectionFactory`・`MachineSectionFactory`〈Task 9 で足す件だけ〉と偽の Model を差す）:
- 「部屋欄はセクション列の先頭に描く」: CleanRoomAirFilter で部屋欄が項目グリッド（alpha）より上
- 「抽選の中身欄は部屋欄のすぐ下に描く」: 部屋・抽選の 2 欄が順に並ぶ
- 「照会欄の無い payload では3欄とも描かず、取得中の欄は取得状態の行だけを描く」: `BlockContextViewModel.None` で文脈の区画が隠れる／部屋だけ未取得で `clean-room-fetch-state` だけ
- 「搬入停止欄は機械欄より上に赤字で描く」は機械の区画が要るので Task 9 Step 1 で同じファイルに足す

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → Expected: 型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`BlockInfoUnavailableLine.cs`:
```csharp
using Client.Game.InGame.BlockInfoPolling;
using Client.Localization;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Text;
using UnityEngine;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 照会が取れていない欄の 1 行（Web BlockInfoUnavailableLine）。文言の {target} に欄の名前を差す
    /// The one line for a section whose query is unavailable (Web BlockInfoUnavailableLine); {target} carries the section name
    /// </summary>
    public sealed class BlockInfoUnavailableLine : LocalizedLabel
    {
        private readonly LocalizationKey _targetKey;

        public BlockInfoUnavailableLine(string testid, LocalizationKey targetKey)
        {
            name = testid;
            _targetKey = targetKey;
            AddToClassList(UiTypographyClass.TextXs);
            AddToClassList(UiTypographyClass.TextDefault);
        }

        public void Render(BlockInfoFetchState fetchState)
        {
            // 取得済みでここへ来るのは呼び手の取り違え。開発者ログへ出して描かない
            // Reaching here while fetched is a caller mix-up; log it and draw nothing
            if (!BlockInfoFetchStateLogic.TryGetUnavailableKey(fetchState, out var key))
            {
                Debug.LogError($"[BlockInfoUnavailableLine] {name}: no unavailable text for {fetchState}");
                return;
            }
            // 欄の名前は描いた時点の言語で解く（1 秒ごとの再描画で言語の変化にも追従する。D-13）
            // The section name resolves in the current language (the one-second redraw follows language changes, D-13)
            SetText(new UiText(key, new[] { new LocalizationTextParam("target", new UiText(_targetKey).Resolve()) }));
        }
    }
}
```

`CleanRoomSectionView.cs`（`VisualElement` に `UiStack(UiGap.Px2)` を 1 つ持つ〈`UiStack` は sealed。3 欄の View はどれもこの形〉。子: `BlockInfoUnavailableLine("clean-room-fetch-state", cleanRoomInfoLabel)` と、`UiStack(Px2)` の `clean-room-section`〈`LackHighlightText("clean-room-headline", Sm)`・`LocalizedLabel clean-room-air`〈`text--sm`・`text--default`〉・清浄機の行の器・`LackHighlightText("clean-room-no-filter", Xs)`・`LackHighlightText("clean-room-halt-reason", Sm)`〉。`Render(ContextEntry<CleanRoomFetchedDetail>)`: `UnavailableContextEntry` なら前者だけ、`FetchedContextEntry` なら `CleanRoomLogic.Display(value)` で見出し〈`HeadlineInsufficient`〉、`EnclosedCleanRoomDisplay` のときだけ空気・清浄機〈`clean-room-air-filter-<i>`・`text--xs`〉・`ShowsNoFilter`、`TryGetHaltReason` が真のときだけ停止理由。表示の切り替えは全部 `SetItemShown`）。`CleanRoomYieldSectionView`（`clean-room-yield-fetch-state`〈`cleanRoomYieldInfoLabel`〉か `clean-room-yield-section` に `CleanRoomYieldLogic.Lines` の行を `LocalizedLabel { name = line.RowId }`〈`text--xs`・`text--default`〉で）。`MachineInputStallSectionView`（`machine-input-stall-fetch-state`〈`machineInputStallInfoLabel`〉か `LackHighlightText("machine-input-stall", Xs)` に `new UiText(MachineInputStallLogic.MessageKey(reason), new[] { new LocalizationTextParam("itemName", content.ItemName(id).Resolve()) })`・不足トーン）

`BlockContextSectionsView.cs`:
```csharp
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.BlockInventory.Context;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 文脈の 3 欄（部屋 → 抽選 → 搬入停止）を Stack gap=sm で並べる。無い欄は SetItemShown で隠す
    /// Lays out the three context sections (room → draw → stall) in Stack gap=sm, hiding absent ones via SetItemShown
    /// </summary>
    public sealed class BlockContextSectionsView : VisualElement
    {
        private readonly UiStack _stack = new(UiGap.Sm);
        private readonly CleanRoomSectionView _cleanRoom = new();
        private readonly CleanRoomYieldSectionView _cleanRoomYield = new();
        private readonly MachineInputStallSectionView _stall;

        public BlockContextSectionsView(GameContentPresentation content)
        {
            name = "block-context-sections";
            _stall = new MachineInputStallSectionView(content);
            _stack.AddItem(_cleanRoom);
            _stack.AddItem(_cleanRoomYield);
            _stack.AddItem(_stall);
            Add(_stack);
        }

        public void Render(BlockContextViewModel viewModel)
        {
            // 無い欄は隠し、ある欄だけ描く（Absent の判定は ContextEntry.IsAbsent の 1 本）
            // Hide absent sections and draw the rest (absence is judged once by ContextEntry.IsAbsent)
            _stack.SetItemShown(_cleanRoom, !viewModel.CleanRoom.IsAbsent);
            if (!viewModel.CleanRoom.IsAbsent) _cleanRoom.Render(viewModel.CleanRoom);
            _stack.SetItemShown(_cleanRoomYield, !viewModel.CleanRoomYield.IsAbsent);
            if (!viewModel.CleanRoomYield.IsAbsent) _cleanRoomYield.Render(viewModel.CleanRoomYield);
            _stack.SetItemShown(_stall, !viewModel.MachineInputStall.IsAbsent);
            if (!viewModel.MachineInputStall.IsAbsent) _stall.Render(viewModel.MachineInputStall);
        }
    }
}
```
（`IsAbsent` は Task 4 の `ContextEntry<T>` の計算プロパティ。`HasAny` も同じ判定を使う）

`ContextSectionPresenter.cs`（機械の Model と文脈の Model を別々に購読し〈どちらの通知でも同じ判断をやり直す〉、`IsApplicable = ContextSectionVisibility.Shows(viewModel, machine.IsPresent)`〈Web `hasContextSections`。判断は State。D-12・D-32〉。真なら `view.Render(viewModel)`）。`ContextSectionFactory.cs`（`Slot => SectionStackSlot.Context`。ctor で `IBlockContextModel`・`IMachineSectionModel`・`GameContentPresentation`）

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.BlockInventory.Context"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Context moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Context
git commit -m "feat(uitk): 部屋・抽選・搬入停止の文脈の区画を足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 8: 採掘機・ポンプ・発電機の区画（`Screens/BlockInventory/Family/`）

**Files:**
- Create: `S/Client.Ui/Screens/BlockInventory/Family/IDetailSectionView.cs`・`DetailSectionPresenter.cs`・`MinerSectionView.cs`・`MinerSectionFactory.cs`・`PumpSectionView.cs`・`PumpSectionFactory.cs`・`GeneratorSectionView.cs`・`GeneratorSectionFactory.cs`・`Family.uss`
- Test: `S/Client.Tests/Ui/Screens/BlockInventory/Family/BlockDetailsFamilyTest.cs`・`FamilyDesignTest.cs`

**Interfaces:**
- Consumes: Task 3（`IMinerDetailModel`・`IPumpDetailModel`・`IGeneratorDetailModel`）、alpha（`PowerRateText`・`MachineStateRow`・`PerMinuteRateRow`・`LackHighlightText`）、C4-b（`ProgressArrowBar`）、C2-c（`GaugeBar`）、`GameIcon`、L0-s4b（`BlockDetailMath.FuelRatio`）
- Produces: `IDetailSectionView<T> { VisualElement Root { get; } void Render(T viewModel); }`・`DetailSectionPresenter<T>`（1 つの Model の `SectionContent<T>` を 1 つの View へ流す区画の Presenter）

- [ ] **Step 1: 失敗するテストを書く**

`BlockDetailsFamilyTest.cs`（`ScreenTestHost`。偽の `FakeMinerDetailModel`・`FakePumpDetailModel`・`FakeGeneratorDetailModel` を差して View＋Presenter を組み Attach）:
- `renders ${type} detail section`: miner・pump・gearPump・generator の Present でそれぞれ `miner-section`・`pump-section`・`pump-section`・`generator-section` が出る（machine は Task 9 の `BlockDetailsMachineTest` が同じタグで見る。gearMachine・filterSplitter は S4-special）
- 電力駆動の採掘機は充足率不足を赤で出す: 電力 50/100・歯車駆動でない → `miner-power-rate` が不足トーン。歯車駆動なら不足トーンでない
- 油井は電力充足率と公称生成速度を出し鉱脈外なら警告行に切り替わる: 電気ポンプ・汲み上げ 1 件 → `pump-state-label`・`pump-power-rate`・`pump-pumping-fluids` の毎分。0 件 → `pump-no-vein` が不足トーン・`pump-pumping-fluids` が隠れる
- 歯車ポンプは電力行を持たず歯車行を出す: `GearPumpDetail` で `pump-state-label`・`pump-power-rate` が無い（歯車行は S4-special）
- タグ無し: 発電機の燃料比 `GaugeBar` の値・稼働率の文（`round(0.456*100)` → 46）・採掘機の `ProgressArrowBar` の幅が進捗

`FamilyDesignTest.cs`（4 件: ゲージとモード切替＝発電機は `GaugeBar`・採掘機は `ProgressArrowBar`／寒色トークン＝`Family.uss` が色を書かず部品の既定トークンのまま〈`ThemeTokenFile.ResolveColor` で部品の地と棒の色がゲージのトークン〉／ポンプの流体アイコン＝`GameIcon` が `--slot-size` 四方で `-unity-background-scale-mode: scale-to-fit`／稼働ラベルと分間レート＝ポンプが alpha の `MachineStateRow`・`PerMinuteRateRow` の要素を持つ）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → Expected: 型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`DetailSectionPresenter.cs`:
```csharp
using System;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Common;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 1 つの Model の値を 1 つの View へ流す区画の Presenter。値が無ければ隠す
    /// A section presenter feeding one model's value into one view; hidden when absent
    /// </summary>
    public sealed class DetailSectionPresenter<T> : IBlockInventorySection
    {
        private readonly IReadOnlyReactiveProperty<SectionContent<T>> _source;
        private readonly IDetailSectionView<T> _view;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private IDisposable _subscription;

        public VisualElement Root => _view.Root;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public DetailSectionPresenter(IReadOnlyReactiveProperty<SectionContent<T>> source, IDetailSectionView<T> view)
        {
            _source = source;
            _view = view;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            _subscription = _source.Subscribe(Apply);
        }

        public void Detach()
        {
            _subscription.Dispose();
        }

        private void Apply(SectionContent<T> content)
        {
            if (content.IsPresent) _view.Render(content.Value);
            _isApplicable.Value = content.IsPresent;
        }
    }
}
```

`MinerSectionView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Components.Gauges;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Parts.Rows;
using Client.Ui.State.BlockInventory.Family;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 採掘機の区画（Web MinerSection）。進捗の棒 → 充足率 → 採掘中の品ごとの毎分
    /// The miner section (Web MinerSection): progress bar → power rate → per-minute per mined item
    /// </summary>
    public sealed class MinerSectionView : IDetailSectionView<MinerDetail>
    {
        private readonly UiStack _root = new(UiGap.Xs) { name = "miner-section" };
        private readonly ProgressArrowBar _progress = new();
        private readonly PowerRateText _powerRate = new("miner-power-rate");
        private readonly UiGroup _items = new(UiGap.Xs) { name = "miner-mining-items" };
        private readonly GameContentPresentation _content;

        public VisualElement Root => _root;

        public MinerSectionView(GameContentPresentation content)
        {
            _content = content;
            _root.AddItem(_progress);
            _root.AddItem(_powerRate);
            _root.AddItem(_items);
        }

        public void Render(MinerDetail detail)
        {
            // 歯車採掘機の不足赤は網の停止理由行が担う（ADR 0056）
            // Gear miners leave shortage red to the network stop-reason row (ADR 0056)
            _progress.SetValue(detail.Progress);
            _powerRate.Render(detail.CurrentPower, detail.RequestPower, !detail.GearDriven);
            _items.ClearItems();
            foreach (var item in detail.MiningItems) _items.AddItem(Row(item));

            #region Internal

            VisualElement Row(MinerMiningItem item)
            {
                var slot = new ItemSlot($"miner-mining-item-{item.ItemId.AsPrimitive()}");
                slot.SetContent(new ItemSlotContent(_content.Item(item.ItemId), null, new ItemSlotMarks(false, false, false), null, null, null));
                var row = new PerMinuteRateRow(slot);
                row.Render(item.ItemsPerMinute);
                return row;
            }

            #endregion
        }
    }
}
```
（Web の採掘機の `ItemSlot` は testid を持たない。UITK の `ItemSlot` は frame の testid が要るので `miner-mining-item-<id>` を付ける〈Web に無い名前。録画・テストの照準用〉）

`PumpSectionView.cs`（`UiStack(Xs)` `pump-section`＝`MachineStateRow("pump-state-label","pump-power-rate")` を `MountInto`〈`ElectricPumpDetail` のときだけ見せる。alpha の `MachineStateRow` が自分の要素の出し入れを `MountInto` の親で持つ形なら、行の器 `UiGroup` を 1 つ挟んで `SetItemShown` する〉→ `UiGroup(Xs)` `pump-pumping-fluids`〈`PerMinuteRateRow(GameIcon)`。`GameIcon` に `pump-section__fluid-icon` と `SetIcon(content.Fluid(id), GameIconFallback.None)`〉→ `LackHighlightText("pump-no-vein", Sm)`〈汲み上げ対象 0 件のときだけ・不足トーン〉）。`GeneratorSectionView.cs`（`UiStack(Xs)` `generator-section`＝`GaugeBar("generator-fuel-progress")` に `(float)BlockDetailMath.FuelRatio(remaining, current)` → `LocalizedLabel generator-operating-rate`〈`text--sm`・`text--default`、`new UiText(generatorOperatingRate, { "rate" = Math.Round(rate*100, MidpointRounding.AwayFromZero) })`。JS の `Math.round` は .5 を上へ丸めるので負でない値では `AwayFromZero` と一致〉）

3 つの工場（`Slot` は Miner・Pump・Generator。`Create(mount)` は `new DetailSectionPresenter<MinerDetail>(_miner.Current, new MinerSectionView(_content))` 等。工場は自分の族の Model だけを ctor で受ける）

`Family.uss`:
```css
/* web: features/blockInventory/details/pump/pumpSection.module.css .icon { width/height: var(--slot-size, 3rem); object-fit: contain } をポンプの液体アイコンに写す / ports the pump fluid icon's slot-size box with object-fit: contain */
.pump-section__fluid-icon {
    width: var(--slot-size);
    height: var(--slot-size);
    -unity-background-scale-mode: scale-to-fit;
}
```

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.BlockInventory.Family"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Family moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Family
git commit -m "feat(uitk): 採掘機・ポンプ・発電機の区画を足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 9: 機械の区画（インベントリモード・ヘッダ・フッタ・枠の操作）（`Screens/BlockInventory/Machine/`）

**Files:**
- Create: `S/Client.Ui/Screens/BlockInventory/Machine/MachineSectionFactory.cs`・`MachineSectionPresenter.cs`・`MachineSectionView.cs`・`MachineSectionFooter.cs`・`MachineInventoryBody.cs`・`MachineProcessRow.cs`・`MachineModuleBand.cs`・`SelectedRecipeHeader.cs`・`MachineRecipeNames.cs`・`Machine.uss`
- Create: `S/Client.Ui/Screens/BlockInventory/Machine/Slots/MachineSlotContents.cs`・`BlockSlotGestureCommands.cs`
- Test: `S/Client.Tests/Ui/Screens/BlockInventory/Machine/MachineSectionTest.cs`・`MachineInventoryBodyTest.cs`・`MachineGesturesTest.cs`・`SelectedRecipeHeaderTest.cs`・`ModuleSlotStackLimitTextTest.cs`・`BlockDetailsMachineTest.cs`・`MachineDesignTest.cs`・`MachineSectionFixtures.cs`
- Modify: `S/Client.Tests/Ui/Screens/BlockInventory/Context/ContextSectionOrderTest.cs`（「搬入停止欄は機械欄より上に赤字で描く」）

**Interfaces:**
- Consumes: Task 5（`IMachineSectionModel`・`MachineSectionViewModel`・`MachineSelectionModeState`・`IMachineRecipeSelectCommand`）、Task 6（`FluidSlotContentMapper.MapWithGhost`）、Task 7（`BlockContextSectionsView`・`IBlockContextModel`）、alpha（`IBlockSlotCommands`・`BlockSlotCommands.CommandName`・`MachineStateRow`・`LackHighlightText`）、C3-a（`ItemSlot`・`SlotGrid`）、C4-a（`FluidSlotRow`・`FluidAmountSlot`・`SlotAmountBadge.Hidden`）、C4-b（`ProgressArrowGlyph`）、C2-c（`GameScrollView`）、C3-b（`HoverTooltipMark`）、`UiCommandResultToaster`
- Produces:
```csharp
namespace Client.Ui.Screens.BlockInventory.Machine
public sealed class MachineSectionView   // Presenter から Render だけを受ける。枠の押下は中の View が IBlockSlotCommands を直接呼び、ヘッダの押下だけを IObservable で出す
{
    public MachineSectionView(bool fillsPanelHeight, GameContentPresentation content, IBlockSlotCommands commands, UiCommandResultToaster toaster);   // Task 12 で末尾に IMachineRecipeSelectCommand を足す（D-33）
    public VisualElement Root { get; }
    public IObservable<Unit> OnChangeRecipeRequested { get; }
    public void Render(MachineSectionViewModel viewModel, BlockContextViewModel context, bool showsSelection);
}
```

- [ ] **Step 1: 失敗するテストを書く**

`MachineSectionFixtures.cs`（State 側の `FakeMachineSectionModel`〈`MachineTestDoubles.cs`〉を使い、偽の `FakeBlockContextModel`、押下を覚える `RecordingBlockSlotCommands : IBlockSlotCommands`〈呼ばれた種類・番地・Shift を列で持ち `UiCommandAccepted.Instance` を返す〉、State 側の `RecordingRecipeSelectCommand`、組み立て `MachineSectionHarness.Mount(host, viewModel, fillsPanelHeight)` が View〈記録係のコマンドを ctor で渡す〉＋Presenter を作り Attach して Root を `Mount`）

`MachineInventoryBodyTest.cs`:
```csharp
/// <web-test file="e2e/tests/block/machineRequiredCount.spec.ts">必要数を満たした素材スロットは入っている数/必要数を黒字で出し、個数バッジを重ねない</web-test>
[Test]
public void 必要数を満たした素材は所持と必要を黒字で個数バッジなし()
{
    // 素材 1 に必要 2 の束縛、枠 0 に 3 個
    // Input binding needs 2 of item 1; slot 0 holds 3
    var vm = MachineTestDetails.ViewModelWithInput(required: 2, held: 3);
    var root = MachineSectionHarness.Mount(this, vm, false).Root;
    simulate.FrameUpdate();
    var slot = Find<VisualElement>(root, "machine-input-slot-0");
    Assert.AreEqual("3/2", ShortageText(slot));
    Assert.IsFalse(IsShortageLacking(slot));
    Assert.IsNull(CountBadge(slot));
}
```
（`ShortageText`・`IsShortageLacking`・`CountBadge` は C3-a の `ItemSlot` のテストの補助と同じ要素の引き方。`grep -rn "shortage" moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Slots` で前例の名前を確かめる）。残り 3 件: 不足は数字だけ赤・アイコンは減光しない／出力に実物があっても必要数なし／必要数の数字の上で右押下 → `RecordingBlockSlotCommands` に `RightDown(0)`（数字の要素の `worldBound.center` で `simulate.MouseDown(button: 1)`）

`MachineGesturesTest.cs` 3 件（入力の空手右押下 → `RightDown(入力の番地)`／出力のダブルクリック → `DoubleClick(InputSlotCount + 0)`／入力の Shift＋左押下 → `LeftDown(0, true)`。Web の「block_inventory.split を送る」等は alpha の型付きコマンドが呼ばれたことで写す。配分の宛先の主張は `MachineSlotRestrictionContributorTest` が持つ）

`MachineSectionTest.cs`（インベントリモードの 6 件。レシピ 0 件の機械は `machine-selected-recipe` も `machine-recipe-selection` も無く `machine-inventory-body` がある／停止中は `machine-power-rate` が無く `machine-state-label` がある／歯車駆動は電力 50/100 でも不足トーンでなく電力駆動は不足トーン／大型（`fillsPanelHeight` 真）だけ本文が `GameScrollView` の中／出力空き不足で `machine-state-label` が待機中のまま `machine-wait-reason` が不足トーン／空き不足でなければ `machine-wait-reason` 無し）

`SelectedRecipeHeaderTest.cs` 2 件（代表が液体 → `machine-selected-recipe-fluid` があり量の帯 `machine-selected-recipe-fluid-amount` が無い／アイテム → `ItemSlot`）＋タグ無し 1 件「出力 2 品でもヘッダは代表の名前だけ」（Web `SelectedRecipeHeader.tsx:17` の `useRecipeDisplayName`。「他 N 件」は選択リストの行だけ）、`ModuleSlotStackLimitTextTest.cs` 1 件（上限 8 → 文に 8。Model の値を 16 に差し替えると 16）、`BlockDetailsMachineTest.cs` 3 件（`renders ${type} detail section` の machine → `machine-section`／`${type} は充足率不足でも…` を gearMachine・gearMiner〈採掘機は Task 8 の View を同じく組む〉の 2 例／出力 2 個・秒 4 → `machine-items-per-minute` の数字が `30`）、`MachineDesignTest.cs` 2 件（3 カラム＝`machine-process-row` の左右の器の `resolvedStyle.flexGrow == 1`・`flexBasis == 0` で矢印の中心 x が行の中心 x と ±0.5px／モジュール帯＝`machine-module-label` が `text--sm text--dimmed`・加工行の下端と `machine-module-label` の上端の距離が xs×2〈Web は親 `Stack gap=xs`＋帯の `mt=xs`〉を `worldBound` で比べる）

`ContextSectionOrderTest.cs` に 1 件:「搬入停止欄は機械欄より上に赤字で描く」＝機械の Model が Present・文脈の詰まりが停止中 → `machine-input-stall` が `machine-inventory-body` より上（`worldBound.yMin`）で不足トーン、区画列の先頭に文脈の区画は出ない

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → Expected: 型が無いコンパイルエラー

- [ ] **Step 3: 実装する**

`MachineSlotContents.cs`:
```csharp
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Machine.Slots
{
    /// <summary>
    /// 機械の枠の中身を組む。ゴーストは束縛から、所持/必要は素材の枠だけ（Web MachineInventoryBody の slotAt）
    /// Builds machine slot content: ghosts from the binding, held/required on input slots only (Web MachineInventoryBody slotAt)
    /// </summary>
    public static class MachineSlotContents
    {
        private static readonly ItemSlotMarks NoMarks = new(false, false, false);

        public static ItemSlotContent For(SlotItem slot, BoundItemSlot bound, bool showsRequirement, GameContentPresentation content)
        {
            var icon = slot.IsEmpty ? null : content.Item(slot.ItemId);
            int? count = slot.IsEmpty ? null : slot.Count;
            var ghost = bound is GhostedItemSlot ghosted ? new ItemSlotGhost(content.Item(ghosted.Ghost.ItemId), ghosted.Ghost.Count) : null;
            var shortage = showsRequirement && MachineSlotGhosts.TryGetInputRequirement(slot, bound, out var requirement)
                ? new ItemSlotShortage(requirement.OwnedCount, requirement.RequiredCount, requirement.Lacking, null)
                : null;
            return new ItemSlotContent(icon, count, NoMarks, shortage, ghost, null);
        }
    }
}
```
（引数の null は C3-a の `ItemSlotContent` の「無し」の約束〈既存の呼び手と同じ〉。`ItemSlot` が所持/必要のあるとき個数バッジを出さないことは C3-a の実物で確かめ、出すなら `count` を null にして渡す）

`BlockSlotGestureCommands.cs`（枠を持つ View が直接呼ぶ静的な写し。Presenter は経由しない。設計書 §4.2 A-8）:
```csharp
using Client.Ui.Components.Slots;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;

namespace Client.Ui.Screens.BlockInventory.Machine.Slots
{
    /// <summary>
    /// 機械の枠の押下をブロック側の型付きコマンドへ写す（alpha BlockItemGrid と同じ写し: 左・右・ダブルだけ。進入は使わない）
    /// Maps machine slot gestures onto the typed block commands (same mapping as alpha BlockItemGrid: left, right, double only; no enter)
    /// </summary>
    public static class BlockSlotGestureCommands
    {
        public static void Run(IBlockSlotCommands commands, UiCommandResultToaster toaster, int blockSlot, SlotGesture gesture)
        {
            switch (gesture.Kind)
            {
                case SlotGestureKind.LeftDown:
                    toaster.Report(BlockSlotCommands.CommandName, commands.LeftDown(blockSlot, gesture.Shift));
                    return;
                case SlotGestureKind.RightDown:
                    toaster.Report(BlockSlotCommands.CommandName, commands.RightDown(blockSlot));
                    return;
                case SlotGestureKind.DoubleClick:
                    toaster.Report(BlockSlotCommands.CommandName, commands.DoubleClick(blockSlot));
                    return;
                default:
                    // silent-ok: 進入とホバーはブロックの枠では使わない（Web と同じ） / 掴みの分割ドラッグは C4-e が持ち、録画 uitk-machine-ui で見る
                    return;
            }
        }
    }
}
```
（同じ写しが alpha の `BlockItemGrid` にもある。寄せる件は「本 PR 外のリファクタ提案」に置き、このレーンでは扱わない）

`MachineProcessRow.cs`（加工行。`VisualElement` `machine-process-row`＝左の器 `machine-process-row__input`〈`SlotGrid("machine-input-slots", max(1,n))`〉・`ProgressArrowGlyph("machine-progress-arrow")`・右の器 `machine-process-row__output`〈`SlotGrid("machine-output-slots", max(1,n))`〉。ctor で `IBlockSlotCommands`・`UiCommandResultToaster` を受ける〈alpha の `BlockItemGrid` と同じ形。機械の枠はゴーストと必要数が要るので `BlockItemGrid` 自体は使えない〉。`Render(MachineSectionViewModel vm)` で各 `ItemSlot($"machine-input-slot-{i}")`／`($"machine-output-slot-{j}")` を作り直し、`SetContent(MachineSlotContents.For(...))`、`OnSlotGestured` を購読して `BlockSlotGestureCommands.Run(_commands, _toaster, 番地, g)` を直接呼ぶ〈作り直す枠の購読は前の枠と一緒に捨てる: 枠の購読を `CompositeDisposable` に持ち、作り直しの頭で `Clear()`〉。矢印は `SetProgress(vm.Detail.Progress)`。枠の testid `machine-input-slot-<i>` は Web に無い名前〈Web の枠は testid を持たず、テストは `machine-input-slots` の子の順で引く〉。録画の照準のために付ける）

`MachineModuleBand.cs`（`VisualElement` の器 `machine-module-band-wrap`〈USS で `padding-top: var(--mantine-spacing-xs)`。`UiStack` は子の `marginTop` を gap で上書きするので、Web の `mt="xs"` は器の内側の余白で写す〉の中に `UiStack(Px4)` `machine-module-band`〈`UiLayoutClass.StackAlignCenter`〉。ctor で `IBlockSlotCommands`・`UiCommandResultToaster` を受け、モジュールの枠の押下を `MachineProcessRow` と同じく直接呼ぶ。`LocalizedLabel machine-module-label`〈`upgradeSlots`・`text--sm text--dimmed`〉→ `LocalizedLabel machine-module-stack-limit`〈`moduleSlotStackLimit {limit}`・`text--xs text--dimmed`〉→ `SlotGrid("machine-module-slots", n)`。`Render` は module 0 なら器ごと隠す〈親の `UiStack` が器に `SetItemShown`〉）

`MachineInventoryBody.cs`（`VisualElement` `machine-inventory-body` の中に `UiStack(Xs)`〈`UiLayoutClass.StackAlignCenter`〉＝加工行 → モジュール帯 → `FluidSlotRow("machine-fluid-slots")`〈`vm.SlotView.Fluids` を `FluidSlotContentMapper.MapWithGhost(vm.FluidSlots[f.Index], f, content)`、`ClearProgress()`〉→ 出力ごとの `machine-items-per-minute`〈`BlockDetailMath.TryGetItemsPerMinute(count, time, out rate)` が真の行だけ。文は `LocalizedLabel`〈`productionPerMinute`〉＋数字の `Label`〈`rate.ToString(CultureInfo.InvariantCulture)`。Web は JS の数値をそのまま出す〉の横並び〉）

`SelectedRecipeHeader.cs`（`VisualElement` `machine-selected-recipe`〈`cursor: link`〉の中に `UiGroup(Xs)`〈`UiLayoutClass.GroupJustifyCenter`〉。代表が `ItemRecipeDisplaySubject` なら `ItemSlot("machine-selected-recipe-item")`、`FluidRecipeDisplaySubject` なら `FluidAmountSlot("machine-selected-recipe-fluid")` に `SetFluid(content.Fluid(id), SlotAmountBadge.Hidden)` → 名前 `machine-selected-recipe-name`〈`MachineRecipeNames.Representative(subject, content)`＝代表の名前だけ〈アイテム名か液体名〉。「他 N 件」は付けない〈Web `useRecipeDisplayName`〉。`MachineRecipeNames` は代表の名前 `Representative` と選択リストの行の名前 `Row` の 2 関数を持つ静的クラス〈名前の解決に `GameContentPresentation` が要るので Client.Ui に置く〉〉→ 秒 `machine-selected-recipe-time`〈`recipeDuration {seconds}`・`text--sm text--dimmed`。秒は `MachineRecipeData.Time` を JS の数値の書式で〉。`HoverTooltipMark.Mark(this, new UiText(changeRecipe))`。押下は `ClickEvent` を `Subject<Unit>` へ）

`MachineSectionFooter.cs`（`VisualElement` の中に `UiGroup(Xs)` `_row`〈`UiLayoutClass.GroupJustifyCenter`〉＝`MachineStateRow("machine-state-label","machine-power-rate").MountInto(_row)`＋`LackHighlightText("machine-wait-reason", Sm)`。`Render(MachineDetail d)`: `stateRow.Render(d.State, d.CurrentPower, d.RequestPower, !d.GearDriven)`、`_row.SetItemShown(wait, d.OutputSpaceShort)`、出すときは `Render(new UiText(machineWaitOutputSpaceShort), true)`）

`MachineSectionView.cs`:
```csharp
using System;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画（Web MachineSection）。外殻がフッタと伸長を 1 か所で持ち、モードで分かれるのは中身だけ
    /// The machine section (Web MachineSection); one shell owns the footer and stretch, only the body branches by mode
    /// </summary>
    public sealed class MachineSectionView
    {
        public const string FillPanelHeightClassName = "machine-section--fill-panel-height";

        private readonly UiStack _root = new(UiGap.Sm) { name = "machine-section" };
        private readonly UiStack _inventoryContent = new(UiGap.Sm);
        private readonly BlockContextSectionsView _context;
        private readonly SelectedRecipeHeader _header;
        private readonly MachineInventoryBody _body;
        private readonly MachineSectionFooter _footer = new();
        private readonly VisualElement _inventoryHost;

        public VisualElement Root => _root;
        public IObservable<Unit> OnChangeRecipeRequested => _header.OnPressed;

        public MachineSectionView(bool fillsPanelHeight, GameContentPresentation content, IBlockSlotCommands commands, UiCommandResultToaster toaster)
        {
            // 枠の押下は本体の枠が直接コマンドへ渡す（設計書 §4.2 A-8。Presenter は中継しない）
            // Slot presses go straight from the body's slots to the commands (design §4.2 A-8; no presenter relay)
            _context = new BlockContextSectionsView(content);
            _header = new SelectedRecipeHeader(content);
            _body = new MachineInventoryBody(content, commands, toaster);
            _inventoryContent.AddItem(_context);
            _inventoryContent.AddItem(_header);
            _inventoryContent.AddItem(_body);

            // 大型面の本文は器高のスクロール器に入れ、フッタに重ならない（列車の本文と同じ土台）
            // On the large panel the body sits in a container-height scroller so it never overlaps the footer
            _root.EnableInClassList(FillPanelHeightClassName, fillsPanelHeight);
            _inventoryHost = fillsPanelHeight ? WrapInScroll(_inventoryContent) : _inventoryContent;
            _root.AddItem(_inventoryHost);
            _root.AddItem(_footer);

            #region Internal

            static VisualElement WrapInScroll(VisualElement content)
            {
                var scroll = new GameScrollView("machine-inventory-scroll");
                scroll.AddToClassList("machine-section__inventory-scroll");
                scroll.Add(content);
                return scroll;
            }

            #endregion
        }

        public void Render(MachineSectionViewModel viewModel, BlockContextViewModel context, bool showsSelection)
        {
            // 選択モードは Task 12 で選択リストを足す。それまで showsSelection は本体を出したままにする
            // Selection mode gets its list in Task 12; until then showsSelection keeps the body shown
            _context.Render(context);
            _inventoryContent.SetItemShown(_context, context.HasAny);
            var hasSelectedRow = viewModel.TryGetSelectedRow(out var selectedRow);
            _inventoryContent.SetItemShown(_header, hasSelectedRow);
            if (hasSelectedRow) _header.Render(selectedRow, viewModel);
            _body.Render(viewModel);
            _footer.Render(viewModel.Detail);
        }
    }
}
```
（Task 12 で `showsSelection` の分岐と選択リストを足す。この段階のコードに「Task 12 まで未使用の引数」が残るので、Task 12 を同じ PR で必ず入れる）

`MachineSectionPresenter.cs`:
```csharp
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の Presenter。機械と文脈の Model を購読して View へ流し、選択モードを変える押下（ヘッダ・行）だけを受ける
    /// The machine section presenter: feeds the machine and context models into the view and handles only the mode-changing presses (header, rows)
    /// </summary>
    public sealed class MachineSectionPresenter : IBlockInventorySection
    {
        private readonly MachineSectionView _view;
        private readonly IMachineSectionModel _machine;
        private readonly IBlockContextModel _context;
        private readonly MachineSelectionModeState _mode = new();
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root => _view.Root;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public MachineSectionPresenter(MachineSectionView view, IMachineSectionModel machine, IBlockContextModel context)
        {
            _view = view;
            _machine = machine;
            _context = context;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            // 値の変化と押下を購読する（Detach で全部切る）
            // Subscribe value changes and presses (all released on Detach)
            _machine.Current.Subscribe(_ => Apply()).AddTo(_subscriptions);
            _context.Current.Subscribe(_ => Apply()).AddTo(_subscriptions);
            _view.OnChangeRecipeRequested.Subscribe(_ =>
            {
                _mode.OpenSelection();
                Apply();
            }).AddTo(_subscriptions);
        }

        public void Detach()
        {
            _subscriptions.Clear();
        }

        private void Apply()
        {
            var machine = _machine.Current.Value;
            if (machine.IsPresent) _view.Render(machine.Value, _context.Current.Value, _mode.ShowsSelection(machine.Value));
            _isApplicable.Value = machine.IsPresent;
        }
    }
}
```

`MachineSectionFactory.cs`（`Slot => SectionStackSlot.Machine`。ctor で `IMachineSectionModel`・`IBlockContextModel`・`IBlockSlotCommands`・`UiCommandResultToaster`・`GameContentPresentation`・`IMachineRecipeSelectCommand`〈Task 12 で使う〉。`Create(mount)` は `new MachineSectionPresenter(new MachineSectionView(mount.FillsPanelHeight, _content, _commands, _toaster), _machine, _context)`。コマンドとトースターは View へ渡し、Presenter は持たない）

`Machine.uss`:
```css
/* web: details/machine/machineInventoryBody.module.css の .processRow（grid 1fr auto 1fr・column-gap 16px）・.inputSide のつまみ上書き・モジュール帯の mt=xs・style.module.css の .fillPanelHeight と .machineInventoryScroll を機械の区画に写す / ports the process row grid, the input-side knob overrides, the module band's mt=xs, fillPanelHeight and machineInventoryScroll */
.machine-section--fill-panel-height {
    flex-grow: 1;
    flex-shrink: 1;
    min-height: 0;
}

.machine-section__inventory-scroll {
    flex-grow: 1;
    flex-shrink: 1;
    min-height: 0;
}

/* grid の 1fr auto 1fr は左右を等幅の伸縮列にして矢印を中央に固定する（UITK に grid が無い） */
.machine-process-row {
    flex-direction: row;
    align-items: center;
    width: 100%;
}

.machine-process-row__input {
    flex-grow: 1;
    flex-basis: 0;
    flex-direction: row;
    justify-content: flex-end;
    margin-right: 16px;
    --shortage-count-right: 2px;
    --shortage-count-bottom: -3px;
    --shortage-count-font-size: 15px;
}

.machine-process-row__output {
    flex-grow: 1;
    flex-basis: 0;
    flex-direction: row;
    justify-content: flex-start;
    margin-left: 16px;
}

/* UiStack は子の marginTop を gap で上書きするので、mt=xs は器の内側の余白で写す */
.machine-module-band-wrap {
    padding-top: var(--mantine-spacing-xs);
}

.machine-selected-recipe {
    cursor: link;
}
```
（`--mantine-spacing-xs` が Tokens.uss に無ければ同値のトークン〈10px〉を `grep -n "spacing-xs" moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/*.uss` で引いて使う。`.machine-module-band-wrap` 等のクラスは各 View の ctor で `AddToClassList`。揃え〈中央寄せ・折り返しなし〉は `UiLayoutClass` で付け、この USS に `justify-content`・`align-items`・`flex-wrap` を書かない〈`UiGroup` の子は内側の行に入るので外側に書いても効かない〉。スクロールバーの色〈Web §8.10 のネイビー〉は `GameScrollView` の既定に任せ、差が出たら Task 14 で LX）

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.BlockInventory.(Machine|Context)"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Machine moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory
git commit -m "feat(uitk): 機械の区画のインベントリモード・ヘッダ・フッタ・枠の操作を足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 10: 登録・共有区画・回帰と設計の L1（`Client.Starter`・tss）

**Files:**
- Create: `S/Client.Starter/Registration/GameUi/Screens/BlockInventoryMachineGameUiRegistration.cs`（`Screens/` が無ければ作る。E-1・D-34）
- Modify: `S/Client.Starter/Registration/MainGameUiRegistration.cs`（共有サービスの並びの末尾に `IBlockFluidSlotsModel` の 1 行・画面登録区画の末尾に 1 行）
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画の末尾に `@import` 3 行。選択リストの USS は Task 12）
- Test: `S/Client.Tests/Ui/Screens/BlockInventory/Machine/SectionStackMachineRegressionTest.cs`・`SectionStackRegressionEntries.cs`・`MachineDesignTest.cs`（2 件を足す）

**Interfaces:**
- Consumes: Task 3〜9 の全部、alpha の登録の形（alpha D-1）
- Produces: DI の登録（区画の工場 7 本〈Context・Machine・Miner・Pump・Generator・FluidDetails・FluidRow〉・寄与 2 本・Model 5 本〈機械・採掘機・ポンプ・発電機・文脈〉・液体の複製の読み手・コマンド・送信）と、共有サービスの並びの `IBlockFluidSlotsModel`（S4-train も ctor で受ける。E-1）

- [ ] **Step 1: 登録ファイルを書く**

```csharp
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Family;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Family.Models;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using VContainer;

namespace Client.Starter.Registration.GameUi.Screens
{
    /// <summary>
    /// 機械と汎用族の区画・詳細 Model・alpha の口への寄与を登録する（S4-machine）
    /// Registers the machine and family sections, detail models and contributions to alpha's ports (S4-machine)
    /// </summary>
    public static class BlockInventoryMachineGameUiRegistration
    {
        public static void Register(IContainerBuilder builder)
        {
            // 詳細 Model（追跡ブロックを購読して配る）
            // Detail models (subscribe the tracked block and publish)
            builder.Register<MachineSectionModel>(Lifetime.Singleton).As<IMachineSectionModel>().AsImplementedInterfaces();
            builder.Register<MinerDetailModel>(Lifetime.Singleton).As<IMinerDetailModel>().AsImplementedInterfaces();
            builder.Register<PumpDetailModel>(Lifetime.Singleton).As<IPumpDetailModel>().AsImplementedInterfaces();
            builder.Register<GeneratorDetailModel>(Lifetime.Singleton).As<IGeneratorDetailModel>().AsImplementedInterfaces();
            builder.Register<BlockContextModel>(Lifetime.Singleton).As<IBlockContextModel>().AsImplementedInterfaces();

            // 選ぶコマンドと送信の外部境界
            // The select command and the sending boundary
            builder.Register<MachineRecipeSelectCommand>(Lifetime.Singleton).As<IMachineRecipeSelectCommand>();
            builder.Register<VanillaMachineRecipeSelectionSender>(Lifetime.Singleton).As<IMachineRecipeSelectionSender>();

            // alpha の口への寄与
            // Contributions to alpha's ports
            builder.Register<RecipeMachineLargePanelRule>(Lifetime.Singleton).As<IBlockInventoryLargePanelRule>();
            builder.Register<MachineSlotRestrictionContributor>(Lifetime.Singleton).As<IBlockSlotRestrictionContributor>();

            // 区画の工場（Slot ごとに 1 本）
            // Section factories (one per slot)
            builder.Register<ContextSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<MachineSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<MinerSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<PumpSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<GeneratorSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<FluidDetailsSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<FluidRowSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
        }
    }
}
```
（`IBlockFluidSlotsModel` と液体の複製の読み手 `IFluidReplicaReader` はここに書かない: S4-train も ctor で受ける共有サービスなので、Step 2 で `MainGameUiRegistration` の共有サービスの並びに足す〈E-1・alpha D-22〉。`AsImplementedInterfaces` は `IInitializable` の登録のため。前例の Model の登録〈alpha の `BlockInventoryPanelModel`〉と同じ書き方に揃える。`GameContentPresentation`・`UiCommandResultToaster`・`IBlockSlotCommands` が DI に在ることを `grep -rn "GameContentPresentation\|UiCommandResultToaster" moorestech_client/Assets/Scripts/Client.Starter` で確かめる）

- [ ] **Step 2: 共有区画に追記する**

`MainGameUiRegistration.cs` の共有サービスの並びの末尾（alpha の `BlockItemGridFactory` 等の後）に:
```csharp
builder.Register<FluidNetworkReplicaReader>(Lifetime.Singleton).As<IFluidReplicaReader>();
builder.Register<BlockFluidSlotsModel>(Lifetime.Singleton).As<IBlockFluidSlotsModel>().AsImplementedInterfaces();
```
（パイプの変化は `IFluidReplicaReader.OnReplicaChanged` で届き、刻みは渡さない〈設計書 [A-57]・D-36〉）。画面登録区画の末尾（alpha の `BlockInventoryGameUiRegistration.Register(builder);` の後）に `BlockInventoryMachineGameUiRegistration.Register(builder);`（`using Client.Starter.Registration.GameUi.Screens;` を足す。S4-inventory の登録と同じ名前空間）。`GameUiTheme.tss` の `Screens` 区画の末尾に:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/Machine/Machine.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/Family/Family.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/Fluid/Fluid.uss");
```

- [ ] **Step 3: 回帰と設計の L1 を書く**

`SectionStackMachineRegressionTest.cs`:
```csharp
/// <web-test file="e2e/tests/regression/sectionStack.spec.ts">${entry.type}は対応セクションだけを表示する</web-test>
[TestCase("chest", new string[0])]
[TestCase("tank", new[] { "generic-block-fluids" })]
[TestCase("machine", new[] { "machine-section", "machine-fluid-slots" })]
[TestCase("gearMachine", new[] { "machine-section" })]
[TestCase("miner", new[] { "miner-section" })]
[TestCase("gearMiner", new[] { "miner-section" })]
[TestCase("generator", new[] { "generator-section" })]
[TestCase("generic", new[] { "generic-block-fluids" })]
[TestCase("filterSplitter", new string[0])]
[TestCase("pump", new[] { "pump-section", "pump-fluid-slots" })]
[TestCase("gearPump", new[] { "pump-section", "pump-fluid-slots" })]
[TestCase("gearGenerator", new string[0])]
public void 対応する区画だけを出す(string entryType, string[] expectedOwnIds)
```
（Web `sectionStack.spec.ts` の `cases` 12 件をそのまま写し、`shown` のうち本レーンの id だけを期待値にする。entry ごとの blockType と詳細〈液体の有無を含む〉は mock-host の `setBlock` の fixture と同じものを `SectionStackRegressionEntries.For(entryType)` が組み、alpha の `BlockInventoryTestBodies.SectionStack(source, blockType, factories)` に本レーンの 7 工場と偽の Model を差す。主張: Web の `sectionIds` のうち本レーンの id〈`machine-section`・`miner-section`・`generator-section`・`pump-section`・`pump-fluid-slots`・`machine-fluid-slots`・`generic-block-fluids`〉で表示中のものの列が `expectedOwnIds` と一致〈entry に無い本レーンの id は出ない〉。歯車・網・項目グリッド・フィルタの id は他レーンの NUnit が見る）

`MachineDesignTest.cs` に 2 件:「%sから禁止Mantine部品とテーマ色を除去する」＝本レーンの USS 4 本（`Machine.uss`・`MachineRecipeSelection.uss`〈Task 12 で足す前は 3 本〉・`Family.uss`・`Fluid.uss`）に `#`・`rgb(`・`rgba(` の直書きが無い（alpha の `BlockInventoryDesignTest` の色の件と同じ判定を自分のファイルに書く）／「レシピ有り機械だけviewer〜items占有の大型パネルへ広げる」＝ alpha の `BlockInventoryTestShell.Create` に本レーンの `RecipeMachineLargePanelRule`（偽の機械の Model で行 1 以上）を `plugged` で渡すと外殻が `block-inventory--large`、行 0 では付かない

- [ ] **Step 4: 全体スイッチ UITK で起動して確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.(Screens.BlockInventory|State.BlockInventory)|UiThemeImportBundleTest|UiStyleConventionTest|UiLayerDependencyDirectionTest|LookExceptionLedgerTest"` → PlayMode で機械・ポンプ・発電機・タンク・パイプを開き `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: `ErrorCount: 0`、全件 PASS、Error ログ 0 件（VContainer の解決失敗・二重登録の例外が無い）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory
git commit -m "feat(uitk): 機械と汎用族の区画を登録し、回帰と設計の L1 を足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 11: 配置の写し（清浄室・低いウィンドウ）

**Files:**
- Test: `S/Client.Tests/Ui/Screens/BlockInventory/Context/CleanRoomLayoutTest.cs`

**Interfaces:**
- Consumes: alpha の `BlockInventoryTestShell.Create`（舞台 1280×720）、Task 6〜10 の区画、C2-c の `GameScrollView`（`GetViewportHeight`・`GetContentHeight`・UITK `ScrollView.ScrollTo`・`contentViewport`）
- Produces: なし

- [ ] **Step 1: 舞台の高さで分ける**

alpha の `BlockInventoryTestShell.Create` の舞台は 1280×720 固定（alpha の Produces）。720 の件だけを L1 で写し、600・1080 の件は Task 15 の録画（`uitk-gameview-resolution-switch.cs` の前例で Game View を 1280×600・1920×1080 に切り替える）で見る。`docs/uitk/disposition.md` には書かず（タグで写した件は処分表に書かない）、録画の証拠を `done.md`「証拠」に書く（D-15）

- [ ] **Step 2: 写す**

`CleanRoomLayoutTest.cs` の 7 件（選択モードの 1 件は Task 12）。Web の `expectReachableInScrollViewport`（`W/e2e/support/layoutAssertions.ts:131-141`: 対象ごとに `scrollIntoViewIfNeeded` → 箱の上下端が視口内 → 中心のヒットテスト）は、テスト内の補助 `ExpectReachableInScrollViewport(GameScrollView scroll, VisualElement target)` で写す: 対象ごとに `scroll.ScrollTo(target)` → `simulate.FrameUpdate()` でレイアウトを確定 → 対象の `worldBound` の `yMin`・`yMax` が `scroll.contentViewport.worldBound` の中（±0.5）、かつ対象の中心で `panel.Pick` が対象かその子孫を返す。`GameScrollView.TryGetContentTop` は中身の上端からの距離でスクロール量に依らないので、到達の判定には使わない。末尾まで 1 回スクロールして複数の対象を見る形にしない（長い本文ではヘッダが視口の上へ出る）

清浄機の台数は `FakeBlockContextModel` の部屋の清浄機の列で、Web の spec ごとに分けて写す: `cleanRoomAirFilterPanel.spec.ts` は 6 部屋（2・8・14・20・30・30 台の未接続〈`cleanRoomAirFilter30FiltersUnconnected`〉）、`cleanRoomManyFilters.spec.ts` は MANY の 16・20 と BOUNDARY の 0・2・8（fixture は `cleanRoomFixtures.ts` と同じ台数）。

Web の `cleanRoomManyFilters.spec.ts` の共通検証（`expectFooterReadable`・`expectSharedBodyViewport`・`expectInventoryBodyReachable`。34-62 行）を、テスト内の補助として 1 か所に写し、各ケースから呼ぶ（`cleanRoomManyFilters.spec.ts:67・77・88` と同じ呼び方）:
- `ExpectFooterReadable(shell)`: フッタの 2 要素（`machine-state-label`・`machine-power-rate`）が本文の `GameScrollView` の外にあり、上下端がパネル（`block-inventory`）の中、中心の `panel.Pick` が当たる
- `ExpectSharedBodyViewport(shell, bodyName)`: `bodyName` の祖先の `GameScrollView` の視口高 ≥ 120（Web の `MIN_BODY_VIEWPORT_HEIGHT` と同じ定数をテストに置く）・視口の下端 ≤ `machine-state-label` の上端・`clean-room-section` がその `GameScrollView` の子孫・その外側に別の `GameScrollView` が無い
- `ExpectInventoryBodyReachable(shell)`: `ExpectFooterReadable` → `machine-selected-recipe`・`machine-input-slots`・`machine-output-slots`・`machine-fluid-slots` の 4 対象にそれぞれ `ExpectReachableInScrollViewport` → `ExpectSharedBodyViewport(shell, "machine-inventory-body")`

主張:
- 清浄機パネル（6 部屋）: 部屋欄の末尾（最後の `clean-room-air-filter-<n-1>`）とフィルタ枠（alpha の `clean-room-filter-grid`）へ `ExpectReachableInScrollViewport` で届き、枠の下端が舞台の下の安全帯（alpha の帯の下端）より上。`clean-room-filter-grid` と `clean-room-section` が同じ `GameScrollView` の子孫で、その外側に別の `GameScrollView` が無い
- 清浄機 2 台・720: 自然高のスクロール器の内容高 ≤ 視口高
- 室内機械・清浄機 2 台（Web `cleanRoomMachineLayout.spec.ts:41-46・58-60`）: (a) `machine-fluid-slots` の矩形を本文の `GameScrollView` の視口で切った矩形が、`machine-power-rate`・`machine-state-label` の矩形と面積を持って交わらない。(b) 視口の `worldBound.yMax` ≤ フッタの各要素の `worldBound.yMin`、かつフッタの下端 ≤ パネルの下端（液体行は本文〈上〉、フッタは下にある）
- 低いウィンドウ・清浄機多数: `machine-state-label` が見え（パネルの下端より上）、`machine-fluid-slots` に `ExpectReachableInScrollViewport` で届く
- MANY（16・20 台）`清浄機${room.filters}台・高さ${windowHeight}でも…`: 最後の `clean-room-air-filter-<n-1>` へ `ExpectReachableInScrollViewport` で届き、`ExpectInventoryBodyReachable`
- BOUNDARY（0・2・8 台）`清浄機${room.filters}台・高さ720の境目でも本文とフッタが使える`: `ExpectInventoryBodyReachable`
- 部屋の無い大型機械: 文脈の区画が無く、`machine-selected-recipe`・`machine-input-slots`・`machine-fluid-slots` に届き（Web と同じ 3 対象）、`ExpectFooterReadable`

- [ ] **Step 3: テストを通す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomLayoutTest"`
Expected: 全件 PASS。差が出たら原因（器の `min-height`・`flex-shrink`）を直し、直せない差は Task 14 で LX に記帳

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Context moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory
git commit -m "test(uitk): 清浄室の部屋欄が長いときの配置を写す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 12: レシピ選択モード（C4-c 依存。C4-c マージ後に実物と照合）

C4-c がまだ `origin/feature/uitk` に無ければ、Task 13〜15 の選択に関わらない部分を先に済ませ、`done.md`「未確認」と指揮へ「C4-c 待ち」を書いて待つ（ポーリングせず、指揮からの知らせで再開する）。C4-c のマージ後に `git merge origin/feature/uitk` してこの Task に入る。

**Files:**
- Create: `S/Client.Ui/Screens/BlockInventory/Machine/RecipeSelection/MachineRecipeSelectionList.cs`・`MachineRecipeSelectionRowView.cs`・`MachineRecipeRowContentBuilder.cs`・`MachineRecipeSelection.uss`
- Modify: `S/Client.Ui/Screens/BlockInventory/Machine/MachineSectionView.cs`・`MachineSectionPresenter.cs`・`MachineSectionFactory.cs`、`GameUiTheme.tss`（`@import` 1 行）
- Modify: `S/Client.Tests/Ui/Screens/BlockInventory/Machine/MachineSectionFixtures.cs`（`MachineSectionHarness.Mount` が組む `MachineSectionView` の ctor に記録係の `RecordingRecipeSelectCommand` を足す。D-33 で View の引数が増えるため）
- Test: `S/Client.Tests/Ui/Screens/BlockInventory/Machine/RecipeSelection/MachineRecipeSelectionRowTest.cs`・`MachineRecipeLayoutTest.cs`・`MachineRecipeSelectionFixtures.cs`、`MachineSectionTest.cs`（4 件と出力空き不足の選択モードの面を足す）・`MachineDesignTest.cs`（2 件）・`Context/CleanRoomLayoutTest.cs`（1 件）

**Interfaces:**
- Consumes: Task 5（`MachineSelectionModeState.RememberRequest`・`IMachineRecipeSelectCommand`・`MachineRecipeSelectCommand.CommandName`）、C4-c（`RecipeRow(testid, arrowTestid)`・`SetSlots`・`SetDuration`・`SetIdle`・`LevelInheritanceBadge(testid)`・`Show`・`Hide`・`RecipeListScrollView(listTestid)`・`SetLeadingContent`・`ClearLeadingContent`・`SetEntries`）、C4-a（`FluidAmountSlot`・`SlotAmountBadge.Show`）、`LevelInheritanceBadgeText.TryBuild`
- Produces: なし（画面の中だけ）

- [ ] **Step 1: C4-c の実物と照合する**

Run:
```bash
grep -n "public RecipeRow(\|public void Set\|public void ClearAction\|public void SetIdle" moorestech_client/Assets/Scripts/Client.Ui/Components/Recipe/RecipeRow/RecipeRow.cs
grep -n "public LevelInheritanceBadge(\|public void Show\|public void Hide" -r moorestech_client/Assets/Scripts/Client.Ui/Components/Recipe
grep -n "public RecipeListScrollView(\|public void Set\|public void Clear" -r moorestech_client/Assets/Scripts/Client.Ui/Components/Controls
grep -rn "recipe-list-gap\|recipe-row-padding-block\|recipe-slot-size-max" moorestech_client/Assets/Scripts/Client.Ui/Components/Recipe moorestech_client/Assets/Scripts/Client.Ui/Components/Controls moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme | head
```
Expected: Global Constraints の C4-c の API と一致。違えば、下のコードの呼び方だけを実物に合わせ、差を `done.md`「未確認」に書く（挙動の差なら「要裁定」）

- [ ] **Step 2: 失敗するテストを書く**

`MachineRecipeSelectionRowTest.cs`（5 件を Web の名前で）:
```csharp
/// <web-test file="src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.test.ts">レシピ名を出力アイテム名で出し、行クリックで選択通知する</web-test>
[Test]
public void レシピ名は出力アイテム名で行押下で選択を通知する()
{
    var row = MachineRecipeSelectionFixtures.Row(MachineTestDetails.RecipeA, outputItem: 1, otherOutputs: 0);
    var view = Mount(new MachineRecipeSelectionRowView(row, MachineRecipeSelectionFixtures.Recipe(MachineTestDetails.RecipeA), Content));
    var pressed = new List<Guid>();
    view.OnPressed.Subscribe(pressed.Add);
    simulate.FrameUpdate();
    Assert.AreEqual(ItemNameOf(1), Find<Label>(view, $"machine-recipe-{MachineTestDetails.RecipeA}-name").text);
    simulate.Click(view.worldBound.center);
    CollectionAssert.AreEqual(new[] { MachineTestDetails.RecipeA }, pressed);
}
```
残り 4 件: 代表が液体 → 名前が液体名／入出力の液体 → `…-input-fluid-0`・`…-output-fluid-0` の `FluidAmountSlot` に量の帯〈`-amount`〉が出る／継承に上限 → `…-level-inheritance` がある／継承なし → 無い

`MachineSectionTest.cs` に 3 件（未選択 → `machine-recipe-selection` があり `machine-inventory-body` が無い／選択済み → ヘッダ＋本体、ヘッダ押下で選択リスト、行押下〈別のレシピ〉で View が `RecordingRecipeSelectCommand` を 1 回呼び、Model の選択が変わるまでリストのまま、変わると本体へ／同じレシピの行押下でも本体へ〈送らない判断はコマンド側で、`MachineRecipeSelectCommandTest` が見る〉）と、往復の 1 件（Web に無い名前の UITK 固有の件。清浄室の室内機械でヘッダ → 選択 → 同じ行で戻った後の並びが Web `MachineSection.tsx:61-67` と同じ `clean-room-section` → `machine-selected-recipe` → `machine-inventory-body` の順で、`clean-room-section` の文脈の器と `machine-selected-recipe` の間、`machine-selected-recipe` と `machine-inventory-body` の間がそれぞれ `--mantine-spacing-sm`）と、「出力空き不足の機械は…両モードのフッタで…」の選択モードの面（選択リストのときも `machine-wait-reason` が出る）

`MachineRecipeLayoutTest.cs`（machineRecipe 10 件。alpha の `BlockInventoryTestShell.Create` に本レーンの登録一式と偽の Model を差す。主張は Web の spec の主張を UITK の要素で: 大型パネル〈`block-inventory--large`〉でヘッダ＋入力の枠の数＝レシピの素材数＋ゴースト・タブ無し／ヘッダ押下 ⇄ 行押下の往復／未選択で選択モード／レシピ無しブロック〈チェスト〉は小型／レシピ 30 件で `machine-recipe-selection-scroll` の視口高がレシピ 3 件のときと同じで、そこだけがスクロールする／フッタの `worldBound.yMin` が両モードで ±0.5px／行の高さで本文の視口を割ると 3.8〜4.5／液体の枠の大きさが同じ行の `ItemSlot` と同じで量の帯がある／テクスチャ欠けで液体名のラベルが枠の中〈`worldBound` の内側〉／液体のみ出力のレシピを選ぶとヘッダが `machine-selected-recipe-fluid`）。`CleanRoomLayoutTest.cs` に 1 件（清浄機 20 台・選択モード: `ExpectFooterReadable` → 選択リストの先頭と末尾の行にそれぞれ `ExpectReachableInScrollViewport` → `ExpectSharedBodyViewport(shell, "machine-recipe-selection")`。Web `cleanRoomManyFilters.spec.ts:72-80`）。`MachineDesignTest.cs` に 2 件（「MachineSectionはレシピ選択リストとヘッダを往復し、電力率を共通フッタに置く」＝両モードで `machine-power-rate` が同じフッタの子／「レシピ選択行は共有RecipeRowを流用し、静止矢印で列挙する」＝行の中に `RecipeRow` があり矢印が静止〈C4-c の静止のクラス〉）

- [ ] **Step 3: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → Expected: 型が無いコンパイルエラー

- [ ] **Step 4: 実装する**

`MachineRecipeRowContentBuilder.cs`（`MachineRecipeData` から素材・結果の枠を作る。素材 i のアイテムは `ItemSlot($"{rowId}-input-item-{i}")`、液体は `FluidAmountSlot($"{rowId}-input-fluid-{i}")` に `SetFluid(content.Fluid(id), SlotAmountBadge.Show(amount))`、結果は `-output-item-j`・`-output-fluid-j`。アイテムの枠は `ItemSlotContent(content.Item(id), count, NoMarks, null, null, null)`。枠の押下は購読しない〈Web の行は行全体が押下の的〉）

`MachineRecipeSelectionRowView.cs`:
```csharp
using System;
using Client.Localization;
using Client.Ui.Components.Recipe;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Logic;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine.RecipeSelection
{
    /// <summary>
    /// レシピ選択の 1 行（Web MachineRecipeSelectionRow）。名前・継承バッジ・静止矢印の RecipeRow。選択中は内側 1px の枠
    /// One recipe-selection row (Web MachineRecipeSelectionRow): name, inheritance badge, a RecipeRow with an idle arrow; selected rows get a 1px inner frame
    /// </summary>
    public sealed class MachineRecipeSelectionRowView : VisualElement
    {
        private readonly Subject<Guid> _pressed = new();

        public IObservable<Guid> OnPressed => _pressed;

        public MachineRecipeSelectionRowView(MachineRecipeSelectionRow row, MachineRecipeData recipe, GameContentPresentation content)
        {
            var rowId = $"machine-recipe-{row.Recipe.RecipeGuid}";
            name = rowId;
            AddToClassList("machine-recipe-selection-row");
            EnableInClassList("machine-recipe-selection-row--selected", row.Selected);

            // 名前 → 継承バッジ → 静止矢印の行（アクション欄なし）
            // Name → inheritance badge → idle-arrow row (no action column)
            var title = new LocalizedLabel { name = $"{rowId}-name" };
            title.AddToClassList("machine-recipe-selection-row__name");
            title.SetText(MachineRecipeNames.Row(row, content));
            Add(title);
            var badge = new LevelInheritanceBadge($"{rowId}-level-inheritance");
            if (LevelInheritanceBadgeText.TryBuild(recipe.LevelInheritance, out var badgeText)) badge.Show(badgeText);
            else badge.Hide();
            Add(badge);
            var recipeRow = new RecipeRow($"{rowId}-row", $"{rowId}-arrow");
            recipeRow.SetSlots(MachineRecipeRowContentBuilder.Materials(rowId, recipe, content), MachineRecipeRowContentBuilder.Results(rowId, recipe, content));
            recipeRow.SetDuration(new UiText(LocalizationKeys.Ui.BlockInventory.RecipeDuration, new[] { new LocalizationTextParam("seconds", MachineRecipeRowContentBuilder.FormatSeconds(recipe.Time)) }));
            recipeRow.SetIdle();
            Add(recipeRow);

            // 選択中の外周は拾わない絶対配置の枠で描く（UITK に outline が無い。寸法に効かない）
            // The selected outline is an absolute, non-picking frame (UITK has no outline; it adds no size)
            var outline = new VisualElement { pickingMode = PickingMode.Ignore };
            outline.AddToClassList("machine-recipe-selection-row__outline");
            Add(outline);
            RegisterCallback<ClickEvent>(_ => _pressed.OnNext(row.Recipe.RecipeGuid));
        }
    }
}
```
（行名は Task 9 の `MachineRecipeNames.Row(row, content)`＝代表の名前〈`Representative`〉に、`OtherOutputCount` が 1 以上なら `recipeNameWithOthers {name,count}` を付ける〈Web `useRecipeRowName`〉。ヘッダは `Representative` だけを使う〈Web `useRecipeDisplayName`〉）

`MachineRecipeSelectionList.cs`（`RecipeListScrollView("machine-recipe-selection")` を持つ。`Render(MachineSectionViewModel vm, VisualElement leading)` で `SetLeadingContent(leading)`〈機械の文脈の View。選択モードではインベントリの器から外してここへ付け替える〉、各行 `new MachineRecipeSelectionRowView(row, recipe, content)`〈`vm.TryGetRecipe(row.Recipe.RecipeGuid, out recipe)` が偽の行は `Debug.LogError` で落とす。行は解放済みの一覧から組むので起きない前提〉を `SetEntries`。行の `OnPressed` を `Subject<Guid>` へ集め、作り直しの頭で前の行の購読を捨てる）

`MachineSectionView` の変更: `_selectionList` を `_root` の先頭に足し、`Render` で `showsSelection` なら `_selectionList` を見せて本体の器を隠し、文脈の View を選択リストの先頭へ（`ClearLeadingContent` → `SetLeadingContent(_context)`）。インベントリへ戻すときは `_selectionList.ClearLeadingContent()` の後に `_inventoryContent.ClearItems()` → `AddItem(_context)`・`AddItem(_header)`・`AddItem(_body)` で積み直し、続けて `SetItemShown` を当て直す（`UiGapContainer` に先頭へ入れる口は無く、`AddItem` は末尾へ足す。`Insert(0, …)` は間隔の対象外で、付け替えの時点で `_context` は `_items` から外れている）。`IObservable<Guid> OnRecipeRowPressed` を足す。選択リストは大型面では器の高さいっぱい（`flex-grow: 1; min-height: 0`）

`MachineSectionView` の行押下: ctor に `IMachineRecipeSelectCommand selectCommand` を足し〈Task 9 の `IBlockSlotCommands`・`UiCommandResultToaster` と同じく View が持つ。A-8〉、選択リストの行の押下で `_toaster.Report(MachineRecipeSelectCommand.CommandName, _selectCommand.Select(guid))` を呼んでから `OnRecipeRowPressed` へ GUID を流す〈拒否は良性なのでトーストは出ず Warning ログだけ〉。`MachineSectionPresenter` の変更: `OnRecipeRowPressed` を購読して `_mode.RememberRequest(guid)` → `Apply()` だけを呼ぶ〈コマンドもトースターも持たない〉（同じなら送らない判断は `MachineRecipeSelectCommand`〈State〉、要求を覚えるのは `MachineSelectionModeState.RememberRequest`。D-24。設計書 §4.2「Presenter は判断を持たない」）。`MachineSectionFactory` は `_selectCommand` を View の ctor へ渡す

`MachineRecipeSelection.uss`:
```css
/* web: details/machine/recipeSelection/machineRecipeSelectionList.module.css の .row・.name・[data-selected] の outline・.list のつまみ上書き（ユーザー裁定 2026-09-05「控えめ」）と listGap を写す / ports the row, name, selected outline, the list knob overrides and listGap */
.machine-recipe-selection-list {
    --recipe-list-gap: var(--machine-recipe-row-gap);
    --recipe-row-padding-block: 5.6px;
    --recipe-slot-size-max: 32px;
    flex-grow: 1;
    min-height: 0;
}

.machine-recipe-selection-row {
    cursor: link;
    min-width: 0;
}

.machine-recipe-selection-row__name {
    color: var(--text-muted);
    font-size: var(--recipe-info-text-size);
    padding: 0 var(--recipe-row-padding-inline) 4px var(--recipe-row-padding-inline);
}

.machine-recipe-selection-row__outline {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
    display: none;
    border-width: 1px;
    border-color: var(--select-cyan);
}

.machine-recipe-selection-row--selected > .machine-recipe-selection-row__outline {
    display: flex;
}
```
（`line-height: 1` は UITK に無いので名前の行の高さを並べ比べで見る。差は LX）。`GameUiTheme.tss` の `Screens` 区画に `@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/Machine/RecipeSelection/MachineRecipeSelection.uss");`

- [ ] **Step 5: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.(Screens.BlockInventory|State.BlockInventory)"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Machine "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory
git commit -m "feat(uitk): 機械のレシピ選択モードを足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 13: 名前の対応・処分表

**Files:**
- Modify: `docs/uitk/disposition.md`（末尾に追記。捨てる行だけ）

- [ ] **Step 1: 対応を確かめる**

Run: `node tools/uitk/test-name-parity.mjs --bundle 4`
Expected: 本レーンのファイル（`blockDetails.spec.ts`・`machineGestures.spec.ts`・`machineRecipe.spec.ts`・`machineRequiredCount.spec.ts`・`cleanRoom/*.spec.ts`・`fluidSlot.spec.ts`・`MachineSection.test.ts`・`SelectedRecipeHeader.test.ts`・`ModuleSlotStackLimitText.test.ts`・`MachineRecipeSelectionRow.test.ts`・`CleanRoomSection.test.ts`・`CleanRoomYieldSection.test.ts`・`MachineInputStallSection.test.ts`・`detailActions.test.ts`）に未対応が無い。`blockDetails` の「歯車機械は消費トルクと基準付きRPMを赤なしで出し、網の需給行を持つ」「歯車発電機は発生トルクと現在RPMだけを出す」の 2 名は S4-special の持ち分で残る（`done.md` に書く）

- [ ] **Step 2: 処分表に書く**

`docs/uitk/disposition.md` の末尾へ、捨てる 4 件だけを既存の行の書式で（`<web-test>` で写した件の行は書かない。`compareBundles.mjs` が誤りにする）:
- `ModuleSlotStackLimitText.test.ts`「上限の配信が届くまでは何も描かない」— 捨てる: UITK は `IModuleSlotStackLimitLookup.CurrentStackLimit` を同期で読み、配信待ちの状態が無い（D-10）
- `e2e/mock-host/detailActions.test.ts` 3 件 — 捨てる: mock-host 自身の契約。UITK は `MachineRecipeSelectionProtocol` を直に送り、解放の検査は `MachineRecipeSelectCommandTest` が持つ（D-11）

Task 11 で録画に回した件があれば、タグで写した L1 と別に「録画」の証拠を `done.md` に書く（処分表には書かない）

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/disposition.md
git commit -m "docs(uitk): 機械の捨てるテストを処分表に足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 14: 並べ比べと見た目の例外

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 見た目の例外」。使ったときだけ LX-116〜LX-120）
- Modify: `.gitignore`（末尾に `/.uitk-capture/` の 1 行。実測で `git check-ignore -v .uitk-capture/web-1280x720/x.png` が何も返さず、撮った PNG が未追跡で残って PR に混ざるため。Step 1 の前に足す）
- 記録: `LOGS/harness/uitk/S4-machine/done.md`「差の表」

- [ ] **Step 1: 1280×720 に無い参照を自分で撮る（E-22）**

master の使い捨て worktree（環境ローカル規約に従い `moores-wt new <使い捨て> --no-editor` で作る）で Web の mock-host を起こし、`W/e2e/capture-machine-qa.ts` の撮り方で 1280×720 の `4-block-machineShortInput`・`4-block-machineFilledOutput`・清浄室 12（`cleanRoomMachine`・`cleanRoomMachineManyFilters`・`cleanRoomMachineNoFilters`・`cleanRoomMachine16Filters`・`cleanRoomMachine20Filters`・`cleanRoomMachine20FiltersSelecting`・`cleanRoomAirFilter2Filters`・`8Filters`・`14Filters`・`20Filters`・`30Filters`・`30FiltersUnconnected`）を撮る。撮影の使い捨てスクリプトは使い捨て worktree の中に置きコミットしない。撮った 14 枚は使い捨て worktree ではなく本レーンの worktree の gitignore 下 `.uitk-capture/web-1280x720/` へ書き出し、`done.md` にそのパスと撮影元のコミットを書いてから、使い捨て worktree を `moores-wt rm` で畳む（`moores-wt rm` は gitignore 下のファイルを確かめずに worktree ごと消すので、置き場を先に移す。撮影表への追加は integrator へ依頼済み）

- [ ] **Step 2: UITK を撮って並べる**

参照 `4-block-machine`・`-machine-recipe-selection`・`-gearMachine`・`-generator`・`-gearGenerator`・`-miner`・`-gearMiner`・`-pump`・`-pumpNoVein`・`-gearPump`・`-tank`・`-generic`（1280×720 は `web-a9d5123537/`、2560×1080 は `web-0e81bbe3c3-2560x1080/`）と Step 1 の 14 枚（本レーンの worktree の `.uitk-capture/web-1280x720/`）と同じ場面を UITK で撮る。Step 1 の 14 場面は `web-0e81bbe3c3-2560x1080/` に既存の参照（`4-block-machineShortInput.png`・`4-block-machineFilledOutput.png`・`4-block-cleanRoom*.png` 12 枚）があるので、2560×1080 でも撮って比べる。場面の組み方と撮り方は ref-uitk-design スキルの「比べ方」節（ランタイムの PanelSettings で撮る。A-111）に従い、同じスキルの比較ツールで並べ比べの HTML を作る（Task 17 の報告ページに載せる）

- [ ] **Step 3: 差の表を書く**

| 参照 | 見る点 | 予想 |
|---|---|---|
| 4-block-machine | 加工行の矢印が中央・入力の所持/必要の位置と大きさ・モジュール帯・液体の行・分間生産数・フッタ | 同じ（3 列の写しの差は LX 候補） |
| 4-block-machine-recipe-selection | 行の密度（padding 5.6px・枠 32px）・名前の行・選択の枠・行の間 8px・文脈の前置 | 名前の行の `line-height: 1` の差（LX 候補） |
| 4-block-gearMachine・-gearMiner・-gearPump・-gearGenerator | 本レーンの区画（充足率が赤でない・電力行が無い） | 歯車の行は S4-special（他レーン持ち） |
| 4-block-generator・-miner・-pump・-pumpNoVein | 区画の並びと文 | 同じ |
| 4-block-tank・-generic | 液体の行（alpha の比較で「他レーン持ち」だった差がここで閉じる） | 同じ |
| machineShortInput・machineFilledOutput（1280×720 自撮り・2560×1080 既存の 2 解像度） | 不足の赤・出力の実物で必要数なし | 同じ |
| 清浄室 12（1280×720 自撮り・2560×1080 既存の 2 解像度） | 部屋欄の長さとスクロール・フッタの位置 | 同じ（Task 11 の主張と一致） |

差は 1 件ずつ「同じ／直した／LX-n／他レーン持ち」で閉じる。LX は `docs/uitk/TODO.md`「## 見た目の例外」に LX-116 から順に使い、USS に `/* look-exception: LX-n */` を付ける

- [ ] **Step 4: コミット**

```bash
git add .gitignore docs/uitk/TODO.md moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory
git commit -m "docs(uitk): 機械と汎用族の並べ比べの差を閉じる（S4-machine）

<実行ホストの指示する帰属行>"
```
（差が無く何も変えていなければコミットしない）

---

### Task 15: 録画

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-machine-ui.cs`

- [ ] **Step 1: シナリオを書く**

前例 `scenarios/uitk/uitk-grab-overlay.cs` の形（冒頭で `UiSystemEditorSwitch.Set(UiSystem.GameUi)`・`ClientDIContainer` から型を引く）に従い、次を順に主張する:
1. `SetupFlatGround` → 電気の機械（レシピを 2 つ以上持つもの。マスタから選ぶ）と発電機・電柱を `PlaceBlockDirect` → `WaitBlockGameObject`
2. 機械を `PressInteract` → `block-inventory` が大型（`block-inventory--large`）・`machine-recipe-selection` が出る（未選択）
3. `machine-recipe-<guid>` を押す → `machine-selected-recipe` と `machine-inventory-body` が出る（サーバーの選択が届くのを `WaitUntil` で待つ）
4. 素材を main に `GiveItem` し、**main 側の素材の枠**を Shift＋左押下（ブロックの枠の Shift＋左は「中身あり → main へ」なので空の入力枠では何もしない。`BlockSlotPlanner.cs:21-24`・Web `machineGestures.spec.ts:43-49`）→ R-13 の `MachineSlotRestrictionContributor` が入力の番地へ配分する → `machine-input-slot-0` の個数が増えたことを `WaitUntil` で確かめてから次へ（main の枠は S4-inventory の持ち物画面。未マージなら `done.md`「未確認」に書いて指揮へ返し、マージを待つ）
5. 開いたまま待つ → `machine-progress-arrow` の進捗が動き、`machine-state-label` が稼働中、`machine-output-slots` に生産物が増える（`Screenshot` を 3 枚: 投入直後・加工中・出力後）
6. `machine-selected-recipe` を押す → 選択リストへ戻る → 同じ行を押す → 本体へ戻る
7. `block-inventory-close` → 閉じる
8. 途中で `AssertNoErrorLogs` が 0 件

押下は DSL の UITK 要素の押下（前例の書き方）。InputSystem の注入だけを使い OS の入力は使わない

- [ ] **Step 2: 回す**

Run: unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh` で `uitk/uitk-machine-ui`・`tier9/tier9-1b-mixed-level-sorting`・`tier9/tier9-3-yield-ui`・`tier9/tier9-6-fusion-elevator`・`tier9/task31-r3/t31r3b-cable-output-space-acceptance`・`misc/cleanroom-room-usability` を、全体スイッチ UITK で（既存の 5 本は冒頭で UITK へ切り替えて回す。シナリオ本体を変えずに回せる口が DSL に無ければ、切り替えの 1 行を足した UITK 用の写しを `scenarios/uitk/` に置く）
Expected: 6 本の `result.json` が PASS、mp4 がある。Task 11 で録画に回した高さ 600・1080 の件も `uitk-gameview-resolution-switch.cs` の前例で撮る。`done.md`「証拠」に result.json のパス・mp4・`UiInputExclusivity.ActiveUiSystem` の値（GameUi）を書く。生の録画は worktree の gitignore 下、`LOGS` へはスクショ 3 枚までと報告だけ

- [ ] **Step 3: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk
git commit -m "test(uitk): 機械 UI を開いたまま更新される録画シナリオを足す（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 16: 全ブランチのレビュー

- [ ] **Step 1: moores-code-review を全ブランチで回す**

moores-code-review スキルを、base `origin/feature/uitk` に対するこのブランチの全差分で起動する。スキルの手順どおり、機械的な修正は自動で当て（**自動適用の後は Task 15 の録画を反映後のバイナリで撮り直す**）、設計判断は D-n として判断記録へ足すか、目的・裁定から外れるものだけ `done.md`「要裁定」へ書く（人へ質問しない）

- [ ] **Step 2: 当てた修正の後に compile とテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.(Screens.BlockInventory|State.BlockInventory)|UiThemeImportBundleTest|UiStyleConventionTest|UiLayerDependencyDirectionTest|LookExceptionLedgerTest"`
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 3: コミット**

```bash
git add -A moorestech_client docs .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk
git commit -m "fix(uitk): 全ブランチのレビューを反映（S4-machine）

<実行ホストの指示する帰属行>"
```

---

### Task 17: PR・取り込み・マージ・報告

- [ ] **Step 1: PR を作る**

pr-create スキルで base `feature/uitk` の PR を作る。本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/`（Task 14 の並べ比べの HTML〈1280×720 の自前撮影は `.uitk-capture/web-1280x720/` を入力にする〉と Task 15・16 の録画を載せる。素材と公開手順は `~/hermes-agent/data/services/pr-review/publish/README.md`。公開に失敗したら報告 URL 無しで完了にしない）。本文の末尾は実行ホストの指示する帰属行

- [ ] **Step 2: `origin/feature/uitk` を取り込む**

```bash
git fetch origin
git merge origin/feature/uitk
```
共有区画（`MainGameUiRegistration` の画面登録区画と共有サービスの並び・`GameUiTheme.tss` の `Screens` 区画・`Client.Ui.State.asmdef` の参照〈同じ参照は 1 行に畳む〉・`UiCommandRejectReason.cs`・`PauseMenuCommandsTest.cs` の良性の配列・`localization.csv`・`.gitignore`・`docs/uitk/disposition.md`・`docs/uitk/TODO.md`）の衝突は両側の行を残して解く。生成物の `_CompileRequester.cs` と `localizationKeys.ts` は手で行を混ぜず、両側の csv を採ったあと `pnpm --dir moorestech_web/webui run gen:i18n` と Editor の再コンパイル（`uloop compile`）で作り直し、その結果をコミットする（E-1・42c8012e3c）。取り込み後に `uloop compile` と Task 16 Step 2 のテストを回し、緑を確かめて push

- [ ] **Step 3: G1 を待ってマージする**

`gh pr checks <番号>` が緑（G1）になったら `gh pr merge <番号> --merge`（squash しない）。`Unity Test - client-remainder` の `UnityCursorTooltipScreenTest` が CI だけで落ちるのは既知で、マージの条件は G1 の緑（他の赤は直して push し直す）

- [ ] **Step 4: 報告と後始末**

1. `bd close <S4-machine の issue>`
2. `../moorestech_logs/harness/uitk/S4-machine/done.md` の 1 行目を「状態: 完了」にし、PR 番号・マージコミット・証拠・差の表・integrator への依頼・未確認を埋める
3. `cmux wait-for -S uitk-s4-machine`
4. `moores-wt rm s4-machine`（未 push があれば拒否されるので push を確かめてから）

---


## Self-Review

- Requirements R-1〜R-20 はそれぞれ Task に当たる: R-1 → Task 2、R-2・R-3 → Task 3、R-4 → Task 4（State）・Task 7（View）、R-5・R-6・R-7・R-8・R-9 → Task 9（State 側の Model は Task 5）、R-10 → Task 8、R-11 → Task 6、R-12・R-13 → Task 5（大型の L1 は Task 10）、R-14 → Task 12、R-15 → Task 11・12、R-16 → Task 14、R-17 → Task 10、R-18 → Task 2〜13（処分表は Task 13）、R-19 → Task 15、R-20 → Task 16・17
- 写すテストの所在: 「写すテスト」表の各行の最右列が Task 番号。タグ付き NUnit 81 件（P 36・C 27・alpha 割り当て 18）は Task 2〜12 に、捨てる 4 件は Task 13 に出る。S4-special だけが写す blockDetails 2 件は Task 13 Step 1 の `done.md` に書く
- 型の一致: Produces の型名・メソッド名（`SectionContent<T>`・`MachineRecipeSelection`・`IMachineSectionModel.Current`・`MachineSelectionModeState.ShowsSelection`/`OpenSelection`/`RememberRequest`・`IMachineRecipeSelectCommand.Select`・`BlockContextSectionsView.Render`・`FluidSlotContentMapper.Map`/`MapWithGhost`・`DetailSectionPresenter<T>`・`BlockSlotGesture`）は Task 2〜12 のコードと同じ綴り
- 区画の口の形: どの区画も「工場が Presenter＋View の組を作り、Presenter が Attach で Model を購読して `View.Render` と `IsApplicable` へ流し、Detach で切る。View は購読しない」（設計書 §4.2）。View が外へ出すのはヘッダ・レシピ行の押下の `IObservable` だけで、それを購読するのも Presenter。ブロックの枠の押下は枠を持つ View が `IBlockSlotCommands` を直接呼ぶ（設計書 §4.2 A-8）。出す・出さない・送る・送らないの判断は State（`FluidRowVisibility`・`ContextSectionVisibility`・`MachineSelectionModeState`・`MachineRecipeSelectCommand`）にあり、Presenter は呼ぶだけ。レシピ行の押下は View がコマンドを呼んで結果をトースターへ渡し、Presenter は同じ押下で `RememberRequest` だけを呼ぶ（D-33）
- プレースホルダ: 「実物に合わせる」と書いた箇所は、起草時に実物が無い名前（C4-c の 4 部品、`UiIcon` の名前の公開名、`ItemSlot` の所持/必要の要素の引き方）だけ。挙動の空白は無い
- 200 行: 最大は `MachineSectionView`（約 110 行・Task 12 の追加後に約 150 行）。超えそうなら選択リストとインベントリの付け替えを `MachineSectionModeSwitcher` へ切り出す（`Machine/` は既に 10 本なので、そのときは `Machine/Shell/` を切って View と付け替えをそこへ移す）
- alpha の再改訂（44c416e9e4）の 3 点（`TryReadBlockSlots` の真の条件・`BlockInventoryPanelLayout` の 3 つの static・題の操作と列車の本文の空の部分）は、本計画のコードと文のどこも使っていない（grep で確かめた）。大型の判定は寄与の真偽と外殻のクラス `block-inventory--large` だけを見る

---

## 判断記録

| ID | 判断 | 理由 | 却下した案 |
|---|---|---|---|
| D-1 | 区画は「詳細 Model（State）→ 区画の Presenter → View」の 3 段。Model は `OpenBlockDetailTracker` 等を購読して `IReadOnlyReactiveProperty<SectionContent<…ViewModel>>` を配り、Presenter は Attach／Detach で購読を持ち、View は `Render` と、選択モードを変える押下（ヘッダ・レシピ行）の `IObservable` だけを持つ。ブロックの枠の押下は枠を持つ View（`MachineProcessRow`・`MachineModuleBand`）が `IBlockSlotCommands` を直接呼ぶ（出所: レビュー r1） | 設計書 §4.2「Model の購読は Presenter、View は Render だけ」と「View がコマンドを直接呼ぶ。Presenter を経由させても中継の配線が増えるだけ」[A-8]、alpha の `BlockItemGrid` と同じ形。alpha の改訂（`OpenBlockInventoryPanel` は `BlockGameObject` を持たない） | View が Model を購読する（§4.2 違反）／枠の押下を Presenter が中継する（A-8 の却下案。r0 の形） |
| D-2 | 値の有無は `SectionContent<T>`（Absent／Present）の union で配り、null を使わない | Nullチェックの指針（外部データ以外で null 前提を作らない）。Presenter の `IsApplicable` が `IsPresent` 1 つで決まる | `T` を null 許容で配る |
| D-3 | Web ホストにしかない導出（`BlockDetailDtoBuilder`・`MachineSlotBindingDtoBuilder`・`MinerDetailDtoBuilder`・`PumpDetailDtoBuilder`・`CleanRoom*DtoBuilder`・`MachineInputStall*`）は State に写して二重に持つ | UITK は `Client.WebUiHost` を参照しない（依頼の制約・alpha D-5 と同じ）。Web は UITK 切替後に消えるので共通化の先が無い | 共通の下位層へ移す（Web 側を変える。範囲外） |
| D-4 | 選択レシピは `MachineRecipeSelection`（Unselected／Selected(guid)）の union で持ち、サーバーの文字列から解くのは `MachineRecipeSelection.TryParse` の 1 か所（読めなければ偽で、呼び手が Error ログ。出所: レビュー r1） | L0-s4b D-27 の境界（空 GUID＝未選択）。比較の取り違え（空 GUID と選択中の一致）を型で防ぐ | `Guid` のまま持ち `Guid.Empty` で判定する |
| D-5 | asmdef `Client.Ui.State` に `Game.Block`・`Game.Fluid`・`Game.Gear` を足す（`Game.UnlockState` は alpha） | 本体の決定。`IMachineParam`・`ElectricPumpBlockParam`・`FluidUnits`・`GearStateDetail` を読む | Web ホストの DTO を経由する（D-3 で参照しない） |
| D-6 | 機械の状態の文字列が読めないときは Error ログを出して機械の区画を出さない。ポンプの未知の param も Error ログで区画を出さない（出所: レビュー r1） | 実測: Web ホストは機械の状態文字列を検査せず素通し（`BlockDetailDtoBuilder.cs:67`）、ポンプの未知の param は `InvalidOperationException`（`PumpDetailDtoBuilder.cs:52`）。UITK は両方ともサーバーと同じ repo の型の追加漏れ（コードの不整合）として開発者に届ける。例外にしないのは、Model の購読の中で投げると購読ごと止まり、他のブロックを開いても区画が出なくなるため。部屋の照会の型・漏れ種別・稼働可否（`CleanRoomViewConversion` の `default`）は例外のまま残す: `Server.Protocol` の閉じた union／enum の網羅で、新種はサーバーと同じ PR でしか増えず、L0 の `ContextViewConversionTest` が先に落ちる（出所: レビュー r2） | 既定の表示（待機中等）に落とす（壊れたまま出荷される）／Web ホストと同じく例外で止める（購読が死ぬ） |
| D-7 | 歯車駆動の判定は「`GearStateDetail` がある」 | Web ホスト `BlockDetailDtoBuilder` と同じ。param の型を列挙すると歯車機械の追加で漏れる | `IGearMachineParam` 等の型で判定する |
| D-8 | 液体の 2 区画は液体の列が 0 件なら出さない | Web `FluidRow`・`FluidInventoryContents` の呼び手が 0 件で null を返す | 空の行を出す |
| D-9 | 大型パネルの寄与 `RecipeMachineLargePanelRule` と Shift 配分の寄与 `MachineSlotRestrictionContributor` は機械の Model を読む（ブロックを自分で読まない） | 機械かどうか・行数の導出を 1 か所（Model）に集める | 寄与ごとにブロックから導出し直す（3 重になる） |
| D-10 | `ModuleSlotStackLimitText.test.ts`「上限の配信が届くまでは何も描かない」は捨てる（出所: レビュー r1: 理由を実測に合わせた） | `ClientModuleSlotStackLimitDatastore` は配信前でもレベル 0 の上限を返し（`CurrentLevel` 初期 0）、未受信の印を持たない。初期データが届く前に機械を開く経路は無いので、Web の「届くまで描かない」に当たる状態は UITK に無い | 未受信の状態を作って写す（無い状態のためのコード） |
| D-11 | `e2e/mock-host/detailActions.test.ts` 3 件は捨てる | mock-host 自身の契約。UITK は `MachineRecipeSelectionProtocol` を直に送り、解放の検査は `MachineRecipeSelectCommandTest` が持つ | mock-host 相当の偽を作って写す |
| D-12 | 文脈の区画（機械以外）は `BlockContextViewModel.HasAny`（3 欄のどれかが Absent でない）のときだけ出す。機械は自分の区画の本文の先頭に同じ `BlockContextSectionsView` を置く（出所: レビュー r1: 条件を `CleanRoomTarget != None` から変えた） | Web `SectionStackView.tsx:64` の `hasContextSections` と同形。ホストは室内機械の稼働可否が届くまで部屋欄を立てない（`CleanRoomDetailDtoBuilder.cs:43-51`）ので、対象かどうかだけで出すと空の区画が出る。基盤は機械を知らない（alpha D-3） | `CleanRoomTarget != None` で出す（r0。状態詳細が揃うまで空の区画）／alpha の `SectionStackBody` に機械の分岐を置く |
| D-13 | 「取得できない」行の `{target}` の入れ子の文言は描画時に解き、言語の変化は 1 秒ごとの照会の再描画で追う | `LocalizationTextParam` は文字列の値を取る。入れ子の再解決の仕組みを足すより、既にある再描画で足りる | 入れ子の `UiText` を受ける引数を部品に足す（共有部品の変更） |
| D-14 | `BlockInfoUnavailableLine` は本レーンの `Screens/BlockInventory/Context/` に置く | Web の使い手は文脈の 3 欄だけ。新しい共有部品は作らない | `Components/` に置く（integrator の持ち分） |
| D-15 | 配置の写しのうち舞台の高さ 600・1080 の件は録画で見る（alpha の `BlockInventoryTestShell` の舞台は 1280×720 固定） | L1 の舞台は 1280×720 固定。Game View の解像度の切り替えは録画の前例（`uitk-gameview-resolution-switch.cs`）がある | テスト用の舞台の大きさの口を alpha のシェルに足す（他レーンの部品の変更） |
| D-16 | レシピを選ぶコマンドは `UiCommandResult` を返す。送れない要求（開いていない・未知・未解放）は良性の拒否 `MachineRecipeBlockNotOpen`・`MachineRecipeInvalid`・`MachineRecipeLocked`（wire は Web の `block_not_open`・`invalid_recipe`・`recipe_locked`）を `UiCommandRejectReason` の末尾に足して返す。`Detail` は既存の前例どおり `""`（`ToWireCode()` が理由の wire を返す）で、GUID は送らない判断のところで Warning ログに出す。結果は行を押した View が `UiCommandResultToaster.Report("machine_recipe.select", …)` に渡す（D-33。良性なのでトーストは出ず Warning ログだけ＝Web と同じく画面に出ない）。`block_not_open` は開いた直後・閉じた直後の古い押下で起きうるので良性にし、S4-train も同じ扱いに揃える（本体から S4-train へ連絡）。送信後のサーバーの拒否は待たずに `VanillaMachineRecipeSelectionSender` が LogWarning（出所: レビュー r2（ADR に合わせる）、レビュー r3 で `Detail` と渡し手を直した） | 設計書 §1.1・§4.1（State の Commands は `UiCommandResult` を返す）・316（拒否は理由 enum とログ）・A-106（受けない状態は良性の拒否。前例 `EventLanguageGate.SelectLanguage` の `LanguageAlreadySelected`）。列挙と網羅 switch の末尾への追記は E-1（fce54ea0b5）が許し、全列挙を見るテストを同じ PR で緑にする | `void`＋`Debug.LogWarning`（r1。ADR の `UiCommandResult` と食い違う）／`Detail` に GUID（r2。ログから理由が消える）／非良性の拒否（Web に無いトーストが出る） |
| D-17 | 選択モードの状態（開いた・要求した GUID）は Presenter が持つ `MachineSelectionModeState` に置く | Web の `useState` と同じく区画が開いている間だけの状態。Model に置くと別のブロックを開いたときに残る | Model に置く |
| D-18 | 加工行の grid（`1fr auto 1fr`）は左右の器を `flex-grow: 1; flex-basis: 0` にして写す | UITK に grid が無い。等幅の伸縮列で矢印が中央に固定される | 左右に固定幅を置く（枠の数で崩れる） |
| D-19 | 選択行の `outline: 1px solid var(--select-cyan)` は、拾わない絶対配置の枠（border 1px）で写す | UITK に outline が無い。border を行に直接付けると寸法が 2px 変わる | 行に border を付ける |
| D-20 | パイプの液体は、追跡ブロックが複製に節を持つ間だけ `FluidNetworkClientCache` の複製を読み直し、液体 id か量が変わった回だけ配り直す。読み直しの契機は D-36 で 4 フレームの刻みから複製の変化の通知へ改めた（出所: レビュー r1） | 実測: 複製には変化の通知が無い（公開は `Replica`・`ReplicaVersion`・`ApplyFullState`・`ApplyTickDiff`・`CurrentHash`・`TryGetNodeIndex`）。Web ホストは `BlockInventoryTopic` の `IntervalFrame(4)`＋`BlockInventoryRepublishPolicy.HasPipeContentsChanged` で同じ間隔で出し直す。r1 の時点では刻みで読んだが、設計書 [A-57] が「源に変化通知の無い連続値は、変化を起こす操作の直後に通知を足して購読する」に改まったので D-36 で通知に寄せた | 状態詳細の更新だけを待つ（r0。パイプを開いたまま量が止まる退化） |
| D-21 | 機械の枠の testid（`machine-input-slot-<i>` 等）と採掘中の品の枠の testid は Web に無いものを付ける | UITK の `ItemSlot` は frame の testid が要る。録画とテストの照準に使う | testid 無しで子の順で引く（並びの変更で壊れる） |
| D-22 | 自分の View は `VisualElement` を継承し、中に `UiStack`／`UiGroup` を 1 つ持つ。揃え・折り返しは `UiLayoutClass`（`GroupJustifyCenter`・`GroupNoWrap`・`StackAlignCenter`）で付け、子の追加の余白（モジュール帯の `mt=xs`）は器に包んで器の `padding-top` で付ける（出所: レビュー r1） | `UiStack`・`UiGroup` は sealed。`UiGroup` の子は内側の行に入るので外側の USS は効かない。`UiStack` は非先頭の子の `marginTop` を gap で上書きする | 基盤の sealed を外す（Foundation の変更）／画面 USS で外側に `justify-content`・`margin-top` を書く（効かない） |
| D-23 | 採掘機・ポンプ・発電機の Model は族ごとに分け、それぞれ `Current` 1 本を配る（`Family/Models/`）（出所: レビュー r1） | 設計書 §4.1 の Model の型と `FakeModel<T>` の remarks「複数の値を並べない」。区画の Presenter（`DetailSectionPresenter<T>`）も RP 1 本を受ける | 1 つの Model に RP を 3 本並べる（r0） |
| D-24 | レシピ行を押したときの「同じなら送らず受理、違えば送る」は `MachineRecipeSelectCommand`（State）が持ち、要求 GUID を覚えるのは `MachineSelectionModeState.RememberRequest`。Presenter は呼ぶだけ（出所: レビュー r1、レビュー r3 で置き場を変えた） | 設計書 §4.2「Presenter は判断を持たない」。選択モードのもう一方の判断 `ShowsSelection` も State にある。送るかの判断をコマンドに置くと、コマンドを呼ぶ View が判断を持たずに済む（D-33） | Presenter の購読の中で分岐する（r0）／`MachineSelectionModeState.Choose` がコマンドを呼び結果を返す（r2。結果を Presenter がトースターへ渡すことになる） |
| D-25 | `IBlockFluidSlotsModel` と液体の複製の読み手 `IFluidReplicaReader` は `MainGameUiRegistration` の共有サービスの並びに登録する（出所: レビュー r1） | S4-train も ctor で受ける。E-1「複数レーンが受ける共有サービスは作るレーンが本体の並びに登録する」・alpha D-22 | 本レーンの登録ファイルに置く（r0。S4-train が本レーンの画面登録に依存する） |
| D-26 | 大型パネルの寄与と Shift 配分の寄与は、機械の Model の値が開いたパネルと同じブロック（`BlockInstanceId` 一致。`MachineSectionViewModel.IsFor(panel)`）のときだけ効く（出所: レビュー r1） | `BlockGuid` だけでは同種の機械を続けて開いたとき前の値と区別できない。`BlockInstanceId` は alpha が区画の Model の鍵として公開している | `BlockGuid` だけで比べる（r0）／`Position` で比べる（同じ座標の差し替えを区別できない） |
| D-27 | レシピを選ぶコマンドは開いた機械の位置を `IBlockInventoryPanelSource`（`OpenBlockInventoryPanel.Position`）から読む（出所: レビュー r1） | 機械の Model と同じ源で、テストは alpha の `FakePanelSource` で組める。`SubInventoryState` を読むと L1 で組めるかが未確認になり、新しい interface が要る | `SubInventoryState.CurrentSubInventorySource` を読む（r0） |
| D-28 | 選択中レシピのヘッダの名前は代表の名前だけ。「他 N 件」は選択リストの行だけに付ける（`MachineRecipeNames.Representative`／`Row`）（出所: レビュー r1） | Web `SelectedRecipeHeader.tsx:17` は `useRecipeDisplayName`（代表の名前）、`recipeNameWithOthers` は `useRecipeRowName` だけ | ヘッダにも「他 N 件」を付ける（r0。Web と違う） |
| D-29 | `blockDetails`(8) は本レーンが 6 名（うち 2 名は S4-special と同じタグで分け合う）、S4-special だけが 2 名を写す（出所: レビュー r1・実行計画 §1.4 876b3229e5） | 歯車の行・フィルタの区画は S4-special の部品。gearPump の `pump-section` は本レーンの区画 | 本レーンが歯車の面まで写す（他レーンの部品を偽で組むことになる） |
| D-30 | 2 つの Model を見る Presenter（FluidRow・Context・Machine）は `CombineLatest` を使わず別々に購読し、どちらの通知でも同じ判断をやり直す（出所: レビュー r1） | `CombineLatest` の結果セレクタは `Func` で、AGENTS.md は `Func<>` の使用を禁止（シグネチャに限らない）。前例 `GrabOverlayModel` が同じ理由で使わない | `CombineLatest` の 2 引数版（r0） |
| D-32 | 文脈の区画を出すかは State の `ContextSectionVisibility.Shows(context, isMachine)` が決め、Presenter は呼ぶだけ（出所: レビュー r2） | 液体の行の `FluidRowVisibility.Shows` と同じ置き場。設計書 §4.2「Presenter は判断を持たない」・A-150 | Presenter が `!machine.IsPresent && HasAny` を合成する（r1） |
| D-33 | レシピ行の押下は View がコマンド `IMachineRecipeSelectCommand.Select` を直接呼び、結果を `UiCommandResultToaster` へ渡す。Presenter は押下の `IObservable` で選択モードの要求を覚えるだけで、コマンドもトースターも持たない（出所: レビュー r3） | 設計書 §4.1「View: 操作はコマンドへ。結果は UiCommandResultToaster へ」・A-8「View がコマンドを直接呼ぶ」。枠の押下（D-1）と同じ形 | Presenter がコマンドを呼び結果をトースターへ渡す（r2。A-8 の例外を増やす） |
| D-34 | 登録ファイルは `Client.Starter/Registration/GameUi/Screens/BlockInventoryMachineGameUiRegistration.cs`（名前空間 `Client.Starter.Registration.GameUi.Screens`）。ディレクトリが無ければ本レーンが作る（出所: レビュー r4） | E-1（ad92a44777）「W4 以降の画面レーンの登録は `GameUi/Screens/`。最初に要したレーンが作る」。`GameUi/` 直下は alpha で 10 本になる。S4-inventory も同じ置き場 | `GameUi/` 直下に置き integrator の再編を待つ（r3。再編は予定されず置き場が決まらない） |
| D-35 | 設計検査 r4 の弱のうち 2 件は直さない: (a)「開いた機械か」をコマンドがパネル源と機械の Model の 2 源から読む — パネル源は送れるかの検査、機械の Model は選択中のレシピを読むためで役割が違い、述語を 1 本にする口は alpha の `IBlockInventoryPanelSource` 側の話で本レーンの範囲外。(b) `Detail` の空文字で `ToWireCode()` の出所が切り替わる — 既存の `UiCommandRejected` の形で、本レーンは使う側（出所: レビュー r4） | 前例どおりの使い方で、故障の入力を組めない | 本レーンで alpha の口や `UiCommandRejected` を変える（他レーンの持ち物） |
| D-31 | 設定が合わない FluidRow・FluidDetails の工場は、`IsApplicable` が常に偽の `NotApplicableSection`（`Fluid/` に 1 本）を返す（出所: レビュー r1） | alpha の `ISectionStackSectionFactory.Create` は null を返せない口。2 つの工場で同じ型を private に 2 つ作らない | 工場ごとに private の入れ子クラス（r0） |
| D-36 | パイプの読み直しの契機を 4 フレームの刻み（`Observable.IntervalFrame(4)`）から、`FluidNetworkClientCache` に足す変化の通知 `OnReplicaVersionChanged`（`ApplyFullState`・`ApplyTickDiff` の直後に流す）の購読へ改める。`IFluidReplicaReader` は `OnReplicaChanged` を持ち、`BlockFluidSlotsModel` は刻みを ctor で受けない（出所: 設計書 [A-57] の改め。S4-alpha 計画 D-57〈moores-code-review〉が A-57 の例外文を差し替えたのを feature/uitk の取り込みで受けた） | 4 フレーム周期の同値読みは AGENTS.md「状態変化の検知は購読で」に反し、正本に残ると列車・特殊族の連続値へ前例として広がる。変化を起こす操作が `ApplyFullState`・`ApplyTickDiff` の 2 つに限られ、直後に流せば取りこぼしが無い。パイプの水位の表示（`FluidPipeWaterLevelPresenter`）は描画更新の順番の中で `ReplicaVersion` を見るまま変えない | 4 フレームの刻みのまま（D-20 の r1 案。設計書 A-57 と食い違う）／毎フレーム `ReplicaVersion` を見る（同じく同値読み） |

---

## 設計書・実行計画へ反映する行

本体が設計書・実行計画へ写す下書き（ID は A-150〜A-154）:

- **A-150** ブロックの区画は「詳細 Model（State の `BlockInventory/<族>/`。族ごとに `Current` 1 本）→ 区画の Presenter（Attach で購読・Detach で切る。判断は持たず State の関数を呼ぶ）→ View（`Render` と、Presenter の状態を変える押下の `IObservable` だけ。ブロックの枠の押下は View が `IBlockSlotCommands` を直接呼ぶ）」の 3 段で作る。値の有無は `SectionContent<T>` で配る（S4-machine D-1・D-2・D-23・D-24）
- **A-151** 機械の選択レシピは `MachineRecipeSelection`（Unselected／Selected）の union で持ち、サーバーの文字列から解くのは 1 か所（L0-s4b D-27 の境界を State に延ばす。S4-machine D-4）
- **A-152** 機械の文脈（部屋・抽選・搬入停止）は機械の区画の本文の先頭に置き、機械以外は文脈の区画として 3 欄のどれかがあるとき（Web `hasContextSections`）だけ出す。基盤（`SectionStackBody`）は機械を知らない（S4-machine D-12・alpha D-3）
- **A-153** 配置の写しで L1 の舞台（1280×720）に乗らない高さの件は、Game View の解像度を切り替える録画で見る。処分表には書かず `done.md` の証拠に残す（S4-machine D-15）
- **A-154** `Client.Ui.State` はサーバーのブロック・液体・歯車の型を読むため `Game.Block`・`Game.Fluid`・`Game.Gear` を参照する（S4-machine D-5。S4-special も `Game.Gear` を使う）

---

## 起草メモ（本体向け）

- **alpha の口は改訂版（85f905aa3d）の Produces 一覧に合わせた**: 区画は `IBlockInventorySection`（`Root`・`Attach(OpenBlockInventoryPanel)`・`Detach`・`IsApplicable`）、工場は `ISectionStackSectionFactory.Create(SectionStackSectionMount)`、液体の設定は `SectionStackConfig.Fluid` の union（`RowSectionStackFluid`・`DetailsSectionStackFluid`）、大型の寄与は `IsLargePanel`、テスト補助は `BlockInventoryTestBodies.SectionStack`・`BlockInventoryTestShell.Create`。工場は null を返せないので、設定が合わない FluidRow・FluidDetails は `IsApplicable` が常に偽の区画を返す。残るのは alpha マージ後の実物の綴りの確認（Task 1 Step 2）だけ
- **C4-c マージ後に実物と照合（要）**: `RecipeRow`・`LevelInheritanceBadge`・`RecipeListScrollView` は PLAN-W3c の C4-c 計画の Produces を写した（`RecipeActionButton` は機械では使わない）。レシピ選択モードは Task 12（最後の実装 Task）に隔離し、C4-c 未マージなら Task 13〜15 の選択に関わらない部分を先に進めて待つ
- **PLAN-W3c への要件は C4-c 計画で既に満たされている**: 下調べで挙げた 6 点（呼び手が作る枠ごとの testid・静止矢印の testid・アクション欄なし・秒の testid `<testid>-duration`・`SlotAmountBadge.Show`・つまみ `--recipe-list-gap`/`--recipe-row-padding-block`/`--recipe-slot-size-max` の上書き）は全部 C4-c の Produces にある。行の間は祖先のクラスで `--recipe-list-gap: var(--machine-recipe-row-gap)` と受け直す（C# でトークンを読まない）。残るのは実物との照合だけ
- **設計書 §1.4 の漏れ**: `MachineRecipeSelectionRow.test` の 5 件が §1.4 の写す表に無い。本体の決定どおり本レーンが写す（Task 12）。設計書の表への追記は本体へ
- **asmdef**: `Game.Block`・`Game.Fluid`・`Game.Gear` を `Client.Ui.State` に足す。S4-special も `Game.Gear` を足す見込みなので、取り込みで同じ行が衝突したら 1 行に畳む
- **S4-special と分け合うもの**: `blockDetails` は実行計画 §1.4（876b3229e5）どおり分けた（D-29）。本レーンが写すのは 6 名で、`renders ${type} detail section` と「歯車ポンプは電力行を持たず歯車行を出す」は同じタグを S4-special と分け合う（本レーンは区画の面、S4-special は歯車・フィルタの面）。「歯車機械は消費トルクと基準付きRPMを赤なしで出し、網の需給行を持つ」「歯車発電機は発生トルクと現在RPMだけを出す」は S4-special だけが写す。`4-block-gear*` の参照の歯車の行も S4-special の差
- **登録のディレクトリ**: E-1（ad92a44777）どおり `Client.Starter/Registration/GameUi/Screens/` に置く（`GameUi/` 直下は alpha で 10 本になる。最初に要したレーンがディレクトリを作る。S4-inventory も同じ置き場。D-34）
- **E-22 の自前撮影**: 1280×720 の参照に無い `machineShortInput`・`machineFilledOutput`・清浄室 12 の計 14 枚を Task 14 で 1280×720 で自分で撮る（2560×1080 は既存の参照とも比べる）。撮影表への追加は integrator へ依頼
- **規約の遵守**: `disposition.md` には捨てる 4 件だけを書き、`<web-test>` で写した件の行を書かない。alpha の `BlockInventoryDesignTest` には追記せず、同じタグを `MachineDesignTest`・`FamilyDesignTest` に置く
- **レビュー r1 の反映**: 再現役の判定で「再現した」「一部」の節を直した（D-1・D-4・D-6・D-10・D-12・D-16・D-20 を直し、D-22〜D-31 を足した。どれも「出所: レビュー r1」）。「再現せず」の 3 節（配置テストの行数・歯車駆動の bool・外部解決の欠損）は直していない。ADR 側の 2 件（設計書 §4.2 の流体の複製の間隔・§1.4 の blockDetails の分け方）は本体のコミット 876b3229e5 に合わせた。S4-special 計画 14 行の E-23 の古い記述は本体から連絡済み
- **S4-special の blockDetails**: S4-special へ本体から連絡済み（44a0bb0334 の対応表）
- **レビュー r2 の反映**: 再現役の判定で「再現」の 15 件を直した（D-6 に 1 文、D-16 を ADR に合わせて書き直し、D-32 を足した。どれも「出所: レビュー r2」）。「再現せず」の 5 件は直していない。S4-train 計画 107 行の `FluidInventoryContents : UiStack` は本計画の `: VisualElement` に合わせるよう指揮経由で連絡する（S4-train は `UiStack` 固有の API を使わないので今は壊れない）
- **レビュー r3 の反映**: 8 件を直した（D-16・D-24 を書き直し、D-33 を足した。どれも「出所: レビュー r3」）。`block_not_open` の良性は S4-train と揃える（本体から S4-train へ連絡）
- **要裁定**: なし
- **未確認（実装時に実物で確かめる）**:
  - L1 のテストでマスタ（機械の param・レシピ）をロードできるか。できなければ偽の要約で組み、マスタを読む件は録画へ
  - C3-a の `ItemSlot` が所持/必要の表示のとき個数バッジを隠すか（隠さなければ `MachineSlotContents` で count を null にする）
  - 開いた直後に状態詳細がパネルより遅れて届き、小型から大型へ作り直されたとき選択モードの状態が消えないか（死活表の要確認。録画で見る）
  - 機械の液体の行のゴースト（`FluidSlotContent.Ghost`）に液体名のツールチップが付くか（C4-a の実物）

---

## 本 PR 外のリファクタ提案

Phase 2.6 の第 3 バケツ。このレーンのタスクにはしない。

- 既存 alpha `Screens/BlockInventory/Parts/BlockItemGrid.cs` と新設 `Machine/Slots/BlockSlotGestureCommands.cs`: 枠の押下の種類 → `IBlockSlotCommands` の同じ写し。`Parts/` の公開静的関数へ寄せる

- 既存 `Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs`・`MinerDetailDtoBuilder.cs`・`PumpDetailDtoBuilder.cs`・`CleanRoom/*`・`Topics/Inventory/BlockInventoryRepublishPolicy.cs`・`Game/Actions/MachineRecipeSelectionActions.cs` と新設 `Client.Ui.State/BlockInventory/{Machine/Detail,Family,Fluid,Context}/` の写し・`MachineRecipeSelectCommand`: 同じ導出と解放の検査が 2 本ある（D-3）。UITK 切替の時に `Client.WebUiHost` 側を消す

---

## 後続レーンへの Produces 一覧

S4-train はこの節だけを読んで液体の表示を使う（E-23: 部品の持ち主は S4-machine、S4-train は使うだけ）。型名・シグネチャはこの計画の Task 3・Task 6 のコードと同じ綴り。すべて `moorestech_client/Assets/Scripts/` の下。

### 液体の詳細の View（`Client.Ui.Screens.BlockInventory.Fluid`）

```csharp
namespace Client.Ui.Screens.BlockInventory.Fluid
public sealed class FluidInventoryContents : VisualElement   // Web FluidInventoryContents。中に UiStack(Xs) を 1 つ持つ（UiStack は sealed で継承しない）
{
    public FluidInventoryContents(string testid, GameContentPresentation content);   // name = testid
    public void Render(IReadOnlyList<BlockFluidSlotState> fluids);                     // 呼ぶたびに行を組み直す。0 件で子なし
}
public sealed class FluidRowView : VisualElement       // 液体の横 1 行（進捗の矢印つき可）
{
    public FluidRowView(string testid, GameContentPresentation content);
    public void Render(BlockFluidSlotsViewModel viewModel, bool showProgress);
}
public static class FluidSlotContentMapper
{
    public static FluidSlotContent Map(BlockFluidSlotState slot, GameContentPresentation content);                          // 中身あり→Filled・それ以外→Empty
    public static FluidSlotContent MapWithGhost(BlockFluidSlotState slot, BoundFluidSlot bound, GameContentPresentation content); // 空でゴーストあり→Ghost
}
```
- `FluidInventoryContents` の要素: 自分はクラス `fluid-inventory-contents`（幅いっぱい）。行 i は `UiGroup(Sm)`（`UiLayoutClass.GroupNoWrap`。縦の中央揃えは `UiGroup` の内側の行が既に持つ）に `FluidSlotRow($"{testid}-slot-{i}")`（C4-a。枠 1 つ）、中身ありならその右に `UiStack(None)` で液体名（`text--sm`）と `"<量> / <容量>"`（`SlotAmountFormat.Format`・`text--sm`）。空の液体は枠だけ（名前と容量を出さない）
- View は購読しない。`Render` は使う側の Presenter が自分の Model の変化で呼ぶ（量の再取得で描き直すのも呼び直すだけ）
- 見た目の USS は `Fluid.uss`（本レーンが `GameUiTheme.tss` に `@import` 済み）。使う側は USS を足さなくてよい
- 押下は持たない（Web と同じ。液体の枠に操作は無い）
- テストの手本: `Client.Tests/Ui/Screens/BlockInventory/Fluid/FluidSectionsTest.cs`（「パイプは液体だけを進捗なしで描画する」「空のパイプは名前や容量を表示せず空スロットを残す」）

### 液体の列の値と読み手（`Client.Ui.State.BlockInventory.Fluid`）

```csharp
namespace Client.Ui.State.BlockInventory.Logic.Common     // L0-s4b（既存）
public abstract class BlockFluidSlotState { double Capacity; abstract bool HasRealFluid; static BlockFluidSlotState Empty(double capacity); static BlockFluidSlotState Filled(FluidId fluidId, double amount, double capacity); }
public sealed class EmptyBlockFluidSlotState : BlockFluidSlotState { }
public sealed class FilledBlockFluidSlotState : BlockFluidSlotState { FluidId FluidId; double Amount; }
namespace Client.Ui.State.BlockInventory.Fluid
public static class BlockFluidSlotConversion
{
    public static IReadOnlyList<BlockFluidSlotState> FromTanks(IReadOnlyList<FluidMessagePack> inputTanks, IReadOnlyList<FluidMessagePack> outputTanks);   // 入力→出力の順。FluidMaster.EmptyFluidId は Empty(容量)、他は Filled
    public static BlockFluidSlotState FromPipeNode(int fluidId, double amount, double capacity);
}
public sealed class BlockFluidSlotsViewModel { IReadOnlyList<BlockFluidSlotState> Slots; bool HasProgress; float Progress; }
public interface IBlockFluidSlotsModel { IReadOnlyReactiveProperty<BlockFluidSlotsViewModel> Current { get; } }   // 共有サービス（MainGameUiRegistration の並び）。追跡ブロック（タンク・機械・液体プラットフォーム・パイプ）の液体。閉・貨車では空の列・進捗なし
```
- 液体プラットフォームはブロックで、`TrainPlatformFluidContainerComponent` が `FluidMachineInventoryStateDetail` を出す（Web ホスト `BlockDetailDtoBuilder.cs:104-109` と同じ経路）。S4-train はプラットフォームの液体を `IBlockFluidSlotsModel.Current` から読める（ctor で受ける。共有サービス）。貨車の液体は追跡ブロックでないので対象外（貨車では空の列）。S4-train は貨車の液体を自分の Model で `SubInventoryModel.Fluids`（`FluidMessagePack`）から `BlockFluidSlotConversion.FromTanks(fluids, Array.Empty<FluidMessagePack>())` で `BlockFluidSlotState` の列へ写し、`FluidInventoryContents.Render` へ渡す
- テストの偽: `Client.Tests/Ui/State/BlockInventory/Family/FakeFamilyModels.cs` の `FakeBlockFluidSlotsModel : FakeModel<BlockFluidSlotsViewModel>, IBlockFluidSlotsModel`（`Set` で書き換える）

### 区画（S4-train は差し込まない）

- `FluidRowSectionFactory`（`SectionStackSlot.FluidRow`）・`FluidDetailsSectionFactory`（`SectionStackSlot.FluidDetails`）は標準の本文の区画で、列車の本文（`ITrainInventoryBodyFactory`）には入らない。列車の本文は上の View を自分の本文の中で直に使う
- 依存の順: S4-train は本レーンのマージ（`origin/feature/uitk`）を待つ。`FluidInventoryContents` の綴りの照合は S4-train の Task 1 で `grep -n "public FluidInventoryContents(\|public void Render" moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Fluid/FluidInventoryContents.cs`

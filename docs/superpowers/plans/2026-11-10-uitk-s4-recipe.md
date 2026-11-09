# UITK S4-recipe（段階 4 画面: レシピビューアとアイテム一覧・長押しクラフト）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/recipe`（`ItemListPanel`＝6 列のアイテム一覧・掴みドラッグのスクロール・クラフト可能数のバッジ、`RecipeViewer`＝選んだアイテムの見出しと装飾線・クラフト優先の単一レシピ列・長押しクラフト・機械レシピの液体と機械名・レベル継承バッジ・Esc で選択解除）を、C4-c（`RecipeRow`・`RecipeActionButton`・`RecipeListScrollView`・`LevelInheritanceBadge`）・C3-a（`ItemSlot`・`SlotGrid`）・C4-a（`FluidAmountSlot`）・C2-a（`GamePanel`）・C2-c（`GameScrollView`）・C3-b（`UiPressGesture`・`HoverTooltipMark`）の部品で Web と同じ見た目の UITK 画面にし、C4-e の `StageColumnsLayout` の `Viewer`・`Items` 列へ載せ、L0・L1・並べ比べ（720p・2560×1080）・録画 `uitk/uitk-recipe-craft` まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。State（`Client.Ui.State/Recipe/`）に、Web の topic の源（`CraftingRecipesTopic`・`RecipeViewerItemListTopic`・持ち物）を直に読むカタログ、選択（`RecipeItemSelection`）、組み立て（`RecipeViewModelFactory`・純関数）、それらを購読して不変の ViewModel を `IReadOnlyReactiveProperty` で配る `RecipeScreenModel`、操作 `IRecipeCommands.Craft(Guid)`（`UiCommandResult`）を置く。機械レシピの解放済み一覧の源は S4-alpha が `Client.Ui.State/Recipe/Source/` に作るものを使うだけ（指揮の割り当て。D-3）。View（`Client.Ui/Screens/Recipe/`）は `Render(vm)` で部品へ流し、エントリはレシピ GUID をキーに要素を使い回して長押しの状態を描き直しで失わない（Web の React の key と同じ）。長押しクラフトはパネルのスケジューラ（`schedule.Execute(...).Every(0)`）で `HoldCraftLogic` を回す画面の部品（`Screens/Recipe/Parts/CraftHoldDriver`）が持ち、`Update()` を使わない。一覧の掴みスクロールは `UiPressGesture` の受け手（`Parts/ItemListDragScroll`）が `UiDragThreshold.NextScrollTop` を当てる。Presenter は起動時に 1 回だけ 2 つの View を `StageColumnsLayout.Get(StageColumn.Viewer)`／`Get(StageColumn.Items)` へ載せ、`UiRoutingState` の画面が `PlayerInventory` のときだけ出す（Web `App.tsx` の `screen === "playerInventory"`）。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`IVisualElementScheduler`・`generateVisualContent`/`Painter2D`・`PointerDownEvent` 系）、UniRx、VContainer、NUnit＋`com.unity.ui.test-framework`（`UITestFixture`・`PanelSimulator.FrameUpdateMs`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（参照キャプチャ・`test-name-parity.mjs`・`npm run gen:i18n`）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`（repo の兄弟）、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`、`THEME` = `S/Client.Ui/Foundation/Theme`。Files 節と各コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（とくに「UITK段階2以降の画面もWeb版と同じ見た目にする」「UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1.1・§2.3・§2.4・§2.7・§3.2〜3.4・§4、[A-8]・[A-13]・[A-25]・[A-45]・[A-46]・[A-52]・[A-57]・[A-92]・C4-c の A-115〜A-129）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-recipe 行・§4.3・§5・§6.2・E-1・E-13・E-18・E-22）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ。設計書に足す行の案は末尾「## 設計書・実行計画へ反映する行」に書き、指揮が §7.1 へ写す（A-165〜A-169・LX-131〜LX-135）
- **レーンの起動は指揮が行う**（実行計画 §4.1。実装は moores-subagent-driven-development でこのホストの Claude Code の subagent が担う。Mac の Editor 枠が逼迫したら Windows の `~/bin/winunity`〈compile/test/play を ssh で回す〉を使ってよい）。報告は `LOGS/harness/uitk/S4-recipe/done.md`

## Requirements

設計の出所: 実行計画 §1.4 の S4-recipe 行（完了条件の原文:「RecipeViewer（`Viewer` 枠）・ItemListPanel（`Items` 枠 [A-45]。6 列・クラフトは `RecipeActionButton` [A-46]・長押しクラフトはパネルのスケジューラ・コンテナクエリは `GeometryChangedEvent`）。アンカー `recipe.craft-button`・`recipe.item-<itemId>`。写す: recipe 52（P19・C8・I1。L24 は L0-s4a 済み）。パネルは `StageColumnsLayout.Get(StageColumn.Viewer)`／`Get(StageColumn.Items)` へ載せる [A-92]」）、§6.2、C4-c 計画の「画面のレーンへの約束（S4-recipe）」、指揮の申し送り（機械レシピの源は S4-alpha）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | State の源: クラフトレシピは `MasterHolder.CraftRecipeMaster.GetAllCraftRecipes()` を `IGameUnlockStateData.CraftRecipeUnlockStateInfos[guid].IsUnlocked` で絞った `CraftRecipeData` の列（Web `CraftingRecipesTopic` の写し）。アイテム一覧は `MasterHolder.ItemMaster.GetItemAllIds()` を `ItemRecipeViewerDataContainer.EvaluateVisibility` が `Show` のものに絞った列（`RecipeViewerItemListTopic` の写し。`UnknownType` は 1 回だけ警告ログ）。機械レシピは S4-alpha の源（`Client.Ui.State/Recipe/Source/`）から受ける。持ち数は `IPlayerInventoryOperations.ReadSnapshot().MainSlots` を `OwnedItemCounts.Build` で数える | `CraftRecipeCatalogTest`・`RecipeViewerItemCatalogTest`（L0。テスト mod のマスタ）が緑 |
| R-2 | State の組み立て: 選択なしは `RecipeViewerNoSelection`、選択ありは `RecipeViewerItemSelected(itemId, entries)`。entries は `CraftLogic.BuildRecipeEntries` の順（クラフト優先の単一列）で、クラフトは素材ごとの所持・必要・不足とクラフト可否（`CraftLogic.Craftable`）、先頭がクラフトならその 1 件だけ `CarriesTutorialAnchor`。一覧は `CraftLogic.CraftableResultCounts` の数を各セルへ。解放イベント・製作上限の解放・持ち物の変化・選択・言語の変化で同期に組み直す（[A-57]） | `RecipeViewModelFactoryTest`・`RecipeScreenModelTest` が緑（Web `RecipeContent.test.ts` 2 件） |
| R-3 | 操作: `IRecipeCommands.Craft(Guid)` は未知の GUID を `CraftRecipeUnknown`（wire `invalid_recipe`）、未解放を `CraftRecipeLocked`（`recipe_locked`）で断り（どちらも良性でない。トースト）、通れば `ICraftRequestSender.Send(guid)`（本番は `ClientContext.VanillaApi.SendOnly.Craft`）。View は拒否を `UiCommandResultToaster.Report("recipe.craft", result)` へ渡す | `RecipeCommandsTest` が緑。`UiCommandRejectReason` の 3 つの switch が網羅（既存の `UiCommandRejectReasonTest` 系が緑） |
| R-4 | アイテム一覧の見た目: `GamePanel("item-list-panel", Default)`、題 `ui.recipe.catalogTitle`、幅 378・高 452・`Items` 列の右上寄せ、つまみ `--panel-top -6.821px`・`--panel-bottom -9.17px`・`--panel-left -1.04px`・`--title-shift-x 1.57px`・`--title-scale-x 0.963`・`--title-scale-y 0.861` を画面の USS で同名上書き（[A-25]）。中に `GameScrollView("item-list-scroll")`（バー 4px・溝は透明・つまみは白 88%・縦だけ）と `SlotGrid("item-list-grid", 6)`（`--slot-size 46.144px`・`--slot-grid-gap 8.656px`・`--slot-grid-row-gap 8.896px`・`--icon-pad 5px`・`--count-bottom -1px`・`--count-font-size 16px`・`--count-letter-spacing 0.12em`）。各セルは `ItemSlot`（catalog・数はクラフト可能数。0 ならバッジ無し）にアンカー `recipe.item-<itemId>` | `ItemListViewTest`・`ItemListScrollGeometryTest` が緑（Web `recipe.spec.ts` 8 件目・`itemListScroll.spec.ts` 1・2・6 件目）。並べ比べ（R-12） |
| R-5 | 一覧の操作: セルのタップ（実画面 5px 未満）でそのアイテムを選ぶ。5px を超えて動かすとスクロール（`NextScrollTop`）し、離しても選ばない | `ItemListDragScrollTest`（Web `useDragScroll.test.ts` 2 件）・`ItemListViewTest`（Web `recipe.spec.ts` 4 件目）が緑 |
| R-6 | ビューアの見た目: `GamePanel("recipe-viewer", Craft)`、幅 337.2、高さは未選択 300・選択あり 432.983、`Viewer` 列の上寄せ中央、上 2px、`translate(0.391px, -0.391px)`。未選択は `selectItemHint`（sm・dimmed・中央）。選択ありは見出し（名前 19.75px・500・字間 0.01em・影、下に 276.743×9 の装飾線＝両端が消えるグラデの 1px 線と中央の 7×7 の菱形）、レシピが無ければ `noRecipes`、あれば `RecipeListScrollView("recipe-entry-list")`。選択が変わるとスクロールは先頭へ | `RecipeViewerViewTest` が緑（Web `recipe.spec.ts` 2・6 件目、`recipeListScroll.spec.ts` 2 件）。並べ比べ（R-12） |
| R-7 | クラフトのエントリ: `RecipeRow("craft-recipe-box-<guid>", "craft-progress-arrow-<guid>")`、秒数 `ui.recipe.duration`（矢印の上。ボタンは秒数を持たない）、素材は `ItemSlot`（不足は `insufficient`・`ItemSlotShortage`〈`MaterialTooltipText`〉・左押下でその素材を選ぶ）、結果は `ItemSlot`（数）、操作は `RecipeActionButton("craft-button-<guid>", craftButtonLabel, Medium)`（クラフト不可で無効）。先頭のクラフトのボタンだけアンカー `recipe.craft-button` | `CraftRecipeEntryViewTest` が緑（Web `CraftRecipeEntry.test.ts` 1・2 件目、`recipe.spec.ts` 1・7 件目） |
| R-8 | 長押しクラフト: ボタンの左押下で始め、離す・ボタンの外へ出る・取消で止めて経過 0。毎フレームの経過（上限 0.25 秒）を `HoldCraftLogic.Advance` に足し、到達ごとに `Craft(guid)`。保持中は矢印 `SetProgress(progress)`、それ以外は `SetProgress(0)`。クラフト可否が偽になったら止める（持ち物が尽きるまで連続クラフト）。フォーカス中の Enter も押している間だけ同じ長押しにし、Space は何もしない（ボタンのインスタンスだけ `focusable=true`。D-31） | `CraftHoldTest`・`CraftHoldKeyTest` が緑（Web `recipe.spec.ts` 3・5 件目・`CraftRecipeEntry.test.ts` 3 件目）。録画（R-13 の段 9） |
| R-9 | 機械のエントリ: `RecipeRow("machine-recipe-box-<guid>", "machine-progress-arrow-<guid>")` を `SetIdle()`、秒数は `recipe.time`。素材はアイテム（数・左押下で選ぶ）の後ろに `FluidAmountSlot("machine-recipe-entry-<guid>-input-fluid-<i>")`（量）、結果も同様（`-output-fluid-<i>`）。操作の欄は機械のアイコン（`--recipe-info-icon-size`。無ければ ID 文字）・機械名（`--text-muted`・`--recipe-info-text-size`・省略記号・最大幅 `--recipe-action-width`）・`LevelInheritanceBadge("machine-recipe-level-inheritance-<guid>")` | `MachineRecipeEntryViewTest` が緑（Web `MachineRecipeEntry.fluids.test.ts`・`.localization.test.ts`・`recipe.spec.ts` 9 件目） |
| R-10 | Esc で選択を外す: 画面の遷移と同じフレームで `InputManager.UI.CloseUI` が押されていれば選択を外す（Web `RecipeSelectionKeyHandler` のゲーム層の Escape の写し） | `RecipeSelectionCloseResetTest` が緑。録画（R-13）の段 |
| R-11 | スクロールとツールチップ・チュートリアル: 一覧・レシピ列はクリップ境界をハイライトの逃げ（`--tutorial-anchor-clip-inset`）ぶん外へ広げ、最上段のアンカーでもラベルが出る・隠れ始めたらラベルを引っ込め完全に隠れたら枠も消える（S2-tutorial の測定で判定）。スロットのツールチップは `HoverTooltipHost` の面で、祖先のスクロールで引っ込む | `RecipeTutorialClipTest`（Web `itemListScroll.spec.ts` 3〜5 件目・`recipeListScroll.spec.ts` 2 件目）・`RecipeSlotTooltipTest`（Web `slotTooltip.spec.ts` 2 件）が緑 |
| R-12 | 並べ比べ: 参照 `4-player-inventory`・`4-item-list-large`・`5-tutorial-recipe-item`（1280×720・2560×1080）と、参照に無い「レシピ選択あり（クラフト 2 件＋機械 1 件・保持中・不足と無効）」を master の使い捨て worktree の mock-host で自分で撮った絵（E-22）を UITK と並べ、差の表を `done.md` に書く | 4 状態×2 解像度が報告ページにあり、差の表の全行に「同じ／直した／LX-n／捨てる」のどれかがある。720p で 2px を超える差は全部 LX-n か直した |
| R-13 | 録画 `uitk/uitk-recipe-craft`（全体スイッチ UITK）: Tab でインベントリを開く → 一覧のセルを押して選ぶ → 一覧を掴んでドラッグしても選択が変わらない → 素材を持たせてクラフトボタンを長押しし素材が尽きるまでに 2 回以上クラフトされ持ち物が増える → ボタンの外へ出して止まる → Esc で閉じ、再度開くと未選択の案内が出る | `result.json` が `Success: true`、録画 mp4、`done.md` に UITK で回した証拠（Note の `系=GameUi`） |
| R-14 | 登録と共有区画: `MainGameUiRegistration` の画面登録区画に `RecipeGameUiRegistration.Register(builder);` の 1 行、`GameUiTheme.tss` の `Screens` 区画に `@import` 2 行、`DerivedTokens.uss` 末尾に名札付きブロック「S4-recipe」、`handWrittenDerivedRows.mjs` の行と `translucent-tokens.md` の再生成、`localization.csv` に拒否文 2 行と `npm run gen:i18n`、`UiCommandRejectReason` の末尾 2 値と 3 switch、`TutorialAnchorCoveragePending.cs` から自分の 2 行を消す | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`・`TutorialAnchorCoverageTest`）が緑 |
| R-15 | 写した Web テストは `/// <web-test>` で示し、写さないものだけ `docs/uitk/disposition.md` に理由つきの行を足す | `node tools/uitk/test-name-parity.mjs --bundle 4 --emit-missing` で recipe の行（下の表の 52 件）の未対応が 0。終了コードが 2 でない |
| R-16 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd を閉じる → `moores-wt rm` | `git log origin/feature/uitk` にこのレーンのマージコミット。`done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 部品（`RecipeRow`・`RecipeActionButton`・`RecipeListScrollView`・`LevelInheritanceBadge`〈C4-c〉、`ItemSlot`・`SlotGrid`〈C3-a〉、`FluidAmountSlot`〈C4-a〉、`GamePanel`〈C2-a〉）と Foundation（`StageColumnsLayout`・`UiPressGesture`・`HoverTooltip*`・`TutorialAnchor`・`AnchorIds`）は変えない。例外は 1 つ: `GameScrollView.uss` に溝・つまみの色と縦バーの上下右の位置のつまみ 5 本を既定値＝今の値で足す（D-9・A-167。見た目は変わらない）。ほかに要るものは `done.md` の「integrator への依頼」に書く
- 機械レシピの解放済み一覧の源（`Client.Ui.State/Recipe/Source/`）は作らない（S4-alpha。D-3）。機械画面のレシピ選択（`MachineRecipeSelectionRow` 等）は S4-machine、インベントリ・装備（`Inv` 列）は S4-inventory、ビューア＋機械 UI＋インベントリの同時表示の L3 は S4-gamma が持つ。重なりは下の「他レーンとの重なり」の表
- `BlockInventoryPanel`（S4-alpha）も `Viewer` 列へ載るが画面 `SubInventory` のときだけで、このレーンの View は `PlayerInventory` のときだけ出す。列そのもの（`StageColumnsLayout`）は触らない
- ボタンの `title`（`holdToCraft`）と掴み中のカーソル `grabbing` は写さない（CEF の画面描画にツールチップとカーソルの受け手が無く、ゲーム内の Web では出ていない。D-12）
- 接続待ちの表示（Web の `ConnectingPlaceholder`）は作らない（[A-13]）
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は変えない（`localization.csv` からの `npm run gen:i18n` の生成物を除く）
- L3（EditModeInPlayingTest）は持たない（S4-gamma）

### 他レーンとの重なり

| 重なる所 | このレーン | 相手 | 決め |
|---|---|---|---|
| `StageColumn.Viewer` 列 | `PlayerInventory` で `RecipeViewerView` | S4-alpha が `SubInventory` で `BlockInventoryPanel` | 同時に出ない。両方の View が自分の画面の条件で `display` を切る。同時表示の L3 は S4-gamma |
| `StageColumn.Inv` 列 | 触らない | S4-inventory | — |
| 機械レシピの源 | 使うだけ | S4-alpha が作る（`Recipe/Source/`） | Task 1 で実物と照合 |
| 機械のレシピ表示とレシピ選択 | ビューアの `MachineRecipeEntryView`（`Screens/Recipe/Parts/`） | S4-machine の選択行（`Screens/BlockInventory/…/Parts/`） | 同じ部品（`RecipeRow`・`LevelInheritanceBadge`）を別々の包みで使う。包みは共有しない（Web も別ファイル） |
| `UiCommandRejectReason` の末尾 | 2 値 | 他の S4 レーンも足す | 末尾追記の衝突は両方を採る（E-1） |
| `TutorialAnchorCoveragePending.cs` | 自分の 2 行を消す | S4-inventory・S4-research も自分の行を消す | 消した側を採る |

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s4-recipe --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる
- **依存**: C4-c・C4-e（`StageColumnsLayout`。マージ済み #1626）・L0-s4a（マージ済み）・C3-b（`UiPressGesture`・`HoverTooltipHost`。マージ済み）・L0-s3（`UiDragThreshold`。マージ済み）・**C4-a**（`FluidAmountSlot`。機械レシピの液体）・**S2-tutorial**（`TutorialAnchor.MarkClipContainer`・`TutorialAnchorCoveragePending`・測定の関数）・**S4-alpha**（機械レシピの源）。Task 1 Step 1 で全部の実物を確かめ、無ければ止めて指揮へ報告する（実行計画 §1.4 の改め案は末尾）
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有区画は `GameUiTheme.tss` の `Screens` 区画の `@import` 2 行、`MainGameUiRegistration` の画面登録区画の 1 行、`DerivedTokens.uss` 末尾の名札付きブロック「S4-recipe」、`tools/uitk/webTokens/handWrittenDerivedRows.mjs` の `rowSources` の行と再生成した `docs/uitk/translucent-tokens.md`、`Localization/localization.csv` の追記と `W/src/shared/i18n/generated/localizationKeys.ts` の再生成（台帳「## 追従記録」に 1 行）、`docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」（LX-131〜135）「## 追従記録」の末尾、`docs/uitk/disposition.md` の行、`S/Client.Ui.State/Commands/UiCommandRejectReason.cs` の末尾 2 値と 3 switch、`S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` の自分の 2 行の削除。宣言した部品の変更は `S/Client.Ui/Components/Controls/GameScrollView/GameScrollView.uss` のつまみ 5 本だけ
- **規約**（AGENTS.md）: 1 ファイル 200 行未満・1 ディレクトリ 10 ファイルまで・partial 禁止・`Func<>` 禁止・イベントは UniRx（`Action` で発火しない）・try-catch 禁止（外部境界のみ）・主要処理に日英 2 行コメント・デフォルト引数禁止・初期化は `Initialize`（ctor → `Initialize`）・単純な getter/setter プロパティ禁止（`{ get; private set; }` は可）・`[SerializeField]` は使わない・fail-closed（拒否・保留・無視）の経路は開発者が読めるログへ理由を出す・`Update()` を使わない（購読と変化の直後のプッシュ）・null チェックは外部データと非同期ロードだけ
- **USS の規則**（ref-uitk-design）: 画面の USS は自分のルートのクラスから書き、ほかの部品のクラスを書かない（部品の見た目はつまみの上書きだけ）。色は `var()` だけ（直書き禁止。Web が直書きする色は `DerivedTokens.uss` の「S4-recipe」ブロックへ）。`gap`・`z-index` は使わない。各 USS の先頭に `/* web: <Web のファイル> */`。calc() が要る値は C# が `UssCustomStyleReader` で読む。字間は UITK では「字の大きさ/100 × 値」で効く（px で書いても同じ。em は生成器の換算に合わせる）
- **色の比較**はランタイムの PanelSettings（Linear 合成）で撮る（ギャラリーは sRGB 合成で半透明が淡く出る）。半透明の α は明暗とも指数 1.85 の式（`correctAlphaForLinearBlend`。明るい色も同じ式でトークンごとに α を選ばない＝A-113）。明るい 2 本（白 88% のつまみ・`rgb(132 137 151 / 78%)` の線）も式の値で、Task 11 で参照と比べて 2px・RGB の差が残れば LX に記帳する（LX-81 の中〜明るい地の残差と同じ扱い）
- **新しい共有を見つけたら**（2 機能目が使う部品・Foundation の口）、自分で作らず `done.md` の「## integrator への依頼」に「何が・なぜ・どの Web の箇所・2 機能目はどこ」を 1 行で書き、画面側は既存の部品の組み合わせで済ませて差を LX-n に記帳する
- **使う部品と公開 API**。マージ済み（C2-a・C2-c・C3-a・C3-b・C4-e・L0-s3・L0-s4a・I-fix2）は `origin/feature/uitk`（0a94c9b96b）の実物から写した。C4-c・C4-a・S2-tutorial・S4-alpha はそれぞれの計画の Produces から写したので **マージ後に実物と照合**（Task 1 Step 1 で grep し、名前が違えば以後を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <旧> → <実物>」と書く）

```csharp
// ===== C4-e（実物。S/Client.Ui/Foundation/Stage/Columns/）=====
namespace Client.Ui.Foundation.Stage.Columns
{
    public enum StageColumn { Inv, Viewer, Items }                         // 列の name は stage-column-viewer・stage-column-items 等
    public sealed class StageColumnsLayout : VisualElement
    {
        public VisualElement Get(StageColumn column);
        public void SetLeftPaddingCollapsed(bool collapsed);               // このレーンは呼ばない
    } // DI は FoundationGameUiRegistration が Singleton。GameUiStageColumnsInstaller が舞台へ載せる
}

// ===== C2-a（実物）=====
namespace Client.Ui.Components.Panels
{
    public enum GamePanelFrame { Default, Craft }
    public sealed class GamePanel : VisualElement
    {
        public GamePanel(string testid, GamePanelFrame frame);            // name = testid。Craft は GamePanelCraftGrip を持つ
        public void SetTitle(UiText title);
        public override VisualElement contentContainer { get; }          // 本文
    } // つまみ --panel-top/right/bottom/left・--title-shift-x・--title-scale-x/y（A-25）
}

// ===== C2-c（実物）=====
namespace Client.Ui.Components.Controls
{
    public sealed class GameScrollView : ScrollView                    // 縦だけ。バーは本文に重ねる。幅は --scrollarea-scrollbar-size
    {
        public GameScrollView(string testid);
        public IObservable<float> OnScrollOffsetChanged { get; }
        public IObservable<Unit> OnContentLayoutChanged { get; }
        public void SetScrollOffset(float y);
        public float GetViewportHeight();
        public float GetContentHeight();
        public bool TryGetContentTop(VisualElement descendant, out float top);
    }
}

// ===== C3-a（実物。S/Client.Ui/Components/Slots/・Content/）=====
namespace Client.Ui.Components.Slots
{
    public readonly struct SlotGesture { public readonly SlotGestureKind Kind; public readonly bool Shift; }
    public enum SlotGestureKind { LeftDown, RightDown, LeftEnter, RightEnter, DoubleClick, HoverEnter, HoverLeave /* ほか */ }
    public sealed class ItemSlot : VisualElement
    {
        public ItemSlot(string frameTestid);                              // 中の SlotFrame の name = frameTestid
        public void SetContent(ItemSlotContent content);
        public IObservable<SlotGesture> OnSlotGestured { get; }          // 左押下は SlotGestureKind.LeftDown
    } // ツールチップは自分で HoverTooltipMark する（優先: 不足 → 上書き → アイコンの名前）。catalog は数 > 0 のときだけバッジ
    public sealed class ItemSlotContent { public ItemSlotContent(UiIcon icon, int? count, ItemSlotMarks marks, ItemSlotShortage shortage, ItemSlotGhost ghost, UiText tooltipOverride); }
    public readonly struct ItemSlotMarks { public ItemSlotMarks(bool selected, bool catalog, bool insufficient); }
    public sealed class ItemSlotShortage { public ItemSlotShortage(double owned, double required, bool lacking, UiText tooltip); }
    public sealed class SlotGrid : VisualElement { public SlotGrid(string testid, int columns); public void AddItem(VisualElement item); public void ClearItems(); }
}
namespace Client.Ui.Content { public sealed class GameContentPresentation { public UiIcon Item(ItemId id); public UiIcon Block(BlockId id); public UiIcon Fluid(FluidId id); public UiText ItemName(ItemId id); } }
namespace Client.Ui.Components.Slots { public sealed class GameIcon : VisualElement { public void SetIcon(UiIcon icon, GameIconFallback fallback); } public enum GameIconFallback { IdText, Label, None } }

// ===== C3-b・L0-s3（実物。S/Client.Ui/Foundation/Input/Gesture/・Tooltip/）=====
namespace Client.Ui.Foundation.Input.Gesture
{
    public sealed class UiPressGesture { public UiPressGesture(VisualElement element, IUiPressGestureHandler handler); } // 左・主ポインタだけ。押下で捕捉。実画面 5px（倍率を掛ける）
    public interface IUiPressGestureHandler { void OnPressStarted(Vector2 panelPosition); void OnDragMoved(UiPressMove move); void OnDragEnded(Vector2 panelPosition); void OnTapped(VisualElement pressTarget); void OnAborted(bool dragged); }
    public readonly struct UiPressMove { public Vector2 FromStart { get; } public Vector2 SinceLast { get; } }
    public static class UiDragThreshold { public const float ThresholdPx = 5f; public static bool Exceeded(float dx, float dy); public static float NextScrollTop(float startScrollTop, float movedY); }
}
namespace Client.Ui.Foundation.Tooltip { public static class HoverTooltipMark { public static void Mark(VisualElement element, UiText text); public static void Unmark(VisualElement element); } } // HoverTooltipScrollWatch が祖先 ScrollView のスクロールで引っ込める

// ===== F1・C3-a の口（実物）=====
namespace Client.Ui.Foundation.Anchor { public static class TutorialAnchor { public static void Mark(VisualElement element, params string[] ids); } }
namespace Client.Game.InGame.Tutorial.UIHighlight { public static class AnchorIds { public const string RecipeCraftButton = "recipe.craft-button"; public const string RecipeItemPrefix = "recipe.item-"; public static string RecipeItem(int itemId); } }
namespace Client.Ui.Foundation.Text { public class LocalizedLabel : Label { public LocalizedLabel(); public void SetText(UiText text); } public static class UiTypographyClass { public const string TextSm = "text--sm"; public const string TextDimmed = "text--dimmed"; public const string TextMedium = "text--medium"; } }
namespace Client.Ui.State.Text { public sealed class UiText { public UiText(LocalizationKey key); public UiText(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams); public static UiText FromContent(ContentLocalizationKey key); public static UiText FromVerbatim(string text); public string Resolve(); } }
namespace Client.Ui.State.Commands { public abstract class UiCommandResult { } public sealed class UiCommandAccepted : UiCommandResult { public static readonly UiCommandAccepted Instance; } public sealed class UiCommandRejected : UiCommandResult { public UiCommandRejected(UiCommandRejectReason reason, string detail); public UiCommandRejectReason Reason { get; } } }
namespace Client.Ui.State.Toast { public sealed class UiCommandResultToaster { public void Report(string commandName, UiCommandResult result); } }
namespace Client.Ui.State.Routing { public sealed class UiRoutingState { public IReadOnlyReactiveProperty<UiRoutingSnapshot> Current { get; } } public sealed class UiRoutingSnapshot { public UiScreen Screen { get; } } public enum UiScreen { None, PlayerInventory, SubInventory, ResearchTree, BuildMenu, ChallengeList, PauseMenu, TrainHud, TrainPause, SkitPause } }

// ===== L0-s4a（実物。S/Client.Ui.State/Recipe/・Items/・MaterialTooltip/）=====
namespace Client.Ui.State.Recipe.Data
{
    public sealed class CraftRecipeData { public CraftRecipeData(Guid recipeGuid, ItemId resultItemId, int resultCount, double craftTime, IReadOnlyList<ItemAmount> requiredItems); public Guid RecipeGuid { get; } public ItemId ResultItemId { get; } public int ResultCount { get; } public double CraftTime { get; } public IReadOnlyList<ItemAmount> RequiredItems { get; } }
    public sealed class MachineRecipeData { public Guid RecipeGuid { get; } public BlockId BlockId { get; } public double Time { get; } public IReadOnlyList<ItemAmount> InputItems { get; } public IReadOnlyList<ItemAmount> OutputItems { get; } public IReadOnlyList<RecipeFluidAmount> InputFluids { get; } public IReadOnlyList<RecipeFluidAmount> OutputFluids { get; } public RecipeLevelInheritance LevelInheritance { get; } }
    public sealed class RecipeFluidAmount { public FluidId FluidId { get; } public double Amount { get; } }
    public abstract class RecipeEntry { } public sealed class CraftRecipeEntry : RecipeEntry { public CraftRecipeData Recipe { get; } } public sealed class MachineRecipeEntry : RecipeEntry { public MachineRecipeData Recipe { get; } }
}
namespace Client.Ui.State.Recipe.Logic
{
    public static class CraftLogic
    {
        public static IReadOnlyList<RecipeEntry> BuildRecipeEntries(IReadOnlyList<CraftRecipeData> craftRecipes, IReadOnlyList<MachineRecipeData> machineRecipes, ItemId itemId);
        public static bool Craftable(CraftRecipeData recipe, IReadOnlyDictionary<ItemId, int> ownedCounts);
        public static IReadOnlyDictionary<ItemId, int> CraftableResultCounts(IReadOnlyList<CraftRecipeData> recipes, IReadOnlyDictionary<ItemId, int> ownedCounts);
    }
    public readonly struct HoldCraftStep { public readonly double Elapsed; public readonly bool DidCraft; }
    public static class HoldCraftLogic { public const double MaxFrameDeltaSeconds = 0.25; public static HoldCraftStep Advance(double elapsed, double deltaSeconds, double craftTime); public static double Progress(double elapsed, double craftTime); }
    public static class LevelInheritanceBadgeText { public static bool TryBuild(RecipeLevelInheritance inheritance, out UiText text); }
}
namespace Client.Ui.State.Items { public static class OwnedItemCounts { public static IReadOnlyDictionary<ItemId, int> Build(IReadOnlyList<SlotItem> slots); } /* SlotItem(ItemId, int) */ public readonly struct ItemAmount { public ItemId ItemId { get; } public int Count { get; } } }
namespace Client.Ui.State.MaterialTooltip { public enum MaterialTooltipKind { RecipeMaterial /* ほか */ } public static class MaterialTooltipText { public static UiText Create(MaterialTooltipKind kind, string itemName, double requiredCount, double ownedCount); } }
namespace Client.Ui.State.Inventory.Actions { public interface IPlayerInventoryOperations { PlayerInventorySnapshot ReadSnapshot(); /* ほか */ } } // ReadSnapshot().MainSlots はメインの枠だけ

// ===== C4-c（計画 uitk-plan-w3c/docs/superpowers/plans/2026-11-09-uitk-c4-c.md の Produces。マージ後に実物と照合）=====
namespace Client.Ui.Components.Controls
{
    public enum RecipeActionLabelWeight { Medium, Regular }
    public sealed class RecipeActionButton : Button { public const string UssClassName = "recipe-action-button"; public RecipeActionButton(string testid, UiText label, RecipeActionLabelWeight weight); public IObservable<Unit> OnClicked { get; } public void SetLabel(UiText label); } // focusable=false。SetEnabled は VisualElement 既定。長押しは置く側
    public sealed class RecipeListScrollView : VisualElement { public const string UssClassName = "recipe-list-scroll-view"; public RecipeListScrollView(string listTestid); public void SetLeadingContent(VisualElement content); public void ClearLeadingContent(); public void SetEntries(IReadOnlyList<VisualElement> entries); } // 列の name = listTestid、中の GameScrollView は <listTestid>-scroll。間隔は --recipe-list-gap（既定 --recipe-entry-gap）
}
namespace Client.Ui.Components.Recipe
{
    public sealed class RecipeRow : VisualElement { public const string UssClassName = "recipe-row"; public RecipeRow(string testid, string arrowTestid); public void SetSlots(IReadOnlyList<VisualElement> materials, IReadOnlyList<VisualElement> results); public void SetDuration(UiText duration); public void SetProgress(float ratio01); public void SetIdle(); public void SetAction(VisualElement action); public void ClearAction(); } // 秒数の name = <testid>-duration。SetDuration は載せる前（後なら LogError）。スロットの寸法は RecipeSlotLayout を GeometryChangedEvent で当てる（Web のコンテナクエリ cqw の写し）
    public sealed class LevelInheritanceBadge : VisualElement { public LevelInheritanceBadge(string testid); public void Show(UiText text); public void Hide(); } // 作った直後は隠れる
}

// ===== C4-a（計画 docs/superpowers/plans/2026-11-07-uitk-c4-a.md の Produces。マージ後に実物と照合）=====
namespace Client.Ui.Components.Slots
{
    public sealed class FluidAmountSlot : VisualElement { public FluidAmountSlot(string frameTestid); public void SetFluid(UiIcon icon, SlotAmountBadge badge); } // 量の帯の name = <frameTestid>-amount
    public abstract class SlotAmountBadge { public static readonly SlotAmountBadge Hidden; public static ShownSlotAmountBadge Show(double amount); }
}

// ===== S2-tutorial（計画 docs/superpowers/plans/2026-11-07-uitk-s2-tutorial.md の Produces。マージ後に実物と照合。テストだけが使う）=====
// TutorialAnchor.MarkClipContainer(VisualElement)（USS の overflow:hidden で切る容器の印。ScrollView の viewport は印なしで祖先クリップに数える）
// Client.Ui.Screens.Tutorial.Anchor.TutorialAnchorMeasure.Measure(VisualElement searchRoot, VisualElement coordinateSpace, string anchorId) : TutorialAnchorState（Ready(rect, clip)／NotFound／Hidden）
// Client.Ui.State.Tutorial.Logic.TutorialHighlightLabelPlacement.ShowsLabel(TutorialClipRect anchorRect, TutorialClipRect clip) : bool
// Client.Ui.State.Tutorial.Logic.TutorialHighlightClip.TryComputeInset(TutorialClipRect box, TutorialClipRect clip, float outset, out TutorialClipInset inset) : bool
// S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs の Owners に { AnchorIds.RecipeCraftButton, "S4-recipe" }・{ AnchorIds.RecipeItemPrefix, "S4-recipe" }

// ===== S4-alpha（出所: S4-alpha 計画 14a85b146c の末尾「## 後続レーンへの Produces 一覧」。レビュー中で変わりうる。マージ後に実物と照合）=====
// MachineRecipeData は Client.Ui.State.Recipe.Data（既存）。Client.Ui.State.asmdef への Game.UnlockState 参照は S4-alpha が足す（このレーンのカタログ・コマンドもそれに乗る。Task 1 で asmdef に参照があることを確かめる）
namespace Client.Ui.State.Recipe.Source
{
    // 解放済みの機械レシピ（Web Topics.machineRecipes の源。マスタ＋解放状態＋製作上限で組み直し、変化で配り直す）
    // ctor(IGameUnlockStateData, ILevelInheritanceCraftCapLookup, IVanillaApiEvent)。IInitializable の Initialize で初回の組み立てと購読（S4-alpha の登録が持つ）
    public sealed class UnlockedMachineRecipeSource : IInitializable { public IReadOnlyReactiveProperty<IReadOnlyList<MachineRecipeData>> Current { get; } }   // DI 登録済み（AsSelf。S4-alpha の登録ファイル）
}
```

- **Web の局所上書き変数（同名で画面の USS に書く）**: ItemListPanel の `--panel-top: -6.821px; --panel-bottom: -9.17px; --panel-left: -1.04px; --title-shift-x: 1.57px; --title-scale-x: 0.963; --title-scale-y: 0.861;`、`GRID_STYLE` の `--slot-size: 46.144px; --slot-grid-gap: 8.656px; --slot-grid-row-gap: 8.896px; --icon-pad: 5px; --count-bottom: -1px; --count-font-size: 16px; --count-letter-spacing: 0.12em;`、ScrollArea `scrollbarSize={4}` → `--scrollarea-scrollbar-size: 4px`、CraftRecipeEntry の素材スロット `--shortage-count-right: 0; --shortage-count-bottom: 0;`
- **トークン（実物の Tokens.uss）**: `--text-muted #a6a7ab`・`--item-list-count-bleed 5px`・`--tutorial-anchor-padding 8px`・`--tutorial-highlight-glow 4px`・`--tutorial-anchor-clip-inset 12px`・`--recipe-info-text-size 12px`・`--recipe-info-icon-size 36.8px`・`--recipe-entry-gap 10px`
- **localization のキー（実物の csv に有る）**: `ui.recipe.catalogTitle`・`selectItemHint`・`materialTooltip`・`duration`・`craftButtonLabel`・`noRecipes`・`levelInheritance`・`levelInheritanceCapped`。足すのは `ui.commandRejected.craftRecipeUnknown`・`ui.commandRejected.craftRecipeLocked` の 2 行だけ

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / Esc の選択解除を State から画面の層 `Screens/Recipe/Input/` へ移した（D-22）・Enter の長押しを計画内で確定しない形にした（その後、指揮の裁定で写すを agent 既定として確定。D-31）。注目点から開き直しの初期化を死活表と View に足した（D-23）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強6・弱4・第3バケツ4 / 修正: `_shownSelection` の null を判別 union へ、明るい半透明を A-113 の式の値へ（D-24）。質問しない指示のため残りは D-25〜D-29 に agent 既定で記録。第3バケツ 2 件を「本PR外のリファクタ提案」へ追記

---

## 死活表（Phase 2.5。Web で今プレイヤーが使える操作 → UITK 後に生きるか）

| 操作 | 生きるか | 根拠 |
|---|---|---|
| Tab でインベントリを開くと右に一覧・左にビューア | 生きる | Presenter が `PlayerInventory` で 2 つを出す（R-4・R-6）。録画 R-13 |
| 一覧のセルのクリックで選ぶ | 生きる | `ItemListDragScroll.OnTapped`（R-5） |
| 一覧の掴みドラッグ・ホイールでスクロール | 生きる | ドラッグは R-5、ホイールは `ScrollView` 既定 |
| 素材スロットのクリックでその素材のレシピへ移る | 生きる | `OnSlotGestured` の LeftDown → `Select`（R-7・R-9） |
| クラフトボタンの長押し（マウス） | 生きる | R-8。録画 R-13 |
| クラフトボタンにフォーカスして Enter で長押し（Space は何もしない） | 生きる | `RecipeActionButton` のインスタンスだけ画面側で `focusable=true` にし、`CraftHoldInput` が Enter の押下・離し・フォーカス外れを長押しへ渡す（D-31。2026-10-01 裁定 とゴールが同じ向き）。`CraftHoldKeyTest`・録画 |
| スロットのツールチップ（不足・名前） | 生きる | `ItemSlot` が自分で印。R-11 |
| ボタンの `title`（長押しの説明） | 変わらない | ゲーム内の CEF でも出ていない（D-12） |
| Esc で選択を外す（閉じると同時） | 生きる | R-10 |
| 閉じて開き直すと一覧とレシピ列が先頭から・長押しが止まる（Web は画面を出るとアンマウント） | 生きる | 両 View の `SetShown(false)` が中身を捨てる／スクロールを 0 に戻す（D-23）。`RecipeViewerViewTest`・`ItemListViewTest` |
| チュートリアルのハイライト（`recipe.craft-button`・`recipe.item-*`） | 生きる | 印は R-4・R-7。枠の描画は S2-tutorial。R-11 |
| 機械レシピの閲覧（液体・機械名・継承バッジ） | 生きる | R-9 |

## 写すテスト（28 件 = P19・C8・I1。recipe 全体 52 のうち L24 は L0-s4a 済み）

数え方: Web の `it(`／`test(` の呼び出し 1 つを 1 件と数える（`it.each`／`test.each` は展開しない＝W0-tools 書式。同じタグの NUnit が複数になってよい）。`describe` は数えずタグにも含めない。recipe の対象ファイルは台帳 `docs/uitk/TODO.md` の recipe 行の Web パス（`src/features/recipe/`・`e2e/tests/recipe/`）の下の `*.test.ts`・`*.spec.ts` で、`logic/*.test.ts`（L24）は L0-s4a が写し済み。タグは `/// <web-test file="<W/ からの相対パス>"><it の第 1 引数></web-test>`（E-13）。

| Web（file・件数） | it の第 1 引数 | 写し先（NUnit クラス・層） | Task |
|---|---|---|---|
| `src/features/recipe/views/RecipeContent.test.ts`(2) | 全レシピをクラフト優先の単一リストへ並べる／チュートリアルアンカーを先頭のクラフトエントリ1件だけへ付ける | `RecipeViewModelFactoryTest`（L0） | 3 |
| `src/features/recipe/views/CraftRecipeEntry.test.ts`(3) | 矢印の上の秒数をui.recipe.durationの書式で組み、ボタンは秒数を持たない／言語ごとの秒数書式を矢印上の表示へ反映する | `CraftRecipeEntryViewTest`（L1） | 6 |
| 同上 | SpaceではクラフトボタンのonKeyDownが何もせずEnterでは長押しを始める | `CraftHoldKeyTest` | 6 |
| `src/features/recipe/views/MachineRecipeEntry.fluids.test.ts`(1) | 液体の入力・出力をアイテムの後ろに量付きで描き、クリック遷移は持たせない | `MachineRecipeEntryViewTest`（L1） | 7 |
| `src/features/recipe/views/MachineRecipeEntry.localization.test.ts`(1・I) | 機械名をblockGuidから解決し、fallbackと言語変更をアイコンaltと表示・秒数へ反映する | `MachineRecipeEntryViewTest`（L1。alt は UITK に無いので名前の表示と ID 文字の代替で見る。部分移植の残りは `done.md`） | 7 |
| `src/features/recipe/panels/useDragScroll.test.ts`(2) | 閾値未満で離すとタップとして押下点を通知する／閾値を超えて動かすとスクロールし、離してもタップにしない | `ItemListDragScrollTest`（L1） | 9 |
| `e2e/tests/recipe/recipe.spec.ts`(9) | 秒数は矢印の上に出し、クラフトボタンは秒数を持たない | `CraftRecipeEntryViewTest` | 6 |
| 同上 | 正本のヘッダ装飾、1段時の無スクロールバー、主要構造を保つ | `RecipeViewerViewTest` | 8 |
| 同上 | アイテム選択でレシピ表示、長押しで素材が尽きるまで連続クラフトする | `CraftHoldTest`（＋録画 R-13） | 6 |
| 同上 | リスト上をドラッグしてもアイテム選択は変わらない（スクロール操作扱い） | `ItemListViewTest` | 9 |
| 同上 | 押下後にボタンから外れるとクラフトが止まり経過時間がリセットされる | `CraftHoldTest` | 6 |
| 同上 | 複数レシピはクラフト優先の単一リストで同時に表示される | `RecipeViewerViewTest` | 8 |
| 同上 | クラフトエントリが複数でもチュートリアルアンカーは1件だけ付く | `RecipeViewerViewTest` | 8 |
| 同上 | クラフト可能数0のアイテムは個数バッジを出さない | `ItemListViewTest` | 9 |
| 同上 | 機械レシピの一覧に液体の入力を量付きで出す | `MachineRecipeEntryViewTest` | 7 |
| `e2e/tests/recipe/itemListScroll.spec.ts`(6) | スクロール領域はパネル本文いっぱいで、既定件数では溢れずバーも出ない／パネルを超える件数で初めて縦バーが出る。横は常に出ない／7段は溢れず、最下段までスクロールしても最終段に逃げが残る | `ItemListScrollGeometryTest` | 9 |
| 同上 | 溢れていない一覧の最上段でもチュートリアルのラベルが出る／アンカーがスクロールで隠れ始めたらラベルを引っ込め、完全に隠れたら枠も消す／クリップ境界はグリッドからハイライトの逃げぶん離れ、枠が削られない | `RecipeTutorialClipTest` | 10 |
| `e2e/tests/recipe/recipeListScroll.spec.ts`(2) | レシピが溢れてもリストの視口高は変わらず、縦バーだけが出る | `RecipeViewerViewTest` | 8 |
| 同上 | クリップ境界はエントリからハイライトの逃げぶん離れ、最下段までスクロールしても保たれる | `RecipeTutorialClipTest` | 10 |
| `e2e/tests/recipe/slotTooltip.spec.ts`(2) | ツールチップはMantine既定の白い角丸ではなくCursorTooltipと同じ面を使う／祖先のスクロールでツールチップを引っ込め、ポインタを動かせば開き直す | `RecipeSlotTooltipTest` | 10 |

計: P19（9＋6＋2＋2）・C8（2＋3＋1＋2）・I1 = 28。写す 28（Enter の長押しは Task 6 の `CraftHoldKeyTest`。D-31）。recipe 全体の台帳は L24 と合わせて 52 で、R-15 の突き合わせは 52 を見る。自前のテスト（タグなし）: `CraftRecipeCatalogTest`・`RecipeViewerItemCatalogTest`・`RecipeCommandsTest`・`RecipeScreenModelTest`・`RecipeSelectionCloseResetTest`・`RecipeDurationTextTest`・`GameScrollViewKnobTest`。Presenter の結線（列へ載せる・画面で出し入れ）は Task 10 の EDC と録画で見る（前例 S2-hud の Presenter）。

## File Structure

| パス | 責務 | 種別 |
|---|---|---|
| `S/Client.Ui.State/Recipe/Catalog/CraftRecipeCatalog.cs` | マスタ＋解放状態 → 解放済み `CraftRecipeData` の列（`CraftingRecipesTopic` の写し） | 新規 |
| `S/Client.Ui.State/Recipe/Catalog/RecipeViewerItemCatalog.cs` | マスタ＋`ItemRecipeViewerDataContainer` → 一覧に出す `ItemId` の列（`RecipeViewerItemListTopic` の写し・未知型の警告 1 回） | 新規 |
| `S/Client.Ui.State/Recipe/Logic/RecipeDurationText.cs` | 秒数 → `ui.recipe.duration` の `UiText`（JS の数の文字化と同じ書式） | 新規 |
| `S/Client.Ui.State/Recipe/Screen/RecipeSelection.cs` | 選択の判別 union（`None`／`Of(ItemId)`） | 新規 |
| `S/Client.Ui.State/Recipe/Screen/RecipeItemSelection.cs` | 選択の持ち主（`Current`・`Select`・`Clear`） | 新規 |
| `S/Client.Ui.State/Recipe/Screen/ItemListViewModel.cs` | 一覧の VM（`ItemListViewModel`・`ItemListCell`） | 新規 |
| `S/Client.Ui.State/Recipe/Screen/RecipeViewerViewModel.cs` | ビューアの VM の判別 union（`RecipeViewerNoSelection`・`RecipeViewerItemSelected`） | 新規 |
| `S/Client.Ui.State/Recipe/Screen/RecipeEntryViewModel.cs` | エントリの VM（`CraftRecipeEntryViewModel`・`MachineRecipeEntryViewModel`・`RecipeMaterialViewModel`） | 新規 |
| `S/Client.Ui.State/Recipe/Screen/RecipeViewModelFactory.cs` | 純関数の組み立て（データ・持ち数・選択 → 2 つの VM） | 新規 |
| `S/Client.Ui.State/Recipe/Screen/RecipeScreenModel.cs` | 源を購読して同期に組み直し `ItemList`・`Viewer` を配る | 新規 |
| `S/Client.Ui.State/Recipe/Screen/{IRecipeScreenSignals.cs,GameRecipeScreenSignals.cs}` | 解放イベントと持ち物の変化の合図（本番は ClientContext と持ち物のコントローラ） | 新規 |
| `S/Client.Ui/Screens/Recipe/Input/{IUiCloseKeyProbe.cs,InputManagerCloseKeyProbe.cs,RecipeSelectionCloseReset.cs}` | 遷移と同じフレームの CloseUI で選択を外す（画面のキー処理なので State でなく画面の層。ref-uitk-design §10「画面のキー処理は Presenter か UIState」・前例 `PauseMenuScreenPresenter` の開閉に連動するリセット。D-22） | 新規 |
| `S/Client.Ui.State/Recipe/Commands/{IRecipeCommands.cs,RecipeCommands.cs,ICraftRequestSender.cs,VanillaApiCraftRequestSender.cs}` | クラフトの操作（検証して送る）とサーバーへの口 | 新規 |
| `S/Client.Ui.State/Commands/UiCommandRejectReason.cs` | 末尾 2 値（`CraftRecipeUnknown`・`CraftRecipeLocked`）と 3 switch | 変更（共有区画） |
| `Localization/localization.csv`・`W/src/shared/i18n/generated/localizationKeys.ts` | 拒否文 2 行と再生成 | 変更（共有区画） |
| `S/Client.Ui/Screens/Recipe/RecipeScreenPresenter.cs` | 2 つの View を列へ載せ、画面とモデルを購読 | 新規 |
| `S/Client.Ui/Screens/Recipe/RecipeViewerView.cs` | ビューアのパネル（未選択の案内・見出し・列） | 新規 |
| `S/Client.Ui/Screens/Recipe/ItemListView.cs` | 一覧のパネル（スクロール・グリッド・セル） | 新規 |
| `S/Client.Ui/Screens/Recipe/{RecipeViewer.uss,ItemList.uss}` | 画面の USS（つまみの上書き・配置） | 新規 |
| `S/Client.Ui/Screens/Recipe/Parts/{ItemHeaderView.cs,ItemHeaderRule.cs}` | 見出しの名前と装飾線（グラデの線と菱形を `generateVisualContent` で） | 新規 |
| `S/Client.Ui/Screens/Recipe/Parts/RecipeEntryList.cs` | エントリを GUID で使い回す列（`RecipeListScrollView` を包む） | 新規 |
| `S/Client.Ui/Screens/Recipe/Parts/CraftRecipeEntryView.cs` | クラフトの 1 件（`RecipeRow`・素材・結果・ボタン・アンカー） | 新規 |
| `S/Client.Ui/Screens/Recipe/Parts/{CraftHoldDriver.cs,CraftHoldInput.cs}` | 長押しの経過をパネルのスケジューラで進める／ボタンの押下・離し・外れ・取消を渡す | 新規 |
| `S/Client.Ui/Screens/Recipe/Parts/MachineRecipeEntryView.cs` | 機械の 1 件（液体・機械名・継承バッジ） | 新規 |
| `S/Client.Ui/Screens/Recipe/Parts/{ItemListCellView.cs,ItemListDragScroll.cs}` | 一覧の 1 セル（`ItemSlot`・アイテム ID・アンカー）／掴みスクロールとタップの選択 | 新規 |
| `S/Client.Ui/Components/Controls/GameScrollView/GameScrollView.uss` | 溝・つまみの色と縦バーの位置のつまみ 5 本（既定＝今の値） | 変更（宣言した部品の変更） |
| `S/Client.Starter/Registration/GameUi/RecipeGameUiRegistration.cs` | DI 登録（Model・Commands・Presenter 等） | 新規 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画に 1 行 | 変更（共有区画） |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | `Screens` 区画に `@import` 2 行 | 変更（共有区画） |
| `S/Client.Ui/Foundation/Theme/DerivedTokens.uss` | 末尾の名札付きブロック「S4-recipe」 | 変更（共有区画） |
| `tools/uitk/webTokens/handWrittenDerivedRows.mjs`・`docs/uitk/translucent-tokens.md` | 半透明の行と再生成 | 変更（共有区画） |
| `S/Client.Tests/Ui/State/Recipe/Catalog/{CraftRecipeCatalogTest.cs,RecipeViewerItemCatalogTest.cs}` | L0（テスト mod のマスタ） | 新規 |
| `S/Client.Tests/Ui/State/Recipe/Screen/{RecipeViewModelFactoryTest.cs,RecipeScreenModelTest.cs,RecipeCommandsTest.cs,RecipeDurationTextTest.cs,RecordingCraftRequestSender.cs}` | L0 と偽 | 新規 |
| `S/Client.Tests/Ui/Screens/Recipe/Input/{RecipeSelectionCloseResetTest.cs,FakeCloseKeyProbe.cs}` | Esc の選択解除の L0 と偽（前例 `ICursorTooltipScreen` と偽を同じ画面の層に置く） | 新規 |
| `S/Client.Tests/Ui/Screens/Recipe/{RecipeScreenFixtures.cs,RecordingRecipeCommands.cs}` | 画面 L1 の共通の材料 | 新規 |
| `S/Client.Tests/Ui/Screens/Recipe/Viewer/{RecipeViewerViewTest.cs,CraftRecipeEntryViewTest.cs,CraftHoldTest.cs,MachineRecipeEntryViewTest.cs}` | ビューアの L1 | 新規 |
| `S/Client.Tests/Ui/Screens/Recipe/ItemList/{ItemListViewTest.cs,ItemListScrollGeometryTest.cs,ItemListDragScrollTest.cs}` | 一覧の L1 | 新規 |
| `S/Client.Tests/Ui/Screens/Recipe/Overlay/{RecipeTutorialClipTest.cs,RecipeSlotTooltipTest.cs}` | チュートリアルの切り抜きとツールチップの L1 | 新規 |
| `S/Client.Tests/Ui/Components/Controls/GameScrollViewKnobTest.cs` | 追加つまみの効き（L1） | 新規 |
| `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` | 自分の 2 行を消す | 変更（共有区画） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-recipe-craft.cs` | 録画のシナリオ | 新規 |
| `docs/uitk/TODO.md`・`docs/uitk/disposition.md` | 実測の結論・LX-131〜135・追従記録（`disposition.md` は部分移植の行があるときだけ） | 変更（共有区画） |
| `LOGS/harness/uitk/S4-recipe/done.md` | 作業報告（repo 外） | 新規 |

ディレクトリのファイル数: `Recipe/Screen/` 9・`Screens/Recipe/Input/` 3・`Recipe/Commands/` 4・`Recipe/Catalog/` 2・`Recipe/Logic/` 5（既存 4＋1）・`Screens/Recipe/` 5＋`Parts/`・`Screens/Recipe/Parts/` 10・`Client.Tests/Ui/State/Recipe/Screen/` 5・`Client.Tests/Ui/Screens/Recipe/Input/` 2・`Client.Tests/Ui/Screens/Recipe/` 2＋3 サブディレクトリ。`Registration/GameUi/` は起草時点の実物 8 本＋このレーン 1 本。同じ W4 の S4-alpha・S4-research・S4-special なども 1 本ずつ足すので、取り込み時点の実数で数え直す（Task 1 Step 1・Task 15 Step 2 の関門）。ほかはどれも 10 以下。このレーンが書くのは上の表のファイルだけで、他の S4 レーンの持ち物（`Screens/BlockInventory/**`・`Screens/Inventory/**`・`Screens/Research/**`・`Client.Ui.State/Recipe/Source/**`）は書かない。

## Tasks

### Task 1: 前提の確認・作業場所・bd

**Files:**
- なし（確認だけ。repo 外の `LOGS/harness/uitk/S4-recipe/done.md` の雛形だけ作る）

**Interfaces:**
- Consumes: このファイルの Global Constraints の API 一覧
- Produces: 実物で読み替えた API 名（`done.md` の「未確認」）。以後の Task はこの読み替えに従う

- [ ] **Step 1: 依存の実物を確かめる**

```bash
pwd   # .../worktrees/moorestech/<s4-recipe の worktree> であること
git fetch origin && git log origin/feature/uitk --oneline -1
S=moorestech_client/Assets/Scripts
grep -rn "class RecipeRow\b\|class RecipeActionButton\b\|class RecipeListScrollView\b\|class LevelInheritanceBadge\b" $S/Client.Ui/Components   # C4-c（4 件）
grep -rn "class FluidAmountSlot\b\|class SlotAmountBadge\b" $S/Client.Ui/Components/Slots                                                # C4-a（2 件）
grep -rn "MarkClipContainer\|class TutorialAnchorMeasure\b\|ShowsLabel" $S/Client.Ui $S/Client.Ui.State                                 # S2-tutorial（3 件以上）
grep -n "RecipeCraftButton\|RecipeItemPrefix" $S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs                                  # 2 行
ls $S/Client.Ui.State/Recipe/Source/ && grep -rn "MachineRecipeData" $S/Client.Ui.State/Recipe/Source/                                     # S4-alpha
grep -n "Game.UnlockState" $S/Client.Ui.State/Client.Ui.State.asmdef                                                       # S4-alpha が足す asmdef 参照
grep -rn "public" $S/Client.Ui/Components/Recipe $S/Client.Ui/Components/Controls/RecipeAction* | grep -v "//"
grep -rn "public ItemRecipeViewerDataContainer(\|void UnlockCraftRecipe" $S/Client.Game ../moorestech_server/Assets/Scripts/Game.UnlockState 2>/dev/null
```

Expected: 全部が見つかる。どれかが無ければ実装に入らず、`done.md` に「状態: 依存待ち（<レーン>）」と書いて指揮へ報告して止める。名前・引数が Global Constraints と違えば以後を実物で読み替え、`done.md` の「未確認」に `API 名の読み替え: <旧> → <実物>` を 1 行ずつ書く。S4-alpha の機械レシピの源は PLAN-W4a の計画 `docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md` 末尾「## 後続レーンへの Produces 一覧」とも照らし、配り方が計画どおりの `Current : IReadOnlyReactiveProperty<IReadOnlyList<MachineRecipeData>>` でなければ Task 5 の購読の 1 行と `Rebuild` の読み 1 行をその形へ読み替える。


登録の本数の関門: `ls moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/*.cs | wc -l` を数え、このレーンの 1 本を足して 10 を超えるなら `done.md` の「integrator への依頼」に「`Registration/GameUi/` の分け方（サブディレクトリの切り方は integrator の持ち物）」を書き、`bd note` にも残して指揮へ報告する。作業は進めてよいが、再編が `origin/feature/uitk` に入るまでこのレーンはマージしない（S4-research・S4-special と同じ関門）。再編後は File Structure と Task 10 の登録のパスを読み替えて `done.md` に書く。あわせて W4 の他レーンが `GameUiInputRules.cs`・`GameScrollView.uss` を書いていないことを `git log origin/feature/uitk --oneline -- <2 本のパス>` と兄弟の計画の grep で確かめる（D-32）。

- [ ] **Step 2: 自前の Editor とコンパイルの疎通**

```bash
moores-wt status
uloop compile --project-path ./moorestech_client
```

Expected: 自分の worktree の Editor が 1 本、ErrorCount 0。

- [ ] **Step 3: bd と報告の雛形**

```bash
bd create "UITK S4-recipe: レシピビューアとアイテム一覧" --parent moorestech-albof -t task
bd update <id> --claim
mkdir -p ../moorestech_logs/harness/uitk/S4-recipe
printf '状態: 実装中\n\n## 未確認\n\n## integrator への依頼\n\n## 部分移植の残り\n' > ../moorestech_logs/harness/uitk/S4-recipe/done.md
```

### Task 2: State のカタログ（クラフトレシピ・一覧のアイテム）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Catalog/CraftRecipeCatalog.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Catalog/RecipeViewerItemCatalog.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Catalog/CraftRecipeCatalogTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Catalog/RecipeViewerItemCatalogTest.cs`

**Interfaces:**
- Consumes: `MasterHolder`、`IGameUnlockStateData`（`Game.UnlockState`）、`ItemRecipeViewerDataContainer`（`Client.Game.InGame.UI.Inventory.RecipeViewer`）
- Produces: `public static class CraftRecipeCatalog { public static IReadOnlyList<CraftRecipeData> BuildUnlocked(IGameUnlockStateData unlockState); }`、`public sealed class RecipeViewerItemCatalog { public RecipeViewerItemCatalog(ItemRecipeViewerDataContainer viewerData); public IReadOnlyList<ItemId> BuildVisibleItems(); }`（未知の型の警告を 1 回に抑えるため状態を持つ）

- [ ] **Step 1: テストを書く**

`CraftRecipeCatalogTest.cs`（マスタの読み込みは前例 `S/Client.Tests/Ui/State/ModeHud/ModeHudPlacementNameTest.cs` の SetUp と同じ）:

```csharp
using System.Linq;
using Client.Ui.State.Recipe.Catalog;
using Core.Master;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Recipe.Catalog
{
    /// <summary>
    /// 解放済みのクラフトレシピだけをマスタの順で写す（L0）
    /// Copies only unlocked craft recipes in master order (L0)
    /// </summary>
    public class CraftRecipeCatalogTest
    {
        private IGameUnlockStateDataController _unlock;

        [SetUp]
        public void SetUp()
        {
            // テスト mod のマスタとサーバーの解放状態を作る
            // Build the test-mod master and the server unlock state
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _unlock = provider.GetService<IGameUnlockStateDataController>();
        }

        [Test]
        public void 解放済みのレシピだけを結果と素材のIDへ写す()
        {
            var all = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes();
            var locked = all.First(r => !_unlock.CraftRecipeUnlockStateInfos[r.CraftRecipeGuid].IsUnlocked);
            _unlock.UnlockCraftRecipe(locked.CraftRecipeGuid);

            var result = CraftRecipeCatalog.BuildUnlocked(_unlock);

            var expected = all.Where(r => _unlock.CraftRecipeUnlockStateInfos[r.CraftRecipeGuid].IsUnlocked).ToList();
            Assert.AreEqual(expected.Count, result.Count);
            var copied = result.First(r => r.RecipeGuid == locked.CraftRecipeGuid);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(locked.CraftResultItemGuid), copied.ResultItemId);
            Assert.AreEqual(locked.CraftResultCount, copied.ResultCount);
            Assert.AreEqual(locked.CraftTime, copied.CraftTime);
            Assert.AreEqual(locked.RequiredItems.Length, copied.RequiredItems.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(locked.RequiredItems[0].ItemGuid), copied.RequiredItems[0].ItemId);
        }
    }
}
```

`RecipeViewerItemCatalogTest.cs`（同じ SetUp。`ItemRecipeViewerDataContainer` の ctor は Task 1 の grep の実物に合わせる）:

```csharp
[Test]
public void 表示判定がShowのアイテムだけをマスタの列挙順で返す()
{
    var viewer = new ItemRecipeViewerDataContainer(_unlock);
    var result = new RecipeViewerItemCatalog(viewer).BuildVisibleItems();

    var expected = MasterHolder.ItemMaster.GetItemAllIds().Where(id => viewer.EvaluateVisibility(id, MasterHolder.ItemMaster.GetItemMaster(id)) == RecipeViewerItemVisibility.Show).ToList();
    CollectionAssert.AreEqual(expected, result);
    Assert.IsNotEmpty(result);
}
```

- [ ] **Step 2: 実装を書く**

`CraftRecipeCatalog.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;
using Game.UnlockState;

namespace Client.Ui.State.Recipe.Catalog
{
    /// <summary>
    /// 解放済みのクラフトレシピを State のデータへ写す（Web CraftingRecipesTopic の源）
    /// Copies unlocked craft recipes into State data (source of the Web CraftingRecipesTopic)
    /// </summary>
    public static class CraftRecipeCatalog
    {
        public static IReadOnlyList<CraftRecipeData> BuildUnlocked(IGameUnlockStateData unlockState)
        {
            var infos = unlockState.CraftRecipeUnlockStateInfos;
            var result = new List<CraftRecipeData>();
            foreach (var recipe in MasterHolder.CraftRecipeMaster.GetAllCraftRecipes())
            {
                // silent-ok: 解放表に無いレシピは Web の topic と同じくロック扱い / 一覧に出ないことを CraftRecipeCatalogTest と録画 uitk-recipe-craft が見る
                if (!infos.TryGetValue(recipe.CraftRecipeGuid, out var info) || !info.IsUnlocked) continue;

                // 素材と結果を ItemId へ引き直す
                // Re-resolve materials and the result to ItemIds
                var required = new ItemAmount[recipe.RequiredItems.Length];
                for (var i = 0; i < required.Length; i++) required[i] = new ItemAmount(MasterHolder.ItemMaster.GetItemId(recipe.RequiredItems[i].ItemGuid), recipe.RequiredItems[i].Count);
                result.Add(new CraftRecipeData(recipe.CraftRecipeGuid, MasterHolder.ItemMaster.GetItemId(recipe.CraftResultItemGuid), recipe.CraftResultCount, recipe.CraftTime, required));
            }
            return result;
        }
    }
}
```

`RecipeViewerItemCatalog.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Core.Master;
using UnityEngine;

namespace Client.Ui.State.Recipe.Catalog
{
    /// <summary>
    /// 一覧に出すアイテムを決める（Web RecipeViewerItemListTopic の源）。未知の型は隠して警告を 1 回だけ出す
    /// Decides the items for the list (source of the Web RecipeViewerItemListTopic); unknown types are hidden with a single warning
    /// </summary>
    public sealed class RecipeViewerItemCatalog
    {
        private readonly ItemRecipeViewerDataContainer _viewerData;
        private bool _unknownTypeWarned;

        public RecipeViewerItemCatalog(ItemRecipeViewerDataContainer viewerData)
        {
            _viewerData = viewerData;
        }

        public IReadOnlyList<ItemId> BuildVisibleItems()
        {
            // マスタの列挙順を保って表示対象だけ詰める
            // Keep the master enumeration order and collect only visible items
            var result = new List<ItemId>();
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                var visibility = _viewerData.EvaluateVisibility(itemId, itemMaster);
                if (visibility == RecipeViewerItemVisibility.UnknownType) WarnUnknownTypeOnce(itemMaster.RecipeViewType);
                if (visibility == RecipeViewerItemVisibility.Show) result.Add(itemId);
            }
            return result;
        }

        private void WarnUnknownTypeOnce(string recipeViewType)
        {
            // 未知の型は隠す（fail-closed）。開発者が気づくよう最初の 1 回だけ警告する
            // Unknown types are hidden (fail-closed); warn once so developers notice
            if (_unknownTypeWarned) return;
            _unknownTypeWarned = true;
            Debug.LogWarning($"[RecipeViewerItemCatalog] 未知の RecipeViewType のアイテムを一覧から隠した: {recipeViewType}");
        }
    }
}
```

（`RecipeViewType` の型が string でなければ実物の型へ合わせる）

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.Recipe.Catalog"`
Expected: ErrorCount 0 / 2 件 PASS（`UnlockCraftRecipe` の名前が違えば `IGameUnlockStateDataController` の実物に合わせる）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Catalog moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Catalog
git commit -m "feat(uitk): レシピ画面の源（解放済みクラフトレシピ・一覧のアイテム）を State に写す（S4-recipe）"
```

### Task 3: 選択・ViewModel・組み立て

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Screen/{RecipeSelection.cs,RecipeItemSelection.cs,ItemListViewModel.cs,RecipeViewerViewModel.cs,RecipeEntryViewModel.cs,RecipeViewModelFactory.cs}`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Logic/RecipeDurationText.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Screen/{RecipeViewModelFactoryTest.cs,RecipeDurationTextTest.cs}`

**Interfaces:**
- Consumes: `CraftLogic`・`OwnedItemCounts`・`CraftRecipeData`・`MachineRecipeData`・`RecipeFixtures`（L0-s4a）
- Produces:
  - `public abstract class RecipeSelection { public static readonly RecipeSelection None; public static RecipeSelection Of(ItemId itemId); public abstract bool SameAs(RecipeSelection other); }`、`public sealed class NoRecipeSelection : RecipeSelection`、`public sealed class ItemRecipeSelection : RecipeSelection { public ItemId ItemId { get; } }`
  - `public sealed class RecipeItemSelection { public IReadOnlyReactiveProperty<RecipeSelection> Current { get; } public void Select(ItemId itemId); public void Clear(); }`
  - `public sealed class ItemListCell { public ItemListCell(ItemId itemId, int craftableCount); public ItemId ItemId { get; } public int CraftableCount { get; } }`、`public sealed class ItemListViewModel { public ItemListViewModel(IReadOnlyList<ItemListCell> cells); public IReadOnlyList<ItemListCell> Cells { get; } }`
  - `public abstract class RecipeViewerViewModel`、`public sealed class RecipeViewerNoSelection : RecipeViewerViewModel { public static readonly RecipeViewerNoSelection Instance; }`、`public sealed class RecipeViewerItemSelected : RecipeViewerViewModel { public RecipeViewerItemSelected(ItemId itemId, IReadOnlyList<RecipeEntryViewModel> entries); public ItemId ItemId { get; } public IReadOnlyList<RecipeEntryViewModel> Entries { get; } }`
  - `public abstract class RecipeEntryViewModel { public abstract Guid RecipeGuid { get; } }`、`public sealed class CraftRecipeEntryViewModel : RecipeEntryViewModel { public CraftRecipeEntryViewModel(CraftRecipeData recipe, IReadOnlyList<RecipeMaterialViewModel> materials, bool craftable, bool carriesTutorialAnchor); public CraftRecipeData Recipe { get; } public IReadOnlyList<RecipeMaterialViewModel> Materials { get; } public bool Craftable { get; } public bool CarriesTutorialAnchor { get; } }`、`public sealed class MachineRecipeEntryViewModel : RecipeEntryViewModel { public MachineRecipeEntryViewModel(MachineRecipeData recipe); public MachineRecipeData Recipe { get; } }`、`public sealed class RecipeMaterialViewModel { public RecipeMaterialViewModel(ItemId itemId, int required, int owned, bool lacking); public ItemId ItemId { get; } public int Required { get; } public int Owned { get; } public bool Lacking { get; } }`
  - `public static class RecipeViewModelFactory { public static ItemListViewModel BuildItemList(IReadOnlyList<ItemId> items, IReadOnlyList<CraftRecipeData> craftRecipes, IReadOnlyDictionary<ItemId, int> owned); public static RecipeViewerViewModel BuildViewer(RecipeSelection selection, IReadOnlyList<CraftRecipeData> craftRecipes, IReadOnlyList<MachineRecipeData> machineRecipes, IReadOnlyDictionary<ItemId, int> owned); }`
  - `public static class RecipeDurationText { public static UiText Create(double seconds); }`

- [ ] **Step 1: テストを書く**

`RecipeViewModelFactoryTest.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using NUnit.Framework;
using static Client.Tests.Ui.State.Recipe.RecipeFixtures;

namespace Client.Tests.Ui.State.Recipe.Screen
{
    /// <summary>
    /// 選択・データ・持ち数から 2 つの VM を組む（L0）
    /// Builds the two VMs from selection, data and owned counts (L0)
    /// </summary>
    public class RecipeViewModelFactoryTest
    {
        private static readonly Dictionary<ItemId, int> Owned = new() { { new ItemId(1), 3 } };

        /// <web-test file="src/features/recipe/views/RecipeContent.test.ts">全レシピをクラフト優先の単一リストへ並べる</web-test>
        [Test]
        public void 全レシピをクラフト優先の単一リストへ並べる()
        {
            var crafts = new[] { Craft(RecipeGuid(1), 7, 1, 2, Amount(1, 2)) };
            var machines = new[] { Machine(RecipeGuid(2), 7, 5, RecipeLevelInheritance.None) };

            var vm = (RecipeViewerItemSelected)RecipeViewModelFactory.BuildViewer(RecipeSelection.Of(new ItemId(7)), crafts, machines, Owned);

            Assert.AreEqual(2, vm.Entries.Count);
            var craft = (CraftRecipeEntryViewModel)vm.Entries[0];
            Assert.IsInstanceOf<MachineRecipeEntryViewModel>(vm.Entries[1]);
            Assert.IsTrue(craft.Craftable);
            Assert.AreEqual(3, craft.Materials[0].Owned);
            Assert.IsFalse(craft.Materials[0].Lacking);
        }

        /// <web-test file="src/features/recipe/views/RecipeContent.test.ts">チュートリアルアンカーを先頭のクラフトエントリ1件だけへ付ける</web-test>
        [Test]
        public void チュートリアルアンカーを先頭のクラフトエントリ1件だけへ付ける()
        {
            var crafts = new[] { Craft(RecipeGuid(1), 7, 1, 2, Amount(1, 9)), Craft(RecipeGuid(3), 7, 1, 0.4, Amount(1, 1)) };

            var vm = (RecipeViewerItemSelected)RecipeViewModelFactory.BuildViewer(RecipeSelection.Of(new ItemId(7)), crafts, new MachineRecipeData[0], Owned);

            Assert.IsTrue(((CraftRecipeEntryViewModel)vm.Entries[0]).CarriesTutorialAnchor);
            Assert.IsFalse(((CraftRecipeEntryViewModel)vm.Entries[1]).CarriesTutorialAnchor);
            Assert.IsTrue(((CraftRecipeEntryViewModel)vm.Entries[0]).Materials[0].Lacking);
        }

        [Test]
        public void 機械レシピだけならアンカーを付けず未選択は案内のVMになる()
        {
            var machines = new[] { Machine(RecipeGuid(2), 7, 5, RecipeLevelInheritance.None) };
            var vm = (RecipeViewerItemSelected)RecipeViewModelFactory.BuildViewer(RecipeSelection.Of(new ItemId(7)), new CraftRecipeData[0], machines, Owned);
            Assert.IsInstanceOf<MachineRecipeEntryViewModel>(vm.Entries[0]);
            Assert.AreSame(RecipeViewerNoSelection.Instance, RecipeViewModelFactory.BuildViewer(RecipeSelection.None, new CraftRecipeData[0], machines, Owned));
        }

        [Test]
        public void 一覧のセルはアイテムの順でクラフト可能数を持ち作れないものは0()
        {
            var crafts = new[] { Craft(RecipeGuid(1), 7, 2, 2, Amount(1, 1)) };
            var list = RecipeViewModelFactory.BuildItemList(Ids(7, 8), crafts, Owned);
            Assert.AreEqual(new ItemId(7), list.Cells[0].ItemId);
            Assert.AreEqual(6, list.Cells[0].CraftableCount);
            Assert.AreEqual(0, list.Cells[1].CraftableCount);
        }
    }
}
```

（`CraftableResultCounts` の数の意味は L0-s4a の `CraftLogicTest` が正。6〈3 回 × 結果 2〉にならなければ実物の意味に合わせて期待値を直し、理由を `done.md` に 1 行）

`RecipeDurationTextTest.cs`（SetUp/TearDown で言語を english に固定して戻す。手順は `ScreenTestHost` の SetUp/TearDown と同じ）:

```csharp
[TestCase(2d, "2 seconds")]
[TestCase(0.4d, "0.4 seconds")]
[TestCase(1.25d, "1.25 seconds")]
public void 秒数をJSの数の文字化と同じ書式でui_recipe_durationへ入れる(double seconds, string expected)
{
    Assert.AreEqual(expected, RecipeDurationText.Create(seconds).Resolve());
}
```

- [ ] **Step 2: 実装を書く**

`RecipeSelection.cs`:

```csharp
using Core.Master;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// レシピビューアの選択（未選択か 1 アイテム）。null を合図に使わない
    /// Recipe viewer selection (none or one item); never uses null as a signal
    /// </summary>
    public abstract class RecipeSelection
    {
        public static readonly RecipeSelection None = new NoRecipeSelection();

        public static RecipeSelection Of(ItemId itemId)
        {
            return new ItemRecipeSelection(itemId);
        }

        public abstract bool SameAs(RecipeSelection other);
    }

    public sealed class NoRecipeSelection : RecipeSelection
    {
        public override bool SameAs(RecipeSelection other)
        {
            return other is NoRecipeSelection;
        }
    }

    public sealed class ItemRecipeSelection : RecipeSelection
    {
        public ItemId ItemId { get; }

        public ItemRecipeSelection(ItemId itemId)
        {
            ItemId = itemId;
        }

        public override bool SameAs(RecipeSelection other)
        {
            return other is ItemRecipeSelection item && item.ItemId == ItemId;
        }
    }
}
```

`RecipeItemSelection.cs`:

```csharp
using Core.Master;
using UniRx;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// 選択の持ち主（Web selectionStore の写し）。同じ選択の再設定は通知しない
    /// Owner of the selection (port of the Web selectionStore); re-setting the same selection does not notify
    /// </summary>
    public sealed class RecipeItemSelection
    {
        private readonly ReactiveProperty<RecipeSelection> _current = new(RecipeSelection.None);
        public IReadOnlyReactiveProperty<RecipeSelection> Current => _current;

        public void Select(ItemId itemId)
        {
            Apply(RecipeSelection.Of(itemId));
        }

        public void Clear()
        {
            Apply(RecipeSelection.None);
        }

        private void Apply(RecipeSelection next)
        {
            // 判別 union は参照が毎回違うので値で比べて重複通知を避ける
            // Union values are new references each time, so compare by value to avoid duplicate notifications
            if (_current.Value.SameAs(next)) return;
            _current.Value = next;
        }
    }
}
```

`ItemListViewModel.cs`・`RecipeViewerViewModel.cs`・`RecipeEntryViewModel.cs` は Produces の形の不変クラス（ctor で全項目を受け、`{ get; }` だけ）。`CraftRecipeEntryViewModel.RecipeGuid` と `MachineRecipeEntryViewModel.RecipeGuid` は `Recipe.RecipeGuid` を返す override。

`RecipeViewModelFactory.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Logic;
using Core.Master;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// データ・持ち数・選択から一覧とビューアの VM を組む純関数（Web RecipeContent・ItemListPanel の計算の写し）
    /// Pure builders of the list and viewer VMs from data, owned counts and selection (port of the Web RecipeContent and ItemListPanel math)
    /// </summary>
    public static class RecipeViewModelFactory
    {
        public static ItemListViewModel BuildItemList(IReadOnlyList<ItemId> items, IReadOnlyList<CraftRecipeData> craftRecipes, IReadOnlyDictionary<ItemId, int> owned)
        {
            // 結果アイテムごとのクラフト可能数をセルへ（無ければ 0。0 はバッジ無し）
            // Craftable count per result item into each cell (0 when absent; 0 shows no badge)
            var counts = CraftLogic.CraftableResultCounts(craftRecipes, owned);
            var cells = new ItemListCell[items.Count];
            for (var i = 0; i < cells.Length; i++) cells[i] = new ItemListCell(items[i], counts.TryGetValue(items[i], out var count) ? count : 0);
            return new ItemListViewModel(cells);
        }

        public static RecipeViewerViewModel BuildViewer(RecipeSelection selection, IReadOnlyList<CraftRecipeData> craftRecipes, IReadOnlyList<MachineRecipeData> machineRecipes, IReadOnlyDictionary<ItemId, int> owned)
        {
            if (selection is not ItemRecipeSelection item) return RecipeViewerNoSelection.Instance;

            // クラフト優先の単一列。アンカーは先頭がクラフトのときその 1 件だけ
            // Craft-first single list; the anchor goes only on the head when it is a craft
            var entries = CraftLogic.BuildRecipeEntries(craftRecipes, machineRecipes, item.ItemId);
            var result = new RecipeEntryViewModel[entries.Count];
            for (var i = 0; i < result.Length; i++) result[i] = ToViewModel(entries[i], i == 0);
            return new RecipeViewerItemSelected(item.ItemId, result);

            #region Internal

            RecipeEntryViewModel ToViewModel(RecipeEntry entry, bool isHead)
            {
                return entry switch
                {
                    CraftRecipeEntry craft => new CraftRecipeEntryViewModel(craft.Recipe, BuildMaterials(craft.Recipe), CraftLogic.Craftable(craft.Recipe, owned), isHead),
                    MachineRecipeEntry machine => new MachineRecipeEntryViewModel(machine.Recipe),
                    _ => throw new ArgumentOutOfRangeException(nameof(entry), entry, "未知のレシピエントリ"),
                };
            }

            IReadOnlyList<RecipeMaterialViewModel> BuildMaterials(CraftRecipeData recipe)
            {
                // 素材ごとの所持・必要・不足（Web の lacking = owned < required）
                // Owned, required and lacking per material (Web lacking = owned < required)
                var materials = new RecipeMaterialViewModel[recipe.RequiredItems.Count];
                for (var i = 0; i < materials.Length; i++)
                {
                    var required = recipe.RequiredItems[i];
                    var ownedCount = owned.TryGetValue(required.ItemId, out var c) ? c : 0;
                    materials[i] = new RecipeMaterialViewModel(required.ItemId, required.Count, ownedCount, ownedCount < required.Count);
                }
                return materials;
            }

            #endregion
        }
    }
}
```

（Web の不足判定が `<` か `<=` かは `CraftRecipeEntry.tsx` の `lacking` の式を Task 3 の着手時に読み、違えば合わせる）

`RecipeDurationText.cs`:

```csharp
using System.Globalization;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Recipe.Logic
{
    /// <summary>
    /// 矢印の上の秒数（Web t(ui.recipe.duration, {seconds}) の写し。数は JS の既定の文字化と同じ最短表記）
    /// Seconds above the arrow (port of the Web t(ui.recipe.duration, {seconds}); the number uses the shortest round-trip form like JS)
    /// </summary>
    public static class RecipeDurationText
    {
        public static UiText Create(double seconds)
        {
            return new UiText(LocalizationKeys.Ui.Recipe.Duration, new[] { new LocalizationTextParam("seconds", seconds.ToString("R", CultureInfo.InvariantCulture)) });
        }
    }
}
```

（`LocalizationKeys`・`LocalizationTextParam` の名前空間は既存 `LevelInheritanceBadgeText.cs` の using に合わせる）

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.Recipe"`
Expected: ErrorCount 0 / 新規 7 件と L0-s4a の既存が PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Screen moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Logic/RecipeDurationText.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Screen
git commit -m "feat(uitk): レシピ画面の選択・ViewModel・組み立てを State に置く（S4-recipe）"
```

### Task 4: クラフトの操作と拒否の理由

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Commands/{IRecipeCommands.cs,RecipeCommands.cs,ICraftRequestSender.cs,VanillaApiCraftRequestSender.cs}`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs`（列挙の末尾 2 値と 3 switch の末尾）
- Modify: `Localization/localization.csv`（2 行）・`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（再生成）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Screen/{RecipeCommandsTest.cs,RecordingCraftRequestSender.cs}`

**Interfaces:**
- Consumes: `IGameUnlockStateData`
- Produces: `public interface IRecipeCommands { UiCommandResult Craft(Guid recipeGuid); }`、`public interface ICraftRequestSender { void Send(Guid recipeGuid); }`、`UiCommandRejectReason.CraftRecipeUnknown`（wire `invalid_recipe`）・`CraftRecipeLocked`（`recipe_locked`）。どちらも良性でない

- [ ] **Step 1: テストを書く**

`RecipeCommandsTest.cs`（SetUp は Task 2 と同じマスタの読み込み）:

```csharp
[Test]
public void 解放済みのレシピは送って受理する()
{
    var recipe = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes().First(r => _unlock.CraftRecipeUnlockStateInfos[r.CraftRecipeGuid].IsUnlocked);
    var sender = new RecordingCraftRequestSender();
    var result = new RecipeCommands(_unlock, sender).Craft(recipe.CraftRecipeGuid);
    Assert.IsInstanceOf<UiCommandAccepted>(result);
    CollectionAssert.AreEqual(new[] { recipe.CraftRecipeGuid }, sender.Sent);
}

[Test]
public void 未知のGUIDと未解放は送らずに良性でない拒否を返す()
{
    var locked = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes().First(r => !_unlock.CraftRecipeUnlockStateInfos[r.CraftRecipeGuid].IsUnlocked);
    var sender = new RecordingCraftRequestSender();
    var commands = new RecipeCommands(_unlock, sender);

    var unknown = (UiCommandRejected)commands.Craft(Guid.NewGuid());
    var lockedResult = (UiCommandRejected)commands.Craft(locked.CraftRecipeGuid);

    Assert.AreEqual(UiCommandRejectReason.CraftRecipeUnknown, unknown.Reason);
    Assert.AreEqual("invalid_recipe", UiCommandRejectReasons.ToWireCode(unknown.Reason));
    Assert.AreEqual(UiCommandRejectReason.CraftRecipeLocked, lockedResult.Reason);
    Assert.AreEqual("recipe_locked", UiCommandRejectReasons.ToWireCode(lockedResult.Reason));
    Assert.IsFalse(UiCommandRejectReasons.IsBenign(UiCommandRejectReason.CraftRecipeLocked));
    Assert.IsEmpty(sender.Sent);
}
```

（`UiCommandRejectReasons` は `UiCommandRejectReason.cs` の 3 switch を持つ静的クラスの実名に合わせる）

`RecordingCraftRequestSender.cs`: `public sealed class RecordingCraftRequestSender : ICraftRequestSender { public List<Guid> Sent { get; } = new(); public void Send(Guid recipeGuid) { Sent.Add(recipeGuid); } }`

- [ ] **Step 2: 実装を書く**

`RecipeCommands.cs`:

```csharp
using System;
using Client.Ui.State.Commands;
using Game.UnlockState;
using UnityEngine;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// クラフトの操作（Web CraftExecuteActionHandler の検証の写し）。通ればサーバーへ送る
    /// Craft operation (port of the Web CraftExecuteActionHandler checks); sends to the server when valid
    /// </summary>
    public sealed class RecipeCommands : IRecipeCommands
    {
        private readonly IGameUnlockStateData _unlockState;
        private readonly ICraftRequestSender _sender;

        public RecipeCommands(IGameUnlockStateData unlockState, ICraftRequestSender sender)
        {
            _unlockState = unlockState;
            _sender = sender;
        }

        public UiCommandResult Craft(Guid recipeGuid)
        {
            // 解放表に無い GUID・未解放は送らずに断る（UI からは起きない想定なのでトーストとログで気づかせる）
            // Refuse unknown or locked GUIDs without sending (should not come from the UI, so toast and log surface it)
            if (!_unlockState.CraftRecipeUnlockStateInfos.TryGetValue(recipeGuid, out var info)) return Reject(UiCommandRejectReason.CraftRecipeUnknown);
            if (!info.IsUnlocked) return Reject(UiCommandRejectReason.CraftRecipeLocked);

            _sender.Send(recipeGuid);
            return UiCommandAccepted.Instance;

            #region Internal

            UiCommandResult Reject(UiCommandRejectReason reason)
            {
                Debug.LogWarning($"[RecipeCommands] クラフトを断った reason={reason} recipe={recipeGuid}");
                return new UiCommandRejected(reason, recipeGuid.ToString());
            }

            #endregion
        }
    }
}
```

`VanillaApiCraftRequestSender.cs`:

```csharp
using System;
using Client.Game.InGame.Context;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// 本番のクラフト要求（ClientContext の送信専用 API）
    /// Production craft request (ClientContext send-only API)
    /// </summary>
    public sealed class VanillaApiCraftRequestSender : ICraftRequestSender
    {
        public void Send(Guid recipeGuid)
        {
            ClientContext.VanillaApi.SendOnly.Craft(recipeGuid);
        }
    }
}
```

`UiCommandRejectReason.cs`: 列挙の末尾に `CraftRecipeUnknown,`・`CraftRecipeLocked,`、`IsBenign` の switch に `UiCommandRejectReason.CraftRecipeUnknown => false,`・`UiCommandRejectReason.CraftRecipeLocked => false,`、`ToLocalizationKey` に `=> LocalizationKeys.Ui.CommandRejected.CraftRecipeUnknown`／`CraftRecipeLocked`、`ToWireCode` に `=> "invalid_recipe"`／`=> "recipe_locked"`（各 switch の `_ =>` の直前）。

`Localization/localization.csv`（`ui.commandRejected.splitDragNoValidSlots` の次。列はその行と同じ並び）:

```
ui.commandRejected.craftRecipeUnknown,This recipe does not exist,This recipe does not exist,このレシピは存在しません,Dieses Rezept existiert nicht,존재하지 않는 레시피입니다
ui.commandRejected.craftRecipeLocked,This recipe is not unlocked yet,This recipe is not unlocked yet,このレシピはまだ解放されていません,Dieses Rezept ist noch nicht freigeschaltet,아직 해금되지 않은 레시피입니다
```

```bash
(cd moorestech_web/webui && npm run gen:i18n)
```

台帳 `docs/uitk/TODO.md` の「## 追従記録」の末尾に「S4-recipe: localization.csv に ui.commandRejected.craftRecipeUnknown・craftRecipeLocked を足し localizationKeys.ts を再生成（Web は使わない）」を 1 行。

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RecipeCommandsTest|UiCommandReject|Localization"`
Expected: ErrorCount 0 / PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Commands moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts docs/uitk/TODO.md moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Screen/RecipeCommandsTest.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Screen/RecordingCraftRequestSender.cs
git commit -m "feat(uitk): クラフトの操作と拒否の理由 2 つを足す（S4-recipe）"
```

### Task 5: 画面のモデルと Esc での選択解除

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Recipe/Screen/{RecipeScreenModel.cs,IRecipeScreenSignals.cs,GameRecipeScreenSignals.cs}`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/Input/{IUiCloseKeyProbe.cs,InputManagerCloseKeyProbe.cs,RecipeSelectionCloseReset.cs}`（画面の層。`Client.Ui` は `Client.Input` を参照済み。D-22）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Screen/RecipeScreenModelTest.cs`・`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/Input/{RecipeSelectionCloseResetTest.cs,FakeCloseKeyProbe.cs}`

**Interfaces:**
- Consumes: Task 2〜3 の Produces、S4-alpha の `UnlockedMachineRecipeSource.Current`（S4-alpha 計画 14a85b146c の Produces 一覧。レビュー中で変わりうる。Task 1 で実物と照合）、`LocalPlayerInventoryController`（`LocalPlayerInventory.OnItemChange`・`OnInventoryRefreshed`。前例 `ResearchNodeStateStore`）、`IPlayerInventoryOperations.ReadSnapshot()`、`Localize.OnLanguageChanged`（前例 `ModeHudModel`）、`UiRoutingState.Current`
- Produces:
  - `public interface IRecipeScreenSignals { IObservable<Unit> OnRecipesUnlocked { get; } IObservable<Unit> OnInventoryChanged { get; } }`、本番 `GameRecipeScreenSignals(IVanillaApiEvent vanillaApiEvent, LocalPlayerInventoryController inventoryController) : IInitializable`（解放イベントは `Initialize` で購読。前例 `ResearchNodeStateStore`・`NotificationFeed`）
  - `public sealed class RecipeScreenModel { public RecipeScreenModel(IGameUnlockStateData unlockState, RecipeViewerItemCatalog itemCatalog, UnlockedMachineRecipeSource machineRecipes, IRecipeScreenSignals signals, IPlayerInventoryOperations inventory, RecipeItemSelection selection); public IReadOnlyReactiveProperty<ItemListViewModel> ItemList { get; } public IReadOnlyReactiveProperty<RecipeViewerViewModel> Viewer { get; } }`
  - `public interface IUiCloseKeyProbe { bool WasPressedThisFrame(); }`、`public sealed class RecipeSelectionCloseReset : IInitializable, IDisposable { public RecipeSelectionCloseReset(IReadOnlyReactiveProperty<UiRoutingSnapshot> routing, IUiCloseKeyProbe closeKey, RecipeItemSelection selection); }`（登録で `UiRoutingState.Current` を渡す）

- [ ] **Step 1: テストを書く**

`RecipeScreenModelTest.cs`（Task 2 と同じマスタの SetUp。持ち物は既存の偽 `S/Client.Tests/Ui/State/Inventory/RecordingPlayerInventoryOperations.cs` の主枠を書き換える〈書き換えの口が無ければこのテストの中に `IPlayerInventoryOperations` の最小の偽を置く〉。合図は `Subject<Unit>` 2 本を持つ偽 `IRecipeScreenSignals`。機械の源は S4-alpha の実物の作り方で空の列にする）:

```csharp
[Test]
public void 持ち物の合図で一覧の数とビューアの可否が組み直される()
{
    var recipe = FirstUnlockedRecipe();
    _selection.Select(recipe.ResultItemId);
    Assert.IsFalse(HeadCraft().Craftable);

    _inventory.SetMainSlots(EnoughFor(recipe, 2));
    _signals.InventoryChanged.OnNext(Unit.Default);

    Assert.IsTrue(HeadCraft().Craftable);
    Assert.AreEqual(2 * recipe.ResultCount, CellOf(recipe.ResultItemId).CraftableCount);
}

[Test]
public void 解放の合図で新しいレシピがビューアに入る()
{
    var locked = FirstLockedRecipe();
    _selection.Select(locked.ResultItemId);
    var before = EntryGuids();
    _unlock.UnlockCraftRecipe(locked.RecipeGuid);
    _signals.RecipesUnlocked.OnNext(Unit.Default);
    CollectionAssert.DoesNotContain(before, locked.RecipeGuid);
    CollectionAssert.Contains(EntryGuids(), locked.RecipeGuid);
}

[Test]
public void 同じアイテムを選び直してもビューアを配り直さない()
{
    var count = 0;
    _model.Viewer.Skip(1).Subscribe(_ => count++);
    _selection.Select(new ItemId(1));
    _selection.Select(new ItemId(1));
    Assert.AreEqual(1, count);
}
```

`RecipeSelectionCloseResetTest.cs`:

```csharp
[Test]
public void 画面が変わったフレームにCloseUIが押されていれば選択を外す()
{
    _selection.Select(new ItemId(3));
    _probe.SetPressed(true);
    _routing.Value = new UiRoutingSnapshot(UiScreen.None);
    Assert.IsInstanceOf<NoRecipeSelection>(_selection.Current.Value);
}

[Test]
public void CloseUIなしで閉じたときは選択を残す()
{
    _selection.Select(new ItemId(3));
    _probe.SetPressed(false);
    _routing.Value = new UiRoutingSnapshot(UiScreen.None);
    Assert.IsInstanceOf<ItemRecipeSelection>(_selection.Current.Value);
}
```

（`_routing` は `new ReactiveProperty<UiRoutingSnapshot>(new UiRoutingSnapshot(UiScreen.PlayerInventory))`、SetUp で `Initialize()` を呼ぶ）

- [ ] **Step 2: 実装を書く**

`RecipeScreenModel.cs`:

```csharp
using Client.Localization;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Catalog;
using Client.Ui.State.Recipe.Source;
using Game.UnlockState;
using UniRx;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// レシピ画面の源を購読し、変化ごとに一覧とビューアの VM を同期に組み直す（A-57）
    /// Subscribes to the recipe screen sources and rebuilds the list and viewer VMs synchronously on each change (A-57)
    /// </summary>
    public sealed class RecipeScreenModel
    {
        private readonly IGameUnlockStateData _unlockState;
        private readonly RecipeViewerItemCatalog _itemCatalog;
        private readonly UnlockedMachineRecipeSource _machineRecipes;
        private readonly IPlayerInventoryOperations _inventory;
        private readonly RecipeItemSelection _selection;
        private readonly ReactiveProperty<ItemListViewModel> _itemList = new();
        private readonly ReactiveProperty<RecipeViewerViewModel> _viewer = new(RecipeViewerNoSelection.Instance);

        public IReadOnlyReactiveProperty<ItemListViewModel> ItemList => _itemList;
        public IReadOnlyReactiveProperty<RecipeViewerViewModel> Viewer => _viewer;

        public RecipeScreenModel(IGameUnlockStateData unlockState, RecipeViewerItemCatalog itemCatalog, UnlockedMachineRecipeSource machineRecipes, IRecipeScreenSignals signals, IPlayerInventoryOperations inventory, RecipeItemSelection selection)
        {
            _unlockState = unlockState;
            _itemCatalog = itemCatalog;
            _machineRecipes = machineRecipes;
            _inventory = inventory;
            _selection = selection;

            // 源の変化ごとに組み直す。言語は素材の名前と秒数を View が描き直すため
            // Rebuild on every source change; language too, so the View redraws material names and seconds
            signals.OnRecipesUnlocked.Subscribe(_ => Rebuild());
            signals.OnInventoryChanged.Subscribe(_ => Rebuild());
            machineRecipes.Current.Skip(1).Subscribe(_ => Rebuild());
            selection.Current.Skip(1).Subscribe(_ => Rebuild());
            Localize.OnLanguageChanged.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            // 持ち数はメインの枠だけで数える（Web の inventory.mainSlots）
            // Owned counts use only the main slots (the Web inventory.mainSlots)
            var owned = OwnedItemCounts.Build(_inventory.ReadSnapshot().MainSlots);
            var crafts = CraftRecipeCatalog.BuildUnlocked(_unlockState);
            _itemList.Value = RecipeViewModelFactory.BuildItemList(_itemCatalog.BuildVisibleItems(), crafts, owned);
            _viewer.Value = RecipeViewModelFactory.BuildViewer(_selection.Current.Value, crafts, _machineRecipes.Current.Value, owned);
        }
    }
}
```

（`ReactiveProperty` は同じ参照なら通知しないが VM は毎回新しい参照なので必ず流れる。View 側は GUID で要素を使い回すので描き直しのたびに作り直さない〈Task 8 `RecipeEntryList`〉。同じ選択で配り直さないテストは `RecipeItemSelection` が重複を止めることで満たす）

`GameRecipeScreenSignals.cs`:

```csharp
using System;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Network.API;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// 本番の合図（解放イベント・持ち物の変化）。前例 ResearchNodeStateStore・CraftingRecipesTopic と同じ源
    /// Production signals (unlock event, inventory change); the same sources as ResearchNodeStateStore and CraftingRecipesTopic
    /// </summary>
    public sealed class GameRecipeScreenSignals : IRecipeScreenSignals, IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly Subject<Unit> _recipesUnlocked = new();
        public IObservable<Unit> OnRecipesUnlocked => _recipesUnlocked;
        public IObservable<Unit> OnInventoryChanged { get; }

        public GameRecipeScreenSignals(IVanillaApiEvent vanillaApiEvent, LocalPlayerInventoryController inventoryController)
        {
            _vanillaApiEvent = vanillaApiEvent;
            OnInventoryChanged = inventoryController.LocalPlayerInventory.OnItemChange.AsUnitObservable().Merge(inventoryController.OnInventoryRefreshed.AsUnitObservable());
        }

        public void Initialize()
        {
            // 解放イベントは Initialize で購読する。VContainer は全 IInitializable を作ってから Initialize を回すので、ClientGameUnlockStateData の ctor の購読が先に走り更新後の表を読める（前例 ResearchNodeStateStore・NotificationFeed）
            // Subscribe to unlock events in Initialize; VContainer builds every IInitializable first, so ClientGameUnlockStateData's ctor subscription runs earlier and the table is updated (precedent ResearchNodeStateStore, NotificationFeed)
            _vanillaApiEvent.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => _recipesUnlocked.OnNext(Unit.Default));
        }
    }
}
```

`RecipeSelectionCloseReset.cs`:

```csharp
using System;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Recipe.Input
{
    /// <summary>
    /// Esc（CloseUI）で画面が変わったら選択を外す（Web RecipeSelectionKeyHandler のゲーム層の Escape の写し）
    /// Clears the selection when Esc (CloseUI) changes the screen (port of the Web RecipeSelectionKeyHandler game-layer Escape)
    /// </summary>
    public sealed class RecipeSelectionCloseReset : IInitializable, IDisposable
    {
        private readonly IReadOnlyReactiveProperty<UiRoutingSnapshot> _routing;
        private readonly IUiCloseKeyProbe _closeKey;
        private readonly RecipeItemSelection _selection;
        private IDisposable _subscription;

        public RecipeSelectionCloseReset(IReadOnlyReactiveProperty<UiRoutingSnapshot> routing, IUiCloseKeyProbe closeKey, RecipeItemSelection selection)
        {
            _routing = routing;
            _closeKey = closeKey;
            _selection = selection;
        }

        public void Initialize()
        {
            // UITK のキーイベントは届かないので、遷移と同じフレームの CloseUI で読む（A-83 と同じく InputManager を正にする）
            // UITK key events never arrive, so read CloseUI in the same frame as the transition (InputManager is authoritative, as in A-83)
            _subscription = _routing.Skip(1).Where(_ => _closeKey.WasPressedThisFrame()).Subscribe(_ => _selection.Clear());
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
```

`InputManagerCloseKeyProbe.cs`: `public bool WasPressedThisFrame() { return InputManager.UI.CloseUI.GetKeyDown; }`（`GetKeyDown` はプロパティ。`Client.Input/InputKey.cs`）（`Client.Input`）。`FakeCloseKeyProbe.cs`: `SetPressed(bool)` の値を返す。

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.State.Recipe|Client.Tests.Ui.Screens.Recipe.Input"`
Expected: ErrorCount 0 / PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Recipe moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/Input moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Recipe/Screen moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/Input
git commit -m "feat(uitk): レシピ画面のモデルと Esc での選択解除を足す（S4-recipe）"
```

### Task 6: クラフトのエントリと長押しクラフト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/Parts/{CraftRecipeEntryView.cs,CraftHoldDriver.cs,CraftHoldInput.cs}`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/{RecipeScreenFixtures.cs,RecordingRecipeCommands.cs}`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/Viewer/{CraftRecipeEntryViewTest.cs,CraftHoldTest.cs,CraftHoldKeyTest.cs}`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/UiKeyFocusKeepMark.cs`（フォーカスを保ってよい印。D-32・E-19 の前例でこのレーンが持つ）
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs`（`OnFocusIn`・`BlurNonTextFieldFocus` の対象から印付きを外す）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiInputRulesTest.cs`（印付きの要素の L1 を 1 件足す。既存の `ButtonFocusIsBlurredWithWarning` は印の無いボタンのまま残す）

**Interfaces:**
- Consumes: C4-c の `RecipeRow`・`RecipeActionButton`、C3-a の `ItemSlot`、Task 3〜4 の `CraftRecipeEntryViewModel`・`RecipeItemSelection`・`IRecipeCommands`・`RecipeDurationText`、`MaterialTooltipText`、`GameContentPresentation`、`UiCommandResultToaster`
- Produces:
  - `public sealed class CraftRecipeEntryView : VisualElement { public CraftRecipeEntryView(CraftRecipeEntryViewModel first, RecipeEntryPartsContext context); public void Render(CraftRecipeEntryViewModel vm); }`（name `craft-recipe-entry-<guid>`。アンカーは ctor の `first.CarriesTutorialAnchor` で 1 回だけ付ける）
  - `public sealed class RecipeEntryPartsContext { public RecipeEntryPartsContext(GameContentPresentation content, IRecipeCommands commands, UiCommandResultToaster toaster, RecipeItemSelection selection); public GameContentPresentation Content { get; } public IRecipeCommands Commands { get; } public UiCommandResultToaster Toaster { get; } public RecipeItemSelection Selection { get; } }`（エントリの包みが共通で受ける依存の束。`Parts/` 内の `CraftRecipeEntryView.cs` の下に置く）
  - `public sealed class CraftHoldDriver { public CraftHoldDriver(VisualElement scheduleOwner, RecipeRow row, IRecipeCommands commands, UiCommandResultToaster toaster); public void SetRecipe(Guid recipeGuid, double craftTime, bool craftable); public void Start(); public void Stop(); }`
  - `public sealed class CraftHoldInput { public CraftHoldInput(VisualElement button, CraftHoldDriver driver); }`（ポインタの押下・離し・外れ・取消と、フォーカス中の Enter の押下・離し・フォーカス外れを長押しへ渡す。`focusable=true` と印は置く側〈`CraftRecipeEntryView`〉がインスタンスに立てる）
  - `namespace Client.Ui.Foundation.Input { public static class UiKeyFocusKeepMark { public static void Mark(VisualElement element); public static bool IsMarked(VisualElement element); } }`（クラス `ui-key-focus-keep` で印を付ける。`GameUiInputRules` は印付きの要素からフォーカスを外さず警告も出さない）

- [ ] **Step 1: テストを書く**

`RecipeScreenFixtures.cs`（画面 L1 の共通の材料。`FakeContentTextureSource`〈既存 `S/Client.Tests/Ui/Content/`〉で `GameContentPresentation` を作り、`RecipeFixtures` の GUID・素材で VM を作る）:

```csharp
public static class RecipeScreenFixtures
{
    public static RecipeEntryPartsContext Context(UiCommandResultToaster toaster, RecordingRecipeCommands commands, RecipeItemSelection selection) { /* new GameContentPresentation(new FakeContentTextureSource()) を使う */ }
    public static CraftRecipeEntryViewModel CraftVm(Guid guid, double craftTime, bool craftable, bool anchor, params RecipeMaterialViewModel[] materials) { /* Craft(guid, 7, 1, craftTime, …) */ }
    public static RecipeMaterialViewModel Material(int itemId, int required, int owned) { return new RecipeMaterialViewModel(new ItemId(itemId), required, owned, owned < required); }
}
```

`RecordingRecipeCommands.cs`: `IRecipeCommands` の偽。`Crafted : List<Guid>`、`SetResult(UiCommandResult)` で返す値を変える。

`CraftRecipeEntryViewTest.cs`（`ScreenTestHost` を継ぐ。言語は english に固定済み）:

```csharp
/// <web-test file="src/features/recipe/views/CraftRecipeEntry.test.ts">矢印の上の秒数をui.recipe.durationの書式で組み、ボタンは秒数を持たない</web-test>
/// <web-test file="e2e/tests/recipe/recipe.spec.ts">秒数は矢印の上に出し、クラフトボタンは秒数を持たない</web-test>
[Test]
public void 秒数は矢印の上に出しボタンは秒数を持たない()
{
    var guid = RecipeFixtures.RecipeGuid(1);
    var view = Mount(new CraftRecipeEntryView(CraftVm(guid, 2, true, false, Material(1, 1, 3)), Context(Toaster, _commands, _selection)));

    var duration = view.Q<Label>($"craft-recipe-box-{guid}-duration");
    var arrow = view.Q($"craft-progress-arrow-{guid}");
    var button = view.Q<RecipeActionButton>($"craft-button-{guid}");
    Assert.AreEqual("2 seconds", duration.text);
    Assert.LessOrEqual(duration.worldBound.yMax, arrow.worldBound.yMin + 0.5f);
    StringAssert.DoesNotContain("second", button.text);
}

/// <web-test file="src/features/recipe/views/CraftRecipeEntry.test.ts">言語ごとの秒数書式を矢印上の表示へ反映する</web-test>
[Test]
public void 言語を変えると秒数の書式が変わる()
{
    var guid = RecipeFixtures.RecipeGuid(1);
    var view = Mount(new CraftRecipeEntryView(CraftVm(guid, 0.4, true, false, Material(1, 1, 3)), Context(Toaster, _commands, _selection)));
    Localize.TrySetChosenLanguage("japanese");
    simulate.FrameUpdate();
    Assert.AreEqual("0.4秒", view.Q<Label>($"craft-recipe-box-{guid}-duration").text);
}

[Test]
public void 不足の素材はinsufficientで押すとその素材を選び不可ならボタンが無効()
{
    var guid = RecipeFixtures.RecipeGuid(1);
    var view = Mount(new CraftRecipeEntryView(CraftVm(guid, 2, false, false, Material(1, 5, 3)), Context(Toaster, _commands, _selection)));
    var slot = view.Q<ItemSlot>();
    Assert.IsTrue(slot.Q(className: "slot-frame--insufficient") != null);
    Assert.IsFalse(view.Q<RecipeActionButton>().enabledSelf);
    PressLeft(slot);                                                     // UiTestHost の左押下（前例 C3-a の ItemSlotTest）
    Assert.AreEqual(new ItemId(1), ((ItemRecipeSelection)_selection.Current.Value).ItemId);
}
```

`CraftHoldTest.cs`:

```csharp
/// <web-test file="e2e/tests/recipe/recipe.spec.ts">アイテム選択でレシピ表示、長押しで素材が尽きるまで連続クラフトする</web-test>
[Test]
public void 長押しで到達ごとにクラフトし可否が偽になったら止まる()
{
    var guid = RecipeFixtures.RecipeGuid(1);
    var view = Mount(new CraftRecipeEntryView(CraftVm(guid, 0.4, true, false, Material(1, 1, 2)), Context(Toaster, _commands, _selection)));
    var button = view.Q<RecipeActionButton>();

    PointerDown(button);
    for (var i = 0; i < 6; i++) simulate.FrameUpdateMs(100);           // 0.6 秒 → 1 回目（0.4）
    Assert.AreEqual(1, _commands.Crafted.Count);
    for (var i = 0; i < 4; i++) simulate.FrameUpdateMs(100);           // 1.0 秒 → 2 回目（0.8）
    CollectionAssert.AreEqual(new[] { guid, guid }, _commands.Crafted);

    view.Render(CraftVm(guid, 0.4, false, false, Material(1, 1, 0)));  // 素材が尽きた
    for (var i = 0; i < 10; i++) simulate.FrameUpdateMs(100);
    Assert.AreEqual(2, _commands.Crafted.Count);
    Assert.AreEqual(0f, ArrowProgress(view, guid));
}

/// <web-test file="e2e/tests/recipe/recipe.spec.ts">押下後にボタンから外れるとクラフトが止まり経過時間がリセットされる</web-test>
[Test]
public void 押下後にボタンの外へ出ると止まり経過が0に戻る()
{
    var guid = RecipeFixtures.RecipeGuid(1);
    var view = Mount(new CraftRecipeEntryView(CraftVm(guid, 1, true, false, Material(1, 1, 9)), Context(Toaster, _commands, _selection)));
    var button = view.Q<RecipeActionButton>();

    PointerDown(button);
    for (var i = 0; i < 5; i++) simulate.FrameUpdateMs(100);
    Assert.Greater(ArrowProgress(view, guid), 0.3f);
    PointerMoveTo(button.worldBound.center + new Vector2(0, button.worldBound.height));   // ボタンの外
    Assert.AreEqual(0f, ArrowProgress(view, guid));
    for (var i = 0; i < 20; i++) simulate.FrameUpdateMs(100);
    Assert.IsEmpty(_commands.Crafted);
}

[Test]
public void 拒否はトーストへ渡り1フレームの経過は上限で頭打ち()
{
    _commands.SetResult(new UiCommandRejected(UiCommandRejectReason.CraftRecipeLocked, "x"));
    var view = Mount(new CraftRecipeEntryView(CraftVm(RecipeFixtures.RecipeGuid(1), 0.3, true, false, Material(1, 1, 9)), Context(Toaster, _commands, _selection)));
    PointerDown(view.Q<RecipeActionButton>());
    simulate.FrameUpdateMs(1000);                                         // 1 フレームで 1 秒 → 0.25 秒だけ進む
    Assert.IsEmpty(_commands.Crafted);
    simulate.FrameUpdateMs(100);                                          // 0.35 → 1 回
    Assert.AreEqual(1, _commands.Crafted.Count);
    Assert.AreEqual(1, ShownToasts.Count);
}
```

`CraftHoldKeyTest.cs`（`ScreenTestHost` を継ぐが、`ScreenTestHost` は入力規則を付けないので、`GameUiInputRulesTest`・`IconButtonTest` と同じくテストごとの器 `_rulesRoot` に `GameUiInputRules.Attach` を付け、エントリはその器へ載せる。キーは `button.SendEvent(KeyDownEvent.GetPooled(…))`／`KeyUpEvent` で、Tab は `NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next)` を `_rulesRoot` へ送る）:

```csharp
/// <web-test file="src/features/recipe/views/CraftRecipeEntry.test.ts">SpaceではクラフトボタンのonKeyDownが何もせずEnterでは長押しを始める</web-test>
[Test]
public void クリックで乗ったフォーカスが規則の下でも残りSpaceでは何もせずEnterを保持する間クラフトが続く()
{
    var guid = RecipeFixtures.RecipeGuid(1);
    var view = MountInRules(new CraftRecipeEntryView(CraftVm(guid, 0.4, true, false, Material(1, 1, 9)), Context(Toaster, _commands, _selection)));
    var button = view.Q<RecipeActionButton>();

    // クリックでフォーカスが乗り（Web のボタンもクリックで乗る）、規則の次のスケジュールでも外れない
    // A click focuses the button (as in Web) and the rules' next scheduled pass does not blur it
    simulate.Click(button.worldBound.center);
    for (var i = 0; i < 3; i++) simulate.FrameUpdate();
    Assert.AreSame(button, panel.focusController.focusedElement);

    SendKeyDown(button, KeyCode.Space);
    for (var i = 0; i < 6; i++) simulate.FrameUpdateMs(100);
    SendKeyUp(button, KeyCode.Space);
    Assert.IsEmpty(_commands.Crafted);

    SendKeyDown(button, KeyCode.Return);
    SendKeyDown(button, KeyCode.Return);                                  // キーリピートで始め直さない
    for (var i = 0; i < 10; i++) simulate.FrameUpdateMs(100);             // 1.0 秒保持 → 2 回（0.4・0.8）
    CollectionAssert.AreEqual(new[] { guid, guid }, _commands.Crafted);
    Assert.AreSame(button, panel.focusController.focusedElement);
    SendKeyUp(button, KeyCode.Return);
    for (var i = 0; i < 10; i++) simulate.FrameUpdateMs(100);
    Assert.AreEqual(2, _commands.Crafted.Count);
}

[Test]
public void Tabのナビゲーションは打ち消されフォーカスは動かず長押しも切れない()
{
    var guid = RecipeFixtures.RecipeGuid(1);
    var view = MountInRules(new CraftRecipeEntryView(CraftVm(guid, 0.4, true, false, Material(1, 1, 9)), Context(Toaster, _commands, _selection)));
    var button = view.Q<RecipeActionButton>();
    simulate.Click(button.worldBound.center);
    SendKeyDown(button, KeyCode.Return);
    using (var tab = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next)) _rulesRoot.SendEvent(tab);
    for (var i = 0; i < 6; i++) simulate.FrameUpdateMs(100);
    Assert.AreSame(button, panel.focusController.focusedElement);        // ゲームの Tab（インベントリの開閉）は InputManager が受ける。UITK のフォーカス移動はしない
    Assert.AreEqual(1, _commands.Crafted.Count);
}

[Test]
public void Enterで長押し中にフォーカスが外れると止まる()
{
    var view = MountInRules(new CraftRecipeEntryView(CraftVm(RecipeFixtures.RecipeGuid(1), 1, true, false, Material(1, 1, 9)), Context(Toaster, _commands, _selection)));
    var button = view.Q<RecipeActionButton>();
    simulate.Click(button.worldBound.center);
    SendKeyDown(button, KeyCode.Return);
    for (var i = 0; i < 5; i++) simulate.FrameUpdateMs(100);
    simulate.Click(new Vector2(1200, 700));                               // 空き領域のクリックでフォーカスが外れる
    for (var i = 0; i < 20; i++) simulate.FrameUpdateMs(100);
    Assert.IsEmpty(_commands.Crafted);
}
```

`GameUiInputRulesTest.cs` に足す 1 件（既存の SetUp の `_rulesRoot` をそのまま使う）:

```csharp
[Test]
public void KeepMarkedButtonFocusIsKeptWithoutWarning()
{
    var button = new Button { name = "rules-keep-button", focusable = true };
    UiKeyFocusKeepMark.Mark(button);
    _rulesRoot.Add(button);

    button.Focus();
    for (var i = 0; i < 3; i++) simulate.FrameUpdate();

    // 印付きは規則の次のスケジュールでも外れず、警告も出ない。入力欄ではないので入力中の旗は立たない
    // A marked element survives the rules' scheduled blur without a warning, and raises no text-input flag
    Assert.AreSame(button, panel.focusController.focusedElement);
    Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
    LogAssert.NoUnexpectedReceived();
}
```

`UiKeyFocusKeepMark.cs`:

```csharp
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// 入力欄でなくてもフォーカスを保ってよい要素の印（キーで操作するボタンだけに付ける。GameUiInputRules が外さない）
    /// Mark for non-text-field elements allowed to keep focus (key-operated buttons only; GameUiInputRules leaves them focused)
    /// </summary>
    public static class UiKeyFocusKeepMark
    {
        private const string ClassName = "ui-key-focus-keep";

        public static void Mark(VisualElement element)
        {
            element.AddToClassList(ClassName);
        }

        public static bool IsMarked(VisualElement element)
        {
            return element != null && element.ClassListContains(ClassName);
        }
    }
}
```

`GameUiInputRules.cs` の変更（2 か所。ほかは今のまま）:

```csharp
        private static void OnFocusIn(FocusInEvent evt)
        {
            var element = evt.target as VisualElement;
            if (IsInsideTextField(element))
            {
                UiInputExclusivity.SetGameUiTextInputFocused(true);
                return;
            }

            // 印付き（キーで操作するボタン）はフォーカスを保つ。入力欄ではないので入力中の旗は立てない
            // Marked elements (key-operated buttons) keep focus; they are not text fields, so the text-input flag stays down
            if (UiKeyFocusKeepMark.IsMarked(element)) return;

            // （以下は今のまま: 警告と次のスケジュールでの Blur）
        }

        private static void BlurNonTextFieldFocus(VisualElement root)
        {
            // silent-ok: 外す前にフォーカスが入力欄・印付き・無しへ移っていれば外すものが無い / 入力欄以外へのフォーカスは OnFocusIn の LogWarning で既に記録済み
            // silent-ok: if focus already moved to a text field, a marked element or nowhere there is nothing to blur / the non-text-field focus was already logged by OnFocusIn
            if (root.focusController?.focusedElement is not VisualElement focused || IsInsideTextField(focused) || UiKeyFocusKeepMark.IsMarked(focused)) return;
            focused.Blur();
        }
```

（`PointerDown`・`PointerMoveTo`・`ArrowProgress` はテスト内のローカル helper。`ArrowProgress` は C4-c の `RecipeRow` の矢印の進み〈Task 1 で実物の読み口を確かめる。無ければ `ProgressArrowBar` の fill の幅の比〉。`simulate.FrameUpdateMs` が `schedule.Execute().Every(0)` の `TimerState.deltaTime` を進めることは C3-b の `UiPressGesture` の L1 と同じ前提。進まなければ Task 6 の中で `simulate.FrameUpdate()` を挟む形へ直し、`done.md` に実測を書く）

- [ ] **Step 2: 実装を書く**

`CraftHoldDriver.cs`:

```csharp
using System;
using Client.Ui.Components.Recipe;
using Client.Ui.State.Recipe.Commands;
using Client.Ui.State.Recipe.Logic;
using Client.Ui.State.Toast;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 長押しクラフトの経過をパネルのスケジューラで進める（Web useHoldCraft の写し。Update は使わない）
    /// Advances the hold-to-craft timer on the panel scheduler (port of the Web useHoldCraft; no Update)
    /// </summary>
    public sealed class CraftHoldDriver
    {
        private readonly VisualElement _scheduleOwner;
        private readonly RecipeRow _row;
        private readonly IRecipeCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private Guid _recipeGuid;
        private double _craftTime;
        private bool _craftable;
        private double _elapsed;
        private IVisualElementScheduledItem _tick;

        public CraftHoldDriver(VisualElement scheduleOwner, RecipeRow row, IRecipeCommands commands, UiCommandResultToaster toaster)
        {
            _scheduleOwner = scheduleOwner;
            _row = row;
            _commands = commands;
            _toaster = toaster;
            // 木から外れている間はスケジューラが止まり、同じ要素を付け直せば続く（GUID で並べ直すときに Web の key と同じく長押しを保つ。D-34）。捨てるときは要素ごと捨てる
            // While detached the scheduler pauses and resumes when the same element is re-attached (keeps the hold across GUID reordering like the Web key; D-34); discarded entries are dropped whole
        }

        public void SetRecipe(Guid recipeGuid, double craftTime, bool craftable)
        {
            _recipeGuid = recipeGuid;
            _craftTime = craftTime;
            _craftable = craftable;
            // 作れなくなったら止める（Web の useEffect([craftable])）
            // Stop once it can no longer be crafted (the Web useEffect([craftable]))
            if (!craftable && _tick != null) Stop();
        }

        public void Start()
        {
            // 保持中・作れないときは始めない
            // Do not start while holding or when not craftable
            if (_tick != null || !_craftable) return;
            _elapsed = 0;
            _row.SetProgress(0f);
            _tick = _scheduleOwner.schedule.Execute(Tick).Every(0);
        }

        public void Stop()
        {
            _tick?.Pause();
            _tick = null;
            _elapsed = 0;
            _row.SetProgress(0f);
        }

        private void Tick(TimerState timer)
        {
            // 1 フレームの経過は 0.25 秒で頭打ち（タブ復帰などの大きな飛びで連続クラフトしない）
            // Cap one frame at 0.25 s so a large jump never crafts several times
            var delta = Math.Min(timer.deltaTime / 1000.0, HoldCraftLogic.MaxFrameDeltaSeconds);
            if (_craftable)
            {
                var step = HoldCraftLogic.Advance(_elapsed, delta, _craftTime);
                _elapsed = step.Elapsed;
                if (step.DidCraft) _toaster.Report("recipe.craft", _commands.Craft(_recipeGuid));
            }
            else
            {
                _elapsed = 0;
            }
            _row.SetProgress((float)HoldCraftLogic.Progress(_elapsed, _craftTime));
        }
    }
}
```

`CraftHoldInput.cs`:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// クラフトボタンの押下・離し・外れ・取消と Enter を長押しへ渡す（Web の onPointerDown/Up/Leave/Cancel・onKeyDown/Up の写し）
    /// Forwards press, release, leave, cancel and Enter on the craft button to the hold (port of the Web pointer and key handlers)
    /// </summary>
    public sealed class CraftHoldInput
    {
        private const int PrimaryButton = 0;
        private readonly VisualElement _button;
        private readonly CraftHoldDriver _driver;
        private bool _pressed;
        private bool _enterHeld;

        public CraftHoldInput(VisualElement button, CraftHoldDriver driver)
        {
            _button = button;
            _driver = driver;
            // Button の Clickable より先に受けるため TrickleDown で登録する
            // Register in TrickleDown so we see the events before the Button's Clickable
            button.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(_ => Release(), TrickleDown.TrickleDown);
            button.RegisterCallback<PointerLeaveEvent>(_ => Release());
            button.RegisterCallback<PointerCancelEvent>(_ => Release(), TrickleDown.TrickleDown);
            button.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);

            // フォーカス中の Enter は押している間だけ長押し、Space は何もしない（Web onKeyDown/onKeyUp・2026-10-01 裁定）
            // Enter while focused holds only while pressed; Space does nothing (Web onKeyDown/onKeyUp, 2026-10-01 ruling)
            button.RegisterCallback<KeyDownEvent>(OnKeyDown);
            button.RegisterCallback<KeyUpEvent>(OnKeyUp);
            button.RegisterCallback<BlurEvent>(_ => ReleaseEnter());
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            // silent-ok: Enter 以外のキーと押しっぱなしのリピートは長押しにしない / CraftHoldKeyTest の Space とリピートの件が見る
            if (!IsEnter(evt.keyCode) || _enterHeld) return;
            _enterHeld = true;
            _driver.Start();
        }

        private void OnKeyUp(KeyUpEvent evt)
        {
            if (IsEnter(evt.keyCode)) ReleaseEnter();
        }

        private void ReleaseEnter()
        {
            _enterHeld = false;
            _driver.Stop();
        }

        private static bool IsEnter(KeyCode keyCode)
        {
            return keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter;
        }

        private void OnDown(PointerDownEvent evt)
        {
            // silent-ok: 左以外は Web と同じく長押しにしない / CraftHoldTest の左押下と録画が左を見る
            if (evt.button != PrimaryButton) return;
            _pressed = true;
            _driver.Start();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            // Clickable が捕捉していると Leave が来ないので、外へ出たかを位置で読む（Web の pointerleave と同じ時点）
            // While Clickable holds capture no Leave arrives, so read the position to detect leaving (same moment as the Web pointerleave)
            if (_pressed && !_button.ContainsPoint(evt.localPosition)) Release();
        }

        private void Release()
        {
            _pressed = false;
            _driver.Stop();
        }
    }
}
```

`CraftRecipeEntryView.cs`（`RecipeEntryPartsContext` も同じファイルの下に置く。200 行未満）:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.State.MaterialTooltip;
using Client.Ui.State.Recipe.Logic;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// クラフトの 1 件（Web CraftRecipeEntry の写し）。素材・結果・ボタン・長押しを持ち、描き直しでは要素を作り直さない
    /// One craft entry (port of the Web CraftRecipeEntry); owns materials, result, button and hold, and never rebuilds on redraw
    /// </summary>
    public sealed class CraftRecipeEntryView : VisualElement
    {
        private const string MaterialSlotClass = "craft-recipe-entry__material";
        private readonly RecipeEntryPartsContext _context;
        private readonly RecipeRow _row;
        private readonly RecipeActionButton _button;
        private readonly CraftHoldDriver _hold;
        private readonly ItemSlot[] _materialSlots;
        private readonly ItemSlot _resultSlot;
        private ItemId[] _materialItemIds;

        public CraftRecipeEntryView(CraftRecipeEntryViewModel first, RecipeEntryPartsContext context)
        {
            _context = context;
            var guid = first.RecipeGuid;
            name = $"craft-recipe-entry-{guid}";

            // 行・秒数・ボタン（秒数は載せる前に入れる。C4-c の約束）
            // Row, seconds and button (seconds go in before mounting; C4-c contract)
            _row = new RecipeRow($"craft-recipe-box-{guid}", $"craft-progress-arrow-{guid}");
            _row.SetDuration(RecipeDurationText.Create(first.Recipe.CraftTime));
            _button = new RecipeActionButton($"craft-button-{guid}", new UiText(LocalizationKeys.Ui.Recipe.CraftButtonLabel), RecipeActionLabelWeight.Medium);
            if (first.CarriesTutorialAnchor) TutorialAnchor.Mark(_button, AnchorIds.RecipeCraftButton);
            _row.SetAction(_button);

            // Enter の長押しのためこのインスタンスだけフォーカスを持ち、規則に外されない印を付ける（部品の既定 focusable=false の画面側の例外。D-31・D-32）
            // Only this instance takes focus for the Enter hold and is marked so the rules keep it (screen-side exception to the component default; D-31, D-32)
            _button.focusable = true;
            UiKeyFocusKeepMark.Mark(_button);
            _hold = new CraftHoldDriver(this, _row, context.Commands, context.Toaster);
            new CraftHoldInput(_button, _hold);

            // 素材と結果のスロットは件数が GUID で決まるので 1 回だけ作る
            // Material and result slots are created once since the GUID fixes their count
            _materialSlots = new ItemSlot[first.Materials.Count];
            for (var i = 0; i < _materialSlots.Length; i++) _materialSlots[i] = CreateMaterialSlot(guid, i);
            _resultSlot = new ItemSlot($"craft-recipe-box-{guid}-result");
            _row.SetSlots(_materialSlots, new VisualElement[] { _resultSlot });
            Add(_row);
            Render(first);
        }

        public void Render(CraftRecipeEntryViewModel vm)
        {
            // 素材の所持・不足とツールチップ、結果の数、ボタンの可否、長押しの可否を当て直す
            // Re-apply material owned/lacking and tooltips, result count, button state and hold eligibility
            _materialItemIds = new ItemId[vm.Materials.Count];
            for (var i = 0; i < _materialSlots.Length; i++) ApplyMaterial(i, vm.Materials[i]);
            _resultSlot.SetContent(new ItemSlotContent(_context.Content.Item(vm.Recipe.ResultItemId), vm.Recipe.ResultCount, new ItemSlotMarks(false, false, false), null, null, null));
            _button.SetEnabled(vm.Craftable);
            _hold.SetRecipe(vm.RecipeGuid, vm.Recipe.CraftTime, vm.Craftable);
        }

        private ItemSlot CreateMaterialSlot(Guid guid, int index)
        {
            // 素材の左押下はその素材のレシピへ移る（Web onLeftDown）
            // A left press on a material moves to that material's recipes (the Web onLeftDown)
            var slot = new ItemSlot($"craft-recipe-box-{guid}-material-{index}");
            slot.AddToClassList(MaterialSlotClass);
            slot.OnSlotGestured.Where(g => g.Kind == SlotGestureKind.LeftDown).Subscribe(_ => _context.Selection.Select(_materialItemIds[index]));
            return slot;
        }

        private void ApplyMaterial(int index, RecipeMaterialViewModel material)
        {
            _materialItemIds[index] = material.ItemId;
            var itemName = _context.Content.ItemName(material.ItemId).Resolve();
            var tooltip = MaterialTooltipText.Create(MaterialTooltipKind.RecipeMaterial, itemName, material.Required, material.Owned);
            var shortage = new ItemSlotShortage(material.Owned, material.Required, material.Lacking, tooltip);
            _materialSlots[index].SetContent(new ItemSlotContent(_context.Content.Item(material.ItemId), material.Required, new ItemSlotMarks(false, false, material.Lacking), shortage, null, null));
        }
    }
}
```

（素材の `count` に必要数を渡すか、`ItemSlotShortage` が数を描くので `null` にするかは Web `CraftRecipeEntry.tsx` の `ItemSlot` の props〈`count`〉を Task 6 の着手時に読んで合わせる。購読の解除は要素の寿命＝ゲーム寿命なので持たない〈AGENTS「既知の制約」〉。ただしエントリは選択ごとに作り直すので、`OnSlotGestured` の購読は `ItemSlot` 自身の Subject に閉じ、外の源を購読しないことで漏れない）

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Recipe.Viewer.(CraftRecipeEntryViewTest|CraftHoldTest|CraftHoldKeyTest)|Client.Tests.Ui.Input.GameUiInputRulesTest"`
Expected: ErrorCount 0 / 新規 10 件（エントリ 3・長押し 3・キー 3・規則 1）と既存の `GameUiInputRulesTest` が PASS（既存の `ButtonFocusIsBlurredWithWarning` も緑のまま）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/Parts moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/UiKeyFocusKeepMark.cs moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiInputRulesTest.cs
git commit -m "feat(uitk): クラフトのエントリと長押しクラフト（マウスと Enter）を画面の部品で作り、フォーカスを保つ印を足す（S4-recipe）"
```

### Task 7: 機械のエントリ

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/Parts/MachineRecipeEntryView.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/Viewer/MachineRecipeEntryViewTest.cs`

**Interfaces:**
- Consumes: C4-c の `RecipeRow`・`LevelInheritanceBadge`、C4-a の `FluidAmountSlot`・`SlotAmountBadge`、`GameIcon`、`LevelInheritanceBadgeText.TryBuild`、Task 6 の `RecipeEntryPartsContext`
- Produces: `public sealed class MachineRecipeEntryView : VisualElement { public MachineRecipeEntryView(MachineRecipeEntryViewModel first, RecipeEntryPartsContext context); public void Render(MachineRecipeEntryViewModel vm); }`（name `machine-recipe-entry-<guid>`）

- [ ] **Step 1: テストを書く**

```csharp
/// <web-test file="src/features/recipe/views/MachineRecipeEntry.fluids.test.ts">液体の入力・出力をアイテムの後ろに量付きで描き、クリック遷移は持たせない</web-test>
/// <web-test file="e2e/tests/recipe/recipe.spec.ts">機械レシピの一覧に液体の入力を量付きで出す</web-test>
[Test]
public void 液体はアイテムの後ろに量付きで矢印の左右に並び押しても選択は変わらない()
{
    var guid = RecipeFixtures.RecipeGuid(9);
    var view = Mount(new MachineRecipeEntryView(new MachineRecipeEntryViewModel(MachineWithFluids(guid, inputFluidAmount: 10, outputFluidAmount: 5)), Context(Toaster, _commands, _selection)));

    var inputFluid = view.Q($"machine-recipe-entry-{guid}-input-fluid-0");
    var outputFluid = view.Q($"machine-recipe-entry-{guid}-output-fluid-0");
    var arrow = view.Q($"machine-progress-arrow-{guid}");
    var inputItem = view.Q($"machine-recipe-box-{guid}-input-0");
    Assert.Less(inputFluid.worldBound.xMax, arrow.worldBound.xMin);
    Assert.Greater(outputFluid.worldBound.xMin, arrow.worldBound.xMax);
    Assert.Less(inputItem.worldBound.xMin, inputFluid.worldBound.xMin);
    StringAssert.Contains("10", view.Q<Label>($"machine-recipe-entry-{guid}-input-fluid-0-amount").text);
    PressLeft(inputFluid);
    Assert.IsInstanceOf<NoRecipeSelection>(_selection.Current.Value);
}

/// <web-test file="src/features/recipe/views/MachineRecipeEntry.localization.test.ts">機械名をblockGuidから解決し、fallbackと言語変更をアイコンaltと表示・秒数へ反映する</web-test>
[Test]
public void 機械名と秒数は言語に追従しアイコンが無ければID文字で描く()
{
    var guid = RecipeFixtures.RecipeGuid(9);
    var view = Mount(new MachineRecipeEntryView(new MachineRecipeEntryViewModel(MachineWithFluids(guid, 10, 5)), Context(Toaster, _commands, _selection)));
    var english = view.Q<Label>(className: "machine-recipe-entry__block-name").text;
    Localize.TrySetChosenLanguage("japanese");
    simulate.FrameUpdate();
    Assert.AreNotEqual(english, view.Q<Label>(className: "machine-recipe-entry__block-name").text);
    StringAssert.EndsWith("秒", view.Q<Label>($"machine-recipe-box-{guid}-duration").text);
    Assert.IsNotNull(view.Q(className: "game-icon--id-text"));          // 偽のテクスチャ源は絵を返さないので ID 文字の代替（クラス名は GameIcon の実物に合わせる）
}

[Test]
public void 継承バッジは継承するレシピだけに出る() { /* RecipeLevelInheritance.Inherit(...) で name machine-recipe-level-inheritance-<guid> のラベルがある・None では無い */ }
```

（`MachineWithFluids` はテストの中の helper: `new MachineRecipeData(guid, new BlockId(1), 2.5, [Amount(1,1)], [Amount(2,1)], [new RecipeFluidAmount(new FluidId(1), 10)], [new RecipeFluidAmount(new FluidId(1), 5)], RecipeLevelInheritance.None)`。機械名の言語追従を見るには `ContentLocalizationKeys.BlockName` の辞書が要るので、前例 `ModeHudPlacementNameTest` の `PlacementNameTestDictionary` の手順で英日の名前を合成する）

- [ ] **Step 2: 実装を書く**

```csharp
using System.Collections.Generic;
using Client.Ui.Components.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Logic;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 機械の 1 件（Web MachineRecipeEntry の写し）。矢印は止めたまま、アイテムの後ろに液体を量付きで並べる
    /// One machine entry (port of the Web MachineRecipeEntry); the arrow stays idle and fluids follow items with amounts
    /// </summary>
    public sealed class MachineRecipeEntryView : VisualElement
    {
        private readonly LevelInheritanceBadge _badge;

        public MachineRecipeEntryView(MachineRecipeEntryViewModel first, RecipeEntryPartsContext context)
        {
            var recipe = first.Recipe;
            var guid = recipe.RecipeGuid;
            name = $"machine-recipe-entry-{guid}";

            // 行は進捗を持たない（Web arrowValue null）
            // The row carries no progress (the Web arrowValue null)
            var row = new RecipeRow($"machine-recipe-box-{guid}", $"machine-progress-arrow-{guid}");
            row.SetDuration(RecipeDurationText.Create(recipe.Time));
            row.SetIdle();
            row.SetSlots(BuildSide(recipe.InputItems, recipe.InputFluids, "input"), BuildSide(recipe.OutputItems, recipe.OutputFluids, "output"));

            // 操作の欄: 機械のアイコン・名前・継承バッジ
            // Action column: machine icon, name and inheritance badge
            var info = new VisualElement();
            info.AddToClassList("machine-recipe-entry__info");
            var icon = new GameIcon();
            icon.AddToClassList("machine-recipe-entry__block-icon");
            icon.SetIcon(context.Content.Block(recipe.BlockId), GameIconFallback.IdText);
            var blockName = new LocalizedLabel();
            blockName.AddToClassList("machine-recipe-entry__block-name");
            blockName.SetText(context.Content.Block(recipe.BlockId).Label);
            _badge = new LevelInheritanceBadge($"machine-recipe-level-inheritance-{guid}");
            info.Add(icon);
            info.Add(blockName);
            info.Add(_badge);
            row.SetAction(info);
            Add(row);
            Render(first);

            #region Internal

            IReadOnlyList<VisualElement> BuildSide(IReadOnlyList<ItemAmount> items, IReadOnlyList<RecipeFluidAmount> fluids, string side)
            {
                // アイテム（押すと選ぶ）の後ろに液体（押しても何もしない）
                // Items (select on press) followed by fluids (no action on press)
                var slots = new List<VisualElement>();
                for (var i = 0; i < items.Count; i++) slots.Add(ItemSlotOf(items[i], $"machine-recipe-box-{guid}-{side}-{i}"));
                for (var i = 0; i < fluids.Count; i++) slots.Add(FluidSlotOf(fluids[i], $"{name}-{side}-fluid-{i}"));
                return slots;
            }

            VisualElement ItemSlotOf(ItemAmount amount, string testid)
            {
                var slot = new ItemSlot(testid);
                slot.SetContent(new ItemSlotContent(context.Content.Item(amount.ItemId), amount.Count, new ItemSlotMarks(false, false, false), null, null, null));
                slot.OnSlotGestured.Where(g => g.Kind == SlotGestureKind.LeftDown).Subscribe(_ => context.Selection.Select(amount.ItemId));
                return slot;
            }

            VisualElement FluidSlotOf(RecipeFluidAmount fluid, string testid)
            {
                var slot = new FluidAmountSlot(testid);
                slot.SetFluid(context.Content.Fluid(fluid.FluidId), SlotAmountBadge.Show(fluid.Amount));
                return slot;
            }

            #endregion
        }

        public void Render(MachineRecipeEntryViewModel vm)
        {
            // 継承の上限は研究で変わるのでバッジだけ当て直す（C4-c の約束: TryBuild の戻り値で分ける）
            // The inheritance cap changes with research, so only the badge is re-applied (C4-c contract: branch on TryBuild)
            if (LevelInheritanceBadgeText.TryBuild(vm.Recipe.LevelInheritance, out var text)) _badge.Show(text);
            else _badge.Hide();
        }
    }
}
```

（Web の機械名の `alt` は UITK に無い。アイコンの代替は `GameIconFallback.IdText`〈Web の fallback idText〉。部分移植の残り「alt の文」を `done.md` の「## 部分移植の残り」に 1 行）

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.Screens.Recipe.Viewer.MachineRecipeEntryViewTest"`
Expected: ErrorCount 0 / 3 件 PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/Parts/MachineRecipeEntryView.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/Viewer/MachineRecipeEntryViewTest.cs
git commit -m "feat(uitk): 機械レシピのエントリ（液体・機械名・継承バッジ）を作る（S4-recipe）"
```

### Task 8: ビューアのパネル（見出し・列・案内）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/RecipeViewerView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/RecipeViewer.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/Parts/{ItemHeaderView.cs,ItemHeaderRule.cs,RecipeEntryList.cs}`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss`（末尾に「S4-recipe」ブロック）
- Modify: `tools/uitk/webTokens/handWrittenDerivedRows.mjs`・`docs/uitk/translucent-tokens.md`（再生成）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/Viewer/RecipeViewerViewTest.cs`

**Interfaces:**
- Consumes: Task 6〜7 のエントリ、C2-a の `GamePanel(Craft)`、C4-c の `RecipeListScrollView`
- Produces:
  - `public sealed class RecipeViewerView : VisualElement { public RecipeViewerView(RecipeEntryPartsContext context); public void Render(RecipeViewerViewModel vm); public void SetShown(bool shown); }`（ルートのクラス `recipe-viewer`。選択ありで `recipe-viewer--selected`）
  - `public sealed class RecipeEntryList : VisualElement { public RecipeEntryList(RecipeEntryPartsContext context); public void Render(IReadOnlyList<RecipeEntryViewModel> entries); }`（GUID の並びが同じなら各エントリの `Render` だけ、違えば GUID の辞書で既存の要素を使い回し新しい GUID だけ作って並べ直し `RecipeListScrollView.SetEntries`）
  - `public sealed class ItemHeaderView : VisualElement { public ItemHeaderView(); public void SetName(UiText name); }`、`public sealed class ItemHeaderRule : VisualElement`（つまみ `--item-header-rule-color` を読み、1px の線を 8 つの色止めの頂点カラーで描く）

- [ ] **Step 1: テストを書く**

```csharp
/// <web-test file="e2e/tests/recipe/recipe.spec.ts">正本のヘッダ装飾、1段時の無スクロールバー、主要構造を保つ</web-test>
[Test]
public void ヘッダの装飾線と菱形があり1件ならバーが出ずグリップは中身に重ならない()
{
    var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
    view.Render(Selected(7, CraftVm(RecipeFixtures.RecipeGuid(1), 2, true, true, Material(1, 1, 3))));
    simulate.FrameUpdate();

    var rule = view.Q<ItemHeaderRule>();
    Assert.AreEqual(276.743f, rule.resolvedStyle.width, 0.5f);
    Assert.AreEqual(9f, rule.resolvedStyle.height, 0.5f);
    Assert.IsNotNull(view.Q(className: "item-header__diamond"));
    var scroll = view.Q<GameScrollView>("recipe-entry-list-scroll");
    Assert.AreEqual(DisplayStyle.None, scroll.verticalScroller.resolvedStyle.display);
    var grip = view.Q(className: "game-panel-craft-grip");            // C2-a の実物のクラス名に合わせる
    Assert.IsFalse(grip.worldBound.Overlaps(view.Q("recipe-entry-list").worldBound));
    Assert.AreEqual(432.983f, view.Q<GamePanel>("recipe-viewer").resolvedStyle.height, 0.5f);
}

/// <web-test file="e2e/tests/recipe/recipe.spec.ts">複数レシピはクラフト優先の単一リストで同時に表示される</web-test>
[Test]
public void 複数レシピはクラフトが先の1本の列に並ぶ() { /* クラフト 2・機械 1 の VM → recipe-entry-list の子の name が craft, craft, machine の順 */ }

/// <web-test file="e2e/tests/recipe/recipe.spec.ts">クラフトエントリが複数でもチュートリアルアンカーは1件だけ付く</web-test>
[Test]
public void クラフトが複数でもアンカーは1件() { /* TutorialAnchorRegistry.Resolve(root, "recipe.craft-button") が Unique で、先頭の craft-button-<guid1> */ }

/// <web-test file="e2e/tests/recipe/recipeListScroll.spec.ts">レシピが溢れてもリストの視口高は変わらず、縦バーだけが出る</web-test>
[Test]
public void 溢れても視口の高さは同じで縦バーだけが出る() { /* 1 件と 8 件で GetViewportHeight が同じ・8 件で verticalScroller が表示・horizontalScroller は非表示 */ }

[Test]
public void 未選択は案内で高さ300に戻り選択を変えるとスクロールが先頭に戻り同じGUIDの並びでは要素を作り直さない()
{
    var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
    view.Render(RecipeViewerNoSelection.Instance);
    simulate.FrameUpdate();
    Assert.AreEqual(300f, view.Q<GamePanel>("recipe-viewer").resolvedStyle.height, 0.5f);
    Assert.AreEqual(DisplayStyle.Flex, view.Q<Label>("recipe-viewer-hint").resolvedStyle.display);

    var vm = Selected(7, ManyCrafts(8));
    view.Render(vm);
    var first = view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}");
    view.Render(Selected(7, ManyCrafts(8)));
    Assert.AreSame(first, view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}"));
    view.Q<GameScrollView>("recipe-entry-list-scroll").SetScrollOffset(80);
    view.Render(Selected(8, ManyCrafts(8)));
    simulate.FrameUpdate();
    Assert.AreEqual(0f, view.Q<GameScrollView>("recipe-entry-list-scroll").scrollOffset.y, 0.5f);
}

[Test]
public void 隠して出し直すと同じ選択でも中身を作り直す()
{
    // Web は画面を出るとアンマウントするので、開き直すと長押しもスクロールも初期化される
    // Web unmounts on leaving the screen, so reopening resets the hold and the scroll
    var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
    view.Render(Selected(7, ManyCrafts(8)));
    var first = view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}");
    view.SetShown(false);
    Assert.IsNull(first.panel);
    view.SetShown(true);                                                  // Render を呼び直さない（Tab で閉じて開き直した直後。持ち物の再取得を待たない）
    var again = view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}");
    Assert.IsNotNull(again);
    Assert.AreNotSame(first, again);
}

[Test]
public void 同じ選択のままレシピ0件から1件になると列が出る()
{
    var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
    view.Render(Selected(7));
    Assert.IsNotNull(view.Q<Label>("recipe-viewer-no-recipes"));
    view.Render(Selected(7, CraftVm(RecipeFixtures.RecipeGuid(1), 2, true, false, Material(1, 1, 3))));
    Assert.IsNull(view.Q<Label>("recipe-viewer-no-recipes"));
    Assert.IsNotNull(view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}"));
    view.Render(Selected(7));
    Assert.IsNotNull(view.Q<Label>("recipe-viewer-no-recipes"));
}

[Test]
public void 最初のエントリは見出しの装飾線の下12pxに来る()
{
    var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
    view.Render(Selected(7, CraftVm(RecipeFixtures.RecipeGuid(1), 2, true, false, Material(1, 1, 3))));
    simulate.FrameUpdate();
    var ruleBottom = view.Q<ItemHeaderRule>().worldBound.yMax;
    var entryTop = view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}").worldBound.yMin;
    Assert.AreEqual(12f, entryTop - ruleBottom, 0.5f);                   // Web の Stack gap sm（-12 の margin と 12 の padding は打ち消し合う）
}

[Test]
public void 解放で並びが変わっても既存のエントリは同じ要素で長押しが続く()
{
    var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
    var guid = RecipeFixtures.RecipeGuid(2);
    view.Render(Selected(7, CraftVm(guid, 0.4, true, false, Material(1, 1, 9))));
    var entry = view.Q($"craft-recipe-entry-{guid}");
    PointerDown(entry.Q<RecipeActionButton>());
    for (var i = 0; i < 3; i++) simulate.FrameUpdateMs(100);
    view.Render(Selected(7, CraftVm(RecipeFixtures.RecipeGuid(1), 0.4, true, false, Material(1, 1, 9)), CraftVm(guid, 0.4, true, false, Material(1, 1, 9))));   // 先頭に別レシピが解放された
    Assert.AreSame(entry, view.Q($"craft-recipe-entry-{guid}"));
    for (var i = 0; i < 2; i++) simulate.FrameUpdateMs(100);             // 合計 0.5 秒 → 1 回（経過を保った）
    CollectionAssert.AreEqual(new[] { guid }, _commands.Crafted);
}
```

（`Selected(itemId, params RecipeEntryViewModel[])`・`ManyCrafts(n)` は `RecipeScreenFixtures` に足す。`TutorialAnchorRegistry.Resolve` は F1 の実物）

- [ ] **Step 2: 実装を書く**

`RecipeViewerView.cs`:

```csharp
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe
{
    /// <summary>
    /// レシピビューアのパネル（Web RecipeViewer・RecipeContent の写し）。選択が変わると中身を作り直してスクロールを先頭へ戻す
    /// Recipe viewer panel (port of the Web RecipeViewer and RecipeContent); a new selection rebuilds the content and resets the scroll
    /// </summary>
    public sealed class RecipeViewerView : VisualElement
    {
        private const string SelectedClass = "recipe-viewer--selected";
        private readonly RecipeEntryPartsContext _context;
        private readonly GamePanel _panel;
        private readonly LocalizedLabel _hint;
        private readonly VisualElement _content;
        private RecipeSelection _shownSelection = RecipeSelection.None;
        private bool _shownHasRecipes;
        private RecipeViewerViewModel _latest = RecipeViewerNoSelection.Instance;
        private bool _shown = true;
        private ItemHeaderView _header;
        private RecipeEntryList _list;
        private LocalizedLabel _noRecipes;

        public RecipeViewerView(RecipeEntryPartsContext context)
        {
            _context = context;
            AddToClassList("recipe-viewer");
            _panel = new GamePanel("recipe-viewer", GamePanelFrame.Craft);
            _hint = new LocalizedLabel { name = "recipe-viewer-hint" };
            _hint.AddToClassList(UiTypographyClass.TextSm);
            _hint.AddToClassList(UiTypographyClass.TextDimmed);
            _hint.AddToClassList("recipe-viewer__hint");
            _hint.SetText(new UiText(LocalizationKeys.Ui.Recipe.SelectItemHint));
            _content = new VisualElement();
            _content.AddToClassList("recipe-viewer__content");
            _panel.Add(_hint);
            _panel.Add(_content);
            Add(_panel);
        }

        public void Render(RecipeViewerViewModel vm)
        {
            // 最新の VM を覚え、隠れている間は組まない（出し直すときに覚えた VM で組む＝Web の再マウント。D-33）
            // Remember the latest VM and skip building while hidden (showing again builds from it, like the Web remount; D-33)
            _latest = vm;
            if (!_shown) return;

            // 未選択は案内だけ、選択ありは高さを切り替えて中身を出す（Web height 300 / 432.983）
            // No selection shows the hint only; a selection switches the height and shows the content (Web height 300 / 432.983)
            var selected = vm as RecipeViewerItemSelected;
            EnableInClassList(SelectedClass, selected != null);
            _hint.style.display = selected == null ? DisplayStyle.Flex : DisplayStyle.None;
            _content.style.display = selected == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (selected == null)
            {
                _shownSelection = RecipeSelection.None;
                return;
            }

            // 別のアイテム、または「レシピ無し」と列の境をまたいだら作り直す（Web は key の再マウントと entries.length の枝の決め直し）
            // Rebuild for a different item or when crossing between "no recipes" and the list (Web remounts by key and re-picks the entries.length branch)
            var hasRecipes = selected.Entries.Count > 0;
            if (!_shownSelection.SameAs(RecipeSelection.Of(selected.ItemId)) || hasRecipes != _shownHasRecipes) RebuildContent(selected);
            if (hasRecipes) _list.Render(selected.Entries);
        }

        public void SetShown(bool shown)
        {
            _shown = shown;
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (shown)
            {
                // 出し直したら覚えた VM で組む（持ち物の再取得など別の契機を待たない）
                // Build from the remembered VM on show (without waiting for another trigger such as an inventory refetch)
                Render(_latest);
                return;
            }

            // 閉じたら中身を捨てる（Web は画面を出るとアンマウント＝長押しが止まり、開き直すとスクロールが先頭）
            // Drop the content on hide (Web unmounts: the hold stops and the scroll is at the top on reopen)
            _content.Clear();
            _list = null;
            _shownSelection = RecipeSelection.None;
        }

        private void RebuildContent(RecipeViewerItemSelected selected)
        {
            _shownSelection = RecipeSelection.Of(selected.ItemId);
            _shownHasRecipes = selected.Entries.Count > 0;
            _content.Clear();
            _list = null;
            _header = new ItemHeaderView();
            _header.SetName(_context.Content.ItemName(selected.ItemId));
            _content.Add(_header);

            // レシピが無ければ「レシピ無し」、あれば列（Web の Stack gap sm）
            // "No recipes" when empty, otherwise the list (the Web Stack gap sm)
            if (selected.Entries.Count == 0)
            {
                _noRecipes = new LocalizedLabel { name = "recipe-viewer-no-recipes" };
                _noRecipes.AddToClassList(UiTypographyClass.TextSm);
                _noRecipes.AddToClassList(UiTypographyClass.TextDimmed);
                _noRecipes.SetText(new UiText(LocalizationKeys.Ui.Recipe.NoRecipes));
                _content.Add(_noRecipes);
                return;
            }
            _list = new RecipeEntryList(_context);
            _content.Add(_list);
        }
    }
}
```

`RecipeEntryList.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.State.Recipe.Screen;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// エントリを GUID で使い回す列（Web の React key と同じく、描き直しで長押しの状態を失わない）
    /// List that reuses entries by GUID (like the Web React key, a redraw never loses the hold state)
    /// </summary>
    public sealed class RecipeEntryList : VisualElement
    {
        private readonly RecipeEntryPartsContext _context;
        private readonly RecipeListScrollView _scroll = new("recipe-entry-list");
        private readonly Dictionary<Guid, VisualElement> _viewsByGuid = new();
        private readonly List<Guid> _order = new();

        public RecipeEntryList(RecipeEntryPartsContext context)
        {
            _context = context;
            AddToClassList("recipe-entry-list");
            Add(_scroll);
        }

        public void Render(IReadOnlyList<RecipeEntryViewModel> entries)
        {
            // 並びが変われば GUID の辞書で既存の要素を使い回して並べ直す（解放で増えても長押し中の要素は同じ。Web の React key と同じ。D-34）
            // On an order change, reuse existing elements from the GUID map and re-lay them (an unlock keeps the holding element; like the Web React key; D-34)
            if (!SameOrder(entries)) Relayout(entries);
            foreach (var entry in entries) Apply(_viewsByGuid[entry.RecipeGuid], entry);
        }

        private bool SameOrder(IReadOnlyList<RecipeEntryViewModel> entries)
        {
            if (entries.Count != _order.Count) return false;
            for (var i = 0; i < entries.Count; i++) if (entries[i].RecipeGuid != _order[i]) return false;
            return true;
        }

        private void Relayout(IReadOnlyList<RecipeEntryViewModel> entries)
        {
            var next = new List<VisualElement>(entries.Count);
            var kept = new HashSet<Guid>();
            foreach (var entry in entries)
            {
                kept.Add(entry.RecipeGuid);
                if (!_viewsByGuid.TryGetValue(entry.RecipeGuid, out var view))
                {
                    view = Create(entry);
                    _viewsByGuid.Add(entry.RecipeGuid, view);
                }
                next.Add(view);
            }

            // 消えた GUID の要素は捨てる
            // Drop elements whose GUID is gone
            foreach (var guid in _order) if (!kept.Contains(guid)) _viewsByGuid.Remove(guid);
            _order.Clear();
            foreach (var entry in entries) _order.Add(entry.RecipeGuid);

            // SetEntries（C4-c）は一度外して付け直す。外れている間は長押しのスケジューラが止まり、同じフレームで付け直すので経過は保たれる（CraftHoldDriver。D-34）
            // SetEntries (C4-c) detaches and re-attaches; the hold scheduler pauses while detached and resumes in the same frame, so progress is kept (CraftHoldDriver; D-34)
            _scroll.SetEntries(next);
        }

        private VisualElement Create(RecipeEntryViewModel entry)
        {
            return entry switch
            {
                CraftRecipeEntryViewModel craft => new CraftRecipeEntryView(craft, _context),
                MachineRecipeEntryViewModel machine => new MachineRecipeEntryView(machine, _context),
                _ => throw new ArgumentOutOfRangeException(nameof(entry), entry, "未知のレシピエントリ"),
            };
        }

        private static void Apply(VisualElement view, RecipeEntryViewModel entry)
        {
            switch (view)
            {
                case CraftRecipeEntryView craftView: craftView.Render((CraftRecipeEntryViewModel)entry); break;
                case MachineRecipeEntryView machineView: machineView.Render((MachineRecipeEntryViewModel)entry); break;
            }
        }
    }
}
```

`ItemHeaderView.cs`・`ItemHeaderRule.cs`（見出しの名前は `LocalizedLabel`〈`item-header__name`。`UiTypographyClass.TextMedium` を付ける〉、その下に `ItemHeaderRule`〈`item-header__rule`〉と子の菱形〈`item-header__diamond`。USS で 7×7・枠 1px・`rotate: 45deg`・中央〉）。`ItemHeaderRule` は `CustomStyleResolvedEvent` で `--item-header-rule-color` を読み、`generateVisualContent` で高さ中央の 1px の帯を 8 つの色止め（0 透明・20%・44%・48% 透明・52% 透明・56%・80%・100% 透明。Web `ItemHeader.module.css` の linear-gradient と同じ）の 16 頂点・14 三角形で描く（前例: 頂点カラーで描く `S/Client.Ui/Screens/CommonHud/CrosshairShadowRing.cs`）:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 見出しの装飾線（Web ItemHeader の ::before の linear-gradient を頂点カラーで写す）
    /// Header decoration line (vertex-color port of the Web ItemHeader ::before linear-gradient)
    /// </summary>
    public sealed class ItemHeaderRule : VisualElement
    {
        private static readonly CustomStyleProperty<Color> RuleColorProperty = new("--item-header-rule-color");
        private static readonly float[] Stops = { 0f, 0.2f, 0.44f, 0.48f, 0.52f, 0.56f, 0.8f, 1f };
        private static readonly bool[] Opaque = { false, true, true, false, false, true, true, false };
        private Color _ruleColor = Color.clear;

        public ItemHeaderRule()
        {
            AddToClassList("item-header__rule");
            pickingMode = PickingMode.Ignore;
            var diamond = new VisualElement { pickingMode = PickingMode.Ignore };
            diamond.AddToClassList("item-header__diamond");
            Add(diamond);
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
            generateVisualContent += Draw;
        }

        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            // 色はトークンの上書きを受けて描き直す。読めなければ線を描かずに警告する
            // Redraw with the token override; warn and draw nothing when unreadable
            if (!evt.customStyle.TryGetValue(RuleColorProperty, out _ruleColor)) Debug.LogWarning("[ItemHeaderRule] --item-header-rule-color を読めないので装飾線を描かない");
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            // 高さの中央に 1px の帯を色止めごとの 2 頂点で張る
            // Lay a 1px band at mid-height with two vertices per color stop
            var rect = contentRect;
            if (rect.width <= 0f || _ruleColor.a <= 0f) return;
            var top = rect.height * 0.5f - 0.5f;
            var mesh = context.Allocate(Stops.Length * 2, (Stops.Length - 1) * 6);
            for (var i = 0; i < Stops.Length; i++)
            {
                var color = Opaque[i] ? _ruleColor : new Color(_ruleColor.r, _ruleColor.g, _ruleColor.b, 0f);
                var x = rect.width * Stops[i];
                mesh.SetNextVertex(new Vertex { position = new Vector3(x, top, Vertex.nearZ), tint = color });
                mesh.SetNextVertex(new Vertex { position = new Vector3(x, top + 1f, Vertex.nearZ), tint = color });
            }
            for (var i = 0; i < Stops.Length - 1; i++)
            {
                var a = (ushort)(i * 2);
                mesh.SetNextIndex(a); mesh.SetNextIndex((ushort)(a + 2)); mesh.SetNextIndex((ushort)(a + 1));
                mesh.SetNextIndex((ushort)(a + 1)); mesh.SetNextIndex((ushort)(a + 2)); mesh.SetNextIndex((ushort)(a + 3));
            }
        }
    }
}
```

`RecipeViewer.uss`（`/* web: src/features/recipe/panels/RecipeViewer.tsx・RecipeViewer.module.css・views/ItemHeader.module.css・views/CraftRecipeEntry.tsx */` を先頭に。値は Web の逐語）:

```css
/* web: src/features/recipe/panels/RecipeViewer.tsx（style）・RecipeViewer.module.css・views/ItemHeader.module.css・views/CraftRecipeEntry.tsx（素材スロットの style） */
.recipe-viewer {
    --recipe-viewer-stack-gap: 12px;   /* web: RecipeContent の <Stack gap="sm">（Mantine 既定 0.75rem） */
    align-self: flex-start;
    align-items: center;
    margin-top: 2px;
    translate: 0.391px -0.391px;
}
.recipe-viewer #recipe-viewer {
    width: 337.2px;
    height: 300px;
}
.recipe-viewer--selected #recipe-viewer {
    height: 432.983px;
}
.recipe-viewer__hint {
    margin: auto;
    -unity-text-align: middle-center;
}
.recipe-viewer__content {
    flex-grow: 1;
    min-height: 0;
}
.recipe-viewer .item-header__name {
    font-size: 19.75px;
    height: 19.963px;
    min-width: 58.715px;
    margin-top: 4.8px;
    letter-spacing: 0.1975px;
    text-shadow: 0.3px 0 0 var(--item-header-name-shadow);   /* web は 0.3px 0 0 と -0.3px 0 0 の 2 本。UITK は 1 本なので右側だけ（左の 0.3px は LX-135 の記帳候補） */
    -unity-text-align: middle-center;
    white-space: nowrap;
}
.recipe-viewer .item-header__rule {
    width: 276.743px;
    height: 9px;
    margin-top: 2.72px;
    align-self: center;
    --item-header-rule-color: var(--item-header-rule-face);
}
.recipe-viewer .item-header__diamond {
    position: absolute;
    left: 50%;
    top: 50%;
    width: 7px;
    height: 7px;
    translate: -50% -50%;
    rotate: 45deg;
    border-width: 1px;
    border-color: var(--item-header-diamond-border);
}
.recipe-viewer .recipe-entry-list {
    flex-grow: 1;
    min-height: 0;
    margin: calc(-1 * var(--tutorial-anchor-clip-inset));   /* 上だけ calc(gap - clip-inset)。下の注 */
}
.recipe-viewer #recipe-viewer-no-recipes {
    margin-top: 12px;   /* web: Stack gap sm */
}
.recipe-viewer .recipe-list-scroll-view {
    --game-scroll-view-scrollbar-inset-top: var(--tutorial-anchor-clip-inset);
    --game-scroll-view-scrollbar-inset-bottom: var(--tutorial-anchor-clip-inset);
    --game-scroll-view-scrollbar-inset-right: var(--tutorial-anchor-clip-inset);
}
.recipe-viewer .craft-recipe-entry__material {
    --shortage-count-right: 0;
    --shortage-count-bottom: 0;
}
.recipe-viewer .machine-recipe-entry__info {
    align-items: center;
}
.recipe-viewer .machine-recipe-entry__block-icon {
    width: var(--recipe-info-icon-size);
    height: var(--recipe-info-icon-size);
}
.recipe-viewer .machine-recipe-entry__block-name {
    color: var(--text-muted);
    font-size: var(--recipe-info-text-size);
    max-width: var(--recipe-action-width);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
```

（USS は `calc()` を持たない。`margin: calc(...)` と viewport の padding は C# で `UssCustomStyleReader` が `--tutorial-anchor-clip-inset` と `--recipe-viewer-stack-gap` を読んで `RecipeEntryList` の `style.marginLeft/Right/Bottom = -inset`・`style.marginTop = gap - inset`（見出しと列の間の Stack gap sm 12px。最初のエントリは見出しの下 12px）と `RecipeListScrollView` の視口の padding へ当てる形に直す〈Task 8 の着手時。前例 C4-e の列の余白〉。名前の字の太さは `ItemHeaderView` が名前のラベルに `UiTypographyClass.TextMedium` を付ける（`Typography.uss` の `.text--medium` がラテンと CJK の両方を持つ。`--font-medium` は無い）。`translate`・`rotate` の書き方は既存の画面 USS の実物に合わせ、`gap`・`z-index` は使わない。字間 0.01em は 19.75px で 0.1975px＝UITK の「大きさ/100 × 値」で 1 を書く〈生成器の換算に合わせ、Task 10 の実測で決める〉）

`DerivedTokens.uss` の末尾（`/* S4-recipe: … */` の日英 2 行の名札の下）:

```css
    --item-header-rule-face: rgba(132, 137, 151, 0.6315); /* web: ItemHeader.module.css の rgb(132 137 151 / 78%) を式 correctAlphaForLinearBlend で補正（A-113） */
    --item-header-diamond-border: #b8c0cc;                 /* web: ItemHeader.module.css ::after の border 1px solid #b8c0cc */
    --item-header-name-shadow: rgba(0, 0, 0, 0.7717);      /* web: ItemHeader の text-shadow rgb(0 0 0 / 55%) を式 correctAlphaForLinearBlend で補正 */
    --item-list-scroll-thumb-face: rgba(255, 255, 255, 0.7894); /* web: ItemListPanel.module.css のつまみ rgb(255 255 255 / 88%) を式で補正（A-113） */
```

`handWrittenDerivedRows.mjs` の `rowSources` の末尾に 3 行（3 本とも `formula:` の行: `--item-header-rule-face`〈`formula: [132, 137, 151, 0.78]`〉・`--item-header-name-shadow`〈`formula: [0, 0, 0, 0.55]`〉・`--item-list-scroll-thumb-face`〈`formula: [255, 255, 255, 0.88]`〉。`method` は `${translucencyMethod}（… S4-recipe）`。式の値が上の USS の値〈0.6315・0.7717・0.7894〉と一致することを再生成の差分で見る）。再生成は `node tools/uitk/webTokens/generate-translucent-table.mjs`（実名は `tools/uitk/webTokens/` を見て合わせる）。

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.Screens.Recipe.Viewer.RecipeViewerViewTest"`
Expected: ErrorCount 0 / 9 件 PASS（USS は Task 10 の tss 登録前なので、テストは `UiTestHost` の本番 tss に加えて `RecipeViewer.uss` を `styleSheets.Add` する。前例は既存の画面 L1 の SetUp を grep して合わせる）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss tools/uitk/webTokens/handWrittenDerivedRows.mjs docs/uitk/translucent-tokens.md moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe
git commit -m "feat(uitk): レシピビューアのパネル（見出しの装飾線・GUID で使い回す列）を作る（S4-recipe）"
```

### Task 9: アイテム一覧のパネルと掴みスクロール

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/ItemListView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/ItemList.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/Parts/{ItemListCellView.cs,ItemListDragScroll.cs}`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView/GameScrollView.uss`（つまみ 5 本を参照するだけ。既定は要素に書かない）・`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss`（「S4-recipe」ブロックに 5 本の既定＝今の値を `:root` で置く。D-32）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/ItemList/{ItemListViewTest.cs,ItemListScrollGeometryTest.cs,ItemListDragScrollTest.cs}`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Controls/GameScrollViewKnobTest.cs`

**Interfaces:**
- Consumes: Task 3 の `ItemListViewModel`・`RecipeItemSelection`、C2-a の `GamePanel`、C2-c の `GameScrollView`、C3-a の `ItemSlot`・`SlotGrid`、C3-b の `UiPressGesture`・`UiDragThreshold`
- Produces:
  - `public sealed class ItemListView : VisualElement { public ItemListView(GameContentPresentation content, RecipeItemSelection selection); public void Render(ItemListViewModel vm); public void SetShown(bool shown); }`（ルートのクラス `item-list`）
  - `public sealed class ItemListCellView : VisualElement { public ItemListCellView(ItemId itemId); public ItemId ItemId { get; } public void SetContent(UiIcon icon, int craftableCount); }`（name `item-list-cell-<id>`、アンカー `recipe.item-<id>`）
  - `public sealed class ItemListDragScroll : IUiPressGestureHandler { public ItemListDragScroll(GameScrollView scroll, RecipeItemSelection selection); }`
  - `GameScrollView.uss` のつまみ: `--game-scroll-view-track-face`（既定 `var(--gauge-track)`）・`--game-scroll-view-thumb-face`（既定 `var(--bevel-c2)`）・`--game-scroll-view-scrollbar-inset-top`・`-bottom`・`-right`（既定 0）。既定は `DerivedTokens.uss` の `:root` にあり、`.game-scroll-view` 自身には宣言しないので、祖先（`.recipe-list-scroll-view` 等）での上書きが中の `GameScrollView` まで継承で届く

- [ ] **Step 1: テストを書く**

`ItemListDragScrollTest.cs`:

```csharp
/// <web-test file="src/features/recipe/panels/useDragScroll.test.ts">閾値未満で離すとタップとして押下点を通知する</web-test>
[Test]
public void 閾値未満で離すと押したセルのアイテムを選ぶ()
{
    var view = Mount(new ItemListView(_content, _selection));
    view.Render(ListOf(30));
    simulate.FrameUpdate();
    var cell = view.Q<ItemListCellView>("item-list-cell-3");
    var at = cell.worldBound.center;
    PressAndRelease(at, at + new Vector2(0, 4));                         // 4px は閾値 5 未満
    Assert.AreEqual(new ItemId(3), ((ItemRecipeSelection)_selection.Current.Value).ItemId);
}

/// <web-test file="src/features/recipe/panels/useDragScroll.test.ts">閾値を超えて動かすとスクロールし、離してもタップにしない</web-test>
[Test]
public void 閾値を超えて動かすとスクロールし離しても選ばない()
{
    var view = Mount(new ItemListView(_content, _selection));
    view.Render(ListOf(120));
    simulate.FrameUpdate();
    var scroll = view.Q<GameScrollView>("item-list-scroll");
    scroll.SetScrollOffset(100);
    var at = view.Q<ItemListCellView>("item-list-cell-40").worldBound.center;
    PressDragRelease(at, at + new Vector2(0, -30));
    Assert.AreEqual(130f, scroll.scrollOffset.y, 0.5f);
    Assert.IsInstanceOf<NoRecipeSelection>(_selection.Current.Value);
}
```

`ItemListViewTest.cs`:

```csharp
/// <web-test file="e2e/tests/recipe/recipe.spec.ts">リスト上をドラッグしてもアイテム選択は変わらない（スクロール操作扱い）</web-test>
[Test]
public void 選択中に一覧をドラッグしても選択は変わらない() { /* Select(5) → セル 9 の上で 40px ドラッグ → 選択は 5 のまま */ }

/// <web-test file="e2e/tests/recipe/recipe.spec.ts">クラフト可能数0のアイテムは個数バッジを出さない</web-test>
[Test]
public void クラフト可能数0はバッジを出さず正の数は出す() { /* 101 → 0、100 → 3 のセル。セル 100 に OutlinedCountLabel の文 "3"、101 には無い */ }

[Test]
public void 隠すとスクロールが先頭へ戻る() { /* ListOf(120) → SetScrollOffset(100) → SetShown(false) → SetShown(true) → FrameUpdate → scrollOffset.y が 0（Web のアンマウントの写し） */ }

[Test]
public void 六列で並びアンカーがセルごとに付き同じ並びの描き直しでセルを作り直さない() { /* 7 件目の top がセル 1 の bottom より下・TutorialAnchorRegistry.Resolve("recipe.item-3") が Unique・同じ id 列の Render で AreSame */ }
```

`ItemListScrollGeometryTest.cs`（`Items` 列と同じ大きさの親に載せる。パネルは 378×452）:

```csharp
/// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">スクロール領域はパネル本文いっぱいで、既定件数では溢れずバーも出ない</web-test>
[Test]
public void 視口は本文いっぱいで既定件数では溢れずバーも出ない()
{
    var view = MountList(ListOf(30));
    var viewport = view.Q<GameScrollView>("item-list-scroll").contentViewport.worldBound;
    var body = view.Q<GamePanel>("item-list-panel").contentContainer.worldBound;
    var grid = view.Q<SlotGrid>("item-list-grid").worldBound;
    Assert.AreEqual(12f, viewport.yMax - body.yMax, 1f);                   // Web: viewportBottom − bodyBottom ≈ 12（clip-inset）
    Assert.Greater(viewport.yMax - grid.yMax, 100f);
    Assert.AreEqual(DisplayStyle.None, view.Q<GameScrollView>("item-list-scroll").verticalScroller.resolvedStyle.display);
}

/// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">パネルを超える件数で初めて縦バーが出る。横は常に出ない</web-test>
[Test]
public void パネルを超える件数で初めて縦バーが出て横は出ない() { /* 42 件（7 段）で非表示・48 件（8 段）で縦が表示・どちらも横は非表示 */ }

/// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">7段は溢れず、最下段までスクロールしても最終段に逃げが残る</web-test>
[Test]
public void 七段は溢れず最下段までスクロールしても最終段の下に逃げが残る() { /* 42 件で溢れない。60 件で最下段までスクロールして viewport.yMax − 最終段のセル.yMax ≥ 12 */ }
```

`GameScrollViewKnobTest.cs`:

```csharp
[Test]
public void つまみの既定は今の値で上書きすると溝つまみの色とバーの位置が変わる()
{
    var scroll = Mount(new GameScrollView("knob"));
    var scroller = scroll.verticalScroller;
    Assert.AreEqual(0f, scroller.resolvedStyle.top);                      // 既定
    scroll.AddToClassList("game-scroll-view-knob-probe");                 // テスト用 USS（ComponentTokenProbe.uss と同じ置き方）で 5 本を上書き
    simulate.FrameUpdate();
    Assert.AreEqual(12f, scroller.resolvedStyle.top);
    Assert.AreEqual(12f, scroller.resolvedStyle.right);
    Assert.AreEqual(new Color(0, 0, 0, 0), scroller.Q(className: "unity-base-slider__tracker").resolvedStyle.backgroundColor);
}

[Test]
public void 祖先の要素で上書きしても中のGameScrollViewのバーの位置が変わる()
{
    // 画面は部品の外側の要素（RecipeListScrollView の外枠など）でつまみを上書きするので、継承で中まで届くことを見る
    // Screens override the knobs on an outer element (e.g. the RecipeListScrollView shell), so check it inherits down to the inner view
    var outer = new VisualElement();
    outer.AddToClassList("game-scroll-view-knob-probe");
    var scroll = new GameScrollView("knob-inner");
    outer.Add(scroll);
    Mount(outer);
    simulate.FrameUpdate();
    Assert.AreEqual(12f, scroll.verticalScroller.resolvedStyle.top);
    Assert.AreEqual(12f, scroll.verticalScroller.resolvedStyle.right);
}
```

- [ ] **Step 2: 実装を書く**

`DerivedTokens.uss` の「S4-recipe」ブロック（`:root`。既定＝今の値なので見た目は変わらない。要素に既定を書くと祖先の上書きを潰すので `:root` に置く。既存の `--scrollarea-scrollbar-size`・`--game-scroll-view-thumb-hover` と同じ置き場）:

```css
    --game-scroll-view-track-face: var(--gauge-track);       /* GameScrollView の溝の既定（A-167） */
    --game-scroll-view-thumb-face: var(--bevel-c2);          /* GameScrollView のつまみの既定 */
    --game-scroll-view-scrollbar-inset-top: 0px;             /* 縦バーの上の逃げの既定 */
    --game-scroll-view-scrollbar-inset-bottom: 0px;
    --game-scroll-view-scrollbar-inset-right: 0px;
```

`GameScrollView.uss` の変更（`.game-scroll-view` には何も足さず、縦バーと溝・つまみがつまみを参照する）:

```css
.game-scroll-view .unity-scroll-view__vertical-scroller {
    top: var(--game-scroll-view-scrollbar-inset-top);
    right: var(--game-scroll-view-scrollbar-inset-right);
    bottom: var(--game-scroll-view-scrollbar-inset-bottom);
    background-color: var(--game-scroll-view-track-face);
    /* position・margin・border-width は既存のまま */
}
.game-scroll-view .unity-scroller .unity-base-slider__dragger {
    background-color: var(--game-scroll-view-thumb-face);
    /* 既存のほかの行はそのまま */
}
```

（Web の縦バーの `bottom: clip-inset + --sa-corner-width` の corner は、横バーを出さない UITK では 0 なので `inset-bottom` は clip-inset だけ。差は 0 px）

`ItemListCellView.cs`:

```csharp
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Icons;
using Core.Master;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 一覧の 1 セル（Web の data-item-id の div と catalog の ItemSlot）。アンカー recipe.item-<id> を持つ
    /// One list cell (the Web data-item-id div with a catalog ItemSlot); carries the anchor recipe.item-<id>
    /// </summary>
    public sealed class ItemListCellView : VisualElement
    {
        private readonly ItemSlot _slot;
        public ItemId ItemId { get; }

        public ItemListCellView(ItemId itemId)
        {
            ItemId = itemId;
            name = $"item-list-cell-{itemId.AsPrimitive()}";
            AddToClassList("item-list-cell");
            _slot = new ItemSlot($"item-list-slot-{itemId.AsPrimitive()}");
            Add(_slot);
            TutorialAnchor.Mark(this, AnchorIds.RecipeItem(itemId.AsPrimitive()));
        }

        public void SetContent(UiIcon icon, int craftableCount)
        {
            // catalog の枠は数 0 でバッジを出さない（C3-a の ItemSlot が判定する）
            // A catalog cell shows no badge at 0 (C3-a ItemSlot decides)
            _slot.SetContent(new ItemSlotContent(icon, craftableCount, new ItemSlotMarks(false, true, false), null, null, null));
        }
    }
}
```

`ItemListDragScroll.cs`:

```csharp
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.Gesture;
using Client.Ui.State.Recipe.Screen;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 一覧の掴みスクロール（Web useDragScroll の写し）。タップはセルを選び、ドラッグはスクロールだけ
    /// Grab-drag scrolling for the list (port of the Web useDragScroll); a tap selects the cell, a drag only scrolls
    /// </summary>
    public sealed class ItemListDragScroll : IUiPressGestureHandler
    {
        private readonly GameScrollView _scroll;
        private readonly RecipeItemSelection _selection;
        private float _startScrollTop;

        public ItemListDragScroll(GameScrollView scroll, RecipeItemSelection selection)
        {
            _scroll = scroll;
            _selection = selection;
        }

        public void OnPressStarted(Vector2 panelPosition)
        {
            _startScrollTop = _scroll.scrollOffset.y;
        }

        public void OnDragMoved(UiPressMove move)
        {
            // 押した点からの移動ぶんだけ逆向きに送る（内容が指に付いて動く）
            // Scroll opposite to the movement from the press point (content follows the pointer)
            _scroll.SetScrollOffset(UiDragThreshold.NextScrollTop(_startScrollTop, move.FromStart.y));
        }

        public void OnDragEnded(Vector2 panelPosition)
        {
        }

        public void OnTapped(VisualElement pressTarget)
        {
            // silent-ok: セルの外（隙間・余白）のタップは Web と同じく何もしない / ItemListDragScrollTest がセル上のタップを見る
            var cell = pressTarget as ItemListCellView ?? pressTarget?.GetFirstAncestorOfType<ItemListCellView>();
            if (cell == null) return;
            _selection.Select(cell.ItemId);
        }

        public void OnAborted(bool dragged)
        {
        }
    }
}
```

（押下の対象はセルの子〈スロットの中の要素〉が普通だが、セル自身のこともあるので両方を見る）

`ItemListView.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.Gesture;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe
{
    /// <summary>
    /// アイテム一覧のパネル（Web ItemListPanel の写し）。6 列・掴みスクロール・クラフト可能数のバッジ
    /// Item list panel (port of the Web ItemListPanel); six columns, grab scrolling and craftable-count badges
    /// </summary>
    public sealed class ItemListView : VisualElement
    {
        private const int Columns = 6;
        private readonly GameContentPresentation _content;
        private readonly SlotGrid _grid = new("item-list-grid", Columns);
        private readonly List<ItemListCellView> _cells = new();
        private readonly GameScrollView _scroll;

        public ItemListView(GameContentPresentation content, RecipeItemSelection selection)
        {
            _content = content;
            AddToClassList("item-list");
            var panel = new GamePanel("item-list-panel", GamePanelFrame.Default);
            panel.SetTitle(new UiText(LocalizationKeys.Ui.Recipe.CatalogTitle));

            // 本文いっぱいのスクロール領域と、その視口に掴みスクロールを付ける（バーの上の押下は拾わない）
            // A body-filling scroll area with grab scrolling on its viewport (presses on the bar are not taken)
            _scroll = new GameScrollView("item-list-scroll");
            _scroll.AddToClassList("item-list__scroll");
            _scroll.Add(_grid);
            new UiPressGesture(_scroll.contentViewport, new ItemListDragScroll(_scroll, selection));
            panel.Add(_scroll);
            Add(panel);
        }

        public void Render(ItemListViewModel vm)
        {
            // 並びが変われば作り直し、同じなら数だけ当て直す
            // Rebuild when the order changes, otherwise only re-apply counts
            if (!SameItems(vm.Cells)) Rebuild(vm.Cells);
            for (var i = 0; i < vm.Cells.Count; i++) _cells[i].SetContent(_content.Item(vm.Cells[i].ItemId), vm.Cells[i].CraftableCount);
        }

        public void SetShown(bool shown)
        {
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

            // 開き直したら先頭から（Web は画面を出るとアンマウントし、スクロール位置を持ち越さない）
            // Reopen at the top (Web unmounts on leaving the screen and does not carry the scroll position)
            if (!shown) _scroll.SetScrollOffset(0f);
        }

        private bool SameItems(IReadOnlyList<ItemListCell> next)
        {
            if (next.Count != _cells.Count) return false;
            for (var i = 0; i < next.Count; i++) if (next[i].ItemId != _cells[i].ItemId) return false;
            return true;
        }

        private void Rebuild(IReadOnlyList<ItemListCell> next)
        {
            // 解放で並びが変わったときだけセルを作り直す
            // Recreate the cells only when an unlock changed the order
            _grid.ClearItems();
            _cells.Clear();
            foreach (var cell in next)
            {
                var view = new ItemListCellView(cell.ItemId);
                _cells.Add(view);
                _grid.AddItem(view);
            }
        }
    }
}
```

（`ItemListCell` は State の VM のセル、`ItemListCellView` は View のセル。名前を分けて衝突を避ける）

`ItemList.uss`:

```css
/* web: src/features/recipe/panels/ItemListPanel.tsx（style・GRID_STYLE・ScrollArea scrollbarSize）・ItemListPanel.module.css */
.item-list {
    align-self: flex-start;
    align-items: flex-end;
}
.item-list #item-list-panel {
    width: 378px;
    height: 452px;
    --panel-top: -6.821px;
    --panel-bottom: -9.17px;
    --panel-left: -1.04px;
    --title-shift-x: 1.57px;
    --title-scale-x: 0.963;
    --title-scale-y: 0.861;
}
.item-list__scroll {
    --scrollarea-scrollbar-size: 4px;
    --game-scroll-view-track-face: rgba(0, 0, 0, 0);
    --game-scroll-view-thumb-face: var(--item-list-scroll-thumb-face);
    --game-scroll-view-scrollbar-inset-top: var(--tutorial-anchor-clip-inset);
    --game-scroll-view-scrollbar-inset-bottom: var(--tutorial-anchor-clip-inset);
    margin-right: 4.435px;
}
.item-list #item-list-grid {
    --slot-size: 46.144px;
    --slot-grid-gap: 8.656px;
    --slot-grid-row-gap: 8.896px;
    --icon-pad: 5px;
    --count-bottom: -1px;
    --count-font-size: 16px;
    --count-letter-spacing: 1.92px;
    padding-bottom: var(--item-list-count-bleed);
}
```

（色の直書き禁止なので `rgba(0, 0, 0, 0)` は `DerivedTokens.uss` の「S4-recipe」に `--item-list-scroll-track-face: rgba(0, 0, 0, 0);` を足して `var()` で受ける。`margin-left: calc(-3.561498px - clip-inset + 3.131498px)`・`margin-top: calc(12px - clip-inset)`・`margin-bottom: -clip-inset` と視口の `padding-top/bottom/left: clip-inset` は calc なので `ItemListView` が `UssCustomStyleReader` で `--tutorial-anchor-clip-inset` を読んで当てる〈前例 C4-e〉。`--count-letter-spacing: 0.12em` は 16px で 1.92px を UITK の「大きさ/100 × 値」で効く値へ換算する〈生成器の I-fix1 の式。Task 10 で実測〉）

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Recipe.ItemList|GameScrollView"`
Expected: ErrorCount 0 / 新規 11 件（一覧 9・つまみ 2）と既存の `GameScrollViewTest` が PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView/GameScrollView.uss moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/ItemList moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Controls/GameScrollViewKnobTest.cs
git commit -m "feat(uitk): アイテム一覧のパネルと掴みスクロールを作り、GameScrollView に溝・つまみ・位置のつまみを足す（S4-recipe）"
```

### Task 10: Presenter・登録・共有区画・チュートリアルとツールチップ

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/RecipeScreenPresenter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/RecipeGameUiRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 1 行）
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に 2 行）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs`（自分の 2 行を消す）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/Overlay/{RecipeTutorialClipTest.cs,RecipeSlotTooltipTest.cs}`

**Interfaces:**
- Consumes: Task 5〜9 の Produces、`StageColumnsLayout`、`UiRoutingState`、`GameContentPresentation`、`UiCommandResultToaster`、S2-tutorial の測定（テストだけ）、C3-b の `HoverTooltipTestStage`（テストだけ）
- Produces: `public sealed class RecipeScreenPresenter : IInitializable, IDisposable { public RecipeScreenPresenter(StageColumnsLayout columns, UiRoutingState routing, RecipeScreenModel model, GameContentPresentation content, IRecipeCommands commands, UiCommandResultToaster toaster, RecipeItemSelection selection); }`、`public static class RecipeGameUiRegistration { public static void Register(IContainerBuilder builder); }`

- [ ] **Step 1: テストを書く**

`RecipeTutorialClipTest.cs`（舞台と同じ 1280×720 に `ItemListView`・`RecipeViewerView` を載せ、S2-tutorial の `TutorialAnchorMeasure.Measure(root, root, id)` で `Ready(rect, clip)` を取り、`TutorialHighlightLabelPlacement.ShowsLabel`・`TutorialHighlightClip.TryComputeInset(rect.Expand(padding), clip, glow, out _)` で Web の判定を見る）:

```csharp
/// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">溢れていない一覧の最上段でもチュートリアルのラベルが出る</web-test>
[Test]
public void 溢れていない一覧の最上段でもラベルが出る() { /* 30 件。recipe.item-<先頭> が Ready で ShowsLabel が真 */ }

/// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">アンカーがスクロールで隠れ始めたらラベルを引っ込め、完全に隠れたら枠も消す</web-test>
[Test]
public void 隠れ始めたらラベルを引っ込め完全に隠れたら枠も消える() { /* 120 件。scroll 20 で ShowsLabel 偽・TryComputeInset 真、scroll 300 で TryComputeInset 偽 */ }

/// <web-test file="e2e/tests/recipe/itemListScroll.spec.ts">クリップ境界はグリッドからハイライトの逃げぶん離れ、枠が削られない</web-test>
[Test]
public void 一覧のクリップ境界はグリッドから逃げぶん離れる() { /* clip.Left ≤ grid.xMin − (padding + glow)・clip.Top ≤ grid.yMin − (padding + glow) */ }

/// <web-test file="e2e/tests/recipe/recipeListScroll.spec.ts">クリップ境界はエントリからハイライトの逃げぶん離れ、最下段までスクロールしても保たれる</web-test>
[Test]
public void レシピ列のクリップ境界は最下段までスクロールしても逃げぶん離れる() { /* 8 件。先頭のボタンと、最下段までスクロールした末尾のエントリで clip の上下右が padding+glow 以上離れる */ }
```

`RecipeSlotTooltipTest.cs`（`HoverTooltipTestStage` と同じ組み立てで `HoverTooltipHost` を舞台に付け、`RecipeViewerView` を載せる）:

```csharp
/// <web-test file="e2e/tests/recipe/slotTooltip.spec.ts">ツールチップはMantine既定の白い角丸ではなくCursorTooltipと同じ面を使う</web-test>
[Test]
public void 素材スロットのツールチップはHoverの面で出る() { /* 素材スロットへホバー → 出た面が TooltipFace（TooltipFaceLook.Hover のクラス）で角丸 0 */ }

/// <web-test file="e2e/tests/recipe/slotTooltip.spec.ts">祖先のスクロールでツールチップを引っ込め、ポインタを動かせば開き直す</web-test>
[Test]
public void 祖先のスクロールで引っ込みポインタを動かすと開き直す() { /* 一覧のセルにホバー → SetScrollOffset → 面が消える → PointerMove → 再表示 */ }
```

- [ ] **Step 2: 実装を書く**

`RecipeScreenPresenter.cs`:

```csharp
using System;
using Client.Ui.Content;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Commands;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Routing;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Recipe
{
    /// <summary>
    /// ビューアと一覧を起動時に舞台の Viewer・Items 列へ載せ、インベントリ画面のときだけ出す（Web App.tsx の screen === "playerInventory"）
    /// Mounts the viewer and list on the Viewer and Items stage columns at startup and shows them only on the inventory screen (Web App.tsx)
    /// </summary>
    public sealed class RecipeScreenPresenter : IInitializable, IDisposable
    {
        private readonly StageColumnsLayout _columns;
        private readonly UiRoutingState _routing;
        private readonly RecipeScreenModel _model;
        private readonly RecipeEntryPartsContext _context;
        private readonly CompositeDisposable _subscriptions = new();
        private RecipeViewerView _viewer;
        private ItemListView _itemList;

        public RecipeScreenPresenter(StageColumnsLayout columns, UiRoutingState routing, RecipeScreenModel model, GameContentPresentation content, IRecipeCommands commands, UiCommandResultToaster toaster, RecipeItemSelection selection)
        {
            _columns = columns;
            _routing = routing;
            _model = model;
            _context = new RecipeEntryPartsContext(content, commands, toaster, selection);
        }

        public void Initialize()
        {
            // 2 つの View を列へ載せ、モデルと画面を購読する
            // Mount both views on their columns and subscribe to the model and the screen
            _viewer = new RecipeViewerView(_context);
            _itemList = new ItemListView(_context.Content, _context.Selection);
            _columns.Get(StageColumn.Viewer).Add(_viewer);
            _columns.Get(StageColumn.Items).Add(_itemList);
            _model.Viewer.Subscribe(_viewer.Render).AddTo(_subscriptions);
            _model.ItemList.Subscribe(_itemList.Render).AddTo(_subscriptions);
            _routing.Current.Subscribe(ApplyScreen).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _viewer?.RemoveFromHierarchy();
            _itemList?.RemoveFromHierarchy();
        }

        private void ApplyScreen(UiRoutingSnapshot snapshot)
        {
            var shown = snapshot.Screen == UiScreen.PlayerInventory;
            _viewer.SetShown(shown);
            _itemList.SetShown(shown);
        }
    }
}
```

`RecipeGameUiRegistration.cs`:

```csharp
using Client.Ui.Screens.Recipe;
using Client.Ui.State.Recipe.Catalog;
using Client.Ui.State.Recipe.Commands;
using Client.Ui.Screens.Recipe.Input;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Routing;
using VContainer;

namespace Client.Starter.Registration.GameUi
{
    /// <summary>
    /// レシピ画面（ビューアと一覧）の登録。全体スイッチ UITK のときだけ MainGameUiRegistration から呼ばれる
    /// Registration of the recipe screen (viewer and list); called from MainGameUiRegistration only when the switch is UITK
    /// </summary>
    public static class RecipeGameUiRegistration
    {
        public static void Register(IContainerBuilder builder)
        {
            // State: 選択・カタログ・合図・モデル・操作
            // State: selection, catalog, signals, model and commands
            builder.Register<RecipeItemSelection>(Lifetime.Singleton);
            builder.Register<RecipeViewerItemCatalog>(Lifetime.Singleton);
            builder.Register<GameRecipeScreenSignals>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<RecipeScreenModel>(Lifetime.Singleton);
            builder.Register<VanillaApiCraftRequestSender>(Lifetime.Singleton).As<ICraftRequestSender>();
            builder.Register<RecipeCommands>(Lifetime.Singleton).As<IRecipeCommands>();

            // Esc での選択解除は画面の遷移を購読する（routing の Current を渡す。前例 FoundationGameUiRegistration の StageScaleSource）
            // Esc clearing subscribes to screen transitions (pass routing.Current; precedent StageScaleSource in FoundationGameUiRegistration)
            builder.Register<InputManagerCloseKeyProbe>(Lifetime.Singleton).As<IUiCloseKeyProbe>();
            builder.Register(resolver => new RecipeSelectionCloseReset(resolver.Resolve<UiRoutingState>().Current, resolver.Resolve<IUiCloseKeyProbe>(), resolver.Resolve<RecipeItemSelection>()), Lifetime.Singleton).AsImplementedInterfaces();

            // Screens
            builder.RegisterEntryPoint<RecipeScreenPresenter>();
        }
    }
}
```

（`RegisterEntryPoint` か `Register(...).AsImplementedInterfaces()` かは既存の `*GameUiRegistration` の前例に合わせる。S4-alpha の `UnlockedMachineRecipeSource` は S4-alpha の登録が持つので、ここでは登録しない〈Task 1 で登録の有無を確かめ、全体スイッチ UITK で S4-alpha の登録が先に呼ばれることを確かめる〉）

`MainGameUiRegistration.cs` の画面登録区画（「画面登録区画: 1行ずつ呼び出しを足す」の下）に 1 行:

```csharp
            RecipeGameUiRegistration.Register(builder);
```

`GameUiTheme.tss` の `Screens` 区画の末尾に 2 行:

```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/Recipe/RecipeViewer.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Screens/Recipe/ItemList.uss");
```

`TutorialAnchorCoveragePending.cs` から `{ AnchorIds.RecipeCraftButton, "S4-recipe" },` と `{ AnchorIds.RecipeItemPrefix, "S4-recipe" },` の 2 行を消す（ファイル冒頭の規則「印を付けたレーンが同じ PR で自分の行を消す」）。

- [ ] **Step 3: コンパイルし、テストと起動を確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Recipe|Client.Tests.Ui.State.Recipe|UiThemeImportBundleTest|UiStyleConventionTest|UiLayerDependencyDirectionTest|LookExceptionLedgerTest|TutorialAnchorCoverageTest"` → `UiSystemEditorSwitch.Set(UiSystem.GameUi)` を EDC で実行してから PlayMode に入り Tab でインベントリを開き、`uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: ErrorCount 0 / 全部 PASS / Error ログ 0 件。EDC で `StageColumnsLayout.Get(StageColumn.Viewer)` の子に `recipe-viewer`、`Items` の子に `item-list-panel` があり、Tab で開いたときだけ `display: flex`

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/RecipeScreenPresenter.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/RecipeGameUiRegistration.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Recipe/Overlay
git commit -m "feat(uitk): レシピ画面を舞台の列へ載せて登録し、チュートリアルとツールチップの L1 を足す（S4-recipe）"
```

### Task 11: 色の実測と Web との並べ比べ（720p・2560×1080）

**Files:**
- 撮影の原本は `moorestech_client/.uloop/outputs/compare/S4-recipe/`（gitignore 下）。差の表は `done.md`
- Modify（直しが出たら）: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe/{RecipeViewer.uss,ItemList.uss}`・`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss`（「S4-recipe」ブロック）・`tools/uitk/webTokens/handWrittenDerivedRows.mjs`・`docs/uitk/translucent-tokens.md`・`docs/uitk/TODO.md`（LX-131〜135・「## 実測の結論」）

**Interfaces:**
- Consumes: 参照 `LOGS/uitk/reference-captures/web-a9d5123537/`・`web-0e81bbe3c3-2560x1080/` の `4-player-inventory.png`（未選択の案内・一覧が溢れてバーあり・バッジ無し）・`4-item-list-large.png`・`5-tutorial-recipe-item.png`（`manifest.json` で状態を確かめる）
- Produces: 4 状態 × 2 解像度の組、差の表（明るい半透明 2 本〈`--item-header-rule-face`・`--item-list-scroll-thumb-face`〉の式の値と参照の画素の差を含む）

- [ ] **Step 1: 参照に無い「選択あり」を master の使い捨て worktree の mock-host で撮る（E-22）**

```bash
moores-wt new uitk-ref-s4-recipe --from origin/master --no-editor
cd <作られた worktree の絶対パス>/moorestech_web/webui && pnpm install
```
`e2e/uitk-reference/` の撮影の台本（前例 C4-e の自撮り。`shotRunner.ts`）に 1 状態「`4-recipe-selected`」を足す: インベントリ画面、アイテム 102（クラフト 2 件〈…0001 は素材不足でボタン無効・…0004 は 0.4 秒〉）を選び、機械レシピのあるアイテム 7 に切り替えた状態と 2 枚（`4-recipe-selected-craft`・`4-recipe-selected-machine`）。長押し中の矢印は `page.mouse.down()` の 0.2 秒後に撮る（`4-recipe-holding`）。fixture は `e2e/mock-host/fixtures/recipeFixtures.ts` の既存の値を使う（無いものは足さない）。
```bash
SHA=$(git rev-parse --short=10 HEAD)
UITK_REFERENCE_OUT=<moorestech_logs の絶対パス>/uitk/reference-captures/web-$SHA-s4-recipe UITK_REFERENCE_PORT=<空きポート> node --import tsx e2e/uitk-reference/capture.ts
```
2560×1080 は `referenceViewport` を一時的に 2560×1080 にしてもう 1 回撮る（`git checkout -- e2e/uitk-reference/shotRunner.ts` で戻す）。撮り終えたら `cd - && moores-wt rm uitk-ref-s4-recipe --force --prune-branch`。`done.md` の「integrator への依頼」に「自撮りの参照 `web-<sha>-s4-recipe`（3 状態×2 解像度）を撮った。台帳『## 参照キャプチャ』への記載をお願いしたい」と書く

- [ ] **Step 2: 撮影の口を用意し、明るい半透明の式の値を参照と比べる**

S3-hotbar-modal の Task 11 Step 2 と同じ手順で、`GameUiPanelSettings.asset` の複製（ランタイムの Linear 合成）に 1280×720・2560×1080 の sRGB ARGB32 RenderTexture を付けた UIDocument（`s4r-compare-<w>`）を作り、言語を参照に揃える。背景は参照の画面から読んだ単色（`4-player-inventory.png` のビューアの外の地）にして差の表に「背景は世界でなく単色」と書く。まず装飾線（`--item-header-rule-face`）とつまみ（`--item-list-scroll-thumb-face`）を式の値（A-113）で描いて画素を読み、参照の同じ位置の画素と比べる。差が残れば α をトークンごとに選び直さず（A-113 が却下した方法）、差の表と `LX-133`（つまみ）・`LX-132`（装飾線）に「式 1.85 の明るい色の残差（LX-81 と同じ型）」として記帳する

- [ ] **Step 3: 4 状態を撮り、差の表を書く**

EDC で各ルートに 1280×720 の舞台と `StageColumnsLayout` を置き、`RecipeViewerView`・`ItemListView` を `Viewer`・`Items` 列へ載せ、PlayMode のマスタで参照と同じ状態の VM（`4-player-inventory`: 未選択・一覧は参照と同じ件数で溢れる・バッジ無し／`4-item-list-large`／`5-tutorial-recipe-item`: 一覧の 1 セルにアンカー〈S2-tutorial の枠が入っていれば枠ごと〉／`4-recipe-selected-*`・`4-recipe-holding`）を `Render` して撮る。見る点: パネルの外枠の位置（C4-e の実測でビューアの左端 x=530・上 168/169.6 at 720p。`translate(0.391px, -0.391px)` を含む）・幅 337.2/378・高さ 300/432.983/452・題の字の拡縮・グリッドの 6 列と隙間・バッジの字間・つまみの太さ 4 と位置（上下 12 の逃げ）・見出しの名前の字の太さと影・装飾線の消え方と菱形・エントリの間隔 10・矢印の上の秒数・ボタンの面と無効・機械名の省略。720p で 2px を超える差は直すか `LX-131`〜`LX-135` に記帳し、該当行に `/* look-exception: LX-n */`。範囲を超えるなら止めて指揮へ番号を求める。2560×1080 は `x' = 320 + 1.5x`・`y' = 1.5y` で 720p の位置と照合する

- [ ] **Step 4: 直しがあればテストを回してコミット**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Recipe|Client.Tests.Ui.State.Recipe|UiStyleConventionTest|LookExceptionLedgerTest"
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Recipe moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss tools/uitk/webTokens/handWrittenDerivedRows.mjs docs/uitk/translucent-tokens.md docs/uitk/TODO.md
git commit -m "fix(uitk): レシピ画面を Web との並べ比べで揃え、明るい半透明の式の値を参照と照らす（S4-recipe）"
```
（直しが無ければ台帳の「実測の結論」の 1 行と再生成だけをコミットする）

### Task 12: unityプレイ録画テスト（`uitk/uitk-recipe-craft`）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-recipe-craft.cs`

**Interfaces:**
- Consumes: プレイテスト DSL（`p.SetupDebugEnvironment`・`p.SkipOpeningSkitIfPlaying`・`p.ExitToGameScreen`・`p.PressKey`・`p.WaitUiState`・`p.ClickUi`・`p.UntilUiElement`・`p.Until`・`p.Screenshot`・`p.Note`・`p.Assert`・`PlaytestUiElementOps.FindUi`・`SemanticInput.MouseMoveTo`・`MouseButtonDown`・`MouseGlideTo`・`MouseButtonUp`）、前例 `SKILL/scenarios/uitk/uitk-grab-overlay.cs`（Tab で開く・系の主張）、`ILocalPlayerInventory`、`RecipeItemSelection`、`UiInputExclusivity.ActiveUiSystem`
- Produces: 録画 1 本（`result.json` が `Success: true`）

- [ ] **Step 1: 録画を書く**

流れ（200 行未満。ドラッグと座標は `#region Internal` のローカル関数）:
1. 冒頭で `p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}")` と `p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）")`
2. `p.SetupDebugEnvironment(new PlaytestEnvironmentConfig())` → `p.SkipOpeningSkitIfPlaying()` → `p.ExitToGameScreen()`。持ち物に、解放済みのクラフトレシピ 1 本（前例の木材→板材など、テスト用マスタで最初から解放されているもの。`CraftRecipeMaster` と解放表から選び、名前を `p.Note`）の素材をちょうど 2 回分入れる（サーバー側へ入れる DSL の実物に合わせる）
3. `p.PressKey(Key.Tab)` → `p.WaitUiState(UIStateEnum.PlayerInventory)` → `FindUi("recipe-viewer")`・`FindUi("item-list-panel")` が見つかり、`FindUi("recipe-viewer-hint")` が見える。`p.Screenshot("01-open")`
4. 一覧のセル `item-list-cell-<結果のアイテム>` を（溢れて見えなければ `ScrollDownUntilUiElementIsClickable` で）クリック → `FindUi("craft-button-<guid>")` が出る。`p.Screenshot("02-select")`
5. 別のセルの中心から上へ 60px ドラッグ（`MouseMoveTo`・`MouseButtonDown(0)`・`MouseGlideTo(…, 0.4f)`・`MouseButtonUp(0)`）→ 選択が変わらない（`craft-button-<guid>` がまだある）。`p.Screenshot("03-drag-keeps")`
6. ボタンの中心で `MouseButtonDown(0)` → `p.Until(結果アイテムの所持が 2 回分増える, 3 * craftTime + 2)` → ボタンが無効になる（素材切れ）→ `MouseButtonUp(0)`。`p.Screenshot("04-hold-craft")`。素材を 1 回分足し、押したまま 0.5×craftTime 待ってボタンの外へ `MouseGlideTo` → `craftTime` 待っても所持が増えない（外れで止まる）。`p.Screenshot("05-leave-stops")`
7. `p.PressKey(Key.Escape)` → インベントリが閉じる → `p.PressKey(Key.Tab)` → `recipe-viewer-hint` が見える（Esc で選択が外れた）。`p.Screenshot("06-esc-clears")`
8. Esc でなく Tab で閉じて開き直すと選択が残り、開いた直後（持ち物の変化を待たず 1 フレーム後）に `craft-button-<guid>` が見える（D-33。Web と同じ）。`p.Screenshot("06b-tab-reopen")`
9. 素材を 1 回分足し、クラフトボタンをクリックしてフォーカスを置いた状態で `Key.Enter` を craftTime＋1 秒押し続ける（DSL のキー押下保持。無ければ `SemanticInput` の KeyDown/KeyUp）→ 所持が 1 回分増える。`Key.Space` を押しても増えない。続けて `p.PressKey(Key.Tab)` → インベントリが閉じる（フォーカス移動に食われずゲームの Tab が先に効く＝D-31 の確かめ）。`p.Screenshot("07-enter-hold")`。Enter のキーイベントが UITK の要素に届かなければ止めて `done.md` に書き、指揮へ報告する

- [ ] **Step 2: 全体スイッチを GameUi にして回す**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
SKILL=.agents/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/uitk/uitk-recipe-craft.cs"
```
Expected: `result.json` が `Success: true`、Note に `系=GameUi`、録画 mp4 がある。失敗したら `result.json` と `uloop get-logs --project-path ./moorestech_client --log-type Error` を読み、原因を `done.md` に書いて直す（直したら L1 を回し、このステップの 2 行目から流し直す）

- [ ] **Step 3: 解放の合図で一覧が増えることを EDC で 1 回見る**

録画と同じ PlayMode で、未解放のクラフトレシピを 1 本選び（`IGameUnlockStateData`）、サーバー側で解放（前例 `PlaytestBlockOps.UnlockBlockServerSide` と同じ層の API。無ければ debug コマンド）→ 1 秒後に `RecipeScreenModel.ItemList.Value.Cells` にその結果アイテムが入る（`RecipeViewerItemCatalog` の表示判定で `Show` になるもの）ことを EDC で読み、`done.md` の「## 証拠」に値を書く

- [ ] **Step 4: 証拠を書いてコミット**

`done.md` の「## 証拠」に `result.json` のパス・`Success`・録画 mp4 のパス・Note の `系=GameUi` を書く。録画の原本は gitignore 下のまま。テストで `Saves/` に作ったワールドは `list-work-saves.sh` で探して消す
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-recipe-craft.cs
git commit -m "test(uitk): レシピの選択・掴みスクロール・長押しクラフト・Esc の選択解除を UITK の録画で通す（S4-recipe）"
```

### Task 13: 処遇の表と名前の突き合わせ

**Files:**
- Modify: なし（突き合わせの結果は `done.md`。部分移植の行が要るときだけ `docs/uitk/disposition.md`）

**Interfaces:**
- Consumes: 写したテストのタグ一式（Enter の長押しは Task 6 の `CraftHoldKeyTest` が持つ）
- Produces: 束 4 の recipe の未対応 0

- [ ] **Step 1: 突き合わせる**

```bash
node tools/uitk/test-name-parity.mjs --bundle 4 --emit-missing
```
Expected: recipe の行（`src/features/recipe/`・`e2e/tests/recipe/`）の未対応が 0。終了コードが 2 でない（タグと処遇の表の二重・書式の誤りなし）。他レーンの束 4 の未対応は数えない（`done.md` に件数だけ書く）。部分移植の残り（`MachineRecipeEntry.localization` の alt、`slotTooltip` の Mantine 既定との比較の部分）は `done.md` の「## 部分移植の残り」に 1 行ずつ

- [ ] **Step 2: コミットする（`disposition.md` に部分移植の行を足したときだけ）**

```bash
git add docs/uitk/disposition.md
git commit -m "docs(uitk): レシピの部分移植の行を処遇の表に記し、束 4 の recipe の突き合わせを 0 にする（S4-recipe）"
```

### Task 14: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイルだけ

**Interfaces:**
- Consumes: Task 1〜13 のコミット一式（`origin/feature/uitk...HEAD`）
- Produces: レビュー記録 `../moorestech_logs/harness/` 配下（`$RUNDIR`）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。レビューが裁定・設計書と食い違う直し（Web と違う見た目へ寄せる・画面 USS に部品のクラスを書く・色を C# に書く・宣言した 5 本の外で部品や Foundation を書き換える・機械レシピの源を自前で作る〈D-3〉・長押しを `UiPressGesture` へ戻す〈D-5〉・Enter の長押しや `focusable=true` の例外を消す〈D-31〉）を提案したら採らず、`done.md` の「要裁定」に書く

- [ ] **Step 2: 自動適用があれば検査と録画を回し直してコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Recipe|Client.Tests.Ui.State.Recipe|GameScrollView|UiThemeImportBundleTest|UiStyleConventionTest|UiLayerDependencyDirectionTest|LookExceptionLedgerTest|TutorialAnchorCoverageTest"`
Expected: ErrorCount 0、全件 PASS。自動適用が `RecipeScreenModel`・`RecipeCommands`・`RecipeSelectionCloseReset`・Parts（長押し・掴みスクロール・エントリ）・View・Presenter・登録・シナリオの判定経路や条件式に触れたら、反映後のバイナリで Task 12 Step 2 を再走する（ユニットテストの通過は代替にならない）。USS・トークンに触れたら Task 11 Step 3 を回し直す。続けて、触ったファイルを個別に `git add` して `git commit -m "fix(uitk): S4-recipe の moores-code-review 反映"`（自動適用が無ければコミットしない）

### Task 15: feature/uitk への PR・取り込み・マージ・片付け（必須）

**Files:**
- Modify: なし（Task 14 後の HEAD をそのまま出す）

**Interfaces:**
- Consumes: Task 14 後の HEAD
- Produces: `origin/feature/uitk` のマージコミット。以後 S4-gamma（ビューア＋機械 UI＋インベントリの L3）が使える

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`）。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ。報告ページは `~/hermes-agent/data/services/pr-review/publish/README.md` の手順で、manifest は次の形（`file` は絶対パス。並べ比べは Web を先に同じ `group` へ置く）:
```json
{
  "head": "<HEAD 10 桁>",
  "summary": [
    "レシピビューア（Viewer 列）とアイテム一覧（Items 列）を UITK に移し、C4-c のレシピ部品・C3-a のスロット・C4-a の液体スロットで Web と同じ見た目にした",
    "長押しクラフトをパネルのスケジューラで回し、エントリを GUID で使い回して持ち物の変化で長押しが切れないようにした",
    "一覧の掴みスクロール・Esc の選択解除・チュートリアルのアンカー 2 種を移し、録画 uitk/uitk-recipe-craft で通した"
  ],
  "media": [
    {"file": "<uitk-recipe-craft の recording.mp4>", "caption": "uitk/uitk-recipe-craft（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<web-a9d5123537/4-player-inventory.png>", "caption": "Web 4-player-inventory 1280×720", "group": "4-player-inventory"},
    {"file": "<compare/S4-recipe/uitk-4-player-inventory-1280x720.png>", "caption": "UITK 4-player-inventory 1280×720", "group": "4-player-inventory"},
    {"file": "<web-0e81bbe3c3-2560x1080/4-player-inventory.png>", "caption": "Web 4-player-inventory 2560×1080", "group": "4-player-inventory"},
    {"file": "<compare/S4-recipe/uitk-4-player-inventory-2560x1080.png>", "caption": "UITK 4-player-inventory 2560×1080", "group": "4-player-inventory"},
    {"file": "<web-a9d5123537/4-item-list-large.png>", "caption": "Web 4-item-list-large 1280×720", "group": "4-item-list-large"},
    {"file": "<compare/S4-recipe/uitk-4-item-list-large-1280x720.png>", "caption": "UITK 4-item-list-large 1280×720", "group": "4-item-list-large"},
    {"file": "<web-a9d5123537/5-tutorial-recipe-item.png>", "caption": "Web 5-tutorial-recipe-item 1280×720", "group": "5-tutorial-recipe-item"},
    {"file": "<compare/S4-recipe/uitk-5-tutorial-recipe-item-1280x720.png>", "caption": "UITK 5-tutorial-recipe-item 1280×720", "group": "5-tutorial-recipe-item"},
    {"file": "<web-…-s4-recipe/4-recipe-selected-craft.png>", "caption": "Web（自撮り）レシピ選択・クラフト 1280×720", "group": "4-recipe-selected"},
    {"file": "<compare/S4-recipe/uitk-4-recipe-selected-craft-1280x720.png>", "caption": "UITK レシピ選択・クラフト 1280×720", "group": "4-recipe-selected"},
    {"file": "<web-…-s4-recipe/4-recipe-selected-machine.png>", "caption": "Web（自撮り）機械レシピ 1280×720", "group": "4-recipe-selected"},
    {"file": "<compare/S4-recipe/uitk-4-recipe-selected-machine-1280x720.png>", "caption": "UITK 機械レシピ 1280×720", "group": "4-recipe-selected"},
    {"file": "<web-…-s4-recipe/4-recipe-holding.png>", "caption": "Web（自撮り）長押し中 1280×720", "group": "4-recipe-holding"},
    {"file": "<compare/S4-recipe/uitk-4-recipe-holding-1280x720.png>", "caption": "UITK 長押し中 1280×720", "group": "4-recipe-holding"}
  ],
  "verification": [
    "Unityコンパイル: エラー0",
    "L0/L1: State.Recipe・Screens.Recipe・GameScrollViewKnobTest の件数と PASS、L2（Conventions・Theme・Layers・Anchor・LookException）PASS",
    "test-name-parity 束 4: recipe の未対応 0・書式の誤りなし（Enter の長押しも CraftHoldKeyTest で写した）",
    "明るい半透明（式 1.85・A-113）と参照の画素の差: 装飾線 <差>・つまみ <差>",
    "unityプレイ録画テスト: uitk-recipe-craft PASS（全体スイッチ GameUi）"
  ],
  "notes": ["差の表と証拠は moorestech_logs/harness/uitk/S4-recipe/done.md。2560×1080 の自撮りの組も同じ group に追加する"]
}
```
`python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <PR番号> <manifest.json>` が失敗したら、報告 URL 無しで完了にしない

- [ ] **Step 2: feature/uitk の進みを取り込み、compile・テストを回して G1 緑を見る**

取り込み後に登録の本数を数え直す（`ls moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/*.cs | wc -l`）。10 を超えていれば integrator の再編が入るまで Step 3 のマージをしない（Task 1 Step 1 の関門と同じ）。

```bash
git fetch origin
git merge --no-ff --no-edit origin/feature/uitk
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.Ui.Screens.Recipe|Client.Tests.Ui.State.Recipe|GameScrollView|UiThemeImportBundleTest|UiStyleConventionTest|UiLayerDependencyDirectionTest|LookExceptionLedgerTest|TutorialAnchorCoverageTest"
git push
gh pr checks <PR番号>
```
Expected: 共有区画（`GameUiTheme.tss` の `Screens` 区画・`MainGameUiRegistration.cs` の画面登録区画・`DerivedTokens.uss` の名札付きブロック・`handWrittenDerivedRows.mjs` の末尾・`localization.csv`〈取り込み後に `npm run gen:i18n` をやり直す〉・`UiCommandRejectReason.cs` の末尾と 3 switch・`docs/uitk/TODO.md` の節の末尾・`docs/uitk/disposition.md`）の衝突は両方の行を採る。`TutorialAnchorCoveragePending.cs` の衝突は消した側を採る。`GameScrollView.uss` に他レーンが同名でないつまみを足していれば両方を採り、同じ役のつまみがあれば相手の名前へ寄せて `done.md` に書く。G1（UITK Web follow gate）が緑になるまで待つ（前景で `gh pr checks <PR番号> --watch`）。CI の `Unity Test - client-remainder` の `UnityCursorTooltipScreenTest` 1 件は CI だけで出る既存の失敗で、マージ条件は G1 緑

- [ ] **Step 3: マージして片付ける**

```bash
gh pr merge <PR番号> --merge
bd close <id> --reason "S4-recipe を feature/uitk へマージ（PR #<番号>）"
```
`done.md` の 1 行目を「状態: 完了」にし、PR・マージコミット・録画・差の表の場所を書く。続けて `moores-wt rm <worktree 名>`（Editor ごと畳む）。Squash は使わない

---

## Self-Review

1. **Requirements coverage**: R-1→Task 2・5、R-2→Task 3・5、R-3→Task 4、R-4→Task 9・11、R-5→Task 9、R-6→Task 8・11、R-7→Task 6、R-8→Task 6・12、R-9→Task 7、R-10→Task 5・12、R-11→Task 10、R-12→Task 11、R-13→Task 12、R-14→Task 4・8・10、R-15→Task 13、R-16→Task 14・15。漏れなし
2. **Placeholder scan**: `TBD`・`TODO`・「後で」は無い。`/* … */` で中身を書いたテストは、主張（何を測り何と比べるか）を 1 行で書いたもので、実装者が Web の spec の同名の it を読んで値を写す（数値は表と Requirements にある）
3. **Type consistency**: `RecipeSelection`（`None`／`Of`／`NoRecipeSelection`／`ItemRecipeSelection`）・`RecipeItemSelection.Select/Clear/Current`・`RecipeViewerNoSelection.Instance`・`RecipeViewerItemSelected(itemId, entries)`・`CraftRecipeEntryViewModel(recipe, materials, craftable, carriesTutorialAnchor)`・`RecipeEntryPartsContext(content, commands, toaster, selection)`・`CraftHoldDriver(scheduleOwner, row, commands, toaster)`・`ItemListCell`（VM）と `ItemListCellView`（View）を全 Task で同じ名前で使った
4. **保留・縮退経路**: (a) 素材が尽きると長押しは止まり、素材が戻っても再開しない（Web と同じ。もう一度押せば始まる）。(b) 機械レシピの源が空（S4-alpha 前）でも組み立ては空の列で動く。(c) 未知の型のアイテムは隠して警告 1 回（一覧に出ないことは L0 で、警告はログで気づく）。(d) Esc の判定は「遷移と同じフレームの CloseUI」で、遷移しない Esc（インベントリ以外で押す）は選択に触れない。最小構成（エントリ 0 件＝`noRecipes`・一覧 0 件・素材 0 件のレシピ〈`Craftable` は真〉）は L1 の `RecipeViewerViewTest`・L0 の `RecipeViewModelFactoryTest` で 0 件の列を 1 件ずつ足して見る
5. **決定性を根拠にした選択**: エントリの順は `CraftLogic.BuildRecipeEntries`（L0-s4a・Web と同じ）に任せ、このレーンで新しい順序の規則は作らない。アンカーは「先頭がクラフト」の 1 規則（Web と同じ）
6. **呼び出し側への責務漏出**: クラフトの可否と不足は State（`RecipeViewModelFactory`）が決め、View は VM を描くだけ。クラフトの検証は `RecipeCommands` の 1 か所で、長押しの部品は結果をトースターへ渡すだけ。素材のツールチップの文は View が `MaterialTooltipText.Create` を呼ぶ（名前の解決が Content 層にあるため。D-13）

## 判断記録（ADR）

設計の正本は裁定 `.decisions/2026-11-06-*` と設計書 `docs/uitk-migration/execution/component-architecture.md`（ADR は `docs/adr/` の UITK 関連を設計書が引く）。planning 中の判断は次のとおり（出所はすべて agent 既定。シミュレーターの指摘で直したものは末尾に「シミュレーター予測」と書く）。

- **D-1 Model を State に置き Web の topic の源を直に読む**: カタログ（`CraftRecipeCatalog`・`RecipeViewerItemCatalog`）は `Client.WebUiHost` の topic と同じ源（マスタ＋解放状態＋`ItemRecipeViewerDataContainer`）を読む。前例 S3-hotbar-modal の Model（`ClientHotbarDatastore` を直に購読）。Web の橋の DTO を経由しない（UITK 切替の日に橋は消える）
- **D-2 Presenter 1 本で 2 つの View**: 2 つのパネルは同じ条件（`PlayerInventory`）で出入りし、同じモデルと選択を共有するので Presenter を分けない。分けると購読と条件が 2 か所に分かれる
- **D-3 機械レシピの源は S4-alpha が作り、このレーンは使うだけ**: 指揮の割り当て（S4-machine も同じ源を使う 2 機能目のため中立の置き場 `Client.Ui.State/Recipe/Source/`）。製作上限の解放（`ILevelInheritanceCraftCapLookup.OnCraftCapUnlocked`）での組み直しも源の側が持つ
- **D-4 依存に C4-a・S2-tutorial・S4-alpha を足す**: 機械レシピの液体（`FluidAmountSlot`）、`TutorialAnchorCoveragePending` の自分の行・切り抜きの判定（テスト）、機械レシピの源。実行計画 §1.4 の改め案に書く
- **D-5 長押しは `UiPressGesture` でなく画面の部品 `CraftHoldInput` で受ける**: C4-c の申し送りは「`UiPressGesture` を重ねる」だが、Web の長押しは押下・離し・外れ・取消の 4 事象だけで、タップとドラッグの分類（5px）が無い。`UiPressGesture` は押下で捕捉し、ボタンの外へ出ても中断しない（Web は外れで止める＝`recipe.spec.ts` 5 件目）。Button の `Clickable` が捕捉しているときは Leave が来ないので、移動の位置でも外れを読む
- **D-6 エントリは GUID で使い回し、別のアイテムを選んだら作り直す**: 持ち物の変化ごとに VM が来るので、作り直すと長押しのスケジューラと経過が消える（Web は React の key で使い回す）。選択の切り替えは Web の `key={selectedItemId}` の再マウント（スクロールが先頭へ）と同じく作り直す。GUID の並びが変わる（解放で増える）ときも作り直す（Web は key の一致する要素を残すが、解放とクラフトの同時は起きにくく差は長押しの中断だけ）
- **D-7 Esc の選択解除は「画面の遷移と同じフレームの CloseUI」**: UITK のキーイベントは届かない（A-83）。Web はゲーム層の keydown の Escape で外すが、ゲーム層の Esc はインベントリを閉じる（遷移する）ので、遷移と CloseUI の組で同じ時点になる。`Update()` を足さない
- **D-8 拒否の理由 2 つは良性でない**: UI からは解放済みのレシピしか出ないので、起きたら不整合。トーストとログで気づかせる（Web の橋も `invalid_recipe`・`recipe_locked` をエラーで返す）
- **D-9 `GameScrollView.uss` につまみ 5 本を足す（宣言した部品の変更）**: 一覧は溝が透明・つまみが白 88%・縦バーの上下に 12 の逃げ、レシピ列は上下右に 12 の逃げ（Web の局所上書き）。画面の USS は部品のクラス（`.unity-scroll-view__vertical-scroller` 等）を書けないので、部品につまみが要る。既定は今の値なので他の画面の見た目は変わらない。2 機能目は S4-machine のレシピ選択の列（同じ逃げ）になりうるので、`done.md` の「integrator への依頼」に「つまみ 5 本を足した。設計書 §2.4 の GameScrollView 行へ」と書く（前例: S3-hotbar-modal の `OutlinedCountLabel` の縁の色のつまみ A-108）
- **D-10 掴みスクロールはパネル px で送る**: Web は `clientY`（実画面 px）の移動を `scrollTop`（CSS px）へそのまま足すので、倍率が 1 でない画面では内容が指より速く／遅く動く。UITK は `UiPressMove.FromStart`（パネル px）を当てて内容を指に付ける。720p（倍率 1）では同じ。閾値（実画面 5px）は `UiPressGesture` が Web と同じに判定する
- **D-11 秒数はエントリを作るときに 1 回だけ入れる**: C4-c の約束（載せる前に `SetDuration`）。言語の変化は `UiText` の再解決で追従する（`CraftRecipeEntryViewTest` の言語の件で確かめる）。クラフト時間は GUID ごとに変わらない
- **D-12 ボタンの `title` と掴み中の `grabbing` カーソルは写さない**: クライアントのコードにオフスクリーンの CEF のツールチップ・カーソルの受け手が無く（`grep -rln "OnTooltip\|CursorChange\|SetCursor" moorestech_client/Assets/Scripts` が 0 件）、ゲーム内の Web では出ていない。見た目を Web と同じにする（裁定）ために足さない
- **D-13 素材のツールチップの文は View が組む**: `MaterialTooltipText.Create` は名前の文字列を受けるが、名前の解決（`GameContentPresentation.ItemName`）は Content 層にあり State から呼べない。モデルは言語の変化で組み直し、View が描き直しのたびに名前を解いて作る。A-31（言語に追従する文は `LocalizedTextBinding` 1 本）は部品の中で `Localize` を購読しない規則で、この形は State の Model が購読する前例 `ModeHudModel` と同じ。名前を文字列で焼き込む `MaterialTooltipText`（C3-a の既存 API）は `LocalizedTextBinding` の束縛 1 本で表せないので、Model の組み直しで追う（Phase 2.6 検査7。agent 既定）
- **D-14 カタログは State に置き、`ItemRecipeViewerDataContainer` を使い回す**: 表示判定（Show／Hide／UnknownType）は既存の評価器 1 か所。未知の型の警告 1 回は topic と同じ振る舞い
- **D-15 Presenter の L1 は持たない**: 列へ載せることと画面での出し入れは Task 10 の EDC と録画で見る（前例 S2-hud・S3-hotbar-modal の Presenter）。`UiRoutingState` は `UIStateControl` を要し L1 で作れない
- **D-16 Tab で閉じたときは選択を残す**: Web の選択はグローバルなストアで、Esc 以外では消えない。録画 R-13 の流れ 8 で見る
- **D-17 見出しの装飾線は頂点カラーのメッシュで描く**: 8 つの色止め（Web の linear-gradient と同じ位置）を 16 頂点で張る。`Painter2D` のグラデは止めの数と補間が Web と一致する保証が無い。前例 `CrosshairShadowRing` の頂点カラー
- **D-18 クラフト要求は `ICraftRequestSender` の口を通す**: L0 で送った GUID を記録するため（前例 `ICursorTooltipScreen`・`IGrabOverlayPointer` の偽を差す口）
- **D-19 長押しの経過はパネルのスケジューラの `TimerState.deltaTime`**: AGENTS の時間の規則（`GameUpdater` のティック）はサーバーのゲームロジックが対象で、これはクライアントの UI の経過（Web の `requestAnimationFrame` の写し）。`Time.deltaTime` は使わない
- **D-20 録画は 1 本、L3 は持たない**: ビューア＋機械 UI＋インベントリの同時の L3 は S4-gamma（実行計画 §1.4）。このレーンの録画は Tab で開く・選ぶ・掴みスクロール・長押し・外れ・Esc を 1 本で見る（ランタイム挙動に触れるので録画は必須）
- **D-21 一覧のセルの View は `ItemListCellView`**: State の VM のセル `ItemListCell` と名前がぶつかるため
- **D-22 Esc の選択解除は画面の層に置く**: ref-uitk-design §10「画面のキー処理（ESC 等）は Presenter か UIState」。`RecipeSelectionCloseReset`・`IUiCloseKeyProbe` は `Client.Ui/Screens/Recipe/Input/`（`Client.Ui` は `Client.Input` を参照済み）。Presenter に直に書かず小さな部品に分けるのは L0 で遷移と CloseUI の組を確かめるため（前例 `ICursorTooltipScreen` と偽が同じ画面の層）（Phase 1〜2.5 違反 #1 の修正）
- **D-23 閉じたら中身とスクロールを初期化する**: Web は `PlayerInventory` を出ると 2 つのパネルをアンマウントするので、開き直すと長押しが止まりスクロールが先頭。UITK は `SetShown(false)` で要素を残すので、ビューアは中身を捨て（外れたエントリのスケジューラも止まる）、一覧はスクロールを 0 に戻す。選択は残す（D-16）
- **D-24 明るい半透明も式 1.85 の値**: A-113 が明るい色の補正も指数 1.85 に決め、トークンごとの α の選び直しを却下している。装飾線 0.6315・つまみ 0.7894・名前の影 0.7717（`correctAlphaForLinearBlend` の実値）。参照との残差は LX-132・133 に記帳（Phase 2.6 検査7。plan を ADR に合わせた）
- **D-25 `ItemSlotContent` の不在は null のまま渡す**: C3-a の既存 API の契約（「null は無し」）で、このレーンは部品を変えない。型を分けるなら C3-a の部品の改めで、本PR外のリファクタ提案に置く（Phase 2.6 5-A。agent 既定）
- **D-26 `RecipeItemSelection` は読み書きを 1 つの class に持つ**: Web の zustand ストアの写しで、サーバーを通らない画面ローカルの状態。書き手は画面の部品だけ、受理の規則も無い（Hotbar の A-106 と違う）ので、`I*Lookup`／`I*Mutation` に分けても 2 つ目の実装が無い（Phase 2.6 5-D。agent 既定）
- **D-27 名前の解決に欠損の分岐を置かない**: `GameContentPresentation.ItemName`・`Block(id).Label` はマスタの ID から引き、ID はマスタ由来のカタログからしか来ない（null チェックはマスタのコアに不要という AGENTS の規則）。欠損は `MasterHolder` の例外でテストと録画に出る（Phase 2.6 5-I。agent 既定）
- **D-28 ビューアの 3 状態は VM の union と件数で分ける**: 未選択（`RecipeViewerNoSelection`）・選択あり（`RecipeViewerItemSelected`）は判別 union で、レシピ無しは `Entries.Count == 0`（Web の `entries.length === 0` と同じ）。列挙型を足すと VM と二重になる（Phase 2.6 5-C 弱。agent 既定）
- **D-30 シミュレーターの指摘で直したもの**: Enter 長押しに 2026-10-01 裁定（残す）を引いた（その後 D-31 で確定）／`GameRecipeScreenSignals` を `IInitializable` にして `IVanillaApiEvent` を注入し、未使用の `IGameUnlockStateData` を外した（前例 `ResearchNodeStateStore`・`NotificationFeed`。VContainer は全 IInitializable を作ってから Initialize を回すので解放表の更新が先）／`CloseUI.GetKeyDown` はプロパティ／長押しのテスト名から 0.25 秒の値を外した（出所: シミュレーター予測。ユーザー承認は未取得）
- **D-31 Enter の長押しを写す（`RecipeActionButton` のインスタンスだけ画面側で `focusable=true`）**: ユーザー裁定 2026-10-01（`.decisions/2026-10-01-クラフトボタンはSpaceに反応させずジャンプ専用にする.md`「キーボードでのクラフトは Enter 長押しのみ」）とゴール（Web と同じ）が同じ向き。`focusable=false` は段階 1 の agent 規則（設計書 §1.2）でユーザー裁定ではないので、その例外にする。部品（C4-c の持ち物）は書き換えず `CraftRecipeEntryView` がインスタンスに立てる。部品の既定を変える必要が出たら `done.md` の「integrator への依頼」に書く。フォーカスを持つボタンが UITK の Tab のフォーカス移動に食われず、ゲームの Tab（インベントリの開閉）が先に効くことは録画の段 9 で確かめる（出所: 指揮の裁定。agent 既定）
- **D-32 フォーカスを保つ印と `GameScrollView` のつまみはこのレーンが基盤に入れる（E-19 の前例）**: `GameUiInputRules` は入力欄以外のフォーカスを次のスケジュールで外すので（`GameUiInputRules.cs:32-48`・`88-93`）、`focusable=true` だけでは Enter が届く前にフォーカスが外れる（指揮の再現で確認）。`Foundation/Input/UiKeyFocusKeepMark`（クラス `ui-key-focus-keep`。前例 `HoverTooltipMark` の静的な印）を足し、規則の Blur と警告の対象から印付きを外す。`GameScrollView` のつまみの既定は要素自身に書くと祖先の上書きを潰すので（指揮の再現で確認）、`DerivedTokens.uss` の `:root` に置く。どちらも画面レーンが最初の使い手になる小さな基盤の変更で、E-19 の前例どおり使い手のレーンが持つ。W4 の兄弟の計画（S4-alpha・S4-inventory・S4-train・S4-research・S4-special）は 2 本のファイルを書かないことを起草時に grep で確かめた（S4-machine・S4-gamma は Task 1 で確かめる）。Tab でフォーカスを乗せる経路は写さない（Web はクリックと Tab の両方で乗るが、ゲームの Tab はインベントリの開閉で、`GameUiInputRules` が `NavigationMoveEvent` を打ち消す。クリックで乗る経路だけを写す）
- **D-33 ビューアは最新の VM を覚え、出し直すときにそれで組む**: 閉じたら中身を捨てる（D-23）ので、開き直しで別の契機（持ち物の再取得）を待たずに組むため。隠れている間の `Render` は覚えるだけ
- **D-34 エントリは GUID の辞書で使い回す**: 解放で並びが変わっても既存の要素を残す（Web の React key）。C4-c の `SetEntries` は一度外して付け直すが、外れている間はスケジューラが止まるだけで長押しの状態は要素に残るので、長押しの部品は木から外れたときに止めない（捨てた要素は参照が切れる）。付け直しで Enter のフォーカスは外れて止まる（Web より狭いが、解放と Enter の長押しが重なるときだけ）
- **D-35 見出しの名前の影は右だけ・字の太さは `.text--medium`**: UITK の text-shadow は 1 本なので Web の左右 2 本のうち右の `0.3px 0 0` を写し、左の差は LX-135 の記帳候補。`--font-medium` は無いので `UiTypographyClass.TextMedium`（ラテンと CJK の両方）
- **D-29 解放の判定と素材の左押下は 2 か所のまま**: カタログは「無い・未解放」を 1 つに畳んで落とし、コマンドは 2 つの拒否の理由に分ける（目的が違う）。素材の左押下→選択はクラフトと機械のエントリに 1 行ずつで、共通の口を作るほどの量でない（Phase 2.6 検査6 弱 2 件。`Client.Ui.State` を Contract へ出す 5-G 弱も設計どおりの向きなので据え置き。agent 既定）

## 要裁定

なし（Enter の長押しは指揮の裁定で D-31 に確定）

## 未確認

- `simulate.FrameUpdateMs` が `schedule.Execute().Every(0)` の `TimerState.deltaTime` を進めるか（Task 6 Step 1 で実測。進まなければ形を直して `done.md` に書く）
- Button の `Clickable` が押下で捕捉しているときに `PointerLeaveEvent` が来るか（来なくても `CraftHoldInput` の位置判定で止まる。`CraftHoldTest` で見る）
- `UiPressGesture` を一覧の視口に付けたとき、セルの `ItemSlot` の `OnSlotGestured`（LeftDown）やホバーのツールチップと干渉しないか（`ItemListViewTest`・`RecipeSlotTooltipTest` で見る）
- 明るい半透明（白 88%・(132,137,151) 78%）を式 1.85 で描いたときの参照との差（Task 11 Step 2。残れば LX-132・133）
- C4-c・C4-a・S2-tutorial・S4-alpha の API 名（Task 1 Step 1 で照合）。S4-alpha の機械レシピの源は S4-alpha 計画 14a85b146c（レビュー中で変わりうる。マージ後に実物と照合）の Produces 一覧から写した（`Current`。初回の組み立ては `Initialize`〈IInitializable〉なので、`RecipeScreenModel` の ctor より後に来ても `Skip(1)` の後の値で組み直る）
- `ItemRecipeViewerDataContainer` の ctor・`IGameUnlockStateDataController.UnlockCraftRecipe` の名前（Task 1 Step 1）
- Web の素材スロットの `count`（必要数を渡すか未指定か）と `lacking` の式（Task 3・6 の着手時に `CraftRecipeEntry.tsx` を読む）

## 本PR外のリファクタ提案

- `S/Client.WebUiHost/Game/Topics/CraftingRecipesTopic.cs`・`RecipeViewerItemListTopic.cs` ／ 新設 `S/Client.Ui.State/Recipe/Catalog/CraftRecipeCatalog.cs`・`RecipeViewerItemCatalog.cs` ／ 同じ源から同じ列を組む（解放の絞り込みと未知の型の警告 1 回）。UITK 切替で Web の橋ごと消えるので、それまでは二重を許す（切替 PR で topic 側を消す）
- `S/Client.WebUiHost/Game/Actions/CraftActions.cs`（`CraftExecuteActionHandler`）／新設 `S/Client.Ui.State/Recipe/Commands/RecipeCommands.cs`／未知の GUID と未解放を同じ理由（`invalid_recipe`・`recipe_locked`）で断る同じ役割。切替で橋ごと消えるまで二重
- `S/Client.Game/InGame/UI/Inventory/RecipeViewer/ItemRecipeViewerDataContainer.cs`（`UnlockedCraftRecipes()`）／新設 `CraftRecipeCatalog.BuildUnlocked`／解放済みクラフトレシピの絞り込み。既存は解放表を添字で引き（無ければ例外）、新設と Web の topic は `TryGetValue` でロック扱い。どちらへ揃えるかは既存側の改めで決める
- `S/Client.Ui/Components/Slots/ItemSlot/ItemSlotContent.cs`（C3-a）／`ItemSlotContent` の shortage・ghost・tooltipOverride の不在を null で表す形を、種別ごとの型か static factory に分ける（D-25）

## 設計書・実行計画へ反映する行

### 設計書 §7.1 に足す行（A-165〜A-169）

| ID | 決定 | 却下した案と理由 |
|---|---|---|
| A-165 | レシピ画面（ビューア・アイテム一覧）の State は `Client.Ui.State/Recipe/` の `Catalog/`（解放済みクラフトレシピ・一覧のアイテムを Web の topic と同じ源から組む）・`Screen/`（選択 `RecipeItemSelection`・判別 union の VM・純関数の `RecipeViewModelFactory`・同期に組み直す `RecipeScreenModel`）・`Commands/`（`IRecipeCommands.Craft(Guid)`。拒否 `CraftRecipeUnknown`・`CraftRecipeLocked` は良性でない）。Esc の選択解除は画面のキー処理なので `Client.Ui/Screens/Recipe/Input/` に置く。機械レシピの解放済み一覧の源は S4-alpha が `Recipe/Source/` に作り、S4-recipe・S4-machine が使う。Presenter 1 本が 2 つの View を `Viewer`・`Items` 列へ載せ、`PlayerInventory` のときだけ出す（S4-recipe 計画 D-1〜D-3・D-15） | Web の橋の DTO を読む: 切替で消える。画面ごとに Presenter を分ける: 同じ条件と同じモデルの購読が 2 か所に分かれる。機械レシピの源を S4-recipe が持つ: S4-machine が 2 機能目で、持ち物の中核（S4-alpha）の中立の置き場が先に入る |
| A-166 | 長押しクラフトは画面の部品（`Screens/Recipe/Parts/CraftHoldDriver`・`CraftHoldInput`）が持つ。経過はパネルのスケジューラ（`schedule.Execute().Every(0)` の `TimerState.deltaTime`、1 フレーム 0.25 秒で頭打ち）で `HoldCraftLogic` を回し、押下・離し・外れ（位置でも判定）・取消で止める。フォーカス中の Enter も押している間だけ長押しにし（Space は何もしない）、そのため `RecipeActionButton` のインスタンスだけ画面側で `focusable=true` にし、`Foundation/Input/UiKeyFocusKeepMark` の印を付けて `GameUiInputRules` に外させない（§1.2「入力欄だけがフォーカスを持つ」の例外。フォーカスはクリックで乗り、Tab では乗らない。ユーザー裁定 2026-10-01）（S4-recipe 計画 D-5・D-19・D-31・D-32） | `UiPressGesture` を重ねる（C4-c の申し送り）: タップとドラッグの分類が Web の長押しに無く、外れで中断しない。`Update()`: 画面に毎フレームの処理を持たせない規則。Enter を写さない: ユーザー裁定 2026-10-01 とゴールに反する。部品の既定を `focusable=true` にする: 他の画面のボタンまでフォーカスを持つ。`focusable=true` だけ: 規則が次のスケジュールで外し Enter が届かない |
| A-167 | `GameScrollView` に溝・つまみの色と縦バーの位置のつまみ `--game-scroll-view-track-face`・`--game-scroll-view-thumb-face`・`--game-scroll-view-scrollbar-inset-top/bottom/right` を足す（既定は今の値で `DerivedTokens.uss` の `:root` に置き、`.game-scroll-view` 自身には宣言しない＝祖先の上書きが継承で届く）。画面は Web の局所上書き（透明の溝・白いつまみ・チュートリアルの逃げ 12）をつまみで書く（S4-recipe 計画 D-9） | 画面の USS で `.unity-scroll-view__vertical-scroller` を書く: 部品のクラスを画面が書かない規則。画面ごとに別のスクロール部品: 同じ部品が 2 つになる |
| A-168 | レシピの列はエントリを GUID で使い回し、持ち物の変化の描き直しで長押しを切らない。別のアイテムを選んだら中身を作り直してスクロールを先頭へ戻す（Web の `key`）。画面を閉じたらビューアの中身を捨て一覧のスクロールを先頭へ戻す（Web のアンマウント）（S4-recipe 計画 D-6・D-23） | VM ごとに作り直す: 長押しの経過とスケジューラが消える |
| A-169 | Esc での選択解除は「画面の遷移と同じフレームに `InputManager.UI.CloseUI` が押されていた」で読む（Tab で閉じたときは残す。Web のゲーム層の Escape と同じ時点）（S4-recipe 計画 D-7・D-16） | UITK のキーイベント: 届かない（A-83）。遷移で常に外す: Tab で閉じたときも消えて Web と違う |

### 設計書 §2 の部品カタログの差分案

- §1.2 の「ボタン類は focusable=false。入力欄だけがフォーカスを持つ」に例外を 1 行: 「キーで操作するボタン（S4-recipe のクラフトボタンの Enter 長押し）は置く側がインスタンスに `focusable=true` と `UiKeyFocusKeepMark` を付け、`GameUiInputRules` はそれを外さない。フォーカスはクリックで乗り Tab では乗らない（A-166）」
- §2 の Foundation/Input の表に `UiKeyFocusKeepMark`（`Mark`・`IsMarked`。S4-recipe が足す）を足す
- §2.4 `GameScrollView` 行の「つまみ」の欄に `--game-scroll-view-track-face`・`--game-scroll-view-thumb-face`・`--game-scroll-view-scrollbar-inset-top/bottom/right`（既定は今の値で `:root`。要素に書かない。A-167）を足す。「使う画面」に recipe（アイテム一覧・レシピ列）を足す
- §2.4 `RecipeActionButton` 行の注記に「長押しは置く側の画面の部品（S4-recipe は `CraftHoldInput`。`UiPressGesture` ではない）。クラフトのボタンは Enter の長押しのため置く側がインスタンスに `focusable=true` を立てる（§1.2 の例外。部品の既定は false のまま。A-166）」を足す

### 実行計画 §1.4 の S4-recipe 行の改め案

`| S4-recipe | レシピビューアと一覧 | 4・W4 | C4-c・C4-e（`StageColumnsLayout`）・L0-s4a・C3-b（`UiPressGesture`・`HoverTooltipHost`）・L0-s3（`UiDragThreshold`）・C4-a（`FluidAmountSlot`）・S2-tutorial（`TutorialAnchorCoveragePending`・切り抜きの判定）・S4-alpha（機械レシピの源 `Client.Ui.State/Recipe/Source/`） | 要 | 単位 | RecipeViewer（`Viewer` 枠）・ItemListPanel（`Items` 枠 [A-45]。6 列・クラフトは `RecipeActionButton` [A-46]・長押しクラフト〈マウスと Enter〉は画面の部品がパネルのスケジューラで回す [A-166]・コンテナクエリは C4-c の `RecipeRow` が `GeometryChangedEvent` で持つ）。State は `Client.Ui.State/Recipe/{Catalog,Screen,Commands}`・Esc の選択解除は `Screens/Recipe/Input` [A-165]、エントリは GUID で使い回す [A-168]、Esc の選択解除 [A-169]、`GameScrollView` のつまみ 5 本 [A-167]。アンカー `recipe.craft-button`・`recipe.item-<itemId>`。写す: recipe 52（P19・C8・I1。L24 は L0-s4a 済み。Enter の長押しも写す〈`focusable=true` の例外 A-166〉）。パネルは `StageColumnsLayout.Get(StageColumn.Viewer)`／`Get(StageColumn.Items)` へ載せる [A-92]。録画 `uitk/uitk-recipe-craft`。計画 `P/2026-11-10-uitk-s4-recipe.md` |`

DAG（§2）に `C4a --> Rec`・`S2t --> Rec`・`A --> Rec`（S4-alpha → S4-recipe）の辺を足す。W4 の枠では S4-recipe を S4-alpha の後に置く。

### 実行計画 §6.1 の割り当て表の行

`| S4-recipe（`P/2026-11-10-uitk-s4-recipe.md`。PLAN-W4b） | A-165〜A-169 | LX-131〜135（131 掴みスクロールの送りの単位〈倍率≠1〉・132 見出しの装飾線の端の消え方・133 明るい半透明のつまみ・134 一覧のつまみの長さ・135 見出しの名前の影の左側〈UITK の影は 1 本〉。並べ比べで差が出たときの候補） |`

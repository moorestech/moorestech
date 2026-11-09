# UITK S4-research（段階 4 画面: 研究ツリー・ノードカード 4 状態・詳細ペイン・解放物・研究ボタン）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/research`（`ResearchTreePanel`・`ResearchNodeCard`・`ResearchDetailPane`・`unlock/*`）を、C4-d の `NodeGraphView`・C4-c の `RecipeActionButton`・C3-a の `ItemSlot`/`BlockSlot`・C2 の `GamePanel`/`GameScrollView` で Web と同じ見た目の UITK 画面にし、Model は Web の topic の源（`ResearchNodeStateStore`・研究マスタ・持ち物）から直に組み、L0/L1・並べ比べ（720p・2560×1080）・録画 `uitk/uitk-research-tree` と既存 `tier9/tier9-5-compute-research` まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。State（`Client.Ui.State/Research/`）は研究マスタを静的な定義の列 `ResearchNodeCatalog` に 1 回だけ写し（機械レシピの出力は Web の橋と同じく機械レシピのマスタから引く。D-13）、`ResearchTreeModel` が定義・サーバーの状態（`ResearchNodeStateStore`）・持ち物の所持数・選択（`ResearchSelectionStore`）を購読して、同期で `ResearchTreeViewModel` を組み直す（[A-57]）。View（`Client.Ui/Screens/Research/`）は `UiLayer.OverlayPanel`・`UiFamily.Stage`（Web の `--z-stage-overlay-panel`）へ Presenter が 1 回だけ載せ、表示は経路（`UiRoutingState`）で `SetShown` する。ツリーは常駐の `NodeGraphView` 1 つにカードを使い回して渡し、ノードの集合か注目点が変わったときだけ `SetGraph` を呼ぶ（閉じて開き直しても視野が残る。C4-d D-23(3)）。詳細ペインは研究エリアの右上に浮かべ、研究ボタンは `RecipeActionButton` を画面 USS の祖先で研究の組に上書きする（[A-46]）。操作は `IResearchTreeCommands`（選択の切り替え・閉じる・研究の実行 `UniTask<UiCommandResult>`）で、失敗は `UiCommandResultToaster` の 1 本へ渡す。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`CustomStyleResolvedEvent`・`PointerDownEvent` 系・`panel.Pick`）、UniRx、UniTask、VContainer、NUnit＋`com.unity.ui.test-framework`（`UITestFixture`・`PanelSimulator`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（Web の参照キャプチャ・test-name-parity・`convert-web-tokens.mjs`・`gen:i18n`）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`（repo の兄弟）、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`、`THEME` = `S/Client.Ui/Foundation/Theme`。Files 節と各コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（とくに「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1.1・§2.7 NodeGraph と「研究ノードカード」・§3.2・§3.3・§4・§5.1、[A-8]・[A-13]・[A-45]・[A-46]・[A-47]・[A-52]・[A-57]・[A-65]・[A-92]）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-research 行・§4.3・§5・§6.2・E-1・E-13・E-22）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ。この計画が設計書・完了条件と違う所は判断記録に書き、設計書に足す行の案は末尾「## 設計書・実行計画へ反映する行」（A-170〜A-174）に置く（指揮が写す）
- **レーンの起動は指揮が行う**（実行計画 §4.1。実装は moores-subagent-driven-development でこのホストの Claude Code の subagent が担う）

## Requirements

設計の出所: 実行計画 §1.4 の S4-research 行（完了条件の原文:「ResearchTreePanel（`NodeGraphView`）・ノードカード 4 状態・詳細ペイン・解放物・研究ボタン（`RecipeActionButton` に無効の面を `rgb(50 52 67)` へ上書き [A-46]）。研究画面の左余白 0 は `StageColumnsLayout` [A-45]。アンカー `research.node-<guid>`（マスタ 7 件）。写す: research 55（P19 から `tmp-icon-click` を除く・C14。L22 は L0-s4b 済み）。録画 `tier9/tier9-5-compute-research`。研究の右の領域の `left` は画面 USS（列ではない）。左の余白の畳みは `StageColumnsLayout` が持つ [A-92]」。依存 C4-d・C3-a・C4-c・C4-e・L0-s4b）、§6.2、設計書 [A-46]・[A-92]、C4-e done.md の D-34（左余白の畳みは `UiScreenTraits.CollapsesStageLeftPadding` の列に移った）、指揮の追加指示（2026-11-10: 解放物の機械レシピの出力の State 側の源は S4-alpha が中立の置き場に作る。依存に S4-alpha を足す）。ただし S4-alpha 計画 14a85b146c の源 `UnlockedMachineRecipeSource` は解放済みの機械レシピだけを配り、研究が解放する前のレシピを引けないので使わない（D-13）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 静的な定義: 研究マスタの全ノードを `ResearchNodeDefinition`（GUID・アイコンの `ItemId`・ツリー座標 Y 上向き・前提 GUID の列・消費アイテム〈受け付ける上位レベルの ID 込み〉・解放物 `ResearchUnlockSet`）へ 1 回写す。解放物は Web の `ResearchNodeDtoFactory` と同じ 8 種（報酬・アイテムのレシピ表示・ブロック・機械レシピ〈アイテムと液体の両方の出力〉・接続ツール・車両・モジュール枠の上限〈レベルでなく上限値〉・製作上限）で、機械レシピの出力は `MasterHolder.MachineRecipesMaster` から引く（D-13） | `ResearchCatalogFromMasterTest`（L0・テスト mod）が緑 |
| R-2 | ViewModel: 定義・サーバーの状態（無いノードは `UnresearchableAllReasons`）・所持数（持ち物のメインの枠）・選択から、カード（4 状態・状態ラベルのキー・選択）・初期の注目点（`ResearchLogic.TryFindInitialFocusNode`）・詳細（ボタンの状態・消費アイテムの割当と不足・解放物の列）を組む純関数 | `ResearchTreeViewModelFactoryTest`（L0）が緑 |
| R-3 | Model: 状態の通知・選択・持ち物の変化・言語で同期で組み直す（表示の有無は Presenter と View。所持はメインの枠を `IPlayerInventoryOperations.ReadSnapshot()` から） | `ResearchTreeViewModelFactoryTest` と録画（R-13）の主張 |
| R-4 | 選択: 同じノードの再タップで閉じる切り替え、閉じるボタン、画面を離れたら選択を捨てる（Web はパネルのアンマウントで消える） | `ResearchSelectionTest`（L0）・`ResearchTreeViewTest`（Web `ResearchTreePanel.test.ts` 1 件目）が緑。離脱で消えることは録画 |
| R-5 | 研究エリア: `research-tree` が `OverlayPanel`/`Stage` 層で、上・右・下 0、`left` = `--inventory-panel-width` + `--stage-column-gap`（画面 USS で受け直し C# で足す [A-47]・[A-92]）。中に題「研究」の `GamePanel`（高さいっぱい）と `NodeGraphView("research")`（`research-viewport`・`research-canvas`） | `ResearchTreeViewTest`（Web `research.spec.ts`「研究パネルは持ち物の右隣から…」）が緑。並べ比べ（R-12） |
| R-6 | ノードカード: `research-node-<guid>`・幅 132・縦積み（名前 14px 省略・アイコン `ItemSlot`・状態ラベル 12px 省略 `research-node-state-<guid>`）・面 `--research-node-face`・枠 1px `--research-node-border`。完了は枠 `--text-default`、研究可能は枠 `--select-cyan`、前提未達はロックで減光（Web `opacity: .45` を Linear 合成で補正した `--research-node-locked-opacity`）、選択は外へ 2px の `--text-high-contrast` の輪。アンカー `research.node-<guid>` を付ける | `ResearchTreeViewTest`（Web `research.spec.ts`「ノードカードが4状態の…」・`ResearchNodeCard.test.ts` 2 件）が緑 |
| R-7 | ビューポート: 開いた時に研究可能なノードを中央に据え、閉じて開き直しても視野が残り、ホイールでカーソル位置を軸に拡大縮小し、空の背景とノードのドラッグでパンし（ドラッグは選択しない）、閾値未満の押下はタップとして詳細を開く | `ResearchTreeViewportTest`（Web `researchViewport.spec.ts` 5 件）が緑 |
| R-8 | 詳細ペイン: `research-detail-pane`（エリアの右上 12px・幅 300・高さはエリアの高さ − 24 まで）に `GamePanel(craft)`、見出し（名前 16px `--text-high-contrast`・閉じる `research-detail-close`「×」）、スクロール `research-detail-scroll`（説明 13px・消費アイテム `research-consume-items`・解放物の節）、研究ボタン `research-button-<guid>`、不可の理由 `research-detail-reason`（完了でも研究可能でもないとき） | `ResearchDetailPaneTest` が緑（Web `ResearchDetailPane.test.ts` の 2 件〈ボタンの活性・理由と閉じる〉） |
| R-9 | 消費アイテム: `ItemSlot` に所持/必要（`ItemSlotShortage`・ツールチップ `MaterialTooltipKind.ResearchConsumeItem`）、不足は `insufficient`。完了ノードは不足を出さない。上位レベルの所持も充足に数える | `ResearchDetailPaneTest`（Web 4 件）が緑 |
| R-10 | 解放物の節: Web の `UnlockSections` と同じ 5 節（ブロック `research-unlock-blocks`・機械レシピ `research-unlock-machine-recipes`・アイテム `research-unlock-items`・報酬 `research-reward-items`・その他 `research-unlock-others`）を初出順に並べ、ブロックは `BlockSlot`、機械レシピはアイテムの `ItemSlot` と液体の量のラベル `research-unlock-fluid`、報酬は個数付き、その他は 13px の文の行（接続ツール・車両・`research-unlock-module-slot-stack-limit`・`research-unlock-craft-cap-<itemId>`） | `ResearchUnlockSectionsTest`（Web `ResearchDetailPane.test.ts` 4 件・`research.spec.ts` 2 件・`researchMachineUpgradeUnlock.spec.ts` 1 件）が緑 |
| R-11 | 研究ボタン: `RecipeActionButton(Regular)` を研究の組（幅はペインの本文の幅・高さは撮影で決めた値・無効の面 `--research-action-disabled-face`・無効の字 `--text-default`・無効の淡さ `--research-action-disabled-opacity`・ホバーで面を変えない・14px・字間 0・縁なし・沈まない）で上書きし、文言は完了なら「完了済み」、それ以外は「研究」。押すと `IResearchTreeCommands.CompleteResearch` を呼び、応答の全状態を `ResearchNodeStateStore.ApplyNodeStates` へ入れ、失敗（応答なし・サーバーの拒否）は良性でない `ResearchFailed`（wire `research_failed`）でトーストに出す | `ResearchDetailPaneTest`（Web `research.spec.ts`「research button sends research.complete…」・`ResearchDetailPane.test.ts` 1 件）と `ResearchTreeViewTest` の研究ボタンの幅（本文の幅 253）が緑。録画（R-13） |
| R-12 | 並べ比べ: 参照 `4-research`・`5-tutorial-research-node`（1280×720・2560×1080）と、参照の組に無い詳細ペインの 3 状態（研究可能・素材不足・完了）を master の mock-host で自撮りして、UITK と並べた差の表を `done.md` に書く | 組が報告ページにあり、差の表の全行に「同じ／直した／LX-n／捨てる」のどれか |
| R-13 | 録画: 新しい `uitk/uitk-research-tree`（全体スイッチ UITK。開く・中央寄せ・ノードのタップで詳細・研究の実行で完了・閉じて開き直しで視野が残り選択が消える・チャレンジ HUD とキーヒントが Pick で遮られない〈判定の間だけ HUD の根を拾う設定にする〉・マスタの研究ノードのアンカー 7 件が引ける）と、既存 `tier9/tier9-5-compute-research` を全体スイッチ UITK で通す | 2 本の `result.json` が `Success: true`、録画 mp4、Note の `系=GameUi` |
| R-14 | 登録: `MainGameUiRegistration` の画面登録区画に `ResearchGameUiRegistration.Register(builder);` 1 行。`GameUiTheme.tss` の `Screens` 区画に `@import` 2 行（`ResearchTree.uss`・`ResearchDetailPane.uss`）。`TutorialAnchorCoveragePending` に `ResearchNodePrefix` の行があれば同じ PR で消す（表がまだ無ければ integrator へ申し送る） | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`。`TutorialAnchorCoverageTest` は在れば）が緑 |
| R-15 | テストの名前: 写したものは `<web-test>`、写さないもの 6 件は `docs/uitk/disposition.md` の「## 処遇」に行を足す | `node tools/uitk/test-name-parity.mjs --bundle 4 --emit-missing` で、このレーンの持ち分（下の「写すテスト」の表）の未対応 0。終了コードが 2 でない |
| R-16 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd を閉じる → `moores-wt rm` | `git log origin/feature/uitk` にこのレーンのマージコミット。`done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 部品（`NodeGraphView`・`RecipeActionButton`・`GamePanel`・`GameScrollView`・`ItemSlot`・`BlockSlot`・`LocalizedLabel`）と Foundation（`StageColumnsLayout`・`GameUiStageColumnsInstaller`・`UiLayer`・`UiScreenTraits`・`TutorialAnchor`・`AnchorIds`・`UiPickingIgnore`）は変えない。足りなければ `done.md` の「integrator への依頼」に書く
- 研究画面の左余白 0（持ち物を左へ寄せる）は作らない。C4-e が `UiScreenTraits.CollapsesStageLeftPadding` と `GameUiStageColumnsInstaller` で済ませた（D-34）。このレーンは確かめる L1 を写すだけ
- 持ち物パネルそのもの（研究画面の左側）は作らない（S4-inventory）。並べ比べで持ち物が未描画なら差の表で「捨てる（S4-inventory の持ち物）」とする
- 機械レシピの源（`Client.Ui.State/Recipe/Source/`。S4-alpha の `UnlockedMachineRecipeSource`）は作らず使わない。解放物の出力は研究マスタと同じくマスタから定義を組むときに読むだけ（D-13）
- チュートリアルの強調の描き手（S2-tutorial）・`TutorialAnchor.MarkClipContainer` は作らない。`NodeGraphView` の切り口の印は部品（C4-d）か S2-tutorial が付けるべきもので、2 機能（研究・チャレンジ）が使うので integrator へ申告する（D-9）
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は一切変えない。`ResearchNodeDtoFactory` と `ResearchNodeDefinitionsFromMaster` の重複は「本PR外のリファクタ提案」に回す
- L3（EditModeInPlayingTest）は持たない（段の締め S4-ladder）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s4-research --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる。Mac の Editor が逼迫したら Windows の `~/bin/winunity`（compile/test/play を ssh で回す）を使ってよい
- **依存**: C3-a（`ItemSlot`・`BlockSlot`。マージ済み）・C4-e（`StageColumnsLayout`・D-34。マージ済み #1626）・L0-s4b（`Client.Ui.State/Research/Logic`。マージ済み）・C4-d（`NodeGraphView`）・C4-c（`RecipeActionButton`・`--research-action-disabled-*`）。C4-d・C4-c は計画の Produces から写したので「マージ後に実物と照合」（Task 1 Step 1 で grep し、名前が違えば以後を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <旧> → <実物>」と書く）。無ければ止めて指揮へ報告する
- **使う部品と公開 API**

```csharp
// ===== 実物（origin/feature/uitk 0a94c9b96b から写した）=====
namespace Client.Ui.Components.Panels
{
    public enum GamePanelFrame { Default, Craft }
    public sealed class GamePanel : VisualElement
    {
        public GamePanel(string testid, GamePanelFrame frame);     // name = testid。.game-panel・--default/--craft
        public void SetTitle(UiText title);                         // 呼ぶまで題の行は隠れる（.game-panel--titled）
        public override VisualElement contentContainer { get; }    // .game-panel__body（flex-grow 1・flex-basis 0%・min-height 0）
    }
}
namespace Client.Ui.Components.Controls
{
    public sealed class GameScrollView : ScrollView                 // 縦。.game-scroll-view
    {
        public GameScrollView(string testid);
        public IObservable<float> OnScrollOffsetChanged { get; }
        public void SetScrollOffset(float y);
    }
}
namespace Client.Ui.Components.Slots
{
    public sealed class ItemSlot : VisualElement { public ItemSlot(string frameTestid); public void SetContent(ItemSlotContent content); public IObservable<SlotGesture> OnSlotGestured { get; } } // 枠の name = frameTestid。所持/必要は .item-slot--shortage・--lack
    public sealed class ItemSlotContent { public ItemSlotContent(UiIcon icon, int? count, ItemSlotMarks marks, ItemSlotShortage shortage, ItemSlotGhost ghost, UiText tooltipOverride); } // 無いものは null（前例 GrabOverlayView）
    public readonly struct ItemSlotMarks { public ItemSlotMarks(bool selected, bool catalog, bool insufficient); }   // insufficient → 枠の .slot-frame--insufficient
    public sealed class ItemSlotShortage { public ItemSlotShortage(double ownedCount, double requiredCount, bool lacking, UiText tooltip); }
    public sealed class BlockSlot : VisualElement { public BlockSlot(string frameTestid); public void SetBlock(UiIcon icon, UiText name); }
}
namespace Client.Ui.Content { public sealed class GameContentPresentation { public GameContentPresentation(IGameContentTextureSource textures); public UiIcon Item(ItemId id); public UiIcon Block(BlockId id); public UiIcon Fluid(FluidId id); public UiText ItemName(ItemId id); } }
namespace Client.Ui.Foundation.Icons { public sealed class UiIcon { public UiText Label { get; } } }
namespace Client.Ui.Foundation.Text { public class LocalizedLabel : Label { public LocalizedLabel(); public void SetText(UiText text); } }
namespace Client.Ui.Foundation.Theme { public static class UssCustomStyleReader { public static bool TryReadPixels(ICustomStyle style, string propertyName, out float pixels); } } // 読めないときは LogError を出して false
namespace Client.Ui.Foundation.Anchor { public static class TutorialAnchor { public static void Mark(VisualElement element, params string[] ids); } }
namespace Client.Ui.Foundation.Input { public static class UiPickingIgnore { public static void ApplyToSubtree(VisualElement root); } }
namespace Client.Ui.Foundation.Root { public sealed class GameUiRoot : MonoBehaviour { public VisualElement GetLayer(UiLayer layer, UiFamily family); } }
// UiLayer.OverlayPanel（--z-stage-overlay-panel）・UiFamily.Stage（Client.Ui.Foundation.Layers・Client.Ui.Foundation.Stage）
namespace Client.Ui.Foundation.Stage.Columns { public sealed class StageColumnsLayout : VisualElement { public StageColumnsLayout(); public void SetLeftPaddingCollapsed(bool collapsed); } }
// AnchorIds.ResearchNode(string guid) = "research.node-<小文字の guid>"・AnchorIds.ResearchNodePrefix（Client.Game.InGame.Tutorial.UIHighlight）
namespace Client.Ui.State.Text { public sealed class UiText { public UiText(LocalizationKey key); public UiText(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams); public static UiText FromContent(ContentLocalizationKey key); public string Resolve(); } public static class SlotAmountFormat { public static string Format(double amount); } }
// LocalizationTextParam(string name, string value)（Client.Localization）。ContentLocalizationKeys.ResearchName(Guid)・ResearchDescription(Guid)・BlockName(Guid)・ConnectToolName(Guid)・TrainCarName(Guid)・FluidName(Guid)・ItemName(Guid)（Mooresmaster.Localization.Generated）
namespace Client.Ui.State.MaterialTooltip { public static class MaterialTooltipText { public static UiText Create(MaterialTooltipKind kind, string itemName, double requiredCount, double ownedCount); } } // kind は ResearchConsumeItem
namespace Client.Ui.State.Items { public static class OwnedItemCounts { public static IReadOnlyDictionary<ItemId, int> Build(IReadOnlyList<SlotItem> slots); } public sealed class SlotItem { public SlotItem(ItemId itemId, int count); } }
namespace Client.Ui.State.Routing { public sealed class UiRoutingState { public IReadOnlyReactiveProperty<UiRoutingSnapshot> Current { get; } } public sealed class UiRoutingSnapshot { public UiScreen Screen { get; } } public static class UiScreenTraitsTable { public static UiScreenTraits Get(UiScreen screen); } } // UiScreen.ResearchTree。Traits の AlwaysOnHud（false）・CollapsesStageLeftPadding（true）
namespace Client.Ui.State.Commands { public abstract class UiCommandResult { } /* UiCommandAccepted.Instance・UiCommandRejected(UiCommandRejectReason reason, string detail)（Detail が空なら理由の wire）*/ public enum UiCommandRejectReason { /* …既存… SplitDragNoValidSlots, ここに足す */ } public static class UiCommandRejectReasonTraits { public static bool IsBenign(UiCommandRejectReason r); public static LocalizationKey ToLocalizationKey(UiCommandRejectReason r); public static string ToWireCode(UiCommandRejectReason r); } }
namespace Client.Ui.State.Toast { public sealed class UiCommandResultToaster { public void Report(string commandName, UiCommandResult result); } } // 拒否は LogWarning、良性でなければトースト。テストは ScreenTestHost.Toaster・ShownToasts

// ===== L0-s4b（実物 S/Client.Ui.State/Research/Logic・Research/ResearchNodeStateStore.cs）=====
namespace Client.Ui.State.Research.Logic
{
    public static class ResearchLogic
    {
        public static int[] AllocateResearchItems(IReadOnlyList<ResearchConsumeItem> consumeItems, IReadOnlyDictionary<ItemId, int> owned);
        public static bool IsConsumeItemLacking(ResearchNodeState state, int required, int? allocated);
        public static ResearchButtonState DeriveResearchButton(ResearchNodeState state);   // .Completed・.Interactable・.TooltipKey
        public static NodeCardState DeriveNodeCardState(ResearchNodeState state);         // .Completed・.Ready・.Locked
        public static LocalizationKey DeriveNodeStateLabelKey(NodeCardState cardState);
        public static bool TryFindInitialFocusNode(IReadOnlyList<ResearchNodeSummary> nodes, out ResearchNodeSummary node);
    }
    public sealed class ResearchConsumeItem { public ResearchConsumeItem(ItemId itemId, int count, IReadOnlyList<ItemId> acceptedItemIds); public ItemId ItemId { get; } public int Count { get; } }
    public readonly struct ResearchNodeSummary { public ResearchNodeSummary(Guid guid, ResearchNodeState state); public Guid Guid { get; } }
    public sealed class ResearchUnlockSet { public ResearchUnlockSet(IReadOnlyList<UnlockBlock> blocks, IReadOnlyList<UnlockMachineRecipe> machineRecipes, IReadOnlyList<ItemId> itemRecipeViewItemIds, IReadOnlyList<RewardItem> rewardItems, IReadOnlyList<Guid> connectToolGuids, IReadOnlyList<Guid> trainCarGuids, IReadOnlyList<int> moduleSlotStackLimits, IReadOnlyList<LevelInheritanceCraftCap> levelInheritanceCraftCaps); }
    // UnlockBlock(BlockId, Guid)・UnlockFluid(FluidId, double)・UnlockMachineRecipe(Guid, IReadOnlyList<ItemId>, IReadOnlyList<UnlockFluid>)・RewardItem(ItemId, int)・LevelInheritanceCraftCap(ItemId, int)
    public static class UnlockEntries { public static List<UnlockEntry> ToUnlockEntries(ResearchUnlockSet unlocks); }
    // UnlockEntry（.Index）の派生: BlockUnlockEntry(.Block)・MachineRecipeOutputUnlockEntry(.Recipe)・ItemRecipeViewUnlockEntry(.ItemId)・RewardItemUnlockEntry(.Reward)・ConnectToolUnlockEntry(.Guid)・TrainCarUnlockEntry(.Guid)・ModuleSlotStackLimitUnlockEntry(.Limit)・LevelInheritanceCraftCapUnlockEntry(.Cap)
}
namespace Client.Ui.State.Research
{
    public sealed class ResearchNodeStateStore : IInitializable   // MainGameUiStateRegistration が両系で登録済み
    {
        public IReadOnlyDictionary<Guid, ResearchNodeState> NodeStates { get; }
        public IObservable<Unit> OnNodeStatesChanged { get; }      // ResearchTree へ入るたび・表示中の持ち物の変化で取り直す
        public void ApplyNodeStates(Dictionary<Guid, ResearchNodeState> nodeStates);
    }
}
// ResearchNodeState（Game.Research）: Completed・Researchable・UnresearchableAllReasons・UnresearchableNotEnoughItem・UnresearchableNotEnoughPreNode
// 研究の実行: ClientContext.VanillaApi.Response.Progression.CompleteResearch(Guid, CancellationToken) → UniTask<ResponseCompleteResearchMessagePack>（null は応答なし。.Success・.NodeState.ToDictionary()）。前例 S/Client.WebUiHost/Game/Actions/ResearchActions.cs
// 持ち物: LocalPlayerInventoryController（Client.Game.InGame.UI.Inventory.Main）.LocalPlayerInventory（ILocalPlayerInventory: this[int]・MainSlotCount・OnItemChange）・.OnInventoryRefreshed。前例 S/Client.Ui.State/Inventory/Actions/LocalPlayerInventoryOperations.cs:34
// 言語: Client.Localization.Localize.OnLanguageChanged（IObservable<Unit>）

// ===== C4-d（計画 uitk-plan-w3c/docs/superpowers/plans/2026-11-09-uitk-c4-d.md の Produces。マージ後に実物と照合）=====
namespace Client.Ui.Components.NodeGraph
{
    public sealed class NodeGraphView : VisualElement
    {
        public NodeGraphView(string testIdPrefix);                 // 根 name = <prefix>-viewport・.node-graph、キャンバス <prefix>-canvas
        public IObservable<string> OnNodeTapped { get; }           // 閾値未満の押下だけ。ドラッグはパン
        public IObservable<NodeGraphViewport> OnViewportChanged { get; }
        public void SetGraph(NodeGraphContent content);            // 毎回組み直し。中央寄せは「ノード・注目点・寸法がそろった最初の 1 回」だけ
        public void RestoreViewport(NodeGraphViewport viewport);
    }
    public sealed class NodeGraphContent { public NodeGraphContent(IReadOnlyList<NodeGraphNode> nodes, NodeGraphFocus initialFocus); }
    public sealed class NodeGraphNode { public NodeGraphNode(string key, Vector2 treePosition, IReadOnlyList<string> previousKeys, VisualElement card); } // Y 上向き。カードは自分で translate -50% -50%。カードの根は PickingMode.Position・flex-shrink を書かない
    public readonly struct NodeGraphFocus { public static NodeGraphFocus None { get; } public static NodeGraphFocus At(Vector2 treePoint); }
    public readonly struct NodeGraphViewport { public float X { get; } public float Y { get; } public float Scale { get; } }
}
// 祖先が display: none（寸法 0）の間は追従も記録もせず、戻っても視野を動かさない（C4-d D-23(3)）。ホイール 1 ノッチ = Web の deltaY 60。押下の閾値は実画面 5px
// テストの土台: S/Client.Tests/Ui/Components/NodeGraph/NodeGraphTestStage.cs（ReadViewport・WheelNotches・Drag の前例。このレーンは読むだけ）

// ===== C4-c（計画 uitk-plan-w3c/docs/superpowers/plans/2026-11-09-uitk-c4-c.md の Produces。マージ後に実物と照合）=====
namespace Client.Ui.Components.Controls
{
    public sealed class RecipeActionButton : Button
    {
        public RecipeActionButton(string testid, UiText label, RecipeActionLabelWeight weight); // 研究は Regular（Web の素の button の 400）
        public IObservable<Unit> OnClicked { get; }                 // 無効では流さない。SetEnabled は VisualElement の既定
        public void SetLabel(UiText label);
    }
    public enum RecipeActionLabelWeight { Medium, Regular }
}
// 研究の上書きの組（C4-c の RecipeActionKnobOverrides.uss の .recipe-action-research-probe と同じ組。幅と高さはこのレーンが撮って決める）:
//   --recipe-action-hover-start/middle/end: var(--recipe-action-background-start/middle/end)
//   --recipe-action-disabled-start/middle/end: var(--research-action-disabled-face)
//   --recipe-action-disabled-text: var(--text-default)・--recipe-action-disabled-opacity: var(--research-action-disabled-opacity)
//   --recipe-action-width・--recipe-action-height・--recipe-action-padding-block: 8px・--recipe-action-border-width: 0px・--recipe-action-font-size: 14px・--recipe-action-letter-spacing: 0px・--recipe-action-text-outline-width: 0px・--recipe-action-active-offset: 0px
// --research-action-disabled-face: rgb(50, 52, 67)・--research-action-disabled-opacity は C4-c が DerivedTokens.uss の C4-c ブロックに置く（このレーンは var() で使うだけ。同じ値を別名で足さない）

// ===== 機械レシピのマスタ（実物 moorestech_server/Assets/Scripts/Core.Master/MachineRecipesMaster.cs）=====
// MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid) → MachineRecipeMasterElement（無い GUID は null。研究が指す GUID は `ResearchMasterUtil` の検証で `MasterHolder.Load` が止めるので届かない）。.OutputItems[i].ItemGuid・.OutputFluids[i].FluidGuid/.Amount
// 前例 S/Client.WebUiHost/Game/Topics/Research/ResearchNodeDtoFactory.cs の BuildUnlockMachineRecipeDto（同じ読み方）
```

- **層の向き**（設計書 §1.1・`UiLayerDependencyRules`）: `Client.Ui.State/**` は Client.Game・Client.Network・Core.Master・Game.Research・Game.Block.Interface を参照してよい（asmdef 既存）。`Core.Item`（`LevelFamilySubstituteUtil`）は未参照なので `Client.Ui.State.asmdef` の `references` に 1 行足す（D-12）。`Client.Ui/Screens/**` は State・Components・Foundation・Content を参照してよく、`Client.Game` の型を受けない（ViewModel は ID・状態・キーだけを運ぶ）
- **AGENTS.md の規約**: 1 ファイル 200 行未満・1 ディレクトリ 10 ファイルまで（.meta は数えない）・`partial`/`Func<>` 禁止（UniRx の `Where`・`Select` にも渡さず、購読の中で分岐する）・イベントは UniRx・try-catch 禁止（ネットワークの待ちは既存 API が null で失敗を返すので catch は要らない）・デフォルト引数禁止・単純な getter/setter プロパティ禁止（`{ get; private set; }` と式本体の読み取り専用は可）・主要処理に「// 日本語 → // English」の 2 行コメント（各 1 行）・メソッド内のローカル関数は `#region Internal`・初期化は `Initialize`・fail-closed の経路はログ（意図した無音は `silent-ok: <理由> / <誰がどこで気づくか>`）・`.meta` は手で作らない・Prefab/シーンを手で書き換えない・`Update()` で毎フレームの同値判定をしない
- **USS の規約**（ref-uitk-design・設計書 §3.2）: 画面 USS はルートのクラス `.research-tree` で始まり、画面のクラスは `research-tree__*`。部品のクラス（`.game-panel__*`・`.node-graph*`・`.item-slot*`・`.recipe-action-button*`）を書かない（部品の見た目はつまみで変える）。色は `var()` だけ、`z-index`・構造擬似クラス・`gap`・合成の太字を使わない（Web の `gap` は子の margin で写す）。USS の先頭に `/* web: <Web の CSS> */` を日英で書く。C# が値を要るときは画面のクラスで `--research-tree-<名>: var(--token)` と受け直して `UssCustomStyleReader` で読む（`:root` の変数は customStyle へ継承されない）
- **calc() の写し**（[A-47]）: Web の `.researchArea` の `left: calc(var(--inventory-panel-width) + var(--stage-column-gap))` と `.detailScroll` の `margin-inline: calc(-1 * var(--research-detail-scroll-inline-bleed))` は、画面内の `ResearchCalcKnobs` が customStyle から読んで inline で当てる。換算値を USS に直書きしない
- **字間・色の罠（持ち越し）**: UITK の字間は字の大きさ/100 × 値（px のまま書くと 100/字の大きさ 倍に効く）。このレーンは字間を書かない（研究ボタンは C4-c の `--recipe-action-letter-spacing: 0px` を当てるだけ）。部品ギャラリーは sRGB 合成なので、色はランタイムの PanelSettings（Linear 合成）で比べる。暗い半透明の α は I-fix1 が指数 1.85 で選び直した。明るい色の α は未実測。このレーンの減光（ロックのカード `opacity: .45`）は要素ごとの opacity で、暗い面の上に明るい字と枠が乗る。前例 `--slot-frame-insufficient-opacity`（Web 0.4 → Linear 合成で明るい中身が淡く残るので 0.2 へ下げた。C3-a Task 8）のとおり、暗い半透明の色の式（1.85）は当てはまらない。初期値は Web と同じ 0.45 にし、Task 11 Step 3 で字・枠・面の画素を Web と比べて RGB 7 段以内になる値に決める（決められなければ `LX-136`）
- **テストの置き場と名前**: `S/Client.Tests/Ui/Screens/Research/`・`S/Client.Tests/Ui/State/Research/`（既存 4 本と同じ所）、namespace はディレクトリに合わせる。テスト名は日本語の挙動文。Web から写したものは `[Test]` の直前に `/// <web-test file="<W/ からの相対パス>">it/test の第 1 引数</web-test>`（describe は含めない。`it.each` は第 1 引数の書式文字列そのままで展開しない。1 つの NUnit が複数の Web テストを写したら行を並べる。実行計画 E-13）
- **コマンド**: コンパイル `uloop compile --project-path ./moorestech_client`（期待: ErrorCount 0）。テスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。このレーンのテストフィルタ（以下 `$FILTER`）は `^Client\.Tests\.Ui\.(Screens\.Research|State\.(Research|PauseMenuCommandsTest)|Conventions|Theme|Layers|Anchor|Routing|Components\.(NodeGraph|Controls))\.`。ドメインリロード中のエラーは 45 秒待って再試行
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有ファイルは、`MainGameUiRegistration.cs` の画面登録区画の 1 行、`GameUiTheme.tss` の `Screens` 区画の `@import` 2 行、`THEME/DerivedTokens.uss` の末尾の名札付きブロック「S4-research」（[A-52]。opacity の 1 行だけで、色の前計算の表 `handWrittenDerivedRows.mjs` は色の行しか持たないので触らない）、`docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」（`LX-136`〜`LX-140`）「## 追従記録」の末尾、`docs/uitk/disposition.md` の「## 処遇」の末尾 6 行、`S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` の `AnchorIds.ResearchNodePrefix` の 1 行の削除（在るときだけ）、`S/Client.Ui.State/Commands/UiCommandRejectReason.cs` の共有区画（enum と 3 つの switch の末尾に `ResearchFailed` を 1 行ずつ）と良性の一覧を固定する `S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs`（良性でない値なので一覧は変わらないが、全値の網羅の行があれば 1 つ足す）、`Localization/localization.csv` の `ui.commandRejected.*` の群の末尾 1 行（`ui.commandRejected.researchFailed`）と再生成した `W/src/shared/i18n/generated/localizationKeys.ts`（`npm run gen:i18n`。台帳「## 追従記録」へ 1 行）・`S/Client.Localization/_CompileRequester.cs`（生成の印）、`S/Client.Ui.State/Client.Ui.State.asmdef` の `references` に `Core.Item` の 1 行。シナリオ `SKILL/scenarios/uitk/uitk-research-tree.cs` はこのレーンが持つ。他の S4 レーン（S4-alpha・S4-inventory・S4-machine・S4-train・S4-recipe・S4-special・S4-gamma）のファイルは書かない
- **画面専用の部品の置き場**（設計書 §2.7・§5.1-2）: ノードカード・カードの集合・詳細ペイン・研究ボタンの行・消費アイテムの節・解放物の節・calc の受け直しは 1 機能目なので `S/Client.Ui/Screens/Research/Parts/` に置く。チャレンジ（S5-challenge）と共有しうるもの（`NodeGraphView` の切り口の印・カードの選択の輪）は作らず宣言だけする（D-9・D-6）
- **新しい共有部品を見つけたときの手順**: 作業中に「別の画面でも要る部品・Foundation の口」を見つけたら作らない。`LOGS/harness/uitk/S4-research/done.md` の「## integrator への依頼」に「部品名／Web の正本のパス／使う画面／このレーンで作らない理由」を 1 行で書き、`bd note <bd> "<同じ 1 行>"` にも残して先へ進む。既知の依頼（Task 1 Step 3 で雛形に書く）: (1) `NodeGraphView` の切り口（`overflow: hidden`）に `TutorialAnchor.MarkClipContainer` を部品が自分で付ける（研究・チャレンジの 2 機能。D-9）(2) `Registration/GameUi/` が 10 ファイルを超えるならサブディレクトリの切り方（Task 1 Step 1）
- **参照キャプチャ**: `LOGS/uitk/reference-captures/web-a9d5123537/`（1280×720）・`web-0e81bbe3c3-2560x1080/`（2560×1080。座標は `x' = 320 + 1.5x`・`y' = 1.5y`）の `4-research.png`（状態の出所は `W/e2e/uitk-reference/referenceShots.ts:112` の controls: topic `researchOwnedItems`・ui `ResearchTree`。`manifest.json` は id・bundle・ledgerRow・file・kind しか持たない。参照で見えるノードは 3 つ〈研究可能・素材不足・ロック〉で、完了の `1111…` は持ち物の背後の左へ切れている〈線だけが左端へ出る〉。差の表はこれを前提に比べる）・`5-tutorial-research-node.png`（`referenceShots.ts:118` の topic `tutorialResearchNode`）。詳細ペインの 3 状態は参照に無いので Task 11 Step 1 で master の mock-host で撮る（E-22。C4-c が Task 8 Step 2 で撮った `web-research-*.png` があればそれを先に使う）
- **並べ比べの画像の原本**は worktree の gitignore 下 `moorestech_client/.uloop/outputs/compare/S4-research/` に置き、`moorestech_logs` へ入れない。`moorestech_logs` へ書く前に `LOGS/README.md` を読む
- **報告**: `LOGS/harness/uitk/S4-research/done.md`（1 行目「状態: 完了」。途中で止まるときは「状態: 停止（理由）」）。節は「証拠」「差の表」「要裁定」「integrator への依頼」「未確認」

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反5件・修正5件 / 所持の読み口を既存の `ReadSnapshot` へ・Model は常に組む形へ・橋の DTO との突き合わせ L0 を追加・死活表に 4 行・研究の実行の二重は帰結を D-25 に記録
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱2・第3バケツ4 / 検査7（未対応の GameActionType）を ADR どおり例外へ直し、他は「このままでよい」を D-24 に記録、第3バケツは本PR外のリファクタ提案へ
- 独立評価 2 体（opus 6 件・codex 4 件）の反映と、変えた範囲の再検査（別 subagent・実物照合）: 実施済み / 反映 8 件（HUD の Pick は判定の間だけ根を拾う設定にし層の順序の主張と明記・採掘の進捗バーは本物の `ProgressHudView` を重ねる L1 へ〈D-18〉・研究ボタンの幅は本文に従う `auto`〈253。D-21〉・USS を `ResearchTree.uss` と `ResearchDetailPane.uss` に分割・閉じて開き直すとスクロールを頭へ〈D-27〉・参照の状態の出所を `referenceShots.ts` へ・`TutorialAnchorCoverageTest` を在ればにし integrator への申し送り）。再検査の指摘 4 件（`Object` の曖昧さで 3 つのテストがコンパイル不通→`UnityEngine.Object`・持ち分 33 の内訳が 28＋6 で合わない→写す 28・処遇 5＋持ち分外 1・研究の組に `--recipe-action-border-width: 0px` の漏れ・USS の見出しの出所の範囲）を全件反映
- user-simulator review（Fable 判事）: 実施済み / 適用推奨1・Warning4・要裁定0 / 機械レシピの null の分岐を消す（参照切れは MasterHolder.Load の検証で止まる）・S4-alpha の消費者から外す申し送りと起動順を末尾へ・網羅テストの申し送りを本PR外へ・古い MainSlots() の行を削除

## 死活表（Phase 2.5）

全体スイッチ UITK（`UiInputExclusivity.ActiveUiSystem == GameUi`）で起動したとき、Web の系で動いていた研究の振る舞いが生きるか。Web の橋（`ResearchTopic`・`ResearchCompleteActionHandler`）は Web の系でだけ登録され、源の `ResearchNodeStateStore` は両系で登録済み。

| 振る舞い | GameUi で生きるか | 根拠 |
|---|---|---|
| R キーで研究画面を開く・ESC で閉じる | 生きる（このレーンの外） | `UIStateControl` の `ResearchTree` 状態の遷移は Client.Game で UI の系を読まない。View は経路（`UiRoutingState`）で `SetShown` するだけ（Task 9） |
| 研究画面へ入るたびの状態の取り直し | 生きる | `ResearchNodeStateStore` が両系で `OnStateChanged` を購読（既存）。Model は `OnNodeStatesChanged` で組み直す（Task 4） |
| ノードの一覧・4 状態・状態ラベル | 生きる | `ResearchTreeModel` → `ResearchTreeView`（Task 4・7〜9） |
| パン・ズーム・慣性・開き直しで視野が残る | 生きる | C4-d の `NodeGraphView` を常駐で 1 つ使う（D-3。Task 9） |
| ノードのタップで詳細・同じノードで閉じる | 生きる | `OnNodeTapped` → `IResearchTreeCommands.ToggleNodeSelection`（Task 5・9） |
| 研究の実行と完了の反映 | 生きる | `ResearchTreeCommands.CompleteResearch` が Web の橋と同じ API と `ApplyNodeStates` を呼ぶ（Task 5・8） |
| 研究の失敗の知らせ | 生きる（Web と同じく良性でないトースト） | `UiCommandRejectReason.ResearchFailed` → `UiCommandResultToaster`（Task 5） |
| 表示中の持ち物の変化で充足を取り直す | 生きる | `ResearchNodeStateStore` が両系で購読（既存）＋ Model が所持数で組み直す（Task 4） |
| 研究画面で持ち物が左へ寄る | 生きる（C4-e 済み） | `GameUiStageColumnsInstaller` が `CollapsesStageLeftPadding` を読む（Task 9 の L1 で確かめる） |
| 研究画面でホットバーと装備 HUD を出さない | 生きる（持ち主は各 HUD） | `UiScreenTraits.AlwaysOnHud` が `ResearchTree` で false。読むのは S3-hotbar・S4-inventory（Task 9 の L1 で表の値を固定） |
| チャレンジ HUD・キーヒントが研究エリアに遮られない | 生きる | 研究エリアは `OverlayPanel` 層、HUD は `Chrome` 層（上）。録画で判定の間だけ HUD の根を拾う設定にし、`panel.Pick` が HUD を返すことを見る（Task 12） |
| 採掘の進捗バーが研究エリアに遮られない | 生きる | 進捗 HUD は `Chrome`・`Viewport` 層（`ProgressHudPresenter`）で研究エリアより上。本物の `ProgressHudView` を研究エリアと重ねた L1 で Pick を見る（Task 9。D-18） |
| チュートリアルの研究ノードの強調（マスタ 7 件） | S2-tutorial の描き手が入っていれば生きる | カードに `TutorialAnchor.Mark(card, AnchorIds.ResearchNode(guid))`（Task 7）。録画で 7 件の ID の要素が引けることを見る（Task 12）。切り口の印は integrator への依頼（D-9） |
| 研究ノードの上のクリックがワールドに抜けない | 生きる | 研究エリアは `GamePanel`（拾う）で覆い、`UiPointerHitTest.IsPointerOverAnyUi` が真になる（エリアの根は拾わない。Task 9 の L1 で Pick） |
| ホバーのツールチップ（カードのアイコンの名前・消費アイテムの所持/必要・解放物の枠の名前） | 生きる | 部品 `ItemSlot`（C3-a）が Foundation の `HoverTooltipHost` へ渡す。カードのアイコンは拾う（D-8）。拡大縮小したキャンバスと `GameScrollView` の中で出ることは録画の流れ 6b で見る |
| 研究パネルの外の右短押しで閉じる・パネルの上では閉じない | 生きる | `ResearchTreeState` が `UiPointerHitTest.IsPointerOverAnyUi` で判定（既存。UI の系を読まない）。研究エリアは `GamePanel` が拾うのでパネルの上は UI。録画の流れ 9 でパネルの上の右短押しで閉じないことを見る |
| Tab で持ち物へ移る | 生きる（このレーンの外） | `ResearchTreeState` の遷移（既存）。View は経路で `SetShown(false)` になる（Task 9）。録画の流れ 9 で状態の遷移を見る |
| 研究画面での掴み絵（持ち物から掴んだアイテム） | このレーンの外（C4-e の掴み絵・S4-inventory の持ち物） | 研究エリアは `OverlayPanel` 層で、掴み絵は `Grab` 層（上）。研究エリアは掴みの落とし先を持たない（Web と同じ） |

---

## File Structure

| パス | 責務 | 新規／変更 |
|---|---|---|
| `S/Client.Ui.State/Research/Catalog/ResearchNodeDefinition.cs` | 研究ノード 1 件の静的な定義（GUID・アイコン・座標・前提・消費・解放物） | 新規 |
| `S/Client.Ui.State/Research/Catalog/ResearchNodeCatalog.cs` | 定義の列を 1 回だけ組んで持つ（DI のシングルトン） | 新規 |
| `S/Client.Ui.State/Research/Catalog/ResearchNodeDefinitionsFromMaster.cs` | 研究マスタ → 定義の列（座標・前提・消費アイテムと受け付ける上位レベル） | 新規 |
| `S/Client.Ui.State/Research/Catalog/ResearchUnlockSetFromMaster.cs` | `ClearedActions` → `ResearchUnlockSet`（8 種。機械レシピの出力は機械レシピのマスタ） | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchSelection.cs` | 選択の有無を型で持つ値（`None`・`Of`・`Is`・`TryGetGuid`・`Toggled`） | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchNodeCardViewModel.cs` | カード 1 枚の表示（状態・ラベルのキー・選択） | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchDetailViewModel.cs` | 詳細の表示（ボタンの状態・消費の枠 `ResearchConsumeSlotViewModel`・解放物の列）。消費の枠の型も同じファイル | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchTreeViewModel.cs` | 画面の不変の ViewModel（カードの列・注目点・詳細） | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchTreeViewModelFactory.cs` | 定義・状態・所持数・選択から ViewModel を組む純関数 | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchTreeModel.cs` | 源を購読し、変化のたびに同期で組み直して `Current` で配る（`IInitializable`） | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchSelectionStore.cs` | 選択を持ち、研究画面を離れたら捨てる（`IInitializable`） | 新規 |
| `S/Client.Ui.State/Research/Commands/IResearchTreeCommands.cs` | 画面から呼ぶ操作（[A-8]） | 新規 |
| `S/Client.Ui.State/Research/Commands/ResearchTreeCommands.cs` | 本番の実装（選択・研究の実行と状態の反映・失敗の拒否） | 新規 |
| `S/Client.Ui.State/Client.Ui.State.asmdef` | `references` に `Core.Item` | 変更（D-12） |
| `S/Client.Ui.State/Commands/UiCommandRejectReason.cs` | enum と 3 つの switch の末尾に `ResearchFailed` | 変更（共有区画） |
| `Localization/localization.csv`・`W/src/shared/i18n/generated/localizationKeys.ts`・`S/Client.Localization/_CompileRequester.cs` | 拒否理由の文言 1 行と再生成 | 変更（共有区画・生成物） |
| `S/Client.Ui/Screens/Research/ResearchTreePresenter.cs` | View を `OverlayPanel`/`Stage` へ 1 回載せ、経路で表示・Model で描画 | 新規 |
| `S/Client.Ui/Screens/Research/ResearchTreeView.cs` | 研究エリア（`GamePanel`・`NodeGraphView`・詳細の置き場）。`Render`・`SetShown` | 新規 |
| `S/Client.Ui/Screens/Research/ResearchTree.uss` | 画面の USS（エリア・カード） | 新規 |
| `S/Client.Ui/Screens/Research/ResearchDetailPane.uss` | 詳細ペインの USS（ペイン・節・解放物・研究ボタンのつまみの上書き） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchNodeCard.cs` | カード 1 枚（名前・アイコン・状態ラベル・選択の輪・アンカー。文字は拾わず、アイコンはホバーの名前のために拾う） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchNodeCardSet.cs` | カードを GUID で使い回し、`NodeGraphContent` を組む・組み直しの要否を返す | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchDetailPane.cs` | 詳細ペイン（見出し・閉じる・スクロール・節・行） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchDetailActions.cs` | 研究ボタンと不可の理由（押下 → 操作 → トースト口） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchConsumeItemsSection.cs` | 消費アイテムの節（所持/必要・不足） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchUnlockSections.cs` | 解放物の節を初出順に組み、1 件ずつ描く | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchUnlockSectionKind.cs` | 解放物の種類 → 節（見出しのキー・testid・枠の列で包むか）の唯一の switch | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchUnlockEntryElements.cs` | 解放物 1 件を枠か文の行にして節へ足す（種類ごとの switch） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchCalcKnobs.cs` | calc() の 2 つ（エリアの `left`・スクロールの逃げ）を customStyle から読んで当てる（[A-47]） | 新規 |
| `S/Client.Starter/Registration/GameUi/ResearchGameUiRegistration.cs` | 定義・選択・Model・操作・Presenter の登録 | 新規 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画に 1 行 | 変更（共有区画） |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | `Screens` 区画に `@import` 2 行 | 変更（共有区画） |
| `THEME/DerivedTokens.uss` | 末尾に名札付きブロック「S4-research」（ロックの減光 1 行） | 変更（共有区画 [A-52]） |
| `S/Client.Tests/Ui/State/Research/ResearchCatalogFromMasterTest.cs` | マスタからの定義・解放物 8 種（L0・テスト mod） | 新規 |
| `S/Client.Tests/Ui/State/Research/ResearchTreeViewModelFactoryTest.cs` | 状態の既定・カード・注目点・詳細の割当と不足（L0） | 新規 |
| `S/Client.Tests/Ui/State/Research/ResearchSelectionTest.cs` | 選択の切り替え（L0） | 新規 |
| `S/Client.Tests/Ui/Screens/Research/ResearchScreenTestKit.cs` | テスト mod のマスタ・Content・定義の見本・VM の組み立て | 新規 |
| `S/Client.Tests/Ui/Screens/Research/RecordingResearchTreeCommands.cs` | 操作を記録し、研究の実行は設定した結果を返す偽 | 新規 |
| `S/Client.Tests/Ui/Screens/Research/ResearchTreeViewTest.cs` | 描画・4 状態・状態ラベル・エリアの寸法・タップの切り替え・Pick | 新規 |
| `S/Client.Tests/Ui/Screens/Research/ResearchTreeViewportTest.cs` | 中央寄せ・開き直しで残る・ホイールとパン・ノードのドラッグ・タップ | 新規 |
| `S/Client.Tests/Ui/Screens/Research/ResearchDetailPaneTest.cs` | ボタン・理由・閉じる・消費アイテムの不足・完了・上位レベル | 新規 |
| `S/Client.Tests/Ui/Screens/Research/ResearchUnlockSectionsTest.cs` | 解放物の節・報酬の個数・機械レシピの液体・その他の行 | 新規 |
| `S/Client.Tests/Ui/Screens/Research/ResearchStageLayoutTest.cs` | 研究画面の左余白 0・常駐 HUD を出さない（表の値） | 新規 |
| `SKILL/scenarios/uitk/uitk-research-tree.cs` | 研究ツリーを UITK で通す録画 | 新規 |
| `docs/uitk/disposition.md` | 「## 処遇」の末尾に 6 行 | 変更（共有区画） |
| `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` | 在れば `AnchorIds.ResearchNodePrefix` の 1 行を消す | 変更（共有区画・在れば） |
| `docs/uitk/TODO.md` | 「## 実測の結論」（ロックの減光）・「## 見た目の例外」（`LX-136`〜`LX-140`）・「## 追従記録」（gen:i18n）の末尾 | 変更（共有区画） |

ディレクトリのファイル数（.cs と .uss）: `State/Research/` 既存 1（`ResearchNodeStateStore.cs`）＋サブディレクトリ、`State/Research/Catalog/` 4、`State/Research/Tree/` 7、`State/Research/Commands/` 2、`Screens/Research/` 4、`Screens/Research/Parts/` 9、`Tests/.../Screens/Research/` 7、`Tests/.../State/Research/` 既存 4＋3＝7。`Registration/GameUi/` は 1 増える（Task 1 Step 1 で 10 以下を確かめる。超えるなら止めて指揮へ）。

**MainGameUiRegistration の画面登録区画に足す行**: 1 行（`ResearchGameUiRegistration.Register(builder);`）。

**つまみの上書き**（Web で局所上書きされている変数と同名。研究ボタンは C4-c の部品のつまみを `.research-tree__detail-body` で上書きする。値は Web の `.researchButton` と同じ）

| つまみ | Web の値（`W/src/features/research/style.module.css` の `.researchButton`） | UITK の値（`.research-tree__detail-body`） |
|---|---|---|
| `--recipe-action-hover-start/middle/end` | （素の button。ホバーで面が変わらない） | `var(--recipe-action-background-start/middle/end)` |
| `--recipe-action-disabled-start/middle/end` | `:disabled` の `background: rgb(50 52 67)` | `var(--research-action-disabled-face)` |
| `--recipe-action-disabled-text` | `:disabled` の `color: var(--text-default)` | `var(--text-default)` |
| `--recipe-action-disabled-opacity` | `:disabled` の `opacity: 0.6` | `var(--research-action-disabled-opacity)`（C4-c が Linear 補正込みで持つ） |
| `--recipe-action-width` | 幅の指定なし（本文の幅いっぱい = 300 − 枠 1×2 − `GamePanel(craft)` の padding 左 10・右 15 − 本文の padding 10×2 ＝ 253） | `auto`（本文の幅に従う。効かなければ `100%`→`253px`。D-21） |
| `--recipe-action-height` | 高さの指定なし（`padding: 8px 0` ＋ 14px の行の高さ `normal`） | `36px` を初期値（14px の Noto Sans JP の `normal` を 20px と見た値。Task 11 Step 3 で Web の画素から決め直す） |
| `--recipe-action-padding-block` | `8px` | `8px` |
| `--recipe-action-border-width` | `border: 0` | `0px`（既定 1px だとグラデが左右 1px 内へ寄る。C4-c の研究の組） |
| `--recipe-action-font-size` | `14px` | `14px` |
| `--recipe-action-letter-spacing` | `normal` | `0px` |
| `--recipe-action-text-outline-width` | （影なし） | `0px` |
| `--recipe-action-active-offset` | （素の button。沈まない） | `0px` |

**受け直すトークン**（`.research-tree` で受け、`ResearchCalcKnobs` が読む）: `--research-tree-inventory-width: var(--inventory-panel-width)`・`--research-tree-column-gap: var(--stage-column-gap)`・`--research-tree-scroll-bleed: var(--research-detail-scroll-inline-bleed)`。

**写すテストの件数（Web の実物を grep して数え直した。数え方: `it(`／`test(` の呼び出し 1 つを 1 件、`it.each` は書式文字列 1 つを 1 件〈行は展開しない〉と数える。1 つの NUnit が複数の Web テストを写すことがあり、`it.each` の 1 件を `[TestCase]` の複数行の NUnit 1 本で写す）**

| Web のファイル（`W/` から） | 件数 | 写す | 写さない（処遇） | 写し先 |
|---|---|---|---|---|
| `e2e/tests/research/research.spec.ts`（P） | 12 | 9 | 捨てる 2（`expectCraftGrip` の自己テスト 2 件「translate後のグリップ矩形だけに重なる境界buttonを…」「padding box外の境界buttonを…」: DOM の補助関数の検査で UITK に同じ物が無い）、録画 1（「研究パネル展開中も常駐チャレンジHUDとキーヒントが遮蔽されない」: 両 HUD は Presenter と状態の源が要り L1 に並べにくいので録画 `uitk/uitk-research-tree` の Pick で見る。採掘の進捗バーの 1 件は本物の `ProgressHudView` を L1 で重ねて写す。D-18） | `ResearchTreeViewTest` 4・`ResearchDetailPaneTest` 1・`ResearchUnlockSectionsTest` 2・`ResearchStageLayoutTest` 2 |
| `e2e/tests/research/researchMachineUpgradeUnlock.spec.ts`（P） | 1 | 1 | 0 | `ResearchUnlockSectionsTest`（`ResearchDetailPane.test.ts` 11 件目と同じ NUnit に 2 行） |
| `e2e/tests/research/researchViewport.spec.ts`（P） | 5 | 5 | 0 | `ResearchTreeViewportTest` |
| `e2e/tests/research/tmp-icon-click.spec.ts`（P） | 1 | 0 | 捨てる 1（`tmp:` の付いた一時の調査テスト。完了条件で除外） | — |
| `src/features/research/ResearchNodeCard.test.ts`（C） | 2（`it` 1・`it.each` 1〈5 行〉） | 2 | 0 | `ResearchTreeViewTest`（`it.each` は `[TestCase]` 5 行の 1 本） |
| `src/features/research/ResearchTreePanel.test.ts`（C） | 2 | 1 | 捨てる 1（「インベントリtopic未受信中は詳細ペインへ所持数をnullで渡す(D4)」: UITK に未受信の状態が無い [A-13]。前例 disposition の A-13 の行） | `ResearchTreeViewTest` |
| `src/features/research/ResearchDetailPane.test.ts`（C） | 11 | 10 | 捨てる 1（「所持数未受信中(owned=null)は不足表示も所持/必要も出さない」: 同上 [A-13]） | `ResearchDetailPaneTest` 6・`ResearchUnlockSectionsTest` 4 |
| `src/features/research/researchLogic.test.ts`・`researchAllocation.test.ts`（L） | 22 | — | L0-s4b で写し済み | — |
| `src/shared/treeView/**`（C・L） | 29 | — | C4-d が写す | — |

合計: Web の P 19・C 15（実行計画の「C14」は `ResearchNodeCard.test.ts` を 1 と数えた値で、実物は `it` 1＋`it.each` 1＝2。D-15）・L 22。このレーンの持ち分は P 18（`tmp-icon-click` を除く）＋ C 15 ＝ 33。うち `<web-test>` で写す 28（P 15・C 13）、処遇 5（捨てる 4・録画 1）。処遇の表にはほかに持ち分外の `tmp-icon-click` 1 行を足し、計 6 行。L1 の NUnit の数は `ResearchTreeViewTest` 7・`ResearchTreeViewportTest` 5・`ResearchDetailPaneTest` 8・`ResearchUnlockSectionsTest` 5・`ResearchStageLayoutTest` 2。タグの無い NUnit（Web に無い主張）は `ResearchTreeViewTest` の Pick 1・`ResearchDetailPaneTest` の失敗のトースト 1 と閉じて開き直したスクロール 1 と L0 の 3 本。

---
## Tasks

### Task 1: 前提の確認と記録の準備

**Files:**
- Create: `LOGS/harness/uitk/S4-research/done.md`（雛形。moorestech_logs 側）

**Interfaces:**
- Consumes: `origin/feature/uitk`（C4-d・C4-c・L0-s4b・C4-e マージ済み）、Global Constraints の「使う部品と公開 API」
- Produces: bd のレーン ID（以下 `<bd>`）、`done.md` の雛形、API 名の読み替え表（違ったときだけ）

- [ ] **Step 1: 作業場所と部品の実物を確かめる**

Run:
```bash
pwd
git log -1 --format='%H %s' origin/feature/uitk
S=moorestech_client/Assets/Scripts
# C4-d（マージ後に実物と照合）
grep -rn "public NodeGraphView(\|IObservable<string> OnNodeTapped\|public void SetGraph(\|public void RestoreViewport(" $S/Client.Ui/Components/NodeGraph/
grep -rn "public NodeGraphContent(\|public NodeGraphNode(\|public static NodeGraphFocus At(\|NodeGraphFocus None" $S/Client.Ui/Components/NodeGraph/
grep -rn "MarkClipContainer" $S/Client.Ui/Components/NodeGraph/ $S/Client.Ui/Foundation/Anchor/ || echo "NO-CLIP-MARK"
ls $S/Client.Tests/Ui/Components/NodeGraph/
# C4-c（マージ後に実物と照合）
grep -rn "public RecipeActionButton(\|enum RecipeActionLabelWeight\|public void SetLabel(" $S/Client.Ui/Components/Controls/
grep -n "research-action-disabled-face\|research-action-disabled-opacity\|recipe-action-background-start" $S/Client.Ui/Foundation/Theme/*.uss
grep -rn "recipe-action-research-probe" -A16 $S/Client.Tests/Ui/Components/Controls/
# 機械レシピのマスタ（解放物の出力の読み口）
grep -n "public MachineRecipeMasterElement GetRecipeElement(Guid" moorestech_server/Assets/Scripts/Core.Master/MachineRecipesMaster.cs
# 実物（念のため）
grep -n "public GamePanel(\|public void SetTitle" $S/Client.Ui/Components/Panels/GamePanel/GamePanel.cs
grep -n "public GameScrollView(\|public void SetScrollOffset" $S/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs
grep -n "public static void ApplyToSubtree" $S/Client.Ui/Foundation/Input/UiPickingIgnore.cs
grep -n "ResearchNodePrefix\|static string ResearchNode(" $S/Client.Game/InGame/Tutorial/UIHighlight/AnchorIds.cs
grep -n "ResearchTree" $S/Client.Ui.State/Routing/UiScreenTraits.cs
grep -n "SplitDragNoValidSlots" $S/Client.Ui.State/Commands/UiCommandRejectReason.cs
grep -n "public async UniTask<CompleteResearchProtocol.ResponseCompleteResearchMessagePack> CompleteResearch" $S/Client.Network/API/Requests/ProgressionQueryApi.cs
grep -n "\"Core.Item\"" $S/Client.Ui.State/Client.Ui.State.asmdef || echo "NO-CORE-ITEM"
grep -n "research-node-face\|research-node-border\|research-detail-slots-bleed\|research-detail-scroll-inline-bleed\|inventory-panel-width\|stage-column-gap\|select-cyan\|text-high-contrast\|text-muted" $S/Client.Ui/Foundation/Theme/Tokens.uss
grep -n "section: Screens" "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
test -f $S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs && grep -n "ResearchNodePrefix" $S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs
# ディレクトリのファイル数（10 以下）
ls $S/Client.Starter/Registration/GameUi/*.cs | wc -l
ls $S/Client.Tests/Ui/State/Research/*.cs | wc -l
```
Expected: worktree の絶対パス。C4-d の 4 行・3 行（`NodeGraphView(string testIdPrefix)`・`OnNodeTapped`・`SetGraph(NodeGraphContent)`・`RestoreViewport`、`NodeGraphContent(IReadOnlyList<NodeGraphNode>, NodeGraphFocus)`・`NodeGraphNode(string, Vector2, IReadOnlyList<string>, VisualElement)`・`NodeGraphFocus.At/None`）と `NodeGraphTestStage.cs`、C4-c の `RecipeActionButton(string, UiText, RecipeActionLabelWeight)`・`--research-action-disabled-face`・`--research-action-disabled-opacity`・研究の上書きの組（Global Constraints と同じ 13 行）、`GetRecipeElement` の 1 行、実物の 10 行、トークン 9 つ、`/* == section: Screens == */`。`Registration/GameUi/*.cs` が 9 以下（1 足して 10 以下）、State の Research のテストが 7 以下。
- C4-d・C4-c のどれかのファイルが無ければ、その依存が未取り込みなので止め、`done.md` を「状態: 停止（<依存> 未取り込み）」にして指揮へ報告する
- 名前が違えば以後の計画を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <旧> → <実物>」と書く。機械レシピの出力はこのレーンがマスタから読む（D-13。S4-alpha の `UnlockedMachineRecipeSource` は解放済みだけなので使わない）
- `NO-CLIP-MARK` 以外（`NodeGraphView` か S2-tutorial が切り口の印をすでに付けている）なら、Task 1 Step 3 の「integrator への依頼」の (1) を書かない
- `Registration/GameUi/*.cs` が 10 以上なら止めて指揮へ。`done.md` の「integrator への依頼」に「`Registration/GameUi/` の分け方」を書く（サブディレクトリの切り方は integrator の持ち物）。再編が `origin/feature/uitk` に入るまでこのレーンは完了にせずマージもしない。再編後は File Structure と Task 10 の登録のパスを読み替え、`done.md` に書く

- [ ] **Step 2: 使う文言が揃っていることを確かめる**

Run:
```bash
grep -c "^ui\.research\." Localization/localization.csv
grep -n "^ui\.research\.\(title\|closeSymbol\|completed\|action\|consumeItemsLabel\|consumeItemTooltip\|unlockBlocksLabel\|unlockMachineRecipesLabel\|unlockItemsLabel\|rewardItemsLabel\|unlockOthersLabel\|unlockFluidSummary\|unlockModuleSlotStackLimit\|unlockLevelInheritanceCraftCap\|stateAvailable\|stateUnavailable\)," Localization/localization.csv | wc -l
grep -n "^ui\.commandRejected\.researchFailed," Localization/localization.csv || echo "NO-RESEARCH-FAILED"
```
Expected: 20、16、`NO-RESEARCH-FAILED`（Task 5 で足す）。画面の文言は既存のキーだけを使い、`localization.csv` に足すのは拒否理由の 1 行だけ

- [ ] **Step 3: bd を積み、done.md の雛形を置く**

Run:
```bash
bd create "UITK S4-research: 研究ツリー・ノードカード・詳細ペイン・解放物・研究ボタンを UITK へ" --parent moorestech-albof -t task -p 2
bd update <bd> --claim
mkdir -p ../moorestech_logs/harness/uitk/S4-research
```
`done.md` に次を書く（1 行目は終わるまで「状態: 作業中」）:
```markdown
状態: 作業中

# S4-research 作業報告

## 証拠
## 差の表
## 要裁定
## integrator への依頼
- NodeGraphView の切り口（overflow: hidden）に TutorialAnchor.MarkClipContainer を部品が自分で付ける／Web W/src/shared/treeView・設計書 A-65／研究ツリー・チャレンジ一覧／2 機能が使う部品の内側で、画面からは部品の内部の要素に手を出せないため（計画 D-9）
## 未確認
```
Task 1 Step 1 の `test -f …TutorialAnchorCoveragePending.cs` が何も出さなかった（表がまだ feature/uitk に無い）ときは、「integrator への依頼」にもう 1 行足す: `- 研究ノードのアンカー（AnchorIds.ResearchNodePrefix）は S4-research がカードに印済み。TutorialAnchorCoveragePending の表を作るとき ResearchNodePrefix の行を入れない／設計書 A-66／S2-tutorial・S4-research／印ありと Pending の二重は A-66 の検査で赤になるため`。表が在って行が無いときも同じ 1 行を足す
Expected: bd の ID が出る。以後 `<bd>` をその ID で読む。`bd note <bd>` に「integrator への依頼」の 1 行を同じ文で残す

---

### Task 2: 研究マスタからの静的な定義（`Client.Ui.State/Research/Catalog`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Catalog/ResearchNodeDefinition.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Catalog/ResearchNodeCatalog.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Catalog/ResearchNodeDefinitionsFromMaster.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Catalog/ResearchUnlockSetFromMaster.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef`（`references` に `"Core.Item"`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Research/ResearchCatalogFromMasterTest.cs`

**Interfaces:**
- Consumes: `MasterHolder.ResearchMaster.GetAllResearches()`・`MasterHolder.ItemMaster.GetItemId(Guid)`・`MasterHolder.BlockMaster.GetBlockId(Guid)`・`LevelFamilySubstituteUtil.GetAcceptedItemIds(ItemId)`・`ModuleSlotStackLimitMasterUtil.GetStackLimit(int)`・`MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid)`・`MasterHolder.FluidMaster.GetFluidId(Guid)`、L0-s4b の `ResearchConsumeItem`・`ResearchUnlockSet` と部品の値の型
- Produces:
  - `namespace Client.Ui.State.Research.Catalog { public sealed class ResearchNodeDefinition { public Guid Guid { get; } public ItemId IconItemId { get; } public Vector2 TreePosition { get; } public IReadOnlyList<Guid> PreviousGuids { get; } public IReadOnlyList<ResearchConsumeItem> ConsumeItems { get; } public ResearchUnlockSet Unlocks { get; } public ResearchNodeDefinition(Guid guid, ItemId iconItemId, Vector2 treePosition, IReadOnlyList<Guid> previousGuids, IReadOnlyList<ResearchConsumeItem> consumeItems, ResearchUnlockSet unlocks); } }`
  - `public sealed class ResearchNodeCatalog { public IReadOnlyList<ResearchNodeDefinition> Nodes { get; } public ResearchNodeCatalog(); }`（ctor でマスタから 1 回だけ組む）
  - `public static class ResearchNodeDefinitionsFromMaster { public static IReadOnlyList<ResearchNodeDefinition> Build(); }`
  - `public static class ResearchUnlockSetFromMaster { public static ResearchUnlockSet Build(ResearchNodeMasterElement master); }`

- [ ] **Step 1: テストを書く**

`ResearchCatalogFromMasterTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Logic;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Game.Research;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// 研究マスタから静的な定義と解放物 8 種を写す（テスト mod）
    /// Copies static definitions and the eight unlock kinds from the research master (test mod)
    /// </summary>
    public class ResearchCatalogFromMasterTest
    {
        private static readonly Guid FirstResearch = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17");
        private static readonly Guid SecondResearch = Guid.Parse("7f1464a7-ba55-4b96-9257-cfdeddf5bbdd");
        private static readonly Guid BlockAndToolsResearch = Guid.Parse("bb000000-0000-4000-8000-000000000001");
        private static readonly Guid ModuleSlotResearch = Guid.Parse("c0de0000-0000-4000-8000-000000000002");
        private static readonly Guid CraftCapResearch = Guid.Parse("c0de0000-0000-4000-8000-000000000001");
        private static readonly Guid RewardResearch = Guid.Parse("bf9bda9e-dace-43c4-9a33-75f248fd17f6");

        [SetUp]
        public void LoadTestMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 全ノードを座標と前提つきで写す()
        {
            var nodes = ResearchNodeDefinitionsFromMaster.Build();

            // マスタの件数と同じで、前提の GUID もそのまま写る
            // Same count as the master, and prerequisite GUIDs carry over as they are
            Assert.AreEqual(MasterHolder.ResearchMaster.GetAllResearches().Count, nodes.Count);
            var second = nodes.Single(node => node.Guid == SecondResearch);
            CollectionAssert.AreEqual(new[] { FirstResearch }, second.PreviousGuids);
        }

        [Test]
        public void ブロックと機械レシピと接続ツールと車両の解放を写す()
        {
            var unlocks = UnlocksOf(BlockAndToolsResearch);

            Assert.IsNotEmpty(unlocks.Blocks);
            Assert.IsNotEmpty(unlocks.MachineRecipes);
            Assert.IsNotEmpty(unlocks.ConnectToolGuids);
            Assert.IsNotEmpty(unlocks.TrainCarGuids);
            // 機械レシピはアイテムか液体の出力を少なくとも 1 つ運ぶ
            // A machine recipe carries at least one item or fluid output
            var recipe = unlocks.MachineRecipes[0];
            Assert.Greater(recipe.OutputItemIds.Count + recipe.OutputFluids.Count, 0);
        }

        [Test]
        public void モジュール枠はレベルでなく上限値で製作上限はアイテムとレベルで写す()
        {
            var moduleSlot = UnlocksOf(ModuleSlotResearch);
            var craftCap = UnlocksOf(CraftCapResearch);

            Assert.AreEqual(1, moduleSlot.ModuleSlotStackLimits.Count);
            Assert.AreEqual(ModuleSlotStackLimitMasterUtil.GetStackLimit(MasterLevelOf(ModuleSlotResearch)), moduleSlot.ModuleSlotStackLimits[0]);
            Assert.IsNotEmpty(craftCap.LevelInheritanceCraftCaps);
            Assert.AreEqual(2, craftCap.LevelInheritanceCraftCaps[0].Level);
        }

        [Test]
        public void 報酬とレシピ表示の解放を写す()
        {
            var unlocks = UnlocksOf(RewardResearch);

            Assert.IsNotEmpty(unlocks.RewardItems);
            Assert.IsNotEmpty(unlocks.ItemRecipeViewItemIds);
            Assert.Greater(unlocks.RewardItems[0].Count, 0);
        }

        [Test]
        public void 全ノードがWebの橋のDTOと同じアイコンと前提と消費と解放物を持つ()
        {
            // Web の橋 ResearchNodeDtoFactory の写しなので、橋の読み方の変更や種別の追加の取りこぼしをここで捕まえる（前例 設計書 A-102）
            // This copies the Web bridge ResearchNodeDtoFactory, so catch missed reading changes or new kinds here (precedent design A-102)
            var noStates = new Dictionary<Guid, ResearchNodeState>();
            foreach (var definition in ResearchNodeDefinitionsFromMaster.Build())
            {
                var dto = ResearchNodeDtoFactory.Create(MasterHolder.ResearchMaster.GetResearch(definition.Guid), noStates);
                var label = definition.Guid.ToString();
                Assert.AreEqual(dto.IconItemId, definition.IconItemId.AsPrimitive(), label);
                CollectionAssert.AreEqual(dto.PrevGuids, definition.PreviousGuids.Select(guid => guid.ToString()).ToList(), label);
                CollectionAssert.AreEqual(dto.ConsumeItems.Select(c => Join(c.ItemId, c.Count, Join(c.AcceptedItemIds.ToArray()))), definition.ConsumeItems.Select(c => Join(c.ItemId.AsPrimitive(), c.Count, Join(c.AcceptedItemIds.Select(id => id.AsPrimitive()).ToArray()))), label);
                AssertSameUnlocks(dto, definition.Unlocks, label);
            }
        }

        #region Helpers

        private static void AssertSameUnlocks(ResearchNodeDto dto, ResearchUnlockSet unlocks, string label)
        {
            // 8 種を種類ごとに同じ並び・同じ値で比べる
            // Compare all eight kinds in the same order with the same values
            CollectionAssert.AreEqual(dto.RewardItems.Select(r => Join(r.ItemId, r.Count)), unlocks.RewardItems.Select(r => Join(r.ItemId.AsPrimitive(), r.Count)), $"{label} reward");
            CollectionAssert.AreEqual(dto.UnlockItemRecipeViewItemIds, unlocks.ItemRecipeViewItemIds.Select(id => id.AsPrimitive()), $"{label} itemRecipeView");
            CollectionAssert.AreEqual(dto.UnlockBlocks.Select(b => b.BlockId), unlocks.Blocks.Select(b => b.BlockId.AsPrimitive()), $"{label} block");
            CollectionAssert.AreEqual(dto.UnlockMachineRecipes.Select(r => Join(r.RecipeGuid, Join(r.OutputItemIds.ToArray()), Join(r.OutputFluids.Select(f => Join(f.FluidId, f.Amount)).ToArray()))), unlocks.MachineRecipes.Select(r => Join(r.RecipeGuid.ToString(), Join(r.OutputItemIds.Select(id => id.AsPrimitive()).ToArray()), Join(r.OutputFluids.Select(f => Join(f.FluidId.AsPrimitive(), f.Amount)).ToArray()))), $"{label} machineRecipe");
            CollectionAssert.AreEqual(dto.UnlockConnectToolGuids, unlocks.ConnectToolGuids.Select(guid => guid.ToString()), $"{label} connectTool");
            CollectionAssert.AreEqual(dto.UnlockTrainCarGuids, unlocks.TrainCarGuids.Select(guid => guid.ToString()), $"{label} trainCar");
            CollectionAssert.AreEqual(dto.UnlockModuleSlotStackLimits, unlocks.ModuleSlotStackLimits, $"{label} moduleSlot");
            CollectionAssert.AreEqual(dto.UnlockLevelInheritanceCraftCaps.Select(c => Join(c.ItemId, c.Level)), unlocks.LevelInheritanceCraftCaps.Select(c => Join(c.ItemId.AsPrimitive(), c.Level)), $"{label} craftCap");
        }

        private static string Join(params object[] parts)
        {
            return string.Join("|", parts.Select(part => Convert.ToString(part, CultureInfo.InvariantCulture)));
        }

        private static ResearchUnlockSet UnlocksOf(Guid researchGuid)
        {
            return ResearchNodeDefinitionsFromMaster.Build().Single(node => node.Guid == researchGuid).Unlocks;
        }

        private static int MasterLevelOf(Guid researchGuid)
        {
            var master = MasterHolder.ResearchMaster.GetAllResearches().Single(node => node.ResearchNodeGuid == researchGuid);
            var action = master.ClearedActions.items.Single(item => item.GameActionType == Mooresmaster.Model.GameActionModule.GameActionElement.GameActionTypeConst.unlockModuleSlotStackLevel);
            return ((Mooresmaster.Model.GameActionModule.UnlockModuleSlotStackLevelGameActionParam)action.GameActionParam).Level;
        }

        #endregion
    }
}
```
（`#region Helpers` はクラス直下の private メソッドを囲う `Internal` ではないので規約に触れない。L0-s4b の型のメンバー名〈`AcceptedItemIds`・`BlockId`・`RecipeGuid`・`FluidId`・`Amount`・`Level`〉と DTO のフィールド名は実物の `S/Client.Ui.State/Research/Logic/**`・`S/Client.WebUiHost/Game/Topics/Research/ResearchTopicDtos.cs` に合わせ、違えばコンパイルで読み替える。`Client.Tests` は `Client.WebUiHost` を参照済み。`Tests.Module.TestMod`・`Server.Boot` の namespace は前例 `S/Client.Tests/Ui/Screens/GrabOverlay/GrabOverlayViewTest.cs` の using に合わせる）

- [ ] **Step 2: 定義の型を書く**

`ResearchNodeDefinition.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Master;
using UnityEngine;

namespace Client.Ui.State.Research.Catalog
{
    /// <summary>
    /// 研究ノード 1 件の静的な定義。マスタから 1 回だけ写し、サーバーの状態とは混ぜない
    /// Static definition of one research node; copied once from the master and never mixed with server state
    /// </summary>
    public sealed class ResearchNodeDefinition
    {
        public Guid Guid { get; }
        public ItemId IconItemId { get; }
        // ツリー座標（Y 上向き。NodeGraphNode と同じ向き）
        // Tree coordinates (Y up, same as NodeGraphNode)
        public Vector2 TreePosition { get; }
        public IReadOnlyList<Guid> PreviousGuids { get; }
        public IReadOnlyList<ResearchConsumeItem> ConsumeItems { get; }
        public ResearchUnlockSet Unlocks { get; }

        public ResearchNodeDefinition(Guid guid, ItemId iconItemId, Vector2 treePosition, IReadOnlyList<Guid> previousGuids, IReadOnlyList<ResearchConsumeItem> consumeItems, ResearchUnlockSet unlocks)
        {
            Guid = guid;
            IconItemId = iconItemId;
            TreePosition = treePosition;
            PreviousGuids = previousGuids;
            ConsumeItems = consumeItems;
            Unlocks = unlocks;
        }
    }
}
```

`ResearchNodeCatalog.cs`:
```csharp
using System.Collections.Generic;

namespace Client.Ui.State.Research.Catalog
{
    /// <summary>
    /// 研究ノードの定義の列。マスタはゲーム中に変わらないので生成時に 1 回だけ組む
    /// The list of research node definitions; the master never changes in play, so it is built once at construction
    /// </summary>
    public sealed class ResearchNodeCatalog
    {
        public IReadOnlyList<ResearchNodeDefinition> Nodes { get; }

        public ResearchNodeCatalog()
        {
            Nodes = ResearchNodeDefinitionsFromMaster.Build();
        }
    }
}
```

- [ ] **Step 3: マスタからの写しを書く**

`ResearchNodeDefinitionsFromMaster.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Item;
using Core.Master;
using Mooresmaster.Model.ResearchModule;
using UnityEngine;

namespace Client.Ui.State.Research.Catalog
{
    /// <summary>
    /// 研究マスタの全ノードを定義へ写す（Web の橋 ResearchNodeDtoFactory と同じ読み方）
    /// Copies every research master node into a definition (same reading as the Web bridge ResearchNodeDtoFactory)
    /// </summary>
    public static class ResearchNodeDefinitionsFromMaster
    {
        public static IReadOnlyList<ResearchNodeDefinition> Build()
        {
            var masters = MasterHolder.ResearchMaster.GetAllResearches();
            var definitions = new List<ResearchNodeDefinition>(masters.Count);
            foreach (var master in masters) definitions.Add(ToDefinition(master));
            return definitions;

            #region Internal

            ResearchNodeDefinition ToDefinition(ResearchNodeMasterElement master)
            {
                // アイコンと座標はグラフ表示の設定から読む
                // Icon and position come from the graph view settings
                var iconItemId = MasterHolder.ItemMaster.GetItemId(master.GraphViewSettings.IconItem);
                var position = new Vector2(master.GraphViewSettings.UIPosition.x, master.GraphViewSettings.UIPosition.y);
                var previous = new List<Guid>(master.PrevResearchNodeGuids);
                return new ResearchNodeDefinition(master.ResearchNodeGuid, iconItemId, position, previous, ConsumeItemsOf(master), ResearchUnlockSetFromMaster.Build(master));
            }

            // 消費アイテムは上位レベルで代替できる ID 群ごと運ぶ（割当の 2 パスが読む）
            // Consume items carry the higher-level substitutes too (the two-pass allocation reads them)
            IReadOnlyList<ResearchConsumeItem> ConsumeItemsOf(ResearchNodeMasterElement master)
            {
                var items = new List<ResearchConsumeItem>(master.ConsumeItems.Length);
                foreach (var consume in master.ConsumeItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(consume.ItemGuid);
                    items.Add(new ResearchConsumeItem(itemId, consume.ItemCount, LevelFamilySubstituteUtil.GetAcceptedItemIds(itemId)));
                }
                return items;
            }

            #endregion
        }
    }
}
```
（`master.ConsumeItems.Length`・`PrevResearchNodeGuids` の型は生成物に合わせる。配列でなければ `Count`。コンパイルで確かめる）

`ResearchUnlockSetFromMaster.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.ResearchModule;

namespace Client.Ui.State.Research.Catalog
{
    /// <summary>
    /// 研究の完了アクションから、詳細ペインに出す解放物 8 種を抜き出す（Web の ResearchNodeDtoFactory と同じ集合）
    /// Extracts the eight unlock kinds shown in the detail pane from a research's cleared actions (same set as the Web ResearchNodeDtoFactory)
    /// </summary>
    public static class ResearchUnlockSetFromMaster
    {
        // 解放はされるが研究画面に出さない種別（Web の ResearchUnlockGameActionCoverageTest の非表示台帳と同じ 7 種）
        // Kinds unlocked but not shown on the research screen (same seven as the Web ResearchUnlockGameActionCoverageTest hidden ledger)
        private static readonly HashSet<string> HiddenGameActionTypes = new()
        {
            GameActionElement.GameActionTypeConst.unlockCraftRecipe,
            GameActionElement.GameActionTypeConst.unlockChallengeCategory,
            GameActionElement.GameActionTypeConst.unlockItemStackLevel,
            GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel,
            GameActionElement.GameActionTypeConst.unlockBlueprint,
            GameActionElement.GameActionTypeConst.playSkit,
            GameActionElement.GameActionTypeConst.playBackgroundSkit,
        };

        public static ResearchUnlockSet Build(ResearchNodeMasterElement master)
        {
            var blocks = new List<UnlockBlock>();
            var machineRecipeUnlocks = new List<UnlockMachineRecipe>();
            var itemRecipeViews = new List<ItemId>();
            var rewards = new List<RewardItem>();
            var connectTools = new List<Guid>();
            var trainCars = new List<Guid>();
            var moduleSlotLimits = new List<int>();
            var craftCaps = new List<LevelInheritanceCraftCap>();
            foreach (var action in master.ClearedActions.items) Append(action);
            return new ResearchUnlockSet(blocks, machineRecipeUnlocks, itemRecipeViews, rewards, connectTools, trainCars, moduleSlotLimits, craftCaps);

            #region Internal

            void Append(GameActionElement action)
            {
                switch (action.GameActionParam)
                {
                    case GiveItemGameActionParam give:
                        foreach (var reward in give.RewardItems) rewards.Add(new RewardItem(MasterHolder.ItemMaster.GetItemId(reward.ItemGuid), reward.ItemCount));
                        break;
                    case UnlockItemRecipeViewGameActionParam view:
                        foreach (var itemGuid in view.UnlockItemGuids) itemRecipeViews.Add(MasterHolder.ItemMaster.GetItemId(itemGuid));
                        break;
                    case UnlockBlockGameActionParam block:
                        foreach (var blockGuid in block.UnlockBlockGuids) blocks.Add(new UnlockBlock(MasterHolder.BlockMaster.GetBlockId(blockGuid), blockGuid));
                        break;
                    case UnlockMachineRecipeGameActionParam recipe:
                        foreach (var recipeGuid in recipe.UnlockMachineRecipeGuids) AppendMachineRecipe(recipeGuid);
                        break;
                    case UnlockConnectToolGameActionParam tool:
                        connectTools.AddRange(tool.UnlockConnectToolGuids);
                        break;
                    case UnlockTrainCarGameActionParam car:
                        trainCars.AddRange(car.UnlockTrainCarGuids);
                        break;
                    case UnlockModuleSlotStackLevelGameActionParam moduleSlot:
                        // レベルでなく、プレイヤーが読む上限値で運ぶ
                        // Carry the limit value the player reads, not the level
                        moduleSlotLimits.Add(ModuleSlotStackLimitMasterUtil.GetStackLimit(moduleSlot.Level));
                        break;
                    case UnlockLevelInheritanceCraftLevelGameActionParam craftCap:
                        foreach (var itemGuid in craftCap.TargetItemGuids) craftCaps.Add(new LevelInheritanceCraftCap(MasterHolder.ItemMaster.GetItemId(itemGuid), craftCap.Level));
                        break;
                    default:
                        // 出さないと決めた 7 種だけ通し、それ以外は表示の裁定が要るので止める（.decisions 2026-08-19「研究の未対応GameActionTypeは例外で落とす」）
                        // Only the seven decided-hidden kinds pass; anything else needs a show-or-hide decision, so stop (.decisions 2026-08-19)
                        if (!HiddenGameActionTypes.Contains(action.GameActionType)) throw new InvalidOperationException($"[ResearchUnlockSetFromMaster] 研究 {master.ResearchNodeGuid} の未対応の GameActionType {action.GameActionType}");
                        break;
                }
            }

            // 機械レシピはレシピ単位でアイテムと液体の両方を運ぶ。未解放のレシピも引くのでマスタから読む（D-13）
            // Each machine recipe carries both item and fluid outputs; locked recipes are needed too, so read the master (D-13)
            void AppendMachineRecipe(Guid recipeGuid)
            {
                // 参照切れは ResearchMasterUtil の検証で MasterHolder.Load が止めるので、ここでは null を見ない
                // Dangling GUIDs are rejected by ResearchMasterUtil validation in MasterHolder.Load, so no null check here
                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid);
                var items = new List<ItemId>(recipe.OutputItems.Length);
                foreach (var output in recipe.OutputItems) items.Add(MasterHolder.ItemMaster.GetItemId(output.ItemGuid));
                var fluids = new List<UnlockFluid>(recipe.OutputFluids.Length);
                foreach (var output in recipe.OutputFluids) fluids.Add(new UnlockFluid(MasterHolder.FluidMaster.GetFluidId(output.FluidGuid), output.Amount));
                machineRecipeUnlocks.Add(new UnlockMachineRecipe(recipeGuid, items, fluids));
            }

            #endregion
        }
    }
}
```
（型での `switch` は Web の橋の `GameActionType` の文字列の `switch` と同じ集合。生成物の param 型の名前は `S/Client.WebUiHost/Game/Topics/Research/ResearchNodeDtoFactory.cs` の using とキャストから写した。`OutputItems`・`OutputFluids` が配列でなければ `Count`。コンパイルで確かめる）

`Client.Ui.State.asmdef` の `references` の `"Core.Master",` の次に `"Core.Item",` を 1 行足す（`LevelFamilySubstituteUtil`。循環なし: `Core.Item` はクライアントを参照しない）

- [ ] **Step 4: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.State.Research.ResearchCatalogFromMasterTest"`
Expected: ErrorCount 0 / 5 PASS。`IconItem` が空のノード（テスト mod）は `GetItemId(Guid.Empty)` が空の ID を返すので落ちない（`ItemMaster.GetItemId` の実物 73 行目）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Research/Catalog moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Research/ResearchCatalogFromMasterTest.cs
git commit -m "feat(uitk): 研究マスタから研究ノードの静的な定義と解放物 8 種を写す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
（`git add` が自動生成の `.meta` を拾うのは可。コミットフックが同じコマンドの未追跡を検査するので、`git add` と `git commit` は別のコマンドで流してよい）

---

### Task 3: 選択と ViewModel（`Client.Ui.State/Research/Tree`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Tree/ResearchSelection.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Tree/ResearchNodeCardViewModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Tree/ResearchDetailViewModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Tree/ResearchTreeViewModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Tree/ResearchTreeViewModelFactory.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Research/ResearchSelectionTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Research/ResearchTreeViewModelFactoryTest.cs`

**Interfaces:**
- Consumes: Task 2 の `ResearchNodeDefinition`、L0-s4b の `ResearchLogic`・`UnlockEntries`・`ResearchButtonState`・`NodeCardState`・`ResearchNodeSummary`
- Produces:
  - `public sealed class ResearchSelection { public static readonly ResearchSelection None; public static ResearchSelection Of(Guid guid); public bool Is(Guid guid); public bool TryGetGuid(out Guid guid); public ResearchSelection Toggled(Guid guid); }`
  - `public sealed class ResearchNodeCardViewModel { public Guid Guid { get; } public ItemId IconItemId { get; } public Vector2 TreePosition { get; } public IReadOnlyList<Guid> PreviousGuids { get; } public NodeCardState CardState { get; } public LocalizationKey StateLabelKey { get; } public bool Selected { get; } public ResearchNodeCardViewModel(Guid guid, ItemId iconItemId, Vector2 treePosition, IReadOnlyList<Guid> previousGuids, NodeCardState cardState, LocalizationKey stateLabelKey, bool selected); }`
  - `public sealed class ResearchConsumeSlotViewModel { public ItemId ItemId { get; } public int Required { get; } public int Allocated { get; } public bool Lacking { get; } public ResearchConsumeSlotViewModel(ItemId itemId, int required, int allocated, bool lacking); }`
  - `public sealed class ResearchDetailViewModel { public Guid Guid { get; } public ResearchButtonState Button { get; } public IReadOnlyList<ResearchConsumeSlotViewModel> ConsumeSlots { get; } public IReadOnlyList<UnlockEntry> Unlocks { get; } public ResearchDetailViewModel(Guid guid, ResearchButtonState button, IReadOnlyList<ResearchConsumeSlotViewModel> consumeSlots, IReadOnlyList<UnlockEntry> unlocks); }`
  - `public sealed class ResearchTreeViewModel { public static readonly ResearchTreeViewModel Empty; public IReadOnlyList<ResearchNodeCardViewModel> Nodes { get; } public static ResearchTreeViewModel Create(IReadOnlyList<ResearchNodeCardViewModel> nodes, ResearchFocusPoint focus); public static ResearchTreeViewModel CreateWithDetail(IReadOnlyList<ResearchNodeCardViewModel> nodes, ResearchFocusPoint focus, ResearchDetailViewModel detail); public bool TryGetInitialFocus(out Vector2 treePoint); public bool TryGetDetail(out ResearchDetailViewModel detail); }`（詳細の無さは生成の別で表し、null を合図にしない）
  - `public readonly struct ResearchFocusPoint { public static ResearchFocusPoint None { get; } public static ResearchFocusPoint At(Vector2 treePoint); public bool TryGetPoint(out Vector2 treePoint); }`（`ResearchTreeViewModel.cs` に同居）
  - `public static class ResearchTreeViewModelFactory { public static ResearchTreeViewModel Create(IReadOnlyList<ResearchNodeDefinition> definitions, IReadOnlyDictionary<Guid, ResearchNodeState> states, IReadOnlyDictionary<ItemId, int> owned, ResearchSelection selection); }`
  - ViewModel は値の等価を持たない（組み直すたびに配る。言語が変わったときも描き直させるため。D-4）


- [ ] **Step 1: テストを書く**

`ResearchSelectionTest.cs`:
```csharp
using System;
using Client.Ui.State.Research.Tree;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// 選択の切り替え（同じノードで閉じる・別のノードへ移る）
    /// Selection toggling (same node closes, another node moves)
    /// </summary>
    public class ResearchSelectionTest
    {
        private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");

        [Test]
        public void 同じノードの再タップで閉じ別のノードは移る()
        {
            var selected = ResearchSelection.None.Toggled(A);
            Assert.IsTrue(selected.Is(A));
            Assert.IsTrue(selected.Toggled(B).Is(B));
            Assert.IsFalse(selected.Toggled(A).TryGetGuid(out _));
        }
    }
}
```

`ResearchTreeViewModelFactoryTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Research.Tree;
using Core.Master;
using Game.Research;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// 定義・状態・所持数・選択から研究ツリーの ViewModel を組む（L0）
    /// Builds the research tree view model from definitions, states, owned counts and selection (L0)
    /// </summary>
    public class ResearchTreeViewModelFactoryTest
    {
        private static readonly Guid Root = Guid.Parse("00000000-0000-0000-0000-000000000101");
        private static readonly Guid Child = Guid.Parse("00000000-0000-0000-0000-000000000102");
        private static readonly ItemId Ore = new(1);
        private static readonly ItemId OreLevel2 = new(2);

        [Test]
        public void 状態の無いノードは全条件未達で描き注目点も無い()
        {
            var model = ResearchTreeViewModelFactory.Create(Definitions(), new Dictionary<Guid, ResearchNodeState>(), Owned(0, 0), ResearchSelection.None);

            Assert.IsTrue(model.Nodes[0].CardState.Locked);
            Assert.AreEqual(LocalizationKeys.Ui.Research.StateUnavailable, model.Nodes[0].StateLabelKey);
            Assert.IsFalse(model.TryGetInitialFocus(out _));
            Assert.IsFalse(model.TryGetDetail(out _));
        }

        [Test]
        public void 研究可能なノードの座標が注目点になり選択の印が付く()
        {
            var states = new Dictionary<Guid, ResearchNodeState> { { Root, ResearchNodeState.Completed }, { Child, ResearchNodeState.Researchable } };
            var model = ResearchTreeViewModelFactory.Create(Definitions(), states, Owned(5, 0), ResearchSelection.Of(Child));

            Assert.IsTrue(model.TryGetInitialFocus(out var focus));
            Assert.AreEqual(new Vector2(200f, 0f), focus);
            Assert.IsFalse(model.Nodes[0].Selected);
            Assert.IsTrue(model.Nodes[1].Selected);
            Assert.IsTrue(model.Nodes[1].CardState.Ready);
        }

        [Test]
        public void 詳細は割当と不足を消費アイテムごとに持ち完了なら不足を出さない()
        {
            var lackingStates = new Dictionary<Guid, ResearchNodeState> { { Child, ResearchNodeState.UnresearchableNotEnoughItem } };
            var lacking = Detail(ResearchTreeViewModelFactory.Create(Definitions(), lackingStates, Owned(1, 1), ResearchSelection.Of(Child)));
            var completedStates = new Dictionary<Guid, ResearchNodeState> { { Child, ResearchNodeState.Completed } };
            var completed = Detail(ResearchTreeViewModelFactory.Create(Definitions(), completedStates, Owned(1, 1), ResearchSelection.Of(Child)));

            // 必要 3 に対し、単一受付の 1 個と上位レベルの 1 個を割り当てて 2 個で不足
            // 3 required; one exact and one higher-level item are allocated, so 2 is short
            Assert.AreEqual(3, lacking.ConsumeSlots[0].Required);
            Assert.AreEqual(2, lacking.ConsumeSlots[0].Allocated);
            Assert.IsTrue(lacking.ConsumeSlots[0].Lacking);
            Assert.IsFalse(lacking.Button.Interactable);
            Assert.IsFalse(completed.ConsumeSlots[0].Lacking);
            Assert.IsTrue(completed.Button.Completed);
        }

        #region Helpers

        private static ResearchDetailViewModel Detail(ResearchTreeViewModel model)
        {
            Assert.IsTrue(model.TryGetDetail(out var detail));
            return detail;
        }

        private static IReadOnlyDictionary<ItemId, int> Owned(int ore, int oreLevel2)
        {
            return new Dictionary<ItemId, int> { { Ore, ore }, { OreLevel2, oreLevel2 } };
        }

        private static IReadOnlyList<ResearchNodeDefinition> Definitions()
        {
            var noUnlocks = new ResearchUnlockSet(new List<UnlockBlock>(), new List<UnlockMachineRecipe>(), new List<ItemId>(), new List<RewardItem>(), new List<Guid>(), new List<Guid>(), new List<int>(), new List<LevelInheritanceCraftCap>());
            var consume = new List<ResearchConsumeItem> { new(Ore, 3, new List<ItemId> { Ore, OreLevel2 }) };
            return new List<ResearchNodeDefinition>
            {
                new(Root, Ore, Vector2.zero, new List<Guid>(), new List<ResearchConsumeItem>(), noUnlocks),
                new(Child, Ore, new Vector2(200f, 0f), new List<Guid> { Root }, consume, noUnlocks),
            };
        }

        #endregion
    }
}
```

- [ ] **Step 2: 値の型を書く**

`ResearchSelection.cs`:
```csharp
using System;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 詳細ペインに出す研究ノードの選択。無いことを型で持つ
    /// The research node selected for the detail pane; absence is part of the type
    /// </summary>
    public sealed class ResearchSelection
    {
        public static readonly ResearchSelection None = new(false, Guid.Empty);

        private readonly bool _hasNode;
        private readonly Guid _guid;

        private ResearchSelection(bool hasNode, Guid guid)
        {
            _hasNode = hasNode;
            _guid = guid;
        }

        public static ResearchSelection Of(Guid guid)
        {
            return new ResearchSelection(true, guid);
        }

        public bool Is(Guid guid)
        {
            return _hasNode && _guid == guid;
        }

        public bool TryGetGuid(out Guid guid)
        {
            guid = _guid;
            return _hasNode;
        }

        // 同じノードの再タップで閉じる（Web の toggleSelect）
        // Tapping the same node again closes it (the Web toggleSelect)
        public ResearchSelection Toggled(Guid guid)
        {
            return Is(guid) ? None : Of(guid);
        }
    }
}
```

`ResearchNodeCardViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 研究ノードのカード 1 枚の表示（4 状態・状態ラベル・選択）
    /// Display of one research node card (four states, state label, selection)
    /// </summary>
    public sealed class ResearchNodeCardViewModel
    {
        public Guid Guid { get; }
        public ItemId IconItemId { get; }
        public Vector2 TreePosition { get; }
        public IReadOnlyList<Guid> PreviousGuids { get; }
        public NodeCardState CardState { get; }
        public LocalizationKey StateLabelKey { get; }
        public bool Selected { get; }

        public ResearchNodeCardViewModel(Guid guid, ItemId iconItemId, Vector2 treePosition, IReadOnlyList<Guid> previousGuids, NodeCardState cardState, LocalizationKey stateLabelKey, bool selected)
        {
            Guid = guid;
            IconItemId = iconItemId;
            TreePosition = treePosition;
            PreviousGuids = previousGuids;
            CardState = cardState;
            StateLabelKey = stateLabelKey;
            Selected = selected;
        }
    }
}
```

`ResearchDetailViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Master;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 選択ノードの詳細（研究ボタンの状態・消費アイテムの割当・解放物）
    /// Details of the selected node (research button state, consume allocation, unlocks)
    /// </summary>
    public sealed class ResearchDetailViewModel
    {
        public Guid Guid { get; }
        public ResearchButtonState Button { get; }
        public IReadOnlyList<ResearchConsumeSlotViewModel> ConsumeSlots { get; }
        public IReadOnlyList<UnlockEntry> Unlocks { get; }

        public ResearchDetailViewModel(Guid guid, ResearchButtonState button, IReadOnlyList<ResearchConsumeSlotViewModel> consumeSlots, IReadOnlyList<UnlockEntry> unlocks)
        {
            Guid = guid;
            Button = button;
            ConsumeSlots = consumeSlots;
            Unlocks = unlocks;
        }
    }

    /// <summary>
    /// 消費アイテム 1 枠（必要数・割り当てた所持数・不足）。UITK には所持数の未受信が無いので割当は常にある（A-13）
    /// One consume slot (required, allocated owned count, shortage); UITK has no unreceived inventory, so the allocation always exists (A-13)
    /// </summary>
    public sealed class ResearchConsumeSlotViewModel
    {
        public ItemId ItemId { get; }
        public int Required { get; }
        public int Allocated { get; }
        public bool Lacking { get; }

        public ResearchConsumeSlotViewModel(ItemId itemId, int required, int allocated, bool lacking)
        {
            ItemId = itemId;
            Required = required;
            Allocated = allocated;
            Lacking = lacking;
        }
    }
}
```

`ResearchTreeViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 研究ツリー画面の表示。値の等価を持たず、組み直すたびに配る（言語の変化でも描き直させる）
    /// Display of the research tree screen; no value equality, so every rebuild is published (language changes redraw too)
    /// </summary>
    public sealed class ResearchTreeViewModel
    {
        public static readonly ResearchTreeViewModel Empty = Create(Array.Empty<ResearchNodeCardViewModel>(), ResearchFocusPoint.None);

        private readonly ResearchFocusPoint _focus;
        private readonly bool _hasDetail;
        private readonly ResearchDetailViewModel _detail;

        public IReadOnlyList<ResearchNodeCardViewModel> Nodes { get; }

        private ResearchTreeViewModel(IReadOnlyList<ResearchNodeCardViewModel> nodes, ResearchFocusPoint focus, bool hasDetail, ResearchDetailViewModel detail)
        {
            Nodes = nodes;
            _focus = focus;
            _hasDetail = hasDetail;
            _detail = detail;
        }

        public static ResearchTreeViewModel Create(IReadOnlyList<ResearchNodeCardViewModel> nodes, ResearchFocusPoint focus)
        {
            return new ResearchTreeViewModel(nodes, focus, false, null);
        }

        public static ResearchTreeViewModel CreateWithDetail(IReadOnlyList<ResearchNodeCardViewModel> nodes, ResearchFocusPoint focus, ResearchDetailViewModel detail)
        {
            return new ResearchTreeViewModel(nodes, focus, true, detail);
        }

        public bool TryGetInitialFocus(out Vector2 treePoint)
        {
            return _focus.TryGetPoint(out treePoint);
        }

        public bool TryGetDetail(out ResearchDetailViewModel detail)
        {
            detail = _detail;
            return _hasDetail;
        }
    }

    /// <summary>
    /// 初期の注目点（研究可能か素材待ちの最前線）。無いことを型で持つ
    /// Initial focus point (researchable or item-waiting frontier); absence is part of the type
    /// </summary>
    public readonly struct ResearchFocusPoint
    {
        public static ResearchFocusPoint None => new(false, Vector2.zero);

        private readonly bool _hasPoint;
        private readonly Vector2 _point;

        private ResearchFocusPoint(bool hasPoint, Vector2 point)
        {
            _hasPoint = hasPoint;
            _point = point;
        }

        public static ResearchFocusPoint At(Vector2 treePoint)
        {
            return new ResearchFocusPoint(true, treePoint);
        }

        public bool TryGetPoint(out Vector2 treePoint)
        {
            treePoint = _point;
            return _hasPoint;
        }
    }
}
```

- [ ] **Step 3: 組み立ての純関数を書く**

`ResearchTreeViewModelFactory.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Game.Research;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 定義・サーバーの状態・所持数・選択から研究ツリーの表示を組む純関数
    /// Pure function building the research tree display from definitions, server states, owned counts and selection
    /// </summary>
    public static class ResearchTreeViewModelFactory
    {
        public static ResearchTreeViewModel Create(IReadOnlyList<ResearchNodeDefinition> definitions, IReadOnlyDictionary<Guid, ResearchNodeState> states, IReadOnlyDictionary<ItemId, int> owned, ResearchSelection selection)
        {
            var cards = new List<ResearchNodeCardViewModel>(definitions.Count);
            var summaries = new List<ResearchNodeSummary>(definitions.Count);
            var hasDetail = false;
            ResearchDetailViewModel detail = null;
            foreach (var definition in definitions) AddNode(definition);

            // 注目点は研究可能を優先し、無ければ素材待ちの最前線
            // Focus prefers researchable, else the item-waiting frontier
            var focus = ResearchLogic.TryFindInitialFocusNode(summaries, out var focusNode) ? ResearchFocusPoint.At(PositionOf(focusNode.Guid)) : ResearchFocusPoint.None;
            return hasDetail ? ResearchTreeViewModel.CreateWithDetail(cards, focus, detail) : ResearchTreeViewModel.Create(cards, focus);

            #region Internal

            void AddNode(ResearchNodeDefinition definition)
            {
                var state = StateOf(definition.Guid);
                var cardState = ResearchLogic.DeriveNodeCardState(state);
                var selected = selection.Is(definition.Guid);
                cards.Add(new ResearchNodeCardViewModel(definition.Guid, definition.IconItemId, definition.TreePosition, definition.PreviousGuids, cardState, ResearchLogic.DeriveNodeStateLabelKey(cardState), selected));
                summaries.Add(new ResearchNodeSummary(definition.Guid, state));
                if (!selected) return; // silent-ok: 選択していないノードは詳細を組まない / 選択は ResearchSelectionStore が持ち L1 が詳細の有無を見る
                hasDetail = true;
                detail = BuildDetail(definition, state);
            }

            // サーバーの状態が無いノードは全条件未達（Web の橋と同じ既定）
            // Nodes without server state are all-reasons-unmet (same default as the Web bridge)
            ResearchNodeState StateOf(Guid guid)
            {
                // silent-ok: 取り直しの前は全ノードが未着 / ResearchNodeStateStore の OnNodeStatesChanged で組み直される
                return states.TryGetValue(guid, out var state) ? state : ResearchNodeState.UnresearchableAllReasons;
            }

            ResearchDetailViewModel BuildDetail(ResearchNodeDefinition definition, ResearchNodeState state)
            {
                // 割当はサーバーと同じ 2 パス。不足の強調は完了で出さない
                // Allocation follows the server's two passes; completed nodes never show shortage
                var allocated = ResearchLogic.AllocateResearchItems(definition.ConsumeItems, owned);
                var slots = new List<ResearchConsumeSlotViewModel>(definition.ConsumeItems.Count);
                for (var i = 0; i < definition.ConsumeItems.Count; i++)
                {
                    var consume = definition.ConsumeItems[i];
                    slots.Add(new ResearchConsumeSlotViewModel(consume.ItemId, consume.Count, allocated[i], ResearchLogic.IsConsumeItemLacking(state, consume.Count, allocated[i])));
                }
                return new ResearchDetailViewModel(definition.Guid, ResearchLogic.DeriveResearchButton(state), slots, UnlockEntries.ToUnlockEntries(definition.Unlocks));
            }

            UnityEngine.Vector2 PositionOf(Guid guid)
            {
                foreach (var definition in definitions)
                {
                    if (definition.Guid == guid) return definition.TreePosition;
                }
                throw new InvalidOperationException($"注目点のノード {guid} が定義に無い");
            }

            #endregion
        }
    }
}
```
（`PositionOf` の例外は到達しない: 注目点は同じ `definitions` から作った `summaries` から選ぶ）

- [ ] **Step 4: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.Research\."`
Expected: ErrorCount 0 / 新しい 4 件と L0-s4b の既存がすべて PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Research/Tree moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Research/ResearchSelectionTest.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Research/ResearchTreeViewModelFactoryTest.cs
git commit -m "feat(uitk): 研究ツリーの選択と ViewModel を組む純関数を足す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 4: 研究ツリーの Model と選択の持ち主

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Tree/ResearchSelectionStore.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Tree/ResearchTreeModel.cs`

**Interfaces:**
- Consumes: Task 2 の `ResearchNodeCatalog.Nodes`、Task 3 の `ResearchTreeViewModelFactory.Create`・`ResearchSelection`、`ResearchNodeStateStore`・`LocalPlayerInventoryController`（変化の通知だけ）・`IPlayerInventoryOperations.ReadSnapshot().MainSlots`（所持の読み口。GameUi で `GrabGameUiRegistration` が登録済み）・`UiRoutingState`（選択の持ち主だけ）・`OwnedItemCounts.Build`・`Localize.OnLanguageChanged`
- Produces:
  - `public sealed class ResearchSelectionStore : IInitializable { public IReadOnlyReactiveProperty<ResearchSelection> Current { get; } public ResearchSelectionStore(UiRoutingState routing); public void Toggle(Guid researchGuid); public void Clear(); }`（研究画面を離れたら `None` へ戻す）
  - `public sealed class ResearchTreeModel : IInitializable { public IReadOnlyReactiveProperty<ResearchTreeViewModel> Current { get; } public ResearchTreeModel(ResearchNodeCatalog catalog, ResearchNodeStateStore store, ResearchSelectionStore selection, LocalPlayerInventoryController inventory, IPlayerInventoryOperations inventoryOperations); }`（源の変化のたびに同期で組み直す。表示の有無は Presenter と View が持つ。前例 `S/Client.Ui.State/Challenge/CurrentChallengeHudModel.cs`・`Inventory/Grab/GrabOverlayModel.cs` [A-57]）

- [ ] **Step 1: 選択の持ち主を書く**

`ResearchSelectionStore.cs`:
```csharp
using System;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 研究ツリーの選択を持つ。画面を離れたら捨てる（Web はパネルのアンマウントで選択が消える）
    /// Holds the research tree selection and drops it on leaving the screen (the Web loses it when the panel unmounts)
    /// </summary>
    public sealed class ResearchSelectionStore : IInitializable
    {
        private readonly UiRoutingState _routing;
        private readonly ReactiveProperty<ResearchSelection> _current = new(ResearchSelection.None);

        public IReadOnlyReactiveProperty<ResearchSelection> Current => _current;

        public ResearchSelectionStore(UiRoutingState routing)
        {
            _routing = routing;
        }

        public void Initialize()
        {
            _routing.Current.Subscribe(snapshot =>
            {
                // silent-ok: 研究画面の中の経路の通知は選択を保つ / 離れたときの None は録画 uitk-research-tree が見る
                if (snapshot.Screen == UiScreen.ResearchTree) return;
                _current.Value = ResearchSelection.None;
            });
        }

        public void Toggle(Guid researchGuid)
        {
            _current.Value = _current.Value.Toggled(researchGuid);
        }

        public void Clear()
        {
            _current.Value = ResearchSelection.None;
        }
    }
}
```

- [ ] **Step 2: Model を書く**

`ResearchTreeModel.cs`:
```csharp
using Client.Game.InGame.UI.Inventory.Main;
using Client.Localization;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.Research.Catalog;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 研究ツリーの表示を源から組み、源が動くたびに同期で組み直して配る（A-57）。表示の有無は Presenter と View が持つ
    /// Builds the research tree display from its sources and rebuilds it synchronously on every change (A-57); showing is owned by the Presenter and View
    /// </summary>
    public sealed class ResearchTreeModel : IInitializable
    {
        private readonly ResearchNodeCatalog _catalog;
        private readonly ResearchNodeStateStore _store;
        private readonly ResearchSelectionStore _selection;
        private readonly LocalPlayerInventoryController _inventory;
        private readonly IPlayerInventoryOperations _inventoryOperations;
        private readonly ReactiveProperty<ResearchTreeViewModel> _current = new(ResearchTreeViewModel.Empty);

        public IReadOnlyReactiveProperty<ResearchTreeViewModel> Current => _current;

        public ResearchTreeModel(ResearchNodeCatalog catalog, ResearchNodeStateStore store, ResearchSelectionStore selection, LocalPlayerInventoryController inventory, IPlayerInventoryOperations inventoryOperations)
        {
            _catalog = catalog;
            _store = store;
            _selection = selection;
            _inventory = inventory;
            _inventoryOperations = inventoryOperations;
        }

        public void Initialize()
        {
            // 状態・選択・所持・言語のどれが動いても組み直す（選択の購読が初回の 1 回も組む）
            // Rebuild when state, selection, inventory or language changes (the selection subscription also builds the first one)
            _store.OnNodeStatesChanged.Subscribe(_ => Rebuild());
            _inventory.LocalPlayerInventory.OnItemChange.Subscribe(_ => Rebuild());
            _inventory.OnInventoryRefreshed.Subscribe(_ => Rebuild());
            Localize.OnLanguageChanged.Subscribe(_ => Rebuild());
            _selection.Current.Subscribe(_ => Rebuild());
        }

        private void Rebuild()
        {
            // 所持数はメインの枠だけで数える（Web の buildOwnedCounts(inventory.mainSlots)。読み口は持ち物の操作と同じ）
            // Owned counts come from the main slots only (the Web buildOwnedCounts(inventory.mainSlots); same read port as inventory operations)
            var owned = OwnedItemCounts.Build(_inventoryOperations.ReadSnapshot().MainSlots);
            _current.Value = ResearchTreeViewModelFactory.Create(_catalog.Nodes, _store.NodeStates, owned, _selection.Current.Value);
        }
    }
}
```

- [ ] **Step 3: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0（Model の振る舞いは Task 3 の純関数の L0 と Task 12 の録画が見る。持ち物の源は MonoBehaviour と通信を要するので L0 を持たない。`OwnedItemCounts.Build` の引数の型が `IReadOnlyList<SlotItem>` でなければ実物に合わせる）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Research/Tree/ResearchSelectionStore.cs moorestech_client/Assets/Scripts/Client.Ui.State/Research/Tree/ResearchTreeModel.cs
git commit -m "feat(uitk): 研究ツリーの Model と選択の持ち主を足す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 5: 操作（`IResearchTreeCommands`）と拒否理由 `ResearchFailed`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Commands/IResearchTreeCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Research/Commands/ResearchTreeCommands.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs`（共有区画）
- Modify: `Localization/localization.csv`・`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`・`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/PauseMenuCommandsTest.cs`（全値の網羅の行があれば）
- Modify: `docs/uitk/TODO.md`（「## 追従記録」の末尾 1 行）

**Interfaces:**
- Consumes: Task 4 の `ResearchSelectionStore.Toggle/Clear`、`ResearchNodeStateStore.ApplyNodeStates`、`ClientContext.VanillaApi.Response.Progression.CompleteResearch`
- Produces:
  - `public interface IResearchTreeCommands { void ToggleNodeSelection(Guid researchGuid); void CloseDetail(); UniTask<UiCommandResult> CompleteResearch(Guid researchGuid); }`
  - `public sealed class ResearchTreeCommands : IResearchTreeCommands { public const string CompleteResearchCommandName = "research.complete"; public ResearchTreeCommands(ResearchSelectionStore selection, ResearchNodeStateStore store); }`
  - `UiCommandRejectReason.ResearchFailed`（良性でない・wire `research_failed`・文言 `ui.commandRejected.researchFailed`）

- [ ] **Step 1: 拒否理由を足す**

`UiCommandRejectReason.cs` の enum の末尾（`SplitDragNoValidSlots,` の次。他レーンが先に足していればその次）に `ResearchFailed,`、3 つの switch の末尾の腕の次に 1 行ずつ:
```csharp
                UiCommandRejectReason.ResearchFailed => false,
```
```csharp
                UiCommandRejectReason.ResearchFailed => LocalizationKeys.Ui.CommandRejected.ResearchFailed,
```
```csharp
                UiCommandRejectReason.ResearchFailed => "research_failed",
```
`IsBenign` の上のコメントは触らない（Web の `BENIGN_ERRORS` に研究は無い）。

`localization.csv` の `ui.commandRejected.*` の群の末尾に 1 行:
```
ui.commandRejected.researchFailed,Research failed,Research failed,研究に失敗しました,Forschung fehlgeschlagen,연구에 실패했습니다
```
Run:
```bash
cd moorestech_web/webui && npm run gen:i18n && cd -
git diff --stat moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
grep -n "LanguageAlreadySelected\|SplitDragNoValidSlots" moorestech_client/Assets/Scripts/Client.Tests/Ui/State/PauseMenuCommandsTest.cs
```
Expected: 生成物 2 本が変わる（`_CompileRequester.cs` が変わらなければ SchemaWatcher の印の手順を `docs/uitk/TODO.md` の「## 追従記録」の前例どおりに足す）。`PauseMenuCommandsTest` の良性の一覧は true の値だけを並べるので変えない。全値を並べる行（`Enum.GetValues` でない手書きの網羅）があれば `ResearchFailed` を足す。台帳「## 追従記録」の末尾に「2026-11-xx S4-research: `ui.commandRejected.researchFailed` を足し `npm run gen:i18n` で `localizationKeys.ts` を再生成」を 1 行

- [ ] **Step 2: 操作を書く**

`IResearchTreeCommands.cs`:
```csharp
using System;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Research.Commands
{
    /// <summary>
    /// 研究ツリーの画面から呼ぶ操作（A-8）
    /// Operations the research tree screen calls (A-8)
    /// </summary>
    public interface IResearchTreeCommands
    {
        void ToggleNodeSelection(Guid researchGuid);
        void CloseDetail();
        UniTask<UiCommandResult> CompleteResearch(Guid researchGuid);
    }
}
```

`ResearchTreeCommands.cs`:
```csharp
using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Ui.State.Commands;
using Client.Ui.State.Research.Tree;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Ui.State.Research.Commands
{
    /// <summary>
    /// 研究ツリーの操作の本番。研究の実行は Web の橋 research.complete と同じ API と状態の反映をする
    /// Production research tree operations; running research uses the same API and state application as the Web bridge research.complete
    /// </summary>
    public sealed class ResearchTreeCommands : IResearchTreeCommands
    {
        public const string CompleteResearchCommandName = "research.complete";

        private readonly ResearchSelectionStore _selection;
        private readonly ResearchNodeStateStore _store;

        public ResearchTreeCommands(ResearchSelectionStore selection, ResearchNodeStateStore store)
        {
            _selection = selection;
            _store = store;
        }

        public void ToggleNodeSelection(Guid researchGuid)
        {
            _selection.Toggle(researchGuid);
        }

        public void CloseDetail()
        {
            _selection.Clear();
        }

        public async UniTask<UiCommandResult> CompleteResearch(Guid researchGuid)
        {
            // 応答なしは失敗。前の状態のまま次の取り直しに任せる
            // No response is a failure; keep the previous states for the next refetch
            var response = await ClientContext.VanillaApi.Response.Progression.CompleteResearch(researchGuid, CancellationToken.None);
            if (response == null)
            {
                Debug.LogWarning($"[ResearchTreeCommands] CompleteResearch {researchGuid} returned no response");
                return new UiCommandRejected(UiCommandRejectReason.ResearchFailed, "");
            }

            // 成否に関わらず応答の全状態を入れ、表示をサーバーの正へ寄せる
            // Apply the full response states whether or not it succeeded, converging the display to the server truth
            _store.ApplyNodeStates(response.NodeState.ToDictionary());
            return response.Success ? UiCommandAccepted.Instance : new UiCommandRejected(UiCommandRejectReason.ResearchFailed, "");
        }
    }
}
```
（`CancellationToken.None` は前例 `ResearchActions.cs` と同じ。ゲーム寿命のオブジェクトで、終了まで待ってよい〈AGENTS.md の既知の制約〉）

- [ ] **Step 3: コンパイルし既存のテストを流す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.(PauseMenuCommandsTest|Commands|Toast)"`
Expected: ErrorCount 0 / PASS（`UiCommandRejectReasonTraits` の 3 つの switch が全値を持つことを既存のテストが見る）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Research/Commands moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/State/PauseMenuCommandsTest.cs docs/uitk/TODO.md
git commit -m "feat(uitk): 研究ツリーの操作と拒否理由 ResearchFailed を足す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---
### Task 6: ロックの減光のトークン（`DerivedTokens.uss` の名札付きブロック）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss`（末尾の `}` の直前に名札付きブロック「S4-research」）

**Interfaces:**
- Consumes: なし
- Produces: `:root` のトークン `--research-node-locked-opacity`（Task 7 の USS が `var()` で読む。値は Task 11 Step 3 で決め直す）

- [ ] **Step 1: ブロックを足す**

`DerivedTokens.uss` の最後のブロック（他レーンのブロックがあればその後）に:
```css

    /* S4-research: 研究ノードのロックの減光。Web .node[data-locked] の opacity。要素の opacity は明るい字と枠を Linear 合成で淡く残すので、Task 11 Step 3 で画素を比べて決める（前例 --slot-frame-insufficient-opacity。設計書 A-52） */
    /* S4-research: dimming of locked research nodes, the Web .node[data-locked] opacity; element opacity keeps light text and borders faint under Linear blending, so Task 11 Step 3 settles it from pixels (precedent --slot-frame-insufficient-opacity; design A-52) */
    --research-node-locked-opacity: 0.45; /* web: .node[data-locked] { opacity: 0.45 } */
```

- [ ] **Step 2: コンパイルし L2 を流す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Theme|Conventions)\."`
Expected: ErrorCount 0 / PASS

- [ ] **Step 3: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss
git commit -m "feat(uitk): 研究ノードのロックの減光のトークンを足す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 7: 画面の USS とノードカード（`Screens/Research/Parts`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/ResearchTree.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/ResearchDetailPane.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/Parts/ResearchNodeCard.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/Parts/ResearchNodeCardSet.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/Parts/ResearchCalcKnobs.cs`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画の末尾に `@import` 2 行）

**Interfaces:**
- Consumes: Task 3 の `ResearchNodeCardViewModel`、Task 6 のトークン、C4-d の `NodeGraphNode`・`NodeGraphContent`・`NodeGraphFocus`、`ItemSlot`・`LocalizedLabel`・`UiPickingIgnore`・`TutorialAnchor`・`AnchorIds`
- Produces:
  - `public sealed class ResearchNodeCard : VisualElement { public ResearchNodeCard(Guid researchGuid); public void Render(ResearchNodeCardViewModel card, GameContentPresentation content); }`（name `research-node-<guid>`・クラス `research-tree__card`・修飾 `--completed`・`--ready`・`--locked`・`--selected`。状態ラベルの name `research-node-state-<guid>`）
  - `public sealed class ResearchNodeCardSet { public ResearchNodeCardSet(GameContentPresentation content); public bool RenderCards(IReadOnlyList<ResearchNodeCardViewModel> nodes); public NodeGraphContent BuildContent(IReadOnlyList<ResearchNodeCardViewModel> nodes, NodeGraphFocus focus); }`（`RenderCards` はノードの並びが前回と違うときだけ true）
  - `public static class ResearchCalcKnobs { public static void ApplyAreaLeft(VisualElement area); public static void ApplyScrollBleed(VisualElement scroll, VisualElement content); }`
  - USS のクラス（Task 8・9 が使う）: `research-tree`・`__panel`・`__graph`・`__detail-host`・`__detail`・`__detail-panel`・`__detail-body`・`__detail-header`・`__detail-name`・`__detail-close`・`__detail-close-symbol`・`__detail-scroll`・`__detail-scroll-content`・`__detail-description`・`__section`・`__section-label`・`__slots`・`__slot`・`__unlock-other`・`__unlock-fluid`・`__actions`・`__reason`

- [ ] **Step 1: 画面の USS を書く**

`ResearchTree.uss`:
```css
/* web: moorestech_web/webui/src/features/research/style.module.css（.researchArea〜.nodeState のエリアとカード。詳細ペインは ResearchDetailPane.uss）。data 属性は修飾クラス、gap は子の margin、calc() は ResearchCalcKnobs、outline は拾わない子の輪で写す */
/* web: moorestech_web/webui/src/features/research/style.module.css (.researchArea to .nodeState: area and cards; the detail pane is in ResearchDetailPane.uss); data attributes become modifier classes, gap becomes child margins, calc() goes to ResearchCalcKnobs, and the outline is a non-picking child ring */

/* 研究エリア: 上右下は舞台の端、left は C# が持ち物の幅と列の間隔の和で当てる（A-92） */
/* Research area: top/right/bottom at the stage edges; C# sets left to inventory width plus column gap (A-92) */
.research-tree {
    position: absolute;
    top: 0;
    right: 0;
    bottom: 0;
    min-width: 0;
    flex-direction: column;
    --research-tree-inventory-width: var(--inventory-panel-width);
    --research-tree-column-gap: var(--stage-column-gap);
}

/* パネルはエリアの高さいっぱい、グラフはパネルの本文いっぱい（Web の height: 100% と .treeContainer） */
/* The panel fills the area height and the graph fills the panel body (Web height: 100% and .treeContainer) */
.research-tree__panel {
    flex-grow: 1;
}

.research-tree__graph {
    flex-grow: 1;
}

/* ノードカード: 名前・アイコン・状態ラベルの縦積み。中心をノードの点に据える */
/* Node card: name, icon and state label stacked; centered on the node point */
.research-tree__card {
    width: 132px;
    flex-direction: column;
    align-items: center;
    padding: 8px 6px;
    background-color: var(--research-node-face);
    border-width: 1px;
    border-color: var(--research-node-border);
    translate: -50% -50%;
}

.research-tree__card--locked {
    opacity: var(--research-node-locked-opacity);
}

.research-tree__card--ready {
    border-color: var(--select-cyan);
}

.research-tree__card--completed {
    border-color: var(--text-default);
}

/* 選択の輪: Web の outline 2px（枠の外）。Yoga の絶対配置は親の padding 辺が基準なので枠 1px ぶん更に外へ */
/* Selection ring: the Web 2px outline outside the border; Yoga absolute offsets start at the padding edge, so go 1px further out */
.research-tree__card-ring {
    display: none;
    position: absolute;
    left: -3px;
    top: -3px;
    right: -3px;
    bottom: -3px;
    border-width: 2px;
    border-color: var(--text-high-contrast);
}

.research-tree__card--selected > .research-tree__card-ring {
    display: flex;
}

.research-tree__card-name {
    max-width: 100%;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    font-size: 14px;
    color: var(--text-default);
    margin-bottom: 6px;
}

.research-tree__card-icon {
    margin-bottom: 6px;
}

.research-tree__card-state {
    max-width: 100%;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    font-size: 12px;
    color: var(--text-default);
}
```

`ResearchDetailPane.uss`:
```css
/* web: moorestech_web/webui/src/features/research/style.module.css（.detailPane〜.unlockFluidAmount）。詳細ペイン・節・解放物・研究ボタンのつまみの上書き */
/* web: moorestech_web/webui/src/features/research/style.module.css (.detailPane to .unlockFluidAmount); detail pane, sections, unlocks and research button knob overrides */

/* 詳細ペイン: エリアの右上 12px・幅 300。高さの上限はエリア − 24（置き場の上下 12 で写す） */
/* Detail pane: 12px from the area's top right, 300 wide; the height cap is area − 24 (the host's 12px top/bottom) */
.research-tree__detail-host {
    position: absolute;
    top: 12px;
    right: 12px;
    bottom: 12px;
    width: 300px;
    flex-direction: column;
    align-items: stretch;
}

.research-tree__detail {
    flex-shrink: 1;
    min-height: 0;
    flex-direction: column;
}

.research-tree__detail-panel {
    flex-shrink: 1;
    min-height: 0;
}

/* 本文: 見出し・スクロール・研究ボタン・理由。研究ボタンは C4-c の部品のつまみを研究の組に上書きする（A-46） */
/* Body: header, scroll, research button, reason; the research button overrides the C4-c part knobs with the research set (A-46) */
.research-tree__detail-body {
    flex-direction: column;
    flex-shrink: 1;
    min-height: 0;
    padding: 10px;
    --recipe-action-hover-start: var(--recipe-action-background-start);
    --recipe-action-hover-middle: var(--recipe-action-background-middle);
    --recipe-action-hover-end: var(--recipe-action-background-end);
    --recipe-action-disabled-start: var(--research-action-disabled-face);
    --recipe-action-disabled-middle: var(--research-action-disabled-face);
    --recipe-action-disabled-end: var(--research-action-disabled-face);
    --recipe-action-disabled-text: var(--text-default);
    --recipe-action-disabled-opacity: var(--research-action-disabled-opacity);
    --recipe-action-width: auto; /* web: .researchButton は幅の指定なしで本文の幅いっぱい（300 − 枠 2 − GamePanel(craft) の左右 25 − 本文の左右 20 ＝ 253）。D-21 */
    --recipe-action-height: 36px; /* web: padding 8px 0 ＋ 14px の行の高さ normal。Task 11 Step 3 で画素から決め直す */
    --recipe-action-padding-block: 8px;
    --recipe-action-border-width: 0px; /* web: .researchButton の border: 0（C4-c の研究の組） */
    --recipe-action-font-size: 14px;
    --recipe-action-letter-spacing: 0px;
    --recipe-action-text-outline-width: 0px;
    --recipe-action-active-offset: 0px;
}

.research-tree__detail-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 8px;
}

.research-tree__detail-name {
    flex-shrink: 1;
    font-size: 16px;
    color: var(--text-high-contrast);
}

.research-tree__detail-close {
    padding: 0 4px;
    background-color: transparent;
}

.research-tree__detail-close-symbol {
    font-size: 16px;
    color: var(--text-default);
}

/* スクロールは左右へ逃げの分はみ出し、中身を同じだけ内へ戻す（ResearchCalcKnobs） */
/* The scroll bleeds out sideways by the clearance and the content steps back in by the same (ResearchCalcKnobs) */
.research-tree__detail-scroll {
    flex-shrink: 1;
    min-height: 0;
    margin-bottom: 8px;
    --research-tree-scroll-bleed: var(--research-detail-scroll-inline-bleed);
}

.research-tree__detail-scroll-content {
    flex-direction: column;
}

.research-tree__detail-description {
    font-size: 13px;
    white-space: normal;
    color: var(--text-default);
}

.research-tree__section {
    flex-direction: column;
    margin-top: 8px;
}

.research-tree__section-label {
    font-size: 12px;
    color: var(--text-muted);
    margin-bottom: 4px;
}

.research-tree__slots {
    flex-direction: row;
    flex-wrap: wrap;
    padding-bottom: var(--research-detail-slots-bleed);
}

.research-tree__slot {
    margin-right: 4px;
    margin-bottom: 4px;
}

.research-tree__unlock-other {
    font-size: 13px;
    white-space: normal;
    color: var(--text-default);
}

.research-tree__unlock-fluid {
    font-size: 13px;
    white-space: nowrap;
    align-self: center;
    color: var(--text-default);
    margin-right: 4px;
    margin-bottom: 4px;
}

.research-tree__actions {
    flex-direction: column;
    flex-shrink: 0;
}

.research-tree__reason {
    font-size: 12px;
    white-space: normal;
    color: var(--text-default);
    margin-top: 8px;
}
```
（`transparent` は前例 `THEME/Theme.uss` と同じキーワード。理由の文の改行は csv の `\n` をそのまま描く〈Web の `white-space: pre-line`〉）

（1 ファイル 200 行未満のため、エリアとカード〈`ResearchTree.uss`〉と詳細ペイン〈`ResearchDetailPane.uss`〉に分ける。`--recipe-action-width: auto` が `var()` 越しに効かない〈Task 9 の `ResearchTreeViewTest` の幅の主張が赤〉なら `100%` を試し、それでも赤なら `253px` を置いて `done.md` の「integrator への依頼」に「C4-c の `RecipeActionButton` の `--recipe-action-width` が auto/100% を受けられるように」を 1 行書く。D-21）

`GameUiTheme.tss` の `/* == section: Screens == */` の区画の末尾に 2 行:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/Research/ResearchTree.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Screens/Research/ResearchDetailPane.uss");
```

- [ ] **Step 2: calc() の受け直しを書く**

`ResearchCalcKnobs.cs`:
```csharp
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// Web の calc() を通る 2 つの値を customStyle から読んで inline で当てる（A-47）
    /// Reads the two values that go through Web calc() from customStyle and applies them inline (A-47)
    /// </summary>
    public static class ResearchCalcKnobs
    {
        private const string InventoryWidthProperty = "--research-tree-inventory-width";
        private const string ColumnGapProperty = "--research-tree-column-gap";
        private const string ScrollBleedProperty = "--research-tree-scroll-bleed";

        // 研究エリアの left = 持ち物の幅 + 列の間隔（Web .researchArea の calc）
        // Research area left = inventory width + column gap (the Web .researchArea calc)
        public static void ApplyAreaLeft(VisualElement area)
        {
            area.RegisterCallback<CustomStyleResolvedEvent>(_ =>
            {
                // 読めないときは UssCustomStyleReader が LogError を出す
                // When unreadable, UssCustomStyleReader logs an error
                if (!UssCustomStyleReader.TryReadPixels(area.customStyle, InventoryWidthProperty, out var width)) return;
                if (!UssCustomStyleReader.TryReadPixels(area.customStyle, ColumnGapProperty, out var gap)) return;
                area.style.left = width + gap;
            });
        }

        // スクロールを左右へ逃げの分はみ出させ、中身を同じだけ内へ戻す（Web .detailScroll の負の margin-inline）
        // Bleed the scroll out sideways by the clearance and step the content back in (the Web .detailScroll negative margin-inline)
        public static void ApplyScrollBleed(VisualElement scroll, VisualElement content)
        {
            scroll.RegisterCallback<CustomStyleResolvedEvent>(_ =>
            {
                if (!UssCustomStyleReader.TryReadPixels(scroll.customStyle, ScrollBleedProperty, out var bleed)) return;
                scroll.style.marginLeft = -bleed;
                scroll.style.marginRight = -bleed;
                content.style.paddingLeft = bleed;
                content.style.paddingRight = bleed;
            });
        }
    }
}
```

- [ ] **Step 3: カードとカードの集合を書く**

`ResearchNodeCard.cs`:
```csharp
using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 研究ノードのカード 1 枚（Web ResearchNodeCard）。押下はカードの根だけが受け、NodeGraphView のタップとパンに渡す
    /// One research node card (Web ResearchNodeCard); only the card root takes presses, handing them to NodeGraphView tap and pan
    /// </summary>
    public sealed class ResearchNodeCard : VisualElement
    {
        private readonly LocalizedLabel _name;
        private readonly ItemSlot _icon;
        private readonly LocalizedLabel _state;

        public ResearchNodeCard(Guid researchGuid)
        {
            var key = researchGuid.ToString();
            name = $"research-node-{key}";
            AddToClassList("research-tree__card");

            // 名前・アイコン・状態ラベル・選択の輪を縦に積む
            // Stack name, icon, state label and the selection ring
            _name = new LocalizedLabel();
            _name.AddToClassList("research-tree__card-name");
            _icon = new ItemSlot($"research-node-icon-{key}");
            _icon.AddToClassList("research-tree__card-icon");
            _state = new LocalizedLabel { name = $"research-node-state-{key}" };
            _state.AddToClassList("research-tree__card-state");
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("research-tree__card-ring");
            Add(_name);
            Add(_icon);
            Add(_state);
            Add(ring);

            // 文字は拾わない。アイコンは Web の ItemSlot と同じくホバーで名前を出すので拾い、押下は祖先のカードの包みへ浮かぶ（C4-d は対象の祖先から包みを探す）
            // Text does not pick; the icon picks for its hover name like the Web ItemSlot, and presses bubble to the card wrapper (C4-d finds it from the target's ancestors)
            UiPickingIgnore.ApplyToSubtree(_name);
            UiPickingIgnore.ApplyToSubtree(_state);
            TutorialAnchor.Mark(this, AnchorIds.ResearchNode(key));
        }

        public void Render(ResearchNodeCardViewModel card, GameContentPresentation content)
        {
            // 4 状態と選択は修飾クラス（Web の data 属性）
            // The four states and the selection are modifier classes (the Web data attributes)
            EnableInClassList("research-tree__card--completed", card.CardState.Completed);
            EnableInClassList("research-tree__card--ready", card.CardState.Ready);
            EnableInClassList("research-tree__card--locked", card.CardState.Locked);
            EnableInClassList("research-tree__card--selected", card.Selected);
            _name.SetText(UiText.FromContent(ContentLocalizationKeys.ResearchName(card.Guid)));
            _icon.SetContent(new ItemSlotContent(content.Item(card.IconItemId), null, new ItemSlotMarks(false, false, false), null, null, null));
            _state.SetText(new UiText(card.StateLabelKey));
        }
    }
}
```
（キーの無い `LocalizedLabel` をパネルに載せると LogError なので、カードは `Render` の後にグラフへ渡す。`ResearchNodeCardSet.RenderCards` がその順を持つ）

`ResearchNodeCardSet.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.Components.NodeGraph;
using Client.Ui.Content;
using Client.Ui.State.Research.Tree;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 研究ノードのカードを GUID で使い回す。並びが変わったときだけグラフの組み直しを求める
    /// Reuses research node cards by GUID and asks for a graph rebuild only when the order changes
    /// </summary>
    public sealed class ResearchNodeCardSet
    {
        private readonly GameContentPresentation _content;
        private readonly Dictionary<Guid, ResearchNodeCard> _cards = new();
        private readonly List<Guid> _order = new();

        public ResearchNodeCardSet(GameContentPresentation content)
        {
            _content = content;
        }

        // 全カードを描き直し、ノードの並びが前回と違えば true
        // Redraw every card and return true when the node order differs from last time
        public bool RenderCards(IReadOnlyList<ResearchNodeCardViewModel> nodes)
        {
            var orderChanged = nodes.Count != _order.Count;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!orderChanged && _order[i] != node.Guid) orderChanged = true;
                if (!_cards.TryGetValue(node.Guid, out var card))
                {
                    card = new ResearchNodeCard(node.Guid);
                    _cards.Add(node.Guid, card);
                }
                card.Render(node, _content);
            }
            if (!orderChanged) return false;
            _order.Clear();
            foreach (var node in nodes) _order.Add(node.Guid);
            return true;
        }

        public NodeGraphContent BuildContent(IReadOnlyList<ResearchNodeCardViewModel> nodes, NodeGraphFocus focus)
        {
            // キーは GUID の文字列（前提も同じ綴り）
            // Keys are GUID strings (prerequisites use the same spelling)
            var graphNodes = new List<NodeGraphNode>(nodes.Count);
            foreach (var node in nodes)
            {
                var previousKeys = new List<string>(node.PreviousGuids.Count);
                foreach (var previous in node.PreviousGuids) previousKeys.Add(previous.ToString());
                graphNodes.Add(new NodeGraphNode(node.Guid.ToString(), node.TreePosition, previousKeys, _cards[node.Guid]));
            }
            return new NodeGraphContent(graphNodes, focus);
        }
    }
}
```

- [ ] **Step 4: コンパイルし L2 を流す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Theme|Conventions|Layers|Anchor)\."`
Expected: ErrorCount 0 / PASS（`UiThemeImportBundleTest` が新しい USS の import を、`UiStyleConventionTest` が USS の規約を見る。落ちたら USS を直す。カードの描画は Task 9 の L1 が見る）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Research "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): 研究ツリーの画面 USS とノードカードを足す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 8: 詳細ペイン（`Screens/Research/Parts`）と L1

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/Parts/ResearchDetailPane.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/Parts/ResearchDetailActions.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/Parts/ResearchConsumeItemsSection.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/Parts/ResearchUnlockSections.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/Parts/ResearchUnlockSectionKind.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/Parts/ResearchUnlockEntryElements.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Research/ResearchScreenTestKit.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Research/RecordingResearchTreeCommands.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Research/ResearchDetailPaneTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Research/ResearchUnlockSectionsTest.cs`

（`Parts/` は Task 7 の 3 本と合わせて 9 本。10 以下）

**Interfaces:**
- Consumes: Task 3 の `ResearchDetailViewModel`・`ResearchConsumeSlotViewModel`、Task 5 の `IResearchTreeCommands`・`ResearchTreeCommands.CompleteResearchCommandName`、Task 7 の `ResearchCalcKnobs.ApplyScrollBleed` と USS のクラス、C4-c の `RecipeActionButton`、`GamePanel`・`GameScrollView`・`ItemSlot`・`BlockSlot`・`MaterialTooltipText`・`UiCommandResultToaster`
- Produces:
  - `public sealed class ResearchDetailPane : VisualElement { public ResearchDetailPane(IResearchTreeCommands commands, GameContentPresentation content, UiCommandResultToaster toaster); public void Render(ResearchDetailViewModel detail); public void Hide(); }`（name `research-detail-pane`。`Render` の後にパネルへ載せる。`Render` が表示に、`Hide` が非表示にし、`Hide` の後の最初の `Render` はスクロールを頭へ戻す）
  - `public sealed class ResearchDetailActions : VisualElement { public ResearchDetailActions(IResearchTreeCommands commands, UiCommandResultToaster toaster); public void Render(Guid researchGuid, ResearchButtonState button); }`
  - `public sealed class ResearchConsumeItemsSection : VisualElement { public ResearchConsumeItemsSection(GameContentPresentation content); public void Render(IReadOnlyList<ResearchConsumeSlotViewModel> slots); }`（name `research-consume-items`。枠の name `research-consume-slot-<i>`）
  - `public sealed class ResearchUnlockSections : VisualElement { public ResearchUnlockSections(GameContentPresentation content); public void Render(IReadOnlyList<UnlockEntry> entries); }`
  - `public enum ResearchUnlockSectionKind { Blocks, MachineRecipes, Items, RewardItems, Others }`・`public static class ResearchUnlockSectionKinds { public static ResearchUnlockSectionKind Of(UnlockEntry entry); public static LocalizationKey LabelKey(ResearchUnlockSectionKind kind); public static string Testid(ResearchUnlockSectionKind kind); public static bool WrapsInSlotsRow(ResearchUnlockSectionKind kind); }`
  - `public static class ResearchUnlockEntryElements { public static void AddTo(VisualElement container, UnlockEntry entry, GameContentPresentation content); }`
  - テストの補助: `internal static class ResearchScreenTestKit`（下の Step 1）・`internal sealed class RecordingResearchTreeCommands : CommandRecorder<string>, IResearchTreeCommands { public void SetCompleteResult(UiCommandResult result); }`

- [ ] **Step 1: テストの補助を書く**

`ResearchScreenTestKit.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Tests.Ui.Content;
using Client.Ui.Content;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Research.Tree;
using Core.Master;
using Game.Research;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究画面の L1 の共通の材料（テスト mod のマスタ・Content・5 状態の木・1 ノードの詳細）
    /// Shared material for research screen L1 tests (test mod master, Content, a five-state tree, single-node details)
    /// </summary>
    internal static class ResearchScreenTestKit
    {
        public static readonly Guid Researchable = Guid.Parse("00000000-0000-0000-0000-000000000201");
        public static readonly Guid Completed = Guid.Parse("00000000-0000-0000-0000-000000000202");
        public static readonly Guid ItemLacking = Guid.Parse("00000000-0000-0000-0000-000000000203");
        public static readonly Guid PreNodeLacking = Guid.Parse("00000000-0000-0000-0000-000000000204");
        public static readonly Guid AllLacking = Guid.Parse("00000000-0000-0000-0000-000000000205");
        public static readonly Guid[] AllNodes = { Researchable, Completed, ItemLacking, PreNodeLacking, AllLacking };
        public static readonly Guid WaterFluid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        public static readonly Guid BlockAndToolsResearch = Guid.Parse("bb000000-0000-4000-8000-000000000001");
        public static readonly Guid ModuleSlotResearch = Guid.Parse("c0de0000-0000-4000-8000-000000000002");
        public static readonly Guid CraftCapResearch = Guid.Parse("c0de0000-0000-4000-8000-000000000001");
        public static readonly Guid RewardResearch = Guid.Parse("bf9bda9e-dace-43c4-9a33-75f248fd17f6");

        public static GameContentPresentation LoadMasterAndContent(Texture2D texture)
        {
            // 名前と絵は ID から引くので、マスタはテスト mod、テクスチャは手元の 1 枚
            // Names and pictures resolve from ids, so the master is the test mod and the texture a local one
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return new GameContentPresentation(new FakeContentTextureSource(texture, texture, texture));
        }

        // 5 状態を 1 枚ずつ持つ木。研究可能が原点、他は上下左右
        // A tree with one card per state; researchable at the origin, the rest around it
        public static ResearchTreeViewModel Tree(ResearchSelection selection)
        {
            var definitions = new List<ResearchNodeDefinition>
            {
                Node(Researchable, Vector2.zero),
                Node(Completed, new Vector2(-220f, 0f)),
                Node(ItemLacking, new Vector2(220f, 0f)),
                Node(PreNodeLacking, new Vector2(0f, 160f), Researchable),
                Node(AllLacking, new Vector2(220f, 160f), ItemLacking),
            };
            var states = new Dictionary<Guid, ResearchNodeState>
            {
                { Researchable, ResearchNodeState.Researchable },
                { Completed, ResearchNodeState.Completed },
                { ItemLacking, ResearchNodeState.UnresearchableNotEnoughItem },
                { PreNodeLacking, ResearchNodeState.UnresearchableNotEnoughPreNode },
                { AllLacking, ResearchNodeState.UnresearchableAllReasons },
            };
            return ResearchTreeViewModelFactory.Create(definitions, states, new Dictionary<ItemId, int>(), selection);
        }

        // 1 ノードの詳細。消費は ItemId1 を 3 個（ItemId2 を上位レベルとして受け付ける）
        // Single-node details; consumes 3 of ItemId1 (accepting ItemId2 as the higher level)
        public static ResearchDetailViewModel Detail(ResearchNodeState state, int ownedItem1, int ownedItem2, ResearchUnlockSet unlocks)
        {
            var consume = new List<ResearchConsumeItem> { new(ForUnitTestItemId.ItemId1, 3, new List<ItemId> { ForUnitTestItemId.ItemId1, ForUnitTestItemId.ItemId2 }) };
            var definition = new ResearchNodeDefinition(Researchable, ForUnitTestItemId.ItemId1, Vector2.zero, new List<Guid>(), consume, unlocks);
            var owned = new Dictionary<ItemId, int> { { ForUnitTestItemId.ItemId1, ownedItem1 }, { ForUnitTestItemId.ItemId2, ownedItem2 } };
            var states = new Dictionary<Guid, ResearchNodeState> { { Researchable, state } };
            var tree = ResearchTreeViewModelFactory.Create(new List<ResearchNodeDefinition> { definition }, states, owned, ResearchSelection.Of(Researchable));
            if (!tree.TryGetDetail(out var detail)) throw new InvalidOperationException("選択したノードの詳細が組まれていない");
            return detail;
        }

        public static ResearchUnlockSet NoUnlocks()
        {
            return Unlocks(new List<UnlockBlock>(), new List<UnlockMachineRecipe>(), new List<ItemId>(), new List<RewardItem>(), new List<Guid>(), new List<Guid>(), new List<int>(), new List<LevelInheritanceCraftCap>());
        }

        public static ResearchUnlockSet Unlocks(IReadOnlyList<UnlockBlock> blocks, IReadOnlyList<UnlockMachineRecipe> machineRecipes, IReadOnlyList<ItemId> itemRecipeViews, IReadOnlyList<RewardItem> rewards, IReadOnlyList<Guid> connectTools, IReadOnlyList<Guid> trainCars, IReadOnlyList<int> moduleSlotLimits, IReadOnlyList<LevelInheritanceCraftCap> craftCaps)
        {
            return new ResearchUnlockSet(blocks, machineRecipes, itemRecipeViews, rewards, connectTools, trainCars, moduleSlotLimits, craftCaps);
        }

        // テスト mod の研究の解放物をそのまま引く（ブロック・接続ツール等の GUID を捏造しない）
        // Take a test mod research's unlocks as they are (no fabricated block or tool GUIDs)
        public static ResearchUnlockSet MasterUnlocks(Guid researchGuid)
        {
            return ResearchNodeDefinitionsFromMaster.Build().Single(node => node.Guid == researchGuid).Unlocks;
        }

        private static ResearchNodeDefinition Node(Guid guid, Vector2 position, params Guid[] previous)
        {
            return new ResearchNodeDefinition(guid, ForUnitTestItemId.ItemId1, position, previous, new List<ResearchConsumeItem>(), NoUnlocks());
        }
    }
}
```

`RecordingResearchTreeCommands.cs`:
```csharp
using System;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Commands;
using Client.Ui.State.Research.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究ツリーの操作を「toggle:<guid>」「close」「complete:<guid>」で記録する偽。研究の実行は設定した結果を同期で返す
    /// Fake that records research tree operations as "toggle:<guid>", "close" and "complete:<guid>"; running research returns the configured result synchronously
    /// </summary>
    internal sealed class RecordingResearchTreeCommands : CommandRecorder<string>, IResearchTreeCommands
    {
        private UiCommandResult _completeResult = UiCommandAccepted.Instance;

        public void SetCompleteResult(UiCommandResult result)
        {
            _completeResult = result;
        }

        public void ToggleNodeSelection(Guid researchGuid)
        {
            Record($"toggle:{researchGuid}");
        }

        public void CloseDetail()
        {
            Record("close");
        }

        public UniTask<UiCommandResult> CompleteResearch(Guid researchGuid)
        {
            Record($"complete:{researchGuid}");
            return UniTask.FromResult(_completeResult);
        }
    }
}
```

- [ ] **Step 2: 詳細ペインの L1 を書く**

`ResearchDetailPaneTest.cs`:
```csharp
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Screens.Research.Parts;
using Client.Ui.State.Commands;
using Client.Ui.State.Research.Tree;
using Game.Research;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 詳細ペインの研究ボタン・理由・閉じる・消費アイテムの不足（L1）
    /// Detail pane research button, reason, close and consume shortage (L1)
    /// </summary>
    public class ResearchDetailPaneTest : ScreenTestHost
    {
        private Texture2D _texture;
        private GameContentPresentation _content;
        private RecordingResearchTreeCommands _commands;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _content = Kit.LoadMasterAndContent(_texture);
            _commands = new RecordingResearchTreeCommands();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_texture);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">研究可能ノードでボタン活性・クリックでresearch.completeを送る</web-test>
        /// <web-test file="e2e/tests/research/research.spec.ts">research button sends research.complete and node becomes completed</web-test>
        [Test]
        public void 研究可能なら押せて押すと研究を実行し完了の表示に替わる()
        {
            var pane = MountPane(Kit.Detail(ResearchNodeState.Researchable, 3, 0, Kit.NoUnlocks()));
            var button = ResearchButton();
            Assert.IsTrue(button.enabledSelf);
            Assert.AreEqual("Research", LabelOf(button));

            // 押すと研究の実行を 1 回呼び、受理ならトーストは出ない
            // Pressing calls research once; acceptance shows no toast
            Click(button);
            CollectionAssert.AreEqual(new[] { $"complete:{Kit.Researchable}" }, _commands.Issued);
            Assert.IsEmpty(ShownToasts);

            // サーバーの状態が完了へ替わると押せず「完了済み」になり理由は出ない
            // Once the server state turns completed it is disabled, reads Completed and shows no reason
            pane.Render(Kit.Detail(ResearchNodeState.Completed, 3, 0, Kit.NoUnlocks()));
            simulate.FrameUpdate();
            Assert.IsFalse(button.enabledSelf);
            Assert.AreEqual("Completed", LabelOf(button));
            Assert.IsFalse(IsShown(Find<Label>(rootVisualElement, "research-detail-reason")));
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">サーバーstateがアイテム不足ならボタン非活性で理由を表示し、閉じるでonCloseが呼ばれる</web-test>
        [Test]
        public void アイテム不足なら押せず理由を出し閉じるで閉じる操作を呼ぶ()
        {
            MountPane(Kit.Detail(ResearchNodeState.UnresearchableNotEnoughItem, 1, 0, Kit.NoUnlocks()));

            Assert.IsFalse(ResearchButton().enabledSelf);
            var reason = Find<Label>(rootVisualElement, "research-detail-reason");
            Assert.IsTrue(IsShown(reason));
            Assert.AreEqual("Not enough research items.", reason.text);
            Click(Find<VisualElement>(rootVisualElement, "research-detail-close"));
            CollectionAssert.AreEqual(new[] { "close" }, _commands.Issued);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">所持数不足の消費アイテムはinsufficient=trueで所持/必要をスロットへ渡す</web-test>
        [Test]
        public void 所持が足りない消費アイテムは不足の枠と所持と必要を出す()
        {
            MountPane(Kit.Detail(ResearchNodeState.UnresearchableNotEnoughItem, 1, 0, Kit.NoUnlocks()));
            AssertConsumeSlot(true, true);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">所持数が足りていればinsufficient=falseで所持/必要をスロットへ渡す</web-test>
        [Test]
        public void 所持が足りていれば不足の枠にせず所持と必要は出す()
        {
            MountPane(Kit.Detail(ResearchNodeState.Researchable, 3, 0, Kit.NoUnlocks()));
            AssertConsumeSlot(false, true);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">完了ノードは所持不足でも消費アイテムを不足強調しない</web-test>
        [Test]
        public void 完了ノードは所持が足りなくても不足の枠にしない()
        {
            MountPane(Kit.Detail(ResearchNodeState.Completed, 0, 0, Kit.NoUnlocks()));
            AssertConsumeSlot(false, true);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">上位レベルの所持も消費アイテムの充足に数える</web-test>
        [Test]
        public void 上位レベルの所持も充足に数える()
        {
            MountPane(Kit.Detail(ResearchNodeState.Researchable, 0, 3, Kit.NoUnlocks()));
            AssertConsumeSlot(false, true);
        }

        [Test]
        public void 閉じて同じノードを開き直すと説明のスクロールは頭へ戻る()
        {
            // Web は閉じるとペインを捨てるので、開き直しは常に頭から。表示中の同じノードの描き直しでは保つ
            // The Web discards the pane on close, so reopening starts at the top; re-rendering the same shown node keeps it
            var detail = Kit.Detail(ResearchNodeState.Researchable, 3, 0, Kit.NoUnlocks());
            var pane = MountPane(detail);
            var scroll = Find<ScrollView>(rootVisualElement, "research-detail-scroll");
            scroll.Add(new VisualElement { style = { height = 2000f } });
            simulate.FrameUpdate();
            scroll.scrollOffset = new Vector2(0f, 100f);
            Assert.Greater(scroll.scrollOffset.y, 0f, "scrolled");

            pane.Render(detail);
            Assert.Greater(scroll.scrollOffset.y, 0f, "same node keeps the scroll while shown");

            pane.Hide();
            pane.Render(detail);
            simulate.FrameUpdate();
            Assert.AreEqual(0f, scroll.scrollOffset.y, 0.5f);
        }

        [Test]
        public void 研究の失敗は良性でないトーストに出る()
        {
            _commands.SetCompleteResult(new UiCommandRejected(UiCommandRejectReason.ResearchFailed, ""));
            MountPane(Kit.Detail(ResearchNodeState.Researchable, 3, 0, Kit.NoUnlocks()));

            Click(ResearchButton());
            Assert.AreEqual(1, ShownToasts.Count);
        }

        #region Helpers

        private ResearchDetailPane MountPane(ResearchDetailViewModel detail)
        {
            // キーの無いラベルを載せないよう、描いてから載せる
            // Render before mounting so no keyless label is attached
            var pane = new ResearchDetailPane(_commands, _content, Toaster);
            pane.Render(detail);
            Mount(pane);
            simulate.FrameUpdate();
            return pane;
        }

        private RecipeActionButton ResearchButton()
        {
            return Find<RecipeActionButton>(rootVisualElement, $"research-button-{Kit.Researchable}");
        }

        private static string LabelOf(RecipeActionButton button)
        {
            return button.Q<Label>(className: RecipeActionButton.LabelUssClassName).text;
        }

        private void Click(VisualElement target)
        {
            var center = target.worldBound.center;
            simulate.MouseDown(center, MouseButton.LeftMouse);
            simulate.MouseUp(center, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }

        private void AssertConsumeSlot(bool insufficient, bool showsShortage)
        {
            var frame = Find<VisualElement>(rootVisualElement, "research-consume-slot-0");
            var slot = frame.parent;
            Assert.AreEqual(insufficient, frame.ClassListContains("slot-frame--insufficient"));
            Assert.AreEqual(insufficient, slot.ClassListContains("item-slot--lack"));
            Assert.AreEqual(showsShortage, slot.ClassListContains("item-slot--shortage"));
        }

        #endregion
    }
}
```
（`ItemSlot` の枠の name が frameTestid で、`ItemSlot` 自身が枠の親であることは実物 `ItemSlot.cs` の ctor のとおり。`Click` は `RecipeActionButton` の `clickable` と、閉じるの `ClickEvent` の両方を起こす）

- [ ] **Step 3: 解放物の節の L1 を書く**

`ResearchUnlockSectionsTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Screens.Research.Parts;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Text;
using Core.Master;
using Game.Research;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 詳細ペインの解放物の節（種類ごとの見出し・枠・文の行）（L1）
    /// Detail pane unlock sections (per-kind headings, slots and text rows) (L1)
    /// </summary>
    public class ResearchUnlockSectionsTest : ScreenTestHost
    {
        private Texture2D _texture;
        private GameContentPresentation _content;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _content = Kit.LoadMasterAndContent(_texture);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">研究報酬itemの個数をtopic payloadどおり詳細ペインで表示する</web-test>
        [Test]
        public void 報酬アイテムはマスタの個数つきで報酬の節に並ぶ()
        {
            var unlocks = Kit.MasterUnlocks(Kit.RewardResearch);
            MountPane(unlocks);

            var section = Find<VisualElement>(rootVisualElement, "research-reward-items");
            Assert.AreEqual("Reward items", SectionLabel(section));
            var count = unlocks.RewardItems[0].Count.ToString(CultureInfo.InvariantCulture);
            Assert.IsTrue(section.Query<Label>().ToList().Exists(label => label.text == count), $"count {count}");
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">詳細ペインに解放物が種類別ラベル付きで並ぶ</web-test>
        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">解放ブロックがあればBlockSlotで描画される</web-test>
        [Test]
        public void 解放物は種類ごとの見出しの節に初出順で並びブロックはブロックの枠で描く()
        {
            MountPane(Kit.MasterUnlocks(Kit.BlockAndToolsResearch));

            var blocks = Find<VisualElement>(rootVisualElement, "research-unlock-blocks");
            var recipes = Find<VisualElement>(rootVisualElement, "research-unlock-machine-recipes");
            var others = Find<VisualElement>(rootVisualElement, "research-unlock-others");
            Assert.AreEqual("Unlocks: Blocks", SectionLabel(blocks));
            Assert.AreEqual("Unlocks: Machine recipes", SectionLabel(recipes));
            Assert.AreEqual("Unlocks: Others", SectionLabel(others));
            Assert.Less(blocks.parent.IndexOf(blocks), recipes.parent.IndexOf(recipes));
            Assert.IsNotNull(blocks.Q<BlockSlot>());
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">機械レシピはアイテム出力と液体出力を両方描く（混在レシピの液体を落とさない）</web-test>
        [Test]
        public void 機械レシピはアイテムの枠と液体の量の行を両方描く()
        {
            var water = MasterHolder.FluidMaster.GetFluidId(Kit.WaterFluid);
            var recipe = new UnlockMachineRecipe(Guid.NewGuid(), new List<ItemId> { ForUnitTestItemId.ItemId1 }, new List<UnlockFluid> { new(water, 250d) });
            MountPane(Kit.Unlocks(new List<UnlockBlock>(), new List<UnlockMachineRecipe> { recipe }, new List<ItemId>(), new List<RewardItem>(), new List<Guid>(), new List<Guid>(), new List<int>(), new List<LevelInheritanceCraftCap>()));

            var section = Find<VisualElement>(rootVisualElement, "research-unlock-machine-recipes");
            Assert.AreEqual(1, section.Query<ItemSlot>().ToList().Count);
            var fluid = Find<Label>(section, "research-unlock-fluid");
            StringAssert.Contains("Water", fluid.text);
            StringAssert.Contains(SlotAmountFormat.Format(250d), fluid.text);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">itemRecipeView解放とconnectTool/trainCarのテキスト行が並ぶ</web-test>
        [Test]
        public void レシピ表示の解放は枠で接続ツールと車両はその他の文の行で並ぶ()
        {
            var master = Kit.MasterUnlocks(Kit.BlockAndToolsResearch);
            MountPane(Kit.Unlocks(new List<UnlockBlock>(), new List<UnlockMachineRecipe>(), new List<ItemId> { ForUnitTestItemId.ItemId1 }, new List<RewardItem>(), master.ConnectToolGuids, master.TrainCarGuids, new List<int>(), new List<LevelInheritanceCraftCap>()));

            Assert.IsNotNull(Find<VisualElement>(rootVisualElement, "research-unlock-items").Q<ItemSlot>());
            var lines = Find<VisualElement>(rootVisualElement, "research-unlock-others").Query<Label>(className: "research-tree__unlock-other").ToList();
            Assert.AreEqual(master.ConnectToolGuids.Count + master.TrainCarGuids.Count, lines.Count);
            Assert.AreEqual(UiText.FromContent(ContentLocalizationKeys.ConnectToolName(master.ConnectToolGuids[0])).Resolve(), lines[0].text);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">スロット上限と製作上限の解放を「その他」の行に出す</web-test>
        /// <web-test file="e2e/tests/research/researchMachineUpgradeUnlock.spec.ts">研究ノードの詳細にスロット上限と製作上限の解放行が出る</web-test>
        [Test]
        public void モジュール枠の上限と製作上限はその他の文の行に出る()
        {
            var limit = Kit.MasterUnlocks(Kit.ModuleSlotResearch).ModuleSlotStackLimits[0];
            var cap = Kit.MasterUnlocks(Kit.CraftCapResearch).LevelInheritanceCraftCaps[0];
            MountPane(Kit.Unlocks(new List<UnlockBlock>(), new List<UnlockMachineRecipe>(), new List<ItemId>(), new List<RewardItem>(), new List<Guid>(), new List<Guid>(), new List<int> { limit }, new List<LevelInheritanceCraftCap> { cap }));

            Assert.AreEqual($"Module slot limit: {limit} per slot", Find<Label>(rootVisualElement, "research-unlock-module-slot-stack-limit").text);
            var capLine = Find<Label>(rootVisualElement, $"research-unlock-craft-cap-{cap.ItemId.AsPrimitive()}");
            StringAssert.EndsWith($"up to Lv{cap.Level}", capLine.text);
        }

        #region Helpers

        private void MountPane(ResearchUnlockSet unlocks)
        {
            var pane = new ResearchDetailPane(new RecordingResearchTreeCommands(), _content, Toaster);
            pane.Render(Kit.Detail(ResearchNodeState.Researchable, 3, 0, unlocks));
            Mount(pane);
            simulate.FrameUpdate();
        }

        private static string SectionLabel(VisualElement section)
        {
            return section.Q<Label>(className: "research-tree__section-label").text;
        }

        #endregion
    }
}
```

- [ ] **Step 4: 部品を書く**

`ResearchUnlockSectionKind.cs`:
```csharp
using System;
using Client.Ui.State.Research.Logic;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.Research.Parts
{
    public enum ResearchUnlockSectionKind
    {
        Blocks,
        MachineRecipes,
        Items,
        RewardItems,
        Others,
    }

    /// <summary>
    /// 解放物の種類 → 節（見出し・testid・枠の列で包むか）の唯一の表（Web UnlockSections の SECTIONS）
    /// The single table from unlock kind to section (heading, testid, whether it wraps in a slots row); the Web UnlockSections SECTIONS
    /// </summary>
    public static class ResearchUnlockSectionKinds
    {
        public static ResearchUnlockSectionKind Of(UnlockEntry entry)
        {
            return entry switch
            {
                BlockUnlockEntry => ResearchUnlockSectionKind.Blocks,
                MachineRecipeOutputUnlockEntry => ResearchUnlockSectionKind.MachineRecipes,
                ItemRecipeViewUnlockEntry => ResearchUnlockSectionKind.Items,
                RewardItemUnlockEntry => ResearchUnlockSectionKind.RewardItems,
                ConnectToolUnlockEntry or TrainCarUnlockEntry or ModuleSlotStackLimitUnlockEntry or LevelInheritanceCraftCapUnlockEntry => ResearchUnlockSectionKind.Others,
                _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.GetType().Name, null),
            };
        }

        public static LocalizationKey LabelKey(ResearchUnlockSectionKind kind)
        {
            return kind switch
            {
                ResearchUnlockSectionKind.Blocks => LocalizationKeys.Ui.Research.UnlockBlocksLabel,
                ResearchUnlockSectionKind.MachineRecipes => LocalizationKeys.Ui.Research.UnlockMachineRecipesLabel,
                ResearchUnlockSectionKind.Items => LocalizationKeys.Ui.Research.UnlockItemsLabel,
                ResearchUnlockSectionKind.RewardItems => LocalizationKeys.Ui.Research.RewardItemsLabel,
                ResearchUnlockSectionKind.Others => LocalizationKeys.Ui.Research.UnlockOthersLabel,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        public static string Testid(ResearchUnlockSectionKind kind)
        {
            return kind switch
            {
                ResearchUnlockSectionKind.Blocks => "research-unlock-blocks",
                ResearchUnlockSectionKind.MachineRecipes => "research-unlock-machine-recipes",
                ResearchUnlockSectionKind.Items => "research-unlock-items",
                ResearchUnlockSectionKind.RewardItems => "research-reward-items",
                ResearchUnlockSectionKind.Others => "research-unlock-others",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        // その他の節だけは枠の列で包まず文の行を縦に並べる
        // Only the others section skips the slots row and stacks text rows
        public static bool WrapsInSlotsRow(ResearchUnlockSectionKind kind)
        {
            return kind != ResearchUnlockSectionKind.Others;
        }
    }
}
```

`ResearchUnlockEntryElements.cs`:
```csharp
using System;
using System.Globalization;
using Client.Localization;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 解放物 1 件を枠か文の行にして節へ足す（Web renderUnlockEntry）
    /// Turns one unlock entry into slots or text rows in its section (Web renderUnlockEntry)
    /// </summary>
    public static class ResearchUnlockEntryElements
    {
        public static void AddTo(VisualElement container, UnlockEntry entry, GameContentPresentation content)
        {
            switch (entry)
            {
                case BlockUnlockEntry block:
                    var blockSlot = new BlockSlot($"research-unlock-block-{entry.Index}");
                    blockSlot.AddToClassList("research-tree__slot");
                    blockSlot.SetBlock(content.Block(block.Block.BlockId), UiText.FromContent(ContentLocalizationKeys.BlockName(block.Block.BlockGuid)));
                    container.Add(blockSlot);
                    break;
                case MachineRecipeOutputUnlockEntry recipe:
                    // アイテムと液体は排他でないので両方を並べる
                    // Items and fluids are not exclusive, so show both
                    for (var i = 0; i < recipe.Recipe.OutputItemIds.Count; i++) container.Add(ItemSlotOf($"research-unlock-recipe-{entry.Index}-{i}", recipe.Recipe.OutputItemIds[i], null, content));
                    foreach (var fluid in recipe.Recipe.OutputFluids) container.Add(FluidLabelOf(fluid, content));
                    break;
                case ItemRecipeViewUnlockEntry item:
                    container.Add(ItemSlotOf($"research-unlock-item-{entry.Index}", item.ItemId, null, content));
                    break;
                case RewardItemUnlockEntry reward:
                    container.Add(ItemSlotOf($"research-reward-item-{entry.Index}", reward.Reward.ItemId, reward.Reward.Count, content));
                    break;
                case ConnectToolUnlockEntry tool:
                    container.Add(OtherLine(null, UiText.FromContent(ContentLocalizationKeys.ConnectToolName(tool.Guid))));
                    break;
                case TrainCarUnlockEntry car:
                    container.Add(OtherLine(null, UiText.FromContent(ContentLocalizationKeys.TrainCarName(car.Guid))));
                    break;
                case ModuleSlotStackLimitUnlockEntry limit:
                    container.Add(OtherLine("research-unlock-module-slot-stack-limit", new UiText(LocalizationKeys.Ui.Research.UnlockModuleSlotStackLimit, new[] { new LocalizationTextParam("limit", limit.Limit.ToString(CultureInfo.InvariantCulture)) })));
                    break;
                case LevelInheritanceCraftCapUnlockEntry cap:
                    var capParams = new[] { new LocalizationTextParam("itemName", content.ItemName(cap.Cap.ItemId).Resolve()), new LocalizationTextParam("level", cap.Cap.Level.ToString(CultureInfo.InvariantCulture)) };
                    container.Add(OtherLine($"research-unlock-craft-cap-{cap.Cap.ItemId.AsPrimitive()}", new UiText(LocalizationKeys.Ui.Research.UnlockLevelInheritanceCraftCap, capParams)));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry), entry.GetType().Name, null);
            }
        }

        private static ItemSlot ItemSlotOf(string frameTestid, ItemId itemId, int? count, GameContentPresentation content)
        {
            var slot = new ItemSlot(frameTestid);
            slot.AddToClassList("research-tree__slot");
            slot.SetContent(new ItemSlotContent(content.Item(itemId), count, new ItemSlotMarks(false, false, false), null, null, null));
            return slot;
        }

        // 液体の出力は容量を持たないので量の文だけで示す（Web UnlockFluidLabel）
        // Fluid outputs have no capacity, so only an amount text is shown (Web UnlockFluidLabel)
        private static LocalizedLabel FluidLabelOf(UnlockFluid fluid, GameContentPresentation content)
        {
            var label = new LocalizedLabel { name = "research-unlock-fluid" };
            label.AddToClassList("research-tree__unlock-fluid");
            var textParams = new[] { new LocalizationTextParam("fluidName", content.Fluid(fluid.FluidId).Label.Resolve()), new LocalizationTextParam("amount", SlotAmountFormat.Format(fluid.Amount)) };
            label.SetText(new UiText(LocalizationKeys.Ui.Research.UnlockFluidSummary, textParams));
            return label;
        }

        private static LocalizedLabel OtherLine(string testid, UiText text)
        {
            var label = new LocalizedLabel();
            if (testid != null) label.name = testid;
            label.AddToClassList("research-tree__unlock-other");
            label.SetText(text);
            return label;
        }
    }
}
```
（`OtherLine` の `testid` の null は「Web も testid を付けない行」の意味で、名前を付けないだけ。`ItemSlotContent` の null は部品の API の「無い」の書き方〈前例 `GrabOverlayView`〉）

`ResearchUnlockSections.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 解放物を種類ごとの節にまとめ、初出順に並べる（Web UnlockSections）
    /// Groups unlocks into per-kind sections in first-appearance order (Web UnlockSections)
    /// </summary>
    public sealed class ResearchUnlockSections : VisualElement
    {
        private readonly GameContentPresentation _content;

        public ResearchUnlockSections(GameContentPresentation content)
        {
            _content = content;
            pickingMode = PickingMode.Ignore;
        }

        public void Render(IReadOnlyList<UnlockEntry> entries)
        {
            // 解放物はノードごとに数件なので描くたびに組み直す
            // Unlocks are a handful per node, so rebuild on every render
            Clear();
            var containers = new Dictionary<ResearchUnlockSectionKind, VisualElement>();
            foreach (var entry in entries) ResearchUnlockEntryElements.AddTo(ContainerOf(ResearchUnlockSectionKinds.Of(entry)), entry, _content);

            #region Internal

            VisualElement ContainerOf(ResearchUnlockSectionKind kind)
            {
                if (containers.TryGetValue(kind, out var existing)) return existing;
                var section = new VisualElement { name = ResearchUnlockSectionKinds.Testid(kind) };
                section.AddToClassList("research-tree__section");
                var label = new LocalizedLabel();
                label.AddToClassList("research-tree__section-label");
                label.SetText(new UiText(ResearchUnlockSectionKinds.LabelKey(kind)));
                section.Add(label);
                Add(section);
                var container = section;
                if (ResearchUnlockSectionKinds.WrapsInSlotsRow(kind))
                {
                    container = new VisualElement();
                    container.AddToClassList("research-tree__slots");
                    section.Add(container);
                }
                containers.Add(kind, container);
                return container;
            }

            #endregion
        }
    }
}
```

`ResearchConsumeItemsSection.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.MaterialTooltip;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 消費アイテムの節。所持/必要と不足を枠へ渡す（Web ResearchDetailPane の consume）
    /// Consume items section; passes owned/required and shortage to the slots (Web ResearchDetailPane consume)
    /// </summary>
    public sealed class ResearchConsumeItemsSection : VisualElement
    {
        private readonly GameContentPresentation _content;
        private readonly VisualElement _slotsRow;
        private readonly List<ItemSlot> _slots = new();

        public ResearchConsumeItemsSection(GameContentPresentation content)
        {
            _content = content;
            name = "research-consume-items";
            AddToClassList("research-tree__section");
            var label = new LocalizedLabel();
            label.AddToClassList("research-tree__section-label");
            label.SetText(new UiText(LocalizationKeys.Ui.Research.ConsumeItemsLabel));
            _slotsRow = new VisualElement();
            _slotsRow.AddToClassList("research-tree__slots");
            Add(label);
            Add(_slotsRow);
        }

        public void Render(IReadOnlyList<ResearchConsumeSlotViewModel> slots)
        {
            // 消費の無いノードは節ごと出さない
            // Nodes without consumption hide the whole section
            style.display = slots.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            while (_slots.Count < slots.Count)
            {
                var slot = new ItemSlot($"research-consume-slot-{_slots.Count}");
                slot.AddToClassList("research-tree__slot");
                _slotsRow.Add(slot);
                _slots.Add(slot);
            }
            for (var i = 0; i < _slots.Count; i++)
            {
                var shown = i < slots.Count;
                _slots[i].style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (shown) _slots[i].SetContent(ContentOf(slots[i]));
            }
        }

        // 不足は枠の強調と所持/必要の赤で示し、ツールチップは名前と所持・必要
        // Shortage shows as the frame highlight and red owned/required; the tooltip lists name, owned and required
        private ItemSlotContent ContentOf(ResearchConsumeSlotViewModel slot)
        {
            var tooltip = MaterialTooltipText.Create(MaterialTooltipKind.ResearchConsumeItem, _content.ItemName(slot.ItemId).Resolve(), slot.Required, slot.Allocated);
            var shortage = new ItemSlotShortage(slot.Allocated, slot.Required, slot.Lacking, tooltip);
            return new ItemSlotContent(_content.Item(slot.ItemId), null, new ItemSlotMarks(false, false, slot.Lacking), shortage, null, null);
        }
    }
}
```

`ResearchDetailActions.cs`:
```csharp
using System;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 研究ボタンと不可の理由。押すと研究を実行し、結果をトースト口へ渡す
    /// The research button and the unavailable reason; pressing runs research and hands the result to the toast port
    /// </summary>
    public sealed class ResearchDetailActions : VisualElement
    {
        private readonly IResearchTreeCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly RecipeActionButton _button;
        private readonly LocalizedLabel _reason;
        private Guid _researchGuid;

        public ResearchDetailActions(IResearchTreeCommands commands, UiCommandResultToaster toaster)
        {
            _commands = commands;
            _toaster = toaster;
            AddToClassList("research-tree__actions");
            pickingMode = PickingMode.Ignore;
            _button = new RecipeActionButton("research-button", new UiText(LocalizationKeys.Ui.Research.Action), RecipeActionLabelWeight.Regular);
            _button.OnClicked.Subscribe(_ => CompleteAsync().Forget());
            _reason = new LocalizedLabel { name = "research-detail-reason" };
            _reason.AddToClassList("research-tree__reason");
            _reason.SetText(new UiText(LocalizationKeys.Ui.Research.MissingItems));
            Add(_button);
            Add(_reason);
        }

        public void Render(Guid researchGuid, ResearchButtonState button)
        {
            // ボタンの testid はノードごと（Web の research-button-<guid>）
            // The button testid is per node (the Web research-button-<guid>)
            _researchGuid = researchGuid;
            _button.name = $"research-button-{researchGuid}";
            _button.SetLabel(new UiText(button.Completed ? LocalizationKeys.Ui.Research.Completed : LocalizationKeys.Ui.Research.Action));
            _button.SetEnabled(button.Interactable);

            // 理由は完了でも研究可能でもないときだけ出す
            // The reason shows only when neither completed nor researchable
            var showsReason = !button.Completed && !button.Interactable;
            _reason.style.display = showsReason ? DisplayStyle.Flex : DisplayStyle.None;
            _reason.SetText(new UiText(button.TooltipKey));
        }

        private async UniTaskVoid CompleteAsync()
        {
            var result = await _commands.CompleteResearch(_researchGuid);
            _toaster.Report(ResearchTreeCommands.CompleteResearchCommandName, result);
        }
    }
}
```

`ResearchDetailPane.cs`:
```csharp
using System;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 選択ノードの詳細ペイン（Web ResearchDetailPane）。パン・ズームに追従しない
    /// Detail pane of the selected node (Web ResearchDetailPane); unaffected by pan and zoom
    /// </summary>
    public sealed class ResearchDetailPane : VisualElement
    {
        private readonly LocalizedLabel _name;
        private readonly LocalizedLabel _description;
        private readonly GameScrollView _scroll;
        private readonly ResearchConsumeItemsSection _consume;
        private readonly ResearchUnlockSections _unlocks;
        private readonly ResearchDetailActions _actions;
        private bool _hasRendered;
        private Guid _renderedGuid;

        public ResearchDetailPane(IResearchTreeCommands commands, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            name = "research-detail-pane";
            AddToClassList("research-tree__detail");
            pickingMode = PickingMode.Ignore;
            var panel = new GamePanel("research-detail-panel", GamePanelFrame.Craft);
            panel.AddToClassList("research-tree__detail-panel");
            var body = new VisualElement();
            body.AddToClassList("research-tree__detail-body");

            // 見出し: 名前と閉じる
            // Header: name and close
            var header = new VisualElement();
            header.AddToClassList("research-tree__detail-header");
            _name = new LocalizedLabel();
            _name.AddToClassList("research-tree__detail-name");
            header.Add(_name);
            header.Add(CloseButton(commands));

            // 見出しとボタンの間だけスクロールさせ、研究ボタンを常に見せる
            // Scroll only between header and button so the research button stays visible
            _scroll = new GameScrollView("research-detail-scroll");
            _scroll.AddToClassList("research-tree__detail-scroll");
            var scrollContent = new VisualElement();
            scrollContent.AddToClassList("research-tree__detail-scroll-content");
            _description = new LocalizedLabel();
            _description.AddToClassList("research-tree__detail-description");
            _consume = new ResearchConsumeItemsSection(content);
            _unlocks = new ResearchUnlockSections(content);
            scrollContent.Add(_description);
            scrollContent.Add(_consume);
            scrollContent.Add(_unlocks);
            _scroll.Add(scrollContent);
            ResearchCalcKnobs.ApplyScrollBleed(_scroll, scrollContent);

            _actions = new ResearchDetailActions(commands, toaster);
            body.Add(header);
            body.Add(_scroll);
            body.Add(_actions);
            panel.Add(body);
            Add(panel);
        }

        public void Render(ResearchDetailViewModel detail)
        {
            // 隠した後の最初か、ノードが替わったらスクロールを頭へ戻す（Web は閉じるとペインを捨て、ScrollArea は key={guid}）
            // Reset the scroll on the first render after hiding or when the node changes (Web discards the pane on close; ScrollArea key={guid})
            if (!_hasRendered || _renderedGuid != detail.Guid) _scroll.SetScrollOffset(0f);
            _hasRendered = true;
            _renderedGuid = detail.Guid;
            style.display = DisplayStyle.Flex;
            _name.SetText(UiText.FromContent(ContentLocalizationKeys.ResearchName(detail.Guid)));
            _description.SetText(UiText.FromContent(ContentLocalizationKeys.ResearchDescription(detail.Guid)));
            _consume.Render(detail.ConsumeSlots);
            _unlocks.Render(detail.Unlocks);
            _actions.Render(detail.Guid, detail.Button);
        }

        public void Hide()
        {
            // 次に出すときは頭から（同じノードでも）
            // The next show starts from the top, even for the same node
            style.display = DisplayStyle.None;
            _hasRendered = false;
        }

        // 閉じるは素の要素とクリックで作る（Unity の Button の既定の見た目を持ち込まない）
        // Close is a plain element with a click (keeps Unity Button default styling out)
        private static VisualElement CloseButton(IResearchTreeCommands commands)
        {
            var close = new VisualElement { name = "research-detail-close" };
            close.AddToClassList("research-tree__detail-close");
            var symbol = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            symbol.AddToClassList("research-tree__detail-close-symbol");
            symbol.SetText(new UiText(LocalizationKeys.Ui.Research.CloseSymbol));
            close.Add(symbol);
            close.RegisterCallback<ClickEvent>(_ => commands.CloseDetail());
            return close;
        }
    }
}
```

- [ ] **Step 5: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Research\.(ResearchDetailPaneTest|ResearchUnlockSectionsTest)"`
Expected: ErrorCount 0 / 13 PASS。`uloop get-logs --project-path ./moorestech_client --log-type Error` に `LocalizedLabel` のキー無しの LogError が無い（あれば、そのラベルの `SetText` を載せる前へ移す）。詳細ペインの高さが 0 なら（`GamePanel` の本文の `flex-basis: 0%` が高さ auto の親で潰れる）、`.research-tree__detail-panel` に `flex-basis: auto` を足して直し、`done.md` の「未確認」に「GamePanel の本文の flex-basis と内容の高さ」を 1 行書く

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Research moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Research
git commit -m "feat(uitk): 研究の詳細ペイン・消費アイテム・解放物の節・研究ボタンを足す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---
### Task 9: 研究エリアの View と Presenter と L1

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/ResearchTreeView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Research/ResearchTreePresenter.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Research/ResearchTreeViewTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Research/ResearchTreeViewportTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Research/ResearchStageLayoutTest.cs`

**Interfaces:**
- Consumes: Task 3 の `ResearchTreeViewModel`、Task 4 の `ResearchTreeModel.Current`、Task 5 の `IResearchTreeCommands`、Task 7 の `ResearchNodeCardSet`・`ResearchCalcKnobs.ApplyAreaLeft`、Task 8 の `ResearchDetailPane`・`ResearchScreenTestKit`・`RecordingResearchTreeCommands`、C4-d の `NodeGraphView`、`GameUiRoot.GetLayer`・`UiRoutingState`・`StageColumnsLayout`・`UiScreenTraitsTable`
- Produces:
  - `public sealed class ResearchTreeView : VisualElement { public ResearchTreeView(IResearchTreeCommands commands, GameContentPresentation content, UiCommandResultToaster toaster); public void Render(ResearchTreeViewModel model); public void SetShown(bool shown); }`（name `research-tree`）
  - `public sealed class ResearchTreePresenter : IInitializable { public ResearchTreePresenter(GameUiRoot gameUiRoot, ResearchTreeModel model, UiRoutingState routing, IResearchTreeCommands commands, GameContentPresentation content, UiCommandResultToaster toaster); }`

- [ ] **Step 1: 研究エリアの L1 を書く**

`ResearchTreeViewTest.cs`:
```csharp
using System;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Progress;
using Client.Ui.Screens.Research;
using Client.Ui.State.Research.Tree;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究エリアの描画・カードの 4 状態・エリアの寸法・選択で開閉する詳細・進捗 HUD との層の順序（L1）
    /// Research area rendering, the four card states, area geometry and selection-driven details (L1)
    /// </summary>
    public class ResearchTreeViewTest : ScreenTestHost
    {
        // Web の持ち物の幅（--inventory-panel-width）と列の間隔（--stage-column-gap）
        // Web inventory width (--inventory-panel-width) and column gap (--stage-column-gap)
        private const float InventoryPanelWidth = 473.891247f;
        private const float StageColumnGap = 16f;

        private Texture2D _texture;
        private GameContentPresentation _content;
        private RecordingResearchTreeCommands _commands;
        private UiLayerStack _layers;
        private VisualElement _layer;
        private ResearchTreeView _view;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _content = Kit.LoadMasterAndContent(_texture);
            _commands = new RecordingResearchTreeCommands();

            // 本番と同じ層（OverlayPanel・Stage）へ、描いてから載せる
            // Render first, then mount on the production layer (OverlayPanel, Stage)
            _view = new ResearchTreeView(_commands, _content, Toaster);
            _view.Render(Kit.Tree(ResearchSelection.None));
            _view.SetShown(true);
            _layers = UiLayerStack.Build(rootVisualElement);
            _layer = _layers.GetLayer(UiLayer.OverlayPanel, UiFamily.Stage);
            _layer.Add(_view);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">research tree renders nodes when uiState enters ResearchTree</web-test>
        [Test]
        public void 研究画面を開くとノードのカードが並ぶ()
        {
            Assert.IsTrue(IsShown(Find<VisualElement>(rootVisualElement, "research-tree")));
            foreach (var guid in Kit.AllNodes) Assert.IsTrue(IsShown(Card(guid)), guid.ToString());
            Assert.AreEqual("Research", Find<VisualElement>(rootVisualElement, "research-panel").Q<Label>(className: "game-panel__title").text);
        }

        /// <web-test file="src/features/research/ResearchNodeCard.test.ts">枠色用のdata属性と状態ラベルは%sで揃って付く</web-test>
        /// <web-test file="e2e/tests/research/research.spec.ts">ノードカードが4状態のdata属性で描き分けられる</web-test>
        [TestCase("00000000-0000-0000-0000-000000000201", true, false, false, "Available")]
        [TestCase("00000000-0000-0000-0000-000000000202", false, true, false, "Completed")]
        [TestCase("00000000-0000-0000-0000-000000000204", false, false, true, "Unavailable")]
        [TestCase("00000000-0000-0000-0000-000000000203", false, false, false, "Unavailable")]
        [TestCase("00000000-0000-0000-0000-000000000205", false, false, true, "Unavailable")]
        public void カードの枠の修飾と状態ラベルが状態で揃って付く(string guidText, bool ready, bool completed, bool locked, string stateLabel)
        {
            var guid = Guid.Parse(guidText);
            var card = Card(guid);
            Assert.AreEqual(ready, card.ClassListContains("research-tree__card--ready"), "ready");
            Assert.AreEqual(completed, card.ClassListContains("research-tree__card--completed"), "completed");
            Assert.AreEqual(locked, card.ClassListContains("research-tree__card--locked"), "locked");
            Assert.AreEqual(stateLabel, Find<Label>(rootVisualElement, $"research-node-state-{guid}").text);

            // ロックだけ減光する（値は Task 11 で決めたトークン）
            // Only locked cards dim (the value is the token settled in Task 11)
            Assert.AreEqual(locked, card.resolvedStyle.opacity < 1f, "opacity");
        }

        /// <web-test file="src/features/research/ResearchNodeCard.test.ts">状態ラベルはnodeState classNameでアイコン直下に描く</web-test>
        [Test]
        public void 状態ラベルはアイコンの直下に描く()
        {
            var card = Card(Kit.Completed);
            Assert.IsTrue(card[0].ClassListContains("research-tree__card-name"));
            Assert.IsTrue(card[1].ClassListContains("research-tree__card-icon"));
            var state = Find<Label>(rootVisualElement, $"research-node-state-{Kit.Completed}");
            Assert.AreSame(card[2], state);
            Assert.GreaterOrEqual(state.worldBound.yMin, card[1].worldBound.yMax - 0.5f);
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">研究パネルは持ち物の右隣から画面端までを占有し持ち物と重ならない</web-test>
        [Test]
        public void 研究エリアは持ち物の幅と列の間隔の右から舞台の右端と上下端までを占める()
        {
            var area = Find<VisualElement>(rootVisualElement, "research-tree").worldBound;
            var stage = _layer.worldBound;
            Assert.AreEqual(InventoryPanelWidth + StageColumnGap, area.xMin - stage.xMin, 1f, "left");
            Assert.AreEqual(stage.xMax, area.xMax, 1.5f, "right");
            Assert.AreEqual(stage.yMin, area.yMin, 1.5f, "top");
            Assert.AreEqual(stage.yMax, area.yMax, 1.5f, "bottom");

            // エリアの根は拾わず、持ち物の側（エリアの左）はエリアに奪われない
            // The area root does not pick, so the inventory side (left of the area) is not taken by it
            Assert.AreEqual(PickingMode.Ignore, Find<VisualElement>(rootVisualElement, "research-tree").pickingMode);
            Assert.IsNull(rootVisualElement.panel.Pick(new Vector2(area.xMin - 20f, area.center.y))?.GetFirstAncestorOfType<ResearchTreeView>());
        }

        /// <web-test file="src/features/research/ResearchTreePanel.test.ts">選択トグルで詳細ペインが開閉しrenderNodeが更新される</web-test>
        [Test]
        public void 選択があれば詳細ペインを出してカードを選択の印にし無くなれば閉じる()
        {
            Assert.IsFalse(DetailPaneShown());

            _view.Render(Kit.Tree(ResearchSelection.Of(Kit.Researchable)));
            simulate.FrameUpdate();
            Assert.IsTrue(DetailPaneShown());
            Assert.IsTrue(Card(Kit.Researchable).ClassListContains("research-tree__card--selected"));

            // 研究ボタンは本文の幅いっぱい（Web は幅の指定なしで 300 − 枠 2 − craft の padding 25 − 本文 20 ＝ 253。D-21）
            // The research button fills the body width (Web has no width: 300 − border 2 − craft padding 25 − body 20 = 253; D-21)
            Assert.AreEqual(253f, Find<VisualElement>(rootVisualElement, $"research-button-{Kit.Researchable}").worldBound.width, 0.5f, "button width");

            _view.Render(Kit.Tree(ResearchSelection.None));
            simulate.FrameUpdate();
            Assert.IsFalse(DetailPaneShown());
            Assert.IsFalse(Card(Kit.Researchable).ClassListContains("research-tree__card--selected"));
        }

        [Test]
        public void 文字の上はカードの根が拾いアイコンの上はツールチップのためにアイコンが拾う()
        {
            // 文字は拾わずカードの根へ、アイコンは Web の ItemSlot のホバーの名前のために自分で拾う（押下の浮上は視野の L1 のタップが見る）
            // Text falls through to the card root; the icon picks for the Web ItemSlot hover name (bubbling of presses is covered by the viewport tap L1)
            var card = Card(Kit.Researchable);
            Assert.AreSame(card, rootVisualElement.panel.Pick(card[0].worldBound.center));
            Assert.AreSame(card, rootVisualElement.panel.Pick(card[2].worldBound.center));
            var onIcon = rootVisualElement.panel.Pick(card[1].worldBound.center);
            Assert.AreNotSame(card, onIcon);
            Assert.AreSame(card, onIcon.GetFirstAncestorOfType<Client.Ui.Screens.Research.Parts.ResearchNodeCard>());
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">研究パネル展開中も採掘進捗バーが遮蔽されない（.viewportOverlayのz封じ込めに依存する回帰ガード）</web-test>
        [Test]
        public void 採掘の進捗バーは研究エリアより上の層にあり研究エリアに遮られない()
        {
            // 本番の ProgressHudPresenter と同じ層（Chrome・Viewport）に本物の進捗 HUD を出す
            // Show the real progress HUD on the production layer (Chrome, Viewport) as ProgressHudPresenter does
            var progress = new ProgressHudView();
            progress.SetShown(true);
            _layers.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(progress);
            simulate.FrameUpdate();
            var bar = Find<VisualElement>(rootVisualElement, "progress-bar");
            var center = bar.worldBound.center;

            // 研究エリアがバーの中心を覆っていなければ層の順序を見たことにならない
            // Unless the research area covers the bar centre, the layer order is not being tested
            Assert.IsTrue(Find<VisualElement>(rootVisualElement, "research-tree").worldBound.Contains(center), "area covers the bar");

            // HUD は拾わないので、Web の expectHitTestWithin と同じく判定の間だけ根を拾う設定にして戻す
            // The HUD ignores picking, so flip its root to Position only for the check and back, like Web expectHitTestWithin
            bar.pickingMode = PickingMode.Position;
            var hit = rootVisualElement.panel.Pick(center);
            bar.pickingMode = PickingMode.Ignore;
            Assert.IsTrue(hit == bar || bar.Contains(hit), hit?.name);
        }

        private VisualElement Card(Guid guid)
        {
            return Find<VisualElement>(rootVisualElement, $"research-node-{guid}");
        }

        private bool DetailPaneShown()
        {
            // 詳細ペインは最初の詳細で初めて載る
            // The detail pane is mounted on the first details
            var pane = rootVisualElement.Q("research-detail-pane");
            return pane != null && IsShown(pane);
        }
    }
}
```
（題のラベルのクラス `game-panel__title` は実物 `GamePanelTitle.cs` のとおり。進捗バーの中心が研究エリアの外になる〈試験のパネルで `progress-hud` の配置がホットバーの床を前提にずれる〉なら、`progress` の根に `position: absolute` と研究エリアの中心の `left`・`top` を inline で当てて重ねる。層の順序だけを見る主張なので重ね方は試験側で決めてよい〈Web の元テストが守るのも z の封じ込め〉）

- [ ] **Step 2: 視野の L1 を書く**

`ResearchTreeViewportTest.cs`:
```csharp
using System;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Research;
using Client.Ui.State.Research.Tree;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究ツリーの視野（中央寄せ・開き直しで残る・ホイールとパン・ノードのドラッグとタップ）（L1）
    /// Research tree viewport (centering, kept across reopen, wheel and pan, node drag and tap) (L1)
    /// </summary>
    public class ResearchTreeViewportTest : ScreenTestHost
    {
        private Texture2D _texture;
        private RecordingResearchTreeCommands _commands;
        private ResearchTreeView _view;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            var content = Kit.LoadMasterAndContent(_texture);
            _commands = new RecordingResearchTreeCommands();
            _view = new ResearchTreeView(_commands, content, Toaster);
            _view.Render(Kit.Tree(ResearchSelection.None));
            _view.SetShown(true);
            UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.OverlayPanel, UiFamily.Stage).Add(_view);
            Settle();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/research/researchViewport.spec.ts">research tree opens centered on the researchable node</web-test>
        [Test]
        public void 研究可能なノードを中央にして開く()
        {
            var viewport = Find<VisualElement>(rootVisualElement, "research-viewport").worldBound.center;
            var node = ResearchableCard().worldBound.center;
            Assert.AreEqual(viewport.x, node.x, 1.5f);
            Assert.AreEqual(viewport.y, node.y, 1.5f);
        }

        /// <web-test file="e2e/tests/research/researchViewport.spec.ts">research tree keeps its pan position across close and reopen</web-test>
        [Test]
        public void パンした位置は閉じて開き直しても残る()
        {
            var before = ResearchableCard().worldBound;
            Drag(EmptyBackgroundPoint(), new Vector2(-60f, 30f));
            var panned = ResearchableCard().worldBound;
            Assert.LessOrEqual(panned.x - before.x, -59.5f);
            Assert.GreaterOrEqual(panned.y - before.y, 29.5f);

            // 閉じて開き直す（Model は入り直しで選択の無い同じ並びを配る）
            // Close and reopen (the Model hands the same order without selection on re-entry)
            _view.SetShown(false);
            Settle();
            _view.SetShown(true);
            _view.Render(Kit.Tree(ResearchSelection.None));
            Settle();
            var reopened = ResearchableCard().worldBound;
            Assert.AreEqual(panned.x, reopened.x, 1f);
            Assert.AreEqual(panned.y, reopened.y, 1f);
        }

        /// <web-test file="e2e/tests/research/researchViewport.spec.ts">research tree zooms with the wheel and pans by dragging its empty background</web-test>
        [Test]
        public void ホイールで拡縮し空の背景のドラッグでパンし右ドラッグでは動かない()
        {
            // ホイール 4 ノッチ（Web の deltaY 240）でカーソルを中心に拡大し、戻すと縮む
            // Four notches (Web deltaY 240) zoom in around the cursor, and the reverse shrinks
            var beforeZoom = ResearchableCard().worldBound;
            var cursor = beforeZoom.center;
            Wheel(cursor, -4f);
            var zoomed = ResearchableCard().worldBound;
            Assert.Greater(zoomed.width, beforeZoom.width);
            Assert.AreEqual(cursor.x, zoomed.center.x, 1f);
            Assert.AreEqual(cursor.y, zoomed.center.y, 1f);
            Wheel(cursor, 4f);
            Assert.Less(ResearchableCard().worldBound.width, zoomed.width);

            // 空の背景を左ドラッグするとドラッグ量以上に動く（慣性の上限は C4-d の L1 が見る）
            // A left drag on empty background moves at least the drag distance (C4-d's L1 owns the glide cap)
            var grip = EmptyBackgroundPoint();
            var beforePan = ResearchableCard().worldBound;
            Drag(grip, new Vector2(-80f, -50f));
            var afterPan = ResearchableCard().worldBound;
            Assert.LessOrEqual(afterPan.x - beforePan.x, -79.5f);
            Assert.LessOrEqual(afterPan.y - beforePan.y, -49.5f);

            // 右ドラッグはパンしない
            // A right drag does not pan
            simulate.MouseDown(grip, MouseButton.RightMouse);
            simulate.MouseMove(grip + new Vector2(-80f, -50f));
            simulate.MouseUp(grip + new Vector2(-80f, -50f), MouseButton.RightMouse);
            Settle();
            Assert.AreEqual(afterPan.position, ResearchableCard().worldBound.position);
        }

        /// <web-test file="e2e/tests/research/researchViewport.spec.ts">ノードを掴んだドラッグもパンになり、詳細ペインは開かない</web-test>
        [Test]
        public void ノードを掴んだドラッグもパンになり選択しない()
        {
            var before = ResearchableCard().worldBound;
            Drag(before.center, new Vector2(-60f, 30f));
            var after = ResearchableCard().worldBound;
            Assert.LessOrEqual(after.x - before.x, -59.5f);
            Assert.GreaterOrEqual(after.y - before.y, 29.5f);
            CollectionAssert.IsEmpty(_commands.Issued);
        }

        /// <web-test file="e2e/tests/research/researchViewport.spec.ts">閾値未満のノード押下はタップとして詳細ペインを開く</web-test>
        [Test]
        public void 閾値未満の押下はタップとして選択を切り替えパンしない()
        {
            var before = ResearchableCard().worldBound;
            Drag(before.center, new Vector2(2f, 2f));
            CollectionAssert.AreEqual(new[] { $"toggle:{Kit.Researchable}" }, _commands.Issued);
            var after = ResearchableCard().worldBound;
            Assert.AreEqual(before.x, after.x, 0.5f);
            Assert.AreEqual(before.y, after.y, 0.5f);
        }

        #region Helpers

        private VisualElement ResearchableCard()
        {
            return Find<VisualElement>(rootVisualElement, $"research-node-{Kit.Researchable}");
        }

        // ビューポートの右下から内へ探し、カードの無い点を返す（Web の findEmptyBackgroundPoint）
        // Probe inward from the viewport's bottom right for a point with no card (Web findEmptyBackgroundPoint)
        private Vector2 EmptyBackgroundPoint()
        {
            var box = Find<VisualElement>(rootVisualElement, "research-viewport").worldBound;
            for (var inset = 40f; inset < Mathf.Min(box.width, box.height) / 2f; inset += 8f)
            {
                var point = new Vector2(box.xMax - inset, box.yMax - inset);
                var picked = rootVisualElement.panel.Pick(point);
                if (picked == null || picked.GetFirstAncestorOfType<ResearchTreeView>() == null) continue;
                if (picked.ClassListContains("research-tree__card") || picked.GetFirstAncestorOfType<Parts.ResearchNodeCard>() != null) continue;
                return point;
            }
            throw new InvalidOperationException("研究ツリーに空の背景の掴み点が無い");
        }

        private void Drag(Vector2 from, Vector2 delta)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(from + delta * 0.5f);
            simulate.MouseMove(from + delta);
            simulate.MouseUp(from + delta, MouseButton.LeftMouse);
            Settle();
        }

        private void Wheel(Vector2 point, float notches)
        {
            // 負の y が上（Web の deltaY < 0 ＝拡大）。1 ノッチは scrollDeltaPerTick（C4-d の WheelNotches と同じ）
            // Negative y is up (Web deltaY < 0 = zoom in); one notch is scrollDeltaPerTick (same as C4-d WheelNotches)
            simulate.ScrollWheel(new Vector2(0f, notches * WheelEvent.scrollDeltaPerTick), point);
            Settle();
        }

        // 逃げの読み取り・中央寄せ・慣性の数フレームを流す
        // Run a few frames for clearance reads, centering and glide
        private void Settle()
        {
            for (var i = 0; i < 4; i++) simulate.FrameUpdate();
        }

        #endregion
    }
}
```
（Web のホイールの検査は 960×540 で撮るが、`UiTestHost` は 1280×720。拡縮の中心とパンの量は画面の寸法に依らないので 1280×720 で写す。D-17。慣性が `simulate.FrameUpdate` で進まず `Settle` の後も動き続けるなら、C4-d の L1〈`NodeGraphViewGestureTest`〉の止め方へ合わせる）

- [ ] **Step 3: 舞台の左余白と常駐 HUD の L1 を書く**

`ResearchStageLayoutTest.cs`:
```csharp
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.State.Routing;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究画面の舞台の性質（左の余白を畳む・常駐 HUD を出さない）。持ち主は C4-e と各 HUD で、ここは研究画面の値を固定する（L1）
    /// Research screen stage traits (left padding collapsed, no always-on HUD); owned by C4-e and each HUD, this pins the research values (L1)
    /// </summary>
    public class ResearchStageLayoutTest : UiTestHost
    {
        // 舞台の左の余白（Web .stage の padding-left。StageColumnsLayoutTest と同じ値）
        // Stage left padding (Web .stage padding-left; same value as StageColumnsLayoutTest)
        private const float StagePaddingLeft = 40f;

        /// <web-test file="e2e/tests/research/research.spec.ts">研究画面では持ち物がstage左paddingぶん左へ寄る</web-test>
        [Test]
        public void 研究画面では持ち物の列が舞台の左の余白ぶん左へ寄り縦は動かない()
        {
            var layout = new StageColumnsLayout();
            UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Screen, UiFamily.Stage).Add(layout);
            var inventory = new VisualElement();
            inventory.style.width = 473.891247f;
            inventory.style.height = 100f;
            layout.Get(StageColumn.Inv).Add(inventory);

            // 本番の GameUiStageColumnsInstaller と同じく、画面の性質の表の値で畳む
            // Collapse by the screen-trait value, as the production GameUiStageColumnsInstaller does
            layout.SetLeftPaddingCollapsed(UiScreenTraitsTable.Get(UiScreen.PlayerInventory).CollapsesStageLeftPadding);
            simulate.FrameUpdate();
            var onInventoryScreen = inventory.worldBound;
            layout.SetLeftPaddingCollapsed(UiScreenTraitsTable.Get(UiScreen.ResearchTree).CollapsesStageLeftPadding);
            simulate.FrameUpdate();
            var onResearchScreen = inventory.worldBound;

            Assert.AreEqual(StagePaddingLeft, onInventoryScreen.xMin - onResearchScreen.xMin, 1f);
            Assert.AreEqual(onInventoryScreen.yMin, onResearchScreen.yMin, 1.5f);
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">研究画面ではホットバーと装備HUDを描画しない</web-test>
        [Test]
        public void 研究画面は常駐HUDを出さない画面で持ち物画面では出す()
        {
            // ホットバーと装備 HUD はこの値を読んで消える（持ち主 S3-hotbar・S4-inventory）
            // The hotbar and equipment HUD read this value to hide (owners S3-hotbar, S4-inventory)
            Assert.IsFalse(UiScreenTraitsTable.Get(UiScreen.ResearchTree).AlwaysOnHud);
            Assert.IsTrue(UiScreenTraitsTable.Get(UiScreen.PlayerInventory).AlwaysOnHud);
        }
    }
}
```
（`UiScreen` の名前空間は Task 1 Step 1 の grep で確かめる。`Client.Ui.State.Routing` に無ければ実物の名前空間を `using` に足す）

- [ ] **Step 4: View を書く**

`ResearchTreeView.cs`:
```csharp
using System;
using Client.Ui.Components.NodeGraph;
using Client.Ui.Components.Panels;
using Client.Ui.Content;
using Client.Ui.Screens.Research.Parts;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research
{
    /// <summary>
    /// 研究エリア（Web ResearchTreePanel）。常駐の NodeGraphView にカードを使い回して渡し、選択があれば詳細ペインを浮かべる
    /// The research area (Web ResearchTreePanel); hands reused cards to a resident NodeGraphView and floats the detail pane while selected
    /// </summary>
    public sealed class ResearchTreeView : VisualElement
    {
        private readonly NodeGraphView _graph;
        private readonly ResearchNodeCardSet _cards;
        private readonly VisualElement _detailHost;
        private readonly ResearchDetailPane _detailPane;
        private bool _hasGraph;
        private bool _hasFocus;
        private Vector2 _focus;

        public ResearchTreeView(IResearchTreeCommands commands, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            name = "research-tree";
            AddToClassList("research-tree");
            pickingMode = PickingMode.Ignore;
            ResearchCalcKnobs.ApplyAreaLeft(this);

            // 題「研究」のパネルとツリー。タップは選択の切り替えへ
            // The "Research" panel and the tree; taps toggle the selection
            var panel = new GamePanel("research-panel", GamePanelFrame.Default);
            panel.AddToClassList("research-tree__panel");
            panel.SetTitle(new UiText(LocalizationKeys.Ui.Research.Title));
            _graph = new NodeGraphView("research");
            _graph.AddToClassList("research-tree__graph");
            _graph.OnNodeTapped.Subscribe(key => commands.ToggleNodeSelection(Guid.Parse(key)));
            panel.Add(_graph);
            Add(panel);

            // 詳細ペインはパン・ズームの外に浮かべ、最初の詳細で載せる（キーの無いラベルを載せない）
            // The detail pane floats outside pan and zoom and mounts on the first details (no keyless labels attached)
            _detailHost = new VisualElement { pickingMode = PickingMode.Ignore };
            _detailHost.AddToClassList("research-tree__detail-host");
            Add(_detailHost);
            _detailPane = new ResearchDetailPane(commands, content, toaster);
            _cards = new ResearchNodeCardSet(content);
        }

        public void Render(ResearchTreeViewModel model)
        {
            // グラフの組み直しはノードの並びか注目点が変わったときだけ（視野を残すため）
            // Rebuild the graph only when node order or focus changes (to keep the viewport)
            var orderChanged = _cards.RenderCards(model.Nodes);
            var hasFocus = model.TryGetInitialFocus(out var focus);
            var focusChanged = hasFocus != _hasFocus || (hasFocus && focus != _focus);
            if (!_hasGraph || orderChanged || focusChanged)
            {
                _graph.SetGraph(_cards.BuildContent(model.Nodes, hasFocus ? NodeGraphFocus.At(focus) : NodeGraphFocus.None));
                _hasGraph = true;
                _hasFocus = hasFocus;
                _focus = focus;
            }

            // 詳細は選択の有無で出し分ける
            // Details show or hide by the selection
            if (!model.TryGetDetail(out var detail))
            {
                _detailPane.Hide();
                return;
            }
            _detailPane.Render(detail);
            if (_detailPane.parent == null) _detailHost.Add(_detailPane);
        }

        public void SetShown(bool shown)
        {
            // 隠す間も要素は残し、NodeGraphView の視野を保つ（C4-d D-23(3)）
            // Keep the element while hidden so NodeGraphView keeps its viewport (C4-d D-23(3))
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
```

`ResearchTreePresenter.cs`:
```csharp
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Routing;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Research
{
    /// <summary>
    /// 研究エリアを起動時に 1 回 OverlayPanel 層へ載せ、経路で表示し Model で描く
    /// Mounts the research area once into the OverlayPanel layer, shows it by routing and draws it from the Model
    /// </summary>
    public sealed class ResearchTreePresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly ResearchTreeModel _model;
        private readonly UiRoutingState _routing;
        private readonly IResearchTreeCommands _commands;
        private readonly GameContentPresentation _content;
        private readonly UiCommandResultToaster _toaster;

        public ResearchTreePresenter(GameUiRoot gameUiRoot, ResearchTreeModel model, UiRoutingState routing, IResearchTreeCommands commands, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
            _routing = routing;
            _commands = commands;
            _content = content;
            _toaster = toaster;
        }

        public void Initialize()
        {
            // 描いてから載せる（キーの無いラベルを載せない）。研究エリアは Web の --z-stage-overlay-panel
            // Render before mounting (no keyless labels attached); the research area is the Web --z-stage-overlay-panel
            var view = new ResearchTreeView(_commands, _content, _toaster);
            view.Render(_model.Current.Value);
            _gameUiRoot.GetLayer(UiLayer.OverlayPanel, UiFamily.Stage).Add(view);
            _routing.Current.Subscribe(snapshot => view.SetShown(snapshot.Screen == UiScreen.ResearchTree));
            _model.Current.Subscribe(view.Render);
        }
    }
}
```

- [ ] **Step 5: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Research\."`
Expected: ErrorCount 0 / 31 PASS（`ResearchTreeViewTest` 11〈NUnit 7 本のうち `[TestCase]` 5 行の 1 本を含む〉・`ResearchTreeViewportTest` 5・`ResearchStageLayoutTest` 2・Task 8 の `ResearchDetailPaneTest` 8・`ResearchUnlockSectionsTest` 5）。`uloop get-logs --project-path ./moorestech_client --log-type Error` が 0 件

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Research moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Research
git commit -m "feat(uitk): 研究エリアの View・Presenter と描画・視野・舞台の L1 を足す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---
### Task 10: 登録と全体スイッチ UITK での起動

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/ResearchGameUiRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 1 行）
- Modify（在るときだけ）: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs`（`AnchorIds.ResearchNodePrefix` の 1 行を消す）

**Interfaces:**
- Consumes: Task 2 の `ResearchNodeCatalog`、Task 4 の `ResearchSelectionStore`・`ResearchTreeModel`、Task 5 の `ResearchTreeCommands`、Task 9 の `ResearchTreePresenter`、既存の `ResearchNodeStateStore`（`MainGameUiStateRegistration`）・`UiCommandResultToaster`・`GameContentPresentation`（`ContentGameUiRegistration`）
- Produces: 全体スイッチ UITK の起動で、R キーの研究画面が UITK で出る

- [ ] **Step 1: 登録を書く**

`ResearchGameUiRegistration.cs`:
```csharp
using Client.Ui.Screens.Research;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // 研究ツリー（GameUi）の登録。状態の源 ResearchNodeStateStore は両系の登録が持つ
    // Registers the GameUi research tree; the ResearchNodeStateStore source belongs to the shared registration
    internal static class ResearchGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<ResearchNodeCatalog>(Lifetime.Singleton);
            builder.RegisterEntryPoint<ResearchSelectionStore>().AsSelf();
            builder.RegisterEntryPoint<ResearchTreeModel>().AsSelf();
            builder.Register<ResearchTreeCommands>(Lifetime.Singleton).As<IResearchTreeCommands>();
            builder.RegisterEntryPoint<ResearchTreePresenter>();
        }
    }
}
```

`MainGameUiRegistration.cs` の画面登録区画の末尾（その時点の最後の `…GameUiRegistration.Register(builder);` の次）に:
```csharp
            ResearchGameUiRegistration.Register(builder);
```
`TutorialAnchorCoveragePending.cs` があり `ResearchNodePrefix` の行があれば、その 1 行を消す

- [ ] **Step 2: コンパイルと L2**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`
Expected: `ErrorCount: 0`、全件 PASS（`UiLayerDependencyDirectionTest`・`UiThemeImportBundleTest`・`LookExceptionLedgerTest` を含む。`TutorialAnchorCoverageTest` は在れば含む）

- [ ] **Step 3: 全体スイッチ UITK で起動し、研究画面を開いてエラー 0 と木を見る**

Run:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
uloop clear-console --project-path ./moorestech_client
uloop control-play-mode --project-path ./moorestech_client --action Play
```
45 秒待ってから（ドメインリロード中のエラーは 45 秒待って再試行。開幕スキットがあれば飛ばす）:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var control = UnityEngine.Object.FindObjectOfType<Client.Game.InGame.UI.UIState.UIStateControl>();
control.RequestTransition(Client.Game.InGame.UI.UIState.UIStateEnum.ResearchTree);
return control.CurrentState.ToString();'
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var root = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetRootElement();
var tree = root.Q("research-tree");
var cards = root.Query(className: "research-tree__card").ToList().Count;
return $"tree={tree != null} display={tree?.resolvedStyle.display} area={tree?.worldBound} cards={cards} master={Core.Master.MasterHolder.ResearchMaster.GetAllResearches().Count}";'
uloop get-logs --project-path ./moorestech_client --log-type Error
```
Expected: 1 本目の EDC が `GameUi`。2 本目が `ResearchTree`（`RequestTransition` が次のフレームで遷移するなら、もう 1 回 `return control.CurrentState.ToString();` だけの EDC を流して確かめる）。3 本目が `tree=True display=Flex`、`area` の x が `489.89 × 舞台の倍率` 付近、`cards` が `master` の件数と同じ。`get-logs` が 0 件。エラーがあれば原因を読んで直し、`done.md` の「証拠」に EDC の出力を貼る

- [ ] **Step 4: コミット**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
git add moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor
git commit -m "feat(uitk): 研究ツリーを全体スイッチ UITK の画面登録区画へ足す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 11: Web との並べ比べ（研究ツリー・チュートリアルの強調・詳細ペイン 3 状態。1280×720・2560×1080）

**Files:**
- なし（撮影の原本は `moorestech_client/.uloop/outputs/compare/S4-research/`。差の表は `done.md`）。直しが出たら `ResearchTree.uss`・`ResearchDetailPane.uss`・`DerivedTokens.uss` の「S4-research」ブロック、要れば台帳「## 見た目の例外」の末尾（`LX-136`〜`LX-140`）と「## 実測の結論」の末尾

**Interfaces:**
- Consumes: 参照 `LOGS/uitk/reference-captures/web-a9d5123537/`・`web-0e81bbe3c3-2560x1080/` の `4-research.png`・`5-tutorial-research-node.png`（状態は `W/e2e/uitk-reference/referenceShots.ts:112`・`:118` の controls で確かめる。`manifest.json` は状態を持たない）、mock-host の研究の fixture（`W/e2e/mock-host/researchFixtures.ts`: 完了 `1111…` (0,0)・前提未達 `2222…` (300,-120)・研究可能 `3333…` (600,0)・素材不足 `7777…` (600,-240)）、Task 9 の `ResearchTreeView`
- Produces: 3 組×2 解像度の並べ比べ、差の表、ロックの減光と研究ボタンの高さの決定値

- [ ] **Step 1: 詳細ペインの 3 状態の Web 参照を撮る**

先に `ls ../moorestech_logs/uitk/reference-captures/ | grep -i research` と C4-c の `done.md`（`../moorestech_logs/harness/uitk/C4-c/done.md`）を見て、C4-c が撮った `web-research-*.png`（研究可能・素材不足・完了の詳細）があればそれを使う。無ければ master の使い捨て worktree の mock-host で撮る（`feature/uitk` の Web には手を入れない。実行計画 E-22）:
```bash
moores-wt new uitk-ref-research-s4r --from origin/master --no-editor
cd <作られた worktree の絶対パス>/moorestech_web/webui && pnpm install
```
`e2e/uitk-reference/referenceShots.ts` の `4-research` の行を写した 3 行（id `4-research-detail-ready`・`4-research-detail-lacking`・`4-research-detail-completed`。controls は `4-research` と同じ topic `researchOwnedItems`・ui `ResearchTree`、steps の先頭に研究可能 `3333…`・素材不足 `7777…`・完了 `1111…` のノードのクリック 1 つと `expectVisible: research-detail-pane`）を一時的に足し、1280×720 と 2560×1080 で撮る。完了 `1111…` は fixture の x=0 で、初期の注目 `3333…`（x=600）へ中央寄せした視野の左外（持ち物の背後）にあり、そのままではクリックできない（`W/e2e/mock-host/researchFixtures.ts:18`・`:53`、`W/src/shared/treeView/TreeView.tsx:76-84`）。`shotRunner.ts` の手順は `click` だけでパンが無いので、使い捨ての worktree の中だけで `ShotStep` に `{ kind: "drag"; testId: string; dx: number; dy: number }` を足し、`runStep` に「`testId` の要素の右下から内へ 40px の空の背景で `page.mouse.move` → `down` → `move(+dx, +dy, { steps: 10 })` → `up`」を足す。完了の行だけ steps を `drag(research-viewport, +400, 0)`・`click(research-node-1111…)`・`expectVisible(research-detail-pane)` にする（両解像度とも css px で +400。カードが詳細ペインの左に見えることを撮った png で確かめ、隠れていれば dx を変えて撮り直し、使った dx を `done.md` の「## 証拠」に書く）:
```bash
SHA=$(git rev-parse --short=10 HEAD)
UITK_REFERENCE_OUT=<moorestech_logs の絶対パス>/uitk/reference-captures/web-$SHA-research-detail \
UITK_REFERENCE_ONLY=4-research-detail UITK_REFERENCE_PORT=<空きポート> node --import tsx e2e/uitk-reference/capture.ts
sed -i '' 's/referenceViewport = { width: 1280, height: 720 }/referenceViewport = { width: 2560, height: 1080 }/' e2e/uitk-reference/shotRunner.ts
UITK_REFERENCE_OUT=<moorestech_logs の絶対パス>/uitk/reference-captures/web-$SHA-research-detail-2560x1080 \
UITK_REFERENCE_ONLY=4-research-detail UITK_REFERENCE_PORT=<空きポート> node --import tsx e2e/uitk-reference/capture.ts
git checkout -- e2e/uitk-reference
cd - && moores-wt rm uitk-ref-research-s4r --force --prune-branch
```
（`UITK_REFERENCE_ONLY` が `capture.ts` に無ければ、足した 3 行以外を一時的にコメントアウトして全件を撮らない。実物の撮影の口は `capture.ts` の冒頭を読んで合わせる）
Expected: 6 枚と `manifest.json`。worktree が消える。`done.md` の「integrator への依頼」に「詳細ペインの参照 `web-<sha>-research-detail*` を撮った。台帳『## 参照キャプチャ』への記載をお願いしたい」と書く

- [ ] **Step 2: 撮影の口を用意する（PlayMode・GameUi・言語・複製した PanelSettings の RenderTexture）**

S3-hotbar-modal の Task 11 Step 2 と同じ手順で、`GameUiPanelSettings.asset` の複製に 1280×720・2560×1080 の sRGB ARGB32 RenderTexture を付けた UIDocument（名前 `s4r-compare-<w>`）を作り、言語を参照（png を Read して確かめる）に揃え、ロケールのクラスを当てる。ルートの背景は参照の研究エリアの外（持ち物の右の列間 `x≈482, y≈360`）の画素の単色にし、差の表に「背景は世界でなく単色。左の持ち物は描かない（S4-inventory）」と書く。ルートに `UiLayerStack.Build` の `OverlayPanel`/`Stage` 層を作り、`ResearchTreeView` を載せる。ViewModel は mock-host の fixture と同じ 4 ノード（座標・前提・状態・所持の割当）を `ResearchTreeViewModelFactory.Create` へ、`ResearchNodeDefinition` を EDC の中で組んで渡す（名前とアイコンは PlayMode のマスタに無い GUID なので、名前は差の表で「捨てる（fixture の名前）」、アイコンは PlayMode のマスタの任意のアイテム）。撮るたびに `uloop focus-window` の後の EDC で `RenderTexture.active` → `ReadPixels` → `EncodeToPNG` で `moorestech_client/.uloop/outputs/compare/S4-research/uitk-<撮影 id>-<w>x<h>.png` へ書く

- [ ] **Step 3: 研究ツリーを撮り、ロックの減光と研究ボタンの高さを決める**

1. `4-research`: 参照を Read し、4 ノードの位置・枠の色（完了 `--text-default`・研究可能 `--select-cyan`・素材不足は素の枠・前提未達はロックの減光）・状態ラベル・線・パネルの題と飾りを確かめ、選択なしの ViewModel で撮る。視野の中央寄せの差（研究可能が中央）を見る
2. ロックの減光: 前提未達のカード（`2222…`）の面・枠・名前の字の画素を 3 点ずつ参照と UITK で読み、RGB の差が 7 段以内か見る。外れたら `DerivedTokens.uss` の `--research-node-locked-opacity` を 0.05 刻みで下げて撮り直し（前例 `--slot-frame-insufficient-opacity` 0.4 → 0.2）、3 点の最大差が最小になる値に決める。7 段に入らなければ最小の値で `LX-136` に記帳する。台帳「## 実測の結論」の末尾に「研究ノードのロックの減光: Web 0.45 → UITK <値>（面・枠・字の最大差 <n> 段。Linear 合成）」を 1 行
3. `5-tutorial-research-node`: 参照の強調の枠（チュートリアルの描き手）は S2-tutorial の持ち物。UITK ではカードに `research.node-<guid>` のアンカーがあり `TutorialAnchorRegistry.Resolve` が `Unique` を返すことだけを見て、強調の描画は差の表で「捨てる（S2-tutorial の描き手。integrator への依頼 D-9）」とする。研究ツリーの残り（題・カード）は 1. と同じ見方
4. 詳細ペインの 3 状態: Step 1 の参照と同じノードを選択した ViewModel で撮る。完了の組は Web と同じ視野にそろえる: 描いた後に視野の空の背景を Step 1 と同じ dx（パネルの論理 px で +400）だけドラッグする（EDC の中で `PointerDownEvent`・`PointerMoveEvent`・`PointerUpEvent` を viewport へ送る。送り方は C4-d の `NodeGraphViewGestureTest` に合わせる。C4-d に視野を読んで当て直す公開 API〈`RestoreViewport` 等〉があればそれで同じ平行移動を当ててよい）。完了のカードの中心が参照の png と 1280×720・2560×1080 の両方で 2px（2560×1080 は 3px）以内に来ることを画素で確かめてから比べる。見る点: 右上 12px・幅 300・`GamePanel(craft)` の枠と飾り・名前 16px・閉じる「×」・説明 13px・消費アイテムの枠と所持/必要（素材不足は赤の強調）・解放物の節の見出し 12px と枠・研究ボタンの面のグラデ・無効の面 `rgb(50 52 67)` と淡さ・文言「研究／完了済み」・理由 12px。研究ボタンの高さは参照のボタンの上端・下端の行を画素で探して実画面 px を出し、`ResearchDetailPane.uss` の `--recipe-action-height` をその値（2560×1080 は 1.5 倍で割る）に直す。幅も同じく画素で測り、本文の幅（1280×720 で 253）と合うことを確かめる

- [ ] **Step 4: 差の表を書き、口を片付ける**

参照と UITK を `Read` で並べて見て、`done.md` の「## 差の表」に組ごとに（位置・寸法／色／装飾の有無／文字の段組）の表を書く。各行は「同じ／直した／LX-n／捨てる」のどれか。一目で分かる差は USS で直して撮り直す。同じにするのが大きくて差が一目で分からないものだけ、台帳「## 見た目の例外」の末尾へ `LX-136`〜`LX-140` で記帳し、該当行に `/* look-exception: LX-n */` を付ける（範囲を超えるなら止めて指揮へ番号を求める）。撮り終えたら `s4r-compare-*` を `Object.Destroy` し、RT を `Release` し、言語を元へ戻す

- [ ] **Step 5: 直しがあればコミット**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Research moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss docs/uitk/TODO.md
git commit -m "fix(uitk): 研究ツリーを Web との並べ比べで揃え、ロックの減光と研究ボタンの高さを決める（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
（直しが無ければコミットしない。`ResearchTreeViewTest` のロックの opacity の主張は値に依らず通る）

---

### Task 12: unityプレイ録画テスト（`uitk/uitk-research-tree`・`tier9/tier9-5-compute-research`）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-research-tree.cs`

**Interfaces:**
- Consumes: プレイテスト DSL（`p.SetupDebugEnvironment`・`p.SkipOpeningSkitIfPlaying`・`p.ExitToGameScreen`・`p.PressKey`・`p.WaitUiState`・`p.ClickUi`・`p.UntilUiElement`・`p.WaitForResearch`・`p.RunOnServer`・`p.Screenshot`・`p.Note`・`p.Assert`・`PlaytestUiElementOps.FindUi`・`SemanticInput.MouseMoveTo`・`MouseButtonDown`・`MouseGlideTo`・`MouseButtonUp`）、`IResearchDataStore`、`MasterHolder.ResearchMaster`、`GameUiRoot`、`TutorialAnchorRegistry.Resolve`・`AnchorIds.ResearchNode`、`UiInputExclusivity.ActiveUiSystem`
- Produces: 録画 2 本（`result.json` が `Success: true`）

- [ ] **Step 1: 録画を書く**

`uitk-research-tree.cs`（前例 `scenarios/uitk/uitk-mode-hud-and-tooltip.cs` の冒頭の入力準備と系の主張、`scenarios/tier9/tier9-5-compute-research.cs` の研究の前提の閉包・`WaitForResearch`・R キー）。流れ:
1. 冒頭で `p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}")` と `p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）")`。入力の準備は前例と同じ
2. `p.SetupDebugEnvironment(new PlaytestEnvironmentConfig())` → `p.SkipOpeningSkitIfPlaying()` → `p.ExitToGameScreen()`。研究可能なノードを 1 つ選ぶ（`MasterHolder.ResearchMaster.GetAllResearches()` から前提が無いか前提を `IResearchDataStore.LoadResearchData` で完了扱いにでき、消費アイテムを `p.RunOnServer` で持たせられるもの。前例 tier9-5 の `WithPrerequisites` と同じ閉包）。前提を完了扱いにし、消費アイテムを持たせる
3. `p.PressKey(Key.R)` → `p.WaitUiState(UIStateEnum.ResearchTree, 10f)` → `FindUi("research-tree")` と `FindUi("research-viewport")` が見える。研究可能なノード（`ResearchLogic.TryFindInitialFocusNode` の選ぶもの。サーバーの状態で 1 つ目の研究可能）のカードの中心がビューポートの中心から 2px 以内（`ScreenRect.center`）。`p.Screenshot("01-open-centered")`
4. マスタの研究ノードのアンカー: `TutorialAnchorRegistry.Resolve(GameUiRoot.Instance.GetRootElement(), AnchorIds.ResearchNode(guid))` が全ノードで `Unique`（チュートリアルが指す 7 件〈`challenges.json` の `research.node-*`〉を含む）。件数を `p.Note` に出す
5. 遮られない（層の順序の主張。Chrome 層の HUD が OverlayPanel 層の研究エリアより上で、上の層に覆われていないこと）: `var root = GameUiRoot.Instance.GetRootElement();` で `root.Q("challenge-hud")`・`root.Q("key-hints")` を引き、それぞれ `resolvedStyle.display == Flex` かつ `worldBound` の幅と高さが 0 より大きいことを `p.Assert`（出ていなければ失敗。素通りさせない）。HUD は部分木ごと拾わない（`UiPickingIgnore.ApplyToSubtree`）ので、Web の `layoutAssertions.ts` の `expectHitTestWithin` と同じく、判定の間だけ HUD の根の `pickingMode` を `Position` にし、中心で `root.panel.Pick(hud.worldBound.center)` を取り、すぐ `Ignore` に戻す。返った要素が HUD 自身かその子孫であることを `p.Assert`。本番の HUD の入力の透過は変えない。1280×720 では両 HUD とも研究エリア（x≈490 から右）の外にあるので、両 HUD と研究エリアの `worldBound` を `p.Note` に出す（重なる配置での層の順序は採掘の進捗バーの L1〈Task 9 の `ResearchTreeViewTest`〉が持つ。D-18）
6. タップで詳細: ステップ 2 のノードのカードを `p.ClickUi($"research-node-{guid}")` → `p.UntilUiElement("research-detail-pane", 5f)`、`research-button-{guid}` が押せる。`p.Screenshot("02-detail")`
6b. ホバーのツールチップ: マウスをカードのアイコン（`research-node-icon-{guid}`）の中心へ動かして 1 秒待ち、`GameUiRoot.Instance.GetLayer(UiLayer.Tooltip, …)`（`HoverTooltipHost` の載る層。実物の家族は `GameUiHoverTooltipInstaller` を読む）に表示中の子があることを `p.Assert`。続けて消費アイテムの枠 `research-consume-slot-0` の上でも同じ。`p.Screenshot("02b-tooltip")`
7. 研究の実行: `p.ClickUi($"research-button-{guid}")` → `p.WaitForResearch(new[] { guid }, Array.Empty<Guid>(), 10f, "UI から研究した")` → カードに `research-tree__card--completed`（`FindUi` の状態名。名前の実物は `UiElementSnapshot.StateFlags` を Note に出して合わせる）、ボタンの文言が完了済み。`p.Screenshot("03-completed")`
8. 視野と選択: 空の背景（ビューポートの右下から内へ 40px）を `MouseMoveTo`・`MouseButtonDown(0)`・`MouseGlideTo(-60, +30 の先, 0.4f)`・`MouseButtonUp(0)` でパンし、カードの中心の位置を控える → `p.PressKey(Key.Escape)` → `GameScreen` → `p.PressKey(Key.R)` → `ResearchTree` → カードの中心が控えた位置から 1px 以内、`FindUi("research-detail-pane")` が見えない（選択が消えた）。`p.Screenshot("04-reopen-kept")`
9. 右短押しと Tab: 研究画面で空の背景の上を右短押し（`MouseButtonDown(1)`・`MouseButtonUp(1)` を短く）→ 0.5 秒後も `ResearchTree` のまま。`p.PressKey(Key.Tab)` → `p.WaitUiState(UIStateEnum.PlayerInventory, 10f)` → `FindUi("research-tree")` が見えない
ファイルは 200 行未満、`#region Internal` にローカル関数（前提の閉包・空の背景の点・中心の読み）をまとめる。超えるなら流れ 6b・9 を `uitk-research-tree-input.cs` に分け（流れ 2〜3 の準備を写す）、Step 2 で 3 本を回す

- [ ] **Step 2: 全体スイッチが GameUi であることを確かめてから 2 本を回す**

Run:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
SKILL=.agents/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/uitk/uitk-research-tree.cs"
```
2 行目の `GameUi` を流し直してから `"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/tier9/tier9-5-compute-research.cs"`
Expected: 2 本とも `result.json` が `Success: true`、録画 mp4 がある。`uitk-research-tree` の Note に `系=GameUi` とアンカーの件数。初回に "already playing" で落ちたら PlayMode を止めて同じコマンドをもう 1 回だけ流す。失敗したら `result.json` と `uloop get-logs --log-type Error` を読み、原因を `done.md` に書いて直す（直したらテストを回し、このステップの 2 行目から流し直す）。tier9-5 が Web 前提の操作で落ちるなら、シナリオを直さずに止めて `done.md` の「要裁定」に書く（既存シナリオの持ち主は S4-ladder の横断）

- [ ] **Step 3: 証拠を書いてコミット**

`done.md` の「## 証拠」に 2 本の `result.json` のパス・`Success`・録画 mp4 のパス・Note の `系=GameUi` の行を書く。録画とスクショの原本は worktree の gitignore 下に置いたまま。テストで `Saves/` に作ったワールドは `list-work-saves.sh` で探して消す
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-research-tree.cs
git commit -m "test(uitk): 研究ツリーを UITK の録画で通す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 13: 写さないテストの処遇と名前の突き合わせ

**Files:**
- Modify: `docs/uitk/disposition.md`（「## 処遇」の末尾に 6 行）

**Interfaces:**
- Consumes: Task 8・9 の `<web-test>`、Task 12 のシナリオ、`tools/uitk/test-name-parity.mjs`
- Produces: 束 4 のこのレーンの持ち分の未対応 0、書式・整合の誤り 0

- [ ] **Step 1: 処遇の 6 行を足す**

`docs/uitk/disposition.md` の「## 処遇」の表の末尾に（PR 列は Task 15 Step 1 で PR 番号に置き換える）:
```markdown
| 4 | `e2e/tests/research/research.spec.ts` | translate後のグリップ矩形だけに重なる境界buttonをexpectCraftGripが検出する | 捨てる | Playwright の補助関数 expectCraftGrip 自身の検査で、DOM の translate とボックスの境界に依る。UITK に同じ補助が無く、写す対象の挙動が無い | #<PR番号> |
| 4 | `e2e/tests/research/research.spec.ts` | padding box外の境界buttonをexpectCraftGripが重なりなしと判定する | 捨てる | 同上（expectCraftGrip の自己テスト） | #<PR番号> |
| 4 | `e2e/tests/research/research.spec.ts` | 研究パネル展開中も常駐チャレンジHUDとキーヒントが遮蔽されない | 録画 | .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-research-tree.cs（研究画面を開いたまま、判定の間だけ HUD の根を拾う設定にして challenge-hud・key-hints の中心の Pick が各 HUD を返す。層の順序の主張） | #<PR番号> |
| 4 | `e2e/tests/research/tmp-icon-click.spec.ts` | * | 捨てる | tmp の付いた一時の調査テストで、実行計画 §1.4 の完了条件が写す数から除いた | #<PR番号> |
| 4 | `src/features/research/ResearchTreePanel.test.ts` | インベントリtopic未受信中は詳細ペインへ所持数をnullで渡す(D4) | 捨てる | UITK は持ち物を同期で持ち、所持数の未受信の状態が無い（設計書 [A-13] の ConnectingPlaceholder と同じ理由） | #<PR番号> |
| 4 | `src/features/research/ResearchDetailPane.test.ts` | 所持数未受信中(owned=null)は不足表示も所持/必要も出さない | 捨てる | 同上（[A-13]。所持数は常に有る） | #<PR番号> |
```
（束の番号は `node tools/uitk/test-name-parity.mjs --bundle 4 --emit-missing | grep research` に research の行が出ることで確かめる。出なければ `tools/uitk/testNameParity/` の束の定義を読み、研究の束の番号で書き直す。`tmp-icon-click.spec.ts` の `*` は前例の 1 行目と同じファイル全体の書き方）

- [ ] **Step 2: 名前を突き合わせる**

Run:
```bash
node tools/uitk/test-name-parity.mjs --bundle 4 --emit-missing | grep -E "features/research|tests/research" || true
node tools/uitk/test-name-parity.mjs --bundle 4; echo "exit=$?"
```
Expected: 1 行目にこのレーンの持ち分（File Structure の「写すテスト」の表の 33 件）の行が出ない（`researchLogic.test.ts`・`researchAllocation.test.ts` は L0-s4b、`shared/treeView/**` は C4-d の持ち物）。2 行目の終了コードは 0 か 1（他レーンの未対応・写す予定が残る）。2（書式・整合の誤り）なら完了にしない: `<web-test>` の綴りか、タグと処遇の行の重複を直して流し直す

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/disposition.md
git commit -m "docs(uitk): 研究ツリーの写さないテスト 6 件の処遇を足す（S4-research）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 14: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイルだけ

**Interfaces:**
- Consumes: Task 1〜13 のコミット一式（`origin/feature/uitk..HEAD`）
- Produces: レビュー記録 `../moorestech_logs/harness/` 配下（`$RUNDIR`）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。レビューが裁定・設計書と食い違う直し（Web と違う見た目へ寄せる・画面 USS に部品のクラスを書く・色を C# に書く・Foundation や部品〈`NodeGraphView`・`RecipeActionButton`〉を書き換える・機械レシピの読み口を S4-alpha の解放済みの源へ替える〈未解放のレシピを引けない。D-13〉・左余白の畳みを画面に持たせる〈D-34〉）を提案したら採らず、`done.md` の「要裁定」に書く

- [ ] **Step 2: 自動適用があれば検査と録画を回し直してコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`
Expected: `ErrorCount: 0`、全件 PASS。自動適用が `ResearchTreeModel`・`ResearchSelectionStore`・`ResearchTreeCommands`・`ResearchTreeView`・Parts・Presenter・登録・シナリオに触れたら、反映後のバイナリで Task 12 Step 2 の 2 本を再走する（ユニットテストの通過は代替にならない）。USS かトークンに触れたら Task 11 Step 3 の該当の組を撮り直す。続けて、触ったファイルを名指しで:
```bash
git add <自動適用が触ったファイル>
git commit -m "fix(uitk): S4-research の moores-code-review 反映

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
（自動適用が無ければコミットしない）

---

### Task 15: feature/uitk への PR・取り込み・片付け（必須）

**Files:**
- Modify: `docs/uitk/disposition.md`（処遇の 6 行の PR 列を PR 番号に）

**Interfaces:**
- Consumes: Task 14 後の HEAD
- Produces: `origin/feature/uitk` のマージコミット。以後 S4-ladder（研究画面の横断）・S5-challenge（`NodeGraphView` の 2 機能目）が使える

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`）。PR ができたら `docs/uitk/disposition.md` の `#<PR番号>` を番号に置き換えてコミットし push する。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ。報告ページは `~/hermes-agent/data/services/pr-review/publish/README.md` の手順で、manifest は次の形（`file` は絶対パス。並べ比べは Web を先に同じ `group` へ置く）:
```json
{
  "head": "<HEAD 10 桁>",
  "summary": [
    "研究ツリー（NodeGraphView を常駐で 1 つ・カードの 4 状態・選択の輪・アンカー research.node-<guid>）を UITK に移し、開き直しても視野が残るようにした",
    "詳細ペイン（消費アイテムの所持/必要と不足・解放物 5 節・研究ボタンは RecipeActionButton を研究の組に上書き）と、研究の失敗の良性でないトースト（ResearchFailed）を足した",
    "研究エリアは OverlayPanel 層で持ち物の幅と列の間隔の右から舞台の端まで。ロックの減光と研究ボタンの高さはランタイムの画素で Web に合わせた"
  ],
  "media": [
    {"file": "<uitk-research-tree の recording.mp4>", "caption": "uitk/uitk-research-tree（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<tier9-5-compute-research の recording.mp4>", "caption": "tier9/tier9-5-compute-research（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<web-a9d5123537/4-research.png>", "caption": "Web 4-research 1280×720", "group": "4-research"},
    {"file": "<compare/S4-research/uitk-4-research-1280x720.png>", "caption": "UITK 4-research 1280×720", "group": "4-research"},
    {"file": "<web-0e81bbe3c3-2560x1080/4-research.png>", "caption": "Web 4-research 2560×1080", "group": "4-research"},
    {"file": "<compare/S4-research/uitk-4-research-2560x1080.png>", "caption": "UITK 4-research 2560×1080", "group": "4-research"},
    {"file": "<web-a9d5123537/5-tutorial-research-node.png>", "caption": "Web 5-tutorial-research-node 1280×720", "group": "5-tutorial-research-node"},
    {"file": "<compare/S4-research/uitk-5-tutorial-research-node-1280x720.png>", "caption": "UITK 5-tutorial-research-node 1280×720", "group": "5-tutorial-research-node"},
    {"file": "<web-…-research-detail/4-research-detail-ready.png>", "caption": "Web 詳細（研究可能）1280×720", "group": "詳細ペイン"},
    {"file": "<compare/S4-research/uitk-4-research-detail-ready-1280x720.png>", "caption": "UITK 詳細（研究可能）1280×720", "group": "詳細ペイン"},
    {"file": "<web-…-research-detail/4-research-detail-lacking.png>", "caption": "Web 詳細（素材不足）1280×720", "group": "詳細ペイン"},
    {"file": "<compare/S4-research/uitk-4-research-detail-lacking-1280x720.png>", "caption": "UITK 詳細（素材不足）1280×720", "group": "詳細ペイン"},
    {"file": "<web-…-research-detail/4-research-detail-completed.png>", "caption": "Web 詳細（完了）1280×720", "group": "詳細ペイン"},
    {"file": "<compare/S4-research/uitk-4-research-detail-completed-1280x720.png>", "caption": "UITK 詳細（完了）1280×720", "group": "詳細ペイン"}
  ],
  "verification": [
    "Unityコンパイル: エラー0",
    "L0/L1: State.Research・Screens.Research の件数と PASS、L2（Conventions・Theme・Layers・Anchor・Routing）PASS",
    "test-name-parity 束 4: このレーンの持ち分の未対応 0・書式・整合の誤りなし（処遇 6 行）",
    "ロックの減光: Web 0.45 → UITK <値>（最大差 <n> 段）・研究ボタンの高さ <h>px",
    "unityプレイ録画テスト: uitk-research-tree PASS・tier9-5-compute-research PASS（全体スイッチ GameUi）"
  ],
  "notes": ["差の表は moorestech_logs/harness/uitk/S4-research/done.md。2560×1080 の組も同じディレクトリにある"]
}
```
（2560×1080 の 5-tutorial と詳細の組も同じ形で `media` に足す。`python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <PR番号> <manifest.json>` が失敗したら、報告 URL 無しで完了にしない）

- [ ] **Step 2: feature/uitk の進みを取り込み、G1 緑を見る**

Run:
```bash
git fetch origin
git merge --no-ff --no-edit origin/feature/uitk
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"
git push
gh pr checks <PR番号>
```
Expected: 進んでいなければ `Already up to date.`。共有区画（`GameUiTheme.tss` の `Screens` 区画・`MainGameUiRegistration.cs` の画面登録区画・`DerivedTokens.uss` の名札付きブロック・`UiCommandRejectReason.cs` の enum と 3 つの switch の末尾・`localization.csv` の `ui.commandRejected.*` の群・`docs/uitk/TODO.md` の節の末尾・`docs/uitk/disposition.md` の処遇の表・`Client.Ui.State.asmdef` の `references`）の衝突は両方の行を採る（`localizationKeys.ts` と `_CompileRequester.cs` は採った後に `npm run gen:i18n` で作り直す）。採ったら compile とテストを回し、衝突が `UiLayer`・`GameUiRoot`・`NodeGraphView`・`RecipeActionButton`・経路に触れていれば Task 12 の 2 本を再走する。`ErrorCount: 0`・PASS。`gh pr checks` で `UITK Web follow gate (G1)` と `UITK Web follow gate self-test` が `pass`（終わっていなければ前景で `gh pr checks <PR番号> --watch`）、G1 の出力で未記帳 0。CI の `Unity Test - client-remainder` の `UnityCursorTooltipScreenTest` 1 件の失敗は既存（CI だけ）で、マージの条件は G1 緑

- [ ] **Step 3: マージコミットで取り込む**

Run: `gh pr merge <PR番号> --merge`
Expected: マージされ、`git fetch origin && git log -1 --format='%H %P' origin/feature/uitk` の親が 2 つ（Squash しない）

- [ ] **Step 4: bd を閉じ、報告を締め、worktree を畳む**

Run:
```bash
bd close <bd> --reason "PR #<番号> を feature/uitk へマージ（研究ツリー・ノードカード・詳細ペイン・解放物・研究ボタン）"
git status --short
git log origin/uitk-lane/s4-research..HEAD
```
Expected: closed、`git status --short` と `git log` が空。`done.md` の 1 行目を「状態: 完了」にし、「## 証拠」に PR 番号・マージコミット・G1 のジョブ URL・各テストと録画の結果・報告ページの URL を足して、`../moorestech_logs` の `README.md` の手順でコミット・push する。続けて、テストで `Saves/` に作ったワールドが残っていないことを確かめてから:
```bash
cd <メインのクローンの絶対パス>
moores-wt rm uitk-lane/s4-research --prune-branch
moores-wt status
```
Expected: worktree と Editor が畳まれ、`moores-wt status` にこのレーンの行が無い（未コミット・未 push で拒否されたら、`git status` と `git log @{u}..` を読んで残りを片付けてからもう一度。`--force` は使わない）

---
## Self-Review

1. **Requirements coverage**: R-1 → Task 2（`ResearchCatalogFromMasterTest`）、R-2 → Task 3（`ResearchTreeViewModelFactoryTest`）、R-3 → Task 4・Task 12（流れ 8 の開き直し）、R-4 → Task 3・4・8・9（`ResearchSelectionTest`・`ResearchTreeViewTest`・閉じるの `ResearchDetailPaneTest`・録画の流れ 8）、R-5 → Task 7・9（`ResearchTreeViewTest` のエリアの寸法）、R-6 → Task 6・7・9（4 状態の `[TestCase]`・状態ラベル・減光は Task 11）、R-7 → Task 9（`ResearchTreeViewportTest` 5 本）、R-8 → Task 7・8（`ResearchDetailPaneTest`）、R-9 → Task 3・8（消費の 4 本）、R-10 → Task 8（`ResearchUnlockSectionsTest` 5 本）、R-11 → Task 5・8・11・12、R-12 → Task 11、R-13 → Task 12、R-14 → Task 7・10、R-15 → Task 8・9・13、R-16 → Task 14・15。完了条件の各句: ResearchTreePanel（`NodeGraphView`。Task 9）・ノードカード 4 状態（Task 7・9）・詳細ペイン（Task 8）・解放物（Task 2・8）・研究ボタンの無効の面 [A-46]（Task 7 の USS・C4-c の `--research-action-disabled-face`）・左余白 0（C4-e 済み。Task 9 の `ResearchStageLayoutTest`）・アンカー 7 件（Task 7・12）・写す 33 件（File Structure の表・Task 8・9・13）・録画 tier9-5（Task 12）・右の領域の `left` は画面 USS（Task 7・9）。§6.2 の必須: 部品と公開 API（Global Constraints）・画面専用の部品の置き場（`Screens/Research/Parts/`）・つまみの同名上書き（File Structure の表）・写すテスト名と件数と `<web-test>`・撮影の解像度（Task 11）・録画（Task 12）・登録の行数 1（Task 10）・新しい共有部品の申告の手順（Global Constraints）。漏れなし
2. **Placeholder scan**: `<bd>`・`<番号>`・`<PR番号>`・`<HEAD 10 桁>`・`<sha>`・`<空きポート>`・`<値>`・`<n>`・`<h>`・manifest の `<… のパス>`・`<自動適用が触ったファイル>` は実行時にしか決まらず、取り方を同じタスクに書いた。「TBD」「適切に」は無い。C4-d・C4-c の型は計画の Produces からの想定で、Task 1 Step 1 で実物と照合し、違えば読み替えて記録する手順を書いた
3. **Type consistency**: `ResearchNodeDefinition(Guid, ItemId, Vector2, IReadOnlyList<Guid>, IReadOnlyList<ResearchConsumeItem>, ResearchUnlockSet)`・`ResearchNodeCatalog.Nodes`・`ResearchNodeDefinitionsFromMaster.Build()`・`ResearchSelection.None/Of/Is/TryGetGuid/Toggled`・`ResearchNodeCardViewModel`（`Guid`・`IconItemId`・`TreePosition`・`PreviousGuids`・`CardState`・`StateLabelKey`・`Selected`）・`ResearchConsumeSlotViewModel(ItemId, int, int, bool)`・`ResearchDetailViewModel`（`Guid`・`Button`・`ConsumeSlots`・`Unlocks`）・`ResearchTreeViewModel.Create/CreateWithDetail/TryGetInitialFocus/TryGetDetail/Nodes/Empty`・`ResearchFocusPoint.None/At/TryGetPoint`・`ResearchTreeViewModelFactory.Create(definitions, states, owned, selection)`・`ResearchSelectionStore.Current/Toggle/Clear`・`ResearchTreeModel.Current`・`IResearchTreeCommands.ToggleNodeSelection/CloseDetail/CompleteResearch`・`ResearchTreeCommands.CompleteResearchCommandName`・`UiCommandRejectReason.ResearchFailed`・`ResearchCalcKnobs.ApplyAreaLeft/ApplyScrollBleed`・`ResearchNodeCard(Guid).Render(vm, content)`・`ResearchNodeCardSet.RenderCards/BuildContent`・`ResearchDetailPane(commands, content, toaster).Render/Hide`・`ResearchDetailActions.Render(Guid, ResearchButtonState)`・`ResearchConsumeItemsSection.Render`・`ResearchUnlockSections.Render(IReadOnlyList<UnlockEntry>)`・`ResearchUnlockSectionKinds.Of/LabelKey/Testid/WrapsInSlotsRow`・`ResearchUnlockEntryElements.AddTo`・`ResearchTreeView(commands, content, toaster).Render/SetShown`・`ResearchTreePresenter(GameUiRoot, ResearchTreeModel, UiRoutingState, IResearchTreeCommands, GameContentPresentation, UiCommandResultToaster)`・`ResearchScreenTestKit.Tree/Detail/NoUnlocks/Unlocks/MasterUnlocks/LoadMasterAndContent`・`RecordingResearchTreeCommands.SetCompleteResult` は定義と使用（テスト・View・Presenter・登録・録画）で同じ綴り。USS のクラス `research-tree`・`__panel`・`__graph`・`__card`（`--completed/--ready/--locked/--selected`）・`__card-name/-icon/-state/-ring`・`__detail-host`・`__detail`・`__detail-panel`・`__detail-body`・`__detail-header`・`__detail-name`・`__detail-close`・`__detail-close-symbol`・`__detail-scroll`・`__detail-scroll-content`・`__detail-description`・`__section`・`__section-label`・`__slots`・`__slot`・`__unlock-other`・`__unlock-fluid`・`__actions`・`__reason` は C#・USS・テストで一致。testid（`research-tree`・`research-panel`・`research-viewport`・`research-node-<guid>`・`research-node-state-<guid>`・`research-detail-pane`・`research-detail-close`・`research-detail-scroll`・`research-consume-items`・`research-consume-slot-<i>`・`research-unlock-blocks/-machine-recipes/-items/-others`・`research-reward-items`・`research-unlock-fluid`・`research-unlock-module-slot-stack-limit`・`research-unlock-craft-cap-<itemId>`・`research-button-<guid>`・`research-detail-reason`）は Web の `data-testid` と同名
4. **保留・縮退経路**: (a) 研究の応答なし → `ResearchTreeCommands` が LogWarning と `Rejected(ResearchFailed)`、トースト口が良性でないトーストを出す（`ResearchDetailPaneTest` の失敗のトーストが固定）。(b) 応答ありで `Success` が偽 → 状態は入れたうえで同じ拒否。(c) 研究画面の外の通知 → Model は組み直すが View は `display: none` で描かない（表示の有無は Presenter が経路で持つ）。(d) 機械レシピの GUID がマスタに無い → この計画では分岐を持たない。`ResearchMasterUtil` が参照切れをエラーに積み `MasterHolder.Load` が `Validate` 失敗で例外を出すので起動時に止まる（AGENTS.md「MasterHolder には null チェック不要」、Web の `ResearchNodeDtoFactory` も同じ）。(e) つまみが読めない → `UssCustomStyleReader` が LogError（`ResearchCalcKnobs`）。(f) 解放物の種類の追加 → マスタの写しは出さない 7 種以外の `GameActionType` で例外（`ResearchUnlockSetFromMaster`。起動時の定義の組み立てで止まる）、`ResearchUnlockSectionKinds.Of`・`ResearchUnlockEntryElements.AddTo` も未知の型で例外（.decisions 2026-08-19「研究の未対応GameActionTypeは例外で落とす」と同じ）。(g) Task 1・10・11・12 の「止める」経路は、解消の主体（指揮・integrator・S4-alpha）と記録先（`done.md` の「integrator への依頼」「要裁定」「未確認」）を書いた
5. **「決定的にする」を根拠にした選択**: 初期の注目点は L0-s4b の `TryFindInitialFocusNode`（Web と同じ規則）をそのまま使い、選び方を足さない。解放物の節の並びは初出順（Web と同じ）。ほかに無し
6. **呼び出し側への責務漏出**: 状態→カードの 4 状態・ラベル・ボタン・不足の判定は L0-s4b の `ResearchLogic` の 1 か所を Factory が読み、View は ViewModel の値を当てるだけ。解放物の種類→節の写像は `ResearchUnlockSectionKinds` の 1 か所（節の見出し・testid・枠の列の有無を同じ switch 群で持つ）。研究の実行と状態の反映と失敗の写しは `ResearchTreeCommands` の 1 か所で、View は結果をトースト口へ渡すだけ。選択の切り替えと離脱で消すのは `ResearchSelectionStore` の 1 か所

---

## 判断記録（ADR）

設計の正本: 設計書 `docs/uitk-migration/execution/component-architecture.md`（[A-8]・[A-13]・[A-45]・[A-46]・[A-47]・[A-52]・[A-57]・[A-92]）、実行計画 §1.4・§4.3・§6.2・E-1・E-13・E-22、裁定 `.decisions/2026-11-06-*`、`.decisions/2026-08-19-研究の未対応GameActionTypeは例外で落とす.md`・`2026-08-26-ツリーのノード押下もパンにし閾値未満だけ選択とする.md`・`2026-08-30-研究ノード状態ラベル写像は網羅Recordで持つ.md`。この計画で新たに決めたもの（agent 既定。出所はすべて起草役の判断。ユーザーへの質問は user-simulator の予測で代えた）:

- **D-1 State の 3 区分**: `Research/Catalog`（マスタから 1 回だけ組む静的な定義）・`Research/Tree`（選択・ViewModel・Model）・`Research/Commands`。マスタはゲーム中に変わらないので、定義は DI のシングルトンが ctor で組む。棄却: Model が毎回マスタを読む（同じ写しを状態の通知のたびに繰り返す）
- **D-2 Model は源の変化のたびに同期で組み直し、表示の有無で止めない**（前例 `CurrentChallengeHudModel`・`GrabOverlayModel` [A-57]。設計書 §4.2 は表示の判断を Presenter と View に置く）。所持はメインの枠を既存の読み口 `IPlayerInventoryOperations.ReadSnapshot()` から読む（同じ数え方を 2 か所に書かない）。設計検査 Phase 1〜2.5 の指摘で、表示中だけ組む案（研究画面の判定が 4 か所に散る）から直した
- **D-3 `NodeGraphView` は常駐 1 つ**で、`SetGraph` はノードの並びか注目点が変わったときだけ呼ぶ。隠すのは `display: none` で要素を残す（C4-d D-23(3) の「祖先が display: none の間は視野を動かさない」に乗る）。`RestoreViewport` は使わない。棄却: 開くたびに View を作り直して視野を外に保存する（Presenter が視野の値を持つことになり、C4-d の口を使うだけの利点が無い）
- **D-4 ViewModel は等値比較を持たない**。Model は変化ごとに新しい ViewModel を配り、View はカードを GUID で使い回して描き直す（Web の再描画と同じ粒度）。解放物の節は数件なので描くたびに組み直す
- **D-5 選択の持ち主は State の `ResearchSelectionStore`** で、経路が研究画面を離れたら `None` へ戻す（Web はパネルのアンマウントで `useState` が消える）。棄却: View が選択を持つ（Model が詳細を組めない）
- **D-6 選択の輪はカードの拾わない子の枠**で写す（Web の `outline: 2px`。UITK に outline が無い）。チャレンジのカードと似るが 1 機能目なので `Parts/` に置き、2 機能目が来たら integrator が上げる
- **D-7 研究エリアは `OverlayPanel`/`Stage` 層**（Web の `--z-stage-overlay-panel`）に Presenter が 1 回載せ、`left` は画面 USS で受け直したトークンの和を `ResearchCalcKnobs` が当てる（[A-47]・[A-92]。列には入れない）
- **D-8 カードの文字は拾わず、アイコンは拾う**: 押下は祖先のカードの包みへ浮かび、C4-d が対象の祖先から包みを探してタップ・パンにする。アイコンは Web の `ItemSlot` と同じくホバーで名前を出すために拾う。`ResearchTreeViewTest` が Pick を、`ResearchTreeViewportTest` のタップがアイコンの上（カードの中心）の押下を固定する
- **D-9 `NodeGraphView` の切り口の印（`TutorialAnchor.MarkClipContainer`）は作らない**: 部品の内側の要素で、研究とチャレンジの 2 機能が使うので integrator へ申告する（`done.md` の「integrator への依頼」）。それまでチュートリアルの強調の枠が切り口で欠けることがありうる（描き手は S2-tutorial）
- **D-10 閉じるボタンは素の要素とクリック**で作る（Unity の `Button` の既定の見た目と押下の色を持ち込まない。Web は素の `button` に `border: 0; background: transparent`）
- **D-11 詳細ペインは最初の詳細で載せ、以後は `display` で出し分ける**。キーの無い `LocalizedLabel` を載せると LogError になるため（`GamePanel.SetTitle` のコメントと同じ理由）。View と Presenter も描いてから載せる
- **D-12 `Client.Ui.State.asmdef` に `Core.Item` を足す**: 消費アイテムの上位レベルの受け付けは `LevelFamilySubstituteUtil`（`Core.Item`）が正本で、Web の `ResearchNodeDtoFactory` も同じ関数を使う。層の規則（`UiLayerDependencyRules`）は State から Core を許すので L2 は通る
- **D-13 解放物の機械レシピの出力は `MasterHolder.MachineRecipesMaster` から読む**（Web の橋 `ResearchNodeDtoFactory.BuildUnlockMachineRecipeDto` と同じ）。指揮の指示（2026-11-10）は S4-alpha の中立の源から引くことだったが、S4-alpha 計画 14a85b146c（レビュー中で変わりうる。マージ後に実物と照合）の源 `UnlockedMachineRecipeSource.Current` は解放済みの機械レシピだけを配る。研究の詳細ペインが出すのは「この研究で解放される＝まだ解放されていない」レシピなので、その源からは引けず、引けば解放物の機械レシピの節が常に空になる（Web と違う）。研究マスタと同じく定義を組むときに 1 回読むだけで、新しい源の型は作らない。S4-alpha への依存は足さない。棄却: S4-alpha の源を使う（上記で節が空になる）・S4-alpha に全件の源を頼む（読むのは出力の 2 列だけで、型を挟む利点が無い）
- **D-14 研究の失敗は新しい拒否理由 `ResearchFailed`（良性でない）**: 応答なし・サーバーの拒否を同じ 1 つで写す（Web は `research.complete` の失敗をトーストで出す）。棄却: 既存の汎用の理由を使う（トーストの文言が研究と分からない）
- **D-15 写す C の件数は 15**（実行計画の「C14」は `ResearchNodeCard.test.ts` を 1 と数えた値で、実物は `it` 1 と `it.each` 1）。持ち分は P 18＋C 15＝33。実行計画 §1.4 の改め案に書く
- **D-16 所持数の未受信の 2 件は捨てる**（[A-13]。UITK は持ち物を同期で持つ。前例 disposition の A-13 の行）
- **D-17 視野のホイールの検査は 1280×720 で写す**（Web は 960×540 へ縮めて撮る）。拡縮の中心とパンの量は画面の寸法に依らない。慣性の上限は C4-d の L1 の持ち物で、このレーンは「ドラッグ量以上に動く」だけを見る
- **D-18 HUD の遮蔽の 2 件は、採掘の進捗バーを L1、チャレンジ HUD とキーヒントを録画で写す**: 進捗バーは本物の `ProgressHudView` を本番と同じ `Chrome`・`Viewport` 層に置き、研究エリア（`OverlayPanel`・`Stage`）と重なる点で Pick する L1 を `ResearchTreeViewTest` に置く（opus の案 (b)）。Web の元テストは `.viewportOverlay` の z の封じ込めの回帰ガードで、守る物は層の順序。`UiLayerStack` が enum 順に足すので L1 で決定的に固定でき、ctor に引数の無い本物の View を使える。棄却 (a) 録画で採掘を始める: 採掘できるブロックと道具の準備が録画に増え、採掘が終わると進捗バーが消えるので Pick の時刻に依る。HUD は部分木ごと拾わないので、L1・録画とも Web の `expectHitTestWithin` と同じく判定の間だけ根の `pickingMode` を `Position` にして戻す（本番の入力の透過は変えない）。チャレンジ HUD とキーヒントは Presenter と状態の源（現在のチャレンジ・キーのトークン）が要るので録画で見る。1280×720 では両 HUD とも研究エリアの外にあり、録画の主張は「上の層に覆われていない」まで（重なる配置での層の順序は進捗バーの L1 が持つ）。独立評価 2 体の指摘
- **D-19 `ResearchStageLayoutTest` は研究画面の性質の値を固定するだけ**: 左の余白の畳み（C4-e）と常駐 HUD の非表示（S3-hotbar・S4-inventory）は持ち主のテストがあり、ここは Web のテスト名の写し先として研究画面の値と結果の寸法を見る
- **D-20 ロックの減光は opacity のトークンで持ち、Task 11 の画素で決める**（初期 0.45。前例 `--slot-frame-insufficient-opacity`）。暗い半透明の色の式（指数 1.85）は要素の opacity に当てはまらない
- **D-21 研究ボタンの幅は本文の幅に従わせ（`--recipe-action-width: auto`）、高さ 36 は初期値**で Task 11 Step 3 で Web の画素から決め直す（C4-c は研究の組の幅と高さをこのレーンに残した）。Web の `.researchButton` は幅の指定が無く、`.detailBody` の列で伸びて 300 − 枠 2 − `GamePanel(craft)` の padding 25 − 本文 20 ＝ 253（`W/src/shared/ui/GamePanel/style.module.css` の `.craft`・UITK の `GamePanel.uss` の `.game-panel--craft` が同じ値）。固定 px にすると本文やペインの幅の変更に追従しないので幅は持たない。C4-c の USS は `width`・`min-width` を `var(--recipe-action-width)` で読むので、`auto` が `var()` 越しに効けば部品は変えずに済む。効かなければ `100%`、それも効かなければ `253px` を置き、C4-c の部品が受けられるようにする依頼を integrator へ出す。棄却: 280px（本文より 27px 広く、ペインの右の余白と枠を越える）
- **D-22 Web の `gap` は子の margin で写す**（USS の規約）。カードは名前とアイコンの下 6px、詳細の本文は見出しとスクロールの下 8px・理由の上 8px、節は上 8px、枠は右と下 4px
- **D-23 録画を持つ**: ランタイムの挙動（入力・UI）に触れるので、unityプレイ録画テスト 2 本（`uitk/uitk-research-tree` 新規・`tier9/tier9-5-compute-research` 既存）を Task 12 に置く。L3（EditModeInPlayingTest）は持たない（段の締め S4-ladder）
- **D-24 設計検査 Phase 2.6 の裁定（user-simulator の予測で代えた。出所: 起草役の既定）**:
  - 5-A（private ctor の null・`ItemSlotContent` の null）→ このままでよい。`ResearchTreeViewModel` の null は private ctor の内側に閉じ、公開面は `TryGetDetail` と生成の別（`Create`／`CreateWithDetail`）で表す。`ItemSlotContent` の null は C3-a の部品の API の「無い」の書き方で、このレーンは部品を変えない
  - 5-D（`ResearchSelectionStore` の読み書きの未分離）→ このままでよい。書き手は `ResearchTreeCommands` 1 つ、読み手は Model 1 つで、画面の内の UI 状態（前例 `HotbarModel` 周りも interface を分けない）。2 人目の書き手が出たら分ける
  - 5-I（名前の解決の欠損）→ このままでよい。`GameContentPresentation`・`UiText.Resolve` はマスタ由来の ID だけを受け、辞書の欠けは補間器が LogError で知らせる（[A-114]）
  - 検査7（解放物の写しの `default` と ADR 2026-08-19）→ ADR に合わせて直した。出さない 7 種を明示し、それ以外の `GameActionType` は例外（Task 2）。Web の橋は素通し＋網羅テストへ変わったが、その網羅テストは Web の橋の撤去で消えるので、UITK 側は ADR の形で閉じる
  - 5-F（`ApplyNodeStates` の書き手が 2 つ）→ このままでよい。Web の橋 `ResearchCompleteActionHandler` と同じ書き手で、研究画面へ入った直後の取り直しの古い応答が研究の完了より後に届く競合は Web と同じく残る（次の取り直し〈持ち物の変化・入り直し〉で直る）。直すなら `ResearchNodeStateStore` の持ち主の改修で、このレーンの外
  - 検査6（研究画面の表示中の判定 3 か所・GUID とキーの変換）→ このままでよい。判定は経路の値の比較 1 式で、持ち主（選択の破棄・組み直しの抑止・表示）が別。GUID の文字列は C4-d のキーの形
- **D-25 研究の実行の手続きは Web の橋と 2 か所に残す**: 前例（`PauseMenuCommands` を両系で登録し、Web の橋が委ねる）に合わせるなら `ResearchCompleteActionHandler` を `ResearchTreeCommands` へ委ねる形に変えるが、Web の橋（`Client.WebUiHost/**`）を変えない範囲を守る（橋の DTO の突き合わせは Task 2 の L0 が縛る）。帰結: CEF の撤去まで同じ手続きが 2 か所に残る（負債が残る）。本PR外のリファクタ提案に載せた。設計検査 Phase 1〜2.5 の指摘
- **D-27 閉じて同じノードを開き直すと説明のスクロールを頭へ戻す**: Web は閉じるとペインを破棄し（`ResearchTreePanel.tsx` の条件付き描画）、`ScrollArea` は `key={guid}`。UITK はペインを使い回すので、`ResearchDetailPane.Hide` が次の `Render` で頭へ戻す印を立てる。表示中の同じノードの描き直し（所持数の変化）では保つ。`ResearchDetailPaneTest` で固定。独立評価（codex）の指摘
- **D-26 Web の橋の DTO と定義を L0 で突き合わせる**: `ResearchCatalogFromMasterTest` の 5 本目が、テスト mod の全ノードで `ResearchNodeDtoFactory.Create` の結果とアイコン・前提・消費（受け付ける上位レベル）・解放物 8 種を比べる（前例 [A-102]）。設計検査 Phase 1〜2.5 の指摘
- **要裁定**: なし（ゴール・裁定から外れる判断は無い。見た目の差が残れば `LX-136`〜`LX-140` に記帳し、§3 の例外として扱う）

## 未確認

- C4-d・C4-c の公開 API は起草時点で未マージの計画の Produces から写した（Task 1 Step 1 で照合）。S4-alpha は使わない（D-13）
- `GamePanel` の本文（`flex-basis: 0%`）が高さ auto の詳細ペインで潰れないか（Task 8 Step 5 で見る。潰れたら `.research-tree__detail-panel` に `flex-basis: auto`）
- `NodeGraphView` の慣性が `simulate.FrameUpdate` で進むか（Task 9 の視野の L1。進まなければ C4-d の L1 の止め方へ合わせる）
- 研究の失敗のトーストの文言（`ui.commandRejected.researchFailed`）は Web の失敗のトーストの文言と同じか（Task 5 で Web の `research.complete` の失敗の扱いを読んで合わせる）

## 本PR外のリファクタ提案

- 既存側 `moorestech_client/Assets/Scripts/Client.WebUiHost/**/ResearchNodeDtoFactory.cs` ／新設側 `Client.Ui.State/Research/Catalog/ResearchNodeDefinitionsFromMaster.cs`・`ResearchUnlockSetFromMaster.cs` ／同じ研究マスタから座標・前提・消費（上位レベル込み）・解放物 8 種を写す同じ役割。Web の撤去（CEF 停止）で前者が消えるまで 2 か所になる

- 既存側 `S/Client.WebUiHost/Game/Actions/ResearchActions.cs`（`ResearchCompleteActionHandler`）／新設側 `Client.Ui.State/Research/Commands/ResearchTreeCommands.cs` ／同じ API を呼び `ApplyNodeStates` で反映し、応答なしと拒否を同じ失敗にする同じ役割
- 既存側 `S/Client.WebUiHost/**/ResearchUnlockGameActionCoverageTest`（表示 8 種＋非表示 7 種＝全 `GameActionTypeConst` の網羅テスト）／新設側 `ResearchUnlockSetFromMaster.HiddenGameActionTypes` ／同じ「解放物の種別の台帳」。Web の橋の撤去でテストが消えると、新種別の追加は research.json がそれを使って起動したときまで気づけない（負債が残る）。橋の撤去レーンへ「UITK 側に同形の網羅テストを移す」を申し送る（本 PR では種別の写しを public にしないため置かない）
- 既存側 `S/Client.Ui.State/Research/ResearchNodeStateStore.cs` の `_researchScreenActive`（`UIStateControl` から）／新設側 `ResearchSelectionStore`・`ResearchTreeModel`・`ResearchTreePresenter` の `UiScreen.ResearchTree` 判定（`UiRoutingState` から）／同じ「研究画面の表示中か」を別の源から導いている

---

## 設計書・実行計画へ反映する行

### 設計書 §7.1 に足す行（A-170〜A-174）

| ID | 判断 | 棄却した案と理由 |
|---|---|---|
| A-170 | 研究ツリーの State は `Research/Catalog`（研究マスタから 1 回だけ組む静的な定義 `ResearchNodeCatalog`。機械レシピの出力も機械レシピのマスタから読む。解放済みだけを配る S4-alpha の源は使わない）・`Research/Tree`（`ResearchSelectionStore`・`ResearchTreeViewModelFactory`・`ResearchTreeModel`）・`Research/Commands` に分け、Model は源の変化のたびに同期で組み直し、表示の有無は Presenter と View が持つ（S4-research） | Model が毎回マスタを読む: 同じ写しを状態の通知のたびに繰り返す。表示中だけ組む: 研究画面の判定が Model・選択・Presenter に散り、前例（`CurrentChallengeHudModel`）とも違う |
| A-171 | 研究エリアは `UiLayer.OverlayPanel`・`UiFamily.Stage` に Presenter が 1 回載せ、`left` は画面 USS で受け直した `--inventory-panel-width` と `--stage-column-gap` の和を C# が当てる（[A-47]・[A-92]）。表示は経路で `display` を切り替え、要素は残す（S4-research） | 舞台の列に入れる: Web も列でなく絶対配置で、持ち物の右から画面端までを占める。開くたびに作り直す: `NodeGraphView` の視野を外に保存する口が要る |
| A-172 | 研究ツリーは常駐の `NodeGraphView` 1 つにカードを GUID で使い回して渡し、`SetGraph` はノードの並びか初期の注目点が変わったときだけ呼ぶ（C4-d D-23(3) に乗り、開き直しても視野が残る）。カードの文字は拾わず、アイコンはホバーの名前のために拾い、押下はカードの包みへ浮かぶ（S4-research） | 毎回 `SetGraph`: 組み直しのたびに包みが作り直され、カードの状態だけの変化で無駄に組む。カードの子をすべて拾わない: Web の `ItemSlot` のホバーの名前が出ない |
| A-173 | 研究ノードのロックの減光は要素の opacity のトークン `--research-node-locked-opacity`（`DerivedTokens.uss` の「S4-research」）で持ち、値はランタイムの PanelSettings の画素で Web と比べて決める（前例 `--slot-frame-insufficient-opacity`）。研究ボタンは `RecipeActionButton(Regular)` を画面 USS の `.research-tree__detail-body` で研究の組に上書きし、幅と高さはこのレーンが撮って決める（[A-46]）（S4-research） | Web の 0.45 のまま: Linear 合成で明るい字と枠が淡く残る。暗い半透明の式（指数 1.85）: 要素の opacity は下の色を混ぜる式が違う |
| A-174 | 研究の実行の失敗（応答なし・サーバーの拒否）は新しい拒否理由 `UiCommandRejectReason.ResearchFailed`（良性でない・wire `research_failed`・文言 `ui.commandRejected.researchFailed`）で `UiCommandResultToaster` の 1 本へ渡す。応答があれば状態は先に `ResearchNodeStateStore.ApplyNodeStates` へ入れる（S4-research） | 既存の汎用の理由: トーストの文言が研究と分からない。View が失敗を直接トーストする: 拒否の口が 2 本になる（[A-8]） |

### 設計書 §2 の部品カタログの差分案

- §2.7（画面だけの部品）の研究の行に足す: `Screens/Research/Parts/` に `ResearchNodeCard`（選択の輪を含む）・`ResearchNodeCardSet`・`ResearchDetailPane`・`ResearchDetailActions`・`ResearchConsumeItemsSection`・`ResearchUnlockSections`・`ResearchUnlockSectionKind`・`ResearchUnlockEntryElements`・`ResearchCalcKnobs`。2 機能目（S5-challenge のカード）が来たら、選択の輪とカードの面の型を上げるかを integrator が決める
- `NodeGraphView` の行に注記: 切り口の印（`TutorialAnchor.MarkClipContainer`）を部品が自分で付ける依頼（研究・チャレンジ。S4-research の integrator への依頼）

### 実行計画 §1.4 の S4-research 行の改め案

| S4-research | 研究ツリー | 4・W4 | C4-d・C3-a・C4-c（`RecipeActionButton`）・C4-e（`StageColumnsLayout`）・L0-s4b | 要 | 単位 | ResearchTreePanel（`NodeGraphView` を常駐 1 つ [A-172]）・ノードカード 4 状態・詳細ペイン・解放物・研究ボタン（`RecipeActionButton` に無効の面を `rgb(50 52 67)` へ上書き [A-46]・[A-173]）。研究画面の左余白 0 は `UiScreenTraits.CollapsesStageLeftPadding`（C4-e D-34）。アンカー `research.node-<guid>`（マスタ 7 件）。写す: research 33（P19 から `tmp-icon-click` を除く 18・C15〈`ResearchNodeCard.test.ts` は `it` 1＋`it.each` 1〉。L22 は L0-s4b 済み。うち写す 28・処遇 5。処遇の表には持ち分外の `tmp-icon-click` を足して 6 行）。録画 `uitk/uitk-research-tree`（新規）・`tier9/tier9-5-compute-research`。研究の右の領域の `left` は画面 USS（列ではない）[A-171]。研究の失敗は `ResearchFailed` [A-174] |

### 指揮・S4-alpha への申し送り

- S4-alpha 計画 14a85b146c は `UnlockedMachineRecipeSource` の消費者に S4-research を数えている（同計画 L326・L4203・D-15）。この計画は使わない（D-13）ので、S4-alpha の計画と実行計画の依存から S4-research を外す
- `origin/feature/uitk` には C4-d（`Components/NodeGraph/`）と C4-c（`RecipeActionButton.cs`）がまだ無い。この 2 つのマージ後にレーンを起動する（先に起動すると Task 1 の照合で止まる）

### 実行計画 §6.1 の割り当て表の行

| S4-research（`P/2026-11-10-uitk-s4-research.md`。PLAN-W4b） | A-170〜A-174 | LX-136〜140（136 ロックの減光・137〜139 並べ比べで差が出たときの候補・140 予備） |


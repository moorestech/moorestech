# UITK S4-research（段階 4 画面: 研究ツリー・ノードカード 4 状態・詳細ペイン・解放物・研究ボタン）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/research`（`ResearchTreePanel`・`ResearchNodeCard`・`ResearchDetailPane`・`unlock/*`）を、C4-d の `NodeGraphView`・C4-c の `RecipeActionButton`・C3-a の `ItemSlot`/`BlockSlot`・C2 の `GamePanel`/`GameScrollView` で Web と同じ見た目の UITK 画面にし、Model は Web の topic の源（`ResearchNodeStateStore`・研究マスタ・持ち物）から直に組み、L0/L1・並べ比べ（720p・2560×1080）・録画 `uitk/uitk-research-tree` と既存 `tier9/tier9-5-compute-research` まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。State（`Client.Ui.State/Research/`）は研究マスタを静的な定義の列 `ResearchNodeCatalog` に 1 回だけ写し（機械レシピの出力は S4-alpha の中立の源から引く）、`ResearchTreeModel` が定義・サーバーの状態（`ResearchNodeStateStore`）・持ち物の所持数・選択（`ResearchSelectionStore`）を購読して、研究画面の表示中だけ同期で `ResearchTreeViewModel` を組み直す（[A-57]）。View（`Client.Ui/Screens/Research/`）は `UiLayer.OverlayPanel`・`UiFamily.Stage`（Web の `--z-stage-overlay-panel`）へ Presenter が 1 回だけ載せ、表示は経路（`UiRoutingState`）で `SetShown` する。ツリーは常駐の `NodeGraphView` 1 つにカードを使い回して渡し、ノードの集合か注目点が変わったときだけ `SetGraph` を呼ぶ（閉じて開き直しても視野が残る。C4-d D-23(3)）。詳細ペインは研究エリアの右上に浮かべ、研究ボタンは `RecipeActionButton` を画面 USS の祖先で研究の組に上書きする（[A-46]）。操作は `IResearchTreeCommands`（選択の切り替え・閉じる・研究の実行 `UniTask<UiCommandResult>`）で、失敗は `UiCommandResultToaster` の 1 本へ渡す。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`CustomStyleResolvedEvent`・`PointerDownEvent` 系・`panel.Pick`）、UniRx、UniTask、VContainer、NUnit＋`com.unity.ui.test-framework`（`UITestFixture`・`PanelSimulator`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（Web の参照キャプチャ・test-name-parity・`convert-web-tokens.mjs`・`gen:i18n`）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`（repo の兄弟）、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`、`THEME` = `S/Client.Ui/Foundation/Theme`。Files 節と各コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（とくに「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1.1・§2.7 NodeGraph と「研究ノードカード」・§3.2・§3.3・§4・§5.1、[A-8]・[A-13]・[A-45]・[A-46]・[A-47]・[A-52]・[A-57]・[A-65]・[A-92]）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-research 行・§4.3・§5・§6.2・E-1・E-13・E-22）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ。この計画が設計書・完了条件と違う所は判断記録に書き、設計書に足す行の案は末尾「## 設計書・実行計画へ反映する行」（A-170〜A-174）に置く（指揮が写す）
- **レーンの起動は指揮が行う**（実行計画 §4.1。実装は moores-subagent-driven-development でこのホストの Claude Code の subagent が担う）

## Requirements

設計の出所: 実行計画 §1.4 の S4-research 行（完了条件の原文:「ResearchTreePanel（`NodeGraphView`）・ノードカード 4 状態・詳細ペイン・解放物・研究ボタン（`RecipeActionButton` に無効の面を `rgb(50 52 67)` へ上書き [A-46]）。研究画面の左余白 0 は `StageColumnsLayout` [A-45]。アンカー `research.node-<guid>`（マスタ 7 件）。写す: research 55（P19 から `tmp-icon-click` を除く・C14。L22 は L0-s4b 済み）。録画 `tier9/tier9-5-compute-research`。研究の右の領域の `left` は画面 USS（列ではない）。左の余白の畳みは `StageColumnsLayout` が持つ [A-92]」。依存 C4-d・C3-a・C4-c・C4-e・L0-s4b）、§6.2、設計書 [A-46]・[A-92]、C4-e done.md の D-34（左余白の畳みは `UiScreenTraits.CollapsesStageLeftPadding` の列に移った）、指揮の追加指示（2026-11-10: 解放物の機械レシピの出力の State 側の源は S4-alpha が中立の置き場に作る。依存に S4-alpha を足す）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 静的な定義: 研究マスタの全ノードを `ResearchNodeDefinition`（GUID・アイコンの `ItemId`・ツリー座標 Y 上向き・前提 GUID の列・消費アイテム〈受け付ける上位レベルの ID 込み〉・解放物 `ResearchUnlockSet`）へ 1 回写す。解放物は Web の `ResearchNodeDtoFactory` と同じ 8 種（報酬・アイテムのレシピ表示・ブロック・機械レシピ〈アイテムと液体の両方の出力〉・接続ツール・車両・モジュール枠の上限〈レベルでなく上限値〉・製作上限）で、機械レシピの出力は S4-alpha の中立の源から引く | `ResearchCatalogFromMasterTest`（L0・テスト mod）が緑 |
| R-2 | ViewModel: 定義・サーバーの状態（無いノードは `UnresearchableAllReasons`）・所持数（持ち物のメインの枠）・選択から、カード（4 状態・状態ラベルのキー・選択）・初期の注目点（`ResearchLogic.TryFindInitialFocusNode`）・詳細（ボタンの状態・消費アイテムの割当と不足・解放物の列）を組む純関数 | `ResearchTreeViewModelFactoryTest`（L0）が緑 |
| R-3 | Model: 状態の通知・選択・持ち物の変化・経路・言語で、研究画面の表示中だけ同期で組み直す。表示外は組まず、入った時に組む | `ResearchTreeViewModelFactoryTest` と録画（R-13）の主張 |
| R-4 | 選択: 同じノードの再タップで閉じる切り替え、閉じるボタン、画面を離れたら選択を捨てる（Web はパネルのアンマウントで消える） | `ResearchSelectionTest`（L0）・`ResearchTreeViewTest`（Web `ResearchTreePanel.test.ts` 1 件目）が緑。離脱で消えることは録画 |
| R-5 | 研究エリア: `research-tree` が `OverlayPanel`/`Stage` 層で、上・右・下 0、`left` = `--inventory-panel-width` + `--stage-column-gap`（画面 USS で受け直し C# で足す [A-47]・[A-92]）。中に題「研究」の `GamePanel`（高さいっぱい）と `NodeGraphView("research")`（`research-viewport`・`research-canvas`） | `ResearchTreeViewTest`（Web `research.spec.ts`「研究パネルは持ち物の右隣から…」）が緑。並べ比べ（R-12） |
| R-6 | ノードカード: `research-node-<guid>`・幅 132・縦積み（名前 14px 省略・アイコン `ItemSlot`・状態ラベル 12px 省略 `research-node-state-<guid>`）・面 `--research-node-face`・枠 1px `--research-node-border`。完了は枠 `--text-default`、研究可能は枠 `--select-cyan`、前提未達はロックで減光（Web `opacity: .45` を Linear 合成で補正した `--research-node-locked-opacity`）、選択は外へ 2px の `--text-high-contrast` の輪。アンカー `research.node-<guid>` を付ける | `ResearchTreeViewTest`（Web `research.spec.ts`「ノードカードが4状態の…」・`ResearchNodeCard.test.ts` 2 件）が緑 |
| R-7 | ビューポート: 開いた時に研究可能なノードを中央に据え、閉じて開き直しても視野が残り、ホイールでカーソル位置を軸に拡大縮小し、空の背景とノードのドラッグでパンし（ドラッグは選択しない）、閾値未満の押下はタップとして詳細を開く | `ResearchTreeViewportTest`（Web `researchViewport.spec.ts` 5 件）が緑 |
| R-8 | 詳細ペイン: `research-detail-pane`（エリアの右上 12px・幅 300・高さはエリアの高さ − 24 まで）に `GamePanel(craft)`、見出し（名前 16px `--text-high-contrast`・閉じる `research-detail-close`「×」）、スクロール `research-detail-scroll`（説明 13px・消費アイテム `research-consume-items`・解放物の節）、研究ボタン `research-button-<guid>`、不可の理由 `research-detail-reason`（完了でも研究可能でもないとき） | `ResearchDetailPaneTest` が緑（Web `ResearchDetailPane.test.ts` の 5 件） |
| R-9 | 消費アイテム: `ItemSlot` に所持/必要（`ItemSlotShortage`・ツールチップ `MaterialTooltipKind.ResearchConsumeItem`）、不足は `insufficient`。完了ノードは不足を出さない。上位レベルの所持も充足に数える | `ResearchDetailPaneTest`（Web 4 件）が緑 |
| R-10 | 解放物の節: Web の `UnlockSections` と同じ 5 節（ブロック `research-unlock-blocks`・機械レシピ `research-unlock-machine-recipes`・アイテム `research-unlock-items`・報酬 `research-reward-items`・その他 `research-unlock-others`）を初出順に並べ、ブロックは `BlockSlot`、機械レシピはアイテムの `ItemSlot` と液体の量のラベル `research-unlock-fluid`、報酬は個数付き、その他は 13px の文の行（接続ツール・車両・`research-unlock-module-slot-stack-limit`・`research-unlock-craft-cap-<itemId>`） | `ResearchUnlockSectionsTest`（Web `ResearchDetailPane.test.ts` 5 件・`research.spec.ts` 2 件・`researchMachineUpgradeUnlock.spec.ts` 1 件）が緑 |
| R-11 | 研究ボタン: `RecipeActionButton(Regular)` を研究の組（幅はペインの本文の幅・高さは撮影で決めた値・無効の面 `--research-action-disabled-face`・無効の字 `--text-default`・無効の淡さ `--research-action-disabled-opacity`・ホバーで面を変えない・14px・字間 0・縁なし・沈まない）で上書きし、文言は完了なら「完了済み」、それ以外は「研究」。押すと `IResearchTreeCommands.CompleteResearch` を呼び、応答の全状態を `ResearchNodeStateStore.ApplyNodeStates` へ入れ、失敗（応答なし・サーバーの拒否）は良性でない `ResearchFailed`（wire `research_failed`）でトーストに出す | `ResearchDetailPaneTest`（Web `research.spec.ts`「research button sends research.complete…」・`ResearchDetailPane.test.ts` 1 件）が緑。録画（R-13） |
| R-12 | 並べ比べ: 参照 `4-research`・`5-tutorial-research-node`（1280×720・2560×1080）と、参照の組に無い詳細ペインの 3 状態（研究可能・素材不足・完了）を master の mock-host で自撮りして、UITK と並べた差の表を `done.md` に書く | 組が報告ページにあり、差の表の全行に「同じ／直した／LX-n／捨てる」のどれか |
| R-13 | 録画: 新しい `uitk/uitk-research-tree`（全体スイッチ UITK。開く・中央寄せ・ノードのタップで詳細・研究の実行で完了・閉じて開き直しで視野が残り選択が消える・チャレンジ HUD とキーヒントが Pick で遮られない・マスタの研究ノードのアンカー 7 件が引ける）と、既存 `tier9/tier9-5-compute-research` を全体スイッチ UITK で通す | 2 本の `result.json` が `Success: true`、録画 mp4、Note の `系=GameUi` |
| R-14 | 登録: `MainGameUiRegistration` の画面登録区画に `ResearchGameUiRegistration.Register(builder);` 1 行。`GameUiTheme.tss` の `Screens` 区画に `@import` 1 行。`TutorialAnchorCoveragePending` に `ResearchNodePrefix` の行があれば同じ PR で消す | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`・`TutorialAnchorCoverageTest`）が緑 |
| R-15 | テストの名前: 写したものは `<web-test>`、写さないもの 7 件は `docs/uitk/disposition.md` の「## 処遇」に行を足す | `node tools/uitk/test-name-parity.mjs --bundle 4 --emit-missing` で、このレーンの持ち分（下の「写すテスト」の表）の未対応 0。終了コードが 2 でない |
| R-16 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd を閉じる → `moores-wt rm` | `git log origin/feature/uitk` にこのレーンのマージコミット。`done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 部品（`NodeGraphView`・`RecipeActionButton`・`GamePanel`・`GameScrollView`・`ItemSlot`・`BlockSlot`・`LocalizedLabel`）と Foundation（`StageColumnsLayout`・`GameUiStageColumnsInstaller`・`UiLayer`・`UiScreenTraits`・`TutorialAnchor`・`AnchorIds`・`UiPickingIgnore`）は変えない。足りなければ `done.md` の「integrator への依頼」に書く
- 研究画面の左余白 0（持ち物を左へ寄せる）は作らない。C4-e が `UiScreenTraits.CollapsesStageLeftPadding` と `GameUiStageColumnsInstaller` で済ませた（D-34）。このレーンは確かめる L1 を写すだけ
- 持ち物パネルそのもの（研究画面の左側）は作らない（S4-inventory）。並べ比べで持ち物が未描画なら差の表で「捨てる（S4-inventory の持ち物）」とする
- 機械レシピの出力の State 側の源（`Client.Ui.State/Recipe/Source/` 想定）は作らない（S4-alpha。指揮の 2026-11-10 の指示）
- チュートリアルの強調の描き手（S2-tutorial）・`TutorialAnchor.MarkClipContainer` は作らない。`NodeGraphView` の切り口の印は部品（C4-d）か S2-tutorial が付けるべきもので、2 機能（研究・チャレンジ）が使うので integrator へ申告する（D-9）
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は一切変えない。`ResearchNodeDtoFactory` と `ResearchNodeDefinitionsFromMaster` の重複は「本PR外のリファクタ提案」に回す
- L3（EditModeInPlayingTest）は持たない（段の締め S4-ladder）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s4-research --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる。Mac の Editor が逼迫したら Windows の `~/bin/winunity`（compile/test/play を ssh で回す）を使ってよい
- **依存**: C3-a（`ItemSlot`・`BlockSlot`。マージ済み）・C4-e（`StageColumnsLayout`・D-34。マージ済み #1626）・L0-s4b（`Client.Ui.State/Research/Logic`。マージ済み）・C4-d（`NodeGraphView`）・C4-c（`RecipeActionButton`・`--research-action-disabled-*`）・S4-alpha（機械レシピの源）。C4-d・C4-c・S4-alpha は計画の Produces から写したので「マージ後に実物と照合」（Task 1 Step 1 で grep し、名前が違えば以後を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <旧> → <実物>」と書く）。無ければ止めて指揮へ報告する
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
//   --recipe-action-width・--recipe-action-height・--recipe-action-padding-block: 8px・--recipe-action-font-size: 14px・--recipe-action-letter-spacing: 0px・--recipe-action-text-outline-width: 0px・--recipe-action-active-offset: 0px
// --research-action-disabled-face: rgb(50, 52, 67)・--research-action-disabled-opacity は C4-c が DerivedTokens.uss の C4-c ブロックに置く（このレーンは var() で使うだけ。同じ値を別名で足さない）

// ===== S4-alpha（PLAN-W4a の計画 uitk-plan-w4a/docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md 末尾「## 後続レーンへの Produces 一覧」から写す。起草時点で未作成のため想定名。マージ後に実物と照合）=====
namespace Client.Ui.State.Recipe.Source
{
    public sealed class MachineRecipeSource                         // 想定: 研究マスタと同じく MasterHolder.MachineRecipesMaster から機械レシピを中立の形で引く。DI のシングルトン
    {
        public bool TryGet(Guid recipeGuid, out MachineRecipeData recipe);  // 無い GUID は false
    }
}
// MachineRecipeData（実物 S/Client.Ui.State/Recipe/Data/MachineRecipeData.cs）: RecipeGuid・OutputItems（IReadOnlyList<ItemAmount>。.ItemId）・OutputFluids（IReadOnlyList<RecipeFluidAmount>。.FluidId・.Amount）
```

- **層の向き**（設計書 §1.1・`UiLayerDependencyRules`）: `Client.Ui.State/**` は Client.Game・Client.Network・Core.Master・Game.Research・Game.Block.Interface を参照してよい（asmdef 既存）。`Core.Item`（`LevelFamilySubstituteUtil`）は未参照なので `Client.Ui.State.asmdef` の `references` に 1 行足す（D-12）。`Client.Ui/Screens/**` は State・Components・Foundation・Content を参照してよく、`Client.Game` の型を受けない（ViewModel は ID・状態・キーだけを運ぶ）
- **AGENTS.md の規約**: 1 ファイル 200 行未満・1 ディレクトリ 10 ファイルまで（.meta は数えない）・`partial`/`Func<>` 禁止（UniRx の `Where`・`Select` にも渡さず、購読の中で分岐する）・イベントは UniRx・try-catch 禁止（ネットワークの待ちは既存 API が null で失敗を返すので catch は要らない）・デフォルト引数禁止・単純な getter/setter プロパティ禁止（`{ get; private set; }` と式本体の読み取り専用は可）・主要処理に「// 日本語 → // English」の 2 行コメント（各 1 行）・メソッド内のローカル関数は `#region Internal`・初期化は `Initialize`・fail-closed の経路はログ（意図した無音は `silent-ok: <理由> / <誰がどこで気づくか>`）・`.meta` は手で作らない・Prefab/シーンを手で書き換えない・`Update()` で毎フレームの同値判定をしない
- **USS の規約**（ref-uitk-design・設計書 §3.2）: 画面 USS はルートのクラス `.research-tree` で始まり、画面のクラスは `research-tree__*`。部品のクラス（`.game-panel__*`・`.node-graph*`・`.item-slot*`・`.recipe-action-button*`）を書かない（部品の見た目はつまみで変える）。色は `var()` だけ、`z-index`・構造擬似クラス・`gap`・合成の太字を使わない（Web の `gap` は子の margin で写す）。USS の先頭に `/* web: <Web の CSS> */` を日英で書く。C# が値を要るときは画面のクラスで `--research-tree-<名>: var(--token)` と受け直して `UssCustomStyleReader` で読む（`:root` の変数は customStyle へ継承されない）
- **calc() の写し**（[A-47]）: Web の `.researchArea` の `left: calc(var(--inventory-panel-width) + var(--stage-column-gap))` と `.detailScroll` の `margin-inline: calc(-1 * var(--research-detail-scroll-inline-bleed))` は、画面内の `ResearchCalcKnobs` が customStyle から読んで inline で当てる。換算値を USS に直書きしない
- **字間・色の罠（持ち越し）**: UITK の字間は字の大きさ/100 × 値（px のまま書くと 100/字の大きさ 倍に効く）。このレーンは字間を書かない（研究ボタンは C4-c の `--recipe-action-letter-spacing: 0px` を当てるだけ）。部品ギャラリーは sRGB 合成なので、色はランタイムの PanelSettings（Linear 合成）で比べる。暗い半透明の α は I-fix1 が指数 1.85 で選び直した。明るい色の α は未実測。このレーンの減光（ロックのカード `opacity: .45`）は暗い面の上なので 1.85 の式 `1-(1-0.45)^1.85 = 0.6691` を初期値にし、並べ比べで確かめる（外れたら値を直すか `LX-136`）
- **テストの置き場と名前**: `S/Client.Tests/Ui/Screens/Research/`・`S/Client.Tests/Ui/State/Research/`（既存 4 本と同じ所）、namespace はディレクトリに合わせる。テスト名は日本語の挙動文。Web から写したものは `[Test]` の直前に `/// <web-test file="<W/ からの相対パス>">it/test の第 1 引数</web-test>`（describe は含めない。`it.each` は第 1 引数の書式文字列そのままで展開しない。1 つの NUnit が複数の Web テストを写したら行を並べる。実行計画 E-13）
- **コマンド**: コンパイル `uloop compile --project-path ./moorestech_client`（期待: ErrorCount 0）。テスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。このレーンのテストフィルタ（以下 `$FILTER`）は `^Client\.Tests\.Ui\.(Screens\.Research|State\.(Research|PauseMenuCommandsTest)|Conventions|Theme|Layers|Anchor|Routing|Components\.(NodeGraph|Controls))\.`。ドメインリロード中のエラーは 45 秒待って再試行
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有ファイルは、`MainGameUiRegistration.cs` の画面登録区画の 1 行、`GameUiTheme.tss` の `Screens` 区画の `@import` 1 行、`THEME/DerivedTokens.uss` の末尾の名札付きブロック「S4-research」（[A-52]）と、その減光 1 行を前計算の表へ載せる `tools/uitk/webTokens/handWrittenDerivedRows.mjs` の `rowSources` の末尾 1 件・再生成した `docs/uitk/translucent-tokens.md`、`docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」（`LX-136`〜`LX-140`）「## 追従記録」の末尾、`docs/uitk/disposition.md` の「## 処遇」の末尾 7 行、`S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` の `AnchorIds.ResearchNodePrefix` の 1 行の削除（在るときだけ）、`S/Client.Ui.State/Commands/UiCommandRejectReason.cs` の共有区画（enum と 3 つの switch の末尾に `ResearchFailed` を 1 行ずつ）と良性の一覧を固定する `S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs`（良性でない値なので一覧は変わらないが、全値の網羅の行があれば 1 つ足す）、`Localization/localization.csv` の `ui.commandRejected.*` の群の末尾 1 行（`ui.commandRejected.researchFailed`）と再生成した `W/src/shared/i18n/generated/localizationKeys.ts`（`npm run gen:i18n`。台帳「## 追従記録」へ 1 行）・`S/Client.Localization/_CompileRequester.cs`（生成の印）、`S/Client.Ui.State/Client.Ui.State.asmdef` の `references` に `Core.Item` の 1 行。シナリオ `SKILL/scenarios/uitk/uitk-research-tree.cs` はこのレーンが持つ。他の S4 レーン（S4-alpha・S4-inventory・S4-machine・S4-train・S4-recipe・S4-special・S4-gamma）のファイルは書かない
- **画面専用の部品の置き場**（設計書 §2.7・§5.1-2）: ノードカード・カードの集合・詳細ペイン・研究ボタンの行・消費アイテムの節・解放物の節・calc の受け直しは 1 機能目なので `S/Client.Ui/Screens/Research/Parts/` に置く。チャレンジ（S5-challenge）と共有しうるもの（`NodeGraphView` の切り口の印・カードの選択の輪）は作らず宣言だけする（D-9・D-6）
- **新しい共有部品を見つけたときの手順**: 作業中に「別の画面でも要る部品・Foundation の口」を見つけたら作らない。`LOGS/harness/uitk/S4-research/done.md` の「## integrator への依頼」に「部品名／Web の正本のパス／使う画面／このレーンで作らない理由」を 1 行で書き、`bd note <bd> "<同じ 1 行>"` にも残して先へ進む。既知の依頼（Task 1 Step 3 で雛形に書く）: (1) `NodeGraphView` の切り口（`overflow: hidden`）に `TutorialAnchor.MarkClipContainer` を部品が自分で付ける（研究・チャレンジの 2 機能。D-9）(2) `Registration/GameUi/` が 10 ファイルを超えるならサブディレクトリの切り方（Task 1 Step 1）
- **参照キャプチャ**: `LOGS/uitk/reference-captures/web-a9d5123537/`（1280×720）・`web-0e81bbe3c3-2560x1080/`（2560×1080。座標は `x' = 320 + 1.5x`・`y' = 1.5y`）の `4-research.png`（`manifest.json` の controls: topic `researchOwnedItems`・ui `ResearchTree`）・`5-tutorial-research-node.png`（topic `tutorialResearchNode`）。詳細ペインの 3 状態は参照に無いので Task 11 Step 1 で master の mock-host で撮る（E-22。C4-c が Task 8 Step 2 で撮った `web-research-*.png` があればそれを先に使う）
- **並べ比べの画像の原本**は worktree の gitignore 下 `moorestech_client/.uloop/outputs/compare/S4-research/` に置き、`moorestech_logs` へ入れない。`moorestech_logs` へ書く前に `LOGS/README.md` を読む
- **報告**: `LOGS/harness/uitk/S4-research/done.md`（1 行目「状態: 完了」。途中で止まるときは「状態: 停止（理由）」）。節は「証拠」「差の表」「要裁定」「integrator への依頼」「未確認」

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

## 死活表（Phase 2.5）

全体スイッチ UITK（`UiInputExclusivity.ActiveUiSystem == GameUi`）で起動したとき、Web の系で動いていた研究の振る舞いが生きるか。Web の橋（`ResearchTopic`・`ResearchCompleteActionHandler`）は Web の系でだけ登録され、源の `ResearchNodeStateStore` は両系で登録済み。

| 振る舞い | GameUi で生きるか | 根拠 |
|---|---|---|
| R キーで研究画面を開く・ESC で閉じる | 生きる（このレーンの外） | `UIStateControl` の `ResearchTree` 状態の遷移は Client.Game で UI の系を読まない。View は経路（`UiRoutingState`）で `SetShown` するだけ（Task 8） |
| 研究画面へ入るたびの状態の取り直し | 生きる | `ResearchNodeStateStore` が両系で `OnStateChanged` を購読（既存）。Model は `OnNodeStatesChanged` で組み直す（Task 4） |
| ノードの一覧・4 状態・状態ラベル | 生きる | `ResearchTreeModel` → `ResearchTreeView`（Task 4・7・8） |
| パン・ズーム・慣性・開き直しで視野が残る | 生きる | C4-d の `NodeGraphView` を常駐で 1 つ使う（D-3。Task 8・9） |
| ノードのタップで詳細・同じノードで閉じる | 生きる | `OnNodeTapped` → `IResearchTreeCommands.ToggleNodeSelection`（Task 5・8） |
| 研究の実行と完了の反映 | 生きる | `ResearchTreeCommands.CompleteResearch` が Web の橋と同じ API と `ApplyNodeStates` を呼ぶ（Task 5・7） |
| 研究の失敗の知らせ | 生きる（Web と同じく良性でないトースト） | `UiCommandRejectReason.ResearchFailed` → `UiCommandResultToaster`（Task 5） |
| 表示中の持ち物の変化で充足を取り直す | 生きる | `ResearchNodeStateStore` が両系で購読（既存）＋ Model が所持数で組み直す（Task 4） |
| 研究画面で持ち物が左へ寄る | 生きる（C4-e 済み） | `GameUiStageColumnsInstaller` が `CollapsesStageLeftPadding` を読む（Task 9 の L1 で確かめる） |
| 研究画面でホットバーと装備 HUD を出さない | 生きる（持ち主は各 HUD） | `UiScreenTraits.AlwaysOnHud` が `ResearchTree` で false。読むのは S3-hotbar・S4-inventory（Task 9 の L1 で表の値を固定） |
| チャレンジ HUD・キーヒントが研究エリアに遮られない | 生きる | 研究エリアは `OverlayPanel` 層、HUD は `Chrome` 層（上）。録画で `panel.Pick` が HUD 自身を返すことを見る（Task 12） |
| チュートリアルの研究ノードの強調（マスタ 7 件） | S2-tutorial の描き手が入っていれば生きる | カードに `TutorialAnchor.Mark(card, AnchorIds.ResearchNode(guid))`（Task 7）。録画で 7 件の ID の要素が引けることを見る（Task 12）。切り口の印は integrator への依頼（D-9） |
| 研究ノードの上のクリックがワールドに抜けない | 生きる | 研究エリアは `GamePanel`（拾う）で覆い、`UiPointerHitTest.IsPointerOverAnyUi` が真になる（エリアの根は拾わない。Task 8 の L1 で Pick） |

---

## File Structure

| パス | 責務 | 新規／変更 |
|---|---|---|
| `S/Client.Ui.State/Research/Catalog/ResearchNodeDefinition.cs` | 研究ノード 1 件の静的な定義（GUID・アイコン・座標・前提・消費・解放物） | 新規 |
| `S/Client.Ui.State/Research/Catalog/ResearchNodeCatalog.cs` | 定義の列を 1 回だけ組んで持つ（DI のシングルトン） | 新規 |
| `S/Client.Ui.State/Research/Catalog/ResearchNodeDefinitionsFromMaster.cs` | 研究マスタ → 定義の列（座標・前提・消費アイテムと受け付ける上位レベル） | 新規 |
| `S/Client.Ui.State/Research/Catalog/ResearchUnlockSetFromMaster.cs` | `ClearedActions` → `ResearchUnlockSet`（8 種。機械レシピは S4-alpha の源） | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchSelection.cs` | 選択の有無を型で持つ値（`None`・`Of`・`Is`・`TryGetGuid`・`Toggled`） | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchNodeCardViewModel.cs` | カード 1 枚の表示（状態・ラベルのキー・選択） | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchDetailViewModel.cs` | 詳細の表示（ボタンの状態・消費の枠 `ResearchConsumeSlotViewModel`・解放物の列）。消費の枠の型も同じファイル | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchTreeViewModel.cs` | 画面の不変の ViewModel（カードの列・注目点・詳細） | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchTreeViewModelFactory.cs` | 定義・状態・所持数・選択から ViewModel を組む純関数 | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchTreeModel.cs` | 源を購読し、表示中だけ同期で組み直して `Current` で配る（`IInitializable`） | 新規 |
| `S/Client.Ui.State/Research/Tree/ResearchSelectionStore.cs` | 選択を持ち、研究画面を離れたら捨てる（`IInitializable`） | 新規 |
| `S/Client.Ui.State/Research/Commands/IResearchTreeCommands.cs` | 画面から呼ぶ操作（[A-8]） | 新規 |
| `S/Client.Ui.State/Research/Commands/ResearchTreeCommands.cs` | 本番の実装（選択・研究の実行と状態の反映・失敗の拒否） | 新規 |
| `S/Client.Ui.State/Client.Ui.State.asmdef` | `references` に `Core.Item` | 変更（D-12） |
| `S/Client.Ui.State/Commands/UiCommandRejectReason.cs` | enum と 3 つの switch の末尾に `ResearchFailed` | 変更（共有区画） |
| `Localization/localization.csv`・`W/src/shared/i18n/generated/localizationKeys.ts`・`S/Client.Localization/_CompileRequester.cs` | 拒否理由の文言 1 行と再生成 | 変更（共有区画・生成物） |
| `S/Client.Ui/Screens/Research/ResearchTreePresenter.cs` | View を `OverlayPanel`/`Stage` へ 1 回載せ、経路で表示・Model で描画 | 新規 |
| `S/Client.Ui/Screens/Research/ResearchTreeView.cs` | 研究エリア（`GamePanel`・`NodeGraphView`・詳細の置き場）。`Render`・`SetShown` | 新規 |
| `S/Client.Ui/Screens/Research/ResearchTree.uss` | 画面の USS（エリア・カード・詳細・研究ボタンのつまみの上書き） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchNodeCard.cs` | カード 1 枚（名前・アイコン・状態ラベル・選択の輪・アンカー） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchNodeCardSet.cs` | カードを GUID で使い回し、`NodeGraphContent` を組む・組み直しの要否を返す | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchDetailPane.cs` | 詳細ペイン（見出し・閉じる・スクロール・節・行） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchDetailActions.cs` | 研究ボタンと不可の理由（押下 → 操作 → トースト口） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchConsumeItemsSection.cs` | 消費アイテムの節（所持/必要・不足） | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchUnlockSections.cs` | 解放物の節を初出順に組み、1 件ずつ描く | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchUnlockSectionKind.cs` | 解放物の種類 → 節（見出しのキー・testid・枠の列で包むか）の唯一の switch | 新規 |
| `S/Client.Ui/Screens/Research/Parts/ResearchCalcKnobs.cs` | calc() の 2 つ（エリアの `left`・スクロールの逃げ）を customStyle から読んで当てる（[A-47]） | 新規 |
| `S/Client.Starter/Registration/GameUi/ResearchGameUiRegistration.cs` | 定義・選択・Model・操作・Presenter の登録 | 新規 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画に 1 行 | 変更（共有区画） |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | `Screens` 区画に `@import` 1 行 | 変更（共有区画） |
| `THEME/DerivedTokens.uss` | 末尾に名札付きブロック「S4-research」（ロックの減光 1 行） | 変更（共有区画 [A-52]） |
| `tools/uitk/webTokens/handWrittenDerivedRows.mjs`・`docs/uitk/translucent-tokens.md` | 減光 1 行を前計算の表へ（再生成） | 変更（共有区画） |
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
| `docs/uitk/disposition.md` | 「## 処遇」の末尾に 7 行 | 変更（共有区画） |
| `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` | 在れば `AnchorIds.ResearchNodePrefix` の 1 行を消す | 変更（共有区画・在れば） |
| `docs/uitk/TODO.md` | 「## 実測の結論」（ロックの減光）・「## 見た目の例外」（`LX-136`〜`LX-140`）・「## 追従記録」（gen:i18n）の末尾 | 変更（共有区画） |

ディレクトリのファイル数（.cs と .uss）: `State/Research/` 既存 1（`ResearchNodeStateStore.cs`）＋サブディレクトリ、`State/Research/Catalog/` 4、`State/Research/Tree/` 7、`State/Research/Commands/` 2、`Screens/Research/` 3、`Screens/Research/Parts/` 8、`Tests/.../Screens/Research/` 7、`Tests/.../State/Research/` 既存 4＋3＝7。`Registration/GameUi/` は 1 増える（Task 1 Step 1 で 10 以下を確かめる。超えるなら止めて指揮へ）。

**MainGameUiRegistration の画面登録区画に足す行**: 1 行（`ResearchGameUiRegistration.Register(builder);`）。

**つまみの上書き**（Web で局所上書きされている変数と同名。研究ボタンは C4-c の部品のつまみを `.research-tree__detail-body` で上書きする。値は Web の `.researchButton` と同じ）

| つまみ | Web の値（`W/src/features/research/style.module.css` の `.researchButton`） | UITK の値（`.research-tree__detail-body`） |
|---|---|---|
| `--recipe-action-hover-start/middle/end` | （素の button。ホバーで面が変わらない） | `var(--recipe-action-background-start/middle/end)` |
| `--recipe-action-disabled-start/middle/end` | `:disabled` の `background: rgb(50 52 67)` | `var(--research-action-disabled-face)` |
| `--recipe-action-disabled-text` | `:disabled` の `color: var(--text-default)` | `var(--text-default)` |
| `--recipe-action-disabled-opacity` | `:disabled` の `opacity: 0.6` | `var(--research-action-disabled-opacity)`（C4-c が Linear 補正込みで持つ） |
| `--recipe-action-width` | 幅の指定なし（列の本文いっぱい = 300 − padding 10×2） | `280px`（Task 11 で確かめる） |
| `--recipe-action-height` | 高さの指定なし（`padding: 8px 0` ＋ 14px の行の高さ `normal`） | `36px` を初期値（14px の Noto Sans JP の `normal` を 20px と見た値。Task 11 Step 3 で Web の画素から決め直す） |
| `--recipe-action-padding-block` | `8px` | `8px` |
| `--recipe-action-font-size` | `14px` | `14px` |
| `--recipe-action-letter-spacing` | `normal` | `0px` |
| `--recipe-action-text-outline-width` | （影なし） | `0px` |
| `--recipe-action-active-offset` | （素の button。沈まない） | `0px` |

**受け直すトークン**（`.research-tree` で受け、`ResearchCalcKnobs` が読む）: `--research-tree-inventory-width: var(--inventory-panel-width)`・`--research-tree-column-gap: var(--stage-column-gap)`・`--research-tree-scroll-bleed: var(--research-detail-scroll-inline-bleed)`。

**写すテストの件数（Web の実物を grep して数え直した。数え方: `it(`／`test(` の呼び出し 1 つを 1 件、`it.each` は書式文字列 1 つを 1 件〈行は展開しない〉と数える。1 つの NUnit が複数の Web テストを写すことがあり、`it.each` の 1 件を `[TestCase]` の複数行の NUnit 1 本で写す）**

| Web のファイル（`W/` から） | 件数 | 写す | 写さない（処遇） | 写し先 |
|---|---|---|---|---|
| `e2e/tests/research/research.spec.ts`（P） | 12 | 8 | 捨てる 2（`expectCraftGrip` の自己テスト 2 件「translate後のグリップ矩形だけに重なる境界buttonを…」「padding box外の境界buttonを…」: DOM の補助関数の検査で UITK に同じ物が無い）、録画 2（「研究パネル展開中も常駐チャレンジHUDとキーヒントが遮蔽されない」「研究パネル展開中も採掘進捗バーが遮蔽されない（…）」: 他レーンの HUD を同じパネルに並べる L1 は持てないので録画 `uitk/uitk-research-tree` の Pick で見る） | `ResearchTreeViewTest` 3・`ResearchDetailPaneTest` 1・`ResearchUnlockSectionsTest` 2・`ResearchStageLayoutTest` 2 |
| `e2e/tests/research/researchMachineUpgradeUnlock.spec.ts`（P） | 1 | 1 | 0 | `ResearchUnlockSectionsTest`（`ResearchDetailPane.test.ts` 11 件目と同じ NUnit に 2 行） |
| `e2e/tests/research/researchViewport.spec.ts`（P） | 5 | 5 | 0 | `ResearchTreeViewportTest` |
| `e2e/tests/research/tmp-icon-click.spec.ts`（P） | 1 | 0 | 捨てる 1（`tmp:` の付いた一時の調査テスト。完了条件で除外） | — |
| `src/features/research/ResearchNodeCard.test.ts`（C） | 2（`it` 1・`it.each` 1〈5 行〉） | 2 | 0 | `ResearchTreeViewTest`（`it.each` は `[TestCase]` 5 行の 1 本） |
| `src/features/research/ResearchTreePanel.test.ts`（C） | 2 | 1 | 捨てる 1（「インベントリtopic未受信中は詳細ペインへ所持数をnullで渡す(D4)」: UITK に未受信の状態が無い [A-13]。前例 disposition の A-13 の行） | `ResearchTreeViewTest` |
| `src/features/research/ResearchDetailPane.test.ts`（C） | 11 | 10 | 捨てる 1（「所持数未受信中(owned=null)は不足表示も所持/必要も出さない」: 同上 [A-13]） | `ResearchDetailPaneTest` 6・`ResearchUnlockSectionsTest` 4 |
| `src/features/research/researchLogic.test.ts`・`researchAllocation.test.ts`（L） | 22 | — | L0-s4b で写し済み | — |
| `src/shared/treeView/**`（C・L） | 29 | — | C4-d が写す | — |

合計: Web の P 19・C 15（実行計画の「C14」は `ResearchNodeCard.test.ts` を 1 と数えた値で、実物は `it` 1＋`it.each` 1＝2。D-15）・L 22。このレーンの持ち分は P 18（`tmp-icon-click` を除く）＋ C 15 ＝ 33。うち `<web-test>` で写す 27（P 14・C 13）、処遇の表に足す 7（捨てる 5・録画 2）。L1 の NUnit の数は `ResearchTreeViewTest` 6・`ResearchTreeViewportTest` 5・`ResearchDetailPaneTest` 7・`ResearchUnlockSectionsTest` 6・`ResearchStageLayoutTest` 2。タグの無い NUnit（Web に無い主張）は `ResearchTreeViewTest` の Pick 1・`ResearchDetailPaneTest` の失敗のトースト 1 と L0 の 3 本。

---

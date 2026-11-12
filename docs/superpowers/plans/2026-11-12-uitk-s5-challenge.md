# UITK S5-challenge（段階 5 画面: チャレンジ一覧・カテゴリのボタン列・ノードカード）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/challenge`（`ChallengePanel`・`ChallengeNodeCard`・`challengeLogic`）を、C4-d の `NodeGraphView`・C2-a の `PaperSurface`・C2-b の `FormButton`・Foundation の `UiGroup` で Web と同じ見た目の UITK 画面にし、各カテゴリの初回訪問だけ進行中へ寄せ再訪と開き直しでは前回の視点を戻す（裁定 2026-11-04）。L0/L1・並べ比べ（1280×720・2560×1080）・録画 2 本まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。State（`Client.Ui.State/Challenge/Tree/`）は `ChallengeProgressState` の全カテゴリから、解放済みのカテゴリだけを受信順（＝マスタの順。握手の応答と完了の知らせはどちらも `CompletedChallengeEventPacket.GetChallengeCategories` がマスタの `ChallengeCategoryMasterElements` の順に並べる）で写す純関数（`ChallengeListViewModelFactory`）と、表示中だけ組み直す `ChallengeListModel`、離れたら先頭へ戻すカテゴリの選択ストア（前例 `ResearchSelectionStore`）を持つ。一覧の表示は型で 2 つに分かれる（解放済みのカテゴリが無い `EmptyChallengeListViewModel`／選んだカテゴリを必ず持つ `SelectedChallengeListViewModel`）。一覧が出ているかの判定は `ChallengeScreenVisibility.IsShown` の 1 本。View（`Client.Ui/Screens/Challenge/Tree/`）は `UiLayer.OverlayPanel`・`UiFamily.Stage`（Web の `--z-stage-overlay-panel`）へ Presenter が 1 回だけ載せ、経路で `SetShown` する。視点は View の寿命で持つ（I-fix6 D-3 が `RestoreViewport` を消し、視点の保持を View の寿命にした形に乗る）: 訪れたカテゴリごとに `NodeGraphView` を 1 本作って控え（カテゴリ Guid → View。画面の寿命）、選んだカテゴリが変わったら表示する View を付け替える。View を作ったときだけ部品が進行中へ 1 回寄せる（裁定 2026-11-04 の初回訪問）。カテゴリのボタン列は `Components/Controls/GameScrollView` の横向き（このレーンが部品に足す `GameScrollAxis.Horizontal`）に入れ、幅を越えたら横に送れる（Web `overflow-x: auto`）。カードは `PaperSurface` を根にした持ち手 `ChallengeNodeCard`（`Screens/Challenge/Parts/`）。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`panel.Pick`）、UniRx、VContainer、NUnit＋`com.unity.ui.test-framework`（`UITestFixture`・`PanelSimulator`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`・`PlaytestGameViewOps`）、node（`tools/uitk/test-name-parity.mjs`）、gh CLI、bd（Beads）、Windows の `~/bin/winunity`

## Requirements

設計の出所: 実行計画 §1.4 の S5-challenge 行（原文）:

> | S5-challenge | チャレンジ一覧 | 5・W5 | C4-d・C2-a・C2-b | 要 | 単位 | `NodeGraphView` の再利用・カテゴリのボタン列（`FormButton` の文言は `UiText.FromContent` [A-40]）・`PaperSurface` のノードカード・`FormButton`。写す: `challenge.spec.ts` 3 件（画面 2 件＋常駐 HUD 維持 1 件） [A-58]・`challengeLogic.test.ts`(2) |

指揮の追加の完了条件（原文の要旨）: (1) S4-research の `NodeGraphView.uss` の `.node-graph__node { height: 0 }` 削除の事後承認（conductor.md「11-11 05:37」節「指揮の判断（agent 既定）: NodeGraphView.uss の .node-graph__node height:0 削除を事後承認（Web の包みは静的でカードが中身なりの高さ。S5-challenge へ申し送る）」・S4-research/done.md:91）。チャレンジのノードカードが中身なりの高さで描かれることを L1 で固定し、承認を判断記録と設計書の A 行案に残す。(2) 裁定 `.decisions/2026-11-04-チャレンジ画面は各カテゴリの初回訪問だけ進行中へ寄せ再訪は前回の視点を戻す.md`（決定: 各カテゴリを初めて開いたときだけ進行中のチャレンジへ寄せる。一度開いたカテゴリへ戻ったとき・パネルを開き直したときは、前回見ていた位置を戻す）を UITK で同じ振る舞いにする。Web は `TreeView` の `viewportStore`（モジュールの Map）と `key={selected.guid}` の作り直しでそうなっている（`W/src/features/challenge/ChallengePanel.tsx`・`W/src/shared/treeView/TreeView.tsx`。確認済み）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 注目点の論理: ノードの列から状態が進行中の最初のノードを返し、無ければ寄せない（Web `findChallengeInitialFocusNode`） | `ChallengeListLogicTest`（L0。`challengeLogic.test.ts` の 2 件）が緑 |
| R-2 | ViewModel の工場: `ChallengeProgressState.Categories`（`ChallengeCategoryResponse` の列）から、`IsUnlocked` のカテゴリだけを受信順（＝マスタの順）で写し（Web `ChallengeDtoBuilder.BuildTree`）、各カテゴリのノードはマスタの `Challenges` の順で、状態は完了＞進行中＞未解放、状態の文言キーは State の `ChallengeListLogic`、座標は `DisplayListParam.UIPosition`、倍率は `UIScale` の x・y、前提は `PrevChallengeGuids`（省略〈null〉は前提なし）のうち同じカテゴリにあるものだけ（C4-d D-5 の申し送り (2)）。選択したカテゴリが出ていなければ先頭（Web の `find(...) ?? categories[0]`）。解放済みが無ければ空の一覧（`EmptyChallengeListViewModel`。選択を持たない） | `ChallengeListViewModelFactoryTest`（L0）の 4 件が緑 |
| R-3 | Model と選択: 完了の知らせ・選択の変化・一覧への突入で同期で組み直す。組み直すのは一覧の表示中だけ（前例 `ResearchTreeModel`）で、表示中かは経路の今の値を `ChallengeScreenVisibility.IsShown` で読む（購読の順に依らない）。カテゴリの選択は一覧を離れると「無し」に戻り、開き直すと先頭のカテゴリになる（Web はパネルのアンマウントで `useState` が消える） | `ChallengeListModelTest`（L0 4 件: 閉画面では組まない・突入と選択と完了の知らせで組み直す・離れた後の戻しは登録順の両方で組まず開き直すと先頭）と `ChallengeCategorySelectionStoreTest`（L0 1 件）が緑。`ChallengeListViewportTest`「閉じて開き直すと先頭のカテゴリへ戻り…」が緑。録画 `uitk-challenge-list` の開き直しの段 |
| R-4 | 視点: 訪れたカテゴリごとに `NodeGraphView("challenge", …)` を 1 本作って控え（`ChallengeCategoryGraph`。カテゴリ Guid → View。画面の寿命）、選んだカテゴリが変わったら置き場に付ける View を付け替える（置き場の子は表示中のカテゴリの 1 本だけ）。View を作ったときだけ部品が進行中へ 1 回寄せ、再訪と開き直しは控えた View の視点のまま（View の寿命で保つ。I-fix6 D-3 と同じ形。`RestoreViewport` と State の視点の Session は使わない） | `ChallengeListViewportTest`（L1）の 3 件が緑（`challenge.spec.ts` の 1 件と裁定 2026-11-04 の 2 件。再訪は同じ View のインスタンスが付け直され、木の中の `NodeGraphView` が常に 1 本であることも見る） |
| R-5 | 一覧の枠: `challenge-panel`（アンカー `challenge.panel`）は舞台の上端から `--menu-upper-safe-area`（168px）下・左右下 0、地 `--mantine-color-dark-8`、内側の余白 `--mantine-spacing-md`、縦積み。中に題（`title--order-2`・`ui.challenge.title`）・カテゴリのボタン列 `challenge-categories`（アンカー `challenge.categories`。中に `GameScrollView("challenge-categories-scroll", GameScrollAxis.Horizontal)`、その中に `UiGroup(UiGap.Md)`＋`ui-group--nowrap`）・`NodeGraphView`（`challenge-viewport`・`challenge-canvas`。残りの高さいっぱい）。カテゴリ列はボタンが幅を越えたら横に送れ、越えなければバーを出さない（Web の `Group` の `flex-wrap: nowrap`・`overflow-x: auto`）。バーはゲームのスクロール領域と同じ `GameScrollView` のバー（太さ `--scrollarea-scrollbar-size`・溝 `--game-scroll-view-track-face`・つまみ `--game-scroll-view-thumb-face`）を本文の下の流れに置き、越えたときだけ列がバーの太さの分だけ高くなる。画面の USS は部品の内側のクラスを書かない。一覧の上（題・カテゴリ列）のホイールは装備・ホットバーへ抜けない | `ChallengeListViewTest`（L1）の 5 件が緑。並べ比べ（R-11） |
| R-6 | カテゴリのボタン: 各カテゴリに `FormButton("challenge-category-<guid>", UiText.FromContent(ContentLocalizationKeys.ChallengeCategoryName(guid)), …)`。選んだカテゴリは `Filled`、他は `Subtle`。押すと `IChallengeListCommands.SelectCategory(guid)` | `ChallengeListViewportTest`（押下で選択の操作が 1 回出て、選んだカテゴリが Filled）が緑 |
| R-7 | ノードカード: 根は `PaperSurface("challenge-node-<guid>")`（面 `--paper-face`・角丸 `--paper-radius`）に幅 240px・内側の余白 `--mantine-spacing-sm`・`translate: -50% -50%`、倍率は `style.scale = (UIScale.x, UIScale.y)`。中は名前（`text--md`＋`text--bold`）・概要（`text--sm`）・状態（`text--xs`。キーは VM の `StateLabelKey`＝`ui.challenge.stateLocked/stateCurrent/stateCompleted`）の縦積み。未解放は不透明度 0.55、進行中は `--mantine-color-blue-5`・完了は `--mantine-color-green-6` の 2px の輪（Web の outline を拾わない子で写す）。高さは中身なり（包みの高さ 0 で潰れない。S4-research の事後承認）。アンカー `challenge.node-<guid>`。指のカーソルは付けない（Web のカードに `cursor: pointer` もタップも無く、ビューポートの `cursor: grab` を継ぐ。`W/src/shared/treeView/TreeView.module.css:11`。設計書 A-191 のチャレンジの部分を改める） | `ChallengeListViewTest`「カードは中身なりの高さで…」が緑 |
| R-8 | 層と表示: 一覧は `OverlayPanel`/`Stage` 層。表示は `UiScreen.ChallengeList` のときだけ。進行中チャレンジ HUD（`Chrome` 層。S2-hud の持ち物）は一覧を開いても同じ位置・寸法のまま、一覧の上端とカテゴリのボタンより上に出て、中心の当たりが HUD 自身 | `ChallengeHudOverListTest`（L1。`challenge.spec.ts`「常駐HUDを…維持する」の一覧の部分）が緑 |
| R-9 | 常駐 HUD の画面を跨ぐ契約: ゲーム画面で測った HUD の位置・寸法のまま、インベントリ・チャレンジ一覧・研究・ビルドメニュー・ポーズ・配置モード・削除モードで出続け、上の安全帯のあるメニュー（インベントリ・一覧・ビルドメニュー・ポーズ）では中身より上、配置 HUD と横に分かれ、削除の上の帯より下。1024×576 でも同じ論理の位置で、インベントリより上 | 録画 `uitk-challenge-hud-resident` の `result.json` が `Success: true` |
| R-10 | アンカー: `challenge.panel`・`challenge.categories`・`challenge.node-<guid>` に印を付け、`TutorialAnchorCoveragePending` の S5-challenge の 3 行を同じ PR で消す | `TutorialAnchorCoverageTest` が緑。録画で先頭カテゴリの全ノード・枠・カテゴリ列のアンカーが Unique |
| R-11 | 並べ比べ: 参照 `5-challenge-list`（`web-8a585f742a`・`web-8a585f742a-2560x1080`）と、録画 `uitk-challenge-list` の中で撮った UITK（1280×720・2560×1080）を並べ、差の表を `done.md` に書く | 組が報告ページにあり、差の表の全行に「同じ／直した／LX-n／捨てる」のどれか |
| R-12 | 録画: 新しい `uitk-challenge-list`（T で開く・進行中へ中央寄せ・2560×1080 でも中央・アンカー・カードが中身なりの高さ・HUD が一覧より上・パンして T で閉じて T で開き直しても視点が残る・一覧の上の右短押しでは閉じない・Tab でインベントリ）と `uitk-challenge-hud-resident`（R-9） | 2 本の `result.json` が `Success: true`、録画 mp4、Note の `系=GameUi` |
| R-13 | 登録: `Client.Starter/Registration/GameUi/Screens/Challenge/ChallengeGameUiRegistration.cs`（直下は 10 ファイルで上限なのでサブディレクトリ）、`MainGameUiRegistration` の画面登録区画に 1 行、`GameUiTheme.tss` の `Screens` 区画に `@import` 1 行 | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`TutorialAnchorCoverageTest`）が緑 |
| R-14 | テストの名前: 写した 5 件に `<web-test>`。このレーンの持ち分（challenge.spec の 3 件・challengeLogic.test の 2 件）は処遇の行を足さない | `node tools/uitk/test-name-parity.mjs --bundle 5 --emit-missing` で持ち分の未対応 0。終了コードが 2 でない |
| R-15 | 記帳: 台帳の「## 実測の結論」の末尾にカードの高さの実測 1 行、「## 未撮影」にカテゴリ列の横バーの 1 行（見た目の例外は足さない。LX-200〜202 は並べ比べの差の予備）。設計書 §7.1 に入った A-240・A-241（と `GameScrollView` の横向きの A 行。番号は指揮が振る）を実装と照らし、食い違いは `done.md` の「integrator への依頼」へ | `done.md` に照らした結果。台帳の 2 行 |
| R-16 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → Editor を閉じる・bd を閉じる・`done.md` → `cmux wait-for -S uitk-s5-challenge` | `git log origin/feature/uitk` にこのレーンのマージコミット。`done.md` の 1 行目が「状態: 完了」 |
| R-17 | 部品 `GameScrollView` の横向き（Components の変更）: ctor を `GameScrollView(string testid, GameScrollAxis axis)` にし、`Horizontal` は横の `ScrollView`。バーは中身が見える幅を越えたときだけ本文の下の流れに出し、縦のバーは出さない。太さ・溝・つまみは縦と同じつまみ。縦の振る舞いは今のまま、呼び出し側 13 か所は `GameScrollAxis.Vertical` を明示する（デフォルト引数を作らない）。`Theme.uss` は変えない | `GameScrollViewHorizontalTest`（L1）の 2 件が緑。既存の `GameScrollViewTest`・`GameScrollViewKnobTest`・`GameScrollViewSpyPortTest`・`RecipeListScrollViewTest` と呼び出し側の画面の L1 が緑 |

**やらないこと（スコープ境界）**

- 部品（`NodeGraphView`・`PaperSurface`・`FormButton`・`LocalizedLabel`）と Foundation（`UiGroup`・`UiLayer`・`UiScreenTraits`・`TutorialAnchor`・`AnchorIds`・`UiPickingIgnore`・Tokens/DerivedTokens/Typography）は変えない（部品の変更は次の次の行の `GameScrollView` の横向きだけ）。足りなければ `done.md` の「integrator への依頼」に書く。`NodeGraphView.uss` は S4-research が直した形のまま使い、事後承認を記録するだけ
- 進行中チャレンジ HUD（`Client.Ui/Screens/Challenge/CurrentHud/`・`Client.Ui.State/Challenge/CurrentChallengeHud*`。S2-hud の持ち物）と `ChallengeProgressState` は触らない。HUD の L1 は既存の View を載せて見るだけ
- 画面の遷移（`ChallengeListState`・`GameScreenState` の T キー）とキーヒント（`ChallengeListStateHints`）は触らない（Client.Game。UI の系を読まない）
- ラダー `uitk-challenge-gate`（S5-ladder）・束 5 の `状態` の切り替え・`TutorialAnchorCoveragePending.cs` の削除（S5-ladder）は持たない
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は一切変えない
- 部品で変えるのは `GameScrollView` に横向きを足すことだけ（Task 2b。R-17）。画面だけの横スクロール部品は作らず、画面の USS は部品の内側のクラス（`unity-scroller__slider`・`unity-base-slider__*` など `__` 付き）を書かない（S5-crosscut T10 が `UiStyleConventionTest` に足す検査・A-167。D-7）。`Theme.uss` は変えない
- L3（EditModeInPlayingTest）は持たない（段の締め S5-ladder）

## Global Constraints

- **作業場所**: 指揮が `moores-wt new uitk-lane/s5-challenge --from origin/feature/uitk` で用意した worktree（Editor 要）。最初に `pwd` で worktree にいることを確かめる。略記: `S` = `moorestech_client/Assets/Scripts`、`W` = `moorestech_web/webui`、`SK` = `.agents/skills/unity-playmode-recorded-playtest`、`LOGS` = `../moorestech_logs`（この Mac mini では `../moorestech_logs`）、`REF` = `$LOGS/uitk/reference-captures`
- **Unity**: Mac mini で同時に動かす Unity Editor は全体で 1 本（このレーンの worktree の 1 本）。起動・停止の前に `moores-wt status` で所有を確かめ、他者の Editor を落とさない。2 本目が要る作業（並列のテスト・録画の並走）は Windows（RTX2060）の `~/bin/winunity`（`compile`・`test`・`play` を ssh で回し `~/.cache/winunity/runs/` へ回収。push 済みのブランチが前提）。subagent は Editor を起動しない（本体が起動して compile の疎通を見てから渡す）
- **実装**: moores-subagent-driven-development で、このホストの Claude Code の subagent（opus）が行う。subagent への依頼文には「バックグラウンドのポーリングで待たず全部前景で実行する」「区切りごとにコミットする」を書く
- **依存（マージ済み）**: C4-d（`NodeGraphView`）・C2-a（`PaperSurface`）・C2-b（`FormButton`）・S4-research（前例の画面・`NodeGraphView.uss` の修正 9a58258265）・S2-hud（`CurrentChallengeHudView`）
- **使う部品と公開 API**（`origin/feature/uitk` 9c83052273 の実物から写した。`S/` 相対）

```csharp
// ===== Components =====
namespace Client.Ui.Components.NodeGraph
{
    public sealed class NodeGraphView : VisualElement                                    // Components/NodeGraph/NodeGraphView/NodeGraphView.cs
    {
        public IObservable<string> OnNodeTapped { get; }                                   // :38（チャレンジは使わない）
        public IObservable<NodeGraphViewport> OnViewportChanged { get; }                   // :39 中央寄せ・パン・ズーム・寸法の追従のたびに流す
        public NodeGraphView(string testIdPrefix, IUiOsCursor osCursor);                   // :41 name = "<prefix>-viewport"、キャンバス "<prefix>-canvas"。:49 で TutorialAnchor.MarkClipContainer(this) 済み
        public void SetGraph(NodeGraphContent content);                                    // :66 注目点への中央寄せはインスタンスにつき 1 回だけ
        // RestoreViewport（:73）は使わない。並走の I-fix6（origin/uitk-lane/i-fix6 の 3614f5c0df。計画 R-15・D-3）が消し、視点は View の寿命で保つ形になる（D-1）
    }
    public sealed class NodeGraphContent { public NodeGraphContent(IReadOnlyList<NodeGraphNode> nodes, NodeGraphFocus initialFocus); }   // Graph/NodeGraphContent.cs:14
    public sealed class NodeGraphNode { public NodeGraphNode(string key, Vector2 treePosition, IReadOnlyList<string> previousKeys, VisualElement card); } // Graph/NodeGraphNode.cs:22 座標は Y 上向き。グラフに無い親のキー・重なったキーは LogError
    public readonly struct NodeGraphFocus { public static NodeGraphFocus None { get; } public static NodeGraphFocus At(Vector2 treePoint); } // Graph/NodeGraphFocus.cs:20-22
    public readonly struct NodeGraphViewport { public float X { get; } public float Y { get; } public float Scale { get; } public NodeGraphViewport(float x, float y, float scale); } // Viewport/NodeGraphViewport.cs:9-13
}
// NodeGraphView.uss（S4-research 後）: .node-graph { --node-graph-clip-inset: var(--tutorial-anchor-clip-inset); flex-grow 1; min-height 0; overflow hidden }・.node-graph__node { position absolute; width 0; align-items flex-start }・.node-graph__node > * { flex-shrink 0 }（カードの根で flex-shrink を書かない。C4-d D-23(1)）
namespace Client.Ui.Components.Panels
{
    public sealed class PaperSurface : VisualElement { public PaperSurface(string testid); } // Components/Panels/PaperSurface/PaperSurface.cs:15。name = testid・.paper-surface（面 var(--paper-face)・角丸 var(--paper-radius)。枠なし・文字色は継承）
}
namespace Client.Ui.Components.Controls
{
    public sealed class FormButton : Button                                                // Components/Controls/FormButton/FormButton.cs
    {
        public const string FilledUssClassName = "form-button--filled";                  // :22
        public const string SubtleUssClassName = "form-button--subtle";                  // :24
        public IObservable<Unit> OnClicked { get; }                                        // :30
        public FormButton(string testid, UiText label, FormButtonVariant variant);         // :32 高さ 36・左右 18・文言は LocalizedLabel（text--medium）
        public void SetVariant(FormButtonVariant variant);                                 // :62
    }
    public enum FormButtonVariant { Filled, Danger, Subtle }                               // FormButtonVariant.cs:7（Subtle は面透明・文字 --mantine-color-blue-light-color）
}
// ===== Foundation =====
namespace Client.Ui.Foundation.Layout
{
    public sealed class UiGroup : UiGapContainer { public UiGroup(UiGap gap); }            // Foundation/Layout/UiGroup.cs:19（横並び・縦中央・gap は子の margin。行頭行末に gap なし）
    public abstract class UiGapContainer : VisualElement { public void AddItem(VisualElement item); public void ClearItems(); } // UiGapContainer.cs:43・61
    public enum UiGap { None, Px2, Px4, Xs, Sm, Md, Lg, Xl }                               // UiGap.cs:9
    public static class UiLayoutClass { public const string GroupNoWrap = "ui-group--nowrap"; } // UiLayoutClass.cs:12
}
namespace Client.Ui.Foundation.Text
{
    public class LocalizedLabel : Label { public LocalizedLabel(); public void SetText(UiText text); } // LocalizedLabel.cs:30・46（言語の変化で解決し直す。text の頭に <line-height> を前置する）
    public static class UiTypographyClass { TextXs = "text--xs"; TextSm = "text--sm"; TextMd = "text--md"; TextBold = "text--bold"; TitleOrder2 = "title--order-2"; } // UiTypographyClass.cs:10-23
}
namespace Client.Ui.Foundation.Anchor { public static class TutorialAnchor { public static void Mark(VisualElement element, params string[] ids); } }   // TutorialAnchor.cs:14
namespace Client.Ui.Foundation.Input { public static class UiPickingIgnore { public static void ApplyToSubtree(VisualElement root); } }            // UiPickingIgnore.cs:15
namespace Client.Ui.Foundation.Input { public static class WebWheelScale { public const float PixelsPerNotch = 60f; } }                          // WebWheelScale.cs:11（GameScrollView.cs:39 が mouseWheelScrollSize に使う）
namespace Client.Ui.Components.Controls { public sealed class GameScrollView : ScrollView { public GameScrollView(string testid); } } // Components/Controls/GameScrollView/GameScrollView.cs（128 行。縦だけ。バーは本文に重なり、有無は流れの高さで決める :43-63。太さ --scrollarea-scrollbar-size 12px・溝 --game-scroll-view-track-face・つまみ --game-scroll-view-thumb-face。A-35・A-167）。Task 2b で ctor を (string testid, GameScrollAxis axis) にし横向きを足す
// 呼び出し側（起草時 13 か所。git grep -n "new GameScrollView("）: RecipeListScrollView:27・BlockInventoryNaturalScroll:20・MachineSectionView:72・TrainInventoryBodyView:37・BuildMenuPanelSkeleton:46・PauseMenuSubPageView:32・ItemListView:41・ResearchDetailPane:50・GameScrollViewGalleryEntry:20・テスト 4（GameScrollViewKnobTest:100・GameScrollViewSpyPortTest:25・GameScrollViewTest:31・EquipmentWheelFlowTest:96）
// UnityEngine.UIElements.ScrollView の horizontalScroller・contentViewport・contentContainer・ScrollTo(VisualElement)（Unity 組み込み。L1 が読む）。Theme.uss:105-134 の .unity-scroller の規則（縦向けの width 8px・溝 transparent・矢印 display none）は変えない
// Client.Ui.Screens.Inventory.Wheel.EquipmentWheelPickJudge.IsBlockingUi(VisualElement picked)（EquipmentWheelPickJudge.cs:12。装備・ホットバーのホイールの読み手 EquipmentWheelInput.cs:45 が使う。L1 が読むだけ）
namespace Client.Ui.Foundation.Input.OsCursor { public interface IUiOsCursor { } public sealed class UiOsCursorRequestStack : IUiOsCursor { } }
namespace Client.Ui.Foundation.Root { public sealed class GameUiRoot : MonoBehaviour { public VisualElement GetLayer(UiLayer layer, UiFamily family); } } // GameUiRoot.cs:48
namespace Client.Ui.Foundation.Layers { public sealed class UiLayerStack { public static UiLayerStack Build(VisualElement root); public VisualElement GetLayer(UiLayer layer, UiFamily family); } } // UiLayerStack.cs:25・57
// UiLayer.OverlayPanel（Web --z-stage-overlay-panel）・UiLayer.Chrome・UiFamily.Stage・UiFamily.Viewport（Client.Ui.Foundation.Layers・Client.Ui.Foundation.Stage）
// AnchorIds.ChallengePanel = "challenge.panel"・ChallengeCategories = "challenge.categories"・ChallengeNodePrefix = "challenge.node-"・ChallengeNode(string guid)（Client.Game.InGame.Tutorial.UIHighlight。AnchorIds.cs:24・25・43・59）
// ===== State =====
namespace Client.Ui.State.Text { public sealed class UiText { public UiText(LocalizationKey key); public static UiText FromContent(ContentLocalizationKey key); public static UiText FromVerbatim(string text); public string Resolve(); } } // UiText.cs:34・49・56・77
// ContentLocalizationKeys.ChallengeTitle(Guid)・ChallengeSummary(Guid)・ChallengeCategoryName(Guid)、LocalizationKeys.Ui.Challenge.Title・StateLocked・StateCurrent・StateCompleted（Mooresmaster.Localization.Generated。localization.csv 88-91 行に既存）
namespace Client.Ui.State.Challenge
{
    public sealed class ChallengeProgressState : IInitializable                           // Client.Ui.State/Challenge/ChallengeProgressState.cs（両系で登録済み）
    {
        public IReadOnlyList<ChallengeCategoryResponse> Categories { get; private set; }  // :21 初期は握手の応答、完了の知らせで全量を置き換える
        public IObservable<Guid> OnChallengeCompleted { get; }                             // :22 Categories を置き換えた後に流す
    }
}
// Client.Network.API.ChallengeCategoryResponse（Client.Network/API/Responses.cs:71）: Category（ChallengeCategoryMasterElement: CategoryGuid・Challenges〈ChallengeMasterElement: ChallengeGuid・PrevChallengeGuids・DisplayListParam.UIPosition〈Vector2〉・UIScale〈Vector3〉〉）・IsUnlocked・CurrentChallenges・CompletedChallenges
namespace Client.Ui.State.Routing
{
    public sealed class UiRoutingState { public IReadOnlyReactiveProperty<UiRoutingSnapshot> Current { get; } }   // UiRoutingState.cs:15
    public sealed class UiRoutingSnapshot { public UiScreen Screen { get; } }
    // UiScreen.ChallengeList。UiScreenTraitsTable の行は (grab F, pause F, skitInput T, backdrop T, alwaysOnHud T, trainHud F, collapse F, covers F)（UiScreenTraits.cs:60）
}
// ===== 前例（読むだけ）=====
// 画面: Client.Ui/Screens/Research/ResearchTreePresenter.cs・ResearchTreeView.cs・Parts/ResearchNodeCard.cs・Parts/ResearchNodeCardSet.cs
// State: Client.Ui.State/Research/Tree/ResearchSelectionStore.cs・ResearchSelection.cs・ResearchTreeModel.cs、Research/Commands/IResearchTreeCommands.cs、状態→文言キー Research/Logic/ResearchLogic.cs:75（DeriveNodeStateLabelKey → ResearchNodeCardViewModel.StateLabelKey）、表示判定 BlockInventory/Panel/BlockInventoryScreenVisibility.cs（X-audit DC-8 の <機能>ScreenVisibility.IsShown）、選択を型で分ける BlockInventory の ClosedBlockInventoryPanel : BlockInventoryPanelState、Client.Ui.State/BuildMenu/Session/BuildMenuSessionStore.cs
// 登録: Client.Starter/Registration/GameUi/Screens/ResearchGameUiRegistration.cs
// L1: Client.Tests/Ui/Screens/Research/ResearchTreeViewportTest.cs（Drag・EmptyBackgroundPoint・Settle）・ResearchScreenTestKit.cs、Client.Tests/Ui/Screens/Challenge/CurrentChallengeHudLayoutTest.cs（ThemeTokenFile.ResolvePixels）
// 録画: SK/scenarios/uitk/uitk-research-tree.cs・uitk-mode-hud-and-tooltip.cs・uitk-gameview-resolution-switch.cs
```

- **並走レーンとの照合**（Task 1 Step 1 で見て、Task 9 Step 2 の取り込みの後にもう一度見る）: (1) I-fix6（`origin/uitk-lane/i-fix6`）は `NodeGraphView.RestoreViewport` を消す。このレーンは使わない（D-1）。(2) S5-crosscut は `Foundation/Layout/UiDisplay`（`SetShown(VisualElement, bool)`。表示の唯一の書き口。R-4）を作る。あれば `ChallengeListView.SetShown` は `UiDisplay.SetShown(this, shown)` を使い、無ければ `style.display` の直書きのまま（S5-crosscut D-20 の置換対象に入る）。(3) S5-crosscut T10 は `UiStyleConventionTest` に「画面の USS が部品の内側のクラスを書かない」を足す。このレーンの USS は当たらない（D-7）。`$FILTER` の `Conventions` が見る。(4) S5-crosscut T14 の L3 `GameUiScreenMountTest`（全画面の View が載り先の層にあるか）。あれば一覧に `ChallengeListView` の行を足す（Task 5 Step 4）。無ければ `done.md` の「integrator への依頼」に書く
- **画面だけの部品の置き場**: `S/Client.Ui/Screens/Challenge/Parts/`（設計書 §2.7「1 feature だけが使うもの」。C4-d 計画の「チャレンジのノードカードは S5-challenge が `Screens/Challenge/Parts/` に作る」）
- **つまみの上書き**: Web の `features/challenge/style.module.css` に局所の上書き変数は無いので、画面 USS はつまみを宣言しない（カテゴリ列のバーは `GameScrollView` の既定のつまみのまま）。値は既存のトークン（`--menu-upper-safe-area`・`--mantine-color-dark-8`・`--mantine-spacing-md`・`--mantine-spacing-sm`・`--mantine-color-blue-5`・`--mantine-color-green-6`）を `var()` で引く。`--paper-face`・`--paper-radius` は上書きしない（Web は Paper の既定のまま）
- **USS**: 色リテラル・`z-index`・構造擬似クラス・合成太字は書かない（`UiStyleConventionTest`）。`opacity` の数値・寸法の px は可。Web の CSS を写した行は `/* web: … */` の出所を付ける
- **コード**: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下・`partial` 禁止・`Func<>` 禁止・イベントは UniRx・try-catch 禁止・主要な処理に日本語→英語の 2 行コメント・`#region Internal` はメソッド内のローカル関数だけ・null チェックは外部データだけ・黙って続行する分岐には `silent-ok: <理由> / <誰がどこで気づくか>`
- **全体スイッチ**: Editor で GameUi を動かすときは PlayMode を止めた状態で `uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'` を実行してから起動・録画する。`run-scenario.sh` が初回 "already playing" で終わったら 1 回だけ投入し直す（bd moorestech-trfka）
- **参照キャプチャ**: `REF/web-8a585f742a/5-challenge-list.png`（1280×720）・`REF/web-8a585f742a-2560x1080/5-challenge-list.png`（2560×1080。座標は `x' = 320 + 1.5x`・`y' = 1.5y`）。状態の出所は `W/e2e/uitk-reference/referenceShots.ts:114`（controls: topic `challengeActive`・ui `ChallengeList`、steps visible `challenge-panel`）。mock-host のデータ（`W/e2e/mock-host/fixtures/presentationFixtures.ts` の challengeTree: Basics〈First Craft 完了 (0,0)・Second Step 進行中 (220,0)〉・Logistics）なので、UITK の撮影（実マスタ）とはカテゴリ数・名前・ノード数が違う。差の表はデータの違いを「捨てる」とし、位置・寸法・色・装飾・段組を比べる。参照の実測（1280×720）: 一覧の上端 y=168、地 (31,31,31)、題の上端 y≈184、ボタンの列 y=219〜255（高さ 36）、Basics は x=16〜96、Logistics の字頭 x≈130、ビューポートの中心 (640, 479.5)、カードは幅 240・高さ約 86（面 (36,36,36)）、完了の輪 (64,192,87)・進行中の輪 (51,154,240) が 2px、2 枚は隣接し後のカードの輪が前のカードの上に重なる
- **並べ比べの画像の原本**は worktree の gitignore 下 `moorestech_client/.uloop/outputs/compare/S5-challenge/` に置き、`LOGS` へ入れない。`LOGS` へ書く前に `LOGS/README.md` を読む
- **報告**: `LOGS/harness/uitk/S5-challenge/done.md`（1 行目「状態: 完了」。止まるときは「状態: 停止（理由）」）。節は「証拠」「差の表」「要裁定」「integrator への依頼」「未確認」
- **共有区画は追記だけ**: `GameUiTheme.tss` の `/* == section: Screens == */`（末尾に 1 行）・`MainGameUiRegistration.cs` の画面登録区画（末尾に 1 行）と `using` の 1 行・`docs/uitk/TODO.md` の「## 実測の結論」と「## 未撮影」の末尾（と、並べ比べで LX-200〜202 の予備を使うときだけ「## 見た目の例外」の末尾）。衝突は両方の行を採る

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 注目点 7 件のうち 6 件を反映（横スクロールを画面の部品で写す・状態の文言キーを State へ・Commands と View の置き場・死活表 4 行）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強2・弱2・第3バケツ3 / 選択の二重を畳み表示判定を 1 本に、カーソルと開き直しは Web・裁定どおりで A-191 を改める。第3バケツはレビュー依頼文へ

## 死活表（Phase 2.5）

全体スイッチ UITK（`UiInputExclusivity.ActiveUiSystem == GameUi`）で起動したとき、Web の系で動いていたチャレンジ一覧の振る舞いが生きるか。Web の橋（`ChallengeTreeTopic`）は Web の系でだけ登録され、源の `ChallengeProgressState` は両系で登録済み（`MainGameUiStateRegistration`）。

| 振る舞い | GameUi で生きるか | 根拠 |
|---|---|---|
| T キーで一覧を開く・T／ESC で閉じる・Tab でインベントリへ | 生きる（このレーンの外） | `GameScreenState`・`ChallengeListState` の遷移は Client.Game で UI の系を読まない。View は経路（`UiRoutingState`）で `SetShown` するだけ（Task 5） |
| 一覧の外（上の 168px の世界）の右短押しで閉じる・一覧の上では閉じない | 生きる | `ChallengeListState` が `UiPointerHitTest.IsPointerOverAnyUi` で判定（既存）。一覧の根と `NodeGraphView` は拾う（Task 3）。録画の流れで一覧の上の右短押しで閉じないことを見る（Task 6） |
| 解放済みのカテゴリのボタン列と選択 | 生きる | `ChallengeListViewModelFactory`（`IsUnlocked` だけ）→ `ChallengeCategoryButtons`（Task 1・3） |
| カテゴリ列の横スクロール（Web `overflow-x: auto`。ボタンが幅を越えたら端のボタンへ送れる） | 生きる | `GameScrollView` の横向き（Task 2b。越えたときだけ流れの中のバー）を `ChallengeCategoryButtons` が使う（Task 3）。L1 `ChallengeListViewTest`「カテゴリのボタンが幅を越えたら…」が送れることとバーの有無を固定する |
| 一覧の後ろの暗幕（traits の backdrop=T） | 生きる（このレーンの外） | `Screens/Shell/ScreenBackdropPresenter.cs:37-45` が経路ごとに `UiScreenTraitsTable.Get(..).Backdrop` を読んで `UiLayer.Backdrop` へ出す。`ChallengeList` の行は Backdrop=T（`UiScreenTraits.cs:60`） |
| 一覧を開いている間のスキット入力（traits の skitInput=T） | 生きる（このレーンの外） | `Screens/Skit/SkitScreenPresenter.cs:55-65` が経路ごとに `SkitInput` 列を読む。スキットは `UiLayer.SkitStage` で一覧（`OverlayPanel`）より上の層（`UiLayer` の並び。Web `--z-stage-skit` ＞ `--z-stage-overlay-panel`） |
| 一覧を開いている間のプレイヤー移動とカメラ | 生きる（このレーンの外。両系で同じ） | `ChallengeListState`（Client.Game）は `LocksPlayerMovement` が false で移動を止めない（`UIStateControl.cs:123` が読む）。`OnEnter` の `InputManager.MouseCursorVisible(true)` でカーソルを出し、見回しはカーソルのロックと対（`PlayerCameraInteractionApplier.cs:20`）。どれも UI の系を読まない |
| グラフの外（題・カテゴリ列）でのホイール | 生きる（装備・ホットバーへ抜けない） | ホイールの読み手 `EquipmentWheelInput.cs:45` はパネルで拾った要素を `EquipmentWheelPickJudge.IsBlockingUi` に通し、`WheelPassthroughMark` の付かない UI の上なら `EquipmentWheelGate` が止める（`ChallengeList` は traits の covers=F なので層では止まらず、UI の上かで決まる）。一覧の根・題・カテゴリ列は拾い、印を付けない。L1 `ChallengeListViewTest`「一覧の題とカテゴリ列の上のホイールは…」が題・カテゴリ列・空の背景で `IsBlockingUi` が真を固定する。グラフの上のホイールは `NodeGraphView` のズーム（C4-d） |
| カテゴリの初回訪問だけ進行中へ寄せ、再訪・開き直しで前回の視点（裁定 2026-11-04） | 生きる | 訪れたカテゴリごとの `NodeGraphView` を控えて付け替える `ChallengeCategoryGraphHost`・`ChallengeCategoryGraph`（Task 3。部品は中央寄せをインスタンスにつき 1 回だけ行う。L1 は Task 4） |
| パン・ズーム・慣性 | 生きる | C4-d の `NodeGraphView`（部品の L1 は C4-d 済み） |
| 完了の知らせで状態が変わる（表示中） | 生きる | `ChallengeProgressState.OnChallengeCompleted` → `ChallengeListModel` が組み直す（Task 2）。閉じている間の変化は次の突入で組む |
| カードの 3 状態・輪・減光・中身なりの高さ | 生きる | `ChallengeNodeCard`・`ChallengeList.uss`（Task 3。L1） |
| チュートリアルの強調（`challenge.panel`・`challenge.categories`・`challenge.node-<guid>`） | S2-tutorial の描き手で生きる | 印を付け（Task 3）、`TutorialAnchorCoveragePending` の 3 行を消す。切り口の印は `NodeGraphView` が自分で付けている（`NodeGraphView.cs:49`） |
| 進行中チャレンジ HUD が一覧に遮られない・一覧の上に出る | 生きる | HUD は `Chrome` 層（上）、一覧は `OverlayPanel` 層。L1（Task 4）と録画（Task 6） |
| 一覧の中でホットバー・キーヒントが出る | 生きる（持ち主は各 HUD） | `UiScreenTraits.AlwaysOnHud` が `ChallengeList` で true。描くのは S3-hotbar・S2-hud（参照にも写る） |
| 言語の切り替え | 生きる | 文言はすべて `LocalizedLabel`（`FormButton` の中も）で、自分で解決し直す。Model は言語で組み直さない（D-12） |

---

## 申し送りの処遇

`LOGS/harness/uitk/*/done.md` と `conductor.md` を `S5-challenge\|チャレンジ一覧\|ChallengePanel` で grep した、このレーン宛ての依頼の全件。

| 出所 | 依頼（要旨） | 処遇 |
|---|---|---|
| conductor.md:126・131（11-11 05:37 節）・S4-research/done.md:91 | `NodeGraphView.uss` の `.node-graph__node { height: 0 }` 削除を事後承認する。チャレンジのカードも同じ故障を受けるはず | 受ける。カードが中身なりの高さ（上下の余白 24＋3 行）で描かれることを `ChallengeListViewTest`（Task 3）で固定し、承認を D-4 と設計書 A-241 の本文案に残す。部品の USS は変えない |
| C4-d 計画 L100-104 の申し送り (1)・D-22 | 視点の保持は画面の State の Session（素の値）に置き、`OnViewportChanged` の最後の値を控えて、作り直した `NodeGraphView` へ載せる前に `RestoreViewport` で渡す（チャレンジはカテゴリごとに 1 つ） | 形を替えて受ける（D-1）。並走の I-fix6 が `RestoreViewport` を消し、視点を View の寿命で保つ形にした（次の行）ので、State の Session は作らず、訪れたカテゴリごとの View を画面が控えて付け替える。「カテゴリごとに 1 つの視点」は View 1 本ずつで満たす |
| I-fix6 計画 R-15・D-3（`origin/uitk-lane/i-fix6` の 3614f5c0df。push 済み・未マージ） | `NodeGraphView.RestoreViewport` を消し、視点の保持は View の寿命で持つ（研究は起動時に 1 回作った View を出し入れするだけで Web と同じ寿命） | 合わせる（D-1）。このレーンは `RestoreViewport` を呼ばないので、I-fix6 とどちらが先に `feature/uitk` へ入っても壊れない。Task 1 Step 1 で `RestoreViewport` の有無を見るのは記録のためだけ（有っても使わない） |
| C4-d 計画 申し送り (2)・D-5 | グラフに無い親のキーは LogError なので、別カテゴリの前提のキーを `PreviousKeys` から除く（Web は黙って省く） | 受ける（D-3。`ChallengeCategoryViewModel.Create`。L0 で固定） |
| C4-d 計画 D-23(1)・L100 | カードの根で `flex-shrink` を書かない。カードの根は `PickingMode.Position`。中心寄せ `translate(-50%,-50%)` と `scale(UIScale)` はカード側 | 受ける（Task 3 の USS と `ChallengeNodeCard`） |
| C4-d/done.md:67 | カードの指カーソルは `new UiOsCursorHoverRequest(card, osCursor, UiOsCursorShape.Pointer, UiOsCursorLayer.Element)` で当てる | 適用しない。Web の `ChallengeNodeCard` は `cursor: pointer` を持たず（`features/challenge/style.module.css` に cursor が無い）、`TreeView` に `onNodeTap` も渡さないので、ビューポートの `cursor: grab`（`W/src/shared/treeView/TreeView.module.css:11`）を継ぐ。研究のカードだけの申し送り。設計書 A-191 の「研究・チャレンジのカードの指は画面が当てる」のチャレンジの部分を改める（A-241 の本文案に足す。D-5） |
| S2-hud/done.md:135・PLAN-W2a/done.md:27 | `challenge.spec.ts` の「常駐HUDをインベントリ・メニュー・操作モードで維持する」と画面 2 件は S5-challenge が写す | 受ける。一覧の部分は L1（`ChallengeHudOverListTest`）に `<web-test>` を付け、画面を跨ぐ部分は録画 `uitk-challenge-hud-resident`（D-9） |
| C4-d/done.md（参照の実測） | `5-challenge-list.png` の地 (460,280)=#1f1f1f、2 枚のカードは隣接し線は隠れる | 並べ比べの前提として Global Constraints の参照の実測に入れた（Task 7） |
| S4-research 計画 D-9 | `NodeGraphView` の切り口に `TutorialAnchor.MarkClipContainer` を付ける（2 機能が使う） | 済み（`NodeGraphView.cs:49`）。このレーンは何もしない |
| I-fix5 計画 10i | `NodeGraphView.uss` の事後承認の申し送り（文書項目） | 上の 1 行目と同じ。I-fix5 が文書を直しても、このレーンの L1 と A-241 の本文案はそのまま |

## File Structure

| パス | 責務 | 新規／変更 |
|---|---|---|
| `S/Client.Ui.State/Challenge/ChallengeScreenVisibility.cs` | 一覧が出ているか（経路が `ChallengeList`。X-audit DC-8 の形） | 新規 |
| `S/Client.Ui.State/Challenge/Tree/ChallengeNodeState.cs` | ノードの 3 状態 | 新規 |
| `S/Client.Ui.State/Challenge/Tree/ChallengeNodeCardViewModel.cs` | カード 1 枚の表示（GUID・座標・倍率・前提・状態・状態の文言キー）とグラフの置き方の比較 | 新規 |
| `S/Client.Ui.State/Challenge/Tree/ChallengeCategoryViewModel.cs` | カテゴリ 1 つ（同じカテゴリの前提だけ残す・注目点・グラフの形の比較） | 新規 |
| `S/Client.Ui.State/Challenge/Tree/ChallengeListViewModel.cs` | 一覧の表示の基底（空か選択ありの 2 つ。選んだカテゴリを決める） | 新規 |
| `S/Client.Ui.State/Challenge/Tree/EmptyChallengeListViewModel.cs` | 解放済みのカテゴリが無い一覧（選択を持たない） | 新規 |
| `S/Client.Ui.State/Challenge/Tree/SelectedChallengeListViewModel.cs` | カテゴリの列と、必ずある選んだカテゴリ | 新規 |
| `S/Client.Ui.State/Challenge/Tree/ChallengeListLogic.cs` | 注目点（Web `challengeLogic.ts`）・状態の文言キー（Web `resolveChallengeStateKey`）・Guid の並びの比較 | 新規 |
| `S/Client.Ui.State/Challenge/Tree/ChallengeListViewModelFactory.cs` | 応答の列 → ViewModel（Web `ChallengeDtoBuilder.BuildTree` と同じ規則） | 新規 |
| `S/Client.Ui.State/Challenge/Tree/ChallengeListModel.cs` | 表示中だけ組み直して配る | 新規 |
| `S/Client.Ui.State/Challenge/Tree/Selection/ChallengeCategorySelection.cs` | カテゴリの選択（無いことを型で持つ） | 新規 |
| `S/Client.Ui.State/Challenge/Tree/Selection/ChallengeCategorySelectionStore.cs` | 選択の持ち主（一覧を離れたら無し） | 新規 |
| `S/Client.Ui.State/Challenge/Commands/IChallengeListCommands.cs` | 画面からの操作（カテゴリを選ぶ） | 新規 |
| `S/Client.Ui.State/Challenge/Commands/ChallengeListCommands.cs` | 操作の実装（選択ストアへ） | 新規 |
| `S/Client.Ui/Screens/Challenge/Tree/ChallengeList.uss` | 一覧の枠・カテゴリ列と横のバー・カードの見た目 | 新規 |
| `S/Client.Ui/Screens/Challenge/Tree/ChallengeListView.cs` | 一覧の枠（題・カテゴリ列・グラフの置き場）。空か選択ありかの分岐の 1 か所 | 新規 |
| `S/Client.Ui/Screens/Challenge/Tree/ChallengeListPresenter.cs` | 1 回載せ、経路で表示、Model で描く | 新規 |
| `S/Client.Ui/Screens/Challenge/Parts/ChallengeNodeCard.cs` | カード 1 枚（`PaperSurface` の根の持ち手） | 新規 |
| `S/Client.Ui/Screens/Challenge/Parts/ChallengeNodeCardSet.cs` | カードを GUID で使い回し、グラフの中身を組む | 新規 |
| `S/Client.Ui/Screens/Challenge/Parts/ChallengeCategoryButtons.cs` | カテゴリのボタン列 | 新規 |
| `S/Client.Ui/Screens/Challenge/Parts/ChallengeCategoryGraphHost.cs` | 訪れたカテゴリの View を控え、表示するカテゴリの View を置き場へ付け替える | 新規 |
| `S/Client.Ui/Screens/Challenge/Parts/ChallengeCategoryGraph.cs` | カテゴリ 1 つの `NodeGraphView`・カードの組・最後に渡した中身（視点は View の寿命） | 新規 |
| `S/Client.Ui/Components/Controls/GameScrollView/GameScrollAxis.cs` | `GameScrollView` の向き（縦・横） | 新規（Components） |
| `S/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs`・`GameScrollView.uss` | ctor に向き。横はバーを本文の下の流れに置く。縦の規則を `.unity-scroller--vertical` に絞る | 変更（Components） |
| `new GameScrollView(` の呼び出し側 13 か所（Task 2b の Files） | `GameScrollAxis.Vertical` を足す | 変更 |
| `S/Client.Tests/Ui/Components/Controls/GameScrollViewAxis/GameScrollViewHorizontalTest.cs` | L1（横向き 2 件） | 新規 |
| `S/Client.Starter/Registration/GameUi/Screens/Challenge/ChallengeGameUiRegistration.cs` | 登録 | 新規 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | `using` 1 行・画面登録区画 1 行 | 変更 |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | `Screens` 区画に `@import` 1 行 | 変更 |
| `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` | S5-challenge の 3 行を消す（表は空） | 変更 |
| `S/Client.Tests/Ui/State/Challenge/ChallengeListLogicTest.cs` | L0（Web 2 件） | 新規 |
| `S/Client.Tests/Ui/State/Challenge/ChallengeListViewModelFactoryTest.cs` | L0（工場 3 件・グラフの形の比較 1 件） | 新規 |
| `S/Client.Tests/Ui/State/Challenge/ChallengeListModelTest.cs`・`ChallengeCategorySelectionStoreTest.cs`・`ChallengeProgressStateTestSource.cs` | L0（Model 4 件・選択 1 件）と、テスト mod の 2 カテゴリで源を組む補助 | 新規 |
| `S/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeListScreenTestKit.cs` | L1 の共通材料 | 新規 |
| `S/Client.Tests/Ui/Screens/Challenge/Tree/RecordingChallengeListCommands.cs` | 操作の記録 | 新規 |
| `S/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeListViewTest.cs` | L1（翻訳済み表示・カードの見た目・空・カテゴリ列の横スクロール・ホイールの行き先） | 新規 |
| `S/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeListViewportTest.cs` | L1（中央寄せとカテゴリ切替・再訪・開き直し） | 新規 |
| `S/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeHudOverListTest.cs` | L1（常駐 HUD と一覧） | 新規 |
| `SK/scenarios/uitk/stage5/uitk-challenge-list.cs` | 録画（画面と比較の撮影） | 新規 |
| `SK/scenarios/uitk/stage5/uitk-challenge-hud-resident.cs` | 録画（HUD の画面を跨ぐ契約） | 新規 |
| `docs/uitk/TODO.md` | 「## 実測の結論」の末尾に 1 行 | 変更（追記） |
| `LOGS/harness/uitk/S5-challenge/done.md` | 作業報告 | 新規（moorestech_logs） |

ディレクトリのファイル数（.cs・.uss を数える）: `Client.Ui.State/Challenge/` 直下 4（既存 3＋`ChallengeScreenVisibility`）・`Challenge/Tree/` 9・`Tree/Selection/` 2・`Challenge/Commands/` 2・`Screens/Challenge/` 直下 0（`CurrentHud/`・`Tree/`・`Parts/` の 3 つのサブディレクトリだけ）・`Screens/Challenge/Tree/` 3・`Parts/` 5（カード・カードの組・ボタン列・グラフの置き場・カテゴリ 1 つのグラフ）・`Components/Controls/GameScrollView/` 3・`Client.Tests/Ui/Components/Controls/GameScrollViewAxis/` 1（`Controls/` 直下は既に 10）・`Registration/GameUi/Screens/Challenge/` 1（直下は既に 10）・`Client.Tests/Ui/State/Challenge/` 5（`Ui/State/` 直下は既に 10）・`Client.Tests/Ui/Screens/Challenge/Tree/` 5。どれも 10 以下。名前空間に `List` を使わない（一覧の View の置き場は `Screens/Challenge/Tree/`。`List` だと `System.Collections.Generic.List` と読み違える。State の `Challenge.Tree`・L1 の `Screens.Challenge.Tree`・研究の `Research.Tree` と同じ名前になり、HUD の `CurrentHud/` と並ぶサブディレクトリになる。D-16）。

**写すテスト（5 件）**

| Web | 件 | NUnit | 層 |
|---|---|---|---|
| `src/features/challenge/challengeLogic.test.ts`「進行中(current)のノードを返す」「進行中が無ければ寄せない」 | 2 | `ChallengeListLogicTest` の 2 件 | L0 |
| `e2e/tests/challenge.spec.ts`「チャレンジ画面が開きツリーだけを翻訳済み表示する」 | 1 | `ChallengeListViewTest.チャレンジ画面が開きツリーだけを翻訳済み表示する` | L1 |
| `e2e/tests/challenge.spec.ts`「チャレンジ画面は進行中のチャレンジを中央にして開き、カテゴリ切替でもその進行中へ寄せる」 | 1 | `ChallengeListViewportTest.進行中のチャレンジを中央にして開きカテゴリ切替でもその進行中へ寄せる` | L1 |
| `e2e/tests/challenge.spec.ts`「常駐HUDをインベントリ・メニュー・操作モードで維持する」 | 1 | `ChallengeHudOverListTest.常駐HUDはチャレンジ一覧を開いても同じ位置のまま一覧とカテゴリより上に出る`（一覧の部分）＋録画 `uitk-challenge-hud-resident`（画面を跨ぐ部分） | L1＋録画 |

タグの付かない L1/L0（裁定・事後承認・工場・死活表）: `ChallengeListViewTest` 4 件・`ChallengeListViewportTest` 2 件・`ChallengeListViewModelFactoryTest` 4 件・`ChallengeListModelTest` 4 件・`ChallengeCategorySelectionStoreTest` 1 件。`challenge.spec.ts` の残り 4 件（HUD）は S2-hud が `CurrentChallengeHudStateTest` で写し済み。

テストのフィルタ（以後 `$FILTER`）: `^Client\.Tests\.Ui\.(State\.Challenge|Screens\.Challenge\.Tree|Components\.Controls|Anchor|Conventions|Theme|Layers|Screens\.Challenge\.Current)`（`Components\.Controls` は `GameScrollView` の縦横の L1）

---

## Tasks

### Task 1: 前提の確認・記録の準備・State の ViewModel と論理（L0）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeNodeState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeNodeCardViewModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeCategoryViewModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeListViewModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/EmptyChallengeListViewModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/SelectedChallengeListViewModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeListLogic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeListViewModelFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/Selection/ChallengeCategorySelection.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge/ChallengeListLogicTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge/ChallengeListViewModelFactoryTest.cs`
- Create: `LOGS/harness/uitk/S5-challenge/done.md`（雛形）

**Interfaces:**
- Consumes: `origin/feature/uitk`（C4-d・C2-a・C2-b・S4-research・S2-hud マージ済み）、Global Constraints の「使う部品と公開 API」
- Produces:
  - `enum ChallengeNodeState { Locked, Current, Completed }`（`Client.Ui.State.Challenge.Tree`）
  - `ChallengeNodeCardViewModel(Guid guid, Vector2 treePosition, Vector2 scale, IReadOnlyList<Guid> previousGuids, ChallengeNodeState state)`・`Guid`・`TreePosition`・`Scale`・`PreviousGuids`・`State`・`LocalizationKey StateLabelKey`（ctor が `ChallengeListLogic.DeriveStateLabelKey(state)` で引く）・`bool HasSameGraphPlacementAs(ChallengeNodeCardViewModel other)`
  - `ChallengeCategoryViewModel.Create(Guid guid, IReadOnlyList<ChallengeNodeCardViewModel> nodes)`・`Guid`・`Nodes`・`bool TryGetInitialFocus(out Vector2 treePoint)`・`bool HasSameGraphAs(ChallengeCategoryViewModel other)`
  - `abstract class ChallengeListViewModel`・`static ChallengeListViewModel Empty`・`static ChallengeListViewModel Create(IReadOnlyList<ChallengeCategoryViewModel> categories, ChallengeCategorySelection selection)`（カテゴリ 0 なら `Empty`、それ以外は `SelectedChallengeListViewModel`）
  - `sealed class EmptyChallengeListViewModel : ChallengeListViewModel`（メンバーなし）
  - `sealed class SelectedChallengeListViewModel : ChallengeListViewModel`・`IReadOnlyList<ChallengeCategoryViewModel> Categories`・`IReadOnlyList<Guid> CategoryGuids`・`ChallengeCategoryViewModel Selected`（null にならない）
  - `ChallengeListLogic.TryFindInitialFocusNode(IReadOnlyList<ChallengeNodeCardViewModel> nodes, out ChallengeNodeCardViewModel focusNode)`・`ChallengeListLogic.DeriveStateLabelKey(ChallengeNodeState state)`・`ChallengeListLogic.SameGuidSequence(IReadOnlyList<Guid> left, IReadOnlyList<Guid> right)`
  - `ChallengeListViewModelFactory.Create(IReadOnlyList<ChallengeCategoryResponse> responses, ChallengeCategorySelection selection)`
  - `ChallengeCategorySelection.None`・`ChallengeCategorySelection.Of(Guid)`・`bool Is(Guid)`（`Client.Ui.State.Challenge.Tree.Selection`）
  - bd のレーン ID（以下 `<bd>`）、`done.md` の雛形

- [ ] **Step 1: 作業場所と部品の実物を確かめる**

Run:
```bash
pwd
git log -1 --format='%H %s' origin/feature/uitk
S=moorestech_client/Assets/Scripts
grep -n "public NodeGraphView(\|public void SetGraph(\|OnViewportChanged =>\|MarkClipContainer" $S/Client.Ui/Components/NodeGraph/NodeGraphView/NodeGraphView.cs
grep -c "public void RestoreViewport(" $S/Client.Ui/Components/NodeGraph/NodeGraphView/NodeGraphView.cs   # 記録だけ（I-fix6 が入ると 0。このレーンは使わない）
grep -n "height" $S/Client.Ui/Components/NodeGraph/NodeGraphView/NodeGraphView.uss
grep -n "public PaperSurface(" $S/Client.Ui/Components/Panels/PaperSurface/PaperSurface.cs
grep -n "public FormButton(\|FilledUssClassName =\|SubtleUssClassName =\|public void SetVariant" $S/Client.Ui/Components/Controls/FormButton/FormButton.cs
grep -n "public UiGroup(\|GroupNoWrap" $S/Client.Ui/Foundation/Layout/UiGroup.cs $S/Client.Ui/Foundation/Layout/UiLayoutClass.cs
grep -n "ChallengePanel\|ChallengeCategories\|ChallengeNodePrefix\|ChallengeNode(" $S/Client.Game/InGame/Tutorial/UIHighlight/AnchorIds.cs
grep -n "S5-challenge" $S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs
grep -n "ChallengeList" $S/Client.Ui.State/Routing/UiScreen.cs $S/Client.Ui.State/Routing/UiScreenTraits.cs
grep -n "public IReadOnlyList<ChallengeCategoryResponse> Categories\|OnChallengeCompleted =>" $S/Client.Ui.State/Challenge/ChallengeProgressState.cs
grep -n "ChallengeProgressState" $S/Client.Starter/Registration/*.cs
grep -n "DisplayListParam.UIPosition\|DisplayListParam.UIScale" $S/Client.WebUiHost/Game/Topics/Challenge/ChallengeDtoBuilder.cs
grep -n "^ui\.challenge\.\(title\|stateLocked\|stateCurrent\|stateCompleted\)," Localization/localization.csv
grep -n "section: Screens" "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
grep -n "menu-upper-safe-area:\|tutorial-anchor-clip-inset:" $S/Client.Ui/Foundation/Theme/Tokens.uss
grep -n "mantine-color-dark-8:\|mantine-color-blue-5:\|mantine-color-green-6:\|mantine-color-body:\|mantine-spacing-sm:\|mantine-spacing-md:" $S/Client.Ui/Foundation/Theme/MantineTokens.uss
grep -rn "\-\-paper-face\|\-\-paper-radius" $S/Client.Ui/Foundation/Theme/ | head -4
grep -n "public GameScrollView(\|ScrollViewMode" $S/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs
git grep -n "new GameScrollView(" -- $S | wc -l
ls $S/Client.Ui/Foundation/Layout/UiDisplay.cs 2>/dev/null; grep -n "public static void SetShown" $S/Client.Ui/Foundation/Layout/UiDisplay.cs 2>/dev/null
find $S/Client.Tests -name "GameUiScreenMountTest.cs"
grep -n "__\|inner" $S/Client.Tests/Ui/Conventions/UiStyleConventionTest.cs | head -5
grep -n "scrollarea-scrollbar-size:\|game-scroll-view-track-face:\|game-scroll-view-thumb-face:" $S/Client.Ui/Foundation/Theme/DerivedTokens.uss
grep -n "PixelsPerNotch" $S/Client.Ui/Foundation/Input/WebWheelScale.cs
grep -n "public static bool IsBlockingUi" $S/Client.Ui/Screens/Inventory/Wheel/EquipmentWheelPickJudge.cs
grep -n "^using Mooresmaster.Localization.Generated" $S/Client.Ui.State/Research/Tree/ResearchNodeCardViewModel.cs
ls $S/Client.Starter/Registration/GameUi/Screens/*.cs | wc -l
ls $S/Client.Tests/Ui/State/*.cs | wc -l
ls -d SK/scenarios/uitk/*/ 2>/dev/null; ls .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/ | head -40
```
Expected: worktree の絶対パス。`NodeGraphView.cs` の 4 行（:39/:41/:49/:66 付近）と `RestoreViewport` の数（1 なら I-fix6 未取り込み・0 なら取り込み済み。どちらでも進む。`done.md` の「証拠」に数を書く）、`NodeGraphView.uss` に `height` の行が無い（S4-research の修正後）、`PaperSurface(string testid)`、`FormButton(string testid, UiText label, FormButtonVariant variant)` と 2 つの定数と `SetVariant`、`UiGroup(UiGap gap)` と `GroupNoWrap`、AnchorIds の 4 行、Pending の 3 行、`ChallengeList` の 2 行、`Categories` と `OnChallengeCompleted`、`MainGameUiStateRegistration` の登録、`UIPosition`・`UIScale` の 4 行、csv の 4 行、`section: Screens`、トークン 2 つ・6 つ・`--paper-*` の定義、`GameScrollView(string testid)` と `ScrollViewMode.Vertical`（横向きがまだ無い。あれば Task 2b は実物の口に合わせて読み替える）・呼び出し 13・バーのつまみ 3 つの定義、`PixelsPerNotch = 60f`、`IsBlockingUi`、研究の VM の `using Mooresmaster.Localization.Generated`（`LocalizationKey` の名前空間）、`Registration/GameUi/Screens/*.cs` が 10、`Ui/State/*.cs` が 10、`scenarios/uitk/` の下の段のサブディレクトリの有無
- どれかの依存が無ければ止め、`done.md` を「状態: 停止（<依存> 未取り込み）」にして指揮へ報告する
- 名前が違えば以後の計画を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <旧> → <実物>」と書く
- `NodeGraphView.uss` に `height: 0` が戻っていたら止めて指揮へ（S4-research の修正が消えている）
- 並走レーンとの照合（Global Constraints）の結果を `done.md` の「証拠」に 1 行で書く: `RestoreViewport` の数・`UiDisplay.SetShown` の有無・`GameUiScreenMountTest.cs` の有無・`UiStyleConventionTest` の内側クラスの検査の有無。`UiDisplay` があれば Task 3 Step 4 の `SetShown` を `UiDisplay.SetShown(this, shown)` で書く。`GameUiScreenMountTest` があれば Task 5 Step 4 で行を足す
- `scenarios/uitk/` がすでに段ごとのサブディレクトリへ移っている（I-fix5 の 10n が先に入った）なら、そこに `stage5/` が無ければ作る。以後の録画のパスは `SK/scenarios/uitk/stage5/` のまま

- [ ] **Step 2: bd を積み、done.md の雛形を置く**

Run:
```bash
bd create "UITK S5-challenge: チャレンジ一覧（カテゴリのボタン列・ノードカード・視点の保持）を UITK へ" --parent moorestech-albof -t task -p 2
bd update <bd> --claim
mkdir -p ../moorestech_logs/harness/uitk/S5-challenge
```
`done.md` に次を書く（1 行目は終わるまで「状態: 作業中」）:
```markdown
状態: 作業中

# S5-challenge 作業報告

## 証拠
## 差の表
## 要裁定
なし
## integrator への依頼
- 設計書 §7.1 に入った A-240・A-241（と GameScrollView の横向きの A 行）を実装と照らした結果（計画 docs/superpowers/plans/2026-11-12-uitk-s5-challenge.md の判断記録 D-1・D-4・D-5・D-7 と食い違いが無いか。食い違えば行と直す向きを書く。A-191 の「研究・チャレンジのカードの指」と §2 の OS カーソルの行のチャレンジの部分の改めは A-241 に従う）
- Registration/GameUi/Screens/ は直下 10 ファイルで上限。S5-challenge は Screens/Challenge/ のサブディレクトリに置いた。以後のレーンも直下へ足さず機能のサブディレクトリへ
## 未確認
```
Expected: bd の ID が出る。以後 `<bd>` をその ID で読む

- [ ] **Step 3: ViewModel と論理を書く**

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeNodeState.cs`:
```csharp
namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジのノードの状態（Web の locked・current・completed）
    /// Challenge node state (the Web locked, current and completed)
    /// </summary>
    public enum ChallengeNodeState
    {
        Locked,
        Current,
        Completed,
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeNodeCardViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジのカード1枚の表示（座標は Y 上向きのツリー座標・倍率はマスタの UIScale の x と y）
    /// Display of one challenge card (Y-up tree position; scale is the master UIScale x and y)
    /// </summary>
    public sealed class ChallengeNodeCardViewModel
    {
        public Guid Guid { get; }
        public Vector2 TreePosition { get; }
        public Vector2 Scale { get; }
        public IReadOnlyList<Guid> PreviousGuids { get; }
        public ChallengeNodeState State { get; }
        public LocalizationKey StateLabelKey { get; }

        public ChallengeNodeCardViewModel(Guid guid, Vector2 treePosition, Vector2 scale, IReadOnlyList<Guid> previousGuids, ChallengeNodeState state)
        {
            Guid = guid;
            TreePosition = treePosition;
            Scale = scale;
            PreviousGuids = previousGuids;
            State = state;

            // 文言キーは状態だけで決まるのでここで引く（状態と文言が食い違う VM を作れない）
            // The label key follows from the state alone, so derive it here (no VM can carry a mismatched label)
            StateLabelKey = ChallengeListLogic.DeriveStateLabelKey(state);
        }

        // グラフへ渡す置き方（キー・座標・線）が同じか。状態と倍率はカードの描き直しで足りる
        // Whether the graph placement (key, position, edges) matches; state and scale only need a card redraw
        public bool HasSameGraphPlacementAs(ChallengeNodeCardViewModel other)
        {
            return Guid == other.Guid && TreePosition == other.TreePosition && ChallengeListLogic.SameGuidSequence(PreviousGuids, other.PreviousGuids);
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeListLogic.cs`:
```csharp
using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の判断（注目点・状態の文言・並びの比較）。web: moorestech_web/webui/src/features/challenge/challengeLogic.ts・ChallengeNodeCard.tsx
    /// Challenge list decisions (focus, state label, order comparison); web: moorestech_web/webui/src/features/challenge/challengeLogic.ts and ChallengeNodeCard.tsx
    /// </summary>
    public static class ChallengeListLogic
    {
        // 寄せ先は並びで最初の進行中。無ければ寄せない
        // Focus the first in-progress node in order; none means no focus
        public static bool TryFindInitialFocusNode(IReadOnlyList<ChallengeNodeCardViewModel> nodes, out ChallengeNodeCardViewModel focusNode)
        {
            foreach (var node in nodes)
            {
                if (node.State != ChallengeNodeState.Current) continue;
                focusNode = node;
                return true;
            }
            focusNode = null;
            return false;
        }

        // 状態の文言キー（Web resolveChallengeStateKey と同じ写し。前例 ResearchLogic.DeriveNodeStateLabelKey）
        // State label key (same mapping as the Web resolveChallengeStateKey; precedent ResearchLogic.DeriveNodeStateLabelKey)
        public static LocalizationKey DeriveStateLabelKey(ChallengeNodeState state)
        {
            return state switch
            {
                ChallengeNodeState.Locked => LocalizationKeys.Ui.Challenge.StateLocked,
                ChallengeNodeState.Current => LocalizationKeys.Ui.Challenge.StateCurrent,
                ChallengeNodeState.Completed => LocalizationKeys.Ui.Challenge.StateCompleted,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }

        // Guid の並びが同じか（カテゴリのボタン列の並びとノードの前提の比較の 1 本）
        // Whether two Guid sequences match (the single comparison for the category row order and node prerequisites)
        public static bool SameGuidSequence(IReadOnlyList<Guid> left, IReadOnlyList<Guid> right)
        {
            if (left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i]) return false;
            }
            return true;
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeCategoryViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジのカテゴリ1つの表示（線は同じカテゴリの前提だけ）
    /// Display of one challenge category (edges only to prerequisites in the same category)
    /// </summary>
    public sealed class ChallengeCategoryViewModel
    {
        public Guid Guid { get; }
        public IReadOnlyList<ChallengeNodeCardViewModel> Nodes { get; }

        private ChallengeCategoryViewModel(Guid guid, IReadOnlyList<ChallengeNodeCardViewModel> nodes)
        {
            Guid = guid;
            Nodes = nodes;
        }

        public static ChallengeCategoryViewModel Create(Guid guid, IReadOnlyList<ChallengeNodeCardViewModel> nodes)
        {
            // カテゴリの中のキーを集める
            // Collect the keys inside the category
            var keys = new HashSet<Guid>();
            foreach (var node in nodes) keys.Add(node.Guid);

            // 別カテゴリの前提は線にしない（Web も描かない。NodeGraphView はグラフに無い親を LogError にするので渡す前に除く。C4-d D-5）
            // Prerequisites in other categories get no edge (the Web draws none; NodeGraphView logs an error for a missing parent, so drop them first; C4-d D-5)
            var linked = new List<ChallengeNodeCardViewModel>(nodes.Count);
            foreach (var node in nodes)
            {
                var previous = new List<Guid>(node.PreviousGuids.Count);
                // silent-ok: 別カテゴリの前提に線を引かないのは Web と同じ仕様 / L0 ChallengeListViewModelFactoryTest が除くことを固定する
                foreach (var previousGuid in node.PreviousGuids) if (keys.Contains(previousGuid)) previous.Add(previousGuid);
                linked.Add(new ChallengeNodeCardViewModel(node.Guid, node.TreePosition, node.Scale, previous, node.State));
            }
            return new ChallengeCategoryViewModel(guid, linked);
        }

        public bool TryGetInitialFocus(out Vector2 treePoint)
        {
            // 注目点は進行中のノードの座標
            // The focus point is the in-progress node position
            var found = ChallengeListLogic.TryFindInitialFocusNode(Nodes, out var node);
            treePoint = found ? node.TreePosition : Vector2.zero;
            return found;
        }

        // グラフの形（並び順のノードの置き方と注目点）が同じか。部品へ中身を渡し直すかの判定の 1 本
        // Whether the graph shape (node placements in order and the focus) matches; the single check for handing content to the component again
        public bool HasSameGraphAs(ChallengeCategoryViewModel other)
        {
            if (Nodes.Count != other.Nodes.Count) return false;
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (!Nodes[i].HasSameGraphPlacementAs(other.Nodes[i])) return false;
            }
            var hasFocus = TryGetInitialFocus(out var focus);
            var otherHasFocus = other.TryGetInitialFocus(out var otherFocus);
            return hasFocus == otherHasFocus && focus == otherFocus;
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/Selection/ChallengeCategorySelection.cs`:
```csharp
using System;

namespace Client.Ui.State.Challenge.Tree.Selection
{
    /// <summary>
    /// 選んだチャレンジのカテゴリ。無いことを型で持つ
    /// The selected challenge category; absence is part of the type
    /// </summary>
    public sealed class ChallengeCategorySelection
    {
        public static readonly ChallengeCategorySelection None = new(false, Guid.Empty);

        private readonly bool _hasCategory;
        private readonly Guid _categoryGuid;

        private ChallengeCategorySelection(bool hasCategory, Guid categoryGuid)
        {
            _hasCategory = hasCategory;
            _categoryGuid = categoryGuid;
        }

        public static ChallengeCategorySelection Of(Guid categoryGuid)
        {
            return new ChallengeCategorySelection(true, categoryGuid);
        }

        public bool Is(Guid categoryGuid)
        {
            return _hasCategory && _categoryGuid == categoryGuid;
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeListViewModel.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.Challenge.Tree.Selection;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の表示。解放済みのカテゴリが無い空（EmptyChallengeListViewModel）か、選んだカテゴリを必ず持つ（SelectedChallengeListViewModel）かの 2 つだけ。等価なし
    /// Challenge list display: only empty with no unlocked category (EmptyChallengeListViewModel) or always holding the selected one (SelectedChallengeListViewModel); no equality
    /// </summary>
    /// <remarks>
    /// 選択なしを bool と null の 2 つで持たないため、型で分ける（前例 BlockInventoryPanelState と ClosedBlockInventoryPanel）。派生はこのアセンブリの 2 つに閉じる
    /// Split by type so "no selection" is never held as both a bool and a null (precedent BlockInventoryPanelState and ClosedBlockInventoryPanel); derivation is closed to the two in this assembly
    /// </remarks>
    public abstract class ChallengeListViewModel
    {
        public static readonly ChallengeListViewModel Empty = new EmptyChallengeListViewModel();

        private protected ChallengeListViewModel()
        {
        }

        public static ChallengeListViewModel Create(IReadOnlyList<ChallengeCategoryViewModel> categories, ChallengeCategorySelection selection)
        {
            // 解放済みが無ければ空。選んだカテゴリが出ていればそれ、無ければ先頭（Web の find(...) ?? categories[0]）
            // Empty without an unlocked category; otherwise the selected one when listed, else the first (the Web find(...) ?? categories[0])
            if (categories.Count == 0) return Empty;
            foreach (var category in categories)
            {
                if (selection.Is(category.Guid)) return new SelectedChallengeListViewModel(categories, category);
            }
            return new SelectedChallengeListViewModel(categories, categories[0]);
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/EmptyChallengeListViewModel.cs`:
```csharp
namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// 解放済みのカテゴリが無いチャレンジ一覧（題だけ。Web は selected が無いと TreeView を描かない）
    /// Challenge list with no unlocked category (title only; the Web skips TreeView without a selection)
    /// </summary>
    public sealed class EmptyChallengeListViewModel : ChallengeListViewModel
    {
        internal EmptyChallengeListViewModel()
        {
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/SelectedChallengeListViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// 解放済みのカテゴリの列と、その中の選んだカテゴリ（必ずある）
    /// Unlocked categories and the selected one among them (always present)
    /// </summary>
    public sealed class SelectedChallengeListViewModel : ChallengeListViewModel
    {
        public IReadOnlyList<ChallengeCategoryViewModel> Categories { get; }
        public IReadOnlyList<Guid> CategoryGuids { get; }
        public ChallengeCategoryViewModel Selected { get; }

        internal SelectedChallengeListViewModel(IReadOnlyList<ChallengeCategoryViewModel> categories, ChallengeCategoryViewModel selected)
        {
            Categories = categories;
            Selected = selected;

            // ボタン列の並びの比較（ChallengeListLogic.SameGuidSequence）に渡す Guid の列
            // The Guid sequence handed to the button row order comparison (ChallengeListLogic.SameGuidSequence)
            var guids = new List<Guid>(categories.Count);
            foreach (var category in categories) guids.Add(category.Guid);
            CategoryGuids = guids;
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeListViewModelFactory.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Network.API;
using Client.Ui.State.Challenge.Tree.Selection;
using UnityEngine;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジの応答の列から一覧の表示を組む（規則は Web の橋 ChallengeDtoBuilder.BuildTree と同じ）
    /// Builds the list display from challenge responses (same rules as the Web bridge ChallengeDtoBuilder.BuildTree)
    /// </summary>
    public static class ChallengeListViewModelFactory
    {
        public static ChallengeListViewModel Create(IReadOnlyList<ChallengeCategoryResponse> responses, ChallengeCategorySelection selection)
        {
            // 解放済みのカテゴリだけを受信順に写す（握手と完了の知らせはどちらもサーバーがマスタの順で並べる）
            // Copy only unlocked categories in received order (the server lists both the handshake and completions in master order)
            var categories = new List<ChallengeCategoryViewModel>();
            foreach (var response in responses)
            {
                // silent-ok: 未解放のカテゴリを出さないのは Web の橋と同じ仕様 / L0 ChallengeListViewModelFactoryTest が固定する
                if (!response.IsUnlocked) continue;
                categories.Add(ChallengeCategoryViewModel.Create(response.Category.CategoryGuid, BuildNodes(response)));
            }
            return ChallengeListViewModel.Create(categories, selection);

            #region Internal

            // マスタの順で、完了＞進行中＞未解放の状態と座標・倍率・前提を写す
            // In master order, copy state (completed over current over locked), position, scale and prerequisites
            List<ChallengeNodeCardViewModel> BuildNodes(ChallengeCategoryResponse response)
            {
                var completed = new HashSet<Guid>();
                foreach (var challenge in response.CompletedChallenges) completed.Add(challenge.ChallengeGuid);
                var current = new HashSet<Guid>();
                foreach (var challenge in response.CurrentChallenges) current.Add(challenge.ChallengeGuid);

                var nodes = new List<ChallengeNodeCardViewModel>();
                foreach (var challenge in response.Category.Challenges)
                {
                    var guid = challenge.ChallengeGuid;
                    var state = completed.Contains(guid) ? ChallengeNodeState.Completed : current.Contains(guid) ? ChallengeNodeState.Current : ChallengeNodeState.Locked;
                    var param = challenge.DisplayListParam;
                    var position = new Vector2(param.UIPosition.x, param.UIPosition.y);
                    var scale = new Vector2(param.UIScale.x, param.UIScale.y);

                    // prevChallengeGuids はスキーマで optional で、省略は null（マスタ JSON は外部データ）。前提なしとして空にする（ChallengeMaster.GetCategoryInitialChallenges と同じ扱い）
                    // prevChallengeGuids is optional in the schema and null when omitted (master JSON is external data); treat it as no prerequisites (same as ChallengeMaster.GetCategoryInitialChallenges)
                    // silent-ok: 省略は前提なしというマスタの仕様 / 線が無いことは並べ比べで見え、L0 が前提の写しを固定する
                    var previous = challenge.PrevChallengeGuids ?? Array.Empty<Guid>();
                    nodes.Add(new ChallengeNodeCardViewModel(guid, position, scale, previous, state));
                }
                return nodes;
            }

            #endregion
        }
    }
}
```
（`PrevChallengeGuids` は `Guid[]`。サーバーの `ChallengeMaster.cs:117` も null を前提なしとして読む。Web の橋 `ChallengeDtoBuilder.cs:43` は null を確かめずに回すので、省略したマスタでは橋が例外になる。UITK は前提なしとして描く）

- [ ] **Step 4: L0 を書く**

`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge/ChallengeListLogicTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Challenge.Tree;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Challenge
{
    /// <summary>
    /// チャレンジ一覧の注目点（L0。Web の challengeLogic.test.ts）
    /// Challenge list focus (L0; Web challengeLogic.test.ts)
    /// </summary>
    public class ChallengeListLogicTest
    {
        private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
        private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");

        /// <web-test file="src/features/challenge/challengeLogic.test.ts">進行中(current)のノードを返す</web-test>
        [Test]
        public void 進行中のノードを返す()
        {
            var nodes = new[] { Node(A, ChallengeNodeState.Completed), Node(B, ChallengeNodeState.Current), Node(C, ChallengeNodeState.Locked) };
            Assert.IsTrue(ChallengeListLogic.TryFindInitialFocusNode(nodes, out var focus));
            Assert.AreEqual(B, focus.Guid);
        }

        /// <web-test file="src/features/challenge/challengeLogic.test.ts">進行中が無ければ寄せない</web-test>
        [Test]
        public void 進行中が無ければ寄せない()
        {
            var nodes = new[] { Node(A, ChallengeNodeState.Completed), Node(C, ChallengeNodeState.Locked) };
            Assert.IsFalse(ChallengeListLogic.TryFindInitialFocusNode(nodes, out _));
            Assert.IsFalse(ChallengeListLogic.TryFindInitialFocusNode(Array.Empty<ChallengeNodeCardViewModel>(), out _));
        }

        private static ChallengeNodeCardViewModel Node(Guid guid, ChallengeNodeState state)
        {
            return new ChallengeNodeCardViewModel(guid, Vector2.zero, Vector2.one, new List<Guid>(), state);
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge/ChallengeListViewModelFactoryTest.cs`:
```csharp
using System;
using System.Linq;
using Client.Network.API;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.Challenge
{
    /// <summary>
    /// チャレンジの応答から一覧の表示を組む規則（L0。テスト mod の challenges.json）
    /// Rules that build the list display from challenge responses (L0; test mod challenges.json)
    /// </summary>
    public class ChallengeListViewModelFactoryTest
    {
        private static readonly Guid Category1 = Guid.Parse("03ca4ded-3b2b-4e7f-bb6e-430f060c4ed1");
        private static readonly Guid Category2 = Guid.Parse("35330f9d-f44f-493d-a6bc-07ae6413d7c4");
        private static readonly Guid Test1 = Guid.Parse("00000000-0000-0000-4567-000000000001");
        private static readonly Guid Test2 = Guid.Parse("00000000-0000-0000-4567-000000000002");
        private static readonly Guid Test3 = Guid.Parse("00000000-0000-0000-4567-000000000003");
        private static readonly Guid Test4 = Guid.Parse("00000000-0000-0000-4567-000000000004");
        private static readonly Guid Research1 = Guid.Parse("00000000-0000-0000-4567-000000000101");
        private static readonly Guid Category2First = Guid.Parse("c2d84cfb-73cb-4ffa-99b5-9703c550f330");

        [SetUp]
        public void LoadMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 未解放のカテゴリを除きマスタの順で状態と座標と前提を写す()
        {
            var model = AsSelected(ChallengeListViewModelFactory.Create(new[] { Response(Category1, true, new[] { Test2 }, new[] { Test1 }), Response(Category2, false, new Guid[0], new Guid[0]) }, ChallengeCategorySelection.None));

            // 未解放の Category2 は出ない。ノードはマスタの順
            // Locked Category2 is absent; nodes follow master order
            Assert.AreEqual(1, model.Categories.Count);
            var nodes = model.Categories[0].Nodes;
            var master = MasterHolder.ChallengeMaster.GetChallengeCategory(Category1).Challenges;
            CollectionAssert.AreEqual(master.Select(c => c.ChallengeGuid).ToList(), nodes.Select(n => n.Guid).ToList());

            // 完了・進行中・未解放と、その文言キー（Web resolveChallengeStateKey）
            // Completed, current, locked and their label keys (Web resolveChallengeStateKey)
            Assert.AreEqual(ChallengeNodeState.Completed, nodes.Single(n => n.Guid == Test1).State);
            Assert.AreEqual(ChallengeNodeState.Current, nodes.Single(n => n.Guid == Test2).State);
            Assert.AreEqual(ChallengeNodeState.Locked, nodes.Single(n => n.Guid == Test3).State);
            Assert.AreEqual(LocalizationKeys.Ui.Challenge.StateCompleted.Key, nodes.Single(n => n.Guid == Test1).StateLabelKey.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Challenge.StateCurrent.Key, nodes.Single(n => n.Guid == Test2).StateLabelKey.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Challenge.StateLocked.Key, nodes.Single(n => n.Guid == Test3).StateLabelKey.Key);

            // 座標と倍率はマスタの displayListParam、前提はマスタの prevChallengeGuids
            // Position and scale come from the master displayListParam; prerequisites from prevChallengeGuids
            var research1 = master.Single(c => c.ChallengeGuid == Research1).DisplayListParam;
            Assert.AreEqual(new Vector2(research1.UIPosition.x, research1.UIPosition.y), nodes.Single(n => n.Guid == Research1).TreePosition);
            Assert.AreEqual(new Vector2(research1.UIScale.x, research1.UIScale.y), nodes.Single(n => n.Guid == Research1).Scale);
            CollectionAssert.AreEqual(new[] { Test1 }, nodes.Single(n => n.Guid == Test4).PreviousGuids);

            // 注目点は進行中の座標
            // The focus is the current node position
            Assert.IsTrue(model.Categories[0].TryGetInitialFocus(out var focus));
            Assert.AreEqual(nodes.Single(n => n.Guid == Test2).TreePosition, focus);
        }

        [Test]
        public void 選択が無いか出ていないカテゴリなら先頭を選び出ていればそれを選ぶ()
        {
            var both = new[] { Response(Category1, true, new[] { Test2 }, new[] { Test1 }), Response(Category2, true, new[] { Category2First }, new Guid[0]) };
            Assert.AreEqual(Category1, Selected(ChallengeListViewModelFactory.Create(both, ChallengeCategorySelection.None)));
            Assert.AreEqual(Category2, Selected(ChallengeListViewModelFactory.Create(both, ChallengeCategorySelection.Of(Category2))));
            Assert.AreEqual(Category1, Selected(ChallengeListViewModelFactory.Create(both, ChallengeCategorySelection.Of(Guid.NewGuid()))));

            // 選んだカテゴリが未解放なら出ないので先頭、解放済みが無ければ空の一覧（選択を持たない）
            // A selected but locked category is absent so the first is used; with no unlocked categories the list is empty (no selection)
            var lockedSecond = new[] { both[0], Response(Category2, false, new Guid[0], new Guid[0]) };
            Assert.AreEqual(Category1, Selected(ChallengeListViewModelFactory.Create(lockedSecond, ChallengeCategorySelection.Of(Category2))));
            var none = ChallengeListViewModelFactory.Create(new[] { Response(Category1, false, new Guid[0], new Guid[0]) }, ChallengeCategorySelection.Of(Category1));
            Assert.IsInstanceOf<EmptyChallengeListViewModel>(none);
            Assert.AreSame(ChallengeListViewModel.Empty, none);
        }

        [Test]
        public void 別カテゴリの前提は線のキーから除く()
        {
            var foreign = Guid.Parse("00000000-0000-0000-0000-00000000f00d");
            var inside = new ChallengeNodeCardViewModel(Test1, Vector2.zero, Vector2.one, new Guid[0], ChallengeNodeState.Completed);
            var crossing = new ChallengeNodeCardViewModel(Test2, new Vector2(220f, 0f), Vector2.one, new[] { Test1, foreign }, ChallengeNodeState.Current);
            var category = ChallengeCategoryViewModel.Create(Category1, new[] { inside, crossing });
            CollectionAssert.AreEqual(new[] { Test1 }, category.Nodes[1].PreviousGuids);
            CollectionAssert.IsEmpty(category.Nodes[0].PreviousGuids);
        }

        [Test]
        public void グラフの形は並びと座標と前提と注目点で比べ状態と倍率では変わらない()
        {
            var first = Card(Test1, Vector2.zero, Vector2.one, ChallengeNodeState.Completed);
            var second = Card(Test2, new Vector2(220f, 0f), Vector2.one, ChallengeNodeState.Current, Test1);
            var shown = Category(first, second);

            // 倍率だけの違いはカードの描き直しで足り、同じ形
            // A scale-only difference needs just a card redraw, so the shape is the same
            Assert.IsTrue(shown.HasSameGraphAs(Category(first, Card(Test2, new Vector2(220f, 0f), new Vector2(2f, 2f), ChallengeNodeState.Current, Test1))));

            // 座標・前提・並び・注目点のどれかが変われば違う形
            // A change in position, prerequisites, order or focus is a different shape
            Assert.IsFalse(shown.HasSameGraphAs(Category(first, Card(Test2, new Vector2(240f, 0f), Vector2.one, ChallengeNodeState.Current, Test1))), "座標");
            Assert.IsFalse(shown.HasSameGraphAs(Category(first, Card(Test2, new Vector2(220f, 0f), Vector2.one, ChallengeNodeState.Current))), "前提");
            Assert.IsFalse(shown.HasSameGraphAs(Category(second, first)), "並び");
            Assert.IsFalse(shown.HasSameGraphAs(Category(first, Card(Test2, new Vector2(220f, 0f), Vector2.one, ChallengeNodeState.Completed, Test1))), "注目点");
        }

        private static ChallengeNodeCardViewModel Card(Guid guid, Vector2 position, Vector2 scale, ChallengeNodeState state, params Guid[] previous)
        {
            return new ChallengeNodeCardViewModel(guid, position, scale, previous, state);
        }

        private static ChallengeCategoryViewModel Category(params ChallengeNodeCardViewModel[] nodes)
        {
            return ChallengeCategoryViewModel.Create(Category1, nodes);
        }

        private static ChallengeCategoryResponse Response(Guid category, bool unlocked, Guid[] current, Guid[] completed)
        {
            var master = MasterHolder.ChallengeMaster;
            return new ChallengeCategoryResponse(master.GetChallengeCategory(category), unlocked, current.Select(master.GetChallenge).ToList(), completed.Select(master.GetChallenge).ToList());
        }

        private static SelectedChallengeListViewModel AsSelected(ChallengeListViewModel model)
        {
            Assert.IsInstanceOf<SelectedChallengeListViewModel>(model);
            return (SelectedChallengeListViewModel)model;
        }

        private static Guid Selected(ChallengeListViewModel model)
        {
            return AsSelected(model).Selected.Guid;
        }
    }
}
```

- [ ] **Step 5: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.Challenge\."`
Expected: ErrorCount 0 / 6 件 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge
git commit -m "feat(uitk): S5-challenge のチャレンジ一覧の ViewModel・注目点・工場と L0"
```

---

### Task 2: 表示判定・選択・操作・Model（`Client.Ui.State/Challenge`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/ChallengeScreenVisibility.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/Selection/ChallengeCategorySelectionStore.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Commands/IChallengeListCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Commands/ChallengeListCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeListModel.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge/ChallengeCategorySelectionStoreTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge/ChallengeListModelTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge/ChallengeProgressStateTestSource.cs`（補助）

**Interfaces:**
- Consumes: Task 1 の `ChallengeListViewModelFactory.Create`・`ChallengeListViewModel.Empty`・`ChallengeCategorySelection`、`ChallengeProgressState.Categories`・`OnChallengeCompleted`、`UiRoutingState.Current`・`UiRoutingSnapshot.Screen`
- Produces:
  - `public static class ChallengeScreenVisibility { public static bool IsShown(UiRoutingSnapshot snapshot); }`（`Client.Ui.State.Challenge`。X-audit DC-8 の `<機能>ScreenVisibility.IsShown` の形。経路を読んで一覧の表示を決める 3 か所〈選択ストア・Model・Presenter〉はこれだけを呼ぶ）
  - `ChallengeCategorySelectionStore(UiRoutingState routing)`（`IInitializable`）・`IReadOnlyReactiveProperty<ChallengeCategorySelection> Current`・`void Select(Guid categoryGuid)`
  - `interface IChallengeListCommands { void SelectCategory(Guid categoryGuid); }`・`ChallengeListCommands(ChallengeCategorySelectionStore selection)`（`Client.Ui.State.Challenge.Commands`。前例 `Client.Ui.State.Research.Commands`）
  - `ChallengeListModel(ChallengeProgressState progress, ChallengeCategorySelectionStore selection, UiRoutingState routing)`（`IInitializable`）・`IReadOnlyReactiveProperty<ChallengeListViewModel> Current`

- [ ] **Step 1: 表示判定・選択・操作を書く**

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/ChallengeScreenVisibility.cs`:
```csharp
using Client.Ui.State.Routing;

namespace Client.Ui.State.Challenge
{
    /// <summary>
    /// チャレンジ一覧を出すか（画面ルータが ChallengeList。前例 BlockInventoryScreenVisibility）
    /// Whether to show the challenge list (router on ChallengeList; precedent BlockInventoryScreenVisibility)
    /// </summary>
    public static class ChallengeScreenVisibility
    {
        public static bool IsShown(UiRoutingSnapshot snapshot)
        {
            return snapshot.Screen == UiScreen.ChallengeList;
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/Selection/ChallengeCategorySelectionStore.cs`:
```csharp
using System;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Challenge.Tree.Selection
{
    /// <summary>
    /// チャレンジ一覧で選んだカテゴリ（離れたら捨てて先頭へ戻す。Web はパネルのアンマウントで消える）
    /// Category selected in the challenge list (dropped on leaving so the first returns; the Web loses it on unmount)
    /// </summary>
    public sealed class ChallengeCategorySelectionStore : IInitializable
    {
        private readonly UiRoutingState _routing;
        private readonly ReactiveProperty<ChallengeCategorySelection> _current = new(ChallengeCategorySelection.None);

        public IReadOnlyReactiveProperty<ChallengeCategorySelection> Current => _current;

        public ChallengeCategorySelectionStore(UiRoutingState routing)
        {
            _routing = routing;
        }

        public void Initialize()
        {
            _routing.Current.Subscribe(snapshot =>
            {
                // silent-ok: 一覧の中の経路の通知は選択を保つ / 離れて無しに戻ることは L0 ChallengeCategorySelectionStoreTest、先頭へ戻ることは L1 ChallengeListViewportTest と録画 uitk-challenge-list が見る
                if (ChallengeScreenVisibility.IsShown(snapshot)) return;
                _current.Value = ChallengeCategorySelection.None;
            });
        }

        public void Select(Guid categoryGuid)
        {
            _current.Value = ChallengeCategorySelection.Of(categoryGuid);
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Commands/IChallengeListCommands.cs`:
```csharp
using System;

namespace Client.Ui.State.Challenge.Commands
{
    /// <summary>
    /// チャレンジ一覧の画面からの操作（サーバーへは送らない。前例 IResearchTreeCommands）
    /// Operations from the challenge list screen (nothing is sent to the server; precedent IResearchTreeCommands)
    /// </summary>
    public interface IChallengeListCommands
    {
        void SelectCategory(Guid categoryGuid);
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Commands/ChallengeListCommands.cs`:
```csharp
using System;
using Client.Ui.State.Challenge.Tree.Selection;

namespace Client.Ui.State.Challenge.Commands
{
    /// <summary>
    /// チャレンジ一覧の操作（カテゴリの選択を選択ストアへ）
    /// Challenge list operations (category selection goes to the selection store)
    /// </summary>
    public sealed class ChallengeListCommands : IChallengeListCommands
    {
        private readonly ChallengeCategorySelectionStore _selection;

        public ChallengeListCommands(ChallengeCategorySelectionStore selection)
        {
            _selection = selection;
        }

        public void SelectCategory(Guid categoryGuid)
        {
            _selection.Select(categoryGuid);
        }
    }
}
```

- [ ] **Step 2: Model を書く**

`moorestech_client/Assets/Scripts/Client.Ui.State/Challenge/Tree/ChallengeListModel.cs`:
```csharp
using Client.Ui.State.Challenge.Tree.Selection;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の表示の組み直しと配信（表示中だけ組む）
    /// Rebuilds and publishes the challenge list display (only while shown)
    /// </summary>
    /// <remarks>
    /// 表示中かは控えた旗でなく経路の今の値で読む。一覧を離れると選択ストアも経路を購読して None に戻すが、どちらの購読が先に呼ばれても隠れた後の選択の変化では組まない（DI の登録順に依らない）
    /// Whether shown is read from the router's current value, not a cached flag; on leaving, the selection store also resets to None from its own router subscription, and whichever runs first no rebuild follows a selection change after hiding (independent of DI registration order)
    /// </remarks>
    public sealed class ChallengeListModel : IInitializable
    {
        private readonly ChallengeProgressState _progress;
        private readonly ChallengeCategorySelectionStore _selection;
        private readonly UiRoutingState _routing;
        private readonly ReactiveProperty<ChallengeListViewModel> _current = new(ChallengeListViewModel.Empty);

        public IReadOnlyReactiveProperty<ChallengeListViewModel> Current => _current;

        public ChallengeListModel(ChallengeProgressState progress, ChallengeCategorySelectionStore selection, UiRoutingState routing)
        {
            _progress = progress;
            _selection = selection;
            _routing = routing;
        }

        public void Initialize()
        {
            // 完了の知らせ（Categories の置き換えの後に来る）と選択の変化で組み直す。ゲーム寿命なので購読は解かない
            // Rebuild on completion notices (after Categories is replaced) and selection changes; game lifetime, so subscriptions stay
            _progress.OnChallengeCompleted.Subscribe(_ => RebuildWhileShown());
            _selection.Current.Skip(1).Subscribe(_ => RebuildWhileShown());

            // 一覧の表示の間だけ組む。経路の通知（突入）で 1 回組む（前例 ResearchTreeModel）
            // Build only while the list is shown, once per router notice (entering; precedent ResearchTreeModel)
            _routing.Current.Subscribe(_ => RebuildWhileShown());
        }

        private void RebuildWhileShown()
        {
            // ReactiveProperty は値を書いてから購読者へ流すので、通知の中でも今の経路が読める
            // ReactiveProperty stores the value before notifying, so the current route is readable inside any notice
            if (!ChallengeScreenVisibility.IsShown(_routing.Current.Value)) return; // silent-ok: 閉画面では組まない / 閉画面で組まず突入で組むことは L0 ChallengeListModelTest が固定し、録画 uitk-challenge-list の開き直しの段が見る
            _current.Value = ChallengeListViewModelFactory.Create(_progress.Categories, _selection.Current.Value);
        }
    }
}
```

- [ ] **Step 3: 選択ストアと Model の L0 を書く**

前例 `Client.Tests/Ui/State/Research/Wiring/ResearchTreeModelTest.cs`・`ResearchSelectionStoreTest.cs`（経路は `UiRoutingStateTestFactory.CreateSwitchable`・`Transit`）。源の `ChallengeProgressState` は前例 `Client.Tests/WebUi/Challenge/ChallengeProgressStateTest.cs` の組み方（`CapturingVanillaApiEvent`・握手の応答・完了の知らせの MessagePack）で、テスト mod の 2 カテゴリから組む。

`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge/ChallengeProgressStateTestSource.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Network.API;
using Client.Tests.Inventory;
using Client.Ui.State.Challenge;
using Core.Master;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Tests.Ui.State.Challenge
{
    /// <summary>
    /// テスト mod の 2 カテゴリで ChallengeProgressState を組み、完了の知らせを流す（前例 ChallengeProgressStateTest）
    /// Builds ChallengeProgressState from the two test mod categories and dispatches completion notices (precedent ChallengeProgressStateTest)
    /// </summary>
    internal sealed class ChallengeProgressStateTestSource
    {
        public static readonly Guid Category1 = Guid.Parse("03ca4ded-3b2b-4e7f-bb6e-430f060c4ed1");
        public static readonly Guid Category2 = Guid.Parse("35330f9d-f44f-493d-a6bc-07ae6413d7c4");
        public static readonly Guid Test1 = Guid.Parse("00000000-0000-0000-4567-000000000001");
        public static readonly Guid Test2 = Guid.Parse("00000000-0000-0000-4567-000000000002");
        public static readonly Guid Category2First = Guid.Parse("c2d84cfb-73cb-4ffa-99b5-9703c550f330");

        private readonly CapturingVanillaApiEvent _events = new();

        public ChallengeProgressState Progress { get; }

        public ChallengeProgressStateTestSource()
        {
            // 2 カテゴリとも解放済み。Category1 は Test1 完了・Test2 進行中、Category2 は先頭が進行中（受信順＝マスタの順）
            // Both categories unlocked; Category1 has Test1 done and Test2 current, Category2 has its first current (received order = master order)
            var initial = new List<ChallengeCategoryResponse>
            {
                Response(Category1, new[] { Test2 }, new[] { Test1 }),
                Response(Category2, new[] { Category2First }, Array.Empty<Guid>()),
            };
            Progress = new ChallengeProgressState(_events, Handshake(initial));
            Progress.Initialize();
        }

        // Test2 を完了にした全量を流す（サーバーの完了イベントと同じ形。Categories を置き換えてから知らせる）
        // Dispatch the full state with Test2 completed (same shape as the server completion event; Categories is replaced before the notice)
        public void CompleteTest2()
        {
#pragma warning disable CS0618
            var message = new CompletedChallengeEventMessagePack
            {
                CompletedChallengeGuidStr = Test2.ToString(),
                NextChallengeGuidsStr = new List<string>(),
                PlayedSkitIds = new List<string>(),
                ChallengeCategories = new List<ChallengeCategoryMessagePack>
                {
                    Category(Category1, Array.Empty<Guid>(), new[] { Test1, Test2 }),
                    Category(Category2, new[] { Category2First }, Array.Empty<Guid>()),
                },
            };
#pragma warning restore CS0618
            _events.Dispatch(CompletedChallengeEventPacket.EventTag, MessagePackSerializer.Serialize(message));
        }

        private static ChallengeCategoryResponse Response(Guid category, Guid[] current, Guid[] completed)
        {
            var master = MasterHolder.ChallengeMaster;
            return new ChallengeCategoryResponse(master.GetChallengeCategory(category), true, current.Select(master.GetChallenge).ToList(), completed.Select(master.GetChallenge).ToList());
        }

        private static ChallengeCategoryMessagePack Category(Guid category, Guid[] current, Guid[] completed)
        {
            return new ChallengeCategoryMessagePack
            {
                ChallengeCategoryGuid = category,
                IsUnlocked = true,
                CurrentChallengeGuidsStr = current.Select(guid => guid.ToString()).ToList(),
                CompletedChallengeGuidsStr = completed.Select(guid => guid.ToString()).ToList(),
            };
        }

        // チャレンジ以外は空で組む（前例 ChallengeProgressStateTest.CreateHandshake）
        // Non-challenge handshake parts stay empty (precedent ChallengeProgressStateTest.CreateHandshake)
        private static InitialHandshakeResponse Handshake(List<ChallengeCategoryResponse> challenges)
        {
#pragma warning disable CS0618
            var initialHandshake = new global::Server.Protocol.PacketResponse.InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack
            {
                Accepted = new global::Server.Protocol.PacketResponse.Handshake.HandshakeAcceptedMessagePack
                {
                    PlayerPos = new Vector3MessagePack(Vector3.zero),
                },
            };
#pragma warning restore CS0618
            return new InitialHandshakeResponse(initialHandshake, (null, null, null, challenges, null, null, null, null));
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge/ChallengeCategorySelectionStoreTest.cs`:
```csharp
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.State.Challenge.Tree.Selection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Source = Client.Tests.Ui.State.Challenge.ChallengeProgressStateTestSource;

namespace Client.Tests.Ui.State.Challenge
{
    /// <summary>
    /// チャレンジのカテゴリの選択は一覧の中で保ち、離れると無しに戻る（L0。Web はパネルのアンマウントで useState が消える）
    /// The challenge category selection holds inside the list and resets to none on leaving (L0; the Web loses useState on panel unmount)
    /// </summary>
    public class ChallengeCategorySelectionStoreTest
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void 一覧の中では選んだカテゴリを保ち離れると無しに戻る()
        {
            _host = new GameObject("ChallengeCategorySelectionStoreTest");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.GameScreen, new[] { UIStateEnum.GameScreen, UIStateEnum.ChallengeList }, out var control);
            var store = new ChallengeCategorySelectionStore(routing);
            store.Initialize();
            Assert.AreSame(ChallengeCategorySelection.None, store.Current.Value, "初期");

            // 一覧へ入って選ぶと保つ
            // Entering the list and selecting keeps it
            UiRoutingStateTestFactory.Transit(control, UIStateEnum.ChallengeList);
            store.Select(Source.Category2);
            Assert.IsTrue(store.Current.Value.Is(Source.Category2));

            // 離れると無しに戻る
            // Leaving resets it to none
            UiRoutingStateTestFactory.Transit(control, UIStateEnum.GameScreen);
            Assert.AreSame(ChallengeCategorySelection.None, store.Current.Value, "離れた後");
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge/ChallengeListModelTest.cs`:
```csharp
using System;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;
using Source = Client.Tests.Ui.State.Challenge.ChallengeProgressStateTestSource;

namespace Client.Tests.Ui.State.Challenge
{
    /// <summary>
    /// チャレンジ一覧の表示は一覧の表示中だけ組み、突入・選択・完了の知らせで組み直し、離れると先頭のカテゴリへ戻る（L0。前例 ResearchTreeModelTest）
    /// The challenge list display builds only while shown, rebuilds on entry, selection and completion notices, and returns to the first category after leaving (L0; precedent ResearchTreeModelTest)
    /// </summary>
    public class ChallengeListModelTest
    {
        private GameObject _host;
        private UIStateControl _control;
        private Source _source;
        private ChallengeCategorySelectionStore _selection;
        private ChallengeListModel _model;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void 閉画面では完了の知らせと選択の変化で組まない()
        {
            Build(true);
            _selection.Select(Source.Category2);
            _source.CompleteTest2();

            Assert.AreSame(ChallengeListViewModel.Empty, _model.Current.Value);
        }

        [Test]
        public void 一覧へ入ると先頭のカテゴリで組み表示中は選択と完了の知らせで組み直す()
        {
            // 突入で解放済みの 2 カテゴリを受信順で組み、先頭を選ぶ
            // Entering builds both unlocked categories in received order and selects the first
            Build(true);
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ChallengeList);
            var entered = AsSelected(_model.Current.Value);
            CollectionAssert.AreEqual(new[] { Source.Category1, Source.Category2 }, entered.CategoryGuids);
            Assert.AreEqual(Source.Category1, entered.Selected.Guid);

            // 選択の変化で組み直す
            // A selection change rebuilds
            _selection.Select(Source.Category2);
            Assert.AreEqual(Source.Category2, AsSelected(_model.Current.Value).Selected.Guid);

            // 完了の知らせ（Categories の置き換えの後）で組み直し、Test2 が完了になる
            // A completion notice (after Categories is replaced) rebuilds and Test2 becomes completed
            var beforeCompletion = _model.Current.Value;
            _source.CompleteTest2();
            Assert.AreNotSame(beforeCompletion, _model.Current.Value);
            var basics = AsSelected(_model.Current.Value).Categories.Single(c => c.Guid == Source.Category1);
            Assert.AreEqual(ChallengeNodeState.Completed, basics.Nodes.Single(n => n.Guid == Source.Test2).State);
        }

        // 本番の登録順（選択ストア → Model）と逆順の両方で、離れた後の選択の戻しでは組まず、開き直すと先頭のカテゴリ（D-21）
        // In both the production order (selection store, then model) and the reverse, the reset after leaving builds nothing and reopening shows the first category (D-21)
        [TestCase(true)]
        [TestCase(false)]
        public void 一覧を離れると選択が無しに戻り開き直すと先頭のカテゴリを組む(bool selectionFirst)
        {
            Build(selectionFirst);
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ChallengeList);
            _selection.Select(Source.Category2);
            var shown = _model.Current.Value;

            // 離れると選択は無しに戻るが、閉じた画面では組まない（最後に出した表示のまま）
            // Leaving resets the selection, but a closed screen builds nothing (the last shown display stays)
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            Assert.AreSame(ChallengeCategorySelection.None, _selection.Current.Value);
            Assert.AreSame(shown, _model.Current.Value);

            // 開き直すと先頭のカテゴリで組む
            // Reopening builds with the first category
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.ChallengeList);
            Assert.AreEqual(Source.Category1, AsSelected(_model.Current.Value).Selected.Guid);
        }

        private void Build(bool selectionFirst)
        {
            // テスト mod のマスタを読み、ゲーム画面と一覧の間を行き来できる経路で組む
            // Load the test mod master and build on a router that moves between the game screen and the list
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _host = new GameObject("ChallengeListModelTest");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.GameScreen, new[] { UIStateEnum.GameScreen, UIStateEnum.ChallengeList }, out _control);
            _source = new Source();
            _selection = new ChallengeCategorySelectionStore(routing);
            _model = new ChallengeListModel(_source.Progress, _selection, routing);

            // 初期化の順を入れ替えられるようにする（DI の登録順に依らないことを見る）
            // Allow either initialization order (checks independence from DI registration order)
            if (selectionFirst)
            {
                _selection.Initialize();
                _model.Initialize();
                return;
            }
            _model.Initialize();
            _selection.Initialize();
        }

        private static SelectedChallengeListViewModel AsSelected(ChallengeListViewModel model)
        {
            Assert.IsInstanceOf<SelectedChallengeListViewModel>(model);
            return (SelectedChallengeListViewModel)model;
        }
    }
}
```
（`ChallengeCategorySelectionStoreTest` は前例 `ResearchSelectionStoreTest` と同じく Initialize 前後の振る舞いだけを見る。マスタを読まないので `Source` の GUID 定数だけを使う）

- [ ] **Step 4: コンパイルして L0 を流す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.Challenge"`
Expected: ErrorCount 0 / 11 件 PASS（Task 1 の 6 件＋ここの 5 件。画面との結線は Task 4 の L1 と Task 6 の録画で見る）
- 「閉画面では…組まない」が落ちたら、`ChallengeListModel` の購読のどれかが `IsShown` を通っていない
- 「離れると…」の `selectionFirst = false` だけ落ちたら、Model が選択の戻しを経路の今の値でなく控えた旗で判定している（D-21）
- 源の組み立てで `MasterHolder` が null なら、`Build` の先頭のマスタの読み込みが `new Source()` より後に来ている

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Challenge moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Challenge
git commit -m "feat(uitk): S5-challenge の表示判定・カテゴリの選択・操作・Model と L0"
```

---

### Task 2b: `GameScrollView` に横向きのモードを足す（Components の変更。R-17）

カテゴリ列の横スクロール（Web `.categories` の `overflow-x: auto`）は `Components/Controls/GameScrollView` で写す。今の `GameScrollView` は縦だけなので、部品に向きの引数を足す。画面の USS は部品の内側のクラス（`unity-scroller__slider`・`unity-base-slider__*` など `__` 付き）を書かない（S5-crosscut T10 が `UiStyleConventionTest` に足す検査。A-167 の「部品のクラスを画面が書かない」）。画面だけの横スクロール部品は作らない。`Theme.uss` は触らない。

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView/GameScrollAxis.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs`（ctor に `GameScrollAxis axis`。バーの有無・太さ・スクロール量の通知を向きの軸で）
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView/GameScrollView.uss`（縦の規則を `.unity-scroller--vertical` に絞り、横の規則を足す）
- Modify（呼び出し側。`GameScrollAxis.Vertical` を足すだけ。起草時の 13 か所。S 相対）: `Client.Ui/Components/Controls/RecipeListScrollView/RecipeListScrollView.cs:27`・`Client.Ui/Screens/BlockInventory/BlockInventoryNaturalScroll.cs:20`・`Client.Ui/Screens/BlockInventory/Machine/MachineSectionView.cs:72`・`Client.Ui/Screens/BlockInventory/Train/TrainInventoryBodyView.cs:37`・`Client.Ui/Screens/BuildMenu/Parts/BuildMenuPanelSkeleton.cs:46`・`Client.Ui/Screens/PauseMenu/PauseMenuSubPageView.cs:32`・`Client.Ui/Screens/Recipe/ItemListView.cs:41`・`Client.Ui/Screens/Research/Parts/ResearchDetailPane.cs:50`・`Client.Ui.Editor/Gallery/Entries/Controls/GameScrollViewGalleryEntry.cs:20`・`Client.Tests/Ui/Components/Controls/GameScrollViewKnobTest.cs:100`・`Client.Tests/Ui/Components/Controls/GameScrollViewSpy/GameScrollViewSpyPortTest.cs:25`・`Client.Tests/Ui/Components/Controls/GameScrollViewTest.cs:31`・`Client.Tests/Ui/Screens/Inventory/EquipmentWheelFlowTest.cs:96`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Controls/GameScrollViewAxis/GameScrollViewHorizontalTest.cs`（`Client.Tests/Ui/Components/Controls/` 直下は既に 10 ファイルなのでサブディレクトリ）

**Interfaces:**
- Consumes: 今の `GameScrollView(string testid)`（`origin/feature/uitk` 9c83052273 の実物。128 行）・`GameScrollView.uss`・`DerivedTokens.uss` の `--scrollarea-scrollbar-size`（12px）・`--game-scroll-view-track-face`・`--game-scroll-view-thumb-face`・`--game-scroll-view-scrollbar-padding-ratio`
- Produces:
  - `public enum GameScrollAxis { Vertical, Horizontal }`（`Client.Ui.Components.Controls`）
  - `GameScrollView(string testid, GameScrollAxis axis)`。根のクラスに `game-scroll-view--vertical` か `game-scroll-view--horizontal`。横はバーを本文の下の流れの中に置き、中身が見える幅を越えたときだけ出す。縦のバーは出さない。太さは `--scrollarea-scrollbar-size`、溝とつまみの色は縦と同じつまみ。`OnScrollOffsetChanged` は向きの軸のバーの値。`SetScrollOffset`・`GetViewportHeight`・`GetContentHeight`・`TryGetContentTop` はスクロールスパイの縦の口のまま（横では使わない）

- [ ] **Step 1: 実物を開き、横の L1 を書いて赤を見る**

Run:
```bash
S=moorestech_client/Assets/Scripts
wc -l $S/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs
git grep -n "new GameScrollView(" -- $S
ls $S/Client.Tests/Ui/Components/Controls/ | grep -v meta | wc -l
```
Expected: 128 行（違えば実物に下の差分だけを当てる）。`new GameScrollView(` は上の Files の 13 か所（増えていれば全部に `GameScrollAxis.Vertical` を足す）。`Controls/` 直下は 10

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Controls/GameScrollViewAxis/GameScrollViewHorizontalTest.cs`:
```csharp
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;

namespace Client.Tests.Ui.Components.Controls.GameScrollViewAxis
{
    /// <summary>
    /// GameScrollView の横向き（越えたときだけ流れの中に横のバー・縦のバーは出さない・太さと色は縦と同じつまみ）
    /// GameScrollView horizontal mode (an in-flow horizontal bar only on overflow, never a vertical bar, the same knobs for size and colors)
    /// </summary>
    public class GameScrollViewHorizontalTest : UiTestHost
    {
        private GameScrollView _view;
        private VisualElement _last;

        [SetUp]
        public void MountRowThatFits()
        {
            // 幅 300 の親に幅 80・高さ 36 の箱を 3 つ（計 240 で収まる）
            // Three 80x36 boxes in a 300-wide parent (240 in total, so they fit)
            var host = new VisualElement();
            host.style.width = 300;
            _view = new GameScrollView("probe-horizontal-scroll", GameScrollAxis.Horizontal);
            for (var i = 0; i < 3; i++) AddBox();
            host.Add(_view);
            rootVisualElement.Add(host);
            Settle();
        }

        [Test]
        public void 横向きは収まるときバーを出さず高さは中身の高さ()
        {
            Assert.AreEqual(ScrollViewMode.Horizontal, _view.mode);
            Assert.IsTrue(_view.ClassListContains("game-scroll-view--horizontal"));
            Assert.AreEqual(DisplayStyle.None, _view.horizontalScroller.resolvedStyle.display, "横のバー");
            Assert.AreEqual(DisplayStyle.None, _view.verticalScroller.resolvedStyle.display, "縦のバー");
            Assert.AreEqual(36f, _view.worldBound.height, 0.5f);
        }

        [Test]
        public void 越えると本文の下に横のバーが出て末尾まで送れ縦のバーは出さない()
        {
            // 箱を 6 つ（計 480）にして幅 300 を越えさせる
            // Six boxes (480 in total) overflow the 300 width
            for (var i = 0; i < 3; i++) AddBox();
            Settle();
            var scroller = _view.horizontalScroller;
            var dragger = scroller.Q(className: "unity-base-slider__dragger");

            // バーは本文の下の流れの中で太さ 12。本文の高さを削らず、全体が中身 36＋バー 12 になる
            // The bar sits in the flow below the body, 12 thick; the body keeps its height and the whole is 36 + 12
            Assert.AreEqual(DisplayStyle.Flex, scroller.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, _view.verticalScroller.resolvedStyle.display);
            Assert.AreEqual(12f, scroller.worldBound.height, 0.01f);
            Assert.AreEqual(_view.contentViewport.worldBound.yMax, scroller.worldBound.yMin, 0.5f);
            Assert.AreEqual(36f + 12f, _view.worldBound.height, 0.5f);

            // 溝とつまみは縦と同じつまみ（gauge-track・角なしの bevel-c2）。つまみは内側 2px（整数丸め）を空けた太さ 8
            // Track and thumb use the vertical knobs (gauge-track, square bevel-c2); the thumb is 8 thick inside the 2px (snapped) inset
            AssertColor(Rgba(10, 14, 27, 0.8f), scroller.resolvedStyle.backgroundColor, "track");
            AssertColor(Rgba(104, 106, 120, 1f), dragger.resolvedStyle.backgroundColor, "thumb");
            Assert.AreEqual(8f, dragger.worldBound.height, 0.5f, "つまみの太さ");
            Assert.Less(dragger.worldBound.width, scroller.worldBound.width, "つまみの長さが溝より短い");

            // 末尾の箱まで送れる（Web overflow-x: auto）
            // The last box scrolls into view (Web overflow-x: auto)
            _view.ScrollTo(_last);
            Settle();
            Assert.Greater(_view.scrollOffset.x, 0f);
            Assert.LessOrEqual(_last.worldBound.xMax, _view.contentViewport.worldBound.xMax + 0.5f);
        }

        private void AddBox()
        {
            var box = new VisualElement();
            box.style.width = 80;
            box.style.height = 36;
            box.style.flexShrink = 0;
            _view.Add(box);
            _last = box;
        }

        private void Settle()
        {
            // バーの出し入れで 2 回目のレイアウトが走る
            // Showing or hiding the bar triggers a second layout
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }
    }
}
```

Run: `uloop compile --project-path ./moorestech_client`
Expected: `GameScrollView(string, GameScrollAxis)` と `GameScrollAxis` が無いのでコンパイルエラー（赤）

- [ ] **Step 2: 部品に向きを足す**

`moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView/GameScrollAxis.cs`:
```csharp
namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// GameScrollView の向き（縦はバーを本文に重ね、横はバーを本文の下の流れに置く）
    /// GameScrollView orientation (vertical overlays the bar on the body; horizontal puts it in the flow below the body)
    /// </summary>
    public enum GameScrollAxis
    {
        Vertical,
        Horizontal,
    }
}
```

`GameScrollView.cs` は次の形にする（縦の振る舞いは今のまま。変わるのは ctor の引数・根の向きのクラス・バーの有無と太さとスクロール量の通知を向きの軸で読むところだけ）:
```csharp
// moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs
using System;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Theme;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// スクロール領域。縦はバーが本文に重なり、横はバーが本文の下の流れに入る
    /// Scroll area; the vertical bar overlays the body and the horizontal bar sits in the flow below it
    /// </summary>
    /// <remarks>
    /// 縦は Mantine ScrollArea の写し（A-35）、横は Web の overflow-x: auto の写し（チャレンジのカテゴリ列）。SetScrollOffset・高さ・上端はスクロールスパイの縦の口で、横では使わない
    /// Vertical copies Mantine ScrollArea (A-35) and horizontal copies the Web overflow-x: auto (challenge category row); SetScrollOffset, heights and content top are the scroll spy's vertical ports, unused for horizontal
    /// </remarks>
    public sealed class GameScrollView : ScrollView
    {
        private const string ScrollbarSizeProperty = "--game-scroll-view-scrollbar-size";

        // バーの内側余白＝太さ×比（既定 0.2 は Mantine ScrollArea の size / 5。一覧は Web と同じく 0 に上書きする）
        // Bar padding = size x ratio (the default 0.2 is Mantine ScrollArea's size / 5; the item list overrides it to 0 as on the Web)
        private const string ScrollbarPaddingRatioProperty = "--game-scroll-view-scrollbar-padding-ratio-resolved";

        private readonly GameScrollAxis _axis;
        private readonly Subject<float> _onScrollOffsetChanged = new();

        private readonly Subject<Unit> _onContentLayoutChanged = new();
        private float _publishedContentHeight = float.NaN;
        private float _publishedViewportHeight = float.NaN;

        public IObservable<float> OnScrollOffsetChanged => _onScrollOffsetChanged;
        public IObservable<Unit> OnContentLayoutChanged => _onContentLayoutChanged;

        public GameScrollView(string testid, GameScrollAxis axis) : base(axis == GameScrollAxis.Horizontal ? ScrollViewMode.Horizontal : ScrollViewMode.Vertical)
        {
            _axis = axis;
            name = testid;
            AddToClassList("game-scroll-view");
            AddToClassList(axis == GameScrollAxis.Horizontal ? "game-scroll-view--horizontal" : "game-scroll-view--vertical");
            // Web（CEF）の 1 ノッチの px（A-71）を UITK の WheelEvent の行へ直す
            // Convert the Web (CEF) pixels per notch (A-71) into UITK WheelEvent lines
            mouseWheelScrollSize = WebWheelScale.PixelsPerNotch / WheelEvent.scrollDeltaPerTick;

            // バーの有無は向きの軸の流れの寸法で決める。Auto は子の変形（押下の 1px 沈み）まで数えて押す間だけバーを出す
            // Bar presence follows the flow size along the axis; Auto counts child transforms (the 1px press sink) and flashes the bar while pressed
            horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            verticalScrollerVisibility = ScrollerVisibility.Hidden;
            contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateScrollerVisibility());
            contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateScrollerVisibility());

            // スクロール量の変化は向きの軸のバーの値の変化で拾う（ホイール・ドラッグ・ScrollTo・SetScrollOffset のどれでも来る）
            // Offset changes come from the axis bar value (wheel, drag, ScrollTo and SetScrollOffset all move it)
            AxisScroller().valueChanged += offset => _onScrollOffsetChanged.OnNext(offset);
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());

            // 中身と視口の寸法の変化をスクロールスパイへ知らせる（スクロールでも事象が来る場合に備え、高さの比較で重複を落とす）
            // Tell the scroll spy about content and viewport size changes (heights are compared to drop duplicates in case scrolling also raises the event)
            contentContainer.RegisterCallback<GeometryChangedEvent>(_ => PublishWhenHeightsChanged());
            contentViewport.RegisterCallback<GeometryChangedEvent>(_ => PublishWhenHeightsChanged());

            #region Internal

            void UpdateScrollerVisibility()
            {
                // 向きの軸で中身が見える寸法を越えたときだけバーを出す。交差の軸のバーは常に出さない
                // Show the bar only when the content exceeds the visible size along the axis; the cross-axis bar never shows
                var overflows = _axis == GameScrollAxis.Horizontal
                    ? contentViewport.layout.width < contentContainer.layout.width
                    : contentViewport.layout.height < contentContainer.layout.height;
                var visibility = overflows ? ScrollerVisibility.AlwaysVisible : ScrollerVisibility.Hidden;
                if (_axis == GameScrollAxis.Horizontal) horizontalScrollerVisibility = visibility;
                else verticalScrollerVisibility = visibility;
            }

            void PublishWhenHeightsChanged()
            {
                var contentHeight = GetContentHeight();
                var viewportHeight = GetViewportHeight();
                if (Mathf.Approximately(contentHeight, _publishedContentHeight) && Mathf.Approximately(viewportHeight, _publishedViewportHeight)) return;
                _publishedContentHeight = contentHeight;
                _publishedViewportHeight = viewportHeight;
                _onContentLayoutChanged.OnNext(Unit.Default);
            }

            #endregion
        }

        public void SetScrollOffset(float y)
        {
            scrollOffset = new Vector2(scrollOffset.x, y);
        }

        public float GetViewportHeight()
        {
            return contentViewport.layout.height;
        }

        public float GetContentHeight()
        {
            return contentContainer.layout.height;
        }

        // 中身の上端からの距離。スクロールの平行移動は両者に同じく掛かるので打ち消し合う
        // Distance from the content top; the scroll translation applies to both and cancels out
        public bool TryGetContentTop(VisualElement descendant, out float top)
        {
            // 中身の外の要素は基準が違う。取り違えを黙って 0 にせず知らせる
            // An element outside the content has another origin; report the mix-up instead of silently returning 0
            if (!contentContainer.Contains(descendant))
            {
                Debug.LogError($"[GameScrollView] {name}: {descendant.name} is not inside the content");
                top = 0f;
                return false;
            }
            top = descendant.ChangeCoordinatesTo(contentContainer, Vector2.zero).y;
            return true;
        }

        private Scroller AxisScroller()
        {
            return _axis == GameScrollAxis.Horizontal ? horizontalScroller : verticalScroller;
        }

        private void OnCustomStyleResolved()
        {
            // 読めないときは UssCustomStyleReader が LogError を出し、バーは Theme.uss の既定の太さのまま
            // When unreadable, UssCustomStyleReader logs an error and the bar keeps the Theme.uss default thickness
            if (!UssCustomStyleReader.TryReadPixels(customStyle, ScrollbarSizeProperty, out var size)) return;
            // 比も読めなければ UssCustomStyleReader が LogError を出し、バーは前の太さと余白のまま
            // If the ratio is unreadable too, UssCustomStyleReader logs an error and the bar keeps its previous thickness and padding
            if (!UssCustomStyleReader.TryReadNumber(customStyle, ScrollbarPaddingRatioProperty, out var ratio)) return;

            // 太さは交差の軸（縦は幅・横は高さ）。内側の余白はレイアウトで整数 px に丸まる（look-exception: LX-42）
            // Thickness runs across the axis (width for vertical, height for horizontal); the padding snaps to whole px in layout (look-exception: LX-42)
            var padding = size * ratio;
            var bar = AxisScroller();
            if (_axis == GameScrollAxis.Horizontal) bar.style.height = size;
            else bar.style.width = size;
            bar.style.paddingLeft = padding;
            bar.style.paddingRight = padding;
            bar.style.paddingTop = padding;
            bar.style.paddingBottom = padding;
        }
    }
}
```
（向きの分岐は ctor の基底呼び出し・根のクラス・バーの有無・太さ・`AxisScroller` の 5 か所で、どれも「どの軸か」だけを選ぶ。縦横で振る舞いの違う処理は増やさない。200 行未満）

`GameScrollView.uss` の縦の 4 規則（`.game-scroll-view .unity-scroller__slider`・`.game-scroll-view .unity-scroller .unity-base-slider__drag-container`・`… __tracker`・`… __dragger`）のセレクタの `.unity-scroller` を `.unity-scroller--vertical` に替え（`.unity-scroller__slider` の規則は `.game-scroll-view .unity-scroller--vertical .unity-scroller__slider`。縦では横のバーが常に隠れているので見た目は変わらない）、`:hover` の規則はそのまま残す（縦横の両方に当たる）。末尾に横の区画を足す:
```css
/* 横: バーは本文の下の流れに置く（Web の overflow-x: auto。重ねると末尾の行の下が押せない）。太さと内側は GameScrollView.cs が高さと padding で当てる */
/* Horizontal: the bar sits in the flow below the body (Web overflow-x: auto; overlaying would block presses on the row bottom); GameScrollView.cs applies thickness and inset as height and padding */
.game-scroll-view--horizontal .unity-scroll-view__content-container {
    flex-shrink: 0;
}

.game-scroll-view--horizontal .unity-scroll-view__horizontal-scroller {
    min-height: 0;
    margin: 0;
    border-width: 0;
    background-color: var(--game-scroll-view-track-face);
}

.game-scroll-view .unity-scroller--horizontal .unity-scroller__slider {
    flex-grow: 1;
    height: 100%;
    min-height: 0;
    margin: 0;
}

.game-scroll-view .unity-scroller--horizontal .unity-base-slider__drag-container {
    height: 100%;
    margin: 0;
}

/* Theme.uss の .unity-scroller の溝（縦向けの width 8px・height auto）を横向きに戻す */
/* Undo Theme.uss's .unity-scroller track (vertical width 8px, height auto) for the horizontal bar */
.game-scroll-view .unity-scroller--horizontal .unity-base-slider__tracker {
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    border-width: 0;
    background-color: transparent;
}

/* つまみ: Theme.uss の縦向けの width 8px と Slider 向けの top: 50% を戻す。長さは Unity がインラインで当てる */
/* Thumb: undo Theme.uss's vertical width 8px and the Slider top: 50%; Unity sets the length inline */
.game-scroll-view .unity-scroller--horizontal .unity-base-slider__dragger {
    top: 0;
    left: 0;
    height: 100%;
    margin: 0;
    border-width: 0;
    border-radius: 0;
    background-color: var(--game-scroll-view-thumb-face);
}
```

呼び出し側の 13 か所は `new GameScrollView(<今の引数>)` を `new GameScrollView(<今の引数>, GameScrollAxis.Vertical)` に替える（デフォルト引数・引数 1 つの多重定義は作らない。AGENTS.md「引数の追加は呼び出し側を変更する」）

- [ ] **Step 3: コンパイルし、部品と呼び出し側のテストを流す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Components\.Controls|Screens\.(BuildMenu|PauseMenu|Recipe|Research|BlockInventory|Inventory))"`
Expected: ErrorCount 0 / PASS（`GameScrollViewHorizontalTest` 2 件、既存の `GameScrollViewTest`・`GameScrollViewKnobTest`・`GameScrollViewSpyPortTest`・`RecipeListScrollViewTest` と各画面の L1 が今のまま緑）
- 横のバーの高さ・つまみの色が違えば、`Theme.uss` の `.unity-scroller` の規則（縦向けの `width: 8px`・`height: auto`）と Slider の `top: 50%` のどれが勝っているかを `uloop execute-dynamic-code` で `resolvedStyle` を読み、`GameScrollView.uss` の横の区画で上書きを足す（`Theme.uss` は触らない）
- つまみの長さが溝いっぱい（`dragger.worldBound.width == scroller.worldBound.width`）なら、横の dragger の幅を USS の `width: 100%` が潰している。横の区画に `width` を書いていないかを確かめる（長さは Unity がインラインで当てる）
- 収まるときの高さが 0 なら、横の `ScrollView` の content container が流れから外れている。`GameScrollView.uss` の横の区画の `.unity-scroll-view__content-container` に `position: relative` を足す
- 既存の縦のテストが落ちたら、セレクタを `.unity-scroller--vertical` に絞った規則の取りこぼし（`:hover` 以外）を見る

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Controls moorestech_client/Assets/Scripts/Client.Ui moorestech_client/Assets/Scripts/Client.Ui.Editor/Gallery moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Inventory/EquipmentWheelFlowTest.cs
git commit -m "feat(uitk): GameScrollView に横向きのモードを足す（S5-challenge のカテゴリ列。呼び出し側は Vertical を明示）"
```
（Unity が作った `.meta` も同じコミットに入れる。手で作らない）

---

### Task 3: 画面の USS・カード・カテゴリのボタン列と横スクロール・グラフの置き場・一覧の View と L1

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Tree/ChallengeList.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Parts/ChallengeNodeCard.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Parts/ChallengeNodeCardSet.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Parts/ChallengeCategoryButtons.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Parts/ChallengeCategoryGraphHost.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Parts/ChallengeCategoryGraph.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Tree/ChallengeListView.cs`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画の末尾に 1 行）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs`（3 行を消す）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeListScreenTestKit.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree/RecordingChallengeListCommands.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeListViewTest.cs`

**Interfaces:**
- Consumes: Task 1・2 の型（`SelectedChallengeListViewModel`・`EmptyChallengeListViewModel`・`ChallengeCategoryViewModel.HasSameGraphAs`・`ChallengeNodeCardViewModel.StateLabelKey`・`ChallengeListLogic.SameGuidSequence`・`IChallengeListCommands`）、Task 2b の `GameScrollView(string, GameScrollAxis.Horizontal)`、Global Constraints の部品（`NodeGraphView`・`PaperSurface`・`FormButton`・`UiGroup`・`LocalizedLabel`・`TutorialAnchor`・`UiPickingIgnore`）
- Produces:
  - `ChallengeListView(IChallengeListCommands commands, IUiOsCursor osCursor)`（`Client.Ui.Screens.Challenge.Tree`）・`void Render(ChallengeListViewModel model)`（`SelectedChallengeListViewModel`／`EmptyChallengeListViewModel` の分岐はここの 1 か所）・`void SetShown(bool shown)`。`name = "challenge-panel"`
  - `ChallengeNodeCard(Guid challengeGuid)`・`PaperSurface Root`・`void Render(ChallengeNodeCardViewModel node)`（`Client.Ui.Screens.Challenge.Parts`）
  - `ChallengeNodeCardSet`・`void RenderCards(IReadOnlyList<ChallengeNodeCardViewModel> nodes)`・`NodeGraphContent BuildContent(IReadOnlyList<ChallengeNodeCardViewModel> nodes, NodeGraphFocus focus)`
  - `ChallengeCategoryButtons(IChallengeListCommands commands)`・`void Render(SelectedChallengeListViewModel model)`・`void ClearButtons()`。`name = "challenge-categories"`、中の横スクロール `challenge-categories-scroll`、ボタン `challenge-category-<guid>`
  - `ChallengeCategoryGraphHost(IUiOsCursor osCursor)`・`void Render(ChallengeCategoryViewModel category)`・`void UnmountGraph()`
  - `ChallengeCategoryGraph(ChallengeCategoryViewModel category, IUiOsCursor osCursor)`・`NodeGraphView View`・`void Render(ChallengeCategoryViewModel category)`
  - USS のクラス: `challenge-list`・`challenge-list__title`・`challenge-list__categories`・`challenge-list__category-group`・`challenge-list__category-button`・`challenge-list__graph-host`・`challenge-list__card`・`challenge-list__card--locked/--current/--completed`・`challenge-list__card-ring`・`challenge-list__card-title`・`challenge-list__card-summary`・`challenge-list__card-state`（つまみは宣言しない）
  - テストの材料 `ChallengeListScreenTestKit`（`Basics`・`Logistics`・`FirstCraft`・`SecondStep`・`LockedStep`・`LogisticsDone`・`LogisticsCurrent`・`LoadMasterAndDictionary()`・`Tree(ChallengeCategorySelection)`・`MountList(UiLayerStack, IChallengeListCommands)`・`MountHud(UiLayerStack)`・`Card(VisualElement, Guid)`・`PlainText(Label)`・`ButtonText(Button)`）・`RecordingChallengeListCommands.Selected`

- [ ] **Step 1: 画面の USS を書く**

`moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Tree/ChallengeList.uss`:
```css
/* web: moorestech_web/webui/src/features/challenge/style.module.css（.panel・.categories・.node・.locked・.current・.completed）と ChallengePanel.tsx・ChallengeNodeCard.tsx の Mantine の既定（Title order 2・Group gap md・Button sm・Paper・Text） */
/* outline は拾わない子の輪で写す。gap は UiGroup が配る。translate(-50%, -50%) はここ、scale(UIScale) は C# がカードの style へ当てる。カードの根で flex-shrink を書かない（C4-d D-23） */
/* カテゴリ列の横スクロールとバーは GameScrollView の横向きが持つ。部品の内側のクラス（__ 付き）はここに書かない */
/* web: moorestech_web/webui/src/features/challenge/style.module.css (.panel, .categories, .node, .locked, .current, .completed) and the Mantine defaults in ChallengePanel.tsx and ChallengeNodeCard.tsx (Title order 2, Group gap md, Button sm, Paper, Text) */
/* The outline becomes a non-picking child ring; UiGroup hands out the gap; translate(-50%, -50%) lives here and C# applies scale(UIScale) to the card style; the card root never sets flex-shrink (C4-d D-23) */
/* GameScrollView's horizontal mode owns the category row scrolling and bar; component inner classes (with __) are never written here */

/* 一覧: 舞台の上の安全帯の下から下端まで、地は dark-8、余白 md の縦積み（Web inset: var(--menu-upper-safe-area) 0 0） */
/* List: from below the stage's upper safe area to the bottom, dark-8 ground, md padding, stacked vertically (Web inset: var(--menu-upper-safe-area) 0 0) */
.challenge-list {
    position: absolute;
    top: var(--menu-upper-safe-area);
    left: 0;
    right: 0;
    bottom: 0;
    flex-direction: column;
    padding-left: var(--mantine-spacing-md);
    padding-right: var(--mantine-spacing-md);
    padding-top: var(--mantine-spacing-md);
    padding-bottom: var(--mantine-spacing-md);
    background-color: var(--mantine-color-dark-8);
}

.challenge-list__title {
    flex-shrink: 0;
}

/* カテゴリ列: 折り返さず、幅を越えたら横に送る（Web .categories の flex-wrap: nowrap・overflow-x: auto）。送りとバーは GameScrollView の横向き */
/* Category row: no wrapping, scrolls sideways when it overflows (Web .categories flex-wrap: nowrap, overflow-x: auto); GameScrollView's horizontal mode scrolls and draws the bar */
.challenge-list__categories {
    flex-shrink: 0;
}

/* ボタンは文言なりの幅で並び縮まない（Web の Button の文言は折り返さず縮まない） */
/* Buttons line up at their label width and never shrink (the Web Button label neither wraps nor shrinks) */
.challenge-list__category-group {
    flex-shrink: 0;
}

.challenge-list__category-button {
    flex-shrink: 0;
}

.challenge-list__graph-host {
    flex-grow: 1;
    min-height: 0;
}

/* カード: 幅 240・余白 sm。中心をノードの点に据える（Web .node と translate(-50%, -50%)） */
/* Card: 240 wide with sm padding, centered on the node point (Web .node and translate(-50%, -50%)) */
.challenge-list__card {
    width: 240px;
    padding-left: var(--mantine-spacing-sm);
    padding-right: var(--mantine-spacing-sm);
    padding-top: var(--mantine-spacing-sm);
    padding-bottom: var(--mantine-spacing-sm);
    translate: -50% -50%;
}

/* 未解放の減光は Web の値のまま（GameUi は sRGB で合成する。設計書 A-180） */
/* Locked dimming keeps the Web value (GameUi composites in sRGB; design A-180) */
.challenge-list__card--locked {
    opacity: 0.55;
}

/* 輪: Web の outline 2px（枠の外）。Paper は枠なしなので padding 辺から 2px 外。角は Paper の角丸 4px に沿う outline の外周 6px */
/* Ring: the Web 2px outline outside the border; Paper has no border, so 2px outside the padding edge; corners follow the outline around Paper's 4px radius (6px outer) */
.challenge-list__card-ring {
    display: none;
    position: absolute;
    left: -2px;
    top: -2px;
    right: -2px;
    bottom: -2px;
    border-left-width: 2px;
    border-right-width: 2px;
    border-top-width: 2px;
    border-bottom-width: 2px;
    border-top-left-radius: 6px;
    border-top-right-radius: 6px;
    border-bottom-right-radius: 6px;
    border-bottom-left-radius: 6px;
}

.challenge-list__card--current > .challenge-list__card-ring {
    display: flex;
    border-left-color: var(--mantine-color-blue-5);
    border-right-color: var(--mantine-color-blue-5);
    border-top-color: var(--mantine-color-blue-5);
    border-bottom-color: var(--mantine-color-blue-5);
}

.challenge-list__card--completed > .challenge-list__card-ring {
    display: flex;
    border-left-color: var(--mantine-color-green-6);
    border-right-color: var(--mantine-color-green-6);
    border-top-color: var(--mantine-color-green-6);
    border-bottom-color: var(--mantine-color-green-6);
}
```

- [ ] **Step 2: カードとカードの組を書く**

`moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Parts/ChallengeNodeCard.cs`:
```csharp
using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// チャレンジのノードカード1枚（Paper の面に名前・概要・状態）。web: moorestech_web/webui/src/features/challenge/ChallengeNodeCard.tsx
    /// One challenge node card (title, summary and state on a Paper face); web: moorestech_web/webui/src/features/challenge/ChallengeNodeCard.tsx
    /// </summary>
    /// <remarks>
    /// PaperSurface は sealed なので、根の PaperSurface を組み立てて Root で渡す。根は拾う（Ignore だと押下の対象がビューポートに変わる。C4-d）。指のカーソルは付けない（Web のカードに cursor: pointer もタップも無く、ビューポートの grab を継ぐ）
    /// PaperSurface is sealed, so this builds the root PaperSurface and hands it out as Root; the root picks (Ignore would retarget presses to the viewport; C4-d); no pointer cursor (the Web card has neither cursor: pointer nor taps and inherits the viewport grab)
    /// </remarks>
    public sealed class ChallengeNodeCard
    {
        private readonly LocalizedLabel _state;

        public PaperSurface Root { get; }

        public ChallengeNodeCard(Guid challengeGuid)
        {
            // 根は Paper の面。名前は testid、アンカーはチャレンジの GUID
            // The root is the Paper face; its name is the testid and its anchor is the challenge GUID
            var key = challengeGuid.ToString();
            Root = new PaperSurface($"challenge-node-{key}");
            Root.AddToClassList("challenge-list__card");
            TutorialAnchor.Mark(Root, AnchorIds.ChallengeNode(key));

            // 名前（太字 md）・概要（sm）・状態（xs）を縦に積む。名前と概要はチャレンジで決まる
            // Stack title (bold md), summary (sm) and state (xs); title and summary are fixed per challenge
            var title = AddText(UiTypographyClass.TextMd, "challenge-list__card-title");
            title.AddToClassList(UiTypographyClass.TextBold);
            title.SetText(UiText.FromContent(ContentLocalizationKeys.ChallengeTitle(challengeGuid)));
            var summary = AddText(UiTypographyClass.TextSm, "challenge-list__card-summary");
            summary.SetText(UiText.FromContent(ContentLocalizationKeys.ChallengeSummary(challengeGuid)));
            _state = AddText(UiTypographyClass.TextXs, "challenge-list__card-state");

            // 輪は Web の outline。拾わない子を最後に重ねる
            // The ring is the Web outline; a non-picking child laid on top last
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("challenge-list__card-ring");
            Root.Add(ring);

            #region Internal

            // 文字は拾わない。押下はカードの根へ浮かぶ
            // Text never picks; presses bubble to the card root
            LocalizedLabel AddText(string sizeClass, string partClass)
            {
                var label = new LocalizedLabel();
                label.AddToClassList(sizeClass);
                label.AddToClassList(partClass);
                UiPickingIgnore.ApplyToSubtree(label);
                Root.Add(label);
                return label;
            }

            #endregion
        }

        public void Render(ChallengeNodeCardViewModel node)
        {
            // 状態は修飾クラスと State が決めた文言キー、Web の scale(UIScale) は style の scale（中心を軸に拡縮）
            // State becomes a modifier class and the label key State chose; the Web scale(UIScale) goes to style scale (about the center)
            Root.EnableInClassList("challenge-list__card--locked", node.State == ChallengeNodeState.Locked);
            Root.EnableInClassList("challenge-list__card--current", node.State == ChallengeNodeState.Current);
            Root.EnableInClassList("challenge-list__card--completed", node.State == ChallengeNodeState.Completed);
            _state.SetText(new UiText(node.StateLabelKey));
            Root.style.scale = new Scale(new Vector2(node.Scale.x, node.Scale.y));
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Parts/ChallengeNodeCardSet.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.Components.NodeGraph;
using Client.Ui.State.Challenge.Tree;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// チャレンジのカードを GUID で使い回し、グラフの中身を組む（前例 ResearchNodeCardSet）
    /// Reuses challenge cards by GUID and builds the graph content (precedent ResearchNodeCardSet)
    /// </summary>
    /// <remarks>
    /// 中身を渡し直すかの判定は State の ChallengeCategoryViewModel.HasSameGraphAs が持つ（並びの比較をここに置かない）
    /// Whether to hand content over again is decided by State's ChallengeCategoryViewModel.HasSameGraphAs (no order comparison here)
    /// </remarks>
    public sealed class ChallengeNodeCardSet
    {
        private readonly Dictionary<Guid, ChallengeNodeCard> _cards = new();

        // 全カードを描き直す。初めての GUID はカードを作る
        // Redraw every card; a new GUID gets a new card
        public void RenderCards(IReadOnlyList<ChallengeNodeCardViewModel> nodes)
        {
            foreach (var node in nodes)
            {
                if (!_cards.TryGetValue(node.Guid, out var card))
                {
                    card = new ChallengeNodeCard(node.Guid);
                    _cards.Add(node.Guid, card);
                }
                card.Render(node);
            }
        }

        public NodeGraphContent BuildContent(IReadOnlyList<ChallengeNodeCardViewModel> nodes, NodeGraphFocus focus)
        {
            // キーは GUID の文字列。前提は同じカテゴリのものだけが来る（ChallengeCategoryViewModel）
            // Keys are GUID strings; only same-category prerequisites arrive (ChallengeCategoryViewModel)
            var graphNodes = new List<NodeGraphNode>(nodes.Count);
            foreach (var node in nodes)
            {
                var previousKeys = new List<string>(node.PreviousGuids.Count);
                foreach (var previous in node.PreviousGuids) previousKeys.Add(previous.ToString());
                graphNodes.Add(new NodeGraphNode(node.Guid.ToString(), node.TreePosition, previousKeys, _cards[node.Guid].Root));
            }
            return new NodeGraphContent(graphNodes, focus);
        }
    }
}
```

- [ ] **Step 3: カテゴリ列の横スクロール・ボタン列・グラフの置き場を書く**

`moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Parts/ChallengeCategoryButtons.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.Challenge.Commands;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// チャレンジのカテゴリのボタン列（Web の Group gap md・nowrap・overflow-x: auto と Button filled/subtle）
    /// Challenge category button row (the Web Group gap md, nowrap, overflow-x: auto and Button filled/subtle)
    /// </summary>
    public sealed class ChallengeCategoryButtons : VisualElement
    {
        private readonly IChallengeListCommands _commands;
        private readonly UiGroup _group = new(UiGap.Md);
        private readonly Dictionary<Guid, FormButton> _buttons = new();
        private IReadOnlyList<Guid> _order = Array.Empty<Guid>();

        public ChallengeCategoryButtons(IChallengeListCommands commands)
        {
            // 列の根がアンカー。行は折り返さず、越えたら横に送る
            // The row root carries the anchor; the row never wraps and scrolls sideways on overflow
            _commands = commands;
            name = "challenge-categories";
            AddToClassList("challenge-list__categories");
            _group.AddToClassList(UiLayoutClass.GroupNoWrap);
            _group.AddToClassList("challenge-list__category-group");
            var scroll = new GameScrollView("challenge-categories-scroll", GameScrollAxis.Horizontal);
            scroll.Add(_group);
            Add(scroll);
            TutorialAnchor.Mark(this, AnchorIds.ChallengeCategories);
        }

        public void Render(SelectedChallengeListViewModel model)
        {
            // カテゴリの並びが変わったときだけボタンを作り直す（並びの比較は State の 1 本）
            // Rebuild the buttons only when the category order changes (the order comparison is the single one in State)
            if (!ChallengeListLogic.SameGuidSequence(_order, model.CategoryGuids)) Rebuild(model);

            // 選んだカテゴリは filled、他は subtle
            // The selected category is filled, the others subtle
            foreach (var category in model.Categories)
            {
                var isSelected = category.Guid == model.Selected.Guid;
                _buttons[category.Guid].SetVariant(isSelected ? FormButtonVariant.Filled : FormButtonVariant.Subtle);
            }
        }

        public void ClearButtons()
        {
            // 解放済みのカテゴリが無い一覧はボタンを持たない
            // A list without unlocked categories holds no buttons
            _group.ClearItems();
            _buttons.Clear();
            _order = Array.Empty<Guid>();
        }

        private void Rebuild(SelectedChallengeListViewModel model)
        {
            // 文言はマスタの内容キー（A-40）。押すとカテゴリを選ぶ。ボタンは縮めない
            // Labels are master content keys (A-40); pressing selects the category; buttons never shrink
            ClearButtons();
            foreach (var category in model.Categories)
            {
                var guid = category.Guid;
                var button = new FormButton($"challenge-category-{guid}", UiText.FromContent(ContentLocalizationKeys.ChallengeCategoryName(guid)), FormButtonVariant.Subtle);
                button.AddToClassList("challenge-list__category-button");
                button.OnClicked.Subscribe(_ => _commands.SelectCategory(guid));
                _group.AddItem(button);
                _buttons.Add(guid, button);
            }
            _order = model.CategoryGuids;
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Parts/ChallengeCategoryGraph.cs`:
```csharp
using Client.Ui.Components.NodeGraph;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Challenge.Tree;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// チャレンジのカテゴリ 1 つのグラフ（NodeGraphView・カードの組・最後に渡した中身）。画面の寿命で控えられ、視点は View が持つ
    /// One challenge category's graph (NodeGraphView, card set and the last content handed over); kept for the screen lifetime, the view holds the viewport
    /// </summary>
    public sealed class ChallengeCategoryGraph
    {
        private readonly ChallengeNodeCardSet _cards = new();
        private ChallengeCategoryViewModel _shown;

        public NodeGraphView View { get; }

        public ChallengeCategoryGraph(ChallengeCategoryViewModel category, IUiOsCursor osCursor)
        {
            // 作ったときに中身を渡す。部品は寸法が揃った最初の回だけ進行中へ寄せる（裁定 2026-11-04 の初回訪問）
            // Hand the content over on creation; the component centers on the current node once, when its size first arrives (first visit, ruling 2026-11-04)
            View = new NodeGraphView("challenge", osCursor);
            _shown = category;
            _cards.RenderCards(category.Nodes);
            View.SetGraph(BuildContent(category));
        }

        public void Render(ChallengeCategoryViewModel category)
        {
            // カードは毎回描き直す（状態・倍率）。渡す中身（並び・座標・線・注目点）が変わったときだけ渡し直す（D-20）
            // Cards are redrawn every time (state, scale); content is handed over only when it changes (order, positions, edges, focus; D-20)
            _cards.RenderCards(category.Nodes);
            var contentChanged = !category.HasSameGraphAs(_shown);
            _shown = category;
            // silent-ok: 中身が同じなら渡し直さない（部品の作り直しを避ける） / カードの描き直しは上で済み、L1 ChallengeListViewportTest が視点の保持を見る
            if (!contentChanged) return;
            View.SetGraph(BuildContent(category));
        }

        private NodeGraphContent BuildContent(ChallengeCategoryViewModel category)
        {
            var hasFocus = category.TryGetInitialFocus(out var focus);
            return _cards.BuildContent(category.Nodes, hasFocus ? NodeGraphFocus.At(focus) : NodeGraphFocus.None);
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Parts/ChallengeCategoryGraphHost.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Challenge.Tree;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// 選んだカテゴリのグラフを置く。訪れたカテゴリごとに NodeGraphView を 1 本控え、選択が変わったら置く View を付け替える（Web の key={selected.guid} と viewportStore）
    /// Hosts the selected category's graph; keeps one NodeGraphView per visited category and swaps the attached view on selection changes (the Web key={selected.guid} and viewportStore)
    /// </summary>
    /// <remarks>
    /// 視点は View の寿命で保たれる（I-fix6 D-3）。View を作ったときだけ部品が進行中へ 1 回寄せ、再訪と開き直しは控えた View の視点のまま（裁定 2026-11-04）。置き場の子は表示中のカテゴリの View 1 本だけなので、名前 challenge-viewport とカードのアンカーは木の中で重ならない
    /// The viewport lives as long as the view (I-fix6 D-3); the component centers once only when a view is created, and revisits and reopens keep that view's viewport (ruling 2026-11-04); the host holds only the shown category's view, so the challenge-viewport name and card anchors never repeat in the tree
    /// </remarks>
    public sealed class ChallengeCategoryGraphHost : VisualElement
    {
        private readonly IUiOsCursor _osCursor;
        private readonly Dictionary<Guid, ChallengeCategoryGraph> _graphs = new();

        public ChallengeCategoryGraphHost(IUiOsCursor osCursor)
        {
            _osCursor = osCursor;
            AddToClassList("challenge-list__graph-host");
            pickingMode = PickingMode.Ignore;
        }

        public void Render(ChallengeCategoryViewModel category)
        {
            // 初めて訪れたカテゴリは View を作る（作った View だけが進行中へ寄せる）。訪れたことがあれば中身を描き直す
            // A first visit creates a view (only a new view centers on the current node); a visited category redraws its content
            if (_graphs.TryGetValue(category.Guid, out var graph)) graph.Render(category);
            else
            {
                graph = new ChallengeCategoryGraph(category, _osCursor);
                _graphs.Add(category.Guid, graph);
            }

            // 表示中のカテゴリの View だけを置く。前のカテゴリの View は外して控え、視点を持ったまま待つ
            // Attach only the shown category's view; the previous one is detached and kept with its viewport
            // silent-ok: 既に置いてあれば付け替えない / 正常系（同じカテゴリの描き直し）。付け替えと視点は L1 ChallengeListViewportTest が見る
            if (graph.View.parent == this) return;
            Clear();
            Add(graph.View);
        }

        public void UnmountGraph()
        {
            // 解放済みのカテゴリが無い一覧はグラフを置かない（控えた View は残す）
            // A list without unlocked categories attaches no graph (kept views stay)
            Clear();
        }
    }
}
```

- [ ] **Step 4: 一覧の View を書き、tss に取り込み、Pending の行を消す**

`moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Tree/ChallengeListView.cs`:
```csharp
using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Challenge.Parts;
using Client.Ui.State.Challenge.Commands;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧（題・カテゴリのボタン列・ノードグラフ）。web: moorestech_web/webui/src/features/challenge/ChallengePanel.tsx
    /// Challenge list (title, category buttons, node graph); web: moorestech_web/webui/src/features/challenge/ChallengePanel.tsx
    /// </summary>
    public sealed class ChallengeListView : VisualElement
    {
        private readonly ChallengeCategoryButtons _categories;
        private readonly ChallengeCategoryGraphHost _graphHost;

        public ChallengeListView(IChallengeListCommands commands, IUiOsCursor osCursor)
        {
            // 根は一覧の面。拾うので一覧の上の押下とホイールは UI に当たる（右短押しで閉じず、ホットバーへ抜けない）
            // The root is the list face; it picks, so presses and wheels over the list hit UI (no close on a right short press, no wheel to the hotbar)
            name = "challenge-panel";
            AddToClassList("challenge-list");
            TutorialAnchor.Mark(this, AnchorIds.ChallengePanel);

            // 題（Title order 2）・カテゴリ列・グラフの置き場を縦に積む
            // Stack the title (Title order 2), the category row and the graph host
            var title = new LocalizedLabel();
            title.AddToClassList(UiTypographyClass.TitleOrder2);
            title.AddToClassList("challenge-list__title");
            title.SetText(new UiText(LocalizationKeys.Ui.Challenge.Title));
            Add(title);
            _categories = new ChallengeCategoryButtons(commands);
            Add(_categories);
            _graphHost = new ChallengeCategoryGraphHost(osCursor);
            Add(_graphHost);
        }

        public void Render(ChallengeListViewModel model)
        {
            // 空か選択ありかの分岐はここの 1 か所。部品は null を見ない
            // The only branch between empty and selected lives here; the parts never see null
            switch (model)
            {
                case SelectedChallengeListViewModel selected:
                    _categories.Render(selected);
                    _graphHost.Render(selected.Selected);
                    break;
                case EmptyChallengeListViewModel:
                    // 解放済みのカテゴリが無ければ題だけ（Web は selected が無いと TreeView を描かない）
                    // Without an unlocked category only the title remains (the Web skips TreeView without a selection)
                    _categories.ClearButtons();
                    _graphHost.UnmountGraph();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, null);
            }
        }

        public void SetShown(bool shown)
        {
            // 隠す間も要素を残し、控えた各カテゴリの View の視点を保つ（Foundation の UiDisplay があればその 1 行に替える）
            // Keep the element while hidden so every kept category view keeps its viewport (replace with the Foundation UiDisplay line when it exists)
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
```

`SetShown` の本体は Task 1 Step 1 の照合で分ける: `Foundation/Layout/UiDisplay.cs` があれば（S5-crosscut が先に入った）`UiDisplay.SetShown(this, shown);` の 1 行にし、`using Client.Ui.Foundation.Layout;` を足す（S5-crosscut R-4「表示の唯一の書き口」）。無ければ上の直書きのまま（S5-crosscut D-20 の置換対象に入る）。どちらにしたかを `done.md` の「証拠」に書く

`moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の `/* == section: Screens == */` 区画の末尾（今の最終行 `Skit.uss` の次）に 1 行足す:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/Challenge/Tree/ChallengeList.uss");
```

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` の辞書の 3 行（`AnchorIds.ChallengePanel`・`AnchorIds.ChallengeCategories`・`AnchorIds.ChallengeNodePrefix` の `"S5-challenge"`）を消し、辞書を空にする（クラスと規則の説明コメントは残す。ファイルを消すのは S5-ladder）:
```csharp
        public static readonly IReadOnlyDictionary<string, string> Owners = new Dictionary<string, string>();
```
`using Client.Game.InGame.Tutorial.UIHighlight;` が使われなくなったら消す。

- [ ] **Step 5: L1 の材料を書く**

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree/RecordingChallengeListCommands.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Challenge.Commands;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の操作を記録するだけの差し替え
    /// Test double that only records challenge list operations
    /// </summary>
    internal sealed class RecordingChallengeListCommands : IChallengeListCommands
    {
        public List<Guid> Selected { get; } = new();

        public void SelectCategory(Guid categoryGuid)
        {
            Selected.Add(categoryGuid);
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeListScreenTestKit.cs`:
```csharp
using System;
using System.Text.RegularExpressions;
using Client.Game.Localization;
using Client.Localization;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Challenge.CurrentHud;
using Client.Ui.Screens.Challenge.Tree;
using Client.Ui.State.Challenge;
using Client.Ui.State.Challenge.Commands;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using Client.Ui.State.Text;
using Core.Master;
using Game.Context;
using Mod.Loader;
using Mooresmaster.Localization.Generated;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の L1 の共通材料（テスト mod の GUID で Web の mock と同じ形の木を組む）
    /// Shared material for challenge list L1 tests (builds the Web mock's tree shape with test mod GUIDs)
    /// </summary>
    internal static class ChallengeListScreenTestKit
    {
        public static readonly Guid Basics = Guid.Parse("03ca4ded-3b2b-4e7f-bb6e-430f060c4ed1");
        public static readonly Guid Logistics = Guid.Parse("35330f9d-f44f-493d-a6bc-07ae6413d7c4");
        public static readonly Guid FirstCraft = Guid.Parse("00000000-0000-0000-4567-000000000001");
        public static readonly Guid SecondStep = Guid.Parse("00000000-0000-0000-4567-000000000002");
        public static readonly Guid LockedStep = Guid.Parse("00000000-0000-0000-4567-000000000003");
        public static readonly Guid LogisticsDone = Guid.Parse("c2d84cfb-73cb-4ffa-99b5-9703c550f330");
        public static readonly Guid LogisticsCurrent = Guid.Parse("74fe5f1f-5671-4924-ad36-4447fd80a9c3");

        private static readonly Regex RichTextTag = new("<[^>]+>");

        // テスト mod を読み、名前・概要・カテゴリ名をマスタの原文で引けるようにする（前例 LocalizeContentTest）
        // Load the test mod so titles, summaries and category names resolve to master source text (precedent LocalizeContentTest)
        public static void LoadMasterAndDictionary()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var masterContainer = ServerContext.GetService<MasterJsonFileContainer>();
            Localize.MergeGameDictionaries(ServerContext.GetService<ModsResource>(), masterContainer.SortedModIds, MasterSourceTextCollector.Collect());
        }

        // Web の mock（presentationFixtures.ts の challengeTree）と同じ形。Basics に未解放を 1 つ足す
        // Same shape as the Web mock (challengeTree in presentationFixtures.ts) plus one locked node in Basics
        public static ChallengeListViewModel Tree(ChallengeCategorySelection selection)
        {
            var basics = ChallengeCategoryViewModel.Create(Basics, new[]
            {
                Node(FirstCraft, Vector2.zero, ChallengeNodeState.Completed),
                Node(SecondStep, new Vector2(220f, 0f), ChallengeNodeState.Current, FirstCraft),
                Node(LockedStep, new Vector2(0f, -200f), ChallengeNodeState.Locked, SecondStep),
            });
            var logistics = ChallengeCategoryViewModel.Create(Logistics, new[]
            {
                Node(LogisticsDone, Vector2.zero, ChallengeNodeState.Completed),
                Node(LogisticsCurrent, new Vector2(1400f, 700f), ChallengeNodeState.Current, LogisticsDone),
            });
            return ChallengeListViewModel.Create(new[] { basics, logistics }, selection);
        }

        // 一覧を描いてから OverlayPanel・Stage 層へ載せる（前例 ResearchTreePresenter）
        // Render the list, then mount it on the OverlayPanel/Stage layer (precedent ResearchTreePresenter)
        public static ChallengeListView MountList(UiLayerStack layers, IChallengeListCommands commands)
        {
            var view = new ChallengeListView(commands, new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            view.Render(Tree(ChallengeCategorySelection.None));
            view.SetShown(true);
            layers.GetLayer(UiLayer.OverlayPanel, UiFamily.Stage).Add(view);
            return view;
        }

        // 進行中チャレンジ HUD（S2-hud）を Chrome・Viewport 層へ載せる
        // Mount the current-challenge HUD (S2-hud) on the Chrome/Viewport layer
        public static CurrentChallengeHudView MountHud(UiLayerStack layers)
        {
            var hud = new CurrentChallengeHudView();
            layers.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(hud);
            hud.Render(CurrentChallengeHudViewModel.Create(new[] { UiText.FromContent(ContentLocalizationKeys.ChallengeTitle(SecondStep)) }, false));
            return hud;
        }

        public static VisualElement Card(VisualElement root, Guid challengeGuid)
        {
            return root.Q($"challenge-node-{challengeGuid}");
        }

        // LocalizedLabel は行高のタグを前置するので外す（前例 ChallengeHudTestText.Plain）
        // LocalizedLabel prefixes a line-height tag, so strip it (precedent ChallengeHudTestText.Plain)
        public static string PlainText(Label label)
        {
            return RichTextTag.Replace(label.text, string.Empty);
        }

        public static string ButtonText(Button button)
        {
            return PlainText(button.Q<Label>());
        }

        private static ChallengeNodeCardViewModel Node(Guid guid, Vector2 position, ChallengeNodeState state, params Guid[] previous)
        {
            return new ChallengeNodeCardViewModel(guid, position, Vector2.one, previous, state);
        }
    }
}
```

- [ ] **Step 6: 一覧の L1 を書く**

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeListViewTest.cs`:
```csharp
using System;
using System.Linq;
using Client.Localization;
using Client.Tests.Ui.Components;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.Challenge.Tree;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Challenge.Tree;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Challenge.Tree.ChallengeListScreenTestKit;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の表示（L1。Web の challenge.spec の画面 1 件・カードの見た目・空の一覧・カテゴリ列の横スクロール・ホイールの行き先）
    /// Challenge list display (L1; one Web challenge.spec screen test, card look, empty list, category row scrolling, wheel target)
    /// </summary>
    public class ChallengeListViewTest : ScreenTestHost
    {
        private ChallengeListView _view;

        [SetUp]
        public void SetUp()
        {
            Kit.LoadMasterAndDictionary();
            var layers = UiLayerStack.Build(rootVisualElement);
            _view = Kit.MountList(layers, new RecordingChallengeListCommands());
            Kit.MountHud(layers);
            Settle();
        }

        /// <web-test file="e2e/tests/challenge.spec.ts">チャレンジ画面が開きツリーだけを翻訳済み表示する</web-test>
        [Test]
        public void チャレンジ画面が開きツリーだけを翻訳済み表示する()
        {
            // Web と同じく日本語で見る
            // Read it in Japanese like the Web
            Localize.TrySetChosenLanguage("japanese");
            Settle();

            // 枠・カテゴリのボタン・2 枚のカード・題が出る
            // The frame, category button, two cards and title show
            Assert.IsTrue(IsShown(Find<VisualElement>(rootVisualElement, "challenge-panel")));
            var basics = Find<FormButton>(rootVisualElement, $"challenge-category-{Kit.Basics}");
            Assert.AreEqual(MasterHolder.ChallengeMaster.GetChallengeCategory(Kit.Basics).CategoryName, Kit.ButtonText(basics));
            Assert.IsTrue(IsShown(Kit.Card(rootVisualElement, Kit.FirstCraft)));
            Assert.IsTrue(IsShown(Kit.Card(rootVisualElement, Kit.SecondStep)));
            Assert.AreEqual("チャレンジ", Kit.PlainText(_view.Q<Label>(className: "challenge-list__title")));

            // 名前と状態は翻訳済み
            // Titles and states are localized
            var first = Kit.Card(rootVisualElement, Kit.FirstCraft);
            Assert.AreEqual(MasterHolder.ChallengeMaster.GetChallenge(Kit.FirstCraft).Title, Kit.PlainText(first.Q<Label>(className: "challenge-list__card-title")));
            var states = _view.Query<Label>(className: "challenge-list__card-state").ToList().Select(Kit.PlainText).ToList();
            CollectionAssert.Contains(states, "完了");
            CollectionAssert.Contains(states, "進行中");

            // 内部キーと欠けの印が残らず、進行中 HUD も出ている
            // No internal keys or missing markers remain, and the current-challenge HUD shows
            foreach (var label in _view.Query<Label>().ToList())
            {
                StringAssert.DoesNotContain("challenge.", label.text);
                StringAssert.DoesNotContain("[!", label.text);
            }
            Assert.IsTrue(IsShown(Find<VisualElement>(rootVisualElement, "challenge-hud")));
        }

        [Test]
        public void カードは中身なりの高さで幅240と状態の輪と減光を持つ()
        {
            // 包みの高さ 0 で潰れず、上下の余白 12 と 3 行の高さの和になる（S4-research の NodeGraphView.uss の事後承認）
            // Not crushed by a zero-height wrapper: 12 padding on each side plus three rows (post-approval of S4-research's NodeGraphView.uss)
            var padding = ThemeTokenFile.ResolvePixels("--mantine-spacing-sm");
            foreach (var guid in new[] { Kit.FirstCraft, Kit.SecondStep, Kit.LockedStep })
            {
                var card = Kit.Card(rootVisualElement, guid);
                var rows = card.Query<Label>().ToList().Sum(label => label.layout.height);
                Assert.AreEqual(240f, card.layout.width, 0.5f, $"{guid} の幅");
                Assert.AreEqual(rows + padding * 2f, card.layout.height, 0.5f, $"{guid} の高さ");
                Assert.GreaterOrEqual(card.layout.height, 24.8f + 20.3f + 16.8f + padding * 2f - 0.5f, $"{guid} が 3 行の最小より低い");
            }

            // 完了は green-6・進行中は blue-5 の 2px の輪、未解放は輪なしで 0.55
            // Completed has a 2px green-6 ring, current blue-5, locked none and 0.55 opacity
            AssertRing(Kit.FirstCraft, "--mantine-color-green-6");
            AssertRing(Kit.SecondStep, "--mantine-color-blue-5");
            var locked = Kit.Card(rootVisualElement, Kit.LockedStep);
            Assert.AreEqual(DisplayStyle.None, locked.Q(className: "challenge-list__card-ring").resolvedStyle.display);
            Assert.AreEqual(0.55f, locked.resolvedStyle.opacity, 0.001f);

            // 面は Paper（body）、一覧の地は dark-8、一覧の上端は上の安全帯
            // Card face is Paper (body), the list ground is dark-8, and the list top is the upper safe area
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-body"), Kit.Card(rootVisualElement, Kit.FirstCraft).resolvedStyle.backgroundColor, "カードの面");
            var panel = Find<VisualElement>(rootVisualElement, "challenge-panel");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-dark-8"), panel.resolvedStyle.backgroundColor, "一覧の地");
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--menu-upper-safe-area"), panel.worldBound.yMin, 0.5f);
        }

        [Test]
        public void 解放済みのカテゴリが無ければ題だけでグラフを置かない()
        {
            _view.Render(ChallengeListViewModel.Empty);
            Settle();
            Assert.IsNull(rootVisualElement.Q("challenge-viewport"));
            Assert.AreEqual(0, rootVisualElement.Query<FormButton>().ToList().Count);
            Assert.IsTrue(IsShown(_view.Q<Label>(className: "challenge-list__title")));
        }

        [Test]
        public void カテゴリのボタンが幅を越えたら横に送れ越えなければバーが出ない()
        {
            // 2 つのボタンは一覧の幅に収まるのでバーは出ず、列の高さはボタンの 36
            // Two buttons fit the list width, so no bar shows and the row is the 36 button height
            var row = Find<VisualElement>(rootVisualElement, "challenge-categories");
            var scroll = Find<GameScrollView>(rootVisualElement, "challenge-categories-scroll");
            Assert.IsFalse(IsShown(scroll.horizontalScroller), "収まるときのバー");
            Assert.AreEqual(36f, row.layout.height, 0.5f, "収まるときの列の高さ");

            // 列を 120px に狭めると越えるので、バーが出て末尾のボタンまで送れる（Web overflow-x: auto）
            // Narrowing the row to 120px overflows it, so the bar shows and the last button scrolls into view (Web overflow-x: auto)
            row.style.width = 120f;
            Settle();
            Assert.IsTrue(IsShown(scroll.horizontalScroller), "越えたときのバー");
            var last = Find<FormButton>(rootVisualElement, $"challenge-category-{Kit.Logistics}");
            Assert.Greater(last.worldBound.xMax, scroll.contentViewport.worldBound.xMax, "送る前は末尾のボタンがはみ出す");
            scroll.ScrollTo(last);
            Settle();
            Assert.LessOrEqual(last.worldBound.xMax, scroll.contentViewport.worldBound.xMax + 0.5f, "送ると末尾のボタンが見える");

            // バーはゲームのスクロール領域と同じ GameScrollView のバー（太さ --scrollarea-scrollbar-size・つまみ game-scroll-view-thumb-face）で、列はバーの分だけ高い
            // The bar is the game's GameScrollView bar (--scrollarea-scrollbar-size thick, game-scroll-view-thumb-face thumb), and the row grows by the bar
            var barSize = ThemeTokenFile.ResolvePixels("--scrollarea-scrollbar-size");
            Assert.AreEqual(barSize, scroll.horizontalScroller.layout.height, 0.5f, "バーの太さ");
            var thumb = scroll.horizontalScroller.Q(className: "unity-base-slider__dragger");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--game-scroll-view-thumb-face"), thumb.resolvedStyle.backgroundColor, "つまみの色");
            Assert.AreEqual(36f + barSize, row.layout.height, 0.5f, "越えたときの列の高さ");
        }

        [Test]
        public void 一覧の題とカテゴリ列の上のホイールは装備とホットバーへ抜けない()
        {
            // 装備・ホットバーのホイールの読み手と同じ判定（EquipmentWheelInput は拾った要素を IsBlockingUi に通す）
            // The same judge the equipment/hotbar wheel reader uses (EquipmentWheelInput runs the picked element through IsBlockingUi)
            var title = _view.Q<Label>(className: "challenge-list__title");
            var row = Find<VisualElement>(rootVisualElement, "challenge-categories");
            var basics = Find<FormButton>(rootVisualElement, $"challenge-category-{Kit.Basics}");
            var rowEnd = new Vector2(row.worldBound.xMax - 8f, row.worldBound.center.y);
            foreach (var (where, point) in new[] { ("題", title.worldBound.center), ("カテゴリのボタン", basics.worldBound.center), ("カテゴリ列の空き", rowEnd) })
            {
                var picked = rootVisualElement.panel.Pick(point);
                Assert.IsTrue(EquipmentWheelPickJudge.IsBlockingUi(picked), $"{where} の上のホイールが抜ける（当たり={picked?.name}）");
            }
        }

        private void AssertRing(Guid guid, string colorToken)
        {
            var ring = Kit.Card(rootVisualElement, guid).Q(className: "challenge-list__card-ring");
            Assert.AreEqual(DisplayStyle.Flex, ring.resolvedStyle.display, $"{guid} の輪");
            Assert.AreEqual(2f, ring.resolvedStyle.borderTopWidth, 0.01f, $"{guid} の輪の幅");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor(colorToken), ring.resolvedStyle.borderTopColor, $"{guid} の輪の色");
        }

        // 中央寄せと逃げの読み取り・バーの出し入れの数フレームを流す
        // Run a few frames for clearance reads, centering and bar toggling
        private void Settle()
        {
            for (var i = 0; i < 4; i++) simulate.FrameUpdate();
        }
    }
}
```

- [ ] **Step 7: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Screens\.Challenge\.Tree\.ChallengeListViewTest|Anchor|Conventions|Theme|Layers)"`
Expected: ErrorCount 0 / PASS（`ChallengeListViewTest` 5 件・`TutorialAnchorCoverageTest`・`UiStyleConventionTest`・`UiThemeImportBundleTest`・`UiLayerDependencyDirectionTest` を含む）
- 日本語へ切り替えた後に名前が `[!challenge…]` になったら、言語の切り替えで合成し直した辞書にマスタの原文が残っていない。`done.md` の「未確認」に書き、`Localize.TrySetChosenLanguage("japanese")` の後に `Kit.LoadMasterAndDictionary()` の `MergeGameDictionaries` の 1 行をもう一度呼ぶ形に直す（本番は `MasterSourceTextCollector` を起動口で合成するので同じ欠けは無い。直した理由を同じ行に書く）
- カードの高さが 0 か 18 前後なら `NodeGraphView.uss` の包みが中身を潰している。部品を直さず止めて指揮へ（S4-research の修正の後退）
- 横スクロールの列の高さが 0 なら、`GameScrollView` の横向きの content container が流れから外れている。Task 2b の `GameScrollViewHorizontalTest` が緑かを先に見て、部品の側（`GameScrollView.uss` の横の区画）で直す。画面の USS に `.unity-scroll-view__*` を書かない。ボタンが縮んで越えないなら `.challenge-list__category-button`・`.challenge-list__category-group` の `flex-shrink: 0` が当たっているかを `uloop execute-dynamic-code` で `resolvedStyle.flexShrink` を読んで確かめる
- バーの太さ・つまみの色が違えば部品の横向きの不具合なので、Task 2b の切り分け（`GameScrollView.uss` の横の区画）へ戻る。画面の USS で上書きしない（`Theme.uss` も触らない）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree
git commit -m "feat(uitk): S5-challenge の一覧の View・カード・カテゴリ列と横スクロール・カテゴリごとのグラフと L1"
```
（Unity が作った `.meta` も同じコミットに入れる。手で作らない）

---

### Task 4: 視点と常駐 HUD の L1

**Files:**
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeListViewportTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeHudOverListTest.cs`

**Interfaces:**
- Consumes: Task 3 の `ChallengeListView`・`ChallengeCategoryGraphHost`・`ChallengeListScreenTestKit`・`RecordingChallengeListCommands`
- Produces: L1 4 件（タグ付き 2・裁定 2）

- [ ] **Step 1: 視点の L1 を書く**

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeListViewportTest.cs`:
```csharp
using System;
using System.Linq;
using Client.Ui.Components.Controls;
using Client.Ui.Components.NodeGraph;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Challenge.Tree.ChallengeListScreenTestKit;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の視点（L1。Web の challenge.spec 1 件と裁定 2026-11-04 の初回訪問・再訪・開き直し）
    /// Challenge list viewport (L1; one Web challenge.spec test and the 2026-11-04 ruling on first visit, revisit and reopen)
    /// </summary>
    public class ChallengeListViewportTest : ScreenTestHost
    {
        private RecordingChallengeListCommands _commands;
        private ChallengeListView _view;

        [SetUp]
        public void SetUp()
        {
            Kit.LoadMasterAndDictionary();
            _commands = new RecordingChallengeListCommands();
            _view = Kit.MountList(UiLayerStack.Build(rootVisualElement), _commands);
            Settle();
        }

        /// <web-test file="e2e/tests/challenge.spec.ts">チャレンジ画面は進行中のチャレンジを中央にして開き、カテゴリ切替でもその進行中へ寄せる</web-test>
        [Test]
        public void 進行中のチャレンジを中央にして開きカテゴリ切替でもその進行中へ寄せる()
        {
            AssertCentered(Kit.SecondStep);

            // ボタンでカテゴリを選ぶと操作が 1 回出て、選んだカテゴリの進行中へ寄る
            // Pressing a category button issues one operation and centers on that category's current node
            Click(CategoryButton(Kit.Logistics));
            CollectionAssert.AreEqual(new[] { Kit.Logistics }, _commands.Selected);
            Select(Kit.Logistics);
            AssertCentered(Kit.LogisticsCurrent);
            Assert.IsTrue(CategoryButton(Kit.Logistics).ClassListContains(FormButton.FilledUssClassName));
            Assert.IsTrue(CategoryButton(Kit.Basics).ClassListContains(FormButton.SubtleUssClassName));
        }

        [Test]
        public void 一度開いたカテゴリへ戻ると寄せ直さず前回の視点を戻す()
        {
            // 先頭のカテゴリでパンし、別のカテゴリへ移って戻る。木の中の NodeGraphView は常に 1 本
            // Pan in the first category, move to another and come back; the tree always holds one NodeGraphView
            var basicsView = SingleGraph();
            var panned = PanBy(new Vector2(-60f, 30f));
            Select(Kit.Logistics);
            Assert.AreNotSame(basicsView, SingleGraph(), "別のカテゴリは別の View");
            Select(Kit.Basics);

            // 戻ると控えた同じ View が付け直され、作り直さないので寄せ直さない
            // Coming back re-attaches the same kept view; no rebuild, so no re-centering
            Assert.AreSame(basicsView, SingleGraph(), "再訪は控えた View の付け直し");
            var back = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound;
            Assert.AreEqual(panned.x, back.x, 1f);
            Assert.AreEqual(panned.y, back.y, 1f);

            // 2 つ目のカテゴリも初回の寄せのまま残る
            // The second category keeps its first-visit centering
            Select(Kit.Logistics);
            AssertCentered(Kit.LogisticsCurrent);
        }

        [Test]
        public void 閉じて開き直すと先頭のカテゴリへ戻り前回の視点を戻す()
        {
            var panned = PanBy(new Vector2(-60f, 30f));
            Select(Kit.Logistics);

            // 閉じると選択ストアが無しに戻すので、開き直しは選択無しで描く（先頭のカテゴリ）
            // Closing resets the selection store, so reopening renders with no selection (the first category)
            _view.SetShown(false);
            Settle();
            _view.SetShown(true);
            _view.Render(Kit.Tree(ChallengeCategorySelection.None));
            Settle();
            var reopened = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound;
            Assert.AreEqual(panned.x, reopened.x, 1f);
            Assert.AreEqual(panned.y, reopened.y, 1f);
            Assert.IsTrue(CategoryButton(Kit.Basics).ClassListContains(FormButton.FilledUssClassName));
        }

        #region Helpers

        private FormButton CategoryButton(Guid category)
        {
            return Find<FormButton>(rootVisualElement, $"challenge-category-{category}");
        }

        private void Select(Guid category)
        {
            _view.Render(Kit.Tree(ChallengeCategorySelection.Of(category)));
            Settle();
        }

        // 置き場に付いている View は常に 1 本（名前とアンカーが重ならない）
        // Exactly one view is attached (names and anchors never repeat)
        private NodeGraphView SingleGraph()
        {
            var graphs = rootVisualElement.Query<NodeGraphView>().ToList();
            Assert.AreEqual(1, graphs.Count, "木の中の NodeGraphView の数");
            return graphs[0];
        }

        private void AssertCentered(Guid challenge)
        {
            var viewport = Find<VisualElement>(rootVisualElement, "challenge-viewport").worldBound.center;
            var node = Kit.Card(rootVisualElement, challenge).worldBound.center;
            Assert.AreEqual(viewport.x, node.x, 1.5f, $"{challenge} の中心 x");
            Assert.AreEqual(viewport.y, node.y, 1.5f, $"{challenge} の中心 y");
        }

        // 空の背景を掴んで動かし、進行中のカードが同じだけ動いたことを見て、その位置を返す
        // Drag the empty background, check the current card moved by the same amount, and return its rect
        private Rect PanBy(Vector2 delta)
        {
            var before = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound;
            Drag(EmptyBackgroundPoint(), delta);
            var after = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound;
            Assert.LessOrEqual(after.x - before.x, delta.x + 0.5f);
            Assert.GreaterOrEqual(after.y - before.y, delta.y - 0.5f);
            return after;
        }

        // カードの無い点を右上から探す
        // Probe from the top right for a point without a card
        private Vector2 EmptyBackgroundPoint()
        {
            var box = Find<VisualElement>(rootVisualElement, "challenge-viewport").worldBound;
            for (var inset = 40f; inset < Mathf.Min(box.width, box.height) / 2f; inset += 8f)
            {
                var point = new Vector2(box.xMax - inset, box.yMin + inset);
                var picked = rootVisualElement.panel.Pick(point);
                if (picked == null || picked.GetFirstAncestorOfType<NodeGraphView>() == null && !(picked is NodeGraphView)) continue;
                if (picked is PaperSurface || picked.GetFirstAncestorOfType<PaperSurface>() != null) continue;
                return point;
            }
            throw new InvalidOperationException("チャレンジ一覧に空の背景の掴み点が無い");
        }

        private void Click(VisualElement target)
        {
            simulate.MouseDown(target.worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(target.worldBound.center, MouseButton.LeftMouse);
            Settle();
        }

        private void Drag(Vector2 from, Vector2 delta)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(from + delta * 0.5f);
            simulate.MouseMove(from + delta);
            simulate.MouseUp(from + delta, MouseButton.LeftMouse);
            Settle();
        }

        // 逃げの読み取り・中央寄せ・滑走の数フレームを流す
        // Run a few frames for clearance reads, centering and glide
        private void Settle()
        {
            for (var i = 0; i < 4; i++) simulate.FrameUpdate();
        }

        #endregion
    }
}
```

- [ ] **Step 2: 常駐 HUD の L1 を書く**

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree/ChallengeHudOverListTest.cs`:
```csharp
using Client.Ui.Foundation.Layers;
using NUnit.Framework;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Challenge.Tree.ChallengeListScreenTestKit;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// 進行中チャレンジ HUD とチャレンジ一覧の重なり（L1。Web の challenge.spec の常駐 HUD の一覧の部分。画面を跨ぐ部分は録画 uitk-challenge-hud-resident）
    /// Current-challenge HUD over the challenge list (L1; the list part of the Web challenge.spec resident HUD test; the cross-screen part is the uitk-challenge-hud-resident recording)
    /// </summary>
    public class ChallengeHudOverListTest : ScreenTestHost
    {
        /// <web-test file="e2e/tests/challenge.spec.ts">常駐HUDをインベントリ・メニュー・操作モードで維持する</web-test>
        [Test]
        public void 常駐HUDはチャレンジ一覧を開いても同じ位置のまま一覧とカテゴリより上に出る()
        {
            // 一覧を隠した状態（ゲーム画面）で HUD の位置を測る
            // Measure the HUD with the list hidden (the game screen)
            Kit.LoadMasterAndDictionary();
            var layers = UiLayerStack.Build(rootVisualElement);
            var list = Kit.MountList(layers, new RecordingChallengeListCommands());
            var hud = Kit.MountHud(layers);
            list.SetShown(false);
            Settle();
            var world = hud.worldBound;

            // 一覧を開いても HUD は出たまま、位置と寸法は変わらない
            // Opening the list keeps the HUD shown with the same position and size
            list.SetShown(true);
            Settle();
            Assert.IsTrue(IsShown(hud));
            Assert.AreEqual(world.x, hud.worldBound.x, 0.05f);
            Assert.AreEqual(world.y, hud.worldBound.y, 0.05f);
            Assert.AreEqual(world.width, hud.worldBound.width, 0.05f);
            Assert.AreEqual(world.height, hud.worldBound.height, 0.05f);

            // 一覧（上の安全帯つき）とカテゴリのボタンより上
            // Above the list (with its upper safe area) and the category buttons
            Assert.LessOrEqual(hud.worldBound.yMax, Find<VisualElement>(rootVisualElement, "challenge-panel").worldBound.yMin + 0.05f);
            Assert.LessOrEqual(hud.worldBound.yMax, Find<VisualElement>(rootVisualElement, $"challenge-category-{Kit.Basics}").worldBound.yMin + 0.05f);

            // 中心の当たりは HUD 自身（Chrome 層が OverlayPanel 層より上）。判定の間だけ根を拾わせる
            // A hit at the center lands on the HUD (Chrome is above OverlayPanel); the root picks only during the check
            var savedPicking = hud.pickingMode;
            hud.pickingMode = PickingMode.Position;
            var picked = rootVisualElement.panel.Pick(hud.worldBound.center);
            hud.pickingMode = savedPicking;
            Assert.IsTrue(picked == hud || hud.Contains(picked), picked?.name);
        }

        private void Settle()
        {
            for (var i = 0; i < 4; i++) simulate.FrameUpdate();
        }
    }
}
```

- [ ] **Step 3: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Challenge\.Tree\."`
Expected: ErrorCount 0 / 9 件 PASS（`ChallengeListViewTest` 5・`ChallengeListViewportTest` 3・`ChallengeHudOverListTest` 1）
- 2 つ目・3 つ目が 1px を超えてずれたら、戻ったカテゴリで `ChallengeCategoryGraph` を作り直していないか（`_graphs` に控えた View を付け直しているか・`AreSame` が通るか）と、付け直しの `GeometryChangedEvent` で部品の寸法の追従が働いていないか（寸法が変わっていないなら動かないはず）を `uloop execute-dynamic-code` で `contentRect` を読んで確かめる。部品は直さない
- `SingleGraph` が 2 本以上を数えたら、`ChallengeCategoryGraphHost.Render` が前の View を外さずに足している。置き場の子を見て直す

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Challenge/Tree
git commit -m "test(uitk): S5-challenge の視点（初回の寄せ・再訪・開き直し）と常駐 HUD の L1"
```

---

### Task 5: Presenter と登録、全体スイッチ UITK での起動

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Tree/ChallengeListPresenter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/Screens/Challenge/ChallengeGameUiRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（`using` 1 行・画面登録区画の末尾 1 行）

**Interfaces:**
- Consumes: Task 2 の `ChallengeScreenVisibility.IsShown`・`ChallengeListModel`・`ChallengeCategorySelectionStore`・`ChallengeListCommands`、Task 3 の `ChallengeListView`
- Produces: `ChallengeListPresenter`（`IInitializable`。`Client.Ui.Screens.Challenge.Tree`）、`ChallengeGameUiRegistration.Register(ContainerBuilder builder)`

- [ ] **Step 1: Presenter を書く**

`moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Tree/ChallengeListPresenter.cs`:
```csharp
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Challenge;
using Client.Ui.State.Challenge.Commands;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の登録・表示・描画（前例 ResearchTreePresenter）
    /// Mounts, shows and draws the challenge list (precedent ResearchTreePresenter)
    /// </summary>
    public sealed class ChallengeListPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly ChallengeListModel _model;
        private readonly UiRoutingState _routing;
        private readonly IChallengeListCommands _commands;
        private readonly IUiOsCursor _osCursor;

        public ChallengeListPresenter(GameUiRoot gameUiRoot, ChallengeListModel model, UiRoutingState routing, IChallengeListCommands commands, IUiOsCursor osCursor)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
            _routing = routing;
            _commands = commands;
            _osCursor = osCursor;
        }

        public void Initialize()
        {
            // 描いてから載せる（キーの無いラベルを載せない）。一覧は Web の --z-stage-overlay-panel
            // Render before mounting (no keyless labels attached); the list is the Web --z-stage-overlay-panel
            var view = new ChallengeListView(_commands, _osCursor);
            view.Render(_model.Current.Value);
            _gameUiRoot.GetLayer(UiLayer.OverlayPanel, UiFamily.Stage).Add(view);
            _routing.Current.Subscribe(snapshot => view.SetShown(ChallengeScreenVisibility.IsShown(snapshot)));
            _model.Current.Subscribe(view.Render);
        }
    }
}
```

- [ ] **Step 2: 登録を書く**

`moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/Screens/Challenge/ChallengeGameUiRegistration.cs`:
```csharp
using Client.Ui.Screens.Challenge.Tree;
using Client.Ui.State.Challenge.Commands;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens.Challenge
{
    // チャレンジ一覧（GameUi）の登録。状態の源 ChallengeProgressState は MainGameUiStateRegistration が持つ
    // Registers the GameUi challenge list; the ChallengeProgressState source lives in MainGameUiStateRegistration
    internal static class ChallengeGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ChallengeCategorySelectionStore>().AsSelf();
            builder.RegisterEntryPoint<ChallengeListModel>().AsSelf();
            builder.Register<ChallengeListCommands>(Lifetime.Singleton).As<IChallengeListCommands>();
            builder.RegisterEntryPoint<ChallengeListPresenter>();
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`:
- `using Client.Starter.Registration.GameUi.Screens;` の次の行に `using Client.Starter.Registration.GameUi.Screens.Challenge;` を足す
- 画面登録区画の末尾（今の最終行 `SkitGameUiRegistration.Register(builder);` の次）に `ChallengeGameUiRegistration.Register(builder);` を足す

- [ ] **Step 3: コンパイルし、全体スイッチ UITK で起動してエラーが無いことを確かめる**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'
uloop clear-console --project-path ./moorestech_client
uloop control-play-mode --project-path ./moorestech_client --action Play
```
45 秒待ってから:
```bash
uloop get-logs --project-path ./moorestech_client --log-type Error
uloop control-play-mode --project-path ./moorestech_client --action Stop
```
Expected: ErrorCount 0、起動後の Error ログ 0 件（`[NodeGraphScene]` の LogError・VContainer の解決失敗が無い）。出たらその行を読んで直してから先へ進む

- [ ] **Step 4: 全画面の載り先の L3 に一覧を足す（S5-crosscut T14 が先に入っているときだけ）**

Task 1 Step 1 で `GameUiScreenMountTest.cs` があったら、その「Presenter が載せる最上位の View 型」の一覧（`Q<T>()` で 1 つずつあることを見る並び。`CurrentChallengeHudView` の行の近く）に `ChallengeListView` の行を 1 つ足し、`using Client.Ui.Screens.Challenge.Tree;` を足す。足し方は実物の並びの書き方に合わせる。
Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GameUiScreenMountTest"`（L3。終わったら 45 秒待ってから次）
Expected: PASS。一覧の型が見つからないと落ちるなら、Presenter の登録（Step 2）が GameUi の系で解かれていない
- 無ければ足さず、`done.md` の「integrator への依頼」に「S5-crosscut T14 の `GameUiScreenMountTest` の一覧に `ChallengeListView`（`Client.Ui.Screens.Challenge.Tree`。`UiLayer.OverlayPanel`・`UiFamily.Stage`）を足す」と書く

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge/Tree/ChallengeListPresenter.cs moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest
git commit -m "feat(uitk): S5-challenge のチャレンジ一覧を GameUi に登録"
```

---

### Task 6: unityプレイ録画テスト（`uitk-challenge-list`・`uitk-challenge-hud-resident`）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-challenge-list.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-challenge-hud-resident.cs`

**Interfaces:**
- Consumes: Task 5 までの GameUi のチャレンジ一覧、`ChallengeProgressState`（`ClientDIContext.DIContainer.DIContainerResolver.Resolve<T>()`）、DSL（`PressKey`・`WaitUiState`・`UntilUiElement`・`ExitToGameScreen`・`Hotbar.AssignHotbar`・`Hotbar.EnterBuildMode`・`Screenshot`・`PlaytestGameViewOps.SetResolution`）
- Produces: 2 本の `result.json`・mp4・スクショ（`01-open-centered-1280x720`・`01b-open-centered-2560x1080` は Task 7 の並べ比べに使う）

- [ ] **Step 1: 画面の録画を書く**

`.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-challenge-list.cs`:
```csharp
// チャレンジ一覧を UITK（GameUi）で通す: T で開いて進行中のカードへ中央寄せ・2560×1080 でも中央・アンカー・中身なりの高さのカード・HUD が一覧より上・パンして T で閉じて開き直しても視点が残る・一覧の上の右短押しでは閉じない・Tab でインベントリ
// Runs the challenge list on UITK (GameUi): open with T centered on the current card, still centered at 2560x1080, anchors, content-height cards, HUD above the list, pan then close and reopen with T keeps the view, a right short press over the list does not close, Tab goes to the inventory
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Challenge;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

return PlaytestRunner.Run("uitk-challenge-list", new PlaytestRunOptions { Record = true }, async p =>
{
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    var root = GameUiRoot.Instance.GetRootElement();

    // 解放済みの先頭カテゴリと、マスタの順で最初の進行中を源から引く（Web の categories[0] と findChallengeInitialFocusNode）
    // Take the first unlocked category and its first current challenge in master order from the source (Web categories[0] and findChallengeInitialFocusNode)
    var progress = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ChallengeProgressState>();
    var category = progress.Categories.First(c => c.IsUnlocked);
    var current = category.Category.Challenges.First(c => category.CurrentChallenges.Contains(c));
    var cardId = $"challenge-node-{current.ChallengeGuid}";
    p.Note($"カテゴリ {category.Category.CategoryName}（解放 {progress.Categories.Count(c => c.IsUnlocked)} 個）・進行中 {current.Title}");

    // T で開き、進行中のカードがビューポートの中央にある
    // Open with T; the current card sits at the viewport center
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
    await p.UntilUiElement("challenge-panel", 5f);
    var offset = await CenterOffset();
    p.Note($"進行中のカードの中心とビューポートの中心の差 {offset}");
    p.Assert(offset.magnitude <= 2f, "進行中のカードがビューポートの中心から 2px 以内");
    await p.Screenshot("01-open-centered-1280x720");

    // 2560×1080 でも寸法の追従で中央のまま。並べ比べの UITK 側を撮って 1280×720 へ戻す
    // At 2560x1080 resize-follow keeps it centered; take the UITK comparison shot and return to 1280x720
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 2560, 1080);
    await UniTask.DelayFrame(3);
    var wideOffset = await CenterOffset();
    p.Note($"2560×1080 の差 {wideOffset}");
    p.Assert(wideOffset.magnitude <= 3f, "2560×1080 でも進行中のカードが中央（3 物理 px 以内）");
    await p.Screenshot("01b-open-centered-2560x1080");
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    await UniTask.DelayFrame(3);

    // 枠・カテゴリ列・先頭カテゴリの全ノードのアンカーが一意に引ける
    // The frame, category row and every node of the first category resolve uniquely
    var anchorIds = new[] { AnchorIds.ChallengePanel, AnchorIds.ChallengeCategories }.Concat(category.Category.Challenges.Select(c => AnchorIds.ChallengeNode(c.ChallengeGuid.ToString()))).ToList();
    var unique = anchorIds.Count(id => TutorialAnchorRegistry.Resolve(root, id) is TutorialAnchorResolution.Unique);
    p.Note($"アンカー 一意 {unique}/{anchorIds.Count}");
    p.Assert(unique == anchorIds.Count, "枠・カテゴリ列・全ノードのアンカーが Unique");

    // カードは幅 240・中身なりの高さ（包みの高さ 0 で潰れない）
    // Cards are 240 wide with content height (not crushed by a zero-height wrapper)
    var card = root.Q(cardId);
    p.Note($"進行中のカードの寸法 {card.layout.size}");
    p.Assert(Mathf.Abs(card.layout.width - 240f) < 0.5f && card.layout.height > 60f, "カードが幅 240・中身なりの高さ");

    // 進行中 HUD は一覧とカテゴリのボタンより上、中心の当たりが HUD 自身（判定の間だけ根を拾わせる）
    // The current HUD is above the list and category buttons, and its center hit is the HUD (the root picks only during the check)
    var hud = root.Q("challenge-hud");
    p.Assert(hud.worldBound.yMax <= root.Q("challenge-panel").worldBound.yMin + 0.5f, "HUD が一覧の上端より上");
    p.Assert(hud.worldBound.yMax <= root.Q($"challenge-category-{category.Category.CategoryGuid}").worldBound.yMin + 0.5f, "HUD がカテゴリのボタンより上");
    var savedPicking = hud.pickingMode;
    hud.pickingMode = PickingMode.Position;
    var picked = root.panel.Pick(hud.worldBound.center);
    hud.pickingMode = savedPicking;
    p.Assert(picked != null && (picked == hud || hud.Contains(picked)), $"HUD の中心の当たりが HUD 自身（当たり={picked?.name}）");

    // 空の背景をパンし、T で閉じて T で開き直しても視点が残る
    // Pan on empty background; closing and reopening with T keeps the view
    var viewport = await PlaytestUiElementOps.FindUi("challenge-viewport");
    var empty = new Vector2(viewport.ScreenRect.xMax - 40f, viewport.ScreenRect.yMin + 40f);
    var beforePan = await CardCenter();
    SemanticInput.MouseMoveTo(empty);
    await p.WaitSeconds(0.5f);
    // 押下・移動・解放は 0.2 秒ずつ空ける（前例 uitk-research-tree の実測）
    // Space press, move and release by 0.2 s (measured in uitk-research-tree)
    SemanticInput.MouseButtonDown(0);
    await p.WaitSeconds(0.2f);
    await SemanticInput.MouseGlideTo(empty + new Vector2(-60f, 30f), 0.4f);
    await p.WaitSeconds(0.2f);
    SemanticInput.MouseButtonUp(0);
    var panned = await SettledCardCenter();
    p.Note($"パンでカードが {panned - beforePan} 動いた");
    p.Assert((panned - beforePan).magnitude > 30f, "背景のドラッグでパンした");
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
    await p.UntilUiElement("challenge-panel", 5f);
    var reopened = await CardCenter();
    p.Note($"開き直したカードの中心 {reopened}（閉じる前 {panned}）");
    p.Assert((reopened - panned).magnitude <= 1f, "開き直しても視点が残る（1px 以内）");
    await p.Screenshot("02-reopen-kept");

    // 一覧の上の右短押しでは閉じず、Tab でインベントリへ移ると一覧が消える
    // A right short press over the list does not close; Tab moves to the inventory and the list goes away
    SemanticInput.MouseMoveTo(empty);
    await p.WaitSeconds(0.5f);
    SemanticInput.MouseButtonDown(1);
    await p.WaitSeconds(0.1f);
    SemanticInput.MouseButtonUp(1);
    await p.WaitSeconds(0.5f);
    p.Assert(p.CurrentUiState == UIStateEnum.ChallengeList, "一覧の上の右短押しでは閉じない");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    p.Assert(!(await PlaytestUiElementOps.FindUi("challenge-panel")).IsFound, "インベントリへ移ると一覧が消える");

    #region Internal

    async UniTask<Vector2> CardCenter() => (await PlaytestUiElementOps.FindUi(cardId)).ScreenRect.center;

    async UniTask<Vector2> CenterOffset() => (await CardCenter()) - (await PlaytestUiElementOps.FindUi("challenge-viewport")).ScreenRect.center;

    // パンの滑走が止まるまで待ってからカードの中心を読む
    // Wait for the pan glide to stop before reading the card center
    async UniTask<Vector2> SettledCardCenter()
    {
        var last = await CardCenter();
        for (var i = 0; i < 25; i++)
        {
            await p.WaitSeconds(0.2f);
            var now = await CardCenter();
            if (now == last) return now;
            last = now;
        }
        return last;
    }

    #endregion
});
```

- [ ] **Step 2: 常駐 HUD の録画を書く**

`.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-challenge-hud-resident.cs`:
```csharp
// 進行中チャレンジ HUD の常駐を UITK（GameUi）で通す（Web challenge.spec「常駐HUDをインベントリ・メニュー・操作モードで維持する」の画面を跨ぐ部分）: ゲーム画面の位置のまま各画面で出続け、上の安全帯のあるメニューの中身より上、配置 HUD と横に分かれ、削除の帯より下、1024×576 でも同じ論理の位置
// Runs the resident current-challenge HUD on UITK (GameUi) (cross-screen part of the Web challenge.spec resident HUD test): same position as the game screen everywhere, above menu content with an upper safe area, beside the placement HUD, below the delete band, same logical position at 1024x576
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

return PlaytestRunner.Run("uitk-challenge-hud-resident", new PlaytestRunOptions { Record = true }, async p =>
{
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    // 木のチェストは研究で解放されるので無料設置（全解放）の足場で始める（前例 uitk-mode-hud-and-tooltip）
    // The wooden chest is research-locked, so start on the free-placement scaffold (precedent uitk-mode-hud-and-tooltip)
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    var root = GameUiRoot.Instance.GetRootElement();
    var world = await HudRect("ゲーム画面");

    // 上の安全帯のあるメニューは中身より上。研究は持ち物の右を上端まで使うので位置だけ見る（Web の ADR 0014）
    // Menus with an upper safe area keep the HUD above their content; research uses the full height right of the inventory, so only the position is checked (Web ADR 0014)
    await CheckMenu(Key.Tab, UIStateEnum.PlayerInventory, "main-grid", true);
    await CheckMenu(Key.T, UIStateEnum.ChallengeList, "challenge-panel", true);
    await CheckMenu(Key.R, UIStateEnum.ResearchTree, "research-tree", false);
    await CheckMenu(Key.B, UIStateEnum.BuildMenu, "build-menu-panel", true);
    await CheckMenu(Key.Escape, UIStateEnum.PauseMenu, "pause-menu", true);

    // 配置モード: 配置 HUD と横に分かれる
    // Placement mode: the HUD and the placement HUD are separated horizontally
    await p.Hotbar.EnterBuildMode(0);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    await p.UntilUiElement("placement-mode-hud", 10f);
    var placement = root.Q("placement-mode-hud").worldBound;
    var placementHud = await HudRect("配置モード");
    p.Assert(placementHud.xMax <= placement.xMin || placement.xMax <= placementHud.xMin, $"HUD {placementHud} と配置 HUD {placement} が横に分かれる");
    AssertSame(world, placementHud, "配置モード");
    await p.ExitToGameScreen();

    // 削除モード: 上の帯が HUD より上
    // Delete mode: the top band is above the HUD
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
    await p.UntilUiElement("delete-mode-warning", 10f);
    var band = root.Q("delete-mode-warning").Q("delete-mode-warning-band").worldBound;
    var deleteHud = await HudRect("削除モード");
    p.Assert(band.yMax <= deleteHud.yMin + 0.5f, $"削除の上の帯 {band} が HUD {deleteHud} より上");
    AssertSame(world, deleteHud, "削除モード");
    await p.Screenshot("01-delete-band");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 1024×576: 舞台の倍率に従い論理の位置は同じ、画面内でインベントリより上
    // 1024x576: follows the stage scale with the same logical rect, inside the screen and above the inventory
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1024, 576);
    await UniTask.DelayFrame(3);
    AssertSame(world, await HudRect("1024×576 のゲーム画面"), "1024×576 のゲーム画面");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await p.UntilUiElement("main-grid", 10f);
    var small = await HudRect("1024×576 のインベントリ");
    var screen = root.worldBound;
    p.Assert(screen.Contains(small.min) && screen.Contains(small.max), $"HUD {small} が画面 {screen} の中");
    p.Assert(small.yMax <= root.Q("main-grid").worldBound.yMin + 0.5f, "1024×576 でも HUD がインベントリより上");
    await p.Screenshot("02-1024x576-inventory");
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    await UniTask.DelayFrame(3);
    await p.ExitToGameScreen();

    #region Internal

    // HUD が出るまで待って矩形を読む
    // Wait for the HUD and read its rect
    async UniTask<Rect> HudRect(string where)
    {
        await p.UntilUiElement("challenge-hud", 10f);
        var hud = root.Q("challenge-hud");
        p.Assert(hud.resolvedStyle.display == DisplayStyle.Flex && hud.worldBound.width > 0f, $"{where} で HUD が出ている");
        p.Note($"{where} の HUD {hud.worldBound}");
        return hud.worldBound;
    }

    void AssertSame(Rect expected, Rect actual, string where)
    {
        var same = Mathf.Abs(expected.x - actual.x) <= 1f && Mathf.Abs(expected.y - actual.y) <= 1f && Mathf.Abs(expected.width - actual.width) <= 1f && Mathf.Abs(expected.height - actual.height) <= 1f;
        p.Assert(same, $"{where} の HUD がゲーム画面と同じ位置・寸法（{actual} / {expected}）");
    }

    // メニューを開き、HUD が同じ位置で（安全帯があれば）中身より上にあることを見て、ゲーム画面へ戻る
    // Open a menu, check the HUD keeps its rect (and sits above the content when there is a safe area), then return
    async UniTask CheckMenu(Key key, UIStateEnum state, string contentId, bool hasUpperSafeArea)
    {
        await p.PressKey(key);
        await p.WaitUiState(state, 10f);
        await p.UntilUiElement(contentId, 10f);
        var hud = await HudRect(state.ToString());
        AssertSame(world, hud, state.ToString());
        if (hasUpperSafeArea) p.Assert(hud.yMax <= root.Q(contentId).worldBound.yMin + 0.5f, $"{state} で HUD が {contentId} より上");
        await p.ExitToGameScreen();
    }

    #endregion
});
```

- [ ] **Step 3: 2 本を流す**

Run（PlayMode を止めた状態で）:
```bash
SKILL=.agents/skills/unity-playmode-recorded-playtest
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/uitk/stage5/uitk-challenge-list.cs"
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/uitk/stage5/uitk-challenge-hud-resident.cs"
```
Expected: 2 本とも `result.json` が `Success: true`、Note に `系=GameUi`、mp4 とスクショが出る。`uitk-challenge-list` の Note のカードの寸法（幅 240・高さ）を控え、Task 7 の実測の行に使う
- Mac の Editor が別作業で塞がっているときは `~/bin/winunity play uitk-lane/s5-challenge "$SKILL/scenarios/uitk/stage5/<名前>.cs" --ui-system GameUi`（push 済みが前提）で流し、`~/.cache/winunity/runs/` から結果を取る
- 解放済みのカテゴリが 1 つ（実マスタ）なので、カテゴリの切り替えは録画では見ない（L1 が持つ）。Note のカテゴリの数を `done.md` の「未確認」に「録画のカテゴリ切替: 実マスタでカテゴリ 1 つのため L1 だけ」と書く
- `SubInventory` と列車の入れ子ポーズ（`TrainHUDScreen`＋`PauseMenuScreen`）は録画に入れない（ブロックの設置と乗車が要る。D-9）。`done.md` の「未確認」に「常駐 HUD の SubInventory・列車の入れ子ポーズの 2 状態は未検証」と書く
- 落ちたら Assert の行と Note を読み、UITK 側（このレーンのファイル）の原因なら直して流し直す。他レーンの HUD・画面の原因（例: ビルドメニューの中身の上端が安全帯に無い）なら直さず、`done.md` の「integrator への依頼」に「名前／正本のパス／見た状態／持ち主」で 1 行書き、`bd note <bd>` に残し、その Assert を外さずに失敗のまま報告する（基準を緩めない）

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5
git commit -m "test(uitk): S5-challenge の録画 uitk-challenge-list・uitk-challenge-hud-resident"
```

---

### Task 7: Web との並べ比べ・記帳・テスト名の突き合わせ

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 実測の結論」の末尾に 1 行・「## 未撮影」の表の末尾に 1 行。並べ比べで例外を使うときだけ「## 見た目の例外」に LX-200〜202 の行）
- Modify: `LOGS/harness/uitk/S5-challenge/done.md`（差の表）

**Interfaces:**
- Consumes: Task 6 のスクショ 2 枚、`REF/web-8a585f742a/5-challenge-list.png`・`REF/web-8a585f742a-2560x1080/5-challenge-list.png`
- Produces: 差の表、台帳の実測の 1 行（と、使ったときだけ例外の行）、test-name-parity の結果

- [ ] **Step 1: 画像を比べ場へ置く**

Run:
```bash
mkdir -p moorestech_client/.uloop/outputs/compare/S5-challenge
cp <uitk-challenge-list の出力ディレクトリ>/01-open-centered-1280x720.png moorestech_client/.uloop/outputs/compare/S5-challenge/uitk-5-challenge-list-1280x720.png
cp <同>/01b-open-centered-2560x1080.png moorestech_client/.uloop/outputs/compare/S5-challenge/uitk-5-challenge-list-2560x1080.png
```
（撮影は Task 6 の録画の中で `PlaytestGameViewOps.SetResolution` を使って撮った。使い捨ての撮影スクリプトは書かない）

- [ ] **Step 2: 並べて差の表を書く**

2 組（1280×720・2560×1080）を Read で開いて並べ、`done.md` の「## 差の表」に `compare-procedure.md` §4 の書式で書く。比べる項目と Web の値（1280×720）:

| 観点 | Web（`5-challenge-list`） | 見るところ |
|---|---|---|
| 位置・寸法 | 一覧の上端 168、左右は舞台の端、題の上端 ≈184・高さ 35、ボタンの列 219〜255、先頭のボタン x=16〜96、ビューポートの中心 (640, 479.5)、カード 240×≈86、カテゴリ列にバーなし（幅に収まる） | UITK の同じ点（2px 以内）。UITK もバーが出ていない（越えたときのバーは参照に無いので L1 だけ）。カードの数・名前・カテゴリの数は実マスタなので「捨てる（データの違い）」 |
| 色 | 地 (31,31,31)、カードの面 (36,36,36)、filled のボタン (25,113,194)、subtle の字 blue-light-color、完了の輪 (64,192,87)、進行中の輪 (51,154,240) | トークン経由で同じ値か（UITK に未解放のカードがあれば減光 0.55 の色も記録） |
| 装飾 | 輪 2px・角丸、線は dark-4・3px（カードの下）、影なし | 輪の太さと角、線がカードの下に隠れる |
| 文字の段組 | 題 26px（和文）、カードの名前 16px 太字・概要 14px・状態 12px の 3 行、左寄せ | 行の高さ・左の余白 12 |

- 差が出たら、まず画面の USS・C#（このレーンのファイル）で直す。部品・Foundation の差なら直さず「integrator への依頼」へ
- 同じにできない差で §3.4 の例外の条件に当たるものだけ予備の LX-200〜202 を使う（3 つで足りなければ指揮へ）。使ったら Step 3 で台帳に行を足し、USS の該当行に `/* look-exception: LX-n */` を付ける。題の字形（和文 Bold）の濃さの差は既存の LX-171 を指す

- [ ] **Step 3: 台帳に記帳する**

`docs/uitk/TODO.md` の「## 未撮影」の表の末尾に 1 行:
```markdown
| 5 | challenge: カテゴリ列 | カテゴリが一覧の幅を越えたときの横のバー（UITK は `GameScrollView` の横向き: 流れの中・太さ `--scrollarea-scrollbar-size`・溝 `--game-scroll-view-track-face`・つまみ `--game-scroll-view-thumb-face`） | mock-host の challengeTree はカテゴリ 2 つで幅に収まり、実マスタは解放済みのカテゴリが 1 つ。Web は `::-webkit-scrollbar` の装飾が無く CEF の既定のバーになる（見た目と場所の取り方は未計測）。越える状態が撮れたら並べ比べ、差が残れば予備の LX-200 |
```

「## 実測の結論」の末尾（最後の箇条の次）に 1 行（見た目の例外の行は Step 2 で予備を使ったときだけ。カテゴリ列の横スクロールは写したので例外にしない）:
```markdown
- S5-challenge カードの高さ（`NodeGraphView.uss` の包みの高さ 0 の削除の事後承認）: チャレンジのカードは包みの中で中身なりの高さ（L1 `ChallengeListViewTest` で上下の余白 24＋3 行。録画 `uitk-challenge-list` の進行中のカード <幅>×<高さ>）。部品は変えていない
```
（`<幅>×<高さ>` は Task 6 の Note の値で埋める）

- [ ] **Step 4: テスト名を突き合わせる**

Run:
```bash
node tools/uitk/test-name-parity.mjs --bundle 5 --emit-missing; echo "exit=$?"
```
Expected: 終了コードが 2 でない。出力の未対応の列に `challenge.spec.ts` の 3 件・`challengeLogic.test.ts` の 2 件が無い（S2-hud の 4 件も無い）。他レーンの持ち分（言語ゲート・横断層）が残るのは正常。このレーンの 5 件が未対応に出たら `<web-test>` の文字列を Web の第 1 引数と 1 字ずつ見比べて直す

- [ ] **Step 5: L2 とこのレーンのテストを流し、コミットする**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`
Expected: PASS

```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): S5-challenge のカードの高さの実測とカテゴリ列の横バーの未撮影"
```
`done.md` は `LOGS` 側（`LOGS/README.md` の手順でコミット・push）

---

### Task 8: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイル

**Interfaces:**
- Consumes: Task 1〜7 の HEAD
- Produces: レビュー済みの HEAD、記録 `LOGS/harness/` の `$RUNDIR`

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。記録は `LOGS/harness/` に置き、ブランチにコミットしない。設計判断の指摘（裁定 2026-11-04・D-1〜D-17 に関わるもの）は、別の subagent に再現させて再現したものだけ残し、ゴール・裁定から外れるものだけ `done.md` の「要裁定」へ、それ以外は agent 既定として判断記録の流儀で `done.md` に書いて進める

- [ ] **Step 2: 反映後のバイナリで確かめ直す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`
Expected: ErrorCount 0 / PASS
- 反映がソースの判定経路・条件式・その評価の時点（`ChallengeCategoryGraphHost.Render` の View の作成と付け替えの条件・`ChallengeCategoryGraph.Render` の渡し直しの条件・`ChallengeListModel` の組み直しの条件・選択ストアの戻し・`ChallengeScreenVisibility`・工場の状態の写し・`GameScrollView` の横向きのバーの出し入れ）に触れたら、Task 6 の録画 2 本を反映後のバイナリで流し直し、`Success: true` を見てから完了にする

- [ ] **Step 3: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5 docs/uitk/TODO.md
git commit -m "fix(uitk): S5-challenge のレビュー指摘の反映"
```
（反映が無ければコミットしない）

---

### Task 9: feature/uitk への PR・取り込み・マージ・報告（必須）

**Files:**
- Modify: `LOGS/harness/uitk/S5-challenge/done.md`

**Interfaces:**
- Consumes: Task 8 後の HEAD
- Produces: `origin/feature/uitk` のマージコミット。以後 S5-ladder（ラダー `uitk-challenge-gate`・`TutorialAnchorCoveragePending` の空の確認）が使える

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`）。セッション終了可能状態にすること: pr-create スキルで PR を作成し、コンフリクトがあれば `origin/feature/uitk` をマージして解消・コンパイル確認のうえ push する。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ（画像・動画の URL を本文に並べない。tailnet の URL も書かない）。報告ページは `~/hermes-agent/data/services/pr-review/publish/README.md` の手順で、manifest は次の形（`file` は絶対パス。並べ比べは Web を先に同じ `group` へ置く）:
```json
{
  "head": "<HEAD 10 桁>",
  "summary": [
    "チャレンジ一覧（題・カテゴリのボタン列・NodeGraphView のノードカード）を UITK に移した。カードは PaperSurface に名前・概要・状態、進行中は青・完了は緑の 2px の輪、未解放は 0.55。カテゴリ列は幅を越えたら横に送れる（Web の overflow-x: auto）",
    "訪れたカテゴリごとに NodeGraphView を 1 本控えて付け替え、視点は View の寿命で保つ（I-fix6 と同じ形）。初回訪問だけ進行中へ寄せ、再訪と開き直しは前回の視点（裁定 2026-11-04）",
    "S4-research の NodeGraphView.uss（包みの高さ 0 の削除）を事後承認し、カードが中身なりの高さで描かれることを L1 で固定した",
    "部品 GameScrollView に横向き（GameScrollAxis）を足し、カテゴリ列の横スクロールに使った。画面の USS は部品の内側のクラスを書かない。縦の呼び出し側 13 か所は Vertical を明示"
  ],
  "media": [
    {"file": "<uitk-challenge-list の recording.mp4>", "caption": "uitk/stage5/uitk-challenge-list（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<uitk-challenge-hud-resident の recording.mp4>", "caption": "uitk/stage5/uitk-challenge-hud-resident（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "../moorestech_logs/uitk/reference-captures/web-8a585f742a/5-challenge-list.png", "caption": "Web 5-challenge-list 1280×720", "group": "5-challenge-list"},
    {"file": "<worktree>/moorestech_client/.uloop/outputs/compare/S5-challenge/uitk-5-challenge-list-1280x720.png", "caption": "UITK 5-challenge-list 1280×720", "group": "5-challenge-list"},
    {"file": "../moorestech_logs/uitk/reference-captures/web-8a585f742a-2560x1080/5-challenge-list.png", "caption": "Web 5-challenge-list 2560×1080", "group": "5-challenge-list"},
    {"file": "<worktree>/moorestech_client/.uloop/outputs/compare/S5-challenge/uitk-5-challenge-list-2560x1080.png", "caption": "UITK 5-challenge-list 2560×1080", "group": "5-challenge-list"}
  ],
  "verification": [
    "Unityコンパイル: エラー0",
    "L0/L1: State.Challenge 11 件・Screens.Challenge.Tree 9 件 PASS、L2（Conventions・Theme・Layers・Anchor）PASS",
    "test-name-parity 束 5: このレーンの持ち分 5 件の未対応 0",
    "unityプレイ録画テスト: uitk-challenge-list PASS・uitk-challenge-hud-resident PASS（全体スイッチ GameUi）"
  ],
  "notes": ["差の表は moorestech_logs/harness/uitk/S5-challenge/done.md。参照は mock-host のデータ、UITK は実マスタなので中身の違いは捨てる"]
}
```
`python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <PR番号> <manifest.json>` が失敗したら、報告 URL 無しで完了にしない

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
Expected: 進んでいなければ `Already up to date.`。共有区画（`GameUiTheme.tss` の `Screens` 区画・`MainGameUiRegistration.cs` の `using` と画面登録区画・`docs/uitk/TODO.md` の節の末尾）の衝突は両方の行を採る。`TutorialAnchorCoveragePending.cs` に他レーンが行を足していたらその行を残し、S5-challenge の 3 行だけを消した形にする。I-fix5 が `scenarios/uitk/` を段のサブディレクトリへ移していたら、このレーンの `stage5/` の 2 本はそのまま（パスが変わらない）。判定経路に触れる衝突（`ChallengeProgressState`・`NodeGraphView`・`UiRoutingState` の変更）を解いたときだけ Task 6 の録画を流し直す。取り込みの後に並走レーンとの照合（Global Constraints）をもう一度見る: `git grep -n "new GameScrollView(" -- moorestech_client/Assets/Scripts` に引数 1 つの呼び出しが増えていれば `GameScrollAxis.Vertical` を足す・`UiDisplay.cs` が入っていれば `ChallengeListView.SetShown` を `UiDisplay.SetShown` に替える・`GameUiScreenMountTest.cs` が入っていれば Task 5 Step 4 の行を足して L3 を流す（足したら `done.md` の依頼の行を消す）・`git grep -n "RestoreViewport" -- moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge` が 0（このレーンは使わない）。直したら compile と `$FILTER` を流し直す。G1 が緑で、未記帳 0

- [ ] **Step 3: マージコミットで取り込む**

Run: `gh pr merge <PR番号> --merge`
Expected: マージされ、`git fetch origin && git log -1 --format='%H %P' origin/feature/uitk` の親が 2 つ（Squash しない）

- [ ] **Step 4: Editor を閉じ、bd を閉じ、報告を締め、合図を送る**

Run:
```bash
moores-wt status
uloop control-play-mode --project-path ./moorestech_client --action Stop
bd close <bd> --reason "PR #<番号> を feature/uitk へマージ（チャレンジ一覧・カテゴリのボタン列・ノードカード・カテゴリごとの視点）"
git status --short
git log origin/uitk-lane/s5-challenge..HEAD
```
Expected: closed、`git status --short` と `git log` が空。このレーンの Editor を閉じる（`moores-wt status` で自分の worktree の Editor だけを確かめ、他者の Editor は落とさない）。テストで `Saves/` に作ったワールドが残っていれば `list-work-saves.sh` で探して消す。`done.md` の 1 行目を「状態: 完了」にし、「## 証拠」に PR 番号・マージコミット・G1 のジョブ URL・各テストと録画の結果・報告ページの URL を足して、`LOGS/README.md` の手順でコミット・push する。最後に:
```bash
cmux wait-for -S uitk-s5-challenge
```
worktree の `moores-wt rm` は指揮が行う（このレーンは行わない）。途中で止まる場合も `done.md` を「状態: 停止（理由）」にして同じ合図を送る

---

## Self-Review

1. **Requirements coverage**: R-1 → Task 1（`ChallengeListLogic`・L0 2 件）。R-2 → Task 1（工場・空と選択ありの型・文言キー・null の前提・L0 工場 4 件）。R-3 → Task 2（`ChallengeScreenVisibility`・Model・選択ストア・L0 5 件）・Task 4（開き直しの L1）・Task 6。R-4 → Task 3（`ChallengeCategoryGraphHost`・`ChallengeCategoryGraph`）・Task 4（L1 3 件。付け替えと View の数）。R-5 → Task 3（View・USS・`GameScrollView` の横向き・L1 5 件の横スクロールとホイール）・Task 7。R-6 → Task 3（`ChallengeCategoryButtons`）・Task 4（押下の L1）。R-7 → Task 1（`StateLabelKey`）・Task 3（カード・USS・L1）。R-8 → Task 4（HUD の L1）・Task 6。R-9 → Task 6（`uitk-challenge-hud-resident`）。R-10 → Task 3（印と Pending）・Task 6（Unique）。R-11 → Task 6（撮影）・Task 7。R-12 → Task 6。R-13 → Task 3（tss）・Task 5。R-14 → Task 1・3・4（タグ）・Task 7 Step 4。R-15 → Task 7 Step 3・Task 1 Step 2（A 行の本文の依頼。A-241 に A-191 の改め）。R-16 → Task 8・9。R-17 → Task 2b（`GameScrollAxis`・L1 2 件・呼び出し側 13 か所）。死活表の 5 行（横スクロール・暗幕・スキット入力・移動とカメラ・グラフ外ホイール）は Task 3 の L1 と前提の確認（Task 1 Step 1 の grep）が持つ。漏れなし
2. **Placeholder scan**: 「TBD」「後で」は無い。`<幅>×<高さ>`・`<PR番号>`・`<bd>`・出力ディレクトリは実行時に決まる値で、埋め方を同じ Step に書いた。`GameScrollView` の横向きの content container の配置とバーの上書きの勝ち負けは実装時に `resolvedStyle` で確かめ、外れたときの直し方を Task 2b Step 3 に書いた
3. **Type consistency**: `ChallengeNodeCardViewModel(Guid, Vector2, Vector2, IReadOnlyList<Guid>, ChallengeNodeState)`・`StateLabelKey`・`HasSameGraphPlacementAs` は Task 1・3（Kit・カード）・4 で同じ。`ChallengeListViewModel.Create(categories, selection)`・`ChallengeListViewModel.Empty`・`SelectedChallengeListViewModel.Categories/CategoryGuids/Selected`・`EmptyChallengeListViewModel` は Task 1・2・3・4 で同じ（View の switch と L0 の `AsSelected`）。`ChallengeCategoryViewModel.Create(guid, nodes)`・`TryGetInitialFocus`・`HasSameGraphAs` は Task 1・3 で同じ。`ChallengeListLogic.SameGuidSequence`・`DeriveStateLabelKey` は Task 1・3 で同じ。`ChallengeScreenVisibility.IsShown(UiRoutingSnapshot)` は Task 2・5 で同じ。`ChallengeCategoryGraph(ChallengeCategoryViewModel, IUiOsCursor)`・`View`・`Render` と `ChallengeCategoryGraphHost(IUiOsCursor)` は Task 3・4 で同じ。`ChallengeListView(IChallengeListCommands, IUiOsCursor)` と `Kit.MountList(UiLayerStack, IChallengeListCommands)` は Task 3・4・5 で同じ。`IChallengeListCommands.SelectCategory(Guid)` は `Client.Ui.State.Challenge.Commands` で Task 2・3・4・5 同じ。名前空間は State が `Client.Ui.State.Challenge`・`.Tree`・`.Tree.Selection`・`.Commands`、画面が `Client.Ui.Screens.Challenge.Tree`・`.Parts`、L1 が `Client.Tests.Ui.Screens.Challenge.Tree` で全タスク同じ
4. **保留・縮退経路の解消可能性**: (a) `ChallengeListModel` は閉画面で組まず、解消の条件は「一覧への突入」（経路の通知で必ず来る）。表示中かは経路の今の値で読むので、選択ストアとの購読順で組み漏れも余分な組み直しも起きない。(b) 最小構成: 解放済みのカテゴリ 0（`EmptyChallengeListViewModel`。題だけ・グラフ無し。L1 `解放済みのカテゴリが無ければ…`）、カテゴリ 1・進行中 0（注目点無し → 寄せない・視点は原点。部品の既定）、初回（View を作る → 部品が寄せる）、カテゴリ列が幅に収まる（バーなし。L1）。どれも恒久の保留にならない。(c) `ChallengeCategoryGraph` の「中身が同じなら渡し直さない」は、カードの描き直しを先に済ませるので表示が古いまま残らない
5. **「決定的にする」を根拠にした選択**: 「先頭のカテゴリ」「最初の進行中」は Web と同じ規則（`categories[0]`・`nodes.find`）で、順序は受信順＝マスタの並び（D-24）に従う（決定性ではなく Web と同じ結果になることが根拠）。開き直しで先頭へ戻すのは裁定 2026-11-04 の「今のまま」（D-2）。L0 で固定
6. **呼び出し側への責務漏出**: 一覧を出すかの判定は `ChallengeScreenVisibility.IsShown` の 1 本（選択ストア・Model・Presenter が呼ぶ）。Guid の並びの比較は `ChallengeListLogic.SameGuidSequence` の 1 本（ボタン列とグラフの形の比較が呼ぶ）。状態→文言キーは State の `ChallengeListLogic.DeriveStateLabelKey` の 1 本で、画面は修飾クラスだけ。空か選択ありかの分岐は View の `Render` の 1 か所。前提の絞り込みは `ChallengeCategoryViewModel.Create` の 1 か所、View を作るか付け替えるかは `ChallengeCategoryGraphHost.Render` の 1 か所、渡し直すかは `ChallengeCategoryGraph.Render` の 1 か所（視点は部品の中にあり、画面は値を控えない）。バーの有無の幾何は `GameScrollView` の `UpdateScrollerVisibility` の 1 か所（縦横の軸だけを選ぶ。D-7）

---

## 判断記録（ADR）

ゴール（ユーザー原文）:「WebUIと同じ見た目で、適切な設計、コンポーネント化されたUITKベースのUIを開発すること」。裁定: `.decisions/2026-11-06-*` の UITK 6 本・CEF 停止の記録・`.decisions/2026-11-04-チャレンジ画面は各カテゴリの初回訪問だけ進行中へ寄せ再訪は前回の視点を戻す.md`。以下はすべて **agent 既定**（ゴール・裁定から外れる判断は無い。要裁定 0）。

- **D-1 視点は View の寿命で持つ: 訪れたカテゴリごとの `NodeGraphView` を控えて付け替える（設計書 §7.1 の A-240。本文はこの D-1）**（指揮の agent 既定（PLAN-W5 評価））: `ChallengeCategoryGraphHost` が訪れたカテゴリごとに `NodeGraphView("challenge", …)` を 1 本作って控え（`ChallengeCategoryGraph`。カテゴリ Guid → View。画面の寿命）、選んだカテゴリが変わったら置き場の子を表示するカテゴリの View へ付け替える（前の View は外して控える）。部品は中央寄せをインスタンスにつき 1 回だけ行うので、View を作ったとき＝そのカテゴリの初回訪問だけ進行中へ寄せ、再訪は控えた View の視点のまま、一覧を閉じて開き直したときも控えた View が戻る（裁定 2026-11-04 を「View を作った時だけ初回の寄せを行う」で満たす）。Web の `key={selected.guid}`（カテゴリごとの作り直し）＋`viewportStore`（カテゴリごとの視点。セッション内）と同じ結果を、視点の寿命＝View の寿命で出す。並走の I-fix6（`origin/uitk-lane/i-fix6` の 3614f5c0df。計画 R-15・D-3）が `NodeGraphView.RestoreViewport` を消し、研究の視点を View の寿命で保つ形にした。その上に乗るので、I-fix6 とどちらが先に `feature/uitk` へ入っても壊れない。置き場の子は常に 1 本なので、`challenge-viewport`・`challenge-canvas` の名前とカードのアンカーは木の中で重ならない（外したカテゴリのカードはパネルに載っていない）。State の `ChallengeViewportSessionStore` は作らない。棄却: (a) カテゴリごとの作り直し＋State の Session に控えて `RestoreViewport` で戻す（旧 D-1・C4-d 申し送り (1)）— I-fix6 が消す口に頼り、I-fix6 が先に入れば Task 1 の前提が外れ、後に入れば取り込みでコンパイルが壊れる。(b) 研究と同じ常駐 1 つ（A-172）— 寄せがインスタンスにつき 1 回なので 2 つ目のカテゴリの初回訪問で寄せられない。(c) カテゴリごとの View を全部置いて非選択を `display: none` で並べる — 名前とアンカーが重なり、テストの ID と `PlaytestUiElementOps.FindUi` が隠れた方を拾いうる。帰結: 外している間に画面の寸法が変わると、付け直したときに部品の寸法の追従で中心を保ち直しうる（Web は復元した視点を寸法に追従させない。研究 A-172 と同じ種類の差。推測で、L1 は寸法を変えない）。控える View の数は訪れたカテゴリの数まで（マスタのカテゴリ数が上限）
- **D-2 カテゴリの選択は State の選択ストア、開き直しは先頭のカテゴリ**: `ChallengeCategorySelectionStore` が一覧を離れると「無し」に戻し、工場が先頭（受信順＝マスタの並び）を選ぶ。前例 `ResearchSelectionStore`。操作は `IChallengeListCommands.SelectCategory`（前例 `IResearchTreeCommands`。L1 で記録の差し替えに替えるため）。開き直しで先頭へ戻す根拠: 裁定 2026-11-04 の本人の明示の引用は「初回だけ寄せる（研究と同じ・今のまま）」で、「今のまま」は当時の Web（`ChallengePanel.tsx:14-15` の `useState` はパネルのアンマウントで消えて `categories[0]` に戻り、視点はカテゴリごとに `viewportStore` が持つ）を指す。決定文の「前回見ていた位置を戻す」はカテゴリの視点のことで、選んでいたカテゴリの記憶ではない。読み方（指揮の agent 既定（PLAN-W5 評価））: 各カテゴリの View を控えるので、開き直して出る先頭のカテゴリの視点は前回のまま戻る。別のカテゴリを見ていて閉じたとき、開き直しでそのカテゴリへ戻すことは含まない（Web の実挙動: `W/src/app/App.tsx:102` の `{screen === "challengeList" && <ChallengePanel />}` がパネルをアンマウントし、`ChallengePanel.tsx:14-15` の `useState` が消えて `categories[0]` に戻る）。表示中かは `ChallengeScreenVisibility.IsShown` で読み、Model は控えた旗でなく経路の今の値を読むので、選択ストアと Model のどちらの購読が先に呼ばれても隠れた後の選択の変化では組まない（DI の登録順に依らない。D-21）
- **D-3 未解放のカテゴリと別カテゴリの前提**: 未解放のカテゴリは出さない（Web の橋 `ChallengeDtoBuilder.BuildTree`）。別カテゴリの前提は `ChallengeCategoryViewModel.Create` で線のキーから除く（C4-d D-5 の申し送り (2)。部品はグラフに無い親を LogError にする。Web は黙って省く）。どちらも Web の仕様なので `silent-ok` を付け、L0 で固定する
- **D-4 S4-research の `NodeGraphView.uss` の事後承認（設計書 §7.1 に A-241 を足す。本文はこの D-4）**: `.node-graph__node` から `height: 0` を消した S4-research の修正（9a58258265。包みは幅 0・高さは中身なり・`align-items: flex-start`・子は `flex-shrink: 0`）を承認する。Web の包みは静的な div でカード自身が絶対配置なので中身なりの高さになる。UITK はカードが包みのフローの子なので、高さ 0 の包みは中身なりのカードを 0 に潰す（S4-research の実測 132×18 → 141）。画面の USS は `.node-graph*` を書けないので部品で直すのが正しい。チャレンジのカードが上下の余白 24＋3 行の高さで描かれることを `ChallengeListViewTest`（L1）と録画 `uitk-challenge-list` で固定した。持ち主 C4-d の部品の変更として記録し、このレーンは部品を変えない
- **D-5 カードの組み立て**: 根は `PaperSurface`（sealed なので持ち手 `ChallengeNodeCard` が組み立てて `Root` で渡す）。Web の outline は拾わない子の輪（研究の前例）で、半径 6px（Paper の角丸 4px に沿う outline の外周）の数値は Web の CSS に無いブラウザの描き方なので画面 USS に直書きする（つまみを作らない）。`translate(-50%, -50%)` は USS、`scale(UIScale)` は C# の `style.scale`（中心を軸。Web と同じ）。指のカーソルは付けず、ビューポートの掴む手（`TreeView.module.css:11` の `cursor: grab`）を継ぐ（C4-d/done.md:67 の申し送りは研究のカードのもの。Web のチャレンジのカードは `cursor: pointer` もタップも持たない）。設計書 A-191 と §2 の OS カーソルの行（:234。使い手の列 research・challenge「ノードグラフの掴む手・カードの指」）はチャレンジのカードにも指を置くと読めるので、A-241 の本文でチャレンジの部分を「カードは指を持たずビューポートの掴む手を継ぐ」に改める（A-241 の本文は設計書に入り済み。A-191 そのものと §2 の行の書き換えは指揮が設計書へ写す。指揮の agent 既定（PLAN-W5 評価））
- **D-6 未解放の減光 0.55 は USS のリテラル**: GameUi は sRGB で合成する（設計書 A-180・I-blend）ので Web の値がそのまま使える。研究の `--research-node-locked-opacity` は Linear 合成の頃の補正のつまみで、チャレンジには作らない。`DerivedTokens.uss` に足さない（I-fix5 がファイルを分ける最中で、重ねない）
- **D-7 カテゴリ列は `UiGroup(UiGap.Md)`＋nowrap を `GameScrollView` の横向きに入れて写す**（指揮の agent 既定（PLAN-W5 評価））: Web の `.categories` は `flex-wrap: nowrap`・`overflow-x: auto` で、ボタンが一覧の幅を越えたら横に送れる。UITK は `Components/Controls/GameScrollView` に向きの引数 `GameScrollAxis` を足し（Task 2b。Components の変更）、`GameScrollView("challenge-categories-scroll", GameScrollAxis.Horizontal)` に入れる。バーは中身が見える幅を越えたときだけ出し（`Auto` は押下の 1px 沈みでバーが点滅するので、縦と同じく幾何で決める）、本文の下の流れに置く（重ねると越えたときにボタンの下の 12px が押せなくなる。Web の `overflow-x: auto` の CEF の既定のバーも場所を取る。推測）。太さ・溝・つまみは縦と同じつまみで、ゲームの他のスクロール領域のバーと同じ見た目になる。ホイールの 1 ノッチは `WebWheelScale.PixelsPerNotch`。画面の USS は部品の内側のクラス（`unity-scroller__slider`・`unity-base-slider__*`・`unity-scroll-view__*`）を書かない（S5-crosscut T10 が `UiStyleConventionTest` に足す「`Screens/**/*.uss` のセレクタが `Components/**/*.uss` の定義する `__` 付きの内側クラスを含まない」に当たり、A-167 の「部品のクラスを画面が書かない」とも食い違うため）。`Theme.uss` は変えない。縦の呼び出し側 13 か所は `GameScrollAxis.Vertical` を明示する（デフォルト引数と引数 1 つの多重定義を作らない）。参照キャプチャはどちらも幅に収まってバーが写っていない（Web の CEF の既定の横バーは未計測。`W/src/**/*.css` に `::-webkit-scrollbar` は無い）ので、越えたときの見た目は台帳の「## 未撮影」に 1 行足す（Task 7。差が測れたら並べ比べ、要るなら予備の LX-200 を使う）。棄却: (a) 画面の部品 `Screens/Challenge/Parts/ChallengeCategoryScrollView`（`ScrollView` 派生）と画面 USS の内側のクラスの上書き（旧 D-7）— 部品の内側のクラスを画面が書き、S5-crosscut の検査に当たる。バーの有無の式とホイールの換算が `GameScrollView` と 2 本になる（A-167 の「画面ごとに別のスクロール部品」）。(b) `overflow: hidden` で切って台帳に例外を記帳する — Web で届くボタンが UITK で押せなくなり、カテゴリが増えた時点で黙って壊れる。(c) バーを縦と同じく本文に重ねる — 越えたときにボタンの下が押せない
- **D-8 層と表示**: 一覧は `UiLayer.OverlayPanel`・`UiFamily.Stage`（Web の `--z-stage-overlay-panel`。舞台の倍率と左右の余白に従う）。Presenter が 1 回だけ載せ、経路で `SetShown`（前例 `ResearchTreePresenter`）。Model は一覧の表示中だけ組む（前例 `ResearchTreeModel`）。進行中 HUD・ホットバー・キーヒントは上の層にあり、このレーンは触らない
- **D-9 写すテストの割り振り**: `challengeLogic.test.ts` 2 件は L0。`challenge.spec.ts` の画面 2 件は L1。「常駐HUDをインベントリ・メニュー・操作モードで維持する」は、一覧の部分（HUD の位置が変わらない・一覧とカテゴリのボタンより上・層の順序）を L1 `ChallengeHudOverListTest` に `<web-test>` を付けて写し、画面を跨ぐ部分（インベントリ・一覧・研究・ビルドメニュー・ポーズ・配置・削除・1024×576）は録画 `uitk-challenge-hud-resident` で見る。L1 で全画面を載せると他レーンの View と Model を組む必要があり、テストが他レーンの持ち物に依存するため。帰結: `SubInventory` と列車の入れ子ポーズの 2 状態はブロックの設置と乗車が要るので録画に入れず、このレーンでは未検証（`done.md` の「未確認」）
- **D-10 録画を 2 本にする**: 画面の録画 `uitk-challenge-list` と HUD の契約の録画 `uitk-challenge-hud-resident`。1 本にまとめると 200 行を越える。どちらもこのレーンの持ち分（ラダー `uitk-challenge-gate` は S5-ladder）。置き場は `scenarios/uitk/stage5/`（段のサブディレクトリの前例 `stage3/`、I-fix5 の 10n と同じ向き）
- **D-11 登録はサブディレクトリ**: `Client.Starter/Registration/GameUi/Screens/` は直下 10 ファイルで上限なので `Screens/Challenge/ChallengeGameUiRegistration.cs`（名前空間 `Client.Starter.Registration.GameUi.Screens.Challenge`）。`MainGameUiRegistration` に `using` 1 行と画面登録区画の 1 行
- **D-12 Model は言語で組み直さない**: 文言はすべて `LocalizedLabel`（`FormButton` の中も）で、言語の変化で自分で解決し直す。研究の Model が言語を購読するのは詳細ペインの文を組むためで、チャレンジには組む文が無い
- **D-13 比較の撮影は録画の中で**: 参照（mock-host）と同じデータは本番の経路で作れないので、実マスタの一覧を `uitk-challenge-list` の中で `PlaytestGameViewOps.SetResolution` を使って 1280×720・2560×1080 で撮る。中身（カテゴリの数・名前・ノードの数）の差は「捨てる（データの違い）」とし、位置・寸法・色・装飾・段組を比べる。部品だけの見本（`UiRuntimePanelCapture`）は使わない（画面の比較なので）
- **D-14 文言は既存のキーだけ**: `ui.challenge.title/stateLocked/stateCurrent/stateCompleted` と内容キー（`ChallengeTitle`・`ChallengeSummary`・`ChallengeCategoryName`）。`localization.csv` に足さない
- **D-15 ランタイムに触れるので録画を持つ**: moores-writing-plans の規定どおり unityプレイ録画テストを Task 6 に置いた。レビューの反映が判定経路に触れたら反映後のバイナリで流し直す（Task 8 Step 2）
- **D-16 名前空間に `List` を使わない**: State・画面・L1 のディレクトリは `Tree`（`Client.Ui.State.Challenge.Tree`・`Client.Ui.Screens.Challenge.Tree`・`Client.Tests.Ui.Screens.Challenge.Tree`）。`List` だと `System.Collections.Generic.List` と読み違える（コンパイルは壊れない）。研究の `Research.Tree` と揃う。設計検査では View・Presenter・USS を `Screens/Challenge/List/` に分ける指定だったが、分ける目的（`Screens/Challenge/` 直下に置かず、`CurrentHud/` と並べる）は `Tree/` でも満たし、State と L1 と同じ名前にそろうので `Tree/` にした（指揮の指定からの逸脱。差し戻すなら名前を `List` に替えるだけで構造は同じ）
- **D-17 アンカーの未移植の表**: 印を付けたので `TutorialAnchorCoveragePending` の S5-challenge の 3 行を同じ PR で消す（規則: 印を付けたレーンが同じ PR で消す）。表は空になり、ファイルの削除と空の確認は S5-ladder
- **D-18 一覧の VM は空と選択ありの型で分ける**（指揮の agent 既定（PLAN-W5 設計検査）・強5-A）: `ChallengeListViewModel` を abstract にし、`EmptyChallengeListViewModel`（解放済みのカテゴリが無い）と `SelectedChallengeListViewModel`（`Categories`・`CategoryGuids`・`Selected` が必ずある）に分ける。View の `Render` の switch の 1 か所だけが分岐し、部品は `SelectedChallengeListViewModel`・`ChallengeCategoryViewModel` だけを受けて null を見ない。棄却: `bool HasSelection`＋`Selected` が null になりうる 1 つの型 — 旗と null の 2 つで同じことを表し、旗を見ずに `Selected` を読む経路が型で止まらない
- **D-19 表示判定と Guid の並びの比較は 1 本**（指揮の agent 既定（PLAN-W5 設計検査）・弱）: 経路から一覧を出すかは `ChallengeScreenVisibility.IsShown(UiRoutingSnapshot)`（前例 `BlockInventoryScreenVisibility.IsShown`、X-audit DC-8 の形）だけで決め、選択ストア・Model・Presenter の 3 か所はこれを呼ぶ。Guid の並びが同じかは `ChallengeListLogic.SameGuidSequence` だけで比べ、ボタン列の作り直しとグラフの中身の比較（`ChallengeCategoryViewModel.HasSameGraphAs`）が使う
- **D-20 グラフを渡し直す条件は渡す中身の値で比べる**（指揮の agent 既定（PLAN-W5 設計検査）・弱5-F）: `ChallengeCategoryGraphHost` は前に渡したカテゴリの VM を控え、カテゴリが変わったときか `HasSameGraphAs` が偽のとき（並び・座標・前提・注目点のどれかが違う）だけ `SetGraph` する。状態と倍率は渡す中身に入らない（カードの描き直しで済む）ので比べない。L0 で「状態と倍率では変わらない」を固定する。棄却: カードの組が「並びが変わったか」の bool を返す — 座標や前提の変化を見落とし、判定が画面の部品に散る
- **D-21 選択ストアと Model の購読順に依らない**（指揮の agent 既定（PLAN-W5 設計検査）・10c）: Model は表示中かを自分の旗に控えず、組むたびに `ChallengeScreenVisibility.IsShown(_routing.Current.Value)` で読む。`ReactiveProperty` は値を書いてから購読者へ流すので、経路の通知の中でも今の値が読める。一覧を離れたとき選択ストアが「無し」に戻す通知が Model の経路の購読より先でも後でも、閉じた画面では組まない。DI の登録順に依る形を残さないために直した（判断記録だけで済ませない）。両方の順で `ChallengeListModelTest` の `[TestCase(true)]`・`[TestCase(false)]` が固定する（Task 2 Step 3）
- **D-22 Commands と View の置き場**（指揮の agent 既定（PLAN-W5 設計検査）・N-3/N-6）: 操作は `Client.Ui.State/Challenge/Commands/`（前例 `Client.Ui.State/Research/Commands/IResearchTreeCommands.cs`）。View・Presenter・USS は `Client.Ui/Screens/Challenge/Tree/`（`Screens/Challenge/` 直下は 0、`CurrentHud/`・`Parts/` と並ぶ。名前は D-16）。どのディレクトリも 10 ファイル以下
- **D-23 状態の文言キーは State が決める**（指揮の agent 既定（PLAN-W5 設計検査）・N-2）: 状態→文言キーは `ChallengeListLogic.DeriveStateLabelKey`（前例 `ResearchLogic.DeriveNodeStateLabelKey`）で、`ChallengeNodeCardViewModel.StateLabelKey`（`LocalizationKey`）としてカードへ渡す。画面は状態の修飾クラスだけを決める。前例との差: 研究は工場がキーを引いて VM へ渡すが、チャレンジは VM の ctor で引く（L1 の材料が ctor で VM を組むので、どの組み方でもキーが状態とずれない）。L0 で状態ごとのキーを固定する
- **D-24 受信順とマスタの並び、前提なしの扱い**（指揮の agent 既定（PLAN-W5 設計検査）・10a/10b）: 握手（`GetChallengeInfoProtocol`）も完了イベントも `CompletedChallengeEventPacket.GetChallengeCategories` がマスタの `ChallengeCategoryMasterElements` 順に組むので、受信順＝マスタの並びと書きそろえる（計画の中で「受信順」「マスタ順」を混ぜない）。`PrevChallengeGuids` はスキーマで optional（`VanillaSchema/challenges.yml:54-56`）で、サーバーも null を前提なし扱いする（`ChallengeMaster.cs:117`）ので、工場は null を空と同じに扱い、同じ行に `silent-ok` で理由を書く（`ChallengeDtoBuilder.cs:43` は null を確かめずに回すが、Web の橋の欠けで仕様ではない）

## 未確認

- 言語を日本語へ切り替えた後に、L1 で合成したマスタの原文が残るか（Task 3 Step 7 に直し方を書いた）
- 実マスタで解放済みのカテゴリが 1 つのため、カテゴリの切り替えと横スクロールは録画で見ない（L1 だけ）
- 複数行に折り返す名前・概要の行送り（Web の line-height 1.55/1.45/1.4）は参照に無く、比べていない
- CEF（Chromium）の既定の横バーの見た目と、場所を取るか重なるかは未計測（参照キャプチャはどちらも幅に収まりバーが写らない）。`GameScrollView` の横向きのバー（流れの中・太さ 12）で写し、台帳の「## 未撮影」に足す（D-7）
- `GameScrollView` の横向きで縦のホイールが横送りになるか（UITK は送る見込み、Chromium の `overflow-x: auto` は既定で送らない）は未計測。L1 は「ホイールが装備・ホットバーへ抜けない」だけを固定する
- `GameScrollView` の横向きの content container が行の高さを持つか（0 なら部品の横の区画に `position: relative` を足す。Task 2b Step 3）は実装時に確かめる

## 本PR外のリファクタ提案

- Web の橋 `Client.WebUiHost/Game/Topics/Challenge/ChallengeDtoBuilder.cs` の `BuildTree` と State の `ChallengeListViewModelFactory.Create` が同じ規則（解放済みのカテゴリだけ・状態の優先 完了＞進行中＞未解放・`DisplayListParam.UIPosition`/`UIScale` の写し）を 2 本持つ。Web の撤去（裁定 2026-11-06「一気切替の日に CEF と Web を同時に撤去する」）で橋ごと消えて 1 本になる見込み。それまでに規則を変えるなら両方を直す
- 研究の `Screens/Research/Parts/ResearchNodeCardSet.cs:28-40` の `bool RenderCards`（並びが変わったかを bool で返す）と Guid→キー文字列の詰め替えは `ChallengeNodeCardSet` と同じ形。D-20 で棄却した「カードの組が並びの変化を bool で返す形」（座標と前提の変化を見落とす）が研究に残っている。NodeGraph 用のカードの使い回しを 1 本にし、渡し直しの判定を VM の値の比較（チャレンジの `HasSameGraphAs`）へ寄せられる
- 研究の `Screens/Research/ResearchTreeView.cs:60-73` の `Render` の渡し直しの条件（`orderChanged || focusChanged`・`hasFocus != _hasFocus || (hasFocus && focus != _focus)`）は、チャレンジの `ChallengeCategoryGraph.Render`（`HasSameGraphAs`）と同じ役割を別の式で持つ。研究も VM の値で比べる形へ寄せると、座標・前提の変化も渡し直しに入る（A-172 の改めになる）

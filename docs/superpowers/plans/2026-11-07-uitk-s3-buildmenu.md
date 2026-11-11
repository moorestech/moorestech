# UITK S3-buildmenu（段階 3 画面: ビルドメニュー）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/buildMenu`（3 カラムのパネル・縦のカテゴリ切替・検索・全カテゴリを 1 本に並べたスクロールとスクロールスパイ・smooth scroll・詳細の sticky・BP の右クリック削除・不足ツールチップ・閉じて開き直したときの復元・ホットバーへのドラッグ源）を、C3-a・C3-b の部品と L0-s3 のロジック（`BuildMenuGrouping`・`BuildMenuShortage`・`BuildMenuSessionStore`・`BuildMenuCategoryScrollTracker`）で Web と同じ見た目の UITK 画面にし、Model を Web の topic の源（`PlacementTargetResolver`・`ConstructionWalletQuery`・`LocalPlayerInventoryController`・`ClientBlueprintLibrary`・`IBlueprintThumbnailLookup`）から直に購読して、L1・並べ比べ（720p・2560×1080）・録画（`misc/webui-buildmenu-click`・`connect/electric-wire-tool-*` 5 本・`building/blueprint-place-like-block`・新設 `uitk/uitk-build-menu`。全体スイッチ UITK）まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。Model（`Client.Ui.State/BuildMenu/Source/BuildMenuModel`）は Client.Game の源を購読して不変の `BuildMenuViewModel` を `IReadOnlyReactiveProperty` で配る。エントリの組み立ては Web の橋 `BuildMenuEntryDtoFactory` の規則を State へ写した `BuildMenuEntryFactory`（橋のファイルは書かない）。検索と群分けの合成は純関数 `BuildMenuShownListComposer`（State）が持ち、View は `Render(BuildMenuViewModel)` と検索語の変化でそれを呼ぶ。View（`Client.Ui/Screens/BuildMenu`）は Model を知らず、操作は `IBuildMenuCommands`（選択・BP 削除・閉じる）を直接呼び、結果は `UiCommandResultToaster.Report` へ渡す。Presenter は起動時に 1 回だけ View を `UiLayer.Screen`・`UiFamily.Stage` へ載せ、`UiRoutingState.Current` で出し入れし、表示中だけ Model を購読する（前例 `PauseMenuScreenPresenter`）。スクロールスパイは L0-s3 の `BuildMenuCategoryScrollTracker` を View が `IBuildMenuScrollViewport` の実装 `BuildMenuScrollViewport`（`GameScrollView` の C3-b の口の上に組む。表示ごとに作り直す）で動かし、smooth scroll は `UiMotionPlayer`＋`ScrollOffsetTweenStep`。見た目の値は Web と同名のトークン（`Tokens.uss` の `--build-menu-*`・`--menu-upper-safe-area`・`--menu-lower-safe-area`・`--slot-size`・`--tutorial-anchor-clip-inset`）だけを使い、部品のつまみ（`--mode-switch-option-height`・`--icon-pad`）は画面のクラスで上書きする。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`GeometryChangedEvent`・`CustomStyleResolvedEvent`）、UniRx、UniTask、VContainer、NUnit＋`com.unity.ui.test-framework`（`UITestFixture`・`simulate.FrameUpdateMs`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（test-name-parity）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`（repo の兄弟）、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`。Files 節と各コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（とくに「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1.1・§2.3・§2.4・§2.7・§2.8・§3・§4.1・§4.2・§4.3・§5.1・§7.1 の A-20・A-47・A-49・A-50・A-57・A-78・A-79）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S3-buildmenu 行・§4.3・§5・§6.2・E-1・E-13・E-20）、L0-s3 計画 `docs/superpowers/plans/2026-11-06-uitk-l0-s3.md` の「後段レーンへ渡すもの」。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ。この計画が設計書・完了条件と違う所は判断記録（D-5・D-18）に書いた。設計書の A-100〜A-105 は計画の段階で反映済み

## Requirements

設計の出所: 実行計画 §1.4 の S3-buildmenu 行（完了条件の原文:「面の寸法 `calc(var(--slot-size) - 8px)` は View の C# が導出 [A-47]。3 カラム・縦 `ModeSwitch`・`GameTextField`(SearchInput)・1 本のスクロールにスクロールスパイと smooth scroll（UiMotion）・詳細の sticky・BP 右クリック削除・不足ツールチップ・セッション復元・ドラッグ源。アンカー `build-menu.entry-*`。写す: P21・C22（L30 は L0-s3 済み）。録画 `misc/webui-buildmenu-click`・`connect/electric-wire-tool-*` 5 本・`building/blueprint-place-like-block`。視口 `IBuildMenuScrollViewport` は `AppliedSpacerHeight`（レイアウト確定後のスペーサ実寸）も返す [A-49]。完了条件に「L0-s3 計画『後段レーンへ渡すもの』の 5 点を満たす」」）、設計書 §4.1・§4.2・[A-49]、L0-s3 計画の「後段レーンへ渡すもの」。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | Model: 解放済みの配置対象（`PlacementTargetResolver.CreateUnlockedTargets()`）・所持品・建設の財布・BP ライブラリ・BP のサムネイル・言語の変化から `BuildMenuViewModel`（カテゴリの並び・エントリの列。エントリは種別・カテゴリ対・表示名・必要素材〈`ItemId` 付き〉・支払い免除・1 セット設置〈Block だけ〉・アイコンの判別 union）を組む。規則は Web の橋 `BuildMenuEntryDtoFactory`・`BuildMenuMaterialAvailability` と同じ。メニューを開いたとき BP ライブラリを `Refresh` する | `BuildMenuEntryFactoryTest`・`BuildMenuShownListComposerTest` が緑 |
| R-2 | 3 カラム: 舞台の上 `--menu-upper-safe-area`・下 `--menu-lower-safe-area` の帯の水平中央に幅 `--build-menu-panel-width` の `GamePanel`（題 `ui.buildMenu.title`）。中は 左 `--build-menu-sidebar-width`・中央 残り・右 `--build-menu-detail-width`、列間・右の余白 `--build-menu-edge-safe-area`、下の余白 `--block-panel-bottom-safe-area`。右上に閉じる `IconButton`（top `--bevel-3`・right `--panel-edge-fade`） | `BuildMenuLayoutTest` 7 件が緑（Web `buildMenuLayout.spec.ts` 7 件） |
| R-3 | 縦の `ModeSwitch<Guid>`（`build-menu-sidebar`。選択肢 `build-menu-category-<guid>`・文はカテゴリ名のコンテンツ辞書キー）。エントリの無いカテゴリは出さず、検索でヒットの無いカテゴリは無効。高さはつまみ `--mode-switch-option-height` を `var(--build-menu-category-height)` で上書き | `BuildMenuViewTest`・`BuildMenuSearchViewTest`・`BuildMenuLayoutTest` が緑 |
| R-4 | 検索: `GameTextField(build-menu-search, false, GaugeInset)`・置き文字 `ui.buildMenu.searchPlaceholder`。同じリストを絞り込み、検索語があって 0 群なら `ui.buildMenu.noResults` を出す。検索語は `BuildMenuSessionStore.SetQuery` へ非 null で渡す | `BuildMenuSearchViewTest` が緑（Web regression 6・7・11 件目） |
| R-5 | 1 本のスクロール（`GameScrollView(build-menu-scroll-area)`）に全カテゴリの群（`build-menu-category-<guid>-group`・大見出し `build-menu-category-heading-<guid>`・FadeRule・サブカテゴリ節 `build-menu-section-<cat>-<sub>`・小見出し・FadeRule・8 列の `SlotGrid`〈`build-menu-grid-<cat>-<sub>`〉）と末尾スペーサ（`build-menu-trailing-spacer`）。サイドバーを押すとその見出しへ smooth scroll し、ハイライトが追従する | `BuildMenuScrollViewTest`・`BuildMenuCategoryListTest` が緑（Web regression 4・5 件目、`BuildMenuCategoryList.test.ts` 2 件） |
| R-6 | エントリ: `SlotFrame(build-menu-entry-<kind>-<id>)`（filled）＋`PlacementTargetFace`。アイコンの余白は `--icon-pad: 4px`（面 = `--slot-size` − 8px）。チュートリアルの錨 `AnchorIds.BuildMenuEntry(kind, id)`。不足があればツールチップ（題 `ui.buildMenu.materialShortageTitle`＋不足行）、枠は赤くしない | `BuildMenuSlotTooltipTest` 4 件・`BuildMenuViewTest` が緑 |
| R-7 | 操作: 左のタップで選択（`IBuildMenuCommands.SelectEntry`）、BP だけ右押下で削除（`DeleteBlueprint`）、閉じるで `Close`（GameScreen への遷移要求）。結果は `UiCommandResultToaster.Report`。良性の拒否（メニューが閉じている・未知のエントリ・BP が既に無い）はトーストしない | `BuildMenuCommandsTest`・`BuildMenuViewTest` が緑（Web regression 2・3 件目） |
| R-8 | 詳細（`build-menu-detail`）: 未ホバーは `ui.buildMenu.detailHint`。ホバーしたエントリを sticky に出し（アイコン・名前・FadeRule・必要素材の題〈1 セット設置なら `requiredItemsPerSet{count}`〉・3 列の `ItemSlot`〈不足は赤枠と所持/必要〉・残り設置数 `build-menu-remaining-placements`〈Block の 1 セット設置だけ〉） | `BuildMenuDetailViewTest` が緑（Web regression 8・10 件目、`BuildMenuDetailSidebar.test.ts` 3 件） |
| R-9 | 復元: 閉じて開き直すと検索語・スクロール位置・詳細の sticky が戻る | `BuildMenuRestoreTest` が緑（Web `buildMenuRestore.spec.ts` 1 件・regression 11 件目） |
| R-10 | ドラッグ源: エントリを C3-b の `UiDragDrop.MarkSource` で源にし、タップは選択、落とし先の解決は S3-hotbar-modal へ渡す口を 1 か所にする | `BuildMenuDragSourceTest` が緑。落とし先の申告が `done.md` の「integrator への依頼」にある |
| R-11 | L0-s3「後段レーンへ渡すもの」5 点（下の L-1〜L-5） | 各行の受け入れ基準 |
| L-1 | スペーサ反映後の `GeometryChangedEvent` で `OnLayoutChanged()` を呼び、末尾ジャンプが末尾群の見出しへ届く | `BuildMenuScrollViewTest`「末尾カテゴリへジャンプしても見出しが視口上端に来る」と録画 `uitk/uitk-build-menu` の末尾ジャンプの主張が緑 |
| L-2 | `AppliedSpacerHeight` はレイアウト確定後の実寸を返し、反映後の高さで保存位置を復元する | `BuildMenuRestoreTest`「スペーサ反映後の高さで保存位置を復元する」が緑 |
| L-3 | 復元前に ScrollView が `OnScrolled(0)` を流すと保存位置が 0 に潰れる（Web と同挙動）かを実機で確かめる | Task 8 Step 4 の実測値が `done.md`「証拠」と台帳「## 実測の結論」にあり、復元の主張（L1・録画）が緑 |
| L-4 | 張り替え時に新しい視口へ `SpacerHeight.Value` を当て直す | `BuildMenuRestoreTest`「開き直した視口へ要求済みのスペーサ高を当て直す」が緑 |
| L-5 | 検索語・入力文は非 null で渡す | `BuildMenuSearchViewTest`「検索欄を空にしても検索語は空文字で保存される」が緑 |
| R-12 | 並べ比べ: 参照 `3-build-menu`（1280×720・2560×1080）と UITK を並べ、差の表を `done.md` に書く | 2 解像度の組（参照と同じ内容の DEMO の VM で撮る）が報告ページにあり、差の表の全行が「同じ／直した／LX-n」のどれか（中身を理由に「捨てる」にしない）。色の差は、所有レーンの直しを取り込んで撮り直し一致を確かめるか、LX に記帳できる性質のものを `LX-72`〜`LX-75` に記帳して閉じるかの 2 択で、どちらかが済むまで完了・マージしない |
| R-13 | 登録: `MainGameUiRegistration` の画面登録区画に 1 行（`BuildMenuGameUiRegistration.Register(builder);`）、`GameUiTheme.tss` の `Screens` 区画に `@import` 1 行 | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`・`TutorialAnchorCoverageTest`）が緑 |
| R-14 | 録画: `misc/webui-buildmenu-click`・`connect/electric-wire-tool-*` 5 本・`building/blueprint-place-like-block`・`uitk/uitk-build-menu` を全体スイッチ UITK で回す | 8 本の `result.json`（`blueprint-place-like-block` は D-18 の扱い）と mp4、`done.md` に `UiInputExclusivity.ActiveUiSystem` の値 |
| R-15 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑・未記帳 0 → `gh pr merge --merge` → bd を閉じる | `git log origin/feature/uitk` にこのレーンのマージコミット。`done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 部品（`GamePanel`・`ModeSwitch`・`GameTextField`・`GameScrollView`・`IconButton`・`FadeRule`・`SlotFrame`・`PlacementTargetFace`・`SlotGrid`・`ItemSlot`）・Foundation（`UiDragDrop`・`HoverTooltipHost`・`HoverTooltipMark`・`UiMotion`・`TutorialAnchor`）・トークン USS・`UiLayer`・`UiScreenTraits`・`AnchorIds` は変えない。要るものは `done.md` の「integrator への依頼」に書く
- ホットバーの落とし先（`HotbarSlotDropTarget` の印・`HotbarDropResolver` の実行・割当コマンド）と BP 名入力のモーダルは作らない（S3-hotbar-modal）。ビルドメニューからホットバーへの D&D の通し（L1。`AssignHotbar(1,id)` が 1 回だけ）・検索欄の入力中にゲームのキーが動かないこと・ESC の扱いは S3-ladder
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は一切変えない（Model は同じ規則を State へ写す。重複は「本PR外のリファクタ提案」）。例外は生成物の再生成だけ: `localization.csv` に行を足すので `pnpm --dir moorestech_web/webui run gen:i18n` で `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` を作り直し、`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の印（Editor の SchemaWatcher が CSV の変化で書き換える）と一緒に CSV と同じコミットへ入れる（前例 S2-skit・commit 42279a73e4）。S3-hotbar-modal も同じ CSV に足すので扱いは同じ
- `placementIconCentering.spec.ts`（2 件）はビルドメニュー側の 1 件「ビルドメニューのアイコンは枠の中央に描かれる」をこのレーンが写し（自分の画面の主張。本体の決定）、ホットバー側の 1 件は S3-hotbar-modal が写す。`textSelection.spec.ts` は S5-crosscut、`useHotbarDragSource.test.ts`（7 件）は S3-hotbar-modal
- L3（EditModeInPlayingTest）は持たない（段の締め S3-ladder）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s3-buildmenu --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる
- **依存**: C3-a（#1612・d4347f75ef。マージ済み）・C3-b（未マージ）・L0-s3（マージ済み）が入った `origin/feature/uitk`。C3-b のマージ前には着手しない（Task 1 Step 1 で確かめ、無ければ止める）。`Registration/GameUi/` は今 6 本で、先に入る C3-b・S2-notification・S2-skit・S2-tutorial が 1 本ずつ足すと 10 本になり、このレーンの 1 本で 11 本になる見込みが高い（推測）。Task 1 Step 1 の `ls` で 10 本以上なら、着手の最初に integrator へ再編の依頼を出す（S3-hotbar-modal と同じ依頼文にまとめる）。再編が入るまでは Task 8 の登録だけを保留して他の Task を進め、入ったら取り込んで登録を足す
- **使う部品と公開 API**（C3-a・既存は `origin/feature/uitk` の実物から写した。**C3-b の分だけ**は C3-b 計画 `docs/superpowers/plans/2026-11-07-uitk-c3-b.md` の Produces から写したので、Task 1 Step 1 で実物と照合し、違えば名前だけ読み替えて `done.md` の「未確認」に「API 名の読み替え: <旧> → <実物>」と書く）

```csharp
// ── C3-a（実物）namespace Client.Ui.Components.Slots
public sealed class SlotFrame : VisualElement
{
    public SlotFrame(string testid);                         // name = testid。.slot-frame
    public IObservable<SlotGesture> OnSlotGestured { get; }   // LeftDown・RightDown・LeftEnter・RightEnter・DoubleClick・HoverEnter・HoverLeave
    public override VisualElement contentContainer { get; }
    public void SetState(SlotFrameState state);
}
public readonly struct SlotFrameState { public SlotFrameState(bool selected, bool filled, bool catalog, bool insufficient, bool ghost); }
public readonly struct SlotGesture { public readonly SlotGestureKind Kind; public readonly bool Shift; }
public sealed class PlacementTargetFace : VisualElement { public PlacementTargetFace(); public void SetIconTarget(UiIcon icon, UiText displayName); public void SetNameTarget(UiText displayName); }
// PlacementTargetFace はテクスチャが null なら名前で描く。アイコンの余白はつまみ --icon-pad（既定 5px）
public sealed class SlotGrid : VisualElement { public SlotGrid(string testid, int columns); public void SetColumns(int columns); public void AddItem(VisualElement item); public void ClearItems(); }
public sealed class ItemSlot : VisualElement { public ItemSlot(string frameTestid); public IObservable<SlotGesture> OnSlotGestured { get; } public void SetContent(ItemSlotContent content); }
public sealed class ItemSlotContent { public ItemSlotContent(UiIcon icon, int? count, ItemSlotMarks marks, ItemSlotShortage shortage, ItemSlotGhost ghost, UiText tooltipOverride); }
public readonly struct ItemSlotMarks { public ItemSlotMarks(bool selected, bool catalog, bool insufficient); }
public sealed class ItemSlotShortage { public ItemSlotShortage(double ownedCount, double requiredCount, bool lacking, UiText tooltip); }
// namespace Client.Ui.Foundation.Icons
public sealed class UiIcon { public UiIcon(Texture2D texture, string idText, UiText label); public Texture2D Texture { get; } }
// namespace Client.Ui.Content（DI 登録は ContentGameUiRegistration 済み）
public sealed class GameContentPresentation { public UiIcon Item(ItemId id); public UiIcon Block(BlockId id); public UiText ItemName(ItemId id); }

// ── 既存（実物）namespace Client.Ui.Components.Panels / Controls / Decor
public sealed class GamePanel : VisualElement { public GamePanel(string testid, GamePanelFrame frame); public void SetTitle(UiText title); public override VisualElement contentContainer { get; } }
public sealed class IconButton : Button { public IconButton(string testid, VisualElement glyph); public IObservable<Unit> OnClicked { get; } }
public sealed class IconButtonCloseGlyph : VisualElement { }
public sealed class GameTextField : VisualElement { public GameTextField(string testid, bool multiline, GameTextFieldLook look); public TextField Field { get; } public IObservable<string> OnValueChanged { get; } public void SetValueWithoutNotify(string value); public void SetPlaceholder(UiText placeholder); }
public enum GameTextFieldLook { GaugeInset, MantineInput }
public sealed class GameScrollView : ScrollView { public GameScrollView(string testid); public IObservable<float> OnScrollOffsetChanged { get; } public void SetScrollOffset(float y); }
public sealed class FadeRule : FadeGradientRule { public FadeRule(); }
// namespace Client.Ui.Foundation.Text: LocalizedLabel { SetKey(LocalizationKey); SetText(UiText); }（行の高さのつまみ --localized-label-line-height）
// namespace Client.Ui.State.Text: UiText(LocalizationKey key)・UiText(LocalizationKey, IReadOnlyList<LocalizationTextParam>)・UiText.FromContent(ContentLocalizationKey)・UiText.FromVerbatim(string)・Resolve()
// namespace Mooresmaster.Localization.Generated: ContentLocalizationKeys.BuildMenuCategoryName(Guid)・BuildMenuSubCategoryName(Guid)、LocalizationKeys.Ui.BuildMenu.*（Title・DetailHint・NoResults・SearchPlaceholder・RequiredItems・RequiredItemsPerSet・RemainingPlacementCount・MaterialShortageTitle）
// namespace Client.Ui.Foundation.Anchor: TutorialAnchor.Mark(VisualElement element, params string[] ids)
// namespace Client.Game.InGame.Tutorial.UIHighlight: AnchorIds.BuildMenuEntry(string kind, string id)（"build-menu.entry-<kind>-<id>" を小文字で返す）
// namespace Client.Ui.Foundation.Tooltip（F1）: HoverTooltipMark.Mark(VisualElement element, UiText text)・Unmark(VisualElement element)・TryFindMarked(VisualElement from, out VisualElement marked, out UiText text)
// namespace Client.Ui.Foundation.Motion.Playback: UiMotionPlayer.Play(VisualElement target, IUiAnimation animation) → UiMotionHandle（Cancel()・Completed）。.Tween: UiTweenAnimation(UiMotionSpec spec, IUiTweenStep step)・UiMotionSpec(float durationMilliseconds, float delayMilliseconds, UiEasing easing, UiMotionLoop loop)・UiEasing { Linear, EaseOutCubic, EaseInOutSine }・UiMotionLoop { Once, Repeat, PingPong }。.Steps: ScrollOffsetTweenStep(ScrollView scrollView, Vector2 from, Vector2 to)。時計はパネルのスケジューラ（実時間。L1 は simulate.FrameUpdateMs で進める）
// namespace Client.Ui.Foundation.Theme: UssCustomStyleReader.TryReadPixels(ICustomStyle, string, out float)
// namespace Client.Ui.Foundation.Root / Layers / Stage: GameUiRoot.GetLayer(UiLayer layer, UiFamily family)・UiLayer.Screen・UiFamily.Stage

// ── L0-s3（実物）namespace Client.Ui.State.BuildMenu.Logic / Session、Client.Ui.Screens.BuildMenu.CategoryScroll、Client.Ui.State.MaterialTooltip
public sealed class BuildMenuCatalogEntry { public BuildMenuCatalogEntry(Guid targetId, Guid categoryGuid, Guid subCategoryGuid, string displayLabel, IReadOnlyList<BuildMenuRequiredItem> requiredItems, bool paymentWaived); /* TargetId・CategoryGuid・SubCategoryGuid・DisplayLabel・RequiredItems・PaymentWaived */ }
public sealed class BuildMenuRequiredItem { public BuildMenuRequiredItem(Guid itemGuid, int count, int held, bool lacking); /* ItemGuid・Count・Held・Lacking */ }
public sealed class BuildMenuCategory { public BuildMenuCategory(Guid categoryGuid, IReadOnlyList<Guid> subCategoryGuids); }
public sealed class BuildMenuCategoryGroup { /* CategoryGuid・Sections */ }  public sealed class BuildMenuSection { /* CategoryGuid・SubCategoryGuid・Entries */ }
public static class BuildMenuGrouping { public static IReadOnlyList<BuildMenuCategoryGroup> Group(IReadOnlyList<BuildMenuCategory> categories, IReadOnlyList<BuildMenuCatalogEntry> entries); public static IReadOnlyList<BuildMenuCatalogEntry> Search(string query, IReadOnlyList<BuildMenuCatalogEntry> entries); }
public static class BuildMenuShortage { public static bool IsItemInsufficient(BuildMenuCatalogEntry entry, BuildMenuRequiredItem item); public static IReadOnlyList<BuildMenuRequiredItem> InsufficientItems(BuildMenuCatalogEntry entry); }
public sealed class BuildMenuSessionStore { public BuildMenuSessionSnapshot Current { get; } public void SetQuery(string query); public void SetScrollTop(float scrollTop); public void SetHoveredEntry(Guid? entryId); }
public sealed class BuildMenuSessionSnapshot { public static readonly BuildMenuSessionSnapshot Initial; public string Query { get; } public float ScrollTop { get; } public Guid? HoveredEntryId { get; } }
public interface IBuildMenuScrollViewport { float ScrollTop { get; } float ClientHeight { get; } float ScrollHeight { get; } float AppliedSpacerHeight { get; } bool TryGetHeadingTop(Guid categoryGuid, out float top); bool TryGetGroupHeight(Guid categoryGuid, out float height); void SetScrollTopImmediately(float scrollTop); void ScrollSmoothlyTo(float scrollTop); }
public sealed class BuildMenuCategoryScrollTracker { public BuildMenuCategoryScrollTracker(BuildMenuSessionStore session); public IReadOnlyReactiveProperty<Guid?> ActiveCategory { get; } public IReadOnlyReactiveProperty<float> SpacerHeight { get; } public void AttachViewport(IBuildMenuScrollViewport viewport); public void SetVisibleCategories(IReadOnlyList<Guid> categoryGuids); public void JumpTo(Guid categoryGuid); public void OnScrolled(float scrollTop); public void OnUserIntervened(); public void OnLayoutChanged(); public void PersistScrollPosition(); }
public static class MaterialTooltipText { public static UiText Create(MaterialTooltipKind kind, string itemName, double requiredCount, double ownedCount); } // kind: BuildMenuMaterial・BuildMenuShortageLine

// ── C3-b（未マージ。C3-b 計画の Produces から写した。Task 1 Step 1 で照合）
// namespace Client.Ui.Components.Controls
public sealed class ModeSwitch<TValue> : VisualElement
{
    public ModeSwitch(string testid, ModeSwitchOrientation orientation, ModeSwitchRole role, IReadOnlyList<ModeSwitchOption<TValue>> options);
    public IObservable<TValue> OnOptionSelected { get; }
    public void SetSelected(TValue value); public void SetSelectedNone(); public void SetEnabledAll(bool enabled); public void SetOptionEnabled(TValue value, bool enabled);
}
public sealed class ModeSwitchOption<TValue> { public ModeSwitchOption(string testid, TValue value, UiText label); }
public enum ModeSwitchOrientation { Horizontal, Vertical }  public enum ModeSwitchRole { Group, Tablist }
// つまみ --mode-switch-option-height（既定 34.8px）。選択のクラス mode-switch__option--selected、無効は Button.SetEnabled(false)
// GameScrollView に足される口: float GetViewportHeight()・float GetContentHeight()・bool TryGetContentTop(VisualElement descendant, out float top)・IObservable<Unit> OnContentLayoutChanged
// namespace Client.Ui.Foundation.DragDrop
public interface IUiDragPayload<in TTarget> where TTarget : class { void OnTapped(); void OnDropped(TTarget target); void OnDroppedOutside(); }
public static class UiDragDrop { public static void MarkSource<TTarget>(VisualElement source, IUiDragPayload<TTarget> payload) where TTarget : class; public static void MarkTarget<TTarget>(VisualElement target, TTarget handler) where TTarget : class; public static void UnmarkTarget<TTarget>(VisualElement target) where TTarget : class; }
// 印は落とし先の型ごとの表。解放点から源と同じ型の最寄りの印を引き、別の型の印は飛ばす（無ければ OnDroppedOutside）。同じ源を 2 つ目の型で印すると LogError で無視。クラス ui-drop-target は全部の型の印が外れたときだけ消える（設計書 A-132）
// HoverTooltipHost は GameUiHoverTooltipInstaller が全体に 1 つ据える（画面は HoverTooltipMark で印を付けるだけ）
```

- **Model の源（Client.Game・Server の実物。Web の topic `build_menu.entries` を流している `S/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs`・`BuildMenuEntryDtoFactory.cs`・`BuildMenuMaterialAvailability.cs` と同じ購読元と規則）**

```csharp
// namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
public class PlacementTargetResolver { public IReadOnlyList<IPlacementTarget> CreateUnlockedTargets(); public bool TryResolve(Guid id, out IPlacementTarget target); }
public interface IPlacementTarget { Guid Id { get; } PlacementTargetKind Kind { get; } string DisplayName { get; } IReadOnlyList<(ItemId, int)> CreateRequiredItems(); }
// 具象: BlockPlacementTarget.BlockId・TrainCarPlacementTarget.TrainCarGuid・ConnectToolPlacementTarget.ConnectToolGuid・BlueprintPlacementTarget.BlueprintGuid/.Blueprint・BlueprintCopyPlacementTarget
// namespace Game.PlacementTarget: enum PlacementTargetKind { Block, TrainCar, ConnectTool, BlueprintCopy, Blueprint }
// namespace Game.Construction（server）
public class ConstructionWalletQuery { public ConstructionWalletStatus? GetWalletStatus(BlockId blockId); public IObservable<…> OnWalletChanged { get; } }
public readonly struct ConstructionWalletStatus { public int PlacementsPerCost { get; } public int RemainingCount { get; } public bool CoversNextPlacement(); }
public static class ConstructionMaterialAccounting { public static IReadOnlyDictionary<ItemId, int> TallyHeld(IEnumerable<IItemStack> items); public static IReadOnlyList<(ItemId itemId, int held, int required)> MatchRequirements(IReadOnlyList<(ItemId, int)> required, IReadOnlyDictionary<ItemId, int> held); }
// namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util: ConstructionCostShortageCalculator.CalculateRequirements(target.CreateRequiredItems(), heldByItem)
// namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning: BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint.Blueprint)・BlueprintPasteCostCalculator.CalcRequiredItems(drafts, walletQuery, bool freeBlockPlacement)
// namespace Common.Debug: DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)
// namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
public class ClientBlueprintLibrary : IBlueprintDeleteService, IBlueprintLookup { public IObservable<…> OnChanged { get; } public UniTask Refresh(CancellationToken ct); }
public interface IBlueprintDeleteService { UniTask<BlueprintDeleteResult> DeleteBlueprint(Guid blueprintGuid, CancellationToken ct); }
public enum BlueprintDeleteResult { Success, NotFound, NotUnlocked, RequestFailed, Unknown }
// namespace ….Blueprint.Thumbnail: IBlueprintThumbnailLookup { IObservable<Guid> OnThumbnailChanged; bool Contains(Guid); bool TryGet(Guid, out Texture2D) }
// namespace Client.Game.InGame.UI.Inventory.Main: LocalPlayerInventoryController.LocalPlayerInventory（IEnumerable<IItemStack>・OnItemChange）・OnInventoryRefreshed
// namespace Client.Game.InGame.UI.BuildMenu: BuildMenuSelection.SetSelectedTarget(IPlacementTarget)（BuildMenuState が次の Update で PlaceBlock へ遷移させる）
// namespace Client.Game.InGame.UI.UIState: UIStateControl.CurrentState・OnStateChanged・RequestTransition(UIStateEnum)、UIStateEnum.BuildMenu・GameScreen
// namespace Client.Game.InGame.Context: ClientContext.TrainCarImageContainer.GetTrainCarView(Guid).ItemTexture・ClientContext.ConnectToolImageContainer.GetConnectToolView(Guid).ItemTexture（Web の橋 ResolveIconUrl が指す絵と同じ）
// MasterHolder.BuildMenuCategoryMaster.Categories（並び）・GetGuidPair(category, subCategory)・GetPairByEntrySource(EntrySourceConst.trainCars/connectTools/blueprintCopyTool/savedBlueprints)、MasterHolder.ItemMaster.GetItemGuid(ItemId)
// DI: MainGameInteractionRegistration が ClientBlueprintLibrary（`.AsSelf().As<IBlueprintLookup>()`。このレーンが `.As<IBlueprintDeleteService>()` を足す）・BlueprintThumbnailContainer（As IBlueprintThumbnailLookup）・PlacementTargetResolver・BuildMenuSelection、MainGameModelRegistration が LocalPlayerInventoryController・ConstructionWalletQuery を登録済み。UIStateControl はシーンの component
```

- **層の向き**（設計書 §1.1）: `Client.Ui.State/**` は Client.Game・Core.Master・Game.PlacementTarget・Server.Protocol を参照してよい（asmdef 既存）。このレーンは `Client.Ui.State.asmdef` に `Common.Debug`・`Game.Construction`・`Client.Mod` を足す（Task 3。前例 S2-mode-tooltip D-38 が `Game.PlacementTarget` を足した）。`Client.Ui/Screens/**` は `Client.Ui.State.*`・Components・Foundation・Content を参照してよい。Screens から `Client.Game` の型は ViewModel 越しにだけ受ける（例外: `AnchorIds` の文字列定数）。State は `Client.Ui`（`UiIcon` 等）を参照できないので、アイコンは判別 union（`Client.Ui.State.PlacementTarget.PlacementTargetIcon`）で渡し、面への当て方と `UiIcon` は `Client.Ui.Content.PlacementTargetIcons` の 1 か所が決める
- **AGENTS.md の規約**: 1 ファイル 200 行未満・1 ディレクトリ 10 ファイルまで（.meta は数えない）・`partial`/`Func<>` 禁止・イベントは UniRx・try-catch 禁止・デフォルト引数禁止・単純な getter/setter プロパティ禁止（`{ get; private set; }` と式本体の読み取り専用は可）・主要処理に「// 日本語 → // English」の 2 行コメント（各 1 行）・メソッド内のローカル関数は `#region Internal`・初期化は `Initialize`・fail-closed の経路はログ・`.meta` は手で作らない・Prefab/シーンを手で書き換えない・`Update()` で毎フレームの同値判定をしない
- **USS の規約**（ref-uitk-design・設計書 §3.2）: 画面 USS はルートのクラス `.build-menu` で始まり、画面のクラスは `build-menu__*`。部品のクラス（`.slot-frame`・`.mode-switch`・`.game-scroll-view` 等）を書かない。部品の見た目は、部品の要素に画面のクラスを足してそのクラスでつまみを上書きする（`customStyle` は祖先の宣言を当てにしない。D-26）。色は `var()` だけ、`z-index`・構造擬似クラス・合成の太字を使わない。USS の先頭に `/* web: <Web の CSS> */` を日英で書く
- **申し送りの罠**（I-fix1・C3-a からの実測）: (a) 字間は px 表記でも UITK では「字の大きさ / 100 × 値」で効く。この画面の Web の CSS に字間は無いが、足すときは I-fix1 の換算トークンを `var()` で使い、換算値を直書きしない (b) ギャラリーの撮影は sRGB 合成で半透明が淡く出るので、色はランタイムの PanelSettings（`GameUiPanelSettings.asset` の複製）で撮って比べる（Task 10） (c) 暗い半透明の α は I-fix1（e26010aa0c）で指数 1.85 に選び直し済み。明るい半透明（`--mode-switch-selected-face` 等）は未実測なので、差の表で見た差を「未確認」に書き、直すなら integrator へ依頼する（トークン USS はこのレーンの持ち物でない）
- **テストの置き場と名前**: `S/Client.Tests/Ui/Screens/BuildMenu/`（補助は `Support/`）・`S/Client.Tests/Ui/State/BuildMenu/`、namespace はディレクトリに合わせる。テスト名は日本語の挙動文。Web から写したものは `[Test]` の直前に `/// <web-test file="<W/ からの相対パス>">it/test の第 1 引数</web-test>`（describe は含めない。テンプレート文字列も展開しない。実行計画 E-13）
- **コマンド**: コンパイル `uloop compile --project-path ./moorestech_client`（期待: ErrorCount 0）。テスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。このレーンのテストフィルタ（以下 `$FILTER`）は `^Client\.Tests\.(Ui\.(Screens\.BuildMenu|State\.BuildMenu|Conventions|Theme|Layers|Anchor|State\.Commands|State\.PauseMenuCommandsTest)|WebUi\.BuildMenuActionsTest)\.`（`PauseMenuCommandsTest` は namespace `Client.Tests.Ui.State` 直下なので名指しで入れる。S3-hotbar-modal と共有のテスト）。ドメインリロード中のエラーは 45 秒待って再試行
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有ファイルは `GameUiTheme.tss` の `Screens` 区画の `@import` 1 行・`MainGameUiRegistration.cs` の画面登録区画の 1 行・`S/Client.Ui.State/Commands/UiCommandRejectReason.cs` の enum 末尾と 3 つの switch の末尾（前例 S2-skit）・`Localization/localization.csv` の `ui.commandRejected.*` の末尾 6 行とその生成物 2 つ（`localizationKeys.ts`・`_CompileRequester.cs`。再生成だけ）・`S/Client.Ui.State/Client.Ui.State.asmdef` の参照 3 つ・`docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」「## 追従記録」の末尾・`S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` の build menu の行の削除（在るときだけ）・`S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` の良性の一覧のテスト（S3-hotbar-modal も同じテストを同じ名前へ置き換える。取り込みの衝突は `benign` の値の和を採る）・`MainGameInteractionRegistration.cs` の `ClientBlueprintLibrary` の行への `.As<IBlueprintDeleteService>()` 1 つ（D-35）・`ContentGameUiRegistration.cs` の末尾の `PlacementTargetIcons` の登録 1 行（D-42）・`S/Client.Ui/Content/IGameContentTextureSource.cs`・`ClientContextContentTextureSource.cs`・`S/Client.Tests/Ui/Content/FakeContentTextureSource.cs` への `TrainCarTexture`・`ConnectToolTexture` の追記（D-29。C3-a はマージ済みで W3 で後に走る C4-a〈液体の絵の出どころ〉が同じインターフェースへ追記しうる。取り込みの衝突は両方のメソッドを採る）。新設して持ち主になる共有の置き場は `S/Client.Ui.State/PlacementTarget/**`・`S/Client.Ui/Content/PlacementTargetIcons.cs`・`S/Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver.cs`（A-100）。持ち物は File Structure の「新規」の行と同じ: `S/Client.Ui/Screens/BuildMenu/**`・`S/Client.Ui.State/BuildMenu/**` への追加・`S/Client.Ui.State/PlacementTarget/**`・`S/Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver.cs`・`S/Client.Ui/Content/PlacementTargetIcons.cs`・`S/Client.Tests/Ui/Screens/BuildMenu/**`・`S/Client.Tests/Ui/State/BuildMenu/**`・`S/Client.Starter/Registration/GameUi/BuildMenuGameUiRegistration.cs`・`tools/uitk/render-mock-placeholder-icons.mjs`（撮影の道具）・シナリオ `SKILL/scenarios/uitk/uitk-build-menu.cs`
- **新しい共有の申告**（設計書 §5.1-2）: 新しい共有の型（他の画面も使う型・Foundation・Components）が要ると分かったら自分で作らず、`done.md` の「## integrator への依頼」に「<種別>: <名前>・使う画面・API 案・このレーンでの仮置き」を書く。例外（本体の決定）: 配置対象の絵の出どころ（`State/PlacementTarget/PlacementTargetIcon.cs`・`PlacementTargetIconResolver.cs`・`Ui/Content/PlacementTargetIcons.cs`）と落としの受け口（`State/Hotbar/Logic/IHotbarDropReceiver.cs`）は、S3-hotbar-modal が後からこのレーンのファイルを書き換えないよう、先に走るこのレーンが最終の置き場に作る。持ち主はこのレーン、S3-hotbar-modal は使うだけ。`done.md` には「作った・持ち主は S3-buildmenu」と書く（Task 1 Step 3 で雛形に書く: (1) 落としの受け口 `IHotbarDropReceiver`〈D-16〉 (2) 絵の出どころ 3 ファイル〈D-29〉 (3) 2560×1080 の参照の台帳記載〈撮ったときだけ〉）
- **参照キャプチャ**: 1280×720 は `LOGS/uitk/reference-captures/web-869b2d4264/3-build-menu.png`（最新の 720p。`manifest.json`・`index.md`）、2560×1080 は `LOGS/uitk/reference-captures/web-0e81bbe3c3-2560x1080/3-build-menu.png`
- **並べ比べの画像の原本**は worktree の gitignore 下 `moorestech_client/.uloop/outputs/compare/S3-buildmenu/` に置き、`moorestech_logs` へ入れない。`moorestech_logs` へ書く前に `LOGS/README.md` を読む
- **報告**: `LOGS/harness/uitk/S3-buildmenu/done.md`（1 行目「状態: 完了」。途中で止まるときは「状態: 停止（理由）」）。節は「証拠」「差の表」「要裁定」「integrator への依頼」「未確認」

## 設計検査記録

- 配置検査: 実施済み / 違反4件・修正4件 / Content を値だけに・State から絵を外し ID の union へ・Screens から Game.PlacementTarget を外す・受け口を void Receive に。再検査: 違反1・強3・弱4、全部反映
- Phase 2.6: 実施済み / 強2・弱6・第3バケツ5 / 1 セット設置を union に・共有の置き場の出所を §5.1-1 へ、弱は判定の重複を 1 本に・空の表示名を LogError。再検査: 違反1・強3・弱4、全部反映
- 再検査 3（独立評価 3〜4 巡目の後の変更範囲・fresh-context）: 実施済み / 違反1・強0・弱2・第3バケツ0 / 全部反映（詳細の文は既存の `UiTypographyClass.TextMd` を付けて USS は行送りだけ・視口の未レイアウト判定を private の 1 述語に・設計書 A-103 に群の寸法変化と未レイアウトの扱いを追記）
- 再検査 2（独立評価 2 巡目の後の変更範囲・fresh-context）: 実施済み / 違反2・強0・弱0 / 全部反映（撮影用スクリプトの Playwright を webui から解決・`Registration/GameUi/` の 11 本目で止める）

## 死活表（Phase 2.5）

全体スイッチ UITK（`UiInputExclusivity.ActiveUiSystem == GameUi`）で起動したとき、Web の系で動いていた振る舞いが生きるか。

| 振る舞い | GameUi で生きるか | 根拠 |
|---|---|---|
| B でビルドメニューを開く・B／ESC／UI の外の右短押しで閉じる | 生きる | `BuildMenuState`（Client.Game）が両系で遷移を持つ。UI の外の判定は `UiPointerHitTest` → `UiInputExclusivity.IsPointerOverActiveUi`（GameUi は `GameUiPointerProbe` の pick）。パネルの帯（`.build-menu`）は `PickingMode.Ignore`、パネルは拾う（Task 6 の L1） |
| エントリ選択 → PlaceBlock | 生きる | `BuildMenuCommands.SelectEntry` が橋と同じく `BuildMenuSelection.SetSelectedTarget` を呼び、`BuildMenuState.GetNextUpdate` が遷移する（Task 4・録画 `misc/webui-buildmenu-click`） |
| BP の右クリック削除 | 生きる | `ClientBlueprintLibrary.DeleteBlueprint`（両系で登録）を `BuildMenuCommands.DeleteBlueprint` が呼ぶ |
| メニューを開いたときの BP ライブラリの更新 | 生きる | 橋の `BuildMenuTopic.OnStateChanged` と同じく `BuildMenuModel` が `Refresh` を呼ぶ。両系で走っても `Refresh` は冪等（橋は Web の系でだけ登録される。`WebUiHost` の登録は `UiSystem.Web` の分岐） |
| 不足ツールチップの表示 | C3-b のマージ後に生きる | 印は `HoverTooltipMark`（F1）、出すのは C3-b の `HoverTooltipHost`。C3-b のマージを着手の前提にした |
| ホットバーへの D&D | S3-hotbar-modal のマージまで死ぬ | 源（タップ＝選択）はこのレーン、落とし先（ホットバーの枠の印と割当）は S3-hotbar-modal。`BuildMenuEntryDragPayload.OnDropped` は最初から `IHotbarDropReceiver` へ渡し、受け口が無いうちは枠の外と同じく何もしない（Web と同じ。D-16）。S3-hotbar-modal は枠の印で受け口を実装するだけで、ビルドメニューのファイルは変えない |
| 錨 `build-menu.entry-*` のチュートリアル強調 | S2-tutorial のマージ後に生きる | このレーンは `TutorialAnchor.Mark` で名前を付けるだけ。`TutorialAnchorCoveragePending.cs` に build menu の行があれば同じ PR で消す（Task 6 Step 6・Task 13 Step 2） |
| 検索欄の入力中にゲームのキー（B で閉じる等）が動かない | C2-b の `GameTextField` のフォーカス中の入力遮断に依る | 確かめるのは S3-ladder（L1）。このレーンの録画は検索欄へ英字を打たず、L1 は `SetValueWithoutNotify`＋`OnValueChanged` で流す |
| 録画 `building/blueprint-place-like-block` の BP 名入力（`modal-input`） | S3-hotbar-modal のマージまで死ぬ | BP 名のモーダルは S3-hotbar-modal の持ち物（W3 の順でこのレーンの後）。扱いは D-18 |
| プレイテスト DSL の UI 経路（`p.ClickBuildMenuBlock`・`PlaytestBuildMenuOps` が待つ testid） | 生きる（確かめる） | DSL が待つ id は `build-menu-entry-<kind>-<TargetId>`。`BuildMenuEntrySlot.TestIdOf` の `TargetId` は配置対象の `Id`（Block は `BlockGuid`）と同値なので、DSL がマスタから引く Guid と一致する。Task 1 Step 1 で `PlaytestBuildMenuOps` の待つ id の組み立てを読んで照合し、録画 `misc/webui-buildmenu-click`・`uitk/uitk-build-menu` が通しで見る |
| 開いたままの言語切替 | 生きる | Model が `Localize.OnLanguageChanged` で開いている間に VM を組み直し（Task 3）、見出しは `UiText`・ツールチップは組み直しで張り替わる（D-15）。L1 は持たず、Task 10 の撮影の後に言語を戻す操作で目視 |
| パネルの上のホイールがカメラのズームへ漏れない | 生きる（C3-b・C2-b の入力の裁きに依る） | GameUi の入力の裁き（パネルの上のポインタはゲームのホイールを止める）に依る。このレーンは `GameScrollView` がホイールを受けることだけを使う。録画 `uitk/uitk-build-menu` の末尾ジャンプの後の手のスクロールで、カメラが動かないことを目視し `done.md` に書く |
| 詳細の列の必要素材（`ItemSlot`）のツールチップ | 生きる | `ItemSlotShortage` に `MaterialTooltipText` を渡す（Task 6）。L1 は `BuildMenuDetailViewTest`（R-8） |
| カテゴリ押下のジャンプ・スクロールスパイ・検索・閉じて開き直す復元 | 生きる | L1 の `BuildMenuScrollViewTest`・`BuildMenuSearchViewTest`・`BuildMenuRestoreTest`（R-3〜R-9）と録画 `uitk/uitk-build-menu` |

---

## File Structure

| パス | 責務 | 新規／変更 |
|---|---|---|
| `S/Client.Ui.State/BuildMenu/Entries/BuildMenuViewModel.cs` | 不変の ViewModel（カテゴリの並び・エントリの列・id 引き） | 新規 |
| `S/Client.Ui.State/BuildMenu/Entries/BuildMenuEntryViewModel.cs` | エントリ 1 件（`Catalog`・`Kind`・`Icon`・`SetPlacement`・`Materials`） | 新規 |
| `S/Client.Ui.State/BuildMenu/Entries/BuildMenuMaterialRow.cs` | 必要素材 1 行（`ItemId` と `BuildMenuRequiredItem`）と、VM が不足を 1 回だけ判定した `BuildMenuMaterialLine` | 新規 |
| `S/Client.Ui.State/PlacementTarget/PlacementTargetIcon.cs` | 配置対象の絵の出どころの判別 union（ID だけ: Block・TrainCar・ConnectTool・撮影済み Blueprint・NameOnly。Blueprint 以外は値の等価）。最終の置き場・持ち主はこのレーン | 新規 |
| `S/Client.Ui.State/PlacementTarget/PlacementTargetIconResolver.cs` | 対象 → `PlacementTargetIcon`（橋 `ResolveIconUrl` の写し）。最終の置き場 | 新規 |
| `S/Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver.cs` | ホットバーへの落としの受け口（`Receive(HotbarDragSource)`）。このレーンが作るのはこの 1 本、S3-hotbar-modal は枠の印で実装する | 新規 |
| `S/Client.Ui/Content/PlacementTargetIcons.cs` | 絵の出どころ → `UiIcon` の値（DI の Singleton。面へ当てる分岐は Screens）。最終の置き場 | 新規 |
| `S/Client.Ui.State/BuildMenu/Entries/BuildMenuSetPlacement.cs` | 1 セット設置の判別 union（`None`・`BuildMenuSetPlacementAvailable(perCost, remaining)`） | 新規 |
| `S/Client.Ui.State/BuildMenu/Entries/BuildMenuEntryKindName.cs` | 種別 → Web の kind 文字列（testid・錨） | 新規 |
| `S/Client.Ui.State/BuildMenu/Entries/BuildMenuShownList.cs` | 合成の結果（出す群・サイドバーの項目・該当なし） | 新規 |
| `S/Client.Ui.State/BuildMenu/Entries/BuildMenuShownListComposer.cs` | VM と検索語から `BuildMenuShownList` を作る純関数 | 新規 |
| `S/Client.Ui.State/BuildMenu/Source/BuildMenuModel.cs` | 源を購読し `Current` で配る（`IInitializable`・`IDisposable`） | 新規 |
| `S/Client.Ui.State/BuildMenu/Source/BuildMenuEntryFactory.cs` | 配置対象 → エントリ（橋 `BuildMenuEntryDtoFactory` の写し） | 新規 |
| `S/Client.Ui.State/BuildMenu/Source/BuildMenuCategoryPairResolver.cs` | 対象 → カテゴリ対 | 新規 |
| `S/Client.Ui.State/BuildMenu/Source/BuildMenuMaterialRowsBuilder.cs` | 必要素材の行（通常・BP の貼り付け費用） | 新規 |
| `S/Client.Ui.State/BuildMenu/Commands/IBuildMenuCommands.cs` | 型付きコマンド | 新規 |
| `S/Client.Ui.State/BuildMenu/Commands/BuildMenuCommands.cs` | 前提の検証と実行（橋 `BuildMenuActions`・`UiStateActions` の写し） | 新規 |
| `S/Client.Ui.State/Commands/UiCommandRejectReason.cs` | enum 末尾に 6 値・switch 3 つの末尾に各 6 腕 | 変更（共有区画） |
| `S/Client.Ui.State/Client.Ui.State.asmdef` | 参照 `Common.Debug`・`Game.Construction`・`Client.Mod` | 変更（共有） |
| `Localization/localization.csv` | `ui.commandRejected.*` の末尾 6 行 | 変更（共有区画） |
| `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` | `gen:i18n` の再生成（手で書かない） | 変更（生成物） |
| `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` | CSV の変化の印（SchemaWatcher が書く） | 変更（生成物） |
| `S/Client.Ui/Screens/BuildMenu/BuildMenuScreenPresenter.cs` | View を Screen/Stage へ 1 回載せ、出し入れ・表示中の購読 | 新規 |
| `S/Client.Ui/Screens/BuildMenu/BuildMenuView.cs` | 画面ルート `.build-menu`。パネル・3 列の組み立てと結線 | 新規 |
| `S/Client.Ui/Screens/BuildMenu/BuildMenu.uss` | 画面の USS（配置・つまみの上書き・文字） | 新規 |
| `S/Client.Ui/Screens/BuildMenu/CategoryScroll/BuildMenuScrollViewport.cs` | `IBuildMenuScrollViewport` の実装（`GameScrollView` の上。表示ごとに作る。smooth scroll・介入・転送の門・隠すときの凍結） | 新規 |
| `S/Client.Ui/Screens/BuildMenu/CategoryScroll/BuildMenuScrollClipInset.cs` | `--tutorial-anchor-clip-inset` を読み、スクロールの負の外余白・中身の内余白・バーの上下右を C# で導く（[A-47] の付け替え。D-5） | 新規 |
| `S/Client.Ui/Screens/BuildMenu/CategoryScroll/BuildMenuScrollBinding.cs` | 表示ごとの視口の張り替え・隠すときの保存と凍結・スペーサ高の反映（L-4） | 新規 |
| `S/Client.Ui/Screens/BuildMenu/BuildMenuEntryActions.cs` | `IBuildMenuEntryHandler` の実装（選択・BP 削除をコマンドへ、結果をトーストへ、ホバーを詳細とセッションへ） | 新規 |
| `S/Client.Ui/Screens/BuildMenu/Parts/BuildMenuPanelSkeleton.cs` | パネルの骨（帯・`GamePanel`・閉じる・3 列・中央列の検索とスクロール）を組んで要素を返す | 新規 |
| `S/Client.Ui/Screens/BuildMenu/Parts/BuildMenuCategorySidebar.cs` | 縦の `ModeSwitch<Guid>` の作り直しと選択・無効の反映 | 新規 |
| `S/Client.Ui/Screens/BuildMenu/Parts/BuildMenuCategoryList.cs` | 群・見出し・節・格子・末尾スペーサ。構造が同じなら差し替えだけ | 新規 |
| `S/Client.Ui/Screens/BuildMenu/Parts/BuildMenuSectionView.cs` | サブカテゴリ節 1 つ（小見出し・FadeRule・8 列の格子） | 新規 |
| `S/Client.Ui/Screens/BuildMenu/Parts/BuildMenuEntrySlot.cs` | エントリ 1 マス（枠・面・錨・ツールチップの印・ドラッグ源・身振り） | 新規 |
| `S/Client.Ui/Screens/BuildMenu/Parts/BuildMenuShortageTooltipText.cs` | 不足ツールチップの文（題＋不足行を改行で結ぶ） | 新規 |
| `S/Client.Ui/Screens/BuildMenu/Parts/BuildMenuDetailSidebar.cs` | 詳細の sticky（ヒント・アイコン・名前・素材・残り） | 新規 |
| `S/Client.Ui/Screens/BuildMenu/Parts/BuildMenuEntryDragPayload.cs` | `IUiDragPayload<IHotbarDropReceiver>`（タップ＝選択・落としは `IHotbarDropReceiver` へ。別の型の落とし先は Foundation が飛ばして `OnDroppedOutside` になり、枠の外と同じく何もしない） | 新規 |
| `S/Client.Ui/Screens/BuildMenu/Parts/IBuildMenuEntryHandler.cs` | マスから View への操作の口（選択・削除・ホバー） | 新規 |
| `S/Client.Starter/Registration/GameUi/BuildMenuGameUiRegistration.cs` | Store・Model・Commands・Presenter の登録 | 新規 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画に 1 行 | 変更（共有区画） |
| `S/Client.Starter/Registration/GameUi/ContentGameUiRegistration.cs` | 末尾に `PlacementTargetIcons` の登録 1 行 | 変更（C3-a のファイルへのこのレーンの追記） |
| `S/Client.Starter/Registration/MainGameInteractionRegistration.cs` | `ClientBlueprintLibrary` の行に `.As<IBlueprintDeleteService>()` を 1 つ | 変更（共有ファイルの 1 行の追記） |
| `S/Client.Ui/Content/IGameContentTextureSource.cs`・`ClientContextContentTextureSource.cs` | `TrainCarTexture`・`ConnectToolTexture` の 2 メソッド | 変更（C3-a のファイルへのこのレーンの追記。W3 で後に走る C4-a〈液体の絵の出どころ〉が同じインターフェースへ追記しうる。取り込みの衝突は両方のメソッドを採る） |
| `S/Client.Tests/Ui/Content/FakeContentTextureSource.cs` | 同じ 2 メソッド（`block` のテクスチャを返す） | 変更（同上） |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | `Screens` 区画に `@import` 1 行 | 変更（共有区画） |
| `S/Client.Tests/Ui/State/BuildMenu/BuildMenuShownListComposerTest.cs` | 合成の L0 | 新規 |
| `S/Client.Tests/Ui/State/BuildMenu/BuildMenuEntryFactoryTest.cs` | 種別・カテゴリ対・アイコン・1 セット設置・免除（テスト mod） | 新規 |
| `S/Client.Tests/Ui/State/BuildMenu/BuildMenuCommandsTest.cs` | 選択・削除・閉じるの結果と拒否理由 | 新規 |
| `S/Client.Tests/Ui/State/BuildMenu/AllPlacementTargetsUnlocked.cs` | 全解放の `IGameUnlockStateData`（前例 `BuildMenuEntryDtoFactoryTest` の private 型の写し） | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Support/BuildMenuSamples.cs` | Web の fixture に合わせた VM（10 カテゴリ・名前・素材） | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Support/RecordingBuildMenuCommands.cs` | 呼び出しの記録 | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Support/RecordingBuildMenuEntryHandler.cs` | マスからの操作の記録 | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Support/BuildMenuReferenceDemo.cs` | 参照キャプチャ `3-build-menu` と同じ中身の VM（並べ比べの撮影用。絵は mock-host の種番号） | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Support/BuildMenuReferenceDemoTextures.cs` | 種番号の PNG を返す撮影専用の `IGameContentTextureSource` | 新規 |
| `tools/uitk/render-mock-placeholder-icons.mjs` | mock-host の `placeholderIcon(n)` を Chromium で 64×64 の PNG にする（撮影の道具） | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Support/BuildMenuScreenTestHost.cs` | View・Store・Tracker の組み立てと表示の切り替え | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuViewTest.cs` | regression 1・2・3・9 件目 | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuSearchViewTest.cs` | regression 6・7・11 件目、L-5 | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuScrollViewTest.cs` | regression 4・5 件目（5 件目が L-1）、ジャンプ中の介入（D-11）、群の高さの配分だけが変わったときのハイライトの引き直し（D-9） | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuCategoryListTest.cs` | `BuildMenuCategoryList.test.ts` 2 件 | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuDetailViewTest.cs` | `BuildMenuDetailSidebar.test.ts` 3 件（部品） | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuDetailStickyViewTest.cs` | regression 8・10 件目（画面） | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuSlotTooltipTest.cs` | `BuildMenuSlot.test.ts` 4 件 | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuLayoutTest.cs` | `buildMenuLayout.spec.ts` 7 件・`placementIconCentering.spec.ts` のビルドメニュー側 1 件 | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuRestoreTest.cs` | `buildMenuRestore.spec.ts` 1 件、L-2・L-4・L-3 の観測（D-8）、末尾の群が視口より高い（スペーサ 0）ときの復元（D-43） | 新規 |
| `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuDragSourceTest.cs` | タップ＝選択・受け口へ 1 回渡す・受け口でない落とし先は何もしない | 新規 |
| `SKILL/scenarios/uitk/uitk-build-menu.cs` | 末尾ジャンプ・検索・閉じて開き直す復元・選択を UITK で通す録画 | 新規 |
| `docs/uitk/TODO.md` | 「## 実測の結論」末尾（L-3 の観測）、要れば「## 見た目の例外」末尾（LX-71〜LX-75） | 変更（共有区画） |
| `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` | 在れば build menu の行を消す | 変更（共有区画・在れば） |

ディレクトリのファイル数（.cs と .uss）: `State/BuildMenu/Entries/` 7・`Source/` 4・`State/PlacementTarget/` 2（新設）・`State/Hotbar/Logic/` 4→5・`Ui/Content/` 3→4・`Commands/` 2（既存 `Logic/` 9・`Session/` 2 は変えない）、`Screens/BuildMenu/` 4・`CategoryScroll/` 4→7・`Parts/` 9、`Tests/.../Screens/BuildMenu/` は既存の L0-s3 の 4 のまま（新しいテストは `Views/` 10・`Support/` 6〈Task 10 の `BuildMenuReferenceDemo`・`BuildMenuReferenceDemoTextures` を含む。`BuildMenuLayoutProbe` を出せば 7〉へ置く。直下に足すと 13 になるため）・`State/BuildMenu/` 4＋既存（Task 1 Step 1 で `ls` して 10 以下を確かめる。超えるなら `State/BuildMenu/Screen/` へ分ける）。`Registration/GameUi/` は実物が 6（Content・CursorTooltip・Hud・ModeHud・PauseMenu・Toast）で 6→7。W3〜S2 の他レーンも同じ置き場に足すので、Task 1 Step 1 の `ls` で数え、このレーンの 1 本を足すと 11 本以上になるなら、足す前に止めて指揮へ報告し、`done.md` の「integrator への依頼」に「`Registration/GameUi/` の分け方（例 `GameUi/Screens/`）」を書く（置き場の再編は共有の構造の変更で integrator の持ち物。S3-hotbar-modal と同じ扱い）。integrator の再編が `origin/feature/uitk` に入り、取り込んで 10 本以下の置き場へ足せるまで、このレーンは完了にせずマージもしない（AGENTS.md の 1 ディレクトリ 10 ファイル）。ほかはすべて 10 以下。

**写すテストの件数（Web の実物を grep して数え直した。tests-ladder と同じ）**

| Web のファイル（`W/` から） | 件数 | 写す | 写さない（処遇） | 写し先 |
|---|---|---|---|---|
| `e2e/tests/regression/buildMenu.spec.ts`（P） | 11 | 11 | 0 | `BuildMenuViewTest` 4・`BuildMenuSearchViewTest` 3・`BuildMenuScrollViewTest` 2・`BuildMenuDetailStickyViewTest` 2 |
| `e2e/tests/regression/buildMenuLayout.spec.ts`（P） | 7 | 7 | 0 | `BuildMenuLayoutTest` |
| `e2e/tests/regression/buildMenuRestore.spec.ts`（P） | 1 | 1 | 0 | `BuildMenuRestoreTest`（test-name-parity は束 5 に数える。D-23） |
| `src/features/buildMenu/views/BuildMenuCategoryList.test.ts`（C） | 2 | 2 | 0 | `BuildMenuCategoryListTest` |
| `src/features/buildMenu/views/BuildMenuDetailSidebar.test.ts`（C） | 3 | 3 | 0 | `BuildMenuDetailViewTest` |
| `src/features/buildMenu/views/BuildMenuSlot.test.ts`（C） | 4 | 4 | 0 | `BuildMenuSlotTooltipTest` |
| `e2e/tests/regression/placementIconCentering.spec.ts`（P） | 2 | 1 | 0（ホットバー側の 1 件は S3-hotbar-modal が写す） | `BuildMenuLayoutTest`（ビルドメニュー側の 1 件） |
| `src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts`（C）・`logic/*.test.ts`（L: `buildMenuGrouping` 15・`buildMenuScrollSpy` 12）・`sessionState` 4 | 13・31 | 0 | 0（L0-s3 が写し済み） | — |

合計: P20（この表の P の写す数。実行計画の P21 はホットバー側の 1 件〈S3-hotbar-modal が写す〉を含む数）・C9（実行計画の C22 は L0-s3 済みの 13 を含む数）・L31（L0-s3 済み。実行計画の L30 との 1 の差は Task 1 Step 3 で `done.md` の「未確認」に書く）。部分だけ写すテストの捨てた部分は D-32。

---
## Tasks

### Task 1: 前提の確認と記録の準備

**Files:**
- Create: `LOGS/harness/uitk/S3-buildmenu/done.md`（雛形。moorestech_logs 側）

**Interfaces:**
- Consumes: `origin/feature/uitk`（C3-a・C3-b・L0-s3 マージ済み）、上の「使う部品と公開 API」「Model の源」
- Produces: bd のレーン ID（以下 `<bd>`）、`done.md` の雛形、C3-b の API の照合結果

- [ ] **Step 1: 作業場所と前提の実物を確かめる（C3-b は名前まで照合する）**

Run:
```bash
pwd
git log -1 --format='%H %s' origin/feature/uitk
S=moorestech_client/Assets/Scripts
# C3-b（未マージのまま計画を書いた。名前の差はここで拾う）
grep -n "public ModeSwitch(\|OnOptionSelected\|SetOptionEnabled" $S/Client.Ui/Components/Controls/ModeSwitch/ModeSwitch.cs
grep -rn "enum ModeSwitchRole\|public ModeSwitchOption(" $S/Client.Ui/Components/Controls/ModeSwitch/
grep -n "GetViewportHeight\|GetContentHeight\|TryGetContentTop\|OnContentLayoutChanged" $S/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs
grep -rn "public static void MarkSource<TTarget>\|interface IUiDragPayload<in TTarget>\|public static void MarkTarget<TTarget>" $S/Client.Ui/Foundation/DragDrop/
grep -rln "class HoverTooltipHost\|class GameUiHoverTooltipInstaller" $S/Client.Ui/Foundation/Tooltip/
# C3-a・L0-s3・既存
grep -n "public SlotFrame(\|public PlacementTargetFace(\|public SlotGrid(\|public ItemSlot(" -r $S/Client.Ui/Components/Slots/ | head
grep -n "public UiIcon Block(\|public UiIcon Item(\|public UiText ItemName(" $S/Client.Ui/Content/GameContentPresentation.cs
grep -n "public BuildMenuCategoryScrollTracker(\|public void AttachViewport" $S/Client.Ui/Screens/BuildMenu/CategoryScroll/BuildMenuCategoryScrollTracker.cs
grep -n "AppliedSpacerHeight" $S/Client.Ui/Screens/BuildMenu/CategoryScroll/IBuildMenuScrollViewport.cs
grep -n "BuildMenuEntry(" $S/Client.Game/InGame/Tutorial/UIHighlight/AnchorIds.cs
grep -n "section: Screens" "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git merge-base --is-ancestor e26010aa0c origin/feature/uitk && echo i-fix1-in
ls $S/Client.Ui.State/BuildMenu $S/Client.Tests/Ui/State/BuildMenu $S/Client.Starter/Registration/GameUi 2>/dev/null
```
Expected: worktree の絶対パス。C3-b の 5 つの grep がそれぞれ当たる（`ModeSwitch(string testid, ModeSwitchOrientation orientation, ModeSwitchRole role, IReadOnlyList<ModeSwitchOption<TValue>> options)`・`OnOptionSelected`・`SetOptionEnabled`・`ModeSwitchOption(string testid, TValue value, UiText label)`・GameScrollView の口 4 つ・`MarkSource<TTarget>`・`IUiDragPayload<in TTarget>`・`MarkTarget<TTarget>`・`HoverTooltipHost.cs`）。C3-a・L0-s3 の行が出る。`section: Screens` と `i-fix1-in` が出る。C3-b の grep が 1 つでも 0 件なら「C3-b 未取り込み」として `done.md` の 1 行目を「状態: 停止（C3-b 未取り込み）」にして止め、指揮へ報告する。名前だけ違うものは以後の計画を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <旧> → <実物>」と書く。最後の `ls` で `Client.Tests/Ui/State/BuildMenu` の既存ファイル数を数え、この計画の 4 本（テスト 3・補助 1）を足して 10 を超えるなら、足す 4 本を `Client.Tests/Ui/State/BuildMenu/Screen/`（namespace `Client.Tests.Ui.State.BuildMenu.Screen`）へ置く

- [ ] **Step 2: 使う文言が辞書と生成キーにあることを確かめる**

Run:
```bash
grep -n "^ui.buildMenu\.\(title\|detailHint\|noResults\|searchPlaceholder\|requiredItems\|requiredItemsPerSet\|remainingPlacementCount\|materialShortageTitle\|materialShortageLine\|materialTooltip\)," Localization/localization.csv | cut -c1-80
grep -n "^ui.common.close,\|^ui.commandRejected\." Localization/localization.csv | cut -c1-60
grep -rn "BuildMenuCategoryName\|BuildMenuSubCategoryName" moorestech_client/Assets/Scripts/Client.Game/Localization/MasterSourceTextCollector.cs | head -2
```
Expected: `ui.buildMenu.*` の 10 行、`ui.common.close`、既存の `ui.commandRejected.*` 6 行（この末尾へ Task 4 で 6 行足す）、`ContentLocalizationKeys.BuildMenuCategoryName`・`BuildMenuSubCategoryName` の使用例。生成キーは `LocalizationKeys.Ui.BuildMenu.Title` 等（namespace `Mooresmaster.Localization.Generated`）。生成名が違えば実物で読み替える

- [ ] **Step 3: bd を積み、done.md の雛形を置く**

Run:
```bash
bd create "UITK S3-buildmenu: ビルドメニューを UITK へ" --parent moorestech-albof -t task -p 2
bd update <bd> --claim
mkdir -p ../moorestech_logs/harness/uitk/S3-buildmenu
```
`done.md` に次を書く（1 行目は終わるまで「状態: 作業中」）:
```markdown
状態: 作業中

# S3-buildmenu 作業報告

## 証拠
## 差の表
## 要裁定
## integrator への依頼
- 作った・持ち主は S3-buildmenu: 落としの受け口 `S/Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver.cs`（`void Receive(HotbarDragSource source)`）。`BuildMenuEntryDragPayload` は `IUiDragPayload<IHotbarDropReceiver>` で、`OnDropped(IHotbarDropReceiver receiver)` は `receiver.Receive(Source)` を呼ぶだけ（断った理由のログは受け手）。別の型の落とし先は Foundation が飛ばして `OnDroppedOutside` になり、枠の外と同じく何もしない。S3-hotbar-modal は枠の印で `IHotbarDropReceiver` を実装し `UiDragDrop.MarkTarget<IHotbarDropReceiver>` で付けるだけ（D-16）
- 作った・持ち主は S3-buildmenu: 配置対象の絵の出どころ `S/Client.Ui.State/PlacementTarget/PlacementTargetIcon.cs`・`PlacementTargetIconResolver.cs`・`S/Client.Ui/Content/PlacementTargetIcons.cs`。S3-hotbar-modal は使うだけ（D-29）
- 設計書: A-100〜A-105 は計画の段階で `component-architecture.md` に反映済み（依頼なし）。実装で設計書と違う形が要ると分かったときだけ、ここへ案を書く
## 未確認
- 実行計画の L30 と Web の実物の L31（logic 27〈buildMenuGrouping 15・buildMenuScrollSpy 12〉＋sessionState 4）の差。L0-s3 が写し済みなので、このレーンでは数え直しの記録だけ
```
Expected: bd の ID が出る。以後 `<bd>` をその ID で読む

---

### Task 2: ViewModel と表示リストの合成（`Client.Ui.State/BuildMenu/Entries`）

**Files:**
- Create: `S/Client.Ui.State/PlacementTarget/PlacementTargetIcon.cs`
- Create: `S/Client.Ui.State/BuildMenu/Entries/BuildMenuSetPlacement.cs`・`BuildMenuMaterialRow.cs`・`BuildMenuEntryViewModel.cs`・`BuildMenuViewModel.cs`・`BuildMenuEntryKindName.cs`・`BuildMenuShownList.cs`・`BuildMenuShownListComposer.cs`
- Test: `S/Client.Tests/Ui/State/BuildMenu/BuildMenuShownListComposerTest.cs`

**Interfaces:**
- Consumes: L0-s3 の `BuildMenuCatalogEntry`・`BuildMenuRequiredItem`・`BuildMenuCategory`・`BuildMenuGrouping`
- Produces:
  - `namespace Client.Ui.State.BuildMenu.Entries { public sealed class BuildMenuViewModel { public static readonly BuildMenuViewModel Empty; public BuildMenuViewModel(IReadOnlyList<BuildMenuCategory> categories, IReadOnlyDictionary<Guid, UiText> headingNames, IReadOnlyList<BuildMenuEntryViewModel> entries); public IReadOnlyList<BuildMenuCategory> Categories { get; } public IReadOnlyList<BuildMenuEntryViewModel> Entries { get; } public IReadOnlyList<BuildMenuCatalogEntry> CatalogEntries { get; } public UiText HeadingNameOf(Guid categoryOrSubCategoryGuid); public bool TryGetEntry(Guid targetId, out BuildMenuEntryViewModel entry); } }`（`headingNames` はカテゴリとサブカテゴリの Guid → 名前。Model はコンテンツ辞書キー、L1 は逐語の名前を入れる。D-2`
  - `public sealed class BuildMenuEntryViewModel { public BuildMenuEntryViewModel(Guid targetId, PlacementTargetKind kind, Guid categoryGuid, Guid subCategoryGuid, string displayLabel, IReadOnlyList<BuildMenuMaterialRow> materials, bool paymentWaived, PlacementTargetIcon icon, BuildMenuSetPlacement setPlacement); public BuildMenuCatalogEntry Catalog { get; } public string KindName { get; } public bool CanDeleteBlueprint { get; } public IReadOnlyList<BuildMenuMaterialLine> Materials { get; } public PlacementTargetIcon Icon { get; } public BuildMenuSetPlacement SetPlacement { get; } }`
  - `public sealed class BuildMenuMaterialRow { public BuildMenuMaterialRow(ItemId itemId, BuildMenuRequiredItem item); public ItemId ItemId { get; } public BuildMenuRequiredItem Item { get; } }`
  - `namespace Client.Ui.State.PlacementTarget { public abstract class PlacementTargetIcon }`・`PlacementTargetBlockIcon(BlockId)`・`PlacementTargetTrainCarIcon(Guid)`・`PlacementTargetConnectToolIcon(Guid)`・`PlacementTargetBlueprintIcon(Guid)`・`PlacementTargetNameOnlyIcon.Instance`（ID だけ・Blueprint 以外は値の等価。持ち主はこのレーン、S3-hotbar-modal は使うだけ）
  - `public abstract class BuildMenuSetPlacement`・`public sealed class BuildMenuSetPlacementNone : BuildMenuSetPlacement { public static readonly BuildMenuSetPlacementNone Instance; }`・`public sealed class BuildMenuSetPlacementAvailable : BuildMenuSetPlacement { public BuildMenuSetPlacementAvailable(int perCost, int remaining); public int PerCost { get; } public int Remaining { get; } }`（判別 union。Factory だけが Block のとき作る）
  - `public static class BuildMenuEntryKindName { public static string Of(PlacementTargetKind kind); }`
  - `public sealed class BuildMenuShownList { public IReadOnlyList<BuildMenuCategoryGroup> Groups { get; } public IReadOnlyList<Guid> SidebarCategories { get; } public bool IsCategoryEnabled(Guid categoryGuid); public bool ShowsNoResults { get; } public IReadOnlyList<Guid> ShownCategoryGuids { get; } }`
  - `public static class BuildMenuShownListComposer { public static BuildMenuShownList Compose(BuildMenuViewModel viewModel, string query); }`

- [ ] **Step 1: 失敗するテストを書く**

`S/Client.Tests/Ui/State/BuildMenu/BuildMenuShownListComposerTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Tests.Ui.Content;
using Client.Ui.Content;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Text;
using Core.Master;
using Game.PlacementTarget;
using UnityEngine;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BuildMenu
{
    public class BuildMenuShownListComposerTest
    {
        private static readonly Guid Logistics = Guid.Parse("10000000-0000-4000-8000-000000000001");
        private static readonly Guid Transport = Guid.Parse("10000000-0000-4000-8000-000000000002");
        private static readonly Guid Empty = Guid.Parse("10000000-0000-4000-8000-000000000003");
        private static readonly Guid Sub = Guid.Parse("20000000-0000-4000-8000-000000000001");

        [Test]
        public void 空の検索語では全群を出しサイドバーは全部有効()
        {
            var shown = BuildMenuShownListComposer.Compose(CreateViewModel(), "");

            CollectionAssert.AreEqual(new[] { Logistics, Transport }, shown.ShownCategoryGuids);
            CollectionAssert.AreEqual(new[] { Logistics, Transport }, shown.SidebarCategories);
            Assert.IsTrue(shown.IsCategoryEnabled(Transport));
            Assert.IsFalse(shown.ShowsNoResults);
        }

        [Test]
        public void 検索は同じリストを絞りヒットの無いカテゴリだけ無効にする()
        {
            var shown = BuildMenuShownListComposer.Compose(CreateViewModel(), "chest");

            CollectionAssert.AreEqual(new[] { Logistics }, shown.ShownCategoryGuids);
            CollectionAssert.AreEqual(new[] { Logistics, Transport }, shown.SidebarCategories);
            Assert.IsTrue(shown.IsCategoryEnabled(Logistics));
            Assert.IsFalse(shown.IsCategoryEnabled(Transport));
        }

        [Test]
        public void 検索0件は該当なしを出す()
        {
            var shown = BuildMenuShownListComposer.Compose(CreateViewModel(), "存在しないブロック");

            Assert.AreEqual(0, shown.Groups.Count);
            Assert.IsTrue(shown.ShowsNoResults);
        }

        [Test]
        public void エントリの無いカテゴリはサイドバーに出ない()
        {
            var shown = BuildMenuShownListComposer.Compose(CreateViewModel(), "");

            CollectionAssert.DoesNotContain(shown.SidebarCategories, Empty);
        }

        private static BuildMenuViewModel CreateViewModel()
        {
            var categories = new[] { Logistics, Transport, Empty }.Select(guid => new BuildMenuCategory(guid, new[] { Sub })).ToList();
            var entries = new List<BuildMenuEntryViewModel>
            {
                Entry(Logistics, "Wood Chest"),
                Entry(Transport, "Rail"),
            };
            var names = new[] { Logistics, Transport, Empty, Sub }.ToDictionary(guid => guid, guid => UiText.FromVerbatim(guid.ToString()));
            return new BuildMenuViewModel(categories, names, entries);
        }

        private static BuildMenuEntryViewModel Entry(Guid category, string label)
        {
            return new BuildMenuEntryViewModel(Guid.NewGuid(), PlacementTargetKind.Block, category, Sub, label, Array.Empty<BuildMenuMaterialRow>(), false, new PlacementTargetBlockIcon(new BlockId(1)), BuildMenuSetPlacementNone.Instance);
        }
    }
}
```

- [ ] **Step 2: テストが落ちることを確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildMenuShownListComposer` 等が無いコンパイルエラー（CS0246）

- [ ] **Step 3: 型を書く**

`S/Client.Ui.State/PlacementTarget/PlacementTargetIcon.cs`（配置対象の絵の出どころ。ビルドメニューとホットバーが使う最終の置き場。持ち主はこのレーン。State は Texture2D とアイコンの容器を持たず ID だけを持つ。判別 union を 1 ファイルに置く。前例 `HotbarDropAction.cs`）:
```csharp
using System;
using Core.Master;

namespace Client.Ui.State.PlacementTarget
{
    /// <summary>
    /// 配置対象の絵の出どころ（ID だけ）。絵は Client.Ui の PlacementTargetIcons が引く
    /// Where a placement target's picture comes from (IDs only); Client.Ui PlacementTargetIcons looks the picture up
    /// </summary>
    public abstract class PlacementTargetIcon
    {
        private protected PlacementTargetIcon()
        {
        }
    }

    public sealed class PlacementTargetBlockIcon : PlacementTargetIcon
    {
        public BlockId BlockId { get; }

        public PlacementTargetBlockIcon(BlockId blockId)
        {
            BlockId = blockId;
        }

        public override bool Equals(object obj) => obj is PlacementTargetBlockIcon other && other.BlockId == BlockId;
        public override int GetHashCode() => BlockId.GetHashCode();
    }

    public sealed class PlacementTargetTrainCarIcon : PlacementTargetIcon
    {
        public Guid TrainCarGuid { get; }

        public PlacementTargetTrainCarIcon(Guid trainCarGuid)
        {
            TrainCarGuid = trainCarGuid;
        }

        public override bool Equals(object obj) => obj is PlacementTargetTrainCarIcon other && other.TrainCarGuid == TrainCarGuid;
        public override int GetHashCode() => TrainCarGuid.GetHashCode();
    }

    public sealed class PlacementTargetConnectToolIcon : PlacementTargetIcon
    {
        public Guid ConnectToolGuid { get; }

        public PlacementTargetConnectToolIcon(Guid connectToolGuid)
        {
            ConnectToolGuid = connectToolGuid;
        }

        public override bool Equals(object obj) => obj is PlacementTargetConnectToolIcon other && other.ConnectToolGuid == ConnectToolGuid;
        public override int GetHashCode() => ConnectToolGuid.GetHashCode();
    }

    // 撮影済みの BP だけ（未撮影は NameOnly）。値の等価を持たない: Guid が同じでも撮り直しで絵が変わるので、組み直した VM を別物として配る
    // Captured blueprints only (uncaptured ones are NameOnly); no value equality: a retake changes the picture under the same Guid, so a rebuilt VM is delivered as new
    public sealed class PlacementTargetBlueprintIcon : PlacementTargetIcon
    {
        public Guid BlueprintGuid { get; }

        public PlacementTargetBlueprintIcon(Guid blueprintGuid)
        {
            BlueprintGuid = blueprintGuid;
        }
    }

    // 絵が無く名前で描く（Web の iconUrl = null。未撮影の BP・BP コピー）。1 つしか無いので参照の等価が値の等価
    // No picture, drawn by name (Web iconUrl = null); a singleton, so reference equality is value equality
    public sealed class PlacementTargetNameOnlyIcon : PlacementTargetIcon
    {
        public static readonly PlacementTargetNameOnlyIcon Instance = new();

        private PlacementTargetNameOnlyIcon()
        {
        }
    }
}
```

`BuildMenuSetPlacement.cs`（判別 union。Factory だけが Block のとき `Available` を作る）:
```csharp
namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// 1 コストで複数個置けるブロックの 1 セット設置。無いことは BuildMenuSetPlacementNone.Instance で表し、使う側は switch で分ける
    /// Set placement for blocks placed several per cost; absence is BuildMenuSetPlacementNone.Instance and callers branch with switch
    /// </summary>
    public abstract class BuildMenuSetPlacement
    {
        private protected BuildMenuSetPlacement()
        {
        }
    }

    public sealed class BuildMenuSetPlacementNone : BuildMenuSetPlacement
    {
        public static readonly BuildMenuSetPlacementNone Instance = new();

        private BuildMenuSetPlacementNone()
        {
        }
    }

    public sealed class BuildMenuSetPlacementAvailable : BuildMenuSetPlacement
    {
        public int PerCost { get; }
        public int Remaining { get; }

        public BuildMenuSetPlacementAvailable(int perCost, int remaining)
        {
            PerCost = perCost;
            Remaining = remaining;
        }
    }
}
```

`BuildMenuMaterialRow.cs`:
```csharp
using Client.Ui.State.BuildMenu.Logic;
using Core.Master;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// 必要素材 1 行。絵と名前を引く ItemId と、不足判定の BuildMenuRequiredItem を対で持つ
    /// One required material: the ItemId for picture and name, paired with the BuildMenuRequiredItem for shortage checks
    /// </summary>
    public sealed class BuildMenuMaterialRow
    {
        public ItemId ItemId { get; }
        public BuildMenuRequiredItem Item { get; }

        public BuildMenuMaterialRow(ItemId itemId, BuildMenuRequiredItem item)
        {
            ItemId = itemId;
            Item = item;
        }
    }

    // VM が持つ素材の行。不足は VM の ctor で 1 回だけ判定し、ツールチップと詳細はこの値を読む
    // A material line held by the VM; shortage is judged once in the VM ctor and the tooltip and detail read this value
    public sealed class BuildMenuMaterialLine
    {
        public ItemId ItemId { get; }
        public BuildMenuRequiredItem Item { get; }
        public bool IsInsufficient { get; }

        public BuildMenuMaterialLine(BuildMenuMaterialRow row, bool isInsufficient)
        {
            ItemId = row.ItemId;
            Item = row.Item;
            IsInsufficient = isInsufficient;
        }
    }
}
```

`BuildMenuEntryViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.PlacementTarget;
using Game.PlacementTarget;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// ビルドメニューのエントリ 1 件。群分け・検索・不足判定は Catalog（L0-s3 の型）で行う
    /// One build-menu entry; grouping, search and shortage run on Catalog (the L0-s3 type)
    /// </summary>
    public sealed class BuildMenuEntryViewModel
    {
        public BuildMenuCatalogEntry Catalog { get; }
        public string KindName { get; }
        public bool CanDeleteBlueprint { get; }
        public IReadOnlyList<BuildMenuMaterialLine> Materials { get; }
        public PlacementTargetIcon Icon { get; }
        public BuildMenuSetPlacement SetPlacement { get; }

        public BuildMenuEntryViewModel(Guid targetId, PlacementTargetKind kind, Guid categoryGuid, Guid subCategoryGuid, string displayLabel, IReadOnlyList<BuildMenuMaterialRow> materials, bool paymentWaived, PlacementTargetIcon icon, BuildMenuSetPlacement setPlacement)
        {
            // 必要素材は 1 本の列から Catalog へ写し、二重に持たない
            // Required items are copied from a single list into Catalog, never held twice
            Catalog = new BuildMenuCatalogEntry(targetId, categoryGuid, subCategoryGuid, displayLabel, materials.Select(row => row.Item).ToList(), paymentWaived);
            // Screens が Game.PlacementTarget を見ずに済むよう、testid の種別名と BP 削除の可否をここで決める
            // Decide the testid kind name and blueprint deletability here so Screens never look at Game.PlacementTarget
            KindName = BuildMenuEntryKindName.Of(kind);
            CanDeleteBlueprint = kind == PlacementTargetKind.Blueprint;
            Materials = materials.Select(row => new BuildMenuMaterialLine(row, BuildMenuShortage.IsItemInsufficient(Catalog, row.Item))).ToList();
            Icon = icon;
            SetPlacement = setPlacement;
        }
    }
}
```

`BuildMenuViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.Text;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// ビルドメニューの不変の ViewModel（マスタ順のカテゴリ・見出しの名前・解放済みのエントリ）
    /// Immutable build-menu view model: categories in master order, heading names and the unlocked entries
    /// </summary>
    public sealed class BuildMenuViewModel
    {
        public static readonly BuildMenuViewModel Empty = new(Array.Empty<BuildMenuCategory>(), new Dictionary<Guid, UiText>(), Array.Empty<BuildMenuEntryViewModel>());

        public IReadOnlyList<BuildMenuCategory> Categories { get; }
        public IReadOnlyList<BuildMenuEntryViewModel> Entries { get; }
        public IReadOnlyList<BuildMenuCatalogEntry> CatalogEntries { get; }

        private readonly IReadOnlyDictionary<Guid, UiText> _headingNames;
        private readonly Dictionary<Guid, BuildMenuEntryViewModel> _entriesById;

        public BuildMenuViewModel(IReadOnlyList<BuildMenuCategory> categories, IReadOnlyDictionary<Guid, UiText> headingNames, IReadOnlyList<BuildMenuEntryViewModel> entries)
        {
            Categories = categories;
            Entries = entries;
            CatalogEntries = entries.Select(entry => entry.Catalog).ToList();
            _headingNames = headingNames;
            _entriesById = entries.ToDictionary(entry => entry.Catalog.TargetId);
        }

        // カテゴリとサブカテゴリの見出しの名前。無い Guid は組み立ての誤りなので例外で止める
        // Heading name of a category or sub-category; a missing Guid is a construction bug, so it throws
        public UiText HeadingNameOf(Guid categoryOrSubCategoryGuid)
        {
            return _headingNames[categoryOrSubCategoryGuid];
        }

        public bool TryGetEntry(Guid targetId, out BuildMenuEntryViewModel entry)
        {
            return _entriesById.TryGetValue(targetId, out entry);
        }
    }
}
```

`BuildMenuEntryKindName.cs`:
```csharp
using System;
using Game.PlacementTarget;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// 種別の Web の綴り（testid・チュートリアルの錨・プレイテスト DSL が同じ文字列を使う）
    /// The Web spelling of a kind (testids, tutorial anchors and the playtest DSL share these strings)
    /// </summary>
    public static class BuildMenuEntryKindName
    {
        // Web の橋 BuildMenuEntryDtoFactory.ResolveKind と同じ値（BuildMenuEntryFactoryTest が突き合わせる）
        // Same values as the Web bridge BuildMenuEntryDtoFactory.ResolveKind (BuildMenuEntryFactoryTest cross-checks)
        public static string Of(PlacementTargetKind kind)
        {
            return kind switch
            {
                PlacementTargetKind.Block => "block",
                PlacementTargetKind.TrainCar => "trainCar",
                PlacementTargetKind.ConnectTool => "connectTool",
                PlacementTargetKind.BlueprintCopy => "blueprintCopy",
                PlacementTargetKind.Blueprint => "blueprint",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }
    }
}
```

`BuildMenuShownList.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Logic;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// 今出すもの（検索を当てた群・サイドバーの項目と有効・該当なし）
    /// What to show now: searched groups, sidebar items with their enabled state, and the no-results flag
    /// </summary>
    public sealed class BuildMenuShownList
    {
        public IReadOnlyList<BuildMenuCategoryGroup> Groups { get; }
        public IReadOnlyList<Guid> SidebarCategories { get; }
        public IReadOnlyList<Guid> ShownCategoryGuids { get; }
        public bool ShowsNoResults { get; }

        private readonly HashSet<Guid> _shown;

        public BuildMenuShownList(IReadOnlyList<BuildMenuCategoryGroup> groups, IReadOnlyList<Guid> sidebarCategories, bool showsNoResults)
        {
            Groups = groups;
            SidebarCategories = sidebarCategories;
            ShownCategoryGuids = groups.Select(group => group.CategoryGuid).ToList();
            ShowsNoResults = showsNoResults;
            _shown = new HashSet<Guid>(ShownCategoryGuids);
        }

        public bool IsCategoryEnabled(Guid categoryGuid)
        {
            return _shown.Contains(categoryGuid);
        }
    }
}
```

`BuildMenuShownListComposer.cs`:
```csharp
using System.Linq;
using Client.Ui.State.BuildMenu.Logic;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// Web BuildMenuPanel の allGroups・shownGroups・sidebarItems・noHit の写し
    /// Port of allGroups, shownGroups, sidebarItems and noHit in the Web BuildMenuPanel
    /// </summary>
    public static class BuildMenuShownListComposer
    {
        public static BuildMenuShownList Compose(BuildMenuViewModel viewModel, string query)
        {
            // サイドバーは全エントリの群、本文は検索を当てた群
            // The sidebar lists groups of all entries; the body lists the searched groups
            var allGroups = BuildMenuGrouping.Group(viewModel.Categories, viewModel.CatalogEntries);
            var searching = query.Length > 0;
            var shownGroups = searching ? BuildMenuGrouping.Group(viewModel.Categories, BuildMenuGrouping.Search(query, viewModel.CatalogEntries)) : allGroups;
            var sidebar = allGroups.Select(group => group.CategoryGuid).ToList();
            return new BuildMenuShownList(shownGroups, sidebar, searching && shownGroups.Count == 0);
        }
    }
}
```

- [ ] **Step 4: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.BuildMenu\."`
Expected: `ErrorCount: 0`、L0-s3 の既存分を含め全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/BuildMenu/Entries moorestech_client/Assets/Scripts/Client.Ui.State/PlacementTarget moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BuildMenu
git commit -m "feat(uitk): ビルドメニューの ViewModel と表示リストの合成を足す（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 3: Model（`Client.Ui.State/BuildMenu/Source`）

**Files:**
- Create: `S/Client.Ui.State/PlacementTarget/PlacementTargetIconResolver.cs`
- Create: `S/Client.Ui.State/BuildMenu/Source/BuildMenuCategoryPairResolver.cs`・`BuildMenuMaterialRowsBuilder.cs`・`BuildMenuEntryFactory.cs`・`BuildMenuModel.cs`
- Modify: `S/Client.Ui.State/Client.Ui.State.asmdef`（`references` に `Common.Debug`・`Game.Construction`・`Client.Mod`。CS0246/CS0012 が出たものだけ）
- Test: `S/Client.Tests/Ui/State/BuildMenu/BuildMenuEntryFactoryTest.cs`・`AllPlacementTargetsUnlocked.cs`

**Interfaces:**
- Consumes: 「Model の源」、Task 2 の型、前例 `S/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs`（テスト mod の立て方・`BlueprintLookupStub`・`BeltConveyorPlacementUnlockSourceMap`）
- Produces:
  - `namespace Client.Ui.State.BuildMenu.Source { public static class BuildMenuEntryFactory { public static IReadOnlyList<BuildMenuEntryViewModel> CreateEntries(IReadOnlyList<IPlacementTarget> targets, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems, IBlueprintThumbnailLookup thumbnails); public static IReadOnlyList<BuildMenuCategory> CreateCategories(); public static IReadOnlyDictionary<Guid, UiText> CreateHeadingNames(); } }`
  - `namespace Client.Ui.State.PlacementTarget { public static class PlacementTargetIconResolver { public static PlacementTargetIcon Resolve(IPlacementTarget target, IBlueprintThumbnailLookup thumbnails); } }`（BP の撮影済みかだけを thumbnails で見る）
  - `public sealed class BuildMenuModel : IInitializable, IDisposable { public BuildMenuModel(UIStateControl uiStateControl, PlacementTargetResolver placementTargetResolver, ConstructionWalletQuery walletQuery, LocalPlayerInventoryController inventoryController, ClientBlueprintLibrary blueprintLibrary, IBlueprintThumbnailLookup thumbnails); public IReadOnlyReactiveProperty<BuildMenuViewModel> Current { get; } }`

- [ ] **Step 1: 失敗するテストを書く（Web の橋と同じ値を出すことを突き合わせる）**

`AllPlacementTargetsUnlocked.cs` は前例 `BuildMenuEntryDtoFactoryTest` の private な `AllPlacementTargetsUnlockedStateData` と同じ中身の `internal sealed class AllPlacementTargetsUnlocked : IGameUnlockStateData`（前例の 8 プロパティをそのまま写す。前例のファイルは変えない）。

`BuildMenuEntryFactoryTest.cs`:
```csharp
using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Construction;
using Client.Tests.PlaceSystem.Blueprint;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Source;
using Client.Ui.State.PlacementTarget;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Core.Item.Interface;
using Core.Master;
using Game.Blueprint;
using Game.Construction;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BuildMenu
{
    public class BuildMenuEntryFactoryTest
    {
        [Test]
        public void 全解放の対象でWebの橋と同じ種別とカテゴリ対と素材と免除と1セット設置を出す()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blueprint = new BlueprintJsonObject("starter-base", new(), new(), new(), Guid.Parse("70000000-0000-4000-8000-000000000001"));
            var targets = new PlacementTargetResolver(new PlacementTargetCatalog(new BeltConveyorPlacementUnlockSourceMap()), new BlueprintLookupStub(blueprint), new AllPlacementTargetsUnlocked()).CreateUnlockedTargets();
            var wallet = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var thumbnails = new BlueprintThumbnailContainer();

            var entries = BuildMenuEntryFactory.CreateEntries(targets, wallet, Array.Empty<IItemStack>(), thumbnails);
            var dtos = BuildMenuEntryDtoFactory.CreateDtos(targets, wallet, Array.Empty<IItemStack>(), thumbnails);

            // 同じ順に 1 対 1 で並び、どの欄も橋の DTO と一致する
            // Same order one-to-one, and every field matches the bridge DTO
            Assert.Greater(entries.Count, 0);
            Assert.AreEqual(dtos.Count, entries.Count);
            for (var i = 0; i < dtos.Count; i++)
            {
                var entry = entries[i];
                var dto = dtos[i];
                Assert.AreEqual(dto.Id, entry.Catalog.TargetId.ToString("D"));
                Assert.AreEqual(dto.Kind, entry.KindName);
                Assert.AreEqual(dto.CategoryGuid, entry.Catalog.CategoryGuid.ToString("D"));
                Assert.AreEqual(dto.SubCategoryGuid, entry.Catalog.SubCategoryGuid.ToString("D"));
                Assert.AreEqual(dto.PaymentWaived, entry.Catalog.PaymentWaived);
                Assert.AreEqual(dto.SetPlacement != null, entry.SetPlacement is BuildMenuSetPlacementAvailable, dto.Id);
                CollectionAssert.AreEqual(dto.RequiredItems.Select(item => (item.ItemId, item.Count, item.Held, item.Lacking)), entry.Materials.Select(row => (row.ItemId.AsPrimitive(), row.Item.Count, row.Item.Held, row.Item.Lacking)), dto.Id);
            }
        }

        [Test]
        public void 撮影済みBPだけBPの絵を指しBPコピーは名前で描く()
        {
            var guid = Guid.NewGuid();
            var target = new BlueprintPlacementTarget(guid, "test", new BlueprintJsonObject());
            var thumbnails = new BlueprintThumbnailContainer();

            Assert.AreSame(PlacementTargetNameOnlyIcon.Instance, PlacementTargetIconResolver.Resolve(target, thumbnails));
            thumbnails.Add(guid, Texture2D.whiteTexture);
            Assert.AreEqual(guid, ((PlacementTargetBlueprintIcon)PlacementTargetIconResolver.Resolve(target, thumbnails)).BlueprintGuid);
            Assert.AreSame(PlacementTargetNameOnlyIcon.Instance, PlacementTargetIconResolver.Resolve(new BlueprintCopyPlacementTarget(Guid.NewGuid()), thumbnails));
        }

        [Test]
        public void ブロックはブロックの絵を引く()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;

            var icon = PlacementTargetIconResolver.Resolve(new BlockPlacementTarget(blockGuid, null), new BlueprintThumbnailContainer());

            Assert.AreEqual(ForUnitTestModBlockId.BlockId, ((PlacementTargetBlockIcon)icon).BlockId);
        }

        [Test]
        public void 絵の出どころは値で比べる()
        {
            // リストの差し替え判定（D-13）とホットバーの面の比較が作り直しのたびに別物と見なさないよう、値で等しい
            // Equal by value so the list's swap check (D-13) and the hotbar face comparison do not treat each rebuild as new
            Assert.AreEqual(new PlacementTargetBlockIcon(ForUnitTestModBlockId.BlockId), new PlacementTargetBlockIcon(ForUnitTestModBlockId.BlockId));
            var carGuid = Guid.NewGuid();
            Assert.AreEqual(new PlacementTargetTrainCarIcon(carGuid), new PlacementTargetTrainCarIcon(carGuid));
            Assert.AreNotEqual(new PlacementTargetTrainCarIcon(carGuid), new PlacementTargetConnectToolIcon(carGuid));
            Assert.AreNotEqual(PlacementTargetNameOnlyIcon.Instance, new PlacementTargetBlockIcon(ForUnitTestModBlockId.BlockId));
        }

        [Test]
        public void カテゴリはマスタ定義順のGuidを保つ()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var categories = BuildMenuEntryFactory.CreateCategories();

            CollectionAssert.AreEqual(MasterHolder.BuildMenuCategoryMaster.Categories.Select(c => c.CategoryGuid), categories.Select(c => c.CategoryGuid));
            CollectionAssert.AreEqual(MasterHolder.BuildMenuCategoryMaster.Categories[0].SubCategories.Select(s => s.SubCategoryGuid), categories[0].SubCategoryGuids);
        }
    }
}
```
（`dto.RequiredItems[i].ItemId` の型は橋の DTO の実物（`int`）に合わせ、`AsPrimitive()` の戻りと比べる。`BlockPlacementTarget`・`BlueprintCopyPlacementTarget` の ctor は S2-mode-tooltip 計画 Global Constraints の記載と同じ）

- [ ] **Step 2: テストが落ちることを確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildMenuEntryFactory` 等が無いコンパイルエラー

- [ ] **Step 3: カテゴリ対・アイコン・素材を書く**

`BuildMenuCategoryPairResolver.cs`:
```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.PlacementTarget;
using Mooresmaster.Model.BuildMenuModule;

namespace Client.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// 配置対象 → カテゴリ対（Web の橋 BuildMenuEntryDtoFactory.ResolveCategoryPair の写し）
    /// Placement target to category pair (port of the Web bridge BuildMenuEntryDtoFactory.ResolveCategoryPair)
    /// </summary>
    public static class BuildMenuCategoryPairResolver
    {
        public static (Guid categoryGuid, Guid subCategoryGuid) Resolve(IPlacementTarget target)
        {
            var categoryMaster = MasterHolder.BuildMenuCategoryMaster;

            // ブロックはマスタのカテゴリ名、それ以外は入り口の種別でサブカテゴリを引く
            // Blocks use the master category names; other kinds look up the sub-category by entry source
            if (target is BlockPlacementTarget block)
            {
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
                return categoryMaster.GetGuidPair(blockMaster.Category, blockMaster.SubCategory);
            }
            return categoryMaster.GetPairByEntrySource(target.Kind switch
            {
                PlacementTargetKind.TrainCar => BuildMenuSubCategoryElement.EntrySourceConst.trainCars,
                PlacementTargetKind.ConnectTool => BuildMenuSubCategoryElement.EntrySourceConst.connectTools,
                PlacementTargetKind.BlueprintCopy => BuildMenuSubCategoryElement.EntrySourceConst.blueprintCopyTool,
                PlacementTargetKind.Blueprint => BuildMenuSubCategoryElement.EntrySourceConst.savedBlueprints,
                _ => throw new ArgumentOutOfRangeException(nameof(target.Kind), target.Kind, null),
            });
        }
    }
}
```

`S/Client.Ui.State/PlacementTarget/PlacementTargetIconResolver.cs`（最終の置き場。ホットバーも使う）:
```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Ui.State.PlacementTarget
{
    /// <summary>
    /// 配置対象 → 絵の出どころの ID（Web の橋 ResolveIconUrl が指す絵と同じもの）
    /// Placement target to the picture-source ID (the same picture the Web bridge ResolveIconUrl points at)
    /// </summary>
    public static class PlacementTargetIconResolver
    {
        public static PlacementTargetIcon Resolve(IPlacementTarget target, IBlueprintThumbnailLookup thumbnails)
        {
            return target switch
            {
                BlockPlacementTarget block => new PlacementTargetBlockIcon(block.BlockId),
                TrainCarPlacementTarget trainCar => new PlacementTargetTrainCarIcon(trainCar.TrainCarGuid),
                ConnectToolPlacementTarget connectTool => new PlacementTargetConnectToolIcon(connectTool.ConnectToolGuid),
                // 撮影済みかの判定だけに thumbnails を使う。未撮影は名前で描く（Web の iconUrl = null）
                // thumbnails only answers whether it is captured; an uncaptured blueprint is drawn by name (Web iconUrl = null)
                BlueprintPlacementTarget blueprint => thumbnails.Contains(blueprint.BlueprintGuid) ? new PlacementTargetBlueprintIcon(blueprint.BlueprintGuid) : PlacementTargetNameOnlyIcon.Instance,
                BlueprintCopyPlacementTarget => PlacementTargetNameOnlyIcon.Instance,
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
            };
        }
    }
}
```

`BuildMenuMaterialRowsBuilder.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Core.Master;
using Game.Construction;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;

namespace Client.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// 必要素材の行（Web の橋 BuildMenuMaterialAvailability と CreateBlueprintRequiredItems の写し）
    /// Required-material rows (port of the Web bridge BuildMenuMaterialAvailability and CreateBlueprintRequiredItems)
    /// </summary>
    public static class BuildMenuMaterialRowsBuilder
    {
        public static IReadOnlyList<BuildMenuMaterialRow> ForTarget(IPlacementTarget target, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            return ToRows(ConstructionCostShortageCalculator.CalculateRequirements(target.CreateRequiredItems(), heldByItem));
        }

        // BP は貼り付けの費用。無料設置で払う物が 0 なら免除し、表示は払うときの費用を出す
        // Blueprints show the paste cost; with free placement and nothing payable it is waived, still listing the full cost
        public static (IReadOnlyList<BuildMenuMaterialRow> rows, bool paymentWaived) ForBlueprint(BlueprintPlacementTarget blueprint, ConstructionWalletQuery walletQuery, bool freeBlockPlacement, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            var drafts = new[] { BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint.Blueprint) };
            var payable = BlueprintPasteCostCalculator.CalcRequiredItems(drafts, walletQuery, freeBlockPlacement);
            var paymentWaived = freeBlockPlacement && payable.Count == 0;
            var required = paymentWaived ? BlueprintPasteCostCalculator.CalcRequiredItems(drafts, walletQuery, false) : payable;
            return (ToRows(ConstructionMaterialAccounting.MatchRequirements(required, heldByItem)), paymentWaived);
        }

        private static IReadOnlyList<BuildMenuMaterialRow> ToRows(IReadOnlyList<(ItemId itemId, int held, int required)> requirements)
        {
            var rows = new List<BuildMenuMaterialRow>();
            foreach (var (itemId, held, required) in requirements)
            {
                var item = new BuildMenuRequiredItem(MasterHolder.ItemMaster.GetItemGuid(itemId), required, held, held < required);
                rows.Add(new BuildMenuMaterialRow(itemId, item));
            }
            return rows;
        }
    }
}
```

- [ ] **Step 4: 組み立てと Model を書く**

`BuildMenuEntryFactory.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Text;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// 配置対象の列 → エントリの列（Web の橋 BuildMenuEntryDtoFactory.CreateDtos の写し）
    /// Placement targets to entries (port of the Web bridge BuildMenuEntryDtoFactory.CreateDtos)
    /// </summary>
    public static class BuildMenuEntryFactory
    {
        public static IReadOnlyList<BuildMenuEntryViewModel> CreateEntries(IReadOnlyList<IPlacementTarget> targets, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems, IBlueprintThumbnailLookup thumbnails)
        {
            var heldByItem = ConstructionMaterialAccounting.TallyHeld(inventoryItems);
            var freeBlockPlacement = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
            var entries = new List<BuildMenuEntryViewModel>();
            foreach (var target in targets) entries.Add(CreateEntry(target));
            return entries;

            #region Internal

            BuildMenuEntryViewModel CreateEntry(IPlacementTarget target)
            {
                // 財布はブロックだけが持つ。免除は無料設置・BP の免除・財布の残りのどれか
                // Only blocks have a wallet; waived by free placement, a waived blueprint, or wallet stock
                var (categoryGuid, subCategoryGuid) = BuildMenuCategoryPairResolver.Resolve(target);
                var block = target as BlockPlacementTarget;
                var walletStatus = block == null ? null : walletQuery.GetWalletStatus(block.BlockId);
                var (rows, blueprintWaived) = target is BlueprintPlacementTarget blueprint
                    ? BuildMenuMaterialRowsBuilder.ForBlueprint(blueprint, walletQuery, freeBlockPlacement, heldByItem)
                    : (BuildMenuMaterialRowsBuilder.ForTarget(target, heldByItem), false);
                var paymentWaived = (freeBlockPlacement && block != null) || blueprintWaived || (walletStatus?.CoversNextPlacement() ?? false);

                // 1 セット設置は Block の財布の状態からだけ作る
                // A set placement only comes from a block's wallet status
                BuildMenuSetPlacement setPlacement = walletStatus is { } status ? new BuildMenuSetPlacementAvailable(status.PlacementsPerCost, status.RemainingCount) : BuildMenuSetPlacementNone.Instance;
                var icon = PlacementTargetIconResolver.Resolve(target, thumbnails);

                // 表示名はマスタ由来で空にならない前提。空なら開発者へ知らせ、表示は Web と同じくそのまま（前例 ModeHudViewModelFactory）
                // Display names come from the master and should never be empty; tell developers and display as is, like Web (precedent ModeHudViewModelFactory)
                if (target.DisplayName.Length == 0) Debug.LogError($"[BuildMenu] empty DisplayName for placement target {target.Kind} {target.Id}");
                return new BuildMenuEntryViewModel(target.Id, target.Kind, categoryGuid, subCategoryGuid, target.DisplayName, rows, paymentWaived, icon, setPlacement);
            }

            #endregion
        }

        public static IReadOnlyList<BuildMenuCategory> CreateCategories()
        {
            return MasterHolder.BuildMenuCategoryMaster.Categories
                .Select(category => new BuildMenuCategory(category.CategoryGuid, category.SubCategories.Select(sub => sub.SubCategoryGuid).ToList()))
                .ToList();
        }

        // 見出しの名前はコンテンツ辞書キー（Web の buildMenuCategoryNameKey・buildMenuSubCategoryNameKey）
        // Heading names are content keys (the Web buildMenuCategoryNameKey and buildMenuSubCategoryNameKey)
        public static IReadOnlyDictionary<Guid, UiText> CreateHeadingNames()
        {
            var names = new Dictionary<Guid, UiText>();
            foreach (var category in MasterHolder.BuildMenuCategoryMaster.Categories)
            {
                names[category.CategoryGuid] = UiText.FromContent(ContentLocalizationKeys.BuildMenuCategoryName(category.CategoryGuid));
                foreach (var sub in category.SubCategories) names[sub.SubCategoryGuid] = UiText.FromContent(ContentLocalizationKeys.BuildMenuSubCategoryName(sub.SubCategoryGuid));
            }
            return names;
        }
    }
}
```
（表示名は Web の Block では辞書の解決名、BP では `target.DisplayName`。UITK は全種別で `IPlacementTarget.DisplayName`〈C# の表示名の正。S2-mode-tooltip D-8〉を使い、言語の変化で組み直す。実物の `DisplayName` はコンテンツ辞書の解決名〈`BlockPlacementTarget.cs:26` の `Localize.GetContent(ContentLocalizationKeys.BlockName(BlockGuid))`。車両・接続ツールも同じ形〉で、Web と同じ名前になる）

`BuildMenuModel.cs`:
```csharp
using System;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Ui.State.BuildMenu.Entries;
using Cysharp.Threading.Tasks;
using Game.Construction;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// ビルドメニューの Model。開いている間だけ源の変化で組み直して Current で配る
    /// Build-menu model; while the menu is open, rebuilds on source changes and publishes through Current
    /// </summary>
    public sealed class BuildMenuModel : IInitializable, IDisposable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly PlacementTargetResolver _placementTargetResolver;
        private readonly ConstructionWalletQuery _walletQuery;
        private readonly LocalPlayerInventoryController _inventoryController;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly IBlueprintThumbnailLookup _thumbnails;
        private readonly ReactiveProperty<BuildMenuViewModel> _current = new(BuildMenuViewModel.Empty);
        private readonly CompositeDisposable _subscriptions = new();
        private bool _rebuildScheduled;
        private bool _disposed;

        public IReadOnlyReactiveProperty<BuildMenuViewModel> Current => _current;

        public BuildMenuModel(UIStateControl uiStateControl, PlacementTargetResolver placementTargetResolver, ConstructionWalletQuery walletQuery, LocalPlayerInventoryController inventoryController, ClientBlueprintLibrary blueprintLibrary, IBlueprintThumbnailLookup thumbnails)
        {
            _uiStateControl = uiStateControl;
            _placementTargetResolver = placementTargetResolver;
            _walletQuery = walletQuery;
            _inventoryController = inventoryController;
            _blueprintLibrary = blueprintLibrary;
            _thumbnails = thumbnails;
        }

        public void Initialize()
        {
            // 開いたときは BP ライブラリを取り直してから組む（Web の橋 BuildMenuTopic.OnStateChanged と同じ）
            // On open, refresh the blueprint library and rebuild (same as the Web bridge BuildMenuTopic.OnStateChanged)
            _uiStateControl.OnStateChanged.Where(state => state == UIStateEnum.BuildMenu).Subscribe(_ =>
            {
                _blueprintLibrary.Refresh(CancellationToken.None).Forget();
                ScheduleRebuild();
            }).AddTo(_subscriptions);

            // 源の変化は開いている間だけ組み直す。同じフレームの多発はフレーム末の 1 回に畳む（橋 BuildMenuTopic.SchedulePublish と同じ。D-19）
            // Source changes rebuild only while open; bursts within a frame fold into one end-of-frame rebuild (same as the bridge's SchedulePublish; D-19)
            _blueprintLibrary.OnChanged.Subscribe(_ => RebuildWhileOpen()).AddTo(_subscriptions);
            _thumbnails.OnThumbnailChanged.Subscribe(_ => RebuildWhileOpen()).AddTo(_subscriptions);
            _walletQuery.OnWalletChanged.Subscribe(_ => RebuildWhileOpen()).AddTo(_subscriptions);
            _inventoryController.LocalPlayerInventory.OnItemChange.Subscribe(_ => RebuildWhileOpen()).AddTo(_subscriptions);
            _inventoryController.OnInventoryRefreshed.Subscribe(_ => RebuildWhileOpen()).AddTo(_subscriptions);
            Localize.OnLanguageChanged.Subscribe(_ => RebuildWhileOpen()).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _disposed = true;
            _subscriptions.Dispose();
        }

        private void RebuildWhileOpen()
        {
            // silent-ok: 閉じている間の変化は捨てる / 次に開いたとき Initialize の購読が組み直す
            // silent-ok: changes while closed are dropped / the next open rebuilds through the Initialize subscription
            if (_uiStateControl.CurrentState != UIStateEnum.BuildMenu) return;
            ScheduleRebuild();
        }

        private void ScheduleRebuild()
        {
            // silent-ok: 予約済みなら同じフレーム末の 1 回が最新の源で組む / 取りこぼしは無い
            // silent-ok: an already scheduled rebuild reads the latest sources at end of frame / nothing is lost
            if (_rebuildScheduled) return;
            _rebuildScheduled = true;
            RebuildAtEndOfFrame().Forget();

            #region Internal

            async UniTaskVoid RebuildAtEndOfFrame()
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                _rebuildScheduled = false;
                // silent-ok: 破棄後（ゲーム終了時だけ）は組まない / 誰も購読していない
                // silent-ok: after dispose (only at game exit) skip the rebuild / no one is subscribed
                if (_disposed) return;
                Rebuild();
            }

            #endregion
        }

        private void Rebuild()
        {
            var entries = BuildMenuEntryFactory.CreateEntries(_placementTargetResolver.CreateUnlockedTargets(), _walletQuery, _inventoryController.LocalPlayerInventory, _thumbnails);
            _current.Value = new BuildMenuViewModel(BuildMenuEntryFactory.CreateCategories(), BuildMenuEntryFactory.CreateHeadingNames(), entries);
        }
    }
}
```
（`LocalPlayerInventory` が `IEnumerable<IItemStack>` で `OnItemChange` を持つことは橋の実物と同じ。型が違えば橋の `BuildMenuTopic` の書き方に合わせる）

- [ ] **Step 5: asmdef の参照を足してテストを通す**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 初回は `Common.Debug`・`Game.Construction`・`Client.Mod`（`ItemViewData`）が見つからない CS0246/CS0012 が出る。`S/Client.Ui.State/Client.Ui.State.asmdef` の `references` の末尾に、出たものだけを足す（`Edit` で 1 行ずつ。並びは既存に合わせ末尾へ）。再度 compile して `ErrorCount: 0`。続けて `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(State\.BuildMenu|Conventions|Layers)\."` が全件 PASS（`UiLayerDependencyDirectionTest` は State を縛らないので asmdef の追加で赤くならない）

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/BuildMenu/Source moorestech_client/Assets/Scripts/Client.Ui.State/PlacementTarget moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BuildMenu
git commit -m "feat(uitk): ビルドメニューの Model を源の購読で組む（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 4: コマンド（選択・BP 削除・閉じる）と拒否理由

**Files:**
- Create: `S/Client.Ui.State/BuildMenu/Commands/IBuildMenuCommands.cs`・`BuildMenuCommands.cs`
- Modify: `S/Client.Ui.State/Commands/UiCommandRejectReason.cs`（enum の末尾に 6 値、`IsBenign`・`ToLocalizationKey`・`ToWireCode` の末尾に各 6 腕。前例 S2-skit）
- Modify: `Localization/localization.csv`（`ui.commandRejected.*` の末尾 6 行）
- Modify（生成物）: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`gen:i18n`）・`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`（印）
- Modify: `S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs`（良性の一覧を固定するテスト。良性を 3 値足すので一覧を直す）
- Test: `S/Client.Tests/Ui/State/BuildMenu/BuildMenuCommandsTest.cs`

**Interfaces:**
- Consumes: 橋 `S/Client.WebUiHost/Game/Actions/BuildMenuActions.cs`（失敗コード）、Web `W/src/bridge/transport/actions.ts` の `BENIGN_ERRORS`（良性の集合）、`UIStateControl`・`PlacementTargetResolver`・`BuildMenuSelection`・`IBlueprintDeleteService`
- Produces:
  - `namespace Client.Ui.State.BuildMenu.Commands { public interface IBuildMenuCommands { UiCommandResult SelectEntry(Guid targetId); UniTask<UiCommandResult> DeleteBlueprint(Guid blueprintGuid); UiCommandResult Close(); } }`
  - `public sealed class BuildMenuCommands : IBuildMenuCommands { public BuildMenuCommands(UIStateControl uiStateControl, PlacementTargetResolver placementTargetResolver, BuildMenuSelection buildMenuSelection, IBlueprintDeleteService blueprintDeleteService); }`
  - `UiCommandRejectReason` の追加 6 値: `BuildMenuNotOpen`（良性・`invalid_state`）・`UnknownBuildMenuEntry`（良性・`unknown_entry`）・`BlueprintDeleteNotFound`（良性・`blueprint_delete_not_found`）・`BlueprintDeleteNotUnlocked`（`blueprint_delete_not_unlocked`）・`BlueprintDeleteRequestFailed`（`blueprint_delete_request_failed`）・`BlueprintDeleteUnknown`（`blueprint_delete_unknown`）

- [ ] **Step 1: 失敗するテストを書く**

`BuildMenuCommandsTest.cs`（`UIStateControl` の状態は前例 `S/Client.Tests/WebUi/BuildMenu/BuildMenuActionsTest.cs` と同じく reflection で `CurrentState` を置く。`BlueprintDeleteServiceStub` は前例と同じ形をこのファイルの private class に書く。`BuildMenuCommands` の第 4 引数は `IBlueprintDeleteService` で受ける〈本番の DI は D-35 のとおり `MainGameInteractionRegistration` の `ClientBlueprintLibrary` の登録に `.As<IBlueprintDeleteService>()` を足して引く。Task 8〉）:
```csharp
using System;
using System.Linq;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BuildMenu
{
    public class BuildMenuCommandsTest
    {
        private GameObject _controlObject;
        private UIStateControl _control;
        private BuildMenuSelection _selection;
        private PlacementTargetResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _controlObject = new GameObject("BuildMenuCommandsTest.Control");
            _control = _controlObject.AddComponent<UIStateControl>();
            _selection = new BuildMenuSelection();
            _resolver = new PlacementTargetResolver(new PlacementTargetCatalog(new BeltConveyorPlacementUnlockSourceMap()), new ClientBlueprintLibrary(), new AllPlacementTargetsUnlocked());
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_controlObject);
        }

        [Test]
        public void ビルドメニュー中の選択は対象を選択へ渡す()
        {
            SetState(UIStateEnum.BuildMenu);
            var entry = _resolver.CreateUnlockedTargets().First();

            var result = CreateCommands(BlueprintDeleteResult.Success).SelectEntry(entry.Id);

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            Assert.IsTrue(_selection.TryConsumeSelectedTarget(out var selected));
            Assert.AreEqual(entry.Id, selected.Id);
        }

        [Test]
        public void 閉じた後と未知のエントリの選択は良性の拒否になる()
        {
            SetState(UIStateEnum.GameScreen);
            var closed = (UiCommandRejected)CreateCommands(BlueprintDeleteResult.Success).SelectEntry(Guid.NewGuid());
            SetState(UIStateEnum.BuildMenu);
            var unknown = (UiCommandRejected)CreateCommands(BlueprintDeleteResult.Success).SelectEntry(Guid.NewGuid());

            Assert.AreEqual("invalid_state", closed.ToWireCode());
            Assert.AreEqual("unknown_entry", unknown.ToWireCode());
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(closed.Reason));
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(unknown.Reason));
        }

        [TestCase(BlueprintDeleteResult.NotFound, "blueprint_delete_not_found", true)]
        [TestCase(BlueprintDeleteResult.NotUnlocked, "blueprint_delete_not_unlocked", false)]
        [TestCase(BlueprintDeleteResult.RequestFailed, "blueprint_delete_request_failed", false)]
        [TestCase(BlueprintDeleteResult.Unknown, "blueprint_delete_unknown", false)]
        public void BP削除の失敗はWebと同じ失敗コードと良性の別に写る(BlueprintDeleteResult deleteResult, string wireCode, bool benign)
        {
            var result = (UiCommandRejected)CreateCommands(deleteResult).DeleteBlueprint(Guid.NewGuid()).GetAwaiter().GetResult();

            Assert.AreEqual(wireCode, result.ToWireCode());
            Assert.AreEqual(benign, UiCommandRejectReasonTraits.IsBenign(result.Reason));
        }

        [Test]
        public void BP削除の成功は受理になる()
        {
            var result = CreateCommands(BlueprintDeleteResult.Success).DeleteBlueprint(Guid.NewGuid()).GetAwaiter().GetResult();

            Assert.IsInstanceOf<UiCommandAccepted>(result);
        }

        [Test]
        public void 閉じるはビルドメニュー中だけ受理する()
        {
            SetState(UIStateEnum.GameScreen);
            var closed = (UiCommandRejected)CreateCommands(BlueprintDeleteResult.Success).Close();

            Assert.AreEqual("invalid_state", closed.ToWireCode());
        }

        private BuildMenuCommands CreateCommands(BlueprintDeleteResult deleteResult)
        {
            return new BuildMenuCommands(_control, _resolver, _selection, new BlueprintDeleteServiceStub(deleteResult));
        }

        private void SetState(UIStateEnum state)
        {
            typeof(UIStateControl).GetProperty(nameof(UIStateControl.CurrentState)).SetValue(_control, state);
        }

        private sealed class BlueprintDeleteServiceStub : IBlueprintDeleteService
        {
            private readonly BlueprintDeleteResult _result;

            public BlueprintDeleteServiceStub(BlueprintDeleteResult result)
            {
                _result = result;
            }

            public UniTask<BlueprintDeleteResult> DeleteBlueprint(Guid blueprintGuid, CancellationToken ct)
            {
                return UniTask.FromResult(_result);
            }
        }
    }
}
```
（`using` に `Game.Block.Interface.Extension`〈`BeltConveyorPlacementUnlockSourceMap`〉ほか、前例 `BuildMenuEntryDtoFactoryTest.cs` の using を写して足す。ビルドメニュー中の `Close` の受理は `RequestTransition` が `UIStateDictionary` の無い `UIStateControl` で例外になり得るので L1 に持たず、録画 `uitk/uitk-build-menu` の閉じるボタンで見る。D-27）

- [ ] **Step 2: テストが落ちることを確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildMenuCommands` が無いコンパイルエラー

- [ ] **Step 3: 拒否理由と文言を足す**

`UiCommandRejectReason.cs` の enum の末尾（`LanguageAlreadySelected,` の下）に:
```csharp
        BuildMenuNotOpen,
        UnknownBuildMenuEntry,
        BlueprintDeleteNotFound,
        BlueprintDeleteNotUnlocked,
        BlueprintDeleteRequestFailed,
        BlueprintDeleteUnknown,
```
`IsBenign` の末尾（`_ =>` の直前）に（Web `BENIGN_ERRORS` の `build_menu.select`・`blueprint.delete` と同じ集合）:
```csharp
                UiCommandRejectReason.BuildMenuNotOpen => true,
                UiCommandRejectReason.UnknownBuildMenuEntry => true,
                UiCommandRejectReason.BlueprintDeleteNotFound => true,
                UiCommandRejectReason.BlueprintDeleteNotUnlocked => false,
                UiCommandRejectReason.BlueprintDeleteRequestFailed => false,
                UiCommandRejectReason.BlueprintDeleteUnknown => false,
```
`S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` の良性の一覧を固定するテスト（土台では「良性の拒否理由は言語の選択済みだけ」。S3-hotbar-modal などが先に名前を変えていれば `grep -n "IsBenign" $S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` で探す）を次に置き換える。全 enum 値を走査して良性の一覧を固定するので、良性を足すとこのテストが赤になる。`benign` の配列は、その時点の `grep -n "=> true" $S/Client.Ui.State/Commands/UiCommandRejectReason.cs` の値の全部（他レーンが足した良性を含む）にこのレーンの 3 値を足して書く:
```csharp
        [Test]
        public void 良性の拒否理由は宣言した一覧と一致する()
        {
            // 状態を変えない再送・閉じた後の選択・消えた BP の削除は利用者に知らせない（ログだけ）
            // Resends, selections after close and deletes of vanished blueprints change no state, so players are not told (log only)
            var benign = new[] { UiCommandRejectReason.LanguageAlreadySelected, UiCommandRejectReason.BuildMenuNotOpen, UiCommandRejectReason.UnknownBuildMenuEntry, UiCommandRejectReason.BlueprintDeleteNotFound };
            foreach (UiCommandRejectReason reason in System.Enum.GetValues(typeof(UiCommandRejectReason)))
            {
                Assert.AreEqual(System.Array.IndexOf(benign, reason) >= 0, UiCommandRejectReasonTraits.IsBenign(reason), reason.ToString());
            }
        }
```
（S3-hotbar-modal も同じ名前へ置き換える。取り込みで衝突したら `benign` を両方の値の和にする）

`ToLocalizationKey` の末尾に:
```csharp
                UiCommandRejectReason.BuildMenuNotOpen => LocalizationKeys.Ui.CommandRejected.BuildMenuNotOpen,
                UiCommandRejectReason.UnknownBuildMenuEntry => LocalizationKeys.Ui.CommandRejected.UnknownBuildMenuEntry,
                UiCommandRejectReason.BlueprintDeleteNotFound => LocalizationKeys.Ui.CommandRejected.BlueprintDeleteNotFound,
                UiCommandRejectReason.BlueprintDeleteNotUnlocked => LocalizationKeys.Ui.CommandRejected.BlueprintDeleteNotUnlocked,
                UiCommandRejectReason.BlueprintDeleteRequestFailed => LocalizationKeys.Ui.CommandRejected.BlueprintDeleteRequestFailed,
                UiCommandRejectReason.BlueprintDeleteUnknown => LocalizationKeys.Ui.CommandRejected.BlueprintDeleteUnknown,
```
`ToWireCode` の末尾に:
```csharp
                UiCommandRejectReason.BuildMenuNotOpen => "invalid_state",
                UiCommandRejectReason.UnknownBuildMenuEntry => "unknown_entry",
                UiCommandRejectReason.BlueprintDeleteNotFound => "blueprint_delete_not_found",
                UiCommandRejectReason.BlueprintDeleteNotUnlocked => "blueprint_delete_not_unlocked",
                UiCommandRejectReason.BlueprintDeleteRequestFailed => "blueprint_delete_request_failed",
                UiCommandRejectReason.BlueprintDeleteUnknown => "blueprint_delete_unknown",
```
`Localization/localization.csv` の `ui.commandRejected.languageAlreadySelected` の行の下に（列は既存の行と同じ 6 列。`grep -n "^ui.commandRejected.languageAlreadySelected" Localization/localization.csv` で列の並びを確かめてから書く）:
```csv
ui.commandRejected.buildMenuNotOpen,The build menu is not open,The build menu is not open,ビルドメニューが開いていません,Das Baumenü ist nicht geöffnet,건설 메뉴가 열려 있지 않습니다
ui.commandRejected.unknownBuildMenuEntry,This entry is no longer available,This entry is no longer available,この項目は選べなくなりました,Dieser Eintrag ist nicht mehr verfügbar,이 항목은 더 이상 사용할 수 없습니다
ui.commandRejected.blueprintDeleteNotFound,The blueprint was already deleted,The blueprint was already deleted,ブループリントはすでに削除されています,Der Bauplan wurde bereits gelöscht,설계도가 이미 삭제되었습니다
ui.commandRejected.blueprintDeleteNotUnlocked,Blueprints are not unlocked yet,Blueprints are not unlocked yet,ブループリントはまだ解放されていません,Baupläne sind noch nicht freigeschaltet,설계도가 아직 해금되지 않았습니다
ui.commandRejected.blueprintDeleteRequestFailed,Could not reach the server to delete the blueprint,Could not reach the server to delete the blueprint,ブループリントの削除をサーバーへ送れませんでした,Der Bauplan konnte nicht gelöscht werden (keine Serververbindung),설계도 삭제 요청을 서버에 보낼 수 없습니다
ui.commandRejected.blueprintDeleteUnknown,Could not delete the blueprint,Could not delete the blueprint,ブループリントを削除できませんでした,Der Bauplan konnte nicht gelöscht werden,설계도를 삭제할 수 없습니다
```
（生成キー `LocalizationKeys.Ui.CommandRejected.*` は CSV から生成される。compile 後に生成名を grep で確かめ、違えば読み替える。D-4）

- [ ] **Step 4: コマンドを書く**

`IBuildMenuCommands.cs`:
```csharp
using System;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.BuildMenu.Commands
{
    /// <summary>
    /// ビルドメニューの型付きコマンド（Web の build_menu.select・blueprint.delete・ui_state.request の写し）
    /// Typed build-menu commands (port of Web build_menu.select, blueprint.delete and ui_state.request)
    /// </summary>
    public interface IBuildMenuCommands
    {
        UiCommandResult SelectEntry(Guid targetId);
        UniTask<UiCommandResult> DeleteBlueprint(Guid blueprintGuid);
        UiCommandResult Close();
    }
}
```

`BuildMenuCommands.cs`:
```csharp
using System;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.BuildMenu.Commands
{
    /// <summary>
    /// 前提（ビルドメニュー中・解放済み）を確かめて実行する。拒否は Web の橋と同じ失敗コードの理由で返す
    /// Checks the preconditions (menu open, unlocked) and executes; rejections carry reasons with the Web bridge failure codes
    /// </summary>
    public sealed class BuildMenuCommands : IBuildMenuCommands
    {
        private readonly UIStateControl _uiStateControl;
        private readonly PlacementTargetResolver _placementTargetResolver;
        private readonly BuildMenuSelection _buildMenuSelection;
        private readonly IBlueprintDeleteService _blueprintDeleteService;

        public BuildMenuCommands(UIStateControl uiStateControl, PlacementTargetResolver placementTargetResolver, BuildMenuSelection buildMenuSelection, IBlueprintDeleteService blueprintDeleteService)
        {
            _uiStateControl = uiStateControl;
            _placementTargetResolver = placementTargetResolver;
            _buildMenuSelection = buildMenuSelection;
            _blueprintDeleteService = blueprintDeleteService;
        }

        public UiCommandResult SelectEntry(Guid targetId)
        {
            // メニューが先に閉じた・対象が消えた古いクリックは良性の拒否（トーストは Toaster が抑える）
            // Stale clicks after the menu closed or the target vanished are benign rejections (the toaster suppresses them)
            if (!IsBuildMenuOpen()) return new UiCommandRejected(UiCommandRejectReason.BuildMenuNotOpen, "");
            if (!_placementTargetResolver.TryResolve(targetId, out var target)) return new UiCommandRejected(UiCommandRejectReason.UnknownBuildMenuEntry, "");
            _buildMenuSelection.SetSelectedTarget(target);
            return UiCommandAccepted.Instance;
        }

        public async UniTask<UiCommandResult> DeleteBlueprint(Guid blueprintGuid)
        {
            var result = await _blueprintDeleteService.DeleteBlueprint(blueprintGuid, CancellationToken.None);
            return result switch
            {
                BlueprintDeleteResult.Success => UiCommandAccepted.Instance,
                BlueprintDeleteResult.NotFound => new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteNotFound, ""),
                BlueprintDeleteResult.NotUnlocked => new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteNotUnlocked, ""),
                BlueprintDeleteResult.Unknown => new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteUnknown, ""),
                _ => new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteRequestFailed, ""),
            };
        }

        public UiCommandResult Close()
        {
            if (!IsBuildMenuOpen()) return new UiCommandRejected(UiCommandRejectReason.BuildMenuNotOpen, "");
            _uiStateControl.RequestTransition(UIStateEnum.GameScreen);
            return UiCommandAccepted.Instance;
        }

        private bool IsBuildMenuOpen()
        {
            return _uiStateControl.CurrentState == UIStateEnum.BuildMenu;
        }
    }
}
```

- [ ] **Step 5: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.(BuildMenu|Commands)\.|UiCommandRejectReason|PauseMenuCommandsTest"`
Expected: `ErrorCount: 0`、全件 PASS（`PauseMenuCommandsTest` の「良性の拒否理由は宣言した一覧と一致する」を含む）

CSV の生成物を作り直す（前例 S2-skit・commit 42279a73e4）:
```bash
pnpm --dir moorestech_web/webui run gen:i18n
git diff --stat -- moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
```
Expected: 2 ファイルとも差分あり（`localizationKeys.ts` に `ui.commandRejected.*` の 6 キー、`_CompileRequester.cs` の `dummyText` が変わる）。`_CompileRequester.cs` が変わっていなければ、Editor の SchemaWatcher が CSV の変化を拾っていないので `uloop compile` をもう 1 回流してから見直す

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/State/PauseMenuCommandsTest.cs moorestech_client/Assets/Scripts/Client.Ui.State/BuildMenu/Commands moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BuildMenu
git commit -m "feat(uitk): ビルドメニューのコマンド（選択・BP 削除・閉じる）と拒否理由を足す（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

- [ ] **Step 7: 再生成のコミットを追従記録へ記帳する（実行計画 E-1）**

Step 6 のコミットは Web の生成物 `localizationKeys.ts` を触るので、G1 は記帳が無いと未記帳に数える（Task 13 のマージ条件「未記帳 0」を満たせない）。台帳 `docs/uitk/TODO.md` の「## 追従記録」の末尾に、前例 `3e946e4bd3`・`42279a73e4` の行と同じ形で 1 行足す（束の番号は台帳「## 行」で `src/shared/i18n/` を持つ束を確かめて書く。前例は 1）:
```markdown
| 1 | <Step 6 のコミットの先頭 10 桁> | 写した | 同じコミットの localization.csv と C#（ビルドメニューの拒否理由 6 値の文言。UiCommandRejectReasonTraits） |
```
```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): ビルドメニューの拒否理由の文言の再生成を追従記録へ（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
Task 13 Step 2 の取り込みで `localization.csv` が衝突し、生成物を作り直してマージコミットに入れた場合は、マージコミットは G1 が数えないので記帳しない。取り込みの後に生成物だけを作り直す別コミットを作ったときは、そのコミットも同じ形で 1 行記帳する

---

### Task 5: エントリのマス（枠・面・錨・不足ツールチップ・ドラッグ源）

**Files:**
- Create: `S/Client.Ui/Content/PlacementTargetIcons.cs`・`S/Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver.cs`
- Modify: `S/Client.Ui/Content/IGameContentTextureSource.cs`・`ClientContextContentTextureSource.cs`・`S/Client.Tests/Ui/Content/FakeContentTextureSource.cs`（2 メソッドの追記）
- Create: `S/Client.Ui/Screens/BuildMenu/Parts/IBuildMenuEntryHandler.cs`・`BuildMenuShortageTooltipText.cs`・`BuildMenuEntryDragPayload.cs`・`BuildMenuEntrySlot.cs`
- Create: `S/Client.Tests/Ui/Screens/BuildMenu/Support/BuildMenuSamples.cs`・`RecordingBuildMenuEntryHandler.cs`
- Test: `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuSlotTooltipTest.cs`・`BuildMenuDragSourceTest.cs`

**Interfaces:**
- Consumes: Task 2 の型、C3-a の `SlotFrame`・`PlacementTargetFace`・`UiIcon`・`GameContentPresentation`、F1 の `HoverTooltipMark`、C3-b の `UiDragDrop`・`IUiDragPayload`、`TutorialAnchor`・`AnchorIds.BuildMenuEntry`、`MaterialTooltipText`、L0-s3 の `BuildMenuEntryDragSource`（`Client.Ui.State/Hotbar/Logic`）
- Produces:
  - `namespace Client.Ui.Screens.BuildMenu.Parts { public interface IBuildMenuEntryHandler { void OnEntrySelected(Guid targetId); void OnBlueprintDeleteRequested(Guid targetId); void OnEntryHovered(Guid targetId); } }`
  - `public sealed class BuildMenuEntrySlot { public const string EntryClass = "build-menu__entry"; public BuildMenuEntrySlot(BuildMenuEntryViewModel entry, GameContentPresentation content, PlacementTargetIcons icons, IBuildMenuEntryHandler handler); public SlotFrame Frame { get; } public Guid TargetId { get; } public void Render(BuildMenuEntryViewModel entry); public static string TestIdOf(BuildMenuEntryViewModel entry); }`
  - `namespace Client.Ui.Content { public sealed class PlacementTargetIcons { public PlacementTargetIcons(GameContentPresentation content, IGameContentTextureSource textures, IBlueprintThumbnailLookup thumbnails); public bool TryToUiIcon(PlacementTargetIcon icon, out UiIcon uiIcon); } }`（値だけを返す。NameOnly と撮影の消えた BP で false。面へ当てる分岐は Screens）・`IGameContentTextureSource` に `Texture2D TrainCarTexture(Guid)`・`Texture2D ConnectToolTexture(Guid)`
  - `namespace Client.Ui.State.Hotbar.Logic { public interface IHotbarDropReceiver { void Receive(HotbarDragSource source); } }`（S3-hotbar-modal が枠の印で実装し、`UiDragDrop.MarkTarget<IHotbarDropReceiver>` で付ける）
  - `public static class BuildMenuShortageTooltipText { public static bool TryCreate(BuildMenuEntryViewModel entry, GameContentPresentation content, out UiText text); }`
  - `public sealed class BuildMenuEntryDragPayload : IUiDragPayload<IHotbarDropReceiver> { public BuildMenuEntryDragPayload(Guid targetId, IBuildMenuEntryHandler handler); public BuildMenuEntryDragSource Source { get; } }`
  - テスト補助 `BuildMenuSamples`（Task 6・7 も使う）: `public static readonly Guid Logistics, Transport, Blueprint, BuildingMaterials, EmptyCategory, Sub; public static readonly Guid WoodChest, IronChest, BeltConveyor, Rail, CargoCar, StarterBase; public static BuildMenuViewModel Create(); public static BuildMenuViewModel Create(IReadOnlyList<BuildMenuMaterialRow> materials); public static BuildMenuViewModel Create(IReadOnlyList<BuildMenuMaterialRow> materials, PlacementTargetIcon woodChestIcon); public static BuildMenuEntryViewModel Block(Guid id, Guid category, string label, IReadOnlyList<BuildMenuMaterialRow> materials, bool paymentWaived, BuildMenuSetPlacement setPlacement); public static BuildMenuMaterialRow Material(ItemId itemId, int count, int held);`

- [ ] **Step 1: テスト補助を書く**

`Support/BuildMenuSamples.cs`（Web の mock fixture の形を写す: 10 カテゴリ〈物流・輸送・BP・ほか 7〉にエントリ、11 個目は空。名前は検索「鉄」が物流と輸送にだけ当たるように選ぶ）:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.Text;
using Core.Master;
using Game.PlacementTarget;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    // Web の mock fixture（10 カテゴリ・物流/輸送/BP）と同じ形の ViewModel
    // View model shaped like the Web mock fixture (10 categories, logistics/transport/blueprint)
    internal static class BuildMenuSamples
    {
        public static readonly Guid Logistics = Guid.Parse("a0000000-0000-4000-8000-000000000001");
        public static readonly Guid Transport = Guid.Parse("a0000000-0000-4000-8000-000000000002");
        public static readonly Guid Blueprint = Guid.Parse("a0000000-0000-4000-8000-000000000003");
        public static readonly Guid EmptyCategory = Guid.Parse("a0000000-0000-4000-8000-0000000000ff");
        public static readonly Guid Sub = Guid.Parse("b0000000-0000-4000-8000-000000000001");
        public static readonly Guid WoodChest = Guid.Parse("c0000000-0000-4000-8000-000000000001");
        public static readonly Guid IronChest = Guid.Parse("c0000000-0000-4000-8000-000000000002");
        public static readonly Guid BeltConveyor = Guid.Parse("c0000000-0000-4000-8000-000000000003");
        public static readonly Guid Rail = Guid.Parse("c0000000-0000-4000-8000-000000000004");
        public static readonly Guid CargoCar = Guid.Parse("c0000000-0000-4000-8000-000000000005");
        public static readonly Guid StarterBase = Guid.Parse("c0000000-0000-4000-8000-000000000006");
        public static readonly Guid BuildingMaterials = Guid.Parse("a0000000-0000-4000-8000-000000000004");

        // Web の mock fixture（english シナリオ）のカテゴリ名を 11 個（Task 5 Step 1 で W/e2e/mock-host/fixtures/buildMenuFixtures.ts と辞書から写す。最長の Building Materials を必ず含める）
        // Eleven category names from the Web mock fixture (english scenario), always including the longest, Building Materials
        private static readonly string[] CategoryNames = { "Logistics", "Transport", "Blueprints", "Building Materials", "Production", "Power", "Fluids", "Mining", "Storage", "Decoration", "Empty" };

        public static BuildMenuViewModel Create()
        {
            return Create(Array.Empty<BuildMenuMaterialRow>(), PlacementTargetNameOnlyIcon.Instance);
        }

        public static BuildMenuViewModel Create(IReadOnlyList<BuildMenuMaterialRow> materials)
        {
            return Create(materials, PlacementTargetNameOnlyIcon.Instance);
        }

        // 木のチェストとベルトコンベアにだけ素材を持たせる版（詳細の素材の題を見る画面テスト用。素材の名前はテスト mod が要る）
        // Variant where only the wood chest and belt conveyor carry materials (for screen tests that read the detail's materials title; item names need the test mod)
        // 木のチェストにだけ絵を持たせられる版（アイコンの中央寄せを測る画面テスト用）
        // Variant where only the wood chest may carry a picture (for the screen test measuring icon centering)
        public static BuildMenuViewModel Create(IReadOnlyList<BuildMenuMaterialRow> materials, PlacementTargetIcon woodChestIcon)
        {
            // 物流・輸送・BP の後に 7 カテゴリ（各 1 件）、最後に空のカテゴリ。id は決め打ちにし、作り直しても構造が同じになるようにする
            // Logistics, transport, blueprint, then 7 categories with one entry each, then an empty one; ids are fixed so a rebuild keeps the same structure
            var others = Enumerable.Range(4, 7).Select(i => Guid.Parse($"a0000000-0000-4000-8000-{i:D12}")).ToList();
            var categoryGuids = new[] { Logistics, Transport, Blueprint }.Concat(others).Append(EmptyCategory).ToList();
            var categories = categoryGuids.Select(guid => new BuildMenuCategory(guid, new[] { Sub })).ToList();
            var entries = new List<BuildMenuEntryViewModel>
            {
                new(WoodChest, PlacementTargetKind.Block, Logistics, Sub, "木のチェスト", materials, false, woodChestIcon, BuildMenuSetPlacementNone.Instance),
                Block(IronChest, Logistics, "鉄のチェスト", Array.Empty<BuildMenuMaterialRow>(), false, BuildMenuSetPlacementNone.Instance),
                Block(BeltConveyor, Logistics, "ベルトコンベア", materials, false, new BuildMenuSetPlacementAvailable(3, 2)),
                Block(Rail, Transport, "鉄道レール", Array.Empty<BuildMenuMaterialRow>(), false, BuildMenuSetPlacementNone.Instance),
                new(CargoCar, PlacementTargetKind.TrainCar, Transport, Sub, "貨車", Array.Empty<BuildMenuMaterialRow>(), false, PlacementTargetNameOnlyIcon.Instance, BuildMenuSetPlacementNone.Instance),
                new(StarterBase, PlacementTargetKind.Blueprint, Blueprint, Sub, "スターター拠点", Array.Empty<BuildMenuMaterialRow>(), false, PlacementTargetNameOnlyIcon.Instance, BuildMenuSetPlacementNone.Instance),
            };
            entries.AddRange(others.Select((guid, i) => Block(Guid.Parse($"c1000000-0000-4000-8000-{i:D12}"), guid, $"機械{i}", Array.Empty<BuildMenuMaterialRow>(), false, BuildMenuSetPlacementNone.Instance)));

            // 見出しは Web の english シナリオの英語名（最長は Building Materials）を逐語で入れる
            // Headings carry the Web english-scenario names verbatim (the longest is Building Materials)
            var names = new Dictionary<Guid, UiText> { [Sub] = UiText.FromVerbatim("General") };
            for (var i = 0; i < categoryGuids.Count; i++) names[categoryGuids[i]] = UiText.FromVerbatim(CategoryNames[i]);
            return new BuildMenuViewModel(categories, names, entries);
        }

        public static BuildMenuEntryViewModel Block(Guid id, Guid category, string label, IReadOnlyList<BuildMenuMaterialRow> materials, bool paymentWaived, BuildMenuSetPlacement setPlacement)
        {
            return new BuildMenuEntryViewModel(id, PlacementTargetKind.Block, category, Sub, label, materials, paymentWaived, PlacementTargetNameOnlyIcon.Instance, setPlacement);
        }

        // 本番と同じ絵の引き方（テクスチャは白、撮影は空）
        // Same picture lookup as production (white textures, no captures)
        public static PlacementTargetIcons Icons(GameContentPresentation content)
        {
            return new PlacementTargetIcons(content, new FakeContentTextureSource(Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture), new BlueprintThumbnailContainer());
        }

        public static BuildMenuMaterialRow Material(ItemId itemId, int count, int held)
        {
            return new BuildMenuMaterialRow(itemId, new BuildMenuRequiredItem(Guid.NewGuid(), count, held, held < count));
        }
    }
}
```
（ブロックのアイコンを `PlacementTargetNameOnlyIcon` にして、L1 がマスタとテクスチャに頼らないようにする。木のチェストだけは中央寄せの L1 が `PlacementTargetBlockIcon(ForUnitTestModBlockId.BlockId)`〈白のテクスチャ〉を渡せる。絵の経路は Task 3 の `BuildMenuEntryFactoryTest` と録画が持つ）

`Support/RecordingBuildMenuEntryHandler.cs`: `IBuildMenuEntryHandler` を実装し、`Selected`・`DeleteRequested`・`Hovered`（各 `List<Guid>`）へ積むだけの `internal sealed class`。

- [ ] **Step 2: 失敗するテストを書く**

`BuildMenuSlotTooltipTest.cs`（`UiTestHost` を土台に、`SetUp` でテスト mod を立てて言語を english にする。`GameContentPresentation` は `new GameContentPresentation(new FakeContentTextureSource(Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture))`）:
```csharp
using System;
using Client.Localization;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Tests.Ui.TestHost;
using Client.Ui.Content;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuSlotTooltipTest : UiTestHost
    {
        private GameContentPresentation _content;

        [SetUp]
        public void SetUpContent()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.TrySetChosenLanguage("english");
            _content = new GameContentPresentation(new FakeContentTextureSource(Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture));
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuSlot.test.ts">不足時は見出しと不足行だけをツールチップに出す</web-test>
        [Test]
        public void 不足時は見出しと不足行だけをツールチップに出す()
        {
            var slot = Mount(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 1), BuildMenuSamples.Material(ForUnitTestItemId.ItemId2, 1, 5), false);

            Assert.IsTrue(HoverTooltipMark.TryFindMarked(slot.Frame, out _, out var text));
            var itemName = _content.ItemName(ForUnitTestItemId.ItemId1).Resolve();
            Assert.AreEqual($"{new UiText(LocalizationKeys.Ui.BuildMenu.MaterialShortageTitle).Resolve()}\n{itemName} 1/3", text.Resolve());
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuSlot.test.ts">支払い免除中は素材が足りなくてもツールチップを無効にする</web-test>
        [Test]
        public void 支払い免除中は素材が足りなくてもツールチップを出さない()
        {
            var slot = Mount(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 1), BuildMenuSamples.Material(ForUnitTestItemId.ItemId2, 1, 5), true);

            Assert.IsFalse(HoverTooltipMark.TryFindMarked(slot.Frame, out _, out _));
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuSlot.test.ts">充足時はツールチップを無効にする</web-test>
        [Test]
        public void 充足時はツールチップを出さない()
        {
            var slot = Mount(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 3), BuildMenuSamples.Material(ForUnitTestItemId.ItemId2, 1, 5), false);

            Assert.IsFalse(HoverTooltipMark.TryFindMarked(slot.Frame, out _, out _));
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuSlot.test.ts">不足していてもスロットに赤枠を付けない</web-test>
        [Test]
        public void 不足していてもスロットに赤枠を付けない()
        {
            var slot = Mount(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 1), BuildMenuSamples.Material(ForUnitTestItemId.ItemId2, 1, 5), false);

            Assert.IsFalse(slot.Frame.ClassListContains("slot-frame--insufficient"));
            Assert.IsTrue(slot.Frame.ClassListContains("slot-frame--filled"));
        }

        private BuildMenuEntrySlot Mount(BuildMenuMaterialRow first, BuildMenuMaterialRow second, bool paymentWaived)
        {
            var entry = BuildMenuSamples.Block(Guid.NewGuid(), BuildMenuSamples.Logistics, "木のチェスト", new[] { first, second }, paymentWaived, BuildMenuSetPlacementNone.Instance);
            var slot = new BuildMenuEntrySlot(entry, _content, BuildMenuSamples.Icons(_content), new RecordingBuildMenuEntryHandler());
            rootVisualElement.Add(slot.Frame);
            simulate.FrameUpdate();
            return slot;
        }
    }
}
```
（`UiTestHost` の `TearDown` は全体スイッチの入力を Web へ戻す。言語は `ScreenTestHost` と同じく保存・復元が要るなら `ScreenTestHost` を土台にする〈その方が前例どおり。`Toasts` は使わない〉。土台は書くときに `ScreenTestHost` に揃える）

`BuildMenuDragSourceTest.cs`（押し方は C3-b の `S/Client.Tests/Ui/DragDrop/UiDragDropTest.cs` の「タップ」と同じ手順を実物から写す）:
- `左のタップは選択を要求する`: Block のマスを載せ、`UiDragDropTest` と同じ押下→解放（移動なし）を流す → `handler.Selected` が `[id]`、`DeleteRequested` が空
- `BPの右押下だけ削除を要求する`: BP のマスと Block のマスに `SlotFrame` の右押下（`PointerDownEvent` の button 1。C3-a の `SlotFrameTest` の流し方を写す）→ `DeleteRequested` が BP の id だけ
- `ホバーで詳細の対象を知らせる`: `PointerEnterEvent`（`SlotFrameTest` の HoverEnter の流し方）→ `Hovered` が `[id]`
- `ホットバーの受け口へ落とすと源を1回渡す`: テストファイル内の `private sealed class RecordingHotbarReceiver : IHotbarDropReceiver`（`List<HotbarDragSource> Received`・`Receive` は積むだけ）へ `new BuildMenuEntryDragPayload(id, handler).OnDropped(receiver)` → `Received.Count == 1`・`((BuildMenuEntryDragSource)Received[0]).TargetId == id`・`LogAssert.NoUnexpectedReceived()`・`Selected` は空
- `受け口でない落とし先と断られた落としは何もしない`: テストファイル内の中身の無い別の型 `private sealed class PlainTarget { }` を、Block のマスの横に置いた箱へ `UiDragDrop.MarkTarget<PlainTarget>(box, new PlainTarget())` で付け、マスから箱へ `UiDragDropTest` と同じ押下→移動→解放を流す。印の型（`PlainTarget`）が源の型（`IHotbarDropReceiver`）と違うので Foundation が飛ばし、枠の外と同じく `OnDroppedOutside` になる → `Selected`・`DeleteRequested` が空、`LogAssert.NoUnexpectedReceived()`

- [ ] **Step 3: テストが落ちることを確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildMenuEntrySlot` 等が無いコンパイルエラー

- [ ] **Step 4: マスの部品を書く**

`IBuildMenuEntryHandler.cs`:
```csharp
using System;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// マスの操作を受ける口。コマンドの呼び出しと詳細の sticky は View が持つ
    /// Receiver for slot actions; the view owns the command calls and the sticky detail
    /// </summary>
    public interface IBuildMenuEntryHandler
    {
        void OnEntrySelected(Guid targetId);
        void OnBlueprintDeleteRequested(Guid targetId);
        void OnEntryHovered(Guid targetId);
    }
}
```

`S/Client.Ui/Content/PlacementTargetIcons.cs`（最終の置き場。ホットバーも使う。持ち主はこのレーン。前例 `GameContentPresentation` と同じく値だけを返し、面へ当てる分岐は Screens が持つ。DI の Singleton）:
```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Ui.Foundation.Icons;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Text;

namespace Client.Ui.Content
{
    /// <summary>
    /// 配置対象の絵の出どころ → UiIcon（Block は表示素材、車両・接続ツールは素材の口、BP は撮影の口から引く）
    /// Placement-target picture source to UiIcon (blocks via presentation, cars and connect tools via the texture source, blueprints via thumbnails)
    /// </summary>
    public sealed class PlacementTargetIcons
    {
        private readonly GameContentPresentation _content;
        private readonly IGameContentTextureSource _textures;
        private readonly IBlueprintThumbnailLookup _thumbnails;

        public PlacementTargetIcons(GameContentPresentation content, IGameContentTextureSource textures, IBlueprintThumbnailLookup thumbnails)
        {
            _content = content;
            _textures = textures;
            _thumbnails = thumbnails;
        }

        // 名前で描く対象（NameOnly）・撮影が消えた BP・テクスチャの欠けは false（面は名前で描き、詳細の列は絵を足さない。[A-100]）
        // False for NameOnly, a blueprint whose capture vanished, and a missing texture (the face draws the name, the detail adds no picture; [A-100])
        public bool TryToUiIcon(PlacementTargetIcon icon, out UiIcon uiIcon)
        {
            uiIcon = icon switch
            {
                PlacementTargetBlockIcon block => _content.Block(block.BlockId),
                PlacementTargetTrainCarIcon car => Textured(_textures.TrainCarTexture(car.TrainCarGuid), car.TrainCarGuid),
                PlacementTargetConnectToolIcon tool => Textured(_textures.ConnectToolTexture(tool.ConnectToolGuid), tool.ConnectToolGuid),
                PlacementTargetBlueprintIcon blueprint => _thumbnails.TryGet(blueprint.BlueprintGuid, out var thumbnail) ? Textured(thumbnail, blueprint.BlueprintGuid) : null,
                PlacementTargetNameOnlyIcon => null,
                _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, null),
            };
            if (uiIcon?.Texture != null) return true;
            uiIcon = null;
            return false;
        }

        // 名前は面と詳細が別に描くので、絵の代わりの文字は持たせない
        // The face and detail draw the name themselves, so no fallback text is carried
        private static UiIcon Textured(UnityEngine.Texture2D texture, Guid id)
        {
            return new UiIcon(texture, id.ToString("D"), UiText.FromVerbatim(string.Empty));
        }
    }
}
```

`S/Client.Ui/Content/IGameContentTextureSource.cs` へ 2 メソッド足す（C3-a はマージ済みで、W3 にこのファイルを触る並走レーンは無い。このレーンの追記）:
```csharp
        Texture2D TrainCarTexture(Guid trainCarGuid);
        Texture2D ConnectToolTexture(Guid connectToolGuid);
```
（`using System;` を足す）`ClientContextContentTextureSource.cs` に実装を足す（欠けは null。各容器が LogError を出す前例どおり）:
```csharp
        public Texture2D TrainCarTexture(Guid trainCarGuid)
        {
            return ClientContext.TrainCarImageContainer.GetTrainCarView(trainCarGuid)?.ItemTexture as Texture2D;
        }

        public Texture2D ConnectToolTexture(Guid connectToolGuid)
        {
            return ClientContext.ConnectToolImageContainer.GetConnectToolView(connectToolGuid)?.ItemTexture as Texture2D;
        }
```
（容器の名前は `grep -n "TrainCarImageContainer\|ConnectToolImageContainer" moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientContext.cs` で実物に合わせる）。`S/Client.Tests/Ui/Content/FakeContentTextureSource.cs` の 2 メソッドは ctor の `block` のテクスチャを返す（L1 は絵の有無だけを見る）


`S/Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver.cs`（ホットバーへの落としの受け口。このレーンが作るのはこの 1 本だけで、S3-hotbar-modal は枠の印で実装し `UiDragDrop.MarkTarget<IHotbarDropReceiver>` で付ける。State は `Client.Ui` を参照できないが、落とし先の型は源の型引数で閉じるので Foundation の型を継ぐ必要は無い）:
```csharp
namespace Client.Ui.State.Hotbar.Logic
{
    /// <summary>
    /// ホットバーの落とし先が源を受け取る口。割当の要求まで受け手が済ませ、断る理由のログも受け手が出す
    /// Port through which a hotbar drop target receives a source; the receiver sends the assignment request and logs any refusal
    /// </summary>
    public interface IHotbarDropReceiver
    {
        void Receive(HotbarDragSource source);
    }
}
```

`BuildMenuShortageTooltipText.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Content;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.MaterialTooltip;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// 不足ツールチップの文。題と不足行を改行で結ぶ（Web BuildMenuSlot の pre-line の写し）
    /// Shortage tooltip text: the title and shortage lines joined by newlines (port of the Web BuildMenuSlot pre-line)
    /// </summary>
    public static class BuildMenuShortageTooltipText
    {
        // 不足が無ければ偽（印を付けない。前例 PlacementTargetIcons.TryToUiIcon の形）
        // False when nothing is short (no mark; same shape as PlacementTargetIcons.TryToUiIcon)
        public static bool TryCreate(BuildMenuEntryViewModel entry, GameContentPresentation content, out UiText text)
        {
            text = null;
            var lines = new List<string> { new UiText(LocalizationKeys.Ui.BuildMenu.MaterialShortageTitle).Resolve() };
            foreach (var row in entry.Materials)
            {
                if (!row.IsInsufficient) continue;
                var itemName = content.ItemName(row.ItemId).Resolve();
                lines.Add(MaterialTooltipText.Create(MaterialTooltipKind.BuildMenuShortageLine, itemName, row.Item.Count, row.Item.Held).Resolve());
            }
            if (lines.Count == 1) return false;
            text = UiText.FromVerbatim(string.Join("\n", lines));
            return true;
        }
    }
}
```
（解決済みの文を `FromVerbatim` で渡すので、言語が変わったときは Model が組み直し、View の `Render` がマスの印を張り替える。D-15）

`BuildMenuEntryDragPayload.cs`:
```csharp
using System;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// エントリのドラッグ源。タップは選択、落としはホットバーの受け口（IHotbarDropReceiver）へ渡す
    /// Drag source of an entry; a tap selects, and a drop goes to the hotbar receiver (IHotbarDropReceiver)
    /// </summary>
    public sealed class BuildMenuEntryDragPayload : IUiDragPayload<IHotbarDropReceiver>
    {
        private readonly IBuildMenuEntryHandler _handler;

        public BuildMenuEntryDragSource Source { get; }

        public BuildMenuEntryDragPayload(Guid targetId, IBuildMenuEntryHandler handler)
        {
            Source = new BuildMenuEntryDragSource(targetId);
            _handler = handler;
        }

        public void OnTapped()
        {
            _handler.OnEntrySelected(Source.TargetId);
        }

        public void OnDropped(IHotbarDropReceiver receiver)
        {
            // ホットバーの印へ源を渡す。別の型の落とし先は Foundation が飛ばし OnDroppedOutside になる。断った理由のログは受け手が出す（D-16）
            // Hand the source to the hotbar mark; other target types are skipped by Foundation into OnDroppedOutside; the receiver logs any refusal (D-16)
            receiver.Receive(Source);
        }

        public void OnDroppedOutside()
        {
            // silent-ok: 正常操作の無操作でありガードの縮退ではない（Web の HotbarDropResolver も何もしない） / 遊ぶ人は何も起きないことで分かる
            // silent-ok: a normal no-op, not a degraded guard (the Web HotbarDropResolver does nothing either) / the player sees nothing happened
        }
    }
}
```

`BuildMenuEntrySlot.cs`:
```csharp
using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.Text;
using UniRx;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// エントリ 1 マス（Web BuildMenuSlot の写し）。同じ id のまま Render で中身だけ差し替える
    /// One entry slot (port of the Web BuildMenuSlot); Render swaps contents while the id stays the same
    /// </summary>
    public sealed class BuildMenuEntrySlot
    {
        public const string EntryClass = "build-menu__entry";

        private readonly GameContentPresentation _content;
        private readonly PlacementTargetIcons _icons;
        private readonly PlacementTargetFace _face = new();

        public SlotFrame Frame { get; }
        public Guid TargetId { get; }

        public BuildMenuEntrySlot(BuildMenuEntryViewModel entry, GameContentPresentation content, PlacementTargetIcons icons, IBuildMenuEntryHandler handler)
        {
            _content = content;
            _icons = icons;
            TargetId = entry.Catalog.TargetId;
            var kind = entry.KindName;
            Frame = new SlotFrame(TestIdOf(entry));
            Frame.AddToClassList(EntryClass);
            Frame.SetState(new SlotFrameState(false, true, false, false, false));
            Frame.Add(_face);
            TutorialAnchor.Mark(Frame, AnchorIds.BuildMenuEntry(kind, TargetId.ToString("D")));

            // 左はドラッグ源（タップ＝選択）、右は BP だけ削除、ホバーは詳細の sticky
            // Left is the drag source (tap selects), right deletes blueprints only, hover feeds the sticky detail
            UiDragDrop.MarkSource(Frame, new BuildMenuEntryDragPayload(TargetId, handler));
            var isBlueprint = entry.CanDeleteBlueprint;
            Frame.OnSlotGestured.Subscribe(gesture =>
            {
                if (gesture.Kind == SlotGestureKind.HoverEnter) handler.OnEntryHovered(TargetId);
                if (gesture.Kind == SlotGestureKind.RightDown && isBlueprint) handler.OnBlueprintDeleteRequested(TargetId);
            });
            Render(entry);
        }

        public static string TestIdOf(BuildMenuEntryViewModel entry)
        {
            return $"build-menu-entry-{entry.KindName}-{entry.Catalog.TargetId:D}";
        }

        public void Render(BuildMenuEntryViewModel entry)
        {
            // 絵があれば絵、無ければ名前（Web PlacementTargetFace の iconUrl の有無）
            // A picture when there is one, otherwise the name (the Web PlacementTargetFace iconUrl check)
            var name = UiText.FromVerbatim(entry.Catalog.DisplayLabel);
            if (_icons.TryToUiIcon(entry.Icon, out var icon)) _face.SetIconTarget(icon, name);
            else _face.SetNameTarget(name);

            // 不足があるときだけツールチップの印を付ける。枠は赤くしない（Web と同じ）
            // Mark the tooltip only when something is short; the frame never turns red (same as the Web)
            if (BuildMenuShortageTooltipText.TryCreate(entry, _content, out var tooltip)) HoverTooltipMark.Mark(Frame, tooltip);
            else HoverTooltipMark.Unmark(Frame);
        }
    }
}
```
（`Frame.OnSlotGestured` の購読はマスの寿命＝枠の寿命なので破棄しない〈枠の `Subject` と一緒に回収される〉。`SlotGestureKind` の値の名前は C3-a の実物どおり）

- [ ] **Step 5: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.BuildMenu\."`
Expected: `ErrorCount: 0`、9 件 PASS（ドラッグ源 5・`BuildMenuSlot.test.ts` の写し 4）

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BuildMenu/Parts moorestech_client/Assets/Scripts/Client.Ui/Content moorestech_client/Assets/Scripts/Client.Tests/Ui/Content/FakeContentTextureSource.cs moorestech_client/Assets/Scripts/Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu
git commit -m "feat(uitk): ビルドメニューのエントリのマス（不足ツールチップ・ドラッグ源・錨）を足す（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 6: リスト・サイドバー・詳細の部品

**Files:**
- Create: `S/Client.Ui/Screens/BuildMenu/Parts/BuildMenuSectionView.cs`・`BuildMenuCategoryList.cs`・`BuildMenuCategorySidebar.cs`・`BuildMenuDetailSidebar.cs`
- Test: `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuCategoryListTest.cs`・`BuildMenuDetailViewTest.cs`（部品の 3 件。View の 2 件は Task 7 の `BuildMenuDetailStickyViewTest`）

**Interfaces:**
- Consumes: Task 2・5 の型、C3-a の `SlotGrid`・`ItemSlot`・`GameIcon`、`FadeRule`・`LocalizedLabel`、C3-b の `ModeSwitch<Guid>`
- Produces:
  - `public sealed class BuildMenuCategoryList : VisualElement { public BuildMenuCategoryList(GameContentPresentation content, PlacementTargetIcons icons, IBuildMenuEntryHandler handler); public VisualElement Spacer { get; } public void Render(IReadOnlyList<BuildMenuCategoryGroup> groups, BuildMenuViewModel viewModel); public void SetSpacerHeight(float height); public bool TryGetHeading(Guid categoryGuid, out VisualElement heading); public bool TryGetGroup(Guid categoryGuid, out VisualElement group); public IObservable<Unit> OnGroupGeometryChanged { get; } }`
  - `public sealed class BuildMenuCategorySidebar : VisualElement { public BuildMenuCategorySidebar(); public IObservable<Guid> OnCategorySelected { get; } public void Render(BuildMenuShownList shown, BuildMenuViewModel viewModel); public void SetActive(Guid? categoryGuid); }`
  - `public sealed class BuildMenuDetailSidebar : VisualElement { public BuildMenuDetailSidebar(GameContentPresentation content, PlacementTargetIcons icons); public void ShowHint(); public void ShowEntry(BuildMenuEntryViewModel entry); }`

- [ ] **Step 1: 失敗するテストを書く**

`BuildMenuCategoryListTest.cs`（`ScreenTestHost` を土台。部品をそのまま載せる）:
```csharp
using System.Linq;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Entries;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuCategoryListTest : ScreenTestHost
    {
        /// <web-test file="src/features/buildMenu/views/BuildMenuCategoryList.test.ts">カテゴリ群ごとに大見出しを置き、全群のsectionと各見出しのh2をrefへ登録し、末尾スペーサ高を反映する</web-test>
        [Test]
        public void カテゴリ群ごとに大見出しを置き見出しと群を引けて末尾スペーサ高を反映する()
        {
            var viewModel = BuildMenuSamples.Create();
            var shown = BuildMenuShownListComposer.Compose(viewModel, "");
            var list = Mount(new BuildMenuCategoryList(null, BuildMenuSamples.Icons(null), new RecordingBuildMenuEntryHandler()));

            list.Render(shown.Groups, viewModel);
            list.SetSpacerHeight(123f);
            simulate.FrameUpdate();

            // 見出しは群の順に並び、testid は Web と同じ
            // Headings follow the group order with the Web testids
            var headings = list.Query<VisualElement>(className: "build-menu__category-heading").ToList();
            CollectionAssert.AreEqual(shown.ShownCategoryGuids.Select(g => $"build-menu-category-heading-{g:D}"), headings.Select(h => h.name));
            Assert.IsTrue(list.TryGetHeading(BuildMenuSamples.Logistics, out var heading));
            Assert.AreSame(headings[0], heading);
            Assert.IsTrue(list.TryGetGroup(BuildMenuSamples.Transport, out var group));
            Assert.AreEqual($"build-menu-category-{BuildMenuSamples.Transport:D}-group", group.name);
            Assert.AreEqual(123f, list.Spacer.resolvedStyle.minHeight.value, 0.01f);
            Assert.AreEqual("build-menu-trailing-spacer", list.Spacer.name);
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuCategoryList.test.ts">groupsが空でもクラッシュせずスペーサのみ出す</web-test>
        [Test]
        public void 群が空でもスペーサだけを出す()
        {
            var list = Mount(new BuildMenuCategoryList(null, BuildMenuSamples.Icons(null), new RecordingBuildMenuEntryHandler()));

            list.Render(System.Array.Empty<Client.Ui.State.BuildMenu.Logic.BuildMenuCategoryGroup>(), BuildMenuViewModel.Empty);
            list.SetSpacerHeight(42f);
            simulate.FrameUpdate();

            Assert.AreEqual(0, list.Query<VisualElement>(className: "build-menu__category-heading").ToList().Count);
            Assert.IsFalse(list.TryGetHeading(BuildMenuSamples.Logistics, out _));
            Assert.AreEqual(42f, list.Spacer.resolvedStyle.minHeight.value, 0.01f);
        }
    }
}
```
（`content` に null を渡せるのは、サンプルのアイコンが全部 `PlacementTargetNameOnlyIcon` で絵を引かないため。マスの文は検索・群分けにしか使わない）

`BuildMenuDetailViewTest.cs`（`ScreenTestHost` を土台。`SetUp` でテスト mod を立て、`_content` は Task 5 と同じ）:
```csharp
        /// <web-test file="src/features/buildMenu/views/BuildMenuDetailSidebar.test.ts">不足素材は赤枠と所持/必要の数値をスロットへ渡す</web-test>
        [Test]
        public void 不足素材は赤枠と所持と必要の数値をスロットへ渡す()
        {
            var detail = ShowEntry(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 1), false);

            var slot = Find<VisualElement>(detail, "build-menu-detail-material-0");
            Assert.IsTrue(slot.ClassListContains("slot-frame--insufficient"));
            StringAssert.Contains("1/3", slot.Q<Label>(className: "item-slot__shortage").text);
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuDetailSidebar.test.ts">素材が足りていれば赤くしない</web-test>
        [Test]
        public void 素材が足りていれば赤くしない()
        {
            var detail = ShowEntry(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 5), false);

            Assert.IsFalse(Find<VisualElement>(detail, "build-menu-detail-material-0").ClassListContains("slot-frame--insufficient"));
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuDetailSidebar.test.ts">支払い免除中は素材が足りなくても赤くしない</web-test>
        [Test]
        public void 支払い免除中は素材が足りなくても赤くしない()
        {
            var detail = ShowEntry(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 1), true);

            Assert.IsFalse(Find<VisualElement>(detail, "build-menu-detail-material-0").ClassListContains("slot-frame--insufficient"));
        }

        private BuildMenuDetailSidebar ShowEntry(BuildMenuMaterialRow material, bool paymentWaived)
        {
            var detail = Mount(new BuildMenuDetailSidebar(_content, BuildMenuSamples.Icons(_content)));
            detail.ShowEntry(BuildMenuSamples.Block(System.Guid.NewGuid(), BuildMenuSamples.Logistics, "木のチェスト", new[] { material }, paymentWaived, BuildMenuSetPlacementNone.Instance));
            simulate.FrameUpdate();
            return detail;
        }
```
（`ItemSlot` の `frameTestid` は中の `SlotFrame` の name になるので、赤枠のクラスはその要素で見る。所持/必要の文の書式と、不足の数字を出すラベルのクラス名は C3-a の `ItemSlot`〈`SlotTexts.ItemCountSummary`〉の実物を読んで合わせる）

- [ ] **Step 2: テストが落ちることを確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildMenuCategoryList` 等が無いコンパイルエラー

- [ ] **Step 3: 節とリストを書く**

`BuildMenuSectionView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// サブカテゴリ節（小見出し → FadeRule → 8 列の格子。Web BuildMenuCategoryGrid の写し）
    /// One sub-category section: heading, FadeRule, 8-column grid (port of the Web BuildMenuCategoryGrid)
    /// </summary>
    public sealed class BuildMenuSectionView : VisualElement
    {
        private const int Columns = 8;
        private readonly List<BuildMenuEntrySlot> _slots = new();

        public BuildMenuSectionView(BuildMenuSection section, BuildMenuViewModel viewModel, GameContentPresentation content, PlacementTargetIcons icons, IBuildMenuEntryHandler handler)
        {
            name = $"build-menu-section-{section.CategoryGuid:D}-{section.SubCategoryGuid:D}";
            AddToClassList("build-menu__section");

            var heading = new LocalizedLabel();
            heading.AddToClassList("build-menu__section-heading");
            heading.SetText(viewModel.HeadingNameOf(section.SubCategoryGuid));
            Add(heading);
            var rule = new FadeRule();
            rule.AddToClassList("build-menu__stacked");
            Add(rule);

            // 格子へエントリのマスを並べる
            // Lay the entry slots into the grid
            var grid = new SlotGrid($"build-menu-grid-{section.CategoryGuid:D}-{section.SubCategoryGuid:D}", Columns);
            grid.AddToClassList("build-menu__stacked");
            foreach (var catalog in section.Entries)
            {
                var slot = new BuildMenuEntrySlot(EntryOf(viewModel, catalog.TargetId), content, icons, handler);
                _slots.Add(slot);
                grid.AddItem(slot.Frame);
            }
            Add(grid);
        }

        // 構造が同じときの差し替え。並びは BuildMenuCategoryList が構造の鍵で保証する
        // Swap contents when the structure is unchanged; BuildMenuCategoryList guarantees the order through its structure key
        public void Render(BuildMenuSection section, BuildMenuViewModel viewModel)
        {
            for (var i = 0; i < _slots.Count; i++) _slots[i].Render(EntryOf(viewModel, section.Entries[i].TargetId));
        }

        // 群は同じ VM の CatalogEntries から作るので必ず引ける。引けないのは組み立ての誤り
        // Groups come from the same view model's CatalogEntries, so lookup always succeeds; a miss is a construction bug
        private static BuildMenuEntryViewModel EntryOf(BuildMenuViewModel viewModel, System.Guid targetId)
        {
            if (!viewModel.TryGetEntry(targetId, out var entry)) throw new System.InvalidOperationException($"[BuildMenu] entry {targetId} is not in the view model");
            return entry;
        }
    }
}
```

`BuildMenuCategoryList.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.Components.Decor;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// 全カテゴリ群と末尾スペーサ（Web BuildMenuCategoryList の写し）。構造が変わらなければ組み直さない
    /// All category groups plus the trailing spacer (port of the Web BuildMenuCategoryList); unchanged structure is not rebuilt
    /// </summary>
    public sealed class BuildMenuCategoryList : VisualElement
    {
        private readonly GameContentPresentation _content;
        private readonly PlacementTargetIcons _icons;
        private readonly IBuildMenuEntryHandler _handler;
        private readonly VisualElement _groupsRoot = new();
        private readonly Dictionary<Guid, VisualElement> _headings = new();
        private readonly Dictionary<Guid, VisualElement> _groups = new();
        private readonly List<(BuildMenuSection section, BuildMenuSectionView view)> _sections = new();
        // null は「まだ描いていない」。空の文字列は「群が 0 の構造」で、別の意味
        // null means "not rendered yet"; the empty string is "a structure with no groups", a different meaning
        private string _structureKey;
        private readonly Subject<Unit> _groupGeometryChanged = new();

        public VisualElement Spacer { get; } = new();

        // どれかの群の寸法が変わった（Web は群ごとに ResizeObserver。全体の高さが同じまま配分だけ変わる場合も拾う。D-9）
        // Some group's size changed (the Web observes each group; catches redistribution with an unchanged total height, D-9)
        public IObservable<Unit> OnGroupGeometryChanged => _groupGeometryChanged;

        public BuildMenuCategoryList(GameContentPresentation content, PlacementTargetIcons icons, IBuildMenuEntryHandler handler)
        {
            _content = content;
            _icons = icons;
            _handler = handler;
            AddToClassList("build-menu__list");
            name = "build-menu-sections";
            Add(_groupsRoot);
            Spacer.name = "build-menu-trailing-spacer";
            Spacer.AddToClassList("build-menu__trailing-spacer");
            Spacer.pickingMode = PickingMode.Ignore;
            Add(Spacer);
        }

        public void Render(IReadOnlyList<BuildMenuCategoryGroup> groups, BuildMenuViewModel viewModel)
        {
            // 同じ構造（群・節・エントリの並び）なら中身だけ差し替え、スクロール位置と hover を保つ
            // For the same structure (groups, sections, entry order) only swap contents, keeping scroll and hover
            var key = string.Join("|", groups.SelectMany(g => g.Sections).SelectMany(s => s.Entries.Select(e => $"{s.CategoryGuid}/{s.SubCategoryGuid}/{e.TargetId}")));
            if (key == _structureKey)
            {
                var current = groups.SelectMany(g => g.Sections).ToList();
                for (var i = 0; i < _sections.Count; i++) _sections[i].view.Render(current[i], viewModel);
                return;
            }
            _structureKey = key;
            Rebuild(groups, viewModel);
        }

        public void SetSpacerHeight(float height)
        {
            Spacer.style.minHeight = height;
        }

        public bool TryGetHeading(Guid categoryGuid, out VisualElement heading) => _headings.TryGetValue(categoryGuid, out heading);

        public bool TryGetGroup(Guid categoryGuid, out VisualElement group) => _groups.TryGetValue(categoryGuid, out group);

        private void Rebuild(IReadOnlyList<BuildMenuCategoryGroup> groups, BuildMenuViewModel viewModel)
        {
            _groupsRoot.Clear();
            _headings.Clear();
            _groups.Clear();
            _sections.Clear();

            // 群同士の間隔は 2 群目以降とスペーサの上の余白で写す（UITK に gap が無い）
            // Gaps between groups become top margins on later groups and the spacer (UITK has no gap)
            for (var i = 0; i < groups.Count; i++) _groupsRoot.Add(CreateGroup(groups[i], i > 0));
            Spacer.EnableInClassList("build-menu__trailing-spacer--following", groups.Count > 0);

            #region Internal

            VisualElement CreateGroup(BuildMenuCategoryGroup group, bool following)
            {
                var root = new VisualElement { name = $"build-menu-category-{group.CategoryGuid:D}-group" };
                root.AddToClassList("build-menu__group");
                root.RegisterCallback<GeometryChangedEvent>(_ => _groupGeometryChanged.OnNext(Unit.Default));
                root.EnableInClassList("build-menu__group--following", following);
                var heading = new LocalizedLabel { name = $"build-menu-category-heading-{group.CategoryGuid:D}" };
                heading.AddToClassList("build-menu__category-heading");
                heading.SetText(viewModel.HeadingNameOf(group.CategoryGuid));
                root.Add(heading);
                var rule = new FadeRule();
                rule.AddToClassList("build-menu__stacked");
                root.Add(rule);

                // 節は 6px の積みに、2 節目からさらに 6px を足す（Web .section + .section）
                // Sections stack with 6px, and later sections add another 6px (Web .section + .section)
                for (var s = 0; s < group.Sections.Count; s++)
                {
                    var view = new BuildMenuSectionView(group.Sections[s], viewModel, _content, _icons, _handler);
                    view.AddToClassList("build-menu__stacked");
                    view.EnableInClassList("build-menu__section--following", s > 0);
                    _sections.Add((group.Sections[s], view));
                    root.Add(view);
                }
                _headings[group.CategoryGuid] = heading;
                _groups[group.CategoryGuid] = root;
                return root;
            }

            #endregion
        }
    }
}
```

- [ ] **Step 4: サイドバーと詳細を書く**

`BuildMenuCategorySidebar.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.Components.Controls;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// 縦のカテゴリ切替（Web CategorySidebar の写し）。項目の並びが変わったときだけ ModeSwitch を作り直す
    /// Vertical category switch (port of the Web CategorySidebar); the ModeSwitch is rebuilt only when the item order changes
    /// </summary>
    public sealed class BuildMenuCategorySidebar : VisualElement
    {
        private readonly Subject<Guid> _onCategorySelected = new();
        private IReadOnlyList<Guid> _categories = Array.Empty<Guid>();
        private ModeSwitch<Guid> _switch;
        private IDisposable _switchSubscription;

        public IObservable<Guid> OnCategorySelected => _onCategorySelected;

        public BuildMenuCategorySidebar()
        {
            AddToClassList("build-menu__sidebar");
        }

        public void Render(BuildMenuShownList shown, BuildMenuViewModel viewModel)
        {
            if (!_categories.SequenceEqual(shown.SidebarCategories)) Rebuild(shown.SidebarCategories, viewModel);

            // ヒットの無いカテゴリだけ無効（Web の disabled）
            // Only categories without hits are disabled (the Web disabled)
            foreach (var category in _categories) _switch.SetOptionEnabled(category, shown.IsCategoryEnabled(category));
        }

        public void SetActive(Guid? categoryGuid)
        {
            // silent-ok: 項目を作る前の現在地は捨てる / 次の Render の後に ActiveCategory の購読がもう一度流す
            // silent-ok: an active category before items exist is dropped / the ActiveCategory subscription replays after the next Render
            if (_switch == null) return;
            if (categoryGuid is { } guid && _categories.Contains(guid)) _switch.SetSelected(guid);
            else _switch.SetSelectedNone();
        }

        private void Rebuild(IReadOnlyList<Guid> categories, BuildMenuViewModel viewModel)
        {
            _categories = categories.ToList();
            _switchSubscription?.Dispose();
            _switch?.RemoveFromHierarchy();

            // 選択肢の文は VM の見出しの名前。高さのつまみは画面のクラスで上書きする（D-26）
            // Option labels are the view model's heading names; the height knob is overridden through the screen class (D-26)
            var options = _categories.Select(guid => new ModeSwitchOption<Guid>($"build-menu-category-{guid:D}", guid, viewModel.HeadingNameOf(guid))).ToList();
            _switch = new ModeSwitch<Guid>("build-menu-sidebar", ModeSwitchOrientation.Vertical, ModeSwitchRole.Group, options);
            _switch.AddToClassList("build-menu__category-switch");
            _switchSubscription = _switch.OnOptionSelected.Subscribe(_onCategorySelected.OnNext);
            Add(_switch);
        }
    }
}
```

`BuildMenuDetailSidebar.cs`:
```csharp
using System;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.MaterialTooltip;
using Client.Ui.State.Text;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// 詳細の sticky（Web BuildMenuDetailSidebar の写し）。ヒントかエントリ 1 件を出す
    /// Sticky detail (port of the Web BuildMenuDetailSidebar); shows either the hint or one entry
    /// </summary>
    public sealed class BuildMenuDetailSidebar : VisualElement
    {
        private const int MaterialColumns = 3;
        private readonly GameContentPresentation _content;
        private readonly PlacementTargetIcons _icons;

        public BuildMenuDetailSidebar(GameContentPresentation content, PlacementTargetIcons icons)
        {
            _content = content;
            _icons = icons;
            name = "build-menu-detail";
            AddToClassList("build-menu__detail");
        }

        public void ShowHint()
        {
            Clear();
            Add(Label("build-menu__detail-hint", new UiText(LocalizationKeys.Ui.BuildMenu.DetailHint)));
        }

        public void ShowEntry(BuildMenuEntryViewModel entry)
        {
            Clear();

            // 絵 → 名前 → FadeRule → 素材 → 残り設置数（1 セット設置は Factory が Block のときだけ作る）
            // Picture, name, FadeRule, materials, remaining placements (the factory makes set placements for blocks only)
            if (_icons.TryToUiIcon(entry.Icon, out var icon)) Add(CreateIcon(icon));
            Add(Label("build-menu__detail-name", UiText.FromVerbatim(entry.Catalog.DisplayLabel)));
            Add(new FadeRule());
            switch (entry.SetPlacement)
            {
                case BuildMenuSetPlacementAvailable available:
                    AddMaterials(entry, new UiText(LocalizationKeys.Ui.BuildMenu.RequiredItemsPerSet, new[] { new LocalizationTextParam("count", available.PerCost.ToString()) }));
                    var remaining = Label("build-menu__detail-cost", new UiText(LocalizationKeys.Ui.BuildMenu.RemainingPlacementCount, new[] { new LocalizationTextParam("count", available.Remaining.ToString()) }));
                    remaining.name = "build-menu-remaining-placements";
                    Add(remaining);
                    break;
                case BuildMenuSetPlacementNone:
                    AddMaterials(entry, new UiText(LocalizationKeys.Ui.BuildMenu.RequiredItems));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry.SetPlacement), entry.SetPlacement, null);
            }

            // Web の gap 8px は 2 つ目以降の子の上の余白で写す（UITK に gap が無い）
            // The Web 8px gap becomes a top margin on every child after the first (UITK has no gap)
            for (var i = 1; i < childCount; i++) this[i].AddToClassList("build-menu__detail-stacked");
        }

        private void AddMaterials(BuildMenuEntryViewModel entry, UiText title)
        {
            // 素材の無いエントリは素材の欄を出さない（Web の条件描画と同じ正常な分岐）
            // Entries without materials show no materials section (a normal branch, same as the Web conditional render)
            if (entry.Materials.Count == 0) return;
            Add(Label("build-menu__detail-cost", title));

            // 不足は赤枠と所持/必要。免除中は赤くしない（VM が 1 回だけ判定した IsInsufficient）
            // Shortages get the red ring and owned/required; never red while payment is waived
            var grid = new SlotGrid("build-menu-detail-materials", MaterialColumns);
            for (var i = 0; i < entry.Materials.Count; i++)
            {
                var row = entry.Materials[i];
                var lacking = row.IsInsufficient;
                var tooltip = MaterialTooltipText.Create(MaterialTooltipKind.BuildMenuMaterial, _content.ItemName(row.ItemId).Resolve(), row.Item.Count, row.Item.Held);
                var slot = new ItemSlot($"build-menu-detail-material-{i}");
                slot.SetContent(new ItemSlotContent(_content.Item(row.ItemId), null, new ItemSlotMarks(false, false, lacking), new ItemSlotShortage(row.Item.Held, row.Item.Count, lacking, tooltip), null, null));
                grid.AddItem(slot);
            }
            Add(grid);
        }

        private static VisualElement CreateIcon(Client.Ui.Foundation.Icons.UiIcon icon)
        {
            var gameIcon = new GameIcon();
            gameIcon.AddToClassList("build-menu__detail-icon");
            gameIcon.SetIcon(icon, GameIconFallback.None);
            return gameIcon;
        }

        private static LocalizedLabel Label(string className, UiText text)
        {
            var label = new LocalizedLabel();
            label.AddToClassList(UiTypographyClass.TextMd);
            label.AddToClassList(className);
            label.SetText(text);
            return label;
        }
    }
}
```
（`ItemSlotContent` の `ghost`・`tooltipOverride` に null を渡せることは C3-a の実物〈`ItemSlotContent` のコメント「null 可」〉で確かめる。`GameIconFallback.None` の綴りも実物に合わせる）

- [ ] **Step 5: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.BuildMenu\."`
Expected: `ErrorCount: 0`、14 件 PASS（USS が未登録なので寸法の主張はまだ持たない。`minHeight` は inline style なので解決される）

- [ ] **Step 6: 錨の未移植の表を確かめ、コミット**

Run:
```bash
test -f moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs && grep -n "BuildMenuEntry\|build-menu" moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs
```
Expected: 何も出ないか、build menu の行が出る。出たらその行を消し、`uloop run-tests ... --filter-value "^Client\.Tests\.Ui\.Anchor\."` で `TutorialAnchorCoverageTest` が緑（印を付けたこのレーンが同じ PR で消さないと「印済みなのに未移植」で赤になる）。
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BuildMenu/Parts moorestech_client/Assets/Scripts/Client.Tests/Ui
git commit -m "feat(uitk): ビルドメニューのリスト・サイドバー・詳細の部品を足す（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---


### Task 7: 画面の View（骨・視口・クリップの逃げ・USS）と画面の L1

**Files:**
- Create: `S/Client.Ui/Screens/BuildMenu/CategoryScroll/BuildMenuScrollClipInset.cs`・`BuildMenuScrollViewport.cs`・`BuildMenuScrollBinding.cs`
- Create: `S/Client.Ui/Screens/BuildMenu/Parts/BuildMenuPanelSkeleton.cs`
- Create: `S/Client.Ui/Screens/BuildMenu/BuildMenuEntryActions.cs`・`BuildMenuView.cs`・`BuildMenu.uss`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画の末尾に `@import` 1 行。L1 の `UiTestHost` は本番の tss から USS を当てるので、画面の L1 より前に足す）
- Create: `S/Client.Tests/Ui/Screens/BuildMenu/Support/RecordingBuildMenuCommands.cs`・`BuildMenuScreenTestHost.cs`
- Test: `S/Client.Tests/Ui/Screens/BuildMenu/Views/BuildMenuViewTest.cs`・`BuildMenuSearchViewTest.cs`・`BuildMenuScrollViewTest.cs`・`BuildMenuDetailStickyViewTest.cs`・`BuildMenuLayoutTest.cs`・`BuildMenuRestoreTest.cs`

**Interfaces:**
- Consumes: Task 2〜6 の型、L0-s3 の `BuildMenuCategoryScrollTracker`・`IBuildMenuScrollViewport`・`BuildMenuSessionStore`、C3-b の `GameScrollView` の口（`GetViewportHeight`・`GetContentHeight`・`TryGetContentTop`・`OnContentLayoutChanged`）、`UiMotionPlayer`・`UiTweenAnimation`・`ScrollOffsetTweenStep`、`GamePanel`・`IconButton`・`GameTextField`・`LocalizedLabel`、`UssCustomStyleReader`、`UiCommandResultToaster`
- Produces:
  - `namespace Client.Ui.Screens.BuildMenu { public sealed class BuildMenuView : VisualElement { public BuildMenuView(IBuildMenuCommands commands, GameContentPresentation content, PlacementTargetIcons icons, UiCommandResultToaster toaster, BuildMenuSessionStore session, BuildMenuCategoryScrollTracker tracker); public void Render(BuildMenuViewModel viewModel); public void SetShown(bool shown); } }`（D-22）
  - `public sealed class BuildMenuEntryActions : IBuildMenuEntryHandler { public BuildMenuEntryActions(IBuildMenuCommands commands, UiCommandResultToaster toaster, BuildMenuSessionStore session, BuildMenuDetailSidebar detail); public void SetViewModel(BuildMenuViewModel viewModel); }`
  - `namespace Client.Ui.Screens.BuildMenu.CategoryScroll { public sealed class BuildMenuScrollViewport : IBuildMenuScrollViewport { public BuildMenuScrollViewport(GameScrollView scroll, BuildMenuCategoryList list, BuildMenuCategoryScrollTracker tracker); public void Freeze(); } public sealed class BuildMenuScrollBinding { public BuildMenuScrollBinding(GameScrollView scroll, BuildMenuCategoryList list, BuildMenuCategoryScrollTracker tracker); public void Attach(); public void Detach(); } public static class BuildMenuScrollClipInset { public static void Apply(GameScrollView scroll); } }`
  - `namespace Client.Ui.Screens.BuildMenu.Parts { public sealed class BuildMenuPanelSkeleton { public BuildMenuPanelSkeleton(BuildMenuCategorySidebar sidebar, BuildMenuCategoryList list, BuildMenuDetailSidebar detail); public VisualElement Band { get; } public IconButton CloseButton { get; } public GameTextField Search { get; } public GameScrollView Scroll { get; } public void SetNoResults(bool showsNoResults); } }`
  - testid（Web と同名）: `build-menu-panel`・`build-menu-close`・`build-menu-columns`・`build-menu-search`・`build-menu-scroll-area`・`build-menu-sections`・`build-menu-sidebar`・`build-menu-detail`。UITK だけの testid: `build-menu-frame`（`GamePanel`）・`build-menu-no-results`（Web は testid の無い span）

- [ ] **Step 1: テスト補助を書く**

`Support/RecordingBuildMenuCommands.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    // 画面から呼ばれたコマンドを記録する。削除の結果だけテストから差し替えられる。public なのは土台 BuildMenuScreenTestHost の protected プロパティの型だから（CS0053）
    // Records commands called by the screen; only the delete result can be swapped by tests
    public sealed class RecordingBuildMenuCommands : IBuildMenuCommands
    {
        private UiCommandResult _deleteResult = UiCommandAccepted.Instance;

        public List<Guid> Selected { get; } = new();
        public List<Guid> DeletedBlueprints { get; } = new();
        public int CloseCount { get; private set; }

        public void SetDeleteResult(UiCommandResult result)
        {
            _deleteResult = result;
        }

        public UiCommandResult SelectEntry(Guid targetId)
        {
            Selected.Add(targetId);
            return UiCommandAccepted.Instance;
        }

        public UniTask<UiCommandResult> DeleteBlueprint(Guid blueprintGuid)
        {
            DeletedBlueprints.Add(blueprintGuid);
            return UniTask.FromResult(_deleteResult);
        }

        public UiCommandResult Close()
        {
            CloseCount++;
            return UiCommandAccepted.Instance;
        }
    }
}
```

`Support/BuildMenuScreenTestHost.cs`:
```csharp
using System;
using System.Linq;
using Client.Tests.Ui.Content;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Screens.BuildMenu;
using Client.Ui.Screens.BuildMenu.CategoryScroll;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Session;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    /// <summary>
    /// ビルドメニュー画面の L1 の土台（View・セッション・トラッカーを組み、開閉と待ちを持つ）
    /// Build-menu screen L1 base: assembles the view, session and tracker, and owns open/close and settling
    /// </summary>
    public abstract class BuildMenuScreenTestHost : ScreenTestHost
    {
        // レイアウト・スペーサの反映・300ms の smooth scroll が収まるまで進める時間
        // Time to advance so layout, spacer application and the 300 ms smooth scroll all finish
        private const int SettleFrames = 12;
        private const int SettleFrameMilliseconds = 50;

        protected RecordingBuildMenuCommands Commands { get; private set; }
        protected BuildMenuSessionStore Session { get; private set; }
        protected BuildMenuCategoryScrollTracker Tracker { get; private set; }
        protected BuildMenuView View { get; private set; }
        protected GameContentPresentation Content { get; private set; }

        protected GameScrollView Scroll => Find<GameScrollView>(View, "build-menu-scroll-area");
        protected VisualElement Detail => Find<VisualElement>(View, "build-menu-detail");

        [SetUp]
        public void SetUpBuildMenuScreen()
        {
            // 素材の名前と絵はテスト mod から引く（前例 BuildMenuSlotTooltipTest）
            // Material names and pictures come from the test mod (precedent BuildMenuSlotTooltipTest)
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Content = new GameContentPresentation(new FakeContentTextureSource(Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture));
            Commands = new RecordingBuildMenuCommands();
            Session = new BuildMenuSessionStore();
            Tracker = new BuildMenuCategoryScrollTracker(Session);
            View = Mount(new BuildMenuView(Commands, Content, BuildMenuSamples.Icons(Content), Toaster, Session, Tracker));
        }

        // 本番の Presenter と同じ順（描いてから出す）で開く
        // Open in the production presenter's order: render, then show
        protected void OpenMenu(BuildMenuViewModel viewModel)
        {
            View.Render(viewModel);
            View.SetShown(true);
            Settle();
        }

        protected void CloseMenu()
        {
            View.SetShown(false);
            Settle();
        }

        protected void Settle()
        {
            for (var i = 0; i < SettleFrames; i++) simulate.FrameUpdateMs(SettleFrameMilliseconds);
        }

        protected void Search(string query)
        {
            Find<TextField>(View, "build-menu-search").value = query;
            Settle();
        }

        protected void Hover(VisualElement element)
        {
            simulate.MouseMove(new Vector2(1f, 1f), element.worldBound.center);
            Settle();
        }

        protected VisualElement Entry(string kind, Guid targetId) => Find<VisualElement>(View, $"build-menu-entry-{kind}-{targetId:D}");

        protected Button CategoryButton(Guid categoryGuid) => Find<Button>(View, $"build-menu-category-{categoryGuid:D}");

        protected Button[] CategoryButtons() => Find<VisualElement>(View, "build-menu-sidebar").Query<Button>().ToList().ToArray();

        // Web の heading.offsetTop（視口の内余白込み）と同じ基準の、中身の上端からの距離
        // Distance from the content top, on the same basis as the Web heading.offsetTop (viewport padding included)
        protected float HeadingTop(Guid categoryGuid)
        {
            Assert.IsTrue(Scroll.TryGetContentTop(Find<VisualElement>(View, $"build-menu-category-heading-{categoryGuid:D}"), out var top));
            return top;
        }

        protected static string TextOf(VisualElement root)
        {
            return string.Join("\n", root.Query<TextElement>().ToList().Select(text => text.text).Where(text => !string.IsNullOrEmpty(text)));
        }
    }
}
```
（`FakeContentTextureSource` の名前空間は C3-a の実物〈`S/Client.Tests/Ui/Content/`〉に合わせる。`ScreenTestHost` の `TearDown` が `rootVisualElement.Clear()` で View を外す）

- [ ] **Step 2: 失敗する画面のテストを書く**

すべて `namespace Client.Tests.Ui.Screens.BuildMenu.Views`、土台は `BuildMenuScreenTestHost`、`using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;`。Web の文言（「該当なし」「必要素材」）は L1 の言語（english）で `UiText(...).Resolve()` から組み、日本語の直書きをしない。エントリの名前（「木のチェスト」等）は VM の逐語なので言語に依らない。

`BuildMenuViewTest.cs`:
```csharp
using System.Linq;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.State.Commands;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuViewTest : BuildMenuScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">ui_stateでビルドメニューを開閉し全カテゴリのエントリが1本スクロールに載る</web-test>
        [Test]
        public void 開くと全カテゴリのエントリが1本のスクロールに載り閉じると隠れる()
        {
            OpenMenu(Create());

            Assert.IsTrue(IsShown(View));
            Assert.IsNotNull(Find<VisualElement>(View, "build-menu-panel"));
            Assert.IsTrue(Scroll.contentContainer.Contains(Entry("block", WoodChest)));
            Assert.IsTrue(Scroll.contentContainer.Contains(Entry("trainCar", CargoCar)));

            CloseMenu();
            Assert.IsFalse(IsShown(View));
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">エントリ選択とBP右クリック削除のアクション契約</web-test>
        [Test]
        public void エントリのタップは選択しBPの右押下は削除を要求する()
        {
            OpenMenu(Create());

            simulate.Click(Entry("block", WoodChest));
            simulate.Click(CategoryButton(Blueprint));
            Settle();
            simulate.Click(Entry("blueprint", StarterBase), MouseButton.RightMouse);
            Settle();

            CollectionAssert.AreEqual(new[] { WoodChest }, Commands.Selected);
            CollectionAssert.AreEqual(new[] { StarterBase }, Commands.DeletedBlueprints);
            Assert.AreEqual(0, ShownToasts.Count);
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">閉じるボタンはGameScreen遷移を要求する</web-test>
        [Test]
        public void 閉じるボタンはGameScreenへの遷移を要求する()
        {
            OpenMenu(Create());

            simulate.Click(Find<Button>(View, "build-menu-close"));

            // 遷移の結果パネルが隠れることは録画 uitk/uitk-build-menu が見る（D-27）
            // The panel hiding after the transition is checked by the uitk/uitk-build-menu recording (D-27)
            Assert.AreEqual(1, Commands.CloseCount);
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">エントリの無いカテゴリはサイドバーに出ない</web-test>
        [Test]
        public void エントリの無いカテゴリはサイドバーに出ない()
        {
            OpenMenu(Create());

            Assert.AreEqual(10, CategoryButtons().Length);
            Assert.IsNull(View.Q<Button>($"build-menu-category-{EmptyCategory:D}"));
        }

        [Test]
        public void 良性でない削除の失敗だけをトーストする()
        {
            OpenMenu(Create());
            simulate.Click(CategoryButton(Blueprint));
            Settle();

            // BP が既に無いのは良性（Web の BENIGN_ERRORS）。要求の失敗はトーストする
            // A blueprint already gone is benign (the Web BENIGN_ERRORS); a failed request is toasted
            Commands.SetDeleteResult(new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteNotFound));
            simulate.Click(Entry("blueprint", StarterBase), MouseButton.RightMouse);
            Settle();
            Assert.AreEqual(0, ShownToasts.Count);

            Commands.SetDeleteResult(new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteRequestFailed));
            simulate.Click(Entry("blueprint", StarterBase), MouseButton.RightMouse);
            Settle();
            Assert.AreEqual(1, ShownToasts.Count);
        }
    }
}
```
（`UiCommandRejected` の ctor の形は `S/Client.Ui.State/Commands/UiCommandResult.cs` の実物に合わせる）

`BuildMenuSearchViewTest.cs`:
```csharp
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuSearchViewTest : BuildMenuScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">検索は同じリストを絞り込み、ヒットの無いカテゴリだけサイドバーで無効になる</web-test>
        [Test]
        public void 検索は同じリストを絞り込みヒットの無いカテゴリだけサイドバーで無効になる()
        {
            OpenMenu(Create());

            Search("鉄");

            Assert.IsNotNull(View.Q($"build-menu-category-heading-{Logistics:D}"));
            Assert.IsNotNull(View.Q($"build-menu-section-{Transport:D}-{Sub:D}"));
            Assert.IsNull(View.Q($"build-menu-category-heading-{Blueprint:D}"));

            // 小見出しはサブカテゴリ名だけ（Web の複合見出しの廃止）
            // The section heading is the sub-category name alone (the Web dropped composite headings)
            var sectionHeading = Find<VisualElement>(View, $"build-menu-section-{Logistics:D}-{Sub:D}").Q<TextElement>(className: "build-menu__section-heading");
            StringAssert.DoesNotContain("/", sectionHeading.text);
            Assert.IsTrue(CategoryButton(Transport).enabledSelf);
            Assert.IsFalse(CategoryButton(Blueprint).enabledSelf);

            Search("");
            Assert.IsTrue(CategoryButton(Blueprint).enabledSelf);
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">検索0件は該当なし表示</web-test>
        [Test]
        public void 検索0件は該当なしをリストと入れ替えて出す()
        {
            OpenMenu(Create());

            Search("存在しないブロック");

            var noResults = Find<TextElement>(View, "build-menu-no-results");
            Assert.IsTrue(IsShown(noResults));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BuildMenu.NoResults).Resolve(), noResults.text);
            Assert.IsFalse(IsShown(Find<VisualElement>(View, "build-menu-sections")));
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">検索文字列も閉じて開き直すと復元される</web-test>
        [Test]
        public void 検索語は閉じて開き直しても戻る()
        {
            OpenMenu(Create());
            Search("鉄");
            CloseMenu();

            OpenMenu(Create());

            Assert.AreEqual("鉄", Find<TextField>(View, "build-menu-search").value);
            Assert.IsFalse(CategoryButton(Blueprint).enabledSelf);
        }

        [Test]
        public void 検索欄を空にしても検索語は空文字で保存される()
        {
            OpenMenu(Create());
            Search("鉄");

            Search("");

            // セッションへは非 null の文字列で渡す（L-5）
            // The session receives a non-null string (L-5)
            Assert.AreEqual("", Session.Current.Query);
        }
    }
}
```

`BuildMenuScrollViewTest.cs`:
```csharp
using System;
using System.Linq;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuScrollViewTest : BuildMenuScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">全カテゴリが1本スクロールに並び、サイドバー押下で見出しへジャンプしハイライトが追従する</web-test>
        [Test]
        public void サイドバーを押すと見出しへ移りハイライトが手のスクロールにも追従する()
        {
            OpenMenu(Create());
            Assert.IsTrue(Scroll.contentContainer.Contains(Find<VisualElement>(View, $"build-menu-category-heading-{Transport:D}")));
            Assert.IsTrue(Scroll.contentContainer.Contains(Entry("block", Rail)));

            simulate.Click(CategoryButton(Transport));
            Settle();

            Assert.AreEqual(HeadingTop(Transport), Scroll.scrollOffset.y, 1f);
            Assert.IsTrue(IsSelected(View, $"build-menu-category-{Transport:D}"));
            Assert.IsFalse(IsSelected(View, $"build-menu-category-{Logistics:D}"));

            // 手のスクロールでもハイライトが追従する
            // The highlight follows manual scrolling too
            Scroll.SetScrollOffset(0f);
            Settle();
            Assert.IsTrue(IsSelected(View, $"build-menu-category-{Logistics:D}"));
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">末尾カテゴリへジャンプしても見出しが視口上端に来る</web-test>
        [Test]
        public void 末尾カテゴリへジャンプしても見出しが視口の上端に来る()
        {
            OpenMenu(Create());
            var last = CategoryButtons().Last();
            var lastGuid = Guid.Parse(last.name.Replace("build-menu-category-", ""));

            simulate.Click(last);
            Settle();

            // スペーサ反映後の GeometryChangedEvent で引き直さないと手前で止まる（L-1）
            // Without re-targeting on the post-spacer GeometryChangedEvent the jump stops short (L-1)
            Assert.Greater(Tracker.SpacerHeight.Value, 0f);
            Assert.AreEqual(HeadingTop(lastGuid), Scroll.scrollOffset.y, 1f);
            Assert.IsTrue(IsSelected(View, last.name));
        }

        [Test]
        public void ジャンプ中に押下すると固定を解いて途中で止まる()
        {
            OpenMenu(Create());
            var target = HeadingTop(Transport);
            simulate.Click(CategoryButton(Transport));
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(100);

            // ホイール・押下・キーは同じ口なので押下で代表させる（D-11）
            // Wheel, press and key share one handler, so a press stands for all three (D-11)
            using (var down = PointerDownEvent.GetPooled()) Scroll.SendEvent(down);
            Settle();

            Assert.Greater(Scroll.scrollOffset.y, 0f);
            Assert.Less(Scroll.scrollOffset.y, target - 1f);
        }

        [Test]
        public void 全体の高さが同じまま群の高さの配分が変わってもハイライトを引き直す()
        {
            OpenMenu(Create());
            Scroll.SetScrollOffset(HeadingTop(Transport) + 1f);
            Settle();
            Assert.IsTrue(IsSelected(View, $"build-menu-category-{Transport:D}"));
            var contentHeight = Scroll.contentContainer.layout.height;

            // 先頭の群を伸ばし 2 つ目の群を同じだけ縮める。末尾群とスペーサは変わらず、中身の高さの通知は来ない（D-9）
            // Grow the first group and shrink the second by the same amount; the last group and spacer stay, so no content-height notification comes (D-9)
            const float shift = 40f;
            var first = Find<VisualElement>(View, $"build-menu-category-{Logistics:D}-group");
            var second = Find<VisualElement>(View, $"build-menu-category-{Transport:D}-group");
            Assert.Greater(second.layout.height, shift * 2f);
            first.style.height = first.layout.height + shift;
            second.style.height = second.layout.height - shift;
            second.style.overflow = Overflow.Hidden;
            Settle();

            // 見出しが下へずれたので、同じ位置では先頭のカテゴリが選ばれる
            // The heading moved down, so the same offset now selects the first category
            Assert.AreEqual(contentHeight, Scroll.contentContainer.layout.height, 0.5f);
            Assert.IsTrue(IsSelected(View, $"build-menu-category-{Logistics:D}"));
        }
    }
}
```
（`PointerDownEvent.GetPooled()` の送り方が実物で target を持たず届かなければ、C3-b の `UiDragDropTest` の押下の流し方〈`simulate` の押下〉を写す。押下の位置は見出しの帯〈エントリでない所〉にする）

`BuildMenuDetailStickyViewTest.cs`:
```csharp
using Client.Localization;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuDetailStickyViewTest : BuildMenuScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">詳細サイドバーはホバー後にstickyで残る</web-test>
        [Test]
        public void 詳細はホバーした後に検索欄へ移ってもそのエントリを出し続ける()
        {
            OpenMenu(Create(new[] { Material(ForUnitTestItemId.ItemId1, 2, 5) }));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BuildMenu.DetailHint).Resolve(), TextOf(Detail));

            Hover(Entry("block", WoodChest));
            StringAssert.Contains("木のチェスト", TextOf(Detail));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BuildMenu.RequiredItems).Resolve(), TextOf(Detail));

            // 検索欄へ退いても sticky のまま
            // Stays sticky after moving to the search field
            Hover(Find<TextField>(View, "build-menu-search"));
            StringAssert.Contains("木のチェスト", TextOf(Detail));
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">残り設置数は1セット複数個のエントリだけに表示される</web-test>
        [Test]
        public void 残り設置数は1セット複数個のエントリだけに出す()
        {
            OpenMenu(Create(new[] { Material(ForUnitTestItemId.ItemId1, 2, 5) }));
            var perSet = new UiText(LocalizationKeys.Ui.BuildMenu.RequiredItemsPerSet, new[] { new LocalizationTextParam("count", "3") }).Resolve();

            Hover(Entry("block", BeltConveyor));
            StringAssert.Contains("2", Find<TextElement>(View, "build-menu-remaining-placements").text);
            StringAssert.Contains(perSet, TextOf(Detail));

            Hover(Entry("block", WoodChest));
            Assert.IsNull(View.Q("build-menu-remaining-placements"));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BuildMenu.RequiredItems).Resolve(), TextOf(Detail));
            StringAssert.DoesNotContain(perSet, TextOf(Detail));
        }
    }
}
```
（`LocalizationTextParam` の名前空間は Task 6 の `BuildMenuDetailSidebar` と同じ）

`BuildMenuLayoutTest.cs`:
```csharp
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Components.Slots;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.PlacementTarget;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuLayoutTest : BuildMenuScreenTestHost
    {
        private const string TokensPath = "Assets/Scripts/Client.Ui/Foundation/Theme/Tokens.uss";
        private const float StageHeight = 720f;

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">パネルは画面水平中央に表示される</web-test>
        [Test]
        public void パネルは画面の水平中央に出る()
        {
            OpenMenu(Create());

            Assert.AreEqual(View.worldBound.center.x, Find<VisualElement>(View, "build-menu-panel").worldBound.center.x, 1f);
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">パネルは上部安全帯の直下にバンド高いっぱいで立つ</web-test>
        [Test]
        public void パネルは上の安全帯の直下に帯の高さいっぱいで立つ()
        {
            OpenMenu(Create());
            var panel = Find<VisualElement>(View, "build-menu-panel").worldBound;
            var frame = Find<VisualElement>(View, "build-menu-frame").worldBound;

            // 期待値はトークンから解く。帯の高さは Web の --menu-content-height（舞台の高さ − 上下の安全帯）
            // Expectations derive from tokens; the band height is the Web --menu-content-height (stage height minus both safe areas)
            var upper = TokenPixels("--menu-upper-safe-area");
            Assert.AreEqual(upper, panel.y - View.worldBound.y, 1f);
            Assert.AreEqual(StageHeight - upper - TokenPixels("--menu-lower-safe-area"), panel.height, 1f);
            Assert.AreEqual(TokenPixels("--build-menu-panel-width"), panel.width, 1f);
            Assert.AreEqual(panel.height, frame.height, 1f);
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">カテゴリボタンは全ボタン同一の固定高</web-test>
        [Test]
        public void カテゴリボタンは全部同じ固定の高さ()
        {
            OpenMenu(Create());
            var expected = TokenPixels("--build-menu-category-height");

            var buttons = CategoryButtons();

            Assert.AreEqual(10, buttons.Length);
            foreach (var button in buttons) Assert.AreEqual(expected, button.layout.height, 1f, button.name);
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">カテゴリサイドバーは全カテゴリとラベルを固定高のまま収める</web-test>
        [Test]
        public void カテゴリサイドバーは全カテゴリとラベルを固定の高さのまま収める()
        {
            OpenMenu(Create());

            AssertCategoryLabelsStayOneLine();
            var columns = Find<VisualElement>(View, "build-menu-columns");
            var contentBottom = columns.worldBound.yMax - columns.resolvedStyle.paddingBottom;
            Assert.LessOrEqual(CategoryButtons().Last().worldBound.yMax, contentBottom + 0.5f);
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">英語ロケールでもカテゴリ名は1行のまま固定高に収まる</web-test>
        [Test]
        public void 英語でもカテゴリ名は1行のまま固定の高さに収まる()
        {
            OpenMenu(Create());

            // 最長の英語名が出ていることを先に固定し、空振りの合格を防ぐ
            // Pin the longest English name first to avoid a vacuous pass
            StringAssert.Contains("Building Materials", TextOf(CategoryButton(BuildingMaterials)));
            AssertCategoryLabelsStayOneLine();
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">グリッドは8列を保ち中央列に収まる</web-test>
        [Test]
        public void 格子は8列を保ち中央の列に収まる()
        {
            OpenMenu(Create());
            var sections = Find<VisualElement>(View, "build-menu-sections");
            var grid = sections.Q<SlotGrid>();

            Assert.AreEqual(8, grid.Q(className: "slot-grid__tracks").childCount);

            // バーの帯の予約も固定し、8 列目にバーが重ならないことを守る
            // Pin the scrollbar reserve too so the bar never overlaps column 8
            Assert.Greater(sections.resolvedStyle.paddingRight, 0f);
            Assert.LessOrEqual(grid.layout.width, sections.contentRect.width + 0.5f);
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">スクロール領域のクリップ境界をハイライト枠の逃げぶんセルから離す</web-test>
        [Test]
        public void スクロール領域のクリップ境界をハイライト枠の逃げぶんセルから離す()
        {
            OpenMenu(Create());
            var clip = Scroll.contentViewport.worldBound;
            var cell = View.Query<VisualElement>(className: "build-menu__entry").First().worldBound;

            // 逃げはリテラルでなく関係式で見る（Web と同じ。トークンが変わっても番人であり続ける）
            // Assert the relation, not literals (same as the Web), so it stays a guard when tokens change
            var required = TokenPixels("--tutorial-anchor-padding") + TokenPixels("--tutorial-highlight-glow");
            Assert.Greater(required, 0f);
            Assert.GreaterOrEqual(cell.x - clip.x, required - 0.01f);
            Assert.GreaterOrEqual(cell.y - clip.y, required - 0.01f);
        }

        /// <web-test file="e2e/tests/regression/placementIconCentering.spec.ts">ビルドメニューのアイコンは枠の中央に描かれる</web-test>
        [Test]
        public void ビルドメニューのアイコンは枠の中央に描かれる()
        {
            OpenMenu(Create(Array.Empty<BuildMenuMaterialRow>(), new PlacementTargetBlockIcon(ForUnitTestModBlockId.BlockId)));
            var slot = Entry("block", WoodChest).worldBound;
            var icon = Entry("block", WoodChest).Q(className: "placement-target-face__icon");
            Assert.AreEqual(DisplayStyle.Flex, icon.resolvedStyle.display);

            // 描画面は絵の要素から --icon-pad の内余白を除いた箱（Web の img の padding を除いた内容箱）
            // The drawn area is the icon element minus its --icon-pad padding (the Web img content box)
            var style = icon.resolvedStyle;
            var left = icon.worldBound.xMin + style.paddingLeft - slot.xMin;
            var right = slot.xMax - (icon.worldBound.xMax - style.paddingRight);
            var top = icon.worldBound.yMin + style.paddingTop - slot.yMin;
            var bottom = slot.yMax - (icon.worldBound.yMax - style.paddingBottom);
            TestContext.WriteLine($"build-menu margins left={left} right={right} top={top} bottom={bottom}");
            Assert.Greater(left, 0f);
            Assert.AreEqual(left, right, 0.5f);
            Assert.AreEqual(top, bottom, 0.5f);
            Assert.AreEqual(left, top, 0.5f);
        }

        // 折り返すと 1 行の高さを超える。Web の scrollHeight と clientHeight の比較に当たる
        // Wrapping exceeds the one-line height; stands for the Web scrollHeight vs clientHeight check
        private void AssertCategoryLabelsStayOneLine()
        {
            foreach (var button in CategoryButtons())
            foreach (var text in button.Query<TextElement>().ToList().Where(text => !string.IsNullOrEmpty(text.text)))
            {
                var oneLine = text.MeasureTextSize("A", 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).y;
                var actual = text.MeasureTextSize(text.text, text.contentRect.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined).y;
                Assert.AreEqual(oneLine, actual, 0.5f, button.name);
                Assert.LessOrEqual(actual, button.contentRect.height + 0.5f, button.name);
            }
        }

        // 期待値はトークンの本文から読む（前例 UssCustomStyleReaderTest の ReadTokenBody）
        // Read expectations from the token body (precedent: UssCustomStyleReaderTest.ReadTokenBody)
        private static float TokenPixels(string name)
        {
            var match = Regex.Match(File.ReadAllText(TokensPath), $@"{Regex.Escape(name)}:\s*(-?[0-9.]+)px");
            Assert.IsTrue(match.Success, name);
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
```
（ホットバー側の 1 件「ホットバーのアイコンは枠の中央に描かれる」は S3-hotbar-modal が写す。このファイルが 200 行を超えたら `TokenPixels` と `AssertCategoryLabelsStayOneLine` を `Support/BuildMenuLayoutProbe.cs`〈Task 7 の時点で `Support/` 4→5、Task 10 の後は 7〉へ出す。L1 のパネルは舞台の枠を持たない 1280×720 なので、舞台の座標とパネルの座標が一致する。Web の `expectNoVerticalOverflow` は DOM の溢れで見るので、UITK は「実幅で測った高さが 1 行と同じ」で写す〈D-32〉）

`BuildMenuRestoreTest.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuRestoreTest : BuildMenuScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/buildMenuRestore.spec.ts">閉じて開き直すと検索・スクロール・詳細stickyが復元される</web-test>
        [Test]
        public void 閉じて開き直すとスクロールと詳細のstickyが戻る()
        {
            OpenMenu(Create());
            Hover(Entry("block", Rail));
            StringAssert.Contains("鉄道レール", TextOf(Detail));

            // スクロールの前にポインタを一覧の外へ逃がす（下に来た別のエントリへ詳細が移らないように。Web と同じ）
            // Park the pointer outside the list before scrolling so the detail never moves to the entry sliding under it (same as the Web)
            simulate.MouseMove(Entry("block", Rail).worldBound.center, new Vector2(1f, 1f));
            var top = HeadingTop(Transport);
            Scroll.SetScrollOffset(top);
            Settle();
            CloseMenu();
            Assert.IsFalse(IsShown(View));

            OpenMenu(Create());

            StringAssert.Contains("鉄道レール", TextOf(Detail));
            Assert.AreEqual(top, Scroll.scrollOffset.y, 0.5f);
        }

        [Test]
        public void スペーサ反映後の高さで保存位置を復元する()
        {
            // 末尾の見出しの位置はスペーサ込みでしか届かない。反映前の高さで復元すると手前へクランプされる（L-2）
            // The last heading is reachable only with the spacer; restoring on the pre-apply height clamps short (L-2)
            OpenMenu(Create());
            simulate.Click(CategoryButtons().Last());
            Settle();
            var saved = Scroll.scrollOffset.y;
            Assert.Greater(Tracker.SpacerHeight.Value, 0f);
            CloseMenu();

            OpenMenu(Create());

            Assert.AreEqual(saved, Scroll.scrollOffset.y, 0.5f);
            Assert.AreEqual(saved, Session.Current.ScrollTop, 0.5f);
        }

        [Test]
        public void 末尾の群が視口より高くても閉じて開き直すと保存位置へ戻る()
        {
            // スペーサが 0 のままの構成。閉じた後の高さ 0 を実測と取り違えると、復元を 0 で確定して保存位置まで潰す
            // A layout whose spacer stays 0; mistaking the post-hide 0 height for a measurement settles the restore at 0 and overwrites the saved position
            OpenMenu(Create());
            var lastGroup = Find<VisualElement>(View, $"build-menu-category-{CategoryButtons().Last().name.Replace("build-menu-category-", "")}-group");
            lastGroup.style.minHeight = Scroll.contentViewport.layout.height * 2f;
            Settle();
            Assert.AreEqual(0f, Tracker.SpacerHeight.Value, 0.01f);
            var saved = HeadingTop(Transport) + 10f;
            Scroll.SetScrollOffset(saved);
            Settle();
            CloseMenu();

            OpenMenu(Create());

            Assert.AreEqual(saved, Scroll.scrollOffset.y, 0.5f);
            Assert.AreEqual(saved, Session.Current.ScrollTop, 0.5f);
        }

        [Test]
        public void 開き直した視口へ要求済みのスペーサ高を当て直す()
        {
            OpenMenu(Create());
            var requested = Tracker.SpacerHeight.Value;
            Assert.Greater(requested, 0f);
            CloseMenu();

            // 閉じている間にスペーサを未適用へ戻す（要求値は変えないので購読は再通知しない）。張り替えの当て直しだけが高さを戻せる
            // Reset the spacer to unapplied while closed (the request is unchanged, so the subscription never re-fires); only the re-apply on attach can restore it
            var spacer = Find<VisualElement>(View, "build-menu-trailing-spacer");
            spacer.style.minHeight = StyleKeyword.Null;
            spacer.style.height = StyleKeyword.Null;
            simulate.FrameUpdate();
            Assert.AreEqual(requested, Tracker.SpacerHeight.Value, 0.01f);
            Assert.Less(spacer.layout.height, requested - 0.5f);

            OpenMenu(Create());

            // 新しい視口の AppliedSpacerHeight が要求値と同じ（L-4。Attach の当て直しを消すとここで赤）
            // The new viewport's applied spacer height matches the request (L-4; removing the re-apply in Attach turns this red)
            Assert.AreEqual(requested, spacer.layout.height, 0.5f);
        }

        [Test]
        public void 復元の前に流れたスクロール通知で保存位置を0へ潰さない()
        {
            // L-3 の観測: 開く間に ScrollView が流した位置を記録して結果へ出し、保存位置が保たれることを主張する（D-8）
            // L-3 observation: record the offsets the ScrollView emits while opening, print them, and assert the saved position survives (D-8)
            var emitted = new List<float>();
            Session.SetScrollTop(150f);
            using (Scroll.OnScrollOffsetChanged.Subscribe(emitted.Add))
            {
                OpenMenu(Create());
            }

            TestContext.WriteLine($"[L-3] offsets emitted while opening: {string.Join(",", emitted.Select(offset => offset.ToString("F1")))}");
            Assert.AreEqual(150f, Scroll.scrollOffset.y, 0.5f);
            Assert.AreEqual(150f, Session.Current.ScrollTop, 0.5f);
        }
    }
}
```

- [ ] **Step 3: テストが落ちることを確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildMenuView` 等が無いコンパイルエラー

- [ ] **Step 4: 視口・クリップの逃げ・張り替えを書く**

`CategoryScroll/BuildMenuScrollClipInset.cs`:
```csharp
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    /// <summary>
    /// クリップ境界をチュートリアル枠の逃げぶんセルから離す（Web の .scroll の負の margin・viewport の padding・縦バーの inset を C# で導く。[A-47] の付け替え・D-5）
    /// Keeps the clip edge away from cells by the tutorial ring's clearance (derives the Web .scroll negative margin, viewport padding and bar inset in C#; retargeted [A-47], D-5)
    /// </summary>
    public static class BuildMenuScrollClipInset
    {
        // 画面 USS が .build-menu__scroll で --tutorial-anchor-clip-inset を写して宣言する（customStyle は祖先の宣言を読まない。D-26）
        // The screen USS redeclares --tutorial-anchor-clip-inset on .build-menu__scroll (customStyle does not read ancestors; D-26)
        private const string ClipInsetProperty = "--build-menu-scroll-clip-inset";

        public static void Apply(GameScrollView scroll)
        {
            scroll.RegisterCallback<CustomStyleResolvedEvent>(_ => OnResolved(scroll));
        }

        private static void OnResolved(GameScrollView scroll)
        {
            // 読めないときは UssCustomStyleReader が LogError を出し、クリップは広げない（BuildMenuLayoutTest の逃げの主張が赤になる）
            // When unreadable, UssCustomStyleReader logs an error and the clip stays unwidened (BuildMenuLayoutTest's clearance assertion turns red)
            if (!UssCustomStyleReader.TryReadPixels(scroll.customStyle, ClipInsetProperty, out var inset)) return;

            // 外へ広げた分だけ内側を空け、格子の位置と寸法を変えない（Web: margin calc(-1 * inset)・padding inset）
            // Pad inward by what was widened outward so the grid keeps its position and size (Web: margin calc(-1 * inset), padding inset)
            var outer = scroll.style;
            outer.marginLeft = -inset;
            outer.marginTop = -inset;
            outer.marginRight = -inset;
            outer.marginBottom = -inset;
            var content = scroll.contentContainer.style;
            content.paddingLeft = inset;
            content.paddingTop = inset;
            content.paddingRight = inset;
            content.paddingBottom = inset;

            // 縦バーの溝も同じだけ詰め、つまみの位置と長さを変えない（Web の scrollbar の top・right・bottom）
            // Inset the vertical track by the same amount so the thumb keeps its position and length (the Web scrollbar top, right and bottom)
            var bar = scroll.verticalScroller.style;
            bar.top = inset;
            bar.right = inset;
            bar.bottom = inset;
        }
    }
}
```
（Web の `bottom: calc(inset + var(--sa-corner-width))` の `--sa-corner-width` は横バーが無いので 0。中身の内余白を `contentViewport` でなく `contentContainer` に付けるのは、Web の viewport の padding がスクロールする中身の側に乗るのと同じ挙動にするため〈見出しの `offsetTop` が内余白込みになる〉）

`CategoryScroll/BuildMenuScrollViewport.cs`:
```csharp
using System;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Motion.Steps;
using Client.Ui.Foundation.Motion.Tween;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.BuildMenu.Parts;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    /// <summary>
    /// GameScrollView の上の IBuildMenuScrollViewport（表示 1 回につき 1 つ。隠すときに凍らせる）
    /// IBuildMenuScrollViewport over a GameScrollView (one per showing; frozen when hidden)
    /// </summary>
    public sealed class BuildMenuScrollViewport : IBuildMenuScrollViewport
    {
        // Web の scrollTo({ behavior: "smooth" }) の代わり。長さは USS のつまみ、緩急は C#（前例 ToastHostView。LX-71。D-10）
        // Stands in for the Web scrollTo({ behavior: "smooth" }); duration is a USS knob, easing stays in C# (precedent ToastHostView; LX-71, D-10)
        private const string SmoothScrollDurationProperty = "--build-menu-smooth-scroll-duration";

        private readonly GameScrollView _scroll;
        private readonly BuildMenuCategoryList _list;
        private readonly BuildMenuCategoryScrollTracker _tracker;
        private readonly CompositeDisposable _subscriptions = new();
        private UiMotionHandle _motion;
        private bool _forwarding;
        private bool _frozen;
        private float _frozenScrollTop;

        public BuildMenuScrollViewport(GameScrollView scroll, BuildMenuCategoryList list, BuildMenuCategoryScrollTracker tracker)
        {
            _scroll = scroll;
            _list = list;
            _tracker = tracker;

            // 位置は最初の復元の後だけ流す。復元前に ScrollView が流す 0 で保存位置を潰さない（L-3。D-8）
            // Forward offsets only after the first restore so the ScrollView's pre-restore 0 never flattens the saved position (L-3, D-8)
            scroll.OnScrollOffsetChanged.Where(_ => _forwarding).Subscribe(tracker.OnScrolled).AddTo(_subscriptions);

            // 中身・視口・スペーサの寸法が変わったらトラッカーへ（L-1。D-9）
            // Content, viewport and spacer size changes go to the tracker (L-1, D-9)
            scroll.OnContentLayoutChanged.Subscribe(_ => tracker.OnLayoutChanged()).AddTo(_subscriptions);
            list.Spacer.RegisterCallback<GeometryChangedEvent>(OnSpacerGeometryChanged);

            // 各カテゴリ群の寸法の変化もトラッカーへ（Web useBuildMenuCategoryScroll の群ごとの監視。D-9）
            // Each category group's size change also goes to the tracker (the Web per-group observation; D-9)
            list.OnGroupGeometryChanged.Subscribe(_ => tracker.OnLayoutChanged()).AddTo(_subscriptions);

            // ホイール・押下・キーはジャンプの固定を解く（Web の wheel・pointerdown・keydown。D-11）
            // Wheel, press and key input release the jump pin (the Web wheel, pointerdown and keydown; D-11)
            scroll.RegisterCallback<WheelEvent>(OnUserIntervened, TrickleDown.TrickleDown);
            scroll.RegisterCallback<PointerDownEvent>(OnUserIntervened, TrickleDown.TrickleDown);
            scroll.RegisterCallback<KeyDownEvent>(OnUserIntervened, TrickleDown.TrickleDown);
        }

        public float ScrollTop => _frozen ? _frozenScrollTop : _scroll.scrollOffset.y;
        public float ClientHeight => _scroll.GetViewportHeight();

        // 初回レイアウト前の NaN も閉じた後の 0 も「未レイアウト」（NaN との比較は偽）
        // Both the pre-layout NaN and the post-hide 0 count as not laid out (comparisons with NaN are false)
        private bool IsViewportLaidOut => ClientHeight > 0f;
        public float ScrollHeight => _scroll.GetContentHeight();

        // レイアウト前の NaN は「まだ当たっていない」を 0 で表す（トラッカーは要求値と比べて反映待ちを続ける）
        // A pre-layout NaN means "not applied yet", expressed as 0 (the tracker compares it with the request and keeps waiting)
        public float AppliedSpacerHeight => float.IsNaN(_list.Spacer.layout.height) ? 0f : _list.Spacer.layout.height;

        // 今の表示でまだ視口がレイアウトされていない間（閉じた後の 0 の高さを含む）は測れないとする。0 を実測と取り違えると復元位置を 0 へ潰す
        // Unmeasurable until the viewport is laid out in this showing (including the 0 height left from hiding); mistaking 0 for a measurement flattens the restore target
        public bool TryGetHeadingTop(Guid categoryGuid, out float top)
        {
            top = 0f;
            return IsViewportLaidOut && _list.TryGetHeading(categoryGuid, out var heading) && !float.IsNaN(heading.layout.height) && _scroll.TryGetContentTop(heading, out top);
        }

        public bool TryGetGroupHeight(Guid categoryGuid, out float height)
        {
            height = 0f;
            if (!IsViewportLaidOut || !_list.TryGetGroup(categoryGuid, out var group) || float.IsNaN(group.layout.height)) return false;
            height = group.layout.height;
            return true;
        }

        public void SetScrollTopImmediately(float scrollTop)
        {
            if (WarnIfFrozen(nameof(SetScrollTopImmediately))) return;
            _motion?.Cancel();
            _scroll.SetScrollOffset(scrollTop);
            _forwarding = true;
        }

        public void ScrollSmoothlyTo(float scrollTop)
        {
            if (WarnIfFrozen(nameof(ScrollSmoothlyTo))) return;
            _motion?.Cancel();
            // 長さが読めなければ（UssCustomStyleReader が LogError を出す）動きを付けずに飛ぶ。位置は Web と同じに着く
            // If the duration is unreadable (UssCustomStyleReader logs an error), jump without motion; the position still lands as on the Web
            if (!UssCustomStyleReader.TryReadMilliseconds(_scroll.customStyle, SmoothScrollDurationProperty, out var durationMs))
            {
                SetScrollTopImmediately(scrollTop);
                return;
            }
            var from = _scroll.scrollOffset;
            var spec = new UiMotionSpec(durationMs, 0f, UiEasing.EaseOutCubic, UiMotionLoop.Once);
            _motion = UiMotionPlayer.Play(_scroll, new UiTweenAnimation(spec, new ScrollOffsetTweenStep(_scroll, from, new Vector2(from.x, scrollTop))));
        }

        // 隠す直前に位置を写し取り、以後は ScrollView から何も受けない（D-7）
        // Snapshot the position right before hiding and take nothing from the ScrollView afterwards (D-7)
        public void Freeze()
        {
            _frozenScrollTop = _scroll.scrollOffset.y;
            _frozen = true;
            _forwarding = false;
            _motion?.Cancel();
            _subscriptions.Dispose();
            _list.Spacer.UnregisterCallback<GeometryChangedEvent>(OnSpacerGeometryChanged);
            _scroll.UnregisterCallback<WheelEvent>(OnUserIntervened, TrickleDown.TrickleDown);
            _scroll.UnregisterCallback<PointerDownEvent>(OnUserIntervened, TrickleDown.TrickleDown);
            _scroll.UnregisterCallback<KeyDownEvent>(OnUserIntervened, TrickleDown.TrickleDown);
        }

        private void OnSpacerGeometryChanged(GeometryChangedEvent _)
        {
            _tracker.OnLayoutChanged();
        }

        private void OnUserIntervened(EventBase _)
        {
            _motion?.Cancel();
            _tracker.OnUserIntervened();
        }

        // 閉じた後の要求は無視し、理由を開発者ログへ出す（Presenter は閉じている間 View を描かないので通常は来ない）
        // Requests after closing are ignored with a developer log (the presenter does not render while closed, so normally none arrive)
        private bool WarnIfFrozen(string operation)
        {
            if (!_frozen) return false;
            Debug.LogWarning($"[BuildMenuScrollViewport] {operation} ignored: the viewport was frozen when the menu closed");
            return true;
        }
    }
}
```

`CategoryScroll/BuildMenuScrollBinding.cs`:
```csharp
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BuildMenu.Parts;
using UniRx;

namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    /// <summary>
    /// 表示ごとに視口を張り替え、隠すときに位置を保存して凍らせる。スペーサの要求高をリストへ当てる
    /// Swaps in a fresh viewport per showing, saves and freezes it on hide, and applies the requested spacer height to the list
    /// </summary>
    public sealed class BuildMenuScrollBinding
    {
        private readonly GameScrollView _scroll;
        private readonly BuildMenuCategoryList _list;
        private readonly BuildMenuCategoryScrollTracker _tracker;
        private BuildMenuScrollViewport _viewport;

        public BuildMenuScrollBinding(GameScrollView scroll, BuildMenuCategoryList list, BuildMenuCategoryScrollTracker tracker)
        {
            _scroll = scroll;
            _list = list;
            _tracker = tracker;
            BuildMenuScrollClipInset.Apply(scroll);

            // スペーサの要求は表示の有無に依らずリストへ当てる（D-9）
            // Apply spacer requests to the list whether or not the menu is shown (D-9)
            tracker.SpacerHeight.Subscribe(list.SetSpacerHeight);
        }

        public void Attach()
        {
            // 新しい視口へ要求済みのスペーサ高を当て直してから張る（L-4）
            // Re-apply the already requested spacer height before attaching the new viewport (L-4)
            _list.SetSpacerHeight(_tracker.SpacerHeight.Value);
            _viewport = new BuildMenuScrollViewport(_scroll, _list, _tracker);
            _tracker.AttachViewport(_viewport);
        }

        public void Detach()
        {
            // silent-ok: 一度も出していなければ凍らせる視口が無い / 次の Attach が新しい視口を張る
            // silent-ok: nothing to freeze if never shown / the next Attach installs a fresh viewport
            if (_viewport == null) return;
            _tracker.PersistScrollPosition();
            _viewport.Freeze();
        }
    }
}
```
（トラッカーは次の `AttachViewport` で古い視口の `ScrollTop`〈凍らせた値〉を保存し直すので、閉じた後の ScrollView の位置〈隠すと 0 に戻り得る〉で上書きされない）

- [ ] **Step 5: 骨・操作の口・View を書く**

`Parts/BuildMenuPanelSkeleton.cs`:
```csharp
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// パネルの骨（帯・額縁・閉じる・3 列・中央列の検索とスクロール）。Web BuildMenuPanel の JSX の並びを写す
    /// Panel skeleton (band, frame, close, three columns, the center column's search and scroll), copying the Web BuildMenuPanel JSX order
    /// </summary>
    public sealed class BuildMenuPanelSkeleton
    {
        public VisualElement Band { get; }
        public IconButton CloseButton { get; }
        public GameTextField Search { get; }
        public GameScrollView Scroll { get; }

        private readonly LocalizedLabel _noResults = new() { name = "build-menu-no-results" };
        private readonly BuildMenuCategoryList _list;

        public BuildMenuPanelSkeleton(BuildMenuCategorySidebar sidebar, BuildMenuCategoryList list, BuildMenuDetailSidebar detail)
        {
            _list = list;

            // 帯は舞台の安全帯の間で拾わず、パネルだけが拾う（Web の pointer-events: none / auto）
            // The band between the stage safe areas never picks; only the panel does (the Web pointer-events none / auto)
            Band = new VisualElement { pickingMode = PickingMode.Ignore };
            Band.AddToClassList("build-menu__band");
            var panel = new VisualElement { name = "build-menu-panel" };
            panel.AddToClassList("build-menu__panel");
            Band.Add(panel);

            var frame = new GamePanel("build-menu-frame", GamePanelFrame.Default);
            frame.AddToClassList("build-menu__frame");
            frame.SetTitle(new UiText(LocalizationKeys.Ui.BuildMenu.Title));
            panel.Add(frame);

            // 中央の列: 検索 → スクロール（該当なしとリストを入れ替える）
            // Center column: search, then the scroll (the no-results label swaps with the list)
            Search = new GameTextField("build-menu-search", false, GameTextFieldLook.GaugeInset);
            Search.AddToClassList("build-menu__search");
            Search.SetPlaceholder(new UiText(LocalizationKeys.Ui.BuildMenu.SearchPlaceholder));
            Scroll = new GameScrollView("build-menu-scroll-area");
            Scroll.AddToClassList("build-menu__scroll");
            _noResults.AddToClassList(UiTypographyClass.TextMd);
            _noResults.AddToClassList("build-menu__no-results");
            _noResults.SetText(new UiText(LocalizationKeys.Ui.BuildMenu.NoResults));
            Scroll.Add(_noResults);
            Scroll.Add(list);
            var main = new VisualElement();
            main.AddToClassList("build-menu__main");
            main.Add(Search);
            main.Add(Scroll);

            // カテゴリ | 検索+スクロール | 詳細 の 3 列。列の間は 2 列目以降の左の余白（UITK に gap が無い）
            // Three columns: categories | search + scroll | detail; gaps are left margins from the second column on (UITK has no gap)
            main.AddToClassList("build-menu__column-following");
            detail.AddToClassList("build-menu__column-following");
            var columns = new VisualElement { name = "build-menu-columns" };
            columns.AddToClassList("build-menu__columns");
            columns.Add(sidebar);
            columns.Add(main);
            columns.Add(detail);
            frame.Add(columns);

            // 閉じるは Web と同じく GamePanel の本文の子（top・right は題の下の本文から測る）。列の後に足して前面へ重ねる（Web の z-index の代わり）
            // The close button is a child of the GamePanel body as on the Web (top/right measured from the body under the title); added after the columns so it draws in front (instead of the Web z-index)
            CloseButton = new IconButton("build-menu-close", new IconButtonCloseGlyph());
            CloseButton.AddToClassList("build-menu__close");
            frame.Add(CloseButton);
            SetNoResults(false);
        }

        // 該当なしはリストと入れ替える（Web の三項。D-28）
        // The no-results label replaces the list (the Web ternary; D-28)
        public void SetNoResults(bool showsNoResults)
        {
            _noResults.style.display = showsNoResults ? DisplayStyle.Flex : DisplayStyle.None;
            _list.style.display = showsNoResults ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
```
（`GamePanel` の `contentContainer` は本文〈`.game-panel__body`。`flex-grow: 1`・`flex-basis: 0%`〉なので、列は本文の高さいっぱいに伸びる）

`BuildMenuEntryActions.cs`:
```csharp
using System;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Session;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;

namespace Client.Ui.Screens.BuildMenu
{
    /// <summary>
    /// マスの操作を型付きコマンドへ流し、結果をトーストへ、ホバーを詳細とセッションへ渡す
    /// Routes slot operations to typed commands, results to toasts, and hovers to the detail and session
    /// </summary>
    public sealed class BuildMenuEntryActions : IBuildMenuEntryHandler
    {
        // トーストのログに出す名前は Web の action 名に揃える
        // Names logged with toasts match the Web action names
        private const string SelectCommandName = "build_menu.select";
        private const string DeleteCommandName = "blueprint.delete";

        private readonly IBuildMenuCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly BuildMenuSessionStore _session;
        private readonly BuildMenuDetailSidebar _detail;
        private BuildMenuViewModel _viewModel = BuildMenuViewModel.Empty;

        public BuildMenuEntryActions(IBuildMenuCommands commands, UiCommandResultToaster toaster, BuildMenuSessionStore session, BuildMenuDetailSidebar detail)
        {
            _commands = commands;
            _toaster = toaster;
            _session = session;
            _detail = detail;
        }

        public void SetViewModel(BuildMenuViewModel viewModel)
        {
            _viewModel = viewModel;
            RefreshDetail();
        }

        public void OnEntrySelected(Guid targetId)
        {
            _toaster.Report(SelectCommandName, _commands.SelectEntry(targetId));
        }

        public void OnBlueprintDeleteRequested(Guid targetId)
        {
            DeleteBlueprintAsync(targetId).Forget();
        }

        public void OnEntryHovered(Guid targetId)
        {
            _session.SetHoveredEntry(targetId);
            RefreshDetail();
        }

        private async UniTaskVoid DeleteBlueprintAsync(Guid blueprintGuid)
        {
            var result = await _commands.DeleteBlueprint(blueprintGuid);
            _toaster.Report(DeleteCommandName, result);
        }

        // sticky: 離れても消さず、エントリが無くなったら（解放の取り消し・BP の削除）ヒントへ戻す（Web の ?? null。D-14）
        // Sticky: never cleared on leave; falls back to the hint once the entry is gone (revoked or deleted), like the Web ?? null (D-14)
        private void RefreshDetail()
        {
            if (_session.Current.HoveredEntryId is { } hovered && _viewModel.TryGetEntry(hovered, out var entry)) _detail.ShowEntry(entry);
            else _detail.ShowHint();
        }
    }
}
```

`BuildMenuView.cs`:
```csharp
using Client.Ui.Content;
using Client.Ui.Screens.BuildMenu.CategoryScroll;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Session;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu
{
    /// <summary>
    /// ビルドメニューの画面（Web BuildMenuPanel の写し）。VM と検索語から表示リストを合成して 3 列へ配る
    /// Build-menu screen (port of the Web BuildMenuPanel); composes the shown list from the view model and query and feeds the three columns
    /// </summary>
    public sealed class BuildMenuView : VisualElement
    {
        private const string CloseCommandName = "ui_state.request";

        private readonly BuildMenuSessionStore _session;
        private readonly BuildMenuCategoryScrollTracker _tracker;
        private readonly BuildMenuPanelSkeleton _skeleton;
        private readonly BuildMenuCategorySidebar _sidebar = new();
        private readonly BuildMenuCategoryList _list;
        private readonly BuildMenuEntryActions _actions;
        private readonly BuildMenuScrollBinding _scrollBinding;
        private BuildMenuViewModel _viewModel = BuildMenuViewModel.Empty;
        private string _query;
        private bool _shown;

        public BuildMenuView(IBuildMenuCommands commands, GameContentPresentation content, PlacementTargetIcons icons, UiCommandResultToaster toaster, BuildMenuSessionStore session, BuildMenuCategoryScrollTracker tracker)
        {
            _session = session;
            _tracker = tracker;
            _query = session.Current.Query;
            AddToClassList("build-menu");
            pickingMode = PickingMode.Ignore;

            // 3 列の部品を作り、骨へ並べる
            // Build the three columns' parts and lay them on the skeleton
            var detail = new BuildMenuDetailSidebar(content, icons);
            _actions = new BuildMenuEntryActions(commands, toaster, session, detail);
            _list = new BuildMenuCategoryList(content, icons, _actions);
            _skeleton = new BuildMenuPanelSkeleton(_sidebar, _list, detail);
            Add(_skeleton.Band);
            _scrollBinding = new BuildMenuScrollBinding(_skeleton.Scroll, _list, tracker);

            // 閉じる・検索・カテゴリの押下・現在地の追従
            // Close, search, category presses and following the current category
            _skeleton.CloseButton.OnClicked.Subscribe(_ => toaster.Report(CloseCommandName, commands.Close()));
            _skeleton.Search.OnValueChanged.Subscribe(OnQueryChanged);
            _sidebar.OnCategorySelected.Subscribe(tracker.JumpTo);
            tracker.ActiveCategory.Subscribe(_sidebar.SetActive);

            // 生成直後は隠し、Presenter の SetShown が出す
            // Start hidden; the presenter's SetShown reveals it
            style.display = DisplayStyle.None;
        }

        public void Render(BuildMenuViewModel viewModel)
        {
            // 開いている間の再配信でも sticky の詳細を引き直す（Web と同じ。A-57・D-19）
            // Re-resolve the sticky detail on every rebroadcast while open (same as the Web; A-57, D-19)
            _viewModel = viewModel;
            _actions.SetViewModel(viewModel);
            RenderShownList();
        }

        public void SetShown(bool shown)
        {
            // silent-ok: 同じ状態の渡し直しは冪等 / 表示は変わらない
            // silent-ok: passing the same state again is idempotent / nothing visible changes
            if (shown == _shown) return;
            _shown = shown;
            if (!shown)
            {
                // 位置を保存して視口を凍らせてから隠す（隠した ScrollView の位置は当てにならないため。D-7）
                // Save the position and freeze the viewport before hiding, since a hidden ScrollView's offset is unreliable (D-7)
                _scrollBinding.Detach();
                style.display = DisplayStyle.None;
                return;
            }

            // 開くたびにセッションの検索語を戻し、出してから新しい視口を張る（D-7）
            // On every open restore the session query, show, then attach a fresh viewport (D-7)
            _query = _session.Current.Query;
            _skeleton.Search.SetValueWithoutNotify(_query);
            style.display = DisplayStyle.Flex;
            RenderShownList();
            _scrollBinding.Attach();
        }

        private void OnQueryChanged(string query)
        {
            _query = query;
            _session.SetQuery(query);
            RenderShownList();
        }

        private void RenderShownList()
        {
            // サイドバーは絞り込み前、リストは絞り込み後の群を見る（Web と同じ）
            // The sidebar sees unfiltered groups and the list filtered ones (same as the Web)
            var shown = BuildMenuShownListComposer.Compose(_viewModel, _query);
            _sidebar.Render(shown, _viewModel);
            _list.Render(shown.Groups, _viewModel);
            _skeleton.SetNoResults(shown.ShowsNoResults);
            _tracker.SetVisibleCategories(shown.ShownCategoryGuids);
            _sidebar.SetActive(_tracker.ActiveCategory.Value);
        }
    }
}
```
（`TextField` の `evt.newValue` は null にならない〈UITK の `TextField` は空を `""` で持つ〉ので `OnQueryChanged` は補わない。L-5 は `BuildMenuSearchViewTest` が見張る。`View.Render` は閉じている間も呼べるが、本番の Presenter は開いている間だけ Model を購読する〈Task 8〉）

- [ ] **Step 6: USS を書き、tss へ足す**

`BuildMenu.uss`:
```css
/* web: moorestech_web/webui/src/features/buildMenu/style.module.css（.panelBand・.panel・.close・.columns・.sidebar・.main・.detail 系・見出し・.categoryGroup・.section・.gridArea・.trailingSpacer）と BuildMenuPanel.tsx の並び */
/* UITK に gap が無いので、Web の gap は 2 つ目以降の子の上（縦）か左（横）の余白で写す。.slotIcon・.slotLabel は Web で使われていない規則なので写さず、マスの絵の余白は --icon-pad で 4px にする（面 = --slot-size − 8px。D-5） */
/* スクロールの負の外余白・中身の内余白・縦バーの inset は BuildMenuScrollClipInset.cs が --build-menu-scroll-clip-inset から導く（[A-47]・D-5）。Web の .main の gap 8px は検索の下の余白で写し、スクロールの負の上余白と足し合わせる（D-36） */
/* Copies style.module.css of moorestech_web/webui/src/features/buildMenu (.panelBand, .panel, .close, .columns, .sidebar, .main, the .detail family, headings, .categoryGroup, .section, .gridArea, .trailingSpacer) and the BuildMenuPanel.tsx order */
/* UITK has no gap, so Web gaps become top (vertical) or left (horizontal) margins on every child after the first. .slotIcon and .slotLabel are unused in the Web and not copied; the slot picture inset is --icon-pad 4px (face = --slot-size - 8px, D-5) */
/* BuildMenuScrollClipInset.cs derives the scroll's negative margin, content padding and bar inset from --build-menu-scroll-clip-inset ([A-47], D-5); the Web .main 8px gap is a bottom margin on the search that adds up with the scroll's negative top margin (D-36) */

.build-menu {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
}

/* 舞台の上の安全帯の下から下の安全帯の上までの帯。水平中央にパネルを立てる */
/* Band from below the upper safe area to above the lower one; the panel stands centered horizontally */
.build-menu__band {
    position: absolute;
    top: var(--menu-upper-safe-area);
    bottom: var(--menu-lower-safe-area);
    left: 0;
    right: 0;
    flex-direction: row;
    justify-content: center;
}

.build-menu__panel {
    width: var(--build-menu-panel-width);
    height: 100%;
}

.build-menu__frame {
    height: 100%;
}

.build-menu__close {
    position: absolute;
    top: var(--bevel-3);
    right: var(--panel-edge-fade);
}

.build-menu__columns {
    flex-direction: row;
    flex-grow: 1;
    min-height: 0;
    padding-right: var(--build-menu-edge-safe-area);
    padding-bottom: var(--block-panel-bottom-safe-area);
}

.build-menu__column-following {
    margin-left: var(--build-menu-edge-safe-area);
}

.build-menu__sidebar {
    width: var(--build-menu-sidebar-width);
    flex-shrink: 0;
    align-self: flex-start;
}

/* ModeSwitch の高さのつまみを部品の要素に足したクラスで上書きする（D-26） */
/* Override the ModeSwitch height knob through a class added to the component element (D-26) */
.build-menu__category-switch {
    --mode-switch-option-height: var(--build-menu-category-height);
}

.build-menu__main {
    flex-grow: 1;
    flex-shrink: 1;
    flex-basis: 0;
    min-width: 0;
    min-height: 0;
}

.build-menu__search {
    margin-bottom: 8px;
}

.build-menu__scroll {
    --build-menu-scroll-clip-inset: var(--tutorial-anchor-clip-inset);
    --build-menu-smooth-scroll-duration: 300ms; /* look-exception: LX-71。Web は scrollTo smooth の長さをブラウザが決める。300ms を固定した（D-10） */
}

.build-menu__list {
    padding-right: var(--build-menu-grid-scrollbar-reserve);
}

.build-menu__group--following {
    margin-top: var(--build-menu-category-gap);
}

.build-menu__trailing-spacer {
    flex-shrink: 0;
}

.build-menu__trailing-spacer--following {
    margin-top: var(--build-menu-category-gap);
}

.build-menu__category-heading {
    color: var(--text-default);
    font-size: var(--label-face-font-size);
    margin: 0;
    /* Web は本文の line-height 1.55 を継ぐ（14px × 1.55 = 21.7px）。UITK は USS に行の高さが無いので min-height で写す（前例 Typography.uss） */
    /* The Web inherits the body line-height 1.55 (14px x 1.55 = 21.7px); USS has no line-height, so min-height carries it (precedent Typography.uss) */
    min-height: 21.7px;
    --localized-label-line-height: 21.7px;
}

.build-menu__section-heading {
    color: var(--text-muted);
    font-size: 12px;
    margin: 0;
    /* 12px × 1.55 = 18.6px */
    min-height: 18.6px;
    --localized-label-line-height: 18.6px;
}

/* 見出し → FadeRule → 節（格子）の 6px の積み。2 節目からはさらに 6px（Web .section + .section） */
/* 6px stacking for heading, FadeRule and sections (grid); later sections add another 6px (Web .section + .section) */
.build-menu__stacked {
    margin-top: 6px;
}

.build-menu__section--following {
    margin-top: 12px;
}

.build-menu__entry {
    --icon-pad: 4px;
}

.build-menu__detail {
    width: var(--build-menu-detail-width);
    flex-shrink: 0;
    min-height: 0;
    padding-top: var(--build-menu-detail-top-safe-area);
}

.build-menu__detail-stacked {
    margin-top: 8px;
}

.build-menu__detail-icon {
    width: var(--slot-size);
    height: var(--slot-size);
}

/* 詳細の文と該当なしは Web の本文（継承の 16px・行の高さ 1.55）。大きさ・1 行の高さ・折り返しは C# が付ける UiTypographyClass.TextMd（.text--md）が持ち、ここは 2 行目以降の行送りだけ（前例 .mode-hud__detail） */
/* Detail text and no-results use the Web body text (inherited 16px, line-height 1.55); size, one-line height and wrapping come from UiTypographyClass.TextMd (.text--md) added in C#, and this only sets the pitch of later lines (precedent .mode-hud__detail) */
.build-menu__detail-name,
.build-menu__detail-hint,
.build-menu__detail-cost,
.build-menu__no-results {
    --localized-label-line-height: 24.8px;
}

.build-menu__detail-name {
    color: var(--text-default);
}

.build-menu__detail-hint,
.build-menu__detail-cost,
.build-menu__no-results {
    color: var(--text-muted);
}
```
（`.build-menu__section--following` は `.build-menu__stacked` より後に置く〈同じ詳細度で後勝ち〉。部品のクラスは書かない。`UiStyleConventionTest` が `z-index`・構造擬似クラス・部品のクラス・色の直書きを見張る）

`GameUiTheme.tss` の `/* == section: Screens == */` の区画の末尾（`CursorTooltip.uss` の行の下）に 1 行:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BuildMenu/BuildMenu.uss");
```

- [ ] **Step 7: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`（`$FILTER` は Global Constraints の値）
Expected: `ErrorCount: 0`。`Views` の 42 件（Task 5 の 9・Task 6 の 5・この Task の 28）・L0-s3 の `CategoryScroll` 系・State の BuildMenu・L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`・`TutorialAnchorCoverageTest`）が全件 PASS。`BuildMenuRestoreTest` の「復元の前に流れた…」の出力行 `[L-3] offsets emitted while opening: …` を TestResults の xml から写して `done.md` の「## 証拠」に書く（L1 の観測。実機は Task 8 Step 4）。新しいファイルが 200 行未満で、`ls` で `Screens/BuildMenu/` 4・`CategoryScroll/` 7・`Parts/` 9・`Tests/.../Views/` 10・`Support/` 4（Probe を出したら 5。Task 10 で +2）を確かめる

落ちたときの見方（推測で直さず、値を出してから直す）:
- 末尾ジャンプが手前で止まる → `Tracker.SpacerHeight.Value` と `Find(View, "build-menu-trailing-spacer").layout.height` と `Scroll.GetContentHeight()` を `TestContext.WriteLine` で出す。スペーサの `GeometryChangedEvent` が来ていなければ D-9 の経路の欠け
- 復元が 0 になる → 「復元の前に…」の出力行で 0 が流れていれば転送の門（`_forwarding`）の欠け、流れていなければ保存の経路（`Detach` の順）
- 逃げの主張が赤 → `Scroll.customStyle` に `--build-menu-scroll-clip-inset` が載っているか（USS の宣言が `.build-menu__scroll` にあるか）

- [ ] **Step 8: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BuildMenu moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): ビルドメニューの View・視口・USS と画面の L1 を足す（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 8: Presenter・登録と PlayMode での疎通（全体スイッチ UITK）・L-3 の実測

**Files:**
- Create: `S/Client.Ui/Screens/BuildMenu/BuildMenuScreenPresenter.cs`
- Create: `S/Client.Starter/Registration/GameUi/BuildMenuGameUiRegistration.cs`
- Modify: `S/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画の末尾に 1 行）
- Modify: `S/Client.Starter/Registration/MainGameInteractionRegistration.cs`（`ClientBlueprintLibrary` の行に `.As<IBlueprintDeleteService>()` を 1 つ）
- Modify: `S/Client.Starter/Registration/GameUi/ContentGameUiRegistration.cs`（末尾に `PlacementTargetIcons` の登録 1 行。C3-a のファイルへの追記）
- Modify: `docs/uitk/TODO.md`（「## 実測の結論」の末尾に 1 行）

**Interfaces:**
- Consumes: Task 3 の `BuildMenuModel`、Task 4 の `BuildMenuCommands`、Task 7 の `BuildMenuView`、前例 `PauseMenuScreenPresenter`・`PauseMenuGameUiRegistration`・`MainGameInteractionRegistration` の `ClientBlueprintLibrary` の行
- Produces: `public sealed class BuildMenuScreenPresenter : IInitializable, IDisposable { public BuildMenuScreenPresenter(GameUiRoot gameUiRoot, UiRoutingState routingState, BuildMenuModel model, IBuildMenuCommands commands, GameContentPresentation content, PlacementTargetIcons icons, UiCommandResultToaster toaster, BuildMenuSessionStore session); }`。全体スイッチ UITK の起動で B のビルドメニューが UITK で出る

- [ ] **Step 1: Presenter を書く**

`BuildMenuScreenPresenter.cs`:
```csharp
using System;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.BuildMenu.CategoryScroll;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.BuildMenu.Session;
using Client.Ui.State.BuildMenu.Source;
using Client.Ui.State.Routing;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.BuildMenu
{
    /// <summary>
    /// ビルドメニューの View を起動時に 1 回だけ載せ、画面ルータで出し入れし、出ている間だけ Model を購読する
    /// Mounts the build-menu view once at startup, shows or hides it on the screen router, and subscribes to the model only while shown
    /// </summary>
    public sealed class BuildMenuScreenPresenter : IInitializable, IDisposable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly UiRoutingState _routingState;
        private readonly BuildMenuModel _model;
        private readonly IBuildMenuCommands _commands;
        private readonly GameContentPresentation _content;
        private readonly PlacementTargetIcons _icons;
        private readonly UiCommandResultToaster _toaster;
        private readonly BuildMenuSessionStore _session;
        private readonly CompositeDisposable _subscriptions = new();
        private IDisposable _modelSubscription;
        private BuildMenuView _view;

        public BuildMenuScreenPresenter(GameUiRoot gameUiRoot, UiRoutingState routingState, BuildMenuModel model, IBuildMenuCommands commands, GameContentPresentation content, PlacementTargetIcons icons, UiCommandResultToaster toaster, BuildMenuSessionStore session)
        {
            _gameUiRoot = gameUiRoot;
            _routingState = routingState;
            _model = model;
            _commands = commands;
            _content = content;
            _icons = icons;
            _toaster = toaster;
            _session = session;
        }

        public void Initialize()
        {
            // 載せるのはここで 1 回だけ。トラッカーは View と同じ寿命（D-22）
            // Mount exactly once here; the tracker lives as long as the view (D-22)
            var tracker = new BuildMenuCategoryScrollTracker(_session);
            _view = new BuildMenuView(_commands, _content, _icons, _toaster, _session, tracker);
            _gameUiRoot.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(_view);
            _routingState.Current.Select(snapshot => snapshot.Screen == UiScreen.BuildMenu).DistinctUntilChanged().Subscribe(SetShown).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _modelSubscription?.Dispose();
            _view?.SetShown(false);
            _view?.RemoveFromHierarchy();
        }

        private void SetShown(bool shown)
        {
            // 出ている間だけ購読する。開いた直後は前回の VM を 1 回描き、同じフレーム末の組み直しで最新に描き直す（D-19）
            // Subscribe only while shown; on open it renders the previous model once, then the same frame's end-of-frame rebuild redraws the latest (D-19)
            _modelSubscription?.Dispose();
            _modelSubscription = shown ? _model.Current.Subscribe(_view.Render) : null;
            _view.SetShown(shown);
        }
    }
}
```

- [ ] **Step 2: 登録を書く**

`BuildMenuGameUiRegistration.cs`:
```csharp
using Client.Ui.Content;
using Client.Ui.Screens.BuildMenu;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.BuildMenu.Session;
using Client.Ui.State.BuildMenu.Source;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // ビルドメニュー（GameUi）の登録。Model・コマンド・セッションは GameUi の系だけが使うので画面と一緒にここで登録する（Web の系は橋が同じ規則で動く）
    // Registers the GameUi build menu; only the GameUi system uses the model, commands and session, so they are registered here with the screen (the Web system runs the bridge with the same rules)
    internal static class BuildMenuGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // セッションはメニューを閉じても残す（Web の module 変数と同じ寿命）
            // The session outlives each close (same lifetime as the Web module variable)
            builder.Register<BuildMenuSessionStore>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BuildMenuModel>().AsSelf();

            // IBlueprintDeleteService は MainGameInteractionRegistration の As で、PlacementTargetIcons は ContentGameUiRegistration で引ける（D-35・D-42）
            // IBlueprintDeleteService comes from the As in MainGameInteractionRegistration and PlacementTargetIcons from ContentGameUiRegistration (D-35, D-42)
            builder.Register<BuildMenuCommands>(Lifetime.Singleton).As<IBuildMenuCommands>();
            builder.RegisterEntryPoint<BuildMenuScreenPresenter>();
        }
    }
}
```

`ContentGameUiRegistration.cs` の末尾（`GameContentPresentation` の行の下）に 1 行（C3-a のファイルへの追記。Content 層の登録の置き場）:
```csharp
            builder.Register<PlacementTargetIcons>(Lifetime.Singleton);
```

`MainGameInteractionRegistration.cs` の `ClientBlueprintLibrary` の行（実物の 112 行目付近）に `.As<IBlueprintDeleteService>()` を 1 つ足す（共有ファイルの 1 行の追記。所有は File Structure）:
```csharp
            builder.Register<ClientBlueprintLibrary>(Lifetime.Singleton).AsSelf().As<IBlueprintLookup>().As<IBlueprintDeleteService>();
```

`MainGameUiRegistration.cs` の画面登録区画の末尾（`CursorTooltipGameUiRegistration.Register(builder);` の下。並走レーンの行があればその下）に 1 行:
```csharp
            BuildMenuGameUiRegistration.Register(builder);
```
（`BuildMenuModel` の `Current` は ctor で作られるので、Presenter の `Initialize` が Model の `Initialize` より先に走っても使える〈前例 S2-mode-tooltip〉。Model の `IInitializable` は購読の開始と開いたときの `ClientBlueprintLibrary.Refresh`〈Task 3〉）

- [ ] **Step 3: コンパイル・L1・L2 を通し、全体スイッチ UITK で PlayMode に入る**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
uloop control-play-mode --project-path ./moorestech_client --action Play
uloop get-logs --project-path ./moorestech_client --log-type Error
```
Expected: `ErrorCount: 0`・全件 PASS・`GameUi`。PlayMode がゲーム画面まで進んだ後に Error 0 件（`BuildMenuScreenPresenter`・`BuildMenuModel`・`IBlueprintDeleteService` の解決失敗〈VContainer の例外〉が無い）。テストの TearDown は全体スイッチ〈SessionState〉を戻さないので、テストを流した後は必ず 3 行目で `GameUi` を確かめてから PlayMode に入る

- [ ] **Step 4: 開いて閉じて開き直し、復元前に流れる位置を実機で測る（L-3）**

開幕スキットを飛ばしてから（録画の `p.SkipOpeningSkit()` と同じ。手で飛ばすなら ESC）、B でメニューを開く。続けて:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var layer = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetLayer(Client.Ui.Foundation.Layers.UiLayer.Screen, Client.Ui.Foundation.Stage.UiFamily.Stage);
var scroll = layer.Q<Client.Ui.Components.Controls.GameScrollView>("build-menu-scroll-area");
scroll.SetScrollOffset(200f);
UniRx.ObservableExtensions.Subscribe(scroll.OnScrollOffsetChanged, offset => UnityEngine.Debug.Log($"[L-3] offset={offset:F1} frame={UnityEngine.Time.frameCount}"));
return $"shown={scroll.resolvedStyle.display} offset={scroll.scrollOffset.y}";'
```
Expected: `offset=200`（届かなければ届く最大値。その値を以後の期待値にする）。B でメニューを閉じ、もう一度 B で開いて 1 秒待ち:
```bash
uloop get-logs --project-path ./moorestech_client --log-type Log --search-text "[L-3]"
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var layer = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetLayer(Client.Ui.Foundation.Layers.UiLayer.Screen, Client.Ui.Foundation.Stage.UiFamily.Stage);
var scroll = layer.Q<Client.Ui.Components.Controls.GameScrollView>("build-menu-scroll-area");
var session = Client.Game.InGame.Context.ClientDIContext.DIContainer.DIContainerResolver.Resolve<Client.Ui.State.BuildMenu.Session.BuildMenuSessionStore>();
return $"offset={scroll.scrollOffset.y} saved={session.Current.ScrollTop}";'
```
Expected: 再オープン後の `offset` と `saved` が閉じる前の値（±1）。ログの `[L-3]` の行に、閉じたとき・開いたときに流れた値（0 を含むか・何フレーム目か）が並ぶ。0 が流れていても `saved` が保たれていれば、D-8 の転送の門が効いている（Web と同じく潰れる挙動は UITK では起きない）。`uloop get-logs` の `--search-text` が無ければ `--log-type Log` の出力を `grep "\[L-3\]"` で絞る。`saved` が 0 なら止め、`done.md` の「要裁定」に値を書いて指揮へ返す（D-8 の前提が崩れたため）

分岐（N3）: 実測で、開くときに `OnScrollOffsetChanged(0)` が流れなかった、または流れても転送の門を外した状態で `saved` が潰れなかったと分かったら、門は要らない。そのときは `BuildMenuScrollViewport` から `_forwarding` を外して受動の転送（`OnScrollOffsetChanged.Subscribe(tracker.OnScrolled)`）にし、`BuildMenuRestoreTest` の 4 件目が緑のままであることを `$FILTER` で確かめて同じコミットに入れる。判断記録 D-8 の行に「実測で不要と分かり外した（`[L-3]` の値）」と書き足す。門を外した状態は、EDC で `_forwarding` を常に真にした視口を張り替えるのではなく、コードを変えて再コンパイルし Step 4 を流し直して確かめる

- [ ] **Step 5: 台帳の「## 実測の結論」へ書く**

`docs/uitk/TODO.md` の「## 実測の結論」の表の末尾に 1 行（列は既存の行に合わせる）:
```markdown
| ビルドメニューの復元前の位置通知 | 閉じて開き直したとき、復元より前に ScrollView が `OnScrollOffsetChanged(0)` を流すか・保存位置が潰れるか（L0-s3 の L-3） | S3-buildmenu | <Step 4 の `[L-3]` の行と `offset`・`saved` の値。例: 開いた直後に 0 が 1 回流れるが、転送の門（最初の復元まで流さない）で saved=200 のまま> |
```

- [ ] **Step 6: PlayMode を止めてコミット**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BuildMenu moorestech_client/Assets/Scripts/Client.Starter/Registration docs/uitk/TODO.md
git commit -m "feat(uitk): ビルドメニューを GameUi の系へ登録し復元前の位置通知を実測する（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 9: Web テストの名前の突き合わせ

**Files:**
- なし（写さないテストが無いので `docs/uitk/disposition.md` は変えない。`placementIconCentering.spec.ts` のホットバー側の 1 件は S3-hotbar-modal の持ち物）

**Interfaces:**
- Consumes: Task 2〜7 の `<web-test>`、`tools/uitk/test-name-parity.mjs`
- Produces: 束 3 の `buildMenu.spec.ts`・`buildMenuLayout.spec.ts`・`views/BuildMenuCategoryList.test.ts`・`views/BuildMenuDetailSidebar.test.ts`・`views/BuildMenuSlot.test.ts` と、束 5 の `buildMenuRestore.spec.ts`（D-23）の未対応 0

- [ ] **Step 1: 名前を突き合わせる**

Run:
```bash
node tools/uitk/test-name-parity.mjs --bundle 3 --emit-missing > moorestech_client/.uloop/outputs/s3bm-parity-3.txt; echo "exit3=$?"
node tools/uitk/test-name-parity.mjs --bundle 5 --emit-missing > moorestech_client/.uloop/outputs/s3bm-parity-5.txt; echo "exit5=$?"
grep -E "regression/buildMenu(Layout)?\.spec\.ts|features/buildMenu/views/" moorestech_client/.uloop/outputs/s3bm-parity-3.txt || true
grep -E "regression/(buildMenuRestore|placementIconCentering)\.spec\.ts" moorestech_client/.uloop/outputs/s3bm-parity-5.txt | grep -v "ホットバー" || true
```
Expected: `exit3`・`exit5` が 0 か 1（2 は実行不能・書式・整合の誤りで、出たら grep の結果を信じず止めて直す）。そのうえで 3・4 行目の grep に何も出ない（P20・C9 の全件が `<web-test>` で対応。`placementIconCentering.spec.ts` は台帳で `e2e/` を持つ行の束 5 に数える〈名指しの行が無い〉。ホットバー側の 1 件「ホットバーのアイコンは枠の中央に描かれる」は S3-hotbar-modal の持ち物なので束 5 に残ってよい）。終了コード 1 は他レーンの未対応が残るためでよい（束の差分 0 は ladder が締める）。2 なら `<web-test>` の綴り（`file` のパス・テスト名の全角/半角・読点）を Web の実物と突き合わせて直す。`buildMenuRestore.spec.ts` が束 5 でなく束 3 に出たら、台帳「## 行」の持ち主の束の読み違いなので D-23 を直し、`done.md` の「未確認」に書く

- [ ] **Step 2: 直しがあればコミット**

```bash
uloop compile --project-path ./moorestech_client
git add moorestech_client/Assets/Scripts/Client.Tests/Ui
git commit -m "test(uitk): ビルドメニューの Web テスト名の綴りを突き合わせに合わせる（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
（直しが無ければコミットしない）

---

### Task 10: Web との並べ比べ（1280×720・2560×1080）

**Files:**
- なし（撮影の原本は `moorestech_client/.uloop/outputs/compare/S3-buildmenu/`。差の表は `done.md`）。直しが出たら `BuildMenu.uss` と、必要なら台帳「## 見た目の例外」の末尾（`LX-71`〜`LX-75`）

**Interfaces:**
- Consumes: 参照 `LOGS/uitk/reference-captures/web-869b2d4264/3-build-menu.png`（1280×720・日本語・mock-host の fixture）・`web-0e81bbe3c3-2560x1080/3-build-menu.png`、前例 S2-mode-tooltip Task 10 の撮影の口（`GameUiPanelSettings.asset` の複製＋RenderTexture）と S2-notification の `UiLayerStack.Build(document.rootVisualElement)`
- Produces: 参照と同じ内容の DEMO の VM で撮った 1 状態 × 2 解像度の組と差の表、別の確認としての実ゲームの VM の撮影
- Create: `S/Client.Tests/Ui/Screens/BuildMenu/Support/BuildMenuReferenceDemo.cs`・`BuildMenuReferenceDemoTextures.cs`（`Support/` 4→6）、`tools/uitk/render-mock-placeholder-icons.mjs`（mock-host の `placeholderIcon` を PNG にする撮影の道具）

- [ ] **Step 1: 撮影の口を用意する（PlayMode・GameUi・日本語・複製した PanelSettings の RenderTexture）**

Task 8 Step 3 と同じく `GameUi` を確かめて PlayMode に入り、開幕スキットを飛ばす。参照の png を `Read` して言語を確かめる（`3-build-menu` は日本語）。S2-mode-tooltip Task 10 Step 2 の EDC を、GameObject 名を `s3bm-compare-1280`・`s3bm-compare-2560`、RT 名を `s3bm-rt-<w>` に替えて流す（言語 `japanese`・ルートに CJK のロケールのクラス）。色は複製のランタイムの PanelSettings で撮った RT で比べ、ギャラリーの撮影（sRGB の合成で半透明が淡く出る）で比べない（申し送りの罠 b。D-31）

- [ ] **Step 2: 参照と同じ内容の VM を作る（mock-host の DEMO の写し）**

参照 `3-build-menu.png` は `moorestech_web/webui/e2e/uitk-reference/referenceShots.ts` の `3-build-menu`（`ui("BuildMenu")` だけ）で、中身は mock-host の `e2e/mock-host/fixtures/buildMenuFixtures.ts` の `buildMenu` と `contentLocalizationFixtures.ts` の日本語の名前である。中身の差で見た目の比較を逃がさないよう、同じカテゴリ・エントリ・名前・選択状態（何も押していない＝先頭の物流が選択、詳細はヒント、検索は空）を VM に写して撮る。下のコードは Task 10 の時点の実物を `Read` して突き合わせ、違えば実物に合わせる（とくに 80 件の量産エントリの名前の出方は参照の png を見て合わせる）。絵は参照と同じものを出す: 参照は mock-host の DEMO モードで撮られ、ブロックの絵（`blockIconUrl(n)`）は実アセットが無いので `e2e/mock-host/assets/demoAssets.ts` の `placeholderIcon(n)`（色相 `(n*47)%360` の 64×64 の SVG。`iconEndpoints.ts` の `serveGameIcon`）が返る。fixture の `iconUrl` の番号はそのまま種になる: 木のチェスト 1・鉄のチェスト 2・ベルトコンベア 3・鉄道レール 4・貨物車両 5・電線接続ツール 6・量産 `8 + (i % 12)`・追加 7 カテゴリ 1。`iconUrl` の無い BP コピーと `starter-base` は名前で描く（`NameOnly`）。VM の絵は種 n を `PlacementTargetBlockIcon(new BlockId(n))`（車両は `PlacementTargetTrainCarIcon`、接続ツールは `PlacementTargetConnectToolIcon`）にし、絵そのものは Step 2b の PNG を返す撮影専用のテクスチャ源 `BuildMenuReferenceDemoTextures` で `PlacementTargetIcons` を組んで出す

`Support/BuildMenuReferenceDemo.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Text;
using Core.Master;
using Game.PlacementTarget;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    /// <summary>
    /// 参照キャプチャ 3-build-menu の中身（mock-host の buildMenuFixtures と日本語の名前）を写した VM。並べ比べの撮影だけが使う
    /// View model copying the 3-build-menu reference content (mock-host buildMenuFixtures and Japanese names); used only by the comparison capture
    /// </summary>
    public static class BuildMenuReferenceDemo
    {
        private static Guid G(string text) => Guid.Parse(text);

        public static BuildMenuViewModel Create()
        {
            // カテゴリとサブカテゴリ（fixtures の categories の順。建材は空のカテゴリ）
            // Categories and sub-categories in fixture order; 建材 is the empty category
            var layout = new (string category, string name, (string sub, string subName)[] subs)[]
            {
                ("51000000-0000-4000-8000-000000000001", "物流", new[] { ("52000000-0000-4000-8000-000000000001", "チェスト"), ("52000000-0000-4000-8000-000000000002", "電気コンベア") }),
                ("51000000-0000-4000-8000-000000000002", "輸送", new[] { ("52000000-0000-4000-8000-000000000003", "鉄道"), ("52000000-0000-4000-8000-000000000004", "車両") }),
                ("51000000-0000-4000-8000-000000000003", "ブループリント", new[] { ("52000000-0000-4000-8000-000000000005", "保存済み") }),
                ("51000000-0000-4000-8000-000000000004", "建材", new[] { ("52000000-0000-4000-8000-000000000006", "土台") }),
                ("51000000-0000-4000-8000-000000000005", "採掘", new[] { ("52000000-0000-4000-8000-000000000007", "採掘機") }),
                ("51000000-0000-4000-8000-000000000006", "生産", new[] { ("52000000-0000-4000-8000-000000000008", "原始加工") }),
                ("51000000-0000-4000-8000-000000000007", "動力", new[] { ("52000000-0000-4000-8000-000000000009", "シャフト") }),
                ("51000000-0000-4000-8000-000000000008", "電力", new[] { ("52000000-0000-4000-8000-000000000010", "発電") }),
                ("51000000-0000-4000-8000-000000000009", "液体", new[] { ("52000000-0000-4000-8000-000000000011", "パイプ") }),
                ("51000000-0000-4000-8000-000000000010", "ツール", new[] { ("52000000-0000-4000-8000-000000000012", "接続") }),
                ("51000000-0000-4000-8000-000000000011", "建築マテリアル", new[] { ("52000000-0000-4000-8000-000000000013", "内装パネル") }),
            };
            var categories = layout.Select(c => new BuildMenuCategory(G(c.category), c.subs.Select(sub => G(sub.sub)).ToList())).ToList();
            var names = new Dictionary<Guid, UiText>();
            foreach (var c in layout)
            {
                names[G(c.category)] = UiText.FromVerbatim(c.name);
                foreach (var sub in c.subs) names[G(sub.sub)] = UiText.FromVerbatim(sub.subName);
            }

            // エントリ（fixtures の entries の順。量産 80 件は輸送/車両、追加 7 カテゴリは 1 件ずつ）
            // Entries in fixture order: 80 fillers under 輸送/車両 and one entry per extra category
            var entries = new List<BuildMenuEntryViewModel>
            {
                Entry("53000000-0000-4000-8000-000000000001", PlacementTargetKind.Block, "01", "01", "木のチェスト", Rows(4, 4), false, BuildMenuSetPlacementNone.Instance, Seed(1)),
                Entry("53000000-0000-4000-8000-000000000002", PlacementTargetKind.Block, "01", "01", "鉄のチェスト", Rows(), false, BuildMenuSetPlacementNone.Instance, Seed(2)),
                Entry("53000000-0000-4000-8000-000000000003", PlacementTargetKind.Block, "01", "02", "ベルトコンベア", Rows(1, 1), true, new BuildMenuSetPlacementAvailable(3, 2), Seed(3)),
                Entry("53000000-0000-4000-8000-000000000004", PlacementTargetKind.Block, "02", "03", "鉄道レール", Rows(), false, BuildMenuSetPlacementNone.Instance, Seed(4)),
                Entry("56000000-0000-4000-8000-000000000001", PlacementTargetKind.TrainCar, "02", "04", "貨物車両", Rows(), false, BuildMenuSetPlacementNone.Instance, new PlacementTargetTrainCarIcon(G("56000000-0000-4000-8000-000000000001"))),
                Entry("55000000-0000-4000-8000-000000000001", PlacementTargetKind.ConnectTool, "02", "03", "電線接続ツール", Rows(), false, BuildMenuSetPlacementNone.Instance, new PlacementTargetConnectToolIcon(G("55000000-0000-4000-8000-000000000001"))),
                Entry("58000000-0000-4000-8000-000000000001", PlacementTargetKind.BlueprintCopy, "03", "05", "ブループリントコピー", Rows(), false, BuildMenuSetPlacementNone.Instance, PlacementTargetNameOnlyIcon.Instance),
                Entry("3f6a9c1e-8b2d-4f7a-9e3c-1a2b3c4d5e6f", PlacementTargetKind.Blueprint, "03", "05", "starter-base", Rows(), false, BuildMenuSetPlacementNone.Instance, PlacementTargetNameOnlyIcon.Instance),
            };
            for (var i = 0; i < 80; i++) entries.Add(Entry($"53000000-0000-4000-8000-0000000010{i:D2}", PlacementTargetKind.Block, "02", "04", FillerLabel(i), Rows(), false, BuildMenuSetPlacementNone.Instance, Seed(8 + i % 12)));
            var extras = new[] { ("05", "07", "05", "電動採掘機"), ("06", "08", "06", "石の加工台"), ("07", "09", "07", "動力シャフト"), ("08", "10", "08", "石炭発電機"), ("09", "11", "09", "銅パイプ"), ("10", "12", "10", "電線接続具"), ("11", "13", "11", "装飾パネル") };
            foreach (var (category, sub, entry, label) in extras) entries.Add(Entry($"53000000-0000-4000-8000-0000000000{entry}", PlacementTargetKind.Block, category, sub, label, Rows(2, 2), false, BuildMenuSetPlacementNone.Instance, Seed(1)));
            return new BuildMenuViewModel(categories, names, entries);
        }

        // 量産エントリの名前は mock-host が名前の無いブロックに出す表示名（Step 2 の冒頭で実物を読んで合わせる）
        // Filler names are what mock-host shows for unnamed blocks (match the real thing at the start of Step 2)
        private static string FillerLabel(int index) => $"53000000-0000-4000-8000-0000000010{index:D2}";

        private static BuildMenuEntryViewModel Entry(string id, PlacementTargetKind kind, string category, string sub, string label, IReadOnlyList<BuildMenuMaterialRow> rows, bool paymentWaived, BuildMenuSetPlacement setPlacement, PlacementTargetIcon icon)
        {
            return new BuildMenuEntryViewModel(G(id), kind, G($"51000000-0000-4000-8000-0000000000{category}"), G($"52000000-0000-4000-8000-0000000000{sub}"), label, rows, paymentWaived, icon, setPlacement);
        }

        // fixture の blockIconUrl(n) の n。絵は BuildMenuReferenceDemoTextures が種 n の PNG を返す
        // The n of fixture blockIconUrl(n); BuildMenuReferenceDemoTextures returns the seed-n PNG for it
        private static PlacementTargetIcon Seed(int n) => new PlacementTargetBlockIcon(new BlockId(n));

        // 素材は fixtures の itemId 1（count・held）。詳細はヒントのままなので見た目には出ない
        // Materials use fixture itemId 1 (count, held); the detail stays on the hint, so they never show
        private static IReadOnlyList<BuildMenuMaterialRow> Rows() => Array.Empty<BuildMenuMaterialRow>();

        private static IReadOnlyList<BuildMenuMaterialRow> Rows(int count, int held) => new[] { new BuildMenuMaterialRow(new ItemId(1), new BuildMenuRequiredItem(Guid.Empty, count, held, held < count)) };
    }
}
```
（200 行未満。`BuildMenuRequiredItem` の第 1 引数の型は L0-s3 の実物に合わせる。`BlockId` は `Core.Master`）

- [ ] **Step 2b: 参照と同じ絵を PNG にし、撮影専用のテクスチャ源を書く**

`tools/uitk/render-mock-placeholder-icons.mjs`（mock-host の実物の `placeholderIcon` を import し、Chromium で 64×64 の PNG に描く。式を写さない）:
```js
// mock-host の DEMO が返すブロックの絵（placeholderIcon）を種番号ごとに PNG にする。UITK の並べ比べの撮影だけが使う
// Renders the block pictures the mock-host DEMO serves (placeholderIcon) to PNG per seed; used only by UITK comparison captures
import { mkdirSync } from "node:fs";
import { createRequire } from "node:module";
import { join, resolve } from "node:path";
import { placeholderIcon } from "../../moorestech_web/webui/e2e/mock-host/assets/demoAssets.ts";

// tools/uitk/ には node_modules が無いので、Playwright は webui の依存から解決する（bare import はこのファイルの位置から解決されるため）
// tools/uitk/ has no node_modules, so resolve Playwright from webui's dependencies (bare imports resolve from this file's location)
const webuiRequire = createRequire(new URL("../../moorestech_web/webui/package.json", import.meta.url));
const { chromium } = webuiRequire("@playwright/test");

const [outDir, maxSeedText] = process.argv.slice(2);
if (!outDir || !maxSeedText) throw new Error("usage: render-mock-placeholder-icons.mjs <outDir> <maxSeed>");
mkdirSync(resolve(outDir), { recursive: true });
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 64, height: 64 } });
for (let seed = 1; seed <= Number(maxSeedText); seed++) {
  // 透明の地に SVG だけを置き、そのまま撮る
  // Put only the SVG on a transparent page and capture it as is
  await page.setContent(`<html><body style="margin:0;background:transparent">${placeholderIcon(seed)}</body></html>`);
  await page.screenshot({ path: join(resolve(outDir), `block-${seed}.png`), omitBackground: true });
}
await browser.close();
```
Run:
```bash
pnpm --dir moorestech_web/webui install --frozen-lockfile
pnpm --dir moorestech_web/webui exec playwright install chromium
pnpm --dir moorestech_web/webui exec tsx ../../tools/uitk/render-mock-placeholder-icons.mjs "$PWD/moorestech_client/.uloop/outputs/compare/S3-buildmenu/demo-icons" 19
ls moorestech_client/.uloop/outputs/compare/S3-buildmenu/demo-icons | wc -l
```
Expected: `19`（種 1〜19。量産の `8 + i % 12` の最大が 19）。1 枚を `Read` で開き、参照 `3-build-menu.png` のマスの絵と同じ色相・同じ帯の絵であることを確かめる（`ERR_MODULE_NOT_FOUND` が出たら、出たモジュール名を見る。`@playwright/test` なら `pnpm --dir moorestech_web/webui install --frozen-lockfile` が済んでいるかを確かめる。`.ts` の相対 import なら `tsx` で流しているかを確かめる）

`S/Client.Tests/Ui/Screens/BuildMenu/Support/BuildMenuReferenceDemoTextures.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Client.Ui.Content;
using Core.Master;
using UnityEngine;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    /// <summary>
    /// mock-host の DEMO の絵（種番号の PNG）を返すテクスチャ源。並べ比べの撮影だけが使う
    /// Texture source returning the mock-host DEMO pictures (PNG per seed); used only by the comparison capture
    /// </summary>
    public sealed class BuildMenuReferenceDemoTextures : IGameContentTextureSource
    {
        // fixture の貨物車両は blockIconUrl(5)、電線接続ツールは blockIconUrl(6)
        // The fixture cargo car uses blockIconUrl(5) and the wire connect tool blockIconUrl(6)
        private const int TrainCarSeed = 5;
        private const int ConnectToolSeed = 6;

        private readonly Dictionary<int, Texture2D> _bySeed = new();

        public BuildMenuReferenceDemoTextures(string directory, int maxSeed)
        {
            // 撮影の道具の出力が欠けていたら撮らずに止める（白や名前で代えると参照と違う絵で比べてしまう）
            // Stop before capturing if the tool output is missing (substituting white or names would compare against different pictures)
            for (var seed = 1; seed <= maxSeed; seed++)
            {
                var path = Path.Combine(directory, $"block-{seed}.png");
                if (!File.Exists(path)) throw new FileNotFoundException("Run tools/uitk/render-mock-placeholder-icons.mjs first (Task 10 Step 2b)", path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(File.ReadAllBytes(path));
                _bySeed[seed] = texture;
            }
        }

        public Texture2D BlockTexture(BlockId id) => _bySeed[id.AsPrimitive()];
        public Texture2D TrainCarTexture(Guid trainCarGuid) => _bySeed[TrainCarSeed];
        public Texture2D ConnectToolTexture(Guid connectToolGuid) => _bySeed[ConnectToolSeed];

        // ビルドメニューの面はアイテム・液体の絵を描かない。呼ばれたら撮影の前提が崩れている
        // The build menu face draws no item or fluid picture; a call means the capture premise is broken
        public Texture2D ItemTexture(ItemId id) => throw new NotSupportedException("The build menu comparison has no item pictures");
        public Texture2D FluidTexture(FluidId id) => throw new NotSupportedException("The build menu comparison has no fluid pictures");
    }
}
```
（`BlockId(n)` の n は撮影時のマスタに要る。`GameContentPresentation.Block` は `MasterHolder.BlockMaster.GetBlockMaster(id)` を読むので、Step 3 の EDC の冒頭で `MasterHolder.BlockMaster` のブロック数が 19 以上であることを確かめ、足りなければ止めて `done.md` の「未確認」に書く。ファイルの読み込みはテスト側の撮影の道具で、本番の経路ではない）

- [ ] **Step 3: DEMO の VM で 2 解像度を撮る（並べ比べの本体）**

PlayMode・GameUi・日本語の撮影の口（Step 1）に、DEMO の VM で View を載せる。テストの asmdef は Editor に読み込まれているので EDC から呼べる（呼べなければ `BuildMenuReferenceDemo.Create` の中身を EDC へ貼る）:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var resolver = Client.Game.InGame.Context.ClientDIContext.DIContainer.DIContainerResolver;
var commands = resolver.Resolve<Client.Ui.State.BuildMenu.Commands.IBuildMenuCommands>();
var content = resolver.Resolve<Client.Ui.Content.GameContentPresentation>();
var demoTextures = new Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuReferenceDemoTextures(System.IO.Path.GetFullPath("Assets/../.uloop/outputs/compare/S3-buildmenu/demo-icons"), 19);
// 絵は参照と同じ種番号の PNG で組み、名前・素材は本番の GameContentPresentation の形のまま
// Pictures come from the seed PNGs matching the reference; names and materials keep the production GameContentPresentation shape
var icons = new Client.Ui.Content.PlacementTargetIcons(new Client.Ui.Content.GameContentPresentation(demoTextures), demoTextures, new Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail.BlueprintThumbnailContainer());
var toaster = resolver.Resolve<Client.Ui.State.Toast.UiCommandResultToaster>();
var demo = Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuReferenceDemo.Create();
foreach (var name in new[] { "s3bm-compare-1280", "s3bm-compare-2560" })
{
    var document = UnityEngine.GameObject.Find(name).GetComponent<UnityEngine.UIElements.UIDocument>();
    document.rootVisualElement.Clear();
    var stage = Client.Ui.Foundation.Layers.UiLayerStack.Build(document.rootVisualElement).GetLayer(Client.Ui.Foundation.Layers.UiLayer.Screen, Client.Ui.Foundation.Stage.UiFamily.Stage);
    var session = new Client.Ui.State.BuildMenu.Session.BuildMenuSessionStore();
    var view = new Client.Ui.Screens.BuildMenu.BuildMenuView(commands, content, icons, toaster, session, new Client.Ui.Screens.BuildMenu.CategoryScroll.BuildMenuCategoryScrollTracker(session));
    stage.Add(view);
    view.Render(demo);
    view.SetShown(true);
}
return demo.Entries.Count;'
```
Expected: `95`（8＋80＋7）。`uloop focus-window` の後、S2-mode-tooltip Task 10 Step 2 の 2 回目の EDC（`RenderTexture.active` → `ReadPixels` → `EncodeToPNG`）で `moorestech_client/.uloop/outputs/compare/S3-buildmenu/uitk-3-build-menu-1280x720.png`・`uitk-3-build-menu-2560x1080.png` を書く（`ls -la` で worktree の下を確かめる）。2560×1080 でパネルが 1.5 倍になっていなければ、舞台の倍率の入れ方を C3-b の `StageScaleSource`・`UiStageFrame` の実物で読み、複製のパネルの倍率を 1 回渡してから撮り直す（S2-notification の撮影と同じ）。口が使えなければ止め、`done.md` の「integrator への依頼」に書いて指揮へ報告する

- [ ] **Step 4: 実ゲームの VM でも撮る（別の確認）**

同じ口で `view.Render(resolver.Resolve<Client.Ui.State.BuildMenu.Source.BuildMenuModel>().Current.Value)` に替えて 1280×720 を 1 枚撮り、`uitk-3-build-menu-live-1280x720.png` に書く。これは並べ比べではなく、実際の名前の長さ・絵・件数で崩れ（はみ出し・折り返し・空の欄）が無いことを見る確認で、`done.md` の「## 証拠」に 1 行で書く（D-25）

- [ ] **Step 5: 差の表を書き、口を片付ける**

参照と UITK（DEMO）を `Read` で並べ、`done.md` の「## 差の表」に compare-procedure §4 の表（位置・寸法／色／装飾の有無／文字の段組〈折り返し・行数〉）を両解像度で書く。中身は同じにしたので、カテゴリ名・エントリ名・件数・選択状態の差は「捨てる」にせず、出たらこのレーンの不具合として直す（背景が世界でなく単色〈S2 の前例の `#9c7b42`〉であることだけは表の注に書く）。見る点:
- 帯とパネル: 上 168・下 70 の安全帯の間、幅 886、水平中央（2560×1080 は 1.5 倍で幅 1329・上 252）
- 列: サイドバー 160・詳細 188・列の間と右の余白 16・下の余白 58。サイドバーのボタン 33px・選択の面（明るい半透明 `--mode-switch-selected-face` は α の換算が未実測。罠 c）
- 検索: 面の色（`--gauge-track`）・線・置き文字の色、下の 8px、外形の高さ（参照 720p で約 35px。参照の画素で上下の縁を探して測り、2px 以内。外れたら `.build-menu__search` に高さを置き、値の出どころを `/* web: */` に書く。`GameTextField` は寸法を持たず置く側が決める）
- リスト: 大見出し 14px（本文色・行の高さ 21.7）と小見出し 12px（muted・行の高さ 18.6）の段差、FadeRule、節の間 12・群の間 20、マス 48 の面 40（`--icon-pad: 4px`）、右のバーの帯 12 と inset 12、クリップの逃げ 12
- 詳細: ヒントの muted、上の逃げ 33.57。文の大きさと行数: 詳細の名前・ヒント・素材の見出し・該当なしが 16px で折り返し、行の高さ 24.8px。参照のヒントは 2 行。撮った後に EDC で `build-menu__detail-hint` の `resolvedStyle.fontSize`（期待 16）と `Mathf.RoundToInt(layout.height / resolvedStyle.minHeight.value)`（同じ要素の 1 行の高さで割るので舞台の倍率に依らない。期待 2）を両解像度の View で出して `done.md` の差の表の「文字の段組」行に書き、参照の行数と合わなければ USS を直して撮り直す（該当なしは検索に `zzzz` を入れて 1 枚撮り、1 行であることを同じ式で見る）
- 閉じる: 本文（`GamePanel` の `contentContainer`。題と罫線の下）の右上から `--bevel-3`・`--panel-edge-fade`。参照の × は題の罫線の下（720p で y≈222）。額縁の上端や題の行に乗っていれば親の取り違え

一目で分かる差は `BuildMenu.uss` で直して撮り直す。字間を足すときは I-fix1 の換算トークンを `var()` で使う（罠 a）。同じにするのが大きくて差が一目で分からないものだけ、台帳「## 見た目の例外」の末尾へ `LX-72`〜`LX-75` で記帳し（`LX-71` は D-10 の smooth scroll の長さと緩急）、該当行に `/* look-exception: LX-n */` を付ける（`LookExceptionLedgerTest` が照合する）。足りなければ止めて指揮へ番号を求める。色の差（明るい半透明の `--mode-switch-selected-face` を含む）は「未確認」で進めない。2 択で閉じる: (1) 直す。トークン USS はこのレーンの持ち物でないので、所有レーン（integrator）へ直しを頼み、その直しを `origin/feature/uitk` から取り込んで撮り直し、一致を確かめる。(2) LX に記帳できる性質のもの（UITK の合成の仕組みで Web と同じにできない等）は `LX-72`〜`LX-75` に理由を付けて記帳する。どちらかが済むまで、このレーンは完了にせず Task 13 のマージもしない（R-12）。撮り終えたら `s3bm-compare-*` の GameObject を `Object.Destroy` し、RT を `Release` して、言語を元へ戻す。`git status` で `GameUiPanelSettings.asset` に変更が無いことを見る

`LX-71` の行（smooth scroll）はこの Task で台帳へ足す（列は既存の行に合わせる）:
```markdown
| LX-71 | S3-buildmenu | ビルドメニューのサイドバーのジャンプの smooth scroll | Web は `scrollTo({ behavior: "smooth" })` の長さと緩急をブラウザ（CEF）が決める。UITK は `UiMotion` で 300ms・EaseOutCubic に固定した | 動きの長さの差は録画で見て、見て分かる差があれば値を直す | `S/Client.Ui/Screens/BuildMenu/CategoryScroll/BuildMenuScrollViewport.cs` |
```
（印 `/* look-exception: LX-71 */` は `BuildMenu.uss` の `--build-menu-smooth-scroll-duration` の行に付いている。緩急〈EaseOutCubic〉は C# の `ScrollSmoothlyTo` にあり、台帳の行に「緩急は C#」と書く）

- [ ] **Step 6: 直しがあればコミット**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BuildMenu moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/Support docs/uitk/TODO.md tools/uitk/render-mock-placeholder-icons.mjs
git commit -m "fix(uitk): ビルドメニューを Web との並べ比べで揃え、smooth scroll の例外を記帳する（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 11: unityプレイ録画テスト（完了条件の 7 本と `uitk/uitk-build-menu`）

**Files:**
- Create: `SKILL/scenarios/uitk/uitk-build-menu.cs`
- Modify: 既存の 7 本は変えない（GameUi で落ちたら原因を直す。シナリオ側の直しが要るときだけ最小の分岐を足し、理由を `done.md` に書く）

**Interfaces:**
- Consumes: プレイテスト DSL（`p.SkipOpeningSkit`・`p.SetupDebugEnvironment`・`p.PressKey`・`p.UntilUiElement`・`p.ClickUi`・`p.ClickBuildMenuBlock`・`p.SetText`・`p.WaitUiState`・`p.WaitSeconds`・`p.Until`・`p.Assert`・`p.Note`・`p.Screenshot`）、`GameUiRoot.Instance`、`UiInputExclusivity.ActiveUiSystem`
- Produces: 8 本の `result.json`（`blueprint-place-like-block` は D-18 の扱い）と mp4

- [ ] **Step 1: 新設の録画を書く**

`uitk-build-menu.cs`:
```csharp
// UITK のビルドメニュー: 末尾ジャンプ・検索・閉じて開き直す復元・選択・閉じるを全体スイッチ GameUi で通す
// UITK build menu: trailing jump, search, close-and-reopen restore, selection and close under the GameUi global switch
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-build-menu", options, async p =>
{
    // 背景 Editor でも注入入力が届くよう焦点無視へ切り替える（前例 uitk-lifeline）
    // Ignore focus so injected input reaches a background Editor (precedent uitk-lifeline)
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();

    // B で開き、UITK のパネルが拾えるまで待つ
    // Open with B and wait until the UITK panel is clickable
    p.Note("B でビルドメニューを開く");
    await p.PressKey(Key.B);
    await p.UntilUiElement("build-menu-panel", 15f);
    await p.Screenshot("01-opened");

    // 末尾カテゴリへジャンプし、見出しが視口の上端に来る（L-1）
    // Jump to the last category; its heading reaches the viewport top (L-1)
    var lastButton = Layer().Q<VisualElement>("build-menu-sidebar").Query<Button>().ToList().Last();
    var lastGuid = Guid.Parse(lastButton.name.Replace("build-menu-category-", ""));
    p.Note($"末尾のカテゴリ {lastButton.name} へジャンプする");
    await p.ClickUi(lastButton.name);
    await p.Until(() => Math.Abs(Scroll().scrollOffset.y - HeadingTop(lastGuid)) <= 1f, 5f, "末尾の見出しが視口の上端に来る");
    await p.Screenshot("02-trailing-jump");

    // 検索で絞り込み、閉じて開き直すと検索語と位置が戻る（R-9）
    // Filter by search; closing and reopening restores the query and the position (R-9)
    p.Note("検索欄に「ベルト」を入れ、閉じて開き直す");
    await p.SetText("build-menu-search", "ベルト");
    await p.WaitSeconds(0.5f);
    var filteredOffset = Scroll().scrollOffset.y;
    await p.Screenshot("03-search");
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.B);
    await p.UntilUiElement("build-menu-panel", 15f);
    await p.WaitSeconds(1f);
    p.Assert(Layer().Q<TextField>("build-menu-search").value == "ベルト", "検索語が開き直しても戻る");
    p.Assert(Math.Abs(Scroll().scrollOffset.y - filteredOffset) <= 1f, $"スクロール位置が戻る（{filteredOffset} → {Scroll().scrollOffset.y}）");
    await p.Screenshot("04-restored");

    // 選択で設置モードへ移る（検索を空に戻してから）
    // Selecting moves to placement mode (after clearing the search)
    await p.SetText("build-menu-search", "");
    p.Note("ベルトコンベアを選ぶ");
    await p.ClickBuildMenuBlock("ベルトコンベア");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "UITK のエントリのタップで PlaceBlock へ移った");

    // 閉じるボタンで GameScreen へ戻り、パネルが隠れる（D-27）
    // The close button returns to GameScreen and the panel hides (D-27)
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.B);
    await p.UntilUiElement("build-menu-panel", 15f);
    await p.ClickUi("build-menu-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    p.Assert(Layer().Q<VisualElement>("build-menu-panel").resolvedStyle.display == DisplayStyle.Flex && !IsMenuShown(), "閉じるでパネルが隠れた");
    await p.Screenshot("05-closed");

    #region Internal

    VisualElement Layer() => GameUiRoot.Instance.GetLayer(UiLayer.Screen, UiFamily.Stage);

    GameScrollView Scroll() => Layer().Q<GameScrollView>("build-menu-scroll-area");

    bool IsMenuShown() => Layer().Q<VisualElement>(className: "build-menu").resolvedStyle.display == DisplayStyle.Flex;

    float HeadingTop(Guid categoryGuid)
    {
        Scroll().TryGetContentTop(Layer().Q<VisualElement>($"build-menu-category-heading-{categoryGuid:D}"), out var top);
        return top;
    }

    #endregion
});
```
（`p.PressKey(Key.Escape)` で PlaceBlock から GameScreen へ戻るかは `BuildMenuState`・`PlaceBlockState` の実物で確かめ、戻らなければ `p.PressKey(Key.B)` で開き直す前に `p.ExitToGameScreen()`〈前例 uitk-lifeline〉を使う。「検索語が戻る」の主張は SetText が `TextField.value` を書くことに依る〈DSL の `SetText` の実物を読む〉。検索欄に英字を打たないのは死活表のとおり〈入力中のゲームキーの遮断は S3-ladder〉。ファイルは 200 行未満）

- [ ] **Step 2: 全体スイッチが GameUi であることを確かめてから 8 本を回す**

Run:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
SKILL=.agents/skills/unity-playmode-recorded-playtest
for s in uitk/uitk-build-menu misc/webui-buildmenu-click connect/electric-wire-tool-extend-chain-via-ui connect/electric-wire-tool-isolated-place-via-ui connect/electric-wire-tool-pole-cycle-rotate-via-ui connect/electric-wire-tool-pole-ghost-hide-paths-check connect/electric-wire-tool-pole-ghost-visibility-check; do
  "$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/$s.cs"
done
```
Expected: 2 行目が `GameUi`。7 本の `result.json` が `Success: true`、録画 mp4 がある。初回に "already playing" で落ちたら PlayMode を止めて同じ 1 本をもう 1 回だけ流す（run-scenario.sh の既知の癖）。失敗したら `result.json` と `uloop get-logs --log-type Error` を読み、原因を `done.md` に書いて直す（直したら `$FILTER` を回し、2 行目から流し直す）。各本の前に 2 行目を流し直す必要は無い（シナリオは全体スイッチを変えない）

- [ ] **Step 3: `building/blueprint-place-like-block` を回す（D-18）**

Run: Step 2 の 2 行目を流してから `"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/building/blueprint-place-like-block.cs"`
Expected: BP 名の入力（`modal-input`）は S3-hotbar-modal の持ち物なので、GameUi では `modal-input` を待つ所で止まり得る。止まった位置が `modal-input` の待ちなら、そこまでの主張（ビルドメニューから BP コピーを選ぶ等）が通っていることを `result.json` で確かめ、`done.md` の「## 証拠」に「GameUi: modal-input の待ちで停止（S3-hotbar-modal の持ち物。通しは S3-ladder）」と書く。全体スイッチを Web に戻して同じシナリオを 1 回流し、`Success: true` で既存の経路が壊れていないことを確かめる（`UiSystemEditorSwitch.Set(Client.Input.UiSystem.Web)`）。`modal-input` より前で止まったら、このレーンの不具合として直す。通しの再走は S3-ladder へ渡すと `done.md` の「integrator への依頼」に書く

- [ ] **Step 4: 証拠を done.md に書いてコミット**

`done.md` の「## 証拠」に 8 本の `result.json` のパス・`Success`・録画 mp4 のパス・`UiInputExclusivity.ActiveUiSystem` の値（`uitk-build-menu` の最初の Assert）を書く。録画とスクショの原本は worktree の gitignore 下に置いたまま（`moorestech_logs` へ入れない）。全体スイッチを `GameUi` に戻しておく
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios
git commit -m "test(uitk): ビルドメニューの末尾ジャンプ・検索・復元・選択を UITK の録画で通す（S3-buildmenu）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 12: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイルだけ

**Interfaces:**
- Consumes: Task 1〜11 のコミット一式（`origin/feature/uitk..HEAD`）
- Produces: レビュー記録 `../moorestech_logs/harness/` 配下（`$RUNDIR`）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。レビューが裁定・設計書と食い違う直し（Web と違う見た目へ寄せる・画面 USS に部品のクラスを書く・色を C# に書く・Foundation や部品を書き換える・橋 `Client.WebUiHost` を書き換える・ホットバーの落とし先をこのレーンで作る）を提案したら採らず、`done.md` の「要裁定」に書く

- [ ] **Step 2: 自動適用があれば検査と録画を回し直してコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`
Expected: `ErrorCount: 0`、全件 PASS。自動適用が `BuildMenuView`・`BuildMenuScrollViewport`・`BuildMenuScrollBinding`・`BuildMenuEntryActions`・`BuildMenuModel`・`BuildMenuCommands`・登録ファイル・シナリオに触れたら、反映後のバイナリで Task 11 Step 2 の 7 本を再走する（ユニットテストの通過は代替にならない）。続けて:
```bash
git add -A moorestech_client/Assets/Scripts "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" Localization moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts docs/uitk .agents/skills/unity-playmode-recorded-playtest/scenarios
git commit -m "fix(uitk): S3-buildmenu の moores-code-review 反映

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
（自動適用が無ければコミットしない）

---

### Task 13: feature/uitk への PR と取り込み（必須）

**Files:**
- なし（PR・マージ・bd・`done.md`）

**Interfaces:**
- Consumes: Task 12 後の HEAD
- Produces: `origin/feature/uitk` のマージコミット。以後 S3-hotbar-modal（落とし先と BP 名のモーダル）と S3-ladder（束 3 の突き合わせ・D&D の通し）が使える

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`）。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ。報告ページは `~/hermes-agent/data/services/pr-review/publish/README.md` の手順で、manifest は次の形（`file` は絶対パス。並べ比べは Web を先に同じ `group` へ置く）:
```json
{
  "head": "<HEAD 10 桁>",
  "summary": [
    "ビルドメニューを UITK に移した（3 列・縦の ModeSwitch・検索・1 本のスクロールにスクロールスパイと smooth scroll・詳細の sticky・BP の右クリック削除・不足ツールチップ・閉じて開き直す復元・ドラッグ源）",
    "Model は PlacementTargetResolver・ConstructionWalletQuery・LocalPlayerInventoryController・ClientBlueprintLibrary・IBlueprintThumbnailLookup を直に購読し、Web の橋 BuildMenuEntryDtoFactory と同じ規則で VM を組む",
    "L0-s3 の後段レーンへ渡す 5 点（スペーサ反映後の引き直し・反映後の高さで復元・復元前の位置通知の実測・張り替え時のスペーサ高・非 null の検索語）を満たした",
    "ホットバーへの落とし先と BP 名のモーダルは S3-hotbar-modal（受け口 IHotbarDropReceiver はこのレーンが作り、実装は S3-hotbar-modal）"
  ],
  "media": [
    {"file": "<uitk-build-menu の recording.mp4>", "caption": "uitk/uitk-build-menu（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<webui-buildmenu-click の recording.mp4>", "caption": "misc/webui-buildmenu-click（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<web-869b2d4264/3-build-menu.png>", "caption": "Web 3-build-menu 1280×720", "group": "3-build-menu"},
    {"file": "<compare/S3-buildmenu/uitk-3-build-menu-1280x720.png>", "caption": "UITK 3-build-menu 1280×720", "group": "3-build-menu"},
    {"file": "<web-0e81bbe3c3-2560x1080/3-build-menu.png>", "caption": "Web 3-build-menu 2560×1080", "group": "3-build-menu"},
    {"file": "<compare/S3-buildmenu/uitk-3-build-menu-2560x1080.png>", "caption": "UITK 3-build-menu 2560×1080", "group": "3-build-menu"}
  ],
  "verification": [
    "Unityコンパイル: エラー0",
    "L0/L1: State.BuildMenu（合成・エントリの工場・コマンド）と Screens.BuildMenu.Views 42 件・L0-s3 の CategoryScroll 系 PASS、L2（Conventions・Theme・Layers・Anchor）PASS",
    "test-name-parity: 束 3 の buildMenu の 5 ファイルと束 5 の buildMenuRestore.spec.ts の未対応 0",
    "unityプレイ録画テスト: uitk-build-menu・webui-buildmenu-click・electric-wire-tool-* 5 本 PASS（全体スイッチ GameUi）、blueprint-place-like-block は D-18 の扱い"
  ],
  "notes": ["並べ比べの差の表と L-3 の実測は moorestech_logs/harness/uitk/S3-buildmenu/done.md"]
}
```
`media` には残りの録画（electric-wire-tool-* 5 本・blueprint-place-like-block）も同じ `group` で並べる。`python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <PR番号> <manifest.json>` が失敗したら、報告 URL 無しで完了にしない

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
Expected: 進んでいなければ `Already up to date.`。共有区画（`GameUiTheme.tss` の `Screens` 区画・`MainGameUiRegistration.cs` の画面登録区画・`UiCommandRejectReason.cs` の enum と switch の末尾・`localization.csv` の `ui.commandRejected.*` の末尾・`Client.Ui.State.asmdef` の参照・`docs/uitk/TODO.md` の 3 節の末尾・`PauseMenuCommandsTest.cs` の `benign` の配列〈両方の値の和。片側だけを採ると良性の値が落ちる〉・`IGameContentTextureSource.cs`・`ClientContextContentTextureSource.cs`・`FakeContentTextureSource.cs`〈C4-a が液体の絵を足していれば両方のメソッドを採る〉）の衝突は両方の行を採る（並走の S3-hotbar-modal も同じ区画に行を足す）。`localization.csv` が衝突したら両方の行を採ったうえで、生成物（`localizationKeys.ts`・`_CompileRequester.cs`）は手で合わせず `pnpm --dir moorestech_web/webui run gen:i18n` と `uloop compile` で作り直して同じマージコミットに入れる。採ったら compile とテストを回し、衝突が `UiLayer`・`GameUiRoot`・入力の裁き・`GameScrollView`・`UiDragDrop` に触れていれば Task 11 Step 2 の 7 本を再走する。`ErrorCount: 0`・PASS。`gh pr checks` で `UITK Web follow gate (G1)` と `UITK Web follow gate self-test` が `pass`（終わっていなければ前景で `gh pr checks <PR番号> --watch`）、G1 の出力で未記帳 0。CI の `BugReportSubmitKindTest.空白本文でも種別を先に解く` の失敗は既存（moorestech-albof.22）で、マージの条件は G1 緑

取り込みの後、2 つを見る（当たれば同じ PR へコミットして push し、`$FILTER` を回し直す）:
- 錨の未移植の表: Task 6 Step 6 の `test -f … && grep …` を流し直し、build menu の行があれば消して `TutorialAnchorCoverageTest` を回す（S2-tutorial がこのレーンより後に入った場合）
- S3-hotbar-modal が先に入っていれば（通常は無い。受け口と絵の出どころはこのレーンが先に作る前提）、同じ型を二重に作っていないかを `git grep -n "interface IHotbarDropReceiver\|class PlacementTargetIcon\b" origin/feature/uitk` で見て、重複があれば止めて指揮へ返す

- [ ] **Step 3: マージコミットで取り込む**

Run: `gh pr merge <PR番号> --merge`
Expected: マージされ、`git fetch origin && git log -1 --format='%H %P' origin/feature/uitk` の親が 2 つ（Squash しない）

- [ ] **Step 4: bd を閉じ、報告を締める**

Run:
```bash
bd close <bd> --reason "PR #<番号> を feature/uitk へマージ（ビルドメニュー）"
git status --short
git log origin/uitk-lane/s3-buildmenu..HEAD
```
Expected: closed、`git status --short` と `git log` が空。テストで `Saves/` に作ったワールドがあれば `list-work-saves.sh` で探して消す。`done.md` の 1 行目を「状態: 完了」にし、「## 証拠」に PR 番号・マージコミット・G1 のジョブ URL・各テストと録画の結果・報告ページの URL を足す。最後に実行計画 §4.3 手順 5 のとおり `moores-wt rm uitk-lane/s3-buildmenu --prune-branch` で worktree と Editor を畳む（依頼文で指揮が片付けると言っていればそれに従い、畳まない）

---

## Self-Review

1. **Requirements coverage**: R-1 → Task 2・3、R-2 → Task 7（`BuildMenuLayoutTest` 7 件・USS）、R-3 → Task 6（サイドバー）・Task 7（`BuildMenuViewTest`・`BuildMenuSearchViewTest`・`BuildMenuLayoutTest`）、R-4 → Task 2（合成）・Task 7（検索）、R-5 → Task 6（リスト）・Task 7（視口・`BuildMenuScrollViewTest`）、R-6 → Task 5（マス・ツールチップ・錨）・Task 7（`--icon-pad`）、R-7 → Task 4（コマンド）・Task 7（`BuildMenuEntryActions`・トースト）、R-8 → Task 6・Task 7（`BuildMenuDetailStickyViewTest`）、R-9 → Task 7（`BuildMenuRestoreTest`）・Task 11、R-10 → Task 5（`BuildMenuDragSourceTest`。受け口へ 1 回渡す・Warning）・Task 1（作ったものの申告）、R-11 → L-1 Task 7（`BuildMenuScrollViewTest` 2 件目）・Task 11、L-2 Task 7（`BuildMenuRestoreTest` 2 件目）、L-3 Task 7（観測）・Task 8 Step 4・5、L-4 Task 7（`BuildMenuScrollBinding.Attach`・`BuildMenuRestoreTest` 3 件目）、L-5 Task 7（`BuildMenuSearchViewTest` 4 件目）、R-12 → Task 10、R-13 → Task 7 Step 6（tss）・Task 8（登録・PlayMode の Error 0）、R-14 → Task 11、R-15 → Task 12・13。完了条件の各句: 面の寸法（A-47 は D-5 で付け替え。面は `--icon-pad`、C# の導出はクリップの逃げ）・3 カラム・縦 `ModeSwitch`・`GameTextField`・スクロールスパイと smooth scroll（UiMotion）・詳細の sticky・BP 右クリック削除・不足ツールチップ・セッション復元・ドラッグ源・錨 `build-menu.entry-*`・P21（P20＋S3-hotbar-modal が写すホットバー側の 1）・C22（C9＋L0-s3 済み 13）・録画 7 本・[A-49] の `AppliedSpacerHeight`・L-1〜L-5。漏れなし
2. **Placeholder scan**: `<bd>`・`<番号>`・`<HEAD 10 桁>`・manifest の `<… のパス>`・台帳の実測の値・`<空きポート>` は実行時にしか決まらず、取り方（`bd create` の出力・`gh pr create`・`git rev-parse`・Task 8 Step 4 の EDC）を同じタスクに書いた。「TBD」「適切に」は無い。C3-b の API は Task 1 Step 1 で照合し、違えば読み替えて記録する手順を書いた。実物で確かめる括弧書き（`UiCommandRejected` の ctor・`FakeContentTextureSource` の名前空間・`PointerDownEvent` の送り方・`Escape` で PlaceBlock から戻るか・`LookExceptionLedgerTest` の照合範囲）は、確かめ方と違ったときの代わりを同じ箇所に書いた
3. **Type consistency**: `BuildMenuViewModel(categories, headingNames, entries)`・`.HeadingNameOf`・`.TryGetEntry`・`.CatalogEntries`、`BuildMenuEntryViewModel(...)`・`.Catalog`・`.KindName`・`.CanDeleteBlueprint`・`.Materials`（`BuildMenuMaterialLine`）・`BuildMenuSetPlacementAvailable`・`PlacementTargetIcons.TryToUiIcon`・`IHotbarDropReceiver.Receive`・`.Icon`・`.SetPlacement`、`BuildMenuShownList.Groups`・`.SidebarCategories`・`.IsCategoryEnabled`・`.ShowsNoResults`・`.ShownCategoryGuids`、`BuildMenuShownListComposer.Compose`、`BuildMenuEntryFactory.CreateEntries`・`.CreateCategories`・`.CreateHeadingNames`、`BuildMenuModel.Current`、`IBuildMenuCommands.SelectEntry`・`DeleteBlueprint`・`Close`、`IBuildMenuEntryHandler.OnEntrySelected`・`OnBlueprintDeleteRequested`・`OnEntryHovered`、`BuildMenuEntrySlot(entry, content, icons, handler)`・`.Frame`・`.Render`、`BuildMenuCategoryList(content, icons, handler)`・`.Spacer`・`.Render(groups, viewModel)`・`.SetSpacerHeight`・`.TryGetHeading`・`.TryGetGroup`、`BuildMenuCategorySidebar.Render(shown, viewModel)`・`.SetActive`・`.OnCategorySelected`、`BuildMenuDetailSidebar(content, icons)`・`.ShowHint`・`.ShowEntry`、`BuildMenuPanelSkeleton(sidebar, list, detail)`・`.Band`・`.CloseButton`・`.Search`・`.Scroll`・`.SetNoResults`、`BuildMenuScrollViewport(scroll, list, tracker)`・`.Freeze`、`BuildMenuScrollBinding(scroll, list, tracker)`・`.Attach`・`.Detach`、`BuildMenuScrollClipInset.Apply`、`BuildMenuEntryActions(commands, toaster, session, detail)`・`.SetViewModel`、`BuildMenuView(commands, content, icons, toaster, session, tracker)`・`.Render`・`.SetShown`、`BuildMenuScreenPresenter(gameUiRoot, routingState, model, commands, content, icons, toaster, session)` は定義と使用（テスト・View・Presenter・登録・EDC・録画）で同じ綴り。USS のクラス `build-menu`・`__band`・`__panel`・`__frame`・`__close`・`__columns`・`__column-following`・`__sidebar`・`__category-switch`・`__main`・`__search`・`__scroll`・`__list`・`__group`・`__group--following`・`__trailing-spacer`・`__trailing-spacer--following`・`__category-heading`・`__section`・`__section-heading`・`__stacked`・`__section--following`・`__entry`・`__detail`・`__detail-stacked`・`__detail-icon`・`__detail-name`・`__detail-hint`・`__detail-cost`・`__no-results`、つまみ `--build-menu-scroll-clip-inset`・`--mode-switch-option-height`・`--icon-pad` は C#・USS・テストで一致。testid は Web の `data-testid` と同名
4. **保留・縮退経路**: (a) `BuildMenuEntryDragPayload` → 受け口でない落とし先（別の型の印）は Foundation が飛ばして `OnDroppedOutside` になり何もしない（正常操作の無操作。`silent-ok`）、断られた落としのログは受け手（S3-hotbar-modal）が出す（D-16）、`PlacementTargetIconResolver` の欠けた絵 → 名前で描く（Web の 404 と同じ見え方。面に名前が出るので目で気づく）、`OnDroppedOutside` → `silent-ok`（Web と同じく何もしない）。(b) `BuildMenuScrollViewport` の凍結後の要求 → Warning（`WarnIfFrozen`）。(c) `BuildMenuScrollBinding.Detach` の未装着 → `silent-ok`（View の `_shown` で通常は来ない）。(d) `BuildMenuScrollClipInset` の読めないつまみ → `UssCustomStyleReader` の LogError、L1 の逃げの主張が赤。(e) `BuildMenuView.SetShown` の同値 → `silent-ok`。(f) `AppliedSpacerHeight` の NaN → 0（反映待ちとして扱われ、トラッカーが待ち続ける。コメントに理由）。(g) `BuildMenuSectionView` の VM 外のエントリ → `InvalidOperationException`（組み立ての誤り）。(h) Model・コマンドの拒否 → `UiCommandResultToaster.Report`（良性はログだけ）。(i) Task 1・8・10・11 の「止める」経路は、解消の主体（指揮・integrator）と記録先（`done.md` の「integrator への依頼」「要裁定」）を書いた
5. **「決定的にする」を根拠にした選択**: D-8（転送の門）は「Web と同挙動に潰れる」可能性を実機で測る前に塞ぐ選択で、根拠は L-3 の受け入れ基準（復元の主張が緑）と、潰れたときに利用者が失うもの（位置の復元）。決定性そのものは根拠にしていない。D-10（300ms・EaseOutCubic）は「ブラウザ任せの値を固定する」選択で、LX-71 に記帳した。ほかに無し
6. **呼び出し側への責務漏出**: 合成（検索・群分け・サイドバーの無効）は `BuildMenuShownListComposer` の 1 か所、エントリの組み立ては `BuildMenuEntryFactory` の 1 か所、カテゴリ対・アイコンはそれぞれの Resolver の 1 か所。視口の寸法の読み・転送の門・介入・凍結は `BuildMenuScrollViewport` の 1 か所、張り替えの順（スペーサ → 視口 → 装着、保存 → 凍結）は `BuildMenuScrollBinding` の 1 か所で、View は順を知らない。クリップの逃げの導出は `BuildMenuScrollClipInset` の 1 か所。トーストの名前は `BuildMenuEntryActions` と View の定数。判定を返す新設 API は無い（`IsCategoryEnabled` は合成の結果の読み出し）

---

## 判断記録（ADR）

従う裁定・設計: Global Constraints の正本、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1.1・§2.3・§2.4・§2.7・§2.8・§3・§4.1・§4.2・§4.3・§5.1、[A-20]・[A-47]・[A-49]・[A-50]・[A-57]・[A-78]・[A-79]）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 S3-buildmenu 行・§4.3・§5・§6.2・E-1・E-13・E-20）、L0-s3 計画の「後段レーンへ渡すもの」。

| ID | 判断 | 棄却した案と理由 | 出所 |
|---|---|---|---|
| D-1 | エントリの組み立ては Web の橋 `BuildMenuEntryDtoFactory`・`BuildMenuMaterialAvailability` の規則を State の `BuildMenuEntryFactory` へ写す。橋は変えず、重複は「本PR外のリファクタ提案」 | 橋を共有化して両系から使う: 橋は `Client.WebUiHost`（Web の系だけの asmdef）にあり、State から参照できない。橋を動かすと Web の系の PR になりこのレーンの範囲を超える | agent 既定（設計書 §1.1） |
| D-2 | VM はアイコンを ID だけの判別 union（`PlacementTargetBlockIcon`・`TrainCarIcon`・`ConnectToolIcon`・`BlueprintIcon`・`NameOnlyIcon`。置き場は D-29）で持ち、見出しの名前を `UiText` の辞書で持つ。絵は `Client.Ui.Content.PlacementTargetIcons` が値として引き、面へ当てる分岐は Screens が持つ | VM に `UiIcon` か Texture2D を持たせる: State は `Client.Ui` を参照できず、テクスチャの容器（`ClientContext`）を State に持ち込むと Content 層（A-4）と二重になる。カテゴリ名を View がコンテンツ辞書キーから作る: L1 が Web の英語名（最長の Building Materials）を逐語で入れられず、レイアウトの 5 件目が空振りになる | 設計検査（配置 V1・V2）・設計書 §1.1・§2.8 |
| D-3 | 検索・群分け・サイドバーの無効の合成は純関数 `BuildMenuShownListComposer`（State） | View の中で合成: L0 で試せず、Web の `BuildMenuPanel` の分岐（非検索時は絞り込みを省く）が View に散る | agent 既定 |
| D-4 | 拒否理由を `UiCommandRejectReason` の末尾に 6 値足し、Web の wire code と良性の集合（`BENIGN_ERRORS`）を写す。CSV に `ui.commandRejected.*` を 6 行 | 画面固有の結果型: トーストの経路（`UiCommandResultToaster`）が 1 本でなくなる。前例 S2-skit が同じ共有区画へ足した | agent 既定（前例 S2-skit） |
| D-5 | マスの面は `--icon-pad: 4px`（面 = `--slot-size` − 8px）で写し、[A-47] の「C# が導出する calc」は Web で生きている calc（`.scroll` の `margin: calc(-1 * var(--tutorial-anchor-clip-inset))`・viewport の padding・縦バーの inset）へ付け替えて `BuildMenuScrollClipInset` が導く。設計書の [A-47] の文言の直しは `done.md` の「integrator への依頼」と設計書行の案（A-100）へ | 完了条件の文言どおり `.slotIcon` の `calc(var(--slot-size) - 8px)` を C# で導く: Web の `.slotIcon`・`.slotLabel` は `BuildMenuSlot.tsx` から参照されない死んだ CSS で、実際の面は `PlacementTargetFace` の `--icon-pad` が決めている（grep で確認）。死んだ規則を写すと Web と違う物を作る | 本体（指揮代行）の決定で採用（Web の CSS と TSX の実物）。設計書行の案 A-101 に [A-47] の改めとして書いた |
| D-6 | 画面は `UiLayer.Screen`・`UiFamily.Stage`。帯は `top: --menu-upper-safe-area`・`bottom: --menu-lower-safe-area` で作る | `--menu-content-height` のトークンを足す: Tokens.uss に無く、トークン USS はこのレーンの持ち物でない。舞台は 720 固定なので top/bottom で同じ高さになる | agent 既定（Web の `.panelBand`・`--z-stage-screen`） |
| D-7 | 隠すときは「位置の保存 → 視口の凍結 → `display: none`」、出すときは「検索語の復元 → `display: flex` → 合成 → スペーサ高の当て直し → 新しい視口の装着」。視口は表示ごとに作り直す | 視口を 1 つ使い回す: トラッカーの `AttachViewport` は同じインスタンスに冪等で、保存位置からの復元（`_scrollRestored` の巻き戻し）が起きない。隠した ScrollView の位置をそのまま読む: `display: none` の間の位置が保たれる保証が無い | agent 既定（L0-s3 の `AttachViewport` の実物） |
| D-8 | 視口は最初の `SetScrollTopImmediately` まで位置の通知をトラッカーへ流さない（転送の門） | Web と同じく全部流す: L0-s3 が「Web と同挙動に潰れ得る」と申し送った経路（L-3）を開けたままにする。門は L1 で記録し、実機の観測は Task 8 Step 4 | agent 既定（L0-s3「後段レーンへ渡すもの」L-3）。実測で不要と分かり外した（Task 8 Step 4: 閉じて開き直す間の `[L-3]` は 0 件・offset=200.0・saved=200.0、門を外しても同じ） |
| D-9 | スペーサは `SpacerHeight` の購読で `minHeight` を当て、`AppliedSpacerHeight` は `Spacer.layout.height`（NaN は 0）。スペーサの `GeometryChangedEvent`・`OnContentLayoutChanged`・各カテゴリ群の `GeometryChangedEvent`（`BuildMenuCategoryList.OnGroupGeometryChanged`。Web `useBuildMenuCategoryScroll.ts` の群ごとの ResizeObserver の写し）の 3 つで `OnLayoutChanged` を呼ぶ | `OnContentLayoutChanged` だけ: スペーサの反映で中身の高さが変わらない（末尾群が長い等）とき引き直しが来ず、末尾ジャンプが手前で止まる（L-1） | agent 既定（L0-s3 の L-1・[A-49]） |
| D-10 | smooth scroll は `UiMotionPlayer`＋`ScrollOffsetTweenStep`、300ms・EaseOutCubic。見た目の例外 LX-71。長さは `BuildMenu.uss` の `.build-menu__scroll` のつまみ `--build-menu-smooth-scroll-duration: 300ms` を `UssCustomStyleReader.TryReadMilliseconds` で読む（前例 `ToastHostView` の `--toast-host-enter-duration`。ref-uitk-design「尺はトークン」）。緩急は C# | UITK の `ScrollView.ScrollTo`: 即時で滑らかに動かない。CEF の実測値に合わせる: ブラウザの smooth の長さは仕様で決まらず、測っても版で変わる | agent 既定（完了条件「smooth scroll（UiMotion）」） |
| D-11 | ホイール・押下・キー（TrickleDown）でモーションを止め `OnUserIntervened` を呼ぶ | 押下だけ: Web は wheel・touchstart・pointerdown・keydown の 4 つで固定を解く（`useBuildMenuCategoryScroll.ts:22`。UITK のデスクトップでは touchstart は押下に畳まれる） | agent 既定（Web `useBuildMenuCategoryScroll.ts`） |
| D-12 | サイドバーは項目の並びが変わったときだけ `ModeSwitch<Guid>` を作り直し、無効は `SetOptionEnabled` | 毎回作り直す: 押下中の状態・フォーカスが消え、検索の打鍵ごとに木が揺れる | agent 既定 |
| D-13 | リストは群・節・エントリの並びのキーが同じなら中身だけ差し替え、違えば作り直す | 毎回作り直す: 所持品の変化（毎秒あり得る）でスクロールの寸法が揺れ、ホバーとツールチップの印が外れる | agent 既定（Web の React の key と同じ効果） |
| D-14 | 詳細は sticky。ホバーした id をセッションに保存し、VM の再配信のたびに引き直す。エントリが消えたらヒントへ戻す | 離れたら消す: Web の regression 8 件目（sticky）に反する | Web `BuildMenuPanel.tsx` |
| D-15 | 不足ツールチップの文は題＋不足行を改行で結んだ逐語（`UiText.FromVerbatim`）で、言語の変化で作り直す | 行ごとの `UiText` の列: `HoverTooltipMark` は `UiText` 1 つを受ける（F1 の実物） | agent 既定（F1 の API） |
| D-16 | 落としの受け口 `IHotbarDropReceiver`（State/Hotbar/Logic）をこのレーンが作り、源は `BuildMenuEntryDragPayload : IUiDragPayload<IHotbarDropReceiver>` で、`OnDropped(IHotbarDropReceiver receiver)` は `receiver.Receive(Source)` を呼ぶだけ（型の判定とキャストは書かない。断った理由のログは受け手が出す）。ホットバーの印は `MarkTarget<IHotbarDropReceiver>` で付き、受け口でない落とし先（別の型の印）は Foundation が飛ばして `OnDroppedOutside` になり何もしない。これは正常操作の無操作で、ガードの縮退ではない（Web と同じ） | Warning だけ置いて S3-hotbar-modal が本体を差し替える: 後のレーンがこのレーンのファイルを書き換え、所有が混ざる。このレーンで割当まで作る: 枠の印と割当は S3-hotbar-modal の持ち物。中身の無い印 interface を受け手に継がせて源がキャストする: 外れたら黙って枠外になり、State は `Client.Ui` を参照できない（A-132）。断られた落としを源で Warning にする: 受け手と源の 2 か所でログが重なり、hotbar-modal 側の扱いとずれる | 本体（指揮代行）の決定。I-fix3 で C3-b 要裁定 3 を推奨 A で反映（設計書 A-132） |
| D-17 | 1 セット設置（`setPlacement`）は Block だけ | 全種別: Web の橋も Block だけに付ける（`BuildMenuEntryDtoFactory`） | Web の橋の実物 |
| D-18 | `building/blueprint-place-like-block` は GameUi で `modal-input` の待ちまでを確かめ、Web で通しを 1 回流して既存経路を守る。GameUi の通しは S3-ladder | GameUi で通しを完了条件にする: BP 名のモーダルは S3-hotbar-modal の持ち物で W3 の順ではこのレーンの後。完了条件から外す: 実行計画の文言にあり、ビルドメニューの経路（BP コピーの選択）はこのレーンで確かめられる | 本体（指揮代行）の決定で採用（実行計画 §1.4・死活表） |
| D-19 | Model は開いている間、源の変化をフレーム末（`PlayerLoopTiming.PostLateUpdate`）の 1 回に畳んで VM を組み直す。開いたときも同じ予約で組む。設計書 A-57 の「フレーム末への間引きはしない」の例外（A-57 の理由〈同じフレームの描き直しは UITK が 1 回の描画にまとめる〉は描画の回数の話で、全エントリの VM と全マスの組み直しの回数は減らさない。done.md の要裁定に載せる）。Presenter は出ている間だけ購読し、開いた直後は前回の VM を同じフレーム内で 1 回描く | 同期で組み直す: スロット通知 1 件ごとに全マスを組み直して描くことになり、橋の実物 `BuildMenuTopic.SchedulePublish`（同フレーム多発をフレーム末の 1 回に畳む）とも違う。開いた瞬間に同期で組む口（`RebuildOnOpen`）を足す: 前回 VM の描画は同フレーム内で上書きされ画面に出ないので、型を増やす理由が無い。閉じている間も購読: 描かない画面に配る理由が無い | moores-code-review 設計判断 D1（推奨案 A を agent 既定で採用。無人実行）・前例 `BuildMenuTopic.SchedulePublish`・設計書 [A-57]（表示中だけの購読の前例は無い。`PauseMenuScreenPresenter` 等は `Initialize` で 1 回購読する。判定の元〈Model は UIState・Presenter はルータの Screen〉が違う点は D-41(a)） |
| D-20 | 登録は `BuildMenuGameUiRegistration` 1 ファイルと `MainGameUiRegistration` の 1 行 | 共有の `MainGameUiStateRegistration` に Model を足す: GameUi の系だけが使う（Web は橋が同じ規則で動く。前例 S2-mode-tooltip D-19） | 前例 S2-mode-tooltip |
| D-21 | `Client.Ui.State.asmdef` に `Common.Debug`・`Game.Construction`・`Client.Mod` を足す（CS0246/CS0012 が出たものだけ） | 規則を写さず橋を呼ぶ: D-1 と同じ理由で参照できない | 前例 S2-mode-tooltip D-38 |
| D-22 | View の ctor は 6 引数（コマンド・コンテンツ・絵の出どころ `PlacementTargetIcons`・トースト・セッション・トラッカー）。トラッカーは Presenter が作り View と同じ寿命 | View がトラッカーを作る: L1 がトラッカーの `SpacerHeight` を読めない。DI で登録: 画面 1 つにしか使わない状態を共有へ出す理由が無い | agent 既定 |
| D-23 | `buildMenuRestore.spec.ts` と `placementIconCentering.spec.ts`（`e2e/` を持つ行）は test-name-parity の束 5 に数える | 束 3: 台帳「## 行」でそのファイルを持つ行の最大の束が 5（Task 9 Step 1 で読み違いなら直す） | `docs/uitk/disposition.md` の書式 |
| D-24 | L3（EditModeInPlayingTest）は持たない | 足す: 段の締め S3-ladder の持ち物（実行計画） | 実行計画 §1.4 |
| D-25 | 並べ比べは、参照キャプチャと同じ中身（mock-host の DEMO の `buildMenuFixtures` と日本語の名前・選択なし・詳細はヒント）を写した `BuildMenuReferenceDemo` の VM を、複製した PanelSettings の RT へ載せて両解像度で撮る。差の表の行は「同じ／直した／LX-n」だけで、中身の差を理由に捨てない。実ゲームの VM の撮影は崩れの確認として別に 1 枚撮る。色の差は所有レーンの直しを取り込んで撮り直すか LX に記帳するまで完了・マージしない | 実ゲームの VM で撮って中身の差を一律に捨てる: 名前の長さ・件数・行数の差に隠れて、寸法・折り返し・行数・色の差を見逃す（独立評価の指摘）。色の差を「未確認＋integrator 依頼」で進める: 直ったことを誰も確かめないまま出荷される | 独立評価（Codex C1・C2）・本体の決定 |
| D-26 | 部品のつまみは、部品の要素に足した画面のクラスで上書きする（`build-menu__category-switch`・`build-menu__entry`・`build-menu__scroll`） | 祖先で宣言: `customStyle` は祖先の宣言を読まない（C# で読むつまみが効かない） | ref-uitk-design・設計書 §3.2 |
| D-27 | 閉じるの L1 はコマンドの呼び出し 1 回だけを見て、パネルが隠れることは録画で見る | L1 で routing まで組む: `UIStateControl` はシーンの component で L1 では遷移しない（前例 S2-mode-tooltip） | agent 既定 |
| D-28 | 検索 0 件の該当なしはリストと入れ替える（リストを `display: none`） | リストの上に重ねる: Web は三項でリストを描かない | Web `BuildMenuPanel.tsx` |
| D-29 | 配置対象の絵の出どころ（`PlacementTargetIcon` の union・`PlacementTargetIconResolver`・`PlacementTargetIcons`）は最初から最終の置き場（`Client.Ui.State/PlacementTarget/`・`Client.Ui/Content/`）に作り、持ち主はこのレーン。ビルドメニューはマスと詳細から使うだけ。車両・接続ツールの絵は C3-a の `IGameContentTextureSource` に 2 メソッドを足して引く（このレーンの追記。絵が欠けたら null を渡し、`PlacementTargetFace` が名前で描く）。種別名 `BuildMenuEntryKindName` は BuildMenu の下に残す | BuildMenu の下に仮置きして後で昇格: S3-hotbar-modal が後からこのレーンのファイルを動かすことになる | 設計書 §5.1-1（Web でビルドメニューとホットバーの 2 機能が既に `ResolveIconUrl` を共有）・本体の決定。設計書行の案 A-100 |
| D-30 | 字間は足さない | 足す: Web のこの画面の CSS に字間が無い（I-fix1 の換算が要るのは字間を持つ画面だけ） | Web の CSS の実物 |
| D-31 | 色の比較はランタイムの PanelSettings の複製で撮った RT で行い、ギャラリーの撮影では比べない | ギャラリー: sRGB の合成で半透明が淡く出る（I-fix1 の罠 b） | I-fix1（e26010aa0c） |
| D-32 | Web テストの部分を写さないところ: `aria-pressed` は選択のクラス（`mode-switch__option--selected`）、`data-locale` 属性の確認は L1 の言語固定（`ScreenTestHost`）、Mantine の viewport のクラス名は `GameScrollView` の `contentViewport`、`expectNoVerticalOverflow` は実幅で測った文の高さが 1 行と同じこと、`fill` は `TextField.value` の代入 | DOM の属性を UITK に作る: 意味の無い写しで、テストの意図（選ばれている・言語が英語・溢れない）は同じ主張で守れる | agent 既定 |
| D-33 | 新しいテストは `S/Client.Tests/Ui/Screens/BuildMenu/Views/`（namespace `…BuildMenu.Views`）と `Support/` に置く | 直下に置く: L0-s3 の 4 ファイルと合わせて 13 になり、1 ディレクトリ 10 ファイルの規約を超える | AGENTS.md |
| D-34 | マスの操作の口（`IBuildMenuEntryHandler`）の実装を View から `BuildMenuEntryActions` へ分ける | View が実装する: View の公開面に操作の口が出て、200 行を超える | AGENTS.md（200 行） |
| D-35 | `IBlueprintDeleteService` は `MainGameInteractionRegistration.cs` の `ClientBlueprintLibrary` の行に `.As<IBlueprintDeleteService>()` を 1 つ足して引く（共有ファイルの 1 行の追記として File Structure に書いた） | 登録ファイルで `Register<IBlueprintDeleteService>(resolver => …)` のラムダで引き当てる: 同じ実体を 2 つの登録で指すことになり、`As` 1 つで済む所に迂回を作る。`BuildMenuCommands` が `ClientBlueprintLibrary` を受ける: L0 で削除の結果を差し替えられない | 設計検査（N2）・本体の決定 |
| D-36 | Web の `.main` の gap 8px は検索の下の余白（`.build-menu__search`）で写し、スクロールの負の上余白（−12）と足し合わせる | スクロールの上余白を `8 − inset` で C# が出す: クリップの逃げの導出に列の間隔が混ざり、`BuildMenuScrollClipInset` が Web の 1 規則の写しでなくなる | agent 既定 |
| D-37 | `PlacementTargetIcons` は DI の Singleton で、`bool TryToUiIcon(PlacementTargetIcon, out UiIcon)` だけを持つ（値だけを返す）。面へ当てる分岐（真なら `SetIconTarget`、偽なら `SetNameTarget`）は `BuildMenuEntrySlot` と詳細の列が持つ | Content が面（`PlacementTargetFace`）を受けて当てる `Apply`: Content 層が Components を操作することになり、前例 `GameContentPresentation`（値だけを返す）から外れる | 設計検査（配置 V1）・本体の決定 |
| D-38 | Screens は `Game.PlacementTarget` を参照しない。VM が `KindName`（`BuildMenuEntryKindName.Of` の結果）と `CanDeleteBlueprint` を持ち、詳細の列は `entry.SetPlacement` をそのまま使う | マスと詳細が `PlacementTargetKind` を見て分岐する: 種別の語彙が Screens に漏れ、同じ判定が 2 か所に散る | 設計検査（配置 V3） |
| D-39 | 落としの受け口は `void Receive(HotbarDragSource)`（状態を書く口なので結果を返さない。断る理由のログは受け手が出す） | `bool TryReceive`: 源は戻り値を使わないので、bool は呼び手に何も伝えない | 設計検査（配置 V4）・本体の決定 |
| D-40 | 1 セット設置は判別 union（`BuildMenuSetPlacementNone.Instance`・`BuildMenuSetPlacementAvailable(perCost, remaining)`）にし、Factory だけが Block のとき作る | `HasValue` 付きの 1 クラス: 値が無いのに `PerCost`・`Remaining` を読めてしまう。詳細の列で Kind を見て Block に限る: Factory と同じ判定が 2 か所になる | 設計検査（Phase 2.6 強） |
| D-41 | 型の弱い指摘の扱い: (a) `CurrentState == BuildMenu` は `BuildMenuCommands` の private `IsBuildMenuOpen` の 1 か所にまとめる。Model の「閉じている間は組み直さない」は別の理由なので残す (b) 素材の不足は VM の ctor で 1 回だけ判定し、`BuildMenuMaterialLine.IsInsufficient` で持つ (c) 空の表示名は前例 `ModeHudViewModelFactory` と同じく `LogError` を出し、表示はそのまま (d) NameOnly のマスも `filled` にする（Web `BuildMenuSlot.tsx` は常に `filled`） (e) `_structureKey` は null を「まだ描いていない」にし、空の文字列（群が 0 の構造）と分ける (f) 合成の結果の bool（`IsCategoryEnabled`・`ShowsNoResults`）と `TryGet` が理由を返さないことはそのまま（呼び手が分岐するだけで、理由を出す先が無い） | 全部を型で塞ぐ: (f) は呼び手の分岐が 1 つずつで、理由を返しても読む所が無い | 設計検査（Phase 2.6 弱） |
| D-46 | 再検査 3 の反映: 詳細の文・該当なしは既存の `UiTypographyClass.TextMd`（`.text--md`。16px・1 行 24.8px・折り返し）を C# で付け、画面 USS は 2 行目以降の行送り `--localized-label-line-height` だけを持つ。視口の未レイアウト判定は private `IsViewportLaidOut`（NaN も 0 も偽）の 1 か所 | 画面 USS に同じ値を書き直す: 部品カタログの「`UiTypographyClass` を付ける」に反し、値が 2 か所に分かれる | agent 既定（再検査 3） |
| D-45 | 独立評価 5 巡目の反映: (a) Task 4 Step 7 で gen:i18n の再生成コミットを台帳「## 追従記録」へ記帳（E-1。無いと G1 の未記帳が 1 残りマージ条件を満たせない） (b) 大見出し・小見出しに Web の継承 line-height 1.55 を min-height で写す (c) 突き合わせは束ごとの終了コードを見て 2 なら止める (d) テクスチャ源のインターフェースは C4-a と衝突しうるので取り込みで両方を採る | — | agent 既定（Codex 5 巡目・opus 5 巡目） |
| D-44 | 独立評価 4 巡目の反映: (a) 閉じるボタンは Web と同じく `GamePanel` の本文の子（`frame.Add`。top・right は題の下の本文から測る）にし、Task 10 で × の位置を見る (b) 視口の `TryGetHeadingTop`・`TryGetGroupHeight` は今の表示で視口が未レイアウト（`ClientHeight <= 0`。閉じた後の 0 を含む）なら測れないとし、`BuildMenuRestoreTest`「末尾の群が視口より高くても…」で確かめる (c) `Support/` の本数を実数に (d) using と `BlueprintThumbnailContainer` の完全修飾名 | (a) 外側のパネルの子: × が題の行に乗り参照より約 45px 上 (b) NaN だけを未計測とする: 閉じた後の 0 を実測と取り違え、スペーサ 0 の構成で保存位置を 0 に潰す（推測。L1 で赤を確かめる） | agent 既定（Codex 4 巡目・opus 4 巡目） |
| D-43 | 独立評価 3 巡目と再検査 2 の反映: (a) 詳細の名前・ヒント・素材の見出し・該当なしは Web の本文〈継承 16px・行の高さ 1.55＝24.8px・折り返す〉を `BuildMenu.uss` で明示し、Task 10 で両解像度の行数を測る（UITK の既定 14px・折り返さないのままだと参照の 2 行のヒントが崩れる） (b) 各カテゴリ群の `GeometryChangedEvent` を `BuildMenuCategoryList.OnGroupGeometryChanged` で追跡器へ流す（Web の群ごとの ResizeObserver。全体の高さが同じまま配分だけ変わる場合）と L1 `BuildMenuScrollViewTest` 4 件目 (c) `Registration/GameUi/` が 11 本以上になるなら足す前に止め、integrator の再編が入るまで完了・マージしない (d) 撮影用の絵の描画スクリプトは `createRequire` で webui の `@playwright/test` を解決 (e) `PauseMenuCommandsTest` を共有ファイル・`$FILTER`・取り込みの衝突の一覧へ（S3-hotbar-modal と同じテスト。`benign` は和を採る） | (a) 色だけ写す: 文字の大きさと行数が参照と合わない (b) 中身全体の通知だけ: 配分の変化でハイライトが古いまま (c) 11 本目のまま進める: AGENTS.md の 10 ファイルに反する | agent 既定（Codex 3 巡目・opus 3 巡目・再検査 2） |
| D-42 | 再検査の反映: (a) `PlacementTargetIcons` の登録は Content 層の登録の置き場 `ContentGameUiRegistration.cs` の末尾へ 1 行（C3-a のファイルへの追記） (b) `TryToUiIcon` はテクスチャの欠けでも false（[A-100] どおり。面は名前で描き、詳細の列は絵を足さない） (c) `BuildMenuSetPlacementNone` を public sealed の `.Instance` にし、使う側は switch で分ける (d) VM の公開面から `Kind` を外し、`KindName`・`CanDeleteBlueprint` だけにする（Factory の内側では使う） (e) `PlacementTargetBlueprintIcon` は値の等価を持たず参照の等価のまま。State はテクスチャを持たないので Guid の等価では撮り直しを見分けられず、等価にすると撮り直し後の VM が同じ物と見なされて絵が古いまま残る。他の union は値の等価のまま (f) 再検査の弱1・弱3 は指揮の決定どおり据え置く | (a) 画面の登録ファイルに置く: Content 層の型をビルドメニューの寿命に結びつけ、ホットバーが画面の登録に依存する (b) 欠けた絵の `UiIcon` を渡して面に任せる: 詳細の列が空の絵を足す (c) `as … != null`: union の網羅が型で見えない | 設計検査の再検査（違反1・強3・弱4）・本体の決定 |
| D-47 | 「絵の出どころは値で比べる」テストも先頭でテスト mod の DI を作る（`ForUnitTestModBlockId.BlockId` が MasterHolder を引くため） | 固定の `new BlockId(1)` で比べる: テスト mod の実 ID と結びつかず、マスタの変更に気づけない | agent 既定（Task 1 の照合・`.superpowers/sdd/drift.md`） |
| D-48 | `Client.Ui.State.asmdef` に足す参照は compile が要求した分だけにし、候補に `Game.Blueprint` を加える（`BlueprintJsonObject` を `BlueprintPasteCopyBuilder` へ渡すため）。共有ファイルの申告の数も実数にする | 計画の 3 つを固定で足す: 使わない参照（`Client.Mod`）が残り、`Game.Blueprint` が抜けて CS0012 | agent 既定（同上） |
| D-49 | `BuildMenuCommands.Close` は `IUiStateTransitionRequester` で GameScreen への遷移を頼み、false なら `BuildMenuNotOpen` で拒否する。Close の受理と拒否を FakeRequester で L1 に持つ（D-27 の「Close の受理は L1 に持たない」を改める） | `UIStateControl.RequestTransition` を直に呼ぶ: 計画の後に入った前例 `BlockInventoryPanelCommands` と許可表 `UiStateTransitionRules` を通らず、L1 で見られない | agent 既定（前例優先。同上） |
| D-50 | `TutorialAnchorCoveragePending.cs:15` の build menu の行は、印を付ける Task 5 と同じコミットで消す | Task 6 で消す: Task 5 の時点で `TutorialAnchorCoverageTest` が赤になる | agent 既定（同上） |
| D-51 | 縦バーの inset は C# のインライン指定をやめ、`BuildMenu.uss` で GameScrollView のつまみ `--game-scroll-view-scrollbar-inset-{top,right,bottom}` を `var(--tutorial-anchor-clip-inset)` に上書きする。C# は margin と contentContainer の padding だけ | `verticalScroller.style` に C# で当てる: 部品のつまみを迂回し、前例 `RecipeViewer.uss`・`RecipeEntryList.ApplyClipClearance` と違う形になる | agent 既定（同上） |
| D-52 | Web の `--sa-corner-width` は既定 0 で書き、Task 10 で縦バーの長さを参照と比べて 4px 長ければ bottom を clip-inset + 4px にする | 最初から 4px（`ItemList.uss` の前例）: 前例が `RecipeViewer.uss`（0）と割れており、この画面の参照で決める | agent 既定（同上） |
| D-53 | LX-71 の台帳行は Task 10 でなく、印を付ける Task 7 Step 6 で `BuildMenu.uss` と同じコミットに足す（台帳は 7 列）。撮影の参照の表記は台帳の正本 `web-a9d5123537`（`web-869b2d4264` と画像は同一）、撮影は `UiRuntimePanelCapture.CaptureToPng`（I-blend 後の Linear の RT）を第一候補にし、罠 (c) の「α 未実測なので未確認」は前提が消えたので色の差も同じく閉じる | 計画どおり Task 10 で足す: Task 7 の `LookExceptionLedgerTest` が赤になる。sRGB の RT の EDC を写す: I-blend 後は色が二重に変換される | agent 既定（同上） |
| D-54 | Task 3 の compile で `Client.Ui.State.asmdef` に実際に要った参照は `Common.Debug`・`Game.Construction`（CS0246/CS0234）と `Game.Blueprint`（CS0012・`BlueprintJsonObject`）の 3 つ。`Client.Mod` は要らなかった（D-48 の推測を実測で確定） | 計画の `Client.Mod` も足す: 使わない参照が残る | agent 既定（Task 3 の compile 実測） |
| D-55 | `BuildMenuEntryFactoryTest` は SetUp で保存言語を退避して `Localize.Initialize()`＋`TrySetChosenLanguage("english")`、TearDown で戻す（前例 `ScreenTestHost`）。UITK は全種別で `IPlacementTarget.DisplayName` を引き、辞書が未初期化だと `LocalizationTextResolver` で NRE になるため | 橋と同じく BP だけ DisplayName を引く: 全種別で DisplayName を使う計画（S2-mode-tooltip D-8）に反する | agent 既定（Task 3 のテスト実測） |
| D-56 | `BuildMenuSamples` のカテゴリ名は Web の `contentLocalizationFixtures.ts`（english）の英訳を逐語で写す（Logistics・Transport・Blueprint・Building Materials・Mining・Production・Power・Electricity・Liquids・Tools、空は Building Materials）。Web の建材 2 つ（空の 0004・中身ありの 0011）は `EmptyCategory` と `BuildingMaterials` に当て、計画の id 割当は変えない | 計画の仮名（Blueprints・Fluids・Storage・Decoration・Empty 等）: Web の fixture と字が違い、見出し幅の比較が Web とずれる | agent 既定（Task 5 で Web fixture を照合） |
| D-57 | `BuildMenuScrollViewport` は視口の高さに加え、ScrollView の縦バーの範囲（`verticalScroller.highValue`）が今の寸法（中身 − 視口、0.5px 許容）に追いつくまで見出し・群を「未レイアウト」とする。群・スペーサの `GeometryChangedEvent` は ScrollView が範囲を更新する前に来る（L1 実測: 開き直しの 1 フレーム目に group→spacer→content の順で、group の時点で high=0・content の時点で 1385）ため、そこで復元すると保存位置が 0 へクランプされる。後に来る中身の通知（`OnContentLayoutChanged`）でやり直す | 視口の高さだけで判定（計画のまま）: `BuildMenuRestoreTest` 3 件が 0 へ潰れて赤。復元を次のフレームへ遅らせる: 位置が 1 フレーム遅れて見える | agent 既定（Task 7 の L1 実測） |
| D-58 | L1 の写しの 2 点: (a) ジャンプ中の押下は `PointerDownEvent.GetPooled()` の `SendEvent` では TrickleDown の口へ届かない（実測）ので、計画の括弧書きどおり `simulate.MouseDown/MouseUp` で Transport の見出しを押す (b) カテゴリ名の 1 行判定の上限は Web の `clientHeight`（内余白込み・枠線抜き）に合わせ、ボタンの内容箱でなく内容箱＋上下の内余白にする（16px の 1 行 22px は内容箱 21.4px を 0.6px 超えるが、Web も 24.8px の行が内容箱を超え clientHeight の内に収まる） | (b) 内容箱のまま: Web より厳しい判定で、Web と同じ寸法なのに赤になる | agent 既定（Task 7 の L1 実測） |
| D-59 | Task 10 の 1280×720 の Web は、この worktree の webui（HEAD）で `3-build-menu` を自分で撮り直した `web-3-build-menu-1280x720.png` と比べる（2560×1080 も同じ口で撮り、参照 `web-0e81bbe3c3-2560x1080/3-build-menu.png` と画素が全一致することを確かめた）。台帳の 1280×720 の参照 `web-a9d5123537`（=`web-869b2d4264`）はマスの絵の位置だけが今の Web と違う（af5fcc9cb3「設置対象アイコンを枠中央へ」より前の撮影。差は絵とホットバーの bbox (394,328)-(495,707) だけ） | 古い参照のまま比べる: マスの絵が 4px 左上にずれた古い Web を正として UITK を崩すことになる | agent 既定（Task 10 実測） |
| D-60 | 縦バーの下の逃げは `clip-inset + 12px` の 24px（`--game-scroll-view-scrollbar-inset-bottom: 24px`）。Web の DOM で `--sa-corner-width` は 12px（横バーは寸法 0 なのに Mantine が縦バーの幅を入れる）、縦バーは y 254.41〜570（高さ 315.59）、つまみ 46.61px。UITK は 0 のままだと溝が 12px 長くつまみが 48px だった | D-52 の「4px」: この画面の DOM の実測は 12px | agent 既定（Task 10 実測） |
| D-61 | Task 10 の DEMO の撮影は EditMode の `UiRuntimePanelCapture.Render` に、`UiLayerStack` の Backdrop/Viewport へ `ScreenBackdropView`（幕 ON。BuildMenu の traits は Backdrop=true）、Screen/Stage へ View を載せ、地は Web の DEMO と同じ `public/mock-orange-gradient.png`（cover）にして撮る。言語は `TrySetChosenLanguage("japanese")`＋`KeepCurrentDictionary`、マスタはテスト mod（ブロック 90）。撮った後に保存言語を戻す | 地を単色 `#9c7b42` にする（S2 の前例）: パネルの面が半透明なので地と幕が違うと面の色の比較ができない（幕なしでは面が (54,41,36)、Web は (35,29,30)） | agent 既定（Task 10 実測） |
| D-62 | Close で遷移が許可表に断られたら `UiCommandRejectReason.BuildMenuCloseRefused`（非良性・wire `transition_not_allowed`・トーストを出す）を返す。閉じた後の再送は `BuildMenuNotOpen`（良性）のまま。enum の末尾・3 switch・localization.csv に 1 行ずつ | 到達しない前提で `InvalidOperationException`（enum を変えない）: 入れ子ポーズや許可表の変更で到達し得る。前例どおり LogWarning だけ: 閉じないのにトーストが出ず、利用者が気づけない | agent 既定（moores-code-review D1 推奨案 A。指揮の「確認を待たずに進める」） |
| D-63 | Model の組み直しはフレーム末の 1 回に畳むまま（D-19 を維持。設計書 A-57 への例外の追記は integrator へ依頼） | 兄弟の Model と同じく同期で `Rebuild()`: スロット通知 1 件ごとに全マスを組み直す（D-19 の棄却理由と同じ） | agent 既定（moores-code-review D2 推奨案 A。ユーザー裁定ではない） |
| D-64 | `BuildMenuMaterialLine` は `BuildMenuMaterialRow` を丸ごと持つ（`Row`・`IsInsufficient`）。消費側は `line.Row.*` を読む | Line だけを残し Row を消す: Factory が不足判定の前に作る行の型が無くなり、Catalog へ写す列と VM の行を別の手で組むことになる | agent 既定（moores-code-review D3 推奨案 A） |
| D-65 | 本番から呼ばれない `BuildMenuShortage.InsufficientItems` を消し、テストは `IsItemInsufficient` と VM の `Materials[].IsInsufficient` を見る | Line を返す形に変えツールチップからも呼ぶ: 不足の判定は VM の ctor で 1 回だけ（`IsInsufficient`）で足り、入口が 2 つに戻る | agent 既定（moores-code-review D4 推奨案 A） |

**未確認**（実装で確かめ、違えば `done.md` の「未確認」へ）:
- C3-b の `ModeSwitch<TValue>`・`GameScrollView` の口・`UiDragDrop` の綴り（Task 1 Step 1 で照合）
- `PointerDownEvent.GetPooled()` を `SendEvent` で送ったとき TrickleDown の受け手に届くか（Task 7 の介入の L1。届かなければ `simulate` の押下に替える）
- `--mode-switch-option-height` を C3-b の `ModeSwitch` が USS の `var()` で読むか `customStyle` で読むか（どちらでも部品の要素のクラスで効く。効かなければ `BuildMenuLayoutTest` 3 件目が赤）
- `UiMotionHandle.Cancel` が途中の位置で止めるか（終値へ飛ばさないか）
- 明るい半透明（`--mode-switch-selected-face`）の α の換算（罠 c。Task 10 の差の表で見る）
- `p.PressKey(Key.Escape)` で PlaceBlock から GameScreen へ戻るか（Task 11）
- 2560×1080 の複製パネルで舞台の倍率が自動で入るか（Task 10 Step 2）

**本PR外のリファクタ提案**（設計検査の第3バケツ 5 件。どれも Web の系と UITK の系の重複で、UITK へ切り替える日に Web の系を消すとき橋ごと消える。それまでは突き合わせのテストで縛る。設計検査の 5 件の原文はこの fork の手元に無いので、計画の中で分かる重複から書いた。指揮の控えと違えば差し替える）:

| 既存側 | 新設側 | 根拠 |
|---|---|---|
| 橋 `S/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs` | `Client.Ui.State/BuildMenu/Source/BuildMenuEntryFactory.cs` | 種別・カテゴリ対・免除・1 セット設置の規則が同じ。`BuildMenuEntryFactoryTest` が橋の DTO と突き合わせる（D-1） |
| 橋 `BuildMenuMaterialAvailability.cs` | `BuildMenuMaterialRowsBuilder.cs` | 必要素材の行と BP の貼り付け費用が同じ規則 |
| 橋の `ResolveIconUrl` | `Client.Ui.State/PlacementTarget/PlacementTargetIconResolver.cs`・`Client.Ui/Content/PlacementTargetIcons.cs` | 絵の出どころの決め方が同じ（設計書 §5.1-1。ビルドメニューとホットバーの 2 機能が共有） |
| 橋の `BuildMenuActions`・`UiStateActions` | `Client.Ui.State/BuildMenu/Commands/BuildMenuCommands.cs` | 選択・BP 削除・閉じるの前提と拒否理由が同じ（Web の wire code を `UiCommandRejectReason` へ写した。D-4） |
| 橋のカテゴリ対の解決（`BuildMenuEntryDtoFactory` の内側） | `BuildMenuCategoryPairResolver.cs` | 対象 → カテゴリ・サブカテゴリの決め方が同じ |

- `BuildMenuEntryKindName` の `Client.Ui.State/PlacementTarget/` への昇格（ホットバーの testid が種別の文字列を要るようになったら。絵の出どころは D-29 で最終の置き場に作った）

---

レーンの起動は指揮が行う（実行計画 §4.1）。

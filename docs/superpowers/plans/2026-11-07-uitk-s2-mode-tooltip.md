# UITK S2-mode-tooltip（段階 2 画面: 操作モード HUD・削除モードの警告帯・CursorTooltip）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/modeHud`（配置モード HUD・削除箱の高さ・削除モードの上下警告帯）と `shared/tooltip`（CursorTooltip）を、C2-a・C2-c の部品（`GamePanel`(craft)・`HudSectionHeading`・`TooltipFace`）で Web と同じ見た目の UITK 画面にし、Model を Web の topic の源（`PlaceSystemStateController`・`PlacementHeightOffset`・`UIStateControl`・`MouseCursorTooltipState`）から直に購読して、L1・並べ比べ（720p・2560×1080）・録画 `misc/cursor-tooltip-follows-crosshair`（全体スイッチ UITK）まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。Model（`Client.Ui.State/ModeHud`・`CursorTooltip`）は Client.Game の源を購読して不変の ViewModel を `IReadOnlyReactiveProperty` で配り、View（`Client.Ui/Screens/ModeHud`・`CursorTooltip`）は Model を知らず `Render(ViewModel)` で部品へ `SetXxx` で流す。Presenter が起動時に 1 回だけ View を層へ載せ、`_model.Current.Subscribe(view.Render)` で繋ぐ（前例 `ToastLayerPresenter`・`PauseMenuScreenPresenter`）。操作モード HUD は `UiLayer.Chrome`・`UiFamily.Viewport`（Web の `.viewportOverlay`）、CursorTooltip は `UiLayer.Tooltip`・`UiFamily.Viewport`（Web の Portal＋`scale(var(--ui-scale))`）に置く。CursorTooltip の位置は Web の `tooltipPosition.ts`（`OFFSET = 12` の実画面 px で端に押し戻す）を `CursorTooltipPlacement` に写し、パネル単位⇔実画面 px を `panel.scaledPixelsPerPoint`（[A-41]）で換算する。カーソルロック中は `ICursorTooltipScreen` が `ScreenCenter.GetPosition()` をパネル座標にして返し、画面中央を指したものとして置く。削除モードの斜線帯は画面内の部品（`Screens/ModeHud/Parts/`）で Painter2D が描く（設計書 §2.7）。見た目の値は Web と同名のトークン（`Tokens.uss` の `--operation-hud-*`・`--tooltip-*`・`--delete-mode-warning-*`）だけを使い、部品のつまみ（`--hud-*`）は画面ルート `.mode-hud` で上書きする。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`generateVisualContent`/Painter2D・`CustomStyleResolvedEvent`・`PointerMoveEvent`）、UniRx、VContainer、NUnit＋`com.unity.ui.test-framework`（`UITestFixture`・`PanelSimulator`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（Web の参照キャプチャ・test-name-parity）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`（repo の兄弟）、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`。Files 節と各コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（7 本。とくに「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1・§2.2・§2.6・§2.7・§2.8・§3・§4・§5.1・§7.1）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S2-mode-tooltip 行・§4.3・§5・§6.2・E-1・E-8・E-13・E-16）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ。この計画が設計書・完了条件と違う所は判断記録（D-3・D-4 ほか）と返答の notes に書いた

## Requirements

設計の出所: 実行計画 §1.4 の S2-mode-tooltip 行（完了条件の原文:「部品: `GamePanel`(craft)・`HudSectionHeading`・`TooltipFace`。斜線帯は画面内（Painter2D）。CursorTooltip は `OFFSET`（現在 12）参照・端で押し戻す（Web `W/shared/tooltip/tooltipPosition.ts` の clamp。旧記述「端で反転」は Web の実物と違った）・ロック中は画面中央。写す: modeHud 17（P6・I6・S5）、`shared/tooltip/*`(8)。録画 `misc/cursor-tooltip-follows-crosshair`」）、設計書 §2.7（斜線帯は画面内）・§2.8（`shared/tooltip` → `Screens/CursorTooltip/`＋`TooltipFace`）・[A-41]（ツールチップ等は舞台の倍率を保つ）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 操作モード HUD の Model: `UIStateControl` の状態・`PlaceSystemStateController` の対象・`PlacementHeightOffset` の高さ・言語の変化から `ModeHudViewModel`（判別 union: `ModeHudHidden`・`ModeHudPlacement`（対象の表示名・高さ。未選択の名前は `ModeHudText.None`）・`ModeHudDelete`（高さ））を組む。表示名は `IPlacementTarget.DisplayName` | `ModeHudViewModelFactoryTest`・`ModeHudPlacementNameTest` が緑（Web `PlacementModeHud.localization.test.ts` 6 件） |
| R-2 | 配置モード HUD: 実画面の右上（`--operation-hud-top` 24・`--operation-hud-right` 24）に幅 `--operation-hud-width` 288 の `GamePanel(craft)`、中に `HudSectionHeading`（題「Placement Mode」・FadeRule）と詳細 2 行（`Selected: {name}`・`Height: {height}`）。置けない理由の警告の行は持たない（D-7）。チュートリアルの錨 `placement.hud` | `ModeHudViewTest`・`ModeHudWideViewportTest` が緑（Web `operation-mode-hud.spec.ts` 1・4 件目、commonHud 1 件目） |
| R-3 | HUD の文字: 題 12px・`--text-muted`・字間は `var(--operation-hud-letter-spacing)`（I-fix1 で UITK の単位へ換算済みの 8.3333px）・下の罫線まで 5px・罫線から一覧まで 8px。詳細は 17px・行の高さ 25px・`--text-high-contrast`・項目間 5px | `ModeHudViewTest` の寸法・色の主張が緑。字間はトークンをそのまま使い、換算値を直書きしない（D-9） |
| R-4 | 削除モード: 実画面の上端と下端に高さ `--delete-mode-warning-band-height` 20 の帯 2 本。黄 `--delete-mode-warning-yellow`・黒 `--delete-mode-warning-black` の 117deg・32px 刻みの斜線を Painter2D で描く。下の帯に錨 `delete.hud`。右上に削除箱の高さ（`Height: {height}`）だけを出す | `DeleteModeWarningBandsTest`・`DeleteModeWarningStripeGeometryTest`・`ModeHudWideViewportTest` が緑（Web `operation-mode-hud.spec.ts` 5・6 件目） |
| R-5 | 操作モード HUD と CursorTooltip は入力を奪わない（Web の `pointer-events: none`）。HUD の下の要素は全部 `PickingMode.Ignore` | `ModeHudViewTest` の「どの要素も拾わない」・`CursorTooltipViewTest` の同主張が緑 |
| R-6 | CursorTooltip の Model: `MouseCursorTooltipState.OnPresentationChanged` を `CursorTooltipViewModel`（`UiText` の行の列。空で非表示）にする | `CursorTooltipModelTest` が緑 |
| R-7 | CursorTooltip の見た目: `TooltipFace(cursor-tooltip, Cursor)`、行の testid `cursor-tooltip-line`、文字 18px・余白 6/10・最大幅 320・`--tooltip-face`・角丸 4px。`UiLayer.Tooltip`・`UiFamily.Viewport` に置き、舞台の倍率で拡大する | `CursorTooltipViewTest` が緑（Web `CursorTooltip.test.ts` 1・2・4 件目、commonHud 3・4 件目） |
| R-8 | CursorTooltip の位置: Web `tooltipPosition.ts` と同じく、実画面 px で `x = max(12, min(px+12, vw-w-12))`（y も同じ）。12 は `CursorTooltipPlacement.Offset` の 1 か所で持ち、Web の `OFFSET` と同値であることを L1 が Web のファイルから確かめる。カーソルロック中は画面中央を指したものとして置く。非表示中のポインタ移動も覚え、表示した最初のフレームからその位置に出る。言語が変わって幅が変われば置き直す | `CursorTooltipPlacementTest`・`CursorTooltipOffsetWebParityTest`・`CursorTooltipPositionViewTest` が緑（Web `tooltipPosition.test.ts`・`CursorTooltip.test.ts` 6・7 件目、commonHud 5 件目） |
| R-9 | 写さない Web テスト（`CursorTooltip.test.ts` 3 件目・`modeHudDesign.test.ts` 5 件目）は `docs/uitk/disposition.md` に `捨てる` で理由を書く | `node tools/uitk/test-name-parity.mjs --bundle 2` の出力で、このレーンの 7 ファイル（`alwaysOnHudWheel.test.ts` の持ち分 3 行を含む）の未対応が 0 |
| R-10 | 並べ比べ: 参照キャプチャ `2-placement`・`2-placement-connect-tool`・`2-delete-bar`・`2-tooltip`・`2-tooltip-multiline` と UITK を 1280×720 と 2560×1080 で撮って並べ、差の表を `done.md` に書く | 5 状態 × 2 解像度の組が報告ページにあり（`2-placement-unavailable` は差の表に `捨てる`。D-7）、差の表に「同じ／直した／LX-n／捨てる」のどれかが全行にある |
| R-11 | 登録: `MainGameUiRegistration` の画面登録区画に 2 行（`ModeHudGameUiRegistration.Register(builder);`・`CursorTooltipGameUiRegistration.Register(builder);`）、`GameUiTheme.tss` の `Screens` 区画に `@import` 2 行。Model は自分の GameUi 登録ファイルで登録する | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`）が緑 |
| R-12 | 録画 `misc/cursor-tooltip-follows-crosshair` を全体スイッチ UITK で通す（GameUi の系ではロック中に隅へ注入しても中央近くに居続けることを主張する分岐を足す）。録画前に全体スイッチが GameUi であることを確かめる | `result.json` が `Success: true`、録画 mp4 があり、`done.md` に UITK で回した証拠（`UiInputExclusivity.ActiveUiSystem` の値）がある |
| R-13 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑・未記帳 0 → `gh pr merge --merge` → bd を閉じる | `git log origin/feature/uitk` にこのレーンのマージコミット。`done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 部品（`GamePanel`・`HudSectionHeading`・`FadeRule`・`TooltipFace`）・Foundation・トークン USS（`Tokens.uss`・`DerivedTokens.uss`・`MantineTokens.uss`）・`UiLayer`・`UiScreenTraits`・`AnchorIds` は変えない。要るものは `done.md` の「integrator への依頼」に書く
- 常駐 HUD（`Screens/Shell/` の幕・ヴィネット、Crosshair・KeyHintHud・ProgressBar・TrainRidingHud・CurrentChallengeHud）は作らない（S2-hud）。KeyHintHud が使う `WheelOwnedByTool` はこの Model に入れない
- `HoverTooltip`（`HoverTooltipHost`）は作らない（C3-b）。チュートリアルの錨の描画（`Screens/Tutorial/**`）は作らない（S2-tutorial。このレーンは `TutorialAnchor.Mark` で錨の名前を付けるだけ）
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は一切変えない。置けない理由（`unavailableReason`）は写さない（C# に書き手が無く、Web の橋も常に空文字。D-7）
- L3（EditModeInPlayingTest）は持たない（段の締め S2-ladder。D-16）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s2-mode-tooltip --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる
- **依存**: C2-a（#1604）・C2-c（#1605）がマージ済みの `origin/feature/uitk`（df955f1dc6 以降）。C2-b の部品は使わない
- **使う部品と公開 API（実物 `S/Client.Ui/**` から写した。Task 1 Step 1 で実物と照合し、違えば名前だけ読み替えて `done.md` に書く）**

```csharp
// namespace Client.Ui.Components.Panels（S/Client.Ui/Components/Panels/GamePanel/）
public enum GamePanelFrame { Default, Craft }
public sealed class GamePanel : VisualElement
{
    public override VisualElement contentContainer { get; }   // 本文 .game-panel__body
    public GamePanel(string testid, GamePanelFrame frame);    // name = testid。Craft は .game-panel--craft と .game-panel__craft-grip（internal GamePanelCraftGrip）を持つ
    public void SetTitle(UiText title);
    public void SetTitleAction(VisualElement action);
}
// ルート・_header・_body は既定の PickingMode.Position、装飾は Ignore（実物）

// namespace Client.Ui.Components.Decor（S/Client.Ui/Components/Decor/HudSectionHeading/）
public sealed class HudSectionHeading : VisualElement
{
    public override VisualElement contentContainer { get; }   // 一覧 .hud-section-heading__list
    public HudSectionHeading(string labelTestid, LocalizationKey label); // 小ラベル（LocalizedLabel, name = labelTestid）→ FadeRule → 一覧
}
// USS クラス: .hud-section-heading・__label・__rule・__list。つまみ: --hud-label-font-size・--hud-label-letter-spacing・--hud-label-rule-gap・--hud-rule-list-gap・--hud-rule-width（既定は DerivedTokens.uss で --challenge-hud-* と 100%）

// namespace Client.Ui.Components.Text（S/Client.Ui/Components/Text/TooltipFace/）
public enum TooltipFaceLook { Cursor, Hover }
public sealed class TooltipFace : VisualElement
{
    public TooltipFace(string testid, TooltipFaceLook look);  // PickingMode.Ignore。行の name は "<testid>-line"、行クラス .tooltip-face__line
    public void SetText(UiText text);
    public void SetLines(IReadOnlyList<UiText> lines);         // 折り返し中は .tooltip-face--wrapped（幅 320）
}

// namespace Client.Ui.Foundation.Text
public class LocalizedLabel : Label { public LocalizedLabel(); public void SetKey(LocalizationKey key); public void SetText(UiText text); }
// 行の高さのつまみ --localized-label-line-height

// namespace Client.Ui.State.Text
public sealed class UiText
{
    public UiText(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams);
    public UiText(LocalizationKey key);
    public static UiText FromContent(ContentLocalizationKey key);
    public static UiText FromVerbatim(string text);
    public string Resolve();
}
// namespace Client.Localization: public LocalizationTextParam(string name, string value); Localize.OnLanguageChanged（IObservable<Unit>）

// namespace Client.Ui.Foundation.Anchor
public static class TutorialAnchor { public const string MarkerClassName = "tutorial-anchor"; public static void Mark(VisualElement element, params string[] ids); }
// namespace Client.Game.InGame.Tutorial.UIHighlight: AnchorIds.PlacementHud = "placement.hud"・AnchorIds.DeleteHud = "delete.hud"

// namespace Client.Ui.Foundation.Theme
public static class UssCustomStyleReader
{
    public static bool TryReadPixels(ICustomStyle style, string propertyName, out float pixels);
    public static bool TryReadColor(ICustomStyle style, string propertyName, out Color color);
    public static bool TryReadNumber(ICustomStyle style, string propertyName, out float number);
    public static bool TryReadMilliseconds(ICustomStyle style, string propertyName, out float milliseconds);
} // 読めないときは Debug.LogError を出して false。角度（deg）を読む口は無い（D-10）

// namespace Client.Ui.Foundation.Root / Layers / Stage
public class GameUiRoot : MonoBehaviour { public VisualElement GetRootElement(); public VisualElement GetLayer(UiLayer layer, UiFamily family); }
public enum UiFamily { Stage, Viewport }
// UiLayer の順: Backdrop, BehindStage, ViewportStage, Screen, OverlayPanel, Chrome, SkitStage, PauseMenu, Tooltip, Grab, WorldPin, Modal, SkitTransition, Toast, Tutorial, Reconnect, FullScreenGate

// namespace Client.Ui.Foundation.Theme（C2-c）: UiTypographyClass.KeyHintText = "key-hint-text"
```

- **Model の源（Client.Game の実物。Web の topic `ui.placement_mode`・`ui.tooltip` を流している `S/Client.WebUiHost/Game/Topics/C2/PlacementModeTopic.cs`・`TooltipTopic.cs` と同じ購読元）**

```csharp
// namespace Client.Game.InGame.BlockSystem.PlaceSystem
public class PlaceSystemStateController { public IPlacementTarget CurrentTarget { get; } public IObservable<IPlacementTarget> OnTargetChanged { get; } }
// namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
public class PlacementHeightOffset { public int Value { get; } public IObservable<int> OnChanged { get; } } // ReactiveProperty（購読時に現在値を流す）
// namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
public interface IPlacementTarget : IEquatable<IPlacementTarget> { Guid Id { get; } PlacementTargetKind Kind { get; } string DisplayName { get; } /* C# の表示名の正 */ }
// ctor: BlockPlacementTarget(Guid, BlockDirection?)・ConnectToolPlacementTarget(Guid)・TrainCarPlacementTarget(Guid)・BlueprintCopyPlacementTarget(Guid)・BlueprintPlacementTarget(Guid, string, BlueprintJsonObject)
// namespace Client.Game.InGame.UI.UIState
public class UIStateControl : MonoBehaviour { public IObservable<UIStateEnum> OnStateChanged { get; } public UIStateEnum CurrentState { get; } }
public enum UIStateEnum { GameScreen, PlayerInventory, SubInventory, PauseMenu, DeleteBar, Story, PlaceBlock, ChallengeList, ResearchTree, Debug, TrainHUDScreen, BuildMenu }
// namespace Client.Game.InGame.UI.Tooltip
public class MouseCursorTooltipState : IMouseCursorTooltip { public IObservable<TooltipPresentation> OnPresentationChanged { get; } /* ReactiveProperty */ public TooltipPresentation GetPresentation(); public void Show(TooltipOwner owner, LocalizationKey key); public void Show(TooltipOwner owner, LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams); public void Show(TooltipOwner owner, IReadOnlyList<TooltipLine> lines); public void Hide(TooltipOwner owner); }
public readonly struct TooltipPresentation { public static readonly TooltipPresentation Hidden; public readonly IReadOnlyList<TooltipLine> Lines; public bool Visible { get; } }
public readonly struct TooltipLine { public readonly LocalizationKey Key; public readonly IReadOnlyList<LocalizationTextParam> TextParams; }
public sealed class TooltipOwner { }
// DI: MainGameInteractionRegistration が PlacementHeightOffset・PlaceSystemStateController・MouseCursorTooltipState（AsSelf ＋ IMouseCursorTooltip）を登録済み。UIStateControl はシーンの component として解決できる（UiRoutingState が同じく ctor で受けている）
// カーソルロックの唯一の書き手は Client.Input.InputManager.MouseCursorVisible(bool)（ロック前に画面中央へワープ）。読む前例は Client.Game/InGame/Control/UiPointerHitTest.cs の Cursor.lockState == CursorLockMode.Locked。画面中央の唯一の定義は Client.Input.ScreenCenter.GetPosition()（`ICursorTooltipScreen` の本番実装だけが読む）。ポインタ位置は Client.Input.HybridInput.GetMousePosition()（画面 px・左下原点）
```

- **層の向き**（設計書 §1.1）: `Client.Ui.State/**` は Client.Game・Client.Input・Client.Localization を参照してよい（asmdef 既存）。`Client.Ui/Screens/**` は `Client.Ui.State.<機能>`・Components・Foundation・Client.Input を参照してよい。Screens から `Client.Game` の型は ViewModel 越しにだけ受ける（例外: `AnchorIds`。前例 HudSectionHeadingGalleryEntry と同じく文字列定数だけを使う）
- **AGENTS.md の規約**: 1 ファイル 200 行未満・1 ディレクトリ 10 ファイルまで（.meta は数えない）・`partial`/`Func<>` 禁止・イベントは UniRx・try-catch 禁止・デフォルト引数禁止・単純な getter/setter プロパティ禁止（`{ get; private set; }` と式本体の読み取り専用は可）・主要処理に「// 日本語 → // English」の 2 行コメント（各 1 行）・メソッド内のローカル関数は `#region Internal`・初期化は `Initialize`・fail-closed の経路はログ・`.meta` は手で作らない・Prefab/シーンを手で書き換えない・`Update()` で毎フレームの同値判定をしない（変化は購読で受ける）
- **USS の規約**（ref-uitk-design・設計書 §3.2）: 画面 USS はルートのクラス（`.mode-hud`・`.cursor-tooltip`）で始まり、画面のクラスは `mode-hud__*`・`cursor-tooltip__*`。部品のクラスを書かない（部品の見た目はつまみで変える）。色は `var()` だけ、`z-index`・構造擬似クラス・合成の太字を使わない。USS の先頭に `/* web: <Web の CSS> */` を日英で書く。C# が値を要るときは画面のクラスで `--<名>: var(--token)` と受け直して `UssCustomStyleReader` で読む（`:root` の変数は customStyle へ継承されない。台帳「## 実測の結論」customStyle）
- **字間**: 字間トークンは I-fix1（PR #1606・設計書 A-70〜A-74）で UITK の単位（字の大きさの 1/100）へ生成器が換算済み（`--operation-hud-letter-spacing: 8.3333px`・`--key-hint-letter-spacing: 5.5px`）。画面は `var(--operation-hud-letter-spacing)` と `.key-hint-text` をそのまま使い、換算値を直書きしない（二重補正になる）
- **テストの置き場と名前**: `S/Client.Tests/Ui/Screens/{ModeHud,CursorTooltip}/`・`S/Client.Tests/Ui/State/{ModeHud,CursorTooltip}/`、namespace はディレクトリに合わせる。テスト名は日本語の挙動文。Web から写したものは `[Test]` の直前に `/// <web-test file="<W/ からの相対パス>">it/test の第 1 引数</web-test>`（describe は含めない。テンプレート文字列も展開しない。実行計画 E-13）。同じ Web テストを複数の NUnit が写すときは各 NUnit に同じ行を付けてよい
- **コマンド**: コンパイル `uloop compile --project-path ./moorestech_client`（期待: ErrorCount 0）。テスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。このレーンのテストフィルタ（以下 `$FILTER`）は `^Client\.Tests\.Ui\.(Screens\.(ModeHud|CursorTooltip)|State\.(ModeHud|CursorTooltip)|Conventions|Theme|Layers|Anchor|State\.PlacementTarget)\.`。ドメインリロード中のエラーは 45 秒待って再試行
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有ファイルは `GameUiTheme.tss` の `Screens` 区画の `@import` 2 行・`MainGameUiRegistration.cs` の画面登録区画の 2 行・`docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」「## 追従記録」の末尾・`docs/uitk/disposition.md` の「## 処遇」の末尾・`S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` の `AnchorIds.PlacementHud`・`DeleteHud` の 2 行の削除（在るときだけ）だけ（disposition.md への追記は設計書 §4.3「テスト名と由来の対応は docs/uitk/disposition.md に、レーンが写したときに書く」による）。`localization.csv` は足さない（使う文言は全部既存。Task 1 Step 2 で確かめる）。シナリオ `SKILL/scenarios/misc/cursor-tooltip-follows-crosshair.cs` はこのレーンが持つ（完了条件の録画）
- **新しい共有の申告**（設計書 §5.1-2）: 画面で作った汎用になりそうなもの（`ModeHudPickingIgnore`・`DegreeCustomStyle`）は 1 機能目なので `Screens/ModeHud/Parts/` に置く。2 機能目で同じものが要ると分かったら作らず、`done.md` の「## integrator への依頼」に「Foundation へ昇格: <名前>・使う画面 2 つ・API」を書く
- **参照キャプチャ**: `LOGS/uitk/reference-captures/web-a9d5123537/`（1280×720。`manifest.json`・`index.md`）。2560×1080 の参照は無いので Task 10 Step 1 で撮る
- **並べ比べの画像の原本**は worktree の gitignore 下 `moorestech_client/.uloop/outputs/compare/S2-mode-tooltip/` に置き、`moorestech_logs` へ入れない。`moorestech_logs` へ書く前に `LOGS/README.md` を読む
- **報告**: `LOGS/harness/uitk/S2-mode-tooltip/done.md`（1 行目「状態: 完了」。途中で止まるときは「状態: 停止（理由）」）。節は「証拠」「差の表」「要裁定」「integrator への依頼」「未確認」

## 設計検査記録

- 配置検査: 実施済み / 違反4件・修正4件 / View の購読・中央の二重定義・死活表を直し、disposition は設計書 §4.3 で可
- Phase 2.6: 実施済み / 強2・弱6・第3バケツ1 / VM を判別 union に、倍率を scaledPixelsPerPoint へ。裁定は agent 既定で判断記録へ

## 死活表（Phase 2.5）

全体スイッチ UITK（`UiInputExclusivity.ActiveUiSystem == GameUi`）で起動したとき、Web の系で動いていた振る舞いが生きるか。`MainGameStarter` は `MainGameInteractionRegistration.Register`（両系で共通）の後に `MainGameUiRegistration.Register(builder, gameUiRoot, UiInputExclusivity.ActiveUiSystem)` を呼ぶ。

| 振る舞い | GameUi で生きるか | 根拠 |
|---|---|---|
| 配置 HUD（題・対象名・高さ） | 生きる | 源 `PlaceSystemStateController`・`PlacementHeightOffset` は `MainGameInteractionRegistration` が両系で登録し、`UIStateControl` はシーンの component。このレーンの `ModeHudModel`→`ModeHudView` が描く（Task 3・6・8） |
| 削除の帯と削除箱の高さ | 生きる | 同じ源の `DeleteBar` 状態と `PlacementHeightOffset` を `ModeHudDelete` で描く（Task 5・6） |
| カーソルツールチップ: 置けない（`placeBlockedByTerrain`）・遠すぎる（`placeTooFar`） | 生きる | 書き手 `PlacementFeedback.cs` は Client.Game で、`MouseCursorTooltipState`（両系で登録）へ書く。`CursorTooltipModel` が読む（Task 4・7） |
| カーソルツールチップ: 電線の数 | 生きる | 書き手 `ElectricWireFeedbackLines.cs` は Client.Game で、同じ `MouseCursorTooltipState` へ書く |
| カーソルツールチップ: World Target（`ui.tooltip.worldTarget`） | 実ゲームには元から無い | C# の書き手が無く、Web の mock `topicControls.ts` だけが流す。並べ比べの撮影（Task 10 Step 4）で `Show` を直に呼ぶ用途だけ |
| 錨 `placement.hud`・`delete.hud` のチュートリアル強調 | S2-tutorial のマージまで死ぬ | 錨の名前は `TutorialAnchor.Mark` で付けるが、GameUi で強調を描くのは S2-tutorial の `Screens/Tutorial/**`。`UIHighlightTutorialManager`（Client.Game）は動くが描き手が無い。notes で指揮へ返す |
| 錨の未移植の表（`TutorialAnchorCoveragePending`）との整合 | 生きる（手順で保つ） | S2-tutorial が先に入れば表に `placement.hud`・`delete.hud` の行があり、`Mark` を付けたこのレーンが同じ PR で消さないと `TutorialAnchorCoverageTest` が「印済みなのに未移植」で赤になる。Task 6 Step 6 と Task 13 Step 2 で消す |
| HUD の下の入力の素通し | 生きる | 部分木は全部 `PickingMode.Ignore`（`ModeHudPickingIgnore`・`TooltipFace`）で、`GameUiPointerProbe` の picking は Ignore を拾わないので、HUD の下のワールド操作へ届く（Task 6・7 の「拾わない」L1、Task 8 Step 4 の `Pick`） |

---

## File Structure

| パス | 責務 | 新規／変更 |
|---|---|---|
| `S/Client.Ui.State/ModeHud/ModeHudViewModel.cs` | 判別 union（抽象基底＋sealed の `ModeHudHidden`・`ModeHudPlacement`・`ModeHudDelete` を 1 ファイルに置く。前例 `HotbarDropAction.cs`）。`Hidden` の 1 値を持つ | 新規 |
| `S/Client.Ui.State/ModeHud/ModeHudText.cs` | 有無を型で持つ文字列（`None`・`Of`） | 新規 |
| `S/Client.Ui.State/ModeHud/ModeHudViewModelFactory.cs` | UI 状態・対象・高さから ViewModel を組む純関数 | 新規 |
| `S/Client.Ui.State/ModeHud/ModeHudModel.cs` | 源 4 本を購読し `Current` で配る（`IInitializable`） | 新規 |
| `S/Client.Ui.State/CursorTooltip/CursorTooltipViewModel.cs` | 不変の ViewModel（`UiText` の行の列）。値の等価 | 新規 |
| `S/Client.Ui.State/CursorTooltip/CursorTooltipModel.cs` | `MouseCursorTooltipState` を購読し `Current` で配る | 新規 |
| `S/Client.Ui.State/CursorTooltip/CursorTooltipPlacement.cs` | Web `tooltipPosition.ts` の写し（`Offset = 12`・`Clamp`・`PlaceInPanel`） | 新規 |
| `S/Client.Ui/Screens/ModeHud/ModeHudPresenter.cs` | View を Chrome/Viewport へ 1 回載せる | 新規 |
| `S/Client.Ui/Screens/ModeHud/ModeHudView.cs` | 画面ルート `.mode-hud`。`Render(ModeHudViewModel)` が型で 3 つの子を出し分け | 新規 |
| `S/Client.Ui/Screens/ModeHud/ModeHud.uss` | 画面の USS（つまみの上書き・配置・文字） | 新規 |
| `S/Client.Ui/Screens/ModeHud/Parts/PlacementModeHudPanel.cs` | 右上の `GamePanel(craft)`＋`HudSectionHeading`＋詳細と警告 | 新規 |
| `S/Client.Ui/Screens/ModeHud/Parts/DeleteBoxHeightHud.cs` | 右上の削除箱の高さ 1 行 | 新規 |
| `S/Client.Ui/Screens/ModeHud/Parts/PlacementHeightText.cs` | 高さの `UiText`（`Height: {height}`）を組む唯一の関数 | 新規 |
| `S/Client.Ui/Screens/ModeHud/Parts/DeleteModeWarningBands.cs` | 全面の帯 2 本の入れ物（上・下。下に錨） | 新規 |
| `S/Client.Ui/Screens/ModeHud/Parts/DeleteModeWarningBand.cs` | 帯 1 本。黒の背景＋黄の斜線を Painter2D で描く | 新規 |
| `S/Client.Ui/Screens/ModeHud/Parts/DeleteModeWarningStripeGeometry.cs` | CSS `repeating-linear-gradient` の黄の帯を矩形で切った凸多角形の列（純関数） | 新規 |
| `S/Client.Ui/Screens/ModeHud/Parts/DegreeCustomStyle.cs` | customStyle の `117deg` を度の数に読む（1 機能目。昇格候補） | 新規 |
| `S/Client.Ui/Screens/ModeHud/Parts/ModeHudPickingIgnore.cs` | 部分木を全部 `PickingMode.Ignore` にする（Web `pointer-events: none` を写す唯一の書き手。昇格候補） | 新規 |
| `S/Client.Ui/Screens/CursorTooltip/CursorTooltipPresenter.cs` | View を Tooltip/Viewport へ 1 回載せる | 新規 |
| `S/Client.Ui/Screens/CursorTooltip/CursorTooltipView.cs` | 画面ルート `.cursor-tooltip`。`TooltipFace` を置き、`Render` と `SetPointer` を受けて位置を決める（中央は計算しない） | 新規 |
| `S/Client.Ui/Screens/CursorTooltip/CursorTooltip.uss` | 画面の USS（絶対配置・拾わない） | 新規 |
| `S/Client.Ui/Screens/CursorTooltip/CursorPointerTracker.cs` | パネルの `PointerMoveEvent` を受けて View へポインタを渡す | 新規 |
| `S/Client.Ui/Screens/CursorTooltip/ICursorTooltipScreen.cs` | 実画面の問い合わせ（錨＝ロック中は画面中央・それ以外は最新のポインタ／今のポインタ／パネル 1 単位の実画面 px） | 新規 |
| `S/Client.Ui/Screens/CursorTooltip/UnityCursorTooltipScreen.cs` | 本番の実装（`Cursor.lockState`・`ScreenCenter.GetPosition()`・`panel.scaledPixelsPerPoint`・`HybridInput`・`RuntimePanelUtils.ScreenToPanel`） | 新規 |
| `S/Client.Starter/Registration/GameUi/ModeHudGameUiRegistration.cs` | Model と Presenter の登録 | 新規 |
| `S/Client.Starter/Registration/GameUi/CursorTooltipGameUiRegistration.cs` | Model・画面の問い合わせ・Presenter の登録 | 新規 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画に 2 行 | 変更（共有区画） |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | `Screens` 区画に `@import` 2 行 | 変更（共有区画） |
| `S/Client.Tests/Ui/State/ModeHud/ModeHudViewModelFactoryTest.cs` | 種別・高さ・削除箱の主張 | 新規 |
| `S/Client.Tests/Ui/State/ModeHud/ModeHudPlacementNameTest.cs` | 対象の表示名（block・raw・connectTool・trainCar・BlueprintCopy） | 新規 |
| `S/Client.Tests/Ui/State/ModeHud/PlacementNameTestDictionary.cs` | 一時 mod の辞書へ導出キーを合成するテスト補助 | 新規 |
| `S/Client.Tests/Ui/State/CursorTooltip/CursorTooltipPlacementTest.cs` | 位置の純関数 | 新規 |
| `S/Client.Tests/Ui/State/CursorTooltip/CursorTooltipOffsetWebParityTest.cs` | `Offset` と Web の `OFFSET` の同値 | 新規 |
| `S/Client.Tests/Ui/State/CursorTooltip/CursorTooltipModelTest.cs` | 表示状態 → 行の列 | 新規 |
| `S/Client.Tests/Ui/Screens/ModeHud/ModeHudViewModelSamples.cs` | Web の fixture 値の ViewModel | 新規 |
| `S/Client.Tests/Ui/Screens/ModeHud/ModeHudViewTest.cs` | 配置 HUD（P6 1 件目・commonHud 1 件目） | 新規 |
| `S/Client.Tests/Ui/Screens/ModeHud/ModeHudWideViewportTest.cs` | 2 解像度の右上固定と削除箱（P6 4・6 件目） | 新規 |
| `S/Client.Tests/Ui/Screens/ModeHud/DeleteModeWarningBandsTest.cs` | 上下の帯（P6 5 件目） | 新規 |
| `S/Client.Tests/Ui/Screens/ModeHud/DeleteModeWarningStripeGeometryTest.cs` | 斜線の幾何 | 新規 |
| `S/Client.Tests/Ui/Screens/ModeHud/ModeHudDesignTest.cs` | 構成・トークンの主張（S5） | 新規 |
| `S/Client.Tests/Ui/Screens/ModeHud/ContrastRatio.cs` | WCAG のコントラスト比（テスト補助） | 新規 |
| `S/Client.Tests/Ui/Screens/ModeHud/ModeHudPickingTest.cs` | 配置モード・削除モード警告・カーソルツールチップが入力を奪わない（Web `alwaysOnHudWheel.test.ts` の 3 行） | 新規 |
| `S/Client.Tests/Ui/Screens/CursorTooltip/CursorTooltipViewModelSamples.cs`・`FakeCursorTooltipScreen.cs` | 行の ViewModel と偽の実画面 | 新規 |
| `S/Client.Tests/Ui/Screens/CursorTooltip/CursorTooltipViewTest.cs` | 文言・行・面・未知のキーの印（C7 1・2・4・5、commonHud 3・4） | 新規 |
| `S/Client.Tests/Ui/Screens/CursorTooltip/CursorTooltipPositionViewTest.cs` | 位置・ロック・言語変更（C7 6・7、commonHud 5） | 新規 |
| `SKILL/scenarios/misc/cursor-tooltip-follows-crosshair.cs` | GameUi の系の分岐を足す | 変更 |
| `SKILL/scenarios/uitk/stage2/uitk-mode-hud-and-tooltip.cs` | 配置 HUD・高さ・削除の帯を UITK で通す録画（並べ比べの実画面の撮影も兼ねる） | 新規 |
| `docs/uitk/disposition.md` | 「## 処遇」の末尾に 2 行（設計書 §4.3） | 変更（共有区画） |
| `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` | 在れば `AnchorIds.PlacementHud`・`DeleteHud` の 2 行を消す（S2-tutorial が先にマージされた場合） | 変更（共有区画・在れば） |
| `docs/uitk/TODO.md` | 「## 実測の結論」末尾（ポインタの届き方・角度の読み方）、要れば「## 見た目の例外」末尾（LX-32〜34） | 変更（共有区画） |

ディレクトリのファイル数（.cs と .uss）: `Screens/ModeHud/` 3・`Parts/` 8・`Screens/CursorTooltip/` 6・`State/ModeHud/` 4・`State/CursorTooltip/` 3・`Tests/.../Screens/ModeHud/` 8・`Screens/CursorTooltip/` 4・`State/ModeHud/` 3・`State/CursorTooltip/` 3。`Registration/GameUi/` は 2→4。すべて 10 以下。

**写すテストの件数（Web の実物を grep して数え直した。tests-ladder と同じ）**

| Web のファイル（`W/` から） | 件数 | 写す | 写さない（処遇） | 写し先 |
|---|---|---|---|---|
| `e2e/tests/modeHud/operation-mode-hud.spec.ts`（P） | 6（テンプレート 1 件を 1 と数える） | 6 | 0 | `ModeHudViewTest`・`ModeHudPlacementNameTest`・`ModeHudWideViewportTest`・`DeleteModeWarningBandsTest` |
| `src/features/modeHud/PlacementModeHud.localization.test.ts`（I） | 6 | 6 | 0 | `ModeHudViewModelFactoryTest`・`ModeHudPlacementNameTest` |
| `src/features/modeHud/modeHudDesign.test.ts`（S） | 5 | 4 | 1（`捨てる`。5 件目〈警告色の共有〉。D-7・D-12） | `ModeHudDesignTest` |
| `src/shared/tooltip/tooltipPosition.test.ts`（L） | 1 | 1 | 0 | `CursorTooltipPlacementTest` |
| `src/shared/tooltip/CursorTooltip.test.ts`（C） | 7 | 6 | 1（`捨てる`。3 件目〈content key〉） | `CursorTooltipViewTest`・`CursorTooltipPositionViewTest` |
| `src/features/commonHud/alwaysOnHudWheel.test.ts` | 1（`it.each` 9 行） | 3 行（配置モード・削除モード警告・カーソルツールチップ） | 0（他の 6 行は S2-hud の `AlwaysOnHudPickingTest`） | `ModeHudPickingTest` |
| `e2e/tests/system/commonHud.spec.ts` | 6 | 4（1・3・4・5 件目。3 件目は tooltip の部分だけで S2-hud と分担） | 0（2・6 件目と 3 件目の採掘・クロスヘアは S2-hud） | `ModeHudViewTest`・`CursorTooltipViewTest`・`CursorTooltipPositionViewTest` |

合計: modeHud 17 件（P6・I6・S5）のうち 16 件を写し 1 件は `捨てる`（S 5 件目。P6 1 件目は警告の行の主張だけを写さない。D-7）。`shared/tooltip/*` 8 件のうち 7 件を写し 1 件は `捨てる`。commonHud は 4 件に `<web-test>` を付ける。`alwaysOnHudWheel.test.ts` の「%s は入力を奪わない」は 3 行分（S2-hud 計画 52 行目・W2a 設計書 A-58 の割り当て）に同名の `<web-test>` を付ける。一部だけ写すテストの捨てた部分は D-32。

---
## Tasks

### Task 1: 前提の確認と記録の準備

**Files:**
- Create: `LOGS/harness/uitk/S2-mode-tooltip/done.md`（雛形。moorestech_logs 側）

**Interfaces:**
- Consumes: `origin/feature/uitk`（C2-a・C2-c マージ済み）、上の「使う部品と公開 API」「Model の源」
- Produces: bd のレーン ID（以下 `<bd>`）、`done.md` の雛形

- [ ] **Step 1: 作業場所と部品の実物を確かめる**

Run:
```bash
pwd
git log -1 --format='%H %s' origin/feature/uitk
grep -rn "public GamePanel(\|public void SetTitle\|enum GamePanelFrame" moorestech_client/Assets/Scripts/Client.Ui/Components/Panels/GamePanel/
grep -n "public HudSectionHeading(" moorestech_client/Assets/Scripts/Client.Ui/Components/Decor/HudSectionHeading/HudSectionHeading.cs
grep -rn "public TooltipFace(\|public void SetLines\|enum TooltipFaceLook" moorestech_client/Assets/Scripts/Client.Ui/Components/Text/TooltipFace/
grep -n "PlacementHud\|DeleteHud" moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/AnchorIds.cs
grep -n "section: Screens" "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git merge-base --is-ancestor e26010aa0c origin/feature/uitk && echo i-fix1-in
```
Expected: worktree の絶対パス、`GamePanel(string testid, GamePanelFrame frame)`・`HudSectionHeading(string labelTestid, LocalizationKey label)`・`TooltipFace(string testid, TooltipFaceLook look)`・`SetLines(IReadOnlyList<UiText> lines)`・`PlacementHud = "placement.hud"`・`DeleteHud = "delete.hud"`・`/* == section: Screens == */` が出る。最後の行は `i-fix1-in`（I-fix1 のマージコミット e26010aa0c が土台にある。字間トークンはこれを前提に `var()` で使う）。出なければ土台が古いので止めて指揮へ報告する。名前が違えば以後の計画を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <旧> → <実物>」と書く。ファイルが無ければ「C2-a/C2-c 未取り込み」として止め、指揮へ報告する

- [ ] **Step 2: 使う文言が辞書と生成キーにあることを確かめる**

Run:
```bash
grep -n "^ui.modeHud\.\(placementModeTitle\|deleteModeTitle\|selectedBlock\|placementHeight\)," Localization/localization.csv
grep -n "^ui.tooltip\.\(worldTarget\|placeBlockedByTerrain\|placeTooFar\|placeWireCost\)," Localization/localization.csv
grep -rln "PlaceWireCost" moorestech_client/Assets/Scripts | head -3
```
Expected: CSV に 8 行（english `Placement Mode`・`Delete Mode`・`Selected: {name}`・`Height: {height}`・`World Target`・`Blocked by terrain`・`Too far away`・`Wire x{count}`）。生成キーは `LocalizationKeys.Ui.ModeHud.PlacementModeTitle`・`.DeleteModeTitle`・`.SelectedBlock`・`.PlacementHeight`、`LocalizationKeys.Ui.Tooltip.WorldTarget`・`.PlaceBlockedByTerrain`・`.PlaceTooFar`・`.PlaceWireCost`（namespace `Mooresmaster.Localization.Generated`。前例 `S/Client.Ui.Editor/Gallery/Entries/Decor/HudSectionHeadingGalleryEntry.cs` が `LocalizationKeys.Ui.ModeHud.PlacementModeTitle` を使っている）。生成名が違えば実物で読み替える。`localization.csv` は書かない

- [ ] **Step 3: bd を積み、done.md の雛形を置く**

Run:
```bash
bd create "UITK S2-mode-tooltip: 操作モード HUD・削除の警告帯・CursorTooltip を UITK へ" --parent moorestech-albof -t task -p 2
bd update <bd> --claim
mkdir -p ../moorestech_logs/harness/uitk/S2-mode-tooltip
```
`done.md` に次を書く（1 行目は終わるまで「状態: 作業中」）:
```markdown
状態: 作業中

# S2-mode-tooltip 作業報告

## 証拠
## 差の表
## 要裁定
## integrator への依頼
## 未確認
```
Expected: bd の ID が出る。以後 `<bd>` をその ID で読む

---

### Task 2: CursorTooltipPlacement（Web `tooltipPosition.ts` の写し）

**Files:**
- Create: `S/Client.Ui.State/CursorTooltip/CursorTooltipPlacement.cs`
- Test: `S/Client.Tests/Ui/State/CursorTooltip/CursorTooltipPlacementTest.cs`・`CursorTooltipOffsetWebParityTest.cs`

**Interfaces:**
- Consumes: なし（UnityEngine の `Vector2`・`Mathf`）
- Produces:
  ```csharp
  namespace Client.Ui.State.CursorTooltip
  {
      public static class CursorTooltipPlacement
      {
          public const float Offset = 12f; // web: shared/tooltip/tooltipPosition.ts の OFFSET（実画面 px）
          public static Vector2 Clamp(Vector2 pointer, Vector2 size, Vector2 viewport);   // 全部実画面 px
          public static Vector2 PlaceInPanel(Vector2 pointerPanel, Vector2 sizePanel, Vector2 viewportPanel, float screenPixelsPerPanelPixel); // パネル単位で受けて返す
      }
  }
  ```

- [ ] **Step 1: 失敗するテストを書く**

`CursorTooltipPlacementTest.cs`:
```csharp
using Client.Ui.State.CursorTooltip;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.CursorTooltip
{
    /// <summary>
    /// ポインタの右下 12px に置き、画面の端では内側へ押し戻す（L0。Web の tooltipPosition.ts）
    /// Places 12px below-right of the pointer and pushes back inside at the edges (L0; Web tooltipPosition.ts)
    /// </summary>
    public class CursorTooltipPlacementTest
    {
        /// <web-test file="src/shared/tooltip/tooltipPosition.test.ts">keeps the tooltip inside the viewport</web-test>
        [Test]
        public void 画面の右下の端では内側へ押し戻す()
        {
            var placed = CursorTooltipPlacement.Clamp(new Vector2(790, 590), new Vector2(180, 80), new Vector2(800, 600));
            Assert.AreEqual(new Vector2(608, 508), placed);
        }

        [Test]
        public void 余白があればポインタの右下12pxに置き左上の端では12pxに留める()
        {
            Assert.AreEqual(new Vector2(652, 372), CursorTooltipPlacement.Clamp(new Vector2(640, 360), new Vector2(120, 40), new Vector2(1280, 720)));
            Assert.AreEqual(new Vector2(12, 12), CursorTooltipPlacement.Clamp(new Vector2(-50, -50), new Vector2(120, 40), new Vector2(1280, 720)));
        }

        [Test]
        public void パネル単位で受けても余白は実画面の12pxになる()
        {
            // 2560x1080 は Expand で 1.5 倍。パネルの 640,360 は実画面の 960,540
            // 2560x1080 is 1.5x under Expand; panel 640,360 is screen 960,540
            var placed = CursorTooltipPlacement.PlaceInPanel(new Vector2(640, 360), new Vector2(120, 40), new Vector2(1706.6666f, 720), 1.5f);
            Assert.AreEqual(640f + 12f / 1.5f, placed.x, 0.001f);
            Assert.AreEqual(360f + 12f / 1.5f, placed.y, 0.001f);
        }
    }
}
```

`CursorTooltipOffsetWebParityTest.cs`（前例 `S/Client.Tests/Ui/Input/WebSilenceThresholdWebParityTest.cs`）:
```csharp
using System.IO;
using System.Text.RegularExpressions;
using Client.Ui.State.CursorTooltip;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.CursorTooltip
{
    /// <summary>
    /// ポインタからの余白が Web の tooltipPosition.ts の OFFSET と同じ値であることを確かめる（L0。片側だけ変えると Web と位置がずれる）
    /// Verifies the pointer offset equals OFFSET in Web tooltipPosition.ts (L0; changing one side alone shifts the position from Web)
    /// </summary>
    public class CursorTooltipOffsetWebParityTest
    {
        private const string TooltipPositionPath = "moorestech_web/webui/src/shared/tooltip/tooltipPosition.ts";

        [Test]
        public void 余白はWebのOFFSETと同じ()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var text = File.ReadAllText(Path.Combine(root, TooltipPositionPath));

            // `const OFFSET = 12;` の数値を拾う
            // Pick the number from `const OFFSET = 12;`
            var match = Regex.Match(text, @"const\s+OFFSET\s*=\s*(\d+(?:\.\d+)?)");
            Assert.IsTrue(match.Success, $"OFFSET not found in {TooltipPositionPath}");
            Assert.AreEqual(float.Parse(match.Groups[1].Value), CursorTooltipPlacement.Offset);
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CursorTooltipPlacement` が無いので CS0103/CS0246 でコンパイルが失敗する

- [ ] **Step 3: 実装する**

```csharp
using UnityEngine;

namespace Client.Ui.State.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの左上の位置を決める。Web の shared/tooltip/tooltipPosition.ts の写し（端で押し戻す）
    /// Decides the cursor tooltip's top-left; a copy of Web shared/tooltip/tooltipPosition.ts (pushes back at the edges)
    /// </summary>
    public static class CursorTooltipPlacement
    {
        // web: moorestech_web/webui/src/shared/tooltip/tooltipPosition.ts の OFFSET。実画面 px で効く
        // web: OFFSET in moorestech_web/webui/src/shared/tooltip/tooltipPosition.ts; applies in screen pixels
        public const float Offset = 12f;

        public static Vector2 Clamp(Vector2 pointer, Vector2 size, Vector2 viewport)
        {
            return new Vector2(ClampAxis(pointer.x, size.x, viewport.x), ClampAxis(pointer.y, size.y, viewport.y));
        }

        public static Vector2 PlaceInPanel(Vector2 pointerPanel, Vector2 sizePanel, Vector2 viewportPanel, float screenPixelsPerPanelPixel)
        {
            // Web は実画面 px で計算するので、パネル単位を実画面 px へ直して計算し戻す
            // Web computes in screen pixels, so convert panel units to screen pixels and back
            var scale = screenPixelsPerPanelPixel;
            return Clamp(pointerPanel * scale, sizePanel * scale, viewportPanel * scale) / scale;
        }

        // Web と同じく max を外に置き、収まらない大きさでは左上の余白を優先する
        // Like Web, max is outermost so the top-left margin wins when the tooltip cannot fit
        private static float ClampAxis(float pointer, float size, float viewport)
        {
            return Mathf.Max(Offset, Mathf.Min(pointer + Offset, viewport - size - Offset));
        }
    }
}
```

- [ ] **Step 4: 通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.CursorTooltip\."`
Expected: `ErrorCount: 0`、4 件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/CursorTooltip moorestech_client/Assets/Scripts/Client.Tests/Ui/State/CursorTooltip
git commit -m "feat(uitk): CursorTooltip の位置を Web の tooltipPosition.ts から写す（S2-mode-tooltip）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: 操作モード HUD の Model（`Client.Ui.State/ModeHud`）

**Files:**
- Create: `S/Client.Ui.State/ModeHud/ModeHudViewModel.cs`（抽象基底と sealed 派生 `ModeHudHidden`・`ModeHudPlacement`・`ModeHudDelete` を 1 ファイル）・`ModeHudText.cs`・`ModeHudViewModelFactory.cs`・`ModeHudModel.cs`
- Test: `S/Client.Tests/Ui/State/ModeHud/ModeHudViewModelFactoryTest.cs`・`ModeHudPlacementNameTest.cs`・`PlacementNameTestDictionary.cs`

**Interfaces:**
- Consumes: `UIStateControl.CurrentState`・`OnStateChanged`、`PlaceSystemStateController.CurrentTarget`・`OnTargetChanged`、`PlacementHeightOffset.Value`・`OnChanged`、`Localize.OnLanguageChanged`、`IPlacementTarget.DisplayName`
- Produces（判別 union。設計書 §4.1 の「抽象基底＋sealed 派生」。前例 `BuildMenuViewModel`）:
  ```csharp
  namespace Client.Ui.State.ModeHud
  {
      // 「無い」を空文字で表さない文の値。HasValue が false なら Value は読まない
      public readonly struct ModeHudText : IEquatable<ModeHudText>
      {
          public static readonly ModeHudText None;
          public readonly bool HasValue;
          public readonly string Value;
          public static ModeHudText Of(string value);
      }
      public abstract class ModeHudViewModel : IEquatable<ModeHudViewModel>
      {
          public static readonly ModeHudViewModel Hidden;   // = ModeHudHidden の唯一の値
          public abstract bool Equals(ModeHudViewModel other);
      }
      public sealed class ModeHudHidden : ModeHudViewModel { }
      public sealed class ModeHudPlacement : ModeHudViewModel
      {
          public readonly ModeHudText Target;             // 設置対象の表示名。未選択の瞬間は None
          public readonly int Height;
          public ModeHudPlacement(ModeHudText target, int height);
      }
      public sealed class ModeHudDelete : ModeHudViewModel
      {
          public readonly int Height;
          public ModeHudDelete(int height);
      }
      public static class ModeHudViewModelFactory
      {
          public static ModeHudViewModel Create(UIStateEnum state, IPlacementTarget target, int height);
      }
      public sealed class ModeHudModel : IInitializable
      {
          public IReadOnlyReactiveProperty<ModeHudViewModel> Current { get; }
          public ModeHudModel(UIStateControl uiStateControl, PlaceSystemStateController placeSystemStateController, PlacementHeightOffset placementHeightOffset);
      }
  }
  ```

- [ ] **Step 1: 失敗するテストを書く（種別と高さ）**

`ModeHudViewModelFactoryTest.cs`:
```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.ModeHud;
using Game.Blueprint;
using NUnit.Framework;

namespace Client.Tests.Ui.State.ModeHud
{
    /// <summary>
    /// UI 状態で派生の型が決まり、削除箱は高さだけを持つ（L1。Web の PlacementModeHud.localization.test.ts）
    /// The UI state decides the derived type and the delete box carries only the height (L1; Web PlacementModeHud.localization.test.ts)
    /// </summary>
    public class ModeHudViewModelFactoryTest
    {
        [TestCase(UIStateEnum.PlaceBlock, typeof(ModeHudPlacement))]
        [TestCase(UIStateEnum.DeleteBar, typeof(ModeHudDelete))]
        [TestCase(UIStateEnum.GameScreen, typeof(ModeHudHidden))]
        [TestCase(UIStateEnum.PlayerInventory, typeof(ModeHudHidden))]
        public void UI状態が設置なら配置削除なら削除それ以外は出さない(UIStateEnum state, Type expected)
        {
            Assert.IsInstanceOf(expected, ModeHudViewModelFactory.Create(state, null, 0));
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">削除箱は共用高さの更新だけを表示する</web-test>
        [Test]
        public void 削除箱は対象を捨て共用の高さだけを持つ()
        {
            var guid = Guid.NewGuid();
            var target = new BlueprintPlacementTarget(guid, "STALE TARGET", new BlueprintJsonObject("STALE TARGET", new(), new(), new(), guid));
            var viewModel = ModeHudViewModelFactory.Create(UIStateEnum.DeleteBar, target, 3);
            Assert.AreEqual(new ModeHudDelete(3), viewModel);
            Assert.AreEqual(new ModeHudDelete(-1), ModeHudViewModelFactory.Create(UIStateEnum.DeleteBar, target, -1));
        }

        [Test]
        public void 対象が未選択なら名前は無いを型で持つ()
        {
            var viewModel = (ModeHudPlacement)ModeHudViewModelFactory.Create(UIStateEnum.PlaceBlock, null, 2);
            Assert.IsFalse(viewModel.Target.HasValue);
            Assert.AreEqual(viewModel, ModeHudViewModelFactory.Create(UIStateEnum.PlaceBlock, null, 2), "同じ値の組は等しい");
        }
    }
}
```
（`BlueprintJsonObject` の ctor は前例 `S/Client.Tests/Ui/State/PlacementTarget/PlacementTargetDisplayNameTest.cs` と同じ形）

- [ ] **Step 2: 失敗するテストを書く（表示名。P6 2・3 件目・I6 2〜6 件目）**

`PlacementNameTestDictionary.cs`（前例 `PlacementTargetDisplayNameTest.MergeContentNames` と同じ手順の補助。前例のファイルは変えない）:
```csharp
using System;
using System.IO;
using Client.Game.Localization;
using Client.Localization;
using Core.Master;
using Mod.Loader;
using UnityEngine;

namespace Client.Tests.Ui.State.ModeHud
{
    /// <summary>
    /// 一時 mod の辞書へ設置対象の導出キーを合成し、終わったら消して言語を戻す
    /// Composes placement-target derived keys into a temporary mod dictionary, then deletes it and restores the language
    /// </summary>
    public sealed class PlacementNameTestDictionary
    {
        private readonly string _directory;
        private readonly bool _hadSavedLanguage;
        private readonly string _savedLanguage;

        public PlacementNameTestDictionary()
        {
            _hadSavedLanguage = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguage = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            _directory = Path.Combine(Path.GetTempPath(), $"mode-hud-names-{Guid.NewGuid():N}");
        }

        public void UseLanguage(string languageCode)
        {
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, languageCode);
            Localize.Initialize();
        }

        // 行は "key,Source,english,japanese" の CSV 本文（見出し行を除く）
        // Rows are CSV body lines "key,Source,english,japanese" without the header
        public void Merge(string rows)
        {
            var modDirectory = Path.Combine(_directory, "mode-hud-names-mod");
            Directory.CreateDirectory(Path.Combine(modDirectory, "master"));
            Directory.CreateDirectory(Path.Combine(modDirectory, "localization"));
            File.WriteAllText(Path.Combine(modDirectory, "master", "modMeta.json"),
                "{\"id\":\"mode-hud-names\",\"name\":\"mode-hud-names\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}");
            File.WriteAllText(Path.Combine(modDirectory, "localization", "localization.csv"), "key,Source,english,japanese\n" + rows);
            Localize.MergeGameDictionaries(new ModsResource(_directory), new[] { new ModId("author:mode-hud-names") }, MasterSourceTextCollector.Collect());
        }

        public void Restore()
        {
            if (_hadSavedLanguage) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguage);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            Localize.Initialize();
        }
    }
}
```

`ModeHudPlacementNameTest.cs`:
```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState;
using Client.Game.Localization;
using Client.Ui.State.ModeHud;
using Game.Blueprint;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.ModeHud
{
    /// <summary>
    /// 配置 HUD の対象名は IPlacementTarget.DisplayName を今の辞書で解いたもの（L1。Web の PlacementModeHud.localization.test.ts と operation-mode-hud.spec.ts）
    /// The placement HUD's target name is IPlacementTarget.DisplayName resolved with the current dictionary (L1; Web PlacementModeHud.localization.test.ts and operation-mode-hud.spec.ts)
    /// </summary>
    public class ModeHudPlacementNameTest
    {
        private static readonly Guid BlockGuid = new("30000000-0000-4000-8000-000000000001");
        private static readonly Guid ConnectToolGuid = new("40000000-0000-4000-8000-000000000001");
        private static readonly Guid TrainCarGuid = new("8f9c2a51-0000-4000-8000-000000000001");
        private PlacementNameTestDictionary _dictionary;

        [SetUp]
        public void SetUp()
        {
            // マスタを読み、日本語で導出キーの辞書を合成する（言語を変えると合成が消えるので順は 言語 → 合成）
            // Load the master and compose the derived-key dictionary in Japanese (changing language drops the merge, so language first)
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _dictionary = new PlacementNameTestDictionary();
            _dictionary.UseLanguage("japanese");
            _dictionary.Merge(
                $"{ContentLocalizationKeys.BlockName(BlockGuid).Key},Assembler,Localized Assembler,組立機\n" +
                $"{ContentLocalizationKeys.ConnectToolName(ConnectToolGuid).Key},Wire Connect Tool,Wire Connect Tool,電線接続ツール\n" +
                $"{ContentLocalizationKeys.TrainCarName(TrainCarGuid).Key},Freight Car,Freight Car,貨物車両\n");
        }

        [TearDown]
        public void TearDown()
        {
            _dictionary.Restore();
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">block Guidを現在辞書の表示名へ解決する</web-test>
        [Test]
        public void ブロックは今の辞書の表示名になる()
        {
            Assert.AreEqual("組立機", TargetName(new BlockPlacementTarget(BlockGuid, null)));
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">block以外のraw表示名をそのまま維持する</web-test>
        [Test]
        public void 保存したブループリントは付けた名前のまま()
        {
            var guid = Guid.NewGuid();
            var blueprint = new BlueprintJsonObject("My Blueprint", new(), new(), new(), guid);
            Assert.AreEqual("My Blueprint", TargetName(new BlueprintPlacementTarget(guid, "My Blueprint", blueprint)));
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">connectTool GuidをGuid導出キーの表示名へ解決する</web-test>
        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">配置対象connectToolをGuidだけの配信から辞書表示名へ解決する</web-test>
        [Test]
        public void 接続ツールはGUIDから導いたキーの表示名になる()
        {
            Assert.AreEqual("電線接続ツール", TargetName(new ConnectToolPlacementTarget(ConnectToolGuid)));
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">trainCar GuidをGuid導出キーの表示名へ解決する</web-test>
        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">配置対象trainCarをGuidだけの配信から辞書表示名へ解決する</web-test>
        [Test]
        public void 列車車両はGUIDから導いたキーの表示名になる()
        {
            Assert.AreEqual("貨物車両", TargetName(new TrainCarPlacementTarget(TrainCarGuid)));
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">Blueprint Copyをtyped UI keyで解決する</web-test>
        [TestCase("english", "Blueprint Copy")]
        [TestCase("japanese", "ブループリントコピー")]
        public void ブループリントコピーはUIの辞書キーで解く(string languageCode, string expected)
        {
            _dictionary.UseLanguage(languageCode);
            Assert.AreEqual(expected, TargetName(new BlueprintCopyPlacementTarget(Guid.NewGuid())));
        }

        private static string TargetName(IPlacementTarget target)
        {
            var placement = (ModeHudPlacement)ModeHudViewModelFactory.Create(UIStateEnum.PlaceBlock, target, 0);
            Assert.IsTrue(placement.Target.HasValue);
            return placement.Target.Value;
        }
    }
}
```
（`ContentLocalizationKeys` の namespace は前例どおり。コンパイルで違えば前例の `using` に合わせる）

- [ ] **Step 3: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `Client.Ui.State.ModeHud` が無いのでコンパイルが失敗する

- [ ] **Step 4: 実装する**

`ModeHudText.cs`:
```csharp
using System;

namespace Client.Ui.State.ModeHud
{
    /// <summary>
    /// 操作モード HUD の「有るかもしれない文」。無いことを空文字ではなく HasValue で表す
    /// An optional text of the operation-mode HUD; absence is HasValue, not an empty string
    /// </summary>
    public readonly struct ModeHudText : IEquatable<ModeHudText>
    {
        public static readonly ModeHudText None = default;

        public readonly bool HasValue;
        public readonly string Value;

        private ModeHudText(string value)
        {
            HasValue = true;
            Value = value;
        }

        public static ModeHudText Of(string value) => new(value);
        public bool Equals(ModeHudText other) => HasValue == other.HasValue && Value == other.Value;
        public override bool Equals(object obj) => obj is ModeHudText other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(HasValue, Value);
    }
}
```

`ModeHudViewModel.cs`（抽象基底と sealed 派生 3 型を 1 ファイルに置く。判別 union を 1 ファイルにまとめる前例 `Client.Ui.State/Hotbar/Logic/HotbarDropAction.cs`・同 PR の `TutorialAnchorState.cs` と揃える）:
```csharp
using System;

namespace Client.Ui.State.ModeHud
{
    /// <summary>
    /// 操作モード HUD の表示状態（判別 union）。Web の App.tsx の uiState 分岐と topic ui.placement_mode の組の写し
    /// Display state of the operation-mode HUD (discriminated union); a copy of Web App.tsx's uiState branch with the ui.placement_mode topic
    /// </summary>
    public abstract class ModeHudViewModel : IEquatable<ModeHudViewModel>
    {
        public static readonly ModeHudViewModel Hidden = new ModeHudHidden();

        public abstract bool Equals(ModeHudViewModel other);
        public override bool Equals(object obj) => Equals(obj as ModeHudViewModel);
        public abstract override int GetHashCode();
    }

    // 何も出さない（GameScreen ほか）
    // Shows nothing (GameScreen and others)
    public sealed class ModeHudHidden : ModeHudViewModel
    {
        internal ModeHudHidden()
        {
        }

        public override bool Equals(ModeHudViewModel other) => other is ModeHudHidden;
        public override int GetHashCode() => 0;
    }

    // 配置モード: 右上の HUD
    // Placement mode: the top-right HUD
    public sealed class ModeHudPlacement : ModeHudViewModel
    {
        public readonly ModeHudText Target;
        public readonly int Height;

        public ModeHudPlacement(ModeHudText target, int height)
        {
            Target = target;
            Height = height;
        }

        public override bool Equals(ModeHudViewModel other) => other is ModeHudPlacement placement && Target.Equals(placement.Target) && Height == placement.Height;
        public override int GetHashCode() => HashCode.Combine(Target, Height);
    }

    // 削除モード: 上下の帯と右上の高さ
    // Delete mode: top and bottom bands with the top-right height
    public sealed class ModeHudDelete : ModeHudViewModel
    {
        public readonly int Height;

        public ModeHudDelete(int height)
        {
            Height = height;
        }

        public override bool Equals(ModeHudViewModel other) => other is ModeHudDelete delete && Height == delete.Height;
        public override int GetHashCode() => Height;
    }
}
```

`ModeHudViewModelFactory.cs`:
```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState;
using UnityEngine;

namespace Client.Ui.State.ModeHud
{
    /// <summary>
    /// UI 状態・設置対象・高さから操作モード HUD の表示状態を組む
    /// Builds the operation-mode HUD state from the UI state, placement target and height
    /// </summary>
    public static class ModeHudViewModelFactory
    {
        public static ModeHudViewModel Create(UIStateEnum state, IPlacementTarget target, int height)
        {
            // Web の App.tsx と同じく placeBlock で配置 HUD、deleteBar で帯と削除箱の高さを出す
            // Like Web App.tsx, placeBlock shows the placement HUD and deleteBar shows the bands and the delete-box height
            switch (state)
            {
                case UIStateEnum.PlaceBlock:
                    return new ModeHudPlacement(TargetName(), height);
                case UIStateEnum.DeleteBar:
                    // 削除箱は共用の高さだけを出す（Web の DeleteBoxHeightHud）
                    // The delete box shows only the shared height (Web DeleteBoxHeightHud)
                    return new ModeHudDelete(height);
                default:
                    return ModeHudViewModel.Hidden;
            }

            #region Internal

            ModeHudText TargetName()
            {
                // 設置モードで対象が未選択の瞬間は CurrentTarget が null になりうる。無いことは型で持つ
                // CurrentTarget can be null for a moment in placement mode; absence is carried by the type
                if (target == null) return ModeHudText.None;

                // 表示名はマスタ由来で空にならない前提。空なら開発者へ知らせ、表示は Web と同じくそのまま
                // Display names come from the master and should never be empty; tell developers and display as is, like Web
                if (target.DisplayName.Length == 0) Debug.LogError($"[ModeHud] empty DisplayName for placement target {target.Kind} {target.Id}");
                return ModeHudText.Of(target.DisplayName);
            }

            #endregion
        }
    }
}
```

`ModeHudModel.cs`:
```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.ModeHud
{
    /// <summary>
    /// UI 状態・設置対象・高さ・言語を購読し、操作モード HUD の表示状態を配る
    /// Subscribes to the UI state, placement target, height and language and publishes the operation-mode HUD state
    /// </summary>
    public sealed class ModeHudModel : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly PlaceSystemStateController _placeSystemStateController;
        private readonly PlacementHeightOffset _placementHeightOffset;
        private readonly ReactiveProperty<ModeHudViewModel> _current;

        public IReadOnlyReactiveProperty<ModeHudViewModel> Current => _current;

        public ModeHudModel(UIStateControl uiStateControl, PlaceSystemStateController placeSystemStateController, PlacementHeightOffset placementHeightOffset)
        {
            _uiStateControl = uiStateControl;
            _placeSystemStateController = placeSystemStateController;
            _placementHeightOffset = placementHeightOffset;
            _current = new ReactiveProperty<ModeHudViewModel>(Build());
        }

        public void Initialize()
        {
            // 変化の源 4 本。表示名は言語で変わるので言語の変化でも組み直す
            // Four change sources; the display name depends on the language, so rebuild on language changes too
            _uiStateControl.OnStateChanged.Subscribe(_ => Rebuild());
            _placeSystemStateController.OnTargetChanged.Subscribe(_ => Rebuild());
            _placementHeightOffset.OnChanged.Subscribe(_ => Rebuild());
            Localize.OnLanguageChanged.Subscribe(_ => Rebuild());

            // ctor から購読までの間の変化を取り込む
            // Pick up any change made between construction and subscription
            Rebuild();
        }

        private void Rebuild()
        {
            _current.Value = Build();
        }

        private ModeHudViewModel Build()
        {
            return ModeHudViewModelFactory.Create(_uiStateControl.CurrentState, _placeSystemStateController.CurrentTarget, _placementHeightOffset.Value);
        }
    }
}
```
（購読の解除は書かない: ゲーム寿命のオブジェクト〈AGENTS.md 既知の制約〉。前例 `PauseMenuModel`。Model の配線〈源 4 本の購読〉は `UIStateControl` が MonoBehaviour で L1 に立てにくいので、Task 8 の PlayMode 確認と Task 11 の録画 `uitk-mode-hud-and-tooltip` で確かめる。D-19）

- [ ] **Step 5: 通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.(ModeHud|PlacementTarget)\."`
Expected: `ErrorCount: 0`、ModeHud 12 件（Factory 6〈TestCase 4＋2〉・Name 6〈TestCase 2 を含む〉）と既存の PlacementTarget が PASS

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/ModeHud moorestech_client/Assets/Scripts/Client.Tests/Ui/State/ModeHud
git commit -m "feat(uitk): 操作モード HUD の Model を判別 union で設置対象・高さ・UI 状態から組む（S2-mode-tooltip）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: CursorTooltip の Model（`Client.Ui.State/CursorTooltip`）

**Files:**
- Create: `S/Client.Ui.State/CursorTooltip/CursorTooltipViewModel.cs`・`CursorTooltipModel.cs`
- Test: `S/Client.Tests/Ui/State/CursorTooltip/CursorTooltipModelTest.cs`

**Interfaces:**
- Consumes: `MouseCursorTooltipState.OnPresentationChanged`（ReactiveProperty。購読時に今の値を流す）、`TooltipLine.Key`・`TextParams`、`UiText(LocalizationKey, IReadOnlyList<LocalizationTextParam>)`
- Produces:
  ```csharp
  namespace Client.Ui.State.CursorTooltip
  {
      public sealed class CursorTooltipViewModel : IEquatable<CursorTooltipViewModel>
      {
          public static readonly CursorTooltipViewModel Hidden;
          public readonly IReadOnlyList<UiText> Lines;            // 空なら非表示
          public readonly IReadOnlyList<TooltipLine> SourceLines; // 等価判定用（UiText は参照で比べるため）
          public bool Visible { get; }                             // 式本体の読み取り専用
          public CursorTooltipViewModel(IReadOnlyList<TooltipLine> sourceLines);
      }
      public sealed class CursorTooltipModel : IInitializable
      {
          public IReadOnlyReactiveProperty<CursorTooltipViewModel> Current { get; }
          public CursorTooltipModel(MouseCursorTooltipState tooltipState);
      }
  }
  ```

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Client.Ui.State.CursorTooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.State.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの状態を行の列にして配り、同じ行の再表示は流さない（L1）
    /// Publishes the cursor tooltip state as a list of lines and does not republish identical lines (L1)
    /// </summary>
    public class CursorTooltipModelTest
    {
        private bool _hadSavedLanguage;
        private string _savedLanguage;

        [SetUp]
        public void PinEnglish()
        {
            _hadSavedLanguage = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguage = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
        }

        [TearDown]
        public void RestoreLanguage()
        {
            if (_hadSavedLanguage) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguage);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [Test]
        public void 表示した行を順に配り隠すと空になる()
        {
            var state = new MouseCursorTooltipState();
            var model = new CursorTooltipModel(state);
            model.Initialize();
            var owner = new TooltipOwner();

            state.Show(owner, new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain), new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceTooFar) });
            Assert.IsTrue(model.Current.Value.Visible);
            Assert.AreEqual(new[] { "Blocked by terrain", "Too far away" }, new[] { model.Current.Value.Lines[0].Resolve(), model.Current.Value.Lines[1].Resolve() });

            state.Hide(owner);
            Assert.IsFalse(model.Current.Value.Visible);
        }

        [Test]
        public void 同じ行の再表示は再配信しない()
        {
            var state = new MouseCursorTooltipState();
            var model = new CursorTooltipModel(state);
            model.Initialize();
            var owner = new TooltipOwner();
            var published = 0;
            model.Current.Subscribe(_ => published++);

            state.Show(owner, LocalizationKeys.Ui.Tooltip.WorldTarget);
            state.Show(owner, LocalizationKeys.Ui.Tooltip.WorldTarget);
            Assert.AreEqual(2, published, "購読時の初期値 1 回と表示 1 回だけ");
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CursorTooltipModel` が無いのでコンパイルが失敗する

- [ ] **Step 3: 実装する**

`CursorTooltipViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Tooltip;
using Client.Ui.State.Text;

namespace Client.Ui.State.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの表示状態。Web の topic ui.tooltip の写しで、行が空なら隠す
    /// Display state of the cursor tooltip; a copy of Web's ui.tooltip topic, hidden when there are no lines
    /// </summary>
    public sealed class CursorTooltipViewModel : IEquatable<CursorTooltipViewModel>
    {
        public static readonly CursorTooltipViewModel Hidden = new(Array.Empty<TooltipLine>());

        public readonly IReadOnlyList<UiText> Lines;
        public readonly IReadOnlyList<TooltipLine> SourceLines;
        public bool Visible => 0 < Lines.Count;

        public CursorTooltipViewModel(IReadOnlyList<TooltipLine> sourceLines)
        {
            SourceLines = sourceLines;
            Lines = sourceLines.Select(line => new UiText(line.Key, line.TextParams)).ToArray();
        }

        // UiText は参照で比べるので、元の行（値の等価を持つ TooltipLine）で比べる
        // UiText compares by reference, so compare the source lines, which have value equality
        public bool Equals(CursorTooltipViewModel other)
        {
            return other != null && SourceLines.SequenceEqual(other.SourceLines);
        }

        public override bool Equals(object obj) => Equals(obj as CursorTooltipViewModel);
        public override int GetHashCode() => SourceLines.Count;
    }
}
```

`CursorTooltipModel.cs`:
```csharp
using Client.Game.InGame.UI.Tooltip;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの表示状態を購読し、画面向けの行の列にして配る
    /// Subscribes to the cursor tooltip presentation and publishes it as lines for the screen
    /// </summary>
    public sealed class CursorTooltipModel : IInitializable
    {
        private readonly MouseCursorTooltipState _tooltipState;
        private readonly ReactiveProperty<CursorTooltipViewModel> _current = new(CursorTooltipViewModel.Hidden);

        public IReadOnlyReactiveProperty<CursorTooltipViewModel> Current => _current;

        public CursorTooltipModel(MouseCursorTooltipState tooltipState)
        {
            _tooltipState = tooltipState;
        }

        public void Initialize()
        {
            // 源は ReactiveProperty なので購読した時点の表示も流れてくる
            // The source is a ReactiveProperty, so subscribing also delivers the current presentation
            _tooltipState.OnPresentationChanged.Subscribe(presentation => _current.Value = new CursorTooltipViewModel(presentation.Lines));
        }
    }
}
```

- [ ] **Step 4: 通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.CursorTooltip\."`
Expected: `ErrorCount: 0`、6 件 PASS（Task 2 の 4 件＋2 件）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/CursorTooltip moorestech_client/Assets/Scripts/Client.Tests/Ui/State/CursorTooltip
git commit -m "feat(uitk): CursorTooltip の Model を MouseCursorTooltipState から組む（S2-mode-tooltip）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: 削除モードの斜線帯（Painter2D）

**Files:**
- Create: `S/Client.Ui/Screens/ModeHud/Parts/DeleteModeWarningStripeGeometry.cs`・`DegreeCustomStyle.cs`・`DeleteModeWarningBand.cs`
- Test: `S/Client.Tests/Ui/Screens/ModeHud/DeleteModeWarningStripeGeometryTest.cs`

**Interfaces:**
- Consumes: `UssCustomStyleReader.TryReadPixels`・`TryReadColor`、`MeshGenerationContext.painter2D`
- Produces:
  ```csharp
  namespace Client.Ui.Screens.ModeHud.Parts
  {
      // CSS repeating-linear-gradient(angle, A 0 s, B s 2s) の A の帯を、幅 width・高さ height の矩形で切った凸多角形の列（要素のローカル座標・y 下向き）
      public static class DeleteModeWarningStripeGeometry
      {
          public static IReadOnlyList<IReadOnlyList<Vector2>> FirstColorPolygons(Vector2 size, float angleDegrees, float stripeSize);
      }
      // customStyle の "117deg" を度の数で読む。読めなければ LogError して false
      public static class DegreeCustomStyle
      {
          public static bool TryReadDegrees(ICustomStyle style, string propertyName, out float degrees);
      }
      // 帯 1 本。背景は USS の黒、黄の斜線を Painter2D で描く。拾わない
      public sealed class DeleteModeWarningBand : VisualElement
      {
          public DeleteModeWarningBand(string testid, string edgeClassName);
      }
  }
  ```

- [ ] **Step 1: 失敗するテストを書く（幾何）**

```csharp
using System.Collections.Generic;
using Client.Ui.Screens.ModeHud.Parts;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 黄の帯の多角形が CSS repeating-linear-gradient の色の決まり方と同じ点を覆う（L1）
    /// The yellow stripe polygons cover the same points as the CSS repeating-linear-gradient colouring (L1)
    /// </summary>
    public class DeleteModeWarningStripeGeometryTest
    {
        private const float Angle = 117f;
        private const float Stripe = 32f;

        [TestCase(1280f, 20f)]
        [TestCase(1706.6666f, 20f)]
        public void 矩形内の各点は多角形の内外がCSSの色と一致する(float width, float height)
        {
            var size = new Vector2(width, height);
            var polygons = DeleteModeWarningStripeGeometry.FirstColorPolygons(size, Angle, Stripe);

            // 帯の境目から 0.5px 以上離れた点だけを見る（境目は描画の縁のにじみで決まらない）
            // Check only points at least 0.5px from a stripe boundary (the boundary itself is antialiased)
            for (var x = 0.5f; x < width; x += 3.7f)
            for (var y = 0.5f; y < height; y += 1.9f)
            {
                var point = new Vector2(x, y);
                var t = CssGradientPosition(point, size, Angle) % (Stripe * 2f);
                if (Mathf.Abs(t - Stripe) < 0.5f || t < 0.5f || Stripe * 2f - t < 0.5f) continue;
                Assert.AreEqual(t < Stripe, IsInsideAny(polygons, point), $"point {point} t={t}");
            }
        }

        [Test]
        public void 多角形はどれも矩形の外へ出ない()
        {
            var size = new Vector2(1280f, 20f);
            foreach (var polygon in DeleteModeWarningStripeGeometry.FirstColorPolygons(size, Angle, Stripe))
            foreach (var vertex in polygon)
            {
                Assert.That(vertex.x, Is.InRange(-0.001f, size.x + 0.001f));
                Assert.That(vertex.y, Is.InRange(-0.001f, size.y + 0.001f));
            }
        }

        // CSS の linear-gradient の位置: 方向 (sin a, -cos a)、線の長さ |w sin a| + |h cos a|、中心を通る
        // CSS linear-gradient position: direction (sin a, -cos a), line length |w sin a| + |h cos a|, through the centre
        private static float CssGradientPosition(Vector2 point, Vector2 size, float angleDegrees)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
            var length = Mathf.Abs(size.x * Mathf.Sin(radians)) + Mathf.Abs(size.y * Mathf.Cos(radians));
            var start = size / 2f - direction * (length / 2f);
            return Vector2.Dot(point - start, direction);
        }

        private static bool IsInsideAny(IReadOnlyList<IReadOnlyList<Vector2>> polygons, Vector2 point)
        {
            foreach (var polygon in polygons)
            {
                var inside = true;
                for (var i = 0; i < polygon.Count && inside; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    var cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                    inside = cross >= 0f;
                }
                if (inside) return true;
            }
            return false;
        }
    }
}
```
（`IsInsideAny` は頂点が y 下向き座標で時計回り〈外積が非負で内側〉の凸多角形を前提にする。実装の巻き方向が逆なら、テストではなく実装の頂点順を時計回りに揃える）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `DeleteModeWarningStripeGeometry` が無いのでコンパイルが失敗する

- [ ] **Step 3: 幾何を実装する**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// CSS repeating-linear-gradient の 1 色目の帯を、要素の矩形で切った凸多角形の列にする
    /// Turns the first-colour stripes of a CSS repeating-linear-gradient into convex polygons clipped to the element rect
    /// </summary>
    public static class DeleteModeWarningStripeGeometry
    {
        public static IReadOnlyList<IReadOnlyList<Vector2>> FirstColorPolygons(Vector2 size, float angleDegrees, float stripeSize)
        {
            // CSS の勾配線: 方向 (sin a, -cos a)（y 下向き）、長さ |w sin a| + |h cos a|、要素の中心を通る
            // CSS gradient line: direction (sin a, -cos a) with y down, length |w sin a| + |h cos a|, through the element centre
            var radians = angleDegrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
            var length = Mathf.Abs(size.x * Mathf.Sin(radians)) + Mathf.Abs(size.y * Mathf.Cos(radians));
            var start = size / 2f - direction * (length / 2f);
            var rect = new List<Vector2> { Vector2.zero, new(size.x, 0f), size, new(0f, size.y) };

            // 周期 2s ごとに [2sk, 2sk+s] の帯を矩形から切り出す
            // Every 2s period, cut the slab [2sk, 2sk+s] out of the rect
            var polygons = new List<IReadOnlyList<Vector2>>();
            for (var from = 0f; from < length; from += stripeSize * 2f)
            {
                var slab = ClipByHalfPlane(rect, start, direction, from);
                slab = ClipByHalfPlane(slab, start, -direction, -(from + stripeSize));
                if (3 <= slab.Count) polygons.Add(slab);
            }
            return polygons;

            #region Internal

            // dot(p - origin, normal) >= offset の側を残す（Sutherland–Hodgman の 1 辺）
            // Keep the side where dot(p - origin, normal) >= offset (one edge of Sutherland–Hodgman)
            List<Vector2> ClipByHalfPlane(List<Vector2> polygon, Vector2 origin, Vector2 normal, float offset)
            {
                var clipped = new List<Vector2>();
                for (var i = 0; i < polygon.Count; i++)
                {
                    var current = polygon[i];
                    var next = polygon[(i + 1) % polygon.Count];
                    var currentDistance = Vector2.Dot(current - origin, normal) - offset;
                    var nextDistance = Vector2.Dot(next - origin, normal) - offset;
                    if (0f <= currentDistance) clipped.Add(current);
                    if ((0f <= currentDistance) != (0f <= nextDistance)) clipped.Add(Vector2.Lerp(current, next, currentDistance / (currentDistance - nextDistance)));
                }
                return clipped;
            }

            #endregion
        }
    }
}
```
（矩形の頂点順 (0,0)→(w,0)→(w,h)→(0,h) は y 下向きで時計回り。Sutherland–Hodgman は巻き方向を保つ）

- [ ] **Step 4: 幾何のテストが通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.ModeHud\.DeleteModeWarningStripeGeometryTest\."`
Expected: `ErrorCount: 0`、3 件 PASS

- [ ] **Step 5: 角度の読み手と帯を実装する**

`DegreeCustomStyle.cs`:
```csharp
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// customStyle の角度（"117deg"）を度の数で読む。UssCustomStyleReader に角度の口が無いので画面に置く（2 機能目が出たら Foundation へ昇格を依頼する）
    /// Reads a customStyle angle ("117deg") as degrees; kept in the screen since UssCustomStyleReader has no angle reader (request promotion to Foundation on a second use)
    /// </summary>
    public static class DegreeCustomStyle
    {
        private const string DegreeSuffix = "deg";

        public static bool TryReadDegrees(ICustomStyle style, string propertyName, out float degrees)
        {
            // 角度の値は数として解けないので文字列で受け、末尾の deg を外して読む
            // An angle value does not resolve as a number, so take it as a string and strip the trailing deg
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out var text)
                && text.EndsWith(DegreeSuffix)
                && float.TryParse(text.Substring(0, text.Length - DegreeSuffix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out degrees))
            {
                return true;
            }

            Debug.LogError($"[DegreeCustomStyle] {propertyName} is not a resolved angle in deg: '{text}'");
            degrees = 0f;
            return false;
        }
    }
}
```
（`CustomStyleProperty<string>` が `117deg` を文字列で返すかは未確認。Step 7 の L1〈`DeleteModeWarningBandsTest` の角度の主張〉で確かめ、文字列で返らなければ `CustomStyleProperty<float>` で 117 が返るかを試して通る方を残し、台帳「## 実測の結論」の末尾に 1 行書く〈Task 8 Step 5〉。どちらでも読めなければ止め、`done.md` の「integrator への依頼」に「`UssCustomStyleReader.TryReadDegrees` の新設」を書く）

`DeleteModeWarningBand.cs`:
```csharp
using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 削除モードの警告帯 1 本。背景は USS の黒、黄の斜線は Web の repeating-linear-gradient を Painter2D で描く
    /// One delete-mode warning band; the black is the USS background and the yellow stripes draw Web's repeating-linear-gradient with Painter2D
    /// </summary>
    public sealed class DeleteModeWarningBand : VisualElement
    {
        // :root の変数は customStyle に継承されないので、帯のクラスで受け直した名前を読む
        // :root variables do not inherit into customStyle, so read the names re-declared on the band class
        private const string YellowProperty = "--mode-hud-band-yellow";
        private const string StripeSizeProperty = "--mode-hud-band-stripe-size";
        private const string StripeAngleProperty = "--mode-hud-band-stripe-angle";

        private Color _yellow;
        private float _stripeSize;
        private float _angleDegrees;
        private bool _resolved;

        public DeleteModeWarningBand(string testid, string edgeClassName)
        {
            name = testid;
            pickingMode = PickingMode.Ignore;
            AddToClassList("mode-hud__warning-band");
            AddToClassList(edgeClassName);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            generateVisualContent += DrawStripes;
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // 3 つ揃ったときだけ描く。欠けは各読み手が LogError を出す
            // Draw only when all three resolve; each reader logs an error for a missing one
            var hasYellow = UssCustomStyleReader.TryReadColor(evt.customStyle, YellowProperty, out _yellow);
            var hasSize = UssCustomStyleReader.TryReadPixels(evt.customStyle, StripeSizeProperty, out _stripeSize);
            var hasAngle = DegreeCustomStyle.TryReadDegrees(evt.customStyle, StripeAngleProperty, out _angleDegrees);
            // 刻み幅 0 以下は帯を作れない（周期のループが進まない）。黙らず理由を出して描かない
            // A stripe size of 0 or less cannot make stripes (the period loop never advances); log why and skip drawing
            if (hasSize && _stripeSize <= 0f) Debug.LogError($"[DeleteModeWarningBand] {StripeSizeProperty} must be positive but was {_stripeSize}");
            _resolved = hasYellow && hasSize && hasAngle && 0f < _stripeSize;
            MarkDirtyRepaint();
        }

        private void DrawStripes(MeshGenerationContext context)
        {
            // silent-ok: 解決前と寸法 0 は描かない / 読めないつまみは OnCustomStyleResolved の読み手が LogError で知らせる
            if (!_resolved || contentRect.width <= 0f || contentRect.height <= 0f) return;

            // 黄の帯を 1 枚ずつ塗る
            // Fill each yellow stripe
            var painter = context.painter2D;
            painter.fillColor = _yellow;
            foreach (var polygon in DeleteModeWarningStripeGeometry.FirstColorPolygons(contentRect.size, _angleDegrees, _stripeSize))
            {
                painter.BeginPath();
                painter.MoveTo(polygon[0]);
                for (var i = 1; i < polygon.Count; i++) painter.LineTo(polygon[i]);
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}
```
（帯に枠と余白は無いので `contentRect` は要素の矩形と同じ。刻み幅 0 以下は LogError を出して描かない〈幾何のループが進まないため〉。`DegreeCustomStyle` の失敗は LogError の文〈値の無い／deg で終わらない／数でない〉で区別できるので bool のままにする）

- [ ] **Step 6: コンパイルを確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount: 0`（帯の描画の L1 は Task 6 の `DeleteModeWarningBandsTest`）

- [ ] **Step 7: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/ModeHud moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/ModeHud
git commit -m "feat(uitk): 削除モードの斜線帯を Painter2D で描く（S2-mode-tooltip）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: 操作モード HUD の View・USS・Presenter

**Files:**
- Create: `S/Client.Ui/Screens/ModeHud/ModeHudView.cs`・`ModeHud.uss`・`ModeHudPresenter.cs`
- Create: `S/Client.Ui/Screens/ModeHud/Parts/PlacementModeHudPanel.cs`・`DeleteBoxHeightHud.cs`・`DeleteModeWarningBands.cs`・`ModeHudPickingIgnore.cs`・`PlacementHeightText.cs`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に 1 行）・在れば `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs`（自分の 2 行を消す。D-30）
- Test: `S/Client.Tests/Ui/Screens/ModeHud/ModeHudViewModelSamples.cs`・`ModeHudViewTest.cs`・`ModeHudWideViewportTest.cs`・`DeleteModeWarningBandsTest.cs`・`ModeHudDesignTest.cs`・`ContrastRatio.cs`

**Interfaces:**
- Consumes: Task 3 の `ModeHudViewModel`（`ModeHudHidden`・`ModeHudPlacement`・`ModeHudDelete`）・`ModeHudText`、Task 5 の `DeleteModeWarningBand`、`GamePanel(string, GamePanelFrame)`・`HudSectionHeading(string, LocalizationKey)`・`LocalizedLabel.SetText(UiText)`・`TutorialAnchor.Mark`・`AnchorIds.PlacementHud`/`DeleteHud`・`UiTypographyClass.KeyHintText`・`GameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport)`
- Produces:
  ```csharp
  namespace Client.Ui.Screens.ModeHud
  {
      public sealed class ModeHudView : VisualElement { public ModeHudView(); public void Render(ModeHudViewModel viewModel); }   // .mode-hud。Model は購読しない（設計書 §4.2・[A-8]）
      public sealed class ModeHudPresenter : IInitializable { public ModeHudPresenter(GameUiRoot gameUiRoot, ModeHudModel model); } // Initialize で _model.Current.Subscribe(_view.Render)
  }
  namespace Client.Ui.Screens.ModeHud.Parts
  {
      public sealed class PlacementModeHudPanel : VisualElement { public PlacementModeHudPanel(); public void Show(ModeHudPlacement placement); }
      public static class PlacementHeightText { public static UiText Create(int height); } // 「Height: {height}」の導出の唯一の場所
      public sealed class DeleteBoxHeightHud : VisualElement { public DeleteBoxHeightHud(); public void SetHeight(int height); }
      public sealed class DeleteModeWarningBands : VisualElement { public DeleteModeWarningBands(); }
      public static class ModeHudPickingIgnore { public static void Apply(VisualElement root); }
  }
  ```
  testid（Web の `data-testid` と同名）: `placement-mode-hud`（右上の箱・錨 `placement.hud`）・`placement-mode-hud-panel`（GamePanel）・`operation-mode-label`・`operation-mode-detail`（2 つ）・`delete-mode-warning`・`delete-mode-warning-band`（2 つ。下に錨 `delete.hud`）・`delete-box-height-hud`

- [ ] **Step 1: 偽モデルと失敗するテストを書く**

`ModeHudViewModelSamples.cs`:
```csharp
using Client.Ui.State.ModeHud;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// Web の mock-host の topic シナリオ（placement）と同じ値の表示状態。テストは View.Render へ直に渡す
    /// Display states with the same values as the Web mock-host topic scenario (placement); tests pass them straight to View.Render
    /// </summary>
    public static class ModeHudViewModelSamples
    {
        public static ModeHudViewModel Placement() => new ModeHudPlacement(ModeHudText.Of("Assembler"), 3);
        public static ModeHudViewModel Delete(int height) => new ModeHudDelete(height);
    }
}
```

`ContrastRatio.cs`（WCAG 2.x の相対輝度とコントラスト比。Web の `operationHudAssertions.ts` の 4.5 以上の主張を写すため）:
```csharp
using UnityEngine;

namespace Client.Tests.Ui.Screens.ModeHud
{
    // WCAG のコントラスト比（sRGB）。半透明の面は背景に重ねてから測る
    // WCAG contrast ratio (sRGB); translucent faces are composited over the backdrop first
    public static class ContrastRatio
    {
        public static float Of(Color foreground, Color background)
        {
            var a = Luminance(foreground) + 0.05f;
            var b = Luminance(background) + 0.05f;
            return Mathf.Max(a, b) / Mathf.Min(a, b);
        }

        public static Color Over(Color face, Color backdrop)
        {
            return new Color(Mathf.Lerp(backdrop.r, face.r, face.a), Mathf.Lerp(backdrop.g, face.g, face.a), Mathf.Lerp(backdrop.b, face.b, face.a), 1f);
        }

        private static float Luminance(Color color)
        {
            return 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);
        }

        private static float Linear(float channel)
        {
            return channel <= 0.03928f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}
```

`ModeHudViewTest.cs`（`ScreenTestHost`。1280×720・english）:
```csharp
using Client.Tests.Ui.Components;
using Client.Ui.Components.Panels;
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 配置モード HUD が右上のクラフト枠に題・対象・高さを Web と同じ寸法と色で出し、入力を奪わず、非表示で起動してもエラーを出さない（L1。Web の operation-mode-hud.spec.ts・commonHud.spec.ts）
    /// The placement HUD shows title, target and height in a top-right craft frame with Web's sizes and colours, never takes input and starts hidden without errors (L1; Web operation-mode-hud.spec.ts, commonHud.spec.ts)
    /// </summary>
    public class ModeHudViewTest : ScreenTestHost
    {
        // .mode-hud__detail が --localized-label-line-height（25px）を宣言するので LocalizedLabel は文の頭に行送りのタグを付ける（前例 TooltipFaceTest.LineHeightTag）
        // .mode-hud__detail declares --localized-label-line-height (25px), so LocalizedLabel prefixes the pitch tag (precedent TooltipFaceTest.LineHeightTag)
        public const string DetailLineHeightTag = "<line-height=25px>";

        private ModeHudView _view;

        [SetUp]
        public void MountModeHud()
        {
            _view = Mount(new ModeHudView());
        }

        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">配置モードHUDを右上のクラフト枠で表示する</web-test>
        [Test]
        public void 配置モードHUDは右上のクラフト枠に題と対象と高さを出す()
        {
            _view.Render(ModeHudViewModelSamples.Placement());
            simulate.FrameUpdate();

            // 実画面の右上 24px・幅 288px・中央を覆わない
            // Top-right at 24px, 288px wide, not covering the centre
            var corner = Find<VisualElement>(_view, "placement-mode-hud");
            Assert.AreEqual(24f, corner.worldBound.yMin, 1f);
            Assert.AreEqual(1280f - 24f, corner.worldBound.xMax, 1f);
            Assert.AreEqual(288f, corner.worldBound.width, 1f);
            Assert.IsFalse(corner.worldBound.Contains(new Vector2(640f, 360f)));

            // 枠はクラフト（グリップあり）・面はクラフトの面のトークン
            // The frame is craft (with grip) and the face uses the craft face token
            var panel = Find<GamePanel>(_view, "placement-mode-hud-panel");
            Assert.IsTrue(panel.ClassListContains("game-panel--craft"));
            Assert.IsNotNull(panel.Q(className: "game-panel__craft-grip"));
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--game-panel-craft-face"), panel.resolvedStyle.backgroundColor, "craft face");

            // 題: 12px・text-muted・字間は 1px の換算値
            // Title: 12px, text-muted, letter spacing converted from 1px
            var label = Find<Label>(_view, "operation-mode-label");
            Assert.AreEqual("Placement Mode", label.text);
            Assert.AreEqual(12f, label.resolvedStyle.fontSize, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-muted"), label.resolvedStyle.color, "label");
            Assert.AreEqual(8.333f, label.resolvedStyle.letterSpacing.value, 0.01f);

            // 詳細 2 行: 17px・白・行の高さ 25px・項目間 5px
            // Two detail lines: 17px, white, 25px line height, 5px apart
            var details = _view.Query<Label>("operation-mode-detail").ToList();
            Assert.AreEqual(new[] { DetailLineHeightTag + "Selected: Assembler", DetailLineHeightTag + "Height: 3" }, new[] { details[0].text, details[1].text });
            Assert.AreEqual(17f, details[0].resolvedStyle.fontSize, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-high-contrast"), details[0].resolvedStyle.color, "detail");
            Assert.AreEqual(25f, details[0].worldBound.height, 0.5f);
            Assert.AreEqual(5f, details[1].worldBound.yMin - details[0].worldBound.yMax, 0.5f);

            // 明暗どちらの世界の上でも詳細が読める（WCAG 4.5 以上）
            // Details stay readable over both light and dark worlds (WCAG 4.5 or more)
            foreach (var backdrop in new[] { Color.white, Color.black })
            {
                var face = ContrastRatio.Over(panel.resolvedStyle.backgroundColor, backdrop);
                Assert.GreaterOrEqual(ContrastRatio.Of(details[0].resolvedStyle.color, face), 4.5f);
            }
        }

        /// <web-test file="e2e/tests/system/commonHud.spec.ts">設置情報と削除警告帯を操作モードへ反映する</web-test>
        [Test]
        public void 設置では錨placement_hudに対象と高さが出て削除では帯2本と錨delete_hudが下端に出る()
        {
            _view.Render(ModeHudViewModelSamples.Placement());
            var placement = _view.Q(className: AnchorIds.PlacementHud);
            Assert.IsNotNull(placement, "錨 placement.hud");
            StringAssert.Contains("Assembler", AllText(placement));
            StringAssert.Contains("3", AllText(placement));

            _view.Render(ModeHudViewModelSamples.Delete(3));
            simulate.FrameUpdate();
            Assert.IsNull(_view.Q("placement-mode-hud"), "配置 HUD は木から外れる");
            var bands = _view.Query<VisualElement>("delete-mode-warning-band").ToList();
            Assert.AreEqual(2, bands.Count);
            Assert.AreEqual(720f, bands[1].worldBound.yMax, 0.5f);
            Assert.IsTrue(bands[1].ClassListContains(AnchorIds.DeleteHud));
        }

        [Test]
        public void 起動直後の非表示と表示の切り替えでエラーを出さない()
        {
            // Presenter と同じく載せた直後に Hidden を描き、数フレーム回しても LocalizedTextBinding の「文の無いラベル」が出ない
            // Like the presenter, render Hidden right after mounting; several frames later no textless-label error appears
            _view.Render(Client.Ui.State.ModeHud.ModeHudViewModel.Hidden);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            _view.Render(ModeHudViewModelSamples.Placement());
            _view.Render(ModeHudViewModelSamples.Delete(1));
            _view.Render(Client.Ui.State.ModeHud.ModeHudViewModel.Hidden);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void どの要素も入力を拾わない()
        {
            _view.Render(ModeHudViewModelSamples.Placement());
            simulate.FrameUpdate();
            _view.Query<VisualElement>().ForEach(element => Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name));
        }

        private static string AllText(VisualElement root)
        {
            return string.Join(" ", root.Query<TextElement>().ToList().ConvertAll(element => element.text));
        }
    }
}
```
（文の完全一致は `LocalizedLabel` が付ける `<line-height=…>` 込みで書く。つまみ `--localized-label-line-height` を外してテストを通すのは禁止〈行送り 25px の写しが消える〉。`TutorialAnchor.Mark` は `tutorial-anchor` と錨の ID〈`placement.hud`〉をクラス名として足すだけなので、`Q(className: AnchorIds.PlacementHud)` で引ける。`using Client.Game.InGame.Tutorial.UIHighlight;` を足す）

`ModeHudWideViewportTest.cs`（前例 `PauseMenuSettingsLayoutTest` の `[TestFixture(w, h)]`）:
```csharp
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 2 つの論理解像度で右上の箱が実画面の右上 24px に固定される。2432x786 は Expand（参照 1280x720）で 2227.7x720
    /// At two logical sizes the top-right box stays 24px from the real top-right; 2432x786 becomes 2227.7x720 under Expand (reference 1280x720)
    /// </summary>
    [TestFixture(1280f, 720f)]
    [TestFixture(2227.7f, 720f)]
    public class ModeHudWideViewportTest : ScreenTestHost
    {
        private readonly Vector2 _logicalSize;
        private ModeHudView _view;

        public ModeHudWideViewportTest(float width, float height)
        {
            _logicalSize = new Vector2(width, height);
        }

        [OneTimeSetUp]
        public void UseLogicalSize()
        {
            panelSize = _logicalSize;
        }

        [SetUp]
        public void MountModeHud()
        {
            _view = Mount(new ModeHudView());
        }

        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">横長画面でも配置モードHUDを実画面右上へ固定する</web-test>
        [Test]
        public void 配置モードHUDは実画面の右上に固定される()
        {
            _view.Render(ModeHudViewModelSamples.Placement());
            simulate.FrameUpdate();
            var corner = Find<VisualElement>(_view, "placement-mode-hud");
            Assert.AreEqual(_logicalSize.x - 24f, corner.worldBound.xMax, 1f);
            Assert.AreEqual(24f, corner.worldBound.yMin, 1f);
            Assert.AreEqual(288f, corner.worldBound.width, 1f);
        }

        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">削除箱の高さだけを右上に表示して更新する ${viewport.width}</web-test>
        [Test]
        public void 削除箱は高さだけを右上に出し更新しゲーム画面で消える()
        {
            _view.Render(ModeHudViewModelSamples.Delete(3));
            simulate.FrameUpdate();
            var box = Find<VisualElement>(_view, "delete-box-height-hud");
            Assert.AreEqual(ModeHudViewTest.DetailLineHeightTag + "Height: 3", box.Q<Label>().text);
            Assert.AreEqual(_logicalSize.x - 24f, box.worldBound.xMax, 1f);
            Assert.AreEqual(24f, box.worldBound.yMin, 1f);
            Assert.AreEqual(288f, box.worldBound.width, 1f);
            Assert.IsNull(_view.Q("placement-mode-hud"), "配置 HUD は木から外れる");

            // 対象が空でも高さだけが更新される
            // Only the height updates even with no target
            _view.Render(ModeHudViewModelSamples.Delete(0));
            Assert.AreEqual(ModeHudViewTest.DetailLineHeightTag + "Height: 0", box.Q<Label>().text);

            _view.Render(Client.Ui.State.ModeHud.ModeHudViewModel.Hidden);
            Assert.IsNull(box.parent, "非表示では削除箱を木から外す");
        }
    }
}
```

`DeleteModeWarningBandsTest.cs`:
```csharp
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 削除モードは説明パネルではなく上下の高さ 20px の警告帯 2 本で、下の帯に錨 delete.hud（L1。Web の operation-mode-hud.spec.ts）
    /// Delete mode is two 20px warning bands at the top and bottom instead of a panel, the bottom one anchored as delete.hud (L1; Web operation-mode-hud.spec.ts)
    /// </summary>
    public class DeleteModeWarningBandsTest : ScreenTestHost
    {
        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">削除モードをuGUI準拠の上下警告帯で表示する</web-test>
        [Test]
        public void 削除モードは上端と下端の警告帯2本で描き拾わない()
        {
            var view = Mount(new ModeHudView());
            view.Render(ModeHudViewModelSamples.Delete(3));
            simulate.FrameUpdate();

            // 配置 HUD は出ず、帯は 2 本で上端 0 と下端 0・高さ 20・全幅
            // No placement HUD; two bands at top 0 and bottom 0, 20px tall, full width
            Assert.IsNull(view.Q("placement-mode-hud"), "配置 HUD は木から外れる");
            var root = Find<VisualElement>(view, "delete-mode-warning");
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            var bands = root.Query<VisualElement>("delete-mode-warning-band").ToList();
            Assert.AreEqual(2, bands.Count);
            Assert.AreEqual(0f, bands[0].worldBound.yMin, 0.5f);
            Assert.AreEqual(720f, bands[1].worldBound.yMax, 0.5f);
            Assert.AreEqual(20f, bands[0].worldBound.height, 0.5f);
            Assert.AreEqual(1280f, bands[0].worldBound.width, 0.5f);

            // 黒は USS の背景。黄と角度と刻みは帯のクラスで受け直したトークン
            // Black is the USS background; yellow, angle and stripe size are tokens re-declared on the band class
            Assert.AreEqual(new Color(0f, 0f, 0f, 1f), bands[0].resolvedStyle.backgroundColor);
            Assert.IsTrue(bands[0].customStyle.TryGetValue(new CustomStyleProperty<Color>("--mode-hud-band-yellow"), out var yellow));
            Assert.AreEqual(new Color(1f, 187f / 255f, 36f / 255f, 1f), yellow);
            Assert.IsTrue(Client.Ui.Screens.ModeHud.Parts.DegreeCustomStyle.TryReadDegrees(bands[0].customStyle, "--mode-hud-band-stripe-angle", out var angle));
            Assert.AreEqual(117f, angle, 0.001f);

            // 錨 delete.hud は下の帯だけに付く
            // The delete.hud anchor sits only on the bottom band
            Assert.IsFalse(bands[0].ClassListContains(AnchorIds.DeleteHud));
            Assert.IsTrue(bands[1].ClassListContains(AnchorIds.DeleteHud));
        }
    }
}
```
（`using Client.Game.InGame.Tutorial.UIHighlight;` を足す）

`ModeHudDesignTest.cs`（Web `modeHudDesign.test.ts` は部品の構成とトークンを文字列で見る。UITK は L1 の構成と USS・トークン USS の本文で同じ意図を主張する）:
```csharp
using System.IO;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 操作モード HUD は共通のクラフト枠と帯で組み、色と寸法をトークン経由に限る（L1。Web の modeHudDesign.test.ts）
    /// The operation-mode HUD is built from the shared craft frame and bands, with colours and sizes only through tokens (L1; Web modeHudDesign.test.ts)
    /// </summary>
    public class ModeHudDesignTest : ScreenTestHost
    {
        private const string ModeHudUssPath = "Scripts/Client.Ui/Screens/ModeHud/ModeHud.uss";
        private const string TokensUssPath = "Scripts/Client.Ui/Foundation/Theme/Tokens.uss";

        /// <web-test file="src/features/modeHud/modeHudDesign.test.ts">配置HUDをMantineではなく共通クラフト枠で構成する</web-test>
        [Test]
        public void 配置HUDは共通のクラフト枠とHUD見出しで組む()
        {
            var view = Mount(new ModeHudView());
            view.Render(ModeHudViewModelSamples.Placement());
            var panel = Find<GamePanel>(view, "placement-mode-hud-panel");
            Assert.IsTrue(panel.ClassListContains("game-panel--craft"));
            Assert.IsNotNull(panel.Q<HudSectionHeading>());
            Assert.IsNotNull(panel.Q(className: "hud-section-heading__rule"));
        }

        /// <web-test file="src/features/modeHud/modeHudDesign.test.ts">削除表示を説明パネルではなく上下の警告帯で構成する</web-test>
        [Test]
        public void 削除表示はパネルを持たず帯2本で組む()
        {
            var view = Mount(new ModeHudView());
            view.Render(ModeHudViewModelSamples.Delete(1));
            var warning = Find<VisualElement>(view, "delete-mode-warning");
            Assert.IsNull(warning.Q<GamePanel>());
            Assert.AreEqual(2, warning.Query<VisualElement>("delete-mode-warning-band").ToList().Count);
        }

        /// <web-test file="src/features/modeHud/modeHudDesign.test.ts">機能CSSの色と寸法をトークン経由に限定する</web-test>
        [Test]
        public void 画面のUSSは色をトークン経由だけで書き帯の色と文字色のトークンを使う()
        {
            var uss = Read(ModeHudUssPath);
            foreach (var token in new[] { "var(--text-high-contrast)", "var(--delete-mode-warning-yellow)", "var(--delete-mode-warning-black)", "var(--operation-hud-top)", "var(--operation-hud-right)", "var(--operation-hud-width)", "var(--delete-mode-warning-band-height)" })
            {
                StringAssert.Contains(token, uss);
            }
            // 色リテラルの禁止は L2 UiStyleConventionTest が全 USS で見る。ここは画面の USS に色の直書きが無いことだけ重ねて見る
            // L2 UiStyleConventionTest bans colour literals in all USS; here only re-check this screen's USS
            StringAssert.DoesNotMatch(@":\s*(white|red|#[0-9a-fA-F]{3,8}|rgba?\()", uss);
        }

        /// <web-test file="src/features/modeHud/modeHudDesign.test.ts">操作モードHUDの視覚寸法を固定長トークンへ集約する</web-test>
        [Test]
        public void 寸法は固定長のトークンにある()
        {
            var tokens = Read(TokensUssPath);
            foreach (var name in new[] { "--operation-hud-right:", "--operation-hud-top:", "--operation-hud-width:", "--operation-hud-label-font-size:", "--operation-hud-detail-font-size:", "--delete-mode-warning-band-height:", "--delete-mode-warning-stripe-size:", "--delete-mode-warning-stripe-angle:" })
            {
                StringAssert.Contains(name, tokens);
            }
            StringAssert.DoesNotContain("--operation-hud-text-shadow:", tokens);
        }

        private static string Read(string pathUnderAssets)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, pathUnderAssets));
        }
    }
}
```
（Web の 5 件目「警告色のコントラスト改善を既存consumerへ共有する」は写さない。この画面は警告の行を持たず〈D-7〉、他の consumer は別画面〈S2-notification・S4〉。処遇は Task 9。D-12）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ModeHudView` が無いのでコンパイルが失敗する

- [ ] **Step 3: Parts を実装する**

`ModeHudPickingIgnore.cs`:
```csharp
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 部分木の要素を全部拾わなくする（Web の pointer-events: none は子へ継承されるが UITK の pickingMode は継承されないため）。2 機能目が出たら Foundation へ昇格を依頼する
    /// Makes every element in a subtree unpickable (Web pointer-events: none inherits, UITK pickingMode does not); request promotion to Foundation on a second use
    /// </summary>
    public static class ModeHudPickingIgnore
    {
        public static void Apply(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }
    }
}
```
（部品〈GamePanel のルート・`_header`・`_body`〉は既定で拾うので、組み終えた後に 1 回掃く。要素を後から足す経路は無い〈文言の差し替えは要素を増やさない〉）

`PlacementModeHudPanel.cs`:
```csharp
using System.Collections.Generic;
using Client.Localization;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Text;
using Client.Ui.State.ModeHud;
using Client.Ui.State.Text;
using Client.Game.InGame.Tutorial.UIHighlight;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 右上の配置モード HUD。Web の PlacementModeHud（GamePanel craft・見出し・詳細 2 行。警告の行は写さない D-7）
    /// The top-right placement HUD; Web PlacementModeHud (craft GamePanel, heading, two details; the warning line is not ported, D-7)
    /// </summary>
    public sealed class PlacementModeHudPanel : VisualElement
    {
        private readonly LocalizedLabel _selected;
        private readonly LocalizedLabel _height;

        public PlacementModeHudPanel()
        {
            name = "placement-mode-hud";
            AddToClassList("mode-hud__corner");
            TutorialAnchor.Mark(this, AnchorIds.PlacementHud);

            // 枠 → 見出し（題・罫線）→ 詳細の列
            // Frame → heading (title, rule) → detail list
            var panel = new GamePanel("placement-mode-hud-panel", GamePanelFrame.Craft);
            var heading = new HudSectionHeading("operation-mode-label", LocalizationKeys.Ui.ModeHud.PlacementModeTitle);
            panel.Add(heading);
            Add(panel);

            _selected = Detail("operation-mode-detail", false);
            _height = Detail("operation-mode-detail", true);
            heading.Add(_selected);
            heading.Add(_height);
        }

        public void Show(ModeHudPlacement placement)
        {
            // silent-ok: 対象が未選択の瞬間は Web と同じく名前を空で「Selected: 」と出す / 次の OnTargetChanged で名前が入り HUD を見る人が気づく
            var targetName = placement.Target.HasValue ? placement.Target.Value : "";
            _selected.SetText(new UiText(LocalizationKeys.Ui.ModeHud.SelectedBlock, new[] { new LocalizationTextParam("name", targetName) }));
            _height.SetText(PlacementHeightText.Create(placement.Height));
        }

        private static LocalizedLabel Detail(string testid, bool following)
        {
            var label = new LocalizedLabel { name = testid };
            label.AddToClassList("mode-hud__detail");
            if (following) label.AddToClassList("mode-hud__detail--following");
            return label;
        }
    }
}
```
（`AnchorIds` は `Client.Game` の文字列定数で、`UiLayerDependencyRules` は Screens からの `Client.Game` 参照を止めていない〈止めているのは `Components/`〉。錨の ID を直書きしない）

`DeleteBoxHeightHud.cs`:
```csharp
using Client.Localization;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 削除モードの右上の高さ 1 行。Web の DeleteBoxHeightHud（.detail と keyHintText）
    /// The delete mode's top-right height line; Web DeleteBoxHeightHud (.detail with keyHintText)
    /// </summary>
    public sealed class DeleteBoxHeightHud : VisualElement
    {
        private readonly LocalizedLabel _height;

        public DeleteBoxHeightHud()
        {
            name = "delete-box-height-hud";
            AddToClassList("mode-hud__corner");
            _height = new LocalizedLabel();
            _height.AddToClassList(UiTypographyClass.KeyHintText);
            _height.AddToClassList("mode-hud__detail");
            Add(_height);
        }

        public void SetHeight(int height)
        {
            _height.SetText(PlacementHeightText.Create(height));
        }
    }
}
```
（`UiTypographyClass` は `Client.Ui.Foundation.Text`〈C2-c の実物〉）

`PlacementHeightText.cs`:
```csharp
using System.Globalization;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.ModeHud.Parts
{
    // 「Height: {height}」の文を作る唯一の場所（配置 HUD と削除箱の 2 か所が使う）。Web の {height} と同じく符号付きの整数
    // The single place that builds "Height: {height}" (used by the placement HUD and the delete box); a signed integer like Web's {height}
    public static class PlacementHeightText
    {
        public static UiText Create(int height)
        {
            return new UiText(LocalizationKeys.Ui.ModeHud.PlacementHeight, new[] { new LocalizationTextParam("height", height.ToString(CultureInfo.InvariantCulture)) });
        }
    }
}
```

`DeleteModeWarningBands.cs`:
```csharp
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Foundation.Anchor;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 削除モードの全面の入れ物と上下の帯 2 本。Web の DeleteModeWarningBands（下の帯に錨 delete.hud）
    /// Full-screen container with the top and bottom bands; Web DeleteModeWarningBands (bottom band anchored as delete.hud)
    /// </summary>
    public sealed class DeleteModeWarningBands : VisualElement
    {
        public DeleteModeWarningBands()
        {
            name = "delete-mode-warning";
            AddToClassList("mode-hud__warning");
            Add(new DeleteModeWarningBand("delete-mode-warning-band", "mode-hud__warning-band--top"));
            var bottom = new DeleteModeWarningBand("delete-mode-warning-band", "mode-hud__warning-band--bottom");
            TutorialAnchor.Mark(bottom, AnchorIds.DeleteHud);
            Add(bottom);
        }
    }
}
```

- [ ] **Step 4: View・USS・Presenter を実装する**

`ModeHudView.cs`:
```csharp
using Client.Ui.Screens.ModeHud.Parts;
using Client.Ui.State.ModeHud;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud
{
    /// <summary>
    /// 操作モード HUD の画面ルート。渡された表示状態の派生の型で配置 HUD・削除の帯と高さを出し分ける。Model は購読しない（Presenter が Render を呼ぶ）
    /// Screen root of the operation-mode HUD; shows the placement HUD or the delete bands and height by the state's derived type; never subscribes (the presenter calls Render)
    /// </summary>
    public sealed class ModeHudView : VisualElement
    {
        private readonly PlacementModeHudPanel _placement = new();
        private readonly DeleteModeWarningBands _deleteBands = new();
        private readonly DeleteBoxHeightHud _deleteHeight = new();

        public ModeHudView()
        {
            AddToClassList("mode-hud");
            // 組は出すときだけ木に載せるので、拾わない指定は組ごとに組み終えた時点で掛ける
            // Groups are attached only while shown, so apply picking-ignore to each group once it is built
            ModeHudPickingIgnore.Apply(this);
            ModeHudPickingIgnore.Apply(_placement);
            ModeHudPickingIgnore.Apply(_deleteBands);
            ModeHudPickingIgnore.Apply(_deleteHeight);
        }

        public void Render(ModeHudViewModel viewModel)
        {
            // 文を渡してから載せる。出さない組は display ではなく木から外す（文の無い LocalizedLabel が載ると 1 フレーム後に LocalizedTextBinding が LogError。前例 GamePanel.SetTitle）
            // Give text before attaching; unmount hidden groups instead of hiding them (an attached textless LocalizedLabel logs an error a frame later; precedent GamePanel.SetTitle)
            switch (viewModel)
            {
                case ModeHudPlacement placement:
                    _placement.Show(placement);
                    MountOnly(_placement);
                    break;
                case ModeHudDelete delete:
                    _deleteHeight.SetHeight(delete.Height);
                    MountOnly(_deleteBands, _deleteHeight);
                    break;
                default:
                    MountOnly();
                    break;
            }

            // Web の帯は同じ viewportOverlay の中で後から描かれるので、出すときは層の中で前へ
            // Web draws the bands later in the same viewportOverlay, so bring the HUD to the front of its layer when shown
            if (viewModel is not ModeHudHidden) BringToFront();
        }

        private void MountOnly(params VisualElement[] shown)
        {
            // 組の並び（帯 → 配置 → 削除箱）は固定。載せる順もこの順
            // Group order (bands, placement, delete box) is fixed and groups are attached in this order
            foreach (var group in new VisualElement[] { _deleteBands, _placement, _deleteHeight })
            {
                var wanted = System.Array.IndexOf(shown, group) >= 0;
                if (wanted && group.parent != this) Add(group);
                if (!wanted && group.parent == this) Remove(group);
            }
        }
    }
}
```

`ModeHud.uss`:
```css
/* web: moorestech_web/webui/src/features/modeHud/style.module.css（.placementHud・.label・.details・.detail・.deleteModeWarning・.band。.warning は写さない D-7）と app/tokens.css の keyHintText */
/* 部品（GamePanel・HudSectionHeading）の見た目はつまみで変え、部品のクラスは書かない */
/* web: moorestech_web/webui/src/features/modeHud/style.module.css (.placementHud, .label, .details, .detail, .deleteModeWarning, .band; .warning is not ported, D-7) and keyHintText in app/tokens.css */
/* Component looks (GamePanel, HudSectionHeading) change only through knobs; component classes are not written here */

/* 画面ルート: 実画面いっぱい（Viewport 族）・HudSectionHeading のつまみを --operation-hud-* で受ける */
/* Screen root: full viewport (Viewport family); HudSectionHeading knobs take --operation-hud-* */
.mode-hud {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
    --hud-label-font-size: var(--operation-hud-label-font-size);
    --hud-label-rule-gap: var(--operation-hud-label-rule-gap);
    --hud-rule-list-gap: var(--operation-hud-rule-list-gap);
    /* 字間: I-fix1 で UITK の単位へ換算済みのトークンをそのまま受ける */
    /* Letter spacing: take the token already converted to UITK units by I-fix1 */
    --hud-label-letter-spacing: var(--operation-hud-letter-spacing);
}

.mode-hud__corner {
    position: absolute;
    top: var(--operation-hud-top);
    right: var(--operation-hud-right);
    width: var(--operation-hud-width);
}

.mode-hud__detail {
    margin: 0;
    padding: 0;
    color: var(--text-high-contrast);
    font-size: var(--operation-hud-detail-font-size);
    --localized-label-line-height: var(--operation-hud-detail-line-height);
    min-height: var(--operation-hud-detail-line-height);
    white-space: normal;
    -unity-text-align: upper-left;
}

.mode-hud__detail--following {
    margin-top: var(--operation-hud-detail-gap);
}

.mode-hud__warning {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
}

/* 帯: 黒は背景。黄・刻み・角度は C# が customStyle で読むので帯のクラスで受け直す */
/* Band: black is the background; yellow, stripe size and angle are re-declared on the band class for C# customStyle reads */
.mode-hud__warning-band {
    position: absolute;
    left: 0;
    right: 0;
    height: var(--delete-mode-warning-band-height);
    background-color: var(--delete-mode-warning-black);
    --mode-hud-band-yellow: var(--delete-mode-warning-yellow);
    --mode-hud-band-stripe-size: var(--delete-mode-warning-stripe-size);
    --mode-hud-band-stripe-angle: var(--delete-mode-warning-stripe-angle);
}

.mode-hud__warning-band--top {
    top: 0;
}

.mode-hud__warning-band--bottom {
    bottom: 0;
}
```
（`.mode-hud__detail` の font-size・行の高さは `.key-hint-text`〈Foundation/Theme の区画〉と同じ詳細度 1 クラスで、`Screens` 区画が後に読まれるので画面が勝つ〈Web の `:where()` の詳細度 0 と同じ結果〉。`overflow-wrap: anywhere` は UITK に無く `white-space: normal` の語折りになる。288px に収まらない長い 1 語〈ブループリント名〉だけ差が出うるので、Task 10 の比較で見て差があれば `LX-32` を記帳する）

`ModeHudPresenter.cs`:
```csharp
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.ModeHud;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.ModeHud
{
    /// <summary>
    /// 操作モード HUD を Chrome 層の Viewport 族へ起動時に 1 回だけ載せる（Web の .viewportOverlay・z 31）
    /// Mounts the operation-mode HUD once at startup into the Chrome layer's Viewport family (Web .viewportOverlay, z 31)
    /// </summary>
    public sealed class ModeHudPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly ModeHudModel _model;

        public ModeHudPresenter(GameUiRoot gameUiRoot, ModeHudModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            // 載せるのは 1 回だけ。購読は Presenter が持ち、購読時の今の値で 1 回描く（前例 ToastLayerPresenter・PauseMenuScreenPresenter）
            // Mount once; the presenter owns the subscription, which draws once with the current value (precedent ToastLayerPresenter, PauseMenuScreenPresenter)
            var view = new ModeHudView();
            _gameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(view);
            _model.Current.Subscribe(view.Render);
        }
    }
}
```

`GameUiTheme.tss` の `/* == section: Screens == */` 区画の末尾に 1 行:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/ModeHud/ModeHud.uss");
```

- [ ] **Step 5: 通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Screens\.ModeHud|Conventions|Theme|Anchor)\."`
Expected: `ErrorCount: 0`。ModeHud 16 件（View 4・Wide 2×2・Bands 1・Design 4・Geometry 3。Wide は fixture ごとに数える）と L2 が PASS。詳細と削除箱の文の完全一致は `<line-height=25px>` 込み（`DetailLineHeightTag`）で、つまみを外して通すのは禁止。`DeleteModeWarningBandsTest` の角度で落ちたら Task 5 Step 5 の注記どおり読み手を直す。`letterSpacing` が 8.333 で合わなければ HudSectionHeading のつまみが祖先の上書きを受けていない〈`UssKnobInheritanceTest` の前提〉ので、`done.md` の「未確認」に実測値を書いて Task 10 の比較で決める

- [ ] **Step 6: 錨の未移植の表があれば自分の 2 行を消す**

S2-tutorial が先にマージされていれば、錨の未移植の表に `placement.hud`・`delete.hud` の行がある。印（`TutorialAnchor.Mark`）を付けたレーンが同じ PR で自分の行を消す（S2-tutorial 計画の表の規則・実行計画 E-1）:
```bash
git fetch origin && git merge --no-ff --no-edit origin/feature/uitk
P=moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs
test -f "$P" && grep -n "AnchorIds.PlacementHud\|AnchorIds.DeleteHud" "$P" || echo "no-pending-table"
```
Expected: `no-pending-table` なら何もしない（Task 13 Step 2 でもう一度見る）。行が出たらその 2 行だけを消し（他レーンの行と冒頭の規則のコメントは触らない）、`uloop compile` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.Anchor.TutorialAnchorCoverageTest"` が PASS（`Mark` 済みの 2 つが表に無くても通る）

- [ ] **Step 7: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/ModeHud moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/ModeHud "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
test -f moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs && git add moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs
git commit -m "feat(uitk): 操作モード HUD（配置 HUD・削除の帯・削除箱の高さ）を UITK で描く（S2-mode-tooltip）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: CursorTooltip の View・USS・Presenter

**Files:**
- Create: `S/Client.Ui/Screens/CursorTooltip/CursorTooltipView.cs`・`CursorTooltip.uss`・`CursorPointerTracker.cs`・`ICursorTooltipScreen.cs`・`UnityCursorTooltipScreen.cs`・`CursorTooltipPresenter.cs`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に 1 行）
- Test: `S/Client.Tests/Ui/Screens/CursorTooltip/CursorTooltipViewModelSamples.cs`・`FakeCursorTooltipScreen.cs`・`CursorTooltipViewTest.cs`・`CursorTooltipPositionViewTest.cs`、`S/Client.Tests/Ui/Screens/ModeHud/ModeHudPickingTest.cs`

**Interfaces:**
- Consumes: Task 2 の `CursorTooltipPlacement.PlaceInPanel`、Task 4 の `CursorTooltipViewModel`、`TooltipFace(string, TooltipFaceLook)`・`SetLines`、`GameUiRoot.GetLayer(UiLayer.Tooltip, UiFamily.Viewport)`、`Client.Input.HybridInput.GetMousePosition()`・`Client.Input.ScreenCenter.GetPosition()`（画面中央の唯一の定義）、`RuntimePanelUtils.ScreenToPanel`、`IPanel.scaledPixelsPerPoint`（設計書 [A-41] の倍率の読み口）
- Produces:
  ```csharp
  namespace Client.Ui.Screens.CursorTooltip
  {
      // 実画面への問い合わせの唯一の口。ロックの分岐と画面 px→パネル座標の換算はここだけが持つ
      public interface ICursorTooltipScreen
      {
          Vector2 ResolveAnchorInPanel(IPanel panel, Vector2 latestPointerInPanel); // ロック中は ScreenCenter をパネル座標へ換算した値、それ以外は渡されたポインタ
          Vector2 CurrentPointerInPanel(IPanel panel);                               // 今のポインタ（View が載った時の初期値）
          float ScreenPixelsPerPanelPixel(IPanel panel);                             // 実画面 px / パネル単位
      }
      public sealed class UnityCursorTooltipScreen : ICursorTooltipScreen { public UnityCursorTooltipScreen(); }
      public sealed class CursorTooltipView : VisualElement
      {
          public CursorTooltipView(ICursorTooltipScreen screen);   // .cursor-tooltip。面の testid "cursor-tooltip"
          public void Render(CursorTooltipViewModel viewModel);    // Presenter が購読して呼ぶ（設計書 §4.2・[A-8]）
          public void SetPointer(Vector2 panelPosition);          // CursorPointerTracker が呼ぶ
      }
      internal sealed class CursorPointerTracker { public CursorPointerTracker(VisualElement panelRoot, CursorTooltipView view); public void Detach(); }
      public sealed class CursorTooltipPresenter : IInitializable { public CursorTooltipPresenter(GameUiRoot gameUiRoot, CursorTooltipModel model, ICursorTooltipScreen screen); }
  }
  ```

- [ ] **Step 0: 何も拾わない所のポインタ移動がパネル最上位に届くかを先に実測する**

`CursorPointerTracker` の前提（D-5）を、View を書く前に確かめる。Task 8 Step 3 の 1・2 行目で全体スイッチ `GameUi` を確かめて PlayMode に入り、ゲーム画面になってから:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var layer = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetLayer(Client.Ui.Foundation.Layers.UiLayer.Tooltip, Client.Ui.Foundation.Stage.UiFamily.Viewport);
var probe = new UnityEngine.UIElements.VisualElement { name = "s2mt-pointer-probe", pickingMode = UnityEngine.UIElements.PickingMode.Ignore };
layer.Add(probe);
layer.panel.visualTree.RegisterCallback<UnityEngine.UIElements.PointerMoveEvent>(e => probe.userData = e.position, UnityEngine.UIElements.TrickleDown.TrickleDown);
Client.Input.InputManager.MouseCursorVisible(true);
var target = new UnityEngine.Vector2(UnityEngine.Screen.width * 0.75f, UnityEngine.Screen.height * 0.75f);
Client.Playtest.Input.SemanticInput.MouseMoveTo(target);
var picked = layer.panel.Pick(new UnityEngine.Vector2(layer.layout.width * 0.75f, layer.layout.height * 0.25f));
return $"picked={(picked == null ? "null" : picked.name)}";'
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var layer = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetLayer(Client.Ui.Foundation.Layers.UiLayer.Tooltip, Client.Ui.Foundation.Stage.UiFamily.Viewport);
var probe = layer.Q("s2mt-pointer-probe");
var received = probe.userData == null ? "none" : probe.userData.ToString();
probe.RemoveFromHierarchy();
return $"received={received}";'
```
Expected: 1 回目が `picked=null`（その位置に拾う要素が無い）、2 回目（1 秒ほど後）が `received=(…)`（座標）。受信は 1 フレームの EDC の中では起きないので、登録と注入、読み出しの 2 回に分ける。`received=none` なら止め、`done.md` の「状態: 停止（何も拾わない所の PointerMoveEvent が届かない）」と「integrator への依頼」に「Client.Input にポインタ位置の変化の購読口（InputSystem の `Mouse.current.position` の変化を UniRx で流す）が要る」と書いて指揮へ報告する（`Client.Ui` は InputSystem を参照しないので画面では作らない。D-5）。結果は Task 8 Step 5 の台帳の 2 行目に書く。測ったら PlayMode を止める

- [ ] **Step 1: 見本・偽物と失敗するテストを書く**

`CursorTooltipViewModelSamples.cs`:
```csharp
using Client.Game.InGame.UI.Tooltip;
using Client.Ui.State.CursorTooltip;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    // Web の mock-host の tooltip・tooltipMultiLine と同じ行の表示状態。テストは View.Render へ直に渡す
    // Display states with the same lines as the Web mock-host tooltip and tooltipMultiLine; tests pass them straight to View.Render
    public static class CursorTooltipViewModelSamples
    {
        public static CursorTooltipViewModel Lines(params TooltipLine[] lines) => new(lines);
    }
}
```

`FakeCursorTooltipScreen.cs`:
```csharp
using Client.Ui.Screens.CursorTooltip;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    // 偽の実画面。ロック中の中央・倍率・今のポインタをテストが決める（既定はロックなし・1 倍・原点）
    // Fake screen; the test decides the locked centre, scale and current pointer (defaults: unlocked, 1x, origin)
    public sealed class FakeCursorTooltipScreen : ICursorTooltipScreen
    {
        private bool _locked;
        private Vector2 _lockedCenterInPanel;
        private float _scale = 1f;
        private Vector2 _currentPointer;

        public void SetLocked(Vector2 centerInPanel) { _locked = true; _lockedCenterInPanel = centerInPanel; }
        public void SetScale(float scale) => _scale = scale;
        public void SetCurrentPointer(Vector2 panelPosition) => _currentPointer = panelPosition;

        public Vector2 ResolveAnchorInPanel(IPanel panel, Vector2 latestPointerInPanel) => _locked ? _lockedCenterInPanel : latestPointerInPanel;
        public Vector2 CurrentPointerInPanel(IPanel panel) => _currentPointer;
        public float ScreenPixelsPerPanelPixel(IPanel panel) => _scale;
    }
}
```

`CursorTooltipViewTest.cs`:
```csharp
using System.Text.RegularExpressions;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Client.Tests.Ui.Components.Text;
using Client.Ui.Screens.CursorTooltip;
using Client.Ui.State.CursorTooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの文言・行・面（L1。Web の CursorTooltip.test.ts・commonHud.spec.ts）
    /// Cursor tooltip text, lines and face (L1; Web CursorTooltip.test.ts, commonHud.spec.ts)
    /// </summary>
    public class CursorTooltipViewTest : ScreenTestHost
    {
        private CursorTooltipView _view;

        [SetUp]
        public void MountTooltip()
        {
            _view = Mount(new CursorTooltipView(new FakeCursorTooltipScreen()));
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">interpolates textParams into the localized template</web-test>
        [Test]
        public void 引数を辞書の文へ差し込む()
        {
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { new LocalizationTextParam("count", "3") })));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Wire x3", Find<Label>(_view, "cursor-tooltip-line").text);
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">preserves a missing count and warns once across renders</web-test>
        [Test]
        public void 欠けた引数は枠のまま残しエラーを出す()
        {
            // UITK の補間は解くたびに出す（Web は 1 回だけ）。差は判断記録 D-13
            // UITK interpolation logs on every resolve (Web logs once); the difference is decision D-13
            // 引数 0 件の UiText は Localize.Get だけを通り補間に入らない（UiText.cs:84）。別名の引数を 1 つ持たせて補間に入れ、count の欠けを出す
            // A UiText with zero params skips interpolation (UiText.cs:84); carry one unrelated param so interpolation runs and reports the missing count
            LogAssert.Expect(LogType.Error, new Regex(@"no value for placeholder \{count\}"));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { new LocalizationTextParam("unrelated", "1") })));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Wire x{count}", Find<Label>(_view, "cursor-tooltip-line").text);

            // count=0 は欠けではなく 0 として差し込む（Web の後半）
            // count=0 is not missing and is interpolated as 0 (second half of the Web test)
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { new LocalizationTextParam("count", "0") })));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Wire x0", Find<Label>(_view, "cursor-tooltip-line").text);
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">shows a loud marker for an unknown localized key</web-test>
        [Test]
        public void 辞書に無いキーは大声の印で出す()
        {
            // LocalizationKey は public ctor で、GameObjectTooltipTarget が Inspector の文字列から作る。綴り違いのキーは [!key] で画面に出る（Web の warn 1 回は写さず、印そのものが気づく経路。D-27）
            // LocalizationKey has a public ctor and GameObjectTooltipTarget builds one from an Inspector string; a misspelt key shows as [!key] (Web's single warn is not ported; the marker itself is the notice, D-27)
            var unknown = new LocalizationKey("ui.tooltip.unknown");
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(unknown)));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "[!ui.tooltip.unknown]", Find<Label>(_view, "cursor-tooltip-line").text);
            _view.Render(CursorTooltipViewModel.Hidden);
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(unknown)));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "[!ui.tooltip.unknown]", Find<Label>(_view, "cursor-tooltip-line").text);
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">renders every line in order</web-test>
        /// <web-test file="e2e/tests/system/commonHud.spec.ts">ツールチップは複数行を順序どおり縦積みで表示する</web-test>
        [Test]
        public void 複数行を順に縦へ積む()
        {
            Localize.TrySetChosenLanguage("japanese");
            // 2 行目は名前付きの引数 3 つの差し込み（Web と同じ placeMaterialShortage）
            // The second line interpolates three named params (placeMaterialShortage, as on Web)
            var shortage = new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage, new[] { new LocalizationTextParam("itemName", "Iron Plate"), new LocalizationTextParam("ownedCount", "3"), new LocalizationTextParam("requiredCount", "10") });
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain), shortage));
            simulate.FrameUpdate();
            var lines = _view.Query<Label>("cursor-tooltip-line").ToList();
            Assert.AreEqual(new[] { TooltipFaceTest.LineHeightTag + "地形に埋まっています", TooltipFaceTest.LineHeightTag + "アイテム不足： Iron Plate 3/10" }, new[] { lines[0].text, lines[1].text });
            Assert.Greater(lines[1].worldBound.yMin, lines[0].worldBound.yMax - 0.5f);
        }

        /// <web-test file="e2e/tests/system/commonHud.spec.ts">採掘進捗・クロスヘア・tooltipのtopic eventを表示する</web-test>
        [Test]
        public void ツールチップは18pxの文字と余白6と10と最大幅320で出て拾わない()
        {
            Localize.TrySetChosenLanguage("japanese");
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.IsTrue(IsShown(face));
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "世界の対象", Find<Label>(_view, "cursor-tooltip-line").text);
            Assert.AreEqual(18f, Find<Label>(_view, "cursor-tooltip-line").resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(6f, face.resolvedStyle.paddingTop, 0.01f);
            Assert.AreEqual(10f, face.resolvedStyle.paddingLeft, 0.01f);
            Assert.AreEqual(320f, face.resolvedStyle.maxWidth.value, 0.01f);
            _view.Query<VisualElement>().ForEach(element => Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name));

            _view.Render(CursorTooltipViewModel.Hidden);
            Assert.IsFalse(IsShown(face));
        }
    }
}
```
（commonHud 3 件目は採掘進捗・クロスヘア〈S2-hud〉と tooltip の 3 つを 1 件で見る。このレーンは tooltip の部分を写し、S2-hud も同じ Web テスト名で自分の部分に `<web-test>` を付ける〈同名の複数付与は可〉。done.md の「integrator への依頼」に「commonHud 3 件目は S2-hud と 2 レーンで分担して付けた」と書く）

`CursorTooltipPositionViewTest.cs`:
```csharp
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Client.Ui.Screens.CursorTooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの位置: ポインタの右下 12px・端で押し戻す・ロック中は実画面の問い合わせが返す中央・言語変更で置き直す（L1。Web の CursorTooltip.test.ts・commonHud.spec.ts）
    /// Cursor tooltip position: 12px below-right, pushed back at edges, the screen query's centre while locked, re-placed on language change (L1; Web CursorTooltip.test.ts, commonHud.spec.ts)
    /// </summary>
    public class CursorTooltipPositionViewTest : ScreenTestHost
    {
        private FakeCursorTooltipScreen _screen;
        private CursorTooltipView _view;

        [SetUp]
        public void MountTooltip()
        {
            _screen = new FakeCursorTooltipScreen();
            _view = Mount(new CursorTooltipView(_screen));
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">positions from the latest pointer when the tooltip first appears without further pointer moves</web-test>
        /// <web-test file="e2e/tests/system/commonHud.spec.ts">ツールチップは非表示中に届いたpointer位置へ初回表示から出る</web-test>
        [Test]
        public void 非表示中に動いたポインタの位置へ最初の表示から出る()
        {
            simulate.MouseMove(new Vector2(640f, 360f));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.AreEqual(652f, face.worldBound.xMin, 0.5f);
            Assert.AreEqual(372f, face.worldBound.yMin, 0.5f);
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">recalculates position when locale changes the resolved text</web-test>
        [Test]
        public void 言語で幅が変わると右端の押し戻しを置き直す()
        {
            simulate.MouseMove(new Vector2(1270f, 100f));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.AreEqual(1280f - 12f, face.worldBound.xMax, 0.5f);

            Localize.TrySetChosenLanguage("japanese");
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(1280f - 12f, face.worldBound.xMax, 0.5f, "幅が変わっても右端から 12px");
        }

        [Test]
        public void ロック中は実画面の問い合わせが返す中央を指したものとして置く()
        {
            simulate.MouseMove(new Vector2(1240f, 40f));
            _screen.SetLocked(new Vector2(640f, 360f));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.AreEqual(652f, face.worldBound.xMin, 0.5f);
            Assert.AreEqual(372f, face.worldBound.yMin, 0.5f);
        }

        [Test]
        public void 載った時は今のポインタを初期位置にする()
        {
            _screen.SetCurrentPointer(new Vector2(100f, 200f));
            var view = Mount(new CursorTooltipView(_screen));
            view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            Assert.AreEqual(112f, view.Q<VisualElement>("cursor-tooltip").worldBound.xMin, 0.5f);
            Assert.AreEqual(212f, view.Q<VisualElement>("cursor-tooltip").worldBound.yMin, 0.5f);
        }
    }
}
```
（`simulate.MouseMove(Vector2)` は `com.unity.ui.test-framework` の `PanelSimulator` にある〈`MouseMove(Vector2 positionTo, EventModifiers modifiers = EventModifiers.None)`〉。テストのパネルのルートが拾う設定だと「何も拾わない所でも受ける」の主張が L1 では弱いので、Task 11 の録画で確かめる。ロック中の中央が `ScreenCenter` の換算になることは本番の `UnityCursorTooltipScreen` の責務で、録画 `cursor-tooltip-follows-crosshair` が確かめる）

- [ ] **Step 2: 失敗を確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CursorTooltipView` が無いのでコンパイルが失敗する

- [ ] **Step 3: 実装する**

`ICursorTooltipScreen.cs`（Produces の形のまま。日英の 2 行コメントをメソッドごとに付ける）

`UnityCursorTooltipScreen.cs`:
```csharp
using Client.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// 本番の実画面。ロック中は ScreenCenter、そうでなければ届いたポインタを指し、倍率はパネルの scaledPixelsPerPoint（設計書 [A-41]）
    /// Production screen: points at ScreenCenter while locked, otherwise at the latest pointer; scale is the panel's scaledPixelsPerPoint (design [A-41])
    /// </summary>
    public sealed class UnityCursorTooltipScreen : ICursorTooltipScreen
    {
        public Vector2 ResolveAnchorInPanel(IPanel panel, Vector2 latestPointerInPanel)
        {
            // ロック中はポインタが凍るので、クロスヘアと同じ画面中央を指す。ロックの唯一の書き手は InputManager.MouseCursorVisible（前例 UiPointerHitTest と同じ読み方）
            // The pointer freezes while locked, so point at the screen centre like the crosshair; the only lock writer is InputManager.MouseCursorVisible (same read as UiPointerHitTest)
            if (Cursor.lockState != CursorLockMode.Locked) return latestPointerInPanel;
            return ScreenToPanel(panel, ScreenCenter.GetPosition());
        }

        public Vector2 CurrentPointerInPanel(IPanel panel)
        {
            var mouse = HybridInput.GetMousePosition();
            return ScreenToPanel(panel, new Vector2(mouse.x, mouse.y));
        }

        public float ScreenPixelsPerPanelPixel(IPanel panel)
        {
            return panel.scaledPixelsPerPoint;
        }

        // 画面 px（左下原点）を上原点へ直してパネル座標へ変える（Foundation/Input/GameUiPointerProbe と同じ換算。本PR外の提案で 1 本へ寄せる）
        // Flip screen pixels (bottom-left origin) to top origin, then convert to panel coordinates (same as Foundation/Input/GameUiPointerProbe; merging is an out-of-PR proposal)
        private static Vector2 ScreenToPanel(IPanel panel, Vector2 screenPosition)
        {
            return RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }
    }
}
```
（`IPanel.scaledPixelsPerPoint` が公開されていなければ、同じ値を返す `VisualElement.scaledPixelsPerPoint` を View のルートから渡す形に読み替える。C3-b の先頭の実測〈`scaledPixelsPerPoint` と PanelSettings の倍率の一致〉が不一致だったら、C3-b が決めた代替の式に従う〈D-2〉）

`CursorPointerTracker.cs`:
```csharp
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// パネルのポインタ移動を最上位で受けて View へ渡す。ツールチップが隠れている間も受ける（Web の window の pointermove）
    /// Receives panel pointer moves at the top and hands them to the view, also while the tooltip is hidden (Web window pointermove)
    /// </summary>
    internal sealed class CursorPointerTracker
    {
        private readonly VisualElement _panelRoot;
        private readonly CursorTooltipView _view;

        public CursorPointerTracker(VisualElement panelRoot, CursorTooltipView view)
        {
            _panelRoot = panelRoot;
            _view = view;
            _panelRoot.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        }

        public void Detach()
        {
            _panelRoot.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            _view.SetPointer(evt.position);
        }
    }
}
```

`CursorTooltipView.cs`:
```csharp
using Client.Ui.Components.Text;
using Client.Ui.State.CursorTooltip;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの画面ルート。実画面の問い合わせが返す点の右下 12 実画面 px に面を置き、端で押し戻す。Model は購読しない（Presenter が Render を呼ぶ）
    /// Screen root of the cursor tooltip; places the face 12 screen px below-right of the point the screen query returns, pushed back at edges; never subscribes (the presenter calls Render)
    /// </summary>
    public sealed class CursorTooltipView : VisualElement
    {
        private readonly ICursorTooltipScreen _screen;
        private readonly TooltipFace _face;
        private Vector2 _pointer;
        private bool _visible;
        private CursorPointerTracker _tracker;

        public CursorTooltipView(ICursorTooltipScreen screen)
        {
            _screen = screen;
            pickingMode = PickingMode.Ignore;
            AddToClassList("cursor-tooltip");
            _face = new TooltipFace("cursor-tooltip", TooltipFaceLook.Cursor);
            _face.AddToClassList("cursor-tooltip__face");
            _face.style.display = DisplayStyle.None;
            Add(_face);

            // 文と言語で面の寸法が変わったら、画面の寸法が変わったら置き直す
            // Re-place when the face size changes with text or language, and when the screen size changes
            _face.RegisterCallback<GeometryChangedEvent>(_ => Reposition());
            RegisterCallback<GeometryChangedEvent>(_ => Reposition());
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(_ => _tracker?.Detach());
        }

        public void Render(CursorTooltipViewModel viewModel)
        {
            _visible = viewModel.Visible;
            _face.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_visible) return;
            _face.SetLines(viewModel.Lines);
            Reposition();
        }

        public void SetPointer(Vector2 panelPosition)
        {
            _pointer = panelPosition;
            Reposition();
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            // 載る前のポインタを初期値にし、以後の移動は隠れている間も追う（Web の 7179.3 の (12,12) 固定を避ける）
            // Start from the pointer before attaching, then follow moves even while hidden (avoids Web 7179.3's stuck (12,12))
            _pointer = _screen.CurrentPointerInPanel(panel);
            _tracker = new CursorPointerTracker(panel.visualTree, this);
        }

        private void Reposition()
        {
            // silent-ok: 隠れている間と載る前・寸法の決まる前は置かない / 次の GeometryChangedEvent で置き直され、L1 の位置の主張が外れを見る
            if (!_visible || panel == null || float.IsNaN(_face.layout.width) || float.IsNaN(layout.width) || layout.width <= 0f) return;

            // 指す点（ロック中の中央を含む）と倍率は実画面の問い合わせが決める
            // The point (including the centre while locked) and the scale come from the screen query
            var anchor = _screen.ResolveAnchorInPanel(panel, _pointer);
            var placed = CursorTooltipPlacement.PlaceInPanel(anchor, _face.layout.size, layout.size, _screen.ScreenPixelsPerPanelPixel(panel));
            _face.style.left = placed.x;
            _face.style.top = placed.y;
        }
    }
}
```
（`_face.layout.size` は `TooltipFace` の外寸〈余白・枠込み〉で、Web の `getBoundingClientRect()` と同じ。Viewport 族はパネルの倍率で拡大されるので、Web の `transform: scale(var(--ui-scale))` と同じ見た目の寸法になる〈D-2〉）

`CursorTooltip.uss`:
```css
/* web: moorestech_web/webui/src/shared/tooltip/style.module.css（.tooltip: position fixed・pointer-events none・transform-origin top left）と CursorTooltip.tsx */
/* 面の見た目は TooltipFace（Cursor）が持つ。ここは実画面いっぱいの入れ物と面の絶対配置だけ */
/* web: moorestech_web/webui/src/shared/tooltip/style.module.css (.tooltip: position fixed, pointer-events none, transform-origin top left) and CursorTooltip.tsx */
/* The face look belongs to TooltipFace (Cursor); this file only holds the full-viewport container and the face's absolute placement */
.cursor-tooltip {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
}

.cursor-tooltip__face {
    position: absolute;
}
```
（`.cursor-tooltip__face` は部品のルートに画面のクラスを足したもの。部品のクラス〈`.tooltip-face`〉は書かない）

`CursorTooltipPresenter.cs`:
```csharp
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.CursorTooltip;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップを Tooltip 層の Viewport 族へ起動時に 1 回だけ載せ、Model を購読して描かせる（Web の Portal・z 250・舞台の倍率で拡大）
    /// Mounts the cursor tooltip once at startup into the Tooltip layer's Viewport family and renders it from the model (Web Portal, z 250, scaled by the stage scale)
    /// </summary>
    public sealed class CursorTooltipPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly CursorTooltipModel _model;
        private readonly ICursorTooltipScreen _screen;

        public CursorTooltipPresenter(GameUiRoot gameUiRoot, CursorTooltipModel model, ICursorTooltipScreen screen)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
            _screen = screen;
        }

        public void Initialize()
        {
            // 載せてから購読する。購読時の今の値で 1 回描く（前例 ToastLayerPresenter）
            // Mount first, then subscribe; subscribing draws once with the current value (precedent ToastLayerPresenter)
            var view = new CursorTooltipView(_screen);
            _gameUiRoot.GetLayer(UiLayer.Tooltip, UiFamily.Viewport).Add(view);
            _model.Current.Subscribe(view.Render);
        }
    }
}
```

`GameUiTheme.tss` の `/* == section: Screens == */` 区画の末尾に 1 行:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/CursorTooltip/CursorTooltip.uss");
```

- [ ] **Step 4: 入力を奪わない Web の 3 行を写す**

Web `alwaysOnHudWheel.test.ts` の `it.each` 「%s は入力を奪わない」の 9 行のうち、配置モード・削除モード警告・カーソルツールチップの 3 行はこのレーンの持ち分（S2-hud 計画の 52 行目と W2a 設計書 A-58 の割り当て）。S2-hud の `AlwaysOnHudPickingTest` には書かず、自分のテストクラスに同じ `<web-test>` で写す。

`S/Client.Tests/Ui/Screens/ModeHud/ModeHudPickingTest.cs`:
```csharp
using Client.Game.InGame.UI.Tooltip;
using Client.Tests.Ui.Screens.CursorTooltip;
using Client.Ui.Screens.CursorTooltip;
using Client.Ui.Screens.ModeHud;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 配置モード・削除モード警告・カーソルツールチップは、出ている真ん中でも下の入力を奪わない（L1。Web alwaysOnHudWheel.test.ts の 9 行のうちこのレーンの 3 行。残りは S2-hud の AlwaysOnHudPickingTest）
    /// The placement HUD, delete-mode warning and cursor tooltip never take input even at their centre (L1; this lane's 3 of the 9 rows in Web alwaysOnHudWheel.test.ts; the rest are S2-hud's AlwaysOnHudPickingTest)
    /// </summary>
    public class ModeHudPickingTest : ScreenTestHost
    {
        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void 配置モードHUDは入力を奪わない()
        {
            var view = Mount(new ModeHudView());
            view.Render(ModeHudViewModelSamples.Placement());
            simulate.FrameUpdate();
            AssertPassesThrough(view, Find<VisualElement>(view, "placement-mode-hud").worldBound.center);
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void 削除モード警告は入力を奪わない()
        {
            var view = Mount(new ModeHudView());
            view.Render(ModeHudViewModelSamples.Delete(3));
            simulate.FrameUpdate();
            AssertPassesThrough(view, Find<VisualElement>(view, "delete-mode-warning-band").worldBound.center);
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void カーソルツールチップは入力を奪わない()
        {
            var view = Mount(new CursorTooltipView(new FakeCursorTooltipScreen()));
            view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            AssertPassesThrough(view, Find<VisualElement>(view, "cursor-tooltip").worldBound.center);
        }

        private static void AssertPassesThrough(VisualElement view, Vector2 point)
        {
            // 拾われるのは画面の外（下の層・ルート）だけで、画面の要素は拾われない
            // Only things outside the screen (lower layers, the root) are picked, never the screen's own elements
            var picked = view.panel.Pick(point);
            Assert.IsFalse(picked == view || (picked != null && view.Contains(picked)), picked == null ? "null" : picked.name);
        }
    }
}
```

- [ ] **Step 5: 通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Screens\.CursorTooltip|Screens\.ModeHud\.ModeHudPickingTest|State\.CursorTooltip|Conventions|Theme)\."`
Expected: `ErrorCount: 0`。CursorTooltip の View 9 件と State 6 件と `ModeHudPickingTest` 3 件と L2 が PASS。`done.md` の「integrator への依頼」に D-13 の 1 行（引数 0 件の UiText が補間を通らない）を書く。行の文の完全一致は `TooltipFaceTest.LineHeightTag`〈`<line-height=27.9px>`〉込みで書く（`TooltipFace` のつまみを外してテストを通すのは禁止）。`言語で幅が変わると…` が置き直しの遅れで落ちたら、`TooltipFace` の行の `GeometryChangedEvent` が面まで届いていない〈行だけが変わり面の寸法が変わらない〉ので、`_face` の子の `GeometryChangedEvent` も `TrickleDown` で受けて `Reposition` する

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/CursorTooltip moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/CursorTooltip moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/ModeHud/ModeHudPickingTest.cs "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): CursorTooltip を UITK で描き Web と同じ位置へ置く（S2-mode-tooltip）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: 登録と PlayMode での疎通（全体スイッチ UITK）

**Files:**
- Create: `S/Client.Starter/Registration/GameUi/ModeHudGameUiRegistration.cs`・`CursorTooltipGameUiRegistration.cs`
- Modify: `S/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 2 行）
- Modify: `docs/uitk/TODO.md`（「## 実測の結論」の末尾に 2 行）

**Interfaces:**
- Consumes: Task 3・4・6・7 の Model・Presenter・`UnityCursorTooltipScreen`、前例 `ToastGameUiRegistration`・`PauseMenuGameUiRegistration`
- Produces: 全体スイッチ UITK の起動で操作モード HUD と CursorTooltip が載る

- [ ] **Step 1: 登録ファイルを書く**

`ModeHudGameUiRegistration.cs`:
```csharp
using Client.Ui.Screens.ModeHud;
using Client.Ui.State.ModeHud;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // 操作モード HUD（GameUi）の登録。Model は GameUi の系だけが使うので画面と一緒にここで登録する
    // Registers the GameUi operation-mode HUD; only the GameUi system uses the model, so it is registered here with the screen
    internal static class ModeHudGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ModeHudModel>().AsSelf();
            builder.RegisterEntryPoint<ModeHudPresenter>();
        }
    }
}
```

`CursorTooltipGameUiRegistration.cs`:
```csharp
using Client.Ui.Screens.CursorTooltip;
using Client.Ui.State.CursorTooltip;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // カーソルツールチップ（GameUi）の登録。実画面の問い合わせは本番の実装を当てる
    // Registers the GameUi cursor tooltip; the screen query uses the production implementation
    internal static class CursorTooltipGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<CursorTooltipModel>().AsSelf();
            builder.Register<UnityCursorTooltipScreen>(Lifetime.Singleton).As<ICursorTooltipScreen>();
            builder.RegisterEntryPoint<CursorTooltipPresenter>();
        }
    }
}
```

`MainGameUiRegistration.cs` の画面登録区画（`ToastGameUiRegistration.Register(builder);` の下）に 2 行:
```csharp
            ModeHudGameUiRegistration.Register(builder);
            CursorTooltipGameUiRegistration.Register(builder);
```
（Model の `Current` は ctor で作られるので、Presenter の `Initialize` が Model の `Initialize` より先に走っても `Current` は使える。Model を共有の `MainGameUiStateRegistration` に足さない理由は D-19）

- [ ] **Step 2: コンパイルと L1・L2 を通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`（`$FILTER` は Global Constraints の値）
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 3: 全体スイッチを GameUi にして PlayMode へ入る**

Run:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
uloop control-play-mode --project-path ./moorestech_client --action Play
uloop get-logs --project-path ./moorestech_client --log-type Error
```
Expected: `GameUi`。PlayMode が起動し、ゲーム画面まで進んだ後に Error が 0 件（`ModeHudPresenter`・`CursorTooltipPresenter` の解決失敗〈VContainer の例外〉が無い）。テストの TearDown は `UiInputExclusivity` を Web に戻すが、全体スイッチ〈SessionState〉は戻さない。テストを流した後は必ずこの Step の 1 行目で `GameUi` を確かめてから PlayMode に入る

- [ ] **Step 4: 実画面でポインタ移動が届くこと・角度が読めることを実測する**

Run（PlayMode のまま。Tab でインベントリを開いてカーソルのロックを外した状態で）:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var root = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetLayer(Client.Ui.Foundation.Layers.UiLayer.Chrome, Client.Ui.Foundation.Stage.UiFamily.Viewport);
var band = root.Q<UnityEngine.UIElements.VisualElement>("delete-mode-warning-band");
var hasAngle = Client.Ui.Screens.ModeHud.Parts.DegreeCustomStyle.TryReadDegrees(band.customStyle, "--mode-hud-band-stripe-angle", out var degrees);
var picked = root.panel.Pick(new UnityEngine.Vector2(root.layout.width - 40f, 40f));
return $"lock={UnityEngine.Cursor.lockState} angle={hasAngle}:{degrees} picked={(picked == null ? "null" : picked.name)} panelScale={root.panel.scaledPixelsPerPoint}";'
```
Expected: `lock=None angle=True:117 picked=null`（右上の何も無い所は拾わない）と、実画面の幅に応じた倍率。ポインタの届き方は Task 7 Step 0 で先に実測済みで、Task 11 の録画 `uitk-mode-hud-and-tooltip` の「表示中の面がポインタに付いて動く」主張で実画面の配線ごと確かめる。録画で面が付いて来なければ Task 7 Step 0 の停止と同じく止めて指揮へ報告する（D-5）

- [ ] **Step 5: 台帳の「## 実測の結論」へ書く**

`docs/uitk/TODO.md` の「## 実測の結論」の表の末尾に、Step 4 と Task 11 の実測で決まった 2 行を書く（表の列は既存の行に合わせる）:
```markdown
| 角度の customStyle | `117deg` を `CustomStyleProperty<string>` で読めるか | S2-mode-tooltip | <Step 4 の結果: 読めた〈文字列 "117deg"〉/ 読めず float で 117 が返った 等> |
| 何も拾わない所のポインタ移動 | GameUi のパネルで、拾う要素が無い位置の `PointerMoveEvent` が `panel.visualTree` の TrickleDown で受けられるか | S2-mode-tooltip | <Task 7 Step 0 の `received` と `picked` の値、Task 11 の録画の結果> |
```

- [ ] **Step 6: PlayMode を止めてコミット**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
git add moorestech_client/Assets/Scripts/Client.Starter/Registration docs/uitk/TODO.md
git commit -m "feat(uitk): 操作モード HUD と CursorTooltip を GameUi の系へ登録する（S2-mode-tooltip）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
（Step 5 の 2 行目は Task 11 の後に埋めるので、ここでは 1 行目だけを足し、2 行目は Task 11 Step 5 で足す）

---

### Task 9: 写さない Web テストの処遇と名前の突き合わせ

**Files:**
- Modify: `docs/uitk/disposition.md`（「## 処遇」の表の末尾に 2 行。設計書 §4.3「テスト名と由来の対応は docs/uitk/disposition.md に、レーンが写したときに書く」）

**Interfaces:**
- Consumes: Task 2〜7 の `<web-test>`、`tools/uitk/test-name-parity.mjs`
- Produces: 束 2 のこのレーンの 7 ファイル（`alwaysOnHudWheel.test.ts` の持ち分 3 行を含む）の未対応 0

- [ ] **Step 1: 処遇の行を足す**

`docs/uitk/disposition.md` の「## 処遇」の表の末尾に:
```markdown
| 2 | `src/shared/tooltip/CursorTooltip.test.ts` | resolves a content key from the dictionary without a raw-text fallback | 捨てる | UITK の CursorTooltip は `MouseCursorTooltipState` の `TooltipLine`（型付きの `LocalizationKey` だけ）を受け、Web の topic の contentKey（文字列のキー）の経路が無い。内容辞書の解決は `UiText.FromContent` と `LocalizationTextResolver` の既存 L1 が持つ | — |
| 2 | `src/features/modeHud/modeHudDesign.test.ts` | 警告色のコントラスト改善を既存consumerへ共有する | 捨てる | UITK の操作モード HUD は置けない理由の警告の行を持たない（C# に書き手が無く、Web の橋も常に空文字。Web の mock だけが流す。D-7）。他の consumer（通知・列車の網・不足の強調）はそれぞれの画面レーンが写す | — |
```
（`PR` 列は Task 13 Step 1 の後に PR 番号へ書き換える）

- [ ] **Step 2: 名前を突き合わせる**

Run:
```bash
node tools/uitk/test-name-parity.mjs --bundle 2 --emit-missing | grep -E "modeHud|operation-mode-hud|shared/tooltip|commonHud" || true
node tools/uitk/test-name-parity.mjs --bundle 2; echo "exit=$?"
```
Expected: 1 行目はこのレーンの 7 ファイル（`alwaysOnHudWheel.test.ts` の「%s は入力を奪わない」〈S2-hud と同名で分担〉・`operation-mode-hud.spec.ts`・`PlacementModeHud.localization.test.ts`・`modeHudDesign.test.ts`・`tooltipPosition.test.ts`・`CursorTooltip.test.ts`・`commonHud.spec.ts` の 1・3・4・5 件目）の行が出ない。commonHud の 2・6 件目は S2-hud の持ち物なので出てよい。2 行目の終了コードは 1 でよい（束 2 の他レーンの未対応が残るため。束の差分 0 は S2-ladder が締める）。終了コード 2（書式の誤り）なら `<web-test>` の綴りを直す

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/disposition.md
git commit -m "docs(uitk): CursorTooltip と modeHudDesign の写さない Web テスト 2 件の処遇を書く（S2-mode-tooltip）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Web との並べ比べ（1280×720・2560×1080）

**Files:**
- なし（撮影の原本は `moorestech_client/.uloop/outputs/compare/S2-mode-tooltip/`。差の表は `done.md`）。直しが出たら `ModeHud.uss`・`CursorTooltip.uss` と、必要なら台帳「## 見た目の例外」の末尾（`LX-32`〜`LX-34`）

**Interfaces:**
- Consumes: 参照キャプチャ `../moorestech_logs/uitk/reference-captures/web-a9d5123537/` の `2-placement.png`・`2-placement-connect-tool.png`・`2-delete-bar.png`・`2-tooltip.png`・`2-tooltip-multiline.png`（`manifest.json`・`index.md` で状態を確かめる）
- Produces: 5 状態 × 2 解像度の組と差の表（`2-placement-unavailable` は `捨てる`。D-7）

- [ ] **Step 1: 2560×1080 の Web 参照を用意する**

先に `ls ../moorestech_logs/uitk/reference-captures/ | grep 2560x1080` で他レーン・integrator が撮ったものを探し、上の 5 つの撮影 id があればそれを使う。無ければ master の worktree で撮る（`feature/uitk` の Web には手を入れない。compare-procedure §2）:
```bash
moores-wt new uitk-ref-2560-s2mt --from origin/master --no-editor
cd <作られた worktree の絶対パス>/moorestech_web/webui && pnpm install
sed -i '' 's/referenceViewport = { width: 1280, height: 720 }/referenceViewport = { width: 2560, height: 1080 }/' e2e/uitk-reference/shotRunner.ts
SHA=$(git rev-parse --short=10 HEAD)
UITK_REFERENCE_OUT=<moorestech_logs の絶対パス>/uitk/reference-captures/web-$SHA-2560x1080 \
UITK_REFERENCE_PORT=<空きポート> node --import tsx e2e/uitk-reference/capture.ts
git checkout -- e2e/uitk-reference/shotRunner.ts
cd - && moores-wt rm uitk-ref-2560-s2mt --force --prune-branch
```
Expected: `web-<sha>-2560x1080/` に 5 つの撮影 id の png と `manifest.json`。`git status` が空のまま worktree が消える（`shotRunner.ts` の書き換えはコミットしない）。撮ったら `done.md` の「integrator への依頼」に「2560×1080 の参照 `web-<sha>-2560x1080` を撮った。台帳『## 参照キャプチャ』への記載をお願いしたい」と書く（台帳のその節はレーンの共有区画ではない）

- [ ] **Step 2: 撮影の口を用意する（PlayMode・GameUi・言語・複製した PanelSettings の RenderTexture）**

Task 8 Step 3 と同じく全体スイッチ `GameUi` を確かめて PlayMode に入り、開幕スキットを飛ばす。撮影は theme レーンの前例（`../moorestech_logs/harness/sdd/feature-uitk-s1-theme/workspace/task-5-brief.md` の Step 7・`task-5-report.md` の実撮影）に揃え、`GameUiPanelSettings.asset` を `Object.Instantiate` した複製に `targetTexture`（1280×720 と 2560×1080 の sRGB ARGB32 RenderTexture）を付けた UIDocument へ載せて、RT を `ReadPixels` する。Game ビューの解像度は変えない（`uloop screenshot` の rendering には UITK が写らない）。

言語は Web の参照に揃える。`manifest.json` と参照の png を Read して言語を確かめる（`2-placement` は日本語で撮られている）。日本語なら最初の EDC の先頭で `Client.Localization.Localize.TrySetChosenLanguage("japanese");` を呼び、撮り終えたら元の言語へ戻す。
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
Client.Localization.Localize.TrySetChosenLanguage("japanese");
var source = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset");
foreach (var size in new[] { new UnityEngine.Vector2Int(1280, 720), new UnityEngine.Vector2Int(2560, 1080) })
{
    var settings = UnityEngine.Object.Instantiate(source);
    settings.targetTexture = new UnityEngine.RenderTexture(size.x, size.y, 0, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB) { name = $"s2mt-rt-{size.x}" };
    var go = new UnityEngine.GameObject($"s2mt-compare-{size.x}");
    var document = go.AddComponent<UnityEngine.UIElements.UIDocument>();
    document.panelSettings = settings;
    // 本番のルートと同じロケールのクラスを当てる（GameUiLocaleClassBinder が GameUiRoot に当てるもの。日本語は CJK の字形と行送り。Typography.uss）
    // Apply the same locale class the production root gets (what GameUiLocaleClassBinder applies to GameUiRoot; Japanese uses CJK glyphs and leading; Typography.uss)
    document.rootVisualElement.EnableInClassList(Client.Ui.Foundation.Text.GameUiLocaleClass.CjkClassName, Client.Ui.Foundation.Text.GameUiLocaleClass.IsCjk(Client.Localization.Localize.GetCurrentLanguageCode()));
}
return "ok";'
```
Expected: `ok`。以後の Step は、名前 `s2mt-compare-1280`・`s2mt-compare-2560` の UIDocument の `rootVisualElement` に撮る物を載せ、`uloop focus-window` の後の 2 回目の EDC で `RenderTexture.active = settings.targetTexture` → `Texture2D.ReadPixels` → `EncodeToPNG` で `moorestech_client/.uloop/outputs/compare/S2-mode-tooltip/uitk-<撮影 id>-<w>x<h>.png` へ書く（書いた先が worktree の下であることを `ls -la` で確かめる）。ルートの背景は参照の世界の色に近い中間色（前例は `#9c7b42`）にし、差の表で「背景は世界でなく単色」と書く。この口が使えなければ止め、`done.md` の「integrator への依頼」に書いて指揮へ報告する。手順を compare-procedure.md に書くのは integrator の持ち物なので、「integrator への依頼」に「2560×1080 の UITK 撮影は PanelSettings 複製＋RT の手順を compare-procedure.md へ」と書く

- [ ] **Step 3: 操作モード HUD の 3 状態を撮る**

Web の参照は mock-host の固定値（`Assembler`・高さ 3・接続ツールの表示名）で撮られており、実ゲームでは同じ値が作れない。View を Web と同じ固定値の ViewModel で Step 2 の 2 つの UIDocument へ載せて撮る:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
foreach (var name in new[] { "s2mt-compare-1280", "s2mt-compare-2560" })
{
    var root = UnityEngine.GameObject.Find(name).GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
    root.Clear();
    var view = new Client.Ui.Screens.ModeHud.ModeHudView { name = "compare-mode-hud" };
    root.Add(view);
    view.Render(new Client.Ui.State.ModeHud.ModeHudPlacement(Client.Ui.State.ModeHud.ModeHudText.Of("Assembler"), 3));
}
return "ok";'
```
Expected: 2 回目の EDC（Step 2 の ReadPixels）で `uitk-2-placement-1280x720.png`・`uitk-2-placement-2560x1080.png` が書かれ、`Read` で開くと右上に HUD がある。同じ手順で `view.Render` の引数を、接続ツールの表示名の `new ModeHudPlacement(ModeHudText.Of(...), 3)`（参照 `2-placement-connect-tool.png` を Read して名前を写す）・`new ModeHudDelete(3)`（`2-delete-bar`）に替えて撮る。参照 `2-placement-unavailable` は撮らず、差の表に `捨てる`（置けない理由の行は実ゲームに書き手が無い。D-7）と書く

- [ ] **Step 4: CursorTooltip の 2 状態を撮る**

位置の正しさは L1（`CursorTooltipPositionViewTest`）と Task 11 の録画が持つので、並べ比べは面の見た目だけを見る。参照 `2-tooltip.png` を Read して面の左上の座標を読み、`.cursor-tooltip` の根に `TooltipFace("cursor-tooltip", TooltipFaceLook.Cursor)` を載せてその座標（2560×1080 では倍率を掛けた座標）へ絶対配置する:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
foreach (var name in new[] { "s2mt-compare-1280", "s2mt-compare-2560" })
{
    var root = UnityEngine.GameObject.Find(name).GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
    root.Clear();
    var screen = new UnityEngine.UIElements.VisualElement();
    screen.AddToClassList("cursor-tooltip");
    var face = new Client.Ui.Components.Text.TooltipFace("cursor-tooltip", Client.Ui.Components.Text.TooltipFaceLook.Cursor);
    face.style.position = UnityEngine.UIElements.Position.Absolute; face.style.left = <参照の x>; face.style.top = <参照の y>;
    face.SetLines(new[] { new Client.Ui.State.Text.UiText(Mooresmaster.Localization.Generated.LocalizationKeys.Ui.Tooltip.WorldTarget) });
    screen.Add(face);
    root.Add(screen);
}
return "ok";'
```
`2-tooltip-multiline` は `SetLines` に `PlaceBlockedByTerrain`・`PlaceTooFar` の 2 行を渡して撮る。Step 2 の ReadPixels で `uitk-2-tooltip-<w>x<h>.png`・`uitk-2-tooltip-multiline-<w>x<h>.png` を撮る

- [ ] **Step 5: 差の表を書き、口を片付ける**

参照と UITK を `Read` で並べて見て、`done.md` の「## 差の表」に状態ごとに compare-procedure §4 の表（位置・寸法／色／装飾の有無／文字の段組）を書く。見る点:
- 配置 HUD: 右上 24px・幅 288（2560×1080 は 1.5 倍で 36px・432px）、題の字間（I-fix1 の換算トークンで Web の 1px と同じ詰まりに見えるか）、罫線、詳細の行間 25px、クラフトの面と右下のグリップ
- 削除: 帯の高さ 20（1.5 倍で 30）、斜線の角度と 32px の刻み、黄 rgb(255,187,36)、帯の端の切れ方
- ツールチップ: 角丸 4px、行の間、最大幅 320 での折り返し、2560×1080 で面が 1.5 倍になること（ポインタからの 12 実画面 px は L1 と録画で見る）
一目で分かる差は USS で直して撮り直す。同じにするのが大きくて差が一目で分からないものだけ、台帳「## 見た目の例外」の末尾へ `LX-32`〜`LX-34` で記帳し、該当行に `/* look-exception: LX-n */` を付ける（`LookExceptionLedgerTest` が照合する）。範囲を超えるなら止めて指揮へ番号を求める。撮り終えたら `s2mt-compare-*` の GameObject を `Object.Destroy` し、RT を `Release` して、言語を元へ戻す

- [ ] **Step 6: 直しがあればコミット**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"
git add moorestech_client/Assets/Scripts/Client.Ui/Screens docs/uitk/TODO.md
git commit -m "fix(uitk): 操作モード HUD と CursorTooltip を Web との並べ比べで揃える（S2-mode-tooltip）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
（直しが無ければコミットしない）

---

### Task 11: unityプレイ録画テスト（`misc/cursor-tooltip-follows-crosshair`・`uitk/stage2/uitk-mode-hud-and-tooltip`）

**Files:**
- Modify: `SKILL/scenarios/misc/cursor-tooltip-follows-crosshair.cs`
- Create: `SKILL/scenarios/uitk/stage2/uitk-mode-hud-and-tooltip.cs`
- Modify: `docs/uitk/TODO.md`（Task 8 Step 5 の 2 行目）

**Interfaces:**
- Consumes: プレイテスト DSL（`p.SkipOpeningSkit`・`p.Hotbar.AssignHotbar`・`p.Hotbar.EnterBuildMode`・`p.PressKey`・`p.WaitUiState`・`p.Screenshot`・`PlaytestUiElementOps.FindUi`・`SemanticInput.MouseMoveTo`）、`UiInputExclusivity.ActiveUiSystem`、`IMouseCursorTooltip`
- Produces: 録画 2 本（`result.json` が `Success: true`）

- [ ] **Step 1: 完了条件の録画に GameUi の分岐を足す**

`cursor-tooltip-follows-crosshair.cs` の「注入カーソルを画面隅へ寄せ…」の塊（`var corner = …` から隅の `p.Assert(…)` まで）を次に置き換える。冒頭の説明コメントにも日英で「GameUi の系ではロック中のツールチップをポインタではなく画面中央に置くので、隅へ注入しても中央近くに居続けることを主張する」を足す:
```csharp
    // 注入カーソルを画面隅へ寄せる。Web は面が隅へ付いて来ることで前提の到達を確かめ、GameUi はロック中なので中央に居続けることを確かめる
    // Park the injected cursor in a corner; Web confirms the precondition by the face following it, GameUi confirms the face stays centred while locked
    var corner = new Vector2(Screen.width - 40f, 40f);
    var screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
    var isGameUi = UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi;
    p.Note($"注入カーソルを画面隅{corner}へ寄せる（系={UiInputExclusivity.ActiveUiSystem}）");
    SemanticInput.MouseMoveTo(corner);
    await p.WaitSeconds(1.5f);
    if (isGameUi) p.Assert(await IsTooltipNearAsync(screenCenter, "クロスヘア"), "ロック中は隅へ注入してもツールチップが中央近くに居続ける（GameUi）");
    else p.Assert(await IsTooltipNearAsync(corner, "隅へ退避したカーソル"), "退避先の隅にツールチップが追従した（前提がWeb側へ届いた）");
```
後段の `var screenCenter = …` の宣言は上へ移したので消す。`using Client.Input;` を足す。ファイルは 200 行未満のまま

- [ ] **Step 2: 操作モード HUD とポインタ追従の録画を書く**

`uitk-mode-hud-and-tooltip.cs`（前例 `SKILL/scenarios/uitk/stage1/uitk-lifeline.cs` の冒頭の入力準備と、`misc/right-short-press-cancel.cs` の `AssignHotbar`・`EnterBuildMode`）。流れ:
1. 冒頭で `p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）")`。入力の準備（`backgroundBehavior`・`EnsureDevices`・`EnableDevice`）は uitk-lifeline と同じ
2. `p.SkipOpeningSkit()` → `p.Hotbar.AssignHotbar(0, "木のチェスト")` → `p.Hotbar.EnterBuildMode(0)` → `p.WaitUiState(UIStateEnum.PlaceBlock, 15f)`
3. `PlaytestUiElementOps.FindUi("placement-mode-hud")` が見つかり、その配下の `operation-mode-detail`（同名 Label が 2 つあり `FindUi` は Ambiguous で文が空になるので、GameUi の木を親の配下で `Query("operation-mode-detail").ToList()` で引く）の 1 つ目の文に `木のチェスト` を含む。`p.Screenshot("01-placement")`
4. `p.PressKey(Key.E)`（`PlacementHeightKeyInput` で高さ +1）→ 配下の `operation-mode-detail` の 2 つ目の文が `高さ: 1`（english なら `Height: 1`。言語は `Localize` の現在値で分岐せず、`Localize.Get` で期待文を組む）で終わる（`LocalizedLabel` が頭に `<line-height=25px>` を付けるので完全一致でなく末尾一致で見る。つまみを外して合わせるのは禁止）。`p.Screenshot("02-height")`
5. `p.PressKey(Key.G)` → `p.WaitUiState(UIStateEnum.DeleteBar, 10f)` → 親 `FindUi("delete-mode-warning")` が見つかり、その配下の `delete-mode-warning-band`（上下 2 本。同名なので `FindUi` でなく `Query(...).ToList()` で数える）が 2 本あり、`FindUi("placement-mode-hud")` が見つからない。`FindUi("delete-box-height-hud")` の文が高さを含む。`p.Screenshot("03-delete-bands")`
6. `p.PressKey(Key.G)` → `GameScreen`。`FindUi("delete-mode-warning")` が見つからない
7. ポインタ追従: `p.PressKey(Key.Tab)` → `PlayerInventory`（ロックが外れる）→ DI の `IMouseCursorTooltip` に `Show(owner, LocalizationKeys.Ui.Tooltip.WorldTarget)` → `SemanticInput.MouseMoveTo(A)`（A = 画面の 1/4,1/4）→ 1 秒待って `FindUi("cursor-tooltip")` の矩形の左上が A の右下 12px 付近（20px 以内）→ `MouseMoveTo(B)`（B = 3/4,1/4）→ 同じく B に付いて来る（何も拾わない所でもポインタ移動が届くことの実測。Task 8 Step 4）。`p.Screenshot("04-tooltip-follows")` → `Hide(owner)` → `p.PressKey(Key.Tab)`
`FindUi` の座標は画面 px（左下原点か上原点かは `UiElementSnapshot.ScreenRect` の実物を読んで合わせる）。`p.UntilUiElement` は「クリックできる」を待つので、拾わない HUD には使わず `FindUi` を期限付きで回す（前例 `cursor-tooltip-follows-crosshair.cs` の `PollUntilQueryAsync`）。ファイルは 200 行未満、`#region Internal` にローカル関数をまとめる

- [ ] **Step 3: 全体スイッチが GameUi であることを確かめてから回す**

Run:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
SKILL=.agents/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/misc/cursor-tooltip-follows-crosshair.cs"
```
Expected: 2 行目が `GameUi`。`result.json` が `Success: true`、Note に `系=GameUi`、録画 mp4 がある。初回に "already playing" で落ちたら PlayMode を止めて同じコマンドをもう 1 回だけ流す（run-scenario.sh の既知の癖）。失敗したら `result.json` と `uloop get-logs --log-type Error` を読み、原因を `done.md` に書いて直す（直したらテストを回し、このステップの 2 行目から流し直す）

- [ ] **Step 4: 2 本目を回す**

Run: Step 3 の 2 行目（`GameUi` の確認）を流してから `"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/uitk/stage2/uitk-mode-hud-and-tooltip.cs"`
Expected: `Success: true`、スクショ 4 枚と録画 mp4。7 のポインタ追従が落ちたら Task 7 Step 0 の停止と同じく止めて指揮へ報告する

- [ ] **Step 5: 台帳の実測の 2 行目を足し、証拠を done.md に書いてコミット**

Task 8 Step 5 の 2 行目（何も拾わない所のポインタ移動）を Task 7 Step 0 と Step 4 の結果で埋めて足す。`done.md` の「## 証拠」に 2 本の `result.json` のパス・`Success`・録画 mp4 のパス・Note の `系=GameUi` の行を書く。録画とスクショの原本は worktree の gitignore 下に置いたまま（`moorestech_logs` へ入れない）
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios docs/uitk/TODO.md
git commit -m "test(uitk): CursorTooltip のロック中の中央と操作モード HUD を UITK の録画で通す（S2-mode-tooltip）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイルだけ

**Interfaces:**
- Consumes: Task 1〜11 のコミット一式（`origin/feature/uitk..HEAD`）
- Produces: レビュー記録 `../moorestech_logs/harness/` 配下（`$RUNDIR`）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。レビューが裁定・設計書と食い違う直し（Web と違う見た目へ寄せる・画面 USS に部品のクラスを書く・色を C# に書く・ツールチップを端で反転させる〈D-3〉・Foundation や部品を書き換える）を提案したら採らず、`done.md` の「要裁定」に書く

- [ ] **Step 2: 自動適用があれば検査と録画を回し直してコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`
Expected: `ErrorCount: 0`、全件 PASS。自動適用が `CursorTooltipView`・`CursorPointerTracker`・`UnityCursorTooltipScreen`・`ModeHudModel`・`ModeHudView`・登録ファイル・シナリオに触れたら、反映後のバイナリで Task 11 Step 3・4 の 2 本を再走する（ユニットテストの通過は代替にならない）。続けて:
```bash
git add -A moorestech_client/Assets/Scripts "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" docs/uitk .agents/skills/unity-playmode-recorded-playtest/scenarios
git commit -m "fix(uitk): S2-mode-tooltip の moores-code-review 反映

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
（自動適用が無ければコミットしない）

---

### Task 13: feature/uitk への PR と取り込み（必須）

**Files:**
- Modify: `docs/uitk/disposition.md`（Task 9 の 2 行の PR 列）

**Interfaces:**
- Consumes: Task 12 後の HEAD
- Produces: `origin/feature/uitk` のマージコミット。以後 S2-tutorial（錨 `placement.hud`・`delete.hud` の要素）と S2-ladder（束 2 の突き合わせ）が使える

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`）。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ。報告ページは `~/hermes-agent/data/services/pr-review/publish/README.md` の手順で、manifest は次の形（`file` は絶対パス。並べ比べは Web を先に同じ `group` へ置く）:
```json
{
  "head": "<HEAD 10 桁>",
  "summary": [
    "操作モード HUD（配置 HUD・削除箱の高さ・削除モードの上下警告帯）を GamePanel(craft)・HudSectionHeading と Painter2D の斜線帯で UITK に移した",
    "CursorTooltip を TooltipFace で UITK に移し、Web の tooltipPosition.ts（OFFSET 12・端で押し戻す）を実画面 px で写した。ロック中は画面中央に置く",
    "Model は PlaceSystemStateController・PlacementHeightOffset・UIStateControl・MouseCursorTooltipState を直に購読する",
    "録画 misc/cursor-tooltip-follows-crosshair に GameUi の分岐を足し、全体スイッチ UITK で通した"
  ],
  "media": [
    {"file": "<cursor-tooltip-follows-crosshair の recording.mp4>", "caption": "misc/cursor-tooltip-follows-crosshair（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<uitk-mode-hud-and-tooltip の recording.mp4>", "caption": "uitk/stage2/uitk-mode-hud-and-tooltip（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<web-a9d5123537/2-placement.png>", "caption": "Web 2-placement", "group": "2-placement"},
    {"file": "<compare/S2-mode-tooltip/uitk-2-placement-1280x720.png>", "caption": "UITK 2-placement 1280×720", "group": "2-placement"},
    {"file": "<web-…-2560x1080/2-placement.png>", "caption": "Web 2-placement 2560×1080", "group": "2-placement"},
    {"file": "<compare/S2-mode-tooltip/uitk-2-placement-2560x1080.png>", "caption": "UITK 2-placement 2560×1080", "group": "2-placement"}
  ],
  "verification": [
    "Unityコンパイル: エラー0",
    "L1: State.ModeHud 12・State.CursorTooltip 6・Screens.ModeHud 19（`ModeHudPickingTest` 3 を含む）・Screens.CursorTooltip 9 件 PASS、L2（Conventions・Theme・Anchor）PASS",
    "test-name-parity 束 2: このレーンの 7 ファイル（`alwaysOnHudWheel.test.ts` の持ち分 3 行を含む）の未対応 0（処遇 捨てる 2）",
    "unityプレイ録画テスト: cursor-tooltip-follows-crosshair PASS・uitk-mode-hud-and-tooltip PASS（全体スイッチ GameUi）"
  ],
  "notes": ["並べ比べの差の表は moorestech_logs/harness/uitk/S2-mode-tooltip/done.md"]
}
```
`media` の並べ比べは 5 状態（`2-placement`・`2-placement-connect-tool`・`2-delete-bar`・`2-tooltip`・`2-tooltip-multiline`）について 2-placement と同じ 4 枚の組を並べる。`python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <PR番号> <manifest.json>` が失敗したら、報告 URL 無しで完了にしない。PR 番号が決まったら `docs/uitk/disposition.md` の Task 9 の 2 行の PR 列を `#<番号>` にしてコミット・push する

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
Expected: 進んでいなければ `Already up to date.`。共有区画（`GameUiTheme.tss` の `Screens` 区画・`MainGameUiRegistration.cs` の画面登録区画・`docs/uitk/TODO.md` の 3 節の末尾・`docs/uitk/disposition.md` の処遇の末尾）の衝突は両方の行を採る（並走の S2-hud・S2-tutorial・S2-notification も同じ区画に行を足す）。採ったら compile とテストを回し、衝突が `UiLayer`・`GameUiRoot`・Tooltip/Chrome 層の載せ方・入力の裁き（判定経路）に触れていれば Task 11 の 2 本を再走する。`ErrorCount: 0`・PASS。`gh pr checks` で `UITK Web follow gate (G1)` と `UITK Web follow gate self-test` が `pass`（終わっていなければ前景で `gh pr checks <PR番号> --watch`）、G1 の出力で未記帳 0。CI の `BugReportSubmitKindTest.空白本文でも種別を先に解く` の失敗は既存（moorestech-albof.22）で、マージの条件は G1 緑

取り込みの後、1 つを見る（当たれば同じ PR へコミットして push し、`$FILTER` を回し直す）:
- 錨の未移植の表: Task 6 Step 6 の `test -f … && grep …` を流し直し、`AnchorIds.PlacementHud`・`DeleteHud` の行があれば消して `TutorialAnchorCoverageTest` を回す（S2-tutorial がこのレーンより後に入った場合）

- [ ] **Step 3: マージコミットで取り込む**

Run: `gh pr merge <PR番号> --merge`
Expected: マージされ、`git fetch origin && git log -1 --format='%H %P' origin/feature/uitk` の親が 2 つ（Squash しない）

- [ ] **Step 4: bd を閉じ、報告を締める**

Run:
```bash
bd close <bd> --reason "PR #<番号> を feature/uitk へマージ（操作モード HUD・削除の警告帯・CursorTooltip）"
git status --short
git log origin/uitk-lane/s2-mode-tooltip..HEAD
```
Expected: closed、`git status --short` と `git log` が空。`done.md` の 1 行目を「状態: 完了」にし、「## 証拠」に PR 番号・マージコミット・G1 のジョブ URL・各テストと録画の結果・報告ページの URL を足す。worktree の片付け（`moores-wt rm`）は指揮が行う

---

## Self-Review

1. **Requirements coverage**: R-1 → Task 3、R-2 → Task 6（`ModeHudViewTest`・`ModeHudWideViewportTest`）、R-3 → Task 6（USS とテストの寸法）、R-4 → Task 5・6、R-5 → Task 6・7（拾わないテスト）と Task 8 Step 4（`Pick`）、R-6 → Task 4、R-7 → Task 7、R-8 → Task 2・7、R-9 → Task 9、R-10 → Task 10、R-11 → Task 6 Step 4・Task 7 Step 3・Task 8、R-12 → Task 11、R-13 → Task 12・13。完了条件の各句: 部品 3 つ（Global Constraints の Consumes・Task 6・7）、斜線帯は画面内 Painter2D（Task 5）、`OFFSET` 参照（Task 2 の定数と Web 照合テスト）、端で押し戻す（Task 2 の clamp。D-3）、ロック中は画面中央（Task 7・11）、modeHud 17（P6・I6・S5 全件）、`shared/tooltip/*`(8)（6 写す・2 捨てる）、録画（Task 11）。漏れなし
2. **Placeholder scan**: `<bd>`・`<番号>`・`<HEAD 10 桁>`・`<sha>`・`<空きポート>`・`<参照の x>`・manifest の `<… のパス>`・台帳の実測の値は実行時にしか決まらず、取り方（`bd create` の出力・`gh pr create`・`git rev-parse`・`Read` で参照を見る・Task 8/11 の実測）を同じタスクに書いた。「TBD」「適切に」は無い。`DegreeCustomStyle` の読み方だけ実測で決まる分岐で、Task 5 Step 5 に両方の手順と止める条件を書いた
3. **Type consistency**: `ModeHudViewModel`（抽象）・`ModeHudHidden`・`ModeHudPlacement(ModeHudText target, int height)`・`ModeHudDelete(int height)`・`ModeHudText.None`・`.Of`・`.HasValue`・`.Value`・`ModeHudViewModel.Hidden`・`ModeHudViewModelFactory.Create(UIStateEnum, IPlacementTarget, int)`・`ModeHudModel.Current`、`CursorTooltipViewModel(IReadOnlyList<TooltipLine>)`・`.Lines`・`.Visible`・`.Hidden`・`CursorTooltipModel.Current`、`CursorTooltipPlacement.Offset`・`Clamp`・`PlaceInPanel`、`ICursorTooltipScreen.ResolveAnchorInPanel(IPanel, Vector2)`・`CurrentPointerInPanel(IPanel)`・`ScreenPixelsPerPanelPixel(IPanel)`、`ModeHudView()`・`.Render(ModeHudViewModel)`、`CursorTooltipView(ICursorTooltipScreen)`・`.Render(CursorTooltipViewModel)`・`.SetPointer(Vector2)`、`PlacementHeightText.Create(int)`、`DeleteModeWarningStripeGeometry.FirstColorPolygons(Vector2, float, float)`、`DegreeCustomStyle.TryReadDegrees`、`DeleteModeWarningBand(string, string)`、`PlacementModeHudPanel.Show(ModeHudPlacement)`・`DeleteBoxHeightHud.SetHeight(int)` は定義と使用（テスト・View・Presenter・登録・録画・EDC）で同じ綴り。USSのクラス `mode-hud`・`mode-hud__corner`・`__detail`・`__detail--following`・`__detail--warning`・`__warning`・`__warning-band`・`__warning-band--top/--bottom`・`cursor-tooltip`・`cursor-tooltip__face`、つまみ `--mode-hud-band-yellow`・`--mode-hud-band-stripe-size`・`--mode-hud-band-stripe-angle` は C#・USS・テストで一致。testid は Web の `data-testid` と同名
4. **保留・縮退経路**: (a) `ModeHudViewModelFactory` の対象 null → `ModeHudText.None`（型で無いことを表す。`PlacementModeHudPanel.Show` が `silent-ok` 付きで空の名前を出す＝Web と同じ表示、次の `OnTargetChanged` で解消）、`DisplayName` が空 → `Debug.LogError("[ModeHud] empty DisplayName …")` を出してそのまま出す（D-25）。(a2) `DeleteModeWarningBand` の刻み ≤0 → 理由付きの LogError（D-25）。(b) `DeleteModeWarningBand.DrawStripes` の解決前・寸法 0 → 描かない（読めないつまみは `UssCustomStyleReader`・`DegreeCustomStyle` が LogError）。(c) `CursorTooltipView.Reposition` の非表示・寸法未定 → 置かない（`silent-ok` 付き。次の `GeometryChangedEvent` で置き直し、L1 の位置の主張が外れを見る）。(d) 置けない理由の行は写さない（C# に書き手が無い。D-7）。(d2) `ModeHudView` は出さない組を木から外し、文の無い `LocalizedLabel` を載せない（LogError の経路を作らない。D-28）。(e) Task 1・5・8・10・11 の「止める」経路は、解消の主体（指揮・integrator）と記録先（`done.md` の「integrator への依頼」「要裁定」）を書いた
5. **「決定的にする」を根拠にした選択**: D-4（ロック中は画面中央）は「Web ではワープの副作用で起きる中央寄せを、UITK では状態を読んで明示する」選択で、根拠は決定性ではなく「完了条件の明記」と「UITK のパネルにワープの移動が届く保証が無い」こと。ほかに無し
6. **呼び出し側への責務漏出**: 表示名の解決は `IPlacementTarget.DisplayName`（C# の正）が持ち、画面と Model は種別で分けない（D-8）。位置の計算と実画面 px の換算は `CursorTooltipPlacement.PlaceInPanel` の 1 か所、ロック中の画面中央・倍率・ポインタの実画面への問い合わせは `ICursorTooltipScreen` の 1 か所で、View は中央を計算しない（D-2・D-4）。部品の見た目はつまみで変え、画面 USS は部品のクラスを書かない。拾わない掃き出しは `ModeHudPickingIgnore` の 1 か所（D-11）。判定を返す新設 API は無い

---

## 判断記録（ADR）

従う裁定・設計: Global Constraints の正本、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1.1・§2.2・§2.6・§2.7・§2.8・§3.2〜3.4・§4・§5.1、[A-41]）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 S2-mode-tooltip 行・§4.3・§5・§6.2・E-1・E-13）。

| ID | 判断 | 棄却した案と理由 | 出所 |
|---|---|---|---|
| D-1 | 操作モード HUD は `UiLayer.Chrome`・`UiFamily.Viewport` に置き、出すときは層の中で `BringToFront` する | `OverlayPanel`（Web の `--z-stage-overlay-panel` 30）: Web の HUD は z 31 の `.viewportOverlay` の中の z 30 で、外から見た重なりは 31（Chrome）。`Stage` 族: Web の `.viewportOverlay` は実画面いっぱいで、帯は実画面の上下端に付く | agent 既定（Web の App.tsx・tokens.css） |
| D-2 | CursorTooltip は `UiLayer.Tooltip`・`UiFamily.Viewport` に置き、面の寸法はパネルの倍率で拡大させる。位置は Web と同じ実画面 px で計算し、`PlaceInPanel` がパネル単位⇔実画面 px を `panel.scaledPixelsPerPoint`（設計書 [A-41]）で換算する。ポインタの画面→パネルは `RuntimePanelUtils.ScreenToPanel`（y 反転込み）。C3-b の初回実測で `scaledPixelsPerPoint` と PanelSettings の倍率が食い違ったら、C3-b が決めた代わりの式に従う（S2-notification と同じ但し書き。D-31） | 倍率を打ち消す（`UiScreenPixelScope`）: Web の CursorTooltip は Portal だが `transform: scale(var(--ui-scale))` を掛けるので倍率を保つ（[A-41] が打ち消すのは倍率を掛けない Portal だけ）。`Screen.width / Viewport の幅`: [A-41] が倍率の正を `scaledPixelsPerPoint` に定めており、幅の比は同じ値の再導出になる | 出所: 設計検査（fresh-context）→ agent 既定（Web の `style.module.css`・`CursorTooltip.tsx`、設計書 [A-41]） |
| D-3 | 端では Web の `tooltipPosition.ts` と同じく押し戻す（`max(12, min(p+12, v-w-12))`）。反転（ポインタの左上へ回す）はしない | 反転（旧記述「端で反転」）: 実行計画の現行の完了条件が「端で押し戻す」に直っており、反転にすると Web の `tooltipPosition.test.ts` の期待値（608,508）と食い違う | 実行計画 §1.4（Web の実物） |
| D-4 | カーソルロック中はポインタではなく画面中央を指したものとして置く。ロックの判定と中央の取得は `ICursorTooltipScreen.ResolveAnchorInPanel` が持ち、本番は `Cursor.lockState` を読んで `Client.Input.ScreenCenter.GetPosition()` をパネル座標にして返す。View はロックも中央も知らない。今のポインタの取り直し（`CurrentPointerInPanel`）は View が載ったときだけ | Web と同じくワープで届くポインタ移動に任せる: UITK のパネルにワープの移動が `PointerMoveEvent` として届く保証が無く、完了条件が「ロック中は画面中央」と明記している。View が `viewport/2` で中央を出す: `ScreenCenter` と二重定義になる。非表示から出るたびに取り直す: 非表示中の移動も `SetPointer` で覚えるので、載ったときの 1 回で Web の bd moorestech-7179.3 の穴は塞がる | 出所: 設計検査（fresh-context）→ agent 既定（完了条件・`Client.Input/ScreenCenter.cs`・`UiPointerHitTest.cs` の前例） |
| D-5 | ポインタの移動はパネルの最上位（`panel.visualTree`）で `PointerMoveEvent` を TrickleDown で受ける。何も拾わない所でも届くかは View を書く前に Task 7 Step 0 の EDC で実測し、届かなければ止めて指揮へ報告する（Client.Input に購読口を依頼） | 毎フレーム `HybridInput.GetMousePosition()` を読む: 設計原則「`Update()` で毎フレームの同値判定をしない」に反する。Client.Ui から InputSystem を直に購読: `Client.Ui.asmdef` は InputSystem を参照せず、入力の裁きは Client.Input が持つ。実測を録画（Task 11）まで待つ: 届かなかったとき View と録画を作り直すことになる | agent 既定（AGENTS.md 設計原則）。実測の前倒しは 出所: 第 2 回修正（独立評価〈Codex〉・user-simulator・fresh-context 再検査・再現役で実物確認）→ agent 既定 |
| D-6 | Model は `UIStateControl` の状態を直に読む | `UiRoutingState` を購読: `PlaceBlock`・`DeleteBar` は `UiScreen.None` に写るので、操作モードを区別できない | agent 既定（`Client.Ui.State/Routing/UiRoutingState.cs`） |
| D-7 | 置けない理由（`unavailableReason`）は写さない。ViewModel の欄・警告の行・`.mode-hud__detail--warning` を持たず、Web の P6 1 件目の警告色の主張の部分と S 5 件目は写さない（S 5 件目は disposition に `捨てる`）。参照 `2-placement-unavailable` は並べ比べの差の表に `捨てる`（disposition の表は Web のテスト名の行なので、撮影 id は差の表で持つ） | 欄を持ち Model が常に None を流す: C# に書き手が無く Web の橋も常に ""（`PlacementModeTopic.cs:42-46`）で、実ゲームで警告の行は出ない。Web の mock だけが流す値のために型と行を持つのは「将来の拡張性は考慮不要」（AGENTS.md）に反する | 出所: user-simulator 予測→再現役が確認→agent 既定 |
| D-8 | 対象の表示名は `IPlacementTarget.DisplayName` を使い、Web の Guid 辞書の再導出（種別ごとの分岐）を C# に作らない。Web I6・P6 の表示名のテストは DisplayName の解決で写す | Web と同じく種別で辞書キーを引く: `IPlacementTarget` のコメントが「C# の消費側は種別で再導出しない」と定めている | agent 既定（`IPlacementTarget.cs`） |
| D-9 | 字間は I-fix1（PR #1606・e26010aa0c。設計書 A-70〜A-74）で換算済みのトークンをそのまま使う。HUD の題は画面ルートで `--hud-label-letter-spacing: var(--operation-hud-letter-spacing)`、削除箱の高さは `.key-hint-text`（`--key-hint-letter-spacing`）に任せ、換算値を直書きしない | 画面ルートに換算値（8.333px・5.5px）を直書きする: I-fix1 がマージ済みなので二重補正になる | 出所: 第 5 回修正（評価役 2 体の 2 巡目・実物確認）→ agent 既定 |
| D-10 | 斜線帯は `Screens/ModeHud/Parts/` に置き、Painter2D で黄の帯を矩形で切った凸多角形として塗る（黒は背景）。角度は画面内の `DegreeCustomStyle` で読む | 部品にする: 設計書 §2.7 が「画面内」と定め、使う画面は 1 つ。頂点カラーの格子: 斜めの境目が格子で段になる。角度をトークンの数値で別に持つ: トークンを足すのは画面レーンの持ち物でない | agent 既定（設計書 §2.7） |
| D-11 | Web の `pointer-events: none` は、組み終えた部分木を全部 `PickingMode.Ignore` にする画面内の `ModeHudPickingIgnore` で写す | 部品の pickingMode を変える: 部品（`GamePanel`）の既定は他画面で拾う前提。USS で切る: UITK に picking の USS プロパティが無い | agent 既定（2 機能目で Foundation 昇格を依頼。設計書 §5.1-2） |
| D-12 | Web `modeHudDesign.test.ts` の 5 件のうち 4 件を写す。構成（部品の型・帯の数）を L1 で、色と寸法のトークン経由を画面 USS とトークン USS の本文で主張する。5 件目（警告色を他 consumer と共有）は `捨てる` | 5 件目を写す: この画面は警告の行を持たず（D-7）、他 consumer（通知・列車の網・不足の強調）は別画面のレーンが写す | agent 既定（D-7 に従って改訂） |
| D-13 | 引数の欠けは補間（`LocalizationTextInterpolator`）が解くたびに LogError を出す（Web は 1 回だけ warn）。引数 0 件の `UiText` は `Localize.Get` だけを通り補間に入らない（`UiText.cs:84`）ので、テストは別名の引数を 1 つ持つ行で補間に入れて Error と枠のまま残る文を主張し、続けて count=0 の「Wire x0」を主張する。本番で引数 0 件の行が `{count}` を残したまま無言で出る経路は Foundation の挙動なので、`done.md` の「integrator への依頼」に「引数 0 件の UiText も補間に通し、欠けた枠を LogError にしてほしい（UiText.cs:84）」と書く | 引数 0 件のまま LogError を期待する: 実物は補間に入らず落ちる。disposition で捨てる: 別名の引数で補間に入れれば Web の主張を写せる。回数を 1 回にする・UiText を直す: Foundation の持ち物 | 出所: 第 5 回修正（評価役 2 体の 2 巡目・実物確認）→ agent 既定 |
| D-14 | 並べ比べは theme レーンの前例どおり、`GameUiPanelSettings.asset` を `Object.Instantiate` した複製に `targetTexture`（1280×720・2560×1080）を付けた UIDocument へ載せて RT を `ReadPixels` で撮る。操作モード HUD は Web と同じ固定値の ViewModel を `Render` し、ツールチップは `TooltipFace` を参照の面の位置へ置いて面の見た目だけを比べる。言語は Web の参照に揃える | `PlayModeWindow.SetCustomRenderingResolution` で Game ビューを変える: 3 計画で撮り方が割れ、theme レーンの実撮影の前例から外れる。実ゲームで同じ状態を作る: 参照は mock-host の固定値で、実ゲームに同じ対象名が無い。ツールチップの位置も撮る: 位置は L1 と録画が持ち、RT のパネルでは実画面のポインタと座標が合わない | 出所: 第 2 回修正（独立評価〈Codex〉・user-simulator・fresh-context 再検査・再現役で実物確認）→ agent 既定（前例 `../moorestech_logs/harness/sdd/feature-uitk-s1-theme/workspace/task-5-brief.md` Step 7）。言語は 出所: 第 3 回修正（opus の独立評価・評価役が実物確認）→ agent 既定 |
| D-15 | 完了条件の録画 `misc/cursor-tooltip-follows-crosshair` に GameUi の分岐（ロック中は隅へ注入しても中央に居続ける）を足し、このレーンが持つ。操作モード HUD とポインタ追従は新しい録画 `uitk/stage2/uitk-mode-hud-and-tooltip` で通す | 既存の録画をそのまま回す: Web 専用の前提（隅への追従を DOM で確かめる）が GameUi では D-4 により成り立たず落ちる。操作モード HUD の録画を作らない: Model の配線（源 4 本の購読）は L1 で見ていない（D-19） | agent 既定（moores-writing-plans の規定） |
| D-16 | L3（EditModeInPlayingTest）は持たない | 持つ: 段の締め S2-ladder が束 2 の L3 を持つ（実行計画）。このレーンのランタイムの確認は録画 2 本で行う | 実行計画 §6.2 |
| D-17 | commonHud.spec.ts は 1・3・4・5 件目に `<web-test>` を付け、3 件目は tooltip の部分だけを写す。2・6 件目と 3 件目の採掘進捗・クロスヘアは S2-hud | 3 件目をどちらか 1 レーンに寄せる: 1 件が 2 レーンの画面を跨いでいて、片方だけでは主張が欠ける。同名の付与は複数可（実行計画 E-13） | agent 既定（notes で S2-hud と共有） |
| D-18 | 並べ比べの原本は worktree の `moorestech_client/.uloop/outputs/compare/S2-mode-tooltip/`（gitignore 下）に置き、報告ページに載せる。2560×1080 の Web 参照が無ければ master の worktree で撮る（`shotRunner.ts` の書き換えはコミットしない） | `moorestech_logs` に置く: AGENTS.md がスクショ 3 枚程度までに絞っている。2560×1080 を撮らない: 実行計画 §6.2 が 2 解像度を求める | agent 既定（compare-procedure） |
| D-19 | Model は自分の GameUi 登録ファイルで登録する。`ModeHudModel` の配線は L1 を持たず録画で見る | 共有の `MainGameUiStateRegistration` に足す: 共有区画（E-1）でなく、Web の系は Model を使わない。Model の L1 を持つ: `UIStateControl` は MonoBehaviour で `Construct` に DI の依存が要り、L1 に立てる偽物を作るとプロダクションに試験用の口が要る。組み立ての純関数（Factory）は L1 で見ている | agent 既定 |
| D-20 | `localization.csv` は足さない | 足す: 使う文言（`ui.modeHud.*`・`ui.tooltip.*`）は全部既存 | agent 既定（Task 1 Step 2 で確かめる） |
| D-21 | View は Model を購読せず `public void Render(XxxViewModel)` を出し、Presenter の `Initialize` が `_model.Current.Subscribe(view.Render)` で繋ぐ。テストは `Render` を直に呼ぶ | View が `IReadOnlyReactiveProperty` を受けて購読する: 設計書 §4.2・[A-8] の Presenter が購読・View が描く分担に反し、テストに偽 Model が要る | 出所: 設計検査（fresh-context）→ agent 既定（前例 `ToastLayerPresenter.cs:31-33`・`PauseMenuScreenPresenter.cs:56`） |
| D-22 | `ModeHudViewModel` は抽象基底＋sealed の `ModeHudHidden`・`ModeHudPlacement`・`ModeHudDelete` の判別 union にし、無い文字列は `ModeHudText.None` で型で表す | 種別 enum＋全フィールド: 削除で表示名を持つ・空文字で「無い」を表すなど、種別と合わない値を型が拒めない | 出所: 設計検査（fresh-context）→ agent 既定（前例 `BuildMenuViewModel`） |
| D-23 | `CursorTooltipPlacement` は `Client.Ui.State/CursorTooltip` に置く | `Screens/CursorTooltip` に置く: UITK に依存しない純関数で L0 で試せ、State に置けば Screens・テストの両方から使える | 出所: 設計検査（fresh-context）→ agent 既定（前例 shared/itemMove → State） |
| D-24 | `ModeHudPickingIgnore` は GamePanel の picking を上書きし、Web の `pointer-events: none` を写す唯一の書き手とする | 部品ごとに pickingMode を書く: 書き手が散り、1 つ漏れると HUD の下の入力が奪われる | 出所: 設計検査（fresh-context）→ agent 既定 |
| D-25 | 高さの `UiText` は `PlacementHeightText.Create(int)` の 1 か所で組む。`DisplayName` が空なら LogError を出してそのまま出す。斜線の刻み ≤0 は理由付きの LogError。`TryGetPointerInPanel` は常に真なので `Try` を外し `CurrentPointerInPanel` にする。`DegreeCustomStyle` は LogError の文で失敗を区別できるので bool のまま | 配置 HUD と削除箱で高さの文を別々に組む: 書式の二重定義。空の名前を黙って出す・刻み 0 で黙って描かない: 開発者に届く経路が無い | 出所: 設計検査（fresh-context）→ agent 既定 |
| D-26 | 死活表（Phase 2.5）の結果、GameUi で死ぬのは錨 `placement.hud`・`delete.hud` のチュートリアル強調だけ（S2-tutorial のマージまで）。World Target のツールチップは実ゲームに書き手が無い | このレーンで強調を描く: `Screens/Tutorial/**` は S2-tutorial の持ち物。notes で指揮へ返す | 出所: 設計検査（fresh-context）→ agent 既定 |
| D-27 | Web `CursorTooltip.test.ts` の「shows a loud marker for an unknown localized key」は写す。綴り違いのキーで `[!key]` の印が出ることを L1 で主張し、Web の warn 1 回は写さない（画面の印そのものが開発者の気づく経路） | `捨てる`（未知のキーを渡す経路が無い）: 誤り。`LocalizationKey` は public ctor で、`GameObjectTooltipTarget.cs:24` が Inspector の文字列から作る | 出所: 第 3 回修正（opus の独立評価・評価役が実物確認）→ agent 既定 |
| D-28 | `ModeHudView` は出さない組（配置 HUD・削除の帯・削除箱の高さ）を `display` で隠さず木から外し、出すときは文を渡してから載せる。起動直後の非表示でエラーが 0 件であることを L1 で主張する | ctor で全部載せて `display: none`: 文の無い `LocalizedLabel` が載ると 1 フレーム後に `LocalizedTextBinding` が LogError を出し（`LocalizedTextBinding.cs:44-58`）、起動時に 4 件のエラーで R-11 と L1 が赤になる。ラベルを文を得るときに作る: 載せ外しの方が部品の組み立てを 1 か所に保てる | 出所: 第 3 回修正（opus の独立評価・評価役が実物確認）→ agent 既定（前例 `GamePanel.cs:96`「キー無しのラベルを載せない」） |
| D-29 | 文の完全一致は `LocalizedLabel` が付ける `<line-height=…>` 込みで書く（ModeHud は `<line-height=25px>`、CursorTooltip は `TooltipFaceTest.LineHeightTag`）。つまみ `--localized-label-line-height` を外してテストを通すのは禁止 | タグを外した文で比べる・つまみを消す: 行送り 25px・27.9px の写しが消える | 出所: 第 3 回修正（opus の独立評価・評価役が実物確認）→ agent 既定（前例 `TooltipFaceTest.cs:24,56`） |
| D-30 | `TutorialAnchorCoveragePending.cs` に `placement.hud`・`delete.hud` の行があれば、`Mark` を付ける Task 6 と最終の取り込み（Task 13 Step 2）の両方で同じ PR で消し、`TutorialAnchorCoverageTest` を回す。テストのフィルタに `Client.Tests.Ui.Anchor` を足す | 消さない: S2-tutorial が先に入っていると、印済みなのに未移植の表に残り `TutorialAnchorCoverageTest` が赤になる | 出所: 第 2 回修正（独立評価〈Codex〉・user-simulator・fresh-context 再検査・再現役で実物確認）→ agent 既定（実行計画 E-1 が書き込みを許す）。フィルタは 出所: 第 3 回修正（opus の独立評価・評価役が実物確認）→ agent 既定 |
| D-31 | 倍率の読み口 `ICursorTooltipScreen.ScreenPixelsPerPanelPixel` は S2-notification と同じ但し書き（C3-b の実測で `scaledPixelsPerPoint` と PanelSettings の倍率が食い違えば C3-b の式に従う）で残し、1 本化は本PR外へ出す | このレーンで C3-b の `UiScreenPixelScope` へ寄せる: C3-b が未マージで、読み口の正は C3-b の持ち物 | 出所: Phase 2.6（強）→ agent 既定 |

| D-32 | 一部だけ写す Web テストの捨てた部分: P5（`削除モードをuGUI準拠の上下警告帯で表示する`）の `role="status"`・`aria-label="Delete Mode"` と S2（`削除表示を説明パネルではなく上下の警告帯で構成する`）の `role="status"` は写さない。P6 1 件目の警告色の主張は D-7。C2 後半の count=0「Wire x0」と C4 の名前付き差し込み（`placeMaterialShortage`）は写す | ARIA を写す: UITK のランタイムパネルに role・aria-label の仕組みが無く、読み上げの経路も無い（DOM 固有）。C2・C4 の残りを捨てる: 同じ `TooltipLine` の引数で L1 に写せる | 出所: 第 5 回修正（評価役 2 体の 2 巡目・実物確認）→ agent 既定 |
| D-33 | D-5 を差し替え: 何も拾わない所では `PointerMoveEvent` が `panel.visualTree` に届かない（Task 7 Step 0 実測 received=none、拾う要素上なら届く）ので、Client.Input に InputSystem.onEvent 駆動のポインタ位置の購読口（`InputSystemPointerPositionStream`。位置が変わったときだけ流す）を足し、`ICursorTooltipScreen` 経由で View が購読する。計画の「止めて指揮へ」は採らない | 止めて integrator へ依頼: 指揮方針「止まるのは解決不能な障害だけ」に反する。毎フレーム `HybridInput` を読む: Update の同値判定禁止に反する | agent 既定（実測＋指揮方針） |
| D-34 | `InputSystemPointerPositionStream` の DI 登録は `CursorTooltipGameUiRegistration` に置く（Client.Input の既存サービスは static で DI 前例が無く、利用者は今 CursorTooltip だけ）。2 機能目（C3-b HoverTooltip）で共有の登録へ移す | 共有サービス並びへ今移す: 共有区画外 | agent 既定 |
| D-35 | 最終レビュー D1 案A: 保存ポインタを実画面 px にして倍率変化後も換算し直し、カーソルロックの変化は `InputManager.OnCursorLockChanged`（唯一の書き手 `MouseCursorVisible` が変化時だけ流す）で受けて置き直す。ロック分岐と上下反転は純関数 `CursorTooltipScreenAnchor` で L0 | 案B（ロックは warp 任せ）: warp が onEvent を通らず消える保証が無い。案C（アンカー列へ集約）: interface 再設計が大きい | agent 既定（moores-code-review の推奨） |
| D-36 | 録画 `uitk-mode-hud-and-tooltip` は `SetupDebugEnvironment`→`SkipOpeningSkitIfPlaying`→`ExitToGameScreen` で始める（`SkipOpeningSkit` だけでは木のチェストが研究ロックで `AssignHotbar` が失敗。前例 right-short-press-cancel と同じ） | 研究を解く手順を足す: 前例と違う | agent 既定（実測） |
| D-37 | 並べ比べの `2-delete-bar` は参照に合わせ `ModeHudDelete(0)`、ツールチップの面は両解像度とも実画面 (14,14)（Web の 2560 参照も実画面 14px）で撮る | 計画どおり高さ 3・倍率を掛けた座標: 参照と文字・位置が揃わない | agent 既定（参照の実物） |
| D-38 | `Client.Ui.State.asmdef` に `Game.PlacementTarget` 参照を足す（Factory のログが `target.Kind` を出すため CS0012） | ログから種別を外す: 診断情報が減る | agent 既定 |
### 未確認

- `CustomStyleProperty<string>` が `117deg` を文字列で返すか（Task 5 Step 5・Task 8 Step 4 で実測し台帳へ）
- 何も拾わない所の `PointerMoveEvent` が GameUi のパネルの最上位で受けられるか（Task 7 Step 0 で先に実測し、届かなければ止める。台帳へ）
- `HudSectionHeading` の字間のつまみを画面ルートで `var(--operation-hud-letter-spacing)` に上書きしたとき、題が 8.3333px の字間になるか（Task 6 Step 5 の L1 と Task 10 の比較）
- `.mode-hud__detail` の `--localized-label-line-height` で 1 行の高さが 25px になるか（Task 6 の L1）
- 複製した PanelSettings の RenderTexture へ `ModeHudView`・`TooltipFace` を載せた撮影が theme レーンと同じく写るか（Task 10 Step 2。写らなければ指揮へ）
- `overflow-wrap: anywhere` の差（長い 1 語の名前）が並べ比べで見えるか（Task 10。見えれば `LX-32`）

## 本PR外のリファクタ提案

- `ModeHudPickingIgnore` と `DegreeCustomStyle` は 2 機能目が出たら Foundation（`Foundation/Input` の「部分木を拾わない」・`UssCustomStyleReader.TryReadDegrees`）へ昇格する（設計書 §5.1-2。integrator の持ち物）
- `LocalizationTextInterpolator` の欠けた引数のエラーを Web と同じく文ごとに 1 回へ絞る（D-13。Foundation/Localization の持ち物）
- A-41 の倍率の読み口を C3-b の Foundation/Stage（`UiScreenPixelScope`）へ 1 本化し、mode-tooltip（`ICursorTooltipScreen.ScreenPixelsPerPanelPixel`）・notification の 2 か所を委譲させる（D-31）
- `UnityCursorTooltipScreen` の画面→パネル座標の換算と `Foundation/Input/GameUiPointerProbe.cs:20-27` の同じ換算を、Foundation の 1 関数にまとめる（Foundation の持ち物）

レーンの起動は指揮が行う（実行計画 §4.1）

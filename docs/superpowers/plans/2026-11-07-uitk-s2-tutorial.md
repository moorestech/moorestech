# UITK S2-tutorial（チュートリアル枠・ラベル・DragGuide・KeyControlHint・ワールドピン）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版のチュートリアルの見せ方（`TutorialOverlay` の枠線・枠線ラベル・`DragGuide`、`KeyControlHintHud`、`WorldPinOverlay`）を、Web と同じ見た目・同じ振る舞いの UITK 画面として `Client.Ui/Screens/Tutorial/` に作り、アンカーの追跡（祖先クリップ ADR 0024・脈動 1200ms・ラベル反転・重複解決の LogError）とアンカーのカバレッジ L1（plan §4.5）まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 状態の源は C# にすでにある 2 つの store（`TutorialPresentationStateStore`・`WorldPinStateStore`）と `UIStateControl`、blocking のスキットは S2-hud の `BlockingSkitPresence.IsBlocking`（Web の `useBlockingSkitActive` と同じ源。D-20）で、Web の topic（`tutorial.presentation`・`tutorial.world_pins`・`uiState`）はこれらを中継しているだけ。UITK 側は `Client.Ui.State/Tutorial/` の Model がこれらを購読して不変の ViewModel にし、`Screens/Tutorial/` の Presenter が層（`UiLayer.Tutorial`・`UiLayer.WorldPin`・`UiLayer.OverlayPanel`）へ View を常駐させ、Model を購読して `View.Render(ViewModel)` を呼ぶ（View は購読しない。設計書 §4.2・[A-8]、前例 `ToastLayerPresenter`）。アンカーは Foundation の `TutorialAnchorRegistry.Resolve` で引き、画面側の `TutorialAnchorTracker` が購読中のアンカーだけを毎フレーム測って、値が変わったときだけ枠を描き直す。Web の計算（clip-path の inset・ラベルの反転と押し戻し・画面端矢印のクランプ）は純ロジックとして `Client.Ui.State/Tutorial/Logic/` に逐語で写し、L0 で確かめる。脈動は UiMotion（`UiTweenAnimation`＋`UiMotionLoop.PingPong`）に、このレーンが Foundation へ足す `ScaleTweenStep` を当てたもので、周期はトークン `--tutorial-pulse-duration` を画面ルートで受け直して読む。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`generateVisualContent`/Painter2D・`CustomStyleResolvedEvent`）、UniRx、VContainer、NUnit＋`com.unity.ui.test-framework`（`UiTestHost`・`ScreenTestHost`・`simulate.FrameUpdateMs`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/src/`、`E/` = `moorestech_web/webui/e2e/`。Files 節と各コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（7 本。とくに「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S2-tutorial 行・§4.3・§5・§6.2・E-1・E-8・E-16）、ADR 0024（祖先クリップ）・ADR 0039（脈動を子へ付ける）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ

## Requirements

設計の出所: 実行計画 §1.4 の S2-tutorial 行（完了条件の逐語「アンカーの枠（祖先クリップ ADR 0024・脈動 1200ms・ラベル反転・二重解決の LogError）、アンカーのカバレッジ L1（plan §4.5）。写す: KeyControlHint・WorldPin（P6・C4）、`system/worldPin.spec.ts`(6)、`TutorialOverlay.test.ts`(17)・`tutorialAttentionDesign.test.ts`(21)・`system/tutorial.spec.ts`(6)・`tutorialHighlightClip.spec.ts`(1)（E-8 で段階 5 から前倒し）、`portalLayerScaling.spec.ts` のピン・枠 3 件。DragGuide（`W/features/tutorial/overlay/DragGuide.tsx`。2 つのアンカーを結ぶ `kind: dragGuide` の要素）を `Screens/Tutorial/` に作り、`TutorialOverlay.test.ts` の drag guides を写す（E-16）」）、設計書 §4（画面の型）・§2（部品カタログ）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 枠線（outline）: アンカーの矩形 ± `PaddingPx` の箱に、3px の原色赤の線（角丸 8px）と外側 `--tutorial-highlight-glow`（4px）の `--tutorial-attention-glow` のグローを描く。全画面を暗くしない。拾わない | L1 `TutorialStageCueTest`（tutorial.spec 6 件のうち枠 3 件）が緑 |
| R-2 | 祖先クリップ（ADR 0024）: アンカーの祖先のうち切る要素（`ScrollView` の viewport・`TutorialAnchor.ClipContainerClassName` の印・インラインの `overflow: hidden`）の矩形を交差させ、Web の `clipPathInset` と同じ式で枠を切る。素の矩形が交わらない・切った後に幅か高さが 0 以下なら要素ごと描かない | L0 `TutorialHighlightClipTest`、L1 `TutorialOverlayClipTest`（3 件）・`TutorialHighlightAncestorClipTest`（1 件）が緑 |
| R-3 | 脈動: 枠は 1.03（subtle）、KeyControlHint の行とワールドピンの画面端矢印の矢印本体は 1.08（strong）。周期は `--tutorial-pulse-duration`（1200ms）、ease-in-out 相当（`UiEasing.EaseInOutSine`）で往復。ラベル面・ピン本体は脈動しない | L1 `TutorialStageCueTest` の脈動 2 件と、`TutorialAttentionDesign*Test` が緑 |
| R-4 | 枠線ラベル: `LabelTutorialGuid` がある枠だけ、`challengeTutorial.<guid>.text` の文を面 `--world-pin-face`・padding `--label-face-padding`・字 `--label-face-font-size` で出す。既定は枠の下（隙間 `--tutorial-highlight-label-gap`）、下に収まらず上に収まるときだけ上へ反転、横は clip の右端で押し戻し、clip より広ければ clip 幅で折り返す。アンカー自身がクリップで欠けているときと文が空のときは出さない | L1 `TutorialOverlayLabelTest`（4 件）と tutorial.spec のラベル 1 件・portalLayerScaling のラベル 1 件が緑 |
| R-5 | アンカー解決: 購読中のアンカーだけを毎フレーム測り、状態（ready・not-found・hidden と理由）か矩形か clip が変わったときだけ描き直す。重複は `Debug.LogError`、未発見は `Debug.LogWarning` を状態が変わった時に 1 回だけ出す（毎フレーム出さない）。購読から外れたアンカーの状態は捨てる。アンカーが消えたら同じフレームで枠も消す | L0 `TutorialAnchorStateBookTest`、L1 `TutorialAnchorTrackerTest`・`TutorialOverlayAnchorTest`（1 件）・tutorial.spec の「枠はアンカーより長生きしない」が緑 |
| R-6 | ack は写さない（D-10）: Web の `tutorial.anchor_ack` の C# の受け手 `TutorialAnchorAckAction` は検証して返すだけで、store・サーバー・ログのどこにも効果が無い。UITK では解決する側が C# 自身なので、未発見・重複は `TutorialAnchorTracker` の遷移ログ（R-5）で開発者へ届く。Web の ack の 2 件は disposition で「捨てる」 | `docs/uitk/disposition.md` の 2 行と test-name-parity の未対応 0 |
| R-7 | DragGuide: `kind: dragGuide` の要素について、両端が ready で、起点の中心が起点の clip の内側にあるときだけ、56px（`--tutorial-drag-guide-size`）のカーソル矢印を起点の中心に置き、3200ms（`--tutorial-drag-guide-duration`）で終点の中心まで動かすループ（0% 透明 → 15% 不透明 → 75% 終点 → 100% 終点で透明）を流す | L1 `TutorialOverlayDragGuideTest`（3 件）が緑 |
| R-8 | KeyControlHint: `kind: keyControl` のうち `UiState` が現在の `UIStateEnum` の名と一致するものだけを、ホットバーの上（`--hotbar-floor-offset` + `--tutorial-key-hint-hotbar-gap`）に中央揃えの列（間 `--tutorial-key-hint-gap`）で出す。各行は C2-c の `ShortcutHintLabel`（`ShortcutHintLayout.Prefix`。kbd は `KeyCap`）で、文字色は `--tutorial-key-hint-color`（kbd は R-19 の `--key-cap-color` の上書き）、kbd と文の間は `--tutorial-key-hint-kbd-gap`、字形は `UiTypographyClass.KeyHintText`。語彙外の `UiState` は 1 回だけ Warning を出して出さない。blocking のスキット中は出さない | L1 `KeyControlHintTest`（4 件）・tutorial.spec の keyControl 2 件が緑 |
| R-9 | ワールドピン: blocking のスキット中かピンが 0 件なら出さない。画面内のピンは `(ScreenX, ScreenY)` に先端（下端中央）を合わせ、ラベル（ラベル面と同じ見た目・折り返さない）と 20×20 のマーカーを縦に積む。画面外は `--world-pin-edge-margin`（40px）だけ内側へクランプした位置に、方向の角度で回した 56px（`--world-pin-arrow-size`）の矢印（塗り原色赤・縁 `--world-pin-face` 1.5・影 `--world-pin-arrow-shadow`）を出し、矢印本体（子）だけを 1.08 で脈動させる | L1 `WorldPinOverlayTest`（6 件）・`TutorialViewportScalingTest`（ピン・矢印 2 件 × 2 解像度）が緑 |
| R-10 | 解像度: 1280×720 と 2560×1080（論理 1706.7×720）で、ピン先端が射影座標に留まり、矢印が余白ごと画面内に収まり、ラベルが枠に付いたまま | L1 `TutorialViewportScalingTest`（`[TestFixture]` 2 つ）が緑 |
| R-11 | 見た目の規則（Web `tutorialAttentionDesign.test.ts` 21 件の意図）: 原色赤・周期・グローはトークンだけが持ち、画面 USS と C# に色・秒の直書きが無い。脈動の振幅（1.08・1.03）は `TutorialAttentionPulse` の 1 か所。`.key-hint-text` の色は白のまま（KeyControlHint の赤は画面ルートの変数の受け直しで、共有の様式を巻き込まない） | L1 `TutorialAttentionTokenTest`(6)・`KeyControlHintDesignTest`(4)・`TutorialRingDesignTest`(5)・`WorldPinArrowDesignTest`(6) が緑 |
| R-12 | アンカーのカバレッジ L1（plan §4.5）: `AnchorIds` の全 id（固定 19・接頭辞 6）が「UITK のどこかで `TutorialAnchor.Mark` に渡っている」か「未移植の表（id → 持ち主レーン）に載っている」のどちらか一方だけ。ポーズの 6 id は実際にポーズを載せて `Unique` に解ける | L1 `TutorialAnchorCoverageTest` が緑 |
| R-13 | 登録: `Client.Starter/Registration/GameUi/TutorialGameUiRegistration.cs` を作り、`MainGameUiRegistration` の画面登録区画に 1 行。USS は `GameUiTheme.tss` の `Screens` 区画へ `@import` 4 行（各 USS を作る Task 6〜9 が 1 行ずつ足す） | `UiThemeImportBundleTest` と既存 `Client.Tests.Ui` が緑。PlayMode（全体スイッチ UITK）で枠・ピン・ヒントが出る |
| R-14 | 並べ比べ: 参照キャプチャ（1280×720）の `2-world-pin-onscreen`・`2-world-pin-offscreen`・`2-key-control-hint`・`5-tutorial-outline-label`・`5-tutorial-recipe-item`・`5-tutorial-research-node` と動画 `2-key-control-hint-pulse`・`2-world-pin-pulse`・`4-drag-guide`・`5-tutorial-pulse` のコマ、2560×1080 の撮影（master の worktree で撮る）と、Web と同じ撮影状態を fixture で作った UITK の撮影（Task 14 の対の表。DragGuide は Web の動画 `4-drag-guide` のコマと fixture の連番を並べる。ポーズ後だけ UITK のみの自己確認）を並べ、差の表を `done.md` に書き、報告ページに載せる | `done.md` の差の表。合否は ref-uitk-design の目安（720p で 2px 以内・トークン同値・装飾の有無・段組） |
| R-15 | Web と同じにできない差は `LX-35`〜`LX-38` で台帳「## 見た目の例外」の末尾へ行を足し、該当行に `/* look-exception: LX-n */` を付ける | `LookExceptionLedgerTest` が緑 |
| R-16 | 写さない Web テスト 2 件（item master 未ロード中の ack・同一 anchor を共有する全 highlight への ack）は `docs/uitk/disposition.md` に「捨てる」と理由を書く。`node tools/uitk/test-name-parity.mjs --bundle 2`・`--bundle 4`・`--bundle 5` でこのレーンの Web テスト名が未対応 0 | parity の出力 |
| R-17 | unityプレイ録画テスト `uitk/stage2/uitk-tutorial-cues` を全体スイッチ UITK で回す | result.json が PASS、recording.mp4 |
| R-19 | 部品の直し（指揮の割り当て A。最初の使い手がこのレーン）: `ShortcutHintLabel.SetHint` は Prefix のとき印の引数を足さず、content・verbatim の文をそのまま受ける。`KeyCap` は色のつまみ `--key-cap-color`（未指定なら `var(--text-high-contrast)`）を持つ。S2-hud の KeyHintHud（Inline）の振る舞いは変えない | L1 `ShortcutHintLabelPrefixTextTest` に Prefix の content/verbatim 1 件、`KeyCapTest` につまみの上書き 1 件。既存の `Client.Tests.Ui.Components.Text.` の L1 と KeyHintHud の L1 が全件緑 |
| R-18 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ付き）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑・未記帳 0 → `gh pr merge --merge` → bd を閉じる | `git log origin/feature/uitk` にこのレーンのマージコミット |

**やらないこと（スコープ境界）**

- 未移植の画面（研究・レシピ・持ち物・ビルドメニュー・チャレンジ・HUD 類）にアンカーの印を付けない。印はその画面を作るレーンが付け、カバレッジの未移植の表から自分の行を消す（D-9）
- Ctrl+U・カットシーン中に全 UI を隠す扱いは S2-skit の持ち物（このレーンは blocking スキットの判定だけを持つ）
- チャレンジ HUD（`challenge.current-hud`）のハイライトの録画と、master の壊れた録画 `scenarios/tutorial-*`（bd moorestech-wwxy）の修理は S2-ladder の持ち物
- Web 側（`moorestech_web/`）は一切変えない。サーバー・`Client.Game` の store・射影（`WorldPinScreenProjection`）は変えない
- `Tokens.uss`・`DerivedTokens.uss`・`MantineTokens.uss`・`UiLayer`・`UiScreenTraits`・`AnchorIds` は書かない。`Components/**` は指揮が割り当てた `ShortcutHintLabel.cs`・`KeyCap.uss` の 2 か所だけ、`Foundation/**` は `Anchor/` の 2 本と新規 `Motion/Steps/ScaleTweenStep.cs` だけを書く（それ以外に要るものは「integrator への依頼」）
- blocking のスキットは S2-hud が作る `Client.Ui.State.SkitPresence.BlockingSkitPresence`（`IReadOnlyReactiveProperty<bool> IsBlocking`。W2a 設計書 A-56・S2-hud 計画 R-8）を DI で受けて使う。このレーンは同じ役割の型も、`Client.Ui.State.asmdef` の `Client.Skit` の行も、その登録も作らない（S2-tutorial は S2-hud に依存。D-20）
- L3（EditModeInPlayingTest）は段の締め S2-ladder が持つので、このレーンでは作らない（D-17）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s2-tutorial --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる
- **前提のマージ**: F1（#1601）・C2-a（#1604）・C2-c（#1605）が `origin/feature/uitk` に入っていること（この計画を書いた時点の `df955f1dc6` に入っている）。C2-c の `ShortcutHintLabel` の Prefix の直しと `KeyCap` の色のつまみは、指揮の割り当てでこのレーンが Task 8 で入れる（D-6・D-8）
- **使う部品と公開 API（実物から写した）**
  - `Client.Ui.Foundation.Anchor`: `TutorialAnchor.MarkerClassName = "tutorial-anchor"`・`TutorialAnchor.Mark(VisualElement element, params string[] ids)`・`TutorialAnchorRegistry.Resolve(VisualElement searchRoot, string anchorId) : TutorialAnchorResolution`・`TutorialAnchorResolution.Missing`／`.Unique(VisualElement element)`（`Element`）／`.Duplicate(int count)`（`Count`）。**宣言して変える**: `Resolve` の Warning を外し（呼ぶ側が遷移時に出す）、`TutorialAnchor.ClipContainerClassName`・`MarkClipContainer(VisualElement)` を足す（Task 2・設計書 A-65）
  - `Client.Ui.Foundation.Motion`: `UiMotionPlayer.Play(VisualElement target, IUiAnimation animation) : UiMotionHandle`・`UiMotionHandle.Cancel()`・`UiTweenAnimation(UiMotionSpec spec, IUiTweenStep step)`・`UiMotionSpec(float durationMilliseconds, float delayMilliseconds, UiEasing easing, UiMotionLoop loop)`・`IUiTweenStep.Apply(float easedProgress)`・`UiEasing { Linear, EaseOutCubic, EaseInOutSine }`・`UiEasingFunctions.Evaluate(UiEasing easing, float t)`・`UiMotionLoop { Once, Repeat, PingPong }`。前例 `OpacityTweenStep(VisualElement target, float from, float to)`。**このレーンが足す**: `Foundation/Motion/Steps/ScaleTweenStep.cs`（新規。既存ファイルは触らない。設計書 §1.2 Motion 行「Step を使い手の段で足す。Scale〈脈動〉は段階 2」・A-63）
  - `Client.Ui.Foundation.Root.GameUiRoot`: `GetRootElement()`・`GetLayer(UiLayer layer, UiFamily family)`。`UiLayer.Tutorial`（`--z-portal-tutorial`）・`UiLayer.WorldPin`（`--z-portal-world-pin`）・`UiLayer.OverlayPanel`（`--z-stage-overlay-panel`）。`UiFamily { Stage, Viewport }`
  - `Client.Ui.Foundation.Theme.UssCustomStyleReader`: `TryReadPixels(ICustomStyle, string, out float)`・`TryReadMilliseconds(…, out float)`・`TryReadColor(…, out Color)`・`TryReadNumber(…, out float)`（読めないと自分で LogError を出す）
  - `Client.Ui.Foundation.Text`: `LocalizedLabel.SetText(UiText)`、`UiTypographyClass.KeyHintText = "key-hint-text"`
  - `Client.Ui.Components.Text`: `KeyCap(string keyText)`・`SetKeyText(string)`（`.key-cap { color: var(--text-high-contrast) }`）、`ShortcutHintLabel(string testid, ShortcutHintLayout layout)`・`SetHint(UiText sentence, string shortcutText)`・`ShortcutHintLayout.Prefix`（間はつまみ `--shortcut-hint-label-gap`。DerivedTokens 55 行目のコメントが KeyControlHint の上書きを想定）。**このレーンが直す**: `SetHint` が Prefix でも `WithLeadingParam` を呼び content の文で例外になる点と、`KeyCap` に色のつまみが無い点（Task 8。指揮の割り当て A）
  - `Client.Ui.State.Text.UiText`: `FromContent(ContentLocalizationKey)`・`FromVerbatim(string)`・`Resolve()`。`ContentLocalizationKeys.ChallengeTutorialText(Guid)`（`Mooresmaster.Localization.Generated`。キー `challengeTutorial.<guid>.text`）
  - 状態の源（`Client.Game.InGame.Tutorial`）: `TutorialPresentationStateStore.Instance.ObserveChanged() : IObservable<TutorialPresentationData>`・`GetCurrent()`・`HasSessionId(string)`、`TutorialPresentationData { int Revision; TutorialSessionData[] Sessions }`・`TutorialSessionData { string TutorialSessionId; string ChallengeId; TutorialOverlayElementData[] Elements }`・`TutorialOverlayElementData { string Kind; string ElementId }`・`TutorialOutlineElementData { KindName="outline"; string AnchorId; int PaddingPx; bool BlocksPointerInput; string LabelTutorialGuid }`・`TutorialDragGuideElementData { KindName="dragGuide"; string FromAnchorId; string ToAnchorId }`・`TutorialKeyControlElementData { KindName="keyControl"; string TutorialGuid; string KeyName; string UiState }`。`WorldPinStateStore.Instance.ObserveChanged() : IObservable<WorldPinPresentationData>`・`GetCurrent()`・`WorldPinPresentationData { int Revision; WorldPinData[] Pins }`・`WorldPinData { string PinId; string TutorialGuid; float ScreenX; float ScreenY; bool OnScreen; float DirectionX; float DirectionY }`（0..1・左上原点・方向は CSS 軸。射影は `MapObjectPin`/`VeinPin` の `Update` で C# が済ませている）。blocking のスキットは S2-hud の `Client.Ui.State.SkitPresence.BlockingSkitPresence.IsBlocking : IReadOnlyReactiveProperty<bool>`（DI 登録は S2-hud の `HudGameUiRegistration`。D-20）。`Client.Game.InGame.UI.UIState.UIStateControl`（DI）: `CurrentState`・`IObservable<UIStateEnum> OnStateChanged`
  - テストの土台: `UiTestHost`（1280×720・本番 tss・SetUp で GameUi・TearDown で Web へ戻す）、`ScreenTestHost.Mount<T>(T)`・`Find<T>(VisualElement, string)`・`IsShown(VisualElement)`、`FakeModel<T>(T initial)`・`Current : IReadOnlyReactiveProperty<T>`・`Set(T)`、`CommandRecorder<T>.Record/Issued/Clear`、`ThemeTokenFile`（`Client.Tests/Ui/Components`）、前例 `PauseMenuSettingsLayoutTest` の `[TestFixture(w, h)]`＋`panelSize` の上書き
- **AGENTS.md の規約**: 1 ファイル 200 行未満・1 ディレクトリ 10 ファイルまで（.meta は数えない）・`partial`/`Func<>` 禁止・イベントは UniRx・try-catch 禁止・デフォルト引数禁止・単純な getter/setter プロパティ禁止（`{ get; private set; }` は可）・主要処理に「// 日本語 → // English」の 2 行コメント（各 1 行）・メソッド内のローカル関数は `#region Internal`・`.meta` は手で作らない・Prefab/シーンを手で書き換えない・初期化は `Initialize`・fail-closed の縮退は理由をログへ
- **USS の規約**（ref-uitk-design・設計書 §3.2）: 1 画面 1〜数 USS（.cs の横）、画面ルートのクラスで始める、部品のクラス（`.key-cap`・`.shortcut-hint-label*`・`.key-hint-text`）を書かない、色は `var()` だけ、`z-index`・構造擬似クラスを使わない、状態は `--<状態>` のクラス、先頭に `/* web: <Web の CSS> */` の出所を日英で書く。C# が値を要るときは画面ルートで `--<画面>-<名>: var(--token)` と受け直して `UssCustomStyleReader` で読む（`:root` の変数は子の customStyle へ来ない）
- **letter-spacing**: このレーンの文字は Web で字間を持たない（`--label-face-*`・`keyHintText` の字間は Typography.uss の持ち物）ので、字間を新たに当てない。生成トークンの字間は I-fix1（取り込み済み）が直した値をそのまま使う
- **暗い半透明トークン**（`--surface-navy`＝`--world-pin-face` の元）は I-fix1（取り込み済み）が α を選び直した値を使う。比べて暗さが違えば差は差として扱う
- **色の比較はランタイムの PanelSettings（PlayMode の実画面）で撮る**（ギャラリーの EditorWindow は sRGB 合成で半透明が淡く出る）
- **テストの置き場と名前**: L0 は `S/Client.Tests/Ui/State/Tutorial/`（namespace `Client.Tests.Ui.State.Tutorial`）、L1 は `S/Client.Tests/Ui/Screens/Tutorial/<束>/`（namespace `Client.Tests.Ui.Screens.Tutorial.<束>`）、カバレッジとアンカーは `S/Client.Tests/Ui/Anchor/`。テスト名は日本語の挙動文。Web から写したものは `/// <web-test file="src/…">it の第 1 引数</web-test>` か `file="e2e/…"`（describe は含めない。E-13）
- **コマンド**: コンパイル `uloop compile --project-path ./moorestech_client`（期待: ErrorCount 0）。テスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。ドメインリロード中のエラーは 45 秒待って再試行。このレーンのテストの総フィルタ（以下 `$FILTER`）は `^Client\.Tests\.Ui\.(State\.Tutorial|Screens\.Tutorial|Anchor)\.`
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有区画は `GameUiTheme.tss` の `Screens` 区画の `@import` 4 行、`MainGameUiRegistration` の画面登録区画の 1 行、台帳 `docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」「## 追従記録」の末尾、`docs/uitk/disposition.md` の行。宣言した Foundation の変更は `S/Client.Ui/Foundation/Anchor/{TutorialAnchor.cs,TutorialAnchorRegistry.cs}` と `S/Client.Tests/Ui/Anchor/TutorialAnchorRegistryTest.cs` だけ。指揮の割り当て（実行計画 E-19）で `S/Client.Ui/Components/Text/ShortcutHintLabel/ShortcutHintLabel.cs`・`S/Client.Ui/Components/Text/KeyCap/KeyCap.uss`・新規 `S/Client.Ui/Foundation/Motion/Steps/ScaleTweenStep.cs` と、それぞれの L1（`S/Client.Tests/Ui/Components/Text/`・`S/Client.Tests/Ui/Motion/`）も書く。`S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` は後続のレーンが行を消す共有の表（規則は実行計画 E-1 に指揮が書く）
- **新しい共有を見つけたら**（設計書 §5.1-2）: 1 機能目なのでこの画面の中（`Screens/Tutorial/<束>/`）に置き、`done.md` の「昇格候補」に名前と理由を書く。2 機能目（ほかのレーンがすでに同じものを持っている）なら自分で作らず、`done.md` の「integrator への依頼」に書いて指揮へ報告し、入るまでそのタスクだけ止める
- **参照キャプチャ**: `../moorestech_logs/uitk/reference-captures/web-a9d5123537/`（repo 相対）（1280×720 のみ）。並べ比べの原本は worktree の gitignore 下 `moorestech_client/PlaytestResults/s2-tutorial-compare/` に置き、`moorestech_logs` へは done.md と 3 枚程度まで。`moorestech_logs` へ書く前に `../moorestech_logs/README.md` を読む
- **既知の失敗**: CI の `BugReportSubmitKindTest.空白本文でも種別を先に解く` は既存失敗（albof.22）。マージ条件は G1 緑

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反5件・修正5件 / DI の I/F・死活表・View の購読・KeyCap のつまみ・asmdef をやめ所有を指揮が割り当て（D-21・本計画「死活表」・D-22・D-8・D-20。Foundation/Anchor・Components/Text・ScaleTweenStep の所有は指揮の割り当て D-3・D-6・D-7）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強6・弱4・第3バケツ2 / 状態を判別 union に、ack を enum に、口を読みと変えに分割。裁定は agent 既定で判断記録へ（D-5・D-23〜D-27。第3バケツは「本PR外」の ack 検証の重複と、未移植のアンカーの共有の表）

---

## File Structure

`S` = `moorestech_client/Assets/Scripts`。ディレクトリごとの .cs/.uss は 10 本以内。

| パス | 種別 | 責務 |
|---|---|---|
| `S/Client.Ui/Foundation/Anchor/{TutorialAnchor.cs,TutorialAnchorRegistry.cs}` | Modify（指揮の割り当て B） | クリップ容器の印を足す・`Resolve` のログを外す |
| `S/Client.Ui/Foundation/Motion/Steps/ScaleTweenStep.cs` | Create（指揮の割り当て C） | 要素の拡縮を補間する step（脈動が最初の使い手） |
| `S/Client.Ui/Components/Text/ShortcutHintLabel/ShortcutHintLabel.cs`・`S/Client.Ui/Components/Text/KeyCap/KeyCap.uss` | Modify（指揮の割り当て A） | Prefix で印の引数を足さない・色のつまみ `--key-cap-color` |
| `S/Client.Ui.State/Tutorial/{TutorialElementKey.cs,TutorialTextFactory.cs}` | Create | `session:element` の鍵・GUID→文（失敗は LogError）の各 1 か所 |
| `S/Client.Ui.State/Tutorial/Logic/{TutorialClipRect.cs,TutorialAnchorReason.cs,TutorialAnchorState.cs,ITutorialAnchorStateLookup.cs,TutorialAnchorStateBook.cs,TutorialHighlightClip.cs,TutorialHighlightLabelPlacement.cs,TutorialDragGuideGate.cs,WorldPinEdgePlacement.cs}` | Create | アンカーの状態（判別 union）・帳簿（読みと変えの口）・Web の計算の逐語の写し（9 本） |
| `S/Client.Ui.State/Tutorial/Overlay/{TutorialOutlineCue.cs,TutorialDragGuideCue.cs,TutorialOverlayViewModel.cs,TutorialOverlayProjection.cs,TutorialOverlayModel.cs}` | Create | 枠と DragGuide の ViewModel（5 本。ack は写さない・D-10） |
| `S/Client.Ui.State/Tutorial/KeyControlHint/{KeyControlHintCue.cs,KeyControlHintViewModel.cs,KeyControlHintProjection.cs,KeyControlHintModel.cs}` | Create | ヒントの ViewModel |
| `S/Client.Ui.State/Tutorial/WorldPin/{WorldPinCue.cs,WorldPinViewModel.cs,WorldPinModel.cs}` | Create | ピンの ViewModel |
| `S/Client.Ui/Screens/Tutorial/Anchor/{ITutorialAnchorSubscriptions.cs,TutorialAnchorChange.cs,TutorialAnchorTracker.cs,TutorialAnchorMeasure.cs,TutorialAncestorClipWalker.cs}` | Create | アンカーの追跡と測定（購読の口。読みの口は Logic の `ITutorialAnchorStateLookup` を実装する） |
| `S/Client.Ui/Screens/Tutorial/Pulse/{TutorialAttentionPulse.cs,TutorialPulseBinding.cs}` | Create | 注意の脈動（振幅の唯一の持ち主と、周期の読み・開始・停止） |
| `S/Client.Ui/Screens/Tutorial/Overlay/{TutorialOverlayPresenter.cs,TutorialOverlayView.cs,TutorialOutlineView.cs,TutorialHighlightLabelView.cs,TutorialOverlay.uss}` | Create | 枠・ラベル |
| `S/Client.Ui/Screens/Tutorial/DragGuide/{TutorialDragGuideView.cs,TutorialDragGuideGlyph.cs,TutorialDragGuideStep.cs,TutorialDragGuide.uss}` | Create | DragGuide（E-16） |
| `S/Client.Ui/Screens/Tutorial/KeyControlHint/{KeyControlHintPresenter.cs,KeyControlHintView.cs,KeyControlHint.uss}` | Create | ホットバー上のヒント |
| `S/Client.Ui/Screens/Tutorial/WorldPin/{WorldPinPresenter.cs,WorldPinOverlayView.cs,WorldPinMarkerView.cs,WorldPinEdgeArrowView.cs,WorldPinGlyphs.cs,WorldPin.uss}` | Create | ワールドピン |
| `S/Client.Starter/Registration/GameUi/TutorialGameUiRegistration.cs`・`S/Client.Starter/Registration/MainGameUiRegistration.cs` | Create・Modify | 登録（画面登録区画に 1 行） |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | Modify | `Screens` 区画へ `@import` 4 行（Task 6〜9 が USS を作るときに 1 行ずつ） |
| `S/Client.Tests/Ui/Anchor/{TutorialAnchorRegistryTest.cs,TutorialAnchorCoverageTest.cs,TutorialAnchorCoveragePending.cs}` | Modify・Create | ログの期待を外す・カバレッジ L1 と未移植の表 |
| `S/Client.Tests/Ui/Components/Text/{ShortcutHintLabelPrefixTextTest.cs,KeyCapTest.cs,KeyCapColorProbe.uss}`・`S/Client.Tests/Ui/Motion/ScaleTweenStepTest.cs` | Create | 部品の直しと ScaleTweenStep の L1（probe はこのレーン専用。共有の `ComponentTokenProbe.uss` は触らない） |
| `S/Client.Tests/Ui/State/Tutorial/{TutorialAnchorStateBookTest.cs,TutorialHighlightClipTest.cs,TutorialHighlightLabelPlacementTest.cs,TutorialDragGuideGateTest.cs,WorldPinEdgePlacementTest.cs,TutorialOverlayProjectionTest.cs,KeyControlHintProjectionTest.cs}` | Create | L0（7 本） |
| `S/Client.Tests/Ui/Screens/Tutorial/Overlay/{TutorialOverlayTestHost.cs,TutorialAnchorTrackerTest.cs,TutorialOverlayAnchorTest.cs,TutorialOverlayLabelTest.cs,TutorialOverlayClipTest.cs,TutorialHighlightAncestorClipTest.cs,TutorialOverlayDragGuideTest.cs,TutorialPulseBindingTest.cs}` | Create | L1（枠・DragGuide・脈動の付け方） |
| `S/Client.Tests/Ui/Screens/Tutorial/Cues/{FakeTutorialCueModels.cs,KeyControlHintTest.cs,WorldPinOverlayTest.cs,TutorialStageCueTest.cs,TutorialViewportScalingTest.cs}` | Create | L1（ヒント・ピン・e2e の写し） |
| `S/Client.Tests/Ui/Screens/Tutorial/Design/{TutorialDesignSource.cs,TutorialAttentionTokenTest.cs,KeyControlHintDesignTest.cs,TutorialRingDesignTest.cs,WorldPinArrowDesignTest.cs}` | Create | 見た目の規則（21 件） |
| `docs/uitk/TODO.md`・`docs/uitk/disposition.md` | Modify | 実測の結論・見た目の例外 LX-35〜38・追従記録・処遇 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-tutorial-cues.cs` | Create | 録画シナリオ |

`Screens/WorldPin/**`・`Client.Ui.State/WorldPin/**`・`WorldPinGameUiRegistration.cs` は所有表にあるが使わない（D-1）。

### 死活表（この変更で動きが変わりうる既存の経路）

| 経路 | この計画の後 | 確かめ方 |
|---|---|---|
| 全体スイッチ Web のときのチュートリアル枠・ack（`Client.WebUiHost/Game/Actions/TutorialAnchorAckAction.cs`・`Topics/Tutorial/*.cs`） | 変わらない。Web 側は `Client.Ui` を登録しない（`MainGameUiRegistration` が `UiSystem.GameUi` 以外で return）ので、`TutorialGameUiRegistration` も Model も Presenter も作られず、store の購読者は WebUiHost の topic だけのまま。`TutorialAnchorAckAction` は変えない | Task 12 Step 3 の `Client.Tests.Ui.(Anchor|Theme|Conventions)` と既存 `Client.Tests.WebUi` の Tutorial のテスト（`grep -rln "TutorialAnchorAck\|tutorial.presentation" S/Client.Tests` で拾ったもの）を回す |
| `TutorialAnchorRegistry.Resolve` の Warning を外す影響 | 本番の呼び手は無い（`grep -rn "TutorialAnchorRegistry.Resolve" S --include='*.cs'` で `Client.Tests` だけ）。外した Warning は、このレーンの `TutorialAnchorTracker` が遷移時に出す | Task 2 Step 1 の grep を done.md に写し、`TutorialAnchorRegistryTest` 7 件 |
| ポーズの 6 アンカー（`PauseMenuView`・`PauseMenuTopPageView`・`PauseMenuSubPageView` の `TutorialAnchor.Mark`） | 変わらない（印の付け方に触れない） | カバレッジ L1 の「ポーズの6つのアンカーは…一意に解ける」と既存 `Client.Tests.Ui.Screens.PauseMenu.` |
| `UIStateControl.OnStateChanged` と `TutorialPresentationStateStore`・`WorldPinStateStore` の既存の購読者（WebUiHost の topic・UI 状態の遷移） | 購読者が 1〜3 増えるだけで、発行の順・値は変えない。新しい購読者は値を読むだけで store へ書かない | 録画 `uitk-tutorial-cues`（Task 15）で、チャレンジのピンと UI 状態の遷移が今までどおり動く |
| S2-hud の KeyHintHud（`ShortcutHintLabel` の Inline）と既存の `KeyCap` の色 | 変わらない。Inline は今までどおり印の引数を足し、`KeyCap` は `--key-cap-color` が無ければ `--text-high-contrast` を使う | Task 8 Step 2 の `^Client\.Tests\.Ui\.Components\.Text\.` 全件（既存の `ShortcutHintLabelTest`・`ShortcutHintSegmentsTest`・`TooltipFace*` を含む） |

### 写すテストの件数（Web の実物を grep して数え直した。tests-ladder と一致）

| Web のファイル | 件数 | 写し先 |
|---|---|---|
| `src/features/tutorial/KeyControlHintHud.test.ts` | 4 | L1 `KeyControlHintTest` 4 |
| `src/features/commonHud/alwaysOnHudWheel.test.ts` | it.each 9 行のうちキー操作ヒントの 1 行（件数は S2-hud の表が数えるので計に入れない。S2-hud 計画 52 行） | L1 `KeyControlHintTest` の `キー操作ヒントは入力を奪わない`（同じ web-test タグ。D-36） |
| `e2e/tests/system/worldPin.spec.ts` | 6 | L1 `WorldPinOverlayTest` 6 |
| `src/features/tutorial/overlay/TutorialOverlay.test.ts` | 17 | L0 4（StateBook 3・Projection 1）＋ L1 11（Anchor 1・Label 4・Clip 3・DragGuide 3）＋ disposition 2（捨てる。ack の 2 件・D-10） |
| `src/features/tutorial/tutorialAttentionDesign.test.ts` | 21 | L1 静的検査 21（Token 6・KeyControl 4・Ring 5・WorldPin 6） |
| `e2e/tests/system/tutorial.spec.ts` | 6 | L1 `TutorialStageCueTest` 6 |
| `e2e/tests/system/tutorialHighlightClip.spec.ts` | 1 | L1 `TutorialHighlightAncestorClipTest` 1 |
| `e2e/tests/system/portalLayerScaling.spec.ts` | 3（4 件中。通知の 1 件は S2-notification） | L1 `TutorialViewportScalingTest` 3（2 fixture で各 3 件走る） |
| 計 | 58 | 写す 56・捨てる 2 |

自前の L0/L1（Web に無い。`web-test` タグなし）: `TutorialHighlightClipTest`・`TutorialHighlightLabelPlacementTest`・`TutorialDragGuideGateTest`・`WorldPinEdgePlacementTest`・`KeyControlHintProjectionTest`・`TutorialAnchorTrackerTest`・`TutorialPulseBindingTest`・`ScaleTweenStepTest`・DragGuide の置き直し 1 件・`TutorialOverlayAnchorTest` の画面を外す 1 件・`TutorialAnchorCoverageTest` と StateBook の自前の数件。

---

### Task 1: 前提の確認と記録の準備

**Files:**
- なし（確認と `done.md` の作成だけ）

**Interfaces:**
- Consumes: Global Constraints の「使う部品と公開 API」の全部（名前が違えば実物を正にして、この計画のコードの該当名だけを読み替える）
- Produces: `$LOGS/harness/uitk/S2-tutorial/done.md`（以降のタスクが証拠を足す）

- [ ] **Step 1: 位置と前提のマージを確かめる**

Run:
```bash
pwd
git branch --show-current
git fetch origin
git merge-base --is-ancestor origin/feature/uitk HEAD && echo BASE-OK
S=moorestech_client/Assets/Scripts
grep -n "public static TutorialAnchorResolution Resolve(VisualElement searchRoot, string anchorId)" $S/Client.Ui/Foundation/Anchor/TutorialAnchorRegistry.cs
grep -n "public static UiMotionHandle Play(VisualElement target, IUiAnimation animation)" $S/Client.Ui/Foundation/Motion/Playback/UiMotionPlayer.cs
grep -n "PingPong\|EaseInOutSine" $S/Client.Ui/Foundation/Motion/Tween/*.cs
grep -n "public KeyCap(string keyText)\|public ShortcutHintLabel(string testid, ShortcutHintLayout layout)\|public void SetHint(UiText sentence, string shortcutText)" -r $S/Client.Ui/Components/Text
grep -n "KeyHintText" $S/Client.Ui/Foundation/Text/UiTypographyClass.cs
grep -n "Tutorial,\|WorldPin,\|OverlayPanel," $S/Client.Ui/Foundation/Layers/UiLayer.cs
for n in --world-pin-face --world-pin-arrow-size --world-pin-arrow-shadow --world-pin-edge-margin --tutorial-drag-guide-size --tutorial-drag-guide-duration --tutorial-drag-guide-shadow --tutorial-highlight-label-gap --label-face-padding --label-face-font-size --tutorial-key-hint-hotbar-gap --tutorial-key-hint-gap --tutorial-key-hint-kbd-gap --tutorial-key-hint-color --tutorial-highlight-glow --tutorial-attention-red --tutorial-attention-glow --tutorial-pulse-duration --hotbar-floor-offset --text-high-contrast; do grep -q -- "$n:" $S/Client.Ui/Foundation/Theme/Tokens.uss || echo "MISSING-TOKEN $n"; done
grep -n -- "--shortcut-hint-label-gap" $S/Client.Ui/Foundation/Theme/DerivedTokens.uss
grep -n "section: Screens" -A6 "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
grep -n "Register(builder)" $S/Client.Starter/Registration/MainGameUiRegistration.cs
grep -n "Story," $S/Client.Game/InGame/UI/UIState/UIStateEnum.cs
grep -rn "UIStateEnum.Story" $S/Client.Game/InGame/UI/UIState/State/*.cs
grep -rn "TutorialAnchorRegistry.Resolve" $S --include='*.cs'
ls $S/Client.Ui/Foundation/Motion/Steps
grep -rn "TutorialAnchor.Mark(" $S/Client.Ui
git merge-base --is-ancestor e26010aa0c origin/feature/uitk && echo IFIX1-IN
grep -n "public IReadOnlyReactiveProperty<bool> IsBlocking" $S/Client.Ui.State/SkitPresence/BlockingSkitPresence.cs
grep -rn "BlockingSkitPresence" $S/Client.Starter/Registration --include='*.cs'
grep -rn "game.crosshair\|challenge.current-hud\|mining.hud\|train-hud.status" $S/Client.Ui --include='*.cs' | grep "TutorialAnchor.Mark"
```
Expected: ブランチ `uitk-lane/s2-tutorial`、`BASE-OK`、各シグネチャが 1 行ずつ、`MISSING-TOKEN` 0 行、`--shortcut-hint-label-gap` が 1 行、`Screens` 区画に BugReport・PauseMenu・Settings・ToastHost の 4 行（ほかのレーンが先にマージしていれば増えている）、`PauseMenuGameUiRegistration.Register(builder);`・`ToastGameUiRegistration.Register(builder);`、`Story,` が 1 行と `UIStateEnum.Story` への遷移が `GameScreenState`・`PlaceBlockState` に 1 行ずつ（参考。blocking の判定には使わない。D-20）、`IFIX1-IN`（I-fix1＝PR #1606 は取り込み済み）、`BlockingSkitPresence.IsBlocking` が 1 行と、その登録（S2-hud の `HudGameUiRegistration` の `RegisterInstance`）が 1 行、S2-hud が印を付けた 4 つ（`game.crosshair`・`challenge.current-hud`・`mining.hud`・`train-hud.status`）の `TutorialAnchor.Mark` が出る、`Resolve` の呼び手が `Client.Tests` だけ（死活表。done.md に写す）、`Steps/` に `ScaleTweenStep.cs` が無い、`Mark(` が PauseMenuView・PauseMenuTopPageView・PauseMenuSubPageView の 3 行（ほかのレーンの印が増えていれば Task 12 の未移植の表から外す）。
分岐: `BlockingSkitPresence` が無い（S2-hud が `origin/feature/uitk` にまだ入っていない）なら、ここで止めて指揮へ報告する（このレーンは S2-hud に依存。自前で作らない）。シグネチャや区画が違えば、違う名前を `done.md` の「前提の読み替え」に写し、この計画のコードの該当名だけを読み替える。トークンが欠けていれば自分で足さず「integrator への依頼」に書いて止める

- [ ] **Step 2: このレーンが直す部品の現状を写す（指揮の割り当て A）**

Run:
```bash
sed -n '/public void SetHint/,/^        }/p' moorestech_client/Assets/Scripts/Client.Ui/Components/Text/ShortcutHintLabel/ShortcutHintLabel.cs
cat moorestech_client/Assets/Scripts/Client.Ui/Components/Text/KeyCap/KeyCap.uss
grep -rn "new ShortcutHintLabel(\|new KeyCap(" moorestech_client/Assets/Scripts --include='*.cs' | grep -v "Client.Tests"
grep -rn -- "--shortcut-hint-label-gap:" moorestech_client/Assets --include='*.uss' --include='*.tss'
```
（最後の grep は部品つまみの既定の置き場の参考。`--key-cap-color` の既定はそこではなく、このレーンが持つ `KeyCap.uss` の `:root` に置く。D-31）
Expected: `SetHint` が Prefix でも `sentence.WithLeadingParam(...)` を呼ぶ（`UiText.WithLeadingParam` は `UiTextSource.Content`・`Verbatim` に `InvalidOperationException`。チュートリアルの文は content なので KeyControlHint が作れない）、`.key-cap { color: var(--text-high-contrast) }`。使い手の一覧（S2-hud の KeyHintHud 等）を done.md の「証拠」に写す。直しは Task 8 で入れる（W2 で同じファイルを書き換えるレーンは他に無い）。すでに誰かが直していれば Task 8 Step 1〜2 の該当部分を飛ばす

- [ ] **Step 3: bd と報告を用意する**

Run:
```bash
LOGS="$(cd "$(git rev-parse --path-format=absolute --git-common-dir)/../.." && pwd)/moorestech_logs"
sed -n 1,80p "$LOGS/README.md"
mkdir -p "$LOGS/harness/uitk/S2-tutorial"
bd list --status open | grep -i "S2-tutorial"
```
Expected: README の配置の節が読める。S2-tutorial の bd が無ければ `bd create "UITK S2-tutorial: チュートリアル枠・KeyControlHint・ワールドピン" --parent moorestech-albof` で作り、`bd update <ID> --claim`。`$LOGS/harness/uitk/S2-tutorial/done.md` を次の見出しで作る: 1 行目 `状態: 作業中`（完了時に「状態: 完了」へ）・`# S2-tutorial 完了報告`・`## 前提の読み替え`・`## 証拠`・`## 差の表`・`## 要裁定`・`## integrator への依頼`・`## 昇格候補`・`## 未確認`

- [ ] **Step 4: 既存テストの基準を取る**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Anchor|Screens\.PauseMenu|Theme|Conventions)\."`
Expected: `ErrorCount: 0`、全件 PASS。件数を `done.md` の「証拠」に書く（前から落ちているものは名前を書いて指揮へ報告）

---

### Task 2: Foundation/Anchor の宣言済み変更（ログを呼ぶ側へ・クリップ容器の印）

**Files:**
- Modify: `S/Client.Ui/Foundation/Anchor/TutorialAnchorRegistry.cs`
- Modify: `S/Client.Ui/Foundation/Anchor/TutorialAnchor.cs`
- Modify: `S/Client.Tests/Ui/Anchor/TutorialAnchorRegistryTest.cs`

**Interfaces:**
- Consumes: 現行の `TutorialAnchorRegistry.Resolve(VisualElement, string)`（0 件・複数件で毎回 `Debug.LogWarning`）
- Produces（設計書 A-65）:
  - `TutorialAnchorRegistry.Resolve(VisualElement searchRoot, string anchorId) : TutorialAnchorResolution`（ログを出さない。出すのは状態の遷移を知っている呼ぶ側）
  - `TutorialAnchor.ClipContainerClassName = "tutorial-anchor-clip"`・`TutorialAnchor.MarkClipContainer(VisualElement element)`（USS の `overflow: hidden` で子を切る要素に画面が付ける印。`IResolvedStyle` に `overflow` が無く、USS で付けた overflow を C# から読めないため）

- [ ] **Step 1: 失敗するテストに書き換える**

`TutorialAnchorRegistryTest.cs` の `ExpectRegistryWarning()` と 3 か所の呼び出しを消し、次の 2 件を足す:
```csharp
        [Test]
        public void 解決はログを出さず状態の遷移の報告を呼ぶ側に任せる()
        {
            var first = new VisualElement();
            var second = new VisualElement();
            TutorialAnchor.Mark(first, AnchorIds.PauseMenu);
            TutorialAnchor.Mark(second, AnchorIds.PauseMenu);
            rootVisualElement.Add(first);
            rootVisualElement.Add(second);

            // 毎フレームの追跡で呼ばれるので、0 件・複数件でも Resolve 自身は黙る
            // Called every frame by the tracker, so Resolve stays silent even for zero or multiple matches
            TutorialAnchorRegistry.Resolve(rootVisualElement, "pause.menu");
            TutorialAnchorRegistry.Resolve(rootVisualElement, "pause.save");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void クリップ容器の印は専用のクラスを付ける()
        {
            var container = new VisualElement();
            TutorialAnchor.MarkClipContainer(container);

            Assert.IsTrue(container.ClassListContains(TutorialAnchor.ClipContainerClassName));
            Assert.IsFalse(container.ClassListContains(TutorialAnchor.MarkerClassName));
        }
```
（`using System.Text.RegularExpressions;` は不要になるので消す）

Run: `uloop compile --project-path ./moorestech_client`
Expected: `MarkClipContainer`・`ClipContainerClassName` が無いのでコンパイルエラー

- [ ] **Step 2: 実装する**

`TutorialAnchorRegistry.cs` の `if (count == 0)` 以下を次に替える（`using UnityEngine;` は不要になるので消す）:
```csharp
            // 0件・複数件の報告は、状態の遷移を知っている呼ぶ側（TutorialAnchorTracker）が出す。毎フレーム呼ばれるのでここでは黙る
            // Callers that know state transitions (TutorialAnchorTracker) report zero or multiple matches; this runs every frame so it stays silent
            if (count == 0) return new TutorialAnchorResolution.Missing();
            if (1 < count) return new TutorialAnchorResolution.Duplicate(count);
            return new TutorialAnchorResolution.Unique(found);
```
`TutorialAnchor.cs` に足す:
```csharp
        public const string ClipContainerClassName = "tutorial-anchor-clip";

        // USS の overflow: hidden で子を切る要素に付ける。C# は USS 由来の overflow を読めないので、祖先クリップの判定はこの印で行う（ADR 0024）
        // Put on elements whose USS overflow: hidden clips children; C# cannot read USS overflow, so the ancestor clip walk uses this mark (ADR 0024)
        public static void MarkClipContainer(VisualElement element)
        {
            element.AddToClassList(ClipContainerClassName);
        }
```

- [ ] **Step 3: 確かめてコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Anchor\."`
Expected: `ErrorCount: 0`、`TutorialAnchorRegistryTest` 7 件・`AnchorIdsWebParityTest` が PASS
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Anchor moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor
git commit -m "feat(uitk): アンカー解決のログを呼ぶ側へ移し、クリップ容器の印を足す（S2-tutorial・設計書 A-65）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Web の計算の写し（L0。`Client.Ui.State/Tutorial/Logic/`）

**Files:**
- Create: `S/Client.Ui.State/Tutorial/Logic/{TutorialClipRect.cs,TutorialAnchorReason.cs,TutorialAnchorState.cs,ITutorialAnchorStateLookup.cs,TutorialAnchorStateBook.cs,TutorialHighlightClip.cs,TutorialHighlightLabelPlacement.cs,TutorialDragGuideGate.cs,WorldPinEdgePlacement.cs}`
- Test: `S/Client.Tests/Ui/State/Tutorial/{TutorialAnchorStateBookTest.cs,TutorialHighlightClipTest.cs,TutorialHighlightLabelPlacementTest.cs,TutorialDragGuideGateTest.cs,WorldPinEdgePlacementTest.cs}`

**Interfaces:**
- Consumes: Web の式（逐語で写す）: `W/shared/tutorialAnchor/ancestorClip.ts` の `clipPathInset`・`isDisjoint`、`W/features/tutorial/overlay/HighlightLabel.tsx` の配置、`W/features/tutorial/overlay/DragGuide.tsx` の門、`W/features/tutorial/WorldPinOverlay.tsx` の `EdgeArrow`、`W/shared/tutorialAnchor/resolveAnchor.ts` の理由
- Produces（namespace `Client.Ui.State.Tutorial.Logic`）:
  - `readonly struct TutorialClipRect(float left, float top, float right, float bottom)`: `Left`・`Top`・`Right`・`Bottom`・`Width`・`Height`・`CenterX`・`CenterY`・`Intersect(TutorialClipRect)`・`Expand(float)`・`IsDisjoint(TutorialClipRect)`・`Contains(float x, float y)`・`ContainsRect(TutorialClipRect)`・`Equals`
  - `enum TutorialAnchorReason { Mounted, Missing, DuplicateAnchor, DisplayNone, VisibilityHidden, ZeroArea, OutsideViewport }`（状態の理由の唯一の enum）
  - `abstract class TutorialAnchorState`（判別 union。前例 `TutorialAnchorResolution`）: `readonly TutorialAnchorReason Reason`・`abstract bool SameAs(TutorialAnchorState other)`、`sealed class TutorialAnchorState.Ready(TutorialClipRect rect, TutorialClipRect clip)`（`Rect`・`Clip`）・`sealed class TutorialAnchorState.NotFound(TutorialAnchorReason reason)`・`sealed class TutorialAnchorState.Hidden(TutorialAnchorReason reason)`
  - `interface ITutorialAnchorStateLookup { bool TryGet(string anchorId, out TutorialAnchorState state); }`（読みの口。帳簿と Task 5 の追跡器の両方が実装し、View もこれを持つ。D-23）
  - `sealed class TutorialAnchorStateBook : ITutorialAnchorStateLookup`: 変えの口 `bool ApplyIfChanged(string anchorId, TutorialAnchorState state)`（変わった時だけ書いて true）・`void KeepOnly(IReadOnlyCollection<string> anchorIds)`、読みの口 `TryGet`
  - `static class TutorialHighlightClip.TryComputeInset(TutorialClipRect box, TutorialClipRect clip, float outset, out TutorialClipInset inset) : bool`・`readonly struct TutorialClipInset(float top, float right, float bottom, float left)`
  - `static class TutorialHighlightLabelPlacement.Place(TutorialClipRect box, TutorialClipRect clip, float gap, float faceWidth, float faceHeight) : TutorialLabelPosition`（`Left`・`Top`・`FlippedAbove`）・`ShowsLabel(TutorialClipRect anchorRect, TutorialClipRect clip) : bool`
  - `static class TutorialDragGuideGate.TryPlace(TutorialAnchorState from, TutorialAnchorState to, out TutorialDragGuidePlacement placement) : bool`（`OriginX`・`OriginY`・`TravelX`・`TravelY`）
  - `static class WorldPinEdgePlacement.TryPlace(float width, float height, float edgeMargin, float directionX, float directionY, out WorldPinEdgeSpot spot) : bool`（`Left`・`Top`・`AngleDegrees`）

- [ ] **Step 1: 失敗するテストを書く（StateBook。Web の 3 件を含む）**

`TutorialAnchorStateBookTest.cs`:
```csharp
using Client.Ui.State.Tutorial.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// アンカーの状態の帳簿が、同値の再解決を捨て、clip の変化と購読外しを拾うことを確かめる（L0）
    /// Verifies the anchor state book drops equal re-resolutions and notices clip changes and unsubscriptions (L0)
    /// </summary>
    public class TutorialAnchorStateBookTest
    {
        private static readonly TutorialClipRect Viewport = new(0, 0, 1280, 720);

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">同値rectの再解決では再描画しない</web-test>
        [Test]
        public void 同じ矩形とclipの再解決は変化として扱わない()
        {
            var book = new TutorialAnchorStateBook();
            Assert.IsTrue(book.ApplyIfChanged("pause.save", new TutorialAnchorState.Ready(new TutorialClipRect(10, 20, 110, 60), Viewport)));
            Assert.IsFalse(book.ApplyIfChanged("pause.save", new TutorialAnchorState.Ready(new TutorialClipRect(10, 20, 110, 60), Viewport)));
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">rectが同値でもclipが変われば再描画する</web-test>
        [Test]
        public void 矩形が同じでもclipが変われば変化として扱う()
        {
            var book = new TutorialAnchorStateBook();
            var rect = new TutorialClipRect(10, 20, 110, 60);
            book.ApplyIfChanged("research.node-a", new TutorialAnchorState.Ready(rect, Viewport));
            Assert.IsTrue(book.ApplyIfChanged("research.node-a", new TutorialAnchorState.Ready(rect, new TutorialClipRect(0, 30, 1280, 720))));
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">購読対象外になったanchorの解決状態は落とす</web-test>
        [Test]
        public void 購読から外れたアンカーの状態は捨てる()
        {
            var book = new TutorialAnchorStateBook();
            book.ApplyIfChanged("pause.save", new TutorialAnchorState.Ready(new TutorialClipRect(0, 0, 10, 10), Viewport));
            book.ApplyIfChanged("pause.back", new TutorialAnchorState.NotFound(TutorialAnchorReason.Missing));
            book.KeepOnly(new[] { "pause.back" });

            Assert.IsFalse(book.TryGet("pause.save", out _));
            Assert.IsTrue(book.TryGet("pause.back", out var kept));
            Assert.IsInstanceOf<TutorialAnchorState.NotFound>(kept);
        }

        [Test]
        public void 理由だけが変わった未解決も変化として扱う()
        {
            var book = new TutorialAnchorStateBook();
            book.ApplyIfChanged("pause.save", new TutorialAnchorState.NotFound(TutorialAnchorReason.Missing));
            Assert.IsTrue(book.ApplyIfChanged("pause.save", new TutorialAnchorState.NotFound(TutorialAnchorReason.DuplicateAnchor)));
        }
    }
}
```

- [ ] **Step 2: 失敗するテストを書く（clip・ラベル・DragGuide・端クランプ。自前の L0）**

`TutorialHighlightClipTest.cs`（`clipPathInset` の写し）:
```csharp
        [Test]
        public void 切らない辺はグロー幅だけ外へ残す()
        {
            Assert.IsTrue(TutorialHighlightClip.TryComputeInset(new TutorialClipRect(100, 100, 200, 150), new TutorialClipRect(0, 0, 1280, 720), 4f, out var inset));
            Assert.AreEqual((-4f, -4f, -4f, -4f), (inset.Top, inset.Right, inset.Bottom, inset.Left));
        }

        [Test]
        public void clipの外へ出た辺だけを切る()
        {
            Assert.IsTrue(TutorialHighlightClip.TryComputeInset(new TutorialClipRect(100, 80, 200, 150), new TutorialClipRect(0, 100, 1280, 720), 4f, out var inset));
            Assert.AreEqual(20f, inset.Top);
            Assert.AreEqual(-4f, inset.Bottom);
        }

        [Test]
        public void 素の矩形が交わらなければ描かない()
        {
            Assert.IsFalse(TutorialHighlightClip.TryComputeInset(new TutorialClipRect(100, 60, 200, 98), new TutorialClipRect(0, 100, 1280, 720), 4f, out _));
        }

        [Test]
        public void 切った後に高さが残らなければ描かない()
        {
            // 素の矩形は交わる（IsDisjoint が偽）まま上 40＋下 60 が高さ 100 に届く入力で、高さ判定の分岐を通す
            // The raw rects overlap (IsDisjoint false) while top 40 + bottom 60 reaches the height 100, exercising the height branch
            Assert.IsFalse(TutorialHighlightClip.TryComputeInset(new TutorialClipRect(100, 60, 200, 160), new TutorialClipRect(0, 100, 1280, 100), 4f, out _));
        }
```
`TutorialHighlightLabelPlacementTest.cs`:
```csharp
        private static readonly TutorialClipRect Clip = new(0, 0, 1280, 720);

        [Test]
        public void 既定は枠の下に隙間を空けて置く()
        {
            var position = TutorialHighlightLabelPlacement.Place(new TutorialClipRect(100, 100, 300, 140), Clip, 4f, 120f, 26f);
            Assert.AreEqual((100f, 144f, false), (position.Left, position.Top, position.FlippedAbove));
        }

        [Test]
        public void 下に収まらず上に収まるときだけ上へ反転する()
        {
            var position = TutorialHighlightLabelPlacement.Place(new TutorialClipRect(100, 680, 300, 710), Clip, 4f, 120f, 26f);
            Assert.AreEqual((650f, true), (position.Top, position.FlippedAbove));
        }

        [Test]
        public void 上にも下にも収まらなければ下に置く()
        {
            var position = TutorialHighlightLabelPlacement.Place(new TutorialClipRect(100, 10, 300, 700), new TutorialClipRect(0, 0, 1280, 720), 4f, 120f, 26f);
            Assert.AreEqual((704f, false), (position.Top, position.FlippedAbove));
        }

        [Test]
        public void 右端を越えるときはclipの内へ押し戻し器より広ければ左端に揃える()
        {
            Assert.AreEqual(1160f, TutorialHighlightLabelPlacement.Place(new TutorialClipRect(1200, 100, 1270, 140), Clip, 4f, 120f, 26f).Left);
            Assert.AreEqual(0f, TutorialHighlightLabelPlacement.Place(new TutorialClipRect(1200, 100, 1270, 140), Clip, 4f, 1400f, 52f).Left);
        }

        [Test]
        public void アンカーがclipで欠けていればラベルを出さない()
        {
            Assert.IsTrue(TutorialHighlightLabelPlacement.ShowsLabel(new TutorialClipRect(10, 10, 50, 50), Clip));
            Assert.IsFalse(TutorialHighlightLabelPlacement.ShowsLabel(new TutorialClipRect(-10, 10, 50, 50), Clip));
        }
```
`TutorialDragGuideGateTest.cs`:
```csharp
        private static readonly TutorialClipRect Clip = new(0, 0, 1280, 720);

        [Test]
        public void 両端がreadyなら起点の中心から終点の中心までの移動量を返す()
        {
            var from = new TutorialAnchorState.Ready(new TutorialClipRect(100, 100, 140, 140), Clip);
            var to = new TutorialAnchorState.Ready(new TutorialClipRect(500, 300, 540, 340), Clip);
            Assert.IsTrue(TutorialDragGuideGate.TryPlace(from, to, out var placement));
            Assert.AreEqual((120f, 120f, 400f, 200f), (placement.OriginX, placement.OriginY, placement.TravelX, placement.TravelY));
        }

        [Test]
        public void 片方が未解決か起点の中心がclipの外なら置かない()
        {
            var to = new TutorialAnchorState.Ready(new TutorialClipRect(500, 300, 540, 340), Clip);
            Assert.IsFalse(TutorialDragGuideGate.TryPlace(new TutorialAnchorState.NotFound(TutorialAnchorReason.Missing), to, out _));
            var scrolledOut = new TutorialAnchorState.Ready(new TutorialClipRect(100, 40, 140, 80), new TutorialClipRect(0, 100, 1280, 720));
            Assert.IsFalse(TutorialDragGuideGate.TryPlace(scrolledOut, to, out _));
        }
```
`WorldPinEdgePlacementTest.cs`:
```csharp
        [Test]
        public void 真右の方向は右端から余白だけ内側の中央に置く()
        {
            Assert.IsTrue(WorldPinEdgePlacement.TryPlace(1280f, 720f, 40f, 1f, 0f, out var spot));
            Assert.AreEqual((1240f, 360f, 0f), (spot.Left, spot.Top, spot.AngleDegrees));
        }

        [Test]
        public void 斜めの方向は先に当たる辺でクランプし角度はatan2の度()
        {
            Assert.IsTrue(WorldPinEdgePlacement.TryPlace(1280f, 720f, 40f, 0.7071f, 0.7071f, out var spot));
            Assert.AreEqual(960f, spot.Left, 0.01f);
            Assert.AreEqual(680f, spot.Top, 0.01f);
            Assert.AreEqual(45f, spot.AngleDegrees, 0.01f);
        }

        [Test]
        public void 寸法が0なら置かない()
        {
            Assert.IsFalse(WorldPinEdgePlacement.TryPlace(0f, 720f, 40f, 1f, 0f, out _));
        }
```
（各ファイルは `using Client.Ui.State.Tutorial.Logic; using NUnit.Framework;`・namespace `Client.Tests.Ui.State.Tutorial`・日英 2 行の `<summary>` を StateBook と同じ形で付ける）

Run: `uloop compile --project-path ./moorestech_client`
Expected: `Client.Ui.State.Tutorial.Logic` が無いのでコンパイルエラー

- [ ] **Step 3: 矩形・状態・帳簿を書く**

`TutorialClipRect.cs`:
```csharp
using System;

namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// 画面座標の矩形（左上原点・パネル単位）。Web の ClipRect の写しで、値で比べる
    /// A screen rect (top-left origin, panel units); a copy of Web's ClipRect compared by value
    /// </summary>
    public readonly struct TutorialClipRect : IEquatable<TutorialClipRect>
    {
        public readonly float Left;
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;

        public TutorialClipRect(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public float Width => Right - Left;
        public float Height => Bottom - Top;
        public float CenterX => (Left + Right) / 2f;
        public float CenterY => (Top + Bottom) / 2f;

        public TutorialClipRect Intersect(TutorialClipRect other)
        {
            return new TutorialClipRect(Math.Max(Left, other.Left), Math.Max(Top, other.Top), Math.Min(Right, other.Right), Math.Min(Bottom, other.Bottom));
        }

        public TutorialClipRect Expand(float amount)
        {
            return new TutorialClipRect(Left - amount, Top - amount, Right + amount, Bottom + amount);
        }

        // Web の isDisjoint と同じく辺が接するだけでも交わらないとみなす
        // Like Web's isDisjoint, touching edges count as disjoint
        public bool IsDisjoint(TutorialClipRect other)
        {
            return other.Right <= Left || other.Left >= Right || other.Bottom <= Top || other.Top >= Bottom;
        }

        public bool Contains(float x, float y)
        {
            return x >= Left && x <= Right && y >= Top && y <= Bottom;
        }

        public bool ContainsRect(TutorialClipRect inner)
        {
            return inner.Left >= Left && inner.Right <= Right && inner.Top >= Top && inner.Bottom <= Bottom;
        }

        public bool Equals(TutorialClipRect other)
        {
            return Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        }

        public override bool Equals(object obj)
        {
            return obj is TutorialClipRect other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Left, Top, Right, Bottom);
        }
    }
}
```
`TutorialAnchorReason.cs`（enum だけ）・`ITutorialAnchorStateLookup.cs`。`TutorialAnchorState.cs`（判別 union。前例 `TutorialAnchorResolution` と同じ入れ子の sealed 派生）:
```csharp
namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// 1 つのアンカーの解決結果。ready だけが矩形と祖先クリップを持ち、未解決は理由だけを持つ
    /// One anchor's resolution; only Ready carries the rect and ancestor clip, unresolved states carry only a reason
    /// </summary>
    public abstract class TutorialAnchorState
    {
        public readonly TutorialAnchorReason Reason;

        private TutorialAnchorState(TutorialAnchorReason reason)
        {
            Reason = reason;
        }

        // ready は矩形と clip まで、未解決は種と理由まで比べる（Web の isSameAnchor）
        // Ready compares rect and clip, unresolved states compare kind and reason (Web isSameAnchor)
        public abstract bool SameAs(TutorialAnchorState other);

        public sealed class Ready : TutorialAnchorState
        {
            public readonly TutorialClipRect Rect;
            public readonly TutorialClipRect Clip;

            public Ready(TutorialClipRect rect, TutorialClipRect clip) : base(TutorialAnchorReason.Mounted)
            {
                Rect = rect;
                Clip = clip;
            }

            public override bool SameAs(TutorialAnchorState other)
            {
                return other is Ready ready && ready.Rect.Equals(Rect) && ready.Clip.Equals(Clip);
            }
        }

        public sealed class NotFound : TutorialAnchorState
        {
            public NotFound(TutorialAnchorReason reason) : base(reason) { }

            public override bool SameAs(TutorialAnchorState other)
            {
                return other is NotFound && other.Reason == Reason;
            }
        }

        public sealed class Hidden : TutorialAnchorState
        {
            public Hidden(TutorialAnchorReason reason) : base(reason) { }

            public override bool SameAs(TutorialAnchorState other)
            {
                return other is Hidden && other.Reason == Reason;
            }
        }
    }
}
```
`TutorialAnchorStateBook.cs`:
```csharp
using System.Collections.Generic;

namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// 購読中のアンカーの最新の解決結果を持ち、同値の再解決を捨てる
    /// Holds the latest resolution of subscribed anchors and drops equal re-resolutions
    /// </summary>
    public sealed class TutorialAnchorStateBook : ITutorialAnchorStateLookup
    {
        private readonly Dictionary<string, TutorialAnchorState> _states = new();

        public bool ApplyIfChanged(string anchorId, TutorialAnchorState state)
        {
            // 同値なら描き直さない（Web の「同値rectの再解決では再描画しない」）
            // Equal values skip redraws (Web: equal rect re-resolution does not redraw)
            if (_states.TryGetValue(anchorId, out var previous) && previous.SameAs(state)) return false;
            _states[anchorId] = state;
            return true;
        }

        public bool TryGet(string anchorId, out TutorialAnchorState state)
        {
            return _states.TryGetValue(anchorId, out state);
        }

        public void KeepOnly(IReadOnlyCollection<string> anchorIds)
        {
            // 購読から外れたものだけを捨て、続くものの状態は保つ
            // Drop only unsubscribed anchors and keep the state of the continuing ones
            var keep = new HashSet<string>(anchorIds);
            var dropped = new List<string>();
            foreach (var anchorId in _states.Keys)
            {
                if (!keep.Contains(anchorId)) dropped.Add(anchorId);
            }
            foreach (var anchorId in dropped) _states.Remove(anchorId);
        }
    }
}
```

- [ ] **Step 4: Web の式を写す（clip・ラベル・DragGuide・端クランプ）**

`TutorialHighlightClip.cs`（`clipPathInset` の逐語。`TutorialClipInset` は同じファイルの下に `readonly struct` で置く）:
```csharp
        // 枠に当てる inset を返す。false は描かない（Web の clipPathInset の null）
        // Returns the inset applied to the ring; false means skip rendering (Web clipPathInset returning null)
        public static bool TryComputeInset(TutorialClipRect box, TutorialClipRect clip, float outset, out TutorialClipInset inset)
        {
            inset = default;
            // silent-ok: クリップで見えない枠は Web と同じく出さない仕様（失敗ではない）/ L1 TutorialOverlayClipTest が見る
            // クランプ後の値ではグロー分の帯を見逃すので、素の矩形で先に交わりを見る
            // Clamped values can miss a fully clipped box, so check raw disjointness first
            if (box.IsDisjoint(clip)) return false;
            var top = Math.Max(-outset, clip.Top - box.Top);
            var right = Math.Max(-outset, box.Right - clip.Right);
            var bottom = Math.Max(-outset, box.Bottom - clip.Bottom);
            var left = Math.Max(-outset, clip.Left - box.Left);
            if (top + bottom >= box.Height || left + right >= box.Width) return false;
            inset = new TutorialClipInset(top, right, bottom, left);
            return true;
        }
```
`TutorialHighlightLabelPlacement.cs`（`HighlightLabel.tsx` の逐語。`TutorialLabelPosition` は同じファイルの下）:
```csharp
        public static TutorialLabelPosition Place(TutorialClipRect box, TutorialClipRect clip, float gap, float faceWidth, float faceHeight)
        {
            // 既定は枠の下。下に収まらず上に収まる時だけ上へ反転する（ユーザー裁定 2026-08-22）
            // Below the ring by default; flip above only when it does not fit below and does fit above (user ruling 2026-08-22)
            var fitsBelow = box.Bottom + gap + faceHeight <= clip.Bottom;
            var fitsAbove = box.Top - gap - faceHeight >= clip.Top;
            var flipped = !fitsBelow && fitsAbove;
            var top = flipped ? box.Top - gap - faceHeight : box.Bottom + gap;
            // 横は反転先が無いので右端で押し戻し、器より広ければ左端に揃える
            // Horizontally there is no flip side, so push back from the right and settle at the left when wider
            var left = Math.Max(clip.Left, Math.Min(box.Left, clip.Right - faceWidth));
            return new TutorialLabelPosition(left, top, flipped);
        }

        // アンカー自身がクリップで欠けていればラベルを出さない（Web renderOutline）
        // Skip the label while the anchor itself is partly clipped (Web renderOutline)
        public static bool ShowsLabel(TutorialClipRect anchorRect, TutorialClipRect clip)
        {
            return clip.ContainsRect(anchorRect);
        }
```
`TutorialDragGuideGate.cs`（`DragGuide.tsx` の門。`TutorialDragGuidePlacement` は同じファイルの下）:
```csharp
        public static bool TryPlace(TutorialAnchorState from, TutorialAnchorState to, out TutorialDragGuidePlacement placement)
        {
            placement = default;
            // 両端が ready で、起点の中心が起点の clip の内側にある時だけ描く
            // Draw only when both ends are ready and the origin center lies inside the origin clip
            // silent-ok: 端が未解決・起点が見えない間は Web と同じく出さない仕様 / 未解決の理由は TutorialAnchorTracker が遷移時に出し、L1 TutorialOverlayDragGuideTest が見る
            if (from is not TutorialAnchorState.Ready origin || to is not TutorialAnchorState.Ready target) return false;
            if (!origin.Clip.Contains(origin.Rect.CenterX, origin.Rect.CenterY)) return false;
            placement = new TutorialDragGuidePlacement(origin.Rect.CenterX, origin.Rect.CenterY, target.Rect.CenterX - origin.Rect.CenterX, target.Rect.CenterY - origin.Rect.CenterY);
            return true;
        }
```
`WorldPinEdgePlacement.cs`（`EdgeArrow` の逐語。`WorldPinEdgeSpot` は同じファイルの下）:
```csharp
        public static bool TryPlace(float width, float height, float edgeMargin, float directionX, float directionY, out WorldPinEdgeSpot spot)
        {
            spot = default;
            // silent-ok: 層の寸法が 0 なのはレイアウト前の 1 フレームだけで、次の GeometryChangedEvent で置き直す / L1 WorldPinOverlayTest が位置を見る
            if (width == 0 || height == 0) return false;
            // 方向を画面端の余白の内側へクランプする
            // Clamp the direction to inside the screen edge margin
            var maxX = width / 2f - edgeMargin;
            var maxY = height / 2f - edgeMargin;
            var scaleX = Math.Abs(directionX) > 0.001f ? maxX / Math.Abs(directionX) : float.MaxValue;
            var scaleY = Math.Abs(directionY) > 0.001f ? maxY / Math.Abs(directionY) : float.MaxValue;
            var scale = Math.Min(scaleX, scaleY);
            var angle = (float)(Math.Atan2(directionY, directionX) * 180.0 / Math.PI);
            spot = new WorldPinEdgeSpot(width / 2f + directionX * scale, height / 2f + directionY * scale, angle);
            return true;
        }
```

- [ ] **Step 5: 確かめてコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.Tutorial\."`
Expected: `ErrorCount: 0`、StateBook 4・Clip 4・Label 5・DragGuideGate 2・EdgePlacement 3 の 18 件 PASS
```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Tutorial moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Tutorial
git commit -m "feat(uitk): チュートリアルの枠・ラベル・DragGuide・画面端矢印の計算を Web から写す（S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: 状態（ViewModel・Projection・Model）

**Files:**
- Create: `S/Client.Ui.State/Tutorial/{TutorialElementKey.cs,TutorialTextFactory.cs}`
- Create: `S/Client.Ui.State/Tutorial/Overlay/{TutorialOutlineCue.cs,TutorialDragGuideCue.cs,TutorialOverlayViewModel.cs,TutorialOverlayProjection.cs,TutorialOverlayModel.cs}`
- Create: `S/Client.Ui.State/Tutorial/KeyControlHint/{KeyControlHintCue.cs,KeyControlHintViewModel.cs,KeyControlHintProjection.cs,KeyControlHintModel.cs}`
- Create: `S/Client.Ui.State/Tutorial/WorldPin/{WorldPinCue.cs,WorldPinViewModel.cs,WorldPinModel.cs}`
- Test: `S/Client.Tests/Ui/State/Tutorial/{TutorialOverlayProjectionTest.cs,KeyControlHintProjectionTest.cs}`

**Interfaces:**
- Consumes: Global Constraints の「状態の源」（2 store・`UIStateControl`・S2-hud の `BlockingSkitPresence`）、`ContentLocalizationKeys.ChallengeTutorialText(Guid)`、`UiText.FromContent`、Task 3 の `TutorialAnchorState`
- Produces（namespace `Client.Ui.State.Tutorial`・`.Overlay`・`.KeyControlHint`・`.WorldPin`）:
  - `static class TutorialElementKey.Of(string sessionId, string elementId) : string`（Web `tutorialElement.ts` の `session:element`。鍵の組み立ての唯一の 1 関数）
  - `static class TutorialTextFactory.TryCreate(string tutorialGuid, string context, out UiText text) : bool`（GUID を読み `UiText.FromContent(ContentLocalizationKeys.ChallengeTutorialText(guid))` を返す。読めなければ `context` 付きの `Debug.LogError` を出して false。3 つの Projection が使う唯一の 1 関数）
  - `abstract class TutorialOutlineCue`（判別 union）: `Key`・`SessionId`・`ElementId`・`AnchorId`・`PaddingPx`、`sealed class TutorialOutlineCue.Plain(string sessionId, string elementId, string anchorId, int paddingPx)`・`sealed class TutorialOutlineCue.Labeled(string sessionId, string elementId, string anchorId, int paddingPx, UiText label)`（`Label`）。`Key` は ctor で `TutorialElementKey.Of` から作る。`TutorialDragGuideCue`: `Key`・`FromAnchorId`・`ToAnchorId`。`TutorialOverlayViewModel`: `Revision`・`IReadOnlyList<TutorialOutlineCue> Outlines`・`IReadOnlyList<TutorialDragGuideCue> DragGuides`・`static Empty`
  - `TutorialOverlayProjection.Project(TutorialPresentationData data) : TutorialOverlayViewModel`（keyControl を含めない）
  - `TutorialOverlayModel : IInitializable`: `IReadOnlyReactiveProperty<TutorialOverlayViewModel> Current`
  - `KeyControlHintCue`: `Key`・`KeyName`・`Text`（`UiText`）。`KeyControlHintViewModel`: `IReadOnlyList<KeyControlHintCue> Hints`・`static Empty`
  - `KeyControlHintProjection`（インスタンス。語彙外の警告済みの名を持つ）: `Project(TutorialPresentationData data, UIStateEnum current, bool blockingSkit) : KeyControlHintViewModel`
  - `KeyControlHintModel(UIStateControl, BlockingSkitPresence) : IInitializable`: `Current`
  - `WorldPinCue`: `PinId`・`Label`（`UiText`）・`ScreenX`・`ScreenY`・`OnScreen`・`DirectionX`・`DirectionY`。`WorldPinViewModel`: `IReadOnlyList<WorldPinCue> Pins`・`static From(WorldPinPresentationData data, bool blockingSkit)`（blocking なら 0 件）
  - `WorldPinModel(BlockingSkitPresence) : IInitializable`: `Current`

- [ ] **Step 1: 鍵と文の helper を書く**

`TutorialElementKey.cs`・`TutorialTextFactory.cs`:
```csharp
    public static class TutorialElementKey
    {
        // Web の tutorialElement.ts と同じ「session:element」
        // Same "session:element" as Web tutorialElement.ts
        public static string Of(string sessionId, string elementId)
        {
            return $"{sessionId}:{elementId}";
        }
    }

    public static class TutorialTextFactory
    {
        public static bool TryCreate(string tutorialGuid, string context, out UiText text)
        {
            text = null;
            // マスタ由来の GUID が読めなければ、その 1 件を出さず開発者に理由を出す
            // When the master GUID cannot be parsed, skip that one item and tell developers why
            if (!Guid.TryParse(tutorialGuid, out var guid))
            {
                Debug.LogError($"[Tutorial] tutorialGuid is not a GUID: '{tutorialGuid}' ({context})");
                return false;
            }
            text = UiText.FromContent(ContentLocalizationKeys.ChallengeTutorialText(guid));
            return true;
        }
    }
```

- [ ] **Step 2: 失敗するテストを書く**

`TutorialOverlayProjectionTest.cs`:
```csharp
using System;
using Client.Game.InGame.Tutorial;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// チュートリアルの演出データから枠と DragGuide の ViewModel を作ることを確かめる（L0）
    /// Verifies the overlay view model is projected from tutorial presentation data (L0)
    /// </summary>
    public class TutorialOverlayProjectionTest
    {
        public static readonly Guid LabelGuid = new("11111111-2222-3333-4444-555555555555");

        public static TutorialPresentationData Data(int revision, params TutorialOverlayElementData[] elements)
        {
            return new TutorialPresentationData { Revision = revision, Sessions = new[] { new TutorialSessionData { TutorialSessionId = "s1", ChallengeId = "c1", Elements = elements } } };
        }

        public static TutorialOutlineElementData Outline(string elementId, string anchorId, string labelGuid)
        {
            return new TutorialOutlineElementData { ElementId = elementId, AnchorId = anchorId, PaddingPx = 8, LabelTutorialGuid = labelGuid };
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">keyControlが混在してもoutline描画件数は変わらない</web-test>
        [Test]
        public void keyControlが混ざっても枠の件数は変わらない()
        {
            var keyControl = new TutorialKeyControlElementData { ElementId = "k1", TutorialGuid = LabelGuid.ToString(), KeyName = "Tab", UiState = "GameScreen" };
            var model = TutorialOverlayProjection.Project(Data(3, Outline("o1", "pause.save", null), keyControl));

            Assert.AreEqual(1, model.Outlines.Count);
            Assert.AreEqual(0, model.DragGuides.Count);
            Assert.AreEqual("s1:o1", model.Outlines[0].Key);
            Assert.AreEqual(3, model.Revision);
        }

        [Test]
        public void ラベルのGUIDがある枠だけがcontentの文を持つ()
        {
            var model = TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()), Outline("o2", "pause.back", "")));

            var labeled = (TutorialOutlineCue.Labeled)model.Outlines[0];
            Assert.AreEqual(UiTextSource.Content, labeled.Label.Source);
            Assert.AreEqual(UiText.FromContent(ContentLocalizationKeys.ChallengeTutorialText(LabelGuid)).Resolve(), labeled.Label.Resolve());
            Assert.IsInstanceOf<TutorialOutlineCue.Plain>(model.Outlines[1]);
        }

        [Test]
        public void 読めないラベルのGUIDは理由をLogErrorに出しラベル無しで枠を出す()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("tutorialGuid is not a GUID: 'not-a-guid'"));
            var model = TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", "not-a-guid")));
            Assert.AreEqual(1, model.Outlines.Count);
            Assert.IsInstanceOf<TutorialOutlineCue.Plain>(model.Outlines[0]);
        }
    }
}
```
（`UiText` の `ContentKey` は private なので、種（`Source`）と解いた文で比べる。using に `Client.Ui.State.Text`・`Mooresmaster.Localization.Generated` を足す）

`KeyControlHintProjectionTest.cs`:
```csharp
        [Test]
        public void 語彙外のuiStateは一度だけ警告しどの画面とも一致させない()
        {
            var data = TutorialOverlayProjectionTest.Data(1, new TutorialKeyControlElementData { ElementId = "k1", TutorialGuid = Guid.NewGuid().ToString(), KeyName = "Tab", UiState = "GameScreeen" });
            var projection = new KeyControlHintProjection();

            LogAssert.Expect(LogType.Warning, new Regex("Unknown keyControl uiState: GameScreeen"));
            Assert.AreEqual(0, projection.Project(data, UIStateEnum.GameScreen, false).Hints.Count);
            Assert.AreEqual(0, projection.Project(data, UIStateEnum.GameScreen, false).Hints.Count);
            LogAssert.NoUnexpectedReceived();
        }
```

Run: `uloop compile --project-path ./moorestech_client`
Expected: 新しい型が無いのでコンパイルエラー

- [ ] **Step 3: ViewModel と Projection を書く**

`TutorialOverlayProjection.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Tutorial.Overlay
{
    /// <summary>
    /// 演出データから枠と DragGuide だけを取り出す。keyControl は KeyControlHint の持ち物
    /// Extracts only outlines and drag guides from presentation data; keyControl belongs to KeyControlHint
    /// </summary>
    public static class TutorialOverlayProjection
    {
        public static TutorialOverlayViewModel Project(TutorialPresentationData data)
        {
            var outlines = new List<TutorialOutlineCue>();
            var dragGuides = new List<TutorialDragGuideCue>();
            foreach (var session in data.Sessions)
            {
                foreach (var element in session.Elements)
                {
                    switch (element)
                    {
                        case TutorialOutlineElementData outline:
                            outlines.Add(CreateOutline(session.TutorialSessionId, outline));
                            break;
                        case TutorialDragGuideElementData dragGuide:
                            dragGuides.Add(new TutorialDragGuideCue(TutorialElementKey.Of(session.TutorialSessionId, dragGuide.ElementId), dragGuide.FromAnchorId, dragGuide.ToAnchorId));
                            break;
                    }
                }
            }
            return new TutorialOverlayViewModel(data.Revision, outlines, dragGuides);

            #region Internal

            TutorialOutlineCue CreateOutline(string sessionId, TutorialOutlineElementData outline)
            {
                // ラベルの GUID が無いか読めなければ枠だけ（読めない時は TutorialTextFactory が LogError）
                // Without a label GUID, or with an unreadable one, show the outline alone (TutorialTextFactory logs the latter)
                if (string.IsNullOrEmpty(outline.LabelTutorialGuid)) return new TutorialOutlineCue.Plain(sessionId, outline.ElementId, outline.AnchorId, outline.PaddingPx);
                var context = $"labelTutorialGuid {TutorialElementKey.Of(sessionId, outline.ElementId)}";
                if (!TutorialTextFactory.TryCreate(outline.LabelTutorialGuid, context, out var label)) return new TutorialOutlineCue.Plain(sessionId, outline.ElementId, outline.AnchorId, outline.PaddingPx);
                return new TutorialOutlineCue.Labeled(sessionId, outline.ElementId, outline.AnchorId, outline.PaddingPx, label);
            }

            #endregion
        }
    }
}
```
`TutorialOutlineCue`（abstract の基底に readonly フィールドと private ctor、入れ子の sealed `Plain`・`Labeled`。前例 `TutorialAnchorResolution`）、`TutorialDragGuideCue`・`TutorialOverlayViewModel`（`Empty = new(0, Array.Empty…)`）は readonly フィールドの不変クラスで書く。

`KeyControlHintProjection.cs`:
```csharp
    public sealed class KeyControlHintProjection
    {
        private readonly HashSet<string> _warnedUiStates = new();

        public KeyControlHintViewModel Project(TutorialPresentationData data, UIStateEnum current, bool blockingSkit)
        {
            // blocking のスキット中はスキットに画面を譲る
            // Yield the screen to a blocking skit
            if (blockingSkit) return KeyControlHintViewModel.Empty;
            var hints = new List<KeyControlHintCue>();
            foreach (var session in data.Sessions)
            {
                foreach (var element in session.Elements)
                {
                    if (element is not TutorialKeyControlElementData keyControl || !Matches(keyControl.UiState)) continue;
                    var key = TutorialElementKey.Of(session.TutorialSessionId, element.ElementId);
                    if (!TutorialTextFactory.TryCreate(keyControl.TutorialGuid, $"keyControl {key}", out var text)) continue;
                    hints.Add(new KeyControlHintCue(key, keyControl.KeyName, text));
                }
            }
            return new KeyControlHintViewModel(hints);

            #region Internal

            bool Matches(string uiState)
            {
                // 語彙外の綴りはどの画面とも一致させず、1 回だけ警告する（Web matchesUiState）
                // An out-of-vocabulary spelling matches no screen and warns once (Web matchesUiState)
                if (Enum.TryParse<UIStateEnum>(uiState, false, out var parsed) && Enum.IsDefined(typeof(UIStateEnum), parsed)) return parsed == current;
                if (_warnedUiStates.Add(uiState)) Debug.LogWarning($"[tutorial] Unknown keyControl uiState: {uiState}");
                return false;
            }

            #endregion
        }
    }
```
（Web は文が空のヒントを出さないが、UITK の辞書は常に読み込み済みで欠けは `[!key]` の可視文字列になるので空の分岐を持たない。D-12）

`WorldPinViewModel.From`:
```csharp
        public static WorldPinViewModel From(WorldPinPresentationData data, bool blockingSkit)
        {
            // ピンは会話窓より上に来るので、blocking のスキット中は引っ込む（Web WorldPinOverlay）
            // Pins paint above the dialogue window, so they withdraw during a blocking skit (Web WorldPinOverlay)
            if (blockingSkit) return Empty;
            var pins = new List<WorldPinCue>(data.Pins.Length);
            foreach (var pin in data.Pins)
            {
                if (!TutorialTextFactory.TryCreate(pin.TutorialGuid, $"worldPin {pin.PinId}", out var label)) continue;
                pins.Add(new WorldPinCue(pin.PinId, label, pin.ScreenX, pin.ScreenY, pin.OnScreen, pin.DirectionX, pin.DirectionY));
            }
            return new WorldPinViewModel(pins);
        }
```

- [ ] **Step 4: Model を書く**

`TutorialOverlayModel.cs`・`WorldPinModel.cs`・`KeyControlHintModel.cs` は `PauseMenuModel` の形（ctor で `ReactiveProperty<VM>` を現在値で作り、`Initialize` で源を購読して作り直す）:
```csharp
    public sealed class KeyControlHintModel : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly BlockingSkitPresence _blockingSkit;
        private readonly KeyControlHintProjection _projection = new();
        private readonly ReactiveProperty<KeyControlHintViewModel> _current = new(KeyControlHintViewModel.Empty);

        public IReadOnlyReactiveProperty<KeyControlHintViewModel> Current => _current;

        public KeyControlHintModel(UIStateControl uiStateControl, BlockingSkitPresence blockingSkit)
        {
            _uiStateControl = uiStateControl;
            _blockingSkit = blockingSkit;
        }

        public void Initialize()
        {
            // 演出・画面状態・スキットのどれが変わっても組み直す
            // Rebuild whenever the presentation, the UI state or the skit changes
            TutorialPresentationStateStore.Instance.ObserveChanged().Subscribe(_ => Rebuild());
            _uiStateControl.OnStateChanged.Subscribe(_ => Rebuild());
            _blockingSkit.IsBlocking.Subscribe(_ => Rebuild());
        }

        private void Rebuild()
        {
            _current.Value = _projection.Project(TutorialPresentationStateStore.Instance.GetCurrent(), _uiStateControl.CurrentState, _blockingSkit.IsBlocking.Value);
        }
    }
```

- [ ] **Step 5: 確かめてコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.Tutorial\."`
Expected: `ErrorCount: 0`、Task 3 の 18 件＋Projection 3・KeyControlProjection 1 の 22 件 PASS
```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Tutorial
git commit -m "feat(uitk): チュートリアル・キー操作ヒント・ワールドピンの状態モデルを作る（blocking スキットは S2-hud の BlockingSkitPresence を使う。S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: アンカーの追跡と脈動（`Screens/Tutorial/Anchor/`・`Pulse/`）

**Files:**
- Create: `S/Client.Ui/Screens/Tutorial/Anchor/{ITutorialAnchorSubscriptions.cs,TutorialAnchorChange.cs,TutorialAnchorTracker.cs,TutorialAnchorMeasure.cs,TutorialAncestorClipWalker.cs}`
- Create: `S/Client.Ui/Foundation/Motion/Steps/ScaleTweenStep.cs`（指揮の割り当て C。既存の Steps は触らない・A-63）
- Create: `S/Client.Ui/Screens/Tutorial/Pulse/{TutorialAttentionPulse.cs,TutorialPulseBinding.cs}`
- Test: `S/Client.Tests/Ui/Screens/Tutorial/Overlay/{TutorialOverlayTestHost.cs,TutorialAnchorTrackerTest.cs}`・`S/Client.Tests/Ui/Motion/ScaleTweenStepTest.cs`（`TutorialPulseBindingTest.cs` は本番の `TutorialOverlay.uss` を読むので Task 6 で足す。D-34）

**Interfaces:**
- Consumes: Task 2 の `TutorialAnchorRegistry.Resolve`・`TutorialAnchor.ClipContainerClassName`、Task 3 の `TutorialAnchorState`・`TutorialAnchorStateBook`・`TutorialClipRect`、`UiMotionPlayer.Play`・`UiTweenAnimation`・`UiMotionSpec`・`UiMotionLoop.PingPong`・`UiEasing.EaseInOutSine`・`UssCustomStyleReader.TryReadMilliseconds`
- Produces（namespace `Client.Ui.Screens.Tutorial.Anchor`・`.Pulse`・`Client.Ui.Foundation.Motion.Steps`）:
  - 読みの口は Task 3 の `ITutorialAnchorStateLookup.TryGet` を追跡器も実装する（読みの口は 1 本。View が持つ。D-23）
  - 変えの口 `interface ITutorialAnchorSubscriptions { IObservable<TutorialAnchorChange> OnAnchorChanged { get; } void SetSubscribedAnchors(IReadOnlyCollection<string> anchorIds); void StopTracking(); }`（Presenter だけが持つ。`StopTracking` は購読を全部外して毎フレームの測定を止め、状態を捨てる。Presenter の `Mount` が View の `DetachFromPanelEvent` で呼ぶ。D-32）、`TutorialAnchorChange(string anchorId, TutorialAnchorState state)`
  - `TutorialAnchorTracker(VisualElement searchRoot, VisualElement coordinateSpace) : ITutorialAnchorStateLookup, ITutorialAnchorSubscriptions`（購読が 1 件以上の間だけ `coordinateSpace.schedule.Execute(...).Every(0)` で毎フレーム測る。遷移時だけ: 重複 `Debug.LogError`・未発見 `Debug.LogWarning`）
  - `TutorialAnchorMeasure.Measure(VisualElement searchRoot, VisualElement coordinateSpace, string anchorId) : TutorialAnchorState`（理由の順は Web `resolveAnchor.ts`: display-none → visibility-hidden → zero-area → outside-viewport。aria-hidden は UITK に無いので持たない。`worldBound` が NaN（レイアウト前）なら `Hidden(ZeroArea)`。D-33）
  - `TutorialAncestorClipWalker.ClipOf(VisualElement anchor, VisualElement coordinateSpace) : TutorialClipRect`（祖先のうち `ScrollView.viewportUssClassName`・`TutorialAnchor.ClipContainerClassName`・インライン `style.overflow == Overflow.Hidden` を持つものの padding box を交差。UITK の overflow は absolute の子も切るので Web の脱出則は要らない。D-14）
  - `TutorialAttentionPulse`: `const float StrongScale = 1.08f`・`const float SubtleScale = 1.03f`・`const string PeriodPropertyName = "--tutorial-attention-pulse-period"`・`static UiMotionHandle Start(VisualElement target, float peakScale, float periodMilliseconds)`
  - `ScaleTweenStep(VisualElement target, float from, float to) : IUiTweenStep`（前例 `OpacityTweenStep(target, from, to)` と同形の汎用ステップ。脈動の語彙は持たない）
  - `TutorialPulseBinding(VisualElement periodHost)`・`void Attach(VisualElement target, float peakScale)`（画面ルートに 1 つ。前例 `ToastHostView`: ルートの `CustomStyleResolvedEvent` で読んだ周期を持っておき、後から `Attach` した子にも当てる。周期の解決前の子は待たせる。子はパネルに載っている間だけ脈動し（`AttachToPanelEvent` で始め `DetachFromPanelEvent` で止める）、周期が変わったら載っている子だけ流し直す。外した子へ `Play` しない）

- [ ] **Step 1: 失敗するテストを書く**

`TutorialOverlayTestHost.cs`（このディレクトリの L1 の土台）:
```csharp
using System.Collections.Generic;
using System.Globalization;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Screens.Tutorial.Anchor;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 枠の L1 の土台。テストのパネルに印付きの要素を置き、本物の追跡器で測らせる
    /// Base for overlay L1 tests; places marked elements on the test panel and measures them with the real tracker
    /// </summary>
    public abstract class TutorialOverlayTestHost : ScreenTestHost
    {
        protected sealed class FakeTutorialOverlayModel : FakeModel<TutorialOverlayViewModel>
        {
            public FakeTutorialOverlayModel() : base(TutorialOverlayViewModel.Empty) { }
        }

        protected VisualElement MountAnchor(VisualElement parent, string anchorId, float left, float top, float width, float height)
        {
            var anchor = new VisualElement { style = { position = Position.Absolute, left = left, top = top, width = width, height = height } };
            TutorialAnchor.Mark(anchor, anchorId);
            parent.Add(anchor);
            return anchor;
        }

        private readonly List<TutorialAnchorTracker> _trackers = new();

        protected TutorialAnchorTracker CreateTracker()
        {
            var tracker = new TutorialAnchorTracker(rootVisualElement, rootVisualElement);
            _trackers.Add(tracker);
            return tracker;
        }

        [TearDown]
        public void StopTrackingAfterTest()
        {
            // 毎フレームの測定を次のテストへ持ち越さない（前のテストの Warning が次の LogAssert を汚す）
            // Never carry per-frame measuring into the next test (a stale Warning would pollute its LogAssert)
            foreach (var tracker in _trackers) tracker.StopTracking();
            _trackers.Clear();
            // Mount した画面は外れると Presenter が DetachFromPanelEvent で追跡を止める
            // Mounted screens stop their own trackers on DetachFromPanelEvent once removed
            rootVisualElement.Clear();
        }

        protected static float Px(string tokenName)
        {
            return ThemeTokenFile.ResolvePixels(tokenName);
        }

        protected static float Ms(string tokenName)
        {
            // 周期のトークンは "1200ms" の形で書かれている
            // Duration tokens are written like "1200ms"
            return float.Parse(ThemeTokenFile.Resolve(tokenName).Replace("ms", string.Empty), CultureInfo.InvariantCulture);
        }

        protected static IReadOnlyCollection<string> Ids(params string[] anchorIds)
        {
            return anchorIds;
        }
    }
}
```
`TutorialAnchorTrackerTest.cs`:
```csharp
        [Test]
        public void 載っているアンカーはreadyで矩形と画面のclipを持つ()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("pause.save"));
            simulate.FrameUpdate();

            Assert.IsTrue(tracker.TryGet("pause.save", out var state));
            Assert.IsInstanceOf<TutorialAnchorState.Ready>(state);
            var ready = (TutorialAnchorState.Ready)state;
            Assert.AreEqual(new TutorialClipRect(100, 50, 300, 90), ready.Rect);
            Assert.AreEqual(new TutorialClipRect(0, 0, 1280, 720), ready.Clip);
        }

        [Test]
        public void 重複は遷移した時に一度だけLogErrorを出す()
        {
            MountAnchor(rootVisualElement, "pause.save", 0, 0, 10, 10);
            MountAnchor(rootVisualElement, "pause.save", 20, 0, 10, 10);
            var tracker = CreateTracker();
            LogAssert.Expect(LogType.Error, new Regex(@"\[TutorialAnchorTracker\] anchor is duplicated \(2\): pause\.save"));
            tracker.SetSubscribedAnchors(Ids("pause.save"));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 未発見は遷移した時に一度だけ警告し毎フレームは出さない()
        {
            var tracker = CreateTracker();
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TutorialAnchorTracker\] anchor is missing: pause\.back"));
            tracker.SetSubscribedAnchors(Ids("pause.back"));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 非表示と面積0と画面外は理由を分けて持つ()
        {
            var hidden = MountAnchor(rootVisualElement, "pause.save", 0, 0, 10, 10);
            hidden.style.display = DisplayStyle.None;
            MountAnchor(rootVisualElement, "pause.back", 0, 0, 0, 10);
            MountAnchor(rootVisualElement, "pause.settings", 2000, 0, 10, 10);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("pause.save", "pause.back", "pause.settings"));
            simulate.FrameUpdate();

            tracker.TryGet("pause.save", out var a);
            tracker.TryGet("pause.back", out var b);
            tracker.TryGet("pause.settings", out var c);
            Assert.AreEqual((TutorialAnchorReason.DisplayNone, TutorialAnchorReason.ZeroArea, TutorialAnchorReason.OutsideViewport), (a.Reason, b.Reason, c.Reason));
        }

        [Test]
        public void ScrollViewの内側のアンカーはviewportの矩形でclipされる()
        {
            var scroll = new ScrollView { style = { position = Position.Absolute, left = 0, top = 100, width = 400, height = 200 } };
            rootVisualElement.Add(scroll);
            var anchor = new VisualElement { style = { height = 40 } };
            TutorialAnchor.Mark(anchor, "research.node-a");
            scroll.Add(anchor);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("research.node-a"));
            simulate.FrameUpdate();

            tracker.TryGet("research.node-a", out var state);
            Assert.IsInstanceOf<TutorialAnchorState.Ready>(state);
            var ready = (TutorialAnchorState.Ready)state;
            Assert.AreEqual(100f, ready.Clip.Top);
            Assert.AreEqual(300f, ready.Clip.Bottom);
        }

        [Test]
        public void 脈動は周期の半分で頂点に達し元へ戻る()
        {
            var target = new VisualElement();
            rootVisualElement.Add(target);
            var handle = TutorialAttentionPulse.Start(target, TutorialAttentionPulse.StrongScale, 1200f);
            // UiMotionHandle は Play の次のスケジューラ呼び出しを開始時刻にする（前例 UiMotionPlayerTest）
            // UiMotionHandle starts at the next scheduler call after Play (precedent UiMotionPlayerTest)
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(600);
            Assert.AreEqual(1.08f, target.resolvedStyle.scale.value.x, 0.005f);
            simulate.FrameUpdateMs(600);
            Assert.AreEqual(1f, target.resolvedStyle.scale.value.x, 0.005f);
            handle.Cancel();
        }

        [Test]
        public void レイアウト前のアンカーはreadyにせず測れた次のフレームでreadyにする()
        {
            // 足した直後はまだレイアウトされず worldBound が NaN になりうる（NaN を矩形として通さない。D-33）
            // Right after Add the anchor is not laid out and worldBound can be NaN (never pass NaN as a rect; D-33)
            MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("pause.save"));
            Assert.IsTrue(tracker.TryGet("pause.save", out var before));
            Assert.IsInstanceOf<TutorialAnchorState.Hidden>(before);
            Assert.AreEqual(TutorialAnchorReason.ZeroArea, before.Reason);

            simulate.FrameUpdate();
            tracker.TryGet("pause.save", out var after);
            Assert.IsInstanceOf<TutorialAnchorState.Ready>(after);
        }

        [Test]
        public void 追跡を止めた後は測らず報告もしない()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("pause.save"));
            simulate.FrameUpdate();

            // 止めた後にアンカーが消えても、未発見の Warning を出さず状態も残さない
            // After stopping, a vanished anchor yields no missing Warning and no kept state
            tracker.StopTracking();
            anchor.RemoveFromHierarchy();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
            Assert.IsFalse(tracker.TryGet("pause.save", out _));
        }
```
（1 件目は、足した直後の `worldBound` が NaN でなく 0 の実物でも、面積 0 の判定で同じ結果になる。`ScrollView` のテストで子の幅が 0 になるなら `anchor.style.width = 100` を足す。`simulate.FrameUpdateMs` の名は C2-c/F1 の Motion テストの実物に合わせる）

Run: `uloop compile --project-path ./moorestech_client`
Expected: `Client.Ui.Screens.Tutorial` が無いのでコンパイルエラー

- [ ] **Step 2: 測定とクリップの歩きを書く**

`TutorialAnchorMeasure.cs`:
```csharp
        public static TutorialAnchorState Measure(VisualElement searchRoot, VisualElement coordinateSpace, string anchorId)
        {
            VisualElement element;
            switch (TutorialAnchorRegistry.Resolve(searchRoot, anchorId))
            {
                case TutorialAnchorResolution.Missing:
                    return new TutorialAnchorState.NotFound(TutorialAnchorReason.Missing);
                case TutorialAnchorResolution.Duplicate:
                    return new TutorialAnchorState.NotFound(TutorialAnchorReason.DuplicateAnchor);
                case TutorialAnchorResolution.Unique unique:
                    element = unique.Element;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(anchorId), $"unknown resolution for {anchorId}");
            }

            // 理由の順は Web resolveAnchor と同じ（表示 → 可視 → 面積 → 画面内）
            // Same reason order as Web resolveAnchor (display, visibility, area, viewport)
            if (IsDisplayNoneInHierarchy(element)) return new TutorialAnchorState.Hidden(TutorialAnchorReason.DisplayNone);
            if (element.resolvedStyle.visibility == Visibility.Hidden) return new TutorialAnchorState.Hidden(TutorialAnchorReason.VisibilityHidden);
            // レイアウト前の worldBound は NaN になりうる。比較 <= 0 を NaN がすり抜けるので、面積 0 として先に返す（D-33）
            // worldBound can be NaN before layout; NaN slips past the <= 0 check, so return zero area first (D-33)
            var world = element.worldBound;
            if (float.IsNaN(world.x) || float.IsNaN(world.y) || float.IsNaN(world.width) || float.IsNaN(world.height)) return new TutorialAnchorState.Hidden(TutorialAnchorReason.ZeroArea);
            var bound = coordinateSpace.WorldToLocal(world);
            var rect = new TutorialClipRect(bound.xMin, bound.yMin, bound.xMax, bound.yMax);
            if (rect.Width <= 0 || rect.Height <= 0) return new TutorialAnchorState.Hidden(TutorialAnchorReason.ZeroArea);
            var viewport = new TutorialClipRect(0, 0, coordinateSpace.layout.width, coordinateSpace.layout.height);
            if (rect.IsDisjoint(viewport)) return new TutorialAnchorState.Hidden(TutorialAnchorReason.OutsideViewport);
            return new TutorialAnchorState.Ready(rect, TutorialAncestorClipWalker.ClipOf(element, coordinateSpace));

            #region Internal

            bool IsDisplayNoneInHierarchy(VisualElement target)
            {
                // 祖先の display: none では子の layout が残るので、祖先まで辿って見る
                // An ancestor's display: none leaves the child's layout stale, so walk up to check
                for (var node = target; node != null; node = node.hierarchy.parent)
                {
                    if (node.resolvedStyle.display == DisplayStyle.None) return true;
                }
                return false;
            }

            #endregion
        }
```
`TutorialAncestorClipWalker.cs`:
```csharp
        public static TutorialClipRect ClipOf(VisualElement anchor, VisualElement coordinateSpace)
        {
            // 画面全体から始め、子を切る祖先の padding box を交差させる（ADR 0024）
            // Start from the whole screen and intersect the padding box of each clipping ancestor (ADR 0024)
            var clip = new TutorialClipRect(0, 0, coordinateSpace.layout.width, coordinateSpace.layout.height);
            for (var node = anchor.hierarchy.parent; node != null; node = node.hierarchy.parent)
            {
                if (!ClipsChildren(node)) continue;
                var bound = coordinateSpace.WorldToLocal(node.worldBound);
                var style = node.resolvedStyle;
                clip = clip.Intersect(new TutorialClipRect(bound.xMin + style.borderLeftWidth, bound.yMin + style.borderTopWidth, bound.xMax - style.borderRightWidth, bound.yMax - style.borderBottomWidth));
            }
            return clip;

            #region Internal

            bool ClipsChildren(VisualElement node)
            {
                // USS の overflow は C# から読めないので、ScrollView の viewport・画面の印・インラインの指定で判定する
                // USS overflow is unreadable from C#, so use the ScrollView viewport, the screen's mark or the inline style
                if (node.ClassListContains(ScrollView.viewportUssClassName)) return true;
                if (node.ClassListContains(TutorialAnchor.ClipContainerClassName)) return true;
                return node.style.overflow.keyword == StyleKeyword.Undefined && node.style.overflow.value == Overflow.Hidden;
            }

            #endregion
        }
```

- [ ] **Step 3: 追跡器を書く**

`TutorialAnchorTracker.cs`:
```csharp
    public sealed class TutorialAnchorTracker : ITutorialAnchorStateLookup, ITutorialAnchorSubscriptions
    {
        private readonly VisualElement _searchRoot;
        private readonly VisualElement _coordinateSpace;
        private readonly TutorialAnchorStateBook _book = new();
        private readonly HashSet<string> _subscribed = new();
        private readonly Subject<TutorialAnchorChange> _onAnchorChanged = new();
        private IVisualElementScheduledItem _poll;

        public IObservable<TutorialAnchorChange> OnAnchorChanged => _onAnchorChanged;

        public TutorialAnchorTracker(VisualElement searchRoot, VisualElement coordinateSpace)
        {
            _searchRoot = searchRoot;
            _coordinateSpace = coordinateSpace;
        }

        public void SetSubscribedAnchors(IReadOnlyCollection<string> anchorIds)
        {
            // 購読の入れ替えでは続くアンカーの状態を保ち、外れたものだけ捨てる
            // Swapping subscriptions keeps continuing anchors and drops only the removed ones
            _subscribed.Clear();
            _subscribed.UnionWith(anchorIds);
            _book.KeepOnly(anchorIds);
            MeasureAll();
            // 購読がある間だけ毎フレーム測る（plan §4.5。木の変化を待たずスクロール・移動にも追従する）
            // Measure every frame only while something is subscribed (plan §4.5; follows scroll and moves too)
            _poll ??= _coordinateSpace.schedule.Execute(MeasureAll).Every(0);
            if (_subscribed.Count == 0) _poll.Pause();
            else _poll.Resume();
        }

        public void StopTracking()
        {
            // 画面が外れたら測定を止めて状態を捨てる（載せ直したら Presenter が購読をやり直す。D-32）
            // Once the screen detaches, stop measuring and drop states (the Presenter resubscribes on re-attach; D-32)
            _subscribed.Clear();
            _book.KeepOnly(Array.Empty<string>());
            _poll?.Pause();
        }

        public bool TryGet(string anchorId, out TutorialAnchorState state)
        {
            return _book.TryGet(anchorId, out state);
        }

        private void MeasureAll()
        {
            foreach (var anchorId in _subscribed)
            {
                var state = TutorialAnchorMeasure.Measure(_searchRoot, _coordinateSpace, anchorId);
                if (!_book.ApplyIfChanged(anchorId, state)) continue;
                ReportTransition(anchorId, state);
                _onAnchorChanged.OnNext(new TutorialAnchorChange(anchorId, state));
            }
        }

        private void ReportTransition(string anchorId, TutorialAnchorState state)
        {
            // 遷移した時だけ開発者へ出す。重複は印の付け間違いなのでエラー、未発見は未移植の画面でも起きるので警告
            // Report only on transitions; duplicates are marking bugs (error), missing can be an unported screen (warning)
            if (state.Reason == TutorialAnchorReason.DuplicateAnchor)
            {
                // 件数は状態に持たせず（比較は理由で足りる）、報告の時だけ引き直す
                // The count is not kept in the state (the reason suffices for comparison); re-resolve only to report
                var count = TutorialAnchorRegistry.Resolve(_searchRoot, anchorId) is TutorialAnchorResolution.Duplicate duplicate ? duplicate.Count : 0;
                Debug.LogError($"[TutorialAnchorTracker] anchor is duplicated ({count}): {anchorId}");
            }
            if (state.Reason == TutorialAnchorReason.Missing) Debug.LogWarning($"[TutorialAnchorTracker] anchor is missing: {anchorId}");
        }
    }
```

- [ ] **Step 4: 脈動を書く**

`S/Client.Tests/Ui/Motion/ScaleTweenStepTest.cs`（先に書いて赤を見る。前例 `Client.Tests/Ui/Motion` の OpacityTweenStep のテストと同じ形）:
```csharp
    public class ScaleTweenStepTest
    {
        [TestCase(0f, 1f)]
        [TestCase(0.5f, 1.04f)]
        [TestCase(1f, 1.08f)]
        public void 進み具合に応じてfromからtoへ等方に拡大する(float progress, float expected)
        {
            var target = new VisualElement();
            new ScaleTweenStep(target, 1f, 1.08f).Apply(progress);
            Assert.AreEqual(expected, target.style.scale.value.value.x, 1e-4f);
            Assert.AreEqual(expected, target.style.scale.value.value.y, 1e-4f);
        }
    }
```
`S/Client.Ui/Foundation/Motion/Steps/ScaleTweenStep.cs`（新規ファイルだけ。既存の Steps は触らない）:
```csharp
    public sealed class ScaleTweenStep : IUiTweenStep
    {
        private readonly VisualElement _target;
        private readonly float _from;
        private readonly float _to;

        public ScaleTweenStep(VisualElement target, float from, float to)
        {
            _target = target;
            _from = from;
            _to = to;
        }

        public void Apply(float easedProgress)
        {
            var scale = Mathf.LerpUnclamped(_from, _to, easedProgress);
            _target.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }
    }
```
`TutorialAttentionPulse.cs`:
```csharp
    public static class TutorialAttentionPulse
    {
        // 振幅は Web tokens.css の @keyframes tutorial-attention-pulse-strong/subtle の 50% の値。ここが唯一の持ち主
        // Amplitudes are the 50% values of Web tokens.css keyframes; this is their only owner
        public const float StrongScale = 1.08f;
        public const float SubtleScale = 1.03f;
        public const string PeriodPropertyName = "--tutorial-attention-pulse-period";

        public static UiMotionHandle Start(VisualElement target, float peakScale, float periodMilliseconds)
        {
            // 0%・100% が 1、50% が頂点の ease-in-out を、半周期の往復（EaseInOutSine）で写す
            // Copy the ease-in-out 1 → peak → 1 keyframes as a half-period ping-pong with EaseInOutSine
            var spec = new UiMotionSpec(periodMilliseconds / 2f, 0f, UiEasing.EaseInOutSine, UiMotionLoop.PingPong);
            return UiMotionPlayer.Play(target, new UiTweenAnimation(spec, new ScaleTweenStep(target, 1f, peakScale)));
        }
    }
```
`TutorialPulseBinding.cs`（前例 `ToastHostView`: ルートで読んだ値を持ち、後から来た子へ当てる）:
```csharp
    public sealed class TutorialPulseBinding
    {
        private readonly Dictionary<VisualElement, float> _peakScales = new();
        private readonly Dictionary<VisualElement, UiMotionHandle> _handles = new();
        private float _periodMilliseconds;
        private bool _periodResolved;

        public TutorialPulseBinding(VisualElement periodHost)
        {
            periodHost.RegisterCallback<CustomStyleResolvedEvent>(evt => OnPeriodResolved(evt.customStyle));
        }

        public void Attach(VisualElement target, float peakScale)
        {
            // 載っている間だけ流す。載る前・外した後には Play しない（UiMotionPlayer はパネル外で LogError）
            // Pulse only while attached; never Play before attach or after removal (UiMotionPlayer logs off-panel)
            _peakScales[target] = peakScale;
            target.RegisterCallback<AttachToPanelEvent>(_ => Start(target));
            target.RegisterCallback<DetachFromPanelEvent>(_ => Stop(target));
            if (target.panel != null) Start(target);
        }

        private void OnPeriodResolved(ICustomStyle customStyle)
        {
            if (!UssCustomStyleReader.TryReadMilliseconds(customStyle, TutorialAttentionPulse.PeriodPropertyName, out var period)) return;
            if (_periodResolved && period == _periodMilliseconds) return;
            _periodMilliseconds = period;
            _periodResolved = true;
            // 周期が変わったら、いま載っている子だけを流し直す
            // When the period changes, restart only the children currently attached
            foreach (var target in _peakScales.Keys)
            {
                if (target.panel == null) continue;
                Stop(target);
                Start(target);
            }
        }

        private void Start(VisualElement target)
        {
            // silent-ok: 周期の解決前は待ち、解決時に OnPeriodResolved が載っている子を流す / L1 TutorialPulseBindingTest が見る
            if (!_periodResolved || _handles.ContainsKey(target)) return;
            _handles[target] = TutorialAttentionPulse.Start(target, _peakScales[target], _periodMilliseconds);
        }

        private void Stop(VisualElement target)
        {
            if (!_handles.TryGetValue(target, out var handle)) return;
            handle.Cancel();
            _handles.Remove(target);
        }
    }
```
（`TryReadMilliseconds` が false のときは Reader 自身が LogError を出すので、ここは脈動なしで続ける。`// silent-ok:` は Reader の LogError が経路なので付けない。周期の受け直しは各画面 USS のルートで `--tutorial-attention-pulse-period: var(--tutorial-pulse-duration);`。外した子は辞書に残るが、ゲーム寿命の画面の中で照合が作り直す数に限られる（AGENTS.md の既知の制約））

- [ ] **Step 5: 確かめてコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Screens\.Tutorial\.Overlay\.TutorialAnchorTrackerTest|Motion\.ScaleTweenStepTest)"`
Expected: `ErrorCount: 0`、TrackerTest 8 件・ScaleTweenStepTest 3 件 PASS。`ScrollView` の clip で viewport のクラス名が違って落ちたら、`ScrollView.viewportUssClassName` の実値を `done.md` の「証拠」に書いて判定を合わせる
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Tutorial moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/Steps/ScaleTweenStep.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Tutorial moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion/ScaleTweenStepTest.cs
git commit -m "feat(uitk): アンカーの追跡（祖先クリップ・遷移時だけの報告）と注意の脈動（Foundation の ScaleTweenStep）を作る（S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: 枠とラベルの画面（`Screens/Tutorial/Overlay/`。TutorialOverlay の anchor 2・labels 4・clipping 3・tutorialHighlightClip 1）

**Files:**
- Create: `S/Client.Ui/Screens/Tutorial/Overlay/{TutorialOverlayPresenter.cs,TutorialOverlayView.cs,TutorialOutlineView.cs,TutorialHighlightLabelView.cs,TutorialOverlay.uss}`（Presenter は購読を持つのでここで作る。DI 登録だけ Task 12）
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に `TutorialOverlay.uss` の 1 行。D-34）
- Test: `S/Client.Tests/Ui/Screens/Tutorial/Overlay/{TutorialOverlayAnchorTest.cs,TutorialOverlayLabelTest.cs,TutorialOverlayClipTest.cs,TutorialHighlightAncestorClipTest.cs,TutorialPulseBindingTest.cs}`

**Interfaces:**
- Consumes: Task 3〜5 の全部、`LocalizedLabel()`・`SetText(UiText)`、`UssCustomStyleReader.TryReadPixels`
- Produces（namespace `Client.Ui.Screens.Tutorial.Overlay`）:
  - `TutorialOverlayPresenter(GameUiRoot gameUiRoot, TutorialOverlayModel model) : IInitializable` と `static TutorialOverlayView Mount(VisualElement layer, VisualElement searchRoot, IReadOnlyReactiveProperty<TutorialOverlayViewModel> model)`（購読は全部ここ: model → `tracker.SetSubscribedAnchors`・`view.Render`、アンカーの変化 → `view.RedrawAnchor`。`Initialize` と L1 が同じ `Mount` を通る。View が外れたら `tracker.StopTracking()`、載せ直したら今の ViewModel で購読し直す（D-32）。ack は送らない（D-10）。D-22）
  - `TutorialOverlayView(ITutorialAnchorStateLookup anchors) : VisualElement`（`name = "tutorial-overlay"`。全面・拾わない。Model を購読しない。`void Render(TutorialOverlayViewModel model)`（照合して全部描き直す）・`void RedrawAnchor(string anchorId)`（そのアンカーを指す枠だけ描き直す）。Task 7 が DragGuide を同じ View に足す）
  - `TutorialOutlineView : VisualElement`（`name = "tutorial-outline"`・クラス `tutorial-outline`。子 `tutorial-outline__clip`（`overflow: hidden`・脈動の対象）＞ `tutorial-outline__glow`・`tutorial-outline__ring`、兄弟に `TutorialHighlightLabelView`）: `TutorialOutlineView(TutorialPulseBinding pulse)`・`void ShowRing(TutorialClipRect box, TutorialClipInset inset, float glowWidth, float ringRadius)`・`void HideRing()`・`TutorialHighlightLabelView Label`（readonly フィールド）
  - `TutorialHighlightLabelView : LocalizedLabel`（`name = "tutorial-highlight-label"`）: `void Place(UiText text, TutorialClipRect box, TutorialClipRect clip, float gap)`（`maxWidth` = clip 幅を当て、`GeometryChangedEvent` で測った面の寸法で `TutorialHighlightLabelPlacement.Place` を当てる。測るまで `visibility: hidden`）・`void HideLabel()`（木から外す。文を当てる前は木に載せない）
  - 画面ルートの受け直し（`TutorialOverlay.uss` の `.tutorial-overlay`）: `--tutorial-overlay-glow-width: var(--tutorial-highlight-glow)`・`--tutorial-overlay-label-gap: var(--tutorial-highlight-label-gap)`・`--tutorial-attention-pulse-period: var(--tutorial-pulse-duration)`・`--tutorial-overlay-ring-radius: 8px`

- [ ] **Step 1: 失敗するテストを書く**

`TutorialOverlayAnchorTest.cs`:
```csharp
using System.Linq;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Client.Tests.Ui.State.Tutorial.TutorialOverlayProjectionTest;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 枠のアンカー解決を確かめる（L1。Web TutorialOverlay.test.ts の anchor resolution。ack の 2 件は捨てる・D-10）
    /// Verifies anchor resolution of the overlay (L1; Web TutorialOverlay.test.ts anchor resolution; the two ack tests are dropped, D-10)
    /// </summary>
    public class TutorialOverlayAnchorTest : TutorialOverlayTestHost
    {
        private FakeTutorialOverlayModel _model;
        private TutorialOverlayView _view;

        [SetUp]
        public void MountOverlay()
        {
            _model = new FakeTutorialOverlayModel();
            _view = TutorialOverlayPresenter.Mount(rootVisualElement, rootVisualElement, _model.Current);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">revision更新でも購読が続くanchorの解決状態を保つ</web-test>
        [Test]
        public void revisionが変わっても続くアンカーの状態を保ち枠を出したままにする()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 200, 40);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", null))));
            simulate.FrameUpdate();

            // フレームを回さずに revision だけを進めても、枠は前の解決結果で出たまま
            // Advancing only the revision without a frame keeps the outline drawn from the previous resolution
            _model.Set(TutorialOverlayProjection.Project(Data(2, Outline("o1", "pause.save", null))));
            var outline = _view.Query<TutorialOutlineView>().First();
            Assert.IsTrue(IsShown(outline.Q("tutorial-outline__clip")));
        }

        [Test]
        public void 画面を外すとアンカーの追跡を止める()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 100, 100, 200, 40);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", null))));
            simulate.FrameUpdate();

            // 画面を外した後にアンカーが消えても未発見の Warning は出ない（測定が止まっている。D-32）
            // After the screen is removed, a vanished anchor emits no missing Warning (measuring has stopped; D-32)
            _view.RemoveFromHierarchy();
            anchor.RemoveFromHierarchy();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
```
`TutorialOverlayLabelTest.cs`（SetUp は上と同じ。`Shown<T>()` は `rootVisualElement.Query<T>().Where(e => IsShown(e)).ToList()` のローカル helper）:
```csharp
        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">labelTutorialGuid がある枠線だけラベルを描く</web-test>
        [Test]
        public void ラベルのGUIDがある枠だけラベルを出す()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 200, 40);
            MountAnchor(rootVisualElement, "pause.back", 100, 300, 200, 40);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()), Outline("o2", "pause.back", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var labels = Shown<TutorialHighlightLabelView>();
            Assert.AreEqual(1, labels.Count);
            Assert.AreEqual(Mathf.Round(100f + 40f + 8f + Gap()), Mathf.Round(labels[0].worldBound.yMin));
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">ラベルが下に収まらない時は枠線の上へ反転する</web-test>
        [Test]
        public void 下に収まらないラベルは枠の上へ反転する()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 680, 200, 30);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var label = Shown<TutorialHighlightLabelView>().Single();
            Assert.AreEqual(680f - 8f - Gap(), label.worldBound.yMax, 0.5f);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">ラベルが右端を越える時はclip内へ押し戻し、器より広い時は折り返して左端に収まる</web-test>
        [Test]
        public void 右端を越えるラベルはclipへ押し戻し器より広ければ折り返して左端に揃える()
        {
            MountAnchor(rootVisualElement, "pause.save", 1220, 100, 40, 40);
            var shortText = new TutorialOutlineCue.Labeled("s1", "o1", "pause.save", 8, UiText.FromVerbatim("右端のラベル"));
            _model.Set(new TutorialOverlayViewModel(1, new[] { shortText }, System.Array.Empty<TutorialDragGuideCue>()));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.LessOrEqual(Shown<TutorialHighlightLabelView>().Single().worldBound.xMax, 1280.5f);

            var longText = new TutorialOutlineCue.Labeled("s1", "o1", "pause.save", 8, UiText.FromVerbatim(string.Concat(Enumerable.Repeat("とても長いチュートリアルの説明文", 12))));
            _model.Set(new TutorialOverlayViewModel(2, new[] { longText }, System.Array.Empty<TutorialDragGuideCue>()));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var wrapped = Shown<TutorialHighlightLabelView>().Single();
            Assert.AreEqual(0f, wrapped.worldBound.xMin, 0.5f);
            Assert.LessOrEqual(wrapped.worldBound.width, 1280.5f);
            Assert.Greater(wrapped.worldBound.height, 30f);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">anchor未解決の枠線はラベルも描かない</web-test>
        [Test]
        public void アンカーが解けない枠はラベルも出さない()
        {
            LogAssert.Expect(LogType.Warning, new Regex("anchor is missing: pause.save"));
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();

            Assert.AreEqual(0, Shown<TutorialHighlightLabelView>().Count);
            Assert.AreEqual(0, _view.Query(className: "tutorial-outline__clip").Where(e => IsShown(e)).ToList().Count);
        }
```
（`LabelGuid` は `TutorialOverlayProjectionTest.LabelGuid`（`using static` で引く。`Outline` の第 3 引数は文字列なので `LabelGuid.ToString()` を渡す）。`Gap()` は `float.Parse(ThemeTokenFile.Resolve("--tutorial-highlight-label-gap").Replace("px", ""))`）

`TutorialOverlayClipTest.cs`（SetUp は同じ。`ClipBox()` は `inline overflow: hidden` と `TutorialAnchor.MarkClipContainer` を付けた `left 0・top 200・幅 600・高 300` の容器を置いて返す helper）:
```csharp
        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">祖先クリップの外へ出た辺はclip-pathで切られる</web-test>
        [Test]
        public void 祖先クリップの外へ出た辺だけが切られ残りの辺はグローを残す()
        {
            var container = ClipBox();
            MountAnchor(container, "research.node-a", 100, -20, 120, 60);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "research.node-a", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var clip = _view.Q(className: "tutorial-outline__clip").worldBound;
            Assert.AreEqual(200f, clip.yMin, 0.5f);
            Assert.AreEqual(100f - 8f - Glow(), clip.xMin, 0.5f);
            Assert.AreEqual(200f + 40f + 8f + Glow(), clip.yMax, 0.5f);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">完全にクリップされた枠は要素ごと描かない</web-test>
        [Test]
        public void 完全にクリップされた枠は出さない()
        {
            var container = ClipBox();
            MountAnchor(container, "research.node-a", 100, -200, 120, 60);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "research.node-a", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_view.Q(className: "tutorial-outline__clip")));
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">グロー幅未満だけ外れた枠は要素ごと描かない</web-test>
        [Test]
        public void グロー幅に満たない分だけ外れた枠も出さない()
        {
            var container = ClipBox();
            // 箱（アンカー ± 8）の下端が容器の上端より 2px 上（グロー 4px より狭い）
            // The box (anchor ± 8) ends 2px above the container top (narrower than the 4px glow)
            MountAnchor(container, "research.node-a", 100, -70, 120, 60);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "research.node-a", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_view.Q(className: "tutorial-outline__clip")));
        }
```
`TutorialHighlightAncestorClipTest.cs`:
```csharp
        /// <web-test file="e2e/tests/system/tutorialHighlightClip.spec.ts">研究ノードのハイライトが祖先のoverflowクリップに合わせてマスクされる</web-test>
        [Test]
        public void スクロールで半分隠れた研究ノードの枠はviewportの境界で切られる()
        {
            // 研究ツリーと同じく ScrollView の中身をずらし、ノードを viewport の上端にまたがらせる
            // Like the research tree, pan the ScrollView content so the node straddles the viewport top
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { style = { position = Position.Absolute, left = 200, top = 150, width = 600, height = 400 } };
            rootVisualElement.Add(scroll);
            var canvas = new VisualElement { style = { width = 2000, height = 2000 } };
            scroll.Add(canvas);
            MountAnchor(canvas, "research.node-a", 300, 480, 160, 80);
            simulate.FrameUpdate();
            scroll.scrollOffset = new Vector2(0, 520);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "research.node-a", null))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var viewport = scroll.contentViewport.worldBound;
            var clip = _view.Q(className: "tutorial-outline__clip").worldBound;
            Assert.AreEqual(viewport.yMin, clip.yMin, 0.5f);
            Assert.Greater(clip.yMax, viewport.yMin);
            // 切っていない左右はグローの帯まで残る
            // The unclipped left and right sides keep the glow band
            Assert.AreEqual(viewport.xMin + 300f - 8f - Glow(), clip.xMin, 0.5f);
        }
```
（Web の spec は Playwright で研究画面を開き、ノードの上の点がハイライトの色を拾わないことを見ている。UITK では研究画面が未移植なので、同じ構造（パンした ScrollView の中のノード）を L1 で作り、切り口の矩形で確かめる。研究画面の実物での確認は S4-research の録画が持つ）

Run: `uloop compile --project-path ./moorestech_client`
Expected: `TutorialOverlayView` 等が無いのでコンパイルエラー

- [ ] **Step 2: USS を書く**

`TutorialOverlay.uss`:
```css
/* web: W/features/tutorial/overlay/style.module.css（.overlay・.highlight・.highlightLabel）。--ui-scale の掛け算はパネル単位＝stage px なので要らない */
/* web: W/features/tutorial/overlay/style.module.css (.overlay, .highlight, .highlightLabel); no --ui-scale since panel units are stage px */
.tutorial-overlay {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
    --tutorial-overlay-glow-width: var(--tutorial-highlight-glow);
    --tutorial-overlay-label-gap: var(--tutorial-highlight-label-gap);
    --tutorial-overlay-ring-radius: 8px;
    --tutorial-attention-pulse-period: var(--tutorial-pulse-duration);
}

.tutorial-outline {
    position: absolute;
    left: 0;
    top: 0;
}

/* Web の clip-path の代わりに、箱を inset で縮めた容器で切る。拡縮は容器ごと（ADR 0039 の「clip も一緒に呼吸する」） */
/* Instead of Web's clip-path, clip with a container shrunk by the inset; it scales as a whole (ADR 0039: the clip breathes too) */
.tutorial-outline__clip {
    position: absolute;
    overflow: hidden;
}

.tutorial-outline__ring {
    position: absolute;
    border-width: 3px;
    border-color: var(--tutorial-attention-red);
    border-radius: 8px;
}

/* box-shadow の spread の代わりに、外側へ glow 幅の枠を重ねる。幅と角丸は C# が --tutorial-overlay-glow-width から当てる */
/* Instead of a box-shadow spread, overlay an outer border of the glow width; C# sets width and radius from --tutorial-overlay-glow-width */
.tutorial-outline__glow {
    position: absolute;
    border-color: var(--tutorial-attention-glow);
}

.tutorial-highlight-label {
    position: absolute;
    padding: var(--label-face-padding);
    color: var(--text-high-contrast);
    background-color: var(--world-pin-face);
    font-size: var(--label-face-font-size);
    white-space: normal;
    margin: 0;
}
```
全要素は C# で `pickingMode = PickingMode.Ignore`（Web の `pointer-events: none`）。

`GameUiTheme.tss` の `/* == section: Screens == */` の区画の末尾（ほかのレーンが先に足していればその後）に、この USS の 1 行だけを足す。まだ無い USS は読み込まない（DragGuide・KeyControlHint・WorldPin の行は、それぞれ Task 7・8・9 が USS を作るときに足す。D-34）:
```
@import url("project://database/Assets/Scripts/Client.Ui/Screens/Tutorial/Overlay/TutorialOverlay.uss");
```

- [ ] **Step 3: View を書く**

`TutorialOutlineView.cs`:
```csharp
    public sealed class TutorialOutlineView : VisualElement
    {
        public readonly TutorialHighlightLabelView Label = new();
        private readonly VisualElement _clip = new() { name = "tutorial-outline__clip" };
        private readonly VisualElement _glow = new();
        private readonly VisualElement _ring = new();

        public TutorialOutlineView(TutorialPulseBinding pulse)
        {
            name = "tutorial-outline";
            AddToClassList("tutorial-outline");
            pickingMode = PickingMode.Ignore;
            _clip.AddToClassList("tutorial-outline__clip");
            _glow.AddToClassList("tutorial-outline__glow");
            _ring.AddToClassList("tutorial-outline__ring");
            foreach (var element in new[] { _clip, _glow, _ring }) element.pickingMode = PickingMode.Ignore;
            _clip.Add(_glow);
            _clip.Add(_ring);
            Add(_clip);
            // ラベルは文を当ててから木に載せる（文の無い LocalizedLabel は 1 フレーム後に LogError。mode-tooltip D-28 と同形）
            // Attach the label only after its text is set (a text-less LocalizedLabel logs an error a frame later; same as mode-tooltip D-28)
            // 枠は 1.03 で脈動し、ラベルは脈動しない（Web tutorialAttentionDesign）
            // The ring pulses at 1.03 and the label does not (Web tutorialAttentionDesign)
            pulse.Attach(_clip, TutorialAttentionPulse.SubtleScale);
            HideRing();
        }

        public void ShowRing(TutorialClipRect box, TutorialClipInset inset, float glowWidth, float ringRadius)
        {
            // 容器は箱を inset で縮めた矩形。負の inset は外へ広げてグローを残す（Web clipPathInset）
            // The container is the box shrunk by the inset; negative insets widen it to keep the glow (Web clipPathInset)
            var left = box.Left + inset.Left;
            var top = box.Top + inset.Top;
            SetRect(_clip, left, top, box.Right - inset.Right - left, box.Bottom - inset.Bottom - top);
            SetRect(_ring, box.Left - left, box.Top - top, box.Width, box.Height);
            SetRect(_glow, box.Left - left - glowWidth, box.Top - top - glowWidth, box.Width + glowWidth * 2f, box.Height + glowWidth * 2f);
            SetBorder(_glow, glowWidth, ringRadius + glowWidth);
            // 拡縮の中心は箱の中心（Web は .highlight 自身の中心）
            // Scale around the box center (Web scales .highlight around its own center)
            _clip.style.transformOrigin = new TransformOrigin(new Length(box.CenterX - left), new Length(box.CenterY - top));
            _clip.style.display = DisplayStyle.Flex;

            #region Internal

            void SetRect(VisualElement element, float x, float y, float width, float height)
            {
                element.style.left = x;
                element.style.top = y;
                element.style.width = width;
                element.style.height = height;
            }

            void SetBorder(VisualElement element, float width, float radius)
            {
                element.style.borderTopWidth = width;
                element.style.borderRightWidth = width;
                element.style.borderBottomWidth = width;
                element.style.borderLeftWidth = width;
                element.style.borderTopLeftRadius = radius;
                element.style.borderTopRightRadius = radius;
                element.style.borderBottomLeftRadius = radius;
                element.style.borderBottomRightRadius = radius;
            }

            #endregion
        }

        public void ShowLabel(UiText text, TutorialClipRect box, TutorialClipRect clip, float gap)
        {
            Label.Place(text, box, clip, gap);
            if (Label.parent == null) Add(Label);
        }

        public void HideRing()
        {
            _clip.style.display = DisplayStyle.None;
            Label.HideLabel();
        }
    }
```

`TutorialHighlightLabelView.cs`:
```csharp
    public sealed class TutorialHighlightLabelView : LocalizedLabel
    {
        private TutorialClipRect _box;
        private TutorialClipRect _clip;
        private float _gap;

        public TutorialHighlightLabelView()
        {
            name = "tutorial-highlight-label";
            AddToClassList("tutorial-highlight-label");
            pickingMode = PickingMode.Ignore;
            // 面の寸法は文と折り返し上限で決まるので測ってから置く。測るまでは見せない（Web は paint 前に確定）
            // The face size depends on text and wrap limit, so place after measuring; hide until then (Web settles before paint)
            RegisterCallback<GeometryChangedEvent>(_ => ApplyPlacement());
        }

        public void Place(UiText text, TutorialClipRect box, TutorialClipRect clip, float gap)
        {
            SetText(text);
            _box = box;
            _clip = clip;
            _gap = gap;
            style.maxWidth = clip.Width;
            style.display = DisplayStyle.Flex;
            ApplyPlacement();
        }

        public void HideLabel()
        {
            // 文の無いまま木に残さない（LocalizedTextBinding の LogError を避ける）
            // Never leave it in the tree without text (avoids the LocalizedTextBinding LogError)
            style.visibility = Visibility.Hidden;
            RemoveFromHierarchy();
        }

        private void ApplyPlacement()
        {
            // silent-ok: 測る前（幅 0）は次の GeometryChangedEvent で置き直す / L1 TutorialOverlayLabelTest が位置を見る
            if (resolvedStyle.display == DisplayStyle.None || layout.width <= 0) return;
            var position = TutorialHighlightLabelPlacement.Place(_box, _clip, _gap, layout.width, layout.height);
            style.left = position.Left;
            style.top = position.Top;
            style.visibility = Visibility.Visible;
        }
    }
```

`TutorialOverlayPresenter.cs`（購読はここだけ。View は Render と RedrawAnchor を受けるだけ）:
```csharp
    public sealed class TutorialOverlayPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly TutorialOverlayModel _model;

        public TutorialOverlayPresenter(GameUiRoot gameUiRoot, TutorialOverlayModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            // 枠は Portal の最前面（--z-portal-tutorial）。アンカーは stage・viewport の両族から探し、座標はこの層で測る
            // Outlines live in the portal's front layer (--z-portal-tutorial); search anchors across both families and measure in this layer
            var layer = _gameUiRoot.GetLayer(UiLayer.Tutorial, UiFamily.Viewport);
            Mount(layer, _gameUiRoot.GetRootElement(), _model.Current);
        }

        public static TutorialOverlayView Mount(VisualElement layer, VisualElement searchRoot, IReadOnlyReactiveProperty<TutorialOverlayViewModel> model)
        {
            var tracker = new TutorialAnchorTracker(searchRoot, layer);
            var view = new TutorialOverlayView(tracker);
            layer.Add(view);
            tracker.OnAnchorChanged.Subscribe(change => view.RedrawAnchor(change.AnchorId));
            model.Subscribe(OnModelChanged);
            // 外れている間は毎フレームの測定を止め、載せ直したら今の ViewModel で購読し直す（D-32）
            // Stop per-frame measuring while detached; resubscribe with the current view model on re-attach (D-32)
            view.RegisterCallback<DetachFromPanelEvent>(_ => tracker.StopTracking());
            view.RegisterCallback<AttachToPanelEvent>(_ => OnModelChanged(model.Value));
            return view;

            #region Internal

            void OnModelChanged(TutorialOverlayViewModel current)
            {
                // 購読を差し替えてから描く（差し替えの同期の測定で出た変化は直後の Render が描き直す）
                // Swap subscriptions, then draw (the following Render redraws changes from the synchronous measurement)
                tracker.SetSubscribedAnchors(AnchorIdsOf(current));
                view.Render(current);
            }

            IReadOnlyCollection<string> AnchorIdsOf(TutorialOverlayViewModel current)
            {
                var anchorIds = new HashSet<string>();
                foreach (var outline in current.Outlines) anchorIds.Add(outline.AnchorId);
                foreach (var guide in current.DragGuides)
                {
                    anchorIds.Add(guide.FromAnchorId);
                    anchorIds.Add(guide.ToAnchorId);
                }
                return anchorIds.ToList();
            }

            #endregion
        }
    }
```
（`SetSubscribedAnchors` の同期の測定で出る変化は、照合前の View へ `RedrawAnchor` として届くが、直後の `Render` が全部描き直すので取りこぼさない）

`TutorialOverlayView.cs`（Model も追跡器の変化も購読しない。200 行を超えそうなら DragGuide の照合を Task 7 の `TutorialOverlayReconciler` へ出す）:
```csharp
    public sealed class TutorialOverlayView : VisualElement
    {
        private readonly ITutorialAnchorStateLookup _anchors;
        private readonly Dictionary<string, TutorialOutlineView> _outlines = new();
        private readonly TutorialPulseBinding _pulse;
        private TutorialOverlayViewModel _current = TutorialOverlayViewModel.Empty;
        private float _glowWidth;
        private float _labelGap;
        private float _ringRadius;

        public TutorialOverlayView(ITutorialAnchorStateLookup anchors)
        {
            name = "tutorial-overlay";
            AddToClassList("tutorial-overlay");
            pickingMode = PickingMode.Ignore;
            _anchors = anchors;
            _pulse = new TutorialPulseBinding(this);
            RegisterCallback<CustomStyleResolvedEvent>(evt => ReadKnobs(evt.customStyle));
        }

        public void Render(TutorialOverlayViewModel model)
        {
            _current = model;
            ReconcileOutlines(model);
            foreach (var outline in model.Outlines) Redraw(outline);
        }

        public void RedrawAnchor(string anchorId)
        {
            foreach (var outline in _current.Outlines)
            {
                if (outline.AnchorId == anchorId && _outlines.ContainsKey(outline.Key)) Redraw(outline);
            }
        }

        private void Redraw(TutorialOutlineCue cue)
        {
            var view = _outlines[cue.Key];
            // ready で、箱がクリップの内に残るときだけ枠を出す（ready 以外は仕様どおりの非表示で、理由は追跡器が遷移時に出す）
            // Show the ring only when ready and the box survives the clip (non-ready is a spec'd hide; the tracker reports reasons)
            if (!_anchors.TryGet(cue.AnchorId, out var state) || state is not TutorialAnchorState.Ready ready)
            {
                // silent-ok: 未解決・非表示のアンカーの枠は Web と同じく出さない仕様 / 理由は TutorialAnchorTracker が遷移時に Warning・LogError で出し、L1 TutorialOverlayAnchorTest が見る
                view.HideRing();
                return;
            }
            var box = ready.Rect.Expand(cue.PaddingPx);
            if (!TutorialHighlightClip.TryComputeInset(box, ready.Clip, _glowWidth, out var inset))
            {
                // silent-ok: 祖先クリップで完全に切れた枠は Web と同じく要素ごと出さない仕様 / L1 TutorialOverlayClipTest の「完全にクリップされた枠」が見る
                view.HideRing();
                return;
            }
            view.ShowRing(box, inset, _glowWidth, _ringRadius);
            if (cue is TutorialOutlineCue.Labeled labeled && TutorialHighlightLabelPlacement.ShowsLabel(ready.Rect, ready.Clip)) view.ShowLabel(labeled.Label, box, ready.Clip, _labelGap);
            else view.Label.HideLabel();
        }

        private void ReconcileOutlines(TutorialOverlayViewModel model)
        {
            var live = new HashSet<string>(model.Outlines.Select(outline => outline.Key));
            foreach (var key in _outlines.Keys.Where(key => !live.Contains(key)).ToList())
            {
                _outlines[key].RemoveFromHierarchy();
                _outlines.Remove(key);
            }
            foreach (var outline in model.Outlines)
            {
                if (_outlines.ContainsKey(outline.Key)) continue;
                var view = new TutorialOutlineView(_pulse);
                _outlines.Add(outline.Key, view);
                Add(view);
            }
        }

        private void ReadKnobs(ICustomStyle customStyle)
        {
            // 寸法はトークンを画面ルートで受け直した変数から読み、読めたら描き直す（読めなければ Reader が LogError）
            // Read sizes from the screen root's re-declared tokens and redraw (the reader logs an error when unreadable)
            UssCustomStyleReader.TryReadPixels(customStyle, "--tutorial-overlay-glow-width", out _glowWidth);
            UssCustomStyleReader.TryReadPixels(customStyle, "--tutorial-overlay-label-gap", out _labelGap);
            UssCustomStyleReader.TryReadPixels(customStyle, "--tutorial-overlay-ring-radius", out _ringRadius);
            Render(_current);
        }
    }
```

- [ ] **Step 4: 脈動の周期の受け直しと、ラベルの padding の複数値 var を確かめる**

`TutorialPulseBindingTest.cs`（`TutorialOverlayTestHost` を継ぐ。Task 5 の `TutorialPulseBinding` を、Step 2 で書いて tss に入れた本番の `TutorialOverlay.uss` のルート `.tutorial-overlay` の受け直しで確かめる。USS と tss の行が入るこのタスクで足す。D-34）:
```csharp
        private VisualElement MountPeriodHost()
        {
            var host = new VisualElement();
            host.AddToClassList("tutorial-overlay");
            rootVisualElement.Add(host);
            simulate.FrameUpdate();
            return host;
        }

        [Test]
        public void 周期の解決より後に足した子も脈動する()
        {
            // 画面は空で常駐し、チュートリアルは後から来る（ルートの解決はもう終わっている）
            // The screen is resident and empty; the tutorial arrives later, after the root has resolved
            var host = MountPeriodHost();
            var binding = new TutorialPulseBinding(host);
            var late = new VisualElement();
            binding.Attach(late, TutorialAttentionPulse.StrongScale);
            host.Add(late);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(Ms("--tutorial-pulse-duration") / 2f);
            Assert.AreEqual(1.08f, late.resolvedStyle.scale.value.x, 0.005f);
        }

        [Test]
        public void 外した子へは周期が変わっても流さず載せ直すと再び流す()
        {
            var host = MountPeriodHost();
            var binding = new TutorialPulseBinding(host);
            var child = new VisualElement();
            binding.Attach(child, TutorialAttentionPulse.SubtleScale);
            host.Add(child);
            simulate.FrameUpdate();
            child.RemoveFromHierarchy();

            // ルートの再解決（クラスの付け外し）で、外した子へ Play して UiMotionPlayer の LogError を出さない
            // A root re-resolution must not Play on the removed child (no UiMotionPlayer LogError)
            host.AddToClassList("tutorial-pulse-binding-test--restyle");
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();

            host.Add(child);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(Ms("--tutorial-pulse-duration") / 2f);
            Assert.AreEqual(1.03f, child.resolvedStyle.scale.value.x, 0.005f);
        }
```
（`Ms(name)` は Task 5 の `TutorialOverlayTestHost` の helper）

`TutorialOverlayLabelTest` に自前の 1 件を足す:
```csharp
        [Test]
        public void ラベルの面はlabel_face_paddingの4px_10pxを受ける()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 200, 40);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();
            var label = Shown<TutorialHighlightLabelView>().Single();
            Assert.AreEqual((4f, 10f, 4f, 10f), (label.resolvedStyle.paddingTop, label.resolvedStyle.paddingRight, label.resolvedStyle.paddingBottom, label.resolvedStyle.paddingLeft));
        }
```
Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Tutorial\.Overlay\."`
Expected: `ErrorCount: 0`。padding の 1 件が落ちたら（複数値の `var()` が 1 つの padding に展開されない）、`TutorialOverlay.uss` と Task 9 の `WorldPin.uss` の `padding: var(--label-face-padding);` を `padding-top: 4px; padding-bottom: 4px; padding-left: 10px; padding-right: 10px; /* web: var(--label-face-padding) = 4px 10px。UITK は複数値の var を padding へ展開しない（台帳 実測の結論） */` に替え、台帳「## 実測の結論」の末尾へ「`padding: var(<複数値>)` が展開されるか | S2-tutorial | 展開されない（L1 `ラベルの面はlabel_face_paddingの4px_10pxを受ける` で paddingTop=<実測>）。4 辺へ分けて書く」を 1 行足す（通れば「展開される」と書く）。全件 PASS（計 21: Tracker 8・PulseBinding 2・Anchor 2・Label 5・Clip 3・AncestorClip 1。Anchor は Web の 1 件〈ack の 1 件は捨てる〉と画面を外す 1 件）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Tutorial moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Tutorial docs/uitk/TODO.md "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): チュートリアルの枠線・グロー・祖先クリップ・枠線ラベルを Web から写す（S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: DragGuide（`Screens/Tutorial/DragGuide/`。TutorialOverlay の drag guides 3・E-16）

**Files:**
- Create: `S/Client.Ui/Screens/Tutorial/DragGuide/{TutorialDragGuideView.cs,TutorialDragGuideGlyph.cs,TutorialDragGuideStep.cs,TutorialDragGuide.uss}`
- Modify: `S/Client.Ui/Screens/Tutorial/Overlay/TutorialOverlayView.cs`（DragGuide の照合と描き直しを足す）
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に `TutorialDragGuide.uss` の 1 行。D-34）
- Test: `S/Client.Tests/Ui/Screens/Tutorial/Overlay/TutorialOverlayDragGuideTest.cs`

**Interfaces:**
- Consumes: Task 3 の `TutorialDragGuideGate.TryPlace`、Task 4 の `TutorialDragGuideCue`、Task 6 の `TutorialOverlayView`、`UiMotionSpec(…, UiEasing.Linear, UiMotionLoop.Repeat)`・`UiEasingFunctions.Evaluate(UiEasing.EaseInOutSine, t)`
- Produces（namespace `Client.Ui.Screens.Tutorial.DragGuide`）:
  - `TutorialDragGuideView : VisualElement`（`name = "tutorial-drag-guide"`）: `void Show(TutorialDragGuidePlacement placement)`・`void HideGuide()`。寸法と周期は `.tutorial-drag-guide` の `--tutorial-drag-guide-view-size: var(--tutorial-drag-guide-size)`・`--tutorial-drag-guide-view-duration: var(--tutorial-drag-guide-duration)` から読む
  - `TutorialDragGuideGlyph : VisualElement`（Painter2D。24 の viewBox の `M6 3 L18 12 L11 13.5 L13.5 20 L10.5 21 L8 14.5 L3 18 Z`、塗り `--tutorial-drag-guide-glyph-fill: var(--text-high-contrast)`、影 `--tutorial-drag-guide-glyph-shadow: var(--tutorial-drag-guide-shadow)` を下へ 1px ずらした写しで描く。LX-35）
  - `TutorialDragGuideStep(VisualElement target) : IUiTweenStep`・`void SetTravel(float x, float y)`（Web の `drag-guide-loop`: 0〜15% で不透明度 0→1、0〜75% で移動 0→travel、75〜100% で不透明度 1→0。区間ごとに ease-in-out）

- [ ] **Step 1: 失敗するテストを書く**

`TutorialOverlayDragGuideTest.cs`（SetUp は Task 6 の `TutorialOverlayAnchorTest` と同じ）:
```csharp
        private static TutorialOverlayViewModel Guide(int revision)
        {
            return new TutorialOverlayViewModel(revision, System.Array.Empty<TutorialOutlineCue>(), new[] { new TutorialDragGuideCue("s1:d1", "pause.save", "pause.settings") });
        }

        private int ShownGuides()
        {
            return _view.Query<TutorialDragGuideView>().Where(guide => IsShown(guide)).ToList().Count;
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">両anchorがreadyのときだけ tutorial-drag-guide を1件描画する</web-test>
        [Test]
        public void 両端がreadyのときだけDragGuideを1件出す()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 40, 40);
            LogAssert.Expect(LogType.Warning, new Regex("anchor is missing: pause.settings"));
            _model.Set(Guide(1));
            simulate.FrameUpdate();
            Assert.AreEqual(0, ShownGuides());

            MountAnchor(rootVisualElement, "pause.settings", 500, 300, 40, 40);
            simulate.FrameUpdate();
            Assert.AreEqual(1, ShownGuides());
            var guide = _view.Q<TutorialDragGuideView>();
            Assert.AreEqual(120f, guide.worldBound.center.x - guide.resolvedStyle.translate.x, 0.5f);
        }

        [Test]
        public void 両端が最初から揃っていても寸法の解決後に中心へ置く()
        {
            // 寸法の解決より先に Show が来ても、解決後に置き直して起点の中心に合わせる
            // Even if Show arrives before the size resolves, re-place after resolution to center on the origin
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 40, 40);
            MountAnchor(rootVisualElement, "pause.settings", 500, 300, 40, 40);
            _model.Set(Guide(1));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var guide = _view.Q<TutorialDragGuideView>();
            Assert.AreEqual(120f, guide.worldBound.center.x - guide.resolvedStyle.translate.x, 0.5f);
            Assert.AreEqual(120f, guide.worldBound.center.y - guide.resolvedStyle.translate.y, 0.5f);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">起点が祖先クリップの外へスクロールしたら矢印を描かない</web-test>
        [Test]
        public void 起点がクリップの外へスクロールしたら出さない()
        {
            var scroll = new ScrollView { style = { position = Position.Absolute, left = 0, top = 100, width = 400, height = 200 } };
            rootVisualElement.Add(scroll);
            var content = new VisualElement { style = { height = 1000 } };
            scroll.Add(content);
            MountAnchor(content, "pause.save", 50, 20, 40, 40);
            MountAnchor(rootVisualElement, "pause.settings", 600, 300, 40, 40);
            _model.Set(Guide(1));
            simulate.FrameUpdate();
            Assert.AreEqual(1, ShownGuides());

            scroll.scrollOffset = new Vector2(0, 200);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(0, ShownGuides());
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">片方のanchorが未解決に戻ると非表示になる</web-test>
        [Test]
        public void 片方のアンカーが消えると隠す()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 40, 40);
            var to = MountAnchor(rootVisualElement, "pause.settings", 500, 300, 40, 40);
            _model.Set(Guide(1));
            simulate.FrameUpdate();
            Assert.AreEqual(1, ShownGuides());

            LogAssert.Expect(LogType.Warning, new Regex("anchor is missing: pause.settings"));
            to.RemoveFromHierarchy();
            simulate.FrameUpdate();
            Assert.AreEqual(0, ShownGuides());
        }
```

Run: `uloop compile --project-path ./moorestech_client`
Expected: `TutorialDragGuideView` が無いのでコンパイルエラー

- [ ] **Step 2: USS・step・字形・View を書く**

`TutorialDragGuide.uss`:
```css
/* web: W/features/tutorial/overlay/style.module.css（.dragGuide・.dragGuide svg・@keyframes drag-guide-loop） */
/* web: W/features/tutorial/overlay/style.module.css (.dragGuide, .dragGuide svg, @keyframes drag-guide-loop) */
.tutorial-drag-guide {
    position: absolute;
    --tutorial-drag-guide-view-size: var(--tutorial-drag-guide-size);
    --tutorial-drag-guide-view-duration: var(--tutorial-drag-guide-duration);
}

.tutorial-drag-guide__glyph {
    width: 100%;
    height: 100%;
    --tutorial-drag-guide-glyph-fill: var(--text-high-contrast);
    --tutorial-drag-guide-glyph-shadow: var(--tutorial-drag-guide-shadow);
}
```
`TutorialDragGuideStep.cs`:
```csharp
        public void Apply(float easedProgress)
        {
            // CSS のキーフレームは区間ごとに ease-in-out が掛かるので、直線の進みを区間へ割って曲線を当てる
            // CSS applies ease-in-out per keyframe interval, so split the linear progress and ease each interval
            var p = easedProgress;
            var move = p < 0.75f ? Ease(p / 0.75f) : 1f;
            var opacity = p < 0.15f ? Ease(p / 0.15f) : p < 0.75f ? 1f : 1f - Ease((p - 0.75f) / 0.25f);
            _target.style.translate = new Translate(new Length(_travelX * move), new Length(_travelY * move));
            _target.style.opacity = opacity;

            #region Internal

            float Ease(float t)
            {
                return UiEasingFunctions.Evaluate(UiEasing.EaseInOutSine, t);
            }

            #endregion
        }
```
`TutorialDragGuideGlyph.cs`（`generateVisualContent` で、`--tutorial-drag-guide-glyph-*` を `CustomStyleResolvedEvent` で読み `MarkDirtyRepaint`。viewBox 24 を `contentRect` の幅へ拡大する）:
```csharp
        private static readonly Vector2[] CursorPath =
        {
            new(6f, 3f), new(18f, 12f), new(11f, 13.5f), new(13.5f, 20f), new(10.5f, 21f), new(8f, 14.5f), new(3f, 18f),
        };

        private void Draw(MeshGenerationContext context)
        {
            var unit = contentRect.width / 24f;
            var painter = context.painter2D;
            // 影は blur を持てないので 1px 下へずらした写しで近似する（LX-35）
            // Painter2D has no blur, so approximate the drop shadow with a copy shifted 1px down (LX-35)
            FillPath(painter, unit, new Vector2(0f, 1f), _shadow);
            FillPath(painter, unit, Vector2.zero, _fill);
        }

        private static void FillPath(Painter2D painter, float unit, Vector2 offset, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(CursorPath[0] * unit + offset);
            for (var i = 1; i < CursorPath.Length; i++) painter.LineTo(CursorPath[i] * unit + offset);
            painter.ClosePath();
            painter.Fill();
        }
```
`TutorialDragGuideView.cs`: ctor で `name = "tutorial-drag-guide"`・クラス・拾わない・子に `TutorialDragGuideGlyph`、`CustomStyleResolvedEvent` で寸法（`style.width/height`）と周期を読み、`UiMotionPlayer.Play(this, new UiTweenAnimation(new UiMotionSpec(duration, 0f, UiEasing.Linear, UiMotionLoop.Repeat), _step))` を 1 回だけ始める（`DetachFromPanelEvent` で `Cancel`）。`Show(placement)` は置き場所を `_placement` に持ち、`display: flex` にして `ApplyPlacement()` を呼ぶ。`ApplyPlacement()` は寸法が未解決（`_size <= 0`）なら何もせず（`// silent-ok: 寸法の解決前は CustomStyleResolvedEvent が置き直す / L1 両端が最初から揃っていても中心に置く が見る`）、解決後は `style.left = _placement.OriginX - _size / 2f`・`style.top = _placement.OriginY - _size / 2f`（Web の負の margin）・`_step.SetTravel(_placement.TravelX, _placement.TravelY)`。`CustomStyleResolvedEvent` で寸法を読んだ直後にも `ApplyPlacement()` を呼ぶ（寸法 0 のまま置いて 28px ずれたまま残るのを防ぐ）。`HideGuide()` は `display: none`。

`TutorialOverlayView.cs` に足す: `Dictionary<string, TutorialDragGuideView> _dragGuides` の照合（`ReconcileOutlines` と同じ形の `ReconcileDragGuides`。`Render` で `ReconcileOutlines` の直後に呼ぶ）、`Render` の最後で全ガイド、`RedrawAnchor(anchorId)` で `FromAnchorId` か `ToAnchorId` が一致するガイドの `RedrawGuide(cue)`。購読は足さない（Presenter の `Mount` が既にガイドの両端も購読している）:
```csharp
        private void RedrawGuide(TutorialDragGuideCue cue)
        {
            var view = _dragGuides[cue.Key];
            // 両端の状態がそろい起点が見えている時だけ出す（Web DragGuide。隠すのは仕様どおりで、理由は追跡器が遷移時に出す）
            // Show only when both ends are known and the origin is visible (Web DragGuide; reasons are reported by the tracker)
            if (_anchors.TryGet(cue.FromAnchorId, out var from) && _anchors.TryGet(cue.ToAnchorId, out var to) && TutorialDragGuideGate.TryPlace(from, to, out var placement)) view.Show(placement);
            // silent-ok: 端が未解決・起点が見えない間は Web と同じく出さない仕様 / 未解決の理由は TutorialAnchorTracker が遷移時に出し、L1 TutorialOverlayDragGuideTest が見る
            else view.HideGuide();
        }
```
`TutorialOverlayView.cs` が 200 行を超えたら、照合 2 つを `TutorialOverlayReconciler`（同じディレクトリ。`Overlay/` は 6 本になる）へ出す。
`GameUiTheme.tss` の `Screens` 区画の末尾（Task 6 の行の次）に `@import url("project://database/Assets/Scripts/Client.Ui/Screens/Tutorial/DragGuide/TutorialDragGuide.uss");` を 1 行足す。

- [ ] **Step 3: 確かめてコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Tutorial\.Overlay\."`
Expected: `ErrorCount: 0`、Task 6 の 21 件＋DragGuide 4 件（Web 3・置き直し 1）の計 25 件が PASS
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Tutorial moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Tutorial "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): 2 つのアンカーを結ぶ DragGuide を Web から写す（S2-tutorial・E-16）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: KeyControlHint（`Screens/Tutorial/KeyControlHint/`。KeyControlHintHud 4）

**Files:**
- Modify: `S/Client.Ui/Components/Text/ShortcutHintLabel/ShortcutHintLabel.cs`（Prefix では印の引数を足さない。指揮の割り当て A）
- Modify: `S/Client.Ui/Components/Text/KeyCap/KeyCap.uss`（色のつまみ `--key-cap-color`。既定は `var(--text-high-contrast)`。指揮の割り当て A）
- Create: `S/Client.Tests/Ui/Components/Text/KeyCapColorProbe.uss`（このレーンのテスト専用の probe。共有の `ComponentTokenProbe.uss` は書き換えない）
- Create: `S/Client.Ui/Screens/Tutorial/KeyControlHint/{KeyControlHintPresenter.cs,KeyControlHintView.cs,KeyControlHint.uss}`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に `KeyControlHint.uss` の 1 行。D-34）
- Test: `S/Client.Tests/Ui/Components/Text/{ShortcutHintLabelPrefixTextTest.cs,KeyCapTest.cs}`（既存の `ShortcutHintLabelTest.cs` は 195 行なので足さず別ファイル）・`S/Client.Tests/Ui/Screens/Tutorial/Cues/{FakeTutorialCueModels.cs,KeyControlHintTest.cs}`

**Interfaces:**
- Consumes: このタスクの Step 1〜2 で直す `ShortcutHintLabel(string testid, ShortcutHintLayout layout)`・`SetHint(UiText sentence, string shortcutText)`、`KeyCap`（`ShortcutHintLabel` の中）、`UiTypographyClass.KeyHintText`、Task 4 の `KeyControlHintViewModel`・`KeyControlHintProjection`・`KeyControlHintModel`、Task 5 の `TutorialPulseBinding`、`GameUiRoot.GetLayer(UiLayer.OverlayPanel, UiFamily.Viewport)`
- Produces（namespace `Client.Ui.Screens.Tutorial.KeyControlHint`）:
  - 部品の直し: `ShortcutHintLabel.SetHint` は Prefix なら文をそのまま渡す（content・verbatim の文でも例外を出さない）、`KeyCap.uss` は `:root { --key-cap-color: var(--text-high-contrast); }` と `.key-cap { color: var(--key-cap-color); }`
  - `KeyControlHintView() : VisualElement`（`name = "key-control-hint-hud"`。Model を購読しない。`void Render(KeyControlHintViewModel model)`。ヒント 0 件で `display: none`。行は `ShortcutHintLabel("key-control-hint", ShortcutHintLayout.Prefix)`＋クラス `key-control-hint__item`・`UiTypographyClass.KeyHintText`）
  - `KeyControlHintPresenter(GameUiRoot gameUiRoot, KeyControlHintModel model) : IInitializable` と `static KeyControlHintView Mount(VisualElement layer, IReadOnlyReactiveProperty<KeyControlHintViewModel> model)`（`model.Subscribe(view.Render)` はここ。D-22）
  - 画面ルート `.key-control-hint` の受け直し: `--key-cap-color: var(--tutorial-key-hint-color)`（kbd の色。D-8）・`.key-control-hint .key-control-hint__item { color: var(--tutorial-key-hint-color); }`（文の色。`--text-high-contrast` は受け直さない）・`--shortcut-hint-label-gap: var(--tutorial-key-hint-kbd-gap)`・`--key-control-hint-floor-offset: var(--hotbar-floor-offset)`・`--key-control-hint-hotbar-gap: var(--tutorial-key-hint-hotbar-gap)`・`--key-control-hint-row-gap: var(--tutorial-key-hint-gap)`・`--tutorial-attention-pulse-period: var(--tutorial-pulse-duration)`

- [ ] **Step 1: 部品の直しの失敗するテストを書く**

`S/Client.Tests/Ui/Components/Text/ShortcutHintLabelPrefixTextTest.cs`（`UiTestHost` を継ぐ。言語の SetUp/TearDown は `ShortcutHintLabelTest` と同じ english 固定）:
```csharp
        [Test]
        public void prefixは辞書キー以外の文でも例外を出さずkbdの後ろに文を置く()
        {
            var content = new ShortcutHintLabel("content", ShortcutHintLayout.Prefix);
            var verbatim = new ShortcutHintLabel("verbatim", ShortcutHintLayout.Prefix);
            rootVisualElement.Add(content);
            rootVisualElement.Add(verbatim);

            // 以前は WithLeadingParam が辞書キー以外で InvalidOperationException を投げた
            // WithLeadingParam used to throw InvalidOperationException for non-dictionary text
            Assert.DoesNotThrow(() => content.SetHint(UiText.FromContent(ContentLocalizationKeys.ChallengeTutorialText(new Guid("8f2c6a1e-3b4d-4c5e-9f60-7a8b9c0d1e2f"))), "Tab"));
            Assert.DoesNotThrow(() => verbatim.SetHint(UiText.FromVerbatim("Open the menu"), "Esc"));
            simulate.FrameUpdate();

            Assert.AreEqual("Open the menu", ((Label)verbatim.Children().Last()).text);
            Assert.IsInstanceOf<KeyCap>(verbatim.Children().First(child => child.resolvedStyle.display == DisplayStyle.Flex));
        }
```
`S/Client.Tests/Ui/Components/Text/KeyCapColorProbe.uss`（`.key-cap-color-probe { --key-cap-color: rgb(255, 0, 0); }` と 2 行セットのコメント）を作り、`S/Client.Tests/Ui/Components/Text/KeyCapTest.cs`（`ShortcutHintLabelTest` の gap のテストと同じ形で、祖先の `.key-cap-color-probe` で当てる）:
```csharp
        [Test]
        public void 祖先でkey_cap_colorを上書きするとKeyCapの色だけが変わる()
        {
            const string probePath = "Assets/Scripts/Client.Tests/Ui/Components/Text/KeyCapColorProbe.uss";
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(probePath);
            Assert.IsNotNull(probe, probePath);
            rootVisualElement.styleSheets.Add(probe);
            var screen = new VisualElement();
            screen.AddToClassList("key-cap-color-probe");
            rootVisualElement.Add(screen);
            var plain = new KeyCap("Esc");
            rootVisualElement.Add(plain);
            var overridden = new KeyCap("Tab");
            screen.Add(overridden);
            simulate.FrameUpdate();

            Assert.AreEqual(Color.white, plain.resolvedStyle.color);
            Assert.AreEqual(Color.red, overridden.resolvedStyle.color);
        }
```
Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Components\.Text\.(ShortcutHintLabelPrefixTextTest|KeyCapTest)"`
Expected: 2 件とも FAIL（Prefix は `InvalidOperationException`、KeyCap は白のまま）

- [ ] **Step 2: 部品を直し、既存の部品テストを全部回す**

`ShortcutHintLabel.SetHint`:
```csharp
        public void SetHint(UiText sentence, string shortcutText)
        {
            // inline だけ {shortcut} に印を入れる。prefix は割らないので印は要らず、辞書キー以外の文もそのまま渡せる
            // Only inline puts the marker in {shortcut}; prefix never splits, so any text source passes through as is
            _keyCap.SetKeyText(shortcutText);
            _sentence.SetText(_layout == ShortcutHintLayout.Inline ? sentence.WithLeadingParam(ShortcutHintSegments.ParamName, ShortcutHintSegments.Marker) : sentence);
        }
```
`KeyCap.uss` の `.key-cap` の `color: var(--text-high-contrast);` を `color: var(--key-cap-color);` に替え、同じファイルの先頭に既定を置く（上のコメントも「色は置く側が `--key-cap-color` で上書きできる。既定は --text-high-contrast（置く側が文の色を変えても kbd は白）」へ直す。2 行セット）:
```css
/* 色のつまみの既定。画面ルートでの上書きは継承で勝つ（D-31） */
/* Default of the color knob; an override at a screen root wins through inheritance (D-31) */
:root {
    --key-cap-color: var(--text-high-contrast);
}
```

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Components\.Text\."`
Expected: `ErrorCount: 0`、新しい 2 件と既存の `ShortcutHintLabelTest`・`ShortcutHintSegmentsTest`・`TooltipFace*` が全件 PASS。続けて S2-hud の `KeyHintHud` が壊れていないことを、その L1 を名前で回して確かめる: `grep -rln "ShortcutHintLabel\|KeyCap" moorestech_client/Assets/Scripts/Client.Tests` で出たテストクラスを `--filter-type regex` の `(A|B|…)` にまとめて回し、全件 PASS（件数を `done.md` の「証拠」へ）。
既定の KeyCap が白でなくなったら（`KeyCapは枠なしで…色は高コントラスト` が落ちる＝部品 USS の `:root` がパネルのルートに効いていない）、`KeyCap.uss` が `GameUiTheme.tss` から読まれているか（テストの UiTestHost が同じ tss を当てているか）を確かめ、実測を台帳「## 実測の結論」へ 1 行書く
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Components/Text moorestech_client/Assets/Scripts/Client.Tests/Ui/Components
git commit -m "fix(uitk): ShortcutHintLabel の Prefix で印の引数を足さず、KeyCap に色のつまみ --key-cap-color を足す（S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 3: 失敗するテストを書く**

`FakeTutorialCueModels.cs`:
```csharp
    public sealed class FakeKeyControlHintModel : FakeModel<KeyControlHintViewModel>
    {
        public FakeKeyControlHintModel() : base(KeyControlHintViewModel.Empty) { }
    }

    public sealed class FakeWorldPinModel : FakeModel<WorldPinViewModel>
    {
        public FakeWorldPinModel() : base(WorldPinViewModel.Empty) { }
    }
```
`KeyControlHintTest.cs`:
```csharp
    public class KeyControlHintTest : ScreenTestHost
    {
        private FakeKeyControlHintModel _model;
        private KeyControlHintProjection _projection;
        private KeyControlHintView _view;

        [SetUp]
        public void MountHint()
        {
            _model = new FakeKeyControlHintModel();
            _projection = new KeyControlHintProjection();
            _view = KeyControlHintPresenter.Mount(rootVisualElement, _model.Current);
        }

        private static TutorialKeyControlElementData Hint(string elementId, string keyName, string uiState)
        {
            return new TutorialKeyControlElementData { ElementId = elementId, TutorialGuid = TutorialOverlayProjectionTest.LabelGuid.ToString(), KeyName = keyName, UiState = uiState };
        }

        private List<ShortcutHintLabel> Hints()
        {
            return _view.Query<ShortcutHintLabel>("key-control-hint").Where(hint => IsShown(hint)).ToList();
        }

        /// <web-test file="src/features/tutorial/KeyControlHintHud.test.ts">uiStateが一致するヒントだけを、既存共有部品のkbd+本文の順で描く</web-test>
        [Test]
        public void 画面状態が一致するヒントだけをkbdと文の順で出す()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "GameScreen"), Hint("k2", "E", "PlayerInventory")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();

            var hint = Hints().Single();
            var keyCap = hint.Q<KeyCap>();
            Assert.AreEqual("Tab", keyCap.text);
            // Prefix は前半の文が空なので、見える子の先頭が kbd、その次が文
            // In Prefix the leading text is empty, so the first visible child is the kbd and the sentence follows
            var visible = hint.Children().Where(child => IsShown(child)).ToList();
            Assert.IsInstanceOf<KeyCap>(visible[0]);
            Assert.IsNotEmpty(((Label)visible[1]).text);
            Assert.IsTrue(hint.ClassListContains(UiTypographyClass.KeyHintText));
        }

        /// <web-test file="src/features/tutorial/KeyControlHintHud.test.ts">一致するヒントが無ければHUD自体を描かない</web-test>
        [Test]
        public void 一致するヒントが無ければHUDごと出さない()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "PlayerInventory")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_view));
        }

        /// <web-test file="src/features/tutorial/KeyControlHintHud.test.ts">blockingスキット中は描かない</web-test>
        [Test]
        public void blockingのスキット中は出さない()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "GameScreen")), UIStateEnum.GameScreen, true));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_view));
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void キー操作ヒントは入力を奪わない()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "GameScreen")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();

            // 行と部品の内側（kbd・文）のどの中心を拾っても、ヒントの要素は当たらない（Web の pointer-events: none。D-36）
            // Picking at the center of the row or any part inside it (kbd, text) never hits a hint element (Web pointer-events: none; D-36)
            var parts = Hints().Single().Query<VisualElement>().Where(part => IsShown(part) && part.worldBound.width > 0).ToList();
            Assert.IsNotEmpty(parts);
            foreach (var part in parts)
            {
                var picked = rootVisualElement.panel.Pick(part.worldBound.center);
                Assert.IsFalse(picked != null && (picked == _view || _view.Contains(picked)), $"{part.name} picked {picked?.name}");
            }
        }

        /// <web-test file="src/features/tutorial/KeyControlHintHud.test.ts">同時に一致するヒントが2件あれば2件とも描く</web-test>
        [Test]
        public void 同時に一致する2件は2件とも縦に積む()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "GameScreen"), Hint("k2", "Q", "GameScreen")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();

            var hints = Hints();
            Assert.AreEqual(2, hints.Count);
            Assert.Less(hints[0].worldBound.yMax, hints[1].worldBound.yMin + 0.5f);
        }
    }
```
（`ShortcutHintLabel` の子の並びは `_before`・`_keyCap`・`_after`。Prefix は `_before` を空で隠すので、見える子の先頭が kbd になる。`_before` を隠さず空の Label として残す実物なら、`IsShown` を「幅が 0 より大きい」に読み替える）

Run: `uloop compile --project-path ./moorestech_client`
Expected: `KeyControlHintView` が無いのでコンパイルエラー

- [ ] **Step 4: USS と View・Presenter を書く**

`KeyControlHint.uss`:
```css
/* web: W/features/tutorial/keyControlHint.module.css（.hud・.hint・.hint kbd）。bottom の calc は USS に無いので C# が足す（設計書 A-47） */
/* web: W/features/tutorial/keyControlHint.module.css (.hud, .hint, .hint kbd); USS has no calc, so C# sums bottom (design A-47) */
.key-control-hint {
    position: absolute;
    left: 0;
    right: 0;
    flex-direction: column;
    align-items: center;
    /* kbd は部品のつまみ --key-cap-color で赤くする。--text-high-contrast は受け直さない（共有の色を画面で書き換えない。D-8） */
    /* Recolor the kbd through the component knob --key-cap-color; never re-declare --text-high-contrast (D-8) */
    --key-cap-color: var(--tutorial-key-hint-color);
    --shortcut-hint-label-gap: var(--tutorial-key-hint-kbd-gap);
    --key-control-hint-floor-offset: var(--hotbar-floor-offset);
    --key-control-hint-hotbar-gap: var(--tutorial-key-hint-hotbar-gap);
    --key-control-hint-row-gap: var(--tutorial-key-hint-gap);
    --tutorial-attention-pulse-period: var(--tutorial-pulse-duration);
}

/* 文の色と行の高さ。Web の .hint { color } と、key-hint-text の 25px × line-height 1.2 の行箱（2 件積んだときの行間を Web に合わせる） */
/* Sentence color and row height; Web .hint { color } and the 25px x line-height 1.2 line box (matches Web spacing when two rows stack) */
.key-control-hint .key-control-hint__item {
    color: var(--tutorial-key-hint-color);
    min-height: 30px;
}
```
`GameUiTheme.tss` の `Screens` 区画の末尾（Task 7 の行の次）に `@import url("project://database/Assets/Scripts/Client.Ui/Screens/Tutorial/KeyControlHint/KeyControlHint.uss");` を 1 行足す。
`KeyControlHintView.cs`: ctor で `name`・クラス・拾わない・`CustomStyleResolvedEvent` で `--key-control-hint-floor-offset`＋`--key-control-hint-hotbar-gap` を `style.bottom` に、`--key-control-hint-row-gap` を 2 行目以降の `style.marginTop` に当てる。Model は購読しない（Presenter の `Mount` が `Render` を呼ぶ）。`Render` は行の数を ViewModel に合わせ（足りなければ `ShortcutHintLabel("key-control-hint", ShortcutHintLayout.Prefix)` を作って `AddToClassList(UiTypographyClass.KeyHintText)`・`AddToClassList("key-control-hint__item")`・ctor で作った `_pulse = new TutorialPulseBinding(this)` に `_pulse.Attach(row, TutorialAttentionPulse.StrongScale)`・`row.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore)` で部品の内側まで拾わなくする（`pickingMode` は継承されない。S2-hud D-11 と同じ 1 行。D-36）、余れば消す）、各行に `SetHint(cue.Text, cue.KeyName)`、0 件なら `display: none`。
`KeyControlHintPresenter.cs`（前例 `ToastLayerPresenter`）:
```csharp
    public sealed class KeyControlHintPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly KeyControlHintModel _model;

        public KeyControlHintPresenter(GameUiRoot gameUiRoot, KeyControlHintModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            // Web は stage の viewportOverlay（z-stage-overlay-panel）に置くので、同じ層の Viewport 族へ常駐させる
            // Web mounts it in the stage viewportOverlay (z-stage-overlay-panel), so keep it resident in that layer's Viewport family
            Mount(_gameUiRoot.GetLayer(UiLayer.OverlayPanel, UiFamily.Viewport), _model.Current);
        }

        public static KeyControlHintView Mount(VisualElement layer, IReadOnlyReactiveProperty<KeyControlHintViewModel> model)
        {
            var view = new KeyControlHintView();
            layer.Add(view);
            model.Subscribe(view.Render);
            return view;
        }
    }
```

- [ ] **Step 5: 確かめてコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Tutorial\.Cues\.KeyControlHintTest"`
Expected: `ErrorCount: 0`、5 件 PASS（Web の KeyControlHintHud 4・alwaysOnHudWheel のキー操作ヒントの 1 行）
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Tutorial moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Tutorial "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): ホットバー上のキー操作ヒントを ShortcutHintLabel で写す（S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: ワールドピン（`Screens/Tutorial/WorldPin/`。worldPin.spec 6）

**Files:**
- Create: `S/Client.Ui/Screens/Tutorial/WorldPin/{WorldPinPresenter.cs,WorldPinOverlayView.cs,WorldPinMarkerView.cs,WorldPinEdgeArrowView.cs,WorldPinGlyphs.cs,WorldPin.uss}`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に `WorldPin.uss` の 1 行。D-34）
- Test: `S/Client.Tests/Ui/Screens/Tutorial/Cues/WorldPinOverlayTest.cs`

**Interfaces:**
- Consumes: Task 3 の `WorldPinEdgePlacement.TryPlace`、Task 4 の `WorldPinViewModel`・`WorldPinModel`、Task 5 の `TutorialPulseBinding`、`LocalizedLabel`、`GameUiRoot.GetLayer(UiLayer.WorldPin, UiFamily.Viewport)`
- Produces（namespace `Client.Ui.Screens.Tutorial.WorldPin`）:
  - `WorldPinOverlayView() : VisualElement`（`name = "world-pin-overlay"`。Model を購読しない。`void Render(WorldPinViewModel model)`。ピン 0 件で `display: none`。層の寸法は `GeometryChangedEvent` で取り、端クランプを置き直す）
  - `WorldPinMarkerView : VisualElement`（`name = "world-pin-<pinId>"`。`left/top` を `Length.Percent(ScreenX*100)`・`Percent(ScreenY*100)`、`translate(-50%, -100%)`、子にラベル（`LocalizedLabel`・クラス `world-pin__label`）とマーカー（20×20 の Painter2D）を縦に積む）: `void Show(WorldPinCue cue)`・`void SetColors(Color face, Color stroke)`
  - `WorldPinEdgeArrowView(TutorialPulseBinding pulse) : VisualElement`（`name = "world-pin-arrow-<pinId>"`。56×56、`translate(-50%, -50%)`＋`rotate(angle)` をインラインで持ち、子の矢印本体 `world-pin__arrow-glyph` だけが 1.08 で脈動）: `void Show(string pinId, WorldPinEdgeSpot spot, float size)`・`void SetColors(Color fill, Color stroke, Color shadow)`
  - `WorldPinGlyphs`: `static void DrawMarker(Painter2D, float size, Color face, Color stroke)`（`M12 22 L5 10 A8 8 0 1 1 19 10 Z`・縁 1.5）・`static void DrawArrow(Painter2D, float size, Color fill, Color stroke, Color shadow)`（`M2 8 H13 V3 L22 12 L13 21 V16 H2 Z`・縁 1.5 を塗りの下・影は 2px 下の写し。LX-36）
  - `WorldPinPresenter(GameUiRoot gameUiRoot, WorldPinModel model) : IInitializable` と `static WorldPinOverlayView Mount(VisualElement layer, IReadOnlyReactiveProperty<WorldPinViewModel> model)`（`model.Subscribe(view.Render)` はここ。D-22）
  - 画面ルート `.world-pin-overlay` の受け直し: `--world-pin-view-edge-margin: var(--world-pin-edge-margin)`・`--world-pin-view-arrow-size: var(--world-pin-arrow-size)`・`--world-pin-view-face: var(--world-pin-face)`・`--world-pin-view-stroke: var(--text-high-contrast)`・`--world-pin-view-arrow-fill: var(--tutorial-attention-red)`・`--world-pin-view-arrow-shadow: var(--world-pin-arrow-shadow)`・`--tutorial-attention-pulse-period: var(--tutorial-pulse-duration)`

- [ ] **Step 1: 失敗するテストを書く**

`WorldPinOverlayTest.cs`（`ScreenTestHost` を継ぐ。SetUp は `_model = new FakeWorldPinModel(); _view = WorldPinPresenter.Mount(rootVisualElement, _model.Current);`。`Pins(params WorldPinData[])` は `new WorldPinPresentationData { Revision = 1, Pins = … }` を返す helper。`Pin(id, x, y)`・`EdgePin(id, dx, dy)` で `WorldPinData` を作る。`TutorialGuid` は `TutorialOverlayProjectionTest.LabelGuid`）:
```csharp
        /// <web-test file="e2e/tests/system/worldPin.spec.ts">on-screen world pin renders with its marker tip at the projected viewport position</web-test>
        [Test]
        public void 画面内のピンはマーカーの先端を射影座標に置く()
        {
            _model.Set(WorldPinViewModel.From(Pins(Pin("p1", 0.5f, 0.4f)), false));
            simulate.FrameUpdate();

            var pin = Find<VisualElement>(_view, "world-pin-p1");
            Assert.AreEqual(640f, pin.worldBound.center.x, 1f);
            Assert.AreEqual(288f, pin.worldBound.yMax, 1f);
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">world pin follows updated projections</web-test>
        [Test]
        public void 射影が更新されるとピンが追従する()
        {
            _model.Set(WorldPinViewModel.From(Pins(Pin("p1", 0.5f, 0.4f)), false));
            simulate.FrameUpdate();
            _model.Set(WorldPinViewModel.From(Pins(Pin("p1", 0.25f, 0.75f)), false));
            simulate.FrameUpdate();

            var pin = Find<VisualElement>(_view, "world-pin-p1");
            Assert.AreEqual(320f, pin.worldBound.center.x, 1f);
            Assert.AreEqual(540f, pin.worldBound.yMax, 1f);
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">off-screen world pin renders a 56px filled shaft arrow at the screen edge</web-test>
        [Test]
        public void 画面外のピンは端に56pxの矢印を出す()
        {
            _model.Set(WorldPinViewModel.From(Pins(EdgePin("p1", 1f, 0f)), false));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var arrow = Find<VisualElement>(_view, "world-pin-arrow-p1");
            Assert.AreEqual((56f, 56f), (arrow.resolvedStyle.width, arrow.resolvedStyle.height));
            Assert.AreEqual(1280f - 40f, arrow.worldBound.center.x, 1f);
            Assert.AreEqual(360f, arrow.worldBound.center.y, 1f);
            Assert.IsNull(_view.Q("world-pin-p1"));
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">off-screen arrow follows a diagonal direction to the corner region</web-test>
        [Test]
        public void 斜めの方向は角の領域へ向き角度も回す()
        {
            _model.Set(WorldPinViewModel.From(Pins(EdgePin("p1", 0.7071f, 0.7071f)), false));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var arrow = Find<VisualElement>(_view, "world-pin-arrow-p1");
            Assert.Greater(arrow.worldBound.center.x, 640f);
            Assert.AreEqual(720f - 40f, arrow.worldBound.center.y, 1f);
            Assert.AreEqual(45f, arrow.resolvedStyle.rotate.angle.ToDegrees(), 0.5f);
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">off-screen arrow remains inside the viewport at all four diagonal corners</web-test>
        [TestCase(1f, 1f)]
        [TestCase(-1f, 1f)]
        [TestCase(1f, -1f)]
        [TestCase(-1f, -1f)]
        public void 四隅の斜め方向でも矢印は画面内に収まる(float dx, float dy)
        {
            _model.Set(WorldPinViewModel.From(Pins(EdgePin("p1", dx * 0.7071f, dy * 0.7071f)), false));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var bound = Find<VisualElement>(_view, "world-pin-arrow-p1").worldBound;
            Assert.GreaterOrEqual(bound.xMin, -0.5f);
            Assert.GreaterOrEqual(bound.yMin, -0.5f);
            Assert.LessOrEqual(bound.xMax, 1280.5f);
            Assert.LessOrEqual(bound.yMax, 720.5f);
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">clearing world pins removes the overlay</web-test>
        [Test]
        public void ピンが0件になるとオーバーレイごと消える()
        {
            _model.Set(WorldPinViewModel.From(Pins(Pin("p1", 0.5f, 0.5f)), false));
            simulate.FrameUpdate();
            _model.Set(WorldPinViewModel.From(Pins(), false));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_view));
        }
```
（`[TestCase]` 4 つは Web の 1 件の写し。parity は名前で数えるので 1 件。`/// <web-test>` はメソッドに 1 つ。マーカーの先端は 20×20 の viewBox の y=22/24 で、ピンの下端と一致しない分（20×2/24 ≒ 1.67px 下にはみ出す前の差）は Web と同じ構造なので許容 1px に入る。外れたら Web の `getBoundingClientRect` が svg の箱を見ている点に合わせ、ピンの下端で比べる今の主張のまま差を `done.md` に書く）

Run: `uloop compile --project-path ./moorestech_client`
Expected: `WorldPinOverlayView` が無いのでコンパイルエラー

- [ ] **Step 2: USS を書く**

`WorldPin.uss`:
```css
/* web: W/features/tutorial/worldPin.module.css（.overlay・.pin・.label・.marker・.arrow・.arrow svg）。scale(var(--ui-scale)) はパネル単位＝stage px なので要らない */
/* web: W/features/tutorial/worldPin.module.css (.overlay, .pin, .label, .marker, .arrow, .arrow svg); no --ui-scale since panel units are stage px */
.world-pin-overlay {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
    --world-pin-view-edge-margin: var(--world-pin-edge-margin);
    --world-pin-view-arrow-size: var(--world-pin-arrow-size);
    --world-pin-view-face: var(--world-pin-face);
    --world-pin-view-stroke: var(--text-high-contrast);
    --world-pin-view-arrow-fill: var(--tutorial-attention-red);
    --world-pin-view-arrow-shadow: var(--world-pin-arrow-shadow);
    --tutorial-attention-pulse-period: var(--tutorial-pulse-duration);
}

.world-pin {
    position: absolute;
    flex-direction: column;
    align-items: center;
    translate: -50% -100%;
}

.world-pin__label {
    margin: 0 0 2px 0;
    padding: var(--label-face-padding);
    color: var(--text-high-contrast);
    background-color: var(--world-pin-face);
    white-space: nowrap;
    font-size: var(--label-face-font-size);
}

.world-pin__marker {
    width: 20px;
    height: 20px;
}

.world-pin__arrow {
    position: absolute;
}

.world-pin__arrow-glyph {
    width: 100%;
    height: 100%;
}
```
（ラベルの padding は Task 6 Step 4 の結果に合わせる）

`GameUiTheme.tss` の `Screens` 区画の末尾（Task 8 の行の次）に `@import url("project://database/Assets/Scripts/Client.Ui/Screens/Tutorial/WorldPin/WorldPin.uss");` を 1 行足す。

- [ ] **Step 3: 字形・View・Presenter を書く**

`WorldPinGlyphs.cs`:
```csharp
    public static class WorldPinGlyphs
    {
        private const float ViewBox = 24f;
        private const float StrokeWidth = 1.5f;
        private static readonly Vector2[] ArrowPath = { new(2f, 8f), new(13f, 8f), new(13f, 3f), new(22f, 12f), new(13f, 21f), new(13f, 16f), new(2f, 16f) };

        public static void DrawMarker(Painter2D painter, float size, Color face, Color stroke)
        {
            // M12 22 L5 10 A8 8 0 1 1 19 10 Z。弧の中心は (12, 10-√15)、大円弧を時計回り
            // M12 22 L5 10 A8 8 0 1 1 19 10 Z; the arc center is (12, 10-√15), the large arc runs clockwise
            var unit = size / ViewBox;
            var center = new Vector2(12f, 10f - Mathf.Sqrt(15f)) * unit;
            var start = Mathf.Atan2(10f - (10f - Mathf.Sqrt(15f)), 5f - 12f) * Mathf.Rad2Deg;
            var end = Mathf.Atan2(10f - (10f - Mathf.Sqrt(15f)), 19f - 12f) * Mathf.Rad2Deg + 360f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(12f, 22f) * unit);
            painter.LineTo(new Vector2(5f, 10f) * unit);
            painter.Arc(center, 8f * unit, Angle.Degrees(start), Angle.Degrees(end), ArcDirection.Clockwise);
            painter.ClosePath();
            painter.fillColor = face;
            painter.Fill();
            painter.strokeColor = stroke;
            painter.lineWidth = StrokeWidth * unit;
            painter.Stroke();
        }

        public static void DrawArrow(Painter2D painter, float size, Color fill, Color stroke, Color shadow)
        {
            // 影は blur を持てないので 2px 下の写しで近似し（LX-36）、縁を塗りの下に描く（paint-order: stroke fill）
            // Approximate the blurred shadow with a copy 2px down (LX-36), then paint the stroke under the fill (paint-order: stroke fill)
            var unit = size / ViewBox;
            Trace(painter, unit, new Vector2(0f, 2f));
            painter.fillColor = shadow;
            painter.Fill();
            Trace(painter, unit, Vector2.zero);
            painter.strokeColor = stroke;
            painter.lineWidth = StrokeWidth * unit;
            painter.lineJoin = LineJoin.Round;
            painter.Stroke();
            painter.fillColor = fill;
            painter.Fill();
        }

        private static void Trace(Painter2D painter, float unit, Vector2 offset)
        {
            painter.BeginPath();
            painter.MoveTo(ArrowPath[0] * unit + offset);
            for (var i = 1; i < ArrowPath.Length; i++) painter.LineTo(ArrowPath[i] * unit + offset);
            painter.ClosePath();
        }
    }
```
（Web の `stroke-width: 1.5` は viewBox 単位なので、56px の矢印では 3.5px、20px のマーカーでは 1.25px になる。SVG と同じく `unit` を掛ける）

`WorldPinMarkerView.cs`: ctor で クラス `world-pin`・拾わない・子 `LocalizedLabel`（`world-pin__label`）と `_marker`（`world-pin__marker`。`generateVisualContent` で `DrawMarker`、色はオーバーレイの `CustomStyleResolvedEvent` で受けた `--world-pin-view-face`・`--world-pin-view-stroke` を `SetColors(face, stroke)` で渡す）。`Show(cue)` は `name = $"world-pin-{cue.PinId}"`・`style.left = Length.Percent(cue.ScreenX * 100f)`・`style.top = Length.Percent(cue.ScreenY * 100f)`・ラベルの `SetText(cue.Label)`。
`WorldPinEdgeArrowView.cs`: ctor で クラス `world-pin__arrow`・子 `_glyph`（`world-pin__arrow-glyph`。`generateVisualContent` で `DrawArrow`）・`pulse.Attach(_glyph, TutorialAttentionPulse.StrongScale)`（`pulse` はオーバーレイが ctor で作った 1 つ。位置と回転は親のインライン、脈動は子。ADR 0039）。`Show(string pinId, WorldPinEdgeSpot spot, float size)` は `name`・`style.width/height = size`・`style.left = spot.Left`・`style.top = spot.Top`・`style.translate = new Translate(Length.Percent(-50), Length.Percent(-50))`・`style.rotate = new Rotate(Angle.Degrees(spot.AngleDegrees))`。
`WorldPinOverlayView.cs`: Model は購読しない。`Render(model)` は受けた ViewModel を `_current` に持ち、`GeometryChangedEvent` と `CustomStyleResolvedEvent` で `Render(_current)` をやり直す。`Render` はピンの照合（`PinId` ごとに画面内なら `WorldPinMarkerView`、画面外なら `WorldPinEdgeArrowView`。`OnScreen` が切り替わったら作り直す）、画面外は `WorldPinEdgePlacement.TryPlace(layout.width, layout.height, _edgeMargin, cue.DirectionX, cue.DirectionY, out var spot)` が false なら隠す、0 件なら `display: none`。
`WorldPinPresenter.cs`: `Initialize` は `Mount(GetLayer(UiLayer.WorldPin, UiFamily.Viewport), _model.Current)`（Web は Portal の `--z-portal-world-pin`）。`Mount` は `var view = new WorldPinOverlayView(); layer.Add(view); model.Subscribe(view.Render); return view;`（KeyControlHint と同じ形）。

- [ ] **Step 4: 確かめてコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Tutorial\.Cues\.WorldPinOverlayTest"`
Expected: `ErrorCount: 0`、6 件（TestCase 込みで 9 回）PASS
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Tutorial moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Tutorial "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): ワールドピン（画面内のマーカーと画面端の矢印）を Web から写す（S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: e2e の写し（tutorial.spec 6・portalLayerScaling 3）

**Files:**
- Test: `S/Client.Tests/Ui/Screens/Tutorial/Cues/{TutorialStageCueTest.cs,TutorialViewportScalingTest.cs}`

**Interfaces:**
- Consumes: Task 6・8・9 の Presenter の `Mount`、`FakeTutorialOverlayModel`（Task 5 の土台。`Cues/` からも使うので `TutorialOverlayTestHost` を継承する）
- Produces: なし（テストだけ）

- [ ] **Step 1: tutorial.spec を写す**

`TutorialStageCueTest.cs`（`TutorialOverlayTestHost` を継承。SetUp で `TutorialOverlayPresenter.Mount(rootVisualElement, rootVisualElement, _overlay.Current)` と `KeyControlHintPresenter.Mount(rootVisualElement, _keyHint.Current)` の両方を通す）:
```csharp
        /// <web-test file="e2e/tests/system/tutorial.spec.ts">tutorial outline highlights the target without dimming the rest of the screen</web-test>
        [Test]
        public void 枠は対象を囲み画面の残りを暗くしない()
        {
            MountAnchor(rootVisualElement, "pause.save", 400, 300, 200, 40);
            _overlay.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", null))));
            simulate.FrameUpdate();

            var ring = _overlayView.Q(className: "tutorial-outline__ring").worldBound;
            Assert.AreEqual(new Rect(392, 292, 216, 56), new Rect(Mathf.Round(ring.x), Mathf.Round(ring.y), Mathf.Round(ring.width), Mathf.Round(ring.height)));
            // 全面の幕を持たない（オーバーレイ自身は透明で拾わない）
            // No full-screen scrim (the overlay itself is transparent and not pickable)
            Assert.AreEqual(0f, _overlayView.resolvedStyle.backgroundColor.a);
            Assert.AreEqual(PickingMode.Ignore, _overlayView.pickingMode);
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">outline with a label renders the label text beside the outline</web-test>
        [Test]
        public void ラベル付きの枠は枠の脇に文を出す()
        {
            MountAnchor(rootVisualElement, "pause.save", 400, 300, 200, 40);
            _overlay.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var label = _overlayView.Q<TutorialHighlightLabelView>();
            Assert.IsTrue(IsShown(label));
            Assert.IsNotEmpty(label.text);
            Assert.AreEqual(400f - 8f, label.worldBound.xMin, 0.5f);
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">keyControl hint renders a kbd and text above the hotbar while uiState matches</web-test>
        [Test]
        public void 画面状態が一致する間はホットバーの上にkbdと文を出す()
        {
            _keyControl.Set(new KeyControlHintProjection().Project(Data(1, KeyHint("k1", "Tab", "GameScreen")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();

            var hint = _keyControlView.Q<ShortcutHintLabel>("key-control-hint");
            Assert.AreEqual("Tab", hint.Q<KeyCap>().text);
            var floor = Px("--hotbar-floor-offset") + Px("--tutorial-key-hint-hotbar-gap");
            Assert.AreEqual(720f - floor, _keyControlView.worldBound.yMax, 1f);
            Assert.AreEqual(640f, hint.worldBound.center.x, 1f);
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">keyControl hint actually plays the strong 1.08 pulse in the browser</web-test>
        [Test]
        public void キー操作ヒントは1_08の脈動を実際に流す()
        {
            _keyControl.Set(new KeyControlHintProjection().Project(Data(1, KeyHint("k1", "Tab", "GameScreen")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();
            // 最初の呼び出しを経過 0 として開始する（UiMotionHandle）
            // The first scheduler call starts the motion at elapsed 0 (UiMotionHandle)
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(Ms("--tutorial-pulse-duration") / 2f);
            Assert.AreEqual(1.08f, _keyControlView.Q<ShortcutHintLabel>("key-control-hint").resolvedStyle.scale.value.x, 0.005f);
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">tutorial outline actually plays the subtle 1.03 pulse in the browser</web-test>
        [Test]
        public void 枠は1_03の脈動を実際に流しラベルは脈動しない()
        {
            MountAnchor(rootVisualElement, "pause.save", 400, 300, 200, 40);
            _overlay.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(Ms("--tutorial-pulse-duration") / 2f);

            Assert.AreEqual(1.03f, _overlayView.Q(className: "tutorial-outline__clip").resolvedStyle.scale.value.x, 0.005f);
            Assert.AreEqual(1f, _overlayView.Q<TutorialHighlightLabelView>().resolvedStyle.scale.value.x, 0.0001f);
        }

        /// <web-test file="e2e/tests/system/tutorial.spec.ts">outline never outlives its anchor even when no frame arrives</web-test>
        [Test]
        public void 演出の更新が来なくてもアンカーが消えたフレームで枠も消える()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 400, 300, 200, 40);
            _overlay.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", null))));
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(_overlayView.Q(className: "tutorial-outline__clip")));

            LogAssert.Expect(LogType.Warning, new Regex("anchor is missing: pause.save"));
            anchor.RemoveFromHierarchy();
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_overlayView.Q(className: "tutorial-outline__clip")));
        }
```
（`Px(name)`・`Ms(name)` は Task 5 の `TutorialOverlayTestHost` の helper。`TutorialViewportScalingTest` も同じ土台を継ぐ。`KeyHint` は `KeyControlHintTest.Hint` を `internal static` にして共有する）

- [ ] **Step 2: portalLayerScaling を 2 解像度で写す**

`TutorialViewportScalingTest.cs`（前例 `PauseMenuSettingsLayoutTest` の形。`[TestFixture(1280f, 720f)]`・`[TestFixture(1706.7f, 720f)]`。2560×1080 は Expand（参照 1280×720）で 1706.7×720。`OneTimeSetUp` で `panelSize = _logicalSize`。SetUp で `TutorialOverlayPresenter.Mount` と `WorldPinPresenter.Mount` を通す）:
```csharp
        /// <web-test file="e2e/tests/system/portalLayerScaling.spec.ts">ワールドピンは高解像度で拡大しても先端が射影座標に留まる</web-test>
        [Test]
        public void ワールドピンの先端はどの解像度でも射影座標に留まる()
        {
            _pins.Set(WorldPinViewModel.From(Pins(Pin("p1", 0.3f, 0.6f)), false));
            simulate.FrameUpdate();
            var pin = Find<VisualElement>(_pinView, "world-pin-p1").worldBound;
            Assert.AreEqual(_logicalSize.x * 0.3f, pin.center.x, 1f);
            Assert.AreEqual(_logicalSize.y * 0.6f, pin.yMax, 1f);
        }

        /// <web-test file="e2e/tests/system/portalLayerScaling.spec.ts">チュートリアルの枠線ラベルは高解像度で拡大しても枠線に付いたまま</web-test>
        [Test]
        public void 枠線ラベルはどの解像度でも枠に付いたまま()
        {
            MountAnchor(rootVisualElement, "pause.save", _logicalSize.x / 2f - 100f, 300, 200, 40);
            _overlay.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var ring = _overlayView.Q(className: "tutorial-outline__ring").worldBound;
            var label = _overlayView.Q<TutorialHighlightLabelView>().worldBound;
            Assert.AreEqual(ring.yMax + Px("--tutorial-highlight-label-gap"), label.yMin, 0.5f);
            Assert.AreEqual(ring.xMin, label.xMin, 0.5f);
        }

        /// <web-test file="e2e/tests/system/portalLayerScaling.spec.ts">画面端矢印は高解像度で拡大しても余白ごと広がり画面内に収まる</web-test>
        [Test]
        public void 画面端の矢印はどの解像度でも余白を保って画面内に収まる()
        {
            _pins.Set(WorldPinViewModel.From(Pins(EdgePin("p1", 1f, 0f)), false));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var arrow = Find<VisualElement>(_pinView, "world-pin-arrow-p1").worldBound;
            Assert.AreEqual(_logicalSize.x - Px("--world-pin-edge-margin"), arrow.center.x, 1f);
            Assert.LessOrEqual(arrow.xMax, _logicalSize.x + 0.5f);
        }
```
（Web は `--ui-scale` で拡大する Portal 層を確かめている。UITK はパネル単位＝stage px なので「論理寸法が広がっても比で同じ所に出る」ことが同じ意図になる。`Pin`・`EdgePin`・`Pins` は `WorldPinOverlayTest` から `internal static` にして共有する）

- [ ] **Step 3: 確かめてコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Tutorial\.Cues\."`
Expected: `ErrorCount: 0`、KeyControl 5・WorldPin 6・StageCue 6・Scaling 3×2 が PASS
```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Tutorial
git commit -m "test(uitk): tutorial.spec と portalLayerScaling のピン・枠を 2 解像度の L1 に写す（S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: 見た目の規則（tutorialAttentionDesign 21）

**Files:**
- Test: `S/Client.Tests/Ui/Screens/Tutorial/Design/{TutorialDesignSource.cs,TutorialAttentionTokenTest.cs,KeyControlHintDesignTest.cs,TutorialRingDesignTest.cs,WorldPinArrowDesignTest.cs}`

**Interfaces:**
- Consumes: Task 5〜9 の USS と .cs（ファイルを文字列で読む）、`ThemeTokenFile.Resolve`
- Produces: `TutorialDesignSource`（`static string ReadScreenFile(string relativePath)`: `Application.dataPath + "/Scripts/Client.Ui/Screens/Tutorial/" + relativePath` を読む。`static string Rule(string uss, string selector)`: USS からセレクタの 1 ブロックを取り出す。`static readonly Regex ColorLiteral = new(@"#[0-9a-fA-F]{3,8}\b|rgba?\(")`・`MillisecondLiteral = new(@"\b\d+(\.\d+)?m?s\b")`）

Web の 21 件は「CSS のどこに何を書くか」の静的検査で、UITK では USS・C# の同じ規則に読み替える。読み替えを次の表に固定する（各行が 1 テスト。`/// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">` に Web の名前）。

| ファイル | Web の名前 | UITK での主張 |
|---|---|---|
| TutorialAttentionTokenTest | 原色赤と周期はtokensが唯一の正 | `ThemeTokenFile.Resolve("--tutorial-attention-red") == "#ff0000"`・`--tutorial-pulse-duration == "1200ms"`。Tutorial 配下の USS 4 本に `#ff0000`・`1200ms` が無い |
| 同 | グローは赤から導出し、色の決定を二重に持たない | `--tutorial-attention-glow` の RGB が `--tutorial-attention-red` と同じ (255,0,0)。Tutorial 配下の USS・.cs に `--tutorial-attention-glow` の値の写しが無い（`var(--tutorial-attention-glow)` だけ） |
| 同 | 脈動キーフレームは振幅を実数値で焼き、var()参照を残さない | `TutorialAttentionPulse.StrongScale == 1.08f`・`SubtleScale == 1.03f`（定数。実行時の値読みではない） |
| 同 | 脈動キーフレームはtokens.cssにだけ存在し、機能側へ複製されない | `Screens/Tutorial/**/*.cs` で `1.08f`・`1.03f` を含むのは `Pulse/TutorialAttentionPulse.cs` だけ |
| 同 | 利用側は素名のanimation直書きを復活させない（CSS Modulesがハッシュ化しキーフレームに届かなくなる） | Tutorial 配下の USS に `transition` と `scale:` の指定が無い（脈動は `TutorialPulseBinding` の 1 経路だけ） |
| 同 | 利用側は振幅を持たず、tokensの名前トークンだけを参照する | 脈動を使う .cs（Overlay・KeyControlHint・WorldPin）は `TutorialAttentionPulse.StrongScale/SubtleScale` だけを渡し、USS は周期を `var(--tutorial-pulse-duration)` で受け直すだけ |
| KeyControlHintDesignTest | 文字色は原色赤トークンを指す | `.key-control-hint` のブロックに `--key-cap-color: var(--tutorial-key-hint-color)`、`.key-control-hint .key-control-hint__item` の `color: var(--tutorial-key-hint-color)`、`--tutorial-key-hint-color` の解決値が `#ff0000` |
| 同 | 1.08の拡縮ループを共有キーフレームで持つ | `KeyControlHintView.cs` に `TutorialAttentionPulse.StrongScale` と `new TutorialPulseBinding(` |
| 同 | 機能側CSSに色と秒数を直書きしない | `KeyControlHint.uss` に `ColorLiteral`・`MillisecondLiteral` が 0 件 |
| 同 | 共有様式keyHintTextの文字色は白のままで、インベントリ左下・研究左下を巻き込まない | `Typography.uss` の `.key-hint-text` が `color: var(--text-high-contrast)` のまま、`--text-high-contrast` の解決値が白、Tutorial 配下の USS に `--text-high-contrast:` の受け直しが 1 つも無い |
| TutorialRingDesignTest | 枠線とグローの両方が原色赤トークンを指し、旧来の黄が残らない | `.tutorial-outline__ring` が `var(--tutorial-attention-red)`、`.tutorial-outline__glow` が `var(--tutorial-attention-glow)`、`TutorialOverlay.uss` に `yellow`・`--tutorial-highlight-color` が無い |
| 同 | 拡縮は1.03で、内側ノードを足さず既存の.highlight自身に付ける | `TutorialOutlineView.cs` の `TutorialPulseBinding` の対象が `_clip`（枠そのものの容器）で、振幅が `SubtleScale` |
| 同 | ラベル面は脈動せず、既存のstage同率スケールを保つ | `TutorialHighlightLabelView.cs` に `TutorialPulseBinding` が無い。L1 の脈動テスト（Task 10）がラベルの scale 1 を見る |
| 同 | 機能側CSSに色リテラルと秒数リテラルを直書きしない | `TutorialOverlay.uss` に `ColorLiteral`・`MillisecondLiteral` が 0 件 |
| 同 | ドラッグガイド矢印は対象外で、移動ループのまま据え置く | `DragGuide/*.cs` に `TutorialPulseBinding`・`TutorialAttentionPulse` が無く、`TutorialDragGuideStep` を使う |
| WorldPinArrowDesignTest | 塗りは原色赤トークンで、世界分離用の縁取りは残す | `.world-pin-overlay` に `--world-pin-view-arrow-fill: var(--tutorial-attention-red)` と `--world-pin-view-face: var(--world-pin-face)`、`WorldPinGlyphs.cs` の `DrawArrow` が `Stroke()` を呼ぶ |
| 同 | 脈動はsvg側に付け、1.08で回す | `WorldPinEdgeArrowView.cs` の `TutorialPulseBinding` の対象が `_glyph`、振幅 `StrongScale` |
| 同 | .arrow div側にはanimationもtransformも付けない（インラインtransformを潰さないため） | `WorldPin.uss` の `.world-pin__arrow` のブロックに `scale`・`rotate`・`translate` が無い（どれも C# のインライン） |
| 同 | 矢印の位置と回転はTSXのインラインtransformが持ち続ける | `WorldPinEdgeArrowView.cs` が `style.rotate` と `style.translate` を書く |
| 同 | 機能側CSSに色リテラルと秒数リテラルを直書きしない | `WorldPin.uss` に `ColorLiteral`・`MillisecondLiteral` が 0 件 |
| 同 | ピン本体のラベル・マーカーは据え置く | `WorldPinMarkerView.cs` に `TutorialPulseBinding` が無く、`.world-pin__label` が `var(--world-pin-face)`・`var(--label-face-font-size)` を持つ |

- [ ] **Step 1: 21 件を書く**

例（他の 20 件も同じ形で、上の表の主張を `Assert` にする）:
```csharp
        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">機能側CSSに色リテラルと秒数リテラルを直書きしない</web-test>
        [Test]
        public void 枠のUSSに色と秒の直書きが無い()
        {
            var uss = TutorialDesignSource.ReadScreenFile("Overlay/TutorialOverlay.uss");
            Assert.IsFalse(TutorialDesignSource.ColorLiteral.IsMatch(uss), uss);
            Assert.IsFalse(TutorialDesignSource.MillisecondLiteral.IsMatch(uss), uss);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">.arrow div側にはanimationもtransformも付けない（インラインtransformを潰さないため）</web-test>
        [Test]
        public void 矢印の外側の要素はUSSで回転も拡縮も持たない()
        {
            var rule = TutorialDesignSource.Rule(TutorialDesignSource.ReadScreenFile("WorldPin/WorldPin.uss"), ".world-pin__arrow");
            StringAssert.DoesNotContain("scale", rule);
            StringAssert.DoesNotContain("rotate", rule);
            StringAssert.DoesNotContain("translate", rule);
        }
```
（`MillisecondLiteral` は `--tutorial-overlay-ring-radius: 8px` の `8px` に掛からないよう `\d+m?s\b` で ms と s だけを拾い、`px` を拾わない。USS のコメント行（`/* web: … */`）に Web の CSS の数字が出るので、`ReadScreenFile` はコメントを外した本文を返す）

- [ ] **Step 2: 確かめてコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Tutorial\.Design\."`
Expected: `ErrorCount: 0`、21 件 PASS
```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Tutorial/Design
git commit -m "test(uitk): チュートリアルの注意喚起の見た目の規則 21 件を USS・C# の静的検査に写す（S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: 登録・tss・アンカーのカバレッジ L1（plan §4.5）

**Files:**
- Create: `S/Client.Starter/Registration/GameUi/TutorialGameUiRegistration.cs`
- Modify: `S/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 1 行）
- 確かめるだけ: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画の 4 行は Task 6〜9 が入れ済み。D-34）
- Test: `S/Client.Tests/Ui/Anchor/{TutorialAnchorCoverageTest.cs,TutorialAnchorCoveragePending.cs}`

**Interfaces:**
- Consumes: Task 4 の Model 3 つ（`BlockingSkitPresence` は S2-hud の `HudGameUiRegistration` が登録済みなので登録しない）、Task 6・8・9 の Presenter、`AnchorIds.StaticIds`・`AnchorIds.*Prefix`、前例 `ToastGameUiRegistration`（`builder.RegisterEntryPoint<…>()`）・`MainGameUiStateRegistration`（`builder.RegisterEntryPoint<PauseMenuModel>().AsSelf()`）、`PauseMenuView` の ctor（`PauseMenuViewTest` の SetUp と同じ引数）
- Produces:
  - `TutorialGameUiRegistration.Register(ContainerBuilder builder)`
  - `TutorialAnchorCoveragePending.Owners : IReadOnlyDictionary<string, string>`（未移植のアンカー id → 持ち主レーン。行の消し方の規則はファイル冒頭のコメントに書く: 印を付けたレーンが同じ PR で自分の行を消す・S5-ladder で空になる。指揮が execution-plan E-1 にも書く。指揮の割り当て E）

- [ ] **Step 1: 登録を書く**

`TutorialGameUiRegistration.cs`:
```csharp
using Client.Ui.Screens.Tutorial.KeyControlHint;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.Screens.Tutorial.WorldPin;
using Client.Ui.State.Tutorial;
using Client.Ui.State.Tutorial.KeyControlHint;
using Client.Ui.State.Tutorial.Overlay;
using Client.Ui.State.Tutorial.WorldPin;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // チュートリアルの枠・キー操作ヒント・ワールドピン（GameUi）の登録
    // Registers the GameUi tutorial outline, key-control hint and world pin
    internal static class TutorialGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<TutorialOverlayModel>().AsSelf();
            builder.RegisterEntryPoint<KeyControlHintModel>().AsSelf();
            builder.RegisterEntryPoint<WorldPinModel>().AsSelf();
            builder.RegisterEntryPoint<TutorialOverlayPresenter>();
            builder.RegisterEntryPoint<KeyControlHintPresenter>();
            builder.RegisterEntryPoint<WorldPinPresenter>();
        }
    }
}
```
`MainGameUiRegistration.cs` の画面登録区画の末尾（`ToastGameUiRegistration.Register(builder);` の次。ほかのレーンが先に足していればその後）に `TutorialGameUiRegistration.Register(builder);` を 1 行。
`GameUiTheme.tss` の `Screens` 区画に、Task 6〜9 が足した 4 行があることを確かめる（足さない。D-34）:
```bash
grep -n "Screens/Tutorial/" "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
```
Expected: `Overlay/TutorialOverlay.uss`・`DragGuide/TutorialDragGuide.uss`・`KeyControlHint/KeyControlHint.uss`・`WorldPin/WorldPin.uss` の 4 行。欠けていれば、その USS を作ったタスクのコミットに漏れがあるので、そのタスクの手順で 1 行足してこのタスクのコミットに含める

- [ ] **Step 2: カバレッジの失敗するテストを書く**

`TutorialAnchorCoveragePending.cs`（Task 1 Step 1 の grep で印が増えていたら、その id の行を入れない。このレーンは S2-hud の後に入るので、S2-hud が印を付けた `game.crosshair`（`AnchorIds.GameCrosshair`）・`challenge.current-hud`（`ChallengeCurrentHud`）・`mining.hud`（`MiningHud`）・`train-hud.status`（`TrainHudStatus`）の 4 行は最初から入れない。Task 1 Step 1 の grep でこの 4 つの `Mark` が出なければ、S2-hud の取り込みが欠けているので止めて指揮へ報告する）:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;

namespace Client.Tests.Ui.Anchor
{
    /// <summary>
    /// UITK で未移植の画面のアンカー（id → 印を付けるレーン）。規則: 印を付けたレーンが同じ PR で自分の行を消す。行を残したまま印を付けると TutorialAnchorCoverageTest が落ちる。S5-ladder で空になる（plan §4.5・execution-plan E-1）
    /// Anchors of screens not yet ported to UITK (id → lane that marks them). Rule: the marking lane deletes its rows in the same PR; marking while the row remains fails TutorialAnchorCoverageTest. S5-ladder empties the table (plan §4.5, execution-plan E-1)
    /// </summary>
    public static class TutorialAnchorCoveragePending
    {
        public static readonly IReadOnlyDictionary<string, string> Owners = new Dictionary<string, string>
        {
            { AnchorIds.PlacementHud, "S2-mode-tooltip" },
            { AnchorIds.DeleteHud, "S2-mode-tooltip" },
            { AnchorIds.HotbarHud, "S3-hotbar-modal" },
            { AnchorIds.BuildMenuEntryPrefix, "S3-buildmenu" },
            { AnchorIds.InventoryCloseButton, "S4-alpha" },
            { AnchorIds.EquipmentHud, "S4-inventory" },
            { AnchorIds.EquipmentSelectedSlot, "S4-inventory" },
            { AnchorIds.EquipmentSlotPrefix, "S4-inventory" },
            { AnchorIds.InventoryItemPrefix, "S4-inventory" },
            { AnchorIds.RecipeCraftButton, "S4-recipe" },
            { AnchorIds.RecipeItemPrefix, "S4-recipe" },
            { AnchorIds.ResearchNodePrefix, "S4-research" },
            { AnchorIds.ChallengePanel, "S5-challenge" },
            { AnchorIds.ChallengeCategories, "S5-challenge" },
            { AnchorIds.ChallengeNodePrefix, "S5-challenge" },
        };
    }
}
```
`TutorialAnchorCoverageTest.cs`:
```csharp
    public class TutorialAnchorCoverageTest : ScreenTestHost
    {
        [Test]
        public void 全てのアンカーIDは印付けか未移植の表のどちらか一方にだけ載る()
        {
            var marked = MarkedAnchorIds();
            var problems = new List<string>();
            foreach (var anchorId in AllAnchorIds())
            {
                // 印も表も無ければ枠が永久に出ない。両方にあれば表の消し忘れ
                // Neither means the outline never shows; both means a stale table row
                var isMarked = marked.Contains(anchorId);
                var isPending = TutorialAnchorCoveragePending.Owners.ContainsKey(anchorId);
                if (!isMarked && !isPending) problems.Add($"{anchorId}: UITK で印が無く、未移植の表にも無い");
                if (isMarked && isPending) problems.Add($"{anchorId}: 印が付いたので未移植の表から {TutorialAnchorCoveragePending.Owners[anchorId]} の行を消す");
            }
            CollectionAssert.IsEmpty(problems);

            #region Internal

            IEnumerable<string> AllAnchorIds()
            {
                // 固定 id と、接頭辞で作る id の接頭辞（AnchorIds の *Prefix 定数）
                // Static ids plus the prefixes of generated ids (AnchorIds *Prefix constants)
                var prefixes = typeof(AnchorIds).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral && field.Name.EndsWith("Prefix")).Select(field => (string)field.GetRawConstantValue());
                return AnchorIds.StaticIds.Concat(prefixes);
            }

            HashSet<string> MarkedAnchorIds()
            {
                // TutorialAnchor.Mark を呼ぶ Client.Ui のファイルが参照する AnchorIds の名を拾い、id に写す（工場メソッドは名前＋Prefix の定数）
                // Collect AnchorIds names referenced by Client.Ui files that call TutorialAnchor.Mark and map them to ids (factory methods map to Name+Prefix)
                var ids = new HashSet<string>();
                var root = Path.Combine(Application.dataPath, "Scripts", "Client.Ui");
                foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    var source = File.ReadAllText(file);
                    if (!source.Contains("TutorialAnchor.Mark(")) continue;
                    foreach (Match match in Regex.Matches(source, @"AnchorIds\.(\w+)"))
                    {
                        var field = typeof(AnchorIds).GetField(match.Groups[1].Value) ?? typeof(AnchorIds).GetField(match.Groups[1].Value + "Prefix");
                        if (field != null && field.IsLiteral) ids.Add((string)field.GetRawConstantValue());
                    }
                }
                return ids;
            }

            #endregion
        }

        [Test]
        public void ポーズの6つのアンカーはポーズを載せると一意に解ける()
        {
            var model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.Top));
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            Mount(new PauseMenuView(model.Current, settings.Current, new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, Toaster, new BugReportDraft()));
            simulate.FrameUpdate();
            foreach (var anchorId in new[] { AnchorIds.PauseMenu, AnchorIds.PauseSave, AnchorIds.PauseBack, AnchorIds.PauseSettings, AnchorIds.PauseBugReport, AnchorIds.PauseBackToTop })
            {
                Assert.IsInstanceOf<TutorialAnchorResolution.Unique>(TutorialAnchorRegistry.Resolve(rootVisualElement, anchorId), anchorId);
            }
        }
    }
```
（2 件目は `S/Client.Tests/Ui/Screens/PauseMenu/PauseMenuSettingsLayoutTest.cs` の SetUp と同じ偽モデル・ctor。偽モデルの namespace を grep して using を足す。サブページの「トップへ戻る」が組み立て時に木にあることを前提にしており、ページを開いた時に作る実物なら `model.Set(FakePauseMenuModel.Page(PauseMenuPage.Settings))` の後に `pause.back-to-top` を確かめる）

- [ ] **Step 3: 確かめてコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Anchor|Theme|Conventions)\."`
Expected: `ErrorCount: 0`、カバレッジ 2 件・`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest` が PASS。カバレッジが「印も表も無い」で落ちたら、その id を grep で探し直して表へ持ち主を足す（`.agents/skills/ref-uitk-design` の画面の持ち主か実行計画 §1.4 の行で決める）
```bash
git add moorestech_client/Assets/Scripts
git commit -m "feat(uitk): チュートリアルの 3 画面を登録し、アンカーのカバレッジ L1 を足す（S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: 処遇・台帳の記帳と test-name-parity

**順序**: Task 14 の並べ比べを済ませてから行う（LX の行は Web と並べた結果と Task 14 の URL を書く。実行順は Task 12 → 14 → 13 → 15。D-35）

**Files:**
- Modify: `docs/uitk/disposition.md`（「## 処遇」の表に 2 行）
- Modify: `docs/uitk/TODO.md`（「## 見た目の例外」末尾に LX-35〜LX-37、「## 実測の結論」末尾に 2 行）
- Modify: LX の印（`TutorialDragGuideGlyph.cs`・`WorldPinGlyphs.cs`・`TutorialAttentionPulse.cs`）

**Interfaces:**
- Consumes: Task 6〜12 の実物、`node tools/uitk/test-name-parity.mjs`
- Produces: 処遇 2 行・LX 3 行・実測 2 行

- [ ] **Step 1: 処遇を書く**

`docs/uitk/disposition.md` の「## 処遇」の表の末尾に:
```markdown
| 5 | `src/features/tutorial/overlay/TutorialOverlay.test.ts` | item master未ロード中は所持アンカーのackを送らず、到着後に送り直す | 捨てる | ack は越境の通知で、C# の受け手 `TutorialAnchorAckAction` は検証して返すだけ（store・サーバー・ログに効果が無い）。UITK は解決する側が C# 自身なので ack を作らない。加えて UITK は `MasterHolder` がゲーム開始前に読み終わっており、マスタ未ロードの状態が無い | #<PR番号> |
| 5 | `src/features/tutorial/overlay/TutorialOverlay.test.ts` | 同一anchorを共有する全highlightへackを配る | 捨てる | ack は越境の通知で、C# の受け手 `TutorialAnchorAckAction` は検証して返すだけ（store・サーバー・ログに効果が無い）。UITK は解決する側が C# 自身なので ack を作らない。同じアンカーを共有する全ての枠を描くことは L1 `TutorialOverlayAnchorTest` と `TutorialStageCueTest` が見る | #<PR番号> |
```

- [ ] **Step 2: 見た目の例外を書く**

`docs/uitk/TODO.md` の「## 見た目の例外」の表の末尾に（撮影は Task 14 の share の URL を入れる）。LX-35・LX-37 は Task 14 で Web の `4-drag-guide.webm` のコマと UITK の `fx-drag-guide` の連番を並べ、影の縁の差と曲線の差が下の記述どおりかを done.md の「差の表」で確かめてから書く。違えば記述を実測に直す:
```markdown
| LX-35 | DragGuide のカーソル矢印の影 | Web は `filter: drop-shadow(0 1px 2px var(--tutorial-drag-guide-shadow))`、UITK は 1px 下へずらしたにじまない写し | Painter2D にぼかしが無く、同じにするには影のテクスチャを自作する必要がある。差は影の縁の柔らかさだけで、動いている矢印では見分けにくい | ずらした写しのまま | <Task 14 の URL> | agent 既定（S2-tutorial D-15） |
| LX-36 | ワールドピンの画面端矢印の影 | Web は `filter: drop-shadow(0 2px 3px var(--world-pin-arrow-shadow))`、UITK は 2px 下へずらしたにじまない写し | LX-35 と同じ | ずらした写しのまま | <Task 14 の URL> | agent 既定（S2-tutorial D-15） |
| LX-37 | 注意の脈動・DragGuide の動きの曲線 | Web は CSS の `ease-in-out`（cubic-bezier(0.42, 0, 0.58, 1)）、UITK は `UiEasing.EaseInOutSine` | 2 つの曲線の進みの差は最大 0.019（進み 0.70 付近。始点・頂点・終点は一致）。脈動の拡縮では 1.08 で 0.0015・1.03 で 0.0006 で、幅 100px の要素の縁で 0.08px 未満。DragGuide の移動では途中だけ travel の 1.9% の位置差。§3.4 の例外の条件（影響が小さく非本質的）に当たり、`UiEasing` を書き換えるのは E-19 で S2-notification だけ | EaseInOutSine のまま。S2-notification のマージ後に integrator が CssEaseInOut を足してこの行を消す | <Task 14 の動画の URL> | agent 既定（S2-tutorial D-16） |
```
対応する行に印を付ける: `TutorialDragGuideGlyph.cs` の影を描く行に `// look-exception: LX-35`、`WorldPinGlyphs.DrawArrow` の影の行に `// look-exception: LX-36`、`TutorialAttentionPulse.Start` の `UiEasing.EaseInOutSine` の行と `TutorialDragGuideStep.Ease` の行に `// look-exception: LX-37`。LX-38 は Task 14 の並べ比べで、一目で分からず直すのが大きい差が見つかったときに使う（使わなければ欠番として done.md に書く）。一目で分かる差は例外にせず直すか、done.md の「要裁定」に書いて止める。

- [ ] **Step 3: 実測の結論を書く**

`docs/uitk/TODO.md` の「## 実測の結論」の表の末尾に（Task 6 Step 4 で padding の行を足していればその 1 行と合わせて）:
```markdown
| アンカーの祖先クリップの判定 | UITK で USS の `overflow: hidden` を C# から読めるか | S2-tutorial | 読めない（`IResolvedStyle` に `overflow` が無い）。祖先クリップは `ScrollView` の viewport・`TutorialAnchor.MarkClipContainer` の印・インラインの `overflow` で判定する。USS で子を切る容器を作る画面は `MarkClipContainer` を付ける（L1 `TutorialAnchorTrackerTest`・`TutorialHighlightAncestorClipTest`） |
| アンカーの追跡の頻度 | 毎フレームの `Resolve`（木の Query）で 60fps を落とさないか | S2-tutorial | 購読中のアンカーだけを測る。録画 `uitk-tutorial-cues` の枠 1・DragGuide 1（アンカー 3）で <Task 15 の Profiler か録画の fps> |
```

- [ ] **Step 4: parity を見てコミットする**

Run:
```bash
node tools/uitk/test-name-parity.mjs --bundle 2 --emit-missing | grep -i "tutorial\|worldPin\|KeyControlHint" || echo "束2: このレーンの未対応 0"
node tools/uitk/test-name-parity.mjs --bundle 4 --emit-missing | grep -i "TutorialOverlay" || echo "束4: このレーンの未対応 0"
node tools/uitk/test-name-parity.mjs --bundle 5 --emit-missing | grep -i "tutorial\|portalLayerScaling" || echo "束5: このレーンの未対応 0"
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Theme|Conventions)\."
```
Expected: 3 行とも「未対応 0」（`portalLayerScaling` の通知の 1 件は S2-notification の持ち物なので、出ていても未対応のまま done.md に「S2-notification 待ち」と書く）。`LookExceptionLedgerTest` が PASS。出力を done.md の「証拠」に写す
```bash
git add docs/uitk moorestech_client/Assets/Scripts/Client.Ui/Screens/Tutorial
git commit -m "docs(uitk): S2-tutorial の処遇・見た目の例外 LX-35〜37・実測の結論を記帳する

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: 並べ比べ（720p と 2560×1080）

**順序**: Task 13 より先に行う（Task 13 の LX の行はこの結果を書く。D-35）

**Files:**
- なし（撮影・比較・done.md）

**Interfaces:**
- Consumes: 参照キャプチャ、master の worktree の `moorestech_web/webui/e2e/uitk-reference/capture.ts`、Web の撮影状態の定義（`moorestech_web/webui/e2e/uitk-reference/referenceShots.ts` の 85〜118 行・`moorestech_web/webui/e2e/mock-host/topics/tutorialControls.ts` の 29〜61 行）、Task 6・8・9 の Presenter の `Mount`、前例 theme レーンの撮り方（`../moorestech_logs/harness/sdd/feature-uitk-s1-theme/workspace/task-5-brief.md` の Step 7・`task-5-report.md`）、Web の動画 `4-drag-guide.webm`（DragGuide の対。参照の `index.md` 253 行）と撮影状態（`moorestech_web/webui/e2e/mock-host/topics/tutorialControls.ts` の `tutorialDragGuide`・`moorestech_web/webui/e2e/uitk-reference/referenceVideos.ts` の 16 行）、`GameUiLocaleClass`・`GameUiLocaleClassBinder`
- Produces: `moorestech_client/PlaytestResults/s2-tutorial-compare/` の組と fixture の EDC コード（gitignore 下）、share の URL、done.md の「並べる対」「fixture の矩形」「差の表」

- [ ] **Step 1: 1280×720 の参照を引く**

Run:
```bash
REF=../moorestech_logs/uitk/reference-captures/web-a9d5123537
grep -n "2-world-pin\|2-key-control-hint\|4-drag-guide\|5-tutorial" "$REF/index.md"
ls "$REF" "$REF/qa" | grep -i "world-pin\|key-control\|drag-guide\|tutorial"
```
Expected: 静止画 `2-world-pin-onscreen`・`2-world-pin-offscreen`・`2-key-control-hint`・`5-tutorial-outline-label`・`5-tutorial-recipe-item`・`5-tutorial-research-node`、動画 `2-key-control-hint-pulse`・`2-world-pin-pulse`・`4-drag-guide`・`5-tutorial-pulse` と `qa/` のコマ。無い名前は done.md の「未確認」に書く

- [ ] **Step 2: 2560×1080 の参照を master の worktree で撮る**

参照キャプチャは 1280×720 だけなので、master の worktree で同じ撮影を 2560×1080 で回す（feature/uitk の Web には手を入れない）:
```bash
LANE_ROOT="$(git rev-parse --show-toplevel)"
moores-wt new uitk-ref-s2-tutorial --from origin/master --no-editor
REF_ROOT="$(moores-wt status --json | python3 -c 'import json,sys; print(next(w["path"] for w in json.load(sys.stdin)["worktrees"] if w["path"].endswith("uitk-ref-s2-tutorial")))')"
cd "$REF_ROOT/moorestech_web/webui"
grep -n "referenceViewport" e2e/uitk-reference/shotRunner.ts
```
`referenceViewport` は `shotRunner.ts` に固定なので、この使い捨ての worktree の中だけで `{ width: 2560, height: 1080 }` に書き換え（コミットしない）、撮影名を 6 つに絞って回す:
```bash
npm ci
UITK_REFERENCE_OUT="$LANE_ROOT/moorestech_client/PlaytestResults/s2-tutorial-compare/web-2560" UITK_REFERENCE_PORT=5191 npx tsx e2e/uitk-reference/capture.ts 2-world-pin-onscreen 2-world-pin-offscreen 2-key-control-hint 5-tutorial-outline-label 5-tutorial-recipe-item 5-tutorial-research-node
cd "$LANE_ROOT"
moores-wt rm uitk-ref-s2-tutorial --force
```
Expected: 6 枚の 2560×1080 の PNG。`moores-wt status --json` の鍵の名が違えば `moores-wt status` の表から worktree のパスを読む。`capture.ts` が撮影名の引数を取らない実物なら全撮影を回して 6 枚だけ使う。`--force` は書き換えを捨てるため（このための worktree なので失うものは無い）

- [ ] **Step 3: Web と同じ撮影状態を UITK の fixture で作る（D-29）**

Web の 6 枚は mock-host の状態で撮られている。UITK でその画面がまだ無い（クロスヘアは S2-hud、レシピ一覧・研究は段階 4）ので、同じ状態を fixture で作る。並べる対:

| Web 撮影名 | Web の状態（referenceShots.ts・tutorialControls.ts） | UITK 撮影名 | UITK の状態の作り方 |
|---|---|---|---|
| `5-tutorial-outline-label` | ゲーム画面。`game.crosshair` に枠（padding 8）＋ラベル「照準に合わせる」 | `fx-outline-label` | S2-hud がマージ済みならクロスヘアの実物の View を同じパネルに載せてその印を使う。未マージなら Web のクロスヘアと同じ矩形の要素を置き `TutorialAnchor.Mark(e, "game.crosshair")` |
| `5-tutorial-recipe-item` | 持ち物画面。レシピ一覧 1 段目のセル `recipe.item-100` に枠（padding 8）＋同じラベル（ScrollArea のクリップの中） | `fx-recipe-item` | Web のレシピ一覧と同じ矩形の `ScrollView`（viewport が祖先クリップ）の中に、1 段目のセルと同じ矩形の要素を置き `Mark(e, "recipe.item-100")` |
| `5-tutorial-research-node` | 研究画面。パンでクリップ境界を跨ぐノード `research.node-<guid>` に枠（padding 8・ラベル無し） | `fx-research-node` | Web のパン領域と同じ矩形の容器に `TutorialAnchor.MarkClipContainer`、その中の Web と同じ位置（境界を跨ぐ）にノードの矩形の要素を置き `Mark` |
| `2-world-pin-onscreen` | `/__worldpin?x=0.6&y=0.4` | `fx-world-pin-onscreen` | `WorldPinPresenter.Mount` に画面内 (0.6, 0.4) のピン 1 件の ViewModel |
| `2-world-pin-offscreen` | `/__worldpin?on=0&dx=1&dy=0.3` | `fx-world-pin-offscreen` | 画面外・方向 (1, 0.3) のピン 1 件 |
| `2-key-control-hint` | ゲーム画面。Tab＋「インベントリを開く」（uiState GameScreen） | `fx-key-control-hint` | `KeyControlHintPresenter.Mount` に同じ 1 行の ViewModel |
| `4-drag-guide`（動画 `$REF/4-drag-guide.webm`） | 持ち物画面。レシピ一覧のセル `recipe.item-100` からホットバー `hotbar.hud` へのドラッグ誘導（`tutorialDragGuide`。動画は 4000ms） | `fx-drag-guide`（連番） | 動画のコマで測った矢印の始点と終点を中心にする要素を from・to に置き（大きさは `5-tutorial-recipe-item` のセルとホットバーの矩形）、`TutorialOverlayPresenter.Mount` に同じ 1 件の DragGuide の ViewModel。Web は `ffmpeg -i "$REF/4-drag-guide.webm" -vf fps=10 "moorestech_client/PlaytestResults/s2-tutorial-compare/web-1280/4-drag-guide/%03d.png"` でコマに割り、UITK は Step 4 の 4 で 100ms おきに撮る。Web の動画は 1280×720 だけなので 2560×1080 は撮らない |
| なし | — | Task 15 の `06-after-pause` | UITK のみ（自己確認）。Web に同じ状態の参照が無いので差の表に載せず、Task 15 で見る。Task 15 の `01`〜`05` も実プレイの自己確認で、対には使わない |

矩形は Web の参照 PNG から測る: 枠の外形から padding 8px だけを引いた矩形がアンカー（Web の枠は `box-sizing: border-box` で 3px の線は外形の内側に描かれ、外形 = アンカー ± padding。`W/features/tutorial/overlay/TutorialOverlay.tsx` 132〜143 行・`W/features/tutorial/overlay/style.module.css` 12〜15 行。例: 720p でアンカーの横が [100, 300] なら外形は [92, 308]。線幅まで引くとアンカーを 3px ずつ小さく作ってしまう）、レシピ一覧・パン領域はクリップの切り口の矩形。1280×720 と 2560×1080（Step 2 の撮影）のそれぞれで測り、2560×1080 はパネルの倍率 1.5（1080/720）で割って論理座標にする。測った値を done.md の「fixture の矩形」に書く。ラベル文は Web の mock と同じ文を `UiText.FromVerbatim` で渡す（字形の比較が目的で、辞書は L1 が見る）。

- [ ] **Step 4: UITK を 2 解像度で撮り、並べる（前例 theme レーンの撮り方）**

Game ビューの解像度は切り替えない。前例（theme レーン task-5 の Step 7）と同じく、空のシーンで PlayMode に入り（`uloop control-play-mode --action Play`。シーンが MainGame なら先に `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene)`）、`uloop execute-dynamic-code` で:
1. `GameUiPanelSettings.asset` を `Object.Instantiate` した複製に `targetTexture`（`new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)`、2 回目は 2560×1080）を付け、`UIDocument` を 1 つ作る
2. 複製パネルのルート（`UIDocument.rootVisualElement`。本番の `GameUiRoot.SetRootClassEnabled` が付けるのと同じ要素）に、本番の `GameUiLocaleClassBinder`（`S/Client.Ui/Foundation/Text/GameUiLocaleClassBinder.cs` 36 行）と同じ判定でロケールのクラス `GameUiLocaleClass.CjkClassName`（`game-ui-locale--cjk`）を付ける。Web の参照は日本語なので、言語が ja で `GameUiLocaleClass.IsCjk(Localize.GetCurrentLanguageCode())` が真になることを確かめてから付ける（付けないと `Typography.uss` 3〜10 行のラテンのフォントのままで、126〜143 行の CJK の字形にならず字形の比較にならない。S2-hud・S2-mode-tooltip・S2-notification の計画と同じ形）
3. ルートに Step 3 の fixture の要素を置き、`TutorialOverlayPresenter.Mount(root, root, overlay)`・`KeyControlHintPresenter.Mount(root, keyHint)`・`WorldPinPresenter.Mount(root, pins)` を `ReactiveProperty` の ViewModel で呼ぶ（撮影名ごとに ViewModel を差し替える）
4. `uloop focus-window` の後、2 回目の execute-dynamic-code で RT を `ReadPixels` して `moorestech_client/PlaytestResults/s2-tutorial-compare/uitk-1280/<UITK 撮影名>.png`・`uitk-2560/` に保存する（脈動があるので、Mount 後に脈動が始まる前の経過 0 の絵を撮る。同じ撮影を 2 回撮って同じ絵になることを確かめる）。`fx-drag-guide` だけは動きを撮るので、Mount と同じ EDC で `root.schedule.Execute(...).Every(100)` を登録し、1 周期（`--tutorial-drag-guide-duration`）の間 100ms おきに RT を `ReadPixels` して `uitk-1280/fx-drag-guide/<経過ms>.png` に連番で残す

fixture の EDC のコードは `moorestech_client/PlaytestResults/s2-tutorial-compare/fixture.cs.txt`（gitignore 下）に残し、done.md に要点を書く。終わったら PlayMode を止める。Web と UITK を Step 3 の対の表の順で横に並べた比較 HTML を 1 枚作って `share-put moorestech_client/PlaytestResults/s2-tutorial-compare -n s2-tutorial-compare` で置く。done.md の「差の表」に対ごとに 1 行: 枠の外形（±2px）・線幅 3px・角丸・グローの幅と色・ラベルの位置（下／反転）と面の色・祖先クリップの切り口・ピンの先端位置・マーカーの形・矢印の大きさと余白・ヒントの位置（ホットバーの上 72px）と赤と 2 行の行間・脈動の周期（Web の動画のコマで頂点の時刻）・DragGuide の矢印の形と大きさ・影の縁（LX-35）・不透明度の出入りの時刻・移動の曲線（始点からの距離を travel で割った値を同じ位相のコマで比べる。LX-37）。暗さの差（`--world-pin-face`＝`--surface-navy` の α）も他の差と同じに扱う（I-fix1 は取り込み済み）。LX-35・LX-37 の記述（影の縁の柔らかさだけの差・曲線の進みの差が最大 0.019）が DragGuide のコマの実測と合うかをここで確かめて done.md に書き、Task 13 Step 2 はその結果で書く。差が LX-35〜37 以外で一目で分かれば直してから撮り直す。直せず大きければ LX-38 で記帳する（Task 13 Step 2）

---

### Task 15: unityプレイ録画テスト（`uitk/stage2/uitk-tutorial-cues`）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-tutorial-cues.cs`

**Interfaces:**
- Consumes: 前例 `scenarios/uitk/stage1/uitk-lifeline.cs`（`PlaytestRunner.Run`・`SetupDebugEnvironment`・`SkipOpeningSkitIfPlaying`・`ExitToGameScreen`・`PressKey`・`WaitUiState`・`PlaytestUiElementOps.FindUi`・`Screenshot`・`Note`・`Assert`）、`TutorialPresentationStateStore.Instance`（`BeginSession(Guid)`・`AddOutlineHighlight(string anchorId, Guid tutorialGuid)`・`AddDragGuide(string from, string to)`・`AddKeyControlHint(string tutorialGuid, string keyName, string uiState)`・`EndSession(Guid)`）、`WorldPinStateStore.Instance`、マスタのチャレンジ 1（`mapObjectPin` を持つ）
- Produces: result.json・recording.mp4・スクショ 6 枚（実プレイの自己確認。`05`・`06` は Task 14 の表に「UITK のみ（自己確認）」で載る）

- [ ] **Step 1: シナリオを書く**

`uitk-tutorial-cues.cs`（冒頭コメントに起動前の手順: PlayMode を止め、`uloop execute-dynamic-code` で `UiSystemEditorSwitch.Set(UiSystem.GameUi)`、受け口 `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9`。撮影は 1280×720 の 1 回だけ。Web との並べ比べは Task 14 の fixture が持ち、このスクショは実プレイの自己確認）:
```csharp
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-tutorial-cues", options, async p =>
{
    // 本番の受け口へ送らない・背景 Editor でも入力が届く（uitk-lifeline と同じ前置き）
    // Never send to production and let injected input reach a background Editor (same preamble as uitk-lifeline)
    if (Environment.GetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE") != "http://127.0.0.1:9")
        throw new InvalidOperationException("MOORESTECH_PLAYTEST_RECEIVER_BASE が起動前に http://127.0.0.1:9 へ差し替えられていない（本番へ送るので中止）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（全体スイッチ UITK）");

    // チャレンジ 1 のワールドピン（画面内と、振り向いた後の画面端矢印）
    // Challenge 1's world pin (on screen, then the edge arrow after turning around)
    await WaitUiFound("world-pin-overlay", 20f);
    await p.Screenshot("01-world-pin-onscreen");
    await p.RotateCameraYaw(180f);
    await p.UntilAsync(() => GameUiRoot.Instance.GetRootElement().Q(className: "world-pin__arrow") != null, 10f);
    await p.Wait(0.6f);
    await p.Screenshot("02-world-pin-offscreen");

    // キー操作ヒント（Tab・GameScreen）を store へ差し込む
    // Inject a key-control hint (Tab, GameScreen) into the store
    var sessionGuid = Guid.NewGuid();
    var tutorialGuid = FirstTutorialGuidOfChallenge(1);
    TutorialPresentationStateStore.Instance.BeginSession(sessionGuid);
    TutorialPresentationStateStore.Instance.AddKeyControlHint(tutorialGuid.ToString(), "Tab", "GameScreen");
    await WaitUiFound("key-control-hint-hud", 10f);
    await p.Wait(0.6f);
    await p.Screenshot("03-key-control-hint");

    // ポーズを開き、設定のボタンに枠とラベル、セーブ→設定へ DragGuide
    // Open the pause menu, outline the settings button with a label and drag-guide from save to settings
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
    TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.PauseSettings, tutorialGuid);
    TutorialPresentationStateStore.Instance.AddDragGuide(AnchorIds.PauseSave, AnchorIds.PauseSettings);
    await WaitUiFound("tutorial-highlight-label", 10f);
    await p.Screenshot("04-outline-label");
    await p.Wait(1.6f);
    await p.Screenshot("05-drag-guide-mid");
    p.Assert(!IsShown("key-control-hint-hud"), "ポーズ中（uiState 不一致）はキー操作ヒントを出さない");

    // ポーズを閉じると枠はアンカーと一緒に消える
    // Closing the pause menu removes the outline together with its anchor
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.Wait(0.2f);
    p.Assert(!IsShown("tutorial-outline__clip"), "アンカーが消えたら枠も消える");
    await p.Screenshot("06-after-pause");
    TutorialPresentationStateStore.Instance.EndSession(sessionGuid);
});
```
（`WaitUiFound(testid, seconds)` は 3 要素が `PickingMode.Ignore` で `UntilUiElement`（クリックできるまで待つ）に当たらないため、`PlaytestUiElementOps.FindUi` の `IsFound` を期限付きで回すローカル関数として同じファイルに書く（前例 `cursor-tooltip-follows-crosshair.cs` の `PollUntilQueryAsync`・mode-tooltip 計画と同形）。`key-control-hint` は行ごとに同名で Ambiguous になるので、ホスト側の一意な名 `key-control-hint-hud` で待つ。（`RotateCameraYaw`・`UntilAsync`・`Wait` は DSL の実物の名に合わせる。無ければ `uitk-lifeline.cs` と `references/` のルーティング表から同じ働きの操作を選び、無い操作は書かずにピンが画面外にある地点へ `SetupDebugEnvironment` の配置で立つ形にする。`FirstTutorialGuidOfChallenge(1)` は `MasterHolder.ChallengeMaster` から 1 番目のチャレンジの最初の `TutorialGuid` を引くローカル関数、`IsShown(nameOrClass)` は `GameUiRoot.Instance.GetRootElement()` を引いて `resolvedStyle.display` を見るローカル関数として同じファイルに書く）

- [ ] **Step 2: 全体スイッチが UITK であることを確かめてから回す**

テストの TearDown が全体スイッチを Web に戻すので、L1 を回した後は必ず戻す:
```bash
uloop control-play-mode --project-path ./moorestech_client --action stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.UiSystemEditorSwitch.Set(Client.Starter.UiSystem.GameUi); return Client.Starter.UiSystemEditorSwitch.Get().ToString();'
```
Expected: `GameUi`（名前空間・メソッド名は `uitk-lifeline.cs` の冒頭コメントの実物に合わせる）。続けて:
```bash
SCRATCH="$PWD/moorestech_client/PlaytestResults/scratch"
mkdir -p "$SCRATCH"
cp .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-tutorial-cues.cs "$SCRATCH/uitk-tutorial-cues.cs"
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client "$SCRATCH/uitk-tutorial-cues.cs"
```
Expected: result.json が PASS、recording.mp4 とスクショ 6 枚。初回に "already playing" で落ちたら PlayMode を止めて同じコマンドで投入し直す。録画で、ピンの脈動（矢印だけ）・ヒントの 1.08 の脈動・枠の 1.03 の脈動・DragGuide の移動と明滅・ポーズを閉じた後に枠が残らないことを目で確かめ、done.md に result.json と recording.mp4 のパスを書く。Task 13 Step 3 の fps の行をここで埋める

- [ ] **Step 3: コミットし、作業で作ったワールドを片付ける**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-tutorial-cues.cs docs/uitk/TODO.md
git commit -m "test(uitk): チュートリアルの枠・ヒント・ワールドピンを UITK で通す録画シナリオを足す（S2-tutorial）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
bash .agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh
```
Expected: このレーンが `Saves/` に作ったワールドが列挙される。消して、消したパスを done.md に書く

---

### Task 16: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイルだけ

**Interfaces:**
- Consumes: Task 1〜15 のコミット一式（`origin/feature/uitk..HEAD`）
- Produces: レビュー記録 `$LOGS/harness/` 配下（`$RUNDIR`）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。レビューが裁定・設計書と食い違う直し（Web と違う見た目へ寄せる・画面 USS に部品のクラスを書く・色を C# に書く・毎フレームの追跡をやめて木の変化イベントに寄せる等）を提案したら採らず、done.md の「要裁定」に書く

- [ ] **Step 2: 自動適用があれば検査と録画を回し直してコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(State\.Tutorial|Screens\.Tutorial|Anchor|Theme|Conventions|Screens\.PauseMenu)\."`
Expected: `ErrorCount: 0`、全件 PASS。自動適用が `Screens/Tutorial/**`・`Foundation/Anchor/**` の .cs/.uss に触れたら、Task 15 Step 2（全体スイッチ UITK の確認から）を反映後のバイナリで回し直す（ユニットテストの通過は代替にならない）。続けて:
```bash
git add -A moorestech_client/Assets/Scripts "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" docs/uitk .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk
git commit -m "fix(uitk): S2-tutorial の moores-code-review 反映

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
（自動適用が無ければコミットしない）

---

### Task 17: feature/uitk への PR と取り込み（必須）

**Files:**
- なし（PR・マージ・後片付け）

**Interfaces:**
- Consumes: Task 16 後の HEAD
- Produces: `origin/feature/uitk` のマージコミット

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`）。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ。報告ページは環境ローカル規約（CLAUDE.local.md の「PR の成果物の載せ先」）が指す手順で、manifest は次の形（`file` は絶対パス。並べ比べは Web と UITK を同じ `group` に続けて置く）:
```json
{
  "head": "<HEAD 10 桁>",
  "summary": [
    "チュートリアルの枠線（祖先クリップ・1.03 の脈動）と枠線ラベル（下／上へ反転・右端の押し戻し）を Web と同じ見た目で UITK に写した",
    "2 つのアンカーを結ぶ DragGuide、ホットバー上のキー操作ヒント（ShortcutHintLabel・1.08 の脈動）、ワールドピン（マーカーと画面端矢印）を写した",
    "アンカーは購読中のものだけ毎フレーム測り、重複は LogError・未発見は Warning を状態が変わった時だけ出す（Foundation の Resolve はログを出さなくなった）",
    "アンカーのカバレッジ L1 を足し、未移植の画面のアンカーを持ち主レーンの表にした",
    "部品を直した: ShortcutHintLabel の Prefix は印の引数を足さない（content・verbatim の文で例外を出さない）、KeyCap に色のつまみ --key-cap-color、Foundation に ScaleTweenStep"
  ],
  "media": [
    {"file": "<uitk-tutorial-cues の recording.mp4>", "caption": "uitk-tutorial-cues（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<web 2-world-pin-onscreen.png>", "caption": "Web: ワールドピン（画面内）", "group": "並べ比べ 1280×720"},
    {"file": "<uitk 01-world-pin-onscreen.png>", "caption": "UITK: ワールドピン（画面内）", "group": "並べ比べ 1280×720"},
    {"file": "<web 2-world-pin-offscreen.png>", "caption": "Web: 画面端矢印", "group": "並べ比べ 1280×720"},
    {"file": "<uitk 02-world-pin-offscreen.png>", "caption": "UITK: 画面端矢印", "group": "並べ比べ 1280×720"},
    {"file": "<web 2-key-control-hint.png>", "caption": "Web: キー操作ヒント", "group": "並べ比べ 1280×720"},
    {"file": "<uitk 03-key-control-hint.png>", "caption": "UITK: キー操作ヒント", "group": "並べ比べ 1280×720"},
    {"file": "<web 5-tutorial-outline-label.png>", "caption": "Web: 枠線とラベル", "group": "並べ比べ 1280×720"},
    {"file": "<uitk 04-outline-label.png>", "caption": "UITK: 枠線とラベル", "group": "並べ比べ 1280×720"},
    {"file": "<web-2560 の 6 枚と uitk-2560 の 6 枚を同じ順で>", "caption": "…", "group": "並べ比べ 2560×1080"}
  ],
  "verification": [
    "Unityコンパイル: エラー0",
    "L0: Tutorial ロジック・状態 24 件 PASS",
    "L1: 追跡 6・枠 11・DragGuide 3・キー操作ヒント 4・ワールドピン 6・e2e 写し 6+3×2・見た目の規則 21・カバレッジ 2・アンカー 7 件 PASS",
    "test-name-parity 束 2・4・5: このレーンの未対応 0（捨てる 2）",
    "unityプレイ録画テスト: uitk-tutorial-cues PASS"
  ],
  "notes": ["並べ比べの差の表は moorestech_logs/harness/uitk/S2-tutorial/done.md"]
}
```
環境ローカル規約（CLAUDE.local.md の「PR の成果物の載せ先」）が指す公開スクリプトが失敗したら、報告 URL 無しで完了にしない。PR 番号を `docs/uitk/disposition.md` の行と台帳の LX の行（`#<PR番号>`）に入れてコミット・push する

- [ ] **Step 2: feature/uitk の進みを取り込み、G1 緑を見る**

Run:
```bash
git fetch origin
git merge --no-ff --no-edit origin/feature/uitk
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(State\.Tutorial|Screens\.Tutorial|Anchor|Theme|Conventions|Screens\.PauseMenu)\."
git push
gh pr checks <PR番号>
```
Expected: 進んでいなければ `Already up to date.`。共有区画（`GameUiTheme.tss` の `Screens` 区画・`MainGameUiRegistration` の画面登録区画・`localization.csv`・台帳の 3 節の末尾・`disposition.md`）の衝突は両方の行を採る。`ShortcutHintLabel.cs`・`KeyCap.uss`・`Foundation/Anchor` の衝突は、このレーンの直し（Prefix で印の引数を足さない・`--key-cap-color`・ログを呼ぶ側へ）を残して相手の変更を重ねる。`TutorialAnchorCoveragePending.cs` の衝突（後続レーンが行を消した）は消した側を採る。ほかのレーンがアンカーの印を足していれば、カバレッジ L1 が「両方にある」で落ちるので、その行を表から消す。`ErrorCount: 0`・PASS。`gh pr checks` で `UITK Web follow gate (G1)` と `UITK Web follow gate self-test` が `pass`、未記帳 0（終わっていなければ前景で `gh pr checks <PR番号> --watch`）。CI の `BugReportSubmitKindTest.空白本文でも種別を先に解く` は既存失敗（albof.22）なのでマージ条件に入れない

- [ ] **Step 3: マージコミットで取り込む**

Run: `gh pr merge <PR番号> --merge`
Expected: マージされ、`git fetch origin && git log -1 --format='%H %P' origin/feature/uitk` の親が 2 つ（Squash しない）

- [ ] **Step 4: bd を閉じ、報告を締める**

Run:
```bash
bd close <S2-tutorial の bd ID> --reason "PR #<番号> を feature/uitk へマージ"
git status --short
git log origin/uitk-lane/s2-tutorial..HEAD
```
Expected: closed、`git status --short` と `git log` が空。done.md の 1 行目を `状態: 完了` にし、「証拠」に PR 番号・マージコミット・G1 のジョブ URL・各テストと録画の結果を足す。worktree の片付け（`moores-wt rm`）は指揮が行う

---

## Self-Review

1. Requirements coverage: R-1 → Task 6・10、R-2 → Task 3・5・6、R-3 → Task 5・6・8・9・10・11、R-4 → Task 3・6・10、R-5 → Task 2・3・5・6・10、R-6 → Task 13、R-7 → Task 3・7、R-8 → Task 4・8・10、R-9 → Task 3・4・9、R-10 → Task 10、R-11 → Task 11、R-12 → Task 12、R-13 → Task 6・7・8・9・12、R-14 → Task 14、R-15 → Task 13、R-16 → Task 13、R-17 → Task 15、R-18 → Task 16・17。漏れなし。完了条件の逐語の各項（祖先クリップ・脈動 1200ms・ラベル反転・二重解決の LogError・カバレッジ L1・KeyControlHint/WorldPin・6+17+21+6+1+3 件・DragGuide）はすべて R に写した。件数 58 は Web の実物を grep して数え直し tests-ladder と一致（写す 56・捨てる 2。ack の 2 件は D-10）
2. Placeholder scan: `<PR番号>`・`<番号>`・`<HEAD 10 桁>`・`<S2-tutorial の bd ID>`・manifest のパス・`<Task 14 の URL>`・`<Task 15 の … fps>` は実行時にしか決まらない値で、取り方（Task 1 Step 3・`git rev-parse`・share-put の出力・録画）を同じタスクに書いた。DSL の操作名（`RotateCameraYaw` 等）と全体スイッチの API 名は実物に合わせる手順を書いた。「TBD」「適切に」は無い
3. Type consistency: `TutorialClipRect`・`TutorialAnchorReason`（唯一の enum）・判別 union `TutorialAnchorState`（`Ready(rect, clip)`・`NotFound(reason)`・`Hidden(reason)`・`SameAs`）・`ITutorialAnchorStateLookup.TryGet`（帳簿と追跡器が実装する唯一の読みの口）/`TutorialAnchorStateBook.ApplyIfChanged/KeepOnly`・`TutorialHighlightClip.TryComputeInset`/`TutorialClipInset`・`TutorialHighlightLabelPlacement.Place/ShowsLabel`/`TutorialLabelPosition`・`TutorialDragGuideGate.TryPlace`/`TutorialDragGuidePlacement`・`WorldPinEdgePlacement.TryPlace`/`WorldPinEdgeSpot`・判別 union `TutorialOutlineCue`（`Plain`・`Labeled(…, UiText label)`）・`TutorialElementKey.Of`・`TutorialTextFactory.TryCreate`・`TutorialOverlayViewModel(int, IReadOnlyList<TutorialOutlineCue>, IReadOnlyList<TutorialDragGuideCue>)`・S2-hud の `BlockingSkitPresence.IsBlocking`・`KeyControlHintProjection.Project(data, UIStateEnum, bool)`・`WorldPinViewModel.From(data, bool)`・`ITutorialAnchorSubscriptions.OnAnchorChanged/SetSubscribedAnchors`・`TutorialAnchorTracker(VisualElement, VisualElement)`・`ScaleTweenStep(VisualElement, float, float)`・`TutorialAttentionPulse.Start/StrongScale/SubtleScale/PeriodPropertyName`・`TutorialPulseBinding(VisualElement).Attach(VisualElement, float)`・`TutorialOverlayView(ITutorialAnchorStateLookup).Render/RedrawAnchor`・`TutorialOverlayPresenter.Mount(layer, searchRoot, model)`・`KeyControlHintPresenter.Mount(layer, model)`・`WorldPinPresenter.Mount(layer, model)`・`TutorialOutlineView(TutorialPulseBinding).ShowRing(box, inset, glow, radius)/ShowLabel(UiText, box, clip, gap)/HideRing/Label`・`TutorialHighlightLabelView.Place(UiText, box, clip, gap)/HideLabel`・`WorldPinEdgeArrowView(TutorialPulseBinding).Show(string, WorldPinEdgeSpot, float)`。Task 間で名前と引数を照合し、Produces とコードの食い違い（ShowRing の角丸・Place の文・矢印の Show）は Produces を直した。キャストは switch のパターン・`is not … x` に替えた
4. 保留・縮退経路: (a) `TutorialAnchorRegistry.Resolve` は黙り、未発見・重複は `TutorialAnchorTracker.ReportTransition` が遷移時に Warning/LogError（L1 で確かめる）。(b) 枠を出さない経路（未解決・完全にクリップ）は Web と同じ仕様の非表示で、理由は (a) のログで開発者に届く（ack は写さない。D-10）。(c) ラベルの GUID・ヒントの GUID・ピンの GUID が読めないときは LogError を出して、その 1 件だけを出さない（Projection の 3 か所）。(d) 語彙外の `UiState` は 1 回だけ Warning（Web と同じ）。(e) `WorldPinEdgePlacement` の寸法 0 と `TutorialHighlightLabelView.ApplyPlacement` の未計測は `// silent-ok:` 付きで次の `GeometryChangedEvent` で置き直す。(f) つまみが読めないときは `UssCustomStyleReader` が LogError を出し、脈動・寸法は前の値か無しで続ける。(g) bool を返す Try 関数（`TryComputeInset`・`TryPlace`・`TryGetState`）が false で枠・矢印を隠すのは仕様どおりの非表示で、各所に `// silent-ok: <理由> / <気づく経路>` を書く（経路はアンカーの理由のログ・L1）。GUID の読み違いは `TutorialTextFactory.TryCreate` の 1 関数が LogError を出す（D-24・D-25）。(h) 脈動の周期の解決前に `Attach` された子は待ち、解決時に流す。外した子には流さない（`TutorialPulseBinding`・L1 `TutorialPulseBindingTest`）
5. 「決定的にする」を根拠にした選択: なし（追跡は購読中のアンカーを毎フレーム測り直すだけで、順序に意味を持たせない）
6. 呼び出し側への責務漏出: アンカーの測定・祖先クリップ・遷移時の報告は `TutorialAnchorTracker` が持ち、各画面はアンカーに印を付けるだけ。USS で子を切る容器だけ `MarkClipContainer` を付ける義務が新しく生まれるので、設計書 A-65（指揮が書く）と ref-uitk-design への記載を integrator へ依頼する（L2 は新設しない。D-27）。購読は Presenter の `Mount` だけが持ち、View は `Render`/`RedrawAnchor` を受けるだけ（D-22）。脈動は画面ルートの `TutorialPulseBinding` が周期の読み・待たせ・開始・停止を持ち、View は子を `Attach` して振幅を渡すだけ（後から足した子も脈動する）。KeyControlHint の赤は部品のつまみ `--key-cap-color` の上書きと文の `color` で、共有の `--text-high-contrast` も `.key-hint-text` も書き換えない

---

## 判断記録（ADR）

従う裁定・設計: Global Constraints の裁定、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1・§2・§3・§4・§5.1）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 S2-tutorial 行・§4.3・§5・§6.2・E-1・E-8・E-16）、ADR 0024・0039。

| ID | 判断 | 棄却した案と理由 | 出所 |
|---|---|---|---|
| D-1 | ワールドピンは `Screens/Tutorial/WorldPin/`、状態は `Client.Ui.State/Tutorial/WorldPin/` に置き、登録も `TutorialGameUiRegistration` の 1 本にする。所有表の `Screens/WorldPin/**`・`Client.Ui.State/WorldPin/**`・`WorldPinGameUiRegistration.cs` は使わない | `Screens/WorldPin/` に分ける: 脈動の helper（`Screens/Tutorial/Pulse/`）を 2 画面で使い Screens 間の参照が生まれる。Web も `features/tutorial/` に置いている | agent 既定（Web の配置） |
| D-2 | 状態の源は C# の 2 store（`TutorialPresentationStateStore`・`WorldPinStateStore`）と `UIStateControl` を直接購読する。blocking のスキットは S2-hud の `BlockingSkitPresence` で読む（D-20）。新しいイベントパケットは作らない | 「イベントパケット＋初期データ＋購読」の 3 点セット: どれもクライアント内の状態（サーバーの可変状態ではない）で、Web の topic もこれらの中継だけ。ワールドピンの射影は `MapObjectPin`/`VeinPin` の `Update` が C# で済ませており、UITK は 0..1 の座標を % で置くだけ（Web と同じ） | agent 既定（実物の `Client.WebUiHost/Game/Topics/Tutorial/*.cs`） |
| D-3 | `TutorialAnchorRegistry.Resolve` のログを外し、遷移時の報告を `TutorialAnchorTracker` に移す。クリップ容器の印 `TutorialAnchor.MarkClipContainer` を足す。Foundation/Anchor の変更はこのレーンが行い、設計書には指揮が A-65 として書く | Resolve を呼ぶ側でログを抑える: Foundation の静的メソッドが毎回 Warning を出すので、毎フレームの追跡ではログが溢れる。Tracker が Registry を使わず自前で Query する: 解決の規則が 2 か所に分かれる | 指揮の割り当て B |
| D-4 | Web の計算（clip-path の inset・ラベル配置・DragGuide の門・画面端のクランプ）は `Client.Ui.State/Tutorial/Logic/` に純ロジックとして逐語で写し L0 で確かめる | View に書く: 座標の式を L1 の寸法でしか確かめられず、Web の式との突き合わせが難しい | agent 既定 |
| D-5 | アンカーの状態は判別 union `TutorialAnchorState`（`Ready(rect, clip)`・`NotFound(reason)`・`Hidden(reason)`）、枠の合図も判別 union `TutorialOutlineCue`（`Plain`・`Labeled`）で持つ。理由は唯一の enum `TutorialAnchorReason`（status は理由から決まる）。キャストは switch のパターンにする。ack を写さないので wire の綴りは作らない（D-10） | Kind の enum＋null 許容のフィールド: Ready 以外で `Rect` を読む誤りがコンパイルで捕まらない。Web と同じく文字列で持つ: 綴りの誤りがコンパイルで捕まらない。status と reason を別の enum にする: 組の矛盾（ready・missing）が作れてしまう | 出所: 設計検査（fresh-context）→ agent 既定 |
| D-6 | KeyControlHint は C2-c の `ShortcutHintLabel`（Prefix）で作り、Prefix で印の引数を足さない直しをこのレーンが `Components/Text/` で行う（L1: content・verbatim の文で例外が出ない 1 件）。既存の `ShortcutHintLabel`/`KeyCap` の L1 と S2-hud の `KeyHintHud` の L1 を全部回して壊していないことを確かめる | `KeyCap`＋`LocalizedLabel` を画面で並べる: kbd と文の間隔のつまみ（`--shortcut-hint-label-gap`）を使えない。文を verbatim に解いてから渡す: 言語の切り替えで解き直されない。integrator へ頼んで待つ: Task 8 が止まる | 指揮の割り当て A |
| D-7 | 脈動の拡縮は新しいファイル `Foundation/Motion/Steps/ScaleTweenStep.cs`（`ScaleTweenStep(target, from, to)`。前例 `OpacityTweenStep`）として、このレーンが作る。既存の Steps のファイルは触らない（A-63） | `Screens/Tutorial/Pulse/` に置いて後で昇格: 同じ step を後で移す手間と、S2-hud 等が使えない期間が生まれる | 指揮の割り当て C |
| D-8 | KeyControlHint の赤は、`KeyCap` に足した色のつまみ `--key-cap-color`（既定 `var(--text-high-contrast)`）を画面ルートで `var(--tutorial-key-hint-color)` に上書きし、文は `.key-control-hint__item` の `color` で赤くする。`--text-high-contrast` は受け直さない（L1: つまみの上書きで色が変わる 1 件） | `--text-high-contrast` を画面で受け直す: 共有の色の意味を画面の中で書き換え、子に別の部品が来たとき巻き込む。画面 USS で `.key-cap { color }` を書く: 部品のクラスを画面に書く規約違反 | 指揮の割り当て A |
| D-9 | 未移植の画面のアンカーは、カバレッジの未移植の表（id → 持ち主レーン）に載せて L1 を緑にし、印を付けるのはその画面を作るレーンの仕事にする。S5-ladder の完了条件に「表が空」を足す（実行計画への依頼） | このレーンで全画面に印を付ける: 画面がまだ無い。L1 を写す予定（赤のまま）にする: 段階 2 の締めで G1 と既存テストが赤になる。表を作らず Web のカバレッジ（`AnchorIdsWebParityTest`）だけに頼る: UITK で印が付いたかを見ない | agent 既定（plan §4.5） |
| D-10 | ack（Web の `tutorial.anchor_ack`）は写さない。`TutorialAnchorAck`・Fanout・`ITutorialAnchorAckCommands`・`TutorialAnchorAckCommands`・そのテスト・DI 登録を作らず、Web の ack の 2 件（item master 未ロード中・同一 anchor の全 highlight）は disposition で「捨てる」。完了条件の「二重解決の LogError」は `TutorialAnchorTracker` の遷移ログで満たす | 写す: C# の唯一の受け手 `Client.WebUiHost/Game/Actions/TutorialAnchorAckAction.cs`（13〜20 行）は検証して返すだけで、store への書き込み・送信・ログが無く、サーバーに読み手も無く、Web も戻り値を捨てる。UITK では解決する側が C# 自身なので観測できる効果が無い。将来の受け手のための口は AGENTS.md「将来の拡張性は考慮不要」により持たない | 出所: user-simulator 予測 → 再現役が確認 → agent 既定 |
| D-11 | `UiState` の照合は `Enum.TryParse<UIStateEnum>` で行い、Web の語彙表（`isKnownUiStateName`）を写さない | 語彙表を写す: `UIStateEnum` そのものが語彙で、Web の表はその写し。2 つ持つと enum を足したときにずれる | agent 既定（Web の uiState 名は enum 名と同じ） |
| D-12 | KeyControlHint は Web の「文が空なら出さない」を写さない | 写す: UITK の辞書は常に読み込み済みで、訳の欠けは `Localize` が `[!key]` の可視文字列で返すので空にならない（C2-c D-20 と同じ扱い）。欠けは画面の `[!key]` で開発者が気づく | agent 既定 |
| D-13 | ラベル面の padding は `padding: var(--label-face-padding)`（複数値）を L1 で実測し、効かなければ 4 辺へ分けて書き、台帳「## 実測の結論」に記帳する | 最初から 4 辺に分ける: トークンの値を画面に写すことになり、トークンが変わったときに追従しない | agent 既定 |
| D-14 | 祖先クリップは UITK の overflow の規則（absolute の子も切る）に従い、Web の position の脱出則（fixed・absolute の包含ブロック）を写さない | 写す: UITK に包含ブロックの概念が無く、`overflow: hidden` は位置指定に関わらず全子孫を切るので、脱出則は常に「切る」になる | agent 既定（ADR 0024 の Consequences） |
| D-15 | DragGuide とワールドピンの矢印の影は、Painter2D で影の色の写しをずらして描く近似にし、LX-35・LX-36 で記帳する | 影のテクスチャを作る: 影の縁の柔らかさだけの差に対して、ぼかしのテクスチャの生成と更新を持つことになる | agent 既定 |
| D-16 | 脈動と DragGuide の曲線は CSS の `ease-in-out` を `UiEasing.EaseInOutSine` で近似し、LX-37 で記帳する。差は進みで最大 0.019（拡縮では 1.08 で 0.0015・1.03 で 0.0006。幅 100px の要素の縁で 0.08px 未満。DragGuide の移動では途中だけ travel の 1.9%） | このレーンで cubic-bezier を足す: §3.4 の例外の条件（影響が小さく非本質的）に当たり、`UiEasing` を書き換えるのは E-19 で S2-notification だけ（同じ曲線を 2 レーンで足すと衝突する）。S2-notification のマージ後に integrator が CssEaseInOut を足して LX-37 を消す。S2-notification が先に入っていても、このレーンは足さず取り込み時に integrator へ依頼する | 指揮の割り当て（第 2 回 4・第 3 回 8）。差の値は agent が計算 |
| D-17 | L3（EditModeInPlayingTest）はこのレーンでは作らず、録画 `uitk-tutorial-cues` を 1 本持つ | L3 を足す: 段の締め S2-ladder が L3 を持つ（共通の依頼）。録画: ランタイム（store の購読・層への常駐・脈動）に触れるので必須 | 共通の依頼・moores-writing-plans の規定 |
| D-18 | KeyControlHint は `UiLayer.OverlayPanel` の `UiFamily.Viewport`、枠は `UiLayer.Tutorial`、ピンは `UiLayer.WorldPin`（どちらも Viewport 族）に置く | 枠を stage 族に置く: アンカーは viewport 族（通知・ピン等）にもあり、Portal の最前面（`--z-portal-tutorial`）でないと枠が他の層の下に隠れる | agent 既定（Web の z トークン） |
| D-19 | アンカーの追跡は購読が 1 件以上の間だけ毎フレーム測る（`schedule.Execute(...).Every(0)`）。枠が無い間は止める | Web と同じくイベント駆動にする: Web も毎フレームではなく MutationObserver・ResizeObserver・IntersectionObserver を rAF で束ねるイベント駆動（`W/shared/tutorialAnchor/anchorRegistry.ts` の 11〜20 行）だが、UITK には `worldBound` の変化イベントが無く、スクロール（`scrollOffset`）や祖先の transform ではアンカー自身の geometry イベントが来ない。購読中だけ毎フレーム測る（前例 `UiMotionPlayer.cs` 23 行の `Every(0)`）。設計原則「状態変化は購読で」の例外で、測る対象（他画面のレイアウト）に購読口が無いことが理由 | agent 既定（plan §4.5）。棄却理由は指揮の指摘で直した |
| D-20 | blocking のスキットは S2-hud が作る `Client.Ui.State.SkitPresence.BlockingSkitPresence.IsBlocking`（W2a 設計書 A-56。中身は Web と同じ `SkitPresentationStateStore` の mode == blocking）を DI で受ける。このレーンは同じ役割の型・`Client.Ui.State.asmdef` の `Client.Skit` の行・その登録を作らず、Task 1 で S2-hud の取り込みを確かめ、無ければ止めて指揮へ報告する | 自前の `BlockingSkitState` を作る: 同じ役割の型を 2 レーンで作ることになる（設計書 §5.1）。`UIStateEnum.Story` で読む: スキットは UI の状態に関係なく始まる。`SkitManager` に変化通知を足す: 所有外で S2-skit と衝突する | 指揮の割り当て（実行計画で S2-tutorial は S2-hud に依存） |
| D-20b | 状態の源 3 つの受け取り方: `TutorialPresentationStateStore.Instance`・`WorldPinStateStore.Instance` は Model が static の Instance を直に読み、`UIStateControl` と `BlockingSkitPresence` は DI で受ける。前の 2 つは Client.Game が static シングルトンとして持ち DI に登録されていない（WebUiHost の topic も Instance を直に読む）ので前例に従い、後の 2 つは DI に登録済みの部品なので DI で受ける | 2 store を DI へ登録し直す: 所有外の登録を足すことになり、WebUiHost と受け取り方が分かれる | agent 既定（前例 `Client.WebUiHost/Game/Topics/Tutorial/*.cs`） |
| D-21 | ack の経路を作らないので（D-10）、DI へ interface で登録するコマンドはこのレーンに無い。Presenter は `GameUiRoot` と Model だけを受ける | `TutorialAnchorAckCommands` を `.As<ITutorialAnchorAckCommands>()` で登録する: 経路ごと落としたので対象が無い | 出所: 設計検査（fresh-context）→ D-10 で対象が消えた |
| D-22 | View は Model を購読せず `Render(VM)`（枠は `RedrawAnchor` も）を公開し、購読は Presenter が持つ。L1 が本番と同じ配線を通るよう、Presenter に static `Mount(layer, …, model, …)` を置き `Initialize` とテストの両方がそれを呼ぶ。Client.Ui に static Mount の前例は無く、L1 で `GameUiRoot` を持つ Presenter を作れないための例外とする（後続の画面は兄弟レーンと同じ Initialize の形。moores-code-review の設計判断 D-2 を agent 既定で案 A） | View の ctor で `model.Subscribe(Render)`: 設計書 §4.2 の Presenter/View の分担に反する。テストで購読を手書きする: 本番の配線（購読の順・ack）を L1 が通らない | 出所: 設計検査（fresh-context）→ agent 既定 |
| D-23 | 読みの口は `ITutorialAnchorStateLookup.TryGet` の 1 本にし、帳簿と追跡器の両方が実装する（View はこれだけを持つ）。変えの口は帳簿の `ApplyIfChanged`（変わった時だけ書いて true。名前を実処理に合わせた）・`KeepOnly` と、追跡器の `ITutorialAnchorSubscriptions` | 読みの口を帳簿と追跡器で別にする: 同じ働きの口が 2 本になる。1 つの interface に読みと変えをまとめる: View が購読の差し替えを呼べてしまう。`TryApply`: 何を試すのか名前から読めない | 出所: 設計検査（fresh-context）→ agent 既定 |
| D-24 | Try 関数は bool を返してよいが、false で隠す各所（`Redraw` の 2 か所・`RedrawGuide`・`TutorialPulseBinding.Start`・`TutorialDragGuideView.ApplyPlacement`）に `// silent-ok: <理由> / <誰がどこで気づくか>` を書く。false は失敗ではなく仕様どおりの非表示か、解決待ち | 例外か LogError: 毎フレームの正常な非表示でログが溢れる。理由は追跡器の遷移ログと L1 で届く | 出所: 設計検査（fresh-context）→ agent 既定 |
| D-25 | `session:element` の鍵は `TutorialElementKey.Of`、GUID→`UiText`→LogError は `TutorialTextFactory.TryCreate` の各 1 関数にまとめ、3 つの Projection と Fanout が使う | 各 Projection に書く: 鍵の綴りとログの文言が 3 か所でずれる | 出所: 設計検査（fresh-context）→ agent 既定 |
| D-26 | 枠の描き直しは、モデルの更新（購読の差し替え → `Render`）とアンカーの変化（`RedrawAnchor`）の 2 経路だけ。追っているアンカーへ後から枠が足されても、`Render` が全部描き直すので枠は出る（L1 `revisionが変わっても続くアンカーの状態を保ち枠を出したままにする`） | ack の送り直し（Web の `lastAck` の空け直し）を写す: ack を写さないので不要（D-10） | 出所: 設計検査（fresh-context）→ D-10 で書き直し |
| D-27 | `MarkClipContainer` を付ける義務は、ref-uitk-design への記載依頼として integrator へ出す。義務を見る L2 は新設しない | L2 で全画面の overflow 容器を検査する: 段階 2 では未移植の画面が多く、誤検知の表を持つことになる | 出所: 設計検査（fresh-context）→ agent 既定 |
| D-28 | `TutorialAnchorCoveragePending` の行の消し方（印を付けたレーンが同じ PR で自分の行を消す・S5-ladder で空になる）はファイル冒頭のコメントに書く。execution-plan E-1 等へは指揮が書く | 計画にだけ書く: 後続のレーンが表のファイルを開いたときに規則が見えない | 指揮の割り当て E |
| D-29 | 並べ比べの UITK 側は、Web の撮影状態（`referenceShots.ts` 85〜118 行・`tutorialControls.ts` 29〜61 行）と同じ状態を fixture で作って撮る。クロスヘアは S2-hud がマージ済みなら実物、レシピ一覧・研究ノードは Web と同じ矩形・祖先クリップ・ラベル文の要素に `TutorialAnchor.Mark`。撮り方は theme レーンの前例（`GameUiPanelSettings` の `Object.Instantiate` の複製に `targetTexture` を付けて `ReadPixels`）。DragGuide は Web の動画 `4-drag-guide.webm`（参照の `index.md` 253 行）のコマと、同じ状態の fixture を 100ms おきに撮った連番を並べる。ポーズ後だけは Web の参照が無いので UITK のみ（自己確認）。fixture の矩形は Web の枠の外形から padding だけを引いて作る（border-box）。複製パネルのルートには本番と同じロケールのクラスを付ける | 実プレイ（ポーズの設定ボタン等）で撮って並べる: Web と状態が違い、差が画面の違いか実装の違いか分からない。Game ビューの解像度を切り替える: 3 計画で撮り方が揃わず、Editor の表示倍率が混ざる | 指揮の指摘（第 2 回 1・8。再現済み。第 5 回 5・6・7）→ fixture の作り方は agent 既定 |
| D-30 | 脈動は画面ルートに 1 つの `TutorialPulseBinding` が、ルートで読んだ周期を持ち、後から `Attach` した子にも当てる（前例 `ToastHostView` 37〜87 行）。子はパネルに載っている間だけ流し、周期が変わったら載っている子だけ流し直す。DragGuide は寸法の解決後に置き直す | 子ごとにルートの `CustomStyleResolvedEvent` で始める: Presenter が空で常駐し、後から作った枠・ヒント・矢印はルートが再解決するまで脈動せず、外した子へ `Play` して `UiMotionPlayer` が LogError を出す。寸法 0 のまま置く: 両端が揃った状態で始まると 28px ずれたまま残る | 指揮の指摘（第 3 回 1・2）→ 形は agent 既定 |
| D-31 | `--key-cap-color` の既定は、E-19 でこのレーンが持つ `KeyCap.uss` の中に `:root { --key-cap-color: var(--text-high-contrast); }` として置き、`.key-cap { color: var(--key-cap-color); }` で読む。前提: `:root` はシートを当てた最上位の要素を指す。今は GameUiTheme.tss の @import でパネルのルート。部品が自分に styleSheets.Add すると壊れる。画面ルートの上書き（KeyControlHint）は継承で勝つ。既存の部品 USS（`Client.Ui/Components/**/*.uss`）に `:root` 宣言の前例は無い（`:root` を持つのは `Foundation/Theme/` のトークンの USS だけ）が、つまみの既定を部品自身が持てば所有外のトークン USS を触らずに済み、部品を読み込んだパネルでだけ効く | 入れ子の `var()` の fallback（`var(--key-cap-color, var(--text-high-contrast))`）: UITK で効くかの実測が要り、効かなければ integrator 待ちになる。`DerivedTokens.uss` に既定を置く: 所有外 | 指揮の割り当て（第 4 回の追加） |
| D-32 | アンカーの追跡は `StopTracking()` で止める（購読を全部外し、状態を捨て、毎フレームの測定を Pause）。Presenter の `Mount` が View の `DetachFromPanelEvent` で止め、`AttachToPanelEvent` で今の ViewModel から購読し直す。L1 の土台 `TutorialOverlayTestHost` は `CreateTracker` で作った追跡器を `[TearDown]` で止め、ルートを空にする（Mount した画面は外れて止まる） | 追跡器を IDisposable にして Presenter が持つ: ゲーム寿命の画面に Dispose の呼び手が無く（AGENTS.md の既知の制約）、テストだけのための口になる。テストごとにパネルを作り直す: 土台 `UiTestHost` の作りを変えることになり所有外 | 指揮の指摘（第 5 回 3）→ 止め方は agent 既定 |
| D-33 | `worldBound` が NaN（レイアウト前）のアンカーは `Hidden(ZeroArea)` として扱い、測れた次のフレームで Ready へ遷移する。ZeroArea は報告の対象外なので、レイアウト待ちでログは出ない | 新しい理由 `NotLaidOut` を足す: Web の理由の列（`resolveAnchor.ts`）に無い語彙になり、Web でも配置前の要素は面積 0 の矩形になる。NaN の間は測らず前の状態を保つ: 初回は前の状態が無く、`rect.Width <= 0` を NaN がすり抜けて NaN の矩形の Ready になる穴が残る | 指揮の指摘（第 5 回 8）→ 扱いは agent 既定 |
| D-34 | tss の `@import` は USS を作るタスク（Task 6・7・8・9）が 1 行ずつ足して同じコミットに含め、Task 12 は 4 行あることを確かめるだけにする。L1 の helper `Px`・`Ms` は Task 5 の `TutorialOverlayTestHost` に置き、`TutorialPulseBindingTest` は `TutorialOverlay.uss` と tss の行が入る Task 6 で足す。各タスクがコンパイル → テスト → コミットで閉じる | 4 行を Task 12 でまとめて足す: L1 は本番の tss を読むので、USS の値を主張する Task 6〜11 のテストが Task 12 まで通らない。4 行を Task 6 で先に足す: まだ無い USS を読み込み `UiThemeImportBundleTest` が落ちる | 指揮の指摘（第 5 回 4・9） |
| D-35 | Task 14（並べ比べ）を Task 13（記帳）より先に行う（実行順 Task 12 → 14 → 13 → 15）。LX-35・LX-37 は Web の `4-drag-guide.webm` のコマと並べてから書く | 番号どおりに行う: LX の行が Web と並べる前の推定のまま台帳に入る | 指揮の指摘（第 5 回 5）→ 番号を振り直さず順序の注記にしたのは agent 既定 |
| D-36 | alwaysOnHudWheel の「%s は入力を奪わない」のうちキー操作ヒントの 1 行は、このレーンの `KeyControlHintTest` に同じ web-test タグで写す。View は行を作るたびに子孫まで `PickingMode.Ignore` を掃く（S2-hud D-11 と同じ 1 行） | S2-hud の `AlwaysOnHudPickingTest` に足す: 他レーンのテストを書き換えることになる（S2-hud 計画 52 行が各レーンで自分の HUD を足すと決めている） | 指揮の指摘（第 5 回 10）・S2-hud 計画 52 行 |

### 未確認

- `padding: var(--label-face-padding)`（複数値の var）が展開されるか（Task 6 Step 4 で実測）
- `ScrollView.viewportUssClassName` の要素の `worldBound` が、スクロール中の viewport の矩形を返すか（Task 5 の L1 で確かめる）
- `UiMotionPlayer` が対象の Detach で自動で止まるか（`TutorialPulseBinding` は自分で `Cancel` する）
- `CustomStyleResolvedEvent` がクラスの付け外しだけで画面ルートに再び来るか（`TutorialPulseBindingTest` の 2 件目の前提。来なければ `--tutorial-attention-pulse-period` を変える probe のクラスで確かめる）
- DSL にカメラを回す操作があるか（Task 15 Step 1 の分岐）
- `capture.ts` が撮影名を引数で絞れるか（Task 14 Step 2 の分岐）
- 部品 USS（`KeyCap.uss`）の `:root` 宣言が、テストパネルと本番パネルの両方で既定として効くか（Task 8 Step 2 の既存 `KeyCap…色は高コントラスト` と新しい `KeyCapTest` で確かめる）

## 本PR外のリファクタ提案

- S2-notification のマージ後に CssEaseInOut を足して LX-37 を消す（integrator）

---

レーンの起動は指揮が行う（実行計画 §4.1）

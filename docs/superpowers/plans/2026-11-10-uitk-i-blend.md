# UITK I-blend（半透明を Web と同じ sRGB 空間で合成する）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanは指揮の指示により Execution Handoff をせず、起草したセッションがそのまま実行する（I-blend 依頼文）。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** GameUi の UITK パネルを Web（Chrome）と同じ sRGB 空間で合成し、半透明トークンごとの α 補正（指数・地の選び分け・手合わせの値）をすべて外して Web の α に戻す。

**Architecture:** PanelSettings の `forceGammaRendering` を立て、パネルを画面と同じ大きさの RT（sRGB 変換なし）へ描かせる（UI 同士の合成が sRGB の値の上で行われ、RT は乗算済みの色と被覆率を持つ）。URP の renderer feature が AfterRendering で、ポスト処理後の世界の中間色を sRGB にして RT を over で重ね、画面へ書く（UI が無い画素は捨てて URP の結果を残す）。補正の仕組み（生成器 `translucency.mjs`・`LinearBlendOpacity`・グローの地の選び分け `TutorialAnchorGround`/`UiSurface`・手合わせの値）は撤去する。

**Tech Stack:** Unity 6000.3.8f1・URP 17.3（互換モード `URP_COMPATIBILITY_MODE`）・UI Toolkit・Node（トークン生成器）

## Requirements

- R-1 UI 同士の半透明の合成が Web の式（sRGB の over）と一致する。受け入れ: ランタイムの PanelSettings で灰の地 7 段（16〜243）に Web の α の見本 6 色を描き、最大差 2 段以内（`uitk-i-blend-measure.cs`・`UiRuntimePanelCapture`）
- R-2 世界（3D）の上の UI の半透明が Web の式と一致する。受け入れ: PlayMode で世界の 4 行に同じ見本を置き、最大差 2 段以内
- R-3 3D の見た目を変えない。受け入れ: UI を全部隠した画で、旧方式と新方式の地面の画素差が最大 2・平均 1 未満（フレーム間の揺れの範囲）
- R-4 入力（ピッキング）が変わらない。受け入れ: 1280×720・2560×1080 で画面座標から要素を拾える（`uitk-i-blend-perf.cs`）、`uitk-grab-overlay`・`uitk-hud`・`uitk-tutorial-cues`・`uitk-lifeline` 全合格
- R-5 フレーム時間の悪化が小さい。受け入れ: GPU 中央値の増分が 1280×720 で 0.2ms・2560×1080 で 1ms 以内（Editor 実測。`uitk-i-blend-perf.cs`）
- R-6 半透明トークンと部品の α は Web の値そのまま。受け入れ: 生成器に α 補正の式が無く、`DerivedTokens.uss`・`Tokens.uss` の半透明の α が Web と同じ。`LinearBlendOpacity`・`TutorialAnchorGround`・`UiSurface`・`--tutorial-attention-glow-over-world` が無い
- R-7 不透明な部品の並べ比べを壊さない。受け入れ: C2〜C4・S2 の done.md の表にある不透明な部品（スロット・パネル・ボタン）の色が撮り直しで悪化しない
- R-8 解消した見た目の例外（LX-23・LX-38・LX-46・LX-66・LX-81）を台帳から外し、実測の結論に移す
- やらないこと: LX-86（キー操作ヒントの影の太さ。0.35px の影の描き方の差で合成方式と別の原因）の作り直し、Render Graph 経路の実装（本 repo は互換モード）、Web 側の変更

## Global Constraints

- コードは 1 ファイル 200 行未満、1 ディレクトリ 10 ファイルまで
- `Func<>` 禁止、`partial` 禁止、try-catch は外部境界のみ
- Prefab・アセットの直接編集禁止（`uloop execute-dynamic-code` 経由は可）
- 並走中の S4-alpha（Screens/BlockInventory・State の SlotCommands・Routing）に触れない
- 設計書の判断記録は A-180〜A-189、台帳の見た目の例外は LX-147〜LX-155
- `.cs` を変えたら `uloop compile`、`^Client\.Tests\.Ui\.` を通す

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 新規パターンは URP の renderer feature を Client.Ui に置く 1 点（A-181）。画面の大きさの追従は前例 `Client.Game/InGame/BugReport/Recording/ScreenFrameReader.cs:37` と同じ形
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱1・第3バケツ1 / 弱: `UiRuntimePanelCapture` で forceGammaRendering を 2 か所で使う → 1 つの変数 `srgbValueRendering` に畳んだ。第3バケツ: `ScreenFrameReader` と `UiSrgbPanelTarget.SyncToScreenSize` の画面の大きさ追従の一本化（本 PR 外）

### 配置と前例

| # | 項目 | 配置 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `UiSrgbPanelTarget` | Client.Ui/Foundation/Composite | PanelSettings の複製に RT を当てる | `RenderPipelineQualityApplier`（URP アセットを実行時に複製し元アセットを汚さない） |
| 2 | `UiSrgbCompositeFeature`・`UiSrgbCompositePass`・`UiSrgbWorldColorCapturePass` | Client.Ui/Foundation/Composite | URP ScriptableRendererFeature（互換モードの Execute） | URP の `FinalBlitPass`（上下反転・sRGB 符号化の判定を写す） |
| 3 | `UiSrgbCompositeSource` | 同上（internal static） | RT の受け渡し | なし（renderer feature は ScriptableObject で DI に乗らない） |
| 4 | `GameUiRoot` の Update で画面の大きさを追う | Foundation/Root | 毎フレームの比較 | `Client.Game/InGame/BugReport/Recording/ScreenFrameReader.cs:37`（画面の大きさと RT を比べて作り直す）。判断記録 D-5 |

---

### Task 1: sRGB 合成の土台（試作の仕上げ）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Composite/UiSrgbPanelTarget.cs`・`UiSrgbCompositeSource.cs`・`UiSrgbCompositeFeature.cs`・`UiSrgbCompositePass.cs`・`UiSrgbWorldColorCapturePass.cs`・`UiSrgbComposite.shader`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Root/GameUiRoot.cs`、`moorestech_client/Assets/Scripts/Client.Ui/Client.Ui.asmdef`（URP Runtime・Core Runtime 参照）、`moorestech_client/Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset`（forceGammaRendering: 1。uloop 経由）、`moorestech_client/Assets/Asset/Common/URPSettings/URP Data.asset`・`URP Data - Skit.asset`（feature 追加。uloop 経由）、`moorestech_client/Assets/Scripts/Client.Ui.Editor/Capture/UiRuntimePanelCapture.cs`（本番が forceGammaRendering なら RT を Linear で作り、読み戻しも linear の Texture2D）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Composite/UiSrgbCompositeSetupTest.cs`

**Interfaces:**
- Produces: `UiSrgbCompositeSource.TryGetTexture(out RenderTexture)`、`UiSrgbPanelTarget(UIDocument)`・`SyncToScreenSize()`・`Dispose()`、`UiRuntimePanelCapture.Render` が本番と同じ合成で描く

- [x] Step 1: 試作（パネル RT・合成シェーダ・feature・AfterRenderingPostProcessing で世界の色を控えるパス）を書き PlayMode で UI が出ることを確かめる（コミット db8fe490ad）
- [x] Step 2: `UiRuntimePanelCapture` を本番の forceGammaRendering に合わせる

```csharp
var readWrite = source.forceGammaRendering ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, readWrite) { hideFlags = HideFlags.DontSave };
// ReadBack: new Texture2D(width, height, TextureFormat.RGBA32, false, source.forceGammaRendering)
```

- [x] Step 3: テストを書く（本番設定の照合と、地 128 に黒 0.6 を描いて Web の 51 になること）

```csharp
[Test] public void ProductionPanelSettingsRendersInSrgb() { Assert.IsTrue(settings.forceGammaRendering); }
[Test] public void BothUrpRenderersCarryTheCompositeFeatureWithItsShader() { /* URP Data・URP Data - Skit の rendererFeatures に UiSrgbCompositeFeature があり compositeShader が非 null */ }
[TestCase(0f, 0f, 0f, 0.6f, 128, 51, 51, 51)]
[TestCase(1f, 0f, 0f, 0.24f, 30, 84, 23, 23)]
[TestCase(1f, 1f, 1f, 0.22f, 16, 69, 69, 69)]
public void TranslucentOverOpaqueMatchesTheWebFormula(...) { /* UiRuntimePanelCapture.Render で読み戻し ±1 */ }
```

- [x] Step 4: compile → `uloop run-tests --filter-type class --filter-value "Client.Tests.Ui.Composite.UiSrgbCompositeSetupTest"` PASS → コミット

### Task 2: 生成器と派生トークンを Web の α へ戻す

**Files:**
- Delete: `tools/uitk/webTokens/translucency.mjs`、`tools/uitk/webTokens/handWrittenDerivedRows.mjs`、`docs/uitk/translucent-tokens.md`
- Modify: `tools/uitk/webTokens/postProcessToken.mjs`（α を変えず素通し）、`tools/uitk/convert-web-tokens.mjs`（半透明の表・指数の照合・LX-81 の印を外す）、`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/Tokens.uss`・`SkitTokens.uss`（再生成）、`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss`（補正値を Web の値へ・LX-23/38/46/66 の印を外す・`--tutorial-attention-glow-over-world` を消す）、`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Modal/UiModalHost.uss`・`moorestech_client/Assets/Scripts/Client.Ui/Screens/TrainHud/TrainRidingHud.uss`・`moorestech_client/Assets/Scripts/Client.Ui.Editor/Gallery/Entries/Panels/PanelsAndDecorSamples.uss`（「α 補正」のコメント）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/TokenFloorAndTranslucencyTest.cs` ほか期待値を持つテスト（Files 一覧は Task 4 の末尾）

- [x] Step 1: `postProcessToken` を「α を変えない」へ、生成器から表と照合を外し `node tools/uitk/convert-web-tokens.mjs` で再生成
- [x] Step 2: `DerivedTokens.uss` の補正値を Web の値へ（例: `--modal-backdrop-face: rgba(0, 0, 0, 0.6)`、`--screen-backdrop-face: rgba(0, 0, 0, 0.42)`、`--slot-frame-face: rgba(50, 52, 67, 0.5)`、`--slot-frame-insufficient-opacity: 0.4`、`--slot-frame-ghost-opacity: 0.35`、`--fluid-slot-ghost-opacity: 0.35`、`--game-panel-rule-*`・`--game-panel-triangle-face`・`--game-panel-title-shadow` は Web の α）
- [x] Step 3: テストの期待値を Web の値へ直し、`^Client\.Tests\.Ui\.` を回す → コミット

### Task 3: `LinearBlendOpacity` を撤去し重みを素直に掛ける

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/LinearBlendOpacity.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/LinearBlendOpacityTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Components/Panels/GamePanel/GamePanelFaceOpacityRamp.cs`（`faceAlpha * MaskWeightAt(...)`）、`moorestech_client/Assets/Scripts/Client.Ui/Components/Gauges/ProgressArrowGlyph/ProgressArrowGlyphShadowMesh.cs`・`moorestech_client/Assets/Scripts/Client.Ui/Components/Panels/DialogFrame/DialogFrameShadow.cs`・`moorestech_client/Assets/Scripts/Client.Ui/Screens/CommonHud/CrosshairShadowRing.cs`・`moorestech_client/Assets/Scripts/Client.Ui/Screens/Shell/ScreenVignette.cs`（`color.a * coverage`）
- Test: `GamePanelFaceOpacityRampTest.cs`・`ScreenVignetteTest.cs`・`ProgressArrowGlyphShadowMeshTest.cs`

- [x] Step 1: 呼び出し 5 か所を `alpha * weight` に置き換え、クラスとテストを消す
- [x] Step 2: テストの期待値を線形の掛け算へ（例: skit 上端の 50% 点は `0.88 * 0.5 = 0.44`）→ compile・テスト → コミット

### Task 4: グローの地の選び分けを撤去する

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.Ui.State/Tutorial/Logic/TutorialAnchorGround.cs`、`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/UiSurface.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/UiSurfaceTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Tutorial/Logic/TutorialAnchorState.cs`（Ready から Ground を外す）、`moorestech_client/Assets/Scripts/Client.Ui/Screens/Tutorial/Anchor/TutorialAnchorMeasure.cs`、`moorestech_client/Assets/Scripts/Client.Ui/Screens/Tutorial/Overlay/TutorialOutlineView.cs`・`TutorialOverlayView.cs`・`TutorialOverlay.uss`、`moorestech_client/Assets/Scripts/Client.Ui/Components/Panels/{GamePanel/GamePanel.cs,GamePanelSurface/GamePanelSurface.cs,DialogFrame/DialogFrame.cs,PaperSurface/PaperSurface.cs}`、`moorestech_client/Assets/Scripts/Client.Ui/Screens/PauseMenu/PauseMenuView.cs`（`UiSurface.Mark` を外す）
- Test: `TutorialAnchorStateBookTest.cs`・`TutorialDragGuideGateTest.cs`・`TutorialAttentionTokenTest.cs`・`TutorialRingDesignTest.cs`・`GamePanelSurfaceTest.cs`・`PauseMenuSurfaceTest.cs`

- [x] Step 1: `Ready(rect, clip)` の 2 引数へ戻し、`ShowRing` から `glowOverWorld` を外し、`--over-world` の class・トークンを消す
- [x] Step 2: テストを直す → compile・`^Client\.Tests\.Ui\.` → コミット

### Task 5: 文書（設計書・台帳・スキル）

**Files:**
- Modify: `docs/uitk-migration/execution/component-architecture.md`（A-180〜A-185）、`docs/uitk/TODO.md`（LX-23・38・46・66・81 を見た目の例外から外し、実測の結論に I-blend の行を足す）、`.agents/skills/ref-uitk-design/SKILL.md`・`references/compare-procedure.md`・`references/css-to-uss.md`・`references/component-catalog.md`（α 補正の記述を sRGB 合成へ）

- [x] Step 1: 書く → `LookExceptionLedgerTest` を回す → コミット

### Task 6: 並べ比べの撮り直しと unityプレイ録画テスト

- [x] Step 1: 不透明な部品（ItemSlot・GamePanel・FormButton・TooltipFace）を `UiRuntimePanelCapture` で Web 参照（`web-a9d5123537`）と同じ地で撮り、旧方式（feature/uitk の done.md の表）と色の差を比べる
- [x] Step 2: unityプレイ録画テスト（C-1 で合成パスを final blit 後へ移した後に既定画質と描画解像度 50% で再実測済み。既定: 世界・面とも最大 1 段。50%: 面は最大 1 段、世界は平均 0.3〜0.8 段で、地平線の細部の行だけ 2% の画素が 5〜96 段ずれる〈URP の拡大と合成の双線形読みの差〉）: `uitk-i-blend-measure`・`uitk-i-blend-perf`・`uitk-hud`・`uitk-grab-overlay`・`uitk-tutorial-cues`・`uitk-lifeline`・`uitk-gameview-resolution-switch`。全合格・エラーログ 0
- [x] Step 3: 2-game-hud・3-modal・チュートリアル枠を PlayMode で撮り、Web 参照と並べる

### Task 7: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

### Task 8: セッション終了可能状態にすること

pr-createスキルでPRを作成（base=feature/uitk）し、origin/feature/uitk を取り込んでコンフリクトを解消・コンパイル確認のうえ push する。G1 緑を見て `gh pr merge --merge`。全作業がコミット・push 済みであることを確認して終える。

## 判断記録（ADR）

- D-1（A-180）合成方式は「パネルを sRGB 変換なしの RT に `forceGammaRendering` で描き、URP の最後に世界を sRGB にして over で重ねる」。比べた案: (b) RT なしで `forceGammaRendering` だけ → 二重に sRGB 化され最大 67 段外れる。(c) 現状維持（トークンごとの α 補正）→ 最大 50 段（ゴースト・グロー）。採った (a) は世界の上・面の上とも最大 2 段（I-blend 実測）。出所: agent 既定（指揮「UI を Web と同じ sRGB 空間で合成できるかを調べる」）
- D-2（A-181）合成は URP の final blit（AfterRendering + 1）より後（AfterRendering + 2）で、final blit と同じカメラの最終ターゲットへ、画面の解像度で行う。AfterRenderingPostProcessing で中間色へ重ねると描画解像度を下げたとき（画質設定の 50%・75%）UI まで拡大されてぼける。世界の色は AfterRenderingPostProcessing のパスで控えたハンドルを読む（AfterRendering では URP のハンドルが画面側に切り替わる）。code-review 指摘（C-1）で final blit より前だった配置を直した。直した後に既定画質と描画解像度 50% で再実測した（Task 6 Step 2）。UI が無い画素は捨てて URP の結果を残すので、3D は描画解像度を下げても URP の拡大のまま。UI が半透明の画素だけ世界を双線形で拡大して読む
- D-3（A-182）renderer feature は Client.Ui（Foundation/Composite）に置き、URP Data と URP Data - Skit の両方に載せる。互換モード（`URP_COMPATIBILITY_MODE`）なので Execute だけ実装する。Render Graph へ切り替えたら URP が毎フレーム「Execute/RecordRenderGraph が無い」と警告し UI が出ないので気づける（silent-ok ではない）
- D-4（A-183）重ねるカメラは「画面へ出す base カメラのうち depth 最大」1 台（スタックの最後の overlay を含む）。二度重ねると半透明が濃くなる
- D-5 RT の生成と画面の大きさの追従は GameUi の系でだけ登録する `GameUiSrgbCompositeInstaller`（IInitializable・ITickable。root.panel を控える入口より先に登録）の Tick で `Screen.width/height` と RT を比べる。Unity に画面の大きさの変化通知が無く、RT の大きさが変わらないとパネルの `GeometryChangedEvent` も起きないため（設計原則「Update で同値判定をしない」の例外）
- D-6 `UiSrgbCompositeSource` の RT が null なのは GameUi を使っていない間だけ（作り損ねは `UiSrgbPanelTarget` の生成時に例外。シェーダ欠落は feature の Create で例外。世界の色を控え損ねたフレームは `Debug.LogError`）
- D-7（A-184）α 補正の仕組みを全部撤去する: 生成器 `translucency.mjs`・`handWrittenDerivedRows.mjs`・半透明の表 `docs/uitk/translucent-tokens.md`、`LinearBlendOpacity`、グローの地の選び分け（`TutorialAnchorGround`・`UiSurface`・`--tutorial-attention-glow-over-world`。A-136 を覆す）、手合わせの値（LX-23 のスロット 0.42・0.2・0.18、LX-66 のゴースト 0.16、`--game-panel-rule-*` の面色の厳密式）。トークンの α は Web の値そのもの
- D-8（A-185）部品ギャラリー（EditorWindow）は Editor のパネルで sRGB のまま合成するので、ランタイムと同じ見え方になる。compare-procedure の「色はランタイムの PanelSettings で比べる」は残す（撮影の道具が本番の設定を写すため）
- D-9 ランタイム挙動（描画・入力）に触れるので unityプレイ録画テストを Task 6 に入れる
- D-10 user-simulator のレビューと Execution Handoff は、指揮の依頼（このセッションで実行・§5 の独立評価不要）により行わない
- D-11 code-review 設計判断 1（推奨 A を採用）: パネル RT と合成パスは GameUi の系でだけ作る。`GameUiRoot.Awake/Update` に置くと既定の Web 起動でも画面大の RT・中間テクスチャ・2 パスが毎フレーム走るため、`GameUiSrgbCompositeInstaller`（IInitializable・ITickable・IDisposable）を `MainGameUiRegistration` の `RegisterComponent(gameUiRoot)` 直後に登録する（パネルを差し替えるので root.panel を控える `GameUiInputInstaller` より先）。出所: moores-code-review の design.md 推奨（指揮の自律実行の依頼により agent 採用）
- D-12 code-review 設計判断 2（推奨 A を採用）: GameUi はカメラの最後で世界へ重なるので uGUI の Screen Space Overlay（CEF を含む）より常に下に出る。仕様として `docs/uitk/TODO.md` の S1 行と A-180 に書き、コードは変えない

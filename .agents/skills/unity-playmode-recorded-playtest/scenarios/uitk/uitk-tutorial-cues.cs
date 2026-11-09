// S2-tutorial: Web ホスト無しで、ワールドピン・キー操作ヒント・強調枠とラベル・DragGuide を UITK で出して録画し、追従と動きを実測する
// S2-tutorial: with no web host, show the world pin, key-control hint, outline with label and DragGuide in UITK, record them and measure tracking and motion
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立て、MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9 を Editor へ立ててから run-scenario.sh を呼ぶ
// Stop PlayMode, call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code and set MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9 in the Editor before run-scenario.sh
// 撮影は 1280×720 の 1 回だけ。Web との並べ比べは L1 の fixture が持ち、このスクショは実プレイの自己確認
// Captured once at 1280x720; the side-by-side with Web lives in the L1 fixtures, these shots are a real-play self check
using System;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Control;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Root;
using Client.WebUiHost.Boot;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

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
    var root = GameUiRoot.Instance.GetRootElement();
    p.Assert(root.Q("tutorial-overlay") != null && root.Q("key-control-hint-hud") != null && root.Q("world-pin-overlay") != null, "Tutorial の 3 画面が DI で組まれて木に載っている");

    // チャレンジ 1 のワールドピン（画面内と、振り向いた後の画面端矢印）
    // Challenge 1's world pin (on screen, then the edge arrow after turning around)
    p.Note("チャレンジ 1 のワールドピン");
    await p.Until(() => WorldPinStateStore.Instance.GetCurrent().Pins.Length > 0, 20f, "ワールドピンの射影が store に来た");
    // 戻った直後はカメラが落ち着かずピンが画面内外を行き来するので、1s 変わらなくなるまで待つ
    // Right after returning the camera still settles and the pin flips on/off screen, so wait until it holds for 1s
    var (pinOnScreen, pinSince) = (WorldPinStateStore.Instance.GetCurrent().Pins[0].OnScreen, Time.realtimeSinceStartup);
    await p.Until(() => { var now = WorldPinStateStore.Instance.GetCurrent().Pins[0].OnScreen; if (now != pinOnScreen) (pinOnScreen, pinSince) = (now, Time.realtimeSinceStartup); return Time.realtimeSinceStartup - pinSince > 1f; }, 10f, "ピンの画面内外が 1s 変わらない");
    await TurnUntilPin(true);
    await p.Until(() => IsShown("world-pin"), 10f, "画面内のピンが出た");
    var (markerMin, markerMax) = await ScaleRange("world-pin__marker", 1.3f);
    p.Note($"画面内ピンの scale min={markerMin:F3} max={markerMax:F3}");
    p.Assert(markerMax < 1.001f, "画面内のピンは脈動しない（脈動は矢印だけ）");
    await p.Screenshot("01-world-pin-onscreen");
    // 画面外へ出てからさらに 60° 回し、画面の縁で出入りしない位置で撮る
    // Turn another 60 degrees after the pin leaves so it does not flicker at the screen edge
    await TurnUntilPin(false);
    await Turn(2);
    await p.Until(() => IsShown("world-pin__arrow"), 10f, "画面端の矢印が出た");
    var (arrowMin, arrowMax) = await ScaleRange("world-pin__arrow-glyph", 1.3f);
    p.Note($"画面端の矢印の scale min={arrowMin:F3} max={arrowMax:F3}");
    p.Assert(arrowMax > 1.07f && arrowMax <= 1.081f && arrowMin < 1.01f, "画面端の矢印は 1→1.08 で脈動する");
    var pin = WorldPinStateStore.Instance.GetCurrent().Pins[0];
    p.Note($"画面外ピン dir=({pin.DirectionX:F2},{pin.DirectionY:F2})");
    await p.Screenshot("02-world-pin-offscreen");

    // キー操作ヒント（Tab・GameScreen）を store へ差し込み、1.08 の脈動を測る
    // Inject a key-control hint (Tab, GameScreen) into the store and measure the 1.08 pulse
    p.Note("キー操作ヒント（Tab）");
    var sessionGuid = Guid.NewGuid();
    var tutorialGuid = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements[0].Challenges[0].Tutorials[0].TutorialGuid;
    TutorialPresentationStateStore.Instance.BeginSession(sessionGuid);
    TutorialPresentationStateStore.Instance.AddKeyControlHint(tutorialGuid.ToString(), "Tab", "GameScreen");
    await p.Until(() => IsShown("key-control-hint-hud"), 10f, "キー操作ヒントが出た");
    var (minHintScale, maxHintScale) = await ScaleRange("key-control-hint__item", 2.5f);
    p.Note($"ヒントの脈動 scale min={minHintScale:F3} max={maxHintScale:F3}");
    p.Assert(maxHintScale > 1.07f && maxHintScale <= 1.081f && minHintScale < 1.01f, "キー操作ヒントは 1→1.08 で脈動する");
    await p.Screenshot("03-key-control-hint");

    // ポーズを開き、設定のボタンに枠とラベル、セーブ→設定へ DragGuide
    // Open the pause menu, outline the settings button with a label and drag-guide from save to settings
    p.Note("ポーズで設定に枠、セーブ→設定へ DragGuide");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
    TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.PauseSettings, tutorialGuid);
    TutorialPresentationStateStore.Instance.AddDragGuide(AnchorIds.PauseSave, AnchorIds.PauseSettings);
    await p.Until(() => IsShown("tutorial-highlight-label"), 10f, "枠のラベルが出た");
    await p.Screenshot("04-outline-label");
    p.Assert(!IsShown("key-control-hint-hud"), "ポーズ中（uiState 不一致）はキー操作ヒントを出さない");

    // DragGuide の 1 周（3.2s）を毎フレーム標本し、fps も同じ窓で測る
    // Sample one DragGuide loop (3.2s) every frame and measure fps over the same window
    var guide = root.Q("tutorial-drag-guide");
    var samples = new System.Collections.Generic.List<(float t, float opacity, float y)>();
    var frameStart = Time.frameCount;
    var sampleStart = Time.realtimeSinceStartup;
    // 動きはパネルの時計（録画中は Recorder の固定 30fps に従う）で進むので、標本の時刻も同じ unscaledTime で取る
    // Motion runs on the panel clock (fixed 30fps under the Recorder), so sample times use the same unscaledTime
    var guideClockStart = Time.unscaledTime;
    while (Time.unscaledTime - guideClockStart < 3.4f)
    {
        samples.Add((Time.unscaledTime - guideClockStart, guide.resolvedStyle.opacity, guide.resolvedStyle.translate.y));
        await UniTask.Yield();
    }
    var fps = (Time.frameCount - frameStart) / (Time.realtimeSinceStartup - sampleStart);
    p.Note($"枠2（チャレンジ1の本物を含む）・DragGuide1（アンカー3）表示中の fps={fps:F1}");
    var opaque = samples.Where(s => s.opacity > 0.99f).ToList();
    var travel = samples.Max(s => s.y);
    var arrived = samples.First(s => s.y > travel - 0.5f);
    p.Note($"DragGuide 不透明 {opaque.First().t:F2}s〜{opaque.Last().t:F2}s・終点 {arrived.t:F2}s（移動 {travel:F0}px）・最小 opacity {samples.Min(s => s.opacity):F2}");
    p.Assert(samples.Min(s => s.opacity) < 0.05f && opaque.Count > 0, "DragGuide は透明と不透明を往復する");
    p.Assert(Mathf.Abs(opaque.Last().t - opaque.First().t - 0.6f * 3.2f) < 0.25f, "不透明の区間は 1 周の 15%〜75%（約 1.92s）");
    await p.Screenshot("05-drag-guide-mid");

    // アンカーの祖先を translate で動かし、枠の中心がアンカーに何フレームで追いつくか測る
    // Move the anchor's ancestor with translate and count frames until the outline center catches up
    var anchor = ((TutorialAnchorResolution.Unique)TutorialAnchorRegistry.Resolve(root, AnchorIds.PauseSettings)).Element;
    // チャレンジ 1 の本物の枠（challenge.current-hud）も出ているので、設定のボタンに一番近い枠を選ぶ
    // Challenge 1's real outline (challenge.current-hud) is also shown, so pick the ring nearest the settings button
    var ring = root.Query(className: "tutorial-outline__ring").ToList().OrderBy(r => Vector2.Distance(r.worldBound.center, anchor.worldBound.center)).First();
    p.Note($"移動前のずれ {CenterGap():F1}px");
    anchor.parent.style.translate = new Translate(new Length(60f), new Length(0f));
    var lagFrames = 0;
    for (; lagFrames < 30 && (lagFrames == 0 || CenterGap() > 1f); lagFrames++) await UniTask.Yield();
    p.Note($"祖先 translate 60px 後の追従 {lagFrames} フレーム（ずれ {CenterGap():F1}px）");
    p.Assert(lagFrames <= 2 && CenterGap() <= 1f, "枠は祖先の移動に 2 フレーム以内で追従する");
    anchor.parent.style.translate = StyleKeyword.Null;
    await p.WaitSeconds(0.5f);
    var settingsCenter = anchor.worldBound.center;

    // ポーズを閉じると枠はアンカーと一緒に消える
    // Closing the pause menu removes the outline together with its anchor
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.Until(() => !root.Query(className: "tutorial-outline__ring").ToList().Any(r => IsShownElement(r) && Vector2.Distance(r.worldBound.center, settingsCenter) < 5f), 5f, "アンカーが消えたら枠も消える");
    p.Assert(!IsShown("tutorial-drag-guide"), "アンカーが消えたら DragGuide も消える");
    await p.Until(() => IsShown("key-control-hint-hud"), 5f, "ゲーム画面へ戻るとキー操作ヒントが戻る");
    await p.Screenshot("06-after-pause");
    TutorialPresentationStateStore.Instance.EndSession(sessionGuid);

    #region Internal

    // 名前かクラスで引き、祖先まで display が Flex か
    // Look up by name or class and check the element and its ancestors are Flex
    bool IsShown(string nameOrClass)
    {
        return IsShownElement(root.Q(nameOrClass) ?? root.Q(className: nameOrClass));
    }

    bool IsShownElement(VisualElement element)
    {
        for (var current = element; current != null; current = current.parent)
        {
            if (current.resolvedStyle.display == DisplayStyle.None) return false;
        }
        return element != null;
    }

    float CenterGap() => Vector2.Distance(ring.worldBound.center, anchor.worldBound.center);

    // 1.3s〜の窓でクラスの最初の要素の scale の最小・最大を毎フレーム測る
    // Measure the min and max scale of the first element with the class every frame over a window
    async UniTask<(float min, float max)> ScaleRange(string className, float seconds)
    {
        var (min, max) = (2f, 0f);
        for (var start = Time.realtimeSinceStartup; Time.realtimeSinceStartup - start < seconds;)
        {
            var scale = root.Q(className: className).resolvedStyle.scale.value.x;
            (min, max) = (Mathf.Min(min, scale), Mathf.Max(max, scale));
            await UniTask.Yield();
        }
        return (min, max);
    }

    // 右ドラッグで 30° ずつ振り向き、ピンが画面内（外）になるまで回す
    // Turn 30 degrees per right drag until the pin is on (off) screen
    async UniTask TurnUntilPin(bool onScreen)
    {
        for (var turn = 0; turn < 14 && WorldPinStateStore.Instance.GetCurrent().Pins[0].OnScreen != onScreen; turn++) await Turn(1);
        p.Assert(WorldPinStateStore.Instance.GetCurrent().Pins[0].OnScreen == onScreen, onScreen ? "振り向いてピンが画面内" : "振り向いてピンが画面外");
    }

    async UniTask Turn(int steps)
    {
        var controller = UnityEngine.Object.FindObjectOfType<InGameCameraController>(true);
        var sensitivity = (Vector2)typeof(InGameCameraController).GetField("sensitivity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        for (var step = 0; step < steps; step++)
        {
            await p.RightDrag(new Vector2(30f / sensitivity.x, 0f));
            await UniTask.DelayFrame(40);
        }
    }
    #endregion
});

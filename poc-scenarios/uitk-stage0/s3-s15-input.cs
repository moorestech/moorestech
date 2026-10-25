using System.Collections.Generic;
using Client.Game.InGame.Control;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.WebUi;
using Client.UitkStage0Probe;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// S3（注入入力がUITKへ届くか・座標の往復・EventSystemの判定）とS15（委譲中のWebのpointer-events）
// S3 (injected input reaching UITK, coordinate round trip, EventSystem checks) and S15 (pointer-events on the delegated Web)
// de-risk: Editorが非前面だと注入先のデバイスが無効になる（実測で mouse=(0,0)・lock解除されず）。実行中だけフォーカスを無視させ、最後に戻す
// de-risk: an unfocused Editor disables the injected devices (observed mouse=(0,0), lock never released); ignore focus only during the run and restore afterwards
var inputSettings = InputSystem.settings;
var previousBackground = inputSettings.backgroundBehavior;
var previousEditorBehavior = inputSettings.editorInputBehaviorInPlayMode;
inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
InputSystem.EnableDevice(Mouse.current);
InputSystem.EnableDevice(Keyboard.current);
Debug.Log($"[S3S15] input focus override bg={previousBackground}->{inputSettings.backgroundBehavior} focused={Application.isFocused} mouse={Mouse.current.enabled} kb={Keyboard.current.enabled}");
PlaytestDomQuery.RegisterHandler();
CefInputForwarder.StartForwarding();
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-s3-s15", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // 開幕スキットは非前面のEditorだと既定の待ちで抜け切らないことがあるので、飛ばした後に長めに待つ
    // The opening skit can outlast the default wait on a throttled Editor, so skip it and then wait longer
    await p.SkipOpeningSkitIfPlaying();
    await Client.Playtest.Operations.Ui.PlaytestUiOps.WaitUiState(Client.Game.InGame.UI.UIState.UIStateEnum.GameScreen, 120f);
    var probe = UitkLayerProbe.Spawn(10, -10);
    await p.WaitSeconds(1.0f);

    // action を数える（Webの適用報告は除く）
    // Count actions, excluding the Web's applied report
    // ログはWS受信スレッドから出るので Threaded 版で受け、lockで数える
    // The log comes from the WS receive thread, so subscribe to the threaded variant and count under a lock
    var actions = new List<string>();
    Application.logMessageReceivedThreaded += (message, stack, type) =>
    {
        if (!message.StartsWith("[PocActionLog]") || message.Contains("poc.pointer_events_ack") || message.Contains("dom_query")) return;
        lock (actions) actions.Add(message);
    };
    int ActionCount() { lock (actions) return actions.Count; }
    string ActionsSince(int from) { lock (actions) return string.Join(", ", actions.GetRange(from, actions.Count - from)); }

    string Flags(string where)
    {
        var eventSystem = EventSystem.current;
        var line = $"{where}: lock={Cursor.lockState} IsPointerOverGameObject={eventSystem != null && eventSystem.IsPointerOverGameObject()} UiPointerHitTest={UiPointerHitTest.IsPointerOverAnyUi()} webPointerOverUi={WebUiInputExclusivity.IsPointerOverWebUi} mouse={SemanticInput.CurrentMousePosition()} F{Time.frameCount}";
        p.Note(line);
        Debug.Log($"[S3S15] {line}");
        return line;
    }

    async UniTask ClickProbe(string label)
    {
        var target = probe.ProbeButtonScreenCenter();
        var expectedPanel = probe.ProbeButton.worldBound.center;
        var roundTrip = ProbeCoordinates.ScreenToPanel(probe.FrontPanel, target);
        await SemanticInput.MouseGlideTo(target, 0.4f);
        await UniTask.DelayFrame(5);
        Flags($"{label} hover button");
        var downsBefore = probe.PointerDownCount;
        var clicksBefore = probe.ClickCount;
        await SemanticInput.Click();
        await UniTask.DelayFrame(5);
        var delivered = probe.PointerDownCount > downsBefore;
        p.Assert(delivered, $"{label}: pointerdown reached UITK button");
        p.Assert(probe.ClickCount > clicksBefore, $"{label}: click reached UITK button");
        var line = $"{label}: screen={target} expectedPanel={expectedPanel} screenToPanel={roundTrip} eventPanel={(delivered ? probe.LastPointerDownPanelPosition.ToString() : "none")} downs={probe.PointerDownCount} clicks={probe.ClickCount}";
        p.Note(line);
        Debug.Log($"[S3S15] {line}");
    }

    // S3-A: 通常のゲーム画面（カーソルロック中）
    // S3-A: regular game screen (cursor locked)
    await ClickProbe("A locked");
    await p.Screenshot("s3_a_locked");

    // S3-B: 左Altでカーソル解放中
    // S3-B: cursor freed by holding left Alt
    SemanticInput.KeyDown(Key.LeftAlt);
    await UniTask.DelayFrame(10);
    await ClickProbe("B alt-free");
    await p.Screenshot("s3_b_altfree");
    await SemanticInput.MouseGlideTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.55f), 0.3f);
    await UniTask.DelayFrame(10);
    Flags("B empty world");
    await SemanticInput.MouseGlideTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.05f), 0.3f);
    await UniTask.DelayFrame(10);
    Flags("B over UITK front plate (picking ignore)");

    // S15: Webのホットバー9枠目で、pointer-eventsの切替前後を比べる
    // S15: compare before/after toggling pointer-events over the Web hotbar slot 9
    var slot = await PlaytestDomQuery.Query("hotbar-slot-8", 5f);
    p.Assert(PlaytestDomQuery.TryGetScreenCenter(slot, out var slotCenter), "hotbar-slot-8 resolved");
    async UniTask WebPhase(string mode)
    {
        if (mode != "baseline")
        {
            PocPointerEventsDriver.Request(mode);
            await p.Until(() => PocPointerEventsDriver.LastAck.Contains(mode), 5f, $"ack {mode}");
            await UniTask.DelayFrame(10);
            Flags($"S15 {mode} after toggle, pointer not moved");
        }
        await SemanticInput.MouseGlideTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.55f), 0.3f);
        await UniTask.DelayFrame(15);
        Flags($"S15 {mode} moved to empty world");
        await SemanticInput.MouseGlideTo(slotCenter + new Vector2(6, 0), 0.3f);
        await SemanticInput.MouseGlideTo(slotCenter, 0.2f);
        await UniTask.DelayFrame(10);
        Flags($"S15 {mode} hover slot9");
        var before = ActionCount();
        await SemanticInput.Click();
        await p.WaitSeconds(0.8f);
        var line = $"S15 {mode}: actions on click={ActionCount() - before} [{ActionsSince(before)}] ack={PocPointerEventsDriver.LastAck}";
        p.Note(line);
        Debug.Log($"[S3S15] {line}");
        await p.Screenshot($"s15_{mode}");
    }
    await WebPhase("baseline");
    await WebPhase("root-none");
    await WebPhase("restore");
    await WebPhase("all-none-important");
    await WebPhase("restore");
    SemanticInput.KeyUp(Key.LeftAlt);
    await p.WaitSeconds(0.5f);
    inputSettings.backgroundBehavior = previousBackground;
    inputSettings.editorInputBehaviorInPlayMode = previousEditorBehavior;
});

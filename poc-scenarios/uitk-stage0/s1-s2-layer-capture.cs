using System.IO;
using Client.Playtest;
using Client.UitkStage0Probe;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

// S1（前後）とS2(1)(2)(3)（録画・スクショ・バグ報告の読み出し）を1本で撮る
// One run capturing S1 (ordering) and S2 paths (1)(2)(3) (recorder, screenshot, bug-report readback)
// 初回はEditor非前面で入力デバイスが無効になりTabが届かなかった。実行中だけフォーカスを無視させ、最後に戻す
// The first run lost Tab because an unfocused Editor disabled the devices; ignore focus only during the run and restore afterwards
var inputSettings = InputSystem.settings;
var previousBackground = inputSettings.backgroundBehavior;
var previousEditorBehavior = inputSettings.editorInputBehaviorInPlayMode;
var devicesBefore = $"focused={Application.isFocused} kb={Keyboard.current.enabled} mouse={Mouse.current.enabled}";
inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
InputSystem.EnableDevice(Mouse.current);
InputSystem.EnableDevice(Keyboard.current);
Debug.Log($"[S1S2] devices before override: {devicesBefore} after: kb={Keyboard.current.enabled}");
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-s1-s2", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // 開幕スキットは非前面のEditorだと既定の待ちで抜け切らないことがあるので、飛ばした後に長めに待つ
    // The opening skit can outlast the default wait on a throttled Editor, so skip it and then wait longer
    await p.SkipOpeningSkitIfPlaying();
    await Client.Playtest.Operations.Ui.PlaytestUiOps.WaitUiState(Client.Game.InGame.UI.UIState.UIStateEnum.GameScreen, 120f);
    string UiState() => Object.FindFirstObjectByType<Client.Game.InGame.UI.UIState.UIStateControl>().CurrentState.ToString();

    // 対照: プローブ無しでTabがインベントリを開くか
    // Control: does Tab open the inventory without any probe panel
    p.Note($"control without probe: before Tab state={UiState()}");
    await p.PressKey(Key.Tab);
    await p.WaitSeconds(1.5f);
    p.Note($"control without probe: after Tab state={UiState()}");
    await p.Screenshot("00_control_noprobe_tab");
    await p.PressKey(Key.Tab);
    await p.WaitSeconds(1.0f);
    p.Note($"control without probe: after 2nd Tab state={UiState()}");

    var probe = UitkLayerProbe.Spawn(10, -10);
    var capture = ProbeCapture.Ensure();
    await p.WaitSeconds(1.5f);
    var directory = Path.GetDirectoryName(await p.Screenshot("00_warmup"));

    async UniTask CaptureSet(string label, int front, int back)
    {
        probe.SetSortingOrders(front, back);
        await UniTask.DelayFrame(5);
        p.Note($"{label} front={front} back={back} state={UiState()} F{Time.frameCount}");
        await p.WaitSeconds(1.0f);
        var target = capture.CompletedFrameReaderCaptures + 1;
        capture.RequestFrameReaderCapture(Path.Combine(directory, $"{label}_framereader_F{Time.frameCount}.png"));
        await p.Until(() => target <= capture.CompletedFrameReaderCaptures, 5f, $"framereader {label}");
        await p.Screenshot($"{label}_screencapture_F{Time.frameCount}");
        await p.WaitSeconds(1.0f);
    }

    await CaptureSet("a_game", 10, -10);
    await p.PressKey(Key.Tab);
    await p.WaitSeconds(1.5f);
    p.Note($"with probe: after Tab state={UiState()}");
    await CaptureSet("b_inv", 10, -10);
    await CaptureSet("c_inv", -1, -10);
    await CaptureSet("d_inv", 0, -10);
    await CaptureSet("e_inv", 10, 1);
    await CaptureSet("f_inv", 1, -1);
    await p.PressKey(Key.Tab);
    await p.WaitSeconds(1.0f);
    await CaptureSet("g_game", 10, -10);
    inputSettings.backgroundBehavior = previousBackground;
    inputSettings.editorInputBehaviorInPlayMode = previousEditorBehavior;
});

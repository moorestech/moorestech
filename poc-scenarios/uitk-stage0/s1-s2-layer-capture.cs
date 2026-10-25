using System.IO;
using Client.Playtest;
using Client.UitkStage0Probe;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

// S1（前後）とS2(1)(2)(3)（録画・スクショ・バグ報告の読み出し）を1本で撮る
// One run capturing S1 (ordering) and S2 paths (1)(2)(3) (recorder, screenshot, bug-report readback)
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-s1-s2", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // 開幕スキットは非前面のEditorだと既定の待ちで抜け切らないことがあるので、飛ばした後に長めに待つ
    // The opening skit can outlast the default wait on a throttled Editor, so skip it and then wait longer
    await p.SkipOpeningSkitIfPlaying();
    await Client.Playtest.Operations.Ui.PlaytestUiOps.WaitUiState(Client.Game.InGame.UI.UIState.UIStateEnum.GameScreen, 120f);
    var probe = UitkLayerProbe.Spawn(10, -10);
    var capture = ProbeCapture.Ensure();
    await p.WaitSeconds(1.5f);
    var directory = Path.GetDirectoryName(await p.Screenshot("00_warmup"));

    async UniTask CaptureSet(string label, int front, int back)
    {
        probe.SetSortingOrders(front, back);
        await UniTask.DelayFrame(5);
        p.Note($"{label} front={front} back={back} F{Time.frameCount}");
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
    await CaptureSet("b_inv", 10, -10);
    await CaptureSet("c_inv", -1, -10);
    await CaptureSet("d_inv", 0, -10);
    await CaptureSet("e_inv", 10, 1);
    await CaptureSet("f_inv", 1, -1);
    await p.PressKey(Key.Tab);
    await p.WaitSeconds(1.0f);
    await CaptureSet("g_game", 10, -10);
});

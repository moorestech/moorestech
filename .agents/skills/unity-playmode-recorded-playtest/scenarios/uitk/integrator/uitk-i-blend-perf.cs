// 半透明の合成方式のフレーム時間とピッキング（I-blend）: sRGB 合成（新）と Linear 合成（旧）を交互に 3 回ずつ、1280×720 と 2560×1080 で測る
// 実行前に PlayMode を止め、UiSystemEditorSwitch.Set(UiSystem.GameUi) を立て、Editor を前面に出してから run-scenario.sh を呼ぶ
// Frame time and picking of the blend methods (I-blend): sRGB blending (new) and Linear blending (old) alternated three times each at 1280x720 and 2560x1080
// Stop PlayMode, set UiSystemEditorSwitch.Set(UiSystem.GameUi) and focus the Editor before run-scenario.sh
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("uitk-i-blend-perf", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await UniTask.DelayFrame(30);

    var document = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).First(d => d.panelSettings != null && d.panelSettings.name.StartsWith("GameUiPanelSettings"));
    var srgbSettings = document.panelSettings;
    var sourceType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Client.Ui.Foundation.Composite.UiSrgbCompositeSource")).First(t => t != null);
    var sourceField = sourceType.GetField("_texture", BindingFlags.Static | BindingFlags.NonPublic);
    var linearSettings = Object.Instantiate(srgbSettings);
    linearSettings.name = srgbSettings.name;
    linearSettings.forceGammaRendering = false;
    linearSettings.targetTexture = null;
    linearSettings.clearColor = false;

    foreach (var (width, height) in new[] { (1280, 720), (2560, 1080) })
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await UniTask.DelayFrame(30);
        var srgbTexture = (RenderTexture)sourceField.GetValue(null);
        p.Note($"{width}x{height} panel RT {srgbTexture.width}x{srgbTexture.height} memory={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(srgbTexture) / 1024} KiB");
        await CheckPicking(width, height);
        var results = new Dictionary<string, List<double>> { ["new-cpu"] = new(), ["new-gpu"] = new(), ["old-cpu"] = new(), ["old-gpu"] = new() };
        for (var round = 0; round < 3; round++)
        {
            await Sample("new", 300, results);
            document.panelSettings = linearSettings;
            sourceField.SetValue(null, null);
            await UniTask.DelayFrame(30);
            await Sample("old", 300, results);
            document.panelSettings = srgbSettings;
            sourceField.SetValue(null, srgbTexture);
            await UniTask.DelayFrame(30);
        }
        p.Note($"{width}x{height} new cpu {Median(results["new-cpu"]):F2}ms gpu {Median(results["new-gpu"]):F2}ms / old cpu {Median(results["old-cpu"]):F2}ms gpu {Median(results["old-gpu"]):F2}ms (median of {results["new-cpu"].Count} frames)");
    }
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    // 画面の座標から UITK の要素を拾えるか（RT に描くパネルでも画面と同じ座標で拾う）
    // Whether a screen position picks the UITK element (an RT panel must pick at the same screen coordinates)
    async UniTask CheckPicking(int width, int height)
    {
        var marker = new VisualElement { name = "i-blend-pick-marker" };
        marker.style.position = Position.Absolute;
        marker.style.left = 600; marker.style.top = 300; marker.style.width = 40; marker.style.height = 40;
        marker.style.backgroundColor = new Color(1, 0, 1, 0.5f);
        document.rootVisualElement.Add(marker);
        await UniTask.DelayFrame(3);
        var scale = document.rootVisualElement.panel.scaledPixelsPerPoint;
        var topLeftScreen = new Vector2((600 + 20) * scale, (300 + 20) * scale);
        var panelPosition = RuntimePanelUtils.ScreenToPanel(document.rootVisualElement.panel, topLeftScreen);
        var picked = document.rootVisualElement.panel.Pick(panelPosition);
        p.Assert(picked == marker, $"{width}x{height} 画面 ({topLeftScreen.x},{topLeftScreen.y}) で目印を拾う (panel {panelPosition}, picked {picked?.name})");
        marker.RemoveFromHierarchy();
    }

    async UniTask Sample(string mode, int frames, Dictionary<string, List<double>> results)
    {
        var timings = new FrameTiming[1];
        for (var f = 0; f < frames; f++)
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) == 1)
            {
                results[$"{mode}-cpu"].Add(timings[0].cpuFrameTime);
                results[$"{mode}-gpu"].Add(timings[0].gpuFrameTime);
            }
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
    }

    double Median(List<double> values) => values.Count == 0 ? -1 : values.OrderBy(v => v).ElementAt(values.Count / 2);

    #endregion
});

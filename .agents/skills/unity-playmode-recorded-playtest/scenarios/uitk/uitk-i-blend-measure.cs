// 半透明の合成方式の実測（I-blend）: 世界の上と UI の面の上に Web の半透明色の見本を置き、sRGB 合成（新）と Linear 合成＋補正 α（旧）で撮る
// 各方式で見本を隠した画（地）と見せた画を撮り、Web の式（sRGB の over）との差は撮った PNG から外で計算する。フレーム時間と RT のメモリも記録する
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立て、Editor を前面に出してから run-scenario.sh を呼ぶ
// Measures the translucency blend methods (I-blend): Web translucent swatches over the world and over UI faces, captured with sRGB blending (new) and Linear blending with corrected alpha (old)
// Each method captures the ground (swatches hidden) and the swatches shown; the error against the Web formula (sRGB over) is computed offline from the PNGs, and frame times and RT memory are logged
// Stop PlayMode, set UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code and focus the Editor before run-scenario.sh
using System.Linq;
using System.Reflection;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("uitk-i-blend-measure", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await UniTask.DelayFrame(30);

    // 見本の色（Web の値）と旧方式の補正 α（世界の上・面の上）。5 番は opacity で薄める絵（液体のゴースト）
    // Swatch colours (Web values) and the old corrected alpha (over the world, over a face); #5 is a picture faded by opacity (fluid ghost)
    var colors = new[] { new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255), new Color32(255, 0, 0, 255), new Color32(255, 255, 255, 255), new Color32(10, 14, 27, 255), new Color32(200, 160, 60, 255) };
    var webAlpha = new[] { 0.6f, 0.42f, 0.24f, 0.22f, 0.8f, 0.35f };
    var oldAlphaWorld = new[] { 0.8605f, 0.69f, 0.4457f, 0.0607f, 0.9491f, 0.16f };
    var oldAlphaFace = new[] { 0.8605f, 0.69f, 0.0713f, 0.0607f, 0.9491f, 0.16f };
    var faceGrounds = new byte[] { 16, 30, 53, 90, 128, 180, 243 };
    var worldRows = new[] { 100, 205, 400, 600 };

    var document = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).First(d => d.panelSettings != null && d.panelSettings.name.StartsWith("GameUiPanelSettings"));
    var srgbSettings = document.panelSettings;
    var sourceType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Client.Ui.Foundation.Composite.UiSrgbCompositeSource")).First(t => t != null);
    var sourceField = sourceType.GetField("_texture", BindingFlags.Static | BindingFlags.NonPublic);
    var srgbTexture = (RenderTexture)sourceField.GetValue(null);
    p.Note($"panel RT {srgbTexture.width}x{srgbTexture.height} {srgbTexture.graphicsFormat} depth={srgbTexture.depthStencilFormat} memory={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(srgbTexture) / 1024} KiB");

    // 新方式: sRGB 合成と Web の α
    // New method: sRGB blending with the Web alpha
    await MeasureMode("new", webAlpha, webAlpha);

    // 旧方式: 本番の複製から sRGB の値のまま描く設定と RT を外し、合成の差し込みを止める
    // Old method: a clone without sRGB-value rendering and RT, with the composite hook stopped
    var linearSettings = Object.Instantiate(srgbSettings);
    linearSettings.name = srgbSettings.name;
    linearSettings.forceGammaRendering = false;
    linearSettings.targetTexture = null;
    linearSettings.clearColor = false;
    sourceField.SetValue(null, null);
    document.panelSettings = linearSettings;
    await UniTask.DelayFrame(30);
    await MeasureMode("old", oldAlphaWorld, oldAlphaFace);

    // 方式 (b): RT を使わず画面へ直接描くパネルで sRGB の値のまま描く設定だけを立てる
    // Method (b): only the sRGB-value rendering flag on a panel drawing straight to the screen without an RT
    linearSettings.forceGammaRendering = true;
    await UniTask.DelayFrame(30);
    await MeasureMode("flagOnly", webAlpha, webAlpha);

    // 新方式へ戻す
    // Restore the new method
    document.panelSettings = srgbSettings;
    sourceField.SetValue(null, srgbTexture);
    await UniTask.DelayFrame(10);

    #region Internal

    async UniTask MeasureMode(string mode, float[] alphaWorld, float[] alphaFace)
    {
        var probe = BuildProbe(alphaWorld, alphaFace);
        document.rootVisualElement.Add(probe);

        // 見本なしのフレーム時間（GPU/CPU の中央値と平均）
        // Frame time without swatches (GPU/CPU median and mean)
        probe.style.visibility = Visibility.Hidden;
        await UniTask.DelayFrame(30);
        await SampleFrameTimes(mode, 240);
        await p.Screenshot($"{mode}-ground");
        probe.style.visibility = Visibility.Visible;
        await UniTask.DelayFrame(5);
        await p.Screenshot($"{mode}-swatches");
        probe.RemoveFromHierarchy();

        // UI を全部隠した世界だけの画（3D の見た目が方式で変わらないかを見る）
        // World-only frame with all UI hidden (checks the 3D look does not change between methods)
        var root = document.rootVisualElement;
        root.style.visibility = Visibility.Hidden;
        await UniTask.DelayFrame(5);
        await p.Screenshot($"{mode}-world-only");
        root.style.visibility = StyleKeyword.Null;
        await UniTask.DelayFrame(5);
    }

    VisualElement BuildProbe(float[] alphaWorld, float[] alphaFace)
    {
        var probe = new VisualElement { pickingMode = PickingMode.Ignore };
        probe.style.position = Position.Absolute;
        probe.style.left = 0; probe.style.top = 0; probe.style.right = 0; probe.style.bottom = 0;
        // 世界の上: 右側 4 行 × 6 色（60×30）
        // Over the world: 4 rows x 6 colours on the right (60x30)
        foreach (var top in worldRows)
            for (var i = 0; i < colors.Length; i++) probe.Add(Swatch(710 + i * 95, top, i, alphaWorld[i]));
        // 面の上: 左下に灰の地 7 段 × 6 色
        // Over a face: 7 grey grounds x 6 colours at the bottom left
        for (var row = 0; row < faceGrounds.Length; row++)
        {
            var g = faceGrounds[row];
            var ground = Box(20, 320 + row * 50, 540, 50, new Color32(g, g, g, 255));
            probe.Add(ground);
            for (var i = 0; i < colors.Length; i++) probe.Add(Swatch(30 + i * 88, 330 + row * 50, i, alphaFace[i]));
        }
        return probe;
    }

    VisualElement Swatch(int left, int top, int index, float alpha)
    {
        // 5 番は不透明の絵を opacity で薄める（ゴーストと同じ掛け方）
        // #5 fades an opaque picture with opacity (as the ghost does)
        if (index == 5)
        {
            var picture = Box(left, top, 60, 30, colors[index]);
            picture.style.opacity = alpha;
            return picture;
        }
        var c = (Color)colors[index];
        return Box(left, top, 60, 30, new Color(c.r, c.g, c.b, alpha));
    }

    VisualElement Box(int left, int top, int width, int height, Color color)
    {
        var box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.style.position = Position.Absolute;
        box.style.left = left; box.style.top = top; box.style.width = width; box.style.height = height;
        box.style.backgroundColor = color;
        return box;
    }

    async UniTask SampleFrameTimes(string mode, int frames)
    {
        var cpu = new System.Collections.Generic.List<double>();
        var gpu = new System.Collections.Generic.List<double>();
        var timings = new FrameTiming[1];
        for (var f = 0; f < frames; f++)
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) == 1)
            {
                cpu.Add(timings[0].cpuFrameTime);
                gpu.Add(timings[0].gpuFrameTime);
            }
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
        p.Note($"[{mode}] frames={cpu.Count} cpu median={Median(cpu):F2}ms mean={Mean(cpu):F2}ms gpu median={Median(gpu):F2}ms mean={Mean(gpu):F2}ms fps={1f / Time.smoothDeltaTime:F1}");
    }

    double Median(System.Collections.Generic.List<double> values) => values.Count == 0 ? -1 : values.OrderBy(v => v).ElementAt(values.Count / 2);
    double Mean(System.Collections.Generic.List<double> values) => values.Count == 0 ? -1 : values.Average();

    #endregion
});

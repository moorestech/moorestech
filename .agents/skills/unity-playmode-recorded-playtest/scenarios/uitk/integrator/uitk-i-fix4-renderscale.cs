// 描画解像度 50% で、UI の半透明の帯の下の世界の色が URP の拡大と同じか（I-fix4）
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ。判定は撮った PNG から外で、帯の各画素を Web の式（sRGB の over: α×色＋(1−α)×地）と比べる。左半分は操作オーバーレイのログが動くので x≥660 だけを見て、地を 2 回撮って動いた画素を除く
// At 50% render scale, whether the world colour under translucent UI strips matches URP's upscale (I-fix4)
// Stop PlayMode and set UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh; judge offline by comparing each strip pixel with the Web formula (sRGB over: a*colour + (1-a)*ground), using only x>=660 because the overlay log moves on the left, and dropping pixels that changed between the two ground shots
using System.Linq;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("uitk-i-fix4-renderscale", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await UniTask.DelayFrame(30);
    var document = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).First(d => d.panelSettings != null && d.panelSettings.name.StartsWith("GameUiPanelSettings"));
    var asset = UniversalRenderPipeline.asset;
    var originalScale = asset.renderScale;
    p.Note($"asset={asset.name} upscalingFilter={asset.upscalingFilter} screen={Screen.width}x{Screen.height}");
    foreach (var scale in new[] { 1f, 0.5f })
    {
        asset.renderScale = scale;
        // 帯: 黒 0.42 と白 0.22 を画面幅いっぱい、地平線の高さの前後に
        // Strips: black 0.42 and white 0.22 across the full width around the horizon
        var probe = new VisualElement { pickingMode = PickingMode.Ignore };
        probe.style.position = Position.Absolute; probe.style.left = 0; probe.style.top = 0; probe.style.right = 0; probe.style.bottom = 0;
        for (var i = 0; i < 6; i++) probe.Add(Strip(140 + i * 30, i % 2 == 0 ? new Color(0, 0, 0, 0.42f) : new Color(1, 1, 1, 0.22f)));
        document.rootVisualElement.Add(probe);
        // 帯と地は同じフレームの世界でないと比べられないので、時を止めて撮る
        // Freeze time so the strip and ground frames see the same world
        Time.timeScale = 0f;
        probe.style.visibility = Visibility.Hidden;
        await UniTask.DelayFrame(20);
        await p.Screenshot($"s{scale:F1}-ground");
        probe.style.visibility = Visibility.Visible;
        await UniTask.DelayFrame(5);
        await p.Screenshot($"s{scale:F1}-strips");
        probe.style.visibility = Visibility.Hidden;
        await UniTask.DelayFrame(5);
        await p.Screenshot($"s{scale:F1}-ground2");
        Time.timeScale = 1f;
        probe.RemoveFromHierarchy();
    }
    asset.renderScale = originalScale;

    VisualElement Strip(int top, Color color)
    {
        var box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.style.position = Position.Absolute; box.style.left = 0; box.style.right = 0; box.style.top = top; box.style.height = 30;
        box.style.backgroundColor = color;
        return box;
    }
});

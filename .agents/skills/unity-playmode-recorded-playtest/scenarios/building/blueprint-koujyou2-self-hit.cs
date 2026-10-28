using Game.Context;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Client.Playtest;
using Client.Playtest.Core;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Blueprint;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

return PlaytestRunner.Run("blueprint-koujyou2-self-hit", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 1200f }, async p =>
{
    var input = Environment.GetEnvironmentVariable("BP_FLICKER_INPUT");
    var mode = Environment.GetEnvironmentVariable("BP_FLICKER_MODE");
    var fixedMode = Environment.GetEnvironmentVariable("BP_FLICKER_EXPECT_FIXED");
    if (string.IsNullOrWhiteSpace(input) || (mode != "original" && mode != "controlled"))
        throw new InvalidOperationException("BP_FLICKER_INPUT and BP_FLICKER_MODE=original|controlled are required");
    if (fixedMode != "0" && fixedMode != "1")
        throw new InvalidOperationException("BP_FLICKER_EXPECT_FIXED=0|1 is required");
    var expectFixed = fixedMode == "1";
    var bp = JsonConvert.DeserializeObject<BlueprintJsonObject>(File.ReadAllText(Path.Combine(input, "koujyou2.json")));
    if (bp == null || bp.Blocks.Count != 675 || bp.Blocks.Select(b => b.BlockGuidStr).Distinct().Count() != 13 || bp.Wires.Count != 38)
        throw new InvalidOperationException("Unexpected koujyou2 fixture: expected 675 blocks, 13 kinds, 38 wires");

    // 描画観測は実行ディレクトリの固定ファイルへ保存する。
    // Save rendering observations in a stable file under this run.
    var runDir = Path.Combine(PlaytestPaths.SessionDirectory, "blueprint-koujyou2-self-hit");
    using var trace = new StreamWriter(Path.Combine(runDir, "preview-frames.tsv"), false);
    trace.WriteLine("mode\tphase\tframe\tghosts\twires\tanchor\tplayer\tcameraPosition\tcameraEuler");
    var settings = InputSystem.settings;
    settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    if (mode == "controlled") await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.Hotbar.UnlockBlueprint();

    // 原本BPを通常のライブラリとビルドメニューに登録する。
    // Register the original blueprint through the normal library and build menu.
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    await p.RunOnServer("原本BPを照合して登録する", () =>
    {
        var datastore = ServerContext.GetService<IBlueprintDatastore>();
        var matches = datastore.Blueprints.Where(value => value.BlueprintGuid == bp.BlueprintGuid).ToArray();
        if (matches.Length > 1) throw new InvalidOperationException("Saved blueprint has duplicate original GUID entries");
        var existing = matches.SingleOrDefault();
        if (existing == null) datastore.Register(bp);
        else if (!JToken.DeepEquals(JToken.FromObject(existing), JToken.FromObject(bp)))
            throw new InvalidOperationException("Saved blueprint has the original GUID but different payload");
    });
    await resolver.Resolve<ClientBlueprintLibrary>().Refresh(Camera.main.GetCancellationTokenOnDestroy());
    var tooltip = resolver.Resolve<MouseCursorTooltipState>();
    var checks = new BlueprintCopyPasteScenarioChecks(p, tooltip);
    var probe = new BlueprintPasteGhostProbe(tooltip);
    if (mode == "original")
    {
        // 原本ログでは報告tickまでフライ状態が続いている。
        // The original log shows flight remained enabled through the report tick.
        UnityEngine.Object.FindObjectOfType<ThirdPersonController>(true).SetFlying(true);
        p.WarpPlayer(new Vector3(623.2227f, 116.6802f, 635.012146f));
        var view = resolver.Resolve<PlayerViewModeController>();
        if (view.GetCurrentMode() != PlayerViewMode.FirstPerson) await p.PressKey(Key.V);
        await UniTask.DelayFrame(40);
        await Record("original-camera-before-input", 1, false);
        // 実際の感度と入力後の角度から誤差を再計算する。
        // Recompute input from the actual sensitivity and reached angles.
        var controller = UnityEngine.Object.FindObjectOfType<InGameCameraController>(true);
        var field = typeof(InGameCameraController).GetField("sensitivity", BindingFlags.Instance | BindingFlags.NonPublic);
        var sensitivity = (Vector2)field.GetValue(controller);
        if (sensitivity.x <= 0f || sensitivity.y <= 0f) throw new InvalidOperationException("Invalid camera sensitivity");
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var angles = Camera.main.transform.eulerAngles;
            var yawError = Mathf.DeltaAngle(angles.y, 113.473808f);
            var pitchError = Mathf.DeltaAngle(angles.x, 62.2692833f);
            if (Mathf.Abs(yawError) < 0.5f && Mathf.Abs(pitchError) < 0.5f) break;
            var pixels = new Vector2(Mathf.Clamp(2f * yawError / sensitivity.x, -180f, 180f),
                Mathf.Clamp(-2f * pitchError / sensitivity.y, -180f, 180f));
            await p.RightDrag(pixels);
            await UniTask.DelayFrame(50);
            await Record($"camera-input-{attempt}", 1, false);
        }
        await Record("original-camera-after-input", 1, false);
        var camera = Camera.main.transform;
        var targetPosition = new Vector3(623.1587f, 118.412971f, 635.0399f);
        var reached = camera.eulerAngles;
        var angleError = Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(reached.x, 62.2692833f)),
            Mathf.Abs(Mathf.DeltaAngle(reached.y, 113.473808f)));
        var positionError = Vector3.Distance(camera.position, targetPosition);
        trace.WriteLine($"camera-difference\tangleDeg={angleError:F3}\tpositionM={positionError:F3}");
        p.Assert(angleError < 1f && positionError < 0.5f,
            $"original camera difference: angle={angleError:F3}deg position={positionError:F3}m");
    }
    else p.WarpPlayer(new Vector3(-12f, 36f, -8f));

    await checks.SelectEntry("build-menu-category-d1000000-0000-4000-8000-000000000010",
        $"build-menu-entry-blueprint-{bp.BlueprintGuid:D}");
    var aim = mode == "original"
        ? Camera.main.transform.position + Camera.main.transform.forward * 15f
        : new Vector3(-11.5f, 32f, 8.5f);
    await p.AimAt(aim);
    await Record("initial", 120, true);
    await p.Screenshot($"{mode}-initial");
    await p.PressKey(Key.R);
    await Record("rotated", 120, true);
    await p.PressKey(Key.E);
    await Record("height-up", 120, true);
    await p.PressKey(Key.Q);
    await Record("height-restored", 120, true);
    await p.ExitToGameScreen();
    await checks.SelectEntry("build-menu-category-d1000000-0000-4000-8000-000000000010",
        $"build-menu-entry-blueprint-{bp.BlueprintGuid:D}");
    await p.AimAt(aim);
    await Record("pool-reused", 120, true);
    await p.Screenshot($"{mode}-reused");

    async UniTask Record(string phase, int frames, bool verifyGhosts)
    {
        var missing = 0;
        for (var frame = 0; frame < frames; frame++)
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            var ghosts = probe.ActiveGhosts();
            var wires = probe.ActiveWireLineCount();
            if (ghosts.Count != 675) missing++;
            var anchor = ghosts.Count == 0 ? "hidden" : BlueprintPasteGhostProbe.MinPosition(ghosts).ToString("F3");
            var camera = Camera.main.transform;
            trace.WriteLine($"{mode}\t{phase}\t{frame}\t{ghosts.Count}\t{wires}\t{anchor}\t{p.PlayerPosition.ToString("F3")}\t{camera.position.ToString("F3")}\t{camera.eulerAngles.ToString("F3")}");
        }
        trace.Flush();
        p.Note($"{mode}/{phase}: missing={missing}/{frames}; trace=preview-frames.tsv");
        // 修正前は欠落を証拠として残し、修正後ランだけ成功条件を適用する。
        // Preserve baseline missing frames as evidence; apply the pass condition on fixed runs.
        if (verifyGhosts && expectFixed)
            p.Assert(missing == 0, $"{mode}/{phase}: all 675 ghosts visible for {frames} frames");
    }
});

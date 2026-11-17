// 段階4のCPU時間: GameScreen・PlayerInventory・ResearchTree を各240フレーム、UIElements のパネル更新と描画の和を測る（録画しない）
// Stage-4 CPU time: sum UIElements panel update and repaint over 240 frames each on GameScreen, PlayerInventory and ResearchTree (no recording)
// winunity play <branch> <このファイル> で流す（GUI Editor の PlayMode で測る）
// Run with winunity play <branch> <this file> (measured in the GUI Editor's PlayMode)
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("uitk-stage4-panel-cost", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    // 裏タブの Game View は描画されず timeScale=0 のままスキットの Skip 送りが止まるので表へ出す（前例 uitk-opening-hud）
    // A Game View behind a tab never renders and skit skip stalls at timeScale=0, so bring it to front (precedent uitk-opening-hud)
    PlaytestGameViewOps.BringToFront();
    await p.Until(() => 0f < Time.timeScale, 10f, "Game View の描画が始まり timeScale が戻る");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // Game View を表へ出し、Story のままなら飛ばし直して Story を抜ける（回数は Driver が Note に残す）
    // Bring the Game View front and re-skip while in Story until it leaves (the Driver notes the rounds)
    await p.SkipOpeningSkitUntilLeftStory();
    await p.ExitToGameScreen();
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1920, 1080);

    // マーカーが見つからなければ名前の一覧を残して止める
    // Leave the marker name list and stop when no marker is found
    var markers = PlaytestPanelCostRecorder.FindPanelMarkers();
    if (markers.Count == 0) p.Note($"UIElements のマーカー一覧: {string.Join(", ", PlaytestPanelCostRecorder.ListUiElementsMarkerNames())}");
    p.Assert(0 < markers.Count, $"パネル更新と描画のマーカーが見つかる（{markers.Count} 本）");
    if (markers.Count == 0)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
        return;
    }

    // 3 画面で測り、Note に 1 行ずつ出す
    // Measure on three screens and note one line each
    await MeasureCurrent(UIStateEnum.GameScreen);
    await MeasureOpened(UIStateEnum.PlayerInventory, Key.Tab);
    await MeasureOpened(UIStateEnum.ResearchTree, Key.R);

    // Game View の解像度は PlayMode の後も残るので 1280x720 へ戻す
    // The Game View resolution outlives PlayMode, so restore 1280x720
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    async UniTask MeasureOpened(UIStateEnum state, Key openKey)
    {
        // キーで開いて測り、Esc でゲーム画面へ戻る
        // Open with the key, measure, then return to the game screen with Esc
        await p.PressKey(openKey);
        await p.WaitUiState(state, 10f);
        await MeasureCurrent(state);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    async UniTask MeasureCurrent(UIStateEnum state)
    {
        await p.WaitSeconds(1f);
        var cost = await PlaytestPanelCostRecorder.Measure(markers, 240);
        p.Note($"panel-cost {state} {Screen.width}x{Screen.height} frames={cost.Frames} mean={cost.MeanMs:F3}ms p95={cost.P95Ms:F3}ms max={cost.MaxMs:F3}ms markers={cost.Markers}");
    }

    #endregion
});

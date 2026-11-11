// スキットの並べ比べ（S2-skit）: store を Web の mock と同じ値（Moore・Blocking message・Choose a route・Route A/B・Background message）で駆動し、参照キャプチャと同じ 5 状態を 1280×720 と 2560×1080 で、打ち出し中を 1280×720 で撮る
// Skit comparison (S2-skit): drive the store with the Web mock values and capture the five reference states at 1280x720 and 2560x1080, plus mid-typing at 1280x720
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
// store を直接駆動するので IsPlayingSkit は偽のままで UIState はゲーム画面。ポーズはゲーム画面のポーズなので、入れ子ポーズに合わせて store の入力を止めてから撮る
// The store is driven directly, so IsPlayingSkit stays false and the UI state is the game screen; the pause is the game-screen pause, so store input is suspended to match the nested pause
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Skit.Skit;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

const string LongBody = "Blocking message. The conveyor is jammed again, check the splitter near the furnace before the night shift starts.";

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("uitk-skit-compare", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);

    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    // 裏タブの Game View は描画されずスクショが空になるので表へ出す
    // A Game View behind another tab never renders and screenshots come out empty, so bring it to front
    PlaytestGameViewOps.BringToFront();
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    var store = SkitPresentationStateStore.Instance;
    var skitManager = Object.FindFirstObjectByType<Client.Game.Skit.SkitManager>(FindObjectsInactive.Include);
    var backgroundSkit = Object.FindFirstObjectByType<Client.Game.InGame.BackgroundSkit.BackgroundSkitManager>(FindObjectsInactive.Include);
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => !skitManager.IsPlayingSkit && p.CurrentUiState == UIStateEnum.GameScreen, 60f, "開幕スキットが終わりゲーム画面");
    await p.WaitSeconds(1f);
    await p.Until(() => !backgroundSkit.IsPlayingSkit, 120f, "背景スキットも終わる");

    // 1280×720 で打ち出し中と 5 状態を撮る
    // At 1280x720, capture mid-typing and the five states
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    store.BeginBlocking(new SkitActionContext());
    store.SetTextAreaVisible(true);
    await ShootTyping();
    await ShootStates("720p");

    // 2560×1080 で 5 状態を撮り、1280×720 へ戻す
    // At 2560x1080, capture the five states, then return to 1280x720
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 2560, 1080);
    await UniTask.DelayFrame(3);
    store.BeginBlocking(new SkitActionContext());
    store.SetTextAreaVisible(true);
    await ShootStates("2560x1080");
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    // 長い本文の打ち出し中と、途中で Ctrl+U を往復した直後（Web はアンマウントで状態が消える。D-44）
    // Long body mid-typing, and right after toggling Ctrl+U twice mid-typing (Web unmounts and loses state; D-44)
    async UniTask ShootTyping()
    {
        store.PresentBlockingText("Moore", LongBody);
        await p.WaitSeconds(0.6f);
        await p.Screenshot("720p-uitk-skit-typing");
        store.PresentBlockingText("Moore", LongBody);
        await p.WaitSeconds(2f);
        await p.Screenshot("720p-uitk-skit-typing-full");

        store.PresentBlockingText("Moore", LongBody);
        await p.WaitSeconds(0.4f);
        for (var i = 0; i < 2; i++)
        {
            SemanticInput.KeyDown(Key.LeftCtrl);
            await SemanticInput.TapKey(Key.U);
            SemanticInput.KeyUp(Key.LeftCtrl);
            await p.WaitSeconds(0.3f);
        }
        p.Note($"Ctrl+U 往復後 uiHidden={store.GetCurrent().PresentationState.UiHidden}");
        await p.Screenshot("720p-uitk-skit-typing-after-ctrlu");
    }

    // 参照キャプチャの 2-skit-text・2-skit-choices・2-skit-transition・1-pause-in-skit・2-skit-background に当たる 5 状態
    // The five states matching the reference ids 2-skit-text, 2-skit-choices, 2-skit-transition, 1-pause-in-skit, 2-skit-background
    async UniTask ShootStates(string prefix)
    {
        store.PresentBlockingText("Moore", "Blocking message");
        await p.WaitSeconds(2f);
        await p.Screenshot($"{prefix}-uitk-skit-text");

        // Web の mock は選択肢の本文を Choose a route にしている
        // The Web mock uses Choose a route as the body under the choices
        store.PresentBlockingText("Moore", "Choose a route");
        await p.WaitSeconds(1.5f);
        store.PresentChoices(new[] { new SkitChoice { ChoiceId = "route-a", Label = "Route A" }, new SkitChoice { ChoiceId = "route-b", Label = "Route B" } });
        await p.UntilUiElement("skit-choice-route-b", 5f);
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"{prefix}-uitk-skit-choices");

        store.SetTransitionVisible(true);
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"{prefix}-uitk-skit-transition");
        store.SetTransitionVisible(false);

        // スキット中のポーズ（会話は背後に残る）
        // Pause during the skit (the dialogue stays behind)
        store.PresentBlockingText("Moore", "Blocking message");
        await p.WaitSeconds(2f);
        await p.PressKey(Key.Escape);
        await p.UntilUiElement("pause-menu", 10f);
        store.SetInputSuspended(true);
        p.Assert(store.GetCurrent().AllowedIntents.Length == 0, $"入力停止で許可 intent が空 (実際 {string.Join(",", store.GetCurrent().AllowedIntents)})");
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"{prefix}-uitk-pause-in-skit");
        store.SetInputSuspended(false);
        await p.PressKey(Key.Escape);
        await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 10f, "ポーズを閉じてゲーム画面");
        await p.WaitSeconds(0.5f);

        store.End();
        store.BeginBackground();
        store.SetBackgroundText("Moore", "Background message");
        await p.WaitSeconds(1.5f);
        await p.Screenshot($"{prefix}-uitk-skit-background");
        store.End();
    }

    #endregion
});

// 感想とバグの送信ボタンを実機CEFで描画し、種別切替を録画する
// Render feedback and bug send labels in real CEF and record kind switching
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Client.Playtest.WebUi;
using UnityEngine;
using UnityEngine.InputSystem;

return PlaytestRunner.Run("feedback-report-labels", new PlaytestRunOptions { Record = true }, async p =>
{
    // 非フォーカスでも注入イベントをGameViewへ届け、終了時に元設定へ戻す
    // Route injected events to GameView while unfocused and restore settings after verification
    var inputSettings = InputSystem.settings;
    var backgroundBehavior = inputSettings.backgroundBehavior;
    var editorInputBehavior = inputSettings.editorInputBehaviorInPlayMode;
    inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    InputSystem.EnableDevice(Mouse.current);
    InputSystem.EnableDevice(Keyboard.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.Note("ポーズの報告画面で感想を選ぶ");
    // 文言検証の前提として既存の画面遷移窓口からポーズへ入る
    // Enter pause through the existing transition API as a precondition for label verification
    Object.FindFirstObjectByType<UIStateControl>().RequestTransition(UIStateEnum.PauseMenu);
    await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
    // 保存済み言語に依存せず、日本語の期待値へ設定画面から揃える
    // Set Japanese through settings so assertions do not depend on the saved locale
    await p.ClickWebUi("pause-menu-open-settings");
    await p.ClickWebUi("language-select-option-japanese");
    await p.UntilWebUiElement("pause-menu-locale-japanese", 15f);
    await p.ClickWebUi("pause-menu-back");
    await p.ClickWebUi("pause-menu-open-bug-report");
    await p.ClickWebUi("bug-report-kind-feedback");
    await PlaytestWebUiOps.WaitWebUiTextContains("bug-report-send", "感想", 15f);
    var feedback = await PlaytestDomQuery.Query("bug-report-send", 5f);
    p.Assert(feedback.Found && feedback.Text == "感想を送信", "感想のボタンは感想を送信");
    await p.Screenshot("feedback-button");

    p.Note("バグへ戻し従来の送信ボタンを確認する");
    await p.ClickWebUi("bug-report-kind-bug");
    await PlaytestWebUiOps.WaitWebUiTextContains("bug-report-send", "バグ", 15f);
    var bug = await PlaytestDomQuery.Query("bug-report-send", 5f);
    p.Assert(bug.Found && bug.Text == "バグ報告を送信", "バグのボタンは従来文言");
    await p.Screenshot("bug-button");
    inputSettings.backgroundBehavior = backgroundBehavior;
    inputSettings.editorInputBehaviorInPlayMode = editorInputBehavior;
});

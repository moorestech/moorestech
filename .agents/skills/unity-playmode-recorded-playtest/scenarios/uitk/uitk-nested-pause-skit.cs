// 入れ子ポーズ（スキット中）: Esc→UITKのポーズ→設定→戻る→Escで閉じ、スキットが続くことを録画する（ADR 0035・R-13）
// Nested pause during a skit: Esc -> UITK pause -> settings -> back -> Esc closes it and the skit continues (ADR 0035, R-13)
using System.Reflection;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-nested-pause-skit", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    var skitStore = SkitPresentationStateStore.Instance;
    var skitManager = Object.FindFirstObjectByType<Client.Game.Skit.SkitManager>(FindObjectsInactive.Include);
    var control = Object.FindFirstObjectByType<UIStateControl>();
    var dictionary = (UIStateDictionary)typeof(UIStateControl).GetField("_uiStateDictionary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control);
    var skitState = (SkitState)dictionary.GetState(UIStateEnum.Story);
    bool MenuOpen() => skitState.SubState == NestedPauseSubStateEnum.PauseMenuScreen;

    // 自動再生中の開幕スキットを先に終わらせる。並走させると古い方の後片付けが再生中の印を下ろしゲーム画面へ戻る
    // Finish the auto-played opening skit first; running two at once lets the old one's cleanup clear the playing flag and drop to the game screen
    await p.SkipOpeningSkitIfPlaying();
    p.Note($"飛ばした直後 timeScale={Time.timeScale} 再生中={skitManager.IsPlayingSkit} UI={p.CurrentUiState}");
    await p.Until(() => !skitManager.IsPlayingSkit && p.CurrentUiState == UIStateEnum.GameScreen, 60f, "開幕スキットが終わりゲーム画面");
    // 開幕チャレンジは続けて背景スキットを流し、その終了処理(End)が再生中スキットの送り待ちを取り消すので、終わるまで待つ
    // The opening challenge chains a background skit whose End() cancels a running skit's advance wait, so wait for it to finish
    var backgroundSkit = Object.FindFirstObjectByType<Client.Game.InGame.BackgroundSkit.BackgroundSkitManager>(FindObjectsInactive.Include);
    await p.WaitSeconds(1f);
    await p.Until(() => !backgroundSkit.IsPlayingSkit && !skitManager.IsPlayingSkit, 120f, "背景スキットも終わる");

    // 開幕スキットを直接起動する
    // Start the opening skit directly
    p.Note("開幕スキットを起動する");
    skitManager.StartSkit("Vanilla/Skit/skits/100_start_game").Forget();
    await p.Until(() => skitStore.GetCurrent().PresentationState.Mode == "blocking", 60f, "スキット(blocking)開始待ち");
    await p.Until(() => p.CurrentUiState == UIStateEnum.Story, 10f, "UIStateがStoryになる");
    // 冒頭の演出（転換・待ち）を抜け、会話の文で送りを待つところまで進める
    // Let the opening transitions finish until a dialogue line is waiting for advance
    await p.Until(() => skitStore.GetCurrent().SceneRevision >= 3, 30f, "会話の文が出て送り待ちになる");
    // メニュー中もスキットは背後で流れ続ける（ADR 0035）。自動送りだと録画中に終わるので切って送り待ちで止める
    // The skit keeps playing behind the menu (ADR 0035); auto-advance would end it mid-recording, so turn it off and hold on advance
    var beforePause = skitStore.GetCurrent();
    p.Note($"自動送り={beforePause.PresentationState.AutoEnabled} スキップ={beforePause.PresentationState.SkipActive}");
    if (beforePause.PresentationState.AutoEnabled)
        p.Assert(skitStore.TrySetAuto(beforePause.SessionId, beforePause.SceneRevision, false).Ok, "自動送りを切る");
    await p.WaitSeconds(1f);

    // Escで入れ子ポーズ（UITK）を開く
    // Open the nested pause (UITK) with Esc
    p.Note("スキット中: Escで入れ子ポーズ（UITK）");
    await p.PressKey(Key.Escape);
    await p.Until(MenuOpen, 5f, "入れ子ポーズが開く");
    await p.UntilUiElement("pause-menu", 10f);
    p.Assert(p.CurrentUiState == UIStateEnum.Story, $"ポーズ中も外側はStory（実測 {p.CurrentUiState}）");
    // ポーズの背後でも会話窓は見えていて、押せない（R-9）
    // The dialogue window stays visible behind the pause and cannot be pressed (R-9)
    var skitWindowBehindPause = await PlaytestUiElementOps.FindUi("blocking-skit");
    p.Assert(skitWindowBehindPause.IsFound && skitWindowBehindPause.Visible, "ポーズの背後で会話窓が見えている");
    p.Assert(!skitWindowBehindPause.Clickable, "ポーズ中は会話窓を押せない");
    await p.Screenshot("01-skit-pause-top");

    // 設定へ進み、戻るでトップへ
    // Go to settings and back to the top
    await p.ClickUi("pause-menu-open-settings");
    await p.UntilUiElement("language-select", 10f);
    await p.Screenshot("02-skit-pause-settings");
    await p.ClickUi("pause-menu-back");
    await p.UntilUiElement("pause-menu-open-settings", 10f);
    p.Assert(MenuOpen(), "設定から戻っても入れ子ポーズが開いている");

    // Escで閉じてスキットへ戻る
    // Close with Esc and return to the skit
    await p.PressKey(Key.Escape);
    await p.Until(() => !MenuOpen(), 5f, "入れ子ポーズが閉じる");
    await p.WaitSeconds(0.5f);
    var closed = await PlaytestUiElementOps.FindUi("pause-menu");
    p.Assert(!closed.IsFound || !closed.Visible, "閉じた後は pause-menu が見えない");
    p.Assert(p.CurrentUiState == UIStateEnum.Story, "閉じた後もStory");
    await p.Screenshot("03-skit-continues");

    // 後始末: スキットを飛ばしてゲーム画面へ
    // Cleanup: skip the skit back to the game screen
    var current = skitStore.GetCurrent();
    skitStore.TrySkip(current.SessionId, current.SceneRevision);
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 120f, "スキット終了でGameScreen");
});

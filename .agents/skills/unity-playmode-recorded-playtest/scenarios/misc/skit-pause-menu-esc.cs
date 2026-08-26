// スキット中のEscでポーズメニューが開閉し、UI非表示中はEscがUI復帰を優先することを実走確認する (ADR 0035)
// Verify at runtime that Esc toggles the pause menu during a skit and restores the hidden dialogue UI first (ADR 0035)
using System.Reflection;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.Skit;
using Client.Playtest;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("skit-pause-menu-esc", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    var skitStore = SkitPresentationStateStore.Instance;
    var skitManager = Object.FindFirstObjectByType<Client.Game.Skit.SkitManager>(FindObjectsInactive.Include);
    // uGUIのPauseMenuObjectは恒久抑止（Webモード固定）なので、入れ子サブステートを正として判定する
    // The uGUI PauseMenuObject is permanently suppressed (web mode is fixed), so the nested sub-state is the source of truth
    var control = Object.FindFirstObjectByType<UIStateControl>();
    var dictionary = (UIStateDictionary)typeof(UIStateControl).GetField("_uiStateDictionary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control);
    var skitState = (SkitState)dictionary.GetState(UIStateEnum.Story);
    bool MenuOpen() => skitState.SubState == SkitScreenUIStateEnum.PauseMenu;

    // 固定world起動はSkitPlaySettingsKeyで開幕スキットを抑止するため、開幕スキットを直接起動する
    // Fixed-world boot suppresses the opening skit via SkitPlaySettingsKey, so start the opening skit directly
    p.Note("開幕スキット(100_start_game)を直接起動する");
    skitManager.StartSkit("Vanilla/Skit/skits/100_start_game").Forget();
    await p.Until(() => skitStore.GetCurrent().PresentationState.Mode == "blocking", 60f, "スキット(blocking)開始待ち");
    await p.Until(() => p.CurrentUiState == UIStateEnum.Story, 10f, "UIStateがStoryになる");
    await p.WaitSeconds(1f);

    // R1: Escでメニューが開く
    p.Note("R1: Escでポーズメニューを開く");
    var revisionBefore = skitStore.GetCurrent().SceneRevision;
    await p.PressKey(Key.Escape);
    await p.Until(() => MenuOpen(), 2f, "PauseMenuが表示される");
    p.Assert(p.CurrentUiState == UIStateEnum.Story, $"メニュー中もUIStateはStory (actual {p.CurrentUiState})");
    await p.Screenshot("01-pause-menu-over-skit");

    // R2: メニュー中も会話が進む（advanceを打ってrevisionが進む）
    p.Note("R2: メニュー中も会話を進める");
    var current = skitStore.GetCurrent();
    skitStore.TryAdvance(current.SessionId, current.SceneRevision);
    await p.Until(() => skitStore.GetCurrent().SceneRevision > revisionBefore, 10f, "メニュー中に会話revisionが進む");
    p.Assert(MenuOpen(), "会話が進んでもメニューは開いたまま");

    // R4: Escでメニューを閉じてStoryのまま
    p.Note("R4: Escでメニューを閉じる");
    await p.PressKey(Key.Escape);
    await p.Until(() => !MenuOpen(), 2f, "PauseMenuが閉じる");
    await p.WaitSeconds(0.5f);
    p.Assert(p.CurrentUiState == UIStateEnum.Story, $"閉じた後もUIStateはStory (actual {p.CurrentUiState})");
    await p.Screenshot("02-menu-closed-skit-continues");

    // R3: Webの「UIを隠す」操作（skit.set_ui_hidden相当）でUIを隠し、1回目Escは復帰のみ・2回目Escでメニュー
    // R3: hide the UI via the web "hide UI" intent (skit.set_ui_hidden); the first Esc only restores it, the second opens the menu
    p.Note("R3: 会話UIを非表示にしてからEsc");
    current = skitStore.GetCurrent();
    skitStore.TrySetUiHidden(current.SessionId, current.SceneRevision, true);
    await p.WaitSeconds(0.5f);
    bool UiHidden() => skitStore.GetCurrent().PresentationState.UiHidden;
    p.Assert(UiHidden(), "会話UIが非表示になった");
    await p.Screenshot("03-skit-ui-hidden");
    await p.PressKey(Key.Escape);
    await p.WaitSeconds(0.5f);
    p.Assert(!UiHidden(), "1回目Escで会話UIが復帰する");
    p.Assert(!MenuOpen(), "1回目Escではメニューは開かない");
    await p.Screenshot("04-skit-ui-restored-no-menu");
    await p.PressKey(Key.Escape);
    await p.Until(() => MenuOpen(), 2f, "2回目Escでメニューが開く");
    await p.Screenshot("05-second-esc-opens-menu");

    // R5: メニューを開いたままSkipでスキット終了→GameScreen
    p.Note("R5: メニューを開いたままスキットをスキップ");
    current = skitStore.GetCurrent();
    skitStore.TrySkip(current.SessionId, current.SceneRevision);
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 120f, "スキット終了でGameScreenへ遷移");
    await p.WaitSeconds(0.5f);
    p.Assert(!MenuOpen(), "スキット終了でメニューが閉じている");
    await p.Screenshot("06-game-screen-after-skip");
});

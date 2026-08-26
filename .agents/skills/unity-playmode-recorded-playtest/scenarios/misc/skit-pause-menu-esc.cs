// スキット中のEscでポーズメニューが開閉し、UI非表示中はEscがUI復帰を優先することを実走確認する (ADR 0035)
// Verify at runtime that Esc toggles the pause menu during a skit and restores the hidden dialogue UI first (ADR 0035)
using System.Reflection;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.Skit;
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
    var skitManager = Object.FindFirstObjectByType<SkitManager>(FindObjectsInactive.Include);
    var pauseMenu = Object.FindFirstObjectByType<PauseMenuObject>(FindObjectsInactive.Include);
    p.Assert(pauseMenu != null, "PauseMenuObjectがシーンに存在する");

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
    await p.Until(() => pauseMenu.gameObject.activeSelf, 2f, "PauseMenuが表示される");
    p.Assert(p.CurrentUiState == UIStateEnum.Story, $"メニュー中もUIStateはStory (actual {p.CurrentUiState})");
    await p.Screenshot("01-pause-menu-over-skit");

    // R2: メニュー中も会話が進む（advanceを打ってrevisionが進む）
    p.Note("R2: メニュー中も会話を進める");
    var current = skitStore.GetCurrent();
    skitStore.TryAdvance(current.SessionId, current.SceneRevision);
    await p.Until(() => skitStore.GetCurrent().SceneRevision > revisionBefore, 10f, "メニュー中に会話revisionが進む");
    p.Assert(pauseMenu.gameObject.activeSelf, "会話が進んでもメニューは開いたまま");

    // R4: Escでメニューを閉じてStoryのまま
    p.Note("R4: Escでメニューを閉じる");
    await p.PressKey(Key.Escape);
    await p.Until(() => !pauseMenu.gameObject.activeSelf, 2f, "PauseMenuが閉じる");
    await p.WaitSeconds(0.5f);
    p.Assert(p.CurrentUiState == UIStateEnum.Story, $"閉じた後もUIStateはStory (actual {p.CurrentUiState})");
    await p.Screenshot("02-menu-closed-skit-continues");

    // R3: HiddenButton相当でUIを隠し、1回目Escは復帰のみ・2回目Escでメニュー
    p.Note("R3: 会話UIを非表示にしてからEsc");
    var skitUi = Object.FindFirstObjectByType<SkitUI>(FindObjectsInactive.Include);
    var tools = typeof(SkitUI).GetField("_skitUITools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skitUi);
    tools.GetType().GetMethod("HideUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tools, null);
    await p.WaitSeconds(0.5f);
    p.Assert(skitManager.IsSkitUiHidden, "会話UIが非表示になった");
    await p.Screenshot("03-skit-ui-hidden");
    await p.PressKey(Key.Escape);
    await p.WaitSeconds(0.5f);
    p.Assert(!skitManager.IsSkitUiHidden, "1回目Escで会話UIが復帰する");
    p.Assert(!pauseMenu.gameObject.activeSelf, "1回目Escではメニューは開かない");
    await p.Screenshot("04-skit-ui-restored-no-menu");
    await p.PressKey(Key.Escape);
    await p.Until(() => pauseMenu.gameObject.activeSelf, 2f, "2回目Escでメニューが開く");
    await p.Screenshot("05-second-esc-opens-menu");

    // R5: メニューを開いたままSkipでスキット終了→GameScreen
    p.Note("R5: メニューを開いたままスキットをスキップ");
    current = skitStore.GetCurrent();
    skitStore.TrySkip(current.SessionId, current.SceneRevision);
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 120f, "スキット終了でGameScreenへ遷移");
    await p.WaitSeconds(0.5f);
    p.Assert(!pauseMenu.gameObject.activeSelf, "スキット終了でメニューが閉じている");
    await p.Screenshot("06-game-screen-after-skip");
});

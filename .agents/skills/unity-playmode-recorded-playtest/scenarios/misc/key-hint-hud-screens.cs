using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

// 全画面の左下ヒント表示を撮り、ADR-0032の画面別内容表と目視で突き合わせる
// Capture the bottom-left hints on every screen so they can be eyeballed against the ADR-0032 content table
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("key-hint-hud-screens", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();

    var control = Object.FindFirstObjectByType<UIStateControl>();

    async UniTask Shot(UIStateEnum state, string name)
    {
        if (p.CurrentUiState != state)
        {
            control.RequestTransition(state);
            await p.WaitSeconds(1f);
        }
        p.Note($"{state} のヒントを撮る");
        await p.Screenshot(name);
    }

    await Shot(UIStateEnum.GameScreen, "01-game-screen");
    await Shot(UIStateEnum.PlayerInventory, "02-player-inventory");
    await Shot(UIStateEnum.ResearchTree, "03-research-tree");
    await Shot(UIStateEnum.BuildMenu, "04-build-menu");
    await Shot(UIStateEnum.DeleteBar, "05-delete-bar");
    await Shot(UIStateEnum.PauseMenu, "06-pause-menu");

    // ポーズ中はBを拾わないため、配置モードへ入る前にGameScreenへ戻す
    // The pause menu ignores B, so return to GameScreen before entering placement mode
    await Shot(UIStateEnum.GameScreen, "07-game-screen-again");

    // 配置モードはビルドメニュー経由でのみ安定して入れる
    // Placement mode is only reachable reliably through the build menu
    await p.OpenBuildMenuAndSelectBlock("木のチェスト");
    await p.Screenshot("08-place-block");
});

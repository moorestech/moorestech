// 並べ比べの解像度切替: Game View を 2560×1080 にして UITK のパネル倍率 1.5 を確かめ、1280×720 に戻して 1.0 を確かめる
// Resolution switch for comparisons: set the Game View to 2560x1080 and check the UITK panel scale 1.5, then back to 1280x720 and check 1.0
// 実行前にPlayModeを止め、execute-dynamic-codeで UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Linq;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-gameview-resolution-switch", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // 横長の 2560×1080 へ切り替えて撮る
    // Switch to the wide 2560x1080 and capture
    p.Note("Game View を 2560×1080 へ切り替える");
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 2560, 1080);
    await UniTask.DelayFrame(3);
    p.Assert(Screen.width == 2560 && Screen.height == 1080, $"Screen が 2560×1080 になった (実際 {Screen.width}x{Screen.height})");
    p.Assert(Mathf.Abs(GameUiScale() - 1.5f) < 0.01f, $"UITK のパネル倍率が 1.5 (実際 {GameUiScale()})");
    await p.Screenshot("01-gameui-2560x1080");

    // 1280×720 へ戻して撮る
    // Switch back to 1280x720 and capture
    p.Note("Game View を 1280×720 へ戻す");
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    await UniTask.DelayFrame(3);
    p.Assert(Screen.width == 1280 && Screen.height == 720, $"Screen が 1280×720 に戻った (実際 {Screen.width}x{Screen.height})");
    p.Assert(Mathf.Abs(GameUiScale() - 1f) < 0.01f, $"UITK のパネル倍率が 1.0 (実際 {GameUiScale()})");
    await p.Screenshot("02-gameui-1280x720");

    #region Internal

    // 本番の GameUi パネル（GameUiPanelSettings）の倍率を読む
    // Read the scale of the production GameUi panel (GameUiPanelSettings)
    float GameUiScale()
    {
        var document = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).First(d => d.panelSettings != null && d.panelSettings.name.StartsWith("GameUiPanelSettings"));
        return document.rootVisualElement.panel.scaledPixelsPerPoint;
    }

    #endregion
});

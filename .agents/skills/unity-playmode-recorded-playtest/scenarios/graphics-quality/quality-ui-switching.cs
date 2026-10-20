using System.Linq;
using Client.DebugSystem.Environment;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Playtest.WebUi;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// 録画前に入力とGameViewを実確認する
// Probe input and Game View before starting the recording
var gameView = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView");
gameView.Focus();
Debug.Log($"[INPUT] focus={Application.isFocused} background={InputSystem.settings.backgroundBehavior} keyboard={Keyboard.current.enabled} mouse={Mouse.current.enabled}");
if (!Keyboard.current.enabled || !Mouse.current.enabled) throw new System.InvalidOperationException("Input devices disabled");
await SemanticInput.TapKey(Key.Escape);
await UniTask.WaitUntil(() => PlaytestUiOps.CurrentUiState() == UIStateEnum.PauseMenu).Timeout(System.TimeSpan.FromSeconds(15));
await SemanticInput.TapKey(Key.Escape);
await UniTask.WaitUntil(() => PlaytestUiOps.CurrentUiState() == UIStateEnum.GameScreen).Timeout(System.TimeSpan.FromSeconds(15));
var hadPreference = PlayerPrefs.HasKey("GraphicsQuality");
var savedPreference = PlayerPrefs.GetString("GraphicsQuality");
return PlaytestRunner.Run("quality-ui-switching", new PlaytestRunOptions { Record = true }, async p =>
{
    try
    {
        // 固定worldのOtherを通常再生のDebug環境へ揃える
        // Match the normal Debug environment instead of fixed-world Other
        Common.Debug.DebugParameters.SaveInt("DebugEnvironmentTypeKey", 0);
        DebugEnvironmentController.SetEnvironment(DebugEnvironmentType.Debug);
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { CreateFlatGround = false, SpawnPosition = new Vector3(659.5f, 96.6f, 626.5f) });
        await p.SkipOpeningSkitIfPlaying();
        GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Medium);
        await p.WaitSeconds(0.5f);
        await p.Screenshot("01-medium-game");
        p.Note("Escapeからトップ4ボタンを確認して画質設定を開く");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
        foreach (var id in new[] { "pause-menu-save", "pause-menu-save-and-quit", "pause-menu-open-settings", "pause-menu-open-bug-report" })
        {
            await p.UntilWebUiElement(id, 15f);
            var rect = await PlaytestDomQuery.Query(id, 5f);
            p.Assert(rect.Visible && rect.X >= 0 && rect.Y >= 0 && rect.X + rect.Width <= Screen.width && rect.Y + rect.Height <= Screen.height, $"トップ見切れなし {id}: {rect.X},{rect.Y},{rect.Width},{rect.Height}");
        }
        await p.Screenshot("02-pause-top");
        await p.ClickWebUi("pause-menu-open-settings");
        await Choose("graphics-preset-low");
        AssertPreset(GraphicsQualityPreset.Low, 300f, false, 0.5f);
        await CloseSettings();
        await p.Screenshot("03-low-game");

        // 高へ戻してから影だけを変えカスタム表示を確認する
        // Restore High, then change shadows alone and verify Custom
        await OpenSettings();
        await Choose("graphics-preset-high");
        AssertPreset(GraphicsQualityPreset.High, 5000f, true, 1f);
        await CloseSettings();
        await p.Screenshot("04-high-game");
        await OpenSettings();
        await Choose("graphics-shadows-off");
        await p.UntilWebUiElement("graphics-preset-custom", 15f);
        p.Assert(!GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out _), "影だけオフでカスタム");
        await p.Screenshot("05-custom-settings");
        await CloseSettings();
        await p.Screenshot("06-custom-game");
    }
    finally
    {
        // 値だけでなく未保存状態も必ず戻す
        // Always restore both the value and the absence of a saved key
        if (hadPreference) PlayerPrefs.SetString("GraphicsQuality", savedPreference);
        else PlayerPrefs.DeleteKey("GraphicsQuality");
        PlayerPrefs.Save();
        p.Note($"GraphicsQuality復元: existed={hadPreference}, value={savedPreference}");
    }

    #region Internal
    async UniTask Choose(string id)
    {
        await PlaytestWebUiOps.ScrollDownUntilWebUiElementIsClickable(id, "pause-menu-body", 15f);
        await p.ClickWebUi(id);
    }
    async UniTask OpenSettings()
    {
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
        await p.ClickWebUi("pause-menu-open-settings");
    }
    async UniTask CloseSettings()
    {
        await p.PressKey(Key.Escape);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    }
    void AssertPreset(GraphicsQualityPreset expected, float distance, bool shadows, float scale)
    {
        p.Assert(GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out var actual) && actual == expected, $"プリセット {expected}");
        p.Assert(Mathf.Approximately(Camera.main.farClipPlane, distance), $"farClip={Camera.main.farClipPlane}");
        p.Assert(Camera.main.GetUniversalAdditionalCameraData().renderShadows == shadows, $"shadows={shadows}");
        p.Assert(Mathf.Approximately(((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale, scale), $"renderScale={scale}");
    }
    #endregion
});

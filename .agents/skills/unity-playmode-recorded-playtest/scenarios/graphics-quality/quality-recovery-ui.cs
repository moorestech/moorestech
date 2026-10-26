using System.Linq;
using Client.DebugSystem.Environment;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Playtest.UiElements;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// 録画前に入力とGameViewを実確認する
// Probe input and Game View before starting the recording
var gameView = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView");
var viewFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var dock = typeof(UnityEditor.EditorWindow).GetField("m_Parent", viewFlags).GetValue(gameView);
var tabs = (System.Collections.IList)dock.GetType().GetField("m_Panes", viewFlags).GetValue(dock);
dock.GetType().GetProperty("selected", viewFlags).SetValue(dock, tabs.IndexOf(gameView));
gameView.Focus();
await UniTask.DelayFrame(3);
Debug.Log($"[INPUT] focus={Application.isFocused} background={InputSystem.settings.backgroundBehavior} keyboard={Keyboard.current.enabled} mouse={Mouse.current.enabled}");
if (!Keyboard.current.enabled || !Mouse.current.enabled) throw new System.InvalidOperationException("Input devices disabled");
await SemanticInput.TapKey(Key.Escape);
await UniTask.WaitUntil(() => PlaytestUiOps.CurrentUiState() == UIStateEnum.PauseMenu).Timeout(System.TimeSpan.FromSeconds(15));
await SemanticInput.TapKey(Key.Escape);
await UniTask.WaitUntil(() => PlaytestUiOps.CurrentUiState() == UIStateEnum.GameScreen).Timeout(System.TimeSpan.FromSeconds(15));
var hadPreference = PlayerPrefs.HasKey("GraphicsQuality");
var savedPreference = PlayerPrefs.GetString("GraphicsQuality");
var savedSettings = GraphicsQualityPreference.Current;
var savedRecovered = GraphicsQualityPreference.RecoveredInvalidPreference;
return PlaytestRunner.Run("quality-recovery-ui", new PlaytestRunOptions { Record = false }, async p =>
{
    try
    {
        // 起動時に壊れた保存値が実ホストtopicへ届く経路を撮る
        // Record the real host topic after repairing a corrupt startup preference
        Common.Debug.DebugParameters.SaveInt("DebugEnvironmentTypeKey", 0);
        DebugEnvironmentController.SetEnvironment(DebugEnvironmentType.Debug);
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { CreateFlatGround = false, SpawnPosition = new Vector3(657.5f, 96.6f, 627.5f) });
        await p.SkipOpeningSkitIfPlaying();
        p.Assert(GraphicsQualityPreference.RecoveredInvalidPreference, "実起動の不正保存値を復旧済み");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
        await p.ClickUi("pause-menu-open-settings");
        await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable("graphics-recovered", "pause-menu-body", 15f);
        var notice = await PlaytestUiElementOps.FindUi("graphics-recovered");
        p.Assert(notice.Visible && notice.Text.Contains("画質設定を中に復旧しました"), "実ホストtopicの復旧通知がUIへ届いた");
        p.Note($"[RECOVERY] before={GraphicsQualityPreference.RecoveredInvalidPreference} text={notice.Text}");
        await p.Screenshot("01-real-host-recovered");
        await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable("graphics-shadows-on", "pause-menu-body", 15f);
        await p.ClickUi("graphics-shadows-on");
        await p.Until(() => !GraphicsQualityPreference.RecoveredInvalidPreference, 10f, "同値の明示選択で復旧通知解除");
        await p.WaitSeconds(0.5f);
        var cleared = await PlaytestUiElementOps.FindUi("graphics-recovered");
        p.Assert(!cleared.IsFound, "実ホストの解除topicがUIへ届いた");
        await p.Screenshot("02-real-host-recovery-cleared");
        await p.PressKey(Key.Escape);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 15f);
        await p.Screenshot("03-game-restored");
    }
    finally
    {
        // 生値と復旧状態を元へ戻す
        // Restore the raw preference and recovery state
        GraphicsQualityPreference.SetSettings(savedSettings);
        typeof(GraphicsQualityPreference).GetField("<RecoveredInvalidPreference>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, savedRecovered);
        if (hadPreference) PlayerPrefs.SetString("GraphicsQuality", savedPreference);
        else PlayerPrefs.DeleteKey("GraphicsQuality");
        PlayerPrefs.Save();
    }
});

using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.UiElements;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

// 起動前に保存した不正値の修復と通知を実画面で検証する
// Verify repair and notification of an invalid preference saved before startup
await PlaytestSetup.SetupDebugEnvironment(new PlaytestEnvironmentConfig { SpawnPosition = new Vector3(0f, 303.5f, 0f) });
// 遅延ロード地形との干渉を避け、試験用足場を十分高く置く
// Raise the test scaffold above terrain loaded asynchronously
GameObject.Find("PlaytestFlatGround").transform.position = new Vector3(0f, 300f, 0f);
GameObject.Find("PlaytestFlatGround").transform.localScale = new Vector3(100f, 4f, 100f);
await PlaytestSkitOps.SkipOpeningSkit(30f, 15f);
SemanticInput.EnsureDevices();
// Editorの背景入力制限だけを一時解除し、終了時に元へ戻す
// Temporarily relax only Editor background input restrictions, restoring them at the end
var originalBackground = InputSystem.settings.backgroundBehavior;
var originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
InputSystem.EnableDevice(Keyboard.current);
InputSystem.EnableDevice(Mouse.current);
await UniTask.DelayFrame(3);
var probe = $"focused={Application.isFocused}, background={InputSystem.settings.backgroundBehavior}, keyboard={Keyboard.current.enabled}, mouse={Mouse.current.enabled}";
Debug.Log("Sprint restart probe: " + probe);
if (Application.isFocused || !Keyboard.current.enabled || !Mouse.current.enabled)
    throw new System.InvalidOperationException("Background input precondition failed: " + probe);
await SemanticInput.TapKey(Key.Tab);
await PlaytestUiOps.WaitUiState(UIStateEnum.PlayerInventory, 10f);
// 録画前にUI照会を確認し、probeのロケータ購読を必ず片付ける
// Verify UI lookup before recording and always clean up the probe locator
PlaytestUiLocatorSession.Start();
try { await PlaytestUiElementOps.WaitUiElement("inventory-sort", 15f); }
finally
{
    PlaytestUiLocatorSession.Stop();
}
await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));
await SemanticInput.TapKey(Key.Tab);
await PlaytestUiOps.WaitUiState(UIStateEnum.GameScreen, 10f);

return PlaytestRunner.Run("sprint-invalid-recovery", new PlaytestRunOptions { Record = false }, async p =>
{
    var player = (MonoBehaviour)PlayerSystemContainer.Instance.PlayerObjectController;
    var controller = player.GetComponent<ThirdPersonController>();
    var inputs = player.GetComponent<StarterAssetsInputs>();
    const string message = "走り設定を復旧しました。方式を選ぶと通知が消えます。";
    try
    {
        p.Note("不正保存値2からの起動修復: " + probe);
        p.Assert(PlayerPrefs.GetString("SprintInputMode") == "Toggle", "起動時に不正保存値をToggleへ実修復");
        p.Assert(SprintInputModePreference.Current == SprintInputMode.Toggle, "復旧後の方式Toggle");
        p.Assert(SprintInputModePreference.RecoveredInvalidPreference, "復旧通知状態あり");
        await p.PressKey(Key.LeftShift);
        p.Assert(!inputs.sprint, "通知確認前に歩きへ切り替え");

        // 実設定ページの復旧文を確認し、同値選択で通知だけを消す
        // Confirm recovery in the real settings page and dismiss it by selecting the same mode
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
        await p.ClickUi("pause-menu-open-settings");
        await PlaytestUiElementOps.WaitUiTextContains("pause-menu", message, 10f);
        var before = await PlaytestUiElementOps.FindUi("pause-menu");
        p.Note("復旧通知表示: " + before.Text);
        p.Assert(before.IsFound && before.Text.Contains(message), "実設定ページに復旧通知を表示");
        await p.Screenshot("01-recovery-notice");
        await p.ClickUi("sprint-mode-option-toggle");
        await p.Until(() => !SprintInputModePreference.RecoveredInvalidPreference, 10f, "同値選択で復旧通知解除");
        await p.WaitSeconds(0.5f);
        var after = await PlaytestUiElementOps.FindUi("pause-menu");
        p.Note("同値選択後: " + after.Text);
        p.Assert(after.IsFound && !after.Text.Contains(message), "実設定ページの復旧通知が消える");
        p.Assert(PlayerPrefs.GetString("SprintInputMode") == "Toggle", "同値選択後もToggleを保存");
        await p.Screenshot("02-recovery-acknowledged");
        await p.ClickUi("pause-menu-back");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        p.Assert(!inputs.sprint, "同値選択とメニュー復帰後も歩きを維持");

        // 通知の解除が歩行速度を変えていないことを実移動で測る
        // Measure actual movement to ensure acknowledgment does not change walking speed
        await p.Until(() => controller.Grounded, 10f, "足場へ着地");
        SemanticInput.KeyDown(Key.W);
        await p.WaitSeconds(0.5f);
        var start = p.PlayerPosition;
        var time = Time.time;
        await p.Until(() => Time.time - time >= 1f, 5f, "同値選択後の速度測定");
        var end = p.PlayerPosition;
        var speed = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z)) / (Time.time - time);
        p.Note($"recovery-acknowledged-walk: {speed:F3} m/s, expected={controller.MoveSpeed:F3}, sprint={inputs.sprint}, focused={Application.isFocused}");
        p.Assert(Mathf.Abs(speed - controller.MoveSpeed) <= 0.5f, "通知解除後の歩き速度±0.5m/s");
        p.Assert(controller.Grounded && Mathf.Abs(end.y - start.y) < 0.05f, "通知解除後も水平な接地移動");
        SemanticInput.KeyUp(Key.W);
        await p.WaitSeconds(0.5f);
        await p.Screenshot("03-recovery-walking");
    }
    finally
    {
        // 失敗時にも注入キーとEditor設定を残さない
        // Restore injected keys and Editor settings even when an operation fails
        SemanticInput.ReleaseAllKeys();
        InputSystem.settings.backgroundBehavior = originalBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
    }
});

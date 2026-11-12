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

// 先行シナリオが保存したHoldを、変更せず新規PlayModeで検証する
// Verify Hold saved by the previous scenario after a fresh PlayMode, without changing it
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

return PlaytestRunner.Run("sprint-hold-restart", new PlaytestRunOptions { Record = false }, async p =>
{
    var player = (MonoBehaviour)PlayerSystemContainer.Instance.PlayerObjectController;
    var controller = player.GetComponent<ThirdPersonController>();
    var inputs = player.GetComponent<StarterAssetsInputs>();
    try
    {
        p.Note("再起動後の背景入力成立: " + probe);
        p.Assert(PlayerPrefs.GetString("SprintInputMode") == "Hold", "再起動後もHold保存値が残る");
        p.Assert(SprintInputModePreference.Current == SprintInputMode.Hold, "起動時Holdを復元");
        await p.Until(() => controller.Grounded, 10f, "足場へ着地");

        // Wを保持したまま、Shiftを押した区間だけ走ることを実測する
        // Measure running only while Shift is pressed, with W continuously held
        SemanticInput.KeyDown(Key.W);
        await p.WaitSeconds(0.5f);
        await Measure("restart-walk", false);
        SemanticInput.KeyDown(Key.LeftShift);
        await p.WaitSeconds(0.5f);
        await Measure("restart-run", true);
        SemanticInput.KeyUp(Key.LeftShift);
        await p.WaitSeconds(0.5f);
        await Measure("restart-release", false);
        SemanticInput.KeyUp(Key.W);
        await p.WaitSeconds(0.5f);
        await p.Screenshot("01-restart-hold");

    }
    finally
    {
        // 失敗時にも注入キーとEditor設定を残さない
        // Restore injected keys and Editor settings even if an assertion operation fails
        SemanticInput.ReleaseAllKeys();
        InputSystem.settings.backgroundBehavior = originalBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
    }

    #region Internal

    async UniTask Measure(string label, bool sprint)
    {
        var position = p.PlayerPosition;
        var started = Time.time;
        await p.Until(() => Time.time - started >= 1f, 5f, label);
        var end = p.PlayerPosition;
        var elapsed = Time.time - started;
        var speed = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(end.x, end.z)) / elapsed;
        var expected = sprint ? controller.SprintSpeed : controller.MoveSpeed;
        p.Note($"{label}: {speed:F3} m/s, expected={expected:F3}, dt={elapsed:F3}, sprint={inputs.sprint}, focused={Application.isFocused}");
        p.Assert(inputs.sprint == sprint, label + " sprint状態");
        p.Assert(Mathf.Abs(speed - expected) <= 0.5f, label + " 水平速度±0.5m/s");
        p.Assert(controller.Grounded && Mathf.Abs(end.y - position.y) < 0.05f, label + " 水平な接地移動");
    }

    #endregion
});

using System.Reflection;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.WebUi;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

// 録画前に背景入力とTab往復を実証する（Shiftの初期値は保持）
// Prove background input and a Tab round trip before recording, preserving initial sprint
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
Debug.Log("Sprint probe: " + probe);
if (Application.isFocused || !Keyboard.current.enabled || !Mouse.current.enabled)
    throw new System.InvalidOperationException("Background input precondition failed: " + probe);
await SemanticInput.TapKey(Key.Tab);
await PlaytestUiOps.WaitUiState(UIStateEnum.PlayerInventory, 10f);
// 録画前にDOM応答も確認し、probeの転送購読を必ず片付ける
// Verify DOM responses before recording and always clean up probe forwarding
PlaytestDomQuery.RegisterHandler();
CefInputForwarder.StartForwarding();
try { await PlaytestWebUiOps.WaitWebUiElement("inventory-sort", 15f); }
finally
{
    CefInputForwarder.StopForwarding();
    PlaytestDomQuery.ResetPending();
}
await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));
await SemanticInput.TapKey(Key.Tab);
await PlaytestUiOps.WaitUiState(UIStateEnum.GameScreen, 10f);

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 180f };
return PlaytestRunner.Run("sprint-toggle-mode", options, async p =>
{
    var player = (MonoBehaviour)PlayerSystemContainer.Instance.PlayerObjectController;
    var controller = player.GetComponent<ThirdPersonController>();
    var inputs = player.GetComponent<StarterAssetsInputs>();
    try
    {
        p.Note("背景入力Tab往復成功: " + probe);
        p.Assert(!PlayerPrefs.HasKey("SprintInputMode") && SprintInputModePreference.Current == SprintInputMode.Toggle, "保存値なしの既定Toggle");
        await p.Until(() => Time.timeScale == 1f, 10f, "録画開始後のtimeScale=1復帰");

        // 加速後の水平移動量を同じ足場で比較する
        // Compare horizontal displacement after acceleration on the same scaffold
        p.Note("a: 起動直後はWだけで走る");
        await BeginMovement();
        await Measure("a-default-run", true);
        await p.Screenshot("01-default-running");
        await EndMovement();
        p.Note("b: Shift単独タップで歩きに切り替える");
        await p.PressKey(Key.LeftShift);
        await BeginMovement();
        await Measure("b-toggle-walk", false);
        await EndMovement();

        // ベルトの鉛直回転を読み取り専用リフレクションで観測する
        // Observe belt vertical rotation through read-only reflection
        p.Note("c: ベルト配置でShift+R、歩き状態を維持");
        await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
        var ui = Object.FindFirstObjectByType<UIStateControl>();
        var states = (UIStateDictionary)ReadField(ui, "_uiStateDictionary");
        var placeController = ReadField(states.GetState(UIStateEnum.PlaceBlock), "_placeSystemStateController");
        var belt = ReadField(placeController, "_currentPlaceSystem");
        var before = (BlockDirection)ReadField(belt, "_currentBlockDirection");
        SemanticInput.KeyDown(Key.LeftShift);
        await p.WaitSeconds(0.5f);
        await p.PressKey(Key.R);
        SemanticInput.KeyUp(Key.LeftShift);
        await p.WaitSeconds(0.5f);
        var after = (BlockDirection)ReadField(belt, "_currentBlockDirection");
        p.Note($"Shift+R direction: {before} -> {after}; expected={before.VerticalRotation()}, sprint={inputs.sprint}");
        p.Assert(after == before.VerticalRotation(), "Shift+Rで鉛直回転");
        p.Assert(!inputs.sprint, "複合Shiftでトグル状態を維持");
        await p.Screenshot("02-shift-r-rotation");
        await p.ExitToGameScreen();
        await BeginMovement();
        await Measure("c-after-shift-r", false);
        await EndMovement();

        // 移動を止めないメニューでも単独Shiftが反転しない
        // A clean Shift tap must not toggle sprint in a non-locking inventory menu
        p.Note("d: インベントリ中のShiftタップは反転しない");
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
        await p.PressKey(Key.LeftShift);
        p.Assert(!inputs.sprint, "インベントリ中のShiftで歩きを維持");
        await p.Screenshot("03-inventory-shift");
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        await BeginMovement();
        await Measure("d-after-inventory", false);
        await EndMovement();

        // 通常のDOMクリック経路で方式を変更し、W保持中にShiftを押し離す
        // Change mode via normal DOM clicks and press/release Shift while holding W
        p.Note("e: ポーズ設定でホールドに変更");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
        await p.ClickWebUi("pause-menu-open-settings");
        await p.ClickWebUi("sprint-mode-option-hold");
        await p.Until(() => SprintInputModePreference.Current == SprintInputMode.Hold, 10f, "Hold設定反映");
        p.Assert(PlayerPrefs.GetString("SprintInputMode") == "Hold", "HoldをPlayerPrefsへ保存");
        await p.Screenshot("04-settings-hold");
        await p.ClickWebUi("pause-menu-back");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        await BeginMovement();
        await Measure("e-hold-released", false);
        SemanticInput.KeyDown(Key.LeftShift);
        await p.WaitSeconds(0.5f);
        await Measure("e-hold-pressed", true);
        await p.Screenshot("05-hold-running");
        SemanticInput.KeyUp(Key.LeftShift);
        await p.WaitSeconds(0.5f);
        await Measure("e-hold-released-again", false);
        await EndMovement();
        await p.Screenshot("06-complete");

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

    async UniTask BeginMovement()
    {
        p.WarpPlayer(new Vector3(0f, 303.5f, 0f));
        await p.WaitSeconds(0.5f);
        await p.Until(() => controller.Grounded, 10f, "足場へ着地");
        SemanticInput.KeyDown(Key.W);
        await p.WaitSeconds(0.5f);
    }

    async UniTask EndMovement()
    {
        SemanticInput.KeyUp(Key.W);
        await p.WaitSeconds(0.5f);
    }

    async UniTask Measure(string label, bool sprint)
    {
        // 計測はクライアント実時間を使い、実フレーム経過で割る
        // Client measurement uses actual frame elapsed time rather than an assumed delta
        var position = p.PlayerPosition;
        var started = Time.time;
        await p.Until(() => Time.time - started >= 1f, 5f, label);
        var end = p.PlayerPosition;
        var elapsed = Time.time - started;
        var speed = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(end.x, end.z)) / elapsed;
        var expected = sprint ? controller.SprintSpeed : controller.MoveSpeed;
        p.Note($"{label}: {speed:F3} m/s, expected={expected:F3}, dt={elapsed:F3}, from={position}, to={end}, sprint={inputs.sprint}, state={p.CurrentUiState}, focused={Application.isFocused}");
        p.Assert(inputs.sprint == sprint, label + " sprint状態");
        p.Assert(Mathf.Abs(speed - expected) <= 0.5f, label + " 水平速度±0.5m/s");
        p.Assert(controller.Grounded && Mathf.Abs(end.y - position.y) < 0.05f, label + " 水平な接地移動");
    }

    object ReadField(object instance, string field)
    {
        return instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    }

    #endregion
});

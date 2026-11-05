// R1の実測（R-14）: CEFありでUITKのポーズを開き、Webのポーズのボタン位置へのクリック・Esc・文字キーでWebがactionを出さないことを数える
// R1 measurement (R-14): with CEF up, open the UITK pause and count web actions caused by clicks on the web pause buttons, Esc and letter keys
// 数えるのは WebSocketMessageDispatcher に一時的に足す "[R14-MEASURE] action received" の行（計測後に外す）
// Counts the temporary "[R14-MEASURE] action received" lines added to WebSocketMessageDispatcher (removed after measuring)
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Cef;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-pause-web-action-count", options, async p =>
{
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    // WSスレッドからのログも拾うためThreadedで数える
    // Count via the threaded callback so logs from the WS thread are caught
    var actions = new ConcurrentQueue<string>();
    // DSLのUI位置問い合わせの応答(playtest.*)は計測器自身の通信なので数えない
    // Skip playtest.* replies to the DSL's UI locator queries; they are the measuring tool's own traffic
    Application.LogCallback collect = (condition, _, _) => { if (condition.StartsWith("[R14-MEASURE]") && !condition.Contains("'playtest.")) actions.Enqueue(condition); };
    Application.logMessageReceivedThreaded += collect;

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await p.Until(() => WebUiHost.Hub != null && WebUiCefConnection.TryGetConnection(WebUiHost.Hub, out _), 90f, "CEFのページがWSでつながる");
    await p.WaitSeconds(3f);
    p.Note($"ゲーム画面での基準: action {actions.Count} 件");
    var baseline = actions.Count;

    // UITKのポーズを開く
    // Open the UITK pause
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
    await p.UntilUiElement("pause-menu", 10f);
    await p.WaitSeconds(1f);
    var afterOpen = actions.Count;
    p.Note($"ポーズを開く操作で増えた action: {afterOpen - baseline}");
    p.Assert(UiInputExclusivity.IsPointerOverWebUi == false, "ポーズ中は Web の pointerOverUi が効かない");

    // Webのポーズのボタンがあった位置（参照キャプチャ 1-pause-top の 1280x720 座標を画面比で写す）をクリックする
    // Click where the web pause buttons were (1280x720 coordinates of reference capture 1-pause-top, scaled to the screen)
    var webButtonsAt720p = new[] { new Vector2(640, 323), new Vector2(640, 368), new Vector2(640, 412), new Vector2(640, 457), new Vector2(800, 260) };
    foreach (var at in webButtonsAt720p)
    {
        var screen = new Vector2(at.x / 1280f * Screen.width, Screen.height - at.y / 720f * Screen.height);
        SemanticInput.MouseMoveTo(screen);
        await p.WaitSeconds(0.2f);
        await SemanticInput.Click();
        await p.WaitSeconds(0.5f);
    }
    // クリックでUITKのボタンが押されていたらトップから外れているので戻す
    // A click may have hit a UITK button and left the top page; go back if so
    p.Note($"クリック 5 回の後: action {actions.Count - afterOpen} 件 / UI状態 {p.CurrentUiState}");
    await p.Screenshot("01-after-clicks");

    // 文字キーとEnterを打つ
    // Type letter keys and Enter
    var beforeKeys = actions.Count;
    foreach (var key in new[] { Key.A, Key.S, Key.D, Key.Space, Key.Enter, Key.Tab })
    {
        await SemanticInput.TapKey(key);
        await p.WaitSeconds(0.2f);
    }
    await p.WaitSeconds(1f);
    p.Note($"文字キー 6 打の後: action {actions.Count - beforeKeys} 件");

    // ポーズ中に Esc（設定ページからトップへ戻る Esc）
    // Esc while paused (the Esc that returns from settings to the top)
    if (p.CurrentUiState == UIStateEnum.PauseMenu)
    {
        await p.ClickUi("pause-menu-open-settings");
        await p.UntilUiElement("language-select", 10f);
        var beforeEsc = actions.Count;
        await p.PressKey(Key.Escape);
        await p.WaitSeconds(1f);
        p.Note($"設定ページでの Esc の後: action {actions.Count - beforeEsc} 件");
    }
    var whilePaused = actions.ToArray().Skip(baseline).ToList();
    p.Note($"ポーズ中の action 合計: {whilePaused.Count} 件 {string.Join(" / ", whilePaused)}");
    await p.Screenshot("02-before-close");

    // 閉じた後: Web側の値がポーズ前のまま効かないこと
    // After closing: the web-side values from before the pause do not take effect
    if (p.CurrentUiState == UIStateEnum.PauseMenu) await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.WaitSeconds(2f);
    p.Note($"閉じた後 2 秒: action {actions.Count - baseline - whilePaused.Count} 件 / pointerOverWeb={UiInputExclusivity.IsPointerOverWebUi} textFocused={UiInputExclusivity.IsTextInputFocused}");
    Application.logMessageReceivedThreaded -= collect;
    p.Assert(whilePaused.Count == 0, $"ポーズ中に Web が出した action が 0 件（実測 {whilePaused.Count}）");
});

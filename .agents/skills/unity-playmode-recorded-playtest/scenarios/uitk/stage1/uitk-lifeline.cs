// ラダー1 uitk-lifeline: Webホストを起動しない状態で、Esc→設定→バグ報告→セーブ→セーブして終了をUITKだけで通す
// Ladder 1 uitk-lifeline: with no web host, go Esc -> settings -> bug report -> save -> save and quit using UITK alone
// 実行前にPlayModeを止め、execute-dynamic-codeで UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ（全体スイッチの UITK 側。Web ホストを起動しない）
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh (UITK side of the global switch; no web host)
// 受け口URLはDI登録時に1回だけ読まれるので、MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9 もPlayMode起動前にEditorプロセスへ立てる
// The receiver URL is read once at DI registration, so also set MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9 in the Editor process before PlayMode starts
// 最後の「セーブして終了」でPlayModeが止まるため、セーブして終了の結果（save.jsonの更新）は呼び出し側のシェルで確かめる
// The final save-and-quit stops PlayMode, so its outcome (save.json updated) is checked by the calling shell
using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-lifeline", options, async p =>
{
    // 本番の受け口へ送らないよう、起動前に差し替えていなければ何も送らずに止める
    // Stop before sending anything unless the receiver was redirected before boot
    if (Environment.GetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE") != "http://127.0.0.1:9")
        throw new InvalidOperationException("MOORESTECH_PLAYTEST_RECEIVER_BASE が起動前に http://127.0.0.1:9 へ差し替えられていない（本番へ送るので中止）");

    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（hostless）");

    // Escでポーズを開く
    // Open the pause menu with Esc
    p.Note("Escでポーズ（UITK）を開く");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
    await p.UntilUiElement("pause-menu", 10f);
    await p.Screenshot("01-pause-top");

    // 設定で言語を日本語にして戻る
    // Switch the language to Japanese in settings and go back
    p.Note("設定で言語を日本語へ");
    await p.ClickUi("pause-menu-open-settings");
    await p.ClickUi("language-select-option-japanese");
    await p.UntilUiElement("pause-menu-locale-japanese", 15f);
    await p.Screenshot("02-settings-japanese");
    await p.ClickUi("pause-menu-back");

    // バグ報告: 種別バグ・日本語の本文を入れて送る
    // Bug report: kind bug, Japanese body, send
    p.Note("バグ報告を送る（受け口は到達しないURL）");
    var outboxBefore = ListBundles();
    await p.ClickUi("pause-menu-open-bug-report");
    await p.ClickUi("bug-report-kind-bug");
    await p.SetText("bug-report-description", "命綱の録画テスト。\nポーズからバグ報告を送る。");
    await p.Screenshot("03-bug-report-filled");
    await p.ClickUi("bug-report-send");
    await p.Until(() => ToastCount() > 0, 60f, "送信結果のトーストが出る");
    await p.Screenshot("04-bug-report-toast");

    // 書かれた箱の manifest に CEF の生の値が入っていること
    // The written bundle's manifest carries the raw CEF values
    var newBundle = ListBundles().Except(outboxBefore).OrderBy(d => d).LastOrDefault();
    p.Assert(newBundle != null, "新しいバグ報告の箱が outbox に書かれた");
    var manifest = File.ReadAllText(Path.Combine(newBundle, "manifest.json"));
    p.Note($"manifest: {newBundle}");
    p.Assert(manifest.Contains("\"webUiHealth\""), "manifest に webUiHealth がある");
    p.Assert(manifest.Contains("skippedByEditorSwitch"), "hostStartup は skippedByEditorSwitch");

    // トップへ戻り、セーブ→セーブして終了
    // Back to the top page, then save and save-and-quit
    await p.UntilUiElement("pause-menu-save", 15f);
    p.Note("セーブ");
    await p.ClickUi("pause-menu-save");
    await p.WaitSeconds(2f);
    await p.Screenshot("05-saved");
    p.Note("セーブして終了（PlayModeが止まる）");
    await p.ClickUi("pause-menu-save-and-quit");

    #region Internal

    string[] ListBundles()
    {
        var root = BugReportOutbox.DefaultRootDirectory;
        return Directory.Exists(root) ? Directory.GetDirectories(root) : Array.Empty<string>();
    }

    int ToastCount()
    {
        return GameUiRoot.Instance.GetLayer(UiLayer.Toast, UiFamily.Viewport).Query<VisualElement>(className: "toast-item").ToList().Count;
    }

    #endregion
});

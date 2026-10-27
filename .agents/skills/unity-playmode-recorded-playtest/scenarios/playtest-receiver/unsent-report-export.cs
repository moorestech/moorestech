// 送信できない報告の行と再送・書き出しボタンを実機CEFで描画し、書き出しのzipを確かめる
// Render the failed-report row with resend/export buttons in real CEF and verify the exported zip
//
// 前提: PlayModeに入る前に実outboxへ偽の箱 99991231_000000_recoveryplaytest（READY・UPLOAD_FAILED本文"playtest"・payload.txt）を1つ置く。
// 本文"playtest"は IsCompletionHold・BugReportBoxFilePolicy.CanResumeHold に当たらず、起動時の送信はこの箱を飛ばす。
// 開発者トークンのあるEditorは本番の受け口へ実送信するため「再送する」(bug-report-resend-failed)は絶対に押さない。
// 後片付け（偽の箱・デスクトップのzip・Finderの窓）は実行後にシェル側で必ず行う。
// Precondition: before PlayMode, place one fake box 99991231_000000_recoveryplaytest (READY, UPLOAD_FAILED body "playtest", payload.txt) in the real outbox.
// The body "playtest" matches neither IsCompletionHold nor BugReportBoxFilePolicy.CanResumeHold, so the startup run skips this box.
// An Editor holding a developer token really ships to the production receiver, so never press resend (bug-report-resend-failed).
// Cleanup (fake box, desktop zip, Finder window) is always done from the shell after the run.
using System.IO;
using System.IO.Compression;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Client.Playtest.WebUi;
using Client.PlaytestReceiver.Recovery;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

const string FakeBoxId = "99991231_000000_recoveryplaytest";
var fakeBox = Path.Combine(GameSystemPaths.BugReportOutboxDirectory, FakeBoxId);
if (!File.Exists(Path.Combine(fakeBox, "UPLOAD_FAILED"))) throw new System.Exception("偽の箱が未作成: " + fakeBox);

// 非フォーカスでも注入イベントをGameViewへ届け、Escape1回でポーズへ入れることを録画前に実測する
// Route injected events to GameView while unfocused and verify one Escape enters pause before recording
var inputSettings = InputSystem.settings;
inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
InputSystem.EnableDevice(Mouse.current);
InputSystem.EnableDevice(Keyboard.current);
var control = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
// 開幕スキットの開始が遅れることがあるため、ゲーム画面に着くまで飛ばしを繰り返す
// The opening skit may start late, so keep skipping until the game screen is reached
var skitDeadline = Time.realtimeSinceStartup + 60f;
while (control.CurrentState != UIStateEnum.GameScreen)
{
    if (skitDeadline < Time.realtimeSinceStartup) throw new System.Exception("ゲーム画面に着きません: " + control.CurrentState);
    await Client.Playtest.Operations.Ui.PlaytestSkitOps.SkipOpeningSkitIfPlaying(3f);
}
await Client.Playtest.Input.SemanticInput.TapKey(Key.Escape);
await UniTask.DelayFrame(5);
if (control.CurrentState != UIStateEnum.PauseMenu) throw new System.Exception("Escape入力の事前検証が失敗しました: " + control.CurrentState);
await Client.Playtest.Input.SemanticInput.TapKey(Key.Escape);
await UniTask.DelayFrame(5);

return PlaytestRunner.Run("unsent-report-export", new PlaytestRunOptions { Record = true }, async p =>
{
    // 非表示のGameViewが録画開始を止めないよう表タブへ出す
    // Bring GameView forward so hidden rendering cannot stall recording
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
    var gameView = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView");
    var dock = typeof(UnityEditor.EditorWindow).GetField("m_Parent", flags).GetValue(gameView);
    var panes = (System.Collections.IList)dock.GetType().GetField("m_Panes", flags).GetValue(dock);
    dock.GetType().GetProperty("selected", flags).SetValue(dock, panes.IndexOf(gameView));
    await p.Until(() => Time.timeScale > 0f, 15f, "録画開始後のゲーム進行");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.Screenshot("01-gameplay");

    // プレイヤーと同じくEscapeでポーズし、日本語表示にしてバグ報告画面を開く
    // Pause with Escape like a player, switch to Japanese, and open the bug report page
    p.Note("Escapeでポーズし、バグ報告画面を開きます");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
    await p.ClickWebUi("pause-menu-open-settings");
    await p.UntilWebUiElement("language-select", 15f);
    await p.ClickWebUi("language-select-option-japanese");
    await p.UntilWebUiElement("pause-menu-locale-japanese", 15f);
    await p.ClickWebUi("pause-menu-back");
    await p.ClickWebUi("pause-menu-open-bug-report");
    await p.UntilWebUiElement("bug-report-description", 15f);

    // 配送状況の行と、その下の2つのボタンをDOMで確かめる
    // Verify the delivery row and the two buttons below it through the DOM
    p.Note("配送状況に送信できない報告1件と、再送・書き出しの2ボタンが出ることを確かめます");
    await PlaytestWebUiOps.WaitWebUiTextContains("bug-report-delivery", "送信できない報告 1件", 60f);
    await p.UntilWebUiElement("bug-report-resend-failed", 15f);
    await p.UntilWebUiElement("bug-report-export-unsent", 15f);
    var delivery = await PlaytestDomQuery.Query("bug-report-delivery", 5f);
    var resend = await PlaytestDomQuery.Query("bug-report-resend-failed", 5f);
    var export = await PlaytestDomQuery.Query("bug-report-export-unsent", 5f);
    p.Note("配送状況: " + delivery.Text);
    p.Assert(delivery.Found && delivery.Text.Contains("送信できない報告 1件"), "配送状況に送信できない報告 1件");
    p.Assert(resend.Found && resend.Visible && resend.Text == "再送する", "再送するボタンが出る");
    p.Assert(export.Found && export.Visible && export.Text == "未送信の報告を書き出す", "未送信の報告を書き出すボタンが出る");
    p.Assert(resend.Y >= delivery.Y + delivery.Height - 1f && export.Y >= delivery.Y + delivery.Height - 1f, "2ボタンは配送状況の行の下");

    // 誤って再送を押さないよう、2ボタンの矩形が重ならないことをクリック前に保証する
    // Guarantee the two button rects are disjoint before clicking so resend can never be hit
    var overlap = export.X < resend.X + resend.Width && resend.X < export.X + export.Width &&
                  export.Y < resend.Y + resend.Height && resend.Y < export.Y + export.Height;
    p.Assert(!overlap, "再送と書き出しの矩形が重ならない");
    if (overlap) throw new System.Exception("再送ボタンと書き出しボタンの矩形が重なるためクリックを中止します");
    await p.Screenshot("02-recovery-buttons");

    // 書き出しを押し、完了の文言とデスクトップのzipパスを待つ
    // Press export and wait for the completion text with the desktop zip path
    p.Note("未送信の報告を書き出すを押します（再送するは押しません）");
    await p.ClickWebUi("bug-report-export-unsent");
    await p.Until(() => PlaytestExportStatusBoard.Current.Value is PlaytestExportDone || PlaytestExportStatusBoard.Current.Value is PlaytestExportFailed, 60f, "書き出しの終了");
    if (PlaytestExportStatusBoard.Current.Value is PlaytestExportFailed failed) throw new System.Exception("書き出し失敗: " + failed.Reason);
    var done = (PlaytestExportDone)PlaytestExportStatusBoard.Current.Value;
    var desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
    p.Note("zip: " + done.ZipPath);
    await PlaytestWebUiOps.WaitWebUiTextContains("bug-report-export-status", "書き出しました", 60f);
    var status = await PlaytestDomQuery.Query("bug-report-export-status", 5f);
    p.Note("書き出し状態: " + status.Text);
    p.Assert(status.Text.Contains(done.ZipPath), "書き出し状態にzipのパスが出る");
    p.Assert(Path.GetDirectoryName(done.ZipPath) == desktop && Path.GetFileName(done.ZipPath).StartsWith("moorestech-reports-"), "zipはデスクトップのmoorestech-reports-*.zip");
    p.Assert(done.BoxCount == 1 && done.Missing.Count == 0, "書き出したのは偽の箱1件で欠落なし");
    await p.Screenshot("03-export-done");

    // zipの中身を確かめ、再送が走っていない（失敗印が残る）ことも確かめる
    // Inspect the zip contents and confirm no resend ran (the failure marker remains)
    // 動的コンパイルでZipFileが別アセンブリの同名型に解決されるため、ZipArchiveを直接開く
    // Open ZipArchive directly because dynamic compilation can resolve ZipFile to a same-named type in another assembly
    using (var archive = new ZipArchive(File.OpenRead(done.ZipPath), ZipArchiveMode.Read))
    {
        var entries = archive.Entries.Select(e => e.FullName).ToArray();
        p.Note("zipの中身: " + string.Join(", ", entries));
        p.Assert(entries.Contains($"reports/{FakeBoxId}/payload.txt"), "zipに偽の箱のpayload.txt");
        p.Assert(entries.Contains("export.json"), "zipにexport.json");
    }
    p.Assert(File.Exists(Path.Combine(fakeBox, "UPLOAD_FAILED")) && !File.Exists(Path.Combine(fakeBox, "UPLOADED")), "偽の箱は再送されていない");
    await p.WaitSeconds(1.5f);
});

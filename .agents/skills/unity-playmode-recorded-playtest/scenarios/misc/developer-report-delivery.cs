using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Playtest;
using Client.Playtest.Operations;
using Client.Playtest.WebUi;
using Client.PlaytestReceiver.Delivery;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

return PlaytestRunner.Run("developer-report-delivery", new PlaytestRunOptions { Record = true }, async p =>
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
    await p.SkipOpeningSkit();
    await p.Screenshot("01-gameplay");
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var menu = resolver.Resolve<PauseMenuStateService>();
    var capture = resolver.Resolve<BugReportCaptureSession>();
    var outbox = GameSystemPaths.BugReportOutboxDirectory;
    Directory.CreateDirectory(outbox);
    var previous = Directory.GetDirectories(outbox);
    var hasToken = File.Exists(GameSystemPaths.PlaytestDeveloperTokenFilePath);

    // プレイヤーと同じポーズ画面で日本語表示を選ぶ
    // Select Japanese through the player's pause screen
    p.Note("ポーズ画面からバグ報告を開き、送信状況を確認します");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
    await p.ClickWebUi("pause-menu-open-settings");
    await p.UntilWebUiElement("language-select", 15f);
    await p.ClickWebUi("language-select-option-japanese");
    await p.UntilWebUiElement("pause-menu-locale-japanese", 15f);
    await p.PressKey(Key.Escape);
    await p.Until(() => menu.CurrentPage.Value == PauseMenuPage.Top, 15f, "設定から戻る");
    await p.ClickWebUi("pause-menu-open-bug-report");
    await p.UntilWebUiElement("bug-report-description", 15f);
    if (!hasToken)
    {
        await PlaytestWebUiOps.WaitWebUiTextContains("bug-report-delivery", "開発者トークンが未設定", 15f);
        p.Assert(PlaytestDeliveryStatusBoard.Current.Value is PlaytestDeliveryLocalOnly, "資格情報なしはローカル保存");
        await p.Screenshot("02-local-only");
    }

    // 実CEF入力で説明を書き、本番の送信処理を呼ぶ
    // Enter a description through CEF and invoke production submission
    await p.ClickWebUi("bug-report-description");
    p.Assert(CefScreenMapper.TryGetBrowser(out var browser), "CEF入力先が存在する");
    var description = hasToken ? "task10 developer production report" : "task10 local pending report";
    foreach (var character in description)
    {
        browser.SendCharEvent(character, 0);
        await UniTask.Yield();
    }
    await p.Until(() => capture.Status.Value.Kind == BugReportCaptureStatus.Ready, 45f, "報告資料の確保");
    p.Note(hasToken ? "本番受け口へ報告を送信します" : "未設定のため、この機材へ報告を保存します");
    await p.ClickWebUi("bug-report-send");
    await p.Until(() => Directory.GetDirectories(outbox).Except(previous).Any(d => File.Exists(Path.Combine(d, "READY"))), 120f, "新しいREADY箱");
    var box = Directory.GetDirectories(outbox).Except(previous).First(d => File.Exists(Path.Combine(d, "READY")));
    p.Note("報告ID: " + Path.GetFileName(box));
    await p.Until(() => menu.CurrentPage.Value == PauseMenuPage.Top, 15f, "送信後のトップ");
    await p.ClickWebUi("pause-menu-open-bug-report");
    if (hasToken)
    {
        if (PlaytestDeliveryStatusBoard.Current.Value is PlaytestDeliveryPending)
        {
            await PlaytestWebUiOps.WaitWebUiTextContains("bug-report-delivery", "送信待ち", 15f);
            await p.Screenshot("03-pending");
        }
        await p.Until(() => File.Exists(Path.Combine(box, "UPLOADED")), 240f, "本番への送信完了印");
        await PlaytestWebUiOps.WaitWebUiTextContains("bug-report-delivery", "すべての報告を送信しました", 30f);
        p.Assert(PlaytestDeliveryStatusBoard.Current.Value is PlaytestDeliveryAllSent, "全報告の送信が完了");
        await p.Screenshot("04-all-sent");
    }
    else
    {
        p.Assert(!File.Exists(Path.Combine(box, "UPLOADED")), "未設定時に送信しない");
        await PlaytestWebUiOps.WaitWebUiTextContains("bug-report-delivery", "開発者トークンが未設定", 15f);
        await p.Screenshot("03-local-saved");
    }
});

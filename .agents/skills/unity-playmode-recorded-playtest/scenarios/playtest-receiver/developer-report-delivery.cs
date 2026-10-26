using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Playtest;
using Client.Playtest.Operations;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using UniRx;

// 録画前にGameViewへフォーカスし単体入力を実測する
// Focus GameView and verify one input before recording
Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView").Focus();
await UniTask.DelayFrame(3);
var control = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
await Client.Playtest.Input.SemanticInput.TapKey(Key.Escape);
await UniTask.DelayFrame(3);
if (control.CurrentState != UIStateEnum.PauseMenu) throw new System.Exception("Escape入力の事前検証が失敗しました");
await Client.Playtest.Input.SemanticInput.TapKey(Key.Escape);
await UniTask.DelayFrame(3);

return PlaytestRunner.Run("developer-report-delivery", new PlaytestRunOptions { Record = false }, async p =>
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
    await Client.Playtest.Operations.Ui.PlaytestSkitOps.SkipOpeningSkitIfPlaying(3f);
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
    await p.ClickUi("pause-menu-open-settings");
    await p.UntilUiElement("language-select", 15f);
    await p.ClickUi("language-select-option-japanese");
    await p.UntilUiElement("pause-menu-locale-japanese", 15f);
    await p.ClickUi("pause-menu-back");
    await p.Until(() => menu.CurrentPage.Value == PauseMenuPage.Top, 15f, "設定から戻る");
    await p.ClickUi("pause-menu-open-bug-report");
    await p.UntilUiElement("bug-report-description", 15f);
    if (!hasToken)
    {
        await PlaytestUiElementOps.WaitUiTextContains("bug-report-delivery", "開発者トークンが未設定", 15f);
        p.Assert(PlaytestDeliveryStatusBoard.Current.Value is PlaytestDeliveryLocalOnly, "資格情報なしはローカル保存");
        await p.Screenshot("02-local-only");
    }

    // 実CEF入力で説明を書き、本番の送信処理を呼ぶ
    // Enter a description through CEF and invoke production submission
    await p.ClickUi("bug-report-description");
    var description = hasToken ? "task10 developer production report" : "task10 local pending report";
    await p.SetText("bug-report-description", description);
    await p.Until(() => capture.Status.Value.Kind == BugReportCaptureStatus.Ready, 45f, "報告資料の確保");
    p.Note(hasToken ? "本番受け口へ報告を送信します" : "未設定のため、この機材へ報告を保存します");
    FileStream pendingBoxLock = null;
    System.IDisposable pendingSubscription = null;
    try
    {
        if (hasToken)
        {
            // 検証用に新しい実箱をロックし、本番Runnerが通知したPendingを撮影可能に保つ
            // Fixture: lock the new real box to retain the Pending state emitted by the production runner
            p.Note("検証用に実箱を一時ロックして送信待ちを撮影し、解除後に本番配送します");
            pendingSubscription = PlaytestDeliveryStatusBoard.Current.Subscribe(status =>
            {
                if (status is not PlaytestDeliveryPending || pendingBoxLock != null) return; // silent-ok: 対象通知だけ処理 / 後続assertでロック取得を確認
                var readyBox = Directory.GetDirectories(outbox).Except(previous).FirstOrDefault(d => File.Exists(Path.Combine(d, "READY")));
                if (readyBox != null) pendingBoxLock = PlaytestOutboxAccess.AcquireBox(readyBox);
            });
        }
        await p.ClickUi("bug-report-send");
        await p.Until(() => Directory.GetDirectories(outbox).Except(previous).Any(d => File.Exists(Path.Combine(d, "READY"))), 120f, "新しいREADY箱");
        var box = Directory.GetDirectories(outbox).Except(previous).First(d => File.Exists(Path.Combine(d, "READY")));
        p.Note("報告ID: " + Path.GetFileName(box));
        await p.Until(() => menu.CurrentPage.Value == PauseMenuPage.Top, 15f, "送信後のトップ");
        await p.ClickUi("pause-menu-open-bug-report");
        if (hasToken)
        {
            p.Assert(pendingBoxLock != null, "検証用の実箱ロックを取得した");
            await PlaytestUiElementOps.WaitUiTextContains("bug-report-delivery", "送信待ち", 15f);
            p.Assert(!File.Exists(Path.Combine(box, "UPLOADED")), "検証用ロック中は未送信の実箱を保持する");
            await p.Screenshot("03-pending");
            pendingSubscription.Dispose();
            pendingSubscription = null;
            pendingBoxLock.Dispose();
            pendingBoxLock = null;
            p.Note("検証用ロックを解除し、本番Runnerへ送信を再要求します");
            resolver.Resolve<IPlaytestUploadRequester>().RequestUpload();
            await p.Until(() => File.Exists(Path.Combine(box, "UPLOADED")), 240f, "本番への送信完了印");
            await PlaytestUiElementOps.WaitUiTextContains("bug-report-delivery", "すべての報告を送信しました", 30f);
            p.Assert(PlaytestDeliveryStatusBoard.Current.Value is PlaytestDeliveryAllSent, "全報告の送信が完了");
            await p.Screenshot("04-all-sent");
        }
        else
        {
            p.Assert(!File.Exists(Path.Combine(box, "UPLOADED")), "未設定時に送信しない");
            await PlaytestUiElementOps.WaitUiTextContains("bug-report-delivery", "開発者トークンが未設定", 15f);
            await p.Screenshot("03-local-saved");
        }
    }
    finally
    {
        pendingSubscription?.Dispose();
        pendingBoxLock?.Dispose();
    }
});

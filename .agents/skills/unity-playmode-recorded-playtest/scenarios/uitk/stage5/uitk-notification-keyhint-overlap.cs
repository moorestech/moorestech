// 通知 4 件とキー操作ヒントの列の重なりを撮る（S2-ladder 表 D・S5-crosscut T29）。開始直後の GameScreen の実ゲームのヒントのまま 4 件を通知し、入りの後に 1280×720 と 2560×1080 で撮る
// winunity play <branch> <this> で流す。UITK の木の矩形と locator の矩形を記録する
// Shoot the overlap of four notifications and the key-hint column (S2-ladder table D, S5-crosscut T29): notify four rows over the real game's hints on the starting GameScreen and shoot after the enter at 1280x720 and 2560x1080
// Run with winunity play <branch> <this>; records UITK tree rects and locator rects
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Screens.KeyHints.Parts;
using Client.Ui.Screens.Notification;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Event.Notification;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("uitk-notification-keyhint-overlap", options, async p =>
{
    // Game View を表へ出してデバッグ環境を整える
    // Bring the Game View to front and set up the debug environment
    PlaytestGameViewOps.BringToFront();
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // Windows の PlayerPrefs は全 worktree 共通なので、元の言語を控えて終わりに戻す
    // Windows PlayerPrefs are shared by every worktree, so remember the language and restore it at the end
    var originalLanguage = Localize.GetCurrentLanguageCode();
    p.Note($"元の言語 {originalLanguage}");
    Localize.TrySetChosenLanguage("japanese");
    p.Assert(Localize.GetCurrentLanguageCode() == "japanese", "言語が日本語");

    // 前例 uitk-notification-compare と同じ 4 件（獲得・研究完了・アイテム解放・拒否）
    // The same four rows as the precedent uitk-notification-compare (earned, research completed, item unlocked, denied)
    var research = MasterHolder.ResearchMaster.GetAllResearches().First();
    var earned = NotificationMessagePack.CreateItemEarned(new ItemId(2), 5);
    var researchCompleted = NotificationMessagePack.CreateAchievementWithItem("achievement.researchCompleted", new Dictionary<string, string> { ["research"] = research.ResearchNodeGuid.ToString() }, new ItemId(1));
    var unlocked = NotificationMessagePack.CreateAchievementWithItem("achievement.unlockedItem", new Dictionary<string, string>(), new ItemId(2));
    var denied = NotificationMessagePack.CreateOperationDenied("denied.researchNotCompletable", new Dictionary<string, string>());

    // 途中の Assert で落ちても言語と解像度を戻す
    // Restore the language and resolution even when an Assert fails midway
    try
    {
        await p.SkipOpeningSkitIfPlaying();
        await p.ExitToGameScreen();
        var backgroundSkit = Object.FindFirstObjectByType<Client.Game.InGame.BackgroundSkit.BackgroundSkitManager>(FindObjectsInactive.Include);
        await p.Until(() => !backgroundSkit.IsPlayingSkit, 120f, "背景スキットが終わる");
        Object.FindFirstObjectByType<PlaytestOverlayView>().enabled = false;
        foreach (var (width, height) in new[] { (1280, 720), (2560, 1080) }) await Shoot(width, height);
    }
    finally
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
        Localize.TrySetChosenLanguage(originalLanguage);
        p.Note($"言語を戻した {Localize.GetCurrentLanguageCode()}");
    }

    #region Internal

    // 前の行が消えるのを待ち、4 件を通知して入りが終わってから矩形を記録して撮る
    // Wait for earlier rows to leave, notify four rows, record the rects after the enter and shoot
    async UniTask Shoot(int width, int height)
    {
        var res = $"{width}x{height}";
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await p.WaitSeconds(6f);
        p.Assert(Screen.width == width && Screen.height == height, $"Screen が {res} (実際 {Screen.width}x{Screen.height})");
        foreach (var message in new[] { earned, researchCompleted, unlocked, denied }) ClientDIContext.ClientLocalNotificationSource.Notify(message);

        // 入りの tween（160ms）が終わり行が不透明になるまで待つ
        // Wait until the enter tween (160ms) ends and the rows are opaque
        await p.Until(() => NotificationRows().Count == 4 && NotificationRows().All(row => row.resolvedStyle.opacity >= 0.999f), 10f, "通知 4 行が不透明");
        await p.WaitSeconds(1.0f);

        await NoteLocatorRect(res, "notification-host");
        await NoteLocatorRect(res, "key-hints");
        NoteTreeRects(res);
        await p.Screenshot($"{res}-notification-keyhint-overlap");
    }

    List<NotificationRowView> NotificationRows()
    {
        return GameUiRoot.Instance.GetLayer(UiLayer.BehindStage, UiFamily.Viewport).Query<NotificationRowView>().ToList();
    }

    // locator の矩形を画面の左上原点の px で記録する（ScreenRect は左下原点）
    // Record the locator rect in top-left-origin screen px (ScreenRect is bottom-left origin)
    async UniTask NoteLocatorRect(string res, string testid)
    {
        var snapshot = await PlaytestUiElementOps.FindUi(testid);
        p.Assert(snapshot.IsFound, $"{testid} が見つかる (状態 {snapshot.Status})");
        var rect = snapshot.ScreenRect;
        p.Note($"locator {res} {testid} x{rect.xMin:F1}-{rect.xMax:F1} y{Screen.height - rect.yMax:F1}-{Screen.height - rect.yMin:F1} 文 {snapshot.Text?.Replace("\n", "|")}");
    }

    // UITK の通知の行とヒントの行の worldBound（パネル座標）を記録する
    // Record the worldBound (panel coordinates) of each UITK notification row and key-hint row
    void NoteTreeRects(string res)
    {
        var scale = GameUiRoot.Instance.GetPanelDocument().panelSettings.scale;
        p.Note($"木 {res} パネルの scale 設定 {scale} Screen {Screen.width}x{Screen.height}");
        foreach (var row in NotificationRows()) p.Note($"木 {res} 通知の行 {row.worldBound} opacity {row.resolvedStyle.opacity}");
        var hintRows = GameUiRoot.Instance.GetRootElement().Q("key-hints").Query<KeyHintRow>().ToList();
        p.Note($"木 {res} ヒントの行数 {hintRows.Count}");
        foreach (var row in hintRows) p.Note($"木 {res} ヒントの行 {row.worldBound}");
    }

    #endregion
});

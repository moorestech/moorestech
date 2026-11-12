// 通知の並べ比べ撮影: GameUi（UITK）で参照 2-notifications と同じ 4 行（獲得・研究完了・アイテム解放・拒否）を日本語で出して 1280×720 と 2560×1080 の実画面を撮り、PanelSettings の複製に描いた通知の層を 1280×720 と 2560×1080 で撮る
// Notification comparison shots: in GameUi (UITK) show the same four rows as reference 2-notifications in Japanese, shoot the real 1280x720 and 2560x1080 screens, then shoot the notification layer drawn on a PanelSettings copy at 1280x720 and 2560x1080
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Client.Ui.State.Notification.Text;
using Client.WebUiHost.Boot;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Event.Notification;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("uitk-notification-compare", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（GameUi）");

    // 参照の元データ（W/e2e/mock-host/topics/topicControls.ts の notificationItemEarned・notificationAchievement・notificationItemUnlocked・notificationDenied）と同じ順・同じ中身。研究完了はアイテム 1 のアイコン付き
    // Same order and contents as the reference source data (topicControls.ts notification controls); research completed carries the item 1 icon
    Localize.TrySetChosenLanguage("japanese");
    var research = MasterHolder.ResearchMaster.GetAllResearches().First();
    var earned = NotificationMessagePack.CreateItemEarned(new ItemId(2), 5);
    var researchCompleted = NotificationMessagePack.CreateAchievementWithItem("achievement.researchCompleted", new Dictionary<string, string> { ["research"] = research.ResearchNodeGuid.ToString() }, new ItemId(1));
    var unlocked = NotificationMessagePack.CreateAchievementWithItem("achievement.unlockedItem", new Dictionary<string, string>(), new ItemId(2));
    var denied = NotificationMessagePack.CreateOperationDenied("denied.researchNotCompletable", new Dictionary<string, string>());

    // 実画面（世界ごと）の 720p。色はこの撮影で比べる（研究名は実マスタの最初の研究なので、文の長さだけ参照と違い得る）
    // The real 720p screen including the world; colors are compared here (the research name is the real master's first research, so only the text length may differ)
    await ShootRealScreen(1280, 720);
    // 実画面の 2560×1080 も撮る（舞台の倍率 1.5 と左端の実画面 16px を世界ごと見る）
    // Also shoot the real 2560x1080 screen (stage scale 1.5 and the 16 real px left edge, world included)
    await ShootRealScreen(2560, 1080);
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    // 位置と寸法は PanelSettings の複製に通知の層だけを描いて 2 解像度で撮る（本番の資産は書き換えない。S1-theme の撮影の前例）
    // Positions and sizes come from the notification layer alone drawn on a PanelSettings copy at two resolutions (the production asset is never rewritten; S1-theme capture precedent)
    p.Note($"1280x720 の複製パネル: {await CaptureOnPanelCopy(1280, 720, "uitk-2-notifications-panel-1280x720.png")}");
    p.Note($"2560x1080 の複製パネル: {await CaptureOnPanelCopy(2560, 1080, "uitk-2-notifications-panel-2560x1080.png")}");
    Localize.TrySetChosenLanguage("english");

    // Game View を指定の解像度にし、前の行が消えるのを待ってから 4 件を通知して入場の後に撮る
    // Set the Game View resolution, wait for earlier rows to leave, notify the four rows and shoot after the enter
    async UniTask ShootRealScreen(int width, int height)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await p.WaitSeconds(6f);
        p.Assert(Screen.width == width && Screen.height == height, $"Screen が {width}x{height} (実際 {Screen.width}x{Screen.height})");
        foreach (var message in new[] { earned, researchCompleted, unlocked, denied }) ClientDIContext.ClientLocalNotificationSource.Notify(message);
        await p.WaitSeconds(0.6f);
        await p.Screenshot($"uitk-2-notifications-{width}x{height}");
    }

    // 複製の PanelSettings に RenderTexture を付けた UIDocument を作り、層の並びの背面の層へ通知のホストを載せて 4 件を積み、入場の後に ReadPixels する
    // Make a UIDocument on a PanelSettings copy with a RenderTexture, mount the notification host in the behind-stage layer, push the four rows and ReadPixels after the enter
    async UniTask<string> CaptureOnPanelCopy(int width, int height, string fileName)
    {
        var panelCopy = Object.Instantiate(GameUiRoot.Instance.GetComponent<UIDocument>().panelSettings);
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        panelCopy.targetTexture = target;
        var documentObject = new GameObject($"notification-compare-{width}x{height}");
        var document = documentObject.AddComponent<UIDocument>();
        document.panelSettings = panelCopy;
        await UniTask.DelayFrame(1);

        // 本番のルートと同じロケールのクラスを当てる（GameUiLocaleClassBinder が GameUiRoot に当てるもの。日本語は CJK の字形と行送り。Typography.uss）
        // Apply the same locale class the production root gets (what GameUiLocaleClassBinder applies to GameUiRoot; Japanese uses CJK glyphs and leading; Typography.uss)
        document.rootVisualElement.EnableInClassList(GameUiLocaleClass.CjkClassName, GameUiLocaleClass.IsCjk(Localize.GetCurrentLanguageCode()));

        var model = new NotificationModel();
        var host = new NotificationHostView(model, new LocalizeNotificationTextLookup(), CompareRowIcons());
        UiLayerStack.Build(document.rootVisualElement).GetLayer(UiLayer.BehindStage, UiFamily.Viewport).Add(host);
        // 複製のパネルは解像度が固定なので、その倍率を 1 回だけ渡して左端を実画面 16px に置く
        // The copy panel has a fixed resolution, so pass its scale once to put the left edge at 16 real px
        host.SetStageScale(document.rootVisualElement.panel.scaledPixelsPerPoint);
        model.Current.Subscribe(host.Render);
        // 研究完了の行は参照のモックの研究名（W/e2e/mock-host/fixtures/contentLocalizationFixtures.ts の「最初の研究」）を文字の引数で渡し、参照と同じ文にする
        // The research row passes the reference mock's research name ("最初の研究" in contentLocalizationFixtures.ts) as a text argument so the text matches the reference
        model.AddNotification(NotificationContentFactory.FromMessage(earned));
        model.AddNotification(new AchievementNotificationContent("achievement.researchCompleted", new NotificationParam[] { new TextNotificationParam("research", "最初の研究") }, new ItemNotificationIcon(new ItemId(1))));
        model.AddNotification(NotificationContentFactory.FromMessage(unlocked));
        model.AddNotification(NotificationContentFactory.FromMessage(denied));
        await UniTask.Delay(600);

        var previousActive = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        RenderTexture.active = previousActive;
        Object.Destroy(documentObject);
        Object.Destroy(panelCopy);
        target.Release();

        var outputDirectory = Path.Combine(Application.dataPath, "..", "..", ".superpowers", "uitk-notification-compare");
        Directory.CreateDirectory(outputDirectory);
        var path = Path.GetFullPath(Path.Combine(outputDirectory, fileName));
        File.WriteAllBytes(path, texture.EncodeToPNG());
        return path;
    }

    // 本番と同じ素材の口で C3-a のアイコンを引く（C3-a 計画 Task の Produces。C3-a マージ後に実物と照合）
    // Resolve icons through the same texture source as production (C3-a plan Produces; verify against the real code after C3-a merges)
    INotificationRowIcons CompareRowIcons()
    {
        return new GameContentNotificationRowIcons(new GameContentPresentation(new ClientContextContentTextureSource()));
    }
});

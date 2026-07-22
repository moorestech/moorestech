// サーバー発通知基盤スモーク
// 1) 素材ゼロで研究完了を試みる→操作拒否(operationDenied)通知が右上に出る
// 2) 素材を付与して研究を完了→実績(achievement)通知「Research completed: 原始研究1」が出る
// 3) 同一失敗を3秒以内に連打→クールダウンで2回目はワイヤに乗らない(通知は増えない)
// Server-notification smoke:
// (1) complete-research with no materials -> operation-denied toast top-right
// (2) grant materials, complete research -> achievement toast "Research completed: 原始研究1"
// (3) burst the same failure within 3s -> cooldown suppresses the 2nd delivery
// 検証は「クライアント側でEventTagを購読して実ペイロードをassert」+「Web UIホストの可視化Until」+「録画/スクショ」の3点で行う
// Verification uses a client-side EventTag subscription (asserts the real payload) + Web-UI host visibility Until + video/screenshots
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.Notification;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("server-notification-smoke", options, async p =>
{
    // Web UI(CEF)前提を整備
    // Prepare Web-UI (CEF) prerequisites
    p.Note("デバッグ環境を構築し、通知イベントをクライアント側で購読する");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // 初回研究ノード(原始研究1)
    // First research node (原始研究1)
    var firstResearchGuid = Guid.Parse("837e9697-8586-406e-a0f6-16a010050218");

    // EventTagをクライアントで購読し、届いた通知の実ペイロードを蓄積する(NotificationTopicとは別の観測用購読)
    // Subscribe to the EventTag on the client and accumulate delivered payloads (observer sub, separate from NotificationTopic)
    var received = new List<NotificationMessagePack>();
    var subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(NotificationService.EventTag, payload =>
    {
        received.Add(MessagePackSerializer.Deserialize<NotificationMessagePack>(payload));
    });

    int DeniedResearchCount() => received.Count(n => n.Category == NotificationCategory.OperationDenied && n.MessageId == "denied.researchNotCompletable");
    int AchievementResearchCount() => received.Count(n => n.Category == NotificationCategory.Achievement && n.MessageId == "achievement.researchCompleted");

    // === Goal1+3: 拒否通知とクールダウン ===
    // === Goal1+3: denied notification and cooldown ===
    p.Note("素材ゼロで研究完了を試みる(1回目)。操作拒否通知が届くはず");
    ClientContext.VanillaApi.SendOnly.CompleteResearch(firstResearchGuid);
    await p.Until(() => 1 <= DeniedResearchCount(), 10f, "1回目の失敗で操作拒否通知(denied.researchNotCompletable)がクライアントに届く");

    // クールダウン窓内に同一失敗を連打
    // Burst the same failure inside the cooldown window
    var deniedAfterFirst = DeniedResearchCount();
    p.Note("同一失敗を3秒以内に連打(2回目)。クールダウンで通知は増えないはず");
    ClientContext.VanillaApi.SendOnly.CompleteResearch(firstResearchGuid);
    await p.WaitSeconds(1.8f);
    p.Assert(DeniedResearchCount() == deniedAfterFirst, $"クールダウンで2回目の失敗通知は抑制される(件数 {deniedAfterFirst} のまま 実際:{DeniedResearchCount()})");

    // 通知ホストの可視化を待って撮影
    // Wait for the notification host to become visible, then capture
    await p.UntilWebUiElement("notification-host", 10f);
    p.Assert(received.Last().MessageId == "denied.researchNotCompletable", $"直近通知が操作拒否である 実際:{received.Last().MessageId}");
    await p.Screenshot("01-denied-notification");

    // クールダウン明けを待ち、次フェーズ(実績)の観測を汚さない
    // Wait out the cooldown so the achievement phase observation stays clean
    await p.WaitSeconds(3.2f);

    // === Goal2: 実績通知 ===
    // === Goal2: achievement notification ===
    p.Note("研究に必要な素材(木の板x5・木の棒x5)を付与する");
    p.GiveItemDirect("木の板", 5);
    p.GiveItemDirect("木の棒", 5);
    await p.WaitSeconds(0.5f);

    var achievementBefore = AchievementResearchCount();
    p.Note("研究を完了する。実績通知『Research completed: 原始研究1』が届くはず");
    ClientContext.VanillaApi.SendOnly.CompleteResearch(firstResearchGuid);
    await p.Until(() => achievementBefore < AchievementResearchCount(), 10f, "研究完了で実績通知(achievement.researchCompleted)がクライアントに届く");

    // 実ペイロードの研究名パラメータまで検証する(Web側テンプレ「Research completed: {p0}」に流し込まれる値)
    // Assert the actual research-name param carried in the payload (fed into the web template "Research completed: {p0}")
    var achievement = received.Last(n => n.Category == NotificationCategory.Achievement && n.MessageId == "achievement.researchCompleted");
    p.Assert(1 <= achievement.MessageParams.Length && achievement.MessageParams[0] == "原始研究1", $"実績通知の研究名パラメータが『原始研究1』 実際:{string.Join(",", achievement.MessageParams)}");

    await p.UntilWebUiElement("notification-host", 10f);
    await p.Screenshot("03-achievement-notification");

    // 後片付け: 観測用購読を破棄する
    // Cleanup: dispose the observer subscription
    subscription.Dispose();
    p.Note("サーバー通知基盤スモーク完了(操作拒否・実績・クールダウンを確認)");
});

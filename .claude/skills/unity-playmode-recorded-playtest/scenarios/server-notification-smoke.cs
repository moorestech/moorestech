// サーバー発通知基盤スモーク(Web UI経路)
// 1) 素材ゼロで研究完了を試みる→操作拒否(operationDenied)通知が右上に出る
// 2) 素材を付与して研究を完了→実績(achievement)通知「Research completed: 原始研究1」が出る
// 3) 同一失敗を3秒以内に連打→クールダウンで2回目はワイヤに乗らない(通知は増えない)
// Server-notification smoke (Web UI route):
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
    // Web UI(CEF)モードの前提を整える。SetupDebugEnvironmentで足場+ワープ+無料設置を一括構築
    // Prepare the Web-UI (CEF) prerequisites; SetupDebugEnvironment builds ground + warp + free placement in one line
    p.Note("デバッグ環境を構築し、通知イベントをクライアント側で購読する");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // 最初の研究ノード(原始研究1)。前提なし・木の板x5/木の棒x5を消費
    // First research node (原始研究1): no prerequisites, consumes 5x wooden board + 5x wooden stick
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

    // === Goal 1 + Goal 3: 操作拒否通知 と クールダウン ===
    // === Goal 1 + Goal 3: operation-denied notification and cooldown ===
    p.Note("素材ゼロで研究完了を試みる(1回目)。操作拒否通知が届くはず");
    ClientContext.VanillaApi.SendOnly.CompleteResearch(firstResearchGuid);
    await p.Until(() => DeniedResearchCount() >= 1, 10f, "1回目の失敗で操作拒否通知(denied.researchNotCompletable)がクライアントに届く");

    // クールダウン窓(3秒)内に同一失敗を連打する。2回目はワイヤに乗らないはず
    // Burst the same failure inside the 3s cooldown window; the 2nd must not reach the wire
    var deniedAfterFirst = DeniedResearchCount();
    p.Note("同一失敗を3秒以内に連打(2回目)。クールダウンで通知は増えないはず");
    ClientContext.VanillaApi.SendOnly.CompleteResearch(firstResearchGuid);
    await p.WaitSeconds(1.8f);
    p.Assert(DeniedResearchCount() == deniedAfterFirst, $"クールダウンで2回目の失敗通知は抑制される(件数 {deniedAfterFirst} のまま 実際:{DeniedResearchCount()})");

    // Web UI右上の通知ホストが可視(=通知が描画された)になるのを待って撮影する
    // Wait until the top-right Web-UI notification host becomes visible (a notification is rendered), then capture
    await p.UntilWebUiElement("notification-host", 10f);
    p.Assert(received.Last().MessageId == "denied.researchNotCompletable", $"直近通知が操作拒否である 実際:{received.Last().MessageId}");
    await p.Screenshot("01-denied-notification");

    // クールダウン明けを待ち、次フェーズ(実績)の観測を汚さない
    // Wait out the cooldown so the achievement phase observation stays clean
    await p.WaitSeconds(3.2f);

    // === Goal 2: 実績通知(研究完了) ===
    // === Goal 2: achievement notification (research completed) ===
    p.Note("研究に必要な素材(木の板x5・木の棒x5)を付与する");
    p.GiveItemDirect("木の板", 5);
    p.GiveItemDirect("木の棒", 5);
    await p.WaitSeconds(0.5f);

    var achievementBefore = AchievementResearchCount();
    p.Note("研究を完了する。実績通知『Research completed: 原始研究1』が届くはず");
    ClientContext.VanillaApi.SendOnly.CompleteResearch(firstResearchGuid);
    await p.Until(() => AchievementResearchCount() > achievementBefore, 10f, "研究完了で実績通知(achievement.researchCompleted)がクライアントに届く");

    // 実ペイロードの研究名パラメータまで検証する(Web側テンプレ「Research completed: {p0}」に流し込まれる値)
    // Assert the actual research-name param carried in the payload (fed into the web template "Research completed: {p0}")
    var achievement = received.Last(n => n.Category == NotificationCategory.Achievement && n.MessageId == "achievement.researchCompleted");
    p.Assert(achievement.MessageParams.Length >= 1 && achievement.MessageParams[0] == "原始研究1", $"実績通知の研究名パラメータが『原始研究1』 実際:{string.Join(",", achievement.MessageParams)}");

    await p.UntilWebUiElement("notification-host", 10f);
    await p.Screenshot("03-achievement-notification");

    // 後片付け: 観測用購読を破棄する
    // Cleanup: dispose the observer subscription
    subscription.Dispose();
    p.Note("サーバー通知基盤スモーク完了(操作拒否・実績・クールダウンを確認)");
});

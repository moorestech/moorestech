using System;
using System.Collections.Generic;
using System.Linq;
using Game.Challenge;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class AchievementNotificationWiringTest
    {
        [Test]
        public void ResearchCompleteFiresAchievementNotification()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            CompleteResearchForTest(serviceProvider, Research1Guid);

            // 研究完了は連鎖アンロック通知も同時に飛び得るため件数はMessageId単位で判定
            // Research completion may also fire chained unlock notifications, so assert by MessageId count
            var notifications = TakeAchievementNotifications(sink, "achievement.researchCompleted");
            Assert.AreEqual(1, notifications.Count);

            // 表示名でなくGuidを送るワイヤ契約を固定する（Web側辞書解決の前提）
            // Pin the wire contract: GUIDs are sent, not display names, for web-side dictionary resolution
            Assert.AreEqual(new[] { Research1Guid.ToString("D") }, notifications[0].MessageParams);
        }

        [Test]
        public void ChallengeCompleteFiresAchievementNotificationWithGuidParam()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            serviceProvider.GetService<ChallengeDatastore>().InitializeCurrentChallenges();

            ChallengeCompletedEventTest.ClearCraftChallenge(packet, serviceProvider);

            // クラフトチャレンジ完了時もアンロック通知が同時に飛ぶためMessageIdで絞る
            // Unlock notifications also fire on craft challenge completion, so narrow by MessageId
            var notifications = TakeAchievementNotifications(sink, "achievement.challengeCompleted");
            Assert.AreEqual(1, notifications.Count);

            // 期待値はテスト側のGuidリテラルから組み、本番の送出式に依存させない
            // The expectation is built from a test-side GUID literal, not from the production expression
            var craftChallengeGuid = new Guid("00000000-0000-0000-4567-000000000001");
            Assert.AreEqual(new[] { craftChallengeGuid.ToString("D") }, notifications[0].MessageParams);
        }

        private static List<NotificationMessagePack> TakeAchievementNotifications(CapturedEventSink sink, string messageId)
        {
            return sink.TakeAll()
                .Where(e => e.Tag == NotificationService.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload))
                .Where(data => data.Category == NotificationCategory.Achievement && data.MessageId == messageId)
                .ToList();
        }
    }
}

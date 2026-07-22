using System.Linq;
using MessagePack;
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
            var notifications = sink.TakeAll().Where(e => e.Tag == NotificationService.EventTag).ToList();
            Assert.AreEqual(1, notifications.Count(e =>
            {
                var data = MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload);
                return data.Category == NotificationCategory.Achievement && data.MessageId == "achievement.researchCompleted";
            }));
        }
    }
}

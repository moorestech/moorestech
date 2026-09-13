using Game.SaveLoad.Interface;
using Game.SaveLoad.Pruning;
using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Event.Notification;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class MissingMasterPruneNotificationTest
    {
        // 接続したプレイヤーへ1回だけ届くこと。除去はロード時に終わっており後から変化しない
        // Arrives exactly once for a connecting player; pruning finished at load and never changes afterwards
        [Test]
        public void 除去があるとsink登録時に通知が1件届くTest()
        {
            var provider = new EventProtocolProvider();
            var reportStore = new MissingMasterPruneReportStore();
            reportStore.SetReport(new MissingMasterPruneReport(3, 4, 5));
            new MissingMasterPruneNotificationWiring(new NotificationService(provider), provider, reportStore).Load();

            var sink = new CapturedEventSink();
            provider.RegisterPlayer(1, sink);

            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual(NotificationService.EventTag, sink.Events[0].Tag);
            var message = MessagePackSerializer.Deserialize<NotificationMessagePack>(sink.Events[0].Payload);
            Assert.AreEqual(NotificationCategory.SaveMigration, message.Category);
            Assert.AreEqual("saveMigration.missingMasterPruned", message.MessageId);
            Assert.AreEqual(new[] { "3", "4", "5" }, message.MessageParams);
        }

        // 除去0件（本番の常態）で通知が出ないこと
        // Nothing removed, the normal case, must produce no notification
        [Test]
        public void 除去が無いと通知が出ないTest()
        {
            var provider = new EventProtocolProvider();
            var reportStore = new MissingMasterPruneReportStore();
            new MissingMasterPruneNotificationWiring(new NotificationService(provider), provider, reportStore).Load();

            var sink = new CapturedEventSink();
            provider.RegisterPlayer(1, sink);

            Assert.AreEqual(0, sink.Events.Count);
        }

        // 2人目の接続にも届くこと（ブロードキャストではなく接続ごとのpushである確認）
        // The second player also receives it, proving this is a per-connection push, not a broadcast
        [Test]
        public void 後から接続したプレイヤーにも届くTest()
        {
            var provider = new EventProtocolProvider();
            var reportStore = new MissingMasterPruneReportStore();
            reportStore.SetReport(new MissingMasterPruneReport(1, 0, 0));
            new MissingMasterPruneNotificationWiring(new NotificationService(provider), provider, reportStore).Load();

            provider.RegisterPlayer(1, new CapturedEventSink());
            var secondSink = new CapturedEventSink();
            provider.RegisterPlayer(2, secondSink);

            Assert.AreEqual(1, secondSink.Events.Count);
        }
    }
}

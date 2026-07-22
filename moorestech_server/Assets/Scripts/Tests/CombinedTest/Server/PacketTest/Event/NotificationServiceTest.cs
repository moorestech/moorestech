using System;
using System.Linq;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class NotificationServiceTest
    {
        private const int PlayerId = 1;

        [Test]
        public void NotifySendsEventAndCooldownSuppressesDuplicate()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var service = serviceProvider.GetService<NotificationService>();

            // 同一キー2連打は1件だけ通る
            // Two same-key bursts pass only once
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.denied", new[] { "p0" }));
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.denied", new[] { "p0" }));
            var events = sink.TakeAll().Where(e => e.Tag == NotificationService.EventTag).ToList();
            Assert.AreEqual(1, events.Count);

            var data = MessagePackSerializer.Deserialize<NotificationMessagePack>(events[0].Payload);
            Assert.AreEqual(NotificationCategory.OperationDenied, data.Category);
            Assert.AreEqual("test.denied", data.MessageId);
            Assert.AreEqual("p0", data.MessageParams[0]);
        }

        [Test]
        public void DifferentMessageIdPassesAndZeroCooldownAllowsRepeat()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var service = serviceProvider.GetService<NotificationService>();

            // 別MessageIdは抑制されない
            // A different MessageId is not suppressed
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.a", Array.Empty<string>()));
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.b", Array.Empty<string>()));
            Assert.AreEqual(2, sink.TakeAll().Count(e => e.Tag == NotificationService.EventTag));

            // クールダウン0なら同一キーも再送される
            // With zero cooldown the same key is sent again
            service.SetCooldownDuration(TimeSpan.Zero);
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.a", Array.Empty<string>()));
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.a", Array.Empty<string>()));
            Assert.AreEqual(2, sink.TakeAll().Count(e => e.Tag == NotificationService.EventTag));
        }

        [Test]
        public void NotifyAllBroadcastsToRegisteredSink()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var service = serviceProvider.GetService<NotificationService>();

            service.NotifyAll(NotificationMessagePack.CreateAchievement("test.achievement", Array.Empty<string>()));
            var events = sink.TakeAll().Where(e => e.Tag == NotificationService.EventTag).ToList();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(NotificationCategory.Achievement, MessagePackSerializer.Deserialize<NotificationMessagePack>(events[0].Payload).Category);
        }
    }
}

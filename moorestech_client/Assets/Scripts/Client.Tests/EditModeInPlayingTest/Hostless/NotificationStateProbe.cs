using System.Collections.Generic;
using Client.Game.InGame.UI.Notification;
using NUnit.Framework;
using Server.Event.Notification;
using UniRx;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // 拒否通知が連番でfeedに流れる
    // A refusal flows through the feed with a sequence
    internal static class NotificationStateProbe
    {
        public static void AssertAvailable(IObjectResolver resolver)
        {
            var feed = resolver.Resolve<NotificationFeed>();
            var received = new List<SequencedNotification>();
            using var subscription = feed.OnNotified.Subscribe(received.Add);

            var message = NotificationMessagePack.CreateOperationDenied("denied.undoRestoreSkipped", new System.Collections.Generic.Dictionary<string, string> { ["count"] = "2" });
            resolver.Resolve<ClientLocalNotificationSource>().Notify(message);

            Assert.AreEqual(1, received.Count, "ローカル通知がfeedに流れない");
            Assert.AreSame(message, received[0].Message);
            Assert.Greater(received[0].Seq, 0);
        }
    }
}

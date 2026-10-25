using System.Collections.Generic;
using Client.Game.InGame.UI.Notification;
using NUnit.Framework;
using Server.Event.Notification;
using UniRx;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // ローカルの拒否通知がWebホスト無しでも連番付きでfeedに流れることを確かめる
    // Verifies a local refusal flows through the feed with a sequence number, without the web host
    internal static class NotificationStateProbe
    {
        public static void AssertAvailable(IObjectResolver resolver)
        {
            var feed = resolver.Resolve<NotificationFeed>();
            var received = new List<SequencedNotification>();
            using var subscription = feed.OnNotified.Subscribe(received.Add);

            var message = NotificationMessagePack.CreateOperationDenied("denied.undoRestoreSkipped", new[] { "2" });
            resolver.Resolve<ClientLocalNotificationSource>().Notify(message);

            Assert.AreEqual(1, received.Count, "ローカル通知がfeedに流れない");
            Assert.AreSame(message, received[0].Message);
            Assert.Greater(received[0].Seq, 0);
        }
    }
}

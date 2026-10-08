using System;
using System.Text.RegularExpressions;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy;
using Client.Game.InGame.UI.Notification;
using NUnit.Framework;
using Server.Event.Notification;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintCreateFailureNotifierTest
    {
        [Test]
        public void 作成失敗はログと理由別通知の両方へ出る()
        {
            var source = new ClientLocalNotificationSource();
            NotificationMessagePack observed = null;
            using var subscription = source.OnNotification.Subscribe(message => observed = message);
            var result = BlueprintCreateResult.Rejected(BlueprintFailureReason.EmptyArea);
            LogAssert.Expect(LogType.Error, new Regex("create rejected: EmptyArea"));

            BlueprintCreateFailureNotifier.NotifyFailure(result, source, Vector3Int.zero, Vector3Int.one, "empty");

            Assert.NotNull(observed);
            Assert.AreEqual(NotificationCategory.OperationDenied, observed.Category);
            Assert.AreEqual("denied.blueprintCreate.EmptyArea", observed.MessageId);
            Assert.AreEqual(0, observed.MessageParams.Length);
        }
    }
}

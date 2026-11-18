using System.Text.RegularExpressions;
using Client.Game.InGame.UI.Notification;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands.Player;
using Core.Master;
using NUnit.Framework;
using Server.Event.Notification;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// Shift の断りが Web と同じ messageId・分類の通知になり、満杯だけは通知せずログに残す（L0）
    /// Shift refusals become notifications with the Web messageId and category; only destination-full skips the notification and logs (L0)
    /// </summary>
    public class ShiftMoveRefusalNotifierTest
    {
        private ClientLocalNotificationSource _source;
        private ShiftMoveRefusalNotifier _notifier;
        private NotificationMessagePack _notified;

        [SetUp]
        public void SetUp()
        {
            _source = new ClientLocalNotificationSource();
            _notifier = new ShiftMoveRefusalNotifier(_source);
            _notified = null;
            _source.OnNotification.Subscribe(m => _notified = m);
        }

        [TestCase(ShiftMoveRefusalReason.NoAcceptingBlockSlot, "denied.inventoryMoveSlotRejected", "noAcceptingBlockSlot")]
        [TestCase(ShiftMoveRefusalReason.InventoryOpenFailed, "denied.inventoryOpenFailed", "inventoryOpenFailed")]
        [TestCase(ShiftMoveRefusalReason.DetailLoading, "denied.inventoryDetailLoading", "detailLoading")]
        public void 理由ごとのmessageIdで操作拒否の通知を出す(ShiftMoveRefusalReason reason, string messageId, string logName)
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[inventory\.shift_move\] not sent: " + logName));
            _notifier.Notify(reason, ItemMaster.EmptyItemId, "(1, 0, 0)");
            Assert.AreEqual(messageId, _notified.MessageId);
            Assert.AreEqual(NotificationCategory.OperationDenied, _notified.Category);
        }

        [Test]
        public void 宛先が満杯の断りは通知せずログだけ残す()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[inventory\.shift_move\] not sent: destinationFull"));
            _notifier.Notify(ShiftMoveRefusalReason.DestinationFull, ItemMaster.EmptyItemId, "(1, 0, 0)");
            Assert.IsNull(_notified);
        }
    }
}

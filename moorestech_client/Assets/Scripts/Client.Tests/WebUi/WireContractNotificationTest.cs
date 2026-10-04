using Client.Game.InGame.UI.Notification;
using System.IO;
using Client.Tests.Inventory;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using MessagePack;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Event.Notification;
using UnityEngine;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// itemEarnedのwire契約を検証
    /// Verifies the notification.events itemEarned wire contract
    /// </summary>
    public class WireContractNotificationTest
    {
        [Test]
        public void ItemEarnedFixtureMatchesDto()
        {
            var dto = new NotificationDto
            {
                Seq = 1,
                Category = "itemEarned",
                MessageId = "itemEarned.mined",
                MessageParams = System.Array.Empty<string>(),
                ItemId = 5,
                Count = 8,
            };

            AssertMatchesFixture(dto, "notification_item_earned.json");
        }

        [Test]
        public void MessageNotificationOmitsItemIdAndCount()
        {
            // countとitemIdを持つのは獲得通知だけ。他カテゴリはキーごと省略しWeb側の判別unionを保つ
            // Only earned notifications carry count and itemId; other categories omit the keys entirely to keep the web's union honest
            var dto = new NotificationDto
            {
                Seq = 2,
                Category = "operationDenied",
                MessageId = "denied.miningInventoryFull",
                MessageParams = System.Array.Empty<string>(),
                ItemId = null,
                Count = null,
            };

            var json = JToken.Parse(WebUiJson.Serialize(dto));
            Assert.IsNull(json["count"]);
            Assert.IsNull(json["itemId"]);
        }

        [Test]
        public void SaveMigrationNoticeIsServedInSnapshotMatchingFixture()
        {
            // 実topicへサーバー通知を流し、配信形とsnapshot再提示の両方をfixtureで固定する
            // Feed a server notification through the real topic and pin both the wire shape and the snapshot re-serve
            var vanillaApiEvent = new CapturingVanillaApiEvent();
            var topic = new NotificationTopic(new WebSocketHub(), vanillaApiEvent, new ClientLocalNotificationSource());
            vanillaApiEvent.Dispatch(NotificationService.EventTag, MessagePackSerializer.Serialize(NotificationMessagePack.CreateSaveMigrationPruned(3, 4, 5)));

            AssertMatchesFixture(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult(), "notification_save_migration.json");
        }

        [Test]
        public void TransientNotificationIsNotServedInSnapshot()
        {
            // 除去告知以外は揮発。snapshotへ載せると購読し直しのたびに再表示される
            // Everything but the prune notice is transient; serving it in the snapshot would re-show it on every resubscribe
            var vanillaApiEvent = new CapturingVanillaApiEvent();
            var topic = new NotificationTopic(new WebSocketHub(), vanillaApiEvent, new ClientLocalNotificationSource());
            vanillaApiEvent.Dispatch(NotificationService.EventTag, MessagePackSerializer.Serialize(NotificationMessagePack.CreateOperationDenied("denied.miningInventoryFull", System.Array.Empty<string>())));

            Assert.AreEqual("{}", topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
        }

        [Test]
        public void LocalUndoNotificationIsPublishedAndUnsubscribedOnDispose()
        {
            var hub = new WebSocketHub();
            var source = new ClientLocalNotificationSource();
            var topic = new NotificationTopic(hub, new CapturingVanillaApiEvent(), source);
            var message = NotificationMessagePack.CreateOperationDenied("denied.undoRestoreSkipped", new[] { "2" });

            // ローカル拒否も配信されるが再購読では再生しない
            // Local refusals publish but are not replayed upon resubscription
            source.Notify(message);
            Assert.AreEqual(1, hub.GetTopicRevision(NotificationTopic.TopicName));
            Assert.AreEqual("{}", topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());

            // topicの再登録で古いローカル購読が残らない
            // Rebinding a topic must not leave its old local subscription active
            topic.Dispose();
            source.Notify(message);
            Assert.AreEqual(1, hub.GetTopicRevision(NotificationTopic.TopicName));
        }

        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            AssertMatchesFixture(WebUiJson.Serialize(dto), fixtureName);
        }

        private static void AssertMatchesFixture(string actualJson, string fixtureName)
        {
            var actual = JToken.Parse(actualJson);
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }
    }
}

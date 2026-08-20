using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
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

        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }
    }
}

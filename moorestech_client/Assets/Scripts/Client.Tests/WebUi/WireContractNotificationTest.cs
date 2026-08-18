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

        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }
    }
}

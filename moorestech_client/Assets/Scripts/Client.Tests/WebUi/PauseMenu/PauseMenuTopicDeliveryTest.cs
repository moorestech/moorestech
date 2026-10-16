using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.PlaytestReceiver.Delivery;
using Client.Tests.BugReport;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class PauseMenuTopicDeliveryTest
    {
        [TearDown]
        public void Reset() { PlaytestDeliveryStatusBoard.ResetOnPlayMode(); }

        [TestCase(PlaytestLocalOnlyReason.DeveloperTokenMissing, "developerTokenMissing")]
        [TestCase(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, "developerTokenUnreadable")]
        [TestCase(PlaytestLocalOnlyReason.DistributionUnavailable, "distributionUnavailable")]
        public void ローカル保存理由を契約どおりに送る(PlaytestLocalOnlyReason reason, string wireReason)
        {
            AssertDelivery(new PlaytestDeliveryLocalOnly(reason, new PlaytestOutboxDeliveryCounts(3, 2)), new JObject { ["kind"] = "localOnly", ["inventory"] = new JObject { ["kind"] = "known", ["pendingCount"] = 3, ["failedCount"] = 2 }, ["reason"] = wireReason });
        }

        [Test]
        public void 送信待ちと拒否と完了を区別して送る()
        {
            AssertDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(2, 3)), new JObject { ["kind"] = "pending", ["inventory"] = new JObject { ["kind"] = "known", ["pendingCount"] = 2, ["failedCount"] = 3 } });
            AssertDelivery(new PlaytestDeliveryRefused(new PlaytestOutboxDeliveryCounts(1, 4)), new JObject { ["kind"] = "refused", ["inventory"] = new JObject { ["kind"] = "known", ["pendingCount"] = 1, ["failedCount"] = 4 } });
            AssertDelivery(PlaytestDeliveryAllSent.Instance, new JObject { ["kind"] = "allSent" });
        }

        [Test]
        public void 件数不明は架空の件数を含めず理由を送る()
        {
            var inventory = new PlaytestOutboxInventoryUnavailable("access denied");
            var wire = new JObject { ["kind"] = "unavailable", ["reason"] = "access denied" };
            AssertDelivery(new PlaytestDeliveryPending(inventory), new JObject { ["kind"] = "pending", ["inventory"] = wire });
            AssertDelivery(new PlaytestDeliveryRefused(inventory), new JObject { ["kind"] = "refused", ["inventory"] = wire.DeepClone() });
            AssertDelivery(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenMissing, inventory), new JObject { ["kind"] = "localOnly", ["reason"] = "developerTokenMissing", ["inventory"] = wire.DeepClone() });
        }

        private static void AssertDelivery(PlaytestDeliveryStatus status, JObject expected)
        {
            // 購読配信とスナップショットを検証
            // Verify status-change publication and the snapshot together
            var hub = new WebSocketHub();
            using var topic = new PauseMenuTopic(hub, new NetworkDisconnectState(), new BugReportCaptureSession(new NullBugReportCaptureSources()), new PauseMenuStateService());
            var before = hub.GetTopicRevision(PauseMenuTopic.TopicName);
            PlaytestDeliveryStatusBoard.Set(status);
            var snapshot = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
            Assert.IsTrue(JToken.DeepEquals(expected, snapshot["delivery"]), snapshot.ToString());
            Assert.Greater(hub.GetTopicRevision(PauseMenuTopic.TopicName), before);
        }
    }
}

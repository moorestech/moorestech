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
        [TestCase(PlaytestLocalOnlyReason.SteamNotRunning, "steamNotRunning")]
        public void ローカル保存理由を契約どおりに送る(PlaytestLocalOnlyReason reason, string wireReason)
        {
            AssertDelivery(new PlaytestDeliveryLocalOnly(reason, 3, 2), new JObject { ["kind"] = "localOnly", ["pendingCount"] = 3, ["failedCount"] = 2, ["reason"] = wireReason });
        }

        [Test]
        public void 送信待ちと拒否と完了を区別して送る()
        {
            AssertDelivery(new PlaytestDeliveryPending(2, 3), new JObject { ["kind"] = "pending", ["pendingCount"] = 2, ["failedCount"] = 3 });
            AssertDelivery(new PlaytestDeliveryRefused(1, 4), new JObject { ["kind"] = "refused", ["pendingCount"] = 1, ["failedCount"] = 4 });
            AssertDelivery(PlaytestDeliveryAllSent.Instance, new JObject { ["kind"] = "allSent" });
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

using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.BugReport;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class PauseMenuTopicExportTest
    {
        [TearDown]
        public void Reset() { PlaytestExportStatusBoard.ResetOnPlayMode(); }

        [Test]
        public void DoneExportCarriesPathCountMissingAndReveal()
        {
            PlaytestExportStatusBoard.Set(new PlaytestExportDone("/tmp/a.zip", 2, new[] { new PlaytestExportMissing("reports/x/video.mp4", "IOException: locked") }, true));
            var export = JObject.Parse(SnapshotJson())["export"];
            Assert.AreEqual("done", (string)export["kind"]);
            Assert.AreEqual("/tmp/a.zip", (string)export["zipPath"]);
            Assert.AreEqual(2, (int)export["boxCount"]);
            Assert.AreEqual("reports/x/video.mp4", (string)export["missing"][0]["item"]);
            Assert.AreEqual("IOException: locked", (string)export["missing"][0]["reason"]);
            Assert.IsTrue((bool)export["revealed"]);
        }

        [TestCase("idle")]
        [TestCase("running")]
        public void SimpleStatesCarryOnlyKind(string kind)
        {
            PlaytestExportStatusBoard.Set(kind == "idle" ? PlaytestExportIdle.Instance : PlaytestExportRunning.Instance);
            var export = (JObject)JObject.Parse(SnapshotJson())["export"];
            Assert.AreEqual(kind, (string)export["kind"]);
            Assert.AreEqual(1, export.Count);
        }

        [Test]
        public void FailedExportCarriesReason()
        {
            PlaytestExportStatusBoard.Set(new PlaytestExportFailed("desktop folder unavailable"));
            var export = JObject.Parse(SnapshotJson())["export"];
            Assert.AreEqual("failed", (string)export["kind"]);
            Assert.AreEqual("desktop folder unavailable", (string)export["reason"]);
        }

        [Test]
        public void ExportChangePublishesTopic()
        {
            // 状態変化が購読経由で配信されることを検証
            // Verify a board change is published through the subscription
            var hub = new WebSocketHub();
            using var topic = new PauseMenuTopic(hub, new NetworkDisconnectState(), new BugReportCaptureSession(new NullBugReportCaptureSources()), new PauseMenuStateService());
            var before = hub.GetTopicRevision(PauseMenuTopic.TopicName);
            PlaytestExportStatusBoard.Set(PlaytestExportRunning.Instance);
            Assert.Greater(hub.GetTopicRevision(PauseMenuTopic.TopicName), before);
        }

        private static string SnapshotJson()
        {
            var hub = new WebSocketHub();
            using var topic = new PauseMenuTopic(hub, new NetworkDisconnectState(), new BugReportCaptureSession(new NullBugReportCaptureSources()), new PauseMenuStateService());
            return topic.GetSnapshotJsonAsync().GetAwaiter().GetResult();
        }
    }
}

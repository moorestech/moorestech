using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.BugReport;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// CurrentPageの購読とPageの直列化がpause_menuトピックへ実際に届くことを確かめる回帰試験
    /// Regression test that CurrentPage's subscription and Page's serialization actually reach the pause_menu topic
    /// </summary>
    public class PauseMenuTopicPublishTest
    {
        [Test]
        public void 画面遷移でsnapshotのpageとtopicRevisionが更新される()
        {
            var hub = new WebSocketHub();
            var pauseMenu = new PauseMenuStateService();
            var topic = new PauseMenuTopic(hub, new NetworkDisconnectState(), new BugReportCaptureSession(new NullBugReportCaptureSources()), pauseMenu);
            try
            {
                var before = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
                Assert.AreEqual("top", before["page"].Value<string>());
                var revisionBefore = hub.GetTopicRevision(PauseMenuTopic.TopicName);

                pauseMenu.ShowPage(PauseMenuPage.Settings);

                var after = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
                Assert.AreEqual("settings", after["page"].Value<string>());
                Assert.Greater(hub.GetTopicRevision(PauseMenuTopic.TopicName), revisionBefore);
            }
            finally
            {
                topic.Dispose();
            }
        }
    }
}

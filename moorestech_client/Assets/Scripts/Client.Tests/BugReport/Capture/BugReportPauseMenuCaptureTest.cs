using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using NUnit.Framework;

namespace Client.Tests.BugReport.Capture
{
    public class BugReportPauseMenuCaptureTest
    {
        [Test]
        public void 子画面の行き来と閉じキーの一段戻りでは記録を確保し直さない()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            var pauseMenu = new PauseMenuStateService();
            new BugReportPauseMenuTrigger(pauseMenu, session).Initialize();
            pauseMenu.OnEnter();
            var workspace = sources.ScreenshotWorkDirectory;

            // 実際の購読を結び、画面移動で記録そのものが作り直されないことを確かめる
            // Wire the real subscription and verify that navigating never replaces the captured records
            pauseMenu.ShowPage(PauseMenuPage.BugReport);
            pauseMenu.ShowPage(PauseMenuPage.Top);
            pauseMenu.ShowPage(PauseMenuPage.Settings);
            Assert.IsFalse(pauseMenu.StepBackOnCloseKey());
            Assert.AreEqual(1, sources.TakeRecordingCount);
            Assert.AreEqual(workspace, sources.ScreenshotWorkDirectory);

            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string>(), new List<string>(), null, 0));
            pauseMenu.ShowPage(PauseMenuPage.BugReport);
            Assert.IsFalse(pauseMenu.StepBackOnCloseKey());
            Assert.AreEqual(1, sources.TakeRecordingCount);
            Assert.AreEqual(BugReportCaptureStatus.Ready, session.Status.Value.Kind);
        }

        [Test]
        public void 古い送信の成功では新しく開いたポーズの記録を置き換えない()
        {
            var sources = new FakeBugReportCaptureSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(new ServerCaptureCompletion(7, 5, true, "/w/snapshots", "/master/server_v8", new List<string>(), new List<string>(), null, 0));
            var first = session.TryBeginSubmit();

            // 別のポーズが先に開いた場合は、遅れて届いた成功で3回目の確保を始めない
            // If another pause opens first, the late success must not trigger a third capture
            session.BeginOnPauseMenu();
            var workspace = sources.ScreenshotWorkDirectory;
            session.CompleteSubmit(first.Data, true, new List<MissingItem>());

            Assert.AreEqual(2, sources.TakeRecordingCount);
            Assert.AreEqual(workspace, sources.ScreenshotWorkDirectory);
            Assert.AreEqual(BugReportCaptureStatus.Capturing, session.Status.Value.Kind);
        }
    }
}

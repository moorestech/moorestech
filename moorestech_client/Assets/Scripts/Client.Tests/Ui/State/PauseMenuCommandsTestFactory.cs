using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.BugReport;
using Client.Tests.Playtest;
using Client.Tests.PlaytestReceiver;
using Client.Ui.State.PauseMenu;
using Client.Tests.BugReport.Bundle;

namespace Client.Tests.Ui.State
{
    /// <summary>
    /// 本物の画面状態と確保なしの送信手続きで PauseMenuCommands を組む
    /// Builds PauseMenuCommands over a real page state and a submitter with no capture session
    /// </summary>
    internal static class PauseMenuCommandsTestFactory
    {
        // セーブして終了（MonoBehaviour）と救出窓口はこのテスト群で呼ばないので null を渡す
        // Save-and-quit (a MonoBehaviour) and the recovery window are never called by these tests, so null is passed
        public static PauseMenuCommands Create(PauseMenuStateService pauseMenuStateService)
        {
            var session = new BugReportCaptureSession(new NullBugReportCaptureSources());
            var writer = new BugReportBundleWriter(new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason), FixedWebUiHealthProbe.NotAttempted(), new TestBugReportOutboxRoot());
            var submitter = new BugReportSubmitter(writer, session, new RecordingProgressSink(), new RecordingUploadRequester());
            return new PauseMenuCommands(new GameSaveRequester(), null, pauseMenuStateService, submitter, null);
        }
    }
}

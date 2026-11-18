using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.BugReport;
using Client.Ui.State.PauseMenu;

namespace Client.Tests.Ui.State
{
    /// <summary>
    /// 本物の画面状態と確保なしのセッションで PauseMenuModel を組み、購読まで済ませる
    /// Builds a PauseMenuModel over a real page state and a no-capture session, already subscribed
    /// </summary>
    internal static class PauseMenuModelTestFactory
    {
        public static PauseMenuModel Create(PauseMenuStateService pauseMenuStateService)
        {
            var model = new PauseMenuModel(new NetworkDisconnectState(), new BugReportCaptureSession(new NullBugReportCaptureSources()), pauseMenuStateService);
            model.Initialize();
            return model;
        }
    }
}

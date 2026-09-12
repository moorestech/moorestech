using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.PauseMenu
{
    public class PauseMenuStateService
    {
        private readonly BugReportCaptureSession _bugReportCaptureSession;
        private bool _closeRequested;

        public PauseMenuStateService(BugReportCaptureSession bugReportCaptureSession)
        {
            _bugReportCaptureSession = bugReportCaptureSession;
        }

        public bool IsClosePause()
        {
            if (_closeRequested)
            {
                _closeRequested = false;
                return true;
            }
            return InputManager.UI.CloseUI.GetKeyDown;
        }

        // バグ報告の送信完了など、ステート外からの閉じ要求。次の更新で消費される
        // Close request from outside the state (e.g. after a bug report is sent); consumed on the next update
        public void RequestClose()
        {
            _closeRequested = true;
        }

        public void OnEnter()
        {
            InputManager.MouseCursorVisible(true);
            // Escapeを押した瞬間の記録を確保する（ADR 0057）。記入中もワールドは止めない
            // Secure the Escape-moment records (ADR 0057); the world keeps running while typing
            _bugReportCaptureSession.BeginOnPauseMenu();
        }

        public void OnExit()
        {
            // 閉じ要求は今開いているメニューにだけ効く。持ち越すと次に開いた瞬間に閉じる
            // A close request applies only to the menu currently open; carrying it over closes the next one instantly
            _closeRequested = false;
        }
    }
}

using Client.Game.InGame.UI.UIState.State.PauseMenu;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport.Capture
{
    // ポーズメニューが開いたらEscape時点の記録を確保する。UI側はバグ報告を知らず、購読するのはこちら側
    // Starts the Escape-moment capture when the pause menu opens; the UI knows nothing of bug reports, this side subscribes
    public sealed class BugReportPauseMenuTrigger : IInitializable
    {
        private readonly PauseMenuStateService _pauseMenuStateService;
        private readonly BugReportCaptureSession _session;

        public BugReportPauseMenuTrigger(PauseMenuStateService pauseMenuStateService, BugReportCaptureSession session)
        {
            _pauseMenuStateService = pauseMenuStateService;
            _session = session;
        }

        public void Initialize()
        {
            // Escapeを押した瞬間の記録を確保する（ADR 0057）。記入中もワールドは止めない
            // Secure the Escape-moment records (ADR 0057); the world keeps running while typing
            _pauseMenuStateService.OnPauseMenuOpened.Subscribe(_ => _session.BeginOnPauseMenu());
        }
    }
}

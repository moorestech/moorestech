using Client.Game.InGame.UI.UIState;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport.Capture
{
    // UIStateControl を購読して現在の画面を確保元へ押し込む。確保元から参照すると生成が循環するため向きを逆にする
    // Subscribes to UIStateControl and pushes the current screen into the capture sources; the direction is inverted because the reverse edge makes construction circular
    public sealed class BugReportUiStatePusher : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly IBugReportCaptureSources _sources;

        public BugReportUiStatePusher(UIStateControl uiStateControl, IBugReportCaptureSources sources)
        {
            _uiStateControl = uiStateControl;
            _sources = sources;
        }

        public void Initialize()
        {
            _sources.SetCurrentUiState(_uiStateControl.CurrentState);
            _uiStateControl.OnStateChanged += _sources.SetCurrentUiState;
        }
    }
}

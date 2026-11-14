using Client.Game.InGame.UI.ProgressBar;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Progress
{
    /// <summary>
    /// 採掘進捗を起動時に実画面の Chrome 層へ載せ、ProgressBarState の変化で出し入れと進捗を押す
    /// Mounts the mining progress on the viewport Chrome layer at startup and pushes visibility and progress on ProgressBarState changes
    /// </summary>
    public sealed class ProgressHudPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly ProgressBarState _progressBarState;
        private ProgressHudView _view;

        public ProgressHudPresenter(GameUiRoot gameUiRoot, ProgressBarState progressBarState)
        {
            _gameUiRoot = gameUiRoot;
            _progressBarState = progressBarState;
        }

        public void Initialize()
        {
            // Web はホットバーと同じ viewportOverlay（実画面）。状態は Subject なので今の値を先に押す
            // On the Web it shares viewportOverlay (the real viewport) with the hotbar; the state is a Subject, so push the current value first
            _view = new ProgressHudView();
            _gameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_view);
            Apply();
            _progressBarState.OnProgressChanged.Subscribe(_ => Apply());

            #region Internal

            void Apply()
            {
                _view.SetShown(_progressBarState.IsShown);
                _view.SetProgress(_progressBarState.CurrentProgress);
            }

            #endregion
        }

    }
}

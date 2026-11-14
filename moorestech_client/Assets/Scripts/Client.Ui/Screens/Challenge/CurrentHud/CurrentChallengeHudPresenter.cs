using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Challenge;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Challenge.CurrentHud
{
    /// <summary>
    /// 進行中チャレンジ HUD を起動時に実画面の Chrome 層へ載せ、Model の変化を View へ押す
    /// Mounts the current challenge HUD on the viewport Chrome layer at startup and pushes Model changes to the View
    /// </summary>
    public sealed class CurrentChallengeHudPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly CurrentChallengeHudModel _model;
        private CurrentChallengeHudView _view;

        public CurrentChallengeHudPresenter(GameUiRoot gameUiRoot, CurrentChallengeHudModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            // Web は viewportOverlay（実画面）。z は stage-overlay-panel だが同じ族の常駐 HUD と同じ Chrome にまとめる（D-3）
            // On the Web it is in viewportOverlay (the real viewport); z is stage-overlay-panel, grouped into Chrome with the sibling resident HUDs (D-3)
            _view = new CurrentChallengeHudView();
            _gameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_view);
            _model.Current.Subscribe(_view.Render);
        }

    }
}

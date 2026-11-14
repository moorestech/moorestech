using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.KeyHints;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.KeyHints
{
    /// <summary>
    /// キーヒントを起動時に実画面の Chrome 層へ載せ、Model の変化を View へ押す
    /// Mounts the key hints on the viewport Chrome layer at startup and pushes Model changes to the View
    /// </summary>
    public sealed class KeyHintHudPresenter : IInitializable
    {
        internal const UiLayer HostLayer = UiLayer.Chrome;
        internal const UiFamily HostFamily = UiFamily.Viewport;

        private readonly GameUiRoot _gameUiRoot;
        private readonly KeyHintHudModel _model;
        private KeyHintHudView _view;

        public KeyHintHudPresenter(GameUiRoot gameUiRoot, KeyHintHudModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            // Web は viewportOverlay（実画面）の z stage-overlay-panel-chrome
            // On the Web it sits in viewportOverlay (the real viewport) at z stage-overlay-panel-chrome
            _view = new KeyHintHudView();
            _gameUiRoot.GetLayer(HostLayer, HostFamily).Add(_view);
            _model.Current.Subscribe(_view.Render);
        }

    }
}

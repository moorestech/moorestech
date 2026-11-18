using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Tutorial.WorldPin;
using UniRx;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Client.Ui.Screens.Tutorial.WorldPin
{
    /// <summary>
    /// ワールドピンを常駐させ、Model の変化を View へ渡す
    /// Keeps the world pins resident and forwards model changes to the View
    /// </summary>
    public sealed class WorldPinPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly WorldPinModel _model;

        public WorldPinPresenter(GameUiRoot gameUiRoot, WorldPinModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            // Web は Portal の --z-portal-world-pin に置くので、同じ層の Viewport 族へ常駐させる
            // Web mounts it in the portal at --z-portal-world-pin, so keep it resident in that layer's Viewport family
            Mount(_gameUiRoot.GetLayer(UiLayer.WorldPin, UiFamily.Viewport), _model.Current);
        }

        public static WorldPinOverlayView Mount(VisualElement layer, IReadOnlyReactiveProperty<WorldPinViewModel> model)
        {
            var view = new WorldPinOverlayView();
            layer.Add(view);
            model.Subscribe(view.Render);
            return view;
        }
    }
}

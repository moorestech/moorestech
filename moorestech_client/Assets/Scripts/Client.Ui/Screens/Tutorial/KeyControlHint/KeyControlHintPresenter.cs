using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Tutorial.KeyControlHint;
using UniRx;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Client.Ui.Screens.Tutorial.KeyControlHint
{
    /// <summary>
    /// キー操作ヒントを常駐させ、Model の変化を View へ渡す
    /// Keeps the key control hints resident and forwards model changes to the View
    /// </summary>
    public sealed class KeyControlHintPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly KeyControlHintModel _model;

        public KeyControlHintPresenter(GameUiRoot gameUiRoot, KeyControlHintModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            // Web は viewportOverlay（z stage-overlay-panel-chrome の積み重ね文脈）の中なので、Chrome 層の Viewport 族へ常駐させる
            // Web mounts it inside viewportOverlay (a stacking context at z stage-overlay-panel-chrome), so keep it resident in the Chrome layer's Viewport family
            Mount(_gameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport), _model.Current);
        }

        public static KeyControlHintView Mount(VisualElement layer, IReadOnlyReactiveProperty<KeyControlHintViewModel> model)
        {
            var view = new KeyControlHintView();
            layer.Add(view);
            model.Subscribe(view.Render);
            return view;
        }
    }
}

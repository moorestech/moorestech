using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Toast
{
    /// <summary>
    /// トースト層へ ToastHostView を常駐させ、GameUiToastFeed に出たトーストを積む
    /// Keeps a ToastHostView resident in the toast layer and pushes toasts that appear on GameUiToastFeed
    /// </summary>
    public sealed class ToastLayerPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly GameUiToastFeed _toastFeed;
        private ToastHostView _host;

        public ToastLayerPresenter(GameUiRoot gameUiRoot, GameUiToastFeed toastFeed)
        {
            _gameUiRoot = gameUiRoot;
            _toastFeed = toastFeed;
        }

        public void Initialize()
        {
            _host = new ToastHostView();
            _gameUiRoot.GetLayer(UiLayer.Toast, UiFamily.Viewport).Add(_host);
            _toastFeed.OnToast.Subscribe(_host.Push);
        }

    }
}

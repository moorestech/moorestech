using System;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Toast
{
    /// <summary>
    /// トースト層へ ToastHostView を常駐させ、GameUiToastFeed に出たトーストを積む
    /// Keeps a ToastHostView resident in the toast layer and pushes toasts that appear on GameUiToastFeed
    /// </summary>
    public sealed class ToastLayerPresenter : IInitializable, IDisposable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly GameUiToastFeed _toastFeed;
        private ToastHostView _host;
        private IDisposable _subscription;

        public ToastLayerPresenter(GameUiRoot gameUiRoot, GameUiToastFeed toastFeed)
        {
            _gameUiRoot = gameUiRoot;
            _toastFeed = toastFeed;
        }

        public void Initialize()
        {
            _host = new ToastHostView();
            _gameUiRoot.GetLayer(UiLayer.Toast).Add(_host);
            _subscription = _toastFeed.OnToast.Subscribe(_host.Push);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _host?.RemoveFromHierarchy();
        }
    }
}

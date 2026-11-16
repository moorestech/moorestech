using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.ScaleReading;
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
        private readonly StageScaleSource _stageScaleSource;
        private ToastHostView _host;

        public ToastLayerPresenter(GameUiRoot gameUiRoot, GameUiToastFeed toastFeed, StageScaleSource stageScaleSource)
        {
            _gameUiRoot = gameUiRoot;
            _toastFeed = toastFeed;
            _stageScaleSource = stageScaleSource;
        }

        public void Initialize()
        {
            _host = new ToastHostView();
            _gameUiRoot.GetLayer(UiLayer.Toast, UiFamily.Viewport).Add(_host);
            // 中身は PanelSettings の倍率で舞台と同率、倍率の外の右下の余白だけ実画面の 16px に保つ（Web .host の bottom/right: 1rem は scale(var(--ui-scale)) の外）
            // Contents scale with the stage via PanelSettings; only the unscaled bottom-right inset stays 16 real px (Web .host bottom/right: 1rem sit outside scale(var(--ui-scale)))
            _stageScaleSource.Scale.Subscribe(_host.SetStageScale);
            _toastFeed.OnToast.Subscribe(_host.Push);
        }

    }
}

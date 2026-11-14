using Client.Game.InGame.UI.Crosshair;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.CommonHud
{
    /// <summary>
    /// クロスヘアを起動時に舞台の層へ載せ、CrosshairVisibility の変化で出し入れする
    /// Mounts the crosshair on the stage layer at startup and toggles it on CrosshairVisibility changes
    /// </summary>
    public sealed class CrosshairHudPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly CrosshairVisibility _crosshairVisibility;
        private CrosshairHudView _view;

        public CrosshairHudPresenter(GameUiRoot gameUiRoot, CrosshairVisibility crosshairVisibility)
        {
            _gameUiRoot = gameUiRoot;
            _crosshairVisibility = crosshairVisibility;
        }

        public void Initialize()
        {
            // Web は stage の中の fixed なので舞台の族。購読時に今の値が流れる
            // On the Web it is fixed inside the stage, so the stage family; subscribing delivers the current value
            _view = new CrosshairHudView();
            _gameUiRoot.GetLayer(UiLayer.ViewportStage, UiFamily.Stage).Add(_view);
            _view.SetShown(_crosshairVisibility.IsVisible());
            _crosshairVisibility.OnVisibleChanged.Subscribe(_view.SetShown);
        }

    }
}

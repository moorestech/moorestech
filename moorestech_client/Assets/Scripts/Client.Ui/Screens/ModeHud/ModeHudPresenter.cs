using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.ModeHud;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.ModeHud
{
    /// <summary>
    /// 操作モード HUD を Chrome 層の Viewport 族へ起動時に 1 回だけ載せる（Web の .viewportOverlay・z 31）
    /// Mounts the operation-mode HUD once at startup into the Chrome layer's Viewport family (Web .viewportOverlay, z 31)
    /// </summary>
    public sealed class ModeHudPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly ModeHudModel _model;

        public ModeHudPresenter(GameUiRoot gameUiRoot, ModeHudModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            // 載せるのは 1 回だけ。購読は Presenter が持ち、購読時の今の値で 1 回描く（前例 ToastLayerPresenter・PauseMenuScreenPresenter）
            // Mount once; the presenter owns the subscription, which draws once with the current value (precedent ToastLayerPresenter, PauseMenuScreenPresenter)
            var view = new ModeHudView();
            _gameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(view);
            _model.Current.Subscribe(view.Render);
        }
    }
}

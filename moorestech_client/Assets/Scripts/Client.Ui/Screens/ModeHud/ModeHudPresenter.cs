using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.ModeHud;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.ModeHud
{
    /// <summary>
    /// 操作モード HUD を起動時に 1 回 Chrome 層へ載せる
    /// Mounts the operation-mode HUD once into the Chrome layer
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

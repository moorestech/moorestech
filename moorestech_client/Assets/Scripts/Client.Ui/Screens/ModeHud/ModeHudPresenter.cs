using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.ModeHud;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.ModeHud
{
    /// <summary>
    /// 操作モード HUD を起動時に 1 回 OverlayPanel 層へ載せる
    /// Mounts the operation-mode HUD once into the OverlayPanel layer
    /// </summary>
    public sealed class ModeHudPresenter : IInitializable
    {
        // Web の帯と配置 HUD は viewportOverlay の z stage-overlay-panel。層の中ではホットバーより前の兄弟（登録順）
        // On the Web the bands and placement HUD sit in viewportOverlay at z stage-overlay-panel; in the layer they precede the hotbar (registration order)
        internal const UiLayer HostLayer = UiLayer.OverlayPanel;
        internal const UiFamily HostFamily = UiFamily.Viewport;

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
            _gameUiRoot.GetLayer(HostLayer, HostFamily).Add(view);
            _model.Current.Subscribe(view.Render);
        }
    }
}

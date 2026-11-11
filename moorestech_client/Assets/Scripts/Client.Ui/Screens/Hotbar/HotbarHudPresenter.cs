using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Hotbar
{
    /// <summary>
    /// ホットバーを起動時に 1 回 Chrome 層へ載せる
    /// Mounts the hotbar once into the Chrome layer
    /// </summary>
    public sealed class HotbarHudPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly HotbarModel _model;
        private readonly IHotbarCommands _commands;
        private readonly PlacementTargetIcons _icons;
        private readonly UiCommandResultToaster _toaster;

        public HotbarHudPresenter(GameUiRoot gameUiRoot, HotbarModel model, IHotbarCommands commands, PlacementTargetIcons icons, UiCommandResultToaster toaster)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
            _commands = commands;
            _icons = icons;
            _toaster = toaster;
        }

        public void Initialize()
        {
            // 載せるのは 1 回だけ。出し分け（常駐 HUD の画面・スキット）は Model が Visible に畳むので、購読は常に持ち今の値で 1 回描く
            // Mount once; the model folds visibility (always-on-HUD screens, skits) into Visible, so the subscription is always held and draws once with the current value
            var view = new HotbarHudView(_commands, _icons, _toaster);
            _gameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(view);
            _model.Current.Subscribe(view.Render);
        }
    }
}

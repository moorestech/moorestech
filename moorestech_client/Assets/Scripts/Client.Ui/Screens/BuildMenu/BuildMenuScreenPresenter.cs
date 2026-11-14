using System;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.BuildMenu.CategoryScroll;
using Client.Ui.State.BuildMenu;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.BuildMenu.Session;
using Client.Ui.State.BuildMenu.Source;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Routing;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.BuildMenu
{
    /// <summary>
    /// ビルドメニューの View を起動時に 1 回だけ載せ、画面ルータで出し入れし、出ている間だけ Model を購読する
    /// Mounts the build-menu view once at startup, shows or hides it on the screen router, and subscribes to the model only while shown
    /// </summary>
    public sealed class BuildMenuScreenPresenter : IInitializable, IDisposable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly UiRoutingState _routingState;
        private readonly BuildMenuModel _model;
        private readonly IBuildMenuCommands _commands;
        private readonly IHotbarCommands _hotbarCommands;
        private readonly GameContentPresentation _content;
        private readonly PlacementTargetIcons _icons;
        private readonly UiCommandResultToaster _toaster;
        private readonly BuildMenuSessionStore _session;
        private readonly IUiOsCursor _osCursor;
        private readonly CompositeDisposable _subscriptions = new();
        private IDisposable _modelSubscription;
        private BuildMenuView _view;

        public BuildMenuScreenPresenter(GameUiRoot gameUiRoot, UiRoutingState routingState, BuildMenuModel model, IBuildMenuCommands commands, IHotbarCommands hotbarCommands, GameContentPresentation content, PlacementTargetIcons icons, UiCommandResultToaster toaster, BuildMenuSessionStore session, IUiOsCursor osCursor)
        {
            _gameUiRoot = gameUiRoot;
            _routingState = routingState;
            _model = model;
            _commands = commands;
            _hotbarCommands = hotbarCommands;
            _content = content;
            _icons = icons;
            _toaster = toaster;
            _session = session;
            _osCursor = osCursor;
        }

        public void Initialize()
        {
            // 載せるのはここで 1 回だけ。トラッカーは View と同じ寿命（D-22）
            // Mount exactly once here; the tracker lives as long as the view (D-22)
            var tracker = new BuildMenuCategoryScrollTracker(_session);
            _view = new BuildMenuView(_commands, _hotbarCommands, _content, _icons, _toaster, _session, tracker, _osCursor);

            _gameUiRoot.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(_view);
            _routingState.Current.Select(BuildMenuScreenVisibility.IsShown).DistinctUntilChanged().Subscribe(SetShown).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _modelSubscription?.Dispose();
            _view?.SetShown(false);
            _view?.RemoveFromHierarchy();
        }

        private void SetShown(bool shown)
        {
            // 出ている間だけ購読する。開いた直後は前回の VM を 1 回描き、同じフレーム末の組み直しで最新に描き直す（D-19）
            // Subscribe only while shown; on open it renders the previous model once, then the same frame's end-of-frame rebuild redraws the latest (D-19)
            _modelSubscription?.Dispose();
            _modelSubscription = shown ? _model.Current.Subscribe(_view.Render) : null;
            _view.SetShown(shown);
        }
    }
}

using System;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Screens.BugReport;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Routing;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズの View を起動時に 1 回だけ載せ、画面ルータの判断で表示・非表示を切り替える
    /// Mounts the pause-menu view once at startup and shows or hides it on the screen router's decision
    /// </summary>
    public sealed class PauseMenuScreenPresenter : IInitializable, IDisposable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly UiRoutingState _routingState;
        private readonly PauseMenuModel _pauseMenuModel;
        private readonly SettingsModel _settingsModel;
        private readonly IPauseMenuCommands _commands;
        private readonly ISettingsCommands _settingsCommands;
        private readonly GameUiToastFeed _toasts;
        private readonly BugReportDraft _draft;
        private readonly CompositeDisposable _subscriptions = new();
        private PauseMenuView _view;

        public PauseMenuScreenPresenter(GameUiRoot gameUiRoot, UiRoutingState routingState, PauseMenuModel pauseMenuModel, SettingsModel settingsModel, IPauseMenuCommands commands, ISettingsCommands settingsCommands, GameUiToastFeed toasts, BugReportDraft draft)
        {
            _gameUiRoot = gameUiRoot;
            _routingState = routingState;
            _pauseMenuModel = pauseMenuModel;
            _settingsModel = settingsModel;
            _commands = commands;
            _settingsCommands = settingsCommands;
            _toasts = toasts;
            _draft = draft;
        }

        public void Initialize()
        {
            // 載せるのはここで 1 回だけ。View は隠れた状態で生まれる
            // Mount exactly once here; the view is born hidden
            _view = new PauseMenuView(_pauseMenuModel.Current, _settingsModel.Current, _commands, _settingsCommands, _toasts, _draft, _gameUiRoot.AssetCatalog);
            _gameUiRoot.GetLayer(UiLayer.PauseMenu).Add(_view);

            // 開くたびに書きかけを空へ戻す（D-7）
            // Clear the draft on every open (D-7)
            _pauseMenuModel.OnPauseMenuOpened.Subscribe(_ => _draft.Reset()).AddTo(_subscriptions);
            _routingState.Current.Subscribe(snapshot => _view.SetShown(IsShownOn(snapshot))).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _view?.SetShown(false);
            _view?.RemoveFromHierarchy();
        }

        // ポーズを持つ画面の列挙は traits 表に任せ、主パネルが GameUi の持ち物のときだけ出す（D-20）
        // The traits table enumerates pause screens; show only when GameUi owns the main panel (D-20)
        private bool IsShownOn(UiRoutingSnapshot snapshot)
        {
            return UiScreenTraitsTable.Get(snapshot.Screen).PauseMenu && snapshot.MainPanelOwner == UiSurfaceOwner.GameUi;
        }
    }
}

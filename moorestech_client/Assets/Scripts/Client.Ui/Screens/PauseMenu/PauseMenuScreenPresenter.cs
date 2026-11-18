using Client.Localization;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
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
    /// ポーズの View を起動時に 1 回だけ載せ、画面ルータの判断で表示・非表示を切り替え、表示中だけ Model・設定・言語を購読する（§4.2 の ①）
    /// Mounts the pause-menu view once at startup, shows or hides it on the screen router's decision and follows the models and language only while shown (§4.2 type 1)
    /// </summary>
    public sealed class PauseMenuScreenPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly UiRoutingState _routingState;
        private readonly PauseMenuModel _pauseMenuModel;
        private readonly SettingsModel _settingsModel;
        private readonly IPauseMenuCommands _commands;
        private readonly ISettingsCommands _settingsCommands;
        private readonly GameUiToastFeed _toasts;
        private readonly BugReportDraft _draft;
        private readonly IUiOsCursor _osCursor;
        private readonly SerialDisposable _shownSubscription = new();
        private PauseMenuView _view;

        public PauseMenuScreenPresenter(GameUiRoot gameUiRoot, UiRoutingState routingState, PauseMenuModel pauseMenuModel, SettingsModel settingsModel, IPauseMenuCommands commands, ISettingsCommands settingsCommands, GameUiToastFeed toasts, BugReportDraft draft, IUiOsCursor osCursor)
        {
            _gameUiRoot = gameUiRoot;
            _routingState = routingState;
            _pauseMenuModel = pauseMenuModel;
            _settingsModel = settingsModel;
            _commands = commands;
            _settingsCommands = settingsCommands;
            _toasts = toasts;
            _draft = draft;
            _osCursor = osCursor;
        }

        public void Initialize()
        {
            // 載せるのはここで 1 回だけ。View は隠れた状態で生まれる
            // Mount exactly once here; the view is born hidden
            _view = new PauseMenuView(_commands, _settingsCommands, _toasts, _draft, _osCursor);

            _gameUiRoot.GetLayer(UiLayer.PauseMenu, UiFamily.Stage).Add(_view);

            // 開くたびに書きかけを空へ戻す（D-7）
            // Clear the draft on every open (D-7)
            _pauseMenuModel.OnPauseMenuOpened.Subscribe(_ => _draft.Reset());
            // ルータの通知は画面が変わらなくても来るので同じ値を弾く（出ている間に頁を作り直すと入力中の欄を失う）
            // Router notices arrive even when the screen is unchanged, so drop repeats (rebuilding while shown loses the field being typed)
            _routingState.Current.Select(IsShownOn).DistinctUntilChanged().Subscribe(SetShown);

            #region Internal

            // ポーズを持つ画面の列挙は traits 表に任せる。描くかどうかは composition root が登録の有無で決める（設計書 C10・[A-16]）
            // The traits table enumerates pause screens; whether this draws is decided by the composition root through registration (design C10, [A-16])
            bool IsShownOn(UiRoutingSnapshot snapshot)
            {
                return UiScreenTraitsTable.Get(snapshot.Screen).PauseMenu;
            }

            void SetShown(bool shown)
            {
                if (!shown)
                {
                    _shownSubscription.Disposable = Disposable.Empty;
                    _view.SetShown(false);
                    return;
                }

                // 最新値で 1 回描き直してから、変化だけを購読する
                // Redraw once from the latest values, then subscribe to changes only
                _view.SetShown(true);
                _view.Redraw(_pauseMenuModel.Current.Value, _settingsModel.Current.Value);
                _shownSubscription.Disposable = new CompositeDisposable(
                    _pauseMenuModel.Current.Skip(1).Subscribe(_view.RenderPauseMenu),
                    _settingsModel.Current.Skip(1).Subscribe(_view.RenderSettings),
                    Localize.OnLanguageChanged.Subscribe(_ => _view.RenderLocaleMarker()));
            }

            #endregion
        }

    }
}

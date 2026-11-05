using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.Ui.Components;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Assets;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BugReport;
using Client.Ui.Screens.Settings;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズ画面の GamePanel。ページは PauseMenuViewModel.Page だけで決め、変わるたびに中身を作り直す（Web の PauseMenuPanel）
    /// The pause-menu GamePanel; the page is decided only by PauseMenuViewModel.Page and rebuilt whenever it changes (Web PauseMenuPanel)
    /// </summary>
    public sealed class PauseMenuView : VisualElement
    {
        private readonly IReadOnlyReactiveProperty<PauseMenuViewModel> _model;
        private readonly IReadOnlyReactiveProperty<SettingsViewModel> _settings;
        private readonly IPauseMenuCommands _commands;
        private readonly ISettingsCommands _settingsCommands;
        private readonly GameUiToastFeed _toasts;
        private readonly BugReportDraft _draft;
        private readonly UiAssetCatalog _catalog;
        private readonly LocalizedLabel _title;
        private readonly VisualElement _pageHost;
        private CompositeDisposable _subscriptions;
        private PauseMenuPage _shownPage;
        private PauseMenuTopPageView _top;
        private LanguageSelectView _language;
        private SprintModeSelectView _sprint;
        private GraphicsQualitySettingsView _graphics;
        private BugReportFormView _bugReport;

        public PauseMenuView(IReadOnlyReactiveProperty<PauseMenuViewModel> model, IReadOnlyReactiveProperty<SettingsViewModel> settings, IPauseMenuCommands commands, ISettingsCommands settingsCommands, GameUiToastFeed toasts, BugReportDraft draft, UiAssetCatalog catalog)
        {
            _model = model;
            _settings = settings;
            _commands = commands;
            _settingsCommands = settingsCommands;
            _toasts = toasts;
            _draft = draft;
            _catalog = catalog;
            styleSheets.Add(catalog.PauseMenu);
            AddToClassList("pause-menu");

            // 自身は層いっぱいの透明な置き場で、testid とアンカーは Web と同じく見える枠に付ける
            // This element is a transparent full-layer host; the testid and anchor go on the visible frame as on the Web
            pickingMode = PickingMode.Ignore;
            var panel = new GamePanel(catalog.GamePanel) { name = "pause-menu" };
            panel.AddToClassList("pause-menu__panel");
            TutorialAnchor.Mark(panel, AnchorIds.PauseMenu);

            // 題は枠のタイトル行に 1 つだけ置き、ページに合わせてキーを差し替える
            // A single title sits in the frame's title row and swaps its key per page
            _title = new LocalizedLabel();
            panel.SetTitle(_title);
            Add(panel);

            // ロケール印は DSL が言語の切り替わりを待つための名前
            // The locale marker is a name the DSL waits on to see the language switch
            _pageHost = new VisualElement();
            _pageHost.AddToClassList("pause-menu__page");
            panel.Add(_pageHost);
            UpdateLocaleMarker();
            ShowPage(model.Value);

            RegisterCallback<AttachToPanelEvent>(_ => OnAttached());
            RegisterCallback<DetachFromPanelEvent>(_ => OnDetached());
        }

        private void OnAttached()
        {
            // 購読はパネルに載っている間だけ張る。どれも購読時に現在値を流す
            // Subscriptions live only while on a panel; each replays its current value on subscribe
            _subscriptions?.Dispose();
            _subscriptions = new CompositeDisposable();
            _model.Subscribe(OnPauseMenuChanged).AddTo(_subscriptions);
            _settings.Subscribe(OnSettingsChanged).AddTo(_subscriptions);
            Localize.OnLanguageChanged.Subscribe(_ => UpdateLocaleMarker()).AddTo(_subscriptions);
            UpdateLocaleMarker();
        }

        private void OnDetached()
        {
            _subscriptions?.Dispose();
            _subscriptions = null;
        }

        private void OnPauseMenuChanged(PauseMenuViewModel model)
        {
            if (_shownPage != model.Page) ShowPage(model);
            _top?.SetDisconnected(model.Disconnected);
            _bugReport?.Apply(model);
        }

        private void OnSettingsChanged(SettingsViewModel model)
        {
            _language?.Apply(model);
            _sprint?.Apply(model);
            _graphics?.Apply(model);
        }

        private void ShowPage(PauseMenuViewModel model)
        {
            // 前のページの View を捨てて作り直す。書きかけは BugReportDraft が持つので消えない
            // Drop the previous page's views and rebuild; the draft survives in BugReportDraft
            _pageHost.Clear();
            _top = null;
            _language = null;
            _sprint = null;
            _graphics = null;
            _bugReport = null;
            _shownPage = model.Page;
            switch (model.Page)
            {
                case PauseMenuPage.Top: BuildTop(); break;
                case PauseMenuPage.Settings: BuildSettings(); break;
                case PauseMenuPage.BugReport: BuildBugReport(); break;
                default: throw new ArgumentOutOfRangeException(nameof(model), model.Page, null);
            }

            #region Internal

            void BuildTop()
            {
                _title.SetKey(LocalizationKeys.Ui.PauseMenu.Title);
                _top = new PauseMenuTopPageView(_commands, _catalog);
                _top.SetDisconnected(model.Disconnected);
                _pageHost.Add(_top);
            }

            void BuildSettings()
            {
                _title.SetKey(LocalizationKeys.Ui.PauseMenu.Settings);
                var subPage = new PauseMenuSubPageView(_commands, _catalog);
                var settings = _settings.Value;
                _language = new LanguageSelectView(settings, _settingsCommands, _toasts, _catalog);
                _sprint = new SprintModeSelectView(settings, _settingsCommands, _catalog);
                _graphics = new GraphicsQualitySettingsView(settings, _settingsCommands, _catalog);
                subPage.AddSection(_language);
                subPage.AddSection(_sprint);
                subPage.AddSection(_graphics);
                _pageHost.Add(subPage);
            }

            void BuildBugReport()
            {
                _title.SetKey(LocalizationKeys.Ui.PauseMenu.BugReport);
                var subPage = new PauseMenuSubPageView(_commands, _catalog);
                _bugReport = new BugReportFormView(model, _commands, _toasts, _draft, _catalog);
                subPage.AddSection(_bugReport);
                _pageHost.Add(subPage);
            }

            #endregion
        }

        private void UpdateLocaleMarker()
        {
            _pageHost.name = $"pause-menu-locale-{Localize.GetCurrentLanguageCode()}";
        }
    }
}

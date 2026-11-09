using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.BugReport;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズ画面の枠。ページは PauseMenuViewModel.Page だけで決め、表示のたびと変わるたびに中身を作り直す（Web の PauseMenuPanel）
    /// The pause-menu frame; the page is decided only by PauseMenuViewModel.Page and rebuilt on every show and change (Web PauseMenuPanel)
    /// </summary>
    public sealed class PauseMenuView : VisualElement
    {
        private readonly IReadOnlyReactiveProperty<PauseMenuViewModel> _model;
        private readonly IReadOnlyReactiveProperty<SettingsViewModel> _settings;
        private readonly IPauseMenuCommands _commands;
        private readonly ISettingsCommands _settingsCommands;
        private readonly GameUiToastFeed _toasts;
        private readonly UiCommandResultToaster _toaster;
        private readonly BugReportDraft _draft;
        private readonly LocalizedLabel _title;
        private readonly VisualElement _pageHost;
        private CompositeDisposable _subscriptions;
        private PauseMenuPage _shownPage;
        private IPauseMenuPage _currentPage;

        public PauseMenuView(IReadOnlyReactiveProperty<PauseMenuViewModel> model, IReadOnlyReactiveProperty<SettingsViewModel> settings, IPauseMenuCommands commands, ISettingsCommands settingsCommands, GameUiToastFeed toasts, UiCommandResultToaster toaster, BugReportDraft draft)
        {
            _model = model;
            _settings = settings;
            _commands = commands;
            _settingsCommands = settingsCommands;
            _toasts = toasts;
            _toaster = toaster;
            _draft = draft;
            AddToClassList("pause-menu");

            // 自身は層いっぱいの透明な置き場で、testid とアンカーは Web と同じく見える枠に付ける
            // This element is a transparent full-layer host; the testid and anchor go on the visible frame as on the Web
            pickingMode = PickingMode.Ignore;
            var panel = new VisualElement { name = "pause-menu" };
            panel.AddToClassList("pause-menu__panel");
            TutorialAnchor.Mark(panel, AnchorIds.PauseMenu);
            Add(panel);

            // 題は枠の先頭に 1 つだけ置き、ページに合わせてキーを差し替える（Web の Title order=1）
            // A single title leads the frame and swaps its key per page (Web Title order=1)
            _title = new LocalizedLabel();
            _title.AddToClassList("pause-menu__title");
            _title.AddToClassList(UiTypographyClass.TitleOrder1);
            _title.SetKey(LocalizationKeys.Ui.PauseMenu.Title);
            panel.Add(_title);

            // ロケール印は DSL が言語の切り替わりを待つための名前
            // The locale marker is a name the DSL waits on to see the language switch
            _pageHost = new VisualElement();
            _pageHost.AddToClassList("pause-menu__page");
            panel.Add(_pageHost);

            // 生成直後は隠しておき、ルータの判断で SetShown が表示する
            // Start hidden; SetShown reveals it on the router's decision
            style.display = DisplayStyle.None;
        }

        public void SetShown(bool shown)
        {
            if (shown == (_subscriptions != null)) return;
            if (!shown) BlurFocusedDescendant();
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown)
            {
                // 隠している間はモデルを購読しない
                // Do not follow the models while hidden
                _subscriptions.Dispose();
                _subscriptions = null;
                return;
            }

            // 最新値で 1 回描き直してから、変化だけを購読する
            // Redraw once from the latest values, then subscribe to changes only
            ShowPage(_model.Value);
            UpdateLocaleMarker();
            _subscriptions = new CompositeDisposable();
            _model.Skip(1).Subscribe(OnPauseMenuChanged).AddTo(_subscriptions);
            _settings.Skip(1).Subscribe(OnSettingsChanged).AddTo(_subscriptions);
            Localize.OnLanguageChanged.Subscribe(_ => UpdateLocaleMarker()).AddTo(_subscriptions);
        }

        private void OnPauseMenuChanged(PauseMenuViewModel model)
        {
            if (_shownPage != model.Page) ShowPage(model);
            _currentPage.ApplyPauseMenu(model);
        }

        private void OnSettingsChanged(SettingsViewModel model)
        {
            _currentPage.ApplySettings(model);
        }

        private void ShowPage(PauseMenuViewModel model)
        {
            // 前のページの View を捨てて作り直す。書きかけは BugReportDraft が持つので消えない
            // Drop the previous page's views and rebuild; the draft survives in BugReportDraft
            _pageHost.Clear();
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
                Show(new PauseMenuTopPageView(_commands));
            }

            void BuildSettings()
            {
                _title.SetKey(LocalizationKeys.Ui.PauseMenu.Settings);
                Show(new PauseMenuSettingsPage(_settings.Value, _commands, _settingsCommands, _toaster));
            }

            void BuildBugReport()
            {
                _title.SetKey(LocalizationKeys.Ui.PauseMenu.BugReport);
                Show(new PauseMenuBugReportPage(model, _commands, _toasts, _toaster, _draft));
            }

            void Show(IPauseMenuPage page)
            {
                page.ApplyPauseMenu(model);
                _currentPage = page;
                _pageHost.Add(page.Root);
            }

            #endregion
        }

        private void UpdateLocaleMarker()
        {
            _pageHost.name = $"pause-menu-locale-{Localize.GetCurrentLanguageCode()}";
        }

        // 入力欄にフォーカスがあるまま隠すと、非表示のまま入力中の扱いが残りキーが効かなくなる
        // Hiding with a text field still focused would leave it counted as typing and keep world keys suppressed
        private void BlurFocusedDescendant()
        {
            if (focusController?.focusedElement is VisualElement focused && Contains(focused)) focused.Blur();
        }
    }
}

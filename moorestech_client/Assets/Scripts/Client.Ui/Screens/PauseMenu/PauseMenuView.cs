using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BugReport;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズ画面の枠。ページは PauseMenuViewModel.Page だけで決め、表示のたびと変わるたびに中身を作り直す。Model は購読せず Presenter が描く口を呼ぶ（Web の PauseMenuPanel）
    /// The pause-menu frame; the page is decided only by PauseMenuViewModel.Page and rebuilt on every show and change; it never subscribes, the presenter calls its render ports (Web PauseMenuPanel)
    /// </summary>
    public sealed class PauseMenuView : VisualElement
    {
        private readonly IPauseMenuCommands _commands;
        private readonly ISettingsCommands _settingsCommands;
        private readonly GameUiToastFeed _toasts;
        private readonly BugReportDraft _draft;
        private readonly IUiOsCursor _osCursor;
        private readonly LocalizedLabel _title;
        private readonly VisualElement _pageHost;
        private bool _shown;
        private SettingsViewModel _settings;
        private PauseMenuPage _shownPage;
        private IPauseMenuPage _currentPage;

        public PauseMenuView(IPauseMenuCommands commands, ISettingsCommands settingsCommands, GameUiToastFeed toasts, BugReportDraft draft, IUiOsCursor osCursor)
        {
            _commands = commands;
            _settingsCommands = settingsCommands;
            _toasts = toasts;
            _draft = draft;
            _osCursor = osCursor;
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
            _title.AddToClassList(UiTypographyClass.TitleHeadingFace);
            _title.SetKey(LocalizationKeys.Ui.PauseMenu.Title);
            panel.Add(_title);

            // ロケール印は DSL が言語の切り替わりを待つための名前
            // The locale marker is a name the DSL waits on to see the language switch
            _pageHost = new VisualElement();
            _pageHost.AddToClassList("pause-menu__page");
            panel.Add(_pageHost);

            // 生成直後は隠しておき、ルータの判断で SetShown が表示する
            // Start hidden; SetShown reveals it on the router's decision
            UiDisplay.SetShown(this, false);
        }

        public void SetShown(bool shown)
        {
            if (shown == _shown) return;
            _shown = shown;
            UiDisplay.SetShown(this, shown);
        }

        // 出た直後の 1 回描き。設定の値を控えてから頁を必ず作り直し、ロケール印を更新する
        // The one draw right after showing: keep the settings value, always rebuild the page and refresh the locale marker
        public void Redraw(PauseMenuViewModel pauseMenu, SettingsViewModel settings)
        {
            _settings = settings;
            ShowPage(pauseMenu);
            RenderLocaleMarker();
        }

        // 変化の反映は Redraw の後にだけ呼ぶ（頁が未作成のまま ApplyPauseMenu へ進まないため）
        // Change ports are called only after Redraw (so ApplyPauseMenu never runs before a page exists)
        public void RenderPauseMenu(PauseMenuViewModel model)
        {
            if (_shownPage != model.Page) ShowPage(model);
            _currentPage.ApplyPauseMenu(model);
        }

        public void RenderSettings(SettingsViewModel model)
        {
            _settings = model;
            _currentPage.ApplySettings(model);
        }

        public void RenderLocaleMarker()
        {
            _pageHost.name = $"pause-menu-locale-{Localize.GetCurrentLanguageCode()}";
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
                Show(new PauseMenuTopPageView(_commands, _osCursor));
            }

            void BuildSettings()
            {
                _title.SetKey(LocalizationKeys.Ui.PauseMenu.Settings);
                Show(new PauseMenuSettingsPage(_settings, _commands, _settingsCommands, _osCursor));
            }

            void BuildBugReport()
            {
                _title.SetKey(LocalizationKeys.Ui.PauseMenu.BugReport);
                Show(new PauseMenuBugReportPage(model, _commands, _toasts, _draft, _osCursor));

            }

            void Show(IPauseMenuPage page)
            {
                page.ApplyPauseMenu(model);
                _currentPage = page;
                _pageHost.Add(page.Root);
            }

            #endregion
        }
    }
}

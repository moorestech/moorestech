using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BugReport;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズのバグ報告ページ。フォーム 1 節を子画面の枠に置く
    /// Pause bug-report page: the single form section inside the sub-page frame
    /// </summary>
    public sealed class PauseMenuBugReportPage : IPauseMenuPage
    {
        private readonly PauseMenuSubPageView _subPage;
        private readonly BugReportFormView _form;

        public PauseMenuBugReportPage(PauseMenuViewModel model, IPauseMenuCommands commands, GameUiToastFeed toasts, UiCommandResultToaster toaster, BugReportDraft draft, IUiOsCursor osCursor)
        {
            _subPage = new PauseMenuSubPageView(commands, osCursor);
            _form = new BugReportFormView(model, commands, toasts, toaster, draft, osCursor);

            _subPage.AddSection(_form);
        }

        public VisualElement Root => _subPage;

        public void ApplyPauseMenu(PauseMenuViewModel model)
        {
            _form.Apply(model);
        }

        public void ApplySettings(SettingsViewModel model)
        {
        }
    }
}

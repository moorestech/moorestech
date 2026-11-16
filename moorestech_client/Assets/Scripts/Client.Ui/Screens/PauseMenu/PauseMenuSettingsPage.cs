using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.Settings;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズの設定ページ。言語・ダッシュ・画質の 3 節を子画面の枠に並べる
    /// Pause settings page: language, sprint and graphics sections inside the sub-page frame
    /// </summary>
    public sealed class PauseMenuSettingsPage : IPauseMenuPage
    {
        private readonly PauseMenuSubPageView _subPage;
        private readonly LanguageSelectView _language;
        private readonly SprintModeSelectView _sprint;
        private readonly GraphicsQualitySettingsView _graphics;

        public PauseMenuSettingsPage(SettingsViewModel settings, IPauseMenuCommands commands, ISettingsCommands settingsCommands, UiCommandResultToaster toaster, IUiOsCursor osCursor)
        {
            _subPage = new PauseMenuSubPageView(commands, osCursor);
            _language = new LanguageSelectView(settings, settingsCommands, toaster, osCursor);
            _sprint = new SprintModeSelectView(settings, settingsCommands, osCursor);
            _graphics = new GraphicsQualitySettingsView(settings, settingsCommands, osCursor);

            _subPage.AddSection(_language);
            _subPage.AddSection(_sprint);
            _subPage.AddSection(_graphics);
        }

        public VisualElement Root => _subPage;

        public void ApplyPauseMenu(PauseMenuViewModel model)
        {
        }

        public void ApplySettings(SettingsViewModel model)
        {
            _language.Apply(model);
            _sprint.Apply(model);
            _graphics.Apply(model);
        }
    }
}

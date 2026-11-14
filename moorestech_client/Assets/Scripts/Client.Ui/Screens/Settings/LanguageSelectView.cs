using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Text;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.Components.Controls;
using Client.Ui.State.Commands;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Settings
{
    /// <summary>
    /// 言語の縦の択一（Web の LanguageSelect）。並びは LanguageCatalog の順、表示名は翻訳しない
    /// Vertical language selector (Web LanguageSelect); ordered as LanguageCatalog, display names untranslated
    /// </summary>
    public sealed class LanguageSelectView : VisualElement
    {
        private const string SetLocaleCommandName = "localization.setLocale";

        private readonly ISettingsCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly ModeSwitch<string> _languages;

        public LanguageSelectView(SettingsViewModel initial, ISettingsCommands commands, UiCommandResultToaster toaster, IUiOsCursor osCursor)
        {
            _commands = commands;
            _toaster = toaster;
            AddToClassList("settings-section");
            Add(SettingsSectionParts.Title(LocalizationKeys.Ui.Settings.Language));

            _languages = new ModeSwitch<string>("language-select", ModeSwitchOrientation.Vertical, ModeSwitchRole.Group, LanguageOptions(), osCursor);

            _languages.OnOptionSelected.Subscribe(OnLanguageSelected);
            Add(_languages);
            Apply(initial);
        }

        public void Apply(SettingsViewModel model)
        {
            _languages.SetSelected(model.LanguageCode);
        }

        // トーストはToasterが決める
        // The toaster decides the toast
        private void OnLanguageSelected(string languageCode)
        {
            _toaster.Report(SetLocaleCommandName, _commands.SetLanguage(languageCode));
        }

        private static IReadOnlyList<ModeSwitchOption<string>> LanguageOptions()
        {
            return LanguageCatalog.Languages
                .Select(language => new ModeSwitchOption<string>($"language-select-option-{language.Code}", language.Code, UiText.FromVerbatim(language.DisplayName)))
                .ToList();
        }
    }
}

using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Text;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.Components.Controls;
using Client.Ui.State.Settings;
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
        private readonly ISettingsCommands _commands;
        private readonly ModeSwitch<string> _languages;

        public LanguageSelectView(SettingsViewModel initial, ISettingsCommands commands, IUiOsCursor osCursor)
        {
            _commands = commands;
            AddToClassList("settings-section");
            Add(SettingsSectionParts.Title(LocalizationKeys.Ui.Settings.Language));

            _languages = new ModeSwitch<string>("language-select", ModeSwitchOrientation.Vertical, ModeSwitchRole.Group, LanguageOptions(), osCursor);

            _languages.OnOptionSelected.Subscribe(OnLanguageSelected);
            Add(_languages);
            Apply(initial);

            #region Internal

            static IReadOnlyList<ModeSwitchOption<string>> LanguageOptions()
            {
                return LanguageCatalog.Languages
                    .Select(language => new ModeSwitchOption<string>($"language-select-option-{language.Code}", language.Code, UiText.FromVerbatim(language.DisplayName)))
                    .ToList();
            }

            #endregion
        }

        public void Apply(SettingsViewModel model)
        {
            _languages.SetSelected(model.LanguageCode);
        }

        private void OnLanguageSelected(string languageCode)
        {
            _commands.SetLanguage(languageCode);
        }
    }
}

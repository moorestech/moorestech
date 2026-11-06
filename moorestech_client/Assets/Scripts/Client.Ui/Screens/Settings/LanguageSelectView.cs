using System.Collections.Generic;
using System.Linq;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Assets;
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
        private readonly GameUiToastFeed _toasts;
        private readonly ModeSwitch<string> _languages;

        public LanguageSelectView(SettingsViewModel initial, ISettingsCommands commands, GameUiToastFeed toasts, UiAssetCatalog catalog)
        {
            _commands = commands;
            _toasts = toasts;
            styleSheets.Add(catalog.Settings);
            AddToClassList("settings-section");
            Add(SettingsSectionParts.Title(LocalizationKeys.Ui.Settings.Language));

            _languages = new ModeSwitch<string>("language-select", ModeSwitchOrientation.Vertical, LanguageOptions(), catalog.ModeSwitch);
            _languages.OnSelected.Subscribe(OnLanguageSelected);
            Add(_languages);
            Apply(initial);
        }

        public void Apply(SettingsViewModel model)
        {
            _languages.SetSelected(model.LanguageCode);
        }

        // 拒否はトーストで知らせる。トーストを出すのは結果を受けた View（D-8）
        // A rejection is surfaced as a toast; the view that received the result shows it (D-8)
        private void OnLanguageSelected(string languageCode)
        {
            if (_commands.SetLanguage(languageCode) is UiCommandRejected rejected) _toasts.ShowRejected(SetLocaleCommandName, rejected);
        }

        private static IReadOnlyList<ModeSwitchOption<string>> LanguageOptions()
        {
            return LanguageCatalog.Languages
                .Select(language => new ModeSwitchOption<string>($"language-select-option-{language.Code}", language.Code, null, language.DisplayName))
                .ToList();
        }
    }
}

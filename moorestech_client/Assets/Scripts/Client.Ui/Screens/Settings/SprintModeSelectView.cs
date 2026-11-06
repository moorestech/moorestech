using System.Collections.Generic;
using Client.Input;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Assets;
using Client.Ui.State.Settings;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Settings
{
    /// <summary>
    /// 走りの操作方式の横の択一と復旧印（Web の SprintModeSelect）
    /// Horizontal sprint input-mode selector with the recovery notice (Web SprintModeSelect)
    /// </summary>
    public sealed class SprintModeSelectView : VisualElement
    {
        private readonly VisualElement _recovered;
        private readonly ModeSwitch<SprintInputMode> _modes;

        public SprintModeSelectView(SettingsViewModel initial, ISettingsCommands commands, UiAssetCatalog catalog)
        {
            styleSheets.Add(catalog.Settings);
            AddToClassList("settings-section");
            Add(SettingsSectionParts.Title(LocalizationKeys.Ui.Settings.SprintMode));
            _recovered = SettingsSectionParts.Note("sprint-mode-recovered", LocalizationKeys.Ui.Settings.SprintModeRecovered);
            Add(_recovered);

            var options = new List<ModeSwitchOption<SprintInputMode>>
            {
                new("sprint-mode-option-toggle", SprintInputMode.Toggle, LocalizationKeys.Ui.Settings.SprintModeToggle, null),
                new("sprint-mode-option-hold", SprintInputMode.Hold, LocalizationKeys.Ui.Settings.SprintModeHold, null),
            };
            _modes = new ModeSwitch<SprintInputMode>("sprint-mode-select", ModeSwitchOrientation.Horizontal, options, catalog.ModeSwitch);
            _modes.OnSelected.Subscribe(commands.SetSprintInputMode);
            Add(_modes);
            Apply(initial);
        }

        public void Apply(SettingsViewModel model)
        {
            SettingsSectionParts.ShowIf(_recovered, model.SprintRecovered);
            _modes.SetSelected(model.SprintMode);
        }
    }
}

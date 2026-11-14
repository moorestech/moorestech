using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.Text;
using System.Collections.Generic;
using Client.Game.InGame.GraphicsQuality;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.Settings.GraphicsQualityRows;
using Client.Ui.State.Settings;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Settings
{
    /// <summary>
    /// 画質のプリセットと各項目（Web の GraphicsQualitySettings）。現在値は SettingsViewModel を正とする
    /// Graphics preset and items (Web GraphicsQualitySettings); SettingsViewModel is authoritative for the current values
    /// </summary>
    public sealed class GraphicsQualitySettingsView : VisualElement
    {
        private readonly ISettingsCommands _commands;
        private readonly VisualElement _recovered;
        private readonly ModeSwitch<GraphicsQualityPreset> _preset;
        private readonly VisualElement _custom;
        private readonly List<IGraphicsQualityRowSwitch> _itemSwitches = new();

        public GraphicsQualitySettingsView(SettingsViewModel initial, ISettingsCommands commands, IUiOsCursor osCursor)
        {
            _commands = commands;
            AddToClassList("settings-section");
            Add(SettingsSectionParts.Title(LocalizationKeys.Ui.Settings.Graphics));
            _recovered = SettingsSectionParts.Note("graphics-recovered", LocalizationKeys.Ui.Settings.GraphicsRecovered);
            Add(_recovered);

            // カスタムはどの選択肢にも一致しないので無選択にし、1 行で知らせる
            // Custom matches no option, so it shows no selection plus a one-line notice
            Add(SettingsSectionParts.Caption(LocalizationKeys.Ui.Settings.GraphicsPreset));
            _preset = new ModeSwitch<GraphicsQualityPreset>("graphics-preset", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, PresetOptions(), osCursor);
            _preset.OnOptionSelected.Subscribe(_commands.ApplyGraphicsPreset);
            Add(_preset);
            _custom = SettingsSectionParts.Note("graphics-preset-custom", LocalizationKeys.Ui.Settings.GraphicsPresetCustom);
            Add(_custom);

            foreach (var row in GraphicsQualityItemRows.All)
            {
                Add(SettingsSectionParts.Caption(row.Caption));
                var itemSwitch = row.CreateSwitch(_commands, osCursor);

                _itemSwitches.Add(itemSwitch);
                Add(itemSwitch.Element);
            }

            Apply(initial);
        }

        public void Apply(SettingsViewModel model)
        {
            UiDisplay.SetShown(_recovered, model.GraphicsRecovered);
            UiDisplay.SetShown(_custom, model.Preset == GraphicsQualityPresetChoice.Custom);
            switch (model.Preset)
            {
                case GraphicsQualityPresetChoice.High: _preset.SetSelected(GraphicsQualityPreset.High); break;
                case GraphicsQualityPresetChoice.Medium: _preset.SetSelected(GraphicsQualityPreset.Medium); break;
                case GraphicsQualityPresetChoice.Low: _preset.SetSelected(GraphicsQualityPreset.Low); break;
                case GraphicsQualityPresetChoice.Custom: _preset.SetSelectedNone(); break;
                default: throw new System.ArgumentOutOfRangeException(nameof(model), model.Preset, null);
            }
            foreach (var itemSwitch in _itemSwitches) itemSwitch.Apply(model.Graphics);
        }

        private static IReadOnlyList<ModeSwitchOption<GraphicsQualityPreset>> PresetOptions()
        {
            return new List<ModeSwitchOption<GraphicsQualityPreset>>
            {
                new("graphics-preset-high", GraphicsQualityPreset.High, new UiText(LocalizationKeys.Ui.Settings.GraphicsPresetHigh)),
                new("graphics-preset-medium", GraphicsQualityPreset.Medium, new UiText(LocalizationKeys.Ui.Settings.GraphicsPresetMedium)),
                new("graphics-preset-low", GraphicsQualityPreset.Low, new UiText(LocalizationKeys.Ui.Settings.GraphicsPresetLow)),
            };
        }
    }
}

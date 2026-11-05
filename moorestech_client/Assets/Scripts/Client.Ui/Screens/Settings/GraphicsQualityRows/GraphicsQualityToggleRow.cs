using System;
using System.Collections.Generic;
using Client.Game.InGame.GraphicsQuality;
using Client.Ui.Components;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.Settings.GraphicsQualityRows
{
    public enum GraphicsQualityToggle
    {
        Shadows,
        Ssao,
        PostProcessing,
        Effects,
        MapObjects,
        Ocean,
    }

    /// <summary>
    /// オン/オフの行。値は bool のまま運び、綴りの文字列を経由しない
    /// An on/off row; the value travels as a bool and never through a spelled string
    /// </summary>
    public sealed class GraphicsQualityToggleRow : GraphicsQualityRow<bool>
    {
        private readonly GraphicsQualityToggle _toggle;

        // testid の語は Web の GraphicsQualitySettings.tsx と同じ綴り
        // The testid word uses the same spelling as Web GraphicsQualitySettings.tsx
        public GraphicsQualityToggleRow(GraphicsQualityToggle toggle, LocalizationKey caption, string testidWord) : base(caption, $"graphics-{testidWord}", new List<ModeSwitchOption<bool>>
        {
            new($"graphics-{testidWord}-on", true, LocalizationKeys.Ui.Settings.On, null),
            new($"graphics-{testidWord}-off", false, LocalizationKeys.Ui.Settings.Off, null),
        })
        {
            _toggle = toggle;
        }

        public override bool Read(GraphicsQualitySettings settings)
        {
            return _toggle switch
            {
                GraphicsQualityToggle.Shadows => settings.ShadowsEnabled,
                GraphicsQualityToggle.Ssao => settings.SsaoEnabled,
                GraphicsQualityToggle.PostProcessing => settings.PostProcessingEnabled,
                GraphicsQualityToggle.Effects => settings.EffectsEnabled,
                GraphicsQualityToggle.MapObjects => settings.MapObjectsVisible,
                GraphicsQualityToggle.Ocean => settings.OceanVisible,
                _ => throw new ArgumentOutOfRangeException(nameof(_toggle), _toggle, null),
            };
        }

        public override GraphicsQualitySettings With(GraphicsQualitySettings settings, bool value)
        {
            return _toggle switch
            {
                GraphicsQualityToggle.Shadows => settings.WithShadowsEnabled(value),
                GraphicsQualityToggle.Ssao => settings.WithSsaoEnabled(value),
                GraphicsQualityToggle.PostProcessing => settings.WithPostProcessingEnabled(value),
                GraphicsQualityToggle.Effects => settings.WithEffectsEnabled(value),
                GraphicsQualityToggle.MapObjects => settings.WithMapObjectsVisible(value),
                GraphicsQualityToggle.Ocean => settings.WithOceanVisible(value),
                _ => throw new ArgumentOutOfRangeException(nameof(_toggle), _toggle, null),
            };
        }
    }
}

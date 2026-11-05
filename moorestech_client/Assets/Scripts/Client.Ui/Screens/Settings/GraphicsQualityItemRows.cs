using System;
using System.Collections.Generic;
using Client.Game.InGame.GraphicsQuality;
using Client.Ui.Components;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.Settings
{
    public enum GraphicsQualityItem
    {
        DrawDistance,
        Shadows,
        RenderScale,
        Ssao,
        PostProcessing,
        Effects,
        MapObjects,
        TerrainDetail,
        Ocean,
    }

    /// <summary>
    /// 画質の 1 項目の行。見出し・ModeSwitch の testid・選択肢（値は Web と同じ綴りの文字列）
    /// One graphics-quality row: caption, ModeSwitch testid and options whose values use the Web spellings
    /// </summary>
    public sealed class GraphicsQualityItemRow
    {
        public GraphicsQualityItem Item { get; }
        public LocalizationKey Caption { get; }
        public string Testid { get; }
        public IReadOnlyList<ModeSwitchOption<string>> Options { get; }

        public GraphicsQualityItemRow(GraphicsQualityItem item, LocalizationKey caption, string testid, IReadOnlyList<ModeSwitchOption<string>> options)
        {
            Item = item;
            Caption = caption;
            Testid = testid;
            Options = options;
        }
    }

    /// <summary>
    /// 画質の項目の表（Web の GraphicsQualitySettings.tsx と同じ順・testid）と、設定値との読み書き
    /// Table of graphics-quality items (same order and testids as Web GraphicsQualitySettings.tsx) and their reads and writes on the settings value
    /// </summary>
    public static class GraphicsQualityItemRows
    {
        private const string On = "on";
        private const string Off = "off";

        public static readonly IReadOnlyList<GraphicsQualityItemRow> All = new List<GraphicsQualityItemRow>
        {
            new(GraphicsQualityItem.DrawDistance, LocalizationKeys.Ui.Settings.GraphicsDrawDistance, "graphics-draw-distance", Literals("graphics-draw-distance", "m", "300", "1000", "5000")),
            BoolRow(GraphicsQualityItem.Shadows, LocalizationKeys.Ui.Settings.GraphicsShadows, "shadows"),
            new(GraphicsQualityItem.RenderScale, LocalizationKeys.Ui.Settings.GraphicsRenderScale, "graphics-render-scale", Literals("graphics-render-scale", "%", "100", "75", "50")),
            BoolRow(GraphicsQualityItem.Ssao, LocalizationKeys.Ui.Settings.GraphicsSsao, "ssao"),
            BoolRow(GraphicsQualityItem.PostProcessing, LocalizationKeys.Ui.Settings.GraphicsPostProcessing, "postProcessing"),
            BoolRow(GraphicsQualityItem.Effects, LocalizationKeys.Ui.Settings.GraphicsEffects, "effects"),
            BoolRow(GraphicsQualityItem.MapObjects, LocalizationKeys.Ui.Settings.GraphicsMapObjects, "mapObjects"),
            new(GraphicsQualityItem.TerrainDetail, LocalizationKeys.Ui.Settings.GraphicsTerrainDetail, "graphics-terrain", new List<ModeSwitchOption<string>>
            {
                new("graphics-terrain-fine", "fine", LocalizationKeys.Ui.Settings.GraphicsTerrainFine, null),
                new("graphics-terrain-coarse", "coarse", LocalizationKeys.Ui.Settings.GraphicsTerrainCoarse, null),
            }),
            BoolRow(GraphicsQualityItem.Ocean, LocalizationKeys.Ui.Settings.GraphicsOcean, "ocean"),
        };

        public static string ValueOf(GraphicsQualityItem item, GraphicsQualitySettings settings)
        {
            return item switch
            {
                GraphicsQualityItem.DrawDistance => settings.DrawDistance switch { GraphicsDrawDistance.Meters300 => "300", GraphicsDrawDistance.Meters1000 => "1000", GraphicsDrawDistance.Meters5000 => "5000", _ => throw new ArgumentOutOfRangeException(nameof(settings)) },
                GraphicsQualityItem.Shadows => OnOff(settings.ShadowsEnabled),
                GraphicsQualityItem.RenderScale => settings.RenderScale switch { GraphicsRenderScale.Percent100 => "100", GraphicsRenderScale.Percent75 => "75", GraphicsRenderScale.Percent50 => "50", _ => throw new ArgumentOutOfRangeException(nameof(settings)) },
                GraphicsQualityItem.Ssao => OnOff(settings.SsaoEnabled),
                GraphicsQualityItem.PostProcessing => OnOff(settings.PostProcessingEnabled),
                GraphicsQualityItem.Effects => OnOff(settings.EffectsEnabled),
                GraphicsQualityItem.MapObjects => OnOff(settings.MapObjectsVisible),
                GraphicsQualityItem.TerrainDetail => settings.TerrainDetail == GraphicsTerrainDetail.Fine ? "fine" : "coarse",
                GraphicsQualityItem.Ocean => OnOff(settings.OceanVisible),
                _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
            };
        }

        // 今の設定値の 1 項目だけを差し替えた値を作る
        // Builds the current settings with exactly one item replaced
        public static GraphicsQualitySettings WithValue(GraphicsQualityItem item, GraphicsQualitySettings s, string value)
        {
            return item switch
            {
                GraphicsQualityItem.DrawDistance => new GraphicsQualitySettings(ParseDrawDistance(value), s.ShadowsEnabled, s.RenderScale, s.SsaoEnabled, s.PostProcessingEnabled, s.EffectsEnabled, s.MapObjectsVisible, s.TerrainDetail, s.OceanVisible),
                GraphicsQualityItem.Shadows => new GraphicsQualitySettings(s.DrawDistance, value == On, s.RenderScale, s.SsaoEnabled, s.PostProcessingEnabled, s.EffectsEnabled, s.MapObjectsVisible, s.TerrainDetail, s.OceanVisible),
                GraphicsQualityItem.RenderScale => new GraphicsQualitySettings(s.DrawDistance, s.ShadowsEnabled, ParseRenderScale(value), s.SsaoEnabled, s.PostProcessingEnabled, s.EffectsEnabled, s.MapObjectsVisible, s.TerrainDetail, s.OceanVisible),
                GraphicsQualityItem.Ssao => new GraphicsQualitySettings(s.DrawDistance, s.ShadowsEnabled, s.RenderScale, value == On, s.PostProcessingEnabled, s.EffectsEnabled, s.MapObjectsVisible, s.TerrainDetail, s.OceanVisible),
                GraphicsQualityItem.PostProcessing => new GraphicsQualitySettings(s.DrawDistance, s.ShadowsEnabled, s.RenderScale, s.SsaoEnabled, value == On, s.EffectsEnabled, s.MapObjectsVisible, s.TerrainDetail, s.OceanVisible),
                GraphicsQualityItem.Effects => new GraphicsQualitySettings(s.DrawDistance, s.ShadowsEnabled, s.RenderScale, s.SsaoEnabled, s.PostProcessingEnabled, value == On, s.MapObjectsVisible, s.TerrainDetail, s.OceanVisible),
                GraphicsQualityItem.MapObjects => new GraphicsQualitySettings(s.DrawDistance, s.ShadowsEnabled, s.RenderScale, s.SsaoEnabled, s.PostProcessingEnabled, s.EffectsEnabled, value == On, s.TerrainDetail, s.OceanVisible),
                GraphicsQualityItem.TerrainDetail => new GraphicsQualitySettings(s.DrawDistance, s.ShadowsEnabled, s.RenderScale, s.SsaoEnabled, s.PostProcessingEnabled, s.EffectsEnabled, s.MapObjectsVisible, value == "fine" ? GraphicsTerrainDetail.Fine : GraphicsTerrainDetail.Coarse, s.OceanVisible),
                GraphicsQualityItem.Ocean => new GraphicsQualitySettings(s.DrawDistance, s.ShadowsEnabled, s.RenderScale, s.SsaoEnabled, s.PostProcessingEnabled, s.EffectsEnabled, s.MapObjectsVisible, s.TerrainDetail, value == On),
                _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
            };
        }

        private static GraphicsQualityItemRow BoolRow(GraphicsQualityItem item, LocalizationKey caption, string key)
        {
            return new GraphicsQualityItemRow(item, caption, $"graphics-{key}", new List<ModeSwitchOption<string>>
            {
                new($"graphics-{key}-on", On, LocalizationKeys.Ui.Settings.On, null),
                new($"graphics-{key}-off", Off, LocalizationKeys.Ui.Settings.Off, null),
            });
        }

        // 数値の選択肢は翻訳しない「300m」「75%」の表示で出す
        // Numeric options display untranslated labels such as "300m" and "75%"
        private static IReadOnlyList<ModeSwitchOption<string>> Literals(string testid, string unit, params string[] values)
        {
            var options = new List<ModeSwitchOption<string>>();
            foreach (var value in values) options.Add(new ModeSwitchOption<string>($"{testid}-{value}", value, null, value + unit));
            return options;
        }

        private static string OnOff(bool enabled)
        {
            return enabled ? On : Off;
        }

        private static GraphicsDrawDistance ParseDrawDistance(string value)
        {
            return value switch
            {
                "300" => GraphicsDrawDistance.Meters300,
                "1000" => GraphicsDrawDistance.Meters1000,
                "5000" => GraphicsDrawDistance.Meters5000,
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
            };
        }

        private static GraphicsRenderScale ParseRenderScale(string value)
        {
            return value switch
            {
                "100" => GraphicsRenderScale.Percent100,
                "75" => GraphicsRenderScale.Percent75,
                "50" => GraphicsRenderScale.Percent50,
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
            };
        }
    }
}

using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.Settings.GraphicsQualityRows
{
    /// <summary>
    /// 画質の項目の表（Web の GraphicsQualitySettings.tsx と同じ順・testid）
    /// Table of graphics-quality items (same order and testids as Web GraphicsQualitySettings.tsx)
    /// </summary>
    public static class GraphicsQualityItemRows
    {
        public static readonly IReadOnlyList<IGraphicsQualityRow> All = new List<IGraphicsQualityRow>
        {
            new GraphicsDrawDistanceRow(),
            new GraphicsQualityToggleRow(GraphicsQualityToggle.Shadows, LocalizationKeys.Ui.Settings.GraphicsShadows, "shadows"),
            new GraphicsRenderScaleRow(),
            new GraphicsQualityToggleRow(GraphicsQualityToggle.Ssao, LocalizationKeys.Ui.Settings.GraphicsSsao, "ssao"),
            new GraphicsQualityToggleRow(GraphicsQualityToggle.PostProcessing, LocalizationKeys.Ui.Settings.GraphicsPostProcessing, "postProcessing"),
            new GraphicsQualityToggleRow(GraphicsQualityToggle.Effects, LocalizationKeys.Ui.Settings.GraphicsEffects, "effects"),
            new GraphicsQualityToggleRow(GraphicsQualityToggle.MapObjects, LocalizationKeys.Ui.Settings.GraphicsMapObjects, "mapObjects"),
            new GraphicsTerrainDetailRow(),
            new GraphicsQualityToggleRow(GraphicsQualityToggle.Ocean, LocalizationKeys.Ui.Settings.GraphicsOcean, "ocean"),
        };
    }
}

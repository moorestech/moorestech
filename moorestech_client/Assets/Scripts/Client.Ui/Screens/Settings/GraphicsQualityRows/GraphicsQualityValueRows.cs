using Client.Ui.State.Text;
using System;
using System.Collections.Generic;
using Client.Game.InGame.GraphicsQuality;
using Client.Ui.Components.Controls;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.Settings.GraphicsQualityRows
{
    // 描画距離。選択肢は enum の全値から組むので、値を足せば行も増え、未知値で画面が開けなくなることは無い
    // Draw distance; options are built from every enum value, so a new value adds an option instead of breaking the screen
    public sealed class GraphicsDrawDistanceRow : GraphicsQualityRow<GraphicsDrawDistance>
    {
        private const string RowTestid = "graphics-draw-distance";

        public GraphicsDrawDistanceRow() : base(LocalizationKeys.Ui.Settings.GraphicsDrawDistance, RowTestid, BuildOptions())
        {
        }

        public override GraphicsDrawDistance Read(GraphicsQualitySettings settings) => settings.DrawDistance;
        public override GraphicsQualitySettings With(GraphicsQualitySettings settings, GraphicsDrawDistance value) => settings.WithDrawDistance(value);

        // 数値の選択肢は翻訳しない「300m」の表示で出す
        // Numeric options display untranslated labels such as "300m"
        private static IReadOnlyList<ModeSwitchOption<GraphicsDrawDistance>> BuildOptions()
        {
            var options = new List<ModeSwitchOption<GraphicsDrawDistance>>();
            foreach (GraphicsDrawDistance value in Enum.GetValues(typeof(GraphicsDrawDistance)))
            {
                var meters = (int)value.ToMeters();
                options.Add(new ModeSwitchOption<GraphicsDrawDistance>($"{RowTestid}-{meters}", value, UiText.FromVerbatim($"{meters}m")));
            }
            return options;
        }
    }

    // 解像度倍率。選択肢は enum の全値から組む
    // Render scale; options are built from every enum value
    public sealed class GraphicsRenderScaleRow : GraphicsQualityRow<GraphicsRenderScale>
    {
        private const string RowTestid = "graphics-render-scale";

        public GraphicsRenderScaleRow() : base(LocalizationKeys.Ui.Settings.GraphicsRenderScale, RowTestid, BuildOptions())
        {
        }

        public override GraphicsRenderScale Read(GraphicsQualitySettings settings) => settings.RenderScale;
        public override GraphicsQualitySettings With(GraphicsQualitySettings settings, GraphicsRenderScale value) => settings.WithRenderScale(value);

        private static IReadOnlyList<ModeSwitchOption<GraphicsRenderScale>> BuildOptions()
        {
            var options = new List<ModeSwitchOption<GraphicsRenderScale>>();
            foreach (GraphicsRenderScale value in Enum.GetValues(typeof(GraphicsRenderScale)))
            {
                var percent = value.ToPercent();
                options.Add(new ModeSwitchOption<GraphicsRenderScale>($"{RowTestid}-{percent}", value, UiText.FromVerbatim($"{percent}%")));
            }
            return options;
        }
    }

    public sealed class GraphicsTerrainDetailRow : GraphicsQualityRow<GraphicsTerrainDetail>
    {
        public GraphicsTerrainDetailRow() : base(LocalizationKeys.Ui.Settings.GraphicsTerrainDetail, "graphics-terrain", new List<ModeSwitchOption<GraphicsTerrainDetail>>
        {
            new("graphics-terrain-fine", GraphicsTerrainDetail.Fine, new UiText(LocalizationKeys.Ui.Settings.GraphicsTerrainFine)),
            new("graphics-terrain-coarse", GraphicsTerrainDetail.Coarse, new UiText(LocalizationKeys.Ui.Settings.GraphicsTerrainCoarse)),
        })
        {
        }

        public override GraphicsTerrainDetail Read(GraphicsQualitySettings settings) => settings.TerrainDetail;
        public override GraphicsQualitySettings With(GraphicsQualitySettings settings, GraphicsTerrainDetail value) => settings.WithTerrainDetail(value);
    }
}

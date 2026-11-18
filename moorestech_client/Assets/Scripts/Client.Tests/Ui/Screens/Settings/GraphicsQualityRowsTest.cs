using System;
using System.Linq;
using Client.Game.InGame.GraphicsQuality;
using Client.Ui.Screens.Settings.GraphicsQualityRows;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.Settings
{
    // 行は型付きの値を往復し、選択肢は enum の全値を覆う（綴り違いで黙って片倒しにならない・値を足しても画面が開ける）
    // Rows round-trip typed values and their options cover every enum value (no silent fallback on a misspelling; a new value never breaks the screen)
    public class GraphicsQualityRowsTest
    {
        private static readonly GraphicsQualitySettings Medium = GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium);

        [Test]
        public void 数値行の選択肢はenumの全値を覆い値を往復する()
        {
            var drawDistance = new GraphicsDrawDistanceRow();
            CollectionAssert.AreEqual(Enum.GetValues(typeof(GraphicsDrawDistance)), drawDistance.Options.Select(option => option.Value));
            foreach (var option in drawDistance.Options) Assert.AreEqual(option.Value, drawDistance.Read(drawDistance.With(Medium, option.Value)));

            var renderScale = new GraphicsRenderScaleRow();
            CollectionAssert.AreEqual(Enum.GetValues(typeof(GraphicsRenderScale)), renderScale.Options.Select(option => option.Value));
            foreach (var option in renderScale.Options) Assert.AreEqual(option.Value, renderScale.Read(renderScale.With(Medium, option.Value)));
        }

        [Test]
        public void オンオフ行は自分の項目だけを書き換える()
        {
            foreach (GraphicsQualityToggle toggle in Enum.GetValues(typeof(GraphicsQualityToggle)))
            {
                var row = new GraphicsQualityToggleRow(toggle, default, "x");
                var flipped = row.With(Medium, !row.Read(Medium));
                Assert.AreEqual(!row.Read(Medium), row.Read(flipped), $"{toggle} が書き換わっていない");
                Assert.AreEqual(1, DifferingItemCount(Medium, flipped), $"{toggle} 以外の項目も変わった");
            }
        }

        [TestCase(GraphicsQualityToggle.Shadows)]
        [TestCase(GraphicsQualityToggle.Ssao)]
        [TestCase(GraphicsQualityToggle.PostProcessing)]
        [TestCase(GraphicsQualityToggle.Effects)]
        [TestCase(GraphicsQualityToggle.MapObjects)]
        [TestCase(GraphicsQualityToggle.Ocean)]
        public void オンオフ行は名前どおりの項目を書き換える(GraphicsQualityToggle toggle)
        {
            var flipped = new GraphicsQualityToggleRow(toggle, default, "x").With(Medium, false);

            Assert.IsFalse(ReadByName(flipped, toggle), $"{toggle} の名前の項目が書き換わっていない");
        }

        [Test]
        public void 表は全てのオンオフ項目を1行ずつ持つ()
        {
            Assert.AreEqual(Enum.GetValues(typeof(GraphicsQualityToggle)).Length + 3, GraphicsQualityItemRows.All.Count);
        }

        // 行の実装でなく項目の名前から読む（行の読み書きが入れ替わっても気づける）
        // Read by the item's name, not through the row, so a swapped read/write is caught
        private static bool ReadByName(GraphicsQualitySettings settings, GraphicsQualityToggle toggle)
        {
            return toggle switch
            {
                GraphicsQualityToggle.Shadows => settings.ShadowsEnabled,
                GraphicsQualityToggle.Ssao => settings.SsaoEnabled,
                GraphicsQualityToggle.PostProcessing => settings.PostProcessingEnabled,
                GraphicsQualityToggle.Effects => settings.EffectsEnabled,
                GraphicsQualityToggle.MapObjects => settings.MapObjectsVisible,
                GraphicsQualityToggle.Ocean => settings.OceanVisible,
                _ => throw new ArgumentOutOfRangeException(nameof(toggle), toggle, null),
            };
        }

        private static int DifferingItemCount(GraphicsQualitySettings a, GraphicsQualitySettings b)
        {
            var differences = new[]
            {
                a.DrawDistance != b.DrawDistance, a.ShadowsEnabled != b.ShadowsEnabled, a.RenderScale != b.RenderScale,
                a.SsaoEnabled != b.SsaoEnabled, a.PostProcessingEnabled != b.PostProcessingEnabled, a.EffectsEnabled != b.EffectsEnabled,
                a.MapObjectsVisible != b.MapObjectsVisible, a.TerrainDetail != b.TerrainDetail, a.OceanVisible != b.OceanVisible,
            };
            return differences.Count(different => different);
        }
    }
}

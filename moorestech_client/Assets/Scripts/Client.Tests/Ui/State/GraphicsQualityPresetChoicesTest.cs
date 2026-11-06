using Client.Game.InGame.GraphicsQuality;
using Client.Ui.State.Settings;
using NUnit.Framework;

namespace Client.Tests.Ui.State
{
    /// <summary>
    /// プリセット導出とカスタムの確認
    /// Checks preset derivation and custom
    /// </summary>
    public class GraphicsQualityPresetChoicesTest
    {
        [TestCase(GraphicsQualityPreset.High, GraphicsQualityPresetChoice.High)]
        [TestCase(GraphicsQualityPreset.Medium, GraphicsQualityPresetChoice.Medium)]
        [TestCase(GraphicsQualityPreset.Low, GraphicsQualityPresetChoice.Low)]
        public void プリセットの値はそのプリセットを選ぶ(GraphicsQualityPreset preset, GraphicsQualityPresetChoice expected)
        {
            Assert.AreEqual(expected, GraphicsQualityPresetChoices.From(GraphicsQualityPresets.Get(preset)));
        }

        [Test]
        public void どのプリセットにも一致しなければカスタム()
        {
            // 中から影だけを切った値はどのプリセットとも一致しない
            // Medium with only shadows turned off matches no preset
            var custom = new GraphicsQualitySettings(GraphicsDrawDistance.Meters300, false, GraphicsRenderScale.Percent100, true, true, true, true, GraphicsTerrainDetail.Fine, true);
            Assert.AreEqual(GraphicsQualityPresetChoice.Custom, GraphicsQualityPresetChoices.From(custom));
        }
    }
}

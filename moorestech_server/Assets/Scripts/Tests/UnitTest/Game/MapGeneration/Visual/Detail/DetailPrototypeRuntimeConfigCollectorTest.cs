using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Biomes;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Detail
{
    /// <summary>
    ///     プロトタイプ設定の並びが密度マップの並びと同じ規則で決まることを検証する。キャッシュヒット時は
    ///     密度マップだけを復元してプロトタイプをここから引き直すため、この並びが崩れると別の草が描かれる
    ///     Verifies the prototype config order follows the same rule as the density maps; on a cache hit only the maps are
    ///     restored and the prototypes come from here, so a broken order draws the wrong plant
    /// </summary>
    public class DetailPrototypeRuntimeConfigCollectorTest
    {
        private static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland, BiomeType.Forest };

        [Test]
        public void ConcatenatesEntriesBiomeByBiomeSkippingEmptyOnes()
        {
            // 空バイオームが1本でも列を占めると、以降の全プロトタイプが1つずれて密度マップと食い違う
            // A single empty biome occupying a slot would shift every later prototype off its density map
            var prototypeConfigs = DetailPrototypeRuntimeConfigCollector.Collect(BiomeTypes, CreateVisualSections());

            Assert.That(prototypeConfigs.Count, Is.EqualTo(2));
            Assert.That(prototypeConfigs[0].minWidth, Is.EqualTo(1f));
            Assert.That(prototypeConfigs[1].minWidth, Is.EqualTo(2f));
        }

        private static BiomeVisualSections CreateVisualSections()
        {
            // プロトタイプ経路はDetailConfigsしか読まない。残り2本は並びを合わせるためだけに置く
            // The prototype path reads only DetailConfigs; the other two exist solely to keep the arrays parallel
            return new BiomeVisualSections(
                new string[BiomeTypes.Length], new BiomeTextureConfig[BiomeTypes.Length],
                new[] { CreateDetailConfig(), CreateDetailConfig(1f, 2f) },
                DetailTestConfigBuilder.CreateDisabledSurroundConfigs(BiomeTypes.Length));
        }

        private static BiomeDetailConfig CreateDetailConfig(params float[] entryMinWidths)
        {
            var entries = new DetailEntry[entryMinWidths.Length];
            for (var index = 0; index < entryMinWidths.Length; index++)
            {
                entries[index] = DetailTestConfigBuilder.CreateEntry(1f, 8);
                entries[index].prototypeConfig.minWidth = entryMinWidths[index];
            }

            return new BiomeDetailConfig { entries = entries, filterRejectThreshold = 0.01f, borderMargin = 0f };
        }
    }
}

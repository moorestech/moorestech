using Client.Game.InGame.Environment.Terrain.Build;
using Client.Game.InGame.Environment.Terrain.Visual;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain
{
    /// <summary>
    ///     バイオームを跨ぐDetailの積み上げを検証する。空バイオームのスキップでseedの添字がずれても、
    ///     prototypeとmapの対応が崩れても例外にはならず、草の分布が静かに変わるだけになる
    ///     Verifies detail accumulation across biomes. Neither a seed index shifted by the empty-biome skip nor a
    ///     broken prototype-to-map pairing throws; both only alter the grass distribution silently
    /// </summary>
    public class TerrainDetailBuilderTest
    {
        private const int Resolution = 5;
        private const int Seed = 4321;

        // 移植元と同じseed規則。空バイオームを飛ばしても添字は「有効バイオーム配列の位置」のまま
        // The source's seed rule; the index stays the position in the enabled-biome array even when a biome is skipped
        private const int DetailSeedBase = 6000;
        private const int DetailSeedStridePerBiome = 100;

        // 添字1のバイオームだけがエントリを持つ。0番は空でスキップされる
        // Only the biome at index 1 owns entries; index 0 is empty and gets skipped
        private const int PopulatedBiomeIndex = 1;

        private static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland, BiomeType.Forest };

        // 距離フィルタを有効にしていないので距離場は作られない。MasterHolder無しで回せる
        // No distance filter is enabled here, so no field is built and the fixture runs without MasterHolder
        private static readonly LedgerPlacement[] NoMapObjects = new LedgerPlacement[0];

        [Test]
        public void KeepsPrototypesAndMapsAlignedAcrossBiomes()
        {
            // 空バイオームのスキップ(TerrainDetailBuilder.csのentries.Length==0)自体はここから観測できない。
            // continueを消してもGenerateForBiomeが空リストを返すだけで出力が完全に一致するため、名前も範囲に合わせてある
            // The empty-biome skip is not observable here: dropping the continue only makes GenerateForBiome return
            // empty lists and the output stays identical, so the name is scoped to what this actually pins
            var visualSections = CreateVisualSections();
            var maps = TerrainDetailBuilder.Build(
                CreateConfig(), BiomeTypes, visualSections, CreateHeights(), CreateHeights(),
                CreateWinnerMasks(), null, null, NoMapObjects, Vector3.zero, 0, 0);
            var prototypes = TerrainDetailPrototypeList.Build(BiomeTypes, visualSections);

            Assert.That(prototypes.Count, Is.EqualTo(2));
            Assert.That(maps.Count, Is.EqualTo(prototypes.Count), "prototypeとmapは同数");

            // 同順であることは幅で見分ける。SetDetailLayerはこの並びの添字でmapを差すため入れ替わると別の草になる
            // Order is checked via width: SetDetailLayer indexes maps by this order, so a swap draws the wrong plant
            Assert.That(prototypes[0].minWidth, Is.EqualTo(1f));
            Assert.That(prototypes[1].minWidth, Is.EqualTo(2f));

            foreach (var map in maps)
            {
                Assert.That(map.GetLength(0), Is.EqualTo(Resolution - 1));
                Assert.That(map.GetLength(1), Is.EqualTo(Resolution - 1));
            }
        }

        [Test]
        public void DerivesTheBiomeSeedFromItsIndexInTheEnabledBiomeArray()
        {
            // 空バイオームを飛ばした結果 index が 0 に繰り下がっていないことを、密度マップの一致で直接見る
            // Compare density maps directly to see the index did not slip to 0 after skipping the empty biome
            var expected = GenerateDirectly(Seed + DetailSeedBase + PopulatedBiomeIndex * DetailSeedStridePerBiome);
            var slipped = GenerateDirectly(Seed + DetailSeedBase + 0 * DetailSeedStridePerBiome);

            var maps = TerrainDetailBuilder.Build(
                CreateConfig(), BiomeTypes, CreateVisualSections(), CreateHeights(), CreateHeights(),
                CreateWinnerMasks(), null, null, NoMapObjects, Vector3.zero, 0, 0);

            Assert.That(AreEqual(maps[0], expected), Is.True, "添字1のseedで生成されている");

            // 対照: 添字が0へ繰り下がった場合と結果が違うこと。同じならこのテストは何も見ていない
            // Control: the slipped index must differ, otherwise this test observes nothing
            Assert.That(AreEqual(expected, slipped), Is.False, "seedの添字項が結果を動かしている");
        }

        private static int[,] GenerateDirectly(int seed)
        {
            var config = CreateConfig();
            var heights = CreateHeights();
            var maps = DetailRuntimeGenerator.GenerateForBiome(
                CreateWinnerMasks()[PopulatedBiomeIndex],
                heights, TerrainSlopeCalculator.Compute(heights, config),
                TerrainDimensions.From(config, config.shoreConfig.waterMargin, 0, 0),
                CreatePopulatedDetailConfig(), new System.Random(seed), null, null, null, null);

            return maps[0];
        }

        private static bool AreEqual(int[,] left, int[,] right)
        {
            for (var z = 0; z < left.GetLength(0); z++)
            for (var x = 0; x < left.GetLength(1); x++)
                if (left[z, x] != right[z, x])
                    return false;

            return true;
        }

        private static BiomeVisualSections CreateVisualSections()
        {
            // Detail経路はDetailConfigsしか読まない。残り2本は並びを合わせるためだけに置く
            // The detail path reads only DetailConfigs; the other two exist solely to keep the arrays parallel
            return new BiomeVisualSections(
                new string[BiomeTypes.Length], new BiomeTextureConfig[BiomeTypes.Length],
                new[] { CreateEmptyDetailConfig(), CreatePopulatedDetailConfig() },
                DetailTestConfigBuilder.CreateDisabledSurroundConfigs(BiomeTypes.Length));
        }

        private static BiomeDetailConfig CreateEmptyDetailConfig()
        {
            return new BiomeDetailConfig { entries = new DetailEntry[0], filterRejectThreshold = 0.01f, borderMargin = 0f };
        }

        private static BiomeDetailConfig CreatePopulatedDetailConfig()
        {
            return new BiomeDetailConfig
            {
                entries = new[] { CreateNoisyEntry(1f), CreateNoisyEntry(2f) },
                filterRejectThreshold = 0.01f,
                borderMargin = 0f,
            };
        }

        // ノイズを効かせないとseedを変えても密度が動かず、seedの検証が素通りする
        // Without active noise the density ignores the seed entirely and the seed assertions pass vacuously
        private static DetailEntry CreateNoisyEntry(float minWidth)
        {
            var entry = DetailTestConfigBuilder.CreateEntry(1f, 8);
            entry.prototypeConfig.minWidth = minWidth;
            entry.noiseStack = DetailTestConfigBuilder.CreateInactiveNoiseStack();
            entry.noiseStack.primary = new DetailNoiseLayer
            {
                noiseType = MapNoiseType.FBM, frequency = 0.05f, amplitude = 1f, offset = 0f, balance = 0.5f,
            };
            return entry;
        }

        private static TerrainGenerationConfig CreateConfig()
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                seed = Seed,
                terrainWidth = 100f,
                terrainLength = 100f,
                terrainHeight = 50f,
            };
        }

        private static float[,] CreateHeights()
        {
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = x / (float)(Resolution - 1) * 0.5f;
            return heights;
        }

        // 添字1のバイオームがタイル全面で勝つ配置。0番は勝者ゼロなので密度マップも空になる
        // The biome at index 1 wins the whole tile; index 0 wins nowhere, so its density maps stay empty
        private static bool[][,] CreateWinnerMasks()
        {
            var masks = new bool[BiomeTypes.Length][,];
            for (var biomeIndex = 0; biomeIndex < BiomeTypes.Length; biomeIndex++)
            {
                masks[biomeIndex] = new bool[Resolution, Resolution];
                for (var z = 0; z < Resolution; z++)
                for (var x = 0; x < Resolution; x++)
                    masks[biomeIndex][z, x] = biomeIndex == PopulatedBiomeIndex;
            }

            return masks;
        }
    }
}

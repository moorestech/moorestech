using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Visual.Detail;
using UnityEngine;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     MapMakingから移植したDetail配置の判定順・座標変換・密度換算が移植元どおりかを検証する。
    ///     Verifies the ported detail placement keeps the source's decision order, coordinate mapping, and density scaling.
    /// </summary>
    public class DetailRuntimeGeneratorTest
    {
        private const int HeightmapResolution = DetailTestConfigBuilder.HeightmapResolution;
        private const int DetailResolution = DetailTestConfigBuilder.DetailResolution;

        [Test]
        public void ScalesEntryWeightByMaxDensityOnEveryUnmaskedPixel()
        {
            var maps = Generate(DetailTestConfigBuilder.CreateFullMask(), DetailTestConfigBuilder.CreateFlatSlopes(0f), DetailTestConfigBuilder.CreateEntry(0.5f, 16));

            Assert.That(maps.Count, Is.EqualTo(1));
            for (var z = 0; z < DetailResolution; z++)
            for (var x = 0; x < DetailResolution; x++)
                Assert.That(maps[0][z, x], Is.EqualTo(8), $"z={z} x={x}");
        }

        [Test]
        public void LeavesMaskedOutHeightmapCellEmptyAtItsMappedDetailPixel()
        {
            // detail x=8 は round(8/15*16)=9 で heightmap x=9 に写る。zとxを取り違えるとこの1点が動く
            // Detail x=8 maps to heightmap x=9 via round(8/15*16); swapping z and x moves this single hole
            var mask = DetailTestConfigBuilder.CreateFullMask();
            mask[0, 9] = false;

            var maps = Generate(mask, DetailTestConfigBuilder.CreateFlatSlopes(0f), DetailTestConfigBuilder.CreateEntry(1f, 16));

            Assert.That(maps[0][0, 8], Is.EqualTo(0));
            Assert.That(maps[0][0, 0], Is.EqualTo(16));
            Assert.That(maps[0][0, 1], Is.EqualTo(16));
            Assert.That(maps[0][0, 3], Is.EqualTo(16));
            Assert.That(maps[0][8, 8], Is.EqualTo(16), "z方向のマスクは落としていないので残る");
        }

        [Test]
        public void RejectsPixelsWhoseSlopeFilterFallsUnderTheRejectThreshold()
        {
            // 傾斜45度は range(0,10) の外なのでフィルタ0となり棄却される
            // A 45-degree slope sits outside range(0,10), so the filter yields 0 and the pixel is rejected
            var slopes = DetailTestConfigBuilder.CreateFlatSlopes(0f);
            slopes[0, 9] = 45f;

            var entry = DetailTestConfigBuilder.CreateEntry(1f, 16);
            entry.slopeFilter.enabled = true;
            entry.slopeFilter.range = new Vector2(0f, 10f);
            entry.slopeFilter.smoothness = Vector2.zero;

            var maps = Generate(DetailTestConfigBuilder.CreateFullMask(), slopes, entry);

            Assert.That(maps[0][0, 8], Is.EqualTo(0));
            Assert.That(maps[0][0, 0], Is.EqualTo(16));
        }

        [TestCase(-16)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(15)]
        [TestCase(17)]
        [TestCase(32)]
        public void RejectsDetailResolutionOutsideTheRuntimeContract(int detailResolution)
        {
            // 不正値は配列確保や座標除算へ進めずruntime境界で拒否する
            // Reject invalid values at the runtime boundary before allocation or coordinate division
            var dimensions = new TerrainDimensions(
                100f, 100f, 50f, 0f, 0f, HeightmapResolution, detailResolution,
                0f, 0f, 1, 0f, 0f, 0, 0, 1, 1);
            var detailConfig = new BiomeDetailConfig
            {
                entries = new[] { DetailTestConfigBuilder.CreateEntry(1f, 16) },
                filterRejectThreshold = 0.01f,
            };

            Assert.Throws<System.InvalidOperationException>(() => DetailRuntimeGenerator.GenerateForBiome(
                DetailTestConfigBuilder.CreateFullMask(), new float[HeightmapResolution, HeightmapResolution],
                DetailTestConfigBuilder.CreateFlatSlopes(0f), dimensions, detailConfig, new System.Random(1),
                null, null, null));
        }

        [Test]
        public void RejectsDensitiesOutsideWeightRange()
        {
            var entry = DetailTestConfigBuilder.CreateEntry(0.5f, 16);
            entry.weightRange = new Vector2(0.6f, 1f);

            var maps = Generate(DetailTestConfigBuilder.CreateFullMask(), DetailTestConfigBuilder.CreateFlatSlopes(0f), entry);

            for (var z = 0; z < DetailResolution; z++)
            for (var x = 0; x < DetailResolution; x++)
                Assert.That(maps[0][z, x], Is.EqualTo(0), $"z={z} x={x}");
        }

        [Test]
        public void SuppressesOccludedEntryWherePrecedingEntryAlreadyPlaced()
        {
            var precedingEntry = DetailTestConfigBuilder.CreateEntry(1f, 16);
            var occludedEntry = DetailTestConfigBuilder.CreateEntry(1f, 16);
            occludedEntry.occludedByOthers = true;

            var maps = Generate(DetailTestConfigBuilder.CreateFullMask(), DetailTestConfigBuilder.CreateFlatSlopes(0f), precedingEntry, occludedEntry);

            Assert.That(maps.Count, Is.EqualTo(2));
            Assert.That(maps[0][0, 0], Is.EqualTo(16));
            for (var z = 0; z < DetailResolution; z++)
            for (var x = 0; x < DetailResolution; x++)
                Assert.That(maps[1][z, x], Is.EqualTo(0), $"z={z} x={x}");
        }

        [Test]
        public void ThrowsWhenATextureFilterLayerIsUnresolved()
        {
            // layerが未解決のままだとEvaluateが永久に一致せず、分布が黙って違う形でしか気づけない
            // An unresolved layer would leave Evaluate never matching, surfacing only as a silently wrong distribution
            var unresolvedEntry = DetailTestConfigBuilder.CreateEntry(1f, 16);
            unresolvedEntry.textureFilter = new DetailTextureFilter
            {
                enabled = true,
                entries = new[] { new DetailTextureFilter.TextureFilterEntry { layerAddressablePath = "addr/grass", weight = 1f } },
            };

            Assert.Throws<System.InvalidOperationException>(
                () => Generate(DetailTestConfigBuilder.CreateFullMask(), DetailTestConfigBuilder.CreateFlatSlopes(0f), unresolvedEntry));
        }

        [Test]
        public void KeepsMapsInTheEntryOrder()
        {
            // mapsの並びはentriesの並びそのもの。DetailPrototypeAssetResolverが同じ並びでプロトタイプを組む前提
            // The map order is exactly the entry order, the premise DetailPrototypeAssetResolver builds its prototypes on
            var firstEntry = DetailTestConfigBuilder.CreateEntry(1f, 16);
            var secondEntry = DetailTestConfigBuilder.CreateEntry(0.5f, 16);

            var maps = Generate(DetailTestConfigBuilder.CreateFullMask(), DetailTestConfigBuilder.CreateFlatSlopes(0f), firstEntry, secondEntry);

            Assert.That(maps.Count, Is.EqualTo(2));
            Assert.That(maps[0][0, 0], Is.EqualTo(16));
            Assert.That(maps[1][0, 0], Is.EqualTo(8));
        }

        private static List<int[,]> Generate(bool[,] mask, float[,] slopes, params DetailEntry[] entries)
        {
            var detailConfig = new BiomeDetailConfig
            {
                entries = entries,
                filterRejectThreshold = 0.01f,
                borderMargin = 0f,
            };

            return DetailRuntimeGenerator.GenerateForBiome(
                mask, new float[HeightmapResolution, HeightmapResolution], slopes,
                DetailTestConfigBuilder.CreateDimensions(), detailConfig, new System.Random(1),
                null, null, null);
        }
    }
}

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
            // detail x=2 は round(2/3*4)=3 で heightmap x=3 に写る。zとxを取り違えるとこの1点が動く
            // Detail x=2 maps to heightmap x=3 via round(2/3*4); swapping z and x moves this single hole
            var mask = DetailTestConfigBuilder.CreateFullMask();
            mask[0, 3] = false;

            var maps = Generate(mask, DetailTestConfigBuilder.CreateFlatSlopes(0f), DetailTestConfigBuilder.CreateEntry(1f, 16));

            Assert.That(maps[0][0, 2], Is.EqualTo(0));
            Assert.That(maps[0][0, 0], Is.EqualTo(16));
            Assert.That(maps[0][0, 1], Is.EqualTo(16));
            Assert.That(maps[0][0, 3], Is.EqualTo(16));
            Assert.That(maps[0][2, 2], Is.EqualTo(16), "z方向のマスクは落としていないので残る");
        }

        [Test]
        public void RejectsPixelsWhoseSlopeFilterFallsUnderTheRejectThreshold()
        {
            // 傾斜45度は range(0,10) の外なのでフィルタ0となり棄却される
            // A 45-degree slope sits outside range(0,10), so the filter yields 0 and the pixel is rejected
            var slopes = DetailTestConfigBuilder.CreateFlatSlopes(0f);
            slopes[0, 3] = 45f;

            var entry = DetailTestConfigBuilder.CreateEntry(1f, 16);
            entry.slopeFilter.enabled = true;
            entry.slopeFilter.range = new Vector2(0f, 10f);
            entry.slopeFilter.smoothness = Vector2.zero;

            var maps = Generate(DetailTestConfigBuilder.CreateFullMask(), slopes, entry);

            Assert.That(maps[0][0, 2], Is.EqualTo(0));
            Assert.That(maps[0][0, 0], Is.EqualTo(16));
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
            // mapsの並びはentriesの並びそのもの。TerrainDetailPrototypeListが同じ並びでプロトタイプを組む前提
            // The map order is exactly the entry order, the premise TerrainDetailPrototypeList builds its prototypes on
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

using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Visual;
using Client.Game.InGame.Environment.Terrain.Visual.Detail;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
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
        public void ThrowsWhenAPrototypeAssetIsUnresolved()
        {
            // 黙って読み飛ばすとアドレス整備漏れが「草が1本も生えない」形でしか現れず、原因に辿り着けない
            // Silently skipping would surface a missing address only as "no grass at all", leaving no trail to the cause
            var unresolvedEntry = DetailTestConfigBuilder.CreateEntry(1f, 16);
            unresolvedEntry.prototypeConfig.SetPrototypeTexture(null);

            Assert.Throws<System.InvalidOperationException>(
                () => GenerateBoth(DetailTestConfigBuilder.CreateFullMask(), DetailTestConfigBuilder.CreateFlatSlopes(0f), unresolvedEntry));
        }

        [Test]
        public void KeepsPrototypesAndMapsIndexAligned()
        {
            var firstEntry = DetailTestConfigBuilder.CreateEntry(1f, 16);
            var secondEntry = DetailTestConfigBuilder.CreateEntry(0.5f, 16);

            var (prototypes, maps) = GenerateBoth(DetailTestConfigBuilder.CreateFullMask(), DetailTestConfigBuilder.CreateFlatSlopes(0f), firstEntry, secondEntry);

            Assert.That(prototypes.Count, Is.EqualTo(2));
            Assert.That(maps.Count, Is.EqualTo(2));
            Assert.That(maps[0][0, 0], Is.EqualTo(16));
            Assert.That(maps[1][0, 0], Is.EqualTo(8));
        }

        private static List<int[,]> Generate(bool[,] mask, float[,] slopes, params DetailEntry[] entries)
        {
            return GenerateBoth(mask, slopes, entries).maps;
        }

        private static (List<DetailPrototype> prototypes, List<int[,]> maps) GenerateBoth(
            bool[,] mask, float[,] slopes, params DetailEntry[] entries)
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
                null, null, null, null);
        }
    }
}

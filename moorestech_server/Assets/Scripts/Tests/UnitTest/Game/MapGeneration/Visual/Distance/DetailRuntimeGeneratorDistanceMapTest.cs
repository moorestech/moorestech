using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Visual.Detail;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Distance
{
    /// <summary>
    ///     DetailRuntimeGeneratorが受け取った距離マップをどの座標系で引くかを検証する。
    ///     供給側（TerrainDetailBuilder）の検証はTerrainDetailBuilderDistanceFieldTestが持つ
    ///     Verifies which coordinate space DetailRuntimeGenerator reads a supplied distance map in.
    ///     The supply side (TerrainDetailBuilder) is covered by TerrainDetailBuilderDistanceFieldTest
    /// </summary>
    public class DetailRuntimeGeneratorDistanceMapTest
    {
        private const int HeightmapResolution = DetailTestConfigBuilder.HeightmapResolution;
        private const int DetailResolution = DetailTestConfigBuilder.DetailResolution;

        [Test]
        public void AppliesTheTreeDistanceFilterOnlyOnceADistanceMapIsSupplied()
        {
            // 距離場を渡さない限り、有効な距離フィルタも黙って休む。木の根元に草が生え続ける形でしか現れない
            // An enabled distance filter idles silently until a field arrives, surfacing only as grass at every tree trunk
            var entry = CreateTreeDistanceFilteredEntry();

            var treeDistanceMap = CreateDistanceMap(50f);
            treeDistanceMap[0, 0] = 2f;

            var withDistanceMap = GenerateWithTreeDistances(entry, treeDistanceMap);
            var withoutDistanceMap = GenerateWithTreeDistances(entry, null);

            Assert.That(withDistanceMap[0][0, 0], Is.EqualTo(0), "10m未満の画素は落ちる");
            Assert.That(withDistanceMap[0][1, 1], Is.EqualTo(16), "帯の内側の画素は残る");
            Assert.That(withoutDistanceMap[0][0, 0], Is.EqualTo(16), "距離場が無ければ同じ画素が素通しになる");
        }

        [Test]
        public void ReadsTheDistanceMapAtDetailCoordinatesWithoutRemappingThem()
        {
            // 距離場だけはheightmap座標へ変換しない。変換を挟むと解像度差ぶんだけ穴の位置がずれる
            // The distance field alone is read unmapped; converting it would shift the hole by the resolution gap
            var entry = CreateTreeDistanceFilteredEntry();

            var treeDistanceMap = CreateDistanceMap(50f);
            treeDistanceMap[1, 2] = 2f;

            var maps = GenerateWithTreeDistances(entry, treeDistanceMap);

            Assert.That(maps[0][1, 2], Is.EqualTo(0));
            Assert.That(maps[0][2, 1], Is.EqualTo(16), "zとxを取り違えていない");
        }

        private static DetailEntry CreateTreeDistanceFilteredEntry()
        {
            var entry = DetailTestConfigBuilder.CreateEntry(1f, 16);
            entry.treeDistanceFilter.enabled = true;
            entry.treeDistanceFilter.range = new Vector2(10f, 200f);
            entry.treeDistanceFilter.smoothness = Vector2.zero;

            return entry;
        }

        private static float[,] CreateDistanceMap(float distance)
        {
            var map = new float[DetailResolution, DetailResolution];
            for (var z = 0; z < DetailResolution; z++)
            for (var x = 0; x < DetailResolution; x++)
                map[z, x] = distance;

            return map;
        }

        private static List<int[,]> GenerateWithTreeDistances(DetailEntry entry, float[,] treeDistanceMap)
        {
            var detailConfig = new BiomeDetailConfig
            {
                entries = new[] { entry },
                filterRejectThreshold = 0.01f,
                borderMargin = 0f,
            };

            return DetailRuntimeGenerator.GenerateForBiome(
                DetailTestConfigBuilder.CreateFullMask(), new float[HeightmapResolution, HeightmapResolution],
                DetailTestConfigBuilder.CreateFlatSlopes(0f),
                DetailTestConfigBuilder.CreateDimensions(), detailConfig, new System.Random(1),
                null, treeDistanceMap, null);
        }
    }
}

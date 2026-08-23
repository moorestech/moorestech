using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Visual.Detail;
using UnityEngine;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     Detail密度のノイズがタイル原点ではなくワールド座標で引かれることを固定する。
    ///     原点を捨てると25タイルが同じ草の粗密を反復し、タイル境界で模様が直線状に切り替わる
    ///     Pins that the detail density noise is drawn in world space rather than from the tile origin;
    ///     dropping the origin repeats one grass pattern on every tile and cuts a straight seam at the boundary
    /// </summary>
    public class DetailWorldSpaceNoiseTest
    {
        private const int HeightmapResolution = DetailTestConfigBuilder.HeightmapResolution;
        private const int DetailResolution = DetailTestConfigBuilder.DetailResolution;
        private const int NoiseOffsetCount = 8;
        private const int DetailSeed = 1;
        private const int MaxDensity = 1000;

        private const float TerrainWidth = 100f;
        private const float TerrainLength = 100f;

        // タイル1枚ぶん右のタイル。原点を無視していれば左タイルと1画素も違わない
        // The tile one step to the right; ignoring the origin makes it identical to the left tile pixel for pixel
        private const float NeighborTileOffsetX = TerrainWidth;

        [Test]
        public void 密度ノイズはワールドオフセットを足した座標で引かれる()
        {
            var offsetX = 1234.5f;
            var offsetZ = 678.25f;
            var entry = CreateNoiseEntry();
            var map = Generate(entry, offsetX, offsetZ);

            // 各画素の期待値を、同じノイズオフセット列を作り直したワールド座標の式から独立に組む
            // Rebuilds each pixel's expectation independently from the world-space formula with the same offset series
            var noiseOffsets = ManagedNoise.GenerateOffsets(new System.Random(DetailSeed), NoiseOffsetCount);
            for (var z = 0; z < DetailResolution; z++)
                for (var x = 0; x < DetailResolution; x++)
                {
                    var worldX = offsetX + (float)x / DetailResolution * TerrainWidth;
                    var worldZ = offsetZ + (float)z / DetailResolution * TerrainLength;
                    var expected = Mathf.Clamp(
                        Mathf.RoundToInt(entry.weight * entry.noiseStack.Sample(worldX, worldZ, noiseOffsets) * MaxDensity),
                        0, MaxDensity);
                    Assert.AreEqual(expected, map[z, x], $"detail画素({x},{z})がタイルローカル座標で引かれている");
                }
        }

        [Test]
        public void 隣接タイルの密度マップは同じ絵を反復しない()
        {
            var entry = CreateNoiseEntry();
            var left = Generate(entry, 0f, 0f);
            var right = Generate(entry, NeighborTileOffsetX, 0f);

            var identical = true;
            for (var z = 0; z < DetailResolution && identical; z++)
                for (var x = 0; x < DetailResolution && identical; x++)
                    if (left[z, x] != right[z, x]) identical = false;

            Assert.IsFalse(identical, "隣タイルの密度マップが1画素も違わない");
        }

        // ノイズだけが密度を決める1エントリ。フィルタは全て無効なので値の出所を1つに絞れる
        // One entry whose density comes from noise alone; every filter is off so the value has a single source
        private static DetailEntry CreateNoiseEntry()
        {
            var entry = DetailTestConfigBuilder.CreateEntry(1f, MaxDensity);
            entry.noiseStack = new DetailNoiseStack
            {
                primary = new DetailNoiseLayer
                {
                    noiseType = MapNoiseType.Simple, frequency = 0.05f, amplitude = 1f,
                },
                secondary = DetailTestConfigBuilder.CreateInactiveNoiseLayer(),
                secondaryOp = NoiseOp.Multiply,
                tertiary = DetailTestConfigBuilder.CreateInactiveNoiseLayer(),
                tertiaryOp = NoiseOp.Multiply,
            };
            return entry;
        }

        private static int[,] Generate(DetailEntry entry, float worldOffsetX, float worldOffsetZ)
        {
            var dimensions = new TerrainDimensions(
                terrainWidth: TerrainWidth, terrainLength: TerrainLength, terrainHeight: 50f,
                worldOffsetX: worldOffsetX, worldOffsetZ: worldOffsetZ,
                resolution: HeightmapResolution, detailResolution: HeightmapResolution - 1, seaLevel: 0f, shoreMinHeight: 0f, seed: 1,
                spawnWorldX: 0f, spawnWorldZ: 0f,
                tileIndexX: 0, tileIndexZ: 0, gridSizeX: 1, gridSizeZ: 1);

            var detailConfig = new BiomeDetailConfig
            {
                entries = new[] { entry },
                filterRejectThreshold = 0.01f,
                borderMargin = 0f,
            };

            return DetailRuntimeGenerator.GenerateForBiome(
                DetailTestConfigBuilder.CreateFullMask(), new float[HeightmapResolution, HeightmapResolution],
                DetailTestConfigBuilder.CreateFlatSlopes(0f), dimensions, detailConfig,
                new System.Random(DetailSeed), null, null, null)[0];
        }
    }
}

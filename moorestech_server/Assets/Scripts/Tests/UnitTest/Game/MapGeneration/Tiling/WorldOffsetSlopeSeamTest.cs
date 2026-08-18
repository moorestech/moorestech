using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Tiling;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // HeightSlopeJob/BoundaryNoiseJobがワールド座標基準でノイズをサンプルすることを検証する。
    // タイル境界(worldOffsetXがterrainWidthぶん違う2窓)の高さが一致するかを見る。
    // Verifies HeightSlopeJob/BoundaryNoiseJob sample noise on world-space coordinates.
    // Checks heights match at a tile boundary (two windows whose worldOffsetX differs by terrainWidth).
    public class WorldOffsetSlopeSeamTest
    {
        private const int Resolution = 129;
        private const int ChunkPadding = 16;
        private const float Tolerance = 1e-5f;

        [Test]
        public void 砂漠スロープのタイル境界高さは隣接窓間で一致する()
        {
            var tileA = BuildConfig(worldOffsetX: 0f, desertEnabled: true, jungleEnabled: false);
            var tileB = BuildConfig(worldOffsetX: tileA.terrainWidth, desertEnabled: true, jungleEnabled: false);

            var heightsA = RunTile(tileA);
            var heightsB = RunTile(tileB);

            Assert.AreEqual(0, CountBoundaryMismatches(heightsA, heightsB), "desert slope seam");
        }

        [Test]
        public void ジャングル境界ノイズのタイル境界高さは隣接窓間で一致する()
        {
            var tileA = BuildConfig(worldOffsetX: 0f, desertEnabled: false, jungleEnabled: true);
            var tileB = BuildConfig(worldOffsetX: tileA.terrainWidth, desertEnabled: false, jungleEnabled: true);

            var heightsA = RunTile(tileA);
            var heightsB = RunTile(tileB);

            Assert.AreEqual(0, CountBoundaryMismatches(heightsA, heightsB), "jungle boundary noise seam");
        }

        // 対照: スロープ/境界ノイズを両方無効化すると境界は元から一致する（ゲートに触れていないことの確認・空テスト防止）。
        // Control: with slope/boundary-noise both disabled the boundary already matched (confirms the gate itself is untouched; guards against a vacuous test).
        [Test]
        public void スロープと境界ノイズを両方無効化すると境界高さは元から一致する()
        {
            var tileA = BuildConfig(worldOffsetX: 0f, desertEnabled: false, jungleEnabled: false);
            var tileB = BuildConfig(worldOffsetX: tileA.terrainWidth, desertEnabled: false, jungleEnabled: false);

            var heightsA = RunTile(tileA);
            var heightsB = RunTile(tileB);

            Assert.AreEqual(0, CountBoundaryMismatches(heightsA, heightsB), "control (no slope/boundary noise)");
        }

        // CreateSmall固定のoverrideResolution(129)・低landThreshold(全面陸)を土台に、
        // worldOffsetXとスロープ/境界ノイズ系統だけを差し替える。
        // Builds on CreateSmall's fixed overrideResolution(129) and low landThreshold (all-land),
        // overriding only worldOffsetX and the slope/boundary-noise biome toggles.
        private static TerrainGenerationConfig BuildConfig(float worldOffsetX, bool desertEnabled, bool jungleEnabled)
        {
            var config = GenerationRuntimeConfigFactory.Build(TestGenerationConfigFactory.CreateSmall());
            config.seed = 42;
            config.worldOffsetX = worldOffsetX;
            config.chunkPadding = ChunkPadding;
            config.biomeBlendRadius = 8;
            config.desertEnabled = desertEnabled;
            config.jungleEnabled = jungleEnabled;

            if (desertEnabled)
            {
                // v8マスタの実運用値（A7背景）に合わせ、HeightmapStageのスロープゲートを確実に通す
                // Matches the v8 master's production values (A7 background) so the HeightmapStage slope gate is guaranteed to pass
                config.desert.canyonOctaves = 4;
                config.desert.duneAmplitude = 0.025f;
                config.desert.absSmoothing = 0.25f;
            }

            return config;
        }

        private static float[] RunTile(TerrainGenerationConfig config)
        {
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var buffers = JobDataConverter.AllocateBuffers(config.Resolution, biomeTypes.Length, 1, Allocator.TempJob);
            buffers.biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            buffers.noiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, buffers.biomeParams, biomeTypes, Allocator.TempJob);
            try
            {
                PaddedWindowStage.Run(config, biomeTypes, buffers);
                return buffers.heights.ToArray();
            }
            finally
            {
                buffers.Dispose();
            }
        }

        // tileAの右端列(タイル境界)とtileBの左端列を突き合わせる。両者は同一ワールドX上の値であるべき
        // Compares tileA's right edge column (the tile boundary) against tileB's left edge column; both sit at the same world X
        private static int CountBoundaryMismatches(float[] heightsA, float[] heightsB)
        {
            var mismatches = 0;
            for (var y = 0; y < Resolution; y++)
            {
                var a = heightsA[y * Resolution + (Resolution - 1)];
                var b = heightsB[y * Resolution + 0];
                if (Tolerance < Mathf.Abs(a - b)) mismatches++;
            }
            return mismatches;
        }
    }
}

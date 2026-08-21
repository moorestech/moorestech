using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Stages;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using TestGenerationConfigFactory = Tests.UnitTest.Game.MapGeneration.TestGenerationConfigFactory;

namespace Client.Tests.UnitTest.Terrain
{
    /// <summary>
    ///     クライアント側のタイル分類が移植元のwinnerマスクの意味を持ち、かつパディング窓で回ることを検証する。
    ///     転送バイト由来へ戻るとビーチ帯から草が消え、等倍窓へ戻るとタイル境界でDetailが途切れる
    ///     Verifies the client tile classification keeps the source's winner-mask meaning and runs on a padded window;
    ///     reverting to the transferred bytes strips the beach band, and to a plain window breaks details at tile borders
    /// </summary>
    public class TerrainClassificationContextTest
    {
        internal const int Resolution = 129;

        // 窓端の影響が届かない内側だけを見る。砂浜16px+blendRadius4+blurRadius2に余裕を足した幅（サーバー側PaddedWindowStageTestと同値）
        // Inspects only the interior beyond window-edge effects: beach 16px + blendRadius 4 + blurRadius 2 plus slack, as in the server's PaddedWindowStageTest
        internal const int Margin = 24;
        private const int BlendRadius = 4;
        private const int ChunkPadding = 8;
        private const float Tolerance = 1e-4f;

        [Test]
        public void ビーチ帯を勝者バイオームのマスクに残す()
        {
            var config = BuildConfig();
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            using var context = new TerrainClassificationContext(config, biomeTypes);
            context.Initialize();

            // 転送バイトはサーバーが確定させる値で、ビーチ帯はBeachに塗り潰されて有効バイオームのどれとも一致しなくなる
            // The transferred byte is what the server decides: the beach band becomes Beach and matches no enabled biome
            var transferred = PlacementInputBuilder.BuildBiomeIndices(
                context.Buffers.winnerBiomeIndex, context.Buffers.landMask, context.Buffers.beachFactor,
                biomeTypes, Resolution * Resolution);

            var beachPixels = 0;
            var beachPixelsWithWinner = 0;
            var oceanPixelsWithWinner = 0;
            for (var i = 0; i < transferred.Length; i++)
            {
                var hasWinner = HasWinner(context.WinnerMasks, i);
                if (transferred[i] == (byte)BiomeType.Beach)
                {
                    beachPixels++;
                    if (hasWinner) beachPixelsWithWinner++;
                }
                else if (transferred[i] == (byte)BiomeType.Ocean && hasWinner)
                {
                    oceanPixelsWithWinner++;
                }
            }

            Assert.Less(0, beachPixels, "ビーチ帯が無いと新旧セマンティクスの差を観測できない");
            Assert.AreEqual(beachPixels, beachPixelsWithWinner, "ビーチ帯は転送バイトでは全マスクfalse、勝者マスクでは全画素true");
            Assert.AreEqual(0, oceanPixelsWithWinner, "海に勝者が立つと水面下に草が生える");
        }

        [Test]
        public void パディング窓のクロップは等倍窓の内陸と一致する()
        {
            var config = BuildConfig();
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var reference = RunUnpadded(config, biomeTypes);
            using var context = new TerrainClassificationContext(config, biomeTypes);
            context.Initialize();

            var totalColumns = biomeTypes.Length + 2;
            Assert.AreEqual(0, CountWeightMismatches(reference.Weights2D, context.Weights2D, totalColumns, 0),
                "重みのクロップ位置がずれている");
            Assert.AreEqual(0, CountMaskMismatches(reference.WinnerMasks, context.WinnerMasks, 0),
                "勝者マスクのクロップ位置がずれている");

            // 1pxずらすと壊れることも要求し、値が一様で素通りする空アサートを弾く
            // Requires a 1px shift to break the match too, rejecting a vacuous assert on a uniform field
            Assert.Less(0, CountWeightMismatches(reference.Weights2D, context.Weights2D, totalColumns, 1),
                "重みが1pxズレを検出できない分布になっている");
            Assert.Less(0, CountMaskMismatches(reference.WinnerMasks, context.WinnerMasks, 1),
                "勝者マスクが1pxズレを検出できない分布になっている");
        }

        [Test]
        public void 公開する分類はタイル解像度でクロップされている()
        {
            var config = BuildConfig();
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            using var context = new TerrainClassificationContext(config, biomeTypes);
            context.Initialize();

            // 窓解像度が外へ漏れると、消費側がresと窓幅を取り違えて全画素が黙ってずれる
            // A leaked window resolution makes consumers confuse res with the window width and silently shifts every pixel
            Assert.AreEqual(Resolution * Resolution, context.Buffers.heights.Length);
            Assert.AreEqual(Resolution * Resolution, context.Weights2D.GetLength(0));
            Assert.AreEqual(biomeTypes.Length + 2, context.Weights2D.GetLength(1));
            Assert.AreEqual(biomeTypes.Length, context.WinnerMasks.Length);

            foreach (var mask in context.WinnerMasks)
            {
                Assert.AreEqual(Resolution, mask.GetLength(0));
                Assert.AreEqual(Resolution, mask.GetLength(1));
            }
        }

        // 解像度129・小さめのblendRadiusで、パディング量が海岸系の到達で決まる小さな窓にする（サーバー側と同じ組み立て）
        // Builds a 129-resolution config with a small blendRadius so the shore reach settles a small padding, as the server test does
        internal static TerrainGenerationConfig BuildConfig()
        {
            var config = GenerationRuntimeConfigFactory.Build(TestGenerationConfigFactory.CreateSmall());
            config.seed = 42;
            config.biomeBlendRadius = BlendRadius;
            config.chunkPadding = ChunkPadding;

            // 海岸線をタイルに通してビーチ帯と陸海の境界を作る。全面陸だと両テストとも空アサートになる
            // Runs a coastline through the tile to create a beach band and a land/sea border; an all-land tile makes both tests vacuous
            config.landThreshold = 0.4f;

            // 小海除去は窓全体を舐めるグローバル判定で、窓サイズを変えると内陸の結果まで動く（ADRで許容済み）
            // Small-sea removal is a global pass over the window, so changing the window size moves inland results too (accepted by ADR)
            config.shoreConfig.minSeaRegionSize = 0;
            return config;
        }

        // パディング前のクライアント経路そのもの。Task 8 以前の SplatmapRuntimeGenerator と同じ回し方で基準を採る
        // The pre-padding client path itself: the baseline is taken exactly as the pre-Task-8 SplatmapRuntimeGenerator ran it
        // NativeArrayの寿命外で比較するため、マネージド配列に落ちた2本だけを返す
        // Only the two managed arrays are returned so the comparison outlives the NativeArrays
        internal static (float[,] Weights2D, bool[][,] WinnerMasks) RunUnpadded(
            TerrainGenerationConfig config, BiomeType[] biomeTypes)
        {
            var biomeCount = biomeTypes.Length;
            var buffers = JobDataConverter.AllocateBuffers(Resolution, biomeCount, 1, Allocator.TempJob);
            var continentalnessOffsets = default(NativeArray<float2>);
            var erosionOffsets = default(NativeArray<float2>);
            try
            {
                buffers.biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
                buffers.noiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, buffers.biomeParams, biomeTypes, Allocator.TempJob);
                JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob, out continentalnessOffsets, out erosionOffsets);
                ClassificationStage.Run(config, biomeCount, buffers, continentalnessOffsets, erosionOffsets, false);

                var weights2D = PlacementInputBuilder.BuildPlacementWeights(
                    buffers.biomeWeights, buffers.shoreMask, buffers.beachFactor, Resolution, biomeCount, biomeCount + 2);
                return (weights2D, BiomeMaskBuilder.BuildAllWinnerMasks(weights2D, Resolution, biomeCount));
            }
            finally
            {
                buffers.Dispose();
                if (continentalnessOffsets.IsCreated) continentalnessOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
            }
        }

        private static bool HasWinner(bool[][,] masks, int pixelIndex)
        {
            var z = pixelIndex / Resolution;
            var x = pixelIndex % Resolution;
            foreach (var mask in masks)
                if (mask[z, x])
                    return true;
            return false;
        }

        private static int CountWeightMismatches(float[,] expected, float[,] actual, int totalColumns, int shiftX)
        {
            var mismatches = 0;
            for (var z = Margin; z < Resolution - Margin; z++)
            for (var x = Margin; x < Resolution - Margin; x++)
            for (var column = 0; column < totalColumns; column++)
            {
                var expectedValue = expected[z * Resolution + x, column];
                var actualValue = actual[z * Resolution + x + shiftX, column];
                if (Tolerance < Mathf.Abs(expectedValue - actualValue)) mismatches++;
            }

            return mismatches;
        }

        private static int CountMaskMismatches(bool[][,] expected, bool[][,] actual, int shiftX)
        {
            var mismatches = 0;
            for (var biomeIndex = 0; biomeIndex < expected.Length; biomeIndex++)
            for (var z = Margin; z < Resolution - Margin; z++)
            for (var x = Margin; x < Resolution - Margin; x++)
                if (expected[biomeIndex][z, x] != actual[biomeIndex][z, x + shiftX])
                    mismatches++;

            return mismatches;
        }
    }
}

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
    // パディング窓＋中央クロップが、等倍窓の生成結果と同じワールド座標を指すことを検証する。
    // Verifies the padded window plus center crop lands on the same world coordinates as the plain window.
    public class PaddedWindowStageTest
    {
        private const int Resolution = 129;

        // 窓端の影響が届かない内側だけを比較する。砂浜半径16px+blendRadius4+blurRadius2 に余裕を足した幅
        // Compare only the inner area beyond window-edge effects: beach radius 16px + blendRadius 4 + blurRadius 2 plus slack
        private const int Margin = 24;
        private const int BlendRadius = 4;

        // 導出padding（この設定では海岸系22 + 高さ1 = 23）を上回る chunkPadding。下回ると両実行が同じ窓になり比較が恒真化する
        // A chunkPadding above the derived padding (22 shore + 1 height = 23 here); below it both runs share one window and the comparison goes vacuous
        private const int WideChunkPadding = 40;
        private const float Tolerance = 1e-4f;

        [Test]
        public void パディング窓の高さクロップは等倍窓の内陸部と一致する()
        {
            var reference = RunUnpadded(BuildConfig(0));
            var padded = RunPadded(BuildConfig(8));

            Assert.AreEqual(Resolution * Resolution, padded.Heights.Length);
            AssertCropAligned("heights", reference.Heights, padded.Heights, 1);
        }

        [Test]
        public void パディング窓の分類クロップは高さと同じ添字で揃う()
        {
            var config = BuildConfig(0);
            var reference = RunUnpadded(config);
            var padded = RunPadded(BuildConfig(8));
            var biomeCount = ClassificationStage.GetEnabledBiomeTypes(config).Length;

            AssertCropAligned("biomeWeights", reference.BiomeWeights, padded.BiomeWeights, biomeCount);
            AssertCropAligned("shoreMask", reference.ShoreMask, padded.ShoreMask, 1);
            AssertCropAligned("landMask", reference.LandMask, padded.LandMask, 1);
            AssertCropAligned("beachFactor", reference.BeachFactor, padded.BeachFactor, 1);
            AssertCropAligned("landTextureFactor", reference.LandTextureFactor, padded.LandTextureFactor, 1);
            AssertCropAligned("seaTextureFactor", reference.SeaTextureFactor, padded.SeaTextureFactor, 1);
            Assert.AreEqual(0, CountWinnerMismatches(reference.Winner, padded.Winner), "winnerBiomeIndex");
        }

        [Test]
        public void chunkPaddingを変えても内陸の高さと分類は一致する()
        {
            var wide = RunPadded(BuildConfig(WideChunkPadding));
            var narrow = RunPadded(BuildConfig(0));

            // 同じ padding に落ちると2実行がビット同一になり以降が恒真になるので、窓が実際に違うことを先に固定する
            // Landing on the same padding would make both runs bit-identical and everything below vacuous, so pin that the windows really differ
            Assert.AreEqual(WideChunkPadding, wide.Padding, "chunkPadding が導出paddingに飲まれている");
            Assert.Less(narrow.Padding, wide.Padding, "両実行の窓サイズが同じで比較が恒真になっている");

            Assert.AreEqual(0, CountMismatches(wide.Heights, narrow.Heights, 1, 0), "heights");
            Assert.AreEqual(0, CountWinnerMismatches(wide.Winner, narrow.Winner), "winnerBiomeIndex");
        }

        // 解像度129・小さめの blendRadius で、chunkPadding が導出paddingを上回れば窓幅を動かせる条件を作る。
        // Builds a 129-resolution config with a small blendRadius so a chunkPadding above the derived padding can move the window width.
        private static TerrainGenerationConfig BuildConfig(int chunkPadding)
        {
            var config = GenerationRuntimeConfigFactory.Build(TestGenerationConfigFactory.CreateSmall());
            config.seed = 42;
            config.biomeBlendRadius = BlendRadius;
            config.chunkPadding = chunkPadding;

            // 海岸線をタイルに通し、陸海・砂浜・砂テクスチャの各チャネルを一様値でなくする（一様だとクロップ検証が空になる）
            // Runs a coastline through the tile so the land/sea, beach, and sand-texture channels are not uniform (uniform ones make the crop check vacuous)
            config.landThreshold = 0.4f;

            // 小海除去は窓全体を舐めるグローバル判定で、窓サイズを変えると内陸の結果まで動く（ADR で許容済み）
            // Small-sea removal is a global pass over the window, so changing the window size moves inland results too (accepted by ADR)
            config.shoreConfig.minSeaRegionSize = 0;
            return config;
        }

        private static WindowResult RunPadded(TerrainGenerationConfig config)
        {
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var buffers = AllocateBuffers(config, biomeTypes);
            try
            {
                PaddedWindowStage.Run(config, biomeTypes, buffers);
                return WindowResult.From(buffers, PaddedWindowStage.ResolvePadding(config, buffers.biomeParams));
            }
            finally
            {
                buffers.Dispose();
            }
        }

        // クロップ前の等倍窓生成。Task 2 以前の経路そのもので、比較の基準になる。
        // The plain unpadded window generation: the pre-Task-2 path itself, used as the comparison baseline.
        private static WindowResult RunUnpadded(TerrainGenerationConfig config)
        {
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var buffers = AllocateBuffers(config, biomeTypes);
            JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob, out var cont, out var ero);
            try
            {
                ClassificationStage.Run(config, biomeTypes.Length, buffers, cont, ero, protectEdgeSea: false);
                HeightmapStage.Run(config, biomeTypes.Length, buffers);
                return WindowResult.From(buffers, 0);
            }
            finally
            {
                buffers.Dispose();
                cont.Dispose();
                ero.Dispose();
            }
        }

        // noiseOffsets は biomeParams の slice 情報を埋めるため、生成順は本番経路と同じにする。
        // GenerateNoiseOffsets fills biomeParams slice info, so keep the production call order.
        private static JobBuffers AllocateBuffers(TerrainGenerationConfig config, BiomeType[] biomeTypes)
        {
            var buffers = JobDataConverter.AllocateBuffers(config.Resolution, biomeTypes.Length, 1, Allocator.TempJob);
            buffers.biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            buffers.noiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, buffers.biomeParams, biomeTypes, Allocator.TempJob);
            return buffers;
        }

        // 一致に加えて「1pxずらすと壊れる」ことも要求し、値が一様で素通りする空アサートを弾く。
        // Requires a 1px shift to break the match as well, rejecting a vacuous assert on a uniform channel.
        private static void AssertCropAligned(string channel, float[] expected, float[] actual, int channels)
        {
            Assert.AreEqual(0, CountMismatches(expected, actual, channels, 0), $"{channel}: クロップ位置がずれている");
            Assert.Less(0, CountMismatches(expected, actual, channels, 1), $"{channel}: 1pxズレを検出できない値分布");
        }

        // 内陸だけを走査し、actual 側を shiftX ピクセルずらして食い違い数を返す。
        // Scans the inland area only and counts mismatches with actual shifted by shiftX pixels.
        private static int CountMismatches(float[] expected, float[] actual, int channels, int shiftX)
        {
            var mismatches = 0;
            for (var y = Margin; y < Resolution - Margin; y++)
            for (var x = Margin; x < Resolution - Margin; x++)
            for (var c = 0; c < channels; c++)
            {
                var expectedValue = expected[(y * Resolution + x) * channels + c];
                var actualValue = actual[(y * Resolution + x + shiftX) * channels + c];
                if (Tolerance < Mathf.Abs(expectedValue - actualValue)) mismatches++;
            }
            return mismatches;
        }

        private static int CountWinnerMismatches(int[] expected, int[] actual)
        {
            var mismatches = 0;
            for (var y = Margin; y < Resolution - Margin; y++)
            for (var x = Margin; x < Resolution - Margin; x++)
                if (expected[y * Resolution + x] != actual[y * Resolution + x]) mismatches++;
            return mismatches;
        }

        // NativeArray の寿命外で比較したいので、必要チャネルだけマネージド配列へ写す。
        // Copies only the needed channels to managed arrays so comparisons outlive the NativeArrays.
        private sealed class WindowResult
        {
            public float[] Heights;
            public float[] BiomeWeights;
            public float[] ShoreMask;
            public float[] LandMask;
            public float[] BeachFactor;
            public float[] LandTextureFactor;
            public float[] SeaTextureFactor;
            public int[] Winner;
            public int Padding;

            public static WindowResult From(JobBuffers buffers, int padding)
            {
                return new WindowResult
                {
                    Padding = padding,
                    Heights = buffers.heights.ToArray(),
                    BiomeWeights = buffers.biomeWeights.ToArray(),
                    ShoreMask = buffers.shoreMask.ToArray(),
                    LandMask = buffers.landMask.ToArray(),
                    BeachFactor = buffers.beachFactor.ToArray(),
                    LandTextureFactor = buffers.landTextureFactor.ToArray(),
                    SeaTextureFactor = buffers.seaTextureFactor.ToArray(),
                    Winner = buffers.winnerBiomeIndex.ToArray(),
                };
            }
        }
    }
}

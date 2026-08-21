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
    // 台地チャネル(plateauMask/regionLabels)がパディング窓から他チャネルと同じ添字で切り出されることを検証する。
    // 候補マスクは等倍窓との一致で原点そのものを固定し、ラベルはその候補マスクとの相互整合で原点を固定する。
    // Verifies the plateau channels (plateauMask/regionLabels) are cropped from the padded window on the same index
    // as the others: the mask's origin is pinned against the plain window and the labels' origin against that mask.
    public class PlateauChannelCropTest
    {
        private const int Resolution = 129;
        private const int Padding = 8;
        private const int BlendRadius = 4;

        // 検出カーネルは plateauSearchBaseRadius<<3 まで届く。既定の8だと届く距離が解像度の半分になり内陸が残らない
        // The detection kernel reaches plateauSearchBaseRadius<<3; the default 8 spans half the resolution and leaves no inland area
        private const int SearchBaseRadius = 2;
        private const int Margin = 32;
        private const float Tolerance = 1e-4f;

        [Test]
        public void 台地候補マスクのクロップ原点は等倍窓の内陸部と一致する()
        {
            // 2本を同じ量ずらしても相互整合は破れない。原点そのものはパディングを通さない等倍窓でしか押さえられない
            // Shifting both channels alike keeps them mutually consistent; only the unpadded window pins the origin itself
            var reference = RunUnpadded();
            var padded = RunPadded();

            Assert.AreEqual(0, CountMaskMismatches(reference, padded, 0), "plateauMask: クロップ位置がずれている");
            Assert.Less(0, CountMaskMismatches(reference, padded, 1), "plateauMask: 1pxズレを検出できない値分布");
        }

        [Test]
        public void 台地チャネルはクロップされラベルと候補マスクが同じ画素で揃う()
        {
            var window = RunPadded();

            // 空のままなら Crop 漏れ。窓で検出した台地が destination へ1画素も届いていない
            // An empty channel means a missing Crop: not one plateau pixel from the window reached destination
            Assert.Less(0, CountMasked(window.PlateauMask), "plateauMaskが空: パディング窓からクロップされていない");
            Assert.Less(0, CountLabeled(window.RegionLabels), "regionLabelsが空: パディング窓からクロップされていない");

            // 受理領域のラベルは候補マスクの部分集合。添字が1つでもずれれば領域の縁で必ず破れる
            // An accepted region's labels are a subset of the candidate mask; any index shift breaks it along the region's rim
            Assert.AreEqual(0, CountLabelOutsideMask(window, 0), "regionLabelsとplateauMaskの添字がずれている");
            Assert.Less(0, CountLabelOutsideMask(window, 1), "1pxズレを検出できない値分布");
        }

        private static PlateauChannels RunPadded()
        {
            var config = BuildConfig();
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var buffers = AllocateBuffers(config, biomeTypes);
            try
            {
                PaddedWindowStage.Run(config, biomeTypes, buffers);
                return PlateauChannels.From(buffers);
            }
            finally
            {
                buffers.Dispose();
            }
        }

        // クロップを1度も通らない等倍窓。ここが原点の基準になる（Task 2 以前の経路そのもの）。
        // The plain window that never passes through a crop; it is the origin baseline (the pre-Task-2 path itself).
        private static PlateauChannels RunUnpadded()
        {
            // PaddedWindowStage を通さないので chunkPadding は効かない。他の設定は padded 側と1つも変えない
            // chunkPadding has no effect without PaddedWindowStage, and not one other setting differs from the padded run
            var config = BuildConfig();
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var buffers = AllocateBuffers(config, biomeTypes);
            JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob, out var cont, out var ero);
            try
            {
                ClassificationStage.Run(config, biomeTypes.Length, buffers, cont, ero, protectEdgeSea: false);
                HeightmapStage.Run(config, biomeTypes.Length, buffers);
                return PlateauChannels.From(buffers);
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

        // Alpine だけを有効にし、台地の検出条件を緩めて受理領域が必ず立つ地形にする。
        // Enables Alpine alone and loosens the plateau thresholds so accepted regions always appear.
        private static TerrainGenerationConfig BuildConfig()
        {
            var config = GenerationRuntimeConfigFactory.Build(TestGenerationConfigFactory.CreateSmall());
            config.seed = 42;
            config.biomeBlendRadius = BlendRadius;
            config.chunkPadding = Padding;
            config.landThreshold = 0f;
            config.shoreConfig.minSeaRegionSize = 0;

            config.grasslandEnabled = false;
            config.forestEnabled = false;
            config.alpineEnabled = true;
            config.alpine.enablePlateau = true;
            config.alpine.prominenceThreshold = 0.01f;
            config.alpine.minProminentDirections = 4;
            config.alpine.minRegionSize = 20;
            config.alpine.minPlateauCoverage = 0f;
            config.alpine.plateauSearchBaseRadius = SearchBaseRadius;
            return config;
        }

        private static int CountMasked(float[] plateauMask)
        {
            var masked = 0;
            for (var i = 0; i < plateauMask.Length; i++)
                if (0f < plateauMask[i]) masked++;

            return masked;
        }

        private static int CountLabeled(int[] regionLabels)
        {
            var labeled = 0;
            for (var i = 0; i < regionLabels.Length; i++)
                if (0 < regionLabels[i]) labeled++;

            return labeled;
        }

        // ラベル側を shiftX 画素ずらして、候補マスクの外に出たラベル画素を数える。
        // Counts labeled pixels landing outside the candidate mask with the labels shifted by shiftX pixels.
        private static int CountLabelOutsideMask(PlateauChannels window, int shiftX)
        {
            var violations = 0;
            for (var y = 0; y < Resolution; y++)
            for (var x = 0; x < Resolution - shiftX; x++)
            {
                if (window.RegionLabels[y * Resolution + x + shiftX] <= 0) continue;
                if (window.PlateauMask[y * Resolution + x] <= 0f) violations++;
            }

            return violations;
        }

        // 窓端でカーネルが打ち切られない内陸だけを走査し、padded 側を shiftX 画素ずらして食い違い数を返す。
        // Scans only the inland area untouched by window-edge truncation, counting mismatches with padded shifted by shiftX pixels.
        private static int CountMaskMismatches(PlateauChannels reference, PlateauChannels padded, int shiftX)
        {
            var mismatches = 0;
            for (var y = Margin; y < Resolution - Margin; y++)
            for (var x = Margin; x < Resolution - Margin; x++)
            {
                var referenceValue = reference.PlateauMask[y * Resolution + x];
                var paddedValue = padded.PlateauMask[y * Resolution + x + shiftX];
                if (Tolerance < Mathf.Abs(referenceValue - paddedValue)) mismatches++;
            }

            return mismatches;
        }

        // NativeArray の寿命外で比較するため、必要な2本だけマネージド配列へ写す。
        // Copies only the two needed channels into managed arrays so the comparison outlives the NativeArrays.
        private sealed class PlateauChannels
        {
            public float[] PlateauMask;
            public int[] RegionLabels;

            public static PlateauChannels From(JobBuffers buffers)
            {
                return new PlateauChannels
                {
                    PlateauMask = buffers.plateauMask.ToArray(),
                    RegionLabels = buffers.regionLabels.ToArray(),
                };
            }
        }
    }
}

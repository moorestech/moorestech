using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Tiling;
using NUnit.Framework;
using Unity.Collections;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // 台地チャネル(plateauMask/regionLabels)がパディング窓から他チャネルと同じ添字で切り出されることを検証する。
    // クロップ位置そのものは PaddedWindowStageTest が heights で固定済みなので、ここは2本の相互整合だけを見る。
    // Verifies the plateau channels (plateauMask/regionLabels) are cropped from the padded window on the same index
    // as the others. PaddedWindowStageTest already pins the crop position through heights, so this checks their mutual consistency.
    public class PlateauChannelCropTest
    {
        private const int Resolution = 129;
        private const int Padding = 8;
        private const int BlendRadius = 4;

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
            var buffers = JobDataConverter.AllocateBuffers(config.Resolution, biomeTypes.Length, 1, Allocator.TempJob);
            buffers.biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            buffers.noiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, buffers.biomeParams, biomeTypes, Allocator.TempJob);
            try
            {
                PaddedWindowStage.Run(config, biomeTypes, buffers);
                return new PlateauChannels
                {
                    PlateauMask = buffers.plateauMask.ToArray(),
                    RegionLabels = buffers.regionLabels.ToArray(),
                };
            }
            finally
            {
                buffers.Dispose();
            }
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

        // NativeArray の寿命外で比較するため、必要な2本だけマネージド配列へ写す。
        // Copies only the two needed channels into managed arrays so the comparison outlives the NativeArrays.
        private sealed class PlateauChannels
        {
            public float[] PlateauMask;
            public int[] RegionLabels;
        }
    }
}

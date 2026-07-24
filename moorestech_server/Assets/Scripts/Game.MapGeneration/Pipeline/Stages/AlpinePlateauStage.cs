using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // ステージ2b-2e: Alpine 台地化（検出→領域分析→フラット化→事後検証→スパイク/境界平滑）。
    // Stage 2b-2e: Alpine plateau processing (detect, analyze, flatten, post-validate, smooth).
    internal static class AlpinePlateauStage
    {
        public static void Apply(TerrainGenerationConfig config, int res, int pixelCount, JobBuffers buffers)
        {
            if (!config.alpineEnabled || !config.alpine.enablePlateau) return;

            // 2b: prominence 検出
            // 2b: prominence detection
            new AlpinePlateauDetectionJob
            {
                resolution = res,
                prominenceThreshold = config.alpine.prominenceThreshold,
                minProminentDirections = config.alpine.minProminentDirections,
                heights = buffers.heights,
                winnerBiomeIndex = buffers.winnerBiomeIndex,
                biomeParams = buffers.biomeParams,
                plateauMask = buffers.plateauMask
            }.Schedule(pixelCount, 64).Complete();

            // 2c: 連結領域分析
            // 2c: connected-region analysis
            new PlateauRegionAnalysisJob
            {
                resolution = res,
                minRegionSize = config.alpine.minRegionSize,
                minCoverageRatio = config.alpine.minPlateauCoverage,
                coverageTolerance = config.alpine.coverageTolerance,
                plateauMask = buffers.plateauMask,
                heights = buffers.heights,
                regionLabels = buffers.regionLabels,
                regionInfos = buffers.regionInfos,
                regionCount = buffers.regionCount
            }.Schedule().Complete();

            int nRegions = buffers.regionCount[0];
            if (nRegions <= 0) return;

            FlattenAndRefine(config, res, pixelCount, buffers);
        }

        // 2d フラット化 + 2e 事後検証 + スパイク除去 + 境界帯リファインを順に適用する。
        // Apply 2d flatten, 2e post-validation, spike removal, and boundary refine in order.
        static void FlattenAndRefine(TerrainGenerationConfig config, int res, int pixelCount, JobBuffers buffers)
        {
            var heightsBackup = new NativeArray<float>(pixelCount, Allocator.TempJob);
            NativeArray<float>.Copy(buffers.heights, heightsBackup);

            new PlateauFlattenJob
            {
                resolution = res,
                baseTransition = config.alpine.plateauBaseTransition,
                transitionScale = config.alpine.plateauTransitionScale,
                boundaryBlend = config.alpine.plateauBoundaryBlend,
                regionLabels = buffers.regionLabels,
                regionInfos = buffers.regionInfos,
                plateauMask = buffers.plateauMask,
                heights = buffers.heights
            }.Schedule(pixelCount, 64).Complete();

            new PlateauPostValidationJob
            {
                resolution = res,
                minCoverageRatio = config.alpine.minPlateauCoverage,
                coverageTolerance = config.alpine.coverageTolerance,
                regionInfos = buffers.regionInfos,
                regionCount = buffers.regionCount,
                heightsBackup = heightsBackup,
                regionLabels = buffers.regionLabels,
                heights = buffers.heights
            }.Schedule().Complete();

            heightsBackup.Dispose();

            // 2d-post-2: 台地内部のスパイク除去（同一領域ボックスブラー）
            // 2d-post-2: remove intra-plateau spikes via same-region box blur
            if (0 < config.alpine.smoothRadius && 0 < config.alpine.smoothIterations)
            {
                var tempHeights = new NativeArray<float>(pixelCount, Allocator.TempJob);
                for (int iter = 0; iter < config.alpine.smoothIterations; iter++)
                {
                    NativeArray<float>.Copy(buffers.heights, tempHeights);
                    new PlateauBoundarySmoothJob
                    {
                        resolution = res,
                        kernelRadius = Mathf.Min(config.alpine.smoothRadius, 4),
                        spikeThreshold = 0.004f,
                        regionLabels = buffers.regionLabels,
                        inputHeights = tempHeights,
                        outputHeights = buffers.heights
                    }.Schedule(pixelCount, 64).Complete();
                }
                tempHeights.Dispose();
            }

            // 2d-post-3: 境界帯ガウシアン＋パーリンノイズ（反復で段差を除去。ノイズは最終回のみ）
            // 2d-post-3: boundary-band gaussian plus perlin noise (iterated; noise only on the last pass)
            var rng = new System.Random(config.seed + 99999);
            var noiseOff = new float2((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);
            int refineIter = Mathf.Max(config.alpine.boundaryRefineIterations, 1);
            var refineInput = new NativeArray<float>(pixelCount, Allocator.TempJob);
            for (int iter = 0; iter < refineIter; iter++)
            {
                NativeArray<float>.Copy(buffers.heights, refineInput);
                bool isLastIter = iter == refineIter - 1;
                new PlateauBoundaryRefineJob
                {
                    resolution = res,
                    innerBand = config.alpine.boundaryInnerBand,
                    outerBand = config.alpine.boundaryOuterBand,
                    gaussSigma = config.alpine.boundaryGaussSigma,
                    noiseFrequency = config.alpine.boundaryNoiseFrequency,
                    noiseAmplitude = isLastIter ? config.alpine.boundaryNoiseAmplitude : 0f,
                    noiseOctaves = config.alpine.boundaryNoiseOctaves,
                    noiseOffset = noiseOff,
                    regionLabels = buffers.regionLabels,
                    regionInfos = buffers.regionInfos,
                    inputHeights = refineInput,
                    outputHeights = buffers.heights
                }.Schedule(pixelCount, 64).Complete();
            }
            refineInput.Dispose();
        }
    }
}

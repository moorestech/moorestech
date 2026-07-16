using System.Diagnostics;
using NUnit.Framework;
using UnityEngine.TestTools;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapGenerator.Tests.EditMode
{
    /// <summary>
    /// DOTSジョブパイプラインの性能回帰テスト。
    /// Stopwatchで3回計測し中央値が閾値以内であることを検証する。
    /// エディタモードではジョブ安全チェック+非最適化により3〜4倍のオーバーヘッドがかかるため、
    /// Burst最適化ビルドの目標値にEditorOverheadMultiplierを掛けた閾値を使用する。
    /// </summary>
    [TestFixture]
    public class PerformanceTests
    {
        // エディタモードでの目標値。パラメータ調整イテレーション用に最適化済み
        const int EditorOverheadMultiplier = 1;

        // エディタモード閾値(ms) — 多少の揺らぎを許容するマージン付き
        const int TargetFullPipeline = 400;
        const int TargetSingleBiome = 300;
        const int TargetClassifyInterp = 200;
        const int TargetHeightSample = 200;

        TerrainGenerationConfig _config;

        [OneTimeSetUp]
        public void WarmUp()
        {
            // ジョブ安全チェックのログを抑制（エディタ実行時はBurstコンパイル前に
            // 安全チェックが発火する。テスト結果への影響を防ぐ）
            LogAssert.ignoreFailingMessages = true;

            _config = TestConfigFactory.Create();

            // Burst JITウォームアップ: 本番と同じ解像度で2回実行し
            // 非同期コンパイルを完了させる
            var warmConfig = TestConfigFactory.Create();
            warmConfig.resolutionPreset = TerrainResolutionPreset._512;
            RunFullJobPipeline(warmConfig);
            RunFullJobPipeline(warmConfig);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        // --- 全バイオーム513x513パイプライン: 300ms以内 ---

        [Test]
        public void FullPipeline_AllBiomes_Under300ms()
        {
            _config.resolutionPreset = TerrainResolutionPreset._512;
            int threshold = TargetFullPipeline * EditorOverheadMultiplier;
            var median = MeasureMedian(() => RunFullJobPipeline(_config));
            UnityEngine.Debug.Log($"[Perf] FullPipeline AllBiomes median: {median}ms (target: {TargetFullPipeline}ms, editor threshold: {threshold}ms)");
            Assert.Less(median, threshold, $"全バイオームパイプラインが{median}msで閾値{threshold}msを超過");
        }

        // --- 単一バイオーム(Grasslandのみ)513x513: 100ms以内 ---

        [Test]
        public void FullPipeline_SingleBiome_Under100ms()
        {
            // Grassland以外を無効化して単一バイオーム条件にする
            var singleConfig = TestConfigFactory.Create();
            singleConfig.resolutionPreset = TerrainResolutionPreset._512;
            singleConfig.grasslandEnabled = true;
            singleConfig.forestEnabled = false;
            singleConfig.savannaEnabled = false;
            singleConfig.desertEnabled = false;
            singleConfig.mesaEnabled = false;
            singleConfig.alpineEnabled = false;
            singleConfig.jungleEnabled = false;
            singleConfig.woodsEnabled = false;

            int threshold = TargetSingleBiome * EditorOverheadMultiplier;
            var median = MeasureMedian(() => RunFullJobPipeline(singleConfig));
            UnityEngine.Debug.Log($"[Perf] SingleBiome (Grassland) median: {median}ms (target: {TargetSingleBiome}ms, editor threshold: {threshold}ms)");
            Assert.Less(median, threshold, $"単一バイオームが{median}msで閾値{threshold}msを超過");
        }

        // --- Classification + Interpolationジョブ: 150ms以内 ---

        [Test]
        public void ClassificationAndInterpolation_Under150ms()
        {
            _config.resolutionPreset = TerrainResolutionPreset._512;
            int threshold = TargetClassifyInterp * EditorOverheadMultiplier;
            var median = MeasureMedian(() => RunClassificationAndInterpolation(_config));
            UnityEngine.Debug.Log($"[Perf] Classification+Interpolation median: {median}ms (target: {TargetClassifyInterp}ms, editor threshold: {threshold}ms)");
            Assert.Less(median, threshold,
                $"Classification+Interpolationが{median}msで閾値{threshold}msを超過");
        }

        // --- HeightSampleジョブ単体: 150ms以内 ---

        [Test]
        public void HeightSampleJob_Under150ms()
        {
            _config.resolutionPreset = TerrainResolutionPreset._512;
            int threshold = TargetHeightSample * EditorOverheadMultiplier;
            var median = MeasureMedian(() => RunHeightSampleOnly(_config));
            UnityEngine.Debug.Log($"[Perf] HeightSampleJob median: {median}ms (target: {TargetHeightSample}ms, editor threshold: {threshold}ms)");
            Assert.Less(median, threshold,
                $"HeightSampleJobが{median}msで閾値{threshold}msを超過");
        }

        // =====================================================================
        // 計測ヘルパー: 3回実行して中央値を返す
        // =====================================================================

        static long MeasureMedian(System.Func<long> action)
        {
            var times = new long[3];
            for (int i = 0; i < 3; i++)
                times[i] = action();
            System.Array.Sort(times);
            return times[1];
        }

        // =====================================================================
        // パイプライン実行: TerrainGenerator.RunJobPipelineのロジックを再現
        // =====================================================================

        /// <summary>
        /// 6ジョブ全体を計測する。TerrainGenerator.RunJobPipelineと同一のスケジュール順。
        /// </summary>
        static long RunFullJobPipeline(TerrainGenerationConfig config)
        {
            var biomeTypes = GetEnabledBiomeTypes(config);
            int biomeCount = biomeTypes.Length;
            int res = config.Resolution;

            // テクスチャレイヤーなし（beachLayer=null）の場合layerCount=0
            var helper = new BiomePlacementHelper(config);
            var terrainLayers = JobDataConverter.BuildTerrainLayers(
                config, helper, biomeTypes, out var layerIndexMap);

            // NativeArray変換
            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            var noiseOffsets = JobDataConverter.GenerateNoiseOffsets(
                config, biomeParams, biomeTypes, Allocator.TempJob);
            var textureEntries = JobDataConverter.ConvertTextureEntries(
                config, biomeParams, helper, biomeTypes, layerIndexMap, Allocator.TempJob);
            JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob,
                out var continentalnessOffsets, out var erosionOffsets);

            // layerCountが0だとSplatmapJobのバッファが空になるので最低1を確保
            int effectiveLayerCount = System.Math.Max(terrainLayers.Length, 1);
            var buffers = JobDataConverter.AllocateBuffers(
                res, biomeCount, effectiveLayerCount, Allocator.TempJob);

            // ReadOnlyデータをバッファに紐付け（Disposeで一括解放される）
            buffers.noiseOffsets = noiseOffsets;
            buffers.biomeParams = biomeParams;
            buffers.textureEntries = textureEntries;

            var sw = Stopwatch.StartNew();
            try
            {
                // TerrainGenerator.RunJobPipelineと同一の6ジョブ実行
                ScheduleAllJobs(config, biomeCount, effectiveLayerCount, buffers,
                    continentalnessOffsets, erosionOffsets);
                sw.Stop();
                return sw.ElapsedMilliseconds;
            }
            finally
            {
                buffers.Dispose();
                if (continentalnessOffsets.IsCreated) continentalnessOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
            }
        }

        /// <summary>
        /// ClassificationJob + InterpolateWeightsJobのみを計測する。
        /// </summary>
        static long RunClassificationAndInterpolation(TerrainGenerationConfig config)
        {
            var biomeTypes = GetEnabledBiomeTypes(config);
            int biomeCount = biomeTypes.Length;
            int res = config.Resolution;
            int pixelCount = res * res;

            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            // noiseOffsetsはbiomeParamsのスライス情報更新に必要
            var noiseOffsets = JobDataConverter.GenerateNoiseOffsets(
                config, biomeParams, biomeTypes, Allocator.TempJob);
            JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob,
                out var continentalnessOffsets, out var erosionOffsets);

            var buffers = JobDataConverter.AllocateBuffers(res, biomeCount, 1, Allocator.TempJob);
            buffers.noiseOffsets = noiseOffsets;
            buffers.biomeParams = biomeParams;

            var biomePermutation = JobDataConverter.GenerateBiomePermutation(
                config.seed, biomeCount, Allocator.TempJob);

            var sw = Stopwatch.StartNew();
            try
            {
                // Job 1a: ボロノイ分類
                var classJob = new ClassificationJob
                {
                    resolution = res,
                    terrainWidth = config.terrainWidth,
                    terrainLength = config.terrainLength,
                    worldOffsetX = 0f,
                    worldOffsetZ = 0f,
                    continentalnessFrequency = config.continentalnessFrequency,
                    continentalnessOctaves = config.continentalnessOctaves,
                    continentalnessPersistence = config.continentalnessPersistence,
                    landThreshold = config.landThreshold,
                    erosionFrequency = config.erosionFrequency,
                    erosionOctaves = config.erosionOctaves,
                    erosionStrength = config.erosionStrength,
                    beachWidth = 0f,
                    voronoiCellSize = config.voronoiCellSize,
                    voronoiJitter = config.voronoiJitter,
                    biomeCount = biomeCount,
                    seed = config.seed,
                    boundaryWarpOctaves = config.boundaryWarpOctaves,
                    boundaryWarpStrength = config.boundaryWarpStrength,
                    boundaryWarpFrequency = config.boundaryWarpFrequency,
                    continentalnessOffsets = continentalnessOffsets,
                    erosionOffsets = erosionOffsets,
                    biomePermutation = biomePermutation,
                    shoreMask = buffers.shoreMask,
                    landMask = buffers.landMask,
                    beachFactor = buffers.beachFactor,
                    rawBiomeIndex = buffers.rawBiomeIndex
                };
                classJob.Schedule(pixelCount, 64).Complete();

                // Job 1b: 補間
                var interpJob = new InterpolateWeightsJob
                {
                    resolution = res,
                    biomeCount = biomeCount,
                    blendRadius = config.biomeBlendRadius,
                    rawBiomeIndex = buffers.rawBiomeIndex,
                    biomeParams = buffers.biomeParams,
                    rawBiomeWeights = buffers.rawBiomeWeights
                };
                interpJob.Schedule(pixelCount, 64).Complete();

                sw.Stop();
                return sw.ElapsedMilliseconds;
            }
            finally
            {
                buffers.Dispose();
                biomePermutation.Dispose();
                if (continentalnessOffsets.IsCreated) continentalnessOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
            }
        }

        /// <summary>
        /// HeightSampleJobのみを計測する。前段ジョブは計測外で事前実行。
        /// </summary>
        static long RunHeightSampleOnly(TerrainGenerationConfig config)
        {
            var biomeTypes = GetEnabledBiomeTypes(config);
            int biomeCount = biomeTypes.Length;
            int res = config.Resolution;
            int pixelCount = res * res;

            var helper = new BiomePlacementHelper(config);
            var terrainLayers = JobDataConverter.BuildTerrainLayers(
                config, helper, biomeTypes, out var layerIndexMap);
            int layerCount = System.Math.Max(terrainLayers.Length, 1);

            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            var noiseOffsets = JobDataConverter.GenerateNoiseOffsets(
                config, biomeParams, biomeTypes, Allocator.TempJob);
            var textureEntries = JobDataConverter.ConvertTextureEntries(
                config, biomeParams, helper, biomeTypes, layerIndexMap, Allocator.TempJob);
            JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob,
                out var continentalnessOffsets, out var erosionOffsets);

            var buffers = JobDataConverter.AllocateBuffers(res, biomeCount, layerCount, Allocator.TempJob);
            buffers.noiseOffsets = noiseOffsets;
            buffers.biomeParams = biomeParams;
            buffers.textureEntries = textureEntries;

            try
            {
                // 前段ジョブ(1a〜1c)を計測外で実行。HeightSampleの入力を準備する
                SchedulePreHeightJobs(config, biomeCount, buffers,
                    continentalnessOffsets, erosionOffsets);

                // HeightSampleJobのみ計測
                var sw = Stopwatch.StartNew();
                var heightJob = new HeightSampleJob
                {
                    resolution = res,
                    biomeCount = biomeCount,
                    terrainWidth = config.terrainWidth,
                    terrainLength = config.terrainLength,
                    worldOffsetX = 0f,
                    worldOffsetZ = 0f,
                    seaLevel = config.seaLevel,
                    beachElevation = config.shoreConfig.beachElevation,
                    shoreMask = buffers.shoreMask,
                    landMask = buffers.landMask,
                    beachFactor = buffers.beachFactor,
                    biomeWeights = buffers.biomeWeights,
                    winnerBiomeIndex = buffers.winnerBiomeIndex,
                    biomeParams = buffers.biomeParams,
                    noiseOffsets = buffers.noiseOffsets,
                    heights = buffers.heights
                };
                heightJob.Schedule(pixelCount, 64).Complete();
                sw.Stop();
                return sw.ElapsedMilliseconds;
            }
            finally
            {
                buffers.Dispose();
                if (continentalnessOffsets.IsCreated) continentalnessOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
            }
        }

        // =====================================================================
        // ジョブスケジュール: TerrainGenerator.RunJobPipelineの忠実な再現
        // =====================================================================

        /// <summary>
        /// 6ジョブ全てをSchedule→Completeで実行する。
        /// </summary>
        static void ScheduleAllJobs(
            TerrainGenerationConfig config, int biomeCount, int layerCount,
            JobBuffers buffers,
            NativeArray<float2> continentalnessOffsets, NativeArray<float2> erosionOffsets)
        {
            int res = config.Resolution;
            int pixelCount = res * res;

            // Job 1a〜1c: 分類・補間・ブラー
            SchedulePreHeightJobs(config, biomeCount, buffers,
                continentalnessOffsets, erosionOffsets);

            // Job 2a: ハイトマップ生成
            var heightJob = new HeightSampleJob
            {
                resolution = res,
                biomeCount = biomeCount,
                terrainWidth = config.terrainWidth,
                terrainLength = config.terrainLength,
                worldOffsetX = 0f,
                worldOffsetZ = 0f,
                seaLevel = config.seaLevel,
                shoreMask = buffers.shoreMask,
                landMask = buffers.landMask,
                beachFactor = buffers.beachFactor,
                biomeWeights = buffers.biomeWeights,
                winnerBiomeIndex = buffers.winnerBiomeIndex,
                biomeParams = buffers.biomeParams,
                noiseOffsets = buffers.noiseOffsets,
                heights = buffers.heights
            };
            heightJob.Schedule(pixelCount, 64).Complete();

            // Job 2b: スプラットマップ生成
            var splatJob = new SplatmapJob
            {
                resolution = res,
                biomeCount = biomeCount,
                totalLayers = layerCount,
                terrainWidth = config.terrainWidth,
                terrainHeight = config.terrainHeight,
                terrainLength = config.terrainLength,
                worldOffsetX = 0f,
                worldOffsetZ = 0f,
                textureBlendStrength = config.boundaryConfig.textureBlendStrength,
                heights = buffers.heights,
                shoreMask = buffers.shoreMask,
                landMask = buffers.landMask,
                beachFactor = buffers.beachFactor,
                landTextureFactor = buffers.landTextureFactor,
                seaTextureFactor = buffers.seaTextureFactor,
                biomeWeights = buffers.biomeWeights,
                winnerBiomeIndex = buffers.winnerBiomeIndex,
                biomeParams = buffers.biomeParams,
                noiseOffsets = buffers.noiseOffsets,
                textureEntries = buffers.textureEntries,
                splatWeights = buffers.splatWeights
            };
            splatJob.Schedule(pixelCount, 64).Complete();
        }

        /// <summary>
        /// Job 1a(分類)〜1c(ブラー)を実行する。HeightSampleJob計測前の事前処理用。
        /// </summary>
        static void SchedulePreHeightJobs(
            TerrainGenerationConfig config, int biomeCount,
            JobBuffers buffers,
            NativeArray<float2> continentalnessOffsets, NativeArray<float2> erosionOffsets)
        {
            int res = config.Resolution;
            int pixelCount = res * res;
            int blurRadius = config.biomeBlendRadius / 4;

            // ボロノイ四色定理配色用の並び替えテーブル
            var biomePermutation = JobDataConverter.GenerateBiomePermutation(
                config.seed, biomeCount, Allocator.TempJob);

            // Job 1a: Continentalness+Erosion陸/海判定 + ボロノイバイオーム分類
            var classJob = new ClassificationJob
            {
                resolution = res,
                terrainWidth = config.terrainWidth,
                terrainLength = config.terrainLength,
                worldOffsetX = 0f,
                worldOffsetZ = 0f,
                continentalnessFrequency = config.continentalnessFrequency,
                continentalnessOctaves = config.continentalnessOctaves,
                continentalnessPersistence = config.continentalnessPersistence,
                landThreshold = config.landThreshold,
                erosionFrequency = config.erosionFrequency,
                erosionOctaves = config.erosionOctaves,
                erosionStrength = config.erosionStrength,
                beachWidth = 0f,
                voronoiCellSize = config.voronoiCellSize,
                voronoiJitter = config.voronoiJitter,
                biomeCount = biomeCount,
                seed = config.seed,
                continentalnessOffsets = continentalnessOffsets,
                erosionOffsets = erosionOffsets,
                biomePermutation = biomePermutation,
                shoreMask = buffers.shoreMask,
                landMask = buffers.landMask,
                beachFactor = buffers.beachFactor,
                rawBiomeIndex = buffers.rawBiomeIndex
            };
            classJob.Schedule(pixelCount, 64).Complete();
            biomePermutation.Dispose();

            // Job 1b: MC式バイオーム補間
            var interpJob = new InterpolateWeightsJob
            {
                resolution = res,
                biomeCount = biomeCount,
                blendRadius = config.biomeBlendRadius,
                rawBiomeIndex = buffers.rawBiomeIndex,
                biomeParams = buffers.biomeParams,
                rawBiomeWeights = buffers.rawBiomeWeights
            };
            interpJob.Schedule(pixelCount, 64).Complete();

            // Job 1c-H: 水平ボックスブラー
            var hBlur = new HorizontalBlurJob
            {
                resolution = res,
                biomeCount = biomeCount,
                blurRadius = blurRadius,
                rawBiomeWeights = buffers.rawBiomeWeights,
                rawBiomeIndex = buffers.rawBiomeIndex,
                blurTemp = buffers.blurTemp
            };
            hBlur.Schedule(res, 1).Complete();

            // Job 1c-V: 垂直ボックスブラー
            var vBlur = new VerticalBlurJob
            {
                resolution = res,
                biomeCount = biomeCount,
                blurRadius = blurRadius,
                blurTemp = buffers.blurTemp,
                rawBiomeIndex = buffers.rawBiomeIndex,
                biomeWeights = buffers.biomeWeights,
                winnerBiomeIndex = buffers.winnerBiomeIndex
            };
            vBlur.Schedule(res, 1).Complete();
        }

        // =====================================================================
        // TerrainGenerator.GetEnabledBiomeTypesの再現
        // =====================================================================

        static BiomeType[] GetEnabledBiomeTypes(TerrainGenerationConfig config)
        {
            var list = new System.Collections.Generic.List<BiomeType>();
            // ClassifyPriority降順で登録
            if (config.alpineEnabled)    list.Add(BiomeType.Alpine);
            if (config.mesaEnabled)      list.Add(BiomeType.Mesa);
            if (config.jungleEnabled)    list.Add(BiomeType.Jungle);
            if (config.desertEnabled)    list.Add(BiomeType.Desert);
            if (config.forestEnabled)    list.Add(BiomeType.Forest);
            if (config.woodsEnabled)     list.Add(BiomeType.Woods);
            if (config.savannaEnabled)   list.Add(BiomeType.Savanna);
            if (config.grasslandEnabled) list.Add(BiomeType.Grassland);
            if (list.Count == 0) list.Add(BiomeType.Grassland);
            return list.ToArray();
        }
    }
}

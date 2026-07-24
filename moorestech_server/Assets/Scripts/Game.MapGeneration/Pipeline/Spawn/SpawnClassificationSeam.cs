using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Spawn
{
    // SpawnRegionFinder が要求する分類結果ウィンドウ（本番一致 final 検証用）。
    // The classification-result window SpawnRegionFinder consumes for final verification.
    public sealed class ClassificationWindow
    {
        public int Resolution;
        public float PitchX;
        public float PitchZ;
        public float OriginX;
        public float OriginZ;
        public int[] WinnerBiomeIndex;
        public float[] LandMask;
        public float[] BeachFactor;
    }

    // スポーン探索用の分類関数。段1は粗グリッドの raw 分類、段2は本番一致の窓分類を返す。
    // Classification functions for spawn search: stage 1 raw coarse grid, stage 2 production-matching window.
    public static class SpawnClassificationSeam
    {
        // 段1: 本番同一の ClassificationJob(raw) を粗グリッドで1回実行し、セル別バイオーム配列インデックスを返す。
        // Stage 1: run the production-identical raw ClassificationJob once on a coarse grid.
        public static CoarseBiomeGrid ClassifyRawGrid(
            TerrainGenerationConfig config, BiomeType[] biomeTypes,
            float centerX, float centerZ, float extent, float cellSize)
        {
            int biomeCount = biomeTypes.Length;
            int res = Mathf.Max(2, Mathf.CeilToInt(extent / cellSize) + 1);
            float originX = centerX - extent * 0.5f;
            float originZ = centerZ - extent * 0.5f;
            int pixelCount = res * res;

            NativeArray<float2> contOffsets = default, erosionOffsets = default;
            NativeArray<int> biomePermutation = default, rawBiomeIndex = default;
            NativeArray<float> shoreMask = default, landMask = default, beachFactor = default;
            try
            {
                JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob,
                    out contOffsets, out erosionOffsets);
                biomePermutation = JobDataConverter.GenerateBiomePermutation(config.seed, biomeCount, Allocator.TempJob);
                rawBiomeIndex = new NativeArray<int>(pixelCount, Allocator.TempJob);
                shoreMask = new NativeArray<float>(pixelCount, Allocator.TempJob);
                landMask = new NativeArray<float>(pixelCount, Allocator.TempJob);
                beachFactor = new NativeArray<float>(pixelCount, Allocator.TempJob);

                var classJob = ClassificationStage.BuildClassificationJob(config, biomeCount, res,
                    extent, extent, originX, originZ,
                    contOffsets, erosionOffsets, biomePermutation,
                    shoreMask, landMask, beachFactor, rawBiomeIndex);
                classJob.Schedule(pixelCount, 64).Complete();

                var arr = new int[pixelCount];
                rawBiomeIndex.CopyTo(arr);
                return new CoarseBiomeGrid(arr, res, res, extent / (res - 1), originX, originZ);
            }
            finally
            {
                if (contOffsets.IsCreated) contOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
                if (biomePermutation.IsCreated) biomePermutation.Dispose();
                if (rawBiomeIndex.IsCreated) rawBiomeIndex.Dispose();
                if (shoreMask.IsCreated) shoreMask.Dispose();
                if (landMask.IsCreated) landMask.Dispose();
                if (beachFactor.IsCreated) beachFactor.Dispose();
            }
        }

        // 段2: 本番 m/px に一致する局所窓で分類パイプラインを走らせ final winner/land/beach を返す。
        // Stage 2: run the classification pipeline on a production-pitch local window for final winner/land/beach.
        public static ClassificationWindow RunClassificationDetailed(
            TerrainGenerationConfig baseConfig, BiomeType[] biomeTypes,
            float windowCenterX, float windowCenterZ, float windowSize)
        {
            if (baseConfig.overrideResolution != 0)
                throw new ArgumentException(
                    "RunClassificationDetailed requires the production config (overrideResolution == 0).",
                    nameof(baseConfig));

            double pX = (double)baseConfig.terrainWidth / (baseConfig.Resolution - 1);
            double pZ = (double)baseConfig.terrainLength / (baseConfig.Resolution - 1);
            int res = Mathf.Max(2, Mathf.CeilToInt((float)(windowSize / pX)) + 1);
            double actualX = (res - 1) * pX;
            double actualZ = (res - 1) * pZ;
            double rawOriginX = windowCenterX - actualX * 0.5;
            double rawOriginZ = windowCenterZ - actualZ * 0.5;
            float originX = (float)(Math.Round(rawOriginX / pX) * pX);
            float originZ = (float)(Math.Round(rawOriginZ / pZ) * pZ);

            int biomeCount = biomeTypes.Length;
            int pixelCount = res * res;

            var cfg = baseConfig.ShallowCopy();
            cfg.overrideResolution = res;
            cfg.terrainWidth = (float)actualX;
            cfg.terrainLength = (float)actualZ;
            cfg.worldOffsetX = originX;
            cfg.worldOffsetZ = originZ;

            NativeArray<BiomeParams> biomeParams = default;
            NativeArray<float2> contOffsets = default, erosionOffsets = default;
            JobBuffers buffers = default;
            bool buffersAllocated = false;
            try
            {
                biomeParams = JobDataConverter.ConvertBiomeParams(cfg, biomeTypes, Allocator.TempJob);
                JobDataConverter.GenerateClassificationOffsets(cfg, Allocator.TempJob, out contOffsets, out erosionOffsets);
                buffers = JobDataConverter.AllocateBuffers(res, biomeCount, 1, Allocator.TempJob);
                buffers.biomeParams = biomeParams;
                buffersAllocated = true;

                // 窓端の大海を保護してクリップ誤判定を防ぐ
                // Protect large edge-sea to avoid clip misclassification
                ClassificationStage.Run(cfg, biomeCount, buffers, contOffsets, erosionOffsets, protectEdgeSea: true);

                var winner = new int[pixelCount];
                var land = new float[pixelCount];
                var beach = new float[pixelCount];
                buffers.winnerBiomeIndex.CopyTo(winner);
                buffers.landMask.CopyTo(land);
                buffers.beachFactor.CopyTo(beach);

                return new ClassificationWindow
                {
                    Resolution = res,
                    PitchX = (float)pX,
                    PitchZ = (float)pZ,
                    OriginX = originX,
                    OriginZ = originZ,
                    WinnerBiomeIndex = winner,
                    LandMask = land,
                    BeachFactor = beach
                };
            }
            finally
            {
                if (buffersAllocated) buffers.Dispose();
                else if (biomeParams.IsCreated) biomeParams.Dispose();
                if (contOffsets.IsCreated) contOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
            }
        }
    }
}

using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // ステージ1: 陸海判定・ボロノイ分類・ビーチ遷移・バイオーム重みブラー。
    // Stage 1: land/sea classification, voronoi biome assignment, beach transition, weight blur.
    public static class ClassificationStage
    {
        // config の有効フラグからバイオーム配列を ClassifyPriority 降順で構築する。
        // Build the biome-type array in ClassifyPriority-descending order from config flags.
        public static BiomeType[] GetEnabledBiomeTypes(TerrainGenerationConfig config)
        {
            var list = new List<BiomeType>();
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

        // ボロノイ分類→小海除去→ビーチ遷移→補間→水平/垂直ブラーで最終バイオーム重みを確定する。
        // Voronoi classify -> small-sea removal -> beach transition -> interpolate -> H/V blur.
        public static void Run(
            TerrainGenerationConfig config, int biomeCount, JobBuffers buffers,
            NativeArray<float2> continentalnessOffsets, NativeArray<float2> erosionOffsets,
            bool protectEdgeSea)
        {
            int res = config.Resolution;
            int pixelCount = res * res;
            var shoreConfig = config.shoreConfig;

            var biomePermutation = JobDataConverter.GenerateBiomePermutation(
                config.seed, biomeCount, Allocator.TempJob);

            // Job 1a: Continentalness+Erosion で陸/海判定 + ボロノイでバイオーム分類
            // Job 1a: land/sea via continentalness+erosion, biome via voronoi
            var classJob = BuildClassificationJob(config, biomeCount, res,
                config.terrainWidth, config.terrainLength, config.worldOffsetX, config.worldOffsetZ,
                continentalnessOffsets, erosionOffsets, biomePermutation,
                buffers.shoreMask, buffers.landMask, buffers.beachFactor, buffers.rawBiomeIndex);
            classJob.Schedule(pixelCount, 64).Complete();
            biomePermutation.Dispose();

            // Job 1a-post: 小さな海領域を陸に変換（ビーチ判定前）
            // Job 1a-post: convert tiny sea regions to land before beach detection
            if (shoreConfig.minSeaRegionSize > 0)
            {
                new SmallSeaRemovalJob
                {
                    resolution = res,
                    minRegionSize = shoreConfig.minSeaRegionSize,
                    protectEdgeRegions = protectEdgeSea,
                    shoreMask = buffers.shoreMask,
                    landMask = buffers.landMask,
                    rawBiomeIndex = buffers.rawBiomeIndex
                }.Schedule().Complete();
            }

            RunBeachTransition(config, res, pixelCount, shoreConfig, buffers);

            // Job 1b: 距離ベースのバイオーム補間
            // Job 1b: distance-based biome interpolation
            new InterpolateWeightsJob
            {
                resolution = res,
                biomeCount = biomeCount,
                blendRadius = config.biomeBlendRadius,
                rawBiomeIndex = buffers.rawBiomeIndex,
                biomeParams = buffers.biomeParams,
                rawBiomeWeights = buffers.rawBiomeWeights
            }.Schedule(pixelCount, 64).Complete();

            // Job 1c: 水平→垂直ボックスブラーで最終重みと winner を確定
            // Job 1c: horizontal then vertical box blur for final weights and winner
            int divisor = Mathf.Max(1, config.boundaryConfig.blurRadiusDivisor);
            int blurRadius = config.biomeBlendRadius / divisor;
            new HorizontalBlurJob
            {
                resolution = res,
                biomeCount = biomeCount,
                blurRadius = blurRadius,
                rawBiomeWeights = buffers.rawBiomeWeights,
                rawBiomeIndex = buffers.rawBiomeIndex,
                blurTemp = buffers.blurTemp
            }.Schedule(res, 1).Complete();

            new VerticalBlurJob
            {
                resolution = res,
                biomeCount = biomeCount,
                blurRadius = blurRadius,
                blurTemp = buffers.blurTemp,
                rawBiomeIndex = buffers.rawBiomeIndex,
                biomeWeights = buffers.biomeWeights,
                winnerBiomeIndex = buffers.winnerBiomeIndex
            }.Schedule(res, 1).Complete();
        }

        // 本番/窓/粗グリッドで共通の ClassificationJob を構築する（フィールド構成を1箇所に集約）。
        // Build the ClassificationJob shared across production/window/coarse grids (single field source).
        public static ClassificationJob BuildClassificationJob(
            TerrainGenerationConfig config, int biomeCount, int res,
            float terrainWidth, float terrainLength, float worldOffsetX, float worldOffsetZ,
            NativeArray<float2> continentalnessOffsets, NativeArray<float2> erosionOffsets,
            NativeArray<int> biomePermutation,
            NativeArray<float> shoreMask, NativeArray<float> landMask,
            NativeArray<float> beachFactor, NativeArray<int> rawBiomeIndex)
        {
            return new ClassificationJob
            {
                resolution = res,
                terrainWidth = terrainWidth,
                terrainLength = terrainLength,
                worldOffsetX = worldOffsetX,
                worldOffsetZ = worldOffsetZ,
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
                shoreMask = shoreMask,
                landMask = landMask,
                beachFactor = beachFactor,
                rawBiomeIndex = rawBiomeIndex
            };
        }

        // Job 1a-beach: EDT 距離場 + Burst ジョブでビーチ遷移帯を生成する。
        // Job 1a-beach: build the beach transition band via EDT distance fields plus a Burst job.
        static void RunBeachTransition(
            TerrainGenerationConfig config, int res, int pixelCount,
            BiomeShoreConfig shoreConfig, JobBuffers buffers)
        {
            var landArr = new float[pixelCount];
            buffers.landMask.CopyTo(landArr);
            var distToSeaSqArr = ClassificationDistanceField.ComputeSq(landArr, res, false);
            var distToLandSqArr = ClassificationDistanceField.ComputeSq(landArr, res, true);

            var distToSeaSq = new NativeArray<float>(pixelCount, Allocator.TempJob);
            var distToLandSq = new NativeArray<float>(pixelCount, Allocator.TempJob);
            distToSeaSq.CopyFrom(distToSeaSqArr);
            distToLandSq.CopyFrom(distToLandSqArr);

            new BeachTransitionJob
            {
                resolution = res,
                beachLandTextureRadius = shoreConfig.beachLandTextureRadius,
                beachLandTerrainRadius = shoreConfig.beachLandTerrainRadius,
                beachSeaTextureRadius = shoreConfig.beachSeaTextureRadius,
                beachSeaTerrainRadius = shoreConfig.beachSeaTerrainRadius,
                landMask = buffers.landMask,
                distToSeaSq = distToSeaSq,
                distToLandSq = distToLandSq,
                shoreMask = buffers.shoreMask,
                beachFactor = buffers.beachFactor,
                coastalSmoothFactor = buffers.coastalSmoothFactor,
                landTextureFactor = buffers.landTextureFactor,
                seaTextureFactor = buffers.seaTextureFactor
            }.Schedule(pixelCount, 64).Complete();

            distToSeaSq.Dispose();
            distToLandSq.Dispose();
        }
    }
}

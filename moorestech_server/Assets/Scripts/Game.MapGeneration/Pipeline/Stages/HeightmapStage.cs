using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // ステージ2: バイオーム加重高さ生成→海岸平滑→Alpine台地→高さブラー→スロープ→境界ノイズ。
    // テクスチャ(Splatmap)経路はサーバー非対象のため実行しない。
    // Stage 2: weighted height sampling, coastal smooth, alpine plateau, height blur, slope, boundary noise.
    // The texture (splatmap) path is not run (server-irrelevant).
    public static class HeightmapStage
    {
        public static void Run(TerrainGenerationConfig config, int biomeCount, JobBuffers buffers)
        {
            int res = config.Resolution;
            int pixelCount = res * res;
            var shoreConfig = config.shoreConfig;

            // Job 2a: バイオーム加重ブレンドで高さを生成
            // Job 2a: generate heights via biome-weighted blend
            new HeightSampleJob
            {
                resolution = res,
                biomeCount = biomeCount,
                terrainWidth = config.terrainWidth,
                terrainLength = config.terrainLength,
                worldOffsetX = config.worldOffsetX,
                worldOffsetZ = config.worldOffsetZ,
                seaLevel = config.seaLevel,
                beachElevation = shoreConfig.beachElevation,
                shoreMask = buffers.shoreMask,
                landMask = buffers.landMask,
                beachFactor = buffers.beachFactor,
                biomeWeights = buffers.biomeWeights,
                winnerBiomeIndex = buffers.winnerBiomeIndex,
                biomeParams = buffers.biomeParams,
                noiseOffsets = buffers.noiseOffsets,
                heights = buffers.heights
            }.Schedule(pixelCount, 64).Complete();

            // 砂浜近傍だけを1回平均化して継ぎ目を和らげる
            // Average around the beach once to soften the seam
            new CoastalSmoothJob
            {
                resolution = res,
                landMask = buffers.landMask,
                coastalSmoothFactor = buffers.coastalSmoothFactor,
                inputHeights = buffers.heights,
                outputHeights = buffers.blurTemp.GetSubArray(0, pixelCount)
            }.Schedule(pixelCount, 64).Complete();
            NativeArray<float>.Copy(buffers.blurTemp, 0, buffers.heights, 0, pixelCount);

            AlpinePlateauStage.Apply(config, res, pixelCount, buffers);

            RunHeightPostProcess(config, res, pixelCount, buffers);
        }

        // 高さブラー→スロープ平滑→崖面侵食ノイズの順に後処理を掛ける。
        // Apply post-processing: height blur, slope smoothing, then cliff-erosion boundary noise.
        static void RunHeightPostProcess(TerrainGenerationConfig config, int res, int pixelCount, JobBuffers buffers)
        {
            // Job 2a-blur: 高さマップのガウシアンブラー（Jungle段差平滑化）
            // Job 2a-blur: gaussian blur for jungle terrace smoothing
            int heightBlurRadius = GetHeightBlurRadius(buffers.biomeParams);
            if (0 < heightBlurRadius)
            {
                new HeightBlurHorizontalJob
                {
                    resolution = res,
                    blurRadius = heightBlurRadius,
                    heights = buffers.heights,
                    blurTemp = buffers.blurTemp
                }.Schedule(res, 1).Complete();

                new HeightBlurVerticalJob
                {
                    resolution = res,
                    blurRadius = heightBlurRadius,
                    blurTemp = buffers.blurTemp,
                    heights = buffers.heights
                }.Schedule(res, 1).Complete();
            }

            // Job 2a-slope: ランダム地点を中心に追加平滑化
            // Job 2a-slope: extra smoothing centered on random points
            var slope = GetSlopeParams(buffers.biomeParams);
            if (0f < slope.Density && 0 < slope.Radius && 0f < slope.BlendStrength)
            {
                NativeArray<float>.Copy(buffers.heights, 0, buffers.blurTemp, 0, pixelCount);
                new HeightSlopeJob
                {
                    resolution = res,
                    slopeRadius = slope.Radius,
                    slopeDensity = slope.Density,
                    slopeCellSize = slope.CellSize,
                    slopeBlendStrength = slope.BlendStrength,
                    terrainWidth = config.terrainWidth,
                    terrainLength = config.terrainLength,
                    blurTemp = buffers.blurTemp,
                    heights = buffers.heights
                }.Schedule(res, 1).Complete();
            }

            // Job 2a-noise: ブラー後の崖面を侵食ノイズで削る
            // Job 2a-noise: erode post-blur cliffs with boundary noise
            if (config.jungleEnabled && 0f < config.jungle.boundaryNoiseStrength)
            {
                NativeArray<float>.Copy(buffers.heights, 0, buffers.blurTemp, 0, pixelCount);
                new BoundaryNoiseJob
                {
                    resolution = res,
                    terrainWidth = config.terrainWidth,
                    terrainLength = config.terrainLength,
                    terrainHeight = config.terrainHeight,
                    noiseStrength = config.jungle.boundaryNoiseStrength,
                    slopeThreshold = config.jungle.boundaryNoiseSlopeThreshold,
                    noiseFrequency = config.jungle.boundaryNoiseFrequency,
                    seed = 12345f,
                    smoothstepWidth = config.boundaryConfig.boundaryNoiseSmoothstepWidth,
                    noiseMidWeight = config.boundaryConfig.boundaryNoiseMidWeight,
                    noiseHighWeight = config.boundaryConfig.boundaryNoiseHighWeight,
                    readHeights = buffers.blurTemp,
                    heights = buffers.heights
                }.Schedule(res, 1).Complete();
            }
        }

        // Jungle(8) の terraceSharpness からガウシアンブラー半径（最大20px）を算出する。
        // Compute the gaussian blur radius (max 20px) from Jungle(8) terraceSharpness.
        static int GetHeightBlurRadius(NativeArray<BiomeParams> biomeParams)
        {
            int maxRadius = 0;
            for (int i = 0; i < biomeParams.Length; i++)
            {
                var bp = biomeParams[i];
                if (bp.biomeType == 8 && 0f < bp.terraceSharpness)
                {
                    int r = (int)(bp.terraceSharpness * 20f);
                    if (maxRadius < r) maxRadius = r;
                }
            }
            return maxRadius;
        }

        readonly struct SlopeInfo
        {
            public readonly float Density;
            public readonly float CellSize;
            public readonly int Radius;
            public readonly float BlendStrength;
            public SlopeInfo(float density, float cellSize, int radius, float blendStrength)
            {
                Density = density; CellSize = cellSize; Radius = radius; BlendStrength = blendStrength;
            }
        }

        // HeightSlopeJob 用パラメータを canyonOctaves>0 の最初のバイオームから取得する。
        // Get HeightSlopeJob params from the first biome whose canyonOctaves > 0.
        static SlopeInfo GetSlopeParams(NativeArray<BiomeParams> biomeParams)
        {
            for (int i = 0; i < biomeParams.Length; i++)
            {
                var bp = biomeParams[i];
                if (0 < bp.canyonOctaves && 0f < bp.secondaryAmplitude)
                    return new SlopeInfo(bp.secondaryAmplitude, bp.secondaryFrequency, bp.canyonOctaves, bp.absSmoothing);
            }
            return default;
        }
    }
}

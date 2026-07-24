using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// Job 2b: 完成済み高さマップから傾斜・曲率を算出し、テクスチャ重みを決定する。
    /// SplatmapFilter.ComputeTextureWeights() + BiomeWeightAssigner.Assign()のBurst並列版。
    /// HeightSampleJob完了後に実行（傾斜計算に隣接ピクセルの高さが必要なため）。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct SplatmapJob : IJobParallelFor
    {
        public int resolution;
        public int biomeCount;
        public int totalLayers;
        public float terrainWidth, terrainHeight, terrainLength;
        public float worldOffsetX, worldOffsetZ;
        // 0=勝者バイオームのみ、1=biomeWeightsで完全ブレンド
        public float textureBlendStrength;

        [ReadOnly] public NativeArray<float> heights;
        [ReadOnly] public NativeArray<float> shoreMask;
        [ReadOnly] public NativeArray<float> landMask;
        [ReadOnly] public NativeArray<float> beachFactor;
        // 陸側砂テクスチャ遷移（BeachTransitionJob生成）
        [ReadOnly] public NativeArray<float> landTextureFactor;
        // 海側砂テクスチャ遷移（BeachTransitionJob生成）
        [ReadOnly] public NativeArray<float> seaTextureFactor;
        [ReadOnly] public NativeArray<float> biomeWeights;
        [ReadOnly] public NativeArray<int> winnerBiomeIndex;
        [ReadOnly] public NativeArray<BiomeParams> biomeParams;
        [ReadOnly] public NativeArray<float2> noiseOffsets;
        [ReadOnly] public NativeArray<TextureEntryParams> textureEntries;

        // [idx * totalLayers + layer] のフラットレイアウト
        [NativeDisableParallelForRestriction]
        public NativeArray<float> splatWeights;

        public void Execute(int idx)
        {
            int winner = winnerBiomeIndex[idx];
            // 海ピクセルはバイオームテクスチャをスキップ、砂ブレンドは後続で処理
            if (winner < 0)
            {
                float seaTex = seaTextureFactor[idx];
                if (seaTex > 0.01f)
                {
                    // 海側ビーチ: 砂テクスチャと海底テクスチャをブレンド
                    splatWeights[idx * totalLayers] = seaTex;
                    splatWeights[idx * totalLayers + 1] = 1f - seaTex;
                }
                else
                {
                    splatWeights[idx * totalLayers + 1] = 1f;
                }
                return;
            }

            int x = idx % resolution;
            int y = idx / resolution;
            // ワールドオフセットを加算して無限ワールドの任意位置をサンプル
            float worldX = worldOffsetX + (float)x / (resolution - 1) * terrainWidth;
            float worldZ = worldOffsetZ + (float)y / (resolution - 1) * terrainLength;

            // 隣接ピクセルから傾斜・曲率を算出（テクスチャフィルタの入力）
            float slope = BurstTerrainMath.ComputeSlope(
                heights, resolution, x, y,
                terrainWidth, terrainHeight, terrainLength);
            float curvature = BurstTerrainMath.ComputeCurvature(heights, resolution, x, y);
            float heightVal = heights[idx];

            // 主テクスチャ割り当て: textureBlendStrengthで勝者のみ↔全バイオームブレンドを制御
            var wp = biomeParams[winner];
            if (textureBlendStrength <= 0.001f)
            {
                // 高速パス: 勝者のみ
                int layerIdx = wp.splatmapLayerIndex;
                if (layerIdx >= 0 && layerIdx < totalLayers)
                    splatWeights[idx * totalLayers + layerIdx] = 1f;
            }
            else
            {
                for (int b = 0; b < biomeCount; b++)
                {
                    float bw = biomeWeights[idx * biomeCount + b];
                    if (bw > 0.01f)
                    {
                        int layerIdx = biomeParams[b].splatmapLayerIndex;
                        if (layerIdx >= 0 && layerIdx < totalLayers)
                        {
                            // blend=1で完全ブレンド、0で勝者のみ
                            float blended = (b == winner)
                                ? math.lerp(1f, bw, textureBlendStrength)
                                : bw * textureBlendStrength;
                            splatWeights[idx * totalLayers + layerIdx] += blended;
                        }
                    }
                }
            }
            // テクスチャエントリ: 全バイオームのエントリをバイオーム重みで加重適用
            for (int b = 0; b < biomeCount; b++)
            {
                float bw = biomeWeights[idx * biomeCount + b];
                if (bw <= 0.01f) continue;
                var bp = biomeParams[b];
                for (int e = 0; e < bp.textureEntryCount; e++)
                {
                    var entry = textureEntries[bp.textureEntryBase + e];
                    float w = entry.weight * bw;

                    if (entry.useSlopeFilter != 0)
                        w *= BurstTerrainMath.FilterRange(slope,
                            entry.slopeMin, entry.slopeMax,
                            entry.slopeSmoothness, entry.slopeSmoothness);

                    if (entry.useHeightFilter != 0)
                        w *= BurstTerrainMath.FilterRange(heightVal,
                            entry.heightMin, entry.heightMax,
                            entry.heightSmoothness, entry.heightSmoothness);

                    if (entry.useCurvatureFilter != 0)
                        w *= BurstTerrainMath.FilterRange(curvature,
                            entry.curvatureMin, entry.curvatureMax,
                            entry.curvatureSmoothness, entry.curvatureSmoothness);

                    if (entry.noiseType > 0)
                    {
                        float n = BurstNoise.SampleByType(entry.noiseType,
                            new float2(worldX, worldZ), entry.noiseFrequency,
                            noiseOffsets, entry.noiseOffsetIndex);
                        w *= math.saturate(n * entry.noiseAmplitude);
                    }

                    if (entry.layerIndex >= 0 && entry.layerIndex < totalLayers)
                        splatWeights[idx * totalLayers + entry.layerIndex] += w;
                }
            }

            // テクスチャ: 海底 / ビーチ×バイオームのブレンド / バイオーム
            float shore = shoreMask[idx];
            float bf = beachFactor[idx];

            // 砂テクスチャのブレンド量: 陸側landTextureFactor、海側seaTextureFactor
            float sandBlend = math.max(landTextureFactor[idx], seaTextureFactor[idx]);

            int rockIdx = wp.rockFallbackLayerIndex;
            float deepThresh = wp.deepSeaThreshold;
            float sandThresh = wp.sandBlendThreshold;
            if (shore < deepThresh && sandBlend < sandThresh)
            {
                // 深海: rockLayer(砂利) 100%
                for (int l = 0; l < totalLayers; l++)
                    splatWeights[idx * totalLayers + l] = 0f;
                splatWeights[idx * totalLayers + rockIdx] = 1f;
            }
            else
            {
                // バイオームテクスチャを正規化
                float total = 0f;
                for (int l = 0; l < totalLayers; l++)
                    total += splatWeights[idx * totalLayers + l];
                if (total > 0f)
                {
                    float inv = 1f / total;
                    for (int l = 0; l < totalLayers; l++)
                        splatWeights[idx * totalLayers + l] *= inv;
                }
                else
                {
                    splatWeights[idx * totalLayers + rockIdx] = 1f;
                }

                // 砂テクスチャをブレンド（陸側beachFactor + 海側seaTextureFactor）
                if (sandBlend > sandThresh)
                {
                    for (int l = 0; l < totalLayers; l++)
                        splatWeights[idx * totalLayers + l] *= (1f - sandBlend);
                    splatWeights[idx * totalLayers] += sandBlend;
                }
            }
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// Job 2a: バイオーム加重ブレンドによるピクセル単位の高さ生成。
    /// IslandHeightSampler.Sample()のBurst並列版。Job 1c(BlurWeights)完了後に実行。
    /// 出力heightsはJob 2b(SplatmapJob)の傾斜・曲率計算に使用される。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct HeightSampleJob : IJobParallelFor
    {
        public int resolution;
        public int biomeCount;
        public float terrainWidth, terrainLength;
        public float worldOffsetX, worldOffsetZ;
        public float seaLevel;
        public float beachElevation;

        [ReadOnly] public NativeArray<float> shoreMask;
        [ReadOnly] public NativeArray<float> landMask;
        [ReadOnly] public NativeArray<float> beachFactor;
        [ReadOnly] public NativeArray<float> biomeWeights;
        [ReadOnly] public NativeArray<int> winnerBiomeIndex;
        [ReadOnly] public NativeArray<BiomeParams> biomeParams;
        [ReadOnly] public NativeArray<float2> noiseOffsets;

        // 各ピクセルの最終高さ（0〜1正規化）
        [NativeDisableParallelForRestriction]
        public NativeArray<float> heights;

        public void Execute(int idx)
        {
            int winner = winnerBiomeIndex[idx];
            // 海ピクセル（バイオーム未割当）でもビーチ遷移帯なら高さを生成する
            if (winner < 0)
            {
                if (beachFactor[idx] > 0.01f)
                {
                    // ビーチ: 海面(0)→seaLevel+beachElevation をshoreMaskで遷移
                    heights[idx] = math.saturate(shoreMask[idx] * (seaLevel + beachElevation));
                }
                else
                {
                    heights[idx] = 0f;
                }
                return;
            }

            int x = idx % resolution;
            int y = idx / resolution;
            // ワールドオフセットを加算して無限ワールドの任意位置をサンプル
            float worldX = worldOffsetX + (float)x / (resolution - 1) * terrainWidth;
            float worldZ = worldOffsetZ + (float)y / (resolution - 1) * terrainLength;
            float2 pos = new float2(worldX, worldZ);

            float height;
            float winnerWeight = biomeWeights[idx * biomeCount + winner];

            // 高速パス: winner重みが支配的ならSampleHeight 1回で済ませる（全ピクセルの~90%）
            float fastPath = biomeParams[winner].heightBlendFastPathThreshold;
            if (winnerWeight >= fastPath)
            {
                var bp = biomeParams[winner];
                height = BurstBiomeSampler.Sample(
                    bp.biomeType, pos, bp, noiseOffsets, bp.noiseOffsetBase);
            }
            else
            {
                // 境界ピクセル: 上位2バイオームのみブレンド（3位以下は視覚影響が小さい）
                int b2 = -1;
                float w2 = 0f;
                for (int b = 0; b < biomeCount; b++)
                {
                    if (b == winner) continue;
                    float w = biomeWeights[idx * biomeCount + b];
                    if (w > w2) { b2 = b; w2 = w; }
                }

                var bp1 = biomeParams[winner];
                float h1 = BurstBiomeSampler.Sample(
                    bp1.biomeType, pos, bp1, noiseOffsets, bp1.noiseOffsetBase);

                if (b2 >= 0 && w2 > biomeParams[winner].heightBlendMinWeight)
                {
                    var bp2 = biomeParams[b2];
                    float h2 = BurstBiomeSampler.Sample(
                        bp2.biomeType, pos, bp2, noiseOffsets, bp2.noiseOffsetBase);
                    float invSum = 1f / (winnerWeight + w2);
                    height = h1 * (winnerWeight * invSum) + h2 * (w2 * invSum);
                }
                else
                {
                    height = h1;
                }
            }

            float shore = shoreMask[idx];
            float beach = beachFactor[idx];
            float beachTop = seaLevel + beachElevation;

            float finalHeight;
            if (beach < biomeParams[winner].beachThreshold)
            {
                // 内陸（ビーチから遠い）: バイオーム高さをそのまま使用
                finalHeight = height;
            }
            else
            {
                // ビーチ遷移帯（陸側）: バイオーム高さ → ビーチ上端高さへイーズイン
                // beachFactor=0 → バイオーム高さ、beachFactor=1 → beachTop
                finalHeight = math.lerp(height, beachTop, beach);
            }
            heights[idx] = math.saturate(finalHeight);
        }
    }
}

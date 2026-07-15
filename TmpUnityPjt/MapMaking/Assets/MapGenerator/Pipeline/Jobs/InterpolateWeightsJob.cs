using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapGenerator.Pipeline.Jobs
{
    /// <summary>
    /// Job 1b: Minecraft 1.18式のバイオーム補間。
    /// 各ピクセルの周囲をblendRadius内でサンプリングし、距離+baseHeight差ベースの
    /// 加重平均で連続的なバイオーム重みを算出する。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct InterpolateWeightsJob : IJobParallelFor
    {
        public int resolution;
        public int biomeCount;
        public int blendRadius;

        [ReadOnly] public NativeArray<int> rawBiomeIndex;
        [ReadOnly] public NativeArray<BiomeParams> biomeParams;

        // 出力: flat [pixel * biomeCount + b]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> rawBiomeWeights;

        public void Execute(int idx)
        {
            int x = idx % resolution;
            int y = idx / resolution;
            int centerWinner = rawBiomeIndex[idx];

            if (centerWinner < 0) return;

            // 高速パス: 4隅が全て同一バイオームなら境界なし→補間スキップ
            // 内部ピクセルの~80%がこのパスを通り、O(n²)ループを完全に回避
            if (IsUniformNeighborhood(x, y, centerWinner))
            {
                rawBiomeWeights[idx * biomeCount + centerWinner] = 1f;
                return;
            }

            float centerBase = biomeParams[centerWinner].baseHeight;
            // ステップ上限を20pxに制限してサンプリング精度を保つ
            int step = math.clamp(blendRadius / 10, 1, 20);
            float radius = (float)blendRadius;
            float totalWeight = 0f;

            for (int sy = -blendRadius; sy <= blendRadius; sy += step)
            {
                for (int sx = -blendRadius; sx <= blendRadius; sx += step)
                {
                    int nx = x + sx;
                    int ny = y + sy;
                    if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution) continue;

                    int nidx = ny * resolution + nx;
                    int sampleBiome = rawBiomeIndex[nidx];
                    if (sampleBiome < 0) continue;

                    float sampleBase = biomeParams[sampleBiome].baseHeight;
                    // リニア減衰: 半径端で0になり、中心付近をフラットに保つ
                    float dist = math.sqrt((float)(sx * sx + sy * sy));
                    float distWeight = math.max(0f, 1f - dist / radius);
                    float adjusted = distWeight / (math.abs(sampleBase - centerBase) + 2f);

                    rawBiomeWeights[idx * biomeCount + sampleBiome] += adjusted;
                    totalWeight += adjusted;
                }
            }

            if (totalWeight > 0f)
            {
                float inv = 1f / totalWeight;
                for (int b = 0; b < biomeCount; b++)
                    rawBiomeWeights[idx * biomeCount + b] *= inv;
            }
        }

        /// <summary>
        /// blendRadius四隅+中点をチェックし、全て同一バイオームなら境界なしと判定。
        /// 境界を漏らさないよう辺の中点も検査する。
        /// </summary>
        bool IsUniformNeighborhood(int x, int y, int center)
        {
            return GetBiomeClamped(x - blendRadius, y - blendRadius) == center
                && GetBiomeClamped(x + blendRadius, y - blendRadius) == center
                && GetBiomeClamped(x - blendRadius, y + blendRadius) == center
                && GetBiomeClamped(x + blendRadius, y + blendRadius) == center
                && GetBiomeClamped(x, y - blendRadius) == center
                && GetBiomeClamped(x, y + blendRadius) == center
                && GetBiomeClamped(x - blendRadius, y) == center
                && GetBiomeClamped(x + blendRadius, y) == center;
        }

        int GetBiomeClamped(int x, int y)
        {
            x = math.clamp(x, 0, resolution - 1);
            y = math.clamp(y, 0, resolution - 1);
            return rawBiomeIndex[y * resolution + x];
        }
    }
}

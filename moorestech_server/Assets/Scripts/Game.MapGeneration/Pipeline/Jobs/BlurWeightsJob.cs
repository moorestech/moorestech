using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// Job 1c-H: 水平ボックスブラー。rawBiomeWeightsの各行を平滑化しblurTempに書き出す。
    /// BiomeInterpolator.SmoothWeightsの水平パス相当。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct HorizontalBlurJob : IJobParallelFor
    {
        public int resolution;
        public int biomeCount;
        public int blurRadius;

        [ReadOnly] public NativeArray<float> rawBiomeWeights;
        [ReadOnly] public NativeArray<int> rawBiomeIndex;

        // 出力: 水平ブラー結果。垂直パスの入力になる
        [NativeDisableParallelForRestriction]
        public NativeArray<float> blurTemp;

        // 行単位で並列実行 (resolution回)
        public void Execute(int y)
        {
            for (int x = 0; x < resolution; x++)
            {
                int idx = y * resolution + x;
                if (rawBiomeIndex[idx] < 0) continue;

                int count = 0;
                for (int dx = -blurRadius; dx <= blurRadius; dx++)
                {
                    int nx = x + dx;
                    if (nx < 0 || nx >= resolution) continue;
                    int nidx = y * resolution + nx;
                    if (rawBiomeIndex[nidx] < 0) continue;
                    for (int b = 0; b < biomeCount; b++)
                        blurTemp[idx * biomeCount + b] += rawBiomeWeights[nidx * biomeCount + b];
                    count++;
                }

                if (count > 0)
                {
                    float inv = 1f / count;
                    for (int b = 0; b < biomeCount; b++)
                        blurTemp[idx * biomeCount + b] *= inv;
                }
            }
        }

    }

    /// <summary>
    /// Job 1c-V: 垂直ボックスブラー。blurTempを読みbiomeWeightsに書き出す。
    /// 再正規化して合計1.0にする。winnerBiomeIndexもargmaxで算出。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct VerticalBlurJob : IJobParallelFor
    {
        public int resolution;
        public int biomeCount;
        public int blurRadius;

        [ReadOnly] public NativeArray<float> blurTemp;
        [ReadOnly] public NativeArray<int> rawBiomeIndex;

        // 最終出力: 正規化済みバイオーム重みと勝者インデックス
        [NativeDisableParallelForRestriction]
        public NativeArray<float> biomeWeights;
        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> winnerBiomeIndex;

        // 列単位で並列実行 (resolution回)
        public void Execute(int x)
        {
            for (int y = 0; y < resolution; y++)
            {
                int idx = y * resolution + x;
                if (rawBiomeIndex[idx] < 0)
                {
                    winnerBiomeIndex[idx] = -1;
                    continue;
                }

                int count = 0;
                for (int dy = -blurRadius; dy <= blurRadius; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= resolution) continue;
                    int nidx = ny * resolution + x;
                    if (rawBiomeIndex[nidx] < 0) continue;
                    for (int b = 0; b < biomeCount; b++)
                        biomeWeights[idx * biomeCount + b] += blurTemp[nidx * biomeCount + b];
                    count++;
                }

                float maxW = 0f;
                int winner = 0;
                if (count > 0)
                {
                    float sum = 0f;
                    for (int b = 0; b < biomeCount; b++)
                        sum += biomeWeights[idx * biomeCount + b];
                    if (sum > 0f)
                    {
                        float inv = 1f / sum;
                        for (int b = 0; b < biomeCount; b++)
                        {
                            biomeWeights[idx * biomeCount + b] *= inv;
                            if (biomeWeights[idx * biomeCount + b] > maxW)
                            {
                                maxW = biomeWeights[idx * biomeCount + b];
                                winner = b;
                            }
                        }
                    }
                }
                winnerBiomeIndex[idx] = winner;
            }
        }

    }
}

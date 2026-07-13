using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapGenerator.Pipeline.Jobs
{
    /// <summary>
    /// 高さマップの分離ガウシアンブラー（水平パス）。
    /// HeightSampleJob完了後にSplatmapJobの前に実行し、
    /// Voronoiセル境界の急な段差を画像処理レベルで平滑化する。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct HeightBlurHorizontalJob : IJobParallelFor
    {
        public int resolution;
        public int blurRadius;

        [ReadOnly] public NativeArray<float> heights;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> blurTemp;

        // 行単位で並列実行
        public void Execute(int y)
        {
            // ガウシアンカーネルのσ = radius / 2.5 で99%カバレッジ
            float sigma = math.max(1f, blurRadius / 2.5f);
            float twoSigmaSq = 2f * sigma * sigma;

            for (int x = 0; x < resolution; x++)
            {
                float weightSum = 0f;
                float valueSum = 0f;

                for (int dx = -blurRadius; dx <= blurRadius; dx++)
                {
                    int nx = math.clamp(x + dx, 0, resolution - 1);
                    float w = math.exp(-(dx * dx) / twoSigmaSq);
                    valueSum += heights[y * resolution + nx] * w;
                    weightSum += w;
                }

                blurTemp[y * resolution + x] = valueSum / weightSum;
            }
        }
    }

    /// <summary>
    /// 高さマップの分離ガウシアンブラー（垂直パス）。
    /// 水平パスの結果を読み、最終的なブラー済み高さをheightsに書き戻す。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct HeightBlurVerticalJob : IJobParallelFor
    {
        public int resolution;
        public int blurRadius;

        [ReadOnly] public NativeArray<float> blurTemp;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> heights;

        // 列単位で並列実行
        public void Execute(int x)
        {
            float sigma = math.max(1f, blurRadius / 2.5f);
            float twoSigmaSq = 2f * sigma * sigma;

            for (int y = 0; y < resolution; y++)
            {
                float weightSum = 0f;
                float valueSum = 0f;

                for (int dy = -blurRadius; dy <= blurRadius; dy++)
                {
                    int ny = math.clamp(y + dy, 0, resolution - 1);
                    float w = math.exp(-(dy * dy) / twoSigmaSq);
                    valueSum += blurTemp[ny * resolution + x] * w;
                    weightSum += w;
                }

                heights[y * resolution + x] = valueSum / weightSum;
            }
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// 砂浜に近い陸側だけを3x3平均で1回なだらかにする。
    /// 複雑な地形再整形はせず、海岸線近傍の段差だけを局所的にぼかす。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct CoastalSmoothJob : IJobParallelFor
    {
        public int resolution;

        [ReadOnly] public NativeArray<float> landMask;
        [ReadOnly] public NativeArray<float> coastalSmoothFactor;
        [ReadOnly] public NativeArray<float> inputHeights;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> outputHeights;

        public void Execute(int idx)
        {
            if (landMask[idx] < 0.5f || coastalSmoothFactor[idx] <= 0.001f)
            {
                outputHeights[idx] = inputHeights[idx];
                return;
            }

            int x = idx % resolution;
            int y = idx / resolution;
            float sum = 0f;
            float weight = 0f;

            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = math.clamp(y + dy, 0, resolution - 1);
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = math.clamp(x + dx, 0, resolution - 1);
                    int nIdx = ny * resolution + nx;
                    float w = (dx == 0 && dy == 0) ? 2f : 1f;
                    sum += inputHeights[nIdx] * w;
                    weight += w;
                }
            }

            float averaged = sum / weight;
            float factor = coastalSmoothFactor[idx];
            outputHeights[idx] = math.lerp(inputHeights[idx], averaged, factor);
        }
    }
}

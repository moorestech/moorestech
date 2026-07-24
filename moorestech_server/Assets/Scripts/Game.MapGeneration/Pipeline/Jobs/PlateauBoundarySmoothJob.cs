using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// Job 2d-post-2: 台地内部スパイク除去。
    /// 同一領域のピクセルだけでボックスブラーし、局所平均からの逸脱が大きいスパイクのみ除去する。
    /// 台地外側の遷移はPlateauBoundaryRefineJobが担当するため、ここでは内部のみ処理する。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct PlateauBoundarySmoothJob : IJobParallelFor
    {
        public int resolution;
        public int kernelRadius;
        public float spikeThreshold;

        [ReadOnly] public NativeArray<int> regionLabels;
        [ReadOnly] public NativeArray<float> inputHeights;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> outputHeights;

        public void Execute(int idx)
        {
            int regionId = regionLabels[idx];
            if (regionId <= 0)
            {
                outputHeights[idx] = inputHeights[idx];
                return;
            }

            int x = idx % resolution;
            int y = idx / resolution;

            // 台地内部: 同一領域のピクセルだけでスパイク除去ブラー
            float sum = 0f;
            int sameCount = 0;
            for (int dy = -kernelRadius; dy <= kernelRadius; dy++)
            {
                for (int dx = -kernelRadius; dx <= kernelRadius; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                        continue;
                    int ni = ny * resolution + nx;
                    if (regionLabels[ni] == regionId)
                    {
                        sum += inputHeights[ni];
                        sameCount++;
                    }
                }
            }

            if (sameCount <= 1)
            {
                outputHeights[idx] = inputHeights[idx];
                return;
            }

            float localAvg = sum / sameCount;
            float deviation = math.abs(inputHeights[idx] - localAvg);
            float blend = math.saturate(deviation / spikeThreshold);
            outputHeights[idx] = math.lerp(inputHeights[idx], localAvg, blend);
        }
    }
}

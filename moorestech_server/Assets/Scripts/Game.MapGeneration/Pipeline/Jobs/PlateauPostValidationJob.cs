using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// Job 2e: 台地化後のカバー率を事後検証し、基準未達の領域をロールバックする。
    /// Flatten適用後のheightsでカバー率を再計算し、minCoverageRatio未満なら
    /// heightsをバックアップから復元してregionLabelsを0にクリアする。
    /// </summary>
    [BurstCompile]
    public struct PlateauPostValidationJob : IJob
    {
        public int resolution;
        public float minCoverageRatio;
        public float coverageTolerance;

        [ReadOnly] public NativeArray<PlateauRegionInfo> regionInfos;
        [ReadOnly] public NativeArray<int> regionCount;
        // 台地化前の高さバックアップ
        [ReadOnly] public NativeArray<float> heightsBackup;

        public NativeArray<int> regionLabels;
        public NativeArray<float> heights;

        public void Execute()
        {
            int pixelCount = resolution * resolution;
            int nRegions = regionCount[0];

            for (int r = 0; r < nRegions; r++)
            {
                int regionId = r + 1;
                float target = regionInfos[r].targetHeight;

                // 台地化後のカバー率を計算
                int regionSize = 0;
                int nearCount = 0;
                for (int i = 0; i < pixelCount; i++)
                {
                    if (regionLabels[i] != regionId) continue;
                    regionSize++;
                    if (math.abs(heights[i] - target) <= coverageTolerance)
                        nearCount++;
                }

                if (regionSize == 0) continue;
                float coverage = (float)nearCount / regionSize;

                if (coverage >= minCoverageRatio) continue;

                // カバー率不足: ロールバック
                for (int i = 0; i < pixelCount; i++)
                {
                    if (regionLabels[i] != regionId) continue;
                    heights[i] = heightsBackup[i];
                    regionLabels[i] = 0;
                }
            }
        }
    }
}

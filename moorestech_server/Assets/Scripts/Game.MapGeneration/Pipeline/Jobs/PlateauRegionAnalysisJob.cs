using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// Job 2c: plateauMaskの連結領域をFlood Fillで検出し、領域ごとのメタデータを算出する。
    /// 最小サイズ・カバー率で不適格領域をフィルタし、受理領域のみにラベルを付与する。
    /// 領域レベルの情報（目標高度等）はregionInfosに格納し、全ジョブで共有する。
    /// </summary>
    [BurstCompile]
    public struct PlateauRegionAnalysisJob : IJob
    {
        public int resolution;
        public int minRegionSize;
        public float minCoverageRatio;
        public float coverageTolerance;

        [ReadOnly] public NativeArray<float> plateauMask;
        [ReadOnly] public NativeArray<float> heights;

        // 各ピクセルの所属領域ID（0=台地外、1〜=受理領域）
        public NativeArray<int> regionLabels;
        // 領域ごとの共通メタデータ（regionLabels値-1でインデックス）
        public NativeArray<PlateauRegionInfo> regionInfos;
        // 受理された領域数
        public NativeArray<int> regionCount;

        public void Execute()
        {
            int pixelCount = resolution * resolution;

            for (int i = 0; i < pixelCount; i++)
                regionLabels[i] = -1; // 未訪問

            var stack = new NativeList<int>(1024, Allocator.Temp);
            var regionPixels = new NativeList<int>(1024, Allocator.Temp);

            int acceptedRegions = 0;
            int maxRegions = regionInfos.Length;

            for (int i = 0; i < pixelCount; i++)
            {
                if (plateauMask[i] <= 0f)
                {
                    regionLabels[i] = 0;
                    continue;
                }
                if (regionLabels[i] != -1)
                    continue;

                // --- Flood Fill: 1つの連結領域を探索 ---
                stack.Clear();
                regionPixels.Clear();
                stack.Add(i);
                regionLabels[i] = 0; // 訪問済み仮マーク

                float boundaryHeightSum = 0f;
                int boundaryCount = 0;

                while (stack.Length > 0)
                {
                    int idx = stack[stack.Length - 1];
                    stack.RemoveAtSwapBack(stack.Length - 1);
                    regionPixels.Add(idx);

                    int x = idx % resolution;
                    int y = idx / resolution;

                    // 8近傍に非候補ピクセルがあれば境界
                    bool isBoundary = false;
                    for (int dy = -1; dy <= 1 && !isBoundary; dy++)
                    {
                        for (int dx = -1; dx <= 1 && !isBoundary; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                            { isBoundary = true; break; }
                            if (plateauMask[ny * resolution + nx] <= 0f)
                                isBoundary = true;
                        }
                    }

                    if (isBoundary)
                    {
                        boundaryHeightSum += heights[idx];
                        boundaryCount++;
                    }

                    // 8方向に隣接する候補ピクセルを同一領域に追加
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < resolution && ny >= 0 && ny < resolution)
                                TryEnqueue(ny * resolution + nx, stack);
                        }
                    }
                }

                // --- 領域レベルの判定: サイズ・カバー率で受理/棄却 ---
                int regionSize = regionPixels.Length;
                if (regionSize < minRegionSize)
                {
                    // 小さすぎる領域は棄却
                    for (int j = 0; j < regionSize; j++)
                        regionLabels[regionPixels[j]] = 0;
                    continue;
                }

                float avgH = boundaryCount > 0
                    ? boundaryHeightSum / boundaryCount
                    : heights[i];

                // カバー率チェックは台地化後に事後検証するため、ここでは行わない

                // --- 仮受理: ラベル付与とメタデータ格納 ---
                if (acceptedRegions >= maxRegions) continue;
                acceptedRegions++;
                int regionId = acceptedRegions; // 1-based

                for (int j = 0; j < regionSize; j++)
                    regionLabels[regionPixels[j]] = regionId;

                regionInfos[regionId - 1] = new PlateauRegionInfo
                {
                    targetHeight = avgH,
                    pixelCount = regionSize,
                    boundaryCount = boundaryCount
                };
            }

            regionCount[0] = acceptedRegions;

            stack.Dispose();
            regionPixels.Dispose();
        }

        void TryEnqueue(int idx, NativeList<int> stack)
        {
            if (plateauMask[idx] > 0f && regionLabels[idx] == -1)
            {
                regionLabels[idx] = 0;
                stack.Add(idx);
            }
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// ClassificationJob直後に実行し、小さな海領域を陸に変換する。
    /// 2値化された landMask 上でflood-fillし、minRegionSize未満の海を
    /// 周囲の多数派バイオームで埋める。ビーチはこの後に別ジョブで生成する。
    /// </summary>
    [BurstCompile]
    public struct SmallSeaRemovalJob : IJob
    {
        public int resolution;
        public int minRegionSize;
        // 段2局所窓用: 窓端に接触する海領域は除去しない（本番では大海なのに窓クリップで小海誤判定するのを防ぐ）
        public bool protectEdgeRegions;

        public NativeArray<float> shoreMask;
        public NativeArray<float> landMask;
        public NativeArray<int> rawBiomeIndex;

        public void Execute()
        {
            int pixelCount = resolution * resolution;

            var visited = new NativeArray<byte>(pixelCount, Allocator.Temp);
            var stack = new NativeList<int>(1024, Allocator.Temp);
            var regionPixels = new NativeList<int>(1024, Allocator.Temp);

            for (int i = 0; i < pixelCount; i++)
            {
                // 陸または訪問済みはスキップ
                if (landMask[i] > 0.5f) continue;
                if (visited[i] != 0) continue;

                stack.Clear();
                regionPixels.Clear();
                stack.Add(i);
                visited[i] = 1;

                // 境界の陸バイオームを多数決で決定
                var biomeCounts = new NativeArray<int>(16, Allocator.Temp);
                int maxBiome = -1;
                int maxCount = 0;
                bool touchesEdge = false;

                while (stack.Length > 0)
                {
                    int idx = stack[stack.Length - 1];
                    stack.RemoveAtSwapBack(stack.Length - 1);
                    regionPixels.Add(idx);

                    int x = idx % resolution;
                    int y = idx / resolution;

                    if (x == 0 || x == resolution - 1 || y == 0 || y == resolution - 1)
                        touchesEdge = true;

                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x, ny = y;
                        switch (d)
                        {
                            case 0: nx = x - 1; break;
                            case 1: nx = x + 1; break;
                            case 2: ny = y - 1; break;
                            case 3: ny = y + 1; break;
                        }

                        if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                            continue;

                        int nIdx = ny * resolution + nx;

                        if (landMask[nIdx] < 0.5f)
                        {
                            if (visited[nIdx] == 0)
                            {
                                visited[nIdx] = 1;
                                stack.Add(nIdx);
                            }
                        }
                        else
                        {
                            int biome = rawBiomeIndex[nIdx];
                            if (biome >= 0 && biome < 16)
                            {
                                biomeCounts[biome]++;
                                if (biomeCounts[biome] > maxCount)
                                {
                                    maxCount = biomeCounts[biome];
                                    maxBiome = biome;
                                }
                            }
                        }
                    }
                }

                // 小さな海領域 → 陸に変換（ただしprotectEdgeRegions時に窓端接触領域は保護）
                if (regionPixels.Length < minRegionSize && maxBiome >= 0
                    && !(protectEdgeRegions && touchesEdge))
                {
                    for (int p = 0; p < regionPixels.Length; p++)
                    {
                        int idx = regionPixels[p];
                        shoreMask[idx] = 1f;
                        landMask[idx] = 1f;
                        rawBiomeIndex[idx] = maxBiome;
                    }
                }

                biomeCounts.Dispose();
            }

            visited.Dispose();
            stack.Dispose();
            regionPixels.Dispose();
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// Job 2d: 受理された台地領域を実際にフラット化する。
    /// regionLabelsで自ピクセルの所属領域を特定し、regionInfosから目標高度を取得。
    /// 境界からの距離に応じたsmoothstep遷移で、急な段差を防ぐ。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct PlateauFlattenJob : IJobParallelFor
    {
        public int resolution;
        public float baseTransition;
        public float transitionScale;
        // プラトー境界ピクセルのブレンド係数（BiomeBoundaryConfig由来）
        public float boundaryBlend;

        [ReadOnly] public NativeArray<int> regionLabels;
        [ReadOnly] public NativeArray<PlateauRegionInfo> regionInfos;
        [ReadOnly] public NativeArray<float> plateauMask;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> heights;

        public void Execute(int idx)
        {
            int regionId = regionLabels[idx];
            if (regionId <= 0) return;

            float target = regionInfos[regionId - 1].targetHeight;
            int x = idx % resolution;
            int y = idx / resolution;

            // 8近傍で境界判定し、targetに向かって50%ブレンドしてリムの芯を除去。
            // 4近傍だと斜め境界のリムが残るため8近傍で広く捕捉する
            bool isBoundary = false;
            for (int dy = -1; dy <= 1 && !isBoundary; dy++)
            {
                for (int dx = -1; dx <= 1 && !isBoundary; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                    { isBoundary = true; break; }
                    if (regionLabels[ny * resolution + nx] != regionId)
                        isBoundary = true;
                }
            }
            if (isBoundary)
            {
                heights[idx] = math.lerp(heights[idx], target, boundaryBlend);
                return;
            }

            // 8方向レイマーチで最近接の「同一領域の境界ピクセル」を探す
            float minDist = 9999f;
            float nearBndH = heights[idx];

            for (int d = 0; d < 8; d++)
            {
                int dx = 0, dy = 0;
                switch (d)
                {
                    case 0: dx = 1; dy = 0; break;
                    case 1: dx = 1; dy = 1; break;
                    case 2: dx = 0; dy = 1; break;
                    case 3: dx = -1; dy = 1; break;
                    case 4: dx = -1; dy = 0; break;
                    case 5: dx = -1; dy = -1; break;
                    case 6: dx = 0; dy = -1; break;
                    case 7: dx = 1; dy = -1; break;
                }
                float stepLen = (dx != 0 && dy != 0) ? 1.41421356f : 1f;

                for (int step = 1; step <= 128; step++)
                {
                    int sx = x + dx * step;
                    int sy = y + dy * step;
                    if (sx < 0 || sx >= resolution || sy < 0 || sy >= resolution)
                        break;

                    int si = sy * resolution + sx;
                    // 同一領域の外に出たら、1つ手前が境界
                    if (regionLabels[si] != regionId)
                    {
                        float dist = (step - 1) * stepLen;
                        if (dist < minDist)
                        {
                            minDist = dist;
                            int bx = x + dx * (step - 1);
                            int by = y + dy * (step - 1);
                            nearBndH = heights[by * resolution + bx];
                        }
                        break;
                    }
                }
            }

            float heightDiff = math.abs(nearBndH - target);
            float transWidth = baseTransition + heightDiff * transitionScale;
            float t = math.saturate(minDist / math.max(transWidth, 0.01f));
            t = t * t * (3f - 2f * t);

            heights[idx] = math.lerp(nearBndH, target, t);
        }
    }
}

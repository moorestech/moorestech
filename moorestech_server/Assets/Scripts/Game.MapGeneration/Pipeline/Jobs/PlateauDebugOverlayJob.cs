using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// デバッグ用: 台地候補をスプラットマップで可視化する。
    /// 近傍ピクセルの同一領域比率でフェードし、ピクセル境界のギザギザを防ぐ。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct PlateauDebugOverlayJob : IJobParallelFor
    {
        public int resolution;
        public int totalLayers;
        public int baseLayerIndex;
        public int debugLayerStart;
        public int debugLayerCount;
        // フェード判定の近傍半径(px)
        public int fadeRadius;

        [ReadOnly] public NativeArray<float> plateauMask;
        [ReadOnly] public NativeArray<int> regionLabels;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> splatWeights;

        public void Execute(int idx)
        {
            int regionId = regionLabels[idx];

            if (regionId > 0 && debugLayerCount > 0)
            {
                int x = idx % resolution;
                int y = idx / resolution;

                // fadeRadius 内の同一領域比率で滑らかなフェードを算出
                int sameCount = 0;
                int totalCount = 0;
                for (int dy = -fadeRadius; dy <= fadeRadius; dy++)
                {
                    for (int dx = -fadeRadius; dx <= fadeRadius; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                            continue;
                        totalCount++;
                        if (regionLabels[ny * resolution + nx] == regionId)
                            sameCount++;
                    }
                }

                float alpha = (float)sameCount / totalCount;
                // 低比率はフェード中、高比率はほぼ不透明
                alpha = alpha * alpha;
                if (alpha < 0.01f) return;

                int baseIdx = idx * totalLayers;
                int layer = debugLayerStart + ((regionId - 1) % debugLayerCount);
                if (layer < 0 || layer >= totalLayers) return;

                for (int l = 0; l < totalLayers; l++)
                {
                    float existing = splatWeights[baseIdx + l];
                    float target = (l == layer) ? 1f : 0f;
                    splatWeights[baseIdx + l] = math.lerp(existing, target, alpha);
                }
                return;
            }

            // 棄却候補: ベースレイヤーで表示
            if (plateauMask[idx] > 0f)
            {
                int baseIdx = idx * totalLayers;
                for (int l = 0; l < totalLayers; l++)
                    splatWeights[baseIdx + l] = 0f;
                if (baseLayerIndex >= 0 && baseLayerIndex < totalLayers)
                    splatWeights[baseIdx + baseLayerIndex] = 1f;
            }
        }
    }
}

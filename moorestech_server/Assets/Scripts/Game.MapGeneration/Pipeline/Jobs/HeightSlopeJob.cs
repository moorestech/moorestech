using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// ガウシアンブラー後のスロープ後処理。ポイントスキャッター方式で
    /// ランダムに選んだ地点を中心に半径n内を追加平滑化する。
    /// グリッドハッシュで決定論的にスロープ中心を配置し、Burst互換。
    /// blurTempにブラー済み高さのコピーを入れてから実行すること。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct HeightSlopeJob : IJobParallelFor
    {
        public int resolution;
        public int slopeRadius;
        // スロープ中心の配置確率（0=なし、1=全グリッドセルに配置）
        public float slopeDensity;
        // グリッドセルサイズ（ワールド単位）。小さい=密、大きい=疎
        public float slopeCellSize;
        // ブレンド強度（0=効果なし、1=フル適用）
        public float slopeBlendStrength;
        public float terrainWidth;
        public float terrainLength;

        [ReadOnly] public NativeArray<float> blurTemp;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> heights;

        // 行単位で並列実行
        public void Execute(int y)
        {
            if (slopeDensity <= 0f || slopeRadius <= 0)
            {
                for (int x = 0; x < resolution; x++)
                    heights[y * resolution + x] = blurTemp[y * resolution + x];
                return;
            }

            float sigma = math.max(1f, slopeRadius / 2.5f);
            float twoSigmaSq = 2f * sigma * sigma;
            float radiusSq = (float)(slopeRadius * slopeRadius);

            // ピクセル↔ワールド変換係数
            float pxPerWorldX = (resolution - 1) / terrainWidth;
            float pxPerWorldZ = (resolution - 1) / terrainLength;
            float pixelSize = terrainWidth / (resolution - 1);

            // スロープ半径をグリッドセル数に変換（検索範囲）
            // cellSizeが極小値（ノイズ周波数の誤流用等）だとgridSearchが爆発するため上限で防御
            float worldRadius = slopeRadius * pixelSize;
            float safeCellSize = math.max(slopeCellSize, worldRadius * 0.5f);
            int gridSearch = math.min((int)math.ceil(worldRadius / safeCellSize) + 1, 10);

            for (int x = 0; x < resolution; x++)
            {
                int idx = y * resolution + x;
                float original = blurTemp[idx];

                float worldX = (float)x / (resolution - 1) * terrainWidth;
                float worldZ = (float)y / (resolution - 1) * terrainLength;

                // このピクセルが属するグリッドセル
                int gridX = (int)math.floor(worldX / safeCellSize);
                int gridZ = (int)math.floor(worldZ / safeCellSize);

                // 近傍グリッドセルのスロープ中心から最大影響度を求める
                float maxInfluence = 0f;

                for (int gz = gridZ - gridSearch; gz <= gridZ + gridSearch; gz++)
                for (int gx = gridX - gridSearch; gx <= gridX + gridSearch; gx++)
                {
                    // ハッシュで配置確率を判定
                    if (Hash(gx * 7 + 13, gz * 11 + 37) >= slopeDensity)
                        continue;

                    // セル内のジッター位置がスロープ中心
                    float cx = (gx + Hash(gx + 1, gz + 2) * 0.8f + 0.1f) * safeCellSize;
                    float cz = (gz + Hash(gz + 3, gx + 4) * 0.8f + 0.1f) * safeCellSize;

                    // ピクセル空間での距離
                    float dx = (worldX - cx) * pxPerWorldX;
                    float dz = (worldZ - cz) * pxPerWorldZ;
                    float distSq = dx * dx + dz * dz;

                    if (distSq < radiusSq)
                    {
                        float influence = math.exp(-distSq / twoSigmaSq);
                        maxInfluence = math.max(maxInfluence, influence);
                    }
                }

                if (maxInfluence < 0.01f)
                {
                    heights[idx] = original;
                    continue;
                }

                // スロープ中心の影響圏内: ガウシアン重み付き近傍平均で平滑化
                float weightSum = 0f;
                float valueSum = 0f;

                for (int dy = -slopeRadius; dy <= slopeRadius; dy++)
                {
                    int ny = math.clamp(y + dy, 0, resolution - 1);
                    for (int dx2 = -slopeRadius; dx2 <= slopeRadius; dx2++)
                    {
                        int nx = math.clamp(x + dx2, 0, resolution - 1);
                        float w = math.exp(-(dx2 * dx2 + dy * dy) / twoSigmaSq);
                        valueSum += blurTemp[ny * resolution + nx] * w;
                        weightSum += w;
                    }
                }

                float smoothed = valueSum / weightSum;
                heights[idx] = math.lerp(original, smoothed, maxInfluence * slopeBlendStrength);
            }
        }

        // Voronoiハッシュとは異なる定数でスロープ配置専用ハッシュ
        static float Hash(int x, int z)
        {
            int h = x * 198491317 + z * 781068421;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }
}

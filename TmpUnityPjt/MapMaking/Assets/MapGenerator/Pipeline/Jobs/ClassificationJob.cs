using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapGenerator.Pipeline.Jobs
{
    /// <summary>
    /// Job 1a: Continentalness+Erosionで陸/海判定 + ボロノイ×四色定理でバイオーム分類。
    /// ジッタード格子上のボロノイセルに (gx+2*gz)%N の配色で均等にバイオームを配置する。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct ClassificationJob : IJobParallelFor
    {
        public int resolution;
        public float terrainWidth, terrainLength;
        public float worldOffsetX, worldOffsetZ;

        // Continentalness: 大陸/海洋の大構造を決定するノイズ
        public float continentalnessFrequency;
        public int continentalnessOctaves;
        public float continentalnessPersistence;
        public float landThreshold;

        // Erosion: 海岸線の複雑さを制御するノイズ
        public float erosionFrequency;
        public int erosionOctaves;
        public float erosionStrength;

        public float beachWidth;

        // ボロノイバイオーム配置パラメータ
        public float voronoiCellSize;
        public float voronoiJitter;
        public int biomeCount;
        public int seed;

        // 境界ドメインワープ: fBmで座標を歪めてボロノイ境界を有機的にする
        public int boundaryWarpOctaves;
        public float boundaryWarpStrength;
        public float boundaryWarpFrequency;

        [ReadOnly] public NativeArray<float2> continentalnessOffsets;
        [ReadOnly] public NativeArray<float2> erosionOffsets;
        // seed依存のバイオーム並び替え。四色定理の配色インデックスから実バイオームへ変換
        [ReadOnly] public NativeArray<int> biomePermutation;

        [WriteOnly] public NativeArray<float> shoreMask;
        [WriteOnly] public NativeArray<float> landMask;
        [WriteOnly] public NativeArray<float> beachFactor;
        [WriteOnly] public NativeArray<int> rawBiomeIndex;

        public void Execute(int idx)
        {
            int x = idx % resolution;
            int y = idx / resolution;
            float worldX = worldOffsetX + (float)x / (resolution - 1) * terrainWidth;
            float worldZ = worldOffsetZ + (float)y / (resolution - 1) * terrainLength;
            float2 pos = new float2(worldX, worldZ);

            // Continentalness: 大スケールfBmで大陸/海洋の大構造を決定
            float continentalness = BurstNoise.FBm(pos,
                continentalnessFrequency, continentalnessOffsets, 0,
                continentalnessPersistence, 2f, continentalnessOctaves);

            // Erosion: 中スケールfBmで海岸線の入り組み具合を制御
            float erosion = BurstNoise.FBm(pos,
                erosionFrequency, erosionOffsets, 0,
                0.5f, 2f, erosionOctaves);

            // landValue: 高い=内陸、低い=海洋。erosionが海岸線を侵食する
            float landValue = continentalness - erosion * erosionStrength;

            // 陸海を2値化。ビーチ遷移はSmallSeaRemoval後にBeachTransitionJobで生成する
            float land = landValue >= landThreshold ? 1f : 0f;

            shoreMask[idx] = land;
            landMask[idx] = land;
            beachFactor[idx] = 0f;

            // 海ピクセル → バイオーム分類不要
            if (land < 0.5f)
            {
                rawBiomeIndex[idx] = -1;
                return;
            }

            // ドメインワープ: fBmでワールド座標を歪めてボロノイ境界を有機的に
            float warpedX = worldX;
            float warpedZ = worldZ;
            if (boundaryWarpOctaves > 0)
            {
                float seedF = (float)(seed % 10000);
                float warpX = 0f, warpZ = 0f;
                float freq = boundaryWarpFrequency;
                float amp = boundaryWarpStrength;
                for (int oct = 0; oct < boundaryWarpOctaves; oct++)
                {
                    warpX += noise.snoise(new float2(worldX * freq + seedF, worldZ * freq)) * amp;
                    warpZ += noise.snoise(new float2(worldX * freq, worldZ * freq + seedF + 1000f)) * amp;
                    freq *= 2f;
                    amp *= 0.5f;
                }
                warpedX += warpX;
                warpedZ += warpZ;
            }

            // ボロノイ分類: ワープ後座標でジッタード格子の最近傍セルを探索
            float cellX = warpedX / voronoiCellSize;
            float cellZ = warpedZ / voronoiCellSize;
            int baseCellX = (int)math.floor(cellX);
            int baseCellZ = (int)math.floor(cellZ);

            int bestGX = baseCellX;
            int bestGZ = baseCellZ;
            float bestDist = float.MaxValue;

            // 探索範囲: ジッターが大きいとseedがセル境界を越えるため広げる
            int searchR = (int)math.ceil(voronoiJitter * 0.5f + 0.5f);
            searchR = math.max(searchR, 1);

            for (int dz = -searchR; dz <= searchR; dz++)
            {
                for (int dx = -searchR; dx <= searchR; dx++)
                {
                    int gx = baseCellX + dx;
                    int gz = baseCellZ + dz;

                    // 決定論的ハッシュジッター: jitter>1でseedがセル境界を越え不規則なセル形状を生む
                    float2 jitter = CellJitter(gx, gz) * voronoiJitter;
                    float2 seedPos = new float2(gx + 0.5f + jitter.x, gz + 0.5f + jitter.y);

                    float dist = math.distancesq(new float2(cellX, cellZ), seedPos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestGX = gx;
                        bestGZ = gz;
                    }
                }
            }

            // 四色定理: (gx + 2*gz) mod N で全8近傍が異なるバイオームに
            // N≧4 のとき隣接セルの差分 {±1, ±2, ±3} はいずれも N の倍数でないため衝突しない
            int N = biomeCount;
            int rawColor = Mod(bestGX + 2 * bestGZ, N);
            rawBiomeIndex[idx] = biomePermutation[rawColor];
        }

        /// <summary>
        /// 整数格子座標から決定論的ジッター(-0.5〜0.5)を返す。
        /// </summary>
        float2 CellJitter(int gx, int gz)
        {
            uint h = CellHash((uint)seed, gx, gz);
            float jx = (float)(h & 0xFFFF) / 65535f - 0.5f;
            float jy = (float)((h >> 16) & 0xFFFF) / 65535f - 0.5f;
            return new float2(jx, jy);
        }

        /// <summary>
        /// 整数座標から高品質ハッシュを生成。ボロノイセル位置の再現性を保証する。
        /// </summary>
        static uint CellHash(uint seed, int x, int z)
        {
            uint h = seed;
            h ^= (uint)x * 0x9E3779B9u;
            h ^= (uint)z * 0x517CC1B7u;
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }

        /// <summary>
        /// 負の値にも対応する正のモジュロ演算。
        /// </summary>
        static int Mod(int a, int n)
        {
            int r = a % n;
            return r < 0 ? r + n : r;
        }
    }
}

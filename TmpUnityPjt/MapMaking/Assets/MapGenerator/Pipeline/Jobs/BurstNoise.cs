using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace MapGenerator.Pipeline.Jobs
{
    /// <summary>
    /// Burst対応のノイズ関数群。NoiseSamplerの全機能をsnoise(simplex)ベースで再実装。
    /// Mathf.PerlinNoise → noise.snoise 置換により Burst SIMD 最適化が効く。
    /// </summary>
    // ジョブ内からのみ呼ばれるためクラスレベルの[BurstCompile]は不要。
    // Direct Call生成で float2 値渡しが BC1067 エラーを起こすため除去。
    public static class BurstNoise
    {
        /// <summary>
        /// fBm (fractional Brownian motion)。snoise出力を[0,1]にリマップして返す。
        /// NoiseSampler.SampleFBm相当。パイプライン全体のベースノイズ。
        /// </summary>
        public static float FBm(float2 pos, float frequency,
            in NativeArray<float2> offsets, int offsetBase,
            float persistence, float lacunarity, int octaves)
        {
            float value = 0f;
            float amplitude = 1f;
            float freq = frequency;
            float maxAmp = 0f;

            for (int o = 0; o < octaves && (offsetBase + o) < offsets.Length; o++)
            {
                // snoise[-1,1]を[0,1]にリマップしてPerlinNoise互換にする
                float2 p = pos * freq + offsets[offsetBase + o];
                float n = (noise.snoise(p) + 1f) * 0.5f;
                value += n * amplitude;
                maxAmp += amplitude;
                // 振幅を縮小・周波数を拡大し、次オクターブで細部を追加
                amplitude *= persistence;
                freq *= lacunarity;
            }

            // 積算振幅で正規化して[0,1]に収める
            return maxAmp > 0f ? value / maxAmp : 0f;
        }

        /// <summary>
        /// fBm生値（-1〜1）。ドメインワープの変位算出に使用。
        /// NoiseSampler.SampleFBmRaw相当。
        /// </summary>
        public static float FBmRaw(float2 pos, float frequency,
            in NativeArray<float2> offsets, int offsetBase,
            float persistence, float lacunarity, int octaves)
        {
            float value = 0f;
            float amplitude = 1f;
            float freq = frequency;
            float maxAmp = 0f;

            for (int o = 0; o < octaves && (offsetBase + o) < offsets.Length; o++)
            {
                float2 p = pos * freq + offsets[offsetBase + o];
                float n = (noise.snoise(p) + 1f) * 0.5f;
                value += n * amplitude;
                maxAmp += amplitude;
                amplitude *= persistence;
                freq *= lacunarity;
            }

            // [0,1]を0.5中心の[-1,1]に変換
            return maxAmp > 0f ? (value / maxAmp - 0.5f) * 2f : 0f;
        }

        /// <summary>
        /// abs()のスムーズ版。k=0で通常のabs、k>0でゼロ交差の折り目を丸める。
        /// 渓谷カービングやリッジノイズののたうち回るアーティファクトを抑制する。
        /// </summary>
        public static float SmoothAbs(float x, float k)
        {
            return math.sqrt(x * x + k * k);
        }

        /// <summary>
        /// Ridged multifractal noise。ゼロ交差点に鋭い稜線を生成する。
        /// NoiseSampler.SampleRidged相当。Alpine/Grasslandのリッジに使用。
        /// smoothness>0 でゼロ交差の折り目を丸め、線状アーティファクトを抑制。
        /// </summary>
        public static float Ridged(float2 pos, float frequency,
            in NativeArray<float2> offsets, int offsetBase,
            float lacunarity, int octaves, float ridgeOffset, float gain,
            float smoothness = 0f)
        {
            float value = 0f;
            float freq = frequency;
            float amp = 1f;
            float weight = 1f;
            float persistence = 0.5f;

            for (int o = 0; o < octaves && (offsetBase + o) < offsets.Length; o++)
            {
                float2 p = pos * freq + offsets[offsetBase + o];
                float n = (noise.snoise(p) + 1f) * 0.5f;
                // abs折り返しで稜線を形成し、weight連鎖で谷間を抑制
                float signal = ridgeOffset - SmoothAbs((n - 0.5f) * 2f, smoothness);
                signal *= signal;
                signal *= weight;
                weight = math.saturate(signal * gain);
                value += signal * amp;
                freq *= lacunarity;
                amp *= persistence;
            }

            return math.min(1f, value * 0.5f);
        }

        /// <summary>
        /// Worley (cellular) ノイズ。3x3近傍走査で最近傍距離を返す。
        /// NoiseSampler.SampleWorley相当。
        /// </summary>
        public static float Worley(float2 pos, float frequency, float2 offset)
        {
            float2 p = (pos + offset) * frequency;
            int2 cell = (int2)math.floor(p);

            float minDist = float.MaxValue;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int cx = cell.x + dx;
                    int cz = cell.y + dz;
                    // 整数セル座標からハッシュで擬似ランダム点を配置
                    float fx = cx + Hash(cx, cz) * 0.99f;
                    float fz = cz + Hash(cz, cx) * 0.99f;
                    float dist = (p.x - fx) * (p.x - fx) + (p.y - fz) * (p.y - fz);
                    minDist = math.min(minDist, dist);
                }
            }
            return math.saturate(math.sqrt(minDist));
        }

        // Worley用の整数座標ハッシュ。セル内の特徴点位置を決定する
        static float Hash(int x, int z)
        {
            int h = x * 374761393 + z * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        /// <summary>
        /// Voronoiセル探索。最近傍と次近傍のセル座標・距離を返す。
        /// Jungle段差地形でセル単位の段レベル割り当てに使用。
        /// </summary>
        public static void VoronoiCell(float2 pos, float frequency, float2 offset,
            out int2 nearestCell, out int2 secondCell, out float dist1, out float dist2)
        {
            float2 p = (pos + offset) * frequency;
            int2 cell = (int2)math.floor(p);

            dist1 = float.MaxValue;
            dist2 = float.MaxValue;
            nearestCell = cell;
            secondCell = cell;

            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int cx = cell.x + dx;
                int cz = cell.y + dz;
                float fx = cx + Hash(cx, cz) * 0.99f;
                float fz = cz + Hash(cz, cx) * 0.99f;
                float d = (p.x - fx) * (p.x - fx) + (p.y - fz) * (p.y - fz);

                if (d < dist1)
                {
                    dist2 = dist1; secondCell = nearestCell;
                    dist1 = d; nearestCell = new int2(cx, cz);
                }
                else if (d < dist2)
                {
                    dist2 = d; secondCell = new int2(cx, cz);
                }
            }

            dist1 = math.sqrt(dist1);
            dist2 = math.sqrt(dist2);
        }

        /// <summary>
        /// VoronoiCell拡張版。セル中心座標も返す。
        /// 境界方向の計算（スロープ配置）に必要。
        /// </summary>
        public static void VoronoiCellEx(float2 pos, float frequency, float2 offset,
            out int2 nearestCell, out int2 secondCell,
            out float dist1, out float dist2,
            out float2 nearestCenter, out float2 secondCenter)
        {
            float2 p = (pos + offset) * frequency;
            int2 cell = (int2)math.floor(p);

            dist1 = float.MaxValue;
            dist2 = float.MaxValue;
            nearestCell = cell;
            secondCell = cell;
            nearestCenter = p;
            secondCenter = p;

            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int cx = cell.x + dx;
                int cz = cell.y + dz;
                float fx = cx + Hash(cx, cz) * 0.99f;
                float fz = cz + Hash(cz, cx) * 0.99f;
                float d = (p.x - fx) * (p.x - fx) + (p.y - fz) * (p.y - fz);

                if (d < dist1)
                {
                    dist2 = dist1; secondCell = nearestCell; secondCenter = nearestCenter;
                    dist1 = d; nearestCell = new int2(cx, cz); nearestCenter = new float2(fx, fz);
                }
                else if (d < dist2)
                {
                    dist2 = d; secondCell = new int2(cx, cz); secondCenter = new float2(fx, fz);
                }
            }

            dist1 = math.sqrt(dist1);
            dist2 = math.sqrt(dist2);
        }

        /// <summary>
        /// セル座標から高さオフセット（-0.5〜0.5）を返す。
        /// Voronoi特徴点ハッシュとは異なる定数で相関を回避。
        /// </summary>
        public static float CellHeightHash(int2 cell)
        {
            int h = cell.x * 1136930381 + cell.y * 852591037;
            h = (h ^ (h >> 13)) * 1521134295;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF - 0.5f;
        }

        /// <summary>
        /// 四色定理に基づく隣接セル異段保証。(cx + cz*2) % steps で
        /// king-graph（8方向隣接）上の有効な彩色を決定論的に生成する。
        /// steps≥4 が前提条件。Voronoiセルのグリッド座標がそのまま入力。
        /// </summary>
        public static int CellToStep(int2 cellCoord, int steps)
        {
            // 負の座標に対応: stepsの倍数を加えてから剰余
            int cx = ((cellCoord.x % steps) + steps) % steps;
            int cz = ((cellCoord.y % steps) + steps) % steps;
            return (cx + cz * 2) % steps;
        }

        /// <summary>
        /// ドメインワープFBM。低周波fBmで座標を歪ませてから再サンプリング。
        /// NoiseSampler.SampleWormFBM相当。
        /// </summary>
        public static float WormFBm(float2 pos, float frequency,
            in NativeArray<float2> offsets, int offsetBase, int octaves, float wormStrength)
        {
            // 低周波fBmでX/Z方向の変位量を算出（ワーム状の歪み）
            float warpX = FBm(pos, frequency * 0.5f, offsets, offsetBase, 0.5f, 2f, 3);
            float warpZ = FBm(pos, frequency * 0.5f, offsets, offsetBase + 2, 0.5f, 2f, 3);
            // 変位で座標を歪ませた後、通常のfBmでサンプリング
            float2 warped = pos + new float2(warpX, warpZ) * wormStrength * 200f;
            return FBm(warped, frequency, offsets, offsetBase, 0.5f, 2f, octaves);
        }

        /// <summary>
        /// 高さを離散段に量子化し段丘を生成。NoiseSampler.Terrace相当。
        /// </summary>
        public static float Terrace(float height, int steps, float sharpness)
        {
            float scaled = height * steps;
            float level = math.floor(scaled);
            float frac = scaled - level;
            // sharpnessが高いほどsmoothstep寄りになり段差が明瞭化
            float t = math.smoothstep(0f, 1f, frac);
            float terraced = (level + math.lerp(frac, t, sharpness)) / steps;
            return math.saturate(terraced);
        }

        /// <summary>
        /// abs-noise minで谷ネットワークを生成。NoiseSampler.ValleyNetwork相当。
        /// depthパラメータでカービング深度を制御。
        /// </summary>
        public static float ValleyNetwork(float2 pos, float frequency,
            int octaves, float depth,
            in NativeArray<float2> offsets, int offsetBase,
            float persistence, float lacunarity, float valleySharpness)
        {
            float valley = 1f;
            float amp = 1f;
            float freq = frequency;
            for (int i = 0; i < octaves; i++)
            {
                // 各オクターブのabs値の最小を追跡して谷筋を形成
                float n = math.abs(FBmRaw(pos, freq, offsets, offsetBase + i, 0.5f, 2f, 1));
                valley = math.min(valley, n / amp);
                amp *= 0.5f;
                freq *= 2f;
            }
            // NoiseSampler.ValleyNetwork互換: 1f - valley * depth
            float powVal = math.pow(math.min(1f, valley * 2f), valleySharpness);
            return 1f - powVal * depth;
        }

        /// <summary>
        /// NoiseType列挙値に応じたディスパッチ。SplatmapFilterのノイズ変調で使用。
        /// </summary>
        public static float SampleByType(int noiseType, float2 pos, float frequency,
            in NativeArray<float2> offsets, int offsetBase)
        {
            // 0=None, 1=WormFBM, 2=Worley, 3=Simple, 4=FBM
            switch (noiseType)
            {
                case 1: return WormFBm(pos, frequency, offsets, offsetBase, 4, 1f);
                case 2: return Worley(pos, frequency, offsets[offsetBase]);
                case 3: return FBm(pos, frequency, offsets, offsetBase, 0.5f, 2f, 1);
                case 4: return FBm(pos, frequency, offsets, offsetBase, 0.5f, 2f, 4);
                default: return 1f;
            }
        }
    }
}

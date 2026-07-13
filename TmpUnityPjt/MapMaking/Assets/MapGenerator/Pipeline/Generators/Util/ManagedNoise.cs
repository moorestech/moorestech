using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Pipeline.Generators.Util
{
    /// <summary>
    /// マネージドコードで使うノイズ関数の最小セット。
    /// ステージ3-5の配置処理(TreePlacement等)がBurst不可のためMathf.PerlinNoiseベースで残す。
    /// ハイトマップ/スプラットマップ生成はBurstNoise側に移行済み。
    /// </summary>
    public static class ManagedNoise
    {
        /// <summary>
        /// RNGからノイズオフセットを生成する。配置処理のノイズ用。
        /// </summary>
        public static Vector2[] GenerateOffsets(System.Random rng, int count)
        {
            var offsets = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = new Vector2(
                    (float)rng.NextDouble() * 10000f,
                    (float)rng.NextDouble() * 10000f
                );
            }
            return offsets;
        }

        /// <summary>
        /// fBm (fractional Brownian motion)。0-1正規化して返す。
        /// </summary>
        public static float SampleFBm(float worldX, float worldZ, float frequency,
            Vector2[] offsets, float persistence, float lacunarity, int octaves)
        {
            float value = 0f, amplitude = 1f, freq = frequency, maxAmp = 0f;
            for (int o = 0; o < octaves && o < offsets.Length; o++)
            {
                float sx = worldX * freq + offsets[o].x;
                float sy = worldZ * freq + offsets[o].y;
                value += Mathf.PerlinNoise(sx, sy) * amplitude;
                maxAmp += amplitude;
                amplitude *= persistence;
                freq *= lacunarity;
            }
            return maxAmp > 0f ? value / maxAmp : 0f;
        }

        /// <summary>
        /// オフセット開始位置を指定できるFBmオーバーロード。
        /// </summary>
        public static float SampleFBm(float worldX, float worldZ, float frequency,
            Vector2[] offsets, int offsetStart, float persistence, float lacunarity, int octaves)
        {
            float value = 0f, amplitude = 1f, freq = frequency, maxAmp = 0f;
            for (int o = 0; o < octaves && (offsetStart + o) < offsets.Length; o++)
            {
                float sx = worldX * freq + offsets[offsetStart + o].x;
                float sy = worldZ * freq + offsets[offsetStart + o].y;
                value += Mathf.PerlinNoise(sx, sy) * amplitude;
                maxAmp += amplitude;
                amplitude *= persistence;
                freq *= lacunarity;
            }
            return maxAmp > 0f ? value / maxAmp : 0f;
        }

        /// <summary>
        /// Worley (cellular) ノイズ。最近傍距離を0-1で返す。
        /// </summary>
        public static float SampleWorley(float worldX, float worldZ, float frequency, Vector2[] offsets)
        {
            float x = (worldX + offsets[0].x) * frequency;
            float z = (worldZ + offsets[0].y) * frequency;
            int xi = Mathf.FloorToInt(x);
            int zi = Mathf.FloorToInt(z);

            float minDist = float.MaxValue;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int cx = xi + dx, cz = zi + dz;
                float fx = cx + Hash(cx, cz) * 0.99f;
                float fz = cz + Hash(cz, cx) * 0.99f;
                float dist = (x - fx) * (x - fx) + (z - fz) * (z - fz);
                if (dist < minDist) minDist = dist;
            }
            return Mathf.Clamp01(Mathf.Sqrt(minDist));
        }

        static float Hash(int x, int z)
        {
            int h = x * 374761393 + z * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        /// <summary>
        /// ドメインワープFBM。座標を歪ませてからfBmサンプリング。
        /// </summary>
        public static float SampleWormFBM(float worldX, float worldZ, float frequency,
            float amplitude, Vector2[] offsets)
        {
            float warpX = SampleFBm(worldX, worldZ, frequency * 0.5f, offsets, 0, 0.5f, 2f, 3);
            float warpZ = SampleFBm(worldX, worldZ, frequency * 0.5f, offsets, 2, 0.5f, 2f, 3);
            float warpedX = worldX + warpX * amplitude * 200f;
            float warpedZ = worldZ + warpZ * amplitude * 200f;
            return SampleFBm(warpedX, warpedZ, frequency, offsets, 0, 0.5f, 2f, 4);
        }

        /// <summary>
        /// MapNoiseTypeに応じたディスパッチ。配置処理のノイズ変調で使用。
        /// </summary>
        public static float SampleByType(MapNoiseType type, float worldX, float worldZ,
            float frequency, Vector2[] offsets)
        {
            switch (type)
            {
                case MapNoiseType.WormFBM:
                    return SampleWormFBM(worldX, worldZ, frequency, 1f, offsets);
                case MapNoiseType.Worley:
                    return SampleWorley(worldX, worldZ, frequency, offsets);
                case MapNoiseType.Simple:
                    return SampleFBm(worldX, worldZ, frequency, offsets, 0, 0.5f, 2f, 1);
                case MapNoiseType.FBM:
                    return SampleFBm(worldX, worldZ, frequency, offsets, 0, 0.5f, 2f, 4);
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// PlacementNoise設定からノイズ値をサンプリング。
        /// offset/balance/テクスチャノイズに対応。
        /// </summary>
        public static float SamplePlacementNoise(PlacementNoise noise,
            float worldX, float worldZ, Vector2[] offsets,
            float terrainWidth = 0f, float terrainLength = 0f)
        {
            if (noise.noiseType == MapNoiseType.None && noise.texture == null)
                return 1f;

            float value;

            // テクスチャが指定されていればテクスチャからサンプリング
            if (noise.texture != null)
            {
                float u = terrainWidth > 0f ? worldX / terrainWidth : 0f;
                float v = terrainLength > 0f ? worldZ / terrainLength : 0f;
                Color pixel = noise.texture.GetPixelBilinear(u, v);
                value = SampleTextureChannel(pixel, noise.channel);
            }
            else
            {
                value = SampleByType(noise.noiseType, worldX, worldZ, noise.frequency, offsets);
            }

            // offset: 出力を上下シフト、balance: 中心をずらす
            return (value + noise.offset + noise.balance) * noise.amplitude;
        }

        /// <summary>
        /// 2層ノイズをNoiseOpで合成する。
        /// </summary>
        public static float CombineNoise(float a, float b, NoiseOp op)
        {
            switch (op)
            {
                case NoiseOp.Add:      return a + b;
                case NoiseOp.Subtract: return a - b;
                case NoiseOp.Multiply: return a * b;
                case NoiseOp.Overlay:  return a < 0.5f ? 2f * a * b : 1f - 2f * (1f - a) * (1f - b);
                case NoiseOp.Min:      return Mathf.Min(a, b);
                case NoiseOp.Max:      return Mathf.Max(a, b);
                default:               return a;
            }
        }

        static float SampleTextureChannel(Color pixel, TextureChannel channel)
        {
            switch (channel)
            {
                case TextureChannel.R: return pixel.r;
                case TextureChannel.G: return pixel.g;
                case TextureChannel.B: return pixel.b;
                case TextureChannel.A: return pixel.a;
                default: return pixel.r;
            }
        }
    }
}

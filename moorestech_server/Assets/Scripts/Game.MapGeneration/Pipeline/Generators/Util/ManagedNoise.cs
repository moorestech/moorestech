using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators.Util
{
    // マネージド配置処理で使うノイズ関数の最小セット（Mathf.PerlinNoise ベース）。
    // texture ソースはスキーマ化で削除済みのため、テクスチャ経路は移植しない。
    // Minimal managed noise set (Mathf.PerlinNoise based) for placement; the texture noise
    // source was removed by schema migration so the texture path is not ported.
    public static class ManagedNoise
    {
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

        public static float SampleWormFBM(float worldX, float worldZ, float frequency,
            float amplitude, Vector2[] offsets)
        {
            float warpX = SampleFBm(worldX, worldZ, frequency * 0.5f, offsets, 0, 0.5f, 2f, 3);
            float warpZ = SampleFBm(worldX, worldZ, frequency * 0.5f, offsets, 2, 0.5f, 2f, 3);
            float warpedX = worldX + warpX * amplitude * 200f;
            float warpedZ = worldZ + warpZ * amplitude * 200f;
            return SampleFBm(warpedX, warpedZ, frequency, offsets, 0, 0.5f, 2f, 4);
        }

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

        // PlacementNoise 設定からノイズ値をサンプリング（offset/balance 適用）。
        // Sample a noise value from PlacementNoise settings (applying offset/balance).
        public static float SamplePlacementNoise(PlacementNoise noise,
            float worldX, float worldZ, Vector2[] offsets,
            float terrainWidth, float terrainLength)
        {
            if (noise.noiseType == MapNoiseType.None)
                return 1f;

            float value = SampleByType(noise.noiseType, worldX, worldZ, noise.frequency, offsets);
            return (value + noise.offset + noise.balance) * noise.amplitude;
        }

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
    }
}

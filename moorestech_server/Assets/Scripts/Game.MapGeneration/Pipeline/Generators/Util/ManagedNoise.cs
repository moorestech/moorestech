using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators.Util
{
    // マネージド配置処理で使うノイズ関数の最小セット（Mathf.PerlinNoise ベース）。
    // PlacementNoise はテクスチャを源に取れるので、その読み出しもここに置く。
    // Minimal managed noise set (Mathf.PerlinNoise based) for placement; since PlacementNoise can
    // take a texture as its source, reading that texture lives here too.
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

        // PlacementNoise 設定からノイズ値をサンプリング（offset/balance/テクスチャ源に対応）。
        // Sample a noise value from PlacementNoise settings (offset/balance and the texture source).
        public static float SamplePlacementNoise(PlacementNoise noise,
            float worldX, float worldZ, Vector2[] offsets,
            float gridOriginX, float gridOriginZ, float gridWidth, float gridLength)
        {
            if (!noise.IsActive)
                return 1f;

            // テクスチャが展開済みならノイズ関数より優先し、格子全体を 0-1 に正規化した UV で読む。
            // タイル1枚で割ると2枚目以降の worldX が UV=1 を超え、残りのタイルが最右列テクセルの定数で塗られる。
            // An expanded texture wins over the noise functions and is read through UV normalized over the whole grid.
            // Dividing by one tile would push every later tile past UV=1 and paint them with the rightmost texel's constant.
            float value;
            if (noise.UsesTexture)
            {
                float u = 0f < gridWidth ? (worldX - gridOriginX) / gridWidth : 0f;
                float v = 0f < gridLength ? (worldZ - gridOriginZ) / gridLength : 0f;
                value = SampleTextureChannel(GetPixelBilinear(noise, u, v), noise.channel);
            }
            else
            {
                value = SampleByType(noise.noiseType, worldX, worldZ, noise.frequency, offsets);
            }

            // offset: 出力を上下シフト、balance: 中心をずらす
            // offset shifts the output up/down, balance moves the center
            return (value + noise.offset + noise.balance) * noise.amplitude;
        }

        // UnityEngine.Texture2D に依存しないバイリニア。GPU と同じテクセル中心基準 (uv*size-0.5) で4近傍を
        // 加重平均し、端は Clamp。移植元が呼ぶ Texture2D.GetPixelBilinear は原点を uv*size に取るため
        // 半テクセルずれるが、そちらは UV=0.5 が隣のテクセル中心に一致してしまい補間が効かない規約なので採らない。
        // Texture2D-free bilinear on the GPU's texel-center basis (uv*size-0.5), clamping at the border.
        // The source's Texture2D.GetPixelBilinear puts the origin at uv*size, half a texel away; that rule is
        // not adopted because it lands UV=0.5 exactly on a texel center and stops interpolating there.
        static Color GetPixelBilinear(PlacementNoise noise, float u, float v)
        {
            float px = u * noise.textureWidth - 0.5f;
            float py = v * noise.textureHeight - 0.5f;
            int x0 = Mathf.FloorToInt(px);
            int y0 = Mathf.FloorToInt(py);
            float tx = px - x0;
            float ty = py - y0;

            Color bottom = Color.Lerp(GetPixel(noise, x0, y0), GetPixel(noise, x0 + 1, y0), tx);
            Color top = Color.Lerp(GetPixel(noise, x0, y0 + 1), GetPixel(noise, x0 + 1, y0 + 1), tx);
            return Color.Lerp(bottom, top, ty);
        }

        // 画素は GetPixels32 と同じ左下始まりの行優先で並ぶ。
        // Pixels are laid out row-major from the bottom-left, exactly as GetPixels32 returns them.
        static Color GetPixel(PlacementNoise noise, int x, int y)
        {
            int cx = Mathf.Clamp(x, 0, noise.textureWidth - 1);
            int cy = Mathf.Clamp(y, 0, noise.textureHeight - 1);
            return noise.texturePixels[cy * noise.textureWidth + cx];
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

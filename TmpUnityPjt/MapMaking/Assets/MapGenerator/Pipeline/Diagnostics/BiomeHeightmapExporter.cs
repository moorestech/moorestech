using System.IO;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MapGenerator.Pipeline.Diagnostics
{
    /// <summary>
    /// 単一バイオームのSampleHeightを全ピクセルに適用し、グレースケールPNGとして出力する。
    /// フルパイプライン（大陸/海洋分類・バイオーム補間）を経由しない、素のノイズパターン確認用。
    /// BurstBiomeSampler経由でDOTSパスと同一の高さを取得する。
    /// </summary>
    public static class BiomeHeightmapExporter
    {
        const int DefaultResolution = 512;
        const string ExportDir = "Assets/MapGenerator/Export";

        /// <summary>
        /// 指定バイオームのハイトマップをPNGとして書き出す。
        /// configのseed・terrainWidth/Lengthを使ってワールド座標を算出し、
        /// BurstBiomeSamplerでバイオーム固有の高さを生成する。
        /// </summary>
        public static string Export(BiomeType biomeType, TerrainGenerationConfig config,
            int resolution = DefaultResolution)
        {
            // JobDataConverterで単一バイオームのパラメータを構築
            var biomeParams = JobDataConverter.CreateSingleBiomeParams(config, biomeType);
            int offsetCount = JobDataConverter.GetNoiseOffsetCount(config, biomeType);

            // 既存パイプラインと同じRNG順序でノイズオフセットを生成
            var rng = new System.Random(config.seed);
            var offsets = new NativeArray<float2>(offsetCount, Allocator.Temp);
            for (int i = 0; i < offsetCount; i++)
            {
                offsets[i] = new float2(
                    (float)rng.NextDouble() * 10000f,
                    (float)rng.NextDouble() * 10000f);
            }

            // 全ピクセルにBurstBiomeSampler.Sampleを適用し、高さ値を収集
            var heights = new float[resolution * resolution];
            float maxHeight = float.MinValue;
            float minHeight = float.MaxValue;
            int biomeTypeInt = (int)biomeType;

            for (int y = 0; y < resolution; y++)
            {
                float worldZ = (float)y / resolution * config.terrainLength;
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = (float)x / resolution * config.terrainWidth;
                    // offsetBase=0: エクスポーターは単一バイオームなのでオフセット配列先頭から使用
                    float h = BurstBiomeSampler.Sample(biomeTypeInt,
                        new float2(worldX, worldZ), biomeParams, offsets, 0);
                    heights[y * resolution + x] = h;
                    if (h > maxHeight) maxHeight = h;
                    if (h < minHeight) minHeight = h;
                }
            }

            offsets.Dispose();

            // 0-1正規化してグレースケール画像に変換。8bit PNGのバンディングで
            // 偽の等高線が出やすいため、微小ディザでアーティファクトを抑える。
            float range = maxHeight - minHeight;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float normalized = range > 0.0001f
                        ? (heights[y * resolution + x] - minHeight) / range
                        : 0f;
                    float dither = (Hash01(x, y) - 0.5f) / 255f;
                    float v = math.saturate(normalized + dither);
                    texture.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }

            texture.Apply();

            // PNG書き出し
            if (!Directory.Exists(ExportDir))
                Directory.CreateDirectory(ExportDir);

            string fileName = $"Biome_{biomeType}.png";
            string path = Path.Combine(ExportDir, fileName);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            UnityEngine.Debug.Log(
                $"[BiomeHeightmapExporter] Exported {biomeType} heightmap ({resolution}x{resolution}) " +
                $"to {path} (height range: {minHeight:F4} - {maxHeight:F4})");

            return path;
        }

        static float Hash01(int x, int y)
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }
}

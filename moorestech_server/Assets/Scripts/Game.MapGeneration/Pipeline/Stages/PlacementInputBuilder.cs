using System.Collections.Generic;
using System.Threading.Tasks;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 配置ステージの入力整形ヘルパー（重み2D化・高さ2D化・biomeインデックス・結果変換）。
    // Input-shaping helpers for placement stages (weights-to-2D, heights-to-2D, biome indices, result convert).
    internal static class PlacementInputBuilder
    {
        // ジョブ出力の biomeWeights を旧形式(Ocean/Beach列付き)に変換する。配置処理が [idx,2+b] で参照するため。
        // Convert job-output biomeWeights to the legacy Ocean/Beach-column layout consumed by placement.
        public static float[,] BuildPlacementWeights(
            Unity.Collections.NativeArray<float> jobWeights,
            Unity.Collections.NativeArray<float> shoreMask,
            Unity.Collections.NativeArray<float> beachFactor,
            int res, int biomeCount, int totalCols)
        {
            int pixelCount = res * res;
            var result = new float[pixelCount, totalCols];
            int bc = biomeCount;

            Parallel.For(0, pixelCount, i =>
            {
                float shore = shoreMask[i];
                float beach = beachFactor[i];
                result[i, 0] = shore < 0.005f ? 1f : 0f;
                result[i, 1] = beach > 0.2f ? beach : 0f;

                float contentSum = 0f;
                for (int b = 0; b < bc; b++)
                {
                    float w = jobWeights[i * bc + b];
                    result[i, 2 + b] = w;
                    contentSum += w;
                }

                float oceanBeach = result[i, 0] + result[i, 1];
                if (oceanBeach > 0f && contentSum > 0f)
                {
                    float scale = Mathf.Max(0f, 1f - oceanBeach);
                    for (int b = 0; b < bc; b++)
                        result[i, 2 + b] *= scale / contentSum;
                }
            });

            return result;
        }

        // flat 高さ配列を [z,x] の2次元へ変換する（配置ジェネレーターの入力形式）。
        // Convert the flat height array to [z,x] 2D form (placement generator input format).
        public static float[,] ConvertHeights(float[] heights, int res)
        {
            var result = new float[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    result[z, x] = heights[z * res + x];
            return result;
        }

        // 陸海マスク＋winnerから各ピクセルの BiomeType 値(byte)を確定する。
        // Determine each pixel's BiomeType value (byte) from land/beach masks and the winner index.
        public static byte[] BuildBiomeIndices(
            Unity.Collections.NativeArray<int> winnerBiomeIndex,
            Unity.Collections.NativeArray<float> landMask,
            Unity.Collections.NativeArray<float> beachFactor,
            BiomeType[] biomeTypes, int pixelCount)
        {
            var result = new byte[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                if (landMask[i] <= 0.5f) { result[i] = (byte)BiomeType.Ocean; continue; }
                if (beachFactor[i] > 0.2f) { result[i] = (byte)BiomeType.Beach; continue; }
                int w = winnerBiomeIndex[i];
                result[i] = (w >= 0 && w < biomeTypes.Length) ? (byte)biomeTypes[w] : (byte)BiomeType.Grassland;
            }
            return result;
        }

        // PlacementEntry 群を距離チェック用の ObjectPlacementResult 群へ変換する。
        // Convert PlacementEntry items to ObjectPlacementResult items for distance checks.
        public static List<ObjectPlacementResult> ToObjectPlacements(List<PlacementEntry> entries)
        {
            var result = new List<ObjectPlacementResult>(entries.Count);
            foreach (var e in entries)
            {
                result.Add(new ObjectPlacementResult
                {
                    MapObjectGuid = e.MapObjectGuid,
                    Position = e.WorldPosition,
                    Rotation = e.Rotation,
                    Scale = e.Scale,
                    Sink = e.Sink,
                    ClusterInfo = e.Cluster ?? new RockClusterInfo { ClusterId = -1 }
                });
            }
            return result;
        }
    }
}

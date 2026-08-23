using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Splat
{
    /// <summary>
    ///     フラットなsplatWeightsをalphamap解像度の[z, x, layer]へ再サンプルする。
    ///     MapMaking TerrainApplier.ConvertSplatWeights の移植
    ///     Resamples the flat splatWeights into an alphamap-resolution [z, x, layer] array;
    ///     ported from MapMaking's TerrainApplier.ConvertSplatWeights
    /// </summary>
    public static class SplatWeightConverter
    {
        public static float[,,] ToAlphamap(
            NativeArray<float> splatWeights, int heightmapResolution, int alphamapResolution, int layerCount)
        {
            var alphamap = new float[alphamapResolution, alphamapResolution, layerCount];

            // Parallel.Forのクロージャが捕捉できるようローカルへ写す
            // Copy into locals so the Parallel.For closure can capture them
            var capturedWeights = splatWeights;
            var heightmapRes = heightmapResolution;
            var alphamapRes = alphamapResolution;
            var layers = layerCount;

            // 行単位で並列化。各ピクセルは独立で、splatWeightsは読み取り専用
            // Row-parallel; every pixel is independent and splatWeights is read-only
            Parallel.For(0, alphamapRes, z =>
            {
                for (var x = 0; x < alphamapRes; x++)
                {
                    // 高さマップ座標へ変換
                    // Convert to heightmap coordinates
                    var heightmapX = Mathf.Clamp(
                        Mathf.RoundToInt((float)x / (alphamapRes - 1) * (heightmapRes - 1)), 0, heightmapRes - 1);
                    var heightmapZ = Mathf.Clamp(
                        Mathf.RoundToInt((float)z / (alphamapRes - 1) * (heightmapRes - 1)), 0, heightmapRes - 1);
                    var sourceIndex = heightmapZ * heightmapRes + heightmapX;

                    for (var layer = 0; layer < layers; layer++)
                        alphamap[z, x, layer] = capturedWeights[sourceIndex * layers + layer];
                }
            });

            return alphamap;
        }
    }
}

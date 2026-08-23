using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Unity.Collections;
using Unity.Jobs;

namespace Game.MapGeneration.Pipeline.Visual
{
    /// <summary>
    ///     ハイトマップ全画素の傾斜角を求める。MapMaking TerrainGenerator.ComputeSlopes の移植で、
    ///     計算本体はサーバーと共通の SlopeComputeJob をそのまま使う
    ///     Computes the slope angle of every heightmap pixel; ported from MapMaking's TerrainGenerator.ComputeSlopes
    ///     with the math itself delegated to the server's own SlopeComputeJob
    /// </summary>
    public static class TerrainSlopeCalculator
    {
        private const int JobBatchSize = 64;

        // 入力・出力とも[z, x]。TerrainFileLoaderの並びとDetailSampleContextの参照規約に合わせる
        // Input and output are both [z, x], matching TerrainFileLoader's layout and DetailSampleContext's access convention
        public static float[,] Compute(float[,] heights, TerrainGenerationConfig config)
        {
            var resolution = config.Resolution;
            var pixelCount = resolution * resolution;

            var flatHeights = new NativeArray<float>(pixelCount, Allocator.TempJob);
            var flatSlopes = new NativeArray<float>(pixelCount, Allocator.TempJob);

            // 途中で例外が出ても必ず解放できる状態を保つ（Step2のSplatmapRuntimeGeneratorと同じ方針）
            // Keep everything releasable even if an exception escapes midway, as Step 2's SplatmapRuntimeGenerator does
            try
            {
                for (var z = 0; z < resolution; z++)
                for (var x = 0; x < resolution; x++)
                    flatHeights[z * resolution + x] = heights[z, x];

                new SlopeComputeJob
                {
                    resolution = resolution,
                    terrainWidth = config.terrainWidth,
                    terrainHeight = config.terrainHeight,
                    terrainLength = config.terrainLength,
                    heights = flatHeights,
                    slopes = flatSlopes,
                }.Schedule(pixelCount, JobBatchSize).Complete();

                var slopes = new float[resolution, resolution];
                for (var z = 0; z < resolution; z++)
                for (var x = 0; x < resolution; x++)
                    slopes[z, x] = flatSlopes[z * resolution + x];

                return slopes;
            }
            finally
            {
                flatSlopes.Dispose();
                flatHeights.Dispose();
            }
        }
    }
}

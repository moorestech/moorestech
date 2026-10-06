using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal sealed class SurfaceGenerationParameters : IDisposable
    {
        internal readonly NativeArray<BiomeParams> BiomeParams;
        internal readonly NativeArray<float2> NoiseOffsets;

        internal SurfaceGenerationParameters(TerrainGenerationConfig config, BiomeType[] biomes)
        {
            // 全タイルで同じ入力を共有し、窓の原点から分離する
            // Share identical inputs across tiles independently of window origins
            BiomeParams = JobDataConverter.ConvertBiomeParams(config, biomes, Allocator.TempJob);
            NoiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, BiomeParams, biomes, Allocator.TempJob);
        }

        public void Dispose()
        {
            NoiseOffsets.Dispose();
            BiomeParams.Dispose();
        }
    }
}

using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Tiling;
using Unity.Collections;

namespace Game.MapGeneration.Pipeline.Surface
{
    // 一時ジョブ配列の所有権を窓へ閉じる
    // Keep temporary job allocation ownership inside the window
    internal sealed class SurfaceGenerationWindow : IDisposable
    {
        internal JobBuffers Buffers;

        internal SurfaceGenerationWindow(TerrainGenerationConfig tileConfig, BiomeType[] biomes, SurfaceGenerationParameters parameters)
        {
            Buffers = JobDataConverter.AllocateBuffers(tileConfig.Resolution, biomes.Length, 1, Allocator.TempJob);
            Buffers.biomeParams = parameters.BiomeParams;
            Buffers.noiseOffsets = parameters.NoiseOffsets;
        }

        internal void Run(TerrainGenerationConfig tileConfig, BiomeType[] biomes,
            SurfaceBoundarySamples boundaries, int tileX, int tileZ)
        {
            PaddedWindowStage.Run(tileConfig, biomes, Buffers);

            // 独立窓の境界候補は所有者を通してから下流へ公開する
            // Publish independent window boundary candidates only through their canonical owner
            boundaries.CaptureOwned(tileX, tileZ, Buffers);
            boundaries.Emit(tileX, tileZ, Buffers);
        }

        public void Dispose()
        {
            // 格子で共有する入力は窓の解放対象から外す
            // Detach grid-wide inputs before releasing the window
            Buffers.biomeParams = default;
            Buffers.noiseOffsets = default;
            Buffers.Dispose();
        }
    }
}

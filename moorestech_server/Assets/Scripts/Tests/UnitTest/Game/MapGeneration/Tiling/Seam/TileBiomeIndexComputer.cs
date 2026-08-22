using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Tiling;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling.Seam
{
    // VanillaGenerator のタイルループを分類だけ独立に再現する共有テストヘルパー。転送されなくなった
    // biome_x_z.bin の代わりに、生成側と同じ公開API(PaddedWindowStage→PlacementInputBuilder.BuildBiomeIndices)を直接呼ぶ。
    // Shared test helper that re-runs VanillaGenerator's tile loop for classification only. In place of the
    // no-longer-transferred biome_x_z.bin, it calls the same public API the generator uses (PaddedWindowStage
    // then PlacementInputBuilder.BuildBiomeIndices) directly.
    public static class TileBiomeIndexComputer
    {
        // 格子全体のタイルぶんをまとめて計算する。biomeParams/noiseOffsets は格子で1組だけ作って使い回す
        // Computes every tile in the grid at once, sharing one set of biomeParams/noiseOffsets across all of them
        public static Dictionary<Vector2Int, byte[]> ComputeForAllTiles(TerrainGenerationConfig config, MapGenerationOutput output)
        {
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var gridConfig = BuildGridConfig(config, output);
            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            var noiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, biomeParams, biomeTypes, Allocator.TempJob);
            var result = new Dictionary<Vector2Int, byte[]>();
            try
            {
                foreach (var tile in output.Tiles)
                    result[new Vector2Int(tile.TileX, tile.TileZ)] =
                        ComputeTile(config, gridConfig, biomeTypes, biomeParams, noiseOffsets, tile.TileX, tile.TileZ);
            }
            finally
            {
                noiseOffsets.Dispose();
                biomeParams.Dispose();
            }

            return result;
        }

        // 1タイルだけを計算する。スポーン地点のような単発参照向け
        // Computes a single tile only, for one-off lookups such as a spawn point
        public static byte[] ComputeForTile(TerrainGenerationConfig config, MapGenerationOutput output, int tileIndexX, int tileIndexZ)
        {
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var gridConfig = BuildGridConfig(config, output);
            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            var noiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, biomeParams, biomeTypes, Allocator.TempJob);
            try
            {
                return ComputeTile(config, gridConfig, biomeTypes, biomeParams, noiseOffsets, tileIndexX, tileIndexZ);
            }
            finally
            {
                noiseOffsets.Dispose();
                biomeParams.Dispose();
            }
        }

        // タイル窓の基準はindex(0,0)タイル。output.NoiseOrigin がその原点そのもの（VanillaGenerator参照）
        // The tile windows are based on the index (0,0) tile; output.NoiseOrigin is exactly that origin (see VanillaGenerator)
        private static TerrainGenerationConfig BuildGridConfig(TerrainGenerationConfig config, MapGenerationOutput output)
        {
            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = output.NoiseOrigin.x;
            gridConfig.worldOffsetZ = output.NoiseOrigin.y;
            return gridConfig;
        }

        private static byte[] ComputeTile(
            TerrainGenerationConfig config, TerrainGenerationConfig gridConfig, BiomeType[] biomeTypes,
            NativeArray<BiomeParams> biomeParams, NativeArray<float2> noiseOffsets, int tileIndexX, int tileIndexZ)
        {
            var tileConfig = gridConfig.CreateTileConfig(tileIndexX, tileIndexZ);
            var buffers = JobDataConverter.AllocateBuffers(config.Resolution, biomeTypes.Length, 1, Allocator.TempJob);
            buffers.biomeParams = biomeParams;
            buffers.noiseOffsets = noiseOffsets;
            try
            {
                PaddedWindowStage.Run(tileConfig, biomeTypes, buffers);
                return PlacementInputBuilder.BuildBiomeIndices(
                    buffers.winnerBiomeIndex, buffers.landMask, buffers.beachFactor, biomeTypes, config.Resolution * config.Resolution);
            }
            finally
            {
                // 共有した2本を切り離してから破棄する。付けたままだと呼び出し側の破棄と二重解放になる
                // Detach the two shared arrays before disposing, otherwise the caller's dispose double-frees them
                buffers.biomeParams = default;
                buffers.noiseOffsets = default;
                buffers.Dispose();
            }
        }
    }
}

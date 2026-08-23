using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Tiling;
using Unity.Collections;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling.Seam
{
    // 本番の TileBiomeIndexBuilder を格子ぶん回すだけのアダプタ
    // Adapter that only drives the production TileBiomeIndexBuilder across the grid
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
                    result[new Vector2Int(tile.TileX, tile.TileZ)] = TileBiomeIndexBuilder.BuildForTile(
                        gridConfig.CreateTileConfig(tile.TileX, tile.TileZ), biomeTypes, biomeParams, noiseOffsets);
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
                return TileBiomeIndexBuilder.BuildForTile(
                    gridConfig.CreateTileConfig(tileIndexX, tileIndexZ), biomeTypes, biomeParams, noiseOffsets);
            }
            finally
            {
                noiseOffsets.Dispose();
                biomeParams.Dispose();
            }
        }

        // 基準はindex(0,0)。NoiseOriginが原点
        // The basis is index (0,0); NoiseOrigin is that origin
        private static TerrainGenerationConfig BuildGridConfig(TerrainGenerationConfig config, MapGenerationOutput output)
        {
            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = output.NoiseOrigin.x;
            gridConfig.worldOffsetZ = output.NoiseOrigin.y;
            return gridConfig;
        }
    }
}

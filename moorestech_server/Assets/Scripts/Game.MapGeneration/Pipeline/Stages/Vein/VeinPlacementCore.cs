using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Tiling;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 鉱脈種別に依存しない共通配置
    // Shared placement independent of vein type
    public static class VeinPlacementCore
    {
        internal static VeinPlacementBatch Generate(
            OreEntry[] entries, float borderMargin,
            TerrainGenerationConfig config, bool[][,] masks, BiomeType[] biomeTypes,
            float[,] heights2D, List<PlacementEntry> treeEntries, List<ObjectPlacementResult> objectPlacements,
            int rngSeedOffset, TilePlacementContext tile, VeinHaloChannels channels)
        {
            if (entries.Length == 0) return new VeinPlacementBatch();

            int biomeCount = biomeTypes.Length;
            int res = config.Resolution;

            var entryMasks = BuildEntryMasks(entries, masks, biomeTypes, biomeCount, res);

            var treeGrid = SpatialGrid.FromPlacements(treeEntries, config.terrainWidth, config.terrainLength, 0f);
            var objectGrid = ObjectsToGrid(objectPlacements, config);

            // 木と岩の近傍判定にも隣タイルぶんを入れる。鉱脈だけタイル内で閉じると境界の帯で距離が破られる。
            // The tree and rock neighbour tests take the adjacent tiles too; closing veins inside one tile breaks the distance in the seam band.
            tile.Halo.Trees.SeedGrid(treeGrid, config.worldOffsetX, config.worldOffsetZ,
                config.terrainWidth, config.terrainLength, tile.Halo.Radius);
            if (objectGrid != null)
                tile.Halo.Objects.SeedGrid(objectGrid, config.worldOffsetX, config.worldOffsetZ,
                    config.terrainWidth, config.terrainLength, tile.Halo.Radius);

            var dims = TerrainDimensions.From(config, 0f, tile.TileIndexX, tile.TileIndexZ);

            // 排他入力はタイル寸法から一意に決まるので、呼び出し元ではなくここで組む。
            // The exclusion input follows uniquely from the tile bounds, so it is built here rather than at each call site.
            var excludedVeins = tile.Halo.CreateConfirmedVeinSnapshot(TileCandidateAabbBounds.From(dims));

            var rng = new System.Random(TileSeedMixer.Mix(
                config.seed + rngSeedOffset, tile.TileIndexX, tile.TileIndexZ));
            // AABB排他はメンバー配置の内側で済んでいるので、返る配置がそのまま確定分になる
            // The AABB exclusion is settled inside member placement, so the returned placement is the confirmed set
            return OrePlacementGenerator.GenerateForWorld(
                entries, entryMasks, borderMargin, heights2D, dims, rng, treeGrid, objectGrid,
                channels, tile.Halo.Radius, excludedVeins);
        }

        static bool[][,] BuildEntryMasks(
            OreEntry[] entries, bool[][,] masks, BiomeType[] biomeTypes, int biomeCount, int res)
        {
            var entryMasks = new bool[entries.Length][,];
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.biomes == BiomeFlags.None) { entryMasks[i] = null; continue; }
                bool[,] union = null;
                for (int b = 0; b < biomeCount; b++)
                {
                    if (!entry.biomes.Includes(biomeTypes[b])) continue;
                    var m = masks[b];
                    if (union == null) union = new bool[res, res];
                    for (int z = 0; z < res; z++)
                        for (int x = 0; x < res; x++)
                            if (m[z, x]) union[z, x] = true;
                }
                entryMasks[i] = union;
            }
            return entryMasks;
        }

        static SpatialGrid ObjectsToGrid(List<ObjectPlacementResult> objects, TerrainGenerationConfig config)
        {
            if (objects == null || objects.Count == 0) return null;
            float cellSize = Mathf.Max(config.terrainWidth / 50f, 5f);
            var grid = new SpatialGrid(config.terrainWidth, config.terrainLength, cellSize);
            foreach (var obj in objects)
                grid.Add(obj.Position.x - config.worldOffsetX, obj.Position.z - config.worldOffsetZ);
            return grid;
        }
    }
}

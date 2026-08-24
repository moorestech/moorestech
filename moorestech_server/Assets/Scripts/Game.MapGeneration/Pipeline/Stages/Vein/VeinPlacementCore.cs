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
        internal static ConfirmedVeinPlacementBatch Generate(
            OreEntry[] entries, float borderMargin,
            TerrainGenerationConfig config, bool[][,] masks, BiomeType[] biomeTypes,
            float[,] heights2D, List<PlacementEntry> treeEntries, List<ObjectPlacementResult> objectPlacements,
            int rngSeedOffset, IReadOnlyList<PlacedVein> excludedVeins,
            TilePlacementContext tile, PlacementHaloChannel memberHalo, PlacementHaloChannelMap centerHalos)
        {
            if (entries.Length == 0) return new ConfirmedVeinPlacementBatch();

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
            var rng = new System.Random(TileSeedMixer.Mix(
                config.seed + rngSeedOffset, tile.TileIndexX, tile.TileIndexZ));
            var placement = OrePlacementGenerator.GenerateForWorld(
                entries, entryMasks, borderMargin, heights2D, dims, rng, treeGrid, objectGrid,
                memberHalo, centerHalos, tile.Halo.Radius);

            // 点ごとに固定サイズの鉱脈を生成。
            // Emit one fixed-size vein per point.
            return BuildVeins(placement);

            #region Internal

            ConfirmedVeinPlacementBatch BuildVeins(VeinPlacementBatch generated)
            {
                // 配置順をそのまま出力順にする
                // The placement order becomes the output order
                var confirmed = new ConfirmedVeinPlacementBatch();
                foreach (var generatedCluster in generated.Clusters)
                {
                    var confirmedCluster = new ConfirmedVeinCluster(
                        generatedCluster.VeinGuid, generatedCluster.WorldCenter);
                    foreach (var member in generatedCluster.Members)
                    {
                        var vein = VeinAabbBuilder.Build(member.MapObjectGuid, member.WorldPosition);
                        if (OverlapsExcludedVein(vein)) continue;

                        confirmed.Veins.Add(vein);
                        confirmedCluster.Members.Add(member);
                    }

                    // 全メンバーが除外された中心はhaloへcommitしない。
                    // A center whose members were all excluded never commits to the halo.
                    if (confirmedCluster.Members.Count != 0)
                        confirmed.Clusters.Add(confirmedCluster);
                }
                return confirmed;
            }

            bool OverlapsExcludedVein(PlacedVein candidate)
            {
                foreach (var excluded in excludedVeins)
                    if (candidate.Min.x <= excluded.Max.x && excluded.Min.x <= candidate.Max.x &&
                        candidate.Min.y <= excluded.Max.y && excluded.Min.y <= candidate.Max.y &&
                        candidate.Min.z <= excluded.Max.z && excluded.Min.z <= candidate.Max.z)
                        return true;
                return false;
            }

            #endregion
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

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
        public static List<PlacedVein> Generate(
            OreEntry[] entries, float borderMargin,
            TerrainGenerationConfig config, bool[][,] masks, BiomeType[] biomeTypes,
            float[,] heights2D, List<PlacementEntry> treeEntries, List<ObjectPlacementResult> objectPlacements,
            int rngSeedOffset, IReadOnlyList<PlacedVein> excludedVeins,
            TilePlacementContext tile, PlacementHaloChannel memberHalo, PlacementHaloChannel centerHalo)
        {
            var veins = new List<PlacedVein>();
            if (entries.Length == 0) return veins;

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
            var members = OrePlacementGenerator.GenerateForWorld(
                entries, entryMasks, borderMargin, heights2D, dims, rng, treeGrid, objectGrid,
                memberHalo, centerHalo, tile.Halo.Radius);

            // クラスター単位でメンバー座標の min/max を整数化し PlacedVein を1件生成する。
            // Snap each cluster's member coord min/max to integers and emit one PlacedVein per cluster.
            return BuildVeins(members, veins, excludedVeins);
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

        static List<PlacedVein> BuildVeins(
            List<PlacementEntry> members, List<PlacedVein> veins, IReadOnlyList<PlacedVein> excludedVeins)
        {
            // clusterId → (guid, min, max) の集約。順序はクラスター発見順（決定論・挿入順）を保つ。
            // Aggregate by clusterId → (guid, min, max); preserve cluster discovery (insertion) order for determinism.
            var order = new List<int>();
            var acc = new Dictionary<int, (string guid, Vector3Int min, Vector3Int max)>();
            foreach (var m in members)
            {
                if (m.Cluster == null) continue;
                int id = m.Cluster.Value.ClusterId;
                var p = new Vector3Int(
                    Mathf.RoundToInt(m.WorldPosition.x),
                    Mathf.RoundToInt(m.WorldPosition.y),
                    Mathf.RoundToInt(m.WorldPosition.z));
                if (!acc.TryGetValue(id, out var e))
                {
                    order.Add(id);
                    acc[id] = (m.MapObjectGuid, p, p);
                    continue;
                }
                e.min = Vector3Int.Min(e.min, p);
                e.max = Vector3Int.Max(e.max, p);
                acc[id] = e;
            }
            foreach (int id in order)
            {
                var e = acc[id];
                var vein = new PlacedVein { VeinGuid = e.guid, Min = e.min, Max = e.max };
                if (!OverlapsExcludedVein(vein)) veins.Add(vein);
            }
            return veins;

            #region Internal

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
    }
}

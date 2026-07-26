using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // ステージ6: ワールド全体の鉱脈配置と、クラスターごとの整数 AABB(PlacedVein) 変換。
    // 鉱石岩の見た目(PlacedMapObject)は出力しない（地表ビジュアルは露頭に一本化・ADR#10）。
    // Stage 6: world-wide vein placement and per-cluster integer AABB (PlacedVein) conversion.
    // No ore-rock visual (PlacedMapObject) is emitted; surface visuals unify on outcrops (ADR#10).
    public static class OrePlacementStage
    {
        public static List<PlacedVein> Generate(
            TerrainGenerationConfig config, bool[][,] masks, BiomeType[] biomeTypes,
            float[,] heights2D, List<PlacementEntry> treeEntries, List<ObjectPlacementResult> objectPlacements)
        {
            var veins = new List<PlacedVein>();
            var ore = config.oreConfig;
            if (!config.generateOre || ore?.entries == null || ore.entries.Length == 0) return veins;

            int biomeCount = biomeTypes.Length;
            int res = config.Resolution;
            var entries = ore.entries;

            // 各 OreEntry の対象バイオーム合成マスク（OR）を構築する。
            // Build the OR-composite biome mask for each OreEntry.
            var entryMasks = BuildEntryMasks(entries, masks, biomeTypes, biomeCount, res);

            var treeGrid = SpatialGrid.FromPlacements(treeEntries, config.terrainWidth, config.terrainLength, 0f);
            var objectGrid = ObjectsToGrid(objectPlacements, config);
            var dims = TerrainDimensions.From(config, 0f);
            var oreRng = new System.Random(config.seed + 7000);
            var members = OrePlacementGenerator.GenerateForWorld(
                entries, entryMasks, ore.borderMargin, heights2D, dims, oreRng, treeGrid, objectGrid);

            // クラスター単位でメンバー座標の min/max を整数化し PlacedVein を1件生成する。
            // Snap each cluster's member coord min/max to integers and emit one PlacedVein per cluster.
            return BuildVeins(members, veins);
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

        static List<PlacedVein> BuildVeins(List<PlacementEntry> members, List<PlacedVein> veins)
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
                veins.Add(new PlacedVein { VeinGuid = e.guid, Min = e.min, Max = e.max });
            }
            return veins;
        }
    }
}

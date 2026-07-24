using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Generators.Util;

namespace Game.MapGeneration.Pipeline.Stages
{
    // ステージ4/4.5: バイオーム別オブジェクト配置と、その周辺への追加樹木配置。
    // Stage 4/4.5: per-biome object placement plus extra trees seeded around those objects.
    public static class ObjectPlacementStage
    {
        // objectEntries を生成し、周辺樹木を treeEntries に追記する。objectPlacements は鉱脈距離チェック用。
        // Produce objectEntries and append around-object trees into treeEntries; objectPlacements feeds ore checks.
        public static void Generate(
            TerrainGenerationConfig config, BiomePlacementHelper helper, BiomeType[] biomeTypes,
            bool[][,] masks, float[] heights, float[,] heights2D, List<PlacementEntry> treeEntries,
            out List<PlacementEntry> objectEntries, out List<ObjectPlacementResult> objectPlacements)
        {
            int biomeCount = biomeTypes.Length;
            objectEntries = new List<PlacementEntry>();

            // Stage 3 樹木から距離チェック用グリッドを構築（オブジェクト配置が参照）。
            // Build the distance-check grid from stage-3 trees (consumed by object placement).
            var treeSpatialGrid = SpatialGrid.FromPlacements(treeEntries, config.terrainWidth, config.terrainLength, 0f);

            for (int b = 0; b < biomeCount; b++)
            {
                var oc = helper.GetObjectConfig(biomeTypes[b]);
                if (oc == null) continue;
                bool hasAny = (oc.entries?.Length > 0) || (oc.clusterEntries?.Length > 0);
                if (!hasAny) continue;

                float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                var dims = TerrainDimensions.From(config, wm);
                var objRng = new System.Random(config.seed + 4000 + b * 100);
                var entries = ObjectPlacementGenerator.GenerateForBiome(masks[b], heights2D, dims, oc, objRng, treeSpatialGrid);
                objectEntries.AddRange(entries);
            }

            // Stage 4.5: 岩周辺樹木。Stage3グリッドを再利用し岩周辺の木も距離チェック対象にする。
            // Stage 4.5: trees around rocks, reusing the stage-3 grid so those trees are distance-checked too.
            var rockTreeGrid = SpatialGrid.FromPlacements(treeEntries, config.terrainWidth, config.terrainLength, 3f);
            objectPlacements = PlacementInputBuilder.ToObjectPlacements(objectEntries);
            for (int b = 0; b < biomeCount; b++)
            {
                var tp = helper.GetTreePlacementConfig(biomeTypes[b]);
                if (tp?.prototypes == null || tp.prototypes.Length == 0) continue;

                float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                var dims = TerrainDimensions.From(config, wm);
                var rockRng = new System.Random(config.seed + 5000 + b * 100);
                var rockEntries = TreePlacementAroundObjects.GenerateAroundObjects(
                    masks[b], heights, dims, tp, objectPlacements, rockRng, rockTreeGrid);
                treeEntries.AddRange(rockEntries);
            }
        }
    }
}

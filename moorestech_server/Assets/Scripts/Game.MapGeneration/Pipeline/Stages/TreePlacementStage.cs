using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;

namespace Game.MapGeneration.Pipeline.Stages
{
    // ステージ3: バイオーム別の樹木配置。各バイオームの winner マスク上に TreePlacementGenerator を回す。
    // Stage 3: per-biome tree placement, running TreePlacementGenerator over each biome winner mask.
    public static class TreePlacementStage
    {
        public static void Generate(
            TerrainGenerationConfig config, BiomePlacementHelper helper, BiomeType[] biomeTypes,
            bool[][,] masks, float[] heights, List<PlacementEntry> treeEntries)
        {
            int biomeCount = biomeTypes.Length;
            for (int b = 0; b < biomeCount; b++)
            {
                var tp = helper.GetTreePlacementConfig(biomeTypes[b]);
                if (tp?.prototypes == null || tp.prototypes.Length == 0) continue;

                float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                var dims = TerrainDimensions.From(config, wm);
                var treeRng = new System.Random(config.seed + 3000 + b * 100);
                var entries = TreePlacementGenerator.GenerateForBiome(masks[b], heights, dims, tp, treeRng);
                treeEntries.AddRange(entries);
            }
        }
    }
}

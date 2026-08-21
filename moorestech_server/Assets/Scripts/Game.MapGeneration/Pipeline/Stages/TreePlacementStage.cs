using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Tiling;

namespace Game.MapGeneration.Pipeline.Stages
{
    // ステージ3: バイオーム別の樹木配置。各バイオームの winner マスク上に TreePlacementGenerator を回す。
    // Stage 3: per-biome tree placement, running TreePlacementGenerator over each biome winner mask.
    public static class TreePlacementStage
    {
        private const int TreeSeedBase = 3000;
        private const int TreeSeedStridePerBiome = 100;

        // 候補点の乱数種とノイズ場の種を分ける幅。ノイズ側にはタイルを混ぜないので同じ数から作ると衝突する。
        // The gap separating the candidate-point seed from the noise-field seed; the noise side omits the tile term, so one number cannot serve both.
        private const int NoiseSeedOffset = 50;

        public static void Generate(
            TerrainGenerationConfig config, BiomePlacementHelper helper, BiomeType[] biomeTypes,
            bool[][,] masks, float[] heights, List<PlacementEntry> treeEntries, TilePlacementContext tile)
        {
            int biomeCount = biomeTypes.Length;
            for (int b = 0; b < biomeCount; b++)
            {
                var tp = helper.GetTreePlacementConfig(biomeTypes[b]);
                if (tp?.prototypes == null || tp.prototypes.Length == 0) continue;

                float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                var dims = TerrainDimensions.From(config, wm, tile.TileIndexX, tile.TileIndexZ);
                int biomeSeed = config.seed + TreeSeedBase + b * TreeSeedStridePerBiome;

                // 候補点の抽選にはタイルを混ぜ、ノイズ場の種には混ぜない。混ぜると同じ地形の上で密度分布が
                // タイルごとに切り替わり、境目に直線が立つ。
                // The candidate draw mixes in the tile, the noise-field seed does not: mixing it would switch the
                // density distribution tile by tile over one continuous terrain and stand a straight line on the seam.
                var treeRng = new System.Random(TileSeedMixer.Mix(biomeSeed, tile.TileIndexX, tile.TileIndexZ));
                var entries = TreePlacementGenerator.GenerateForBiome(
                    masks[b], heights, dims, tp, treeRng, biomeSeed + NoiseSeedOffset,
                    tile.Halo.Trees, tile.Halo.Radius);
                treeEntries.AddRange(entries);
            }
        }
    }
}

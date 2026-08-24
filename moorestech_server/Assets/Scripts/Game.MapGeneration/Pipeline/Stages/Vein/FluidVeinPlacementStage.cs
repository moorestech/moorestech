using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Tiling;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 配置本体は共通コアへ委譲する
    // Delegate placement to the shared core
    public static class FluidVeinPlacementStage
    {
        private const int FluidVeinRngSeedOffset = 7500;

        public static List<PlacedVein> Generate(
            TerrainGenerationConfig config, bool[][,] masks, BiomeType[] biomeTypes,
            float[,] heights2D, List<PlacementEntry> treeEntries, List<ObjectPlacementResult> objectPlacements,
            TilePlacementContext tile)
        {
            var ore = config.oreConfig;
            if (!config.generateOre || ore.fluidEntries.Length == 0) return new List<PlacedVein>();

            // item鉱脈とは別の乱数列を使い、同一seedでも配置候補列を独立させる
            // Use a distinct random stream so item and fluid candidate sequences stay independent under the same seed
            var placement = VeinPlacementCore.Generate(
                ore.fluidEntries, ore.borderMargin,
                config, masks, biomeTypes, heights2D, treeEntries, objectPlacements,
                FluidVeinRngSeedOffset, tile.Halo.CreateConfirmedVeinSnapshot(
                    config.worldOffsetX, config.worldOffsetZ, config.terrainWidth, config.terrainLength),
                tile, tile.Halo.FluidVeinMembers, tile.Halo.FluidVeinCenters);
            tile.Halo.CommitFluidVeins(placement);
            return placement.Veins;
        }
    }
}

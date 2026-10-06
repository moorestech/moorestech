using Game.MapGeneration.Pipeline.Surface.Placement;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Generators;
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

        public static VeinPlacementBatch GenerateBatch(
            TerrainGenerationConfig config, bool[][,] masks, BiomeType[] biomeTypes,
            float[,] heights2D, List<PlacementEntry> treeEntries, List<ObjectPlacementResult> objectPlacements,
            TilePlacementContext tile, IVeinLandConstraint landConstraint)
        {
            var ore = config.oreConfig;
            if (!config.generateOre || ore.fluidEntries.Length == 0) return new VeinPlacementBatch();

            // item鉱脈とは別の乱数列を使い、同一seedでも配置候補列を独立させる
            // Use a distinct random stream so item and fluid candidate sequences stay independent under the same seed
            // seed先とcommit先へ同じ束を渡し、種別の取り違えを起こせなくする。
            // The same bundle goes to seeding and to committing, so the kinds cannot be mismatched.
            var channels = tile.Halo.FluidVeins;
            var placement = VeinPlacementCore.Generate(
                ore.fluidEntries, ore.borderMargin,
                config, masks, biomeTypes, heights2D, treeEntries, objectPlacements,
                FluidVeinRngSeedOffset, tile, channels, landConstraint);
            tile.Halo.CommitVeins(channels, placement);
            return placement;
        }
    }
}

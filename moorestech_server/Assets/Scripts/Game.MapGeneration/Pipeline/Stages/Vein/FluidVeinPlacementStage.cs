using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Stages
{
    // ステージ6: ワールド全体のfluid鉱脈配置。OreEntryと同形のfluidEntriesを消費し、
    // クラスタ配置本体はOrePlacementStageと共有のVeinPlacementCoreへ委譲する。
    // Stage 6: world-wide fluid vein placement. Consumes fluidEntries (same shape as OreEntry),
    // delegating cluster placement to VeinPlacementCore, shared with OrePlacementStage.
    public static class FluidVeinPlacementStage
    {
        public static List<PlacedVein> Generate(
            TerrainGenerationConfig config, bool[][,] masks, BiomeType[] biomeTypes,
            float[,] heights2D, List<PlacementEntry> treeEntries, List<ObjectPlacementResult> objectPlacements)
        {
            var ore = config.oreConfig;
            // item鉱脈(seed+7000)とrng列が重ならないよう別オフセットを使う。
            // Use a distinct offset so the rng stream never overlaps the item vein pass (seed+7000).
            return VeinPlacementCore.Generate(
                ore.fluidEntries, ore.borderMargin,
                config, masks, biomeTypes, heights2D, treeEntries, objectPlacements,
                rngSeedOffset: 7500);
        }
    }
}

using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Tiling;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 鉱石岩の見た目(PlacedMapObject)は出力しない（地表ビジュアルは露頭に一本化・ADR#10）。
    // No ore-rock visual (PlacedMapObject) is emitted; surface visuals unify on outcrops (ADR#10).
    public static class OrePlacementStage
    {
        private const int ItemVeinRngSeedOffset = 7000;

        public static List<PlacedVein> Generate(
            TerrainGenerationConfig config, bool[][,] masks, BiomeType[] biomeTypes,
            float[,] heights2D, List<PlacementEntry> treeEntries, List<ObjectPlacementResult> objectPlacements,
            TilePlacementContext tile)
        {
            var ore = config.oreConfig;
            if (!config.generateOre || ore.entries.Length == 0) return new List<PlacedVein>();
            // seed先とcommit先へ同じ束を渡し、種別の取り違えを起こせなくする。
            // The same bundle goes to seeding and to committing, so the kinds cannot be mismatched.
            var channels = tile.Halo.ItemVeins;
            var placement = VeinPlacementCore.Generate(
                ore.entries, ore.borderMargin,
                config, masks, biomeTypes, heights2D, treeEntries, objectPlacements,
                ItemVeinRngSeedOffset, tile, channels);
            tile.Halo.CommitVeins(channels, placement);
            return placement.Veins;
        }
    }
}

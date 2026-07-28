using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Stages
{
    // ステージ6: ワールド全体のitem鉱脈配置（クラスタ配置本体はVeinPlacementCoreへ集約）。
    // 鉱石岩の見た目(PlacedMapObject)は出力しない（地表ビジュアルは露頭に一本化・ADR#10）。
    // Stage 6: world-wide item vein placement (cluster placement itself lives in VeinPlacementCore).
    // No ore-rock visual (PlacedMapObject) is emitted; surface visuals unify on outcrops (ADR#10).
    public static class OrePlacementStage
    {
        public static List<PlacedVein> Generate(
            TerrainGenerationConfig config, bool[][,] masks, BiomeType[] biomeTypes,
            float[,] heights2D, List<PlacementEntry> treeEntries, List<ObjectPlacementResult> objectPlacements)
        {
            var ore = config.oreConfig;
            return VeinPlacementCore.Generate(
                ore.entries, ore.borderMargin,
                config, masks, biomeTypes, heights2D, treeEntries, objectPlacements,
                rngSeedOffset: 7000);
        }
    }
}

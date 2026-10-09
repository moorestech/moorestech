using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Surface;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    // 焼き手へ渡すpass-1成果を、台帳と表示高さ手順だけで組み立てる
    // Assemble the pass-1 result handed to the baker from just a ledger and a display-height policy
    internal static class LedgerRunFixture
    {
        internal static GenerationRun Legacy(PlacementLedger ledger)
        {
            var config = new TerrainGenerationConfig { surfaceRevision = WorldSurfaceRevision.Legacy4 };
            return new GenerationRun(new MapGenerationOutput(), ledger, config, new LegacySurfaceHeightPolicy());
        }

        internal static GenerationRun Grounded(PlacementLedger ledger, LandCellField land)
        {
            var config = new TerrainGenerationConfig { surfaceRevision = WorldSurfaceRevision.Grounded5 };
            return new GenerationRun(new MapGenerationOutput(), ledger, config,
                new GroundedSurfaceHeightPolicy(land, SurfaceEnvelope.GeneratedV5));
        }
    }
}

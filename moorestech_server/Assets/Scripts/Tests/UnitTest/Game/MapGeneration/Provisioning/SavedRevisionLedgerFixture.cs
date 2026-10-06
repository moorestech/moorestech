using Core.Master;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration.Provisioning
{
    internal static class SavedRevisionLedgerFixture
    {
        // 保存版と原点を渡し解決側と同じ台帳を再構築する
        // Reconstruct the resolver's ledger using the saved revision and origins
        internal static string ComputeDigest(WorldDataDirectory worldDataDirectory)
        {
            var meta = (GeneratedTerrainTransferMeta)TerrainTransferMetaReader.Read(worldDataDirectory);
            var generation = MasterHolder.GenerationMaster.SelectedGeneration;
            var config = MapGenerationPipeline.BuildConfigWithSettledOrigins(
                generation, meta.WorldSeed, TestModDirectory.ForUnitTestModDirectory,
                meta.GeneratedPayload.Origins, meta.GeneratedPayload.GeneratorVersion);
            return MapGenerationPipeline.Generate(generation, config).Ledger.ComputeDigest();
        }
    }
}

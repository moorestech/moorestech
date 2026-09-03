using System.IO;
using Core.Master;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Tests.Module;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration.Provisioning
{
    /// <summary>
    ///     world.jsonへ記録する台帳digestが、クライアントが辿る再生成経路のdigestと一致することを固定する。
    ///     TileVisualBakerは不一致をfail-closedの例外にするため、ここがずれると生成ワールドへ入れなくなる。
    ///     Pins that the ledger digest recorded into world.json equals the one the client's regeneration path produces.
    ///     TileVisualBaker turns a mismatch into a fail-closed exception, so a drift here locks the generated world out.
    /// </summary>
    public class PlacementLedgerDigestParityTest
    {
        private TerrainTransferTestScope _testScope;

        [SetUp]
        public void SetUp()
        {
            _testScope = new TerrainTransferTestScope(nameof(PlacementLedgerDigestParityTest));
        }

        [Test]
        public void 記録された台帳digestは原点注入経路の再生成digestと一致する()
        {
            var worldDataDirectory = _testScope.ProvisionGeneratedWorld(12345);
            var terrainMeta = (GeneratedTerrainTransferMeta)TerrainTransferMetaReader.Read(worldDataDirectory);
            var sharedCacheRoot = WorldDataDirectory.ForWorldCache(terrainMeta.WorldId).Root;

            try
            {
                // クライアント(WorldTerrainSession.Open)と同じ組み立てでpass-1を回す
                // Run pass-1 through the very assembly the client (WorldTerrainSession.Open) uses
                var selectedGeneration = MasterHolder.GenerationMaster.SelectedGeneration;
                var config = MapGenerationPipeline.BuildConfigWithSettledOrigins(
                    selectedGeneration, terrainMeta.WorldSeed, TestModDirectory.ForUnitTestModDirectory,
                    terrainMeta.GeneratedPayload.Origins);
                var regeneratedDigest = MapGenerationPipeline.Generate(selectedGeneration, config).Ledger.ComputeDigest();

                Assert.AreEqual(terrainMeta.GeneratedPayload.PlacementLedgerDigest, regeneratedDigest);
            }
            finally
            {
                if (Directory.Exists(sharedCacheRoot)) Directory.Delete(sharedCacheRoot, true);
                _testScope.End();
            }
        }
    }
}

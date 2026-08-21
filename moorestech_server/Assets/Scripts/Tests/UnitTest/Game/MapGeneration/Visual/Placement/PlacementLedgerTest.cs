using System.Linq;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Tiling;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Placement
{
    public class PlacementLedgerTest
    {
        // 同じconfigを2回回して台帳が完全一致する。クライアントがサーバーと同じ配置を再現する前提そのもの
        // Running the same config twice yields identical ledgers: the very premise of clients reproducing the server's placements
        [Test]
        public void SameConfigYieldsIdenticalLedger()
        {
            var config = MultiTileTestWorld.BuildConfig(2, 99);
            MultiTileTestWorld.EnableTrees(config);
            MultiTileTestWorld.EnableObjects(config);
            var first = new VanillaGenerator().Generate(config).Ledger;
            var second = new VanillaGenerator().Generate(config).Ledger;
            Assert.That(first.Placements.Count, Is.GreaterThan(0));
            Assert.That(first.Placements.Count, Is.EqualTo(second.Placements.Count));
            for (var i = 0; i < first.Placements.Count; i++)
            {
                Assert.That(second.Placements[i].Guid, Is.EqualTo(first.Placements[i].Guid), $"#{i}");
                Assert.That(second.Placements[i].ScenePosition, Is.EqualTo(first.Placements[i].ScenePosition), $"#{i}");
                Assert.That(second.Placements[i].ClusterId, Is.EqualTo(first.Placements[i].ClusterId), $"#{i}");
                Assert.That(second.Placements[i].SurroundEffect, Is.EqualTo(first.Placements[i].SurroundEffect), $"#{i}");
            }
        }

        // 台帳は出力の mapObject と1対1（同じ順・同じGUID・同じ位置）で、種別は配置元エントリの値
        // The ledger pairs one-to-one with the output's mapObjects (same order, guid, position) and carries the source entry's kind
        [Test]
        public void LedgerMirrorsMapObjectsAndCarriesKind()
        {
            var config = MultiTileTestWorld.BuildConfig(1, 7);
            MultiTileTestWorld.EnableTrees(config);
            MultiTileTestWorld.EnableObjects(config);

            // entries[1] が独立散布(IndependentMapObjectGuid)。entries[0] はクラスタ採番側(TestMapObjectGuid)で別GUIDのため
            // Entry index 1 is the independently scattered one (IndependentMapObjectGuid); index 0 is the cluster-numbered one with a different guid
            config.grassland.objectConfig.entries[1].terrainSurroundEffectType = TerrainSurroundEffectType.rockBareGround;
            var output = new VanillaGenerator().Generate(config);
            var ledger = output.Ledger;
            Assert.That(ledger.Placements.Count, Is.EqualTo(output.MapObjects.Count));
            for (var i = 0; i < ledger.Placements.Count; i++)
            {
                Assert.That(ledger.Placements[i].Guid, Is.EqualTo(output.MapObjects[i].MapObjectGuid));
                Assert.That(ledger.Placements[i].ScenePosition, Is.EqualTo(output.MapObjects[i].Position));
            }
            Assert.That(ledger.Placements.Any(p => p.Guid == MultiTileTestWorld.IndependentMapObjectGuid
                                                   && p.SurroundEffect == TerrainSurroundEffectType.rockBareGround), Is.True);
        }
    }
}

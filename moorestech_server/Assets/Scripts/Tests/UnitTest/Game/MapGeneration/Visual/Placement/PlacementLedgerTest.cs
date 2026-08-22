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
                Assert.That(second.Placements[i].Cluster, Is.EqualTo(first.Placements[i].Cluster), $"#{i}");
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

            // entries[0]=クラスタ採番、entries[1]=独立散布（別GUID）
            // entries[0] is cluster-numbered, entries[1] is independently scattered (different guid)
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

        // 木のterrainSurroundEffectTypeが台帳へ転記される。既定値(treeRootPatch)以外を指定し、
        // 転記が抜けて既定値のまま残る退行をゼロ以外の期待値で検出できるようにする
        // A tree's terrainSurroundEffectType is transcribed to the ledger. A non-default value is used
        // so a dropped transcription (silently left at the default treeRootPatch) shows up as a mismatch
        [Test]
        public void TreePrototypeSurroundEffectIsTranscribedToLedger()
        {
            var config = MultiTileTestWorld.BuildConfig(1, 11);
            MultiTileTestWorld.EnableTrees(config);
            config.grassland.treePlacement.prototypes[0].terrainSurroundEffectType = TerrainSurroundEffectType.rockNoBareGround;
            config.forest.treePlacement.prototypes[0].terrainSurroundEffectType = TerrainSurroundEffectType.rockNoBareGround;

            var ledger = new VanillaGenerator().Generate(config).Ledger;

            Assert.That(ledger.Placements.Count, Is.GreaterThan(0));
            Assert.That(ledger.Placements.All(p => p.SurroundEffect == TerrainSurroundEffectType.rockNoBareGround), Is.True);
        }

        // primary/secondary双方が独立に転記されることを検証
        // Verifies both primary and secondary are transcribed independently
        [Test]
        public void ClusterPrimaryAndSecondarySurroundEffectAreTranscribedToLedger()
        {
            var config = MultiTileTestWorld.BuildConfig(1, 13);
            var objectConfig = new BiomeObjectConfig
            {
                clusterEntries = new[]
                {
                    new ObjectClusterEntry
                    {
                        primary = new[] { TestGenerationConfigFactory.TestMapObjectGuid },
                        terrainSurroundEffectType = TerrainSurroundEffectType.rockBareGround,
                        secondaries = new[]
                        {
                            new ObjectClusterSecondary
                            {
                                mapObjectGuids = new[] { MultiTileTestWorld.IndependentMapObjectGuid },
                                terrainSurroundEffectType = TerrainSurroundEffectType.rockNoBareGround,
                            },
                        },
                    },
                },
            };
            config.grassland.objectConfig = objectConfig;
            config.forest.objectConfig = objectConfig;

            var ledger = new VanillaGenerator().Generate(config).Ledger;

            Assert.That(ledger.Placements.Any(p => p.Guid == TestGenerationConfigFactory.TestMapObjectGuid
                                                   && p.SurroundEffect == TerrainSurroundEffectType.rockBareGround), Is.True);
            Assert.That(ledger.Placements.Any(p => p.Guid == MultiTileTestWorld.IndependentMapObjectGuid
                                                   && p.SurroundEffect == TerrainSurroundEffectType.rockNoBareGround), Is.True);
        }
    }
}

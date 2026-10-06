using System;
using Game.MapGeneration.Facade;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Surround;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class LegacySurfaceLoadTest
    {
        [Test]
        public void LegacyHeightBuilderKeepsPreTreeInputAndDoesNotRaiseLowLand()
        {
            var config = new TerrainGenerationConfig
            {
                surfaceRevision = WorldSurfaceRevision.Legacy4,
                overrideResolution = 17,
                terrainWidth = 32f,
                terrainLength = 32f,
            };
            var pre = new float[17, 17];
            pre[8, 8] = 0.003f;
            var ledger = new PlacementLedger();

            // 旧版では陸地場を要求せず低い値を保持する
            // Legacy builds require no land field and retain low heights
            var pair = TileSurfaceHeightBuilder.Build(pre, config, Vector3.zero, ledger, MapGenerationAlgorithmTable.ResolveSurface(config.surfaceRevision).CreateHeightPolicy(config));
            Assert.That(pair.Pre, Is.SameAs(pre));
            CollectionAssert.AreEqual(pre, pair.Post);
            Assert.That(pre[8, 8], Is.EqualTo(0.003f));
            Assert.That(WorldTerrainLayout.CreateTerrainAsset().SurfacePresentation, Is.TypeOf<TerrainSurfacePresentation.Existing>());
        }

        [Test]
        public void LedgerValidationIsLazyAndResolvesOnlyOnce()
        {
            var ledger = new PlacementLedger();
            var source = new CountingSource(ledger);
            var config = new TerrainGenerationConfig();
            var species = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), Array.Empty<BiomeType>());
            var validated = new ValidatedPlacementLedgerSource(source, ledger.ComputeDigest(), species);
            Assert.That(source.Count, Is.Zero);
            Assert.That(validated.Resolve(), Is.SameAs(ledger));
            Assert.That(validated.Resolve(), Is.SameAs(ledger));
            Assert.That(source.Count, Is.EqualTo(1));
        }

        [Test]
        public void LedgerDigestMismatchLogsAndStopsReconstruction()
        {
            var ledger = new PlacementLedger();
            var config = new TerrainGenerationConfig();
            var species = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), Array.Empty<BiomeType>());
            var validated = new ValidatedPlacementLedgerSource(new CountingSource(ledger), "different", species);
            string reason = $"[TileVisualBaker] Resolved placement ledger digest '{ledger.ComputeDigest()}' does not match expected digest 'different'.";
            LogAssert.Expect(LogType.Error, reason);
            Assert.Throws<InvalidOperationException>(() => validated.Resolve());
        }

        private sealed class CountingSource : IPlacementLedgerSource
        {
            private readonly PlacementLedger _ledger;
            public int Count { get; private set; }

            public CountingSource(PlacementLedger ledger)
            {
                _ledger = ledger;
            }

            public PlacementLedger Resolve()
            {
                Count++;
                return _ledger;
            }
        }
    }
}

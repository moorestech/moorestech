using System;
using Game.MapGeneration.Facade;
using Game.MapGeneration.Surface;
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
            var post = TileSurfaceHeightBuilder.Build(pre, config, Vector3.zero, ledger, new LegacySurfaceHeightPolicy(), null);
            CollectionAssert.AreEqual(pre, post);
            Assert.That(pre[8, 8], Is.EqualTo(0.003f));
            Assert.That(WorldTerrainLayout.CreateTerrainAsset().SurfacePresentation, Is.TypeOf<TerrainSurfacePresentation.Legacy>());
        }

        [Test]
        public void LedgerValidationIsLazyAndResolvesOnlyOnce()
        {
            var ledger = new PlacementLedger();
            var run = LedgerRunFixture.Legacy(ledger);
            var source = new CountingSource(run);
            var config = new TerrainGenerationConfig();
            var species = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), Array.Empty<BiomeType>());
            var validated = new ValidatedGenerationRunSource(source, ledger.ComputeDigest(), species, config);
            Assert.That(source.Count, Is.Zero);
            Assert.That(validated.Resolve(), Is.SameAs(run));
            Assert.That(validated.Resolve(), Is.SameAs(run));
            Assert.That(source.Count, Is.EqualTo(1));
        }

        [Test]
        public void LedgerDigestMismatchLogsAndStopsReconstruction()
        {
            var ledger = new PlacementLedger();
            var config = new TerrainGenerationConfig();
            var species = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), Array.Empty<BiomeType>());
            var validated = new ValidatedGenerationRunSource(new CountingSource(LedgerRunFixture.Legacy(ledger)), "different", species, config);
            string reason = $"[GeneratedSurface] seed={config.seed} revision={config.surfaceRevision} tile=ledger: [TileVisualBaker] Resolved placement ledger digest '{ledger.ComputeDigest()}' does not match expected digest 'different'.";
            LogAssert.Expect(LogType.Error, reason);
            Assert.Throws<InvalidOperationException>(() => validated.Resolve());
        }

        private sealed class CountingSource : IGenerationRunSource
        {
            private readonly GenerationRun _run;
            public int Count { get; private set; }

            public CountingSource(GenerationRun run)
            {
                _run = run;
            }

            public GenerationRun Resolve()
            {
                Count++;
                return _run;
            }
        }
    }
}

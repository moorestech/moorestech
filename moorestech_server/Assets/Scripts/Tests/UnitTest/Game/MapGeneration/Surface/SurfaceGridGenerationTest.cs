using System;
using System.Text.RegularExpressions;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Surface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class SurfaceGridGenerationTest
    {
        [Test]
        public void GeneratedRectangularTilesShareEveryBoundaryVertex()
        {
            var config = GenerationRuntimeConfigFactory.Build(TestGenerationConfigFactory.CreateSmall());
            config.gridSizeX = 2;
            config.gridSizeZ = 2;
            config.terrainWidth = 1024f;
            config.terrainLength = 512f;
            config.biomeBlendRadius = 4;
            config.chunkPadding = 4;
            config.shoreConfig.minSeaRegionSize = 0;

            // 実分類と高さ生成を境界検査へ通す
            // Run real classification and heights through the seam checks
            var first = SurfaceGridBuilder.Build(config);
            var second = SurfaceGridBuilder.Build(config);
            Assert.That(first.Output.Tiles.Count, Is.EqualTo(4));
            for (int tile = 0; tile < first.Output.Tiles.Count; tile++)
                CollectionAssert.AreEqual(first.Output.Tiles[tile].Heights, second.Output.Tiles[tile].Heights);

            var southwest = first.Output.Tiles[0];
            var southeast = first.Output.Tiles[1];
            int resolution = config.Resolution;
            for (int z = 0; z < resolution; z++)
                Assert.That(southwest.Heights[z * resolution + resolution - 1],
                    Is.EqualTo(southeast.Heights[z * resolution]));
        }

        [Test]
        public void DirectLegacyGeneratorPinsItsOwnRevisionWithoutChangingInput()
        {
            TestGenerationConfigFactory.LoadMasterWithMapObjectScaleForProvisioning(1f);
            var config = GenerationRuntimeConfigFactory.Build(TestGenerationConfigFactory.CreateSmall());
            config.surfaceRevision = WorldSurfaceRevision.Grounded5;

            // 旧生成器直呼びでも旧版判定を保つ
            // Keep legacy admission even when calling the legacy generator
            var run = new LegacyVanillaGenerator().Generate(config);
            Assert.That(run.Config.surfaceRevision, Is.EqualTo(WorldSurfaceRevision.Legacy4));
            Assert.That(config.surfaceRevision, Is.EqualTo(WorldSurfaceRevision.Grounded5));
            Assert.That(run.Ledger.GroundingPads, Is.Empty);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        public void InvalidClassificationSeaLevelFailsBeforeGeneration(float seaLevel)
        {
            // 生成器の入口で生成より前に拒否する
            // The generator entry rejects it before any generation runs
            var config = GenerationRuntimeConfigFactory.Build(TestGenerationConfigFactory.CreateSmall());
            config.seaLevel = seaLevel;
            LogAssert.Expect(LogType.Error, new Regex("GeneratedSurface.*Classification seaLevel"));
            Assert.Throws<InvalidOperationException>(() => new GroundedVanillaGenerator(SurfaceEnvelope.GeneratedV5).Generate(config));
        }

        [Test]
        public void HeightmapDisabledCannotProduceGuaranteedSaveData()
        {
            var config = GenerationRuntimeConfigFactory.Build(TestGenerationConfigFactory.CreateSmall());
            config.generateHeightmap = false;
            LogAssert.Expect(LogType.Error, new Regex("GeneratedSurface.*Heightmap-disabled"));
            Assert.Throws<InvalidOperationException>(() => new GroundedVanillaGenerator(SurfaceEnvelope.GeneratedV5).Generate(config));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        public void InvalidTileHeightFailsWithCoordinates(float height)
        {
            var grid = SurfaceGridFixture.Create(1, 3, 4f, 8f, true);
            grid.Output.Tiles[0].Heights[4] = height;
            LogAssert.Expect(LogType.Error, new Regex("GeneratedSurface.*tile=0,0: Invalid normalized height at 1,1"));
            Assert.Throws<InvalidOperationException>(() =>
                new SurfaceTileGrid(grid.Output, SurfaceGridFixture.Masks(1, 9, true), grid.Config));
        }
    }
}

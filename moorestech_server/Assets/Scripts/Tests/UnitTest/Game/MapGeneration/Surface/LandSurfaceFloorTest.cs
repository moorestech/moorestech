using System;
using System.Text.RegularExpressions;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface;
using Mooresmaster.Model.GenerationModule;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class LandSurfaceFloorTest
    {
        [TestCase(600f)]
        [TestCase(5f)]
        [TestCase(1000f)]
        public void QuantizedFloorAndPadPreserveTheirDirectedBounds(float height)
        {
            var envelope = SurfaceEnvelope.GeneratedV5;
            var config = new TerrainGenerationConfig { terrainHeight = height };
            float floor = SurfaceQuantization.LandFloor(config, envelope, "fixture");
            Assert.That(floor, Is.GreaterThanOrEqualTo(SurfaceGuaranteeBounds.LandMinimum));
            Assert.That(floor, Is.LessThan(SurfaceGuaranteeBounds.LandMinimum + height / TerrainHeightStorage.Steps));

            // 上限と整数境界でも採掘面の直下を維持する
            // Keep pads immediately below mining bottoms at integer and upper boundaries
            foreach (int bottom in new[] { 5, (int)height })
            {
                float pad = SurfaceQuantization.PadHeight(bottom, config, "fixture");
                Assert.That(pad, Is.LessThanOrEqualTo(bottom - SurfaceQuantization.MiningBottomClearanceMeters));
                Assert.That(bottom - pad, Is.LessThanOrEqualTo(height / TerrainHeightStorage.Steps + SurfaceGuaranteeBounds.RangeGapTolerance));
            }
        }

        [Test]
        public void OneLandCornerProtectsAdjacentCellsWithoutRaisingRemoteSea()
        {
            var grid = SurfaceGridFixture.Create(1, 5, 4f, 8f, false);
            var mask = new bool[25];
            mask[2 * 5 + 2] = true;
            grid = new SurfaceTileGrid(grid.Output, new[] { mask }, grid.Config);
            grid.ApplyLandFloor(SurfaceEnvelope.GeneratedV5);

            // 陸角に隣接する4セルだけを保護する
            // Protect only the four cells touching the land corner
            for (int z = 0; z < 5; z++)
            for (int x = 0; x < 5; x++)
                Assert.That(grid.GetHeight(x, z), 1 <= x && x <= 3 && 1 <= z && z <= 3
                    ? Is.GreaterThanOrEqualTo(SurfaceGuaranteeBounds.LandMinimum) : Is.EqualTo(0f));
            Assert.That(grid.Land.ContainsSupport(new Rect(1f, 2f, 2f, 4f)), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AllSeaAndAllLandKeepTheirClassification(bool land)
        {
            var grid = SurfaceGridFixture.Create(1, 3, 4f, 8f, land);
            grid.ApplyLandFloor(SurfaceEnvelope.GeneratedV5);
            Assert.That(grid.GetHeight(1, 1), land ? Is.GreaterThanOrEqualTo(SurfaceGuaranteeBounds.LandMinimum) : Is.EqualTo(0f));
            Assert.That(grid.Land.ContainsSupport(new Rect(1f, 1f, 1f, 1f)), Is.EqualTo(land));
        }

        [Test]
        public void RectangularTilesShareFourWayVertexAndInterpolation()
        {
            var grid = SurfaceGridFixture.Create(2, 3, 4f, 8f, true);
            grid.SetHeight(2, 2, 20f);
            foreach (var tile in grid.Output.Tiles)
            {
                int localX = tile.TileX == 0 ? 2 : 0;
                int localZ = tile.TileZ == 0 ? 2 : 0;
                Assert.That(tile.Heights[localZ * 3 + localX], Is.EqualTo(20f / 600f));
            }
            Assert.That(grid.SampleHeight(Vector2.zero), Is.EqualTo(20f).Within(0.00001f));
            Assert.That(grid.SampleHeight(new Vector2(-1f, -2f)), Is.EqualTo(5f).Within(0.00001f));
            Assert.That(grid.Land.ContainsSupport(new Rect(-1f, -1f, 2f, 2f)), Is.True);
            Assert.That(grid.Land.ContainsSupport(new Rect(-5f, -1f, 2f, 2f)), Is.False);
        }

        [Test]
        public void SharedHeightMismatchFailsWithTileDiagnostics()
        {
            var grid = SurfaceGridFixture.Create(2, 3, 4f, 8f, true);
            grid.Output.Tiles[1].Heights[0] = 0.1f;
            LogAssert.Expect(LogType.Error, new Regex("seed=.*revision=.*tile=.*Shared vertex mismatch"));
            var exception = Assert.Throws<InvalidOperationException>(() =>
                new SurfaceTileGrid(grid.Output, SurfaceGridFixture.Masks(4, 9, true), grid.Config));
            Assert.That(exception.Message, Does.Contain($"owner={0f:R}, incoming={0.1f:R}"));
            Assert.That(exception.Message, Does.Contain("ownerLand=True, incomingLand=True"));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0f)]
        [TestCase(4f)]
        public void InvalidHeightCannotGenerateGuaranteedWorld(float height)
        {
            var config = new TerrainGenerationConfig { terrainHeight = height, gridSizeX = 1, gridSizeZ = 1 };
            LogAssert.Expect(LogType.Error, new Regex("GeneratedSurface.*seed=.*revision=.*tile="));
            Assert.Throws<InvalidOperationException>(() => new GroundedVanillaGenerator(SurfaceEnvelope.GeneratedV5).Generate(config));
        }

        [Test]
        public void DispatchPreservesLegacyGenerator()
        {
            Assert.That(MapGenerationAlgorithmTable.Resolve(Generation.AlgorithmConst.VanillaGenerator,
                WorldSurfaceRevision.Legacy4), Is.TypeOf<LegacyVanillaGenerator>());
            Assert.That(MapGenerationAlgorithmTable.Resolve(Generation.AlgorithmConst.VanillaGenerator,
                WorldSurfaceRevision.Grounded5), Is.TypeOf<GroundedVanillaGenerator>());
        }
    }
}

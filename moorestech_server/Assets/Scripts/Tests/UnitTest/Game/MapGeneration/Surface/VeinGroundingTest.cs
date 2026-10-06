using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Pipeline.Visual.Placement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class VeinGroundingTest
    {
        [Test]
        public void ConnectedItemAndFluidCoresShareBottomWithoutAddingVisualPlacements()
        {
            var grid = SurfaceGridFixture.Create(1, 33, 32f, 32f, true);
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) grid.SetHeight(x, z, x + z);
            var ledger = new PlacementLedger();
            AddVein(grid.Output.ItemVeins, 11, 12, 7);
            AddVein(grid.Output.FluidVeins, 16, 12, 50);
            ledger.Add(new LedgerPlacement("tree", new Vector3(5f, 9f, 5f), Vector3.one,
                TerrainSurroundEffectType.rockNoBareGround, null));
            var original = ledger.Placements[0];
            var result = VeinGroundingPlanner.Build(grid, SurfaceEnvelope.GeneratedV5).Apply(ledger);

            // 異種鉱脈の支持が重なれば共通底面
            // Use one bottom when supports of different veins overlap
            int bottom = grid.Output.ItemVeins[0].Min.y;
            Assert.That(grid.Output.FluidVeins[0].Min.y, Is.EqualTo(bottom));
            Assert.That(grid.Output.ItemVeins[0].Max.y - bottom, Is.EqualTo(2));
            Assert.That(result.Placements[0].ScenePosition.y, Is.EqualTo(original.ScenePosition.y));
            Assert.That(ledger.Placements[0].ScenePosition, Is.EqualTo(original.ScenePosition));
            Assert.That(result.Placements.Count, Is.EqualTo(1));
            Assert.That(result.GroundingPads.Count, Is.EqualTo(2));
            Assert.That(result.Placements[0].Scale, Is.EqualTo(original.Scale));
            foreach (var pad in result.GroundingPads)
            {
                var support = grid.Geometry.SupportVertices(pad.Core);
                for (int z = support.yMin; z < support.yMax; z++)
                for (int x = support.xMin; x < support.xMax; x++)
                    Assert.That(grid.GetHeight(x, z), Is.EqualTo(pad.HeightMeters).Within(0.00001f));
                Assert.That(pad.HeightMeters, Is.LessThan(bottom));
            }
        }

        [Test]
        public void ProjectionIsOrderIndependentAndLeavesOutsideSkirtsUnchanged()
        {
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) heights[z, x] = (x + z) / 600f;
            var a = new VeinGroundingPad(new Rect(10f, 10f, 4f, 4f), 25f, 2f);
            var b = new VeinGroundingPad(new Rect(18f, 10f, 4f, 4f), 40f, 2f);
            var forward = Project(heights, Vector2.zero, new[] { a, b });
            var reverse = Project(heights, Vector2.zero, new[] { b, a });
            CollectionAssert.AreEqual(forward, reverse);
            Assert.That(forward[12, 12], Is.EqualTo(25f / 600f));
            Assert.That(forward[0, 0], Is.EqualTo(heights[0, 0]));
            Assert.That(heights[12, 12], Is.EqualTo(24f / 600f));
        }

        [Test]
        public void BoundaryPadHasIdenticalHeightOnBothTiles()
        {
            var pad = new VeinGroundingPad(new Rect(14f, 6f, 4f, 4f), 30f, 2f);
            var left = Project(new float[17, 17], Vector2.zero, new[] { pad });
            var right = Project(new float[17, 17], new Vector2(16f, 0f), new[] { pad });
            for (int z = 0; z < 17; z++) Assert.That(left[z, 16], Is.EqualTo(right[z, 0]));
        }

        [Test]
        public void DisplayProjectionReassignsCoresWithoutBlendingSkirtsAgain()
        {
            var grid = SurfaceGridFixture.Create(1, 33, 32f, 32f, true);
            var input = new float[33, 33];
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) input[z, x] = (100f + x + z) / grid.Config.terrainHeight;
            float padHeight = SurfaceQuantization.PadHeight(20, grid.Config, "fixture");
            var pad = new VeinGroundingPad(new Rect(10f, 10f, 4f, 4f), padHeight, 4f);
            var post = FinalSurfaceProjector.Apply(input, grid.Config, Vector3.zero, grid.Land, new[] { pad }, SurfaceEnvelope.GeneratedV5);

            // skirt圏内でもcore外は入力のまま、coreだけpad高さ
            // Inside the skirt reach non-core vertices keep the input and only cores take the pad height
            var support = grid.Geometry.SupportVertices(pad.Core);
            int skirtX = support.xMax;
            Assert.That(skirtX - pad.Core.xMax, Is.LessThan(pad.BlendWidth), "fixture: the vertex lies inside the skirt reach");
            Assert.That(post[12, skirtX], Is.EqualTo(SurfaceQuantization.EncodeNormalized(input[12, skirtX])));
            Assert.That(SurfaceQuantization.StoredNormalized(post[12, 12]) * grid.Config.terrainHeight, Is.EqualTo(pad.HeightMeters));
        }

        [Test]
        public void EmptyPlanRetainsLegacyDigest()
        {
            var grid = SurfaceGridFixture.Create(1, 17, 32f, 32f, true);
            var ledger = new PlacementLedger();
            var result = VeinGroundingPlanner.Build(grid, SurfaceEnvelope.GeneratedV5).Apply(ledger);
            Assert.That(result.ComputeDigest(), Is.EqualTo("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));
        }

        [Test]
        public void TerrainHeightUpperLimitCanBeGradedBelowMiningBottom()
        {
            var grid = SurfaceGridFixture.Create(1, 17, 32f, 32f, true);
            for (int z = 0; z < 17; z++)
            for (int x = 0; x < 17; x++) grid.SetHeight(x, z, 600f);
            var ledger = new PlacementLedger();
            AddVein(grid.Output.ItemVeins, 12, 12, 600);
            var result = VeinGroundingPlanner.Build(grid, SurfaceEnvelope.GeneratedV5).Apply(ledger);
            Assert.That(grid.Output.ItemVeins[0].Min.y, Is.EqualTo(600));
            Assert.That(result.GroundingPads[0].HeightMeters, Is.LessThan(600f));
        }

        [Test]
        public void DuplicateAndInvalidBindingsFailWithDiagnostics()
        {
            var bindings = new SurfacePlacementBindings(SurfaceGridFixture.Create(1, 17, 32f, 32f, true).Config);
            bindings.AddMapObject(0, 0);
            LogAssert.Expect(LogType.Error, new Regex(@"\[GeneratedSurface\] seed=.* revision=.* tile=bindings: Invalid or duplicate surface binding: output=1, ledger=0\.$"));
            Assert.Throws<InvalidOperationException>(() => bindings.AddMapObject(1, 0));
            LogAssert.Expect(LogType.Error, new Regex(@"\[GeneratedSurface\] seed=.* revision=.* tile=bindings: Invalid or duplicate surface binding: output=-1, ledger=2\.$"));
            Assert.Throws<InvalidOperationException>(() => bindings.AddMapObject(-1, 2));
        }

        private static float[,] Project(float[,] heights, Vector2 origin, VeinGroundingPad[] pads)
        {
            return GroundingHeightProjector.ApplyAtGeneration(heights, origin, Vector2.one, 600f, pads);
        }

        private static void AddVein(List<PlacedVein> veins, int x, int z, int bottom)
        {
            veins.Add(new PlacedVein("fixture", new Vector3Int(x - 1, bottom, z - 1), new Vector3Int(x + 1, bottom + 2, z + 1)));
        }
    }
}

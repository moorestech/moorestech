using System;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Surface.Placement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class VeinLandConstraintTest
    {
        [Test]
        public void LegacyConstraintDoesNotChangeCandidateAcceptance()
        {
            var constraint = new UnrestrictedVeinLandConstraint();
            Assert.That(constraint.Accept(Candidate(0, 0)), Is.True);
        }

        [Test]
        public void EntireCoreAndSkirtOnLandAreAcceptedWithoutHeightRestriction()
        {
            var constraint = Create(true, -1, -1, Vector2.zero);
            Assert.That(constraint.Accept(Candidate(10, 10)), Is.True);
            var high = new PlacedVein("high", new Vector3Int(9, 500, 9), new Vector3Int(11, 500, 11));
            Assert.That(constraint.Accept(high), Is.True);
        }

        [TestCase(14, 10)]
        [TestCase(15, 15)]
        public void SeaInSkirtOrSingleInterpolationSupportCornerRejects(int seaX, int seaZ)
        {
            // 候補のcoreは陸でも外周と補間支持点の海を拒否する
            // Reject sea in the skirt and interpolation support even when the core is land
            var constraint = Create(true, seaX, seaZ, Vector2.zero);
            Assert.That(constraint.Accept(Candidate(10, 10)), Is.False);
            Assert.That(constraint.Accept(Candidate(25, 25)), Is.True);
            LogAssert.Expect(LogType.Warning, new Regex("seed=196.*tile=0,0.*coast=1, world-edge=0"));
            constraint.ReportRejections(196, 0, 0, "entry");
        }

        [Test]
        public void WorldEdgeAndAllSeaRejectWithSeparateReasons()
        {
            var coast = Create(false, -1, -1, Vector2.zero);
            Assert.That(coast.Accept(Candidate(10, 10)), Is.False);
            Assert.That(coast.Accept(Candidate(1, 1)), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("coast=1, world-edge=1"));
            coast.ReportRejections(196, 0, 0, "entry");
        }

        [Test]
        public void LandNarrowerThanEntireGradingFootprintRejects()
        {
            var geometry = new SurfaceLattice(Vector2.zero, Vector2.one, 41, 41);
            var mask = new bool[41 * 41];
            for (int z = 9; z <= 12; z++)
            for (int x = 9; x <= 12; x++) mask[z * 41 + x] = true;
            var constraint = new GroundedVeinLandConstraint(new LandCellField(geometry, mask), Vector2.zero, SurfaceEnvelope.GeneratedV5);
            Assert.That(constraint.Accept(Candidate(10, 10)), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("coast=1, world-edge=0"));
            constraint.ReportRejections(196, 0, 0, "entry");
        }

        [Test]
        public void AdjacentTileLandRemainsAvailableAcrossSeam()
        {
            var grid = SurfaceGridFixture.Create(2, 17, 32f, 32f, true);
            var constraint = new GroundedVeinLandConstraint(grid.Land, Vector2.zero, SurfaceEnvelope.GeneratedV5);
            var seam = grid.Geometry.ScenePosition(16, 16);
            Assert.That(constraint.Accept(Candidate(Mathf.RoundToInt(seam.x), Mathf.RoundToInt(seam.y))), Is.True);
        }

        [Test]
        public void NoiseToSceneShiftIsAppliedOnce()
        {
            var shift = new Vector2(100f, -70f);
            var constraint = Create(true, -1, -1, shift);
            Assert.That(constraint.Accept(Candidate(110, -60)), Is.True);
            Assert.That(constraint.Accept(Candidate(10, 10)), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("coast=0, world-edge=1"));
            constraint.ReportRejections(196, 0, 0, "entry");
        }

        [Test]
        public void GroundedHorizontalExclusionIncludesDifferentOriginalHeights()
        {
            var low = Candidate(10, 10);
            var high = new PlacedVein("high", new Vector3Int(9, 20, 9), new Vector3Int(11, 20, 11));
            Assert.That(VeinAabbBuilder.OverlapsAny(low, new[] { high }), Is.False);
            Assert.That(VeinAabbBuilder.OverlapsAnyXz(low, new[] { high }), Is.True);
        }

        private static GroundedVeinLandConstraint Create(bool land, int seaX, int seaZ, Vector2 shift)
        {
            var geometry = new SurfaceLattice(Vector2.zero, Vector2.one, 41, 41);
            var mask = new bool[41 * 41];
            for (int i = 0; i < mask.Length; i++) mask[i] = land;
            if (seaX >= 0) mask[seaZ * 41 + seaX] = false;
            return new GroundedVeinLandConstraint(new LandCellField(geometry, mask), shift, SurfaceEnvelope.GeneratedV5);
        }

        private static PlacedVein Candidate(int x, int z)
        {
            return new PlacedVein(Guid.Empty.ToString(), new Vector3Int(x - 1, 0, z - 1), new Vector3Int(x + 1, 0, z + 1));
        }
    }
}

using System;
using Game.MapGeneration.Surface;
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
    public class VeinPlacementRuleTest
    {
        [Test]
        public void LegacyRuleDoesNotChangeCandidateAcceptance()
        {
            var rule = new LegacyVeinPlacementRule();
            Assert.That(Accept(rule, Candidate(0, 0)), Is.True);
        }

        [Test]
        public void EntireCoreAndSkirtOnLandAreAcceptedWithoutHeightRestriction()
        {
            var constraint = Create(true, -1, -1, Vector2.zero);
            Assert.That(Accept(constraint, Candidate(10, 10)), Is.True);
            var high = new PlacedVein("high", new Vector3Int(9, 500, 9), new Vector3Int(11, 500, 11));
            Assert.That(Accept(constraint, high), Is.True);
        }

        [TestCase(14, 10)]
        [TestCase(15, 15)]
        public void SeaInSkirtOrSingleInterpolationSupportCornerRejects(int seaX, int seaZ)
        {
            // coreが陸でも外周・補間点の海を拒否
            // Reject sea in skirt and support points even if the core is land
            var constraint = Create(true, seaX, seaZ, Vector2.zero);
            Assert.That(Accept(constraint, Candidate(10, 10)), Is.False);
            Assert.That(Accept(constraint, Candidate(25, 25)), Is.True);
            LogAssert.Expect(LogType.Warning, new Regex("seed=196.*tile=0,0.*coast=1, world-edge=0"));
            constraint.ReportRejections(196, 0, 0, "entry");
        }

        [Test]
        public void WorldEdgeAndAllSeaRejectWithSeparateReasons()
        {
            var coast = Create(false, -1, -1, Vector2.zero);
            Assert.That(Accept(coast, Candidate(10, 10)), Is.False);
            Assert.That(Accept(coast, Candidate(1, 1)), Is.False);
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
            var constraint = new GroundedVeinPlacementRule(new LandCellField(geometry, mask), Vector2.zero, SurfaceEnvelope.GeneratedV5, WorldSurfaceRevision.Grounded5);
            Assert.That(Accept(constraint, Candidate(10, 10)), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("coast=1, world-edge=0"));
            constraint.ReportRejections(196, 0, 0, "entry");
        }

        [Test]
        public void AdjacentTileLandRemainsAvailableAcrossSeam()
        {
            var grid = SurfaceGridFixture.Create(2, 17, 32f, 32f, true);
            var constraint = new GroundedVeinPlacementRule(grid.Land, Vector2.zero, SurfaceEnvelope.GeneratedV5, WorldSurfaceRevision.Grounded5);
            var seam = grid.Geometry.ScenePosition(16, 16);
            Assert.That(Accept(constraint, Candidate(Mathf.RoundToInt(seam.x), Mathf.RoundToInt(seam.y))), Is.True);
        }

        [Test]
        public void NoiseToSceneShiftIsAppliedOnce()
        {
            var shift = new Vector2(100f, -70f);
            var constraint = Create(true, -1, -1, shift);
            Assert.That(Accept(constraint, Candidate(110, -60)), Is.True);
            Assert.That(Accept(constraint, Candidate(10, 10)), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("coast=0, world-edge=1"));
            constraint.ReportRejections(196, 0, 0, "entry");
        }

        [Test]
        public void GroundedHorizontalExclusionIncludesDifferentOriginalHeights()
        {
            var low = Candidate(10, 10);
            var high = new PlacedVein("high", new Vector3Int(9, 20, 9), new Vector3Int(11, 20, 11));
            Assert.That(new LegacyVeinPlacementRule().TryAcceptMember(low, new[] { high }, Array.Empty<PlacedVein>()), Is.True);
            Assert.That(new LegacyVeinPlacementRule().TryAcceptMember(low, Array.Empty<PlacedVein>(), new[] { high }), Is.True);
            Assert.That(Create(true, -1, -1, Vector2.zero).TryAcceptMember(low, new[] { high }, Array.Empty<PlacedVein>()), Is.False);
            Assert.That(Create(true, -1, -1, Vector2.zero).TryAcceptMember(low, Array.Empty<PlacedVein>(), new[] { high }), Is.False);
        }

        [Test]
        public void EmptyEntryIsQuietButAttemptedMemberShortageIsReportedAndReset()
        {
            var rule = Create(true, -1, -1, Vector2.zero);
            rule.ReportRejections(196, 0, 0, "outside-band");
            LogAssert.NoUnexpectedReceived();

            // 陸上でも重なりで落ちた候補は評価数と重なり却下数に分けて記録する
            // A land candidate dropped by overlap is recorded as evaluated and overlap-rejected, never as accepted
            var occupied = Candidate(10, 10);
            Assert.That(rule.TryAcceptMember(occupied, new[] { occupied }, Array.Empty<PlacedVein>()), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("eligible centers=0, evaluated=1, accepted=0, coast=0, world-edge=0, overlap=1"));
            rule.ReportRejections(196, 0, 0, "excluded-member");
            rule.BeginCluster();
            LogAssert.Expect(LogType.Warning, new Regex("eligible centers=1, evaluated=0, accepted=0"));
            rule.ReportRejections(196, 0, 0, "filtered-center");
            Assert.That(Accept(rule, Candidate(10, 10)), Is.True);
            rule.ReportRejections(196, 0, 0, "accepted-entry");
            rule.ReportRejections(196, 0, 0, "next-empty-entry");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RejectionLogNamesTheRuleRevision()
        {
            var rule = Create(false, -1, -1, Vector2.zero);
            Assert.That(Accept(rule, Candidate(10, 10)), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("seed=196 revision=Grounded5 tile=2,3 entry=entry"));
            rule.ReportRejections(196, 2, 3, "entry");
        }

        private static bool Accept(IVeinPlacementRule rule, PlacedVein candidate)
        {
            return rule.TryAcceptMember(candidate, Array.Empty<PlacedVein>(), Array.Empty<PlacedVein>());
        }

        private static GroundedVeinPlacementRule Create(bool land, int seaX, int seaZ, Vector2 shift)
        {
            var geometry = new SurfaceLattice(Vector2.zero, Vector2.one, 41, 41);
            var mask = new bool[41 * 41];
            for (int i = 0; i < mask.Length; i++) mask[i] = land;
            if (0 <= seaX) mask[seaZ * 41 + seaX] = false;
            return new GroundedVeinPlacementRule(new LandCellField(geometry, mask), shift, SurfaceEnvelope.GeneratedV5, WorldSurfaceRevision.Grounded5);
        }

        private static PlacedVein Candidate(int x, int z)
        {
            return new PlacedVein(Guid.Empty.ToString(), new Vector3Int(x - 1, 0, z - 1), new Vector3Int(x + 1, 0, z + 1));
        }
    }
}

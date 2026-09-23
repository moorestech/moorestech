using System.Linq;
using Game.BeltSegment;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
namespace Tests.CombinedTest.Game.BeltSegmentWorld
{
    public class BeltWorldTopologyTest
    {
        [Test]
        public void StraightAndCornerFormOneMaximalPath()
        {
            var f = new BeltWorldFixture();
            f.Belt(Vector3Int.zero, BlockDirection.North);
            f.Belt(Vector3Int.forward, BlockDirection.East);
            f.Belt(Vector3Int.forward + Vector3Int.right, BlockDirection.East);
            f.Tick(1);
            var s = f.Snapshot();
            Assert.AreEqual(1, s.Routes.Length); Assert.AreEqual(3, s.Simulation.Segments[0].Capacity);
            Assert.AreEqual(BeltEntryDirection.FromLeft, s.Routes[0].Cells[2].Entry);
        }
        [TestCase(true)] [TestCase(false)]
        public void PureLoopHasCanonicalHeadAndOneNormalSelfLink(bool reverse)
        {
            var f = new BeltWorldFixture(); f.Loop(reverse); f.Tick(1); var s = f.Snapshot();
            Assert.AreEqual(1, s.Routes.Length); Assert.AreEqual(4, s.Routes[0].Cells.Length);
            Assert.AreEqual(new BeltCell(0, 0, 0), s.Routes[0].Cells[0].Cell);
            Assert.AreEqual(BeltSegmentKind.Normal, s.Simulation.Segments[0].Kind);
            Assert.AreEqual(1, s.Simulation.Links.Length);
            Assert.AreEqual(s.Simulation.Links[0].SourceSegmentId, s.Simulation.Links[0].TargetSegmentId);
        }
        [TestCase(true)] [TestCase(false)]
        public void SlopeUsesSharedSurfaceBoundary(bool up)
        {
            var f = new BeltWorldFixture();
            f.Belt(new Vector3Int(0, up ? 0 : 1, -1), BlockDirection.North);
            f.Add(up ? ForUnitTestModBlockId.TestBeltConveyorUp : ForUnitTestModBlockId.TestBeltConveyorDown,
                Vector3Int.zero, BlockDirection.North);
            f.Belt(new Vector3Int(0, up ? 1 : 0, 1), BlockDirection.North);
            f.Tick(1); var route = f.Snapshot().Routes.Single();
            Assert.AreEqual(3, route.Cells.Length);
            Assert.AreEqual(1, route.Cells[1].CenterHeightTwice);
            Assert.AreEqual(up ? 0 : 2, route.Cells[1].InputHeightTwice);
            Assert.AreEqual(up ? 0 : 2, route.Cells[0].CenterHeightTwice);
            Assert.AreEqual(up ? 2 : 0, route.Cells[2].CenterHeightTwice);
        }
        [Test]
        public void BranchTerminatesUpstreamPathAndMergeOwnsOneCell()
        {
            var f = new BeltWorldFixture(); f.Belt(Vector3Int.back, BlockDirection.North);
            f.Add(ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero, BlockDirection.North);
            f.Belt(Vector3Int.left, BlockDirection.West); f.Belt(Vector3Int.right, BlockDirection.East);
            f.Tick(1);
            var branch = f.Snapshot().Simulation.Segments.Single(s => s.Kind == BeltSegmentKind.Branch);
            Assert.AreEqual(2, branch.Capacity);
            f = new BeltWorldFixture();
            f.Belt(Vector3Int.zero, BlockDirection.North); f.Belt(Vector3Int.back, BlockDirection.North);
            f.Belt(Vector3Int.left, BlockDirection.East); f.Belt(Vector3Int.right, BlockDirection.West);
            f.Tick(1);
            Assert.AreEqual(1, f.Snapshot().Simulation.Segments.Single(s => s.Kind == BeltSegmentKind.Merge).Capacity);
        }
        [Test]
        public void DisconnectedBeltsAndZeroBeltsHaveValidSnapshots()
        {
            var f = new BeltWorldFixture(); f.Tick(1); Assert.AreEqual(0, f.Snapshot().Routes.Length);
            f.Belt(Vector3Int.zero, BlockDirection.North); f.Belt(Vector3Int.right * 8, BlockDirection.North);
            f.Tick(1); Assert.AreEqual(2, f.Snapshot().Routes.Length);
        }
    }
}

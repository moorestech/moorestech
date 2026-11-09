using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 周囲の入出力辺だけから分岐器の向きと出口が決まることを確かめる（ADR 0097「既存ベルトからの分岐」2026-11-09）
    // Checks that the splitter's facing and exits follow from the surrounding port edges alone (ADR 0097, branch section, 2026-11-09)
    public class BeltRouteBranchGeometryTest
    {
        private static readonly Vector3Int Cell = new(10, 3, 10);

        [Test]
        public void StraightBeltKeepsItsFacingAndDefaultsToRight()
        {
            var edges = NewEdges();
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.East, false, out var shape));
            Assert.AreEqual(BlockDirection.East, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.South, shape.BranchExit);

            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.East, true, out shape));
            Assert.AreEqual(BlockDirection.North, shape.BranchExit);
        }

        [Test]
        public void StraightBeltFedFromBackIsStillStraight()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.West);
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.East, false, out var shape));
            Assert.AreEqual(BlockDirection.East, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.South, shape.BranchExit);
        }

        // 北向きのベルトが西から受けている角: 分岐器は東向き、既定の出口は正面（東）、R で空いている横（南）
        // A north-facing belt fed from the west: the splitter faces east, the default exit is the front (east), R switches to the free side (south)
        [Test]
        public void CornerFedFromLeftFacesTheFeederAndDefaultsToFront()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.West);
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, false, out var shape));
            Assert.AreEqual(BlockDirection.East, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.East, shape.BranchExit);

            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, true, out shape));
            Assert.AreEqual(BlockDirection.East, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.South, shape.BranchExit);
        }

        [Test]
        public void CornerFedFromRightFacesTheFeeder()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.East);
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, false, out var shape));
            Assert.AreEqual(BlockDirection.West, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.West, shape.BranchExit);
        }

        [Test]
        public void BackAndSideFeedsCannotBranch()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.South);
            AddFeeder(edges, BlockDirection.West);
            Assert.AreEqual(BeltRouteBranchBlockReason.MultipleInputs, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, false, out _));
        }

        [Test]
        public void BothSideFeedsCannotBranch()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.West);
            AddFeeder(edges, BlockDirection.East);
            Assert.AreEqual(BeltRouteBranchBlockReason.MultipleInputs, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, false, out _));
        }

        // 使わない出口の先に受け手がいると分岐不可。出口を切り替えて受け手側を分岐にすれば成立する
        // A receiver beyond the unused exit blocks the branch; switching the exit so the receiver's side becomes the branch makes it valid
        [Test]
        public void ReceiverBeyondUnusedExitBlocksOnlyThatChoice()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.West);
            AddReceiver(edges, BlockDirection.South);
            Assert.AreEqual(BeltRouteBranchBlockReason.UnusedExitHasReceiver, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, false, out _));
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, true, out var shape));
            Assert.AreEqual(BlockDirection.South, shape.BranchExit);
        }

        // 直線で使わない出口（既定なら左）の先に受け手がいると分岐不可。Rで右左を入れ替えると成立する
        // On a straight belt a receiver beyond the unused exit (left by default) blocks the branch; toggling the exit makes it valid
        [Test]
        public void StraightBeltReceiverBeyondUnusedExitBlocksOnlyThatChoice()
        {
            var edges = NewEdges();
            AddReceiver(edges, BlockDirection.North);
            Assert.AreEqual(BeltRouteBranchBlockReason.UnusedExitHasReceiver, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.East, false, out _));
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.East, true, out var shape));
            Assert.AreEqual(BlockDirection.North, shape.BranchExit);
        }

        private static BeltRouteGrid NewEdges()
        {
            return new BeltRouteGrid(Cell, Vector3Int.one);
        }

        // 隣の出力口がこのセルの face 面へ向いている
        // A neighbour's output port aimed at this cell's given face
        private static void AddFeeder(BeltRouteGrid edges, BlockDirection face)
        {
            edges.AddOutputEdge(BeltRouteEdge.FromFace(Cell, face, Cell.y));
        }

        // 隣の入力口がこのセルの face 面に接している
        // A neighbour's input port touching this cell's given face
        private static void AddReceiver(BeltRouteGrid edges, BlockDirection face)
        {
            edges.AddInputEdge(BeltRouteEdge.FromFace(Cell, face, Cell.y));
        }
    }
}

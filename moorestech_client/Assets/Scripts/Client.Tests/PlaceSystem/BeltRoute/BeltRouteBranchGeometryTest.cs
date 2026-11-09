using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 入出力辺だけで向きと出口が決まることを確認（ADR 0097）
    // Checks the facing and exits follow from port edges alone (ADR 0097)
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

        // 北向き角を西から受ける: 分岐器は東向き・既定東・Rで南
        // North-facing corner fed from the west: splitter faces east, default east, R gives south
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

        // 使わない出口の先に受け手がいると不可、出口切替で成立
        // A receiver beyond the unused exit blocks it; switching the exit makes it valid
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

        // 直線の未使用出口の先に受け手があると不可、R で成立
        // On a straight belt a receiver beyond the unused exit blocks it; R makes it valid
        [Test]
        public void StraightBeltReceiverBeyondUnusedExitBlocksOnlyThatChoice()
        {
            var edges = NewEdges();
            AddReceiver(edges, BlockDirection.North);
            Assert.AreEqual(BeltRouteBranchBlockReason.UnusedExitHasReceiver, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.East, false, out _));
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.East, true, out var shape));
            Assert.AreEqual(BlockDirection.North, shape.BranchExit);
        }

        private static BeltRouteNeighbourEdges NewEdges()
        {
            return new BeltRouteNeighbourEdges();
        }

        // 隣の出力口がこのセルの face 面へ向いている
        // A neighbour's output port aimed at this cell's given face
        private static void AddFeeder(BeltRouteNeighbourEdges edges, BlockDirection face)
        {
            edges.AddOutputEdge(BeltRouteEdge.FromFace(Cell, face, Cell.y));
        }

        // 隣の入力口がこのセルの face 面に接している
        // A neighbour's input port touching this cell's given face
        private static void AddReceiver(BeltRouteNeighbourEdges edges, BlockDirection face)
        {
            edges.AddInputEdge(BeltRouteEdge.FromFace(Cell, face, Cell.y));
        }
    }
}

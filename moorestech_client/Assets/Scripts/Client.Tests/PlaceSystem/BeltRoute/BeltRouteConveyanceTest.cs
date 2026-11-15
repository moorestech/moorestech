using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Client.Tests.PlaceSystem.BeltRoute.BeltRouteConveyanceWorld;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 探索で組んだ経路をテスト用サーバーへ置き、実際に荷物が流れることを確かめる。分岐も同じく確かめる
    // Places a searched route on the test server and checks items actually flow; a branch is checked the same way
    public class BeltRouteConveyanceTest
    {
        [Test]
        public void RouteOverObstacleConveysItemToTheFarChest()
        {
            var world = BootWorld();
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.BlockId, new Vector3Int(3, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            // 障害を跨ぐ経路を本番の探索で求める
            // Plan a route over the obstacle with the production search
            var query = new BeltRouteQuery(new Vector3Int(0, 0, 0), BlockDirection.East, false, new Vector3Int(6, 0, 0), BlockDirection.East, true, false);
            var result = new BeltRoutePlanner(BuildGridFromWorld(world), BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, "no route");
            BeltRoutePlanVerifier.AssertValid(BuildGridFromWorld(world), query, result.Plan, "obstacle route");
            Assert.IsTrue(result.Plan.Cells.Any(cell => cell.Kind == BeltRouteCellKind.Up), "route should climb over the obstacle");
            Debug.Log("route: " + string.Join(" ", result.Plan.Cells));

            var family = ForUnitTestModBlockId.GearBeltConveyor;
            foreach (var cell in result.Plan.Cells)
                Assert.IsTrue(world.TryAddBlock(ResolveFamilyBlock(family, cell.Kind), cell.Cell, cell.Facing, Array.Empty<BlockCreateParam>(), out _), $"placement failed at {cell}");

            // 歯車ベルトは回転が要るので、最上段の水平セルの横に発電機を置く
            // Gear belts need rotation, so a generator goes beside a flat cell on the top level
            var topFlat = result.Plan.Cells.First(cell => cell.Kind == BeltRouteCellKind.Straight && cell.Cell.y == result.Plan.Cells.Max(c => c.Cell.y));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, topFlat.Cell + new Vector3Int(0, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            var source = PlaceChest(world, new Vector3Int(-1, 0, 0));
            var sink = PlaceChest(world, new Vector3Int(7, 0, 0));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(TestItemId, 1));

            Assert.IsTrue(TickUntilDelivered(sink, 1, null, 0), "item did not reach the far chest");
            Assert.AreEqual(0, CountOf(source));
        }

        [Test]
        public void BranchSplitsItemsToBothChests()
        {
            var world = BootWorld();
            var straight = ForUnitTestModBlockId.GetBlock("7b3e1a10-0001-4000-8000-000000000009");
            var splitter = ForUnitTestModBlockId.GetBlock("7b3e1a10-0001-4000-8000-000000000012");
            for (var x = 0; x <= 4; x++)
                Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(x, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _));

            // (2,0,0)の東向きベルトから右（南）へ分岐する
            // Branch from the east-facing belt at (2,0,0) to its right (south)
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 0), BlockDirection.South, true, new Vector3Int(2, 0, -3), BlockDirection.South, true, false);
            var result = new BeltRoutePlanner(BuildGridFromWorld(world), BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, "no route");
            BeltRoutePlanVerifier.AssertValid(BuildGridFromWorld(world), query, result.Plan, "branch route");

            // 置き換えは撤去と設置の2段
            // Replacement is a removal followed by a placement
            Assert.IsTrue(world.RemoveBlock(new Vector3Int(2, 0, 0), BlockRemoveReason.ManualRemove));
            Assert.IsTrue(world.TryAddBlock(splitter, new Vector3Int(2, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _));
            foreach (var cell in result.Plan.Cells)
                Assert.IsTrue(world.TryAddBlock(straight, cell.Cell, cell.Facing, Array.Empty<BlockCreateParam>(), out _), $"placement failed at {cell}");

            var source = PlaceChest(world, new Vector3Int(-1, 0, 0));
            var mainSink = PlaceChest(world, new Vector3Int(5, 0, 0));
            var branchSink = PlaceChest(world, new Vector3Int(2, 0, -4));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(TestItemId, 6));

            Assert.IsTrue(TickUntilDelivered(mainSink, 1, branchSink, 1), $"items did not reach both chests main:{CountOf(mainSink)} branch:{CountOf(branchSink)}");
        }

        // 西から北へ曲がる角 (2,0,0) を分岐器へ置換（東向き、既定は東）
        // Replace the west-to-north corner at (2,0,0) with a splitter (faces east, default exit east)
        [Test]
        public void CornerBranchSplitsItemsToBothChests()
        {
            var world = BootWorld();
            var straight = ForUnitTestModBlockId.GetBlock("7b3e1a10-0001-4000-8000-000000000009");
            var splitter = ForUnitTestModBlockId.GetBlock("7b3e1a10-0001-4000-8000-000000000012");
            var corner = new Vector3Int(2, 0, 0);
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(0, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, corner, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(2, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(2, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            // 本番 Judge と同じ写し方で向きと出口を決める
            // Decide the facing and exit from the same copy the production Judge makes
            var reason = BeltRouteBranchGeometry.Resolve(BuildNeighbourEdges(world, corner), corner, BlockDirection.North, false, out var shape);
            Assert.AreEqual(BeltRouteBranchBlockReason.None, reason);
            Assert.AreEqual(BlockDirection.East, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.East, shape.BranchExit);

            var query = new BeltRouteQuery(corner, shape.BranchExit, true, new Vector3Int(5, 0, 0), BlockDirection.East, true, false);
            var result = new BeltRoutePlanner(BuildGridFromWorld(world), BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, "no route");
            BeltRoutePlanVerifier.AssertValid(BuildGridFromWorld(world), query, result.Plan, "corner branch route");

            Assert.IsTrue(world.RemoveBlock(corner, BlockRemoveReason.ManualRemove));
            Assert.IsTrue(world.TryAddBlock(splitter, corner, shape.SplitterFacing, Array.Empty<BlockCreateParam>(), out _));
            foreach (var cell in result.Plan.Cells)
                Assert.IsTrue(world.TryAddBlock(straight, cell.Cell, cell.Facing, Array.Empty<BlockCreateParam>(), out _), $"placement failed at {cell}");

            var source = PlaceChest(world, new Vector3Int(-1, 0, 0));
            var cornerSink = PlaceChest(world, new Vector3Int(2, 0, 3));
            var branchSink = PlaceChest(world, new Vector3Int(6, 0, 0));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(TestItemId, 6));

            Assert.IsTrue(TickUntilDelivered(cornerSink, 1, branchSink, 1), $"items did not reach both chests corner:{CountOf(cornerSink)} branch:{CountOf(branchSink)}");
        }
    }
}

using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 探索で組んだ経路をテスト用サーバーへ置き、実際に荷物が流れることを確かめる。分岐も同じく確かめる
    // Places a searched route on the test server and checks items actually flow; a branch is checked the same way
    public class BeltRouteConveyanceTest
    {
        private const int MaxTransportTicks = 600;
        private static readonly ItemId TestItemId = new(1);

        [Test]
        public void RouteOverObstacleConveysItemToTheFarChest()
        {
            var world = BootWorld();
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.BlockId, new Vector3Int(3, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            // 障害を跨ぐ経路を本番の探索で求める
            // Plan a route over the obstacle with the production search
            var query = new BeltRouteQuery(new Vector3Int(0, 0, 0), BlockDirection.East, false, new Vector3Int(6, 0, 0), BlockDirection.East, true, false, BeltRouteStepPriority.Default);
            var result = new BeltRoutePlanner(BuildGridFromWorld(world), BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, $"no route: {result.NoPathReason}");
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
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 0), BlockDirection.South, true, new Vector3Int(2, 0, -3), BlockDirection.South, true, false, BeltRouteStepPriority.Default);
            var result = new BeltRoutePlanner(BuildGridFromWorld(world), BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, $"no route: {result.NoPathReason}");
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

        private static IWorldBlockDatastore BootWorld()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return ServerContext.WorldBlockDatastore;
        }

        // サーバーの全ブロックを占有と入出力辺として箱へ写す。地形は無し
        // Copies every server block into the box as occupancy and port edges; there is no terrain
        private static BeltRouteGrid BuildGridFromWorld(IWorldBlockDatastore world)
        {
            var grid = new BeltRouteGrid(new Vector3Int(-8, 0, -8), new Vector3Int(24, 6, 24));
            foreach (var data in world.BlockMasterDictionary.Values)
            {
                var position = data.BlockPositionInfo;
                for (var x = position.MinPos.x; x <= position.MaxPos.x; x++)
                for (var y = position.MinPos.y; y <= position.MaxPos.y; y++)
                for (var z = position.MinPos.z; z <= position.MaxPos.z; z++)
                    grid.MarkBlockCell(new Vector3Int(x, y, z));
                BeltRouteBlockEdgeScanner.Write(data.Block.BlockMasterElement, data.Block.BlockId, position, grid);
            }
            return grid;
        }

        private static BlockId ResolveFamilyBlock(BlockId straightId, BeltRouteCellKind kind)
        {
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(straightId, out var family));
            return kind switch
            {
                BeltRouteCellKind.Up => family.UpBlockId.Value,
                BeltRouteCellKind.Down => family.DownBlockId.Value,
                _ => family.StraightBlockId,
            };
        }

        private static IOpenableBlockInventoryComponent PlaceChest(IWorldBlockDatastore world, Vector3Int position)
        {
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.ChestId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest), $"chest placement failed at {position}");
            return chest.GetComponent<IOpenableBlockInventoryComponent>();
        }

        // 片方だけ待つときはsecondをnullにする
        // Pass null as second to wait for one chest only
        private static bool TickUntilDelivered(IOpenableBlockInventoryComponent first, int firstMin, IOpenableBlockInventoryComponent second, int secondMin)
        {
            for (var tick = 0; tick < MaxTransportTicks; tick++)
            {
                GameUpdater.UpdateOneTick();
                if (CountOf(first) < firstMin) continue;
                if (second == null || secondMin <= CountOf(second)) return true;
            }
            return false;
        }

        private static int CountOf(IOpenableBlockInventoryComponent inventory)
        {
            var count = 0;
            for (var i = 0; i < inventory.GetSlotSize(); i++)
                if (inventory.GetItem(i).Id == TestItemId) count += inventory.GetItem(i).Count;
            return count;
        }
    }
}

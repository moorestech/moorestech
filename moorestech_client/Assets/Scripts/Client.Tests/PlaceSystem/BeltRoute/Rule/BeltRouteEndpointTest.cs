using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session;
using Client.Tests.PlaceSystem.BeltRoute.World;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using System;
using Tests.Module.TestMod;
using UnityEngine;
using static Client.Tests.PlaceSystem.BeltRoute.BeltRouteConveyanceWorld;

namespace Client.Tests.PlaceSystem.BeltRoute.Rule
{
    // 端点（始点・終点）の種別と向きの規則を確かめる（ADR 0102）
    // Checks the rules for the endpoints' kinds and facings (ADR 0102)
    public class BeltRouteEndpointTest
    {
        // 始点の隣で1つ下の終点。終点が下り坂になれないと遠回りするしかない
        // An end beside the start and one level down; without a down-slope end the only option is a detour
        [Test]
        public void EndOneLevelBelowTheAdjacentStartBecomesADownSlope()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            var query = new BeltRouteQuery(new Vector3Int(3, 1, 2), BlockDirection.East, false, new Vector3Int(4, 0, 2), BlockDirection.East, true);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "down end");
            Assert.AreEqual(2, result.Plan.Cells.Count);
            Assert.AreEqual(BeltRouteCellKind.Down, result.Plan.Cells[1].Kind);
            Assert.AreEqual(BeltRouteCellKindExtension.StraightCost + BeltRouteCellKindExtension.SlopeCost, result.Plan.TotalCost);
        }

        // 終点の真上に既存ブロックがあると下り坂にできず、終点は直線のまま
        // With a block right above the end it cannot be a down slope, so the end stays straight
        [Test]
        public void EndUnderABlockStaysStraight()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            grid.MarkBlockCell(new Vector3Int(6, 1, 2));
            var query = new BeltRouteQuery(new Vector3Int(2, 1, 2), BlockDirection.East, false, new Vector3Int(6, 0, 2), BlockDirection.East, true);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "straight end under a block");
            Assert.AreEqual(BeltRouteCellKind.Straight, result.Plan.Cells[^1].Kind);
        }

        // 坂の無いファミリーでは終点も下り坂になれず、1つ下の終点へは経路なし
        // A family without slopes cannot make the end a down slope either, so there is no route to an end one level down
        [Test]
        public void NoRouteToTheLowerEndWithoutSlopes()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            var query = new BeltRouteQuery(new Vector3Int(3, 1, 2), BlockDirection.East, false, new Vector3Int(4, 0, 2), BlockDirection.East, false);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(query);

            Assert.IsFalse(result.HasPlan);
        }

        // (4,0,3)のブロックが南の面から(4,0,2)へ出力している。(4,0,2)から北の面（ブロックへ向かう向き=北）を狙う
        // A block at (4,0,3) outputs through its south face into (4,0,2); the north face is aimed from (4,0,2) (the heading into the block is north)
        private static BeltRouteGrid GridWithOutputPortFacingSouth()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            grid.MarkBlockCell(new Vector3Int(4, 0, 3));
            grid.AddOutputEdge(BeltRouteEdge.FromFace(new Vector3Int(4, 0, 3), BlockDirection.South, 0));
            return grid;
        }

        // 出力口の面。始点はブロックから離れる向き（南）、終点はキーの向き（東）
        // An output port face: the start heads away from the block (south), the end keeps the key's heading (east)
        [TestCase(BeltRouteEndpointRole.Start, BlockDirection.South)]
        [TestCase(BeltRouteEndpointRole.End, BlockDirection.East)]
        public void OutputPortFaceTurnsTheStartAwayAndLeavesTheEndToTheKey(BeltRouteEndpointRole role, BlockDirection expected)
        {
            var facing = BeltRouteEndpointFacing.Resolve(GridWithOutputPortFacingSouth(), new Vector3Int(4, 0, 2), BlockDirection.North, BlockDirection.East, role);
            Assert.AreEqual(expected, facing);
        }

        // 出力口でない面。始点はキーの向き、終点はブロックへ差し込む向き
        // A face without an output port: the start keeps the key's heading, the end heads into the block
        [TestCase(BeltRouteEndpointRole.Start, BlockDirection.East)]
        [TestCase(BeltRouteEndpointRole.End, BlockDirection.West)]
        public void PlainFaceKeepsTheFormerHeading(BeltRouteEndpointRole role, BlockDirection expected)
        {
            var facing = BeltRouteEndpointFacing.Resolve(GridWithOutputPortFacingSouth(), new Vector3Int(5, 0, 3), BlockDirection.West, BlockDirection.East, role);
            Assert.AreEqual(expected, facing);
        }

        // 1つ上の高さの出力口は、その下のセルの向きを変えない
        // An output port one level up does not change the heading of the cell below it
        [Test]
        public void OutputPortOneLevelUpIsIgnored()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            grid.MarkBlockCell(new Vector3Int(4, 0, 3));
            grid.MarkBlockCell(new Vector3Int(4, 1, 3));
            grid.AddOutputEdge(BeltRouteEdge.FromFace(new Vector3Int(4, 1, 3), BlockDirection.South, 1));
            var facing = BeltRouteEndpointFacing.Resolve(grid, new Vector3Int(4, 0, 2), BlockDirection.North, BlockDirection.East, BeltRouteEndpointRole.Start);
            Assert.AreEqual(BlockDirection.East, facing);
        }

        // 始点の隣で1つ下の終点は下り坂になり、荷物が終点の先のチェストへ届く
        // An end beside the start and one level down becomes a down slope, and the item reaches the chest beyond it
        [Test]
        public void DownSlopeEndConveysItemToTheLowerChest()
        {
            var world = BootWorld();
            var query = new BeltRouteQuery(new Vector3Int(0, 1, 0), BlockDirection.East, false, new Vector3Int(1, 0, 0), BlockDirection.East, true);
            var result = new BeltRoutePlanner(BuildGridFromWorld(world), BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, "no route");
            BeltRoutePlanVerifier.AssertValid(BuildGridFromWorld(world), query, result.Plan, "down end route");
            Assert.AreEqual(BeltRouteCellKind.Down, result.Plan.Cells[^1].Kind);

            var family = ForUnitTestModBlockId.GearBeltConveyor;
            foreach (var cell in result.Plan.Cells)
                Assert.IsTrue(world.TryAddBlock(ResolveFamilyBlock(family, cell.Kind), cell.Cell, cell.Facing, Array.Empty<BlockCreateParam>(), out _), $"placement failed at {cell}");
            // 歯車ベルトは回転が要るので、水平の始点の横に発電機を置く
            // Gear belts need rotation, so a generator goes beside the flat start
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 1, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            var source = PlaceChest(world, new Vector3Int(-1, 1, 0));
            var sink = PlaceChest(world, new Vector3Int(2, 0, 0));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(TestItemId, 1));

            Assert.IsTrue(TickUntilDelivered(sink, 1, null, 0), "item did not reach the lower chest");
        }
    }
}

using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Tests.PlaceSystem.BeltRoute.World;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 基本形（平地・坂・障害・分岐等）を個別に確認
    // Checks basic shapes (flat, slope, obstacle, branch) one by one
    public class BeltRoutePlannerBasicTest
    {
        private static BeltRouteGrid FlatGrid()
        {
            var grid = new BeltRouteGrid(Vector3Int.zero, new Vector3Int(16, 5, 16));
            for (var x = 0; x < 16; x++)
            for (var z = 0; z < 16; z++)
                grid.SetColumnFloor(x, z, 0);
            return grid;
        }

        internal static BeltRouteGrid FlatGrid(int size)
        {
            var grid = new BeltRouteGrid(Vector3Int.zero, new Vector3Int(size, 3, size));
            for (var x = 0; x < size; x++)
            for (var z = 0; z < size; z++)
                grid.SetColumnFloor(x, z, 0);
            return grid;
        }

        // 通常ベルト・坂ありの問い合わせ
        // A query for a normal belt with slopes
        private static BeltRouteQuery Query(Vector3Int start, BlockDirection startDirection, Vector3Int end, BlockDirection endDirection)
        {
            return new BeltRouteQuery(start, startDirection, false, end, endDirection, true, false);
        }

        [Test]
        public void StraightRunEastIsAllStraightCells()
        {
            var grid = FlatGrid();
            var query = Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(8, 0, 2), BlockDirection.East);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "straight");
            Assert.AreEqual(7, result.Plan.Cells.Count);
            Assert.IsTrue(result.Plan.Cells.All(cell => cell.Kind == BeltRouteCellKind.Straight && cell.Facing == BlockDirection.East));
            Assert.AreEqual(35, result.Plan.TotalCost);
        }

        [Test]
        public void ClimbUsesUpSlopesWithoutPeaks()
        {
            var grid = FlatGrid();
            var query = Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(9, 2, 2), BlockDirection.East);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "climb");
            Assert.AreEqual(2, result.Plan.Cells.Count(cell => cell.Kind == BeltRouteCellKind.Up));
            Assert.AreEqual(0, result.Plan.Cells.Count(cell => cell.Kind == BeltRouteCellKind.Down));
        }

        [Test]
        public void WallOfBlocksYieldsNoRoute()
        {
            var grid = FlatGrid();
            for (var z = 0; z < 16; z++)
            for (var y = 0; y < 5; y++)
                grid.MarkBlockCell(new Vector3Int(6, y, z));
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(10, 0, 2), BlockDirection.East));

            Assert.IsFalse(result.HasPlan);
        }

        [Test]
        public void TerrainRidgeYieldsNoRoute()
        {
            var grid = FlatGrid();
            for (var z = 0; z < 16; z++) grid.SetColumnFloor(6, z, 5);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(10, 0, 2), BlockDirection.East));

            Assert.IsFalse(result.HasPlan);
        }

        [Test]
        public void EndOutsideTheTestWorldYieldsNoRoute()
        {
            var result = new BeltRoutePlanner(FlatGrid(), BeltRoutePlanner.NoRecoverySlack).Plan(Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(40, 0, 2), BlockDirection.East));

            Assert.IsFalse(result.HasPlan);
        }

        [Test]
        public void CellInFrontOfEndIsNeverUsed()
        {
            var grid = FlatGrid();
            var query = Query(new Vector3Int(2, 0, 2), BlockDirection.North, new Vector3Int(2, 0, 6), BlockDirection.South);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "end front");
            Assert.IsFalse(result.Plan.Cells.Any(cell => cell.Cell == new Vector3Int(2, 0, 5)));
        }

        [Test]
        public void CellReceivingExistingOutputIsAvoided()
        {
            var grid = FlatGrid();
            // (4,0,1)の北向きベルトが(4,0,2)へ流している
            // A north-facing belt at (4,0,1) flows into (4,0,2)
            grid.MarkBlockCell(new Vector3Int(4, 0, 1));
            grid.AddOutputEdge(BeltRouteEdge.FromFace(new Vector3Int(4, 0, 1), BlockDirection.North, 0));
            // 坂なしの直線ベルトはその面を入口に持つので、横へ迂回するしかない
            // A flat straight belt owns that face as an input, so the only option is a sideways detour
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 2), BlockDirection.East, false, new Vector3Int(7, 0, 2), BlockDirection.East, false, false);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "existing output");
            Assert.IsFalse(result.Plan.Cells.Any(cell => cell.Cell == new Vector3Int(4, 0, 2)));
            Assert.LessOrEqual(result.Plan.Cells.Count, 8 * BeltRouteSearch.HeuristicWeight);
        }

        [Test]
        public void BranchStartsBesideTheExistingBelt()
        {
            var grid = FlatGrid();
            grid.MarkBlockCell(new Vector3Int(2, 0, 2));
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 2), BlockDirection.East, true, new Vector3Int(6, 0, 5), BlockDirection.North, true, false);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "branch");
            Assert.AreEqual(new Vector3Int(3, 0, 2), result.Plan.Cells[0].Cell);
        }

        [Test]
        public void SplitterHoldingTurnsOnTheExitSideAndStaysFlat()
        {
            var grid = FlatGrid();
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 2), BlockDirection.East, false, new Vector3Int(6, 0, 6), BlockDirection.North, false, true);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "splitter");
            Assert.AreEqual(BlockDirection.East, result.Plan.Cells[0].Facing);
            Assert.IsTrue(result.Plan.Cells.All(cell => cell.Kind == BeltRouteCellKind.Straight && cell.Cell.y == 0));
        }

        // 終点が始点の真上（と真上の1つ前）で逆向き。終点の真下を上りにする形が一番短いが、終点が自分の坂の真上に乗るので外へ回る
        // A reversed end directly above the start (and one ahead of it); rising right under the end is shortest, but the end would sit on the own slope head, so the route detours
        [TestCase(4, 1, 4, 37)]
        [TestCase(4, 1, 5, 42)]
        public void EndAboveStartDetoursInsteadOfRisingUnderTheEnd(int endX, int endY, int endZ, int expectedCost)
        {
            var grid = FlatGrid();
            var query = Query(new Vector3Int(4, 0, 4), BlockDirection.North, new Vector3Int(endX, endY, endZ), BlockDirection.South);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, $"end {query.EndCell}: no route");
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "end above start");
            Assert.IsFalse(result.Plan.Cells.Any(cell => cell.Kind.IsSlope() && cell.Cell + Vector3Int.up == query.EndCell), "a slope sits right under the end");
            Assert.LessOrEqual(result.Plan.TotalCost, expectedCost * BeltRouteSearch.HeuristicWeight);
        }

        // 旧箱超えの距離。平地は最短のまま
        // Beyond the old box; flat ground stays shortest
        [Test]
        public void LongOpenRunBeyondTheOldBoxIsStraightAndShortest()
        {
            var grid = FlatGrid(120);
            var query = Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(110, 0, 100), BlockDirection.North);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "long run");
            Assert.AreEqual(207, result.Plan.Cells.Count);
            Assert.AreEqual(207 * BeltRouteCellKindExtension.StraightCost, result.Plan.TotalCost);
            Assert.IsTrue(result.Plan.Cells.All(cell => cell.Kind == BeltRouteCellKind.Straight));
        }

        // 囲まれた終点は経路なし。本探索の前に終点の囲まれ判定で止まる
        // An enclosed end yields no route; it stops at the end-enclosure probe before the main search
        [Test]
        public void EnclosedEndYieldsNoRoute()
        {
            var grid = FlatGrid(40);
            for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
            for (var y = 0; y < 3; y++)
                if (dx != 0 || dz != 0) grid.MarkBlockCell(new Vector3Int(30 + dx, y, 30 + dz));
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(30, 0, 30), BlockDirection.East));

            Assert.IsFalse(result.HasPlan);
        }
    }
}

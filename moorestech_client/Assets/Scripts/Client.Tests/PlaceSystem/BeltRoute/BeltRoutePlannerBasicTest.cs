using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 平地・坂・障害・終点の前・既存出力口・分岐・分岐器手持ち・Cキーの切り替えを個別に確かめる
    // Checks flat runs, slopes, obstacles, the cell in front of the end, existing outputs, branches, splitter holding and the C-key cycle one by one
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

        // 通常ベルト・坂あり・優先順0の問い合わせ
        // A query for a normal belt with slopes and priority 0
        private static BeltRouteQuery Query(Vector3Int start, BlockDirection startDirection, Vector3Int end, BlockDirection endDirection)
        {
            return Query(start, startDirection, end, endDirection, 0);
        }

        private static BeltRouteQuery Query(Vector3Int start, BlockDirection startDirection, Vector3Int end, BlockDirection endDirection, int priority)
        {
            return new BeltRouteQuery(start, startDirection, false, end, endDirection, true, false, new BeltRouteStepPriority(priority));
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
        public void CyclingPriorityYieldsDifferentEqualCostRoutes()
        {
            var grid = FlatGrid();
            var planner = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack);
            var routes = Enumerable.Range(0, BeltRouteStepPriority.PermutationCount).Select(priority =>
            {
                var query = Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(7, 0, 7), BlockDirection.North, priority);
                var result = planner.Plan(query);
                Assert.IsTrue(result.HasPlan, $"priority {priority}");
                BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, $"priority {priority}");
                return string.Join("|", result.Plan.Cells.Select(cell => cell.Cell.ToString()));
            }).ToList();

            // 同コストで形の違う経路がCで出る
            // C yields routes of equal cost but different shape
            Assert.Greater(routes.Distinct().Count(), 1, "cycling priority never changed the route");
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
        public void WallOfBlocksIsReportedAsExistingBlockObstacle()
        {
            var grid = FlatGrid();
            for (var z = 0; z < 16; z++)
            for (var y = 0; y < 5; y++)
                grid.MarkBlockCell(new Vector3Int(6, y, z));
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(10, 0, 2), BlockDirection.East));

            Assert.IsFalse(result.HasPlan);
            Assert.AreEqual(BeltRouteNoPathReason.ExistingBlock, result.NoPathReason);
        }

        [Test]
        public void TerrainRidgeIsReportedAsTerrainObstacle()
        {
            var grid = FlatGrid();
            for (var z = 0; z < 16; z++) grid.SetColumnFloor(6, z, 5);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(10, 0, 2), BlockDirection.East));

            Assert.IsFalse(result.HasPlan);
            Assert.AreEqual(BeltRouteNoPathReason.Terrain, result.NoPathReason);
        }

        [Test]
        public void EndOutsideBoxIsOutOfRange()
        {
            var result = new BeltRoutePlanner(FlatGrid(), BeltRoutePlanner.NoRecoverySlack).Plan(Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(40, 0, 2), BlockDirection.East));

            Assert.IsFalse(result.HasPlan);
            Assert.AreEqual(BeltRouteNoPathReason.OutOfRange, result.NoPathReason);
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
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 2), BlockDirection.East, false, new Vector3Int(7, 0, 2), BlockDirection.East, false, false, BeltRouteStepPriority.Default);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "existing output");
            Assert.IsFalse(result.Plan.Cells.Any(cell => cell.Cell == new Vector3Int(4, 0, 2)));
            Assert.AreEqual(8, result.Plan.Cells.Count);
        }

        [Test]
        public void BranchStartsBesideTheExistingBelt()
        {
            var grid = FlatGrid();
            grid.MarkBlockCell(new Vector3Int(2, 0, 2));
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 2), BlockDirection.East, true, new Vector3Int(6, 0, 5), BlockDirection.North, true, false, BeltRouteStepPriority.Default);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "branch");
            Assert.AreEqual(new Vector3Int(3, 0, 2), result.Plan.Cells[0].Cell);
        }

        [Test]
        public void SplitterHoldingTurnsOnTheExitSideAndStaysFlat()
        {
            var grid = FlatGrid();
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 2), BlockDirection.East, false, new Vector3Int(6, 0, 6), BlockDirection.North, false, true, BeltRouteStepPriority.Default);
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
            Assert.IsTrue(result.HasPlan, $"end {query.EndCell}: {result.NoPathReason}");
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "end above start");
            Assert.IsFalse(result.Plan.Cells.Any(cell => cell.Kind.IsSlope() && cell.Cell + Vector3Int.up == query.EndCell), "a slope sits right under the end");
            Assert.AreEqual(expectedCost, result.Plan.TotalCost);
        }

        [Test]
        public void ReplacedGridWithSameBoxIsAccepted()
        {
            var planner = new BeltRoutePlanner(FlatGrid(), BeltRoutePlanner.NoRecoverySlack);
            var replaced = FlatGrid();
            replaced.MarkBlockCell(new Vector3Int(5, 0, 2));
            planner.ReplaceGrid(replaced);

            var query = Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(8, 0, 2), BlockDirection.East);
            var result = planner.Plan(query);
            Assert.IsTrue(result.HasPlan);
            Assert.IsFalse(result.Plan.Cells.Any(cell => cell.Cell == new Vector3Int(5, 0, 2)));
        }
    }
}

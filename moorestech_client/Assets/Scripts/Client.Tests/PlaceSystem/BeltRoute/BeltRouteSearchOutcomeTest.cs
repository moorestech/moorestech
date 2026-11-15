using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Tests.PlaceSystem.BeltRoute.World;
using Game.Block.Interface;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 探索の終わり方（到達・枯渇・上限）を区別して守る
    // Guards the distinct ways a search ends: reached, exhausted and cap reached
    public class BeltRouteSearchOutcomeTest
    {
        private static BeltRouteSearch RunSearch(BeltRouteGrid grid, Vector3Int end, int maxExpansions, out BeltRouteSearchOutcome outcome)
        {
            var start = new Vector3Int(2, 0, 2);
            var query = new BeltRouteQuery(start, BlockDirection.East, false, end, BlockDirection.East, true, false);
            var starts = new[] { (new BeltRouteState(start, BlockDirection.East, BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost) };
            var search = new BeltRouteSearch();
            outcome = search.Run(new BeltRouteTransitionRule(grid, query), starts, maxExpansions);
            return search;
        }

        [Test]
        public void CapStopsTheSearchBeforeAFarEndIsReached()
        {
            var search = RunSearch(BeltRoutePlannerBasicTest.FlatGrid(40), new Vector3Int(35, 0, 35), 10, out var outcome);

            Assert.AreEqual(BeltRouteSearchOutcome.CapReached, outcome);
            Assert.AreEqual(10, search.Expansions);
        }

        [Test]
        public void ClosedWorldWithAnEnclosedEndExhaustsTheQueue()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(10);
            for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
            for (var y = 0; y < 3; y++)
                if (dx != 0 || dz != 0) grid.MarkBlockCell(new Vector3Int(6 + dx, y, 6 + dz));
            var search = RunSearch(grid, new Vector3Int(6, 0, 6), 1000000, out var outcome);

            Assert.AreEqual(BeltRouteSearchOutcome.Exhausted, outcome);
            Assert.Less(search.Expansions, 1000000);
        }

        // 壁の向こうの終点は囲まれ判定を抜け、本番の Planner が探索量の上限で経路なしにする
        // An end beyond a wall passes the enclosure probe, and the production planner gives up at the expansion cap
        [Test]
        public void PlannerGivesUpAtTheExpansionCapBehindAWall()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(200);
            for (var z = 0; z < 200; z++)
            for (var y = 0; y < 3; y++)
                grid.MarkBlockCell(new Vector3Int(100, y, z));
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 2), BlockDirection.East, false, new Vector3Int(190, 0, 190), BlockDirection.East, true, false);

            LogAssert.Expect(LogType.Log, new Regex($"CapReached expansions:{BeltRoutePlanner.MaxExpansions}"));
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(query);

            Assert.IsFalse(result.HasPlan);
        }

        [Test]
        public void ReachableEndReportsReached()
        {
            RunSearch(BeltRoutePlannerBasicTest.FlatGrid(10), new Vector3Int(7, 0, 7), 1000000, out var outcome);

            Assert.AreEqual(BeltRouteSearchOutcome.Reached, outcome);
        }
    }
}

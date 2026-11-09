using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Tests.PlaceSystem.BeltRoute.World;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 探索の終わり方（到達・枯渇・上限）を区別して守る
    // Guards the distinct ways a search ends: reached, exhausted and cap reached
    public class BeltRouteSearchOutcomeTest
    {
        private static BeltRouteGrid FlatGrid(int size)
        {
            var grid = new BeltRouteGrid(Vector3Int.zero, new Vector3Int(size, 3, size));
            for (var x = 0; x < size; x++)
            for (var z = 0; z < size; z++)
                grid.SetColumnFloor(x, z, 0);
            return grid;
        }

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
            var search = RunSearch(FlatGrid(40), new Vector3Int(35, 0, 35), 10, out var outcome);

            Assert.AreEqual(BeltRouteSearchOutcome.CapReached, outcome);
            Assert.AreEqual(10, search.Expansions);
        }

        [Test]
        public void ClosedWorldWithAnEnclosedEndExhaustsTheQueue()
        {
            var grid = FlatGrid(10);
            for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
            for (var y = 0; y < 3; y++)
                if (dx != 0 || dz != 0) grid.MarkBlockCell(new Vector3Int(6 + dx, y, 6 + dz));
            var search = RunSearch(grid, new Vector3Int(6, 0, 6), 1000000, out var outcome);

            Assert.AreEqual(BeltRouteSearchOutcome.Exhausted, outcome);
            Assert.Less(search.Expansions, 1000000);
        }

        [Test]
        public void ReachableEndReportsReached()
        {
            RunSearch(FlatGrid(10), new Vector3Int(7, 0, 7), 1000000, out var outcome);

            Assert.AreEqual(BeltRouteSearchOutcome.Reached, outcome);
        }
    }
}

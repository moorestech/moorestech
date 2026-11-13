using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Tests.PlaceSystem.BeltRoute.World;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 組み上がった経路が仕様の不変条件（重複なし・坂の真上なし・終点の前なし・山谷なし・隣接接続）を満たすか検査する
    // Checks an assembled route against the spec's invariants: no duplicate cells, nothing above own slopes, no cell in front of the end, no peaks or valleys, adjacent cells connected
    public static class BeltRoutePlanVerifier
    {
        public static void AssertValid(BeltRouteGrid grid, BeltRouteQuery query, BeltRoutePlan plan, string label)
        {
            var cells = plan.Cells;
            Assert.Greater(cells.Count, 0, $"{label}: empty plan");

            var expectedFirst = query.StartIsExistingBelt ? query.StartCell + BeltRouteDirection.Forward(query.StartDirection) : query.StartCell;
            Assert.AreEqual(expectedFirst.x, cells[0].Cell.x, $"{label}: first cell x");
            Assert.AreEqual(expectedFirst.z, cells[0].Cell.z, $"{label}: first cell z");
            if (!query.StartIsExistingBelt) Assert.AreEqual(query.StartCell, cells[0].Cell, $"{label}: first cell");
            Assert.AreEqual(query.EndCell, cells[^1].Cell, $"{label}: last cell");
            Assert.AreEqual(query.EndDirection, cells[^1].Facing, $"{label}: last facing");
            Assert.AreEqual(BeltRouteCellKind.Straight, cells[^1].Kind, $"{label}: last kind");

            var rule = new BeltRouteTransitionRule(grid, query);
            var used = new HashSet<Vector3Int>();
            var slopeHeads = new HashSet<Vector3Int>();
            var totalCost = 0;
            foreach (var cell in cells)
            {
                Assert.IsTrue(used.Add(cell.Cell), $"{label}: duplicate cell {cell.Cell}");
                Assert.IsTrue(rule.IsCellFree(cell.Cell), $"{label}: cell on obstacle {cell.Cell}");
                if (cell.Kind.IsSlope()) slopeHeads.Add(cell.Cell + Vector3Int.up);
                totalCost += cell.Kind.Cost();
            }
            Assert.AreEqual(plan.TotalCost, totalCost, $"{label}: total cost");
            foreach (var cell in cells) Assert.IsFalse(slopeHeads.Contains(cell.Cell), $"{label}: cell above own slope {cell.Cell}");
            // 終点の前のセルが始点なら、終点が始点へ流し込む輪になるだけで許す（ユーザー裁定 2026-11-07）
            // The cell in front of the end may be the start; the end then feeds the start and the route forms a ring, which is allowed (user decision 2026-11-07)
            var endFront = query.EndCell + BeltRouteDirection.Forward(query.EndDirection);
            Assert.IsTrue(!used.Contains(endFront) || endFront == query.StartCell, $"{label}: cell in front of the end is used");

            // 状態列へ戻して隣接の接続規則を確かめる
            // Rebuild the state sequence and check the adjacency connection rules
            var states = ToStates(query, cells);
            for (var i = 1; i < states.Count; i++)
            {
                var previous = states[i - 1];
                var next = states[i];
                Assert.IsTrue(next.Kind.CanFollow(previous.Kind), $"{label}: peak or valley at {next.Cell}");
                Assert.IsTrue(rule.IsConnected(previous, next), $"{label}: {previous} -> {next} not connected");
                var isGoal = i == states.Count - 1;
                if (isGoal) Assert.IsTrue(rule.CanEnterGoal(previous), $"{label}: goal not enterable from {previous}");
                else Assert.IsTrue(rule.IsIntermediatePlaceable(next.Cell, next.Kind, previous.Outgoing, next.Outgoing), $"{label}: {next} not placeable mid-route");
            }

            // 分岐の始点（既存ベルト）からも最初のセルへ繋がること
            // The first cell must also connect from a branch's existing start belt
            if (query.StartIsExistingBelt)
            {
                var virtualStart = new BeltRouteState(query.StartCell, query.StartDirection, BeltRouteCellKind.Straight);
                Assert.IsTrue(rule.IsConnected(virtualStart, states[0]), $"{label}: first cell not connected to the branch belt");
            }
        }

        // 出る向きは次セルへの差分。分岐器は向きが入る側なので差分から復元し、通常ベルトは向きそのもの
        // The exit heading is the delta to the next cell; a splitter faces its entry so it is recovered from the delta, a normal belt faces its exit
        private static List<BeltRouteState> ToStates(BeltRouteQuery query, IReadOnlyList<BeltRouteCell> cells)
        {
            var states = new List<BeltRouteState>(cells.Count);
            for (var i = 0; i < cells.Count; i++)
            {
                var outgoing = i + 1 < cells.Count ? BeltRouteDirection.FromStep(cells[i + 1].Cell - cells[i].Cell) : query.EndDirection;
                if (!query.TurnByOutput) Assert.AreEqual(outgoing, cells[i].Facing, $"belt facing must equal its exit heading at {cells[i].Cell}");
                else Assert.AreEqual(i == 0 ? query.StartDirection : BeltRouteDirection.FromStep(cells[i].Cell - cells[i - 1].Cell), cells[i].Facing, $"splitter facing must equal its entry heading at {cells[i].Cell}");
                states.Add(new BeltRouteState(cells[i].Cell, outgoing, cells[i].Kind));
            }
            return states;
        }
    }
}

using System.Diagnostics;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 組み立て（終点から戻る深さ優先）が最悪になる配置を意図して作り、本番の Plan の時間を余裕なし・ありで測る
    // Builds layouts that drive the recovery (depth-first walk back from the end) to its worst and times the production Plan with and without slack
    // 形: 始点は囲いの中で、唯一の出口が幅1の廊下。廊下の先で向きを変えて廊下の上段（高さ1）を戻り、囲いの中の広場を渡って終点へ入る
    // Shape: the start sits inside an enclosure whose only exit is a one-wide corridor; the route turns around beyond it, returns on the corridor's upper level and crosses the enclosure's plaza to the end
    // A* は廊下のセルを高さ0で使い回す経路を最短と見るが組めず、戻る走査は広場の同コスト経路を全部数えてから諦める
    // A* takes the route that reuses the corridor cells at height 0 as shortest, which cannot be assembled, so the walk back enumerates every equal-cost plaza path before giving up
    public class BeltRouteRecoveryWorstCaseTest
    {
        private const int Runs = 3;
        private const int RecoverySlackUnderTest = 20;
        private static readonly Vector3Int BoxSize = new(BeltRouteGrid.HorizontalSize, 6, BeltRouteGrid.HorizontalSize);
        private static readonly Vector3Int Start = new(20, 0, 28);
        private static readonly Vector3Int End = new(40, 1, 10);
        private const int CorridorX = 20;
        private const int WallZ = 29;
        private const int CorridorEndZ = 37;

        // 廊下の先が開けた広場: 2セル余計に使えば組める（坂を戻り道から外す）。余裕ありなら予算内で見つかる
        // An open plaza beyond the corridor: assemblable with two extra cells (the slope moves off the return path); with slack it is found within the budget
        [TestCase(BeltRoutePlanner.NoRecoverySlack, false)]
        [TestCase(RecoverySlackUnderTest, true)]
        public void OpenPlazaBeyondCorridor(int recoverySlack, bool expectFound)
        {
            var (result, searchCost) = Measure(BuildCompound(true), recoverySlack, "open plaza");
            Assert.AreEqual(expectFound, result.HasPlan);
            if (expectFound) Assert.That(result.Plan.TotalCost, Is.GreaterThan(searchCost).And.LessThanOrEqualTo(searchCost + recoverySlack));
        }

        // 廊下の先が2×2の袋小路と幅1の筋の先の3×3: 組めるのは遥か先で向きを変える経路だけで、余裕20には収まらない
        // A 2x2 pocket beyond the corridor plus a one-wide lane to a 3x3 patch: the only assemblable route turns around far away, beyond a slack of 20
        [TestCase(BeltRoutePlanner.NoRecoverySlack)]
        [TestCase(RecoverySlackUnderTest)]
        public void PocketBeyondCorridor(int recoverySlack)
        {
            var (result, _) = Measure(BuildCompound(false), recoverySlack, "pocket");
            Assert.IsFalse(result.HasPlan);
        }

        // 本番の大きさの箱でランダムな障害と近接した終点を振り、余裕なし・ありの最悪時間を比べる
        // Random obstacles and near ends in a production-sized box, comparing the worst time with and without slack
        [TestCase(BeltRoutePlanner.NoRecoverySlack)]
        [TestCase(RecoverySlackUnderTest)]
        public void RandomLargeBoxesWithNearEnds(int recoverySlack)
        {
            var random = new System.Random(20261107);
            var stopwatch = new Stopwatch();
            var worst = 0.0;
            var found = 0;
            var compared = 0;
            for (var i = 0; i < 200; i++)
            {
                var grid = BeltRouteRandomizedSearchTest.BuildRandomGrid(random, new Vector3Int(BeltRouteGrid.HorizontalSize, 12, BeltRouteGrid.HorizontalSize));
                if (!BeltRouteReferenceCrossCheckTest.TryPickQuery(random, grid, out var query)) continue;
                compared++;
                var planner = new BeltRoutePlanner(grid, recoverySlack);
                stopwatch.Restart();
                var result = planner.Plan(query);
                stopwatch.Stop();
                if (result.HasPlan) found++;
                worst = System.Math.Max(worst, stopwatch.Elapsed.TotalMilliseconds);
            }
            Debug.Log($"[BeltRouteRecoveryWorstCase] random large boxes slack {recoverySlack}: worst {worst:F1} ms over {compared} queries, found {found}");
        }

        private static (BeltRoutePlanResult result, int searchCost) Measure(BeltRouteGrid grid, int recoverySlack, string label)
        {
            var query = new BeltRouteQuery(Start, BlockDirection.North, false, End, BlockDirection.South, true, false, BeltRouteStepPriority.Default);
            var rule = new BeltRouteTransitionRule(grid, query, BeltRouteObstacleMask.None);

            // 配置が狙いどおりか: A* 単体は終点へ届く
            // Sanity of the layout: A* alone reaches the end
            var search = new BeltRouteSearch(new BeltRouteStateCodec(grid));
            Assert.IsTrue(search.Run(rule, BeltRouteReferenceSolver.StartStates(rule)), $"{label}: A* must reach the end for this layout to measure recovery");
            var searchCost = search.BestCost(new BeltRouteState(End, BlockDirection.South, BeltRouteCellKind.Straight));

            var planner = new BeltRoutePlanner(grid, recoverySlack);
            var stopwatch = new Stopwatch();
            var worst = 0.0;
            BeltRoutePlanResult result = null;
            for (var run = 0; run < Runs; run++)
            {
                stopwatch.Restart();
                result = planner.Plan(query);
                stopwatch.Stop();
                worst = System.Math.Max(worst, stopwatch.Elapsed.TotalMilliseconds);
            }
            var outcome = result.HasPlan ? $"found cost {result.Plan.TotalCost}" : $"no route ({result.NoPathReason})";
            Debug.Log($"[BeltRouteRecoveryWorstCase] {label} slack {recoverySlack}: worst {worst:F1} ms over {Runs} runs, A* goal cost {searchCost}, plan {outcome}");
            if (result.HasPlan) BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, label);
            return (result, searchCost);
        }

        private static BeltRouteGrid BuildCompound(bool openPlaza)
        {
            var grid = new BeltRouteGrid(Vector3Int.zero, BoxSize);
            for (var x = 0; x < BoxSize.x; x++)
            for (var z = 0; z < BoxSize.z; z++)
                grid.SetColumnFloor(x, z, 0);

            // 囲いの北壁（廊下の入口だけ開ける）と、廊下の両脇の壁
            // The enclosure's north wall (open only at the corridor mouth) and the walls on both sides of the corridor
            for (var x = 0; x < BoxSize.x; x++)
                if (x != CorridorX) FillColumn(grid, x, WallZ);
            for (var z = WallZ; z <= CorridorEndZ; z++)
            {
                FillColumn(grid, CorridorX - 1, z);
                FillColumn(grid, CorridorX + 1, z);
                // 廊下の天井: 高さ0と1だけ通れる
                // Corridor ceiling: only heights 0 and 1 are passable
                for (var y = 2; y < BoxSize.y; y++) grid.MarkBlockCell(new Vector3Int(CorridorX, y, z));
            }
            if (openPlaza) return grid;

            // 廊下の先を全部塞ぎ、2×2の袋小路・幅1の筋・3×3の広場だけ空ける
            // Block everything beyond the corridor except a 2x2 pocket, a one-wide lane and a 3x3 patch
            for (var x = 0; x < BoxSize.x; x++)
            for (var z = CorridorEndZ + 1; z < BoxSize.z; z++)
            {
                var pocket = (x == CorridorX || x == CorridorX + 1) && z <= CorridorEndZ + 2;
                var lane = x == CorridorX && z <= CorridorEndZ + 13;
                var patch = CorridorX - 1 <= x && x <= CorridorX + 1 && CorridorEndZ + 14 <= z && z <= CorridorEndZ + 16;
                if (!pocket && !lane && !patch) FillColumn(grid, x, z);
            }
            return grid;
        }

        private static void FillColumn(BeltRouteGrid grid, int x, int z)
        {
            for (var y = 0; y < BoxSize.y; y++) grid.MarkBlockCell(new Vector3Int(x, y, z));
        }
    }
}

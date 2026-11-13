using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Tests.PlaceSystem.BeltRoute.World;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 本番探索を総当たり参照と突き合わせる（存在とコスト上限）
    // Cross-checks production search against the exhaustive reference (existence, cost bound)
    // 参照が見つけたら本番も見つける。コストは参照×W＋余裕以内
    // When the reference finds a route production must too, within W times the reference plus slack
    // ランダム試験は見つけた経路の形だけを検査し「経路なし」は素通りするので、存在はここで守る
    // The randomized test only inspects found routes and lets "no route" pass, so existence is guarded here
    public class BeltRouteReferenceCrossCheckTest
    {
        // 回数は PR 追加テスト全体で 1 秒に収める裁定（2026-11-07）に合わせる
        // The count follows the decision (2026-11-07) to keep the PR's tests within one second in total
        private const int Iterations = 100;
        private const int Seed = 20261107;
        private const int ReportedDisagreements = 12;

        // 参照ソルバは総当たりなので箱とコスト上限を小さく保つ（上限50＝10セル）
        // The reference solver is exhaustive, so the box and the cost cap stay small (cap 50 = 10 cells)
        private const int CostCap = 50;

        // 本番と同じ余裕（生成と上限で共用）
        // Production slack (shared by construction and bound)
        private const int RecoverySlack = BeltRoutePlanner.DefaultRecoverySlack;
        private static readonly Vector3Int BoxSize = new(6, 3, 6);

        // 始点の真上や隣など近接した終点は探索と組み立ての食い違いが出やすいので、半分は始点の±2以内に終点を置く
        // Ends right above or beside the start are where search and recovery disagree most, so half the ends land within ±2 of the start
        private const int NearRange = 2;

        [Test]
        public void RandomSmallBoxesAgreeWithTheExhaustiveReference()
        {
            var random = new System.Random(Seed);
            var compared = 0;
            var referenceFound = 0;
            var disagreements = new List<string>();
            for (var i = 0; i < Iterations; i++)
            {
                var grid = BeltRouteRandomizedSearchTest.BuildRandomGrid(random, BoxSize);
                if (!TryPickQuery(random, grid, out var query)) continue;
                compared++;
                var label = $"iteration {i} query {query.StartCell}:{query.StartDirection} -> {query.EndCell}:{query.EndDirection} slopes:{query.AllowSlopes} splitter:{query.TurnByOutput} branch:{query.StartIsExistingBelt}";

                var result = new BeltRoutePlanner(grid, RecoverySlack).Plan(query);
                var rule = new BeltRouteTransitionRule(grid, query);
                var referenceCost = new BeltRouteReferenceSolver(rule, CostCap).Solve();
                if (referenceCost != BeltRouteReferenceSolver.NotFound) referenceFound++;
                if (result.HasPlan) BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, label);
                var disagreement = Disagreement(result, referenceCost);
                if (disagreement != null) disagreements.Add($"{label}: {disagreement}");
            }
            Debug.Log($"[BeltRouteReferenceCrossCheck] slack {RecoverySlack}: compared {compared} queries, reference found {referenceFound} routes within cost {CostCap}, disagreements {disagreements.Count}");
            Assert.Greater(referenceFound, Iterations / 10, "too few routes within the cap; the cross-check is not exercising the search");
            Assert.AreEqual(0, disagreements.Count, $"{disagreements.Count} disagreements, first {ReportedDisagreements}:\n{string.Join("\n", disagreements.GetRange(0, Mathf.Min(ReportedDisagreements, disagreements.Count)))}");
        }

        // 本番が参照を見落とさずコスト上限内か検査。一致ならnull
        // Checks production misses no reference route within the cost bound; null if agree
        private static string Disagreement(BeltRoutePlanResult result, int referenceCost)
        {
            if (referenceCost == BeltRouteReferenceSolver.NotFound)
            {
                if (result.HasPlan && result.Plan.TotalCost <= CostCap) return $"production found cost {result.Plan.TotalCost} but the reference found nothing within the cap";
                return null;
            }
            if (!result.HasPlan) return $"reference found a route of cost {referenceCost} but production found none";
            var bound = referenceCost * BeltRouteSearch.HeuristicWeight + RecoverySlack;
            if (bound < result.Plan.TotalCost) return $"production cost {result.Plan.TotalCost} exceeds the bound {bound} (reference {referenceCost})";
            return null;
        }

        // 終点の半分を始点の±NearRange以内に置く問い合わせ
        // A query whose end lands within ±NearRange of the start half of the time
        internal static bool TryPickQuery(System.Random random, BeltRouteGrid grid, out BeltRouteQuery query)
        {
            query = null;
            if (!TryPickFreeCell(random, grid, Vector3Int.zero, grid.Size, out var start)) return false;
            var nearMin = Vector3Int.Max(Vector3Int.zero, start - Vector3Int.one * NearRange);
            var nearMax = Vector3Int.Min(grid.Size, start + Vector3Int.one * (NearRange + 1));
            var near = random.Next(2) == 0;
            if (!TryPickFreeCell(random, grid, near ? nearMin : Vector3Int.zero, near ? nearMax : grid.Size, out var end) || start == end) return false;

            var startIsExistingBelt = random.Next(10) == 0;
            var turnByOutput = !startIsExistingBelt && random.Next(5) == 0;
            var allowSlopes = !turnByOutput && random.Next(5) != 0;
            var startDirection = (BlockDirection)random.Next(4);
            var endDirection = (BlockDirection)random.Next(4);
            if (startIsExistingBelt) grid.MarkBlockCell(start);
            query = new BeltRouteQuery(start, startDirection, startIsExistingBelt, end, endDirection, allowSlopes, turnByOutput);
            return true;
        }

        // [min, max) の範囲から空きセルを引く
        // Draws a free cell from the half-open range [min, max)
        private static bool TryPickFreeCell(System.Random random, BeltRouteGrid grid, Vector3Int min, Vector3Int max, out Vector3Int cell)
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                cell = new Vector3Int(random.Next(min.x, max.x), random.Next(min.y, max.y), random.Next(min.z, max.z));
                if (!grid.IsBlockedByBlock(cell) && !grid.IsBlockedByTerrain(cell) && !grid.IsSlopeHead(cell)) return true;
            }
            cell = default;
            return false;
        }
    }
}

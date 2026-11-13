using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 本番の探索を総当たりの参照ソルバと突き合わせる。経路があるのに見つけない・最短でない・無いのに見つける、の3つを捕まえる
    // Cross-checks the production search against the exhaustive reference solver, catching missed routes, non-shortest routes and routes that should not exist
    // 見つけた経路の形だけを検査するランダム試験では「経路なし」が常に通ってしまうので、存在と最短性はここで守る
    // The randomized test only inspects found routes, so "no route" always passes there; existence and optimality are guarded here
    // 余裕なし（ParamegaFactory と同じ）では既知の穴（最短が自分の坂の真上を通る形しか無く組めない）だけを数えて許し、余裕ありでは穴ゼロを要求する
    // Without slack (as ParamegaFactory) only the known gap (the shortest exists solely through an own slope head) is counted and tolerated; with slack zero gaps are required
    public class BeltRouteReferenceCrossCheckTest
    {
        // 回数は PR 追加テスト全体で 1 秒に収める裁定（2026-11-07）に合わせる
        // The count follows the decision (2026-11-07) to keep the PR's tests within one second in total
        private const int Iterations = 100;
        private const int Seed = 20261107;
        private const int ReportedDisagreements = 12;

        // 余裕なしの既知の穴の上限。100問で1件（iteration 45）が実測（2026-11-07）。増えたら探索か組み立ての退行を疑う
        // Cap on the known gap without slack: 1 of 100 queries (iteration 45) measured on 2026-11-07; growth points at a search or recovery regression
        private const int KnownGapAllowed = 3;

        // 参照ソルバは総当たりなので箱とコスト上限を小さく保つ（上限50＝10セル）
        // The reference solver is exhaustive, so the box and the cost cap stay small (cap 50 = 10 cells)
        private const int CostCap = 50;
        private static readonly Vector3Int BoxSize = new(6, 3, 6);

        // 始点の真上や隣など近接した終点は探索と組み立ての食い違いが出やすいので、半分は始点の±2以内に終点を置く
        // Ends right above or beside the start are where search and recovery disagree most, so half the ends land within ±2 of the start
        private const int NearRange = 2;

        [TestCase(BeltRoutePlanner.NoRecoverySlack, false)]
        [TestCase(BeltRoutePlanner.DefaultRecoverySlack, true)]
        public void RandomSmallBoxesAgreeWithTheExhaustiveReference(int recoverySlack, bool expectNoKnownGap)
        {
            var random = new System.Random(Seed);
            var compared = 0;
            var referenceFound = 0;
            var knownGaps = new List<string>();
            var longerThanReference = new List<string>();
            var disagreements = new List<string>();
            for (var i = 0; i < Iterations; i++)
            {
                var grid = BeltRouteRandomizedSearchTest.BuildRandomGrid(random, BoxSize);
                if (!TryPickQuery(random, grid, out var query)) continue;
                compared++;
                var label = $"iteration {i} query {query.StartCell}:{query.StartDirection} -> {query.EndCell}:{query.EndDirection} slopes:{query.AllowSlopes} splitter:{query.TurnByOutput} branch:{query.StartIsExistingBelt}";

                var result = new BeltRoutePlanner(grid, recoverySlack).Plan(query);
                var rule = new BeltRouteTransitionRule(grid, query, BeltRouteObstacleMask.None);
                var referenceCost = new BeltRouteReferenceSolver(rule, CostCap, true).Solve();
                if (referenceCost != BeltRouteReferenceSolver.NotFound) referenceFound++;
                if (result.HasPlan) BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, label);
                var disagreement = Disagreement(result, referenceCost);
                if (disagreement == null) continue;
                if (IsLongerWithinSlack(result, referenceCost, recoverySlack)) longerThanReference.Add($"{label}: {disagreement}");
                else if (IsKnownGap(grid, rule, result, referenceCost)) knownGaps.Add($"{label}: {disagreement}");
                else disagreements.Add($"{label}: {disagreement}");
            }
            Debug.Log($"[BeltRouteReferenceCrossCheck] slack {recoverySlack}: compared {compared} queries, reference found {referenceFound} routes within cost {CostCap}, known gaps {knownGaps.Count}, longer than reference within slack {longerThanReference.Count}, disagreements {disagreements.Count}\n{string.Join("\n", knownGaps)}\n{string.Join("\n", longerThanReference)}");
            Assert.Greater(referenceFound, Iterations / 10, "too few routes within the cap; the cross-check is not exercising the search");
            Assert.AreEqual(0, disagreements.Count, $"{disagreements.Count} disagreements, first {ReportedDisagreements}:\n{string.Join("\n", disagreements.GetRange(0, Mathf.Min(ReportedDisagreements, disagreements.Count)))}");
            if (expectNoKnownGap) Assert.AreEqual(0, knownGaps.Count, $"known gaps must vanish with slack:\n{string.Join("\n", knownGaps)}");
            else Assert.That(knownGaps.Count, Is.GreaterThan(0).And.LessThanOrEqualTo(KnownGapAllowed), $"known gaps without slack must stay visible and bounded:\n{string.Join("\n", knownGaps)}");
        }

        // 参照が見つけたら本番も同じコストで見つける。参照が上限内に見つけないなら本番も上限内の経路を出さない。一致ならnull
        // When the reference finds a route the production must find one of equal cost; when it finds none within the cap, production must not produce one within the cap; null when they agree
        private static string Disagreement(BeltRoutePlanResult result, int referenceCost)
        {
            if (referenceCost == BeltRouteReferenceSolver.NotFound)
            {
                if (result.HasPlan && result.Plan.TotalCost <= CostCap) return $"production found cost {result.Plan.TotalCost} but the reference found nothing within the cap";
                return null;
            }
            if (!result.HasPlan) return $"reference found a route of cost {referenceCost} but production reported {result.NoPathReason}";
            if (result.Plan.TotalCost != referenceCost) return $"production cost {result.Plan.TotalCost} differs from reference {referenceCost}";
            return null;
        }

        // 余裕ありは最短で組めないとき予算内の経路を返すので、参照より長くても予算内なら食い違いではない
        // With slack, production returns a route within the budget when the shortest cannot be assembled, so longer than the reference but within the slack is not a disagreement
        private static bool IsLongerWithinSlack(BeltRoutePlanResult result, int referenceCost, int recoverySlack)
        {
            if (!result.HasPlan || referenceCost == BeltRouteReferenceSolver.NotFound) return false;
            return referenceCost < result.Plan.TotalCost && result.Plan.TotalCost <= referenceCost + recoverySlack;
        }

        // 既知の穴＝本番は経路なしで、A* の終点コストが「規則だけの最短」と一致し、それが組める最短より小さい（組み立てだけが弾いた）
        // The known gap: production found no route, the A* goal cost equals the rule-only shortest, and that is below the assemblable shortest (only recovery rejected it)
        private static bool IsKnownGap(BeltRouteGrid grid, BeltRouteTransitionRule rule, BeltRoutePlanResult result, int referenceCost)
        {
            if (result.HasPlan || referenceCost == BeltRouteReferenceSolver.NotFound) return false;
            var search = new BeltRouteSearch(new BeltRouteStateCodec(grid));
            if (!search.Run(rule, BeltRouteReferenceSolver.StartStates(rule))) return false;
            var searchCost = search.BestCost(new BeltRouteState(rule.Query.EndCell, rule.Query.EndDirection, BeltRouteCellKind.Straight));
            var ruleOnlyCost = new BeltRouteReferenceSolver(rule, CostCap, false).Solve();
            return searchCost == ruleOnlyCost && ruleOnlyCost < referenceCost;
        }

        // 終点の半分を始点の±NearRange以内に置く問い合わせ。性能試験からも使う
        // A query whose end lands within ±NearRange of the start half of the time; the performance tests use it too
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
            var priority = new BeltRouteStepPriority(random.Next(BeltRouteStepPriority.PermutationCount));
            if (startIsExistingBelt) grid.MarkBlockCell(start);
            query = new BeltRouteQuery(start, startDirection, startIsExistingBelt, end, endDirection, allowSlopes, turnByOutput, priority);
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

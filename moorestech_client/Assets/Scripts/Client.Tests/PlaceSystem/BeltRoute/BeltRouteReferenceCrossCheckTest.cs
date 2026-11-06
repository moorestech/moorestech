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
    // 既知の穴（最短コストが自分の坂の真上を通る形しか無く組めない。ParamegaFactory と同じく経路なしのまま）だけは数えて許し、それ以外の食い違いで落とす
    // Only the known gap (the shortest cost exists solely through an own slope head and cannot be assembled; left as no route like ParamegaFactory) is counted and tolerated; any other disagreement fails
    public class BeltRouteReferenceCrossCheckTest
    {
        private const int Iterations = 1500;
        private const int Seed = 20261107;
        private const int ReportedDisagreements = 12;

        // 既知の穴の上限。1461問で5件が実測（2026-11-06）。増えたら探索か組み立ての退行を疑う
        // Cap on the known gap: 5 of 1461 queries measured on 2026-11-06; growth points at a search or recovery regression
        private const int KnownGapAllowed = 10;

        // 参照ソルバは総当たりなので箱とコスト上限を小さく保つ（上限50＝10セル）
        // The reference solver is exhaustive, so the box and the cost cap stay small (cap 50 = 10 cells)
        private const int CostCap = 50;
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
            var knownGaps = new List<string>();
            var disagreements = new List<string>();
            for (var i = 0; i < Iterations; i++)
            {
                var grid = BeltRouteRandomizedSearchTest.BuildRandomGrid(random, BoxSize);
                if (!TryPickQuery(random, grid, out var query)) continue;
                compared++;
                var label = $"iteration {i} query {query.StartCell}:{query.StartDirection} -> {query.EndCell}:{query.EndDirection} slopes:{query.AllowSlopes} splitter:{query.TurnByOutput} branch:{query.StartIsExistingBelt}";

                var result = new BeltRoutePlanner(grid).Plan(query);
                var rule = new BeltRouteTransitionRule(grid, query, BeltRouteObstacleMask.None);
                var referenceCost = new BeltRouteReferenceSolver(rule, CostCap, true).Solve();
                if (referenceCost != BeltRouteReferenceSolver.NotFound) referenceFound++;
                var disagreement = Disagreement(result, referenceCost);
                if (disagreement == null) continue;
                if (IsKnownGap(grid, rule, result, referenceCost)) knownGaps.Add($"{label}: {disagreement}");
                else disagreements.Add($"{label}: {disagreement}");
            }
            Debug.Log($"[BeltRouteReferenceCrossCheck] compared {compared} queries, reference found {referenceFound} routes within cost {CostCap}, known gaps {knownGaps.Count}, disagreements {disagreements.Count}\n{string.Join("\n", knownGaps)}");
            Assert.Greater(referenceFound, Iterations / 10, "too few routes within the cap; the cross-check is not exercising the search");
            Assert.AreEqual(0, disagreements.Count, $"{disagreements.Count} disagreements, first {ReportedDisagreements}:\n{string.Join("\n", disagreements.GetRange(0, Mathf.Min(ReportedDisagreements, disagreements.Count)))}");
            Assert.LessOrEqual(knownGaps.Count, KnownGapAllowed, $"known gaps grew:\n{string.Join("\n", knownGaps)}");
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

        private static bool TryPickQuery(System.Random random, BeltRouteGrid grid, out BeltRouteQuery query)
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
            if (startIsExistingBelt) grid.MarkBlockCell(start);
            query = new BeltRouteQuery(start, (BlockDirection)random.Next(4), startIsExistingBelt, end, (BlockDirection)random.Next(4), allowSlopes, turnByOutput, new BeltRouteStepPriority(random.Next(BeltRouteStepPriority.PermutationCount)));
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

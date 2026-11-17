using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     終点から始点へ、総コストが予算に収まる状態を深さ優先でたどって経路を組み立てる
    ///     Assembles the route by walking depth-first from the end toward the start through states whose total cost fits the budget
    ///     各状態の最短コスト（A*の表）＋ここまで置いた分を下限にして枝を切る。予算が最短コストちょうどなら同コストの辺だけをたどる
    ///     Each state's shortest cost (the A* table) plus the cells placed so far bounds the total; with the budget at the shortest cost only equal-cost edges are walked
    ///     組めなかった状態は、失敗が自分より先に置いたセルに依存しないときだけ記憶して二度と試さない。これが無いと広場の同コスト経路を全部数えてしまう
    ///     A state that failed is remembered and never retried, but only when its failure did not depend on cells placed before it; without this every equal-cost plaza path gets enumerated
    /// </summary>
    public sealed class BeltRouteRecovery
    {
        // 保険。死んだ状態の記憶があるので通常は届かず、届いたら経路なし扱い
        // A backstop; with dead states remembered it is normally never reached, and reaching it means no route
        private const int StepLimit = 20000;

        private enum Outcome { Found, Dead, Aborted }

        private readonly BeltRouteSearch _search;
        private readonly BeltRouteRecoveryMap _map = new();
        private readonly BeltRouteRecoveryCandidates _candidates;
        private readonly List<BeltRouteState> _reversedStates = new();
        private IReadOnlyList<(BeltRouteState state, int cost)> _startStates;
        private BeltRouteTransitionRule _rule;
        private int _budget;
        private int _steps;

        public BeltRouteRecovery(BeltRouteSearch search)
        {
            _search = search;
            _candidates = new BeltRouteRecoveryCandidates(search);
        }

        // 探索が終点へ届いた後に呼ぶ。budgetは経路の総コストの上限。組めなければnull
        // Called after the search reached the goal; budget caps the route's total cost; null when nothing assembles within it
        public BeltRoutePlan Recover(BeltRouteTransitionRule rule, IReadOnlyList<(BeltRouteState state, int cost)> startStates, int budget)
        {
            _rule = rule;
            _startStates = startStates;
            _budget = budget;
            _map.Clear();
            _reversedStates.Clear();
            _steps = 0;

            var goal = _search.Goal;
            if (_search.BestCost(goal) == BeltRouteSearch.Unreached) return null;

            Place(goal, 0);
            // silent-ok: 歩数上限での打ち切りも「経路なし」として扱う / 赤いゴーストと理由行で分かる
            return WalkBack(goal, goal.Kind.Cost(), 0, out _) == Outcome.Found ? BuildPlan() : null;
        }

        // placedCostは終点からこの状態までに置いたセルのコストの和（この状態を含む）。depthは終点を0とした段数
        // placedCost is the cost of the cells placed from the end through this state, inclusive; depth counts from the end at 0
        // dependencyDepthは、この先の失敗が依存した置き済みセルの最も浅い深さ。depth以上なら失敗はこの状態から先だけで決まっている
        // dependencyDepth is the shallowest depth among placed cells the failures below depended on; at or beyond depth the failure is decided by this subtree alone
        private Outcome WalkBack(BeltRouteState state, int placedCost, int depth, out int dependencyDepth)
        {
            dependencyDepth = BeltRouteRecoveryMap.NoConflict;
            if (StepLimit < ++_steps) return Outcome.Aborted;
            if (IsStartState(state)) return Outcome.Found;

            var candidates = _candidates.Collect(_rule, state, placedCost, _budget);
            foreach (var previous in candidates)
            {
                // 分岐の始点は既存ベルトなので作業地図には置かない
                // A branch start is an existing belt, so it never enters the working map
                var isExistingStart = _rule.Query.StartIsExistingBelt && IsStartState(previous);
                var previousPlacedCost = placedCost + previous.Kind.Cost();
                if (!isExistingStart)
                {
                    if (_map.IsDead(previous, _budget - previousPlacedCost)) continue;
                    if (!_map.CanPlace(previous, out var conflictDepth))
                    {
                        // 終点（深さ0）は常に置いてあるので、終点との衝突は文脈に依らない
                        // The end (depth 0) is always placed, so a collision with it does not depend on the context
                        if (conflictDepth != 0) dependencyDepth = Mathf.Min(dependencyDepth, conflictDepth);
                        continue;
                    }
                    Place(previous, depth + 1);
                }

                var outcome = WalkBack(previous, previousPlacedCost, depth + 1, out var childDependency);
                if (outcome == Outcome.Found) return Outcome.Found;
                if (!isExistingStart) Unplace(previous);
                if (outcome == Outcome.Aborted) return Outcome.Aborted;
                dependencyDepth = Mathf.Min(dependencyDepth, childDependency);
            }

            // 失敗が自分以深のセルにしか依存しないなら、同じ残り予算で再び来ても組めない
            // When the failure depends only on cells from this depth on, arriving again with the same remaining budget cannot assemble either
            if (depth <= dependencyDepth) _map.MarkDead(state, _budget - placedCost);
            return Outcome.Dead;
        }

        private bool IsStartState(BeltRouteState state)
        {
            foreach (var (start, _) in _startStates)
            {
                if (start.Equals(state)) return true;
            }
            return false;
        }

        private void Place(BeltRouteState state, int depth)
        {
            _map.Place(state, depth);
            _reversedStates.Add(state);
        }

        private void Unplace(BeltRouteState state)
        {
            _map.Unplace(state);
            _reversedStates.RemoveAt(_reversedStates.Count - 1);
        }

        // 分岐器は入る向きが正面。通常ベルトは出る向きが正面。総コストは置いたセルの和（分岐の始点は置かないので0）
        // A splitter faces its entry heading; a normal belt faces its exit heading; the total is the sum of placed cells (a branch start is not placed, so 0)
        private BeltRoutePlan BuildPlan()
        {
            var cells = new List<BeltRouteCell>(_reversedStates.Count);
            var incoming = _rule.Query.StartDirection;
            var totalCost = 0;
            for (var i = _reversedStates.Count - 1; 0 <= i; i--)
            {
                var state = _reversedStates[i];
                var facing = _rule.Query.TurnByOutput ? incoming : state.Outgoing;
                cells.Add(new BeltRouteCell(state.Cell, facing, state.Kind));
                incoming = state.Outgoing;
                totalCost += state.Kind.Cost();
            }
            return new BeltRoutePlan(cells, totalCost);
        }
    }
}

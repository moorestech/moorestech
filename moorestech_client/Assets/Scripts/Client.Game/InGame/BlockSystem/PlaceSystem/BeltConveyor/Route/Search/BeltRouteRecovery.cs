using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     終点から始点へ、総コストが予算に収まる状態を深さ優先でたどって経路を組み立てる
    ///     Assembles the route by walking depth-first from the end toward the start through states whose total cost fits the budget
    ///     各状態の最短コスト（A*の表）＋ここまで置いた分を下限にして枝を切る。予算が最短コストちょうどなら同コストの辺だけをたどる
    ///     Each state's shortest cost (the A* table) plus the cells placed so far bounds the total; with the budget at the shortest cost only equal-cost edges are walked
    ///     使ったセルを作業用の地図に書き、同じセルの再使用と自分の坂の真上の通過は一歩戻って別候補を試す
    ///     Used cells go into a working map; reusing a cell or crossing above one's own slope backs up one step to try another candidate
    /// </summary>
    public sealed class BeltRouteRecovery
    {
        // 同コスト経路が大量にある地形で指数的に膨らむのを止める保険
        // A guard against exponential blow-up on maps with huge numbers of equal-cost routes
        private const int StepLimit = 200000;

        private readonly BeltRouteSearch _search;
        private readonly HashSet<Vector3Int> _usedCells = new();
        private readonly HashSet<Vector3Int> _ownSlopeHeads = new();
        private readonly List<BeltRouteState> _reversedStates = new();
        private IReadOnlyList<(BeltRouteState state, int cost)> _startStates;
        private BeltRouteTransitionRule _rule;
        private int _budget;
        private int _steps;

        public BeltRouteRecovery(BeltRouteSearch search)
        {
            _search = search;
        }

        // 探索が終点へ届いた後に呼ぶ。budgetは経路の総コストの上限。組めなければnull
        // Called after the search reached the goal; budget caps the route's total cost; null when nothing assembles within it
        public BeltRoutePlan Recover(BeltRouteTransitionRule rule, IReadOnlyList<(BeltRouteState state, int cost)> startStates, int budget)
        {
            _rule = rule;
            _startStates = startStates;
            _budget = budget;
            _usedCells.Clear();
            _ownSlopeHeads.Clear();
            _reversedStates.Clear();
            _steps = 0;

            var query = rule.Query;
            var goal = new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Straight);
            if (_search.BestCost(goal) == BeltRouteSearch.Unreached) return null;

            Place(goal);
            if (!WalkBack(goal, goal.Kind.Cost())) return null;
            return BuildPlan();
        }

        // placedCostは終点からこの状態までに置いたセルのコストの和（この状態を含む）
        // placedCost is the cost of the cells placed from the end through this state, inclusive
        private bool WalkBack(BeltRouteState state, int placedCost)
        {
            if (StepLimit < ++_steps) return false;
            if (IsStartState(state)) return true;

            // 候補列は再帰の深い側に上書きされないよう呼び出しごとに持つ
            // Each call keeps its own candidate list so deeper recursion cannot overwrite it
            var candidates = CollectCandidates(state, placedCost);
            foreach (var previous in candidates)
            {
                // 分岐の始点は既存ベルトなので作業地図には置かない
                // A branch start is an existing belt, so it never enters the working map
                var isExistingStart = _rule.Query.StartIsExistingBelt && IsStartState(previous);
                if (!isExistingStart && !CanPlace(previous)) continue;

                if (!isExistingStart) Place(previous);
                if (WalkBack(previous, placedCost + previous.Kind.Cost())) return true;
                if (!isExistingStart) Unplace(previous);
                if (StepLimit < _steps) return false;
            }
            return false;
        }

        // 手前候補を列挙し、総コストの下限が小さい順、同じなら優先順で並べる。候補は入る向き3通り×種別3通り
        // Enumerates predecessor candidates ordered by the lower bound of the total cost, then by the step priority; up to three incomings times three kinds
        private List<BeltRouteState> CollectCandidates(BeltRouteState state, int placedCost)
        {
            var ranked = new List<(int bound, int rank, BeltRouteState state)>(9);
            var entryHeight = state.EntryHeight;
            foreach (var incoming in IncomingOptions(state))
            {
                var column = state.Cell - BeltRouteDirection.Forward(incoming);
                for (var k = 0; k < BeltRouteCellKindExtension.KindCount; k++)
                {
                    var kind = (BeltRouteCellKind)k;
                    var previous = new BeltRouteState(new Vector3Int(column.x, entryHeight - kind.ExitHeightOffset(), column.z), incoming, kind);
                    if (!_rule.Query.AllowSlopes && kind.IsSlope()) continue;
                    if (!_rule.IsInsideBox(previous.Cell)) continue;
                    // 最短コスト＋置いた分が予算を超える候補は、どう繋いでも予算に収まらない
                    // A candidate whose shortest cost plus the placed cells exceeds the budget cannot fit however it connects
                    var best = _search.BestCost(previous);
                    if (best == BeltRouteSearch.Unreached || _budget < best + placedCost) continue;
                    if (!IsReachableTransition(previous, state)) continue;
                    Insert((best + placedCost, Rank(previous), previous));
                }
            }
            var ordered = new List<BeltRouteState>(ranked.Count);
            foreach (var candidate in ranked) ordered.Add(candidate.state);
            return ordered;

            #region Internal

            void Insert((int bound, int rank, BeltRouteState state) candidate)
            {
                // 安定な挿入。同じ下限・同ランクは列挙順を保つ
                // Stable insertion; equal bound and rank keep enumeration order
                var index = ranked.Count;
                while (0 < index && IsBefore(candidate, ranked[index - 1])) index--;
                ranked.Insert(index, candidate);
            }

            bool IsBefore((int bound, int rank, BeltRouteState state) a, (int bound, int rank, BeltRouteState state) b)
            {
                return a.bound < b.bound || (a.bound == b.bound && a.rank < b.rank);
            }

            #endregion
        }

        // 終点は手前の状態から入れるか、途中セルは手前の出る向きを入口として置けるか
        // The end checks whether it can be entered from the previous state; a mid cell whether it is placeable with that entry heading
        private bool IsReachableTransition(BeltRouteState previous, BeltRouteState next)
        {
            if (!_rule.IsConnected(previous, next)) return false;
            var isGoal = next.Cell == _rule.Query.EndCell;
            if (isGoal) return _rule.CanEnterGoal(previous);
            return _rule.IsIntermediatePlaceable(next.Cell, next.Kind, previous.Outgoing, next.Outgoing);
        }

        private static IEnumerable<BlockDirection> IncomingOptions(BeltRouteState state)
        {
            yield return state.Outgoing;
            if (state.Kind.IsSlope()) yield break;
            yield return BeltRouteDirection.Left(state.Outgoing);
            yield return BeltRouteDirection.Right(state.Outgoing);
        }

        private int Rank(BeltRouteState previous)
        {
            var axis = previous.Kind.IsSlope() ? BeltRouteStepAxis.Y : BeltRouteDirection.IsXAxis(previous.Outgoing) ? BeltRouteStepAxis.X : BeltRouteStepAxis.Z;
            return _rule.Query.Priority.Rank(axis);
        }

        private bool IsStartState(BeltRouteState state)
        {
            foreach (var (start, _) in _startStates)
            {
                if (start.Equals(state)) return true;
            }
            return false;
        }

        // 同じセルの再使用、既に置いた坂の真上、坂なら真上が使用済み、のいずれかで衝突
        // Collides on reusing a cell, sitting above an already placed slope, or a slope whose head is already used
        private bool CanPlace(BeltRouteState state)
        {
            if (_usedCells.Contains(state.Cell) || _ownSlopeHeads.Contains(state.Cell)) return false;
            return !state.Kind.IsSlope() || !_usedCells.Contains(state.Cell + Vector3Int.up);
        }

        private void Place(BeltRouteState state)
        {
            _usedCells.Add(state.Cell);
            if (state.Kind.IsSlope()) _ownSlopeHeads.Add(state.Cell + Vector3Int.up);
            _reversedStates.Add(state);
        }

        private void Unplace(BeltRouteState state)
        {
            _usedCells.Remove(state.Cell);
            if (state.Kind.IsSlope()) _ownSlopeHeads.Remove(state.Cell + Vector3Int.up);
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

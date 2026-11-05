using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     終点から始点へ、コストが1セルぶんずつ減る状態を深さ優先でたどって経路を組み立てる
    ///     Assembles the route by walking depth-first from the end toward the start through states whose cost drops by one cell each time
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
        private readonly (int rank, BeltRouteState state)[] _candidates = new (int, BeltRouteState)[9];
        private IReadOnlyList<(BeltRouteState state, int cost)> _startStates;
        private BeltRouteTransitionRule _rule;
        private int _steps;

        public BeltRouteRecovery(BeltRouteSearch search)
        {
            _search = search;
        }

        // 探索が終点へ届いた後に呼ぶ。組めなければnull（最短長で組める経路が無い）
        // Called after the search reached the goal; null when no route of the shortest length can be assembled
        public BeltRoutePlan Recover(BeltRouteTransitionRule rule, IReadOnlyList<(BeltRouteState state, int cost)> startStates)
        {
            _rule = rule;
            _startStates = startStates;
            _usedCells.Clear();
            _ownSlopeHeads.Clear();
            _reversedStates.Clear();
            _steps = 0;

            var query = rule.Query;
            var goal = new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Straight);
            var goalCost = _search.BestCost(goal);
            if (goalCost == BeltRouteSearch.Unreached) return null;

            Place(goal);
            if (!WalkBack(goal, goalCost)) return null;
            return BuildPlan(goalCost);
        }

        private bool WalkBack(BeltRouteState state, int cost)
        {
            if (StepLimit < ++_steps) return false;
            if (IsStartState(state, cost)) return true;

            var previousCost = cost - state.Kind.Cost();
            var count = CollectCandidates(state, cost);
            for (var i = 0; i < count; i++)
            {
                var previous = _candidates[i].state;

                // 分岐の始点は既存ベルトなので作業地図には置かない
                // A branch start is an existing belt, so it never enters the working map
                var isExistingStart = _rule.Query.StartIsExistingBelt && IsStartState(previous, previousCost);
                if (!isExistingStart && !CanPlace(previous)) continue;

                if (!isExistingStart) Place(previous);
                if (WalkBack(previous, previousCost)) return true;
                if (!isExistingStart) Unplace(previous);
                if (StepLimit < _steps) return false;
            }
            return false;
        }

        // 手前候補を列挙し、優先順でランク付けして並べる。候補は入る向き3通り×種別3通り
        // Enumerates predecessor candidates ranked by the step priority; up to three incomings times three kinds
        private int CollectCandidates(BeltRouteState state, int cost)
        {
            var count = 0;
            var entryHeight = state.EntryHeight;
            var previousCost = cost - state.Kind.Cost();
            foreach (var incoming in IncomingOptions(state))
            {
                var column = state.Cell - BeltRouteDirection.Forward(incoming);
                for (var k = 0; k < BeltRouteCellKindExtension.KindCount; k++)
                {
                    var kind = (BeltRouteCellKind)k;
                    var previous = new BeltRouteState(new Vector3Int(column.x, entryHeight - kind.ExitHeightOffset(), column.z), incoming, kind);
                    if (!_rule.Query.AllowSlopes && kind.IsSlope()) continue;
                    if (!_rule.IsInsideBox(previous.Cell) || _search.BestCost(previous) != previousCost) continue;
                    if (!IsReachableTransition(previous, state)) continue;
                    Insert(count++, (Rank(previous), previous));
                }
            }
            return count;

            #region Internal

            void Insert(int index, (int rank, BeltRouteState state) candidate)
            {
                // 安定な挿入ソート。同ランクは列挙順を保つ
                // Stable insertion sort; equal ranks keep enumeration order
                var i = index;
                while (0 < i && candidate.rank < _candidates[i - 1].rank)
                {
                    _candidates[i] = _candidates[i - 1];
                    i--;
                }
                _candidates[i] = candidate;
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

        private bool IsStartState(BeltRouteState state, int cost)
        {
            foreach (var (start, startCost) in _startStates)
            {
                if (start.Equals(state) && startCost == cost) return true;
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

        // 分岐器は入る向きが正面。通常ベルトは出る向きが正面
        // A splitter faces its entry heading; a normal belt faces its exit heading
        private BeltRoutePlan BuildPlan(int totalCost)
        {
            var cells = new List<BeltRouteCell>(_reversedStates.Count);
            var incoming = _rule.Query.StartDirection;
            for (var i = _reversedStates.Count - 1; 0 <= i; i--)
            {
                var state = _reversedStates[i];
                var facing = _rule.Query.TurnByOutput ? incoming : state.Outgoing;
                cells.Add(new BeltRouteCell(state.Cell, facing, state.Kind));
                incoming = state.Outgoing;
            }
            return new BeltRoutePlan(cells, totalCost);
        }
    }
}

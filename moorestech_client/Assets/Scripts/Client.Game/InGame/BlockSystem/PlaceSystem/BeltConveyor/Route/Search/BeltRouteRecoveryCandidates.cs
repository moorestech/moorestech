using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     組み立ての1歩で試す手前候補の列挙。入る向き3通り×種別3通りを、総コストの下限が小さい順、同じなら優先順に並べる
    ///     Enumerates the predecessor candidates for one recovery step: three incomings times three kinds, ordered by the lower bound of the total cost, then by the step priority
    /// </summary>
    public sealed class BeltRouteRecoveryCandidates
    {
        private readonly BeltRouteSearch _search;
        private readonly List<(int bound, int rank, BeltRouteState state)> _ranked = new(9);

        public BeltRouteRecoveryCandidates(BeltRouteSearch search)
        {
            _search = search;
        }

        // 候補列は再帰の深い側に上書きされないよう呼び出しごとに新しいリストで返す
        // The result is a fresh list per call so deeper recursion cannot overwrite it
        public List<BeltRouteState> Collect(BeltRouteTransitionRule rule, BeltRouteState state, int placedCost, int budget)
        {
            _ranked.Clear();
            var entryHeight = state.EntryHeight;
            foreach (var incoming in IncomingOptions(state))
            {
                var column = state.Cell - BeltRouteDirection.Forward(incoming);
                for (var k = 0; k < BeltRouteCellKindExtension.KindCount; k++)
                {
                    var kind = (BeltRouteCellKind)k;
                    var previous = new BeltRouteState(new Vector3Int(column.x, entryHeight - kind.ExitHeightOffset(), column.z), incoming, kind);
                    if (!rule.Query.AllowSlopes && kind.IsSlope()) continue;
                    if (!rule.IsInsideBox(previous.Cell)) continue;
                    // 最短コスト＋置いた分が予算を超える候補は、どう繋いでも予算に収まらない
                    // A candidate whose shortest cost plus the placed cells exceeds the budget cannot fit however it connects
                    var best = _search.BestCost(previous);
                    if (best == BeltRouteSearch.Unreached || budget < best + placedCost) continue;
                    if (!IsReachableTransition(rule, previous, state)) continue;
                    Insert((best + placedCost, Rank(rule, previous), previous));
                }
            }
            var ordered = new List<BeltRouteState>(_ranked.Count);
            foreach (var candidate in _ranked) ordered.Add(candidate.state);
            return ordered;
        }

        // 安定な挿入。同じ下限・同ランクは列挙順を保つ
        // Stable insertion; equal bound and rank keep enumeration order
        private void Insert((int bound, int rank, BeltRouteState state) candidate)
        {
            var index = _ranked.Count;
            while (0 < index && IsBefore(candidate, _ranked[index - 1])) index--;
            _ranked.Insert(index, candidate);
        }

        private static bool IsBefore((int bound, int rank, BeltRouteState state) a, (int bound, int rank, BeltRouteState state) b)
        {
            return a.bound < b.bound || (a.bound == b.bound && a.rank < b.rank);
        }

        // 終点は手前の状態から入れるか、途中セルは手前の出る向きを入口として置けるか
        // The end checks whether it can be entered from the previous state; a mid cell whether it is placeable with that entry heading
        private static bool IsReachableTransition(BeltRouteTransitionRule rule, BeltRouteState previous, BeltRouteState next)
        {
            if (!rule.IsConnected(previous, next)) return false;
            var isGoal = next.Cell == rule.Query.EndCell;
            if (isGoal) return rule.CanEnterGoal(previous);
            return rule.IsIntermediatePlaceable(next.Cell, next.Kind, previous.Outgoing, next.Outgoing);
        }

        private static IEnumerable<BlockDirection> IncomingOptions(BeltRouteState state)
        {
            yield return state.Outgoing;
            if (state.Kind.IsSlope()) yield break;
            yield return BeltRouteDirection.Left(state.Outgoing);
            yield return BeltRouteDirection.Right(state.Outgoing);
        }

        private static int Rank(BeltRouteTransitionRule rule, BeltRouteState previous)
        {
            var axis = previous.Kind.IsSlope() ? BeltRouteStepAxis.Y : BeltRouteDirection.IsXAxis(previous.Outgoing) ? BeltRouteStepAxis.X : BeltRouteStepAxis.Z;
            return rule.Query.Priority.Rank(axis);
        }
    }
}

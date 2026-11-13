using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     探索の終わり方（到達・枯渇・上限）
    ///     How the search ended (reached, drained, capped)
    /// </summary>
    public enum BeltRouteSearchOutcome
    {
        Reached,
        Exhausted,
        CapReached,
    }

    /// <summary>
    ///     重み付きA*で状態コストを辞書へ埋める
    ///     Weighted A* filling state costs into a dictionary
    ///     評価値は g + 2h。開けた場所では最短のまま、障害物の周りだけ遠回りを許して探索量を抑える（ADR 0100）
    ///     The score is g + 2h: shortest in the open, detours allowed only around obstacles, keeping the explored set small (ADR 0100)
    ///     箱を持たないので範囲の上限は無く、展開数の上限だけが探索を止める
    ///     Without a box there is no range limit; only the expansion cap stops the search
    /// </summary>
    public sealed class BeltRouteSearch
    {
        public const int Unreached = int.MaxValue;
        public const int HeuristicWeight = 2;

        private readonly Dictionary<BeltRouteState, int> _bestCost = new();
        private readonly BeltRouteOpenHeap _open = new();

        // Run〜ExpandMore間のみ有効
        // Valid only between Run and ExpandMore
        private BeltRouteTransitionRule _rule;

        // 経路なしのとき呼び手がログに出す
        // Reported by the caller's log when there is no route
        public int Expansions { get; private set; }

        // 終点状態。Runごとに決まり呼び手も共用
        // Goal state, fixed per Run and shared with callers
        public BeltRouteState Goal { get; private set; }

        public int BestCost(BeltRouteState state) => _bestCost.TryGetValue(state, out var g) ? g : Unreached;

        public BeltRouteSearchOutcome Run(BeltRouteTransitionRule rule, IReadOnlyList<(BeltRouteState state, int cost)> startStates, int maxExpansions)
        {
            _bestCost.Clear();
            _open.Clear();
            _rule = rule;
            Expansions = 0;
            var query = rule.Query;
            Goal = new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Straight);
            foreach (var (state, cost) in startStates) Relax(state, cost);
            return ExpandUntil(maxExpansions, true);
        }

        // 終点に届いた後、組み立ての代替候補を増やすために展開を続ける
        // After the goal is reached, keeps expanding so recovery has more alternatives to assemble from
        public void ExpandMore(int extraExpansions)
        {
            ExpandUntil(Expansions + extraExpansions, false);
        }

        private BeltRouteSearchOutcome ExpandUntil(int expansionLimit, bool stopAtGoal)
        {
            while (0 < _open.Count)
            {
                // silent-ok: 上限到達は呼び手（BeltRoutePlanner）が展開数つきでログに出す
                if (expansionLimit <= Expansions) return BestCost(Goal) == Unreached ? BeltRouteSearchOutcome.CapReached : BeltRouteSearchOutcome.Reached;
                var (_, g, state) = _open.Pop();
                // 古い項は飛ばす
                // Skip stale entries
                if (g != BestCost(state)) continue;
                if (state.Equals(Goal))
                {
                    if (stopAtGoal) return BeltRouteSearchOutcome.Reached;
                    continue;
                }
                Expansions++;
                Expand(state, g);
            }
            // silent-ok: 待ち行列の枯渇は呼び手（BeltRoutePlanner）がログに出す
            return BestCost(Goal) == Unreached ? BeltRouteSearchOutcome.Exhausted : BeltRouteSearchOutcome.Reached;

            #region Internal

            void Expand(BeltRouteState state, int g)
            {
                // 出る向きへ水平に1歩
                // One horizontal step along the exit heading
                var nextColumn = state.Cell + BeltRouteDirection.Forward(state.Outgoing);
                var exitHeight = state.ExitHeight;
                for (var k = 0; k < BeltRouteCellKindExtension.KindCount; k++)
                {
                    var nextKind = (BeltRouteCellKind)k;
                    if (nextKind.IsSlope() && !_rule.Query.AllowSlopes) continue;
                    if (!nextKind.CanFollow(state.Kind)) continue;
                    var nextCell = new Vector3Int(nextColumn.x, exitHeight - nextKind.EntryHeightOffset(), nextColumn.z);

                    // 終点セルそのものへ入るときは終点状態（直線・固定向き）だけを生成する。終点の列の他の高さは普通のセル
                    // Entering the end cell itself generates only the goal state (straight, fixed facing); other heights of that column are ordinary cells
                    if (nextCell == Goal.Cell)
                    {
                        if (nextKind == BeltRouteCellKind.Straight && _rule.CanEnterGoalFrom(state.Outgoing)) Relax(Goal, g + BeltRouteCellKindExtension.StraightCost);
                        continue;
                    }
                    TryRelax(nextCell, nextKind, state.Outgoing, state.Outgoing, g);
                    if (nextKind.IsSlope()) continue;
                    TryRelax(nextCell, nextKind, state.Outgoing, BeltRouteDirection.Left(state.Outgoing), g);
                    TryRelax(nextCell, nextKind, state.Outgoing, BeltRouteDirection.Right(state.Outgoing), g);
                }
            }

            void TryRelax(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing, int g)
            {
                if (!_rule.IsIntermediatePlaceable(cell, kind, incoming, outgoing)) return;
                Relax(new BeltRouteState(cell, outgoing, kind), g + kind.Cost());
            }

            #endregion
        }

        private void Relax(BeltRouteState state, int g)
        {
            if (BestCost(state) <= g) return;
            _bestCost[state] = g;
            _open.Push(g + HeuristicWeight * Heuristic(state), g, state);

            #region Internal

            // 残り距離と高低差から下限コストを見積もる
            // Lower-bounds the cost from distance and height difference
            int Heuristic(BeltRouteState state)
            {
                var horizontal = Mathf.Abs(Goal.Cell.x - state.Cell.x) + Mathf.Abs(Goal.Cell.z - state.Cell.z);
                var vertical = Mathf.Abs(Goal.Cell.y - state.ExitHeight);
                return BeltRouteCellKindExtension.StraightCost * Mathf.Max(horizontal, vertical) + (BeltRouteCellKindExtension.SlopeCost - BeltRouteCellKindExtension.StraightCost) * vertical;
            }

            #endregion
        }
    }
}

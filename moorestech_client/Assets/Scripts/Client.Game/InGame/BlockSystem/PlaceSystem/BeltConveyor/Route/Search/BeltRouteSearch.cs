using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     状態（セル・出る向き・種別）の上でA*を回し、各状態への最短コストを埋める。経路の組み立てはBeltRouteRecovery
    ///     Runs A* over states (cell, exit heading, kind) and fills each state's best cost; route assembly is BeltRouteRecovery's job
    ///     コストは整数なのでf値ごとのバケットで順に処理する（ParamegaFactoryのDjkと同じ方式）
    ///     Costs are integers, so buckets per f value are processed in order (the same scheme as ParamegaFactory's Djk)
    /// </summary>
    public sealed class BeltRouteSearch
    {
        public const int Unreached = int.MaxValue;

        // f値の上限＝探索できる経路コストの上限。超える経路は無しとみなす
        // The f cap is the cap on route cost; routes beyond it are treated as absent
        private const int BucketCount = 8192;

        private readonly BeltRouteStateCodec _codec;
        private readonly int[] _bestCost;
        private readonly List<int> _touchedStates = new();
        private readonly List<(int g, int state)>[] _buckets = new List<(int, int)>[BucketCount];

        public BeltRouteSearch(BeltRouteStateCodec codec)
        {
            _codec = codec;
            _bestCost = new int[codec.StateCount];
            for (var i = 0; i < _bestCost.Length; i++) _bestCost[i] = Unreached;
            for (var i = 0; i < BucketCount; i++) _buckets[i] = new List<(int, int)>();
        }

        public int BestCost(BeltRouteState state) => _bestCost[_codec.Encode(state)];

        // 終点状態まで到達できればtrue。同じf値の状態まで全て確定させてから返す（組み立てで同コスト候補を全部使えるように）
        // Returns true when the goal state is reached, after settling every state of the same f so recovery sees all equal-cost candidates
        public bool Run(BeltRouteTransitionRule rule, IReadOnlyList<(BeltRouteState state, int cost)> startStates)
        {
            ResetTouched();
            var query = rule.Query;
            var goal = new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Straight);
            var goalIndex = _codec.Encode(goal);
            foreach (var (state, cost) in startStates) Relax(state, cost, query.EndCell);

            var goalFound = false;
            for (var f = FirstActiveBucket(0); f < BucketCount; f = FirstActiveBucket(f + 1))
            {
                if (goalFound) break;
                var bucket = _buckets[f];
                // 展開中に同じf値へ積まれた分も同じ回で処理する
                // Entries pushed into the same f during expansion are handled in this same pass
                for (var i = 0; i < bucket.Count; i++)
                {
                    var (g, stateIndex) = bucket[i];
                    if (g != _bestCost[stateIndex]) continue;
                    if (stateIndex == goalIndex) { goalFound = true; continue; }
                    Expand(rule, _codec.Decode(stateIndex), g);
                }
                bucket.Clear();
            }
            ClearBuckets();
            return goalFound;
        }

        private void Expand(BeltRouteTransitionRule rule, BeltRouteState state, int g)
        {
            var query = rule.Query;
            var next = state.Cell + BeltRouteDirection.Forward(state.Outgoing);
            var exitHeight = state.ExitHeight;

            // 終点の列へ入るときは終点状態だけを生成する
            // Entering the end column generates only the goal state
            if (next.x == query.EndCell.x && next.z == query.EndCell.z)
            {
                if (rule.CanEnterGoal(state)) Relax(new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Straight), g + BeltRouteCellKindExtension.StraightCost, query.EndCell);
                return;
            }

            for (var k = 0; k < BeltRouteCellKindExtension.KindCount; k++)
            {
                var kind = (BeltRouteCellKind)k;
                if (kind.IsSlope() && !query.AllowSlopes) continue;
                if (!kind.CanFollow(state.Kind)) continue;
                var cell = new Vector3Int(next.x, exitHeight - kind.EntryHeightOffset(), next.z);
                if (kind.IsSlope())
                {
                    TryRelax(rule, cell, kind, state.Outgoing, state.Outgoing, g);
                    continue;
                }
                TryRelax(rule, cell, kind, state.Outgoing, state.Outgoing, g);
                TryRelax(rule, cell, kind, state.Outgoing, BeltRouteDirection.Left(state.Outgoing), g);
                TryRelax(rule, cell, kind, state.Outgoing, BeltRouteDirection.Right(state.Outgoing), g);
            }
        }

        private void TryRelax(BeltRouteTransitionRule rule, Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing, int g)
        {
            if (!rule.IsTurnAllowed(incoming, outgoing)) return;
            if (!rule.IsIntermediatePlaceable(cell, kind, incoming, outgoing)) return;
            Relax(new BeltRouteState(cell, outgoing, kind), g + kind.Cost(), rule.Query.EndCell);
        }

        private void Relax(BeltRouteState state, int g, Vector3Int endCell)
        {
            var index = _codec.Encode(state);
            if (_bestCost[index] <= g) return;
            if (_bestCost[index] == Unreached) _touchedStates.Add(index);
            _bestCost[index] = g;
            var f = g + Heuristic(state, endCell);
            // silent-ok: 上限超えは「経路なし」として組み立て側が扱う / ツールチップの理由行で気づける
            if (BucketCount <= f) return;
            _buckets[f].Add((g, index));
        }

        // 残り水平距離と残り高低差から下回らないコストを見積もる（一歩は直線5・坂7）
        // Lower-bounds the remaining cost from horizontal distance and height difference (a step is 5 straight, 7 slope)
        private static int Heuristic(BeltRouteState state, Vector3Int endCell)
        {
            var horizontal = Mathf.Abs(endCell.x - state.Cell.x) + Mathf.Abs(endCell.z - state.Cell.z);
            var vertical = Mathf.Abs(endCell.y - state.ExitHeight);
            return BeltRouteCellKindExtension.StraightCost * Mathf.Max(horizontal, vertical) + (BeltRouteCellKindExtension.SlopeCost - BeltRouteCellKindExtension.StraightCost) * vertical;
        }

        private int FirstActiveBucket(int from)
        {
            for (var f = from; f < BucketCount; f++)
            {
                if (0 < _buckets[f].Count) return f;
            }
            return BucketCount;
        }

        // 前回触った状態だけ戻す。全配列の初期化より桁違いに軽い
        // Reset only the states touched last time; far cheaper than clearing the whole array
        private void ResetTouched()
        {
            foreach (var index in _touchedStates) _bestCost[index] = Unreached;
            _touchedStates.Clear();
        }

        private void ClearBuckets()
        {
            foreach (var bucket in _buckets) bucket.Clear();
        }
    }
}

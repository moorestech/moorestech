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
    ///     内側は整数の添字だけで回す。Vector3Intの演算はMonoでは遅く、経路なしの探索は箱の全状態を通るため
    ///     The inner loop runs on integer indices alone; Vector3Int math is slow under Mono, and a no-route search visits every state in the box
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
        private readonly int _sizeX;
        private readonly int _sizeY;
        private readonly int _sizeZ;
        private readonly int _layer;

        // 1回のRunの間だけ有効
        // Valid only during one Run
        private BeltRouteTransitionRule _rule;
        private int _goalStateIndex;
        private int _endCellIndex;
        private int _endX;
        private int _endY;
        private int _endZ;

        public BeltRouteSearch(BeltRouteStateCodec codec)
        {
            _codec = codec;
            _sizeX = codec.Size.x;
            _sizeY = codec.Size.y;
            _sizeZ = codec.Size.z;
            _layer = _sizeX * _sizeZ;
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
            _rule = rule;
            var query = rule.Query;
            var endLocal = query.EndCell - _codec.Min;
            _endX = endLocal.x;
            _endY = endLocal.y;
            _endZ = endLocal.z;
            _endCellIndex = _codec.CellIndex(query.EndCell);
            _goalStateIndex = _codec.Encode(new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Straight));
            foreach (var (state, cost) in startStates)
            {
                var local = state.Cell - _codec.Min;
                Relax(_codec.Encode(state), cost, local.x, local.y + state.Kind.ExitHeightOffset(), local.z);
            }

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
                    if (stateIndex == _goalStateIndex) { goalFound = true; continue; }
                    Expand(stateIndex, g);
                }
                bucket.Clear();
            }
            ClearBuckets();
            return goalFound;
        }

        private void Expand(int stateIndex, int g)
        {
            var kind = (BeltRouteCellKind)(stateIndex % BeltRouteCellKindExtension.KindCount);
            var rest = stateIndex / BeltRouteCellKindExtension.KindCount;
            var outgoing = (BlockDirection)(rest % BeltRouteDirection.DirectionCount);
            var cellIndex = rest / BeltRouteDirection.DirectionCount;
            var x = cellIndex % _sizeX;
            var columns = cellIndex / _sizeX;
            var z = columns % _sizeZ;
            var y = columns / _sizeZ;

            // 出る向きへ水平に1歩。箱の端から外へは出られない
            // One horizontal step along the exit heading; the box edge cannot be left
            switch (outgoing)
            {
                case BlockDirection.North: z++; break;
                case BlockDirection.East: x++; break;
                case BlockDirection.South: z--; break;
                default: x--; break;
            }
            if (x < 0 || _sizeX <= x || z < 0 || _sizeZ <= z) return;
            var exitHeight = y + kind.ExitHeightOffset();
            var nextColumn = z * _sizeX + x;

            for (var k = 0; k < BeltRouteCellKindExtension.KindCount; k++)
            {
                var nextKind = (BeltRouteCellKind)k;
                if (nextKind.IsSlope() && !_rule.Query.AllowSlopes) continue;
                if (!nextKind.CanFollow(kind)) continue;
                var cellY = exitHeight - nextKind.EntryHeightOffset();
                if (cellY < 0 || _sizeY <= cellY) continue;
                var nextCell = cellY * _layer + nextColumn;

                // 終点セルそのものへ入るときは終点状態（直線・固定向き）だけを生成する。終点の列の他の高さは普通のセル
                // Entering the end cell itself generates only the goal state (straight, fixed facing); other heights of that column are ordinary cells
                if (nextCell == _endCellIndex)
                {
                    if (nextKind == BeltRouteCellKind.Straight && _rule.CanEnterGoalFrom(outgoing)) Relax(_goalStateIndex, g + BeltRouteCellKindExtension.StraightCost, _endX, _endY, _endZ);
                    continue;
                }
                if (nextKind.IsSlope())
                {
                    TryRelax(nextCell, x, cellY, z, nextKind, outgoing, outgoing, g);
                    continue;
                }
                TryRelax(nextCell, x, cellY, z, nextKind, outgoing, outgoing, g);
                TryRelax(nextCell, x, cellY, z, nextKind, outgoing, BeltRouteDirection.Left(outgoing), g);
                TryRelax(nextCell, x, cellY, z, nextKind, outgoing, BeltRouteDirection.Right(outgoing), g);
            }
        }

        private void TryRelax(int cellIndex, int x, int cellY, int z, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing, int g)
        {
            if (!_rule.IsTurnAllowed(incoming, outgoing)) return;
            if (!_rule.IsIntermediatePlaceableAt(cellIndex, kind, incoming, outgoing)) return;
            Relax(BeltRouteStateCodec.Encode(cellIndex, outgoing, kind), g + kind.Cost(), x, cellY + kind.ExitHeightOffset(), z);
        }

        // x・z・出口高さは箱の中の局所座標
        // x, z and the exit height are local coordinates inside the box
        private void Relax(int stateIndex, int g, int x, int exitHeight, int z)
        {
            if (_bestCost[stateIndex] <= g) return;
            if (_bestCost[stateIndex] == Unreached) _touchedStates.Add(stateIndex);
            _bestCost[stateIndex] = g;
            var f = g + Heuristic(x, exitHeight, z);
            // silent-ok: 上限超えは「経路なし」として組み立て側が扱う / ツールチップの理由行で気づける
            if (BucketCount <= f) return;
            _buckets[f].Add((g, stateIndex));
        }

        // 残り水平距離と残り高低差から下回らないコストを見積もる（一歩は直線5・坂7）
        // Lower-bounds the remaining cost from horizontal distance and height difference (a step is 5 straight, 7 slope)
        private int Heuristic(int x, int exitHeight, int z)
        {
            var horizontal = Mathf.Abs(_endX - x) + Mathf.Abs(_endZ - z);
            var vertical = Mathf.Abs(_endY - exitHeight);
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

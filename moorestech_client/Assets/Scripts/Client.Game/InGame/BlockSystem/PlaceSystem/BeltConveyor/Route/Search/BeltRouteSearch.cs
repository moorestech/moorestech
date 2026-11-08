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

        private readonly BeltRouteStateCodec _codec;
        private readonly int[] _bestCost;
        private readonly List<int> _touchedStates = new();
        private readonly BeltRouteCostBuckets _buckets = new();
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

        // 確定済みの最大f値。-1は未着手
        // The largest settled f; -1 before any pass
        private int _settledF = -1;

        public BeltRouteSearch(BeltRouteStateCodec codec)
        {
            _codec = codec;
            _sizeX = codec.Size.x;
            _sizeY = codec.Size.y;
            _sizeZ = codec.Size.z;
            _layer = _sizeX * _sizeZ;
            _bestCost = new int[codec.StateCount];
            for (var i = 0; i < _bestCost.Length; i++) _bestCost[i] = Unreached;
        }

        public int BestCost(BeltRouteState state) => _bestCost[_codec.Encode(state)];

        // 終点状態まで到達できればtrue。同じf値の状態まで全て確定させてから返す（組み立てで同コスト候補を全部使えるように）
        // Returns true when the goal state is reached, after settling every state of the same f so recovery sees all equal-cost candidates
        public bool Run(BeltRouteTransitionRule rule, IReadOnlyList<(BeltRouteState state, int cost)> startStates)
        {
            ResetTouched();
            _buckets.Clear();
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
            _settledF = -1;
            return Settle(BeltRouteCostBuckets.Count, true);
        }

        // 終点のf値を超えてmaxFまで確定を進める（最短で組めないとき、余裕ぶんの状態も正確なコストにする）
        // Settles beyond the goal's f up to maxF so states within the slack carry exact costs when the shortest cannot be assembled
        public void SettleUpTo(int maxF)
        {
            Settle(Mathf.Min(maxF, BeltRouteCostBuckets.Count - 1), false);
        }

        // f値のバケットを順に確定させる。stopAtGoal なら終点を確定させた回で止まり、以降は SettleUpTo で続きから進める
        // Settles buckets in f order; with stopAtGoal it stops after the pass that settles the goal, and SettleUpTo resumes from there
        private bool Settle(int maxF, bool stopAtGoal)
        {
            var goalFound = false;
            for (var f = _buckets.FirstActive(_settledF + 1); f <= maxF && f < BeltRouteCostBuckets.Count; f = _buckets.FirstActive(f + 1))
            {
                var bucket = _buckets.At(f);
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
                _settledF = f;
                if (stopAtGoal && goalFound) break;
            }
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
            _buckets.TryAdd(f, g, stateIndex);
        }

        // 残り水平距離と残り高低差から下回らないコストを見積もる（一歩は直線5・坂7）
        // Lower-bounds the remaining cost from horizontal distance and height difference (a step is 5 straight, 7 slope)
        private int Heuristic(int x, int exitHeight, int z)
        {
            var horizontal = Mathf.Abs(_endX - x) + Mathf.Abs(_endZ - z);
            var vertical = Mathf.Abs(_endY - exitHeight);
            return BeltRouteCellKindExtension.StraightCost * Mathf.Max(horizontal, vertical) + (BeltRouteCellKindExtension.SlopeCost - BeltRouteCellKindExtension.StraightCost) * vertical;
        }

        // 前回触った状態だけ戻す。全配列の初期化より桁違いに軽い
        // Reset only the states touched last time; far cheaper than clearing the whole array
        private void ResetTouched()
        {
            foreach (var index in _touchedStates) _bestCost[index] = Unreached;
            _touchedStates.Clear();
        }
    }
}

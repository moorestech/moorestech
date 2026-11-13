using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 本番の探索（A*＋組み立て）と突き合わせる参照ソルバ。小さな箱で全経路を深さ優先で総当たりし、組める最短コストを返す
    // A reference solver to cross-check the production search (A* plus recovery): exhaustively walks every route in a small box and returns the shortest assemblable cost
    // 遷移の可否は本番と同じ規則（BeltRouteTransitionRule）を使い、組み立ての不変条件（セル再使用なし・自分の坂の真上なし・始点終点は置き済み）はここで素直に持つ
    // Transitions use the same rule as production (BeltRouteTransitionRule); the assembly invariants (no cell reuse, nothing above own slopes, start and end already placed) are held plainly here
    public sealed class BeltRouteReferenceSolver
    {
        public const int NotFound = -1;

        private readonly BeltRouteTransitionRule _rule;
        private readonly BeltRouteQuery _query;
        private readonly int _costCap;
        private readonly bool _enforceAssembly;
        private readonly HashSet<Vector3Int> _usedCells = new();
        private readonly HashSet<Vector3Int> _ownSlopeHeads = new();
        private int _best;

        // enforceAssembly=false は組み立ての不変条件を外した「規則だけの最短」。本番のA*が出す値と一致するはずの対照用
        // enforceAssembly=false drops the assembly invariants to get the rule-only shortest cost, the control value the production A* should match
        public BeltRouteReferenceSolver(BeltRouteTransitionRule rule, int costCap, bool enforceAssembly)
        {
            _rule = rule;
            _query = rule.Query;
            _costCap = costCap;
            _enforceAssembly = enforceAssembly;
        }

        // 上限コスト以下で組める最短コスト。無ければNotFound
        // The shortest assemblable cost within the cap, or NotFound
        public int Solve()
        {
            _best = NotFound;
            _usedCells.Clear();
            _ownSlopeHeads.Clear();
            _usedCells.Add(_query.EndCell);
            foreach (var (start, cost) in StartStates(_rule))
            {
                if (_query.StartIsExistingBelt)
                {
                    Walk(start, cost);
                    continue;
                }
                if (!TryPlace(start)) continue;
                Walk(start, cost);
                Unplace(start);
            }
            return _best;
        }

        // 本番のBuildStartStatesと同じ出発候補。通常ベルトは直線か上り、分岐器は3方向、分岐は既存ベルトからコスト0
        // The same start candidates as production: straight or up for a normal belt, three headings for a splitter, cost 0 from an existing belt for a branch
        internal static List<(BeltRouteState state, int cost)> StartStates(BeltRouteTransitionRule rule)
        {
            var query = rule.Query;
            var starts = new List<(BeltRouteState, int)>();
            var cell = query.StartCell;
            var direction = query.StartDirection;
            if (query.StartIsExistingBelt)
            {
                starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Straight), 0));
                return starts;
            }
            starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
            if (query.TurnByOutput)
            {
                starts.Add((new BeltRouteState(cell, BeltRouteDirection.Left(direction), BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
                starts.Add((new BeltRouteState(cell, BeltRouteDirection.Right(direction), BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
                return starts;
            }
            if (query.AllowSlopes && rule.IsSlopeHeadFree(cell)) starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Up), BeltRouteCellKindExtension.SlopeCost));
            return starts;
        }

        private void Walk(BeltRouteState current, int cost)
        {
            if (_best != NotFound && _best <= cost) return;
            var nextColumn = current.Cell + BeltRouteDirection.Forward(current.Outgoing);
            for (var k = 0; k < BeltRouteCellKindExtension.KindCount; k++)
            {
                var kind = (BeltRouteCellKind)k;
                if (kind.IsSlope() && !_query.AllowSlopes) continue;
                var nextCell = new Vector3Int(nextColumn.x, current.ExitHeight - kind.EntryHeightOffset(), nextColumn.z);
                if (!_rule.IsInsideBox(nextCell)) continue;
                foreach (var outgoing in OutgoingOptions(current.Outgoing, kind)) Step(current, new BeltRouteState(nextCell, outgoing, kind), cost);
            }
        }

        private void Step(BeltRouteState current, BeltRouteState next, int cost)
        {
            if (!_rule.IsConnected(current, next)) return;
            var nextCost = cost + next.Kind.Cost();
            if (_costCap < nextCost) return;

            // 終点セルへ入るのは終点状態（直線・固定向き）だけ。他の種別・向きは終点セルに置けない
            // Only the goal state (straight, fixed facing) may enter the end cell; no other kind or heading may occupy it
            if (next.Cell == _query.EndCell)
            {
                if (next.Kind != BeltRouteCellKind.Straight || next.Outgoing != _query.EndDirection || !_rule.CanEnterGoal(current)) return;
                if (_best == NotFound || nextCost < _best) _best = nextCost;
                return;
            }
            if (!_rule.IsIntermediatePlaceable(next.Cell, next.Kind, current.Outgoing, next.Outgoing)) return;
            if (!TryPlace(next)) return;
            Walk(next, nextCost);
            Unplace(next);
        }

        // 坂は曲がれない。直線は前・左・右
        // A slope cannot turn; a straight cell exits forward, left or right
        private static IEnumerable<BlockDirection> OutgoingOptions(BlockDirection incoming, BeltRouteCellKind kind)
        {
            yield return incoming;
            if (kind.IsSlope()) yield break;
            yield return BeltRouteDirection.Left(incoming);
            yield return BeltRouteDirection.Right(incoming);
        }

        // 置き済みセルの再使用、自分の坂の真上への設置、真上が置き済みの坂、のどれかなら置けない
        // Cannot place on a used cell, above an own slope, or a slope whose head is already used
        private bool TryPlace(BeltRouteState state)
        {
            var head = state.Cell + Vector3Int.up;
            // 規則だけの最短でも、真上が始点か終点になる坂は規則として不可（探索もこれを知っている前提）
            // Even the rule-only shortest rejects a slope whose head is the start or end, since the search is expected to know that rule
            if (!_enforceAssembly) return !state.Kind.IsSlope() || (head != _query.StartCell && head != _query.EndCell);
            if (_usedCells.Contains(state.Cell) || _ownSlopeHeads.Contains(state.Cell)) return false;
            if (state.Kind.IsSlope() && _usedCells.Contains(head)) return false;
            _usedCells.Add(state.Cell);
            if (state.Kind.IsSlope()) _ownSlopeHeads.Add(head);
            return true;
        }

        private void Unplace(BeltRouteState state)
        {
            _usedCells.Remove(state.Cell);
            if (state.Kind.IsSlope()) _ownSlopeHeads.Remove(state.Cell + Vector3Int.up);
        }
    }
}

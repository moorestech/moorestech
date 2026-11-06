using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     1つの探索箱に対する経路計画の入口。塗りつぶしで届かなければ即経路なし、届けば探索→組み立て
    ///     The entry for planning routes within one search box: an unreachable flood means no route at once, otherwise search then recovery
    ///     経路なしの理由は地形・既存ブロックを片方ずつ無視した塗りつぶしで分ける（本探索を繰り返すと経路なしのたびに引っかかる）
    ///     The no-route reason comes from floods that ignore terrain or blocks one at a time; repeating the real search would hitch on every miss
    /// </summary>
    public sealed class BeltRoutePlanner
    {
        private readonly BeltRouteStateCodec _codec;
        private readonly BeltRouteSearch _search;
        private readonly BeltRouteRecovery _recovery;
        private readonly BeltRouteReachability _reachability;
        private BeltRouteGrid _grid;

        // 最短コストで組めないとき、総コストをここまで余分に許して組み直す。0なら最短ちょうどだけ（ParamegaFactory と同じ）
        // Extra total cost allowed when the shortest cannot be assembled; 0 walks the shortest cost only (as ParamegaFactory does)
        private readonly int _recoverySlack;
        public const int NoRecoverySlack = 0;

        // 本番の余裕。直線4セル分。最短が自分の坂の真上を通る形しか無い配置は2セル分の遠回りで組めるのが実測で、その倍を見ている
        // The production slack: four straight cells; layouts whose shortest exists only through an own slope head assemble with a two-cell detour by measurement, so twice that is allowed
        public const int DefaultRecoverySlack = 20;

        public BeltRoutePlanner(BeltRouteGrid grid, int recoverySlack)
        {
            _grid = grid;
            _recoverySlack = recoverySlack;
            _codec = new BeltRouteStateCodec(grid);
            _search = new BeltRouteSearch(_codec);
            _recovery = new BeltRouteRecovery(_search);
            _reachability = new BeltRouteReachability(grid.CellCount);
        }

        public BeltRouteGrid Grid => _grid;

        // 箱の位置と大きさが同じ障害情報へ差し替える。探索配列は箱の大きさで確保しているので使い回せる
        // Swaps in obstacle data for the same box; the search arrays are sized by the box, so they are reused
        public void ReplaceGrid(BeltRouteGrid grid)
        {
            if (!_codec.IsSameBox(grid)) throw new System.ArgumentException($"BeltRoutePlanner: grid box differs. expected min:{_codec.Min} size:{_codec.Size} actual min:{grid.Min} size:{grid.Size}");
            _grid = grid;
        }

        // 始点・終点のセル自体の可否は呼び出し側（セッション）が先に確かめている
        // The start and end cells themselves are validated beforehand by the caller (the session)
        public BeltRoutePlanResult Plan(BeltRouteQuery query)
        {
            if (!_grid.Contains(query.EndCell)) return BeltRoutePlanResult.NotFound(BeltRouteNoPathReason.OutOfRange);

            // セル単位で届かない終点は本探索を回さない（囲われた終点で箱全体を舐めるのを避ける）
            // An end the flood cannot touch skips the real search, so an enclosed end never sweeps the whole box
            var rule = new BeltRouteTransitionRule(_grid, query, BeltRouteObstacleMask.None);
            if (!_reachability.CanReach(rule, _grid)) return BeltRoutePlanResult.NotFound(JudgeNoPathReason(query));

            var plan = TryPlan(query, rule);
            if (plan != null) return BeltRoutePlanResult.Found(plan);
            return BeltRoutePlanResult.NotFound(JudgeNoPathReason(query));
        }

        // 片方だけ無視して届けばそれが障害。両方届く（坂の規則だけで届かない）・両方届かないは「両方」
        // Reaching with one kind ignored names that kind; reaching both ways (only slope rules block) or neither is "both"
        private BeltRouteNoPathReason JudgeNoPathReason(BeltRouteQuery query)
        {
            var reachableWithoutTerrain = _reachability.CanReach(new BeltRouteTransitionRule(_grid, query, BeltRouteObstacleMask.IgnoreTerrain), _grid);
            var reachableWithoutBlocks = _reachability.CanReach(new BeltRouteTransitionRule(_grid, query, BeltRouteObstacleMask.IgnoreBlocks), _grid);
            if (reachableWithoutTerrain && !reachableWithoutBlocks) return BeltRouteNoPathReason.Terrain;
            if (reachableWithoutBlocks && !reachableWithoutTerrain) return BeltRouteNoPathReason.ExistingBlock;
            return BeltRouteNoPathReason.Both;
        }

        private BeltRoutePlan TryPlan(BeltRouteQuery query, BeltRouteTransitionRule rule)
        {
            var startStates = BuildStartStates(query, rule);
            if (startStates.Count == 0) return null;
            if (!_search.Run(rule, startStates)) return null;

            // まず最短コストちょうどで組む。組めなければ予算を余裕ぶん広げ、その範囲の状態を正確なコストにしてから組み直す
            // Assemble at exactly the shortest cost first; if that fails, widen the budget by the slack, settle states up to it and assemble again
            var shortest = _search.BestCost(new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Straight));
            var plan = _recovery.Recover(rule, startStates, shortest);
            if (plan != null || _recoverySlack == NoRecoverySlack) return plan;
            _search.SettleUpTo(shortest + _recoverySlack);
            plan = _recovery.Recover(rule, startStates, shortest + _recoverySlack);
            // silent-ok: 予算内で組める経路が無いのは仕様上の「経路なし」 / 赤いゴーストと理由行で分かる
            return plan;
        }

        // 通常ベルトは直線か上りで出発、分岐器は背面入力のまま3方向へ、分岐の始点は既存ベルトの横出口（置かないのでコスト0）
        // A normal belt starts straight or rising, a splitter keeps its back entry and exits three ways, and a branch start is the existing belt's side exit (cost 0, never placed)
        private static List<(BeltRouteState state, int cost)> BuildStartStates(BeltRouteQuery query, BeltRouteTransitionRule rule)
        {
            var starts = new List<(BeltRouteState, int)>(3);
            var cell = query.StartCell;
            var direction = query.StartDirection;
            if (query.StartIsExistingBelt)
            {
                starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Straight), 0));
                return starts;
            }

            if (query.TurnByOutput)
            {
                starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
                starts.Add((new BeltRouteState(cell, BeltRouteDirection.Left(direction), BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
                starts.Add((new BeltRouteState(cell, BeltRouteDirection.Right(direction), BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
                return starts;
            }

            starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
            if (query.AllowSlopes && rule.IsSlopeHeadFree(cell)) starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Up), BeltRouteCellKindExtension.SlopeCost));
            return starts;
        }
    }
}

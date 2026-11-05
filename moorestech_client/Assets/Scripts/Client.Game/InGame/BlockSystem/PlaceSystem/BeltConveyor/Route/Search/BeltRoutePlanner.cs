using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     1つの探索箱に対する経路計画の入口。探索→組み立て、経路なし時は地形・既存ブロックを片方ずつ無視して理由を分ける
    ///     The entry for planning routes within one search box: search then recovery, and on failure attribute the cause by ignoring terrain or blocks one at a time
    /// </summary>
    public sealed class BeltRoutePlanner
    {
        private readonly BeltRouteStateCodec _codec;
        private readonly BeltRouteSearch _search;
        private readonly BeltRouteRecovery _recovery;
        private BeltRouteGrid _grid;

        public BeltRoutePlanner(BeltRouteGrid grid)
        {
            _grid = grid;
            _codec = new BeltRouteStateCodec(grid);
            _search = new BeltRouteSearch(_codec);
            _recovery = new BeltRouteRecovery(_search);
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

            var plan = TryPlan(query, BeltRouteObstacleMask.None);
            if (plan != null) return BeltRoutePlanResult.Found(plan);

            // 到達可否は経路なしになったときだけ求める（始点確定時にまとめて求めると引っかかる）
            // Reachability is probed only once a route is missing; probing at start selection would cause a hitch
            var reachableWithoutTerrain = TryPlan(query, BeltRouteObstacleMask.IgnoreTerrain) != null;
            var reachableWithoutBlocks = TryPlan(query, BeltRouteObstacleMask.IgnoreBlocks) != null;
            if (reachableWithoutTerrain && !reachableWithoutBlocks) return BeltRoutePlanResult.NotFound(BeltRouteNoPathReason.Terrain);
            if (reachableWithoutBlocks && !reachableWithoutTerrain) return BeltRoutePlanResult.NotFound(BeltRouteNoPathReason.ExistingBlock);
            return BeltRoutePlanResult.NotFound(BeltRouteNoPathReason.Both);
        }

        private BeltRoutePlan TryPlan(BeltRouteQuery query, BeltRouteObstacleMask mask)
        {
            var rule = new BeltRouteTransitionRule(_grid, query, mask);
            var startStates = BuildStartStates(query, rule);
            if (startStates.Count == 0) return null;
            if (!_search.Run(rule, startStates)) return null;

            var plan = _recovery.Recover(rule, startStates);
            // silent-ok: 最短長で組める経路が無いのは仕様上の「経路なし」 / 赤いゴーストと理由行で分かる
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

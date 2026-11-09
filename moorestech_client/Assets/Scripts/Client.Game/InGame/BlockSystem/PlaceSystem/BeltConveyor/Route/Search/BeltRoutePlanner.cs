using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     経路計画の入口。世界をその場で読む重み付き探索→組み立て。箱も範囲の上限も無く、探索量の上限だけで経路なしを決める（ADR 0099）
    ///     The entry for route planning: a weighted search reading the world on demand, then recovery; no box or range limit, only the expansion cap decides no-route (ADR 0099)
    /// </summary>
    public sealed class BeltRoutePlanner
    {
        public const int NoRecoverySlack = 0;

        // 本番の余裕。直線4セル分。最短が自分の坂の真上を通る形しか無い配置は2セル分の遠回りで組めるのが実測で、その倍を見ている（ADR 0097）
        // The production slack: four straight cells; layouts whose shortest exists only through an own slope head assemble with a two-cell detour by measurement, so twice that is allowed (ADR 0097)
        public const int DefaultRecoverySlack = 20;

        // 探索量の上限。囲まれた終点・遠すぎる終点はここで止まる。手元で約 100 ms に収まる見込みの値（実測は ADR 0099）
        // The expansion cap; enclosed or far-away ends stop here. Chosen to stay around 100 ms locally (measurements in ADR 0099)
        public const int MaxExpansions = 20000;

        // 最短で組めないとき、組み立ての代替候補を増やすために追加で展開する数
        // Extra expansions run when the shortest cannot be assembled, so recovery sees more alternatives
        private const int RecoveryExtraExpansions = 4000;

        private readonly int _recoverySlack;
        private readonly BeltRouteSearch _search = new();
        private readonly BeltRouteRecovery _recovery;
        private IBeltRouteWorld _world;

        public BeltRoutePlanner(IBeltRouteWorld world, int recoverySlack)
        {
            _world = world;
            _recoverySlack = recoverySlack;
            _recovery = new BeltRouteRecovery(_search);
        }

        // 世界を差し替える（Task 3 で BeltRouteSession が BeltRouteLiveWorld を持つまでの暫定。Task 3 で削除）
        // Swaps the world (interim until BeltRouteSession owns a BeltRouteLiveWorld in Task 3; removed there)
        public void ReplaceWorld(IBeltRouteWorld world)
        {
            _world = world;
        }

        // 始点・終点のセル自体の可否は呼び出し側（セッション）が先に確かめている
        // The start and end cells themselves are validated beforehand by the caller (the session)
        public BeltRoutePlanResult Plan(BeltRouteQuery query)
        {
            var rule = new BeltRouteTransitionRule(_world, query);
            var startStates = BuildStartStates(query, rule);
            if (startStates.Count == 0) return NotFound(query, "no start state");

            var outcome = _search.Run(rule, startStates, MaxExpansions);
            if (outcome != BeltRouteSearchOutcome.Reached) return NotFound(query, outcome.ToString());

            // まず届いたコストちょうどで組む。組めなければ少し探索を進め、余裕ぶん広げて組み直す
            // Assemble at exactly the reached cost first; if that fails, search a little further and assemble again with the slack
            var reached = _search.BestCost(new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Straight));
            var plan = _recovery.Recover(rule, startStates, reached);
            if (plan == null && _recoverySlack != NoRecoverySlack)
            {
                _search.ExpandMore(RecoveryExtraExpansions);
                plan = _recovery.Recover(rule, startStates, reached + _recoverySlack);
            }
            return plan == null ? NotFound(query, "recovery failed") : BeltRoutePlanResult.Found(plan);
        }

        // 利用者には理由行「経路が見つかりません」、開発者には出どころと展開数をログで
        // The user sees the "no route found" line; developers get the origin and the expansion count in the log
        private BeltRoutePlanResult NotFound(BeltRouteQuery query, string origin)
        {
            Debug.Log($"[BeltRoute] no route {query.StartCell}->{query.EndCell}: {origin} expansions:{_search.Expansions}");
            return BeltRoutePlanResult.NotFound();
        }

        // 通常ベルトは直線か上りで出発、分岐器は背面入力のまま3方向へ、分岐の始点は既存ベルトの出口（置かないのでコスト0）
        // A normal belt starts straight or rising, a splitter keeps its back entry and exits three ways, and a branch start is the existing belt's exit (cost 0, never placed)
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

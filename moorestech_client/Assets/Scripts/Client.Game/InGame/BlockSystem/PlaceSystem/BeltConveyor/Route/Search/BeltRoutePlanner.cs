using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     経路計画の入口。重み付き探索→組み立て
    ///     Route planning entry: weighted search, then recovery
    /// </summary>
    public sealed class BeltRoutePlanner
    {
        public const int NoRecoverySlack = 0;

        // 本番の余裕。直線4セル分。最短が自分の坂の真上を通る形しか無い配置は2セル分の遠回りで組めるのが実測で、その倍を見ている（ADR 0097）
        // The production slack: four straight cells; layouts whose shortest exists only through an own slope head assemble with a two-cell detour by measurement, so twice that is allowed (ADR 0097)
        public const int DefaultRecoverySlack = 20;

        // 探索量の上限。囲いの大きい終点・遠すぎる終点はここで止まる。手元で約 100 ms に収まる見込みの値（実測は ADR 0100）
        // The expansion cap; enclosed or far-away ends stop here. Chosen to stay around 100 ms locally (measurements in ADR 0100)
        public const int MaxExpansions = 20000;

        // 囲まれた終点で上限まで探索しないための小さな塗りつぶし。理由の区別には使わない（ADR 0100）
        // A small flood fill so an enclosed end does not search up to the cap; not used to tell reasons apart (ADR 0100)
        private const int EnclosureProbeCells = 512;

        // 組み立て失敗時の追加展開数
        // Extra expansions when recovery fails
        private const int RecoveryExtraExpansions = 4000;

        private readonly int _recoverySlack;
        private readonly BeltRouteSearch _search = new();
        private readonly BeltRouteRecovery _recovery;
        private readonly IBeltRouteWorld _world;

        public BeltRoutePlanner(IBeltRouteWorld world, int recoverySlack)
        {
            _world = world;
            _recoverySlack = recoverySlack;
            _recovery = new BeltRouteRecovery(_search);
        }

        // 始点・終点のセル自体の可否は呼び出し側（セッション）が先に確かめている
        // The start and end cells themselves are validated beforehand by the caller (the session)
        public BeltRoutePlanResult Plan(BeltRouteQuery query)
        {
            var rule = new BeltRouteTransitionRule(_world, query);
            if (!BeltRouteEndEnclosureProbe.IsOpen(rule, query.EndCell, EnclosureProbeCells)) return NotFound(query, "end enclosed", 0);
            var startStates = BuildStartStates(query, rule);

            var outcome = _search.Run(rule, startStates, MaxExpansions);
            if (outcome != BeltRouteSearchOutcome.Reached) return NotFound(query, outcome.ToString(), _search.Expansions);

            // 届いたコストで組み、失敗時は追加展開して再組み立て
            // Assemble at the reached cost; on failure expand more and retry
            var reached = _search.BestCost(_search.Goal);
            var plan = _recovery.Recover(rule, startStates, reached);
            if (plan == null && _recoverySlack != NoRecoverySlack)
            {
                _search.ExpandMore(RecoveryExtraExpansions);
                plan = _recovery.Recover(rule, startStates, reached + _recoverySlack);
            }
            return plan == null ? NotFound(query, "recovery failed", _search.Expansions) : BeltRoutePlanResult.Found(plan);
        }

        // 理由行は利用者へ、出どころと展開数はログへ
        // Reason line to the user; origin and expansions to the log
        private static BeltRoutePlanResult NotFound(BeltRouteQuery query, string origin, int expansions)
        {
            Debug.Log($"[BeltRoute] no route {query.StartCell}->{query.EndCell}: {origin} expansions:{expansions}");
            return BeltRoutePlanResult.NotFound();
        }

        // 出発規則: 通常は直線か上り、分岐器は背面入力3方向
        // Start rule: normal belt straight or up, splitter back entry three ways
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

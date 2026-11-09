using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     組み立て済み経路の1セル。Facingは置くブロックの向き（通常ベルトは出る向き、分岐器は入る向き）
    ///     One cell of an assembled route; Facing is the placed block's heading (exit for a normal belt, entry for a splitter)
    /// </summary>
    public readonly struct BeltRouteCell
    {
        public readonly Vector3Int Cell;
        public readonly BlockDirection Facing;
        public readonly BeltRouteCellKind Kind;

        public BeltRouteCell(Vector3Int cell, BlockDirection facing, BeltRouteCellKind kind)
        {
            Cell = cell;
            Facing = facing;
            Kind = kind;
        }

        public override string ToString() => $"{Cell}:{Facing}:{Kind}";
    }

    /// <summary>
    ///     始点から終点へ並んだ経路。分岐の始点（既存ベルト）は含まない
    ///     A route ordered from start to end; a branch's existing start belt is not included
    /// </summary>
    public sealed class BeltRoutePlan
    {
        public readonly IReadOnlyList<BeltRouteCell> Cells;
        public readonly int TotalCost;

        public BeltRoutePlan(IReadOnlyList<BeltRouteCell> cells, int totalCost)
        {
            Cells = cells;
            TotalCost = totalCost;
        }
    }

    /// <summary>
    ///     探索の結果。経路があればPlan、無ければ経路なし（利用者向けの理由は「見つかりません」の1つ。ADR 0099）
    ///     The search outcome: a plan when a route exists, otherwise no route (the single user-facing reason is "not found", ADR 0099)
    /// </summary>
    public sealed class BeltRoutePlanResult
    {
        public readonly BeltRoutePlan Plan;

        public bool HasPlan => Plan != null;

        private BeltRoutePlanResult(BeltRoutePlan plan)
        {
            Plan = plan;
        }

        public static BeltRoutePlanResult Found(BeltRoutePlan plan) => new(plan);
        public static BeltRoutePlanResult NotFound() => new(null);
    }
}

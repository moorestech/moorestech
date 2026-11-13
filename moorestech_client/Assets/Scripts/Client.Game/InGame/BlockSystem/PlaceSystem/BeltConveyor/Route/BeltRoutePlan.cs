using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
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
    ///     探索結果。経路ありはPlan、なしは理由1つ
    ///     Search outcome: a plan, or no route with one reason
    /// </summary>
    public sealed class BeltRoutePlanResult
    {
        public readonly BeltRoutePlan Plan;

        // 終点が使えない理由。他はNone
        // Why the end is unusable; None otherwise
        public readonly BeltRouteCellBlockReason EndCellBlockReason;

        public bool HasPlan => Plan != null;

        private BeltRoutePlanResult(BeltRoutePlan plan, BeltRouteCellBlockReason endCellBlockReason)
        {
            Plan = plan;
            EndCellBlockReason = endCellBlockReason;
        }

        public static BeltRoutePlanResult Found(BeltRoutePlan plan) => new(plan, BeltRouteCellBlockReason.None);
        public static BeltRoutePlanResult NotFound() => new(null, BeltRouteCellBlockReason.None);
        public static BeltRoutePlanResult EndCellBlocked(BeltRouteCellBlockReason reason) => new(null, reason);
    }
}

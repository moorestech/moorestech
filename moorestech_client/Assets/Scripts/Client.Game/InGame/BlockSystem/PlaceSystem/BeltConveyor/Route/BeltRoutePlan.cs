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
    ///     経路が組めなかった理由。地形と既存ブロックを片方ずつ無視した到達可否で分ける
    ///     Why no route could be built, split by reachability when terrain or existing blocks are ignored one at a time
    /// </summary>
    public enum BeltRouteNoPathReason
    {
        Terrain,
        ExistingBlock,
        Both,
        OutOfRange,
    }

    /// <summary>
    ///     探索の結果。経路があればPlan、無ければ理由
    ///     The search outcome: a plan when a route exists, otherwise the reason
    /// </summary>
    public sealed class BeltRoutePlanResult
    {
        public readonly BeltRoutePlan Plan;
        public readonly BeltRouteNoPathReason NoPathReason;

        public bool HasPlan => Plan != null;

        private BeltRoutePlanResult(BeltRoutePlan plan, BeltRouteNoPathReason noPathReason)
        {
            Plan = plan;
            NoPathReason = noPathReason;
        }

        public static BeltRoutePlanResult Found(BeltRoutePlan plan) => new(plan, BeltRouteNoPathReason.Both);
        public static BeltRoutePlanResult NotFound(BeltRouteNoPathReason reason) => new(null, reason);
    }
}

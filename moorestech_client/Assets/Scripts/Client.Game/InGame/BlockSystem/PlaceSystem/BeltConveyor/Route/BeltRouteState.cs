using System;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     探索の状態＝置いたセル・そのセルから出る向き・セル種別。出口の辺はこの3つで一意に決まる
    ///     A search state: the placed cell, the heading it exits toward, and its kind; the exit edge follows uniquely from these
    /// </summary>
    public readonly struct BeltRouteState : IEquatable<BeltRouteState>
    {
        public readonly Vector3Int Cell;
        public readonly BlockDirection Outgoing;
        public readonly BeltRouteCellKind Kind;

        public BeltRouteState(Vector3Int cell, BlockDirection outgoing, BeltRouteCellKind kind)
        {
            Cell = cell;
            Outgoing = outgoing;
            Kind = kind;
        }

        public int ExitHeight => Cell.y + Kind.ExitHeightOffset();
        public int EntryHeight => Cell.y + Kind.EntryHeightOffset();

        public bool Equals(BeltRouteState other)
        {
            return Cell == other.Cell && Outgoing == other.Outgoing && Kind == other.Kind;
        }

        public override bool Equals(object obj) => obj is BeltRouteState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Cell, Outgoing, Kind);
        public override string ToString() => $"{Cell}:{Outgoing}:{Kind}";
    }
}

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

        public BeltRouteEdge ExitEdge => BeltRouteEdge.FromFace(Cell, Outgoing, ExitHeight);

        public bool Equals(BeltRouteState other)
        {
            return Cell == other.Cell && Outgoing == other.Outgoing && Kind == other.Kind;
        }

        public override bool Equals(object obj) => obj is BeltRouteState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Cell, Outgoing, Kind);
        public override string ToString() => $"{Cell}:{Outgoing}:{Kind}";
    }

    /// <summary>
    ///     状態と配列添字の相互変換。箱の全セル×4向き×3種別を1本の配列に並べる
    ///     Converts states to and from array indices, laying every cell x 4 headings x 3 kinds into one array
    /// </summary>
    public sealed class BeltRouteStateCodec
    {
        private const int StatesPerCell = BeltRouteDirection.DirectionCount * BeltRouteCellKindExtension.KindCount;

        // 箱の位置と大きさだけを持つ。障害情報を差し替えても同じ箱なら添字は変わらない
        // Holds only the box origin and size, so swapping obstacle data for the same box keeps indices stable
        public readonly Vector3Int Min;
        public readonly Vector3Int Size;

        public BeltRouteStateCodec(BeltRouteGrid grid)
        {
            Min = grid.Min;
            Size = grid.Size;
        }

        public int StateCount => Size.x * Size.y * Size.z * StatesPerCell;

        public bool IsSameBox(BeltRouteGrid grid) => grid.Min == Min && grid.Size == Size;

        // BeltRouteGrid.CellIndex と同じ並び
        // The same layout as BeltRouteGrid.CellIndex
        public int CellIndex(Vector3Int cell)
        {
            var local = cell - Min;
            return (local.y * Size.z + local.z) * Size.x + local.x;
        }

        public int Encode(BeltRouteState state)
        {
            return Encode(CellIndex(state.Cell), state.Outgoing, state.Kind);
        }

        public static int Encode(int cellIndex, BlockDirection outgoing, BeltRouteCellKind kind)
        {
            return (cellIndex * BeltRouteDirection.DirectionCount + (int)outgoing) * BeltRouteCellKindExtension.KindCount + (int)kind;
        }

        public BeltRouteState Decode(int index)
        {
            var kind = (BeltRouteCellKind)(index % BeltRouteCellKindExtension.KindCount);
            var rest = index / BeltRouteCellKindExtension.KindCount;
            var outgoing = (BlockDirection)(rest % BeltRouteDirection.DirectionCount);
            var cellIndex = rest / BeltRouteDirection.DirectionCount;
            var x = cellIndex % Size.x;
            var columns = cellIndex / Size.x;
            var cell = Min + new Vector3Int(x, columns / Size.z, columns % Size.z);
            return new BeltRouteState(cell, outgoing, kind);
        }
    }
}

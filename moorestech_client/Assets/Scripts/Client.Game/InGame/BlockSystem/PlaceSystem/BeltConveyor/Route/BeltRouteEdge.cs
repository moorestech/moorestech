using System;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     セル境界の縦面を高さ付きで表す。サーバーのBeltEdgeと同じ正規化（正側セル＋法線軸）で同じ面を同じ値にする
    ///     A vertical cell boundary face with a height, normalized like the server's BeltEdge (positive-side cell plus normal axis)
    /// </summary>
    public readonly struct BeltRouteEdge : IEquatable<BeltRouteEdge>
    {
        // yは面の高さ（接続が成立するセル段）
        // y holds the face height (the cell level at which a connection forms)
        public readonly Vector3Int PositiveCell;
        public readonly bool NormalIsX;

        private BeltRouteEdge(Vector3Int positiveCell, bool normalIsX)
        {
            PositiveCell = positiveCell;
            NormalIsX = normalIsX;
        }

        // cellからoutward側へ出る面。North/Eastは隣セルが正側、South/Westは自セルが正側
        // The face leaving cell toward outward; North/East make the neighbour the positive side, South/West the cell itself
        public static BeltRouteEdge FromFace(Vector3Int cell, BlockDirection outward, int height)
        {
            var normalIsX = BeltRouteDirection.IsXAxis(outward);
            var positiveSide = outward == BlockDirection.North || outward == BlockDirection.East;
            var positiveCell = positiveSide ? cell + BeltRouteDirection.Forward(outward) : cell;
            return new BeltRouteEdge(new Vector3Int(positiveCell.x, height, positiveCell.z), normalIsX);
        }

        public bool Equals(BeltRouteEdge other)
        {
            return PositiveCell == other.PositiveCell && NormalIsX == other.NormalIsX;
        }

        public override bool Equals(object obj)
        {
            return obj is BeltRouteEdge other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PositiveCell, NormalIsX);
        }

        public override string ToString()
        {
            return $"Edge({PositiveCell}, {(NormalIsX ? "X" : "Z")})";
        }
    }
}

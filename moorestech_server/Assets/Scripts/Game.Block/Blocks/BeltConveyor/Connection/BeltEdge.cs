using System;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    internal readonly struct BeltEdge : IEquatable<BeltEdge>
    {
        internal readonly Vector3Int PositiveCell;
        internal readonly Vector3Int Normal;

        internal BeltEdge(Vector3Int cell, Vector3Int outward, int height)
        {
            // 正側セルと正向き法線で同じedgeを正規化する
            // Normalize the edge using its positive-side cell and positive normal
            Normal = new Vector3Int(Math.Abs(outward.x), 0, Math.Abs(outward.z));
            PositiveCell = cell + (0 < outward.x + outward.z ? Normal : Vector3Int.zero);
            PositiveCell = new Vector3Int(PositiveCell.x, height, PositiveCell.z);
        }

        internal Vector3Int UpperCell(bool positiveSide) => PositiveCell - (positiveSide ? Vector3Int.zero : Normal);
        public bool Equals(BeltEdge other) => PositiveCell == other.PositiveCell && Normal == other.Normal;
        public override bool Equals(object obj) => obj is BeltEdge other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(PositiveCell, Normal);
    }
}

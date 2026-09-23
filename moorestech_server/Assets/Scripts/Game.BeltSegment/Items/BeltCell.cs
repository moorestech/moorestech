using System;
using System.Numerics;

namespace Game.BeltSegment
{
    /// <summary>マスの整数座標。Z=0は地上、負数は地下階層。ToPositionはマス中心のワールド座標を返す。</summary>
    public readonly struct BeltCell : IEquatable<BeltCell>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public BeltCell(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(BeltCell other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is BeltCell && Equals((BeltCell)obj);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((X * 397) ^ Y) * 397 ^ Z;
            }
        }

        public static bool operator ==(BeltCell left, BeltCell right) => left.Equals(right);
        public static bool operator !=(BeltCell left, BeltCell right) => !left.Equals(right);

        public Vector3 ToPosition() => new Vector3(
            X * BeltConstants.ItemWidth, Y * BeltConstants.ItemWidth, Z * BeltConstants.ItemWidth);
    }
}

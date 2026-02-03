using System;
using UnityEngine;

namespace Game.Train.SaveLoad
{
    // Vector3Intをシリアライズ可能な構造体に変換する
    // Serialize Vector3Int with a plain struct
    [Serializable]
    public struct SerializableVector3Int
    {
        public int x, y, z;
        public SerializableVector3Int(int x_, int y_, int z_)
        {
            x = x_;
            y = y_;
            z = z_;
        }

        public static implicit operator SerializableVector3Int(Vector3Int v)
            => new SerializableVector3Int(v.x, v.y, v.z);

        public static implicit operator Vector3Int(SerializableVector3Int s)
            => new Vector3Int(s.x, s.y, s.z);
    }

    // レールノードを特定するための接続先情報
    // Identifier for resolving a rail node
    [Serializable]
    public struct ConnectionDestination : IEquatable<ConnectionDestination>
    {
        public SerializableVector3Int blockPosition { get; set; }
        public int componentIndex { get; set; }
        public bool IsFront { get; set; }
        public static readonly ConnectionDestination Default = new ConnectionDestination(new SerializableVector3Int(-1, -1, -1), -1, false);

        public ConnectionDestination(SerializableVector3Int blockPosition, int componentIndex, bool isFront)
        {
            this.blockPosition = blockPosition;
            this.componentIndex = componentIndex;
            IsFront = isFront;
        }

        public ConnectionDestination(Vector3Int blockPosition, int componentIndex, bool isFront) : this((SerializableVector3Int)blockPosition, componentIndex, isFront)
        {
        }

        public bool Equals(ConnectionDestination other)
            => blockPosition.Equals(other.blockPosition) && componentIndex == other.componentIndex && IsFront == other.IsFront;

        public override bool Equals(object obj)
            => obj is ConnectionDestination other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = blockPosition.x;
                hashCode = (hashCode * 397) ^ blockPosition.y;
                hashCode = (hashCode * 397) ^ blockPosition.z;
                hashCode = (hashCode * 397) ^ componentIndex;
                hashCode = (hashCode * 397) ^ IsFront.GetHashCode();
                return hashCode;
            }
        }
    }

    // 既定値判定の共通ヘルパー
    // Shared helper for default checks
    public static class ConnectionDestinationExtensions
    {
        public static bool IsDefault(this ConnectionDestination dest)
        {
            return dest.Equals(ConnectionDestination.Default);
        }
    }
}

using System;
using UnityEngine;

namespace Game.Block.Component.IOConnector
{
    /// <summary>
    ///     ブロックを北向きに置いた時、どの方向に接続するかどうかを指定する
    ///     X Y Zで表さないのは、この値は相対的な値であるため、絶対的なXYZと誤認しないようにするため
    /// </summary>
    public class ConnectDirection : IEquatable<ConnectDirection>
    {
        public readonly int Front; // Z、北向き
        public readonly int Right; // X、東向き
        public readonly int Up; // Y、上向き
        
        public ConnectDirection(int front, int right, int up)
        {
            Front = front;
            Right = right;
            Up = up;
        }
        
        public ConnectDirection(Vector3Int distance)
        {
            Front = distance.z;
            Right = distance.x;
            Up = distance.y;
        }
        
        public bool Equals(ConnectDirection other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Front == other.Front && Right == other.Right && Up == other.Up;
        }
        
        public Vector3Int ToVector3Int()
        {
            return new Vector3Int(Right, Up, Front);
        }
        
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ConnectDirection)obj);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(Front, Right, Up);
        }
    }
}
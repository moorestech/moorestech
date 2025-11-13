using UnityEngine;
using System;
using System.Collections.Generic;
using ClassLibrary;
using System.ComponentModel;

namespace Game.Train.RailGraph
{
    [Serializable]
    public struct SerializableVector3Int
    {
        public int x, y, z;
        public SerializableVector3Int(int x_, int y_, int z_) { x = x_; y = y_; z = z_; }

        public static implicit operator SerializableVector3Int(Vector3Int v)
            => new SerializableVector3Int(v.x, v.y, v.z);

        // 逆変換
        public static implicit operator Vector3Int(SerializableVector3Int s)
            => new Vector3Int(s.x, s.y, s.z);
    }

    /// <summary>
    /// RailComponentID
    /// railcomponentを座標とIDで一意に識別するための構造体
    /// </summary>
    [Serializable]
    public struct RailComponentID : IEquatable<RailComponentID>
    {
        public SerializableVector3Int Position { get; }//これはブロックが登録されている座標
        public int ID { get; }//そこのブロック座標で何番目のRailComponentか

        public RailComponentID(SerializableVector3Int position, int id)
        {
            Position = position;
            ID = id;
        }
        public static readonly RailComponentID Default = new RailComponentID(new SerializableVector3Int(-1, -1, -1), -1);

        public RailComponentID(Vector3Int pos, int id) : this((SerializableVector3Int)pos, id)
        {
        }

        public bool Equals(RailComponentID other)
            => Position.Equals(other.Position) && ID == other.ID;

        public override bool Equals(object obj)
            => obj is RailComponentID other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var position = Position;
                var hashCode = position.x;
                hashCode = (hashCode * 397) ^ position.y;
                hashCode = (hashCode * 397) ^ position.z;
                hashCode = (hashCode * 397) ^ ID;
                return hashCode;
            }
        }
        /*
        public static bool operator ==(RailComponentID left, RailComponentID right)
            => left.Equals(right);

        public static bool operator !=(RailComponentID left, RailComponentID right)
            => !left.Equals(right);
        */
    }


    /// <summary>
    /// RailNodeを一つを(座標とID)とfront or backで表す構造体
    /// </summary>
    [Serializable]
    public struct ConnectionDestination : IEquatable<ConnectionDestination>
    {
        public RailComponentID railComponentID { get; }
        public bool IsFront { get; }
        public static readonly ConnectionDestination Default = new ConnectionDestination(RailComponentID.Default, true);
        
        public ConnectionDestination(RailComponentID dest, bool front)
        {
            railComponentID = dest;
            IsFront = front;
        }
        
        public bool Equals(ConnectionDestination other)
            => railComponentID.Equals(other.railComponentID) && IsFront == other.IsFront;

        public override bool Equals(object obj)
            => obj is ConnectionDestination other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = railComponentID.GetHashCode();
                hashCode = (hashCode * 397) ^ IsFront.GetHashCode();
                return hashCode;
            }
        }
    }
    public static class ConnectionDestinationExtensions
    {
        public static bool IsDefault(this ConnectionDestination dest)
        {
            return dest.Equals(ConnectionDestination.Default);
        }
    }

    /// <summary>
    /// 1つのRailComponentのセーブ情報
    /// </summary>
    [Serializable]
    public class RailComponentInfo
    {
        public RailComponentID MyID;
        public float BezierStrength;
        public List<ConnectionDestination> ConnectMyFrontTo = new List<ConnectionDestination>();
        public List<ConnectionDestination> ConnectMyBackTo = new List<ConnectionDestination>();
        
        public Vector3JsoObjects RailDirection;
    }

    /// <summary>
    /// RailSaverComponentが管理するデータ全体
    /// </summary>
    [Serializable]
    public class RailSaverData
    {
        // RailComponent1つにつき1要素
        public List<RailComponentInfo> Values = new List<RailComponentInfo>();
    }
}

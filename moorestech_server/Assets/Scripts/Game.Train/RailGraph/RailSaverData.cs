using UnityEngine;
using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    [Serializable]
    public struct SerializableVector3Int
    {
        public int x, y, z;
        public SerializableVector3Int(int x_, int y_, int z_) { x = x_; y = y_; z = z_; }

        // Vector3Int -> SerializableVector3Int
        public static implicit operator SerializableVector3Int(Vector3Int v)
            => new SerializableVector3Int(v.x, v.y, v.z);

        // 逆変換
        public static implicit operator Vector3Int(SerializableVector3Int s)
            => new Vector3Int(s.x, s.y, s.z);
    }

    /// <summary>
    /// RailComponentID
    /// railcomponentを座標とIDで一意に識別するためのクラス
    /// </summary>
    [Serializable]
    public class RailComponentID : IEquatable<RailComponentID>
    {
        public SerializableVector3Int Position { get; set; }//これはブロックが登録されている座標
        public int ID { get; set; }//そこのブロック座標で何番目のRailComponentか

        public RailComponentID()
        {
            Position = default;
            ID = default;
        }

        public RailComponentID(SerializableVector3Int position, int id)
        {
            Position = position;
            ID = id;
        }

        public RailComponentID(Vector3Int pos, int id) : this((SerializableVector3Int)pos, id)
        {
        }

        public bool Equals(RailComponentID other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Position.Equals(other.Position) && ID == other.ID;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is RailComponentID other && Equals(other);
        }

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

        public static bool operator ==(RailComponentID left, RailComponentID right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(RailComponentID left, RailComponentID right)
        {
            return !Equals(left, right);
        }
    }
    /// <summary>
    /// 接続先一つを表すクラス
    /// </summary>
    [Serializable]
    public class ConnectionDestination : IEquatable<ConnectionDestination>
    {
        public RailComponentID DestinationID { get; set; }
        public bool IsFront { get; set; }

        public ConnectionDestination()
        {
            DestinationID = new RailComponentID();
            IsFront = default;
        }

        // コンストラクタ
        public ConnectionDestination(RailComponentID dest, bool front)
        {
            DestinationID = dest;
            IsFront = front;
        }

        public bool Equals(ConnectionDestination other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(DestinationID, other.DestinationID) && IsFront == other.IsFront;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is ConnectionDestination other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = DestinationID != null ? DestinationID.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ IsFront.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(ConnectionDestination left, ConnectionDestination right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ConnectionDestination left, ConnectionDestination right)
        {
            return !Equals(left, right);
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

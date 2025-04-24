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
    public class RailComponentID
    {
        public SerializableVector3Int Position;//これはブロックが登録されている座標
        public int ID;//そこのブロック座標で何番目のRailComponentか

        public RailComponentID(Vector3Int pos, int id)
        {
            Position = pos;
            ID = id;
        }
    }
    /// <summary>
    /// 接続先一つを表すクラス
    /// </summary>
    [Serializable]
    public class ConnectionDestination
    {
        public RailComponentID DestinationID;
        public bool IsFront;
        // コンストラクタ
        public ConnectionDestination(RailComponentID dest, bool front)
        {
            DestinationID = dest;
            IsFront = front;
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

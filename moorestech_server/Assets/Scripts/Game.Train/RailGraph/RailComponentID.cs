using UnityEngine;
using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    /// <summary>
    /// RailComponentID
    /// railcomponentを座標とIDで一意に識別するためのクラス
    /// </summary>
    [Serializable]
    public class RailComponentID
    {
        public Vector3Int Position; // railcomponentがアタッチされているブロックのワールド座標
        public int ID;             // RailSaverComponent内でのインデックス

        public RailComponentID(Vector3Int position, int id)
        {
            Position = position;
            ID = id;
        }
    }

    /// <summary>
    /// 接続先一つを表すクラス
    /// </summary>
    [Serializable]
    public class ConnectionDestination
    {
        // タプル(destinationRailComponentID, isFront)
        public (RailComponentID, bool) Destination;

        public ConnectionDestination(RailComponentID destination, bool isFront)
        {
            Destination = (destination, isFront);
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

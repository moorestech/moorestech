using UnityEngine;
using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    /// <summary>
    /// セーブ・ロード用
    /// </summary>
    
    /// RailComponentID
    /// railcomponentを座標とIDで一意に識別するためのクラス
    
    [Serializable]
    public class RailComponentID
    {
        public Vector3Int Position;//railcomponentがアタッチされてるオブジェクト(ブロック)のワールド座標
        public int ID;//railSaverComponentからみて自分のrailcomponentが何番目か。駅だと2つのrailcomponentを持つため、それぞれにIDを振る
        public RailComponentID(Vector3Int position, int id)
        {
            Position = position;
            ID = id;
        }
    }

    [Serializable]
    public class ConnectionDestination//接続先1つに対応
    {
        public (RailComponentID, bool) destination;
        public ConnectionDestination(RailComponentID destination_, bool isFront)
        {
            destination = (destination_, isFront);
        }
    }

    [Serializable]
    public class RailComponentInfo//1つのRailComponentに1つ対応
    {
        public RailComponentID myID;
        public float bezierStrength;
        public List<ConnectionDestination> connectMyFrontTo = new List<ConnectionDestination>();
        public List<ConnectionDestination> connectMyBackTo = new List<ConnectionDestination>();
    }
    [Serializable]
    public class RailSaverData
    {
        public List<RailComponentInfo> values = new List<RailComponentInfo>();
    }

}
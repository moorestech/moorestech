using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace Game.Train.RailGraph
{
    /// <summary>
    /// セーブ・ロード用
    /// railcomponentを座標とIDで一意に識別するためのクラス
    /// </summary>
    public class RailComponentID
    {
        public Vector3Int Position { get; }//railcomponentがアタッチされてるオブジェクトのワールド座標
        public int ID { get; }//railSaverComponentからみて自分のrailcomponentが何番目か。駅だと2つのrailcomponentを持つため、それぞれにIDを振る
        public RailComponentID(Vector3Int position, int id)
        {
            Position = position;
            ID = id;
        }
        //セーブ用に文字列化
        public string GetSaveState()
        {
            return Position.x + "," + Position.y + "," + Position.z + "," + ID;
        }
        //ロード用に文字列から復元
        public static RailComponentID Load(string saveData)
        {
            var data = saveData.Split(',');
            return new RailComponentID(new Vector3Int(int.Parse(data[0]), int.Parse(data[1]), int.Parse(data[2])), int.Parse(data[3]));
        }
    }

}
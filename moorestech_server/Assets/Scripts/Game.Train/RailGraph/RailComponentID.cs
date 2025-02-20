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
        public int IsFront { get; }//表1か裏0か

        public RailComponentID(Vector3Int position, int id, bool isFront)
        {
            Position = position;
            ID = id;
            IsFront = isFront ? 1 : 0;
        }
        public RailComponentID Return_Reverse_Front() 
        {
            return new RailComponentID(Position, ID, IsFront == 0);
        }
        //セーブ用に文字列化
        public string GetSaveState()
        {
            return Position.x + "," + Position.y + "," + Position.z + "," + ID + "," + IsFront;
        }
        //ロード用に文字列から復元
        public static RailComponentID Load(string saveData)
        {
            var data = saveData.Split(',');
            return new RailComponentID(new Vector3Int(int.Parse(data[0]), int.Parse(data[1]), int.Parse(data[2])), int.Parse(data[3]), int.Parse(data[4]) == 1);
        }

        public (Vector3Int, int, bool) GetInfo()
        {
            return (Position, ID, IsFront == 1);
        }
    }

}
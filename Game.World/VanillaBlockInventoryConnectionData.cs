using System.Collections.Generic;
using Core.Block.Config;

namespace World
{
    /// <summary>
    /// ベルトコンベアや機械などのインベントリのあるブロックが、どの方角にあるブロックとつながるかを指定するクラス
    /// 北向きを基準として、つながる方向を指定する
    /// </summary>
    public class VanillaBlockInventoryConnectionData
    {
        public Dictionary<string, ConnectionPosition[]> Get()
        {
            return new Dictionary<string, ConnectionPosition[]>
            {
                {VanillaBlockType.Machine,new ConnectionPosition[]{new (1,0),new (-1,0),new (0,1),new (0,-1)}},
                {VanillaBlockType.BeltConveyor,new ConnectionPosition[]{new (1,0)}}
            };
        }
    }

    public class ConnectionPosition
    {
        public ConnectionPosition(int north, int east)
        {
            North = north;
            East = east;
        }

        public readonly int North;
        public readonly int East;
    }
}
using System;
using System.Collections.Generic;
using Core.Block.Config;

namespace World.EventListener
{
    /// <summary>
    /// ベルトコンベアや機械などのインベントリのあるブロックが、どの方角にあるブロックとつながるかを指定するクラス
    /// 北向きを基準として、つながる方向を指定する
    /// </summary>
    public class VanillaBlockInventoryConnectionData
    {
        public Dictionary<string, IoConnectionData> Get()
        {
            return new Dictionary<string, IoConnectionData>
            {
                {VanillaBlockType.Machine,new IoConnectionData(
                    new ConnectionPosition[]{new (1,0),new (-1,0),new (0,1),new (0,-1)},
                    new ConnectionPosition[]{new (1,0),new (-1,0),new (0,1),new (0,-1)})},
                
                {VanillaBlockType.BeltConveyor,new IoConnectionData(
                    new ConnectionPosition[]{new (-1,0),new (0,1),new (0,-1)},
                    new ConnectionPosition[]{new (1,0)})}
            };
        }
    }

    /// <summary>
    /// 入力位置と出力位置を指定するクラス
    /// 北向きを基準として、入出力方向を指定する
    /// </summary>
    public class IoConnectionData
    {
        public readonly ConnectionPosition[] InputConnector;
        public readonly ConnectionPosition[] OutputConnector;

        public IoConnectionData(ConnectionPosition[] inputConnector, ConnectionPosition[] outputConnector)
        {
            InputConnector = inputConnector;
            OutputConnector = outputConnector;
        }
    }
    public class ConnectionPosition : IEquatable<ConnectionPosition>
    {
        public ConnectionPosition(int north, int east)
        {
            North = north;
            East = east;
        }

        public readonly int North;
        public readonly int East;

        public bool Equals(ConnectionPosition other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return North == other.North && East == other.East;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ConnectionPosition) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(North, East);
        }
    }
}
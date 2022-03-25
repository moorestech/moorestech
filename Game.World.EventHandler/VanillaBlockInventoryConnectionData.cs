using System;
using System.Collections.Generic;
using Core.Block.Config;

namespace Game.World.EventHandler
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
                {
                    VanillaBlockType.Machine, new IoConnectionData(
                        new ConnectDirection[] {new(1, 0), new(-1, 0), new(0, 1), new(0, -1)},
                        new ConnectDirection[] {new(1, 0), new(-1, 0), new(0, 1), new(0, -1)})
                },
                {
                    VanillaBlockType.Chest, new IoConnectionData(
                        new ConnectDirection[] {new(1, 0), new(-1, 0), new(0, 1), new(0, -1)},
                        new ConnectDirection[] {new(1, 0), new(-1, 0), new(0, 1), new(0, -1)})
                },

                {
                    VanillaBlockType.BeltConveyor, new IoConnectionData(
                        // 南、西、東をからの接続を受け、アイテムをインプットする
                        new ConnectDirection[] {new(-1, 0), new(0, 1), new(0, -1)},
                        //北向きに出力する
                        new ConnectDirection[] {new(1, 0)})
                }
            };
        }
    }

    /// <summary>
    /// 入力位置と出力位置を指定するクラス
    /// 北向きを基準として、入出力方向を指定する
    /// </summary>
    public class IoConnectionData
    {
        public readonly ConnectDirection[] InputConnector;
        public readonly ConnectDirection[] OutputConnector;

        public IoConnectionData(ConnectDirection[] inputConnector, ConnectDirection[] outputConnector)
        {
            InputConnector = inputConnector;
            OutputConnector = outputConnector;
        }
    }

    public class ConnectDirection : IEquatable<ConnectDirection>
    {
        public ConnectDirection(int north, int east)
        {
            North = north;
            East = east;
        }

        public readonly int North;
        public readonly int East;

        public bool Equals(ConnectDirection other)
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
            return Equals((ConnectDirection) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(North, East);
        }
    }
}
using System;
using System.Collections.Generic;
using Game.Block;
using UnityEngine;

namespace Game.World.EventHandler.InventoryEvent
{
    /// <summary>
    ///     ベルトコンベアや機械などのインベントリのあるブロックが、どの方角にあるブロックとつながるかを指定するクラス
    ///     北向きを基準として、つながる方向を指定する
    /// </summary>
    public static class VanillaBlockInventoryConnectionData
    {
        public static readonly Dictionary<string, IoConnectionData> IoConnectionData = new()
        {
            {
                VanillaBlockType.Machine,
                new IoConnectionData(
                    new ConnectDirection[] { new(1, 0,0), new(-1, 0,0), new(0, 1,0), new(0, -1,0) },
                    new ConnectDirection[] { new(1, 0,0), new(-1, 0,0), new(0, 1,0), new(0, -1,0) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Chest,
                new IoConnectionData(
                    new ConnectDirection[] { new(1, 0,0), new(-1, 0,0), new(0, 1,0), new(0, -1,0) },
                    new ConnectDirection[] { new(1, 0,0), new(-1, 0,0), new(0, 1,0), new(0, -1,0) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Generator,
                new IoConnectionData(
                    new ConnectDirection[] { new(1, 0,0), new(-1, 0,0), new(0, 1,0), new(0, -1,0) },
                    new ConnectDirection[] { },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Miner,
                new IoConnectionData(
                    new ConnectDirection[] { },
                    new ConnectDirection[] { new(1, 0,0), new(-1, 0,0), new(0, 1,0), new(0, -1,0) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.BeltConveyor, new IoConnectionData(
                    // 南、西、東をからの接続を受け、アイテムをインプットする
                    new ConnectDirection[] { new(-1, 0,0), new(0, 1,0), new(0, -1,0) },
                    //北向きに出力する
                    new ConnectDirection[] { new(1, 0,0) },
                    new[]
                    {
                        VanillaBlockType.Machine, VanillaBlockType.Chest, VanillaBlockType.Generator,
                        VanillaBlockType.Miner, VanillaBlockType.BeltConveyor
                    })
            }
        };
    }


    /// <summary>
    ///     入力位置と出力位置を指定するクラス
    ///     北向きを基準として、入出力方向を指定する
    /// </summary>
    public class IoConnectionData
    {
        public readonly string[] ConnectableBlockType;
        public readonly ConnectDirection[] InputConnector;
        public readonly ConnectDirection[] OutputConnector;

        public IoConnectionData(ConnectDirection[] inputConnector, ConnectDirection[] outputConnector, string[] connectableBlockType)
        {
            InputConnector = inputConnector;
            OutputConnector = outputConnector;
            ConnectableBlockType = connectableBlockType;
        }
    }

    /// <summary>
    /// ブロックを北向きに置いた時、どの方向に接続するかどうかを指定する
    /// X Y Zで表さないのは、この値は相対的な値であるため、絶対的なXYZと誤認しないようにするため
    /// </summary>
    public class ConnectDirection : IEquatable<ConnectDirection>
    {
        public readonly int Front; // Z、北向き
        public readonly int Right; // X、東向き
        public readonly int Up; // Y、上向き

        public ConnectDirection(int front, int right, int up)
        {
            Front = front;
            Right = right;
            Up = up;
        }
        
        public ConnectDirection(Vector3Int distance)
        {
            Front = distance.z;
            Right = distance.x;
            Up = distance.y;
        }

        public bool Equals(ConnectDirection other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Front == other.Front && Right == other.Right && Up == other.Up;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ConnectDirection)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Front, Right, Up);
        }
    }
}
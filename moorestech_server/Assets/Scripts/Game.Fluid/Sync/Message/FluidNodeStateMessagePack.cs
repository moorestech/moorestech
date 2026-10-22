using System;
using Core.Master;
using Game.Fluid.Sync.State;
using MessagePack;
using UnityEngine;

namespace Game.Fluid.Sync.Message
{
    // 全量に載せるノード1本の通信形
    // Wire form of one node in the full state
    [MessagePackObject]
    public class FluidNodeStateMessagePack
    {
        [Key(0)] public int X { get; set; }
        [Key(1)] public int Y { get; set; }
        [Key(2)] public int Z { get; set; }
        [Key(3)] public double Capacity { get; set; }
        [Key(4)] public double Amount { get; set; }
        [Key(5)] public int FluidId { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public FluidNodeStateMessagePack() { }

        public FluidNodeStateMessagePack(in FluidNodeState state)
        {
            X = state.Position.x;
            Y = state.Position.y;
            Z = state.Position.z;
            Capacity = state.Capacity;
            Amount = state.Amount;
            FluidId = state.FluidId.AsPrimitive();
        }

        public FluidNodeState ToState()
        {
            return new FluidNodeState(new Vector3Int(X, Y, Z), Capacity, Amount, new FluidId(FluidId));
        }
    }
}

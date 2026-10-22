using System;
using Game.Fluid.Sync.State;
using MessagePack;

namespace Game.Fluid.Sync.Message
{
    // 全量に載せる面1枚の通信形
    // Wire form of one face in the full state
    [MessagePackObject]
    public class FluidFaceStateMessagePack
    {
        [Key(0)] public int NodeAIndex { get; set; }
        [Key(1)] public int NodeBIndex { get; set; }
        [Key(2)] public double FlowCapacityPerTick { get; set; }
        [Key(3)] public double Velocity { get; set; }
        [Key(4)] public bool AllowAToB { get; set; }
        [Key(5)] public bool AllowBToA { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public FluidFaceStateMessagePack() { }

        public FluidFaceStateMessagePack(in FluidFaceState state)
        {
            NodeAIndex = state.NodeAIndex;
            NodeBIndex = state.NodeBIndex;
            FlowCapacityPerTick = state.FlowCapacityPerTick;
            Velocity = state.Velocity;
            AllowAToB = state.AllowAToB;
            AllowBToA = state.AllowBToA;
        }

        public FluidFaceState ToState()
        {
            return new FluidFaceState(NodeAIndex, NodeBIndex, FlowCapacityPerTick, Velocity, AllowAToB, AllowBToA);
        }
    }
}

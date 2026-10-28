using System;
using Game.Fluid.Sync.State;
using MessagePack;

namespace Game.Fluid.Sync.Message
{
    // 全量に載せる境界ポート1つの通信形(上限・速度は単位の整数)
    // Wire form of one boundary port in the full state (cap and velocity as integer units)
    [MessagePackObject]
    public class FluidPortStateMessagePack
    {
        [Key(0)] public int NodeIndex { get; set; }
        [Key(1)] public long FlowCapacityPerTick { get; set; }
        [Key(2)] public long Velocity { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public FluidPortStateMessagePack() { }

        public FluidPortStateMessagePack(in FluidPortState state)
        {
            NodeIndex = state.NodeIndex;
            FlowCapacityPerTick = state.FlowCapacityPerTick;
            Velocity = state.Velocity;
        }

        public FluidPortState ToState()
        {
            return new FluidPortState(NodeIndex, FlowCapacityPerTick, Velocity);
        }
    }
}

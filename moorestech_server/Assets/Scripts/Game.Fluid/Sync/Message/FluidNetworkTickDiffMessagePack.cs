using System;
using Core.Master;
using Game.Fluid.Sync.Diff;
using MessagePack;

namespace Game.Fluid.Sync.Message
{
    // 1tick分の境界の出入りの通信形(量は単位の整数)。差分が空でも毎tick送られ、クライアントが複製を1tick進める合図になる
    // Wire form of one tick's boundary exchange (amounts as integer units); sent every tick even when empty, as the client's cue to advance its replica one tick
    [MessagePackObject]
    public class FluidNetworkTickDiffMessagePack
    {
        [Key(0)] public uint ServerTick { get; set; }
        [Key(1)] public uint TickSequenceId { get; set; }

        // 渡した量はポート番号の列と量の列を同じ添字で対にする。入れた量はノード番号・流体ID・量の3列を同じ添字で対にする
        // Deliveries pair a port-index array with an amount array by position; inflows pair node-index, fluid-id and amount arrays the same way
        [Key(2)] public int[] DeliveryPortIndices { get; set; }
        [Key(3)] public long[] DeliveryUnits { get; set; }
        [Key(4)] public int[] InflowNodeIndices { get; set; }
        [Key(5)] public int[] InflowFluidIds { get; set; }
        [Key(6)] public long[] InflowUnits { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public FluidNetworkTickDiffMessagePack() { }

        public FluidNetworkTickDiffMessagePack(uint serverTick, uint tickSequenceId, FluidTickDiff diff)
        {
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
            DeliveryPortIndices = new int[diff.Deliveries.Length];
            DeliveryUnits = new long[diff.Deliveries.Length];
            for (var i = 0; i < diff.Deliveries.Length; i++)
            {
                DeliveryPortIndices[i] = diff.Deliveries[i].PortIndex;
                DeliveryUnits[i] = diff.Deliveries[i].DeliveredUnits;
            }
            InflowNodeIndices = new int[diff.Inflows.Length];
            InflowFluidIds = new int[diff.Inflows.Length];
            InflowUnits = new long[diff.Inflows.Length];
            for (var i = 0; i < diff.Inflows.Length; i++)
            {
                InflowNodeIndices[i] = diff.Inflows[i].NodeIndex;
                InflowFluidIds[i] = diff.Inflows[i].FluidId.AsPrimitive();
                InflowUnits[i] = diff.Inflows[i].AcceptedUnits;
            }
        }

        public FluidTickDiff ToDiff()
        {
            var deliveries = new FluidPortDeliveryRecord[DeliveryPortIndices.Length];
            for (var i = 0; i < deliveries.Length; i++) deliveries[i] = new FluidPortDeliveryRecord(DeliveryPortIndices[i], DeliveryUnits[i]);
            var inflows = new FluidNodeInflowRecord[InflowNodeIndices.Length];
            for (var i = 0; i < inflows.Length; i++) inflows[i] = new FluidNodeInflowRecord(InflowNodeIndices[i], new FluidId(InflowFluidIds[i]), InflowUnits[i]);
            return new FluidTickDiff(deliveries, inflows);
        }
    }
}

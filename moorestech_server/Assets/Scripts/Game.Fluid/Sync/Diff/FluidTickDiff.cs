using System;

namespace Game.Fluid.Sync.Diff
{
    // 1tick分の境界の出入り。渡した量は流体tick中にポート番号順、入れた量はその後のブロック更新で起きた順
    // One tick's boundary exchange: deliveries in port order during the fluid tick, inflows in the order they happened during the following block updates
    public sealed class FluidTickDiff
    {
        public static readonly FluidTickDiff Empty = new(Array.Empty<FluidPortDeliveryRecord>(), Array.Empty<FluidNodeInflowRecord>());

        public readonly FluidPortDeliveryRecord[] Deliveries;
        public readonly FluidNodeInflowRecord[] Inflows;

        public FluidTickDiff(FluidPortDeliveryRecord[] deliveries, FluidNodeInflowRecord[] inflows)
        {
            Deliveries = deliveries;
            Inflows = inflows;
        }
    }
}

namespace Core.BeltTransport
{
    public sealed class BeltTickDifference
    {
        public readonly ulong Tick;
        public readonly BeltBoundaryChange[] BeforeTick, AfterTick;
        public readonly BeltOutputResult[] Outputs;
        public readonly BeltTickOrder Order;
        public BeltTickDifference(ulong tick, BeltBoundaryChange[] beforeTick, BeltOutputResult[] outputs, BeltBoundaryChange[] afterTick, BeltTickOrder order)
        { Tick = tick; BeforeTick = beforeTick; Outputs = outputs; AfterTick = afterTick; Order = order; }
    }
}

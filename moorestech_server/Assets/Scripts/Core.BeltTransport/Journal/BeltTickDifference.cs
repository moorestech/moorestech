namespace Core.BeltTransport
{
    public sealed class BeltTickDifference
    {
        public readonly ulong Tick;
        public readonly BeltBoundaryChange[] BeforeTick, AfterTick;
        public readonly BeltOutputResult[] Outputs;
        public BeltTickDifference(ulong tick, BeltBoundaryChange[] beforeTick, BeltOutputResult[] outputs, BeltBoundaryChange[] afterTick)
        { Tick = tick; BeforeTick = beforeTick; Outputs = outputs; AfterTick = afterTick; }
    }
}

using Core.Master;

namespace Game.Fluid.Sync.Diff
{
    // 機械がパイプへ入れ、ノードが受け入れた量(単位)。拒否された残りは含まないので、複製は確認なしにそのまま足す
    // The units a machine pushed into a pipe and the node accepted; the rejected remainder is excluded, so the replica adds it without re-checking
    public readonly struct FluidNodeInflowRecord
    {
        public readonly int NodeIndex;
        public readonly FluidId FluidId;
        public readonly long AcceptedUnits;

        public FluidNodeInflowRecord(int nodeIndex, FluidId fluidId, long acceptedUnits)
        {
            NodeIndex = nodeIndex;
            FluidId = fluidId;
            AcceptedUnits = acceptedUnits;
        }
    }
}

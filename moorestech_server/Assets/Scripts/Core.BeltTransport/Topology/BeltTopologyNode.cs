using System.Collections.Generic;

namespace Core.BeltTransport
{
    internal sealed class BeltTopologyNode
    {
        internal readonly BeltNetworkCell Cell;
        internal readonly List<BeltNetworkConnection> Inputs = new List<BeltNetworkConnection>();
        internal readonly List<BeltNetworkConnection> Outputs = new List<BeltNetworkConnection>();
        internal bool Assigned;
        internal BeltSegmentKind Kind => 1 < Inputs.Count ? BeltSegmentKind.Merge
            : 1 < Outputs.Count ? BeltSegmentKind.Branch : BeltSegmentKind.Normal;

        internal BeltTopologyNode(BeltNetworkCell cell) { Cell = cell; }
    }
}

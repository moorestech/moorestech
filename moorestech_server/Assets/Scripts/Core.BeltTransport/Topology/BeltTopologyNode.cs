using System.Collections.Generic;

namespace Core.BeltTransport
{
    internal sealed class BeltTopologyNode
    {
        internal readonly BeltNetworkCell Cell;
        internal readonly List<BeltNetworkConnection> Inputs = new List<BeltNetworkConnection>();
        internal readonly List<BeltNetworkConnection> Outputs = new List<BeltNetworkConnection>();
        internal bool Assigned;
        internal BeltSegmentKind Kind => Inputs.Count > 1 ? BeltSegmentKind.Merge
            : Outputs.Count > 1 ? BeltSegmentKind.Branch : BeltSegmentKind.Normal;

        internal BeltTopologyNode(BeltNetworkCell cell) { Cell = cell; }
    }
}

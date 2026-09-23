using System.Collections.Generic;
namespace Game.Block.Blocks.BeltConveyor
{
    internal sealed class BeltTopologyNode
    {
        internal readonly SegmentBeltComponent Belt;
        internal readonly List<BeltTopologyEdge> Incoming = new(), Outgoing = new();
        internal BeltTopologyNode(SegmentBeltComponent belt) => Belt = belt;
        internal bool IsMerge => 2 <= Incoming.Count;
    }
}

using System;
using Core.BeltTransport;
using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    [Union(0, typeof(BeltSpeedChangeMessagePack))]
    [Union(1, typeof(BeltInputChangeMessagePack))]
    [Union(2, typeof(BeltCellItemsChangeMessagePack))]
    [Union(3, typeof(BeltTopologyChangeMessagePack))]
    public abstract class BeltChangeMessagePack
    {
        public abstract BeltBoundaryChange ToCore();
        public static BeltChangeMessagePack FromCore(BeltBoundaryChange change) => change switch
        {
            BeltSpeedChange speed => new BeltSpeedChangeMessagePack(speed),
            BeltInputChange input => new BeltInputChangeMessagePack(input),
            BeltCellItemsChange items => new BeltCellItemsChangeMessagePack(items),
            BeltTopologyChange topology => new BeltTopologyChangeMessagePack(topology),
            _ => throw new ArgumentException($"Unknown belt boundary change: {change.GetType()}.")
        };
    }
}

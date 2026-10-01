using System;
using System.Linq;
using Core.BeltTransport;
using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltTopologyChangeMessagePack : BeltChangeMessagePack
    {
        [Key(0)] public BeltCellMessagePack[] ChangedCells { get; }
        [Key(1)] public int[] RemovedCells { get; }
        [Key(2)] public BeltConnectionMessagePack[] AddedConnections { get; }
        [Key(3)] public BeltConnectionMessagePack[] RemovedConnections { get; }
        [Key(4)] public BeltCellItemMessagePack[] AddedItems { get; }
        [SerializationConstructor]
        public BeltTopologyChangeMessagePack(BeltCellMessagePack[] changedCells, int[] removedCells, BeltConnectionMessagePack[] addedConnections, BeltConnectionMessagePack[] removedConnections, BeltCellItemMessagePack[] addedItems)
        {
            ChangedCells = changedCells;
            RemovedCells = removedCells;
            AddedConnections = addedConnections;
            RemovedConnections = removedConnections;
            AddedItems = addedItems;
        }
        [Obsolete("Reserved for MessagePack.")]
        public BeltTopologyChangeMessagePack() { }
        public BeltTopologyChangeMessagePack(BeltTopologyChange value)
        {
            ChangedCells = value.ChangedCells.Select(v => new BeltCellMessagePack(v)).ToArray();
            RemovedCells = value.RemovedCells;
            AddedConnections = value.AddedConnections.Select(v => new BeltConnectionMessagePack(v)).ToArray();
            RemovedConnections = value.RemovedConnections.Select(v => new BeltConnectionMessagePack(v)).ToArray();
            AddedItems = value.AddedItems.Select(v => new BeltCellItemMessagePack(v)).ToArray();
        }
        public override BeltBoundaryChange ToCore() => new BeltTopologyChange(ChangedCells.Select(v => v.ToCore()).ToArray(), RemovedCells,
            AddedConnections.Select(v => v.ToCore()).ToArray(), RemovedConnections.Select(v => v.ToCore()).ToArray(), AddedItems.Select(v => v.ToCore()).ToArray());
    }
}

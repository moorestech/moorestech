using System;
using System.Linq;
using Core.BeltTransport;
using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltCellItemsChangeMessagePack : BeltChangeMessagePack
    {
        [Key(0)] public int CellId { get; }
        [Key(1)] public BeltCellItemMessagePack[] Items { get; }
        [SerializationConstructor]
        public BeltCellItemsChangeMessagePack(int cellId, BeltCellItemMessagePack[] items)
        {
            CellId = cellId;
            Items = items;
        }
        [Obsolete("Reserved for MessagePack.")]
        public BeltCellItemsChangeMessagePack() { }
        public BeltCellItemsChangeMessagePack(BeltCellItemsChange value)
        {
            CellId = value.CellId;
            Items = value.Items.Select(v => new BeltCellItemMessagePack(v)).ToArray();
        }
        public override BeltBoundaryChange ToCore() => new BeltCellItemsChange(CellId, Items.Select(v => v.ToCore()).ToArray());
    }
}

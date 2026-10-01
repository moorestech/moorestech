using System;
using Core.BeltTransport;
using MessagePack;

namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltPriorityMessagePack
    {
        [Key(0)] public int CellId { get; }
        [Key(1)] public int Order { get; }

        [SerializationConstructor]
        public BeltPriorityMessagePack(int cellId, int order)
        {
            CellId = cellId;
            Order = order;
        }
        [Obsolete("Reserved for MessagePack.")]
        public BeltPriorityMessagePack() { }

        public BeltPriorityMessagePack(BeltCellPriority value)
        {
            CellId = value.CellId;
            Order = value.Order;
        }

        public BeltCellPriority ToCore() => new(CellId, Order);
    }
}

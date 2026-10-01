using System;
using Core.BeltTransport;
using Core.Master;
using MessagePack;

namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltItemMessagePack
    {
        [Key(0)] public Guid Guid { get; }
        [Key(1)] public ItemId ItemId { get; }

        [SerializationConstructor]
        public BeltItemMessagePack(Guid guid, ItemId itemId)
        {
            Guid = guid;
            ItemId = itemId;
        }
        [Obsolete("Reserved for MessagePack.")]
        public BeltItemMessagePack() { }

        public BeltItemMessagePack(BeltItem value)
        {
            Guid = value.Guid;
            ItemId = new ItemId(value.ItemId);
        }

        public BeltItem ToCore() => new(Guid, ItemId.AsPrimitive());
    }
}

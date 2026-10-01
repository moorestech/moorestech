using System;
using Core.BeltTransport;
using MessagePack;

namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltConnectionMessagePack
    {
        [Key(0)] public int SourceId { get; }
        [Key(1)] public int TargetId { get; }
        [Key(2)] public bool SourceIsBelt { get; }
        [Key(3)] public bool TargetIsBelt { get; }
        [Key(4)] public BeltDirection Direction { get; }
        [Key(5)] public int EntryHeight { get; }

        [SerializationConstructor]
        public BeltConnectionMessagePack(int sourceId, int targetId, bool sourceIsBelt, bool targetIsBelt, BeltDirection direction, int entryHeight)
        {
            SourceId = sourceId;
            TargetId = targetId;
            SourceIsBelt = sourceIsBelt;
            TargetIsBelt = targetIsBelt;
            Direction = direction;
            EntryHeight = entryHeight;
        }
        [Obsolete("Reserved for MessagePack.")]
        public BeltConnectionMessagePack() { }

        public BeltConnectionMessagePack(BeltNetworkConnection value)
        {
            SourceId = value.SourceId;
            TargetId = value.TargetId;
            SourceIsBelt = value.SourceIsBelt;
            TargetIsBelt = value.TargetIsBelt;
            Direction = value.Direction;
            EntryHeight = value.EntryHeight;
        }

        public BeltNetworkConnection ToCore() => new(SourceId, TargetId, SourceIsBelt, TargetIsBelt, Direction, EntryHeight);
    }
}

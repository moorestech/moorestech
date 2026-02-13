using System;
using MessagePack;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class RailConnectionCreatedMessagePack
    {
        [Key(0)] public int FromNodeId { get; set; }
        [Key(1)] public Guid FromGuid { get; set; }
        [Key(2)] public int ToNodeId { get; set; }
        [Key(3)] public Guid ToGuid { get; set; }
        [Key(4)] public int Distance { get; set; }
        [Key(5)] public uint ServerTick { get; set; }
        [Key(6)] public Guid RailTypeGuid { get; set; }
        [Key(7)] public bool IsDrawable { get; set; }
        [Key(8)] public uint TickSequenceId { get; set; }

        [Obsolete("For serialization")]
        public RailConnectionCreatedMessagePack()
        {
        }

        public RailConnectionCreatedMessagePack(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, int distance, uint serverTick, Guid railTypeGuid, bool isDrawable, uint tickSequenceId)
        {
            FromNodeId = fromNodeId;
            FromGuid = fromGuid;
            ToNodeId = toNodeId;
            ToGuid = toGuid;
            Distance = distance;
            ServerTick = serverTick;
            RailTypeGuid = railTypeGuid;
            IsDrawable = isDrawable;
            TickSequenceId = tickSequenceId;
        }
    }
}

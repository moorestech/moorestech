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
        [Key(5)] public long Tick { get; set; }
        [Key(6)] public Guid RailTypeGuid { get; set; }
        [Key(7)] public bool IsDrawable { get; set; }

        [Obsolete("For serialization")]
        public RailConnectionCreatedMessagePack()
        {
        }

        public RailConnectionCreatedMessagePack(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, int distance, long tick, Guid railTypeGuid, bool isDrawable)
        {
            FromNodeId = fromNodeId;
            FromGuid = fromGuid;
            ToNodeId = toNodeId;
            ToGuid = toGuid;
            Distance = distance;
            Tick = tick;
            RailTypeGuid = railTypeGuid;
            IsDrawable = isDrawable;
        }
    }
}

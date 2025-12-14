using System;
using MessagePack;

namespace Server.Util.MessagePack
{
    /// <summary>
    ///     RailConnection削除を表現するMessagePackモデル
    ///     Message payload representing a removed rail connection
    /// </summary>
    [MessagePackObject]
    public class RailConnectionRemovedMessagePack
    {
        [Key(0)] public int FromNodeId { get; set; }
        [Key(1)] public Guid FromGuid { get; set; }
        [Key(2)] public int ToNodeId { get; set; }
        [Key(3)] public Guid ToGuid { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public RailConnectionRemovedMessagePack()
        {
        }

        public RailConnectionRemovedMessagePack(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
        {
            FromNodeId = fromNodeId;
            FromGuid = fromGuid;
            ToNodeId = toNodeId;
            ToGuid = toGuid;
        }
    }
}

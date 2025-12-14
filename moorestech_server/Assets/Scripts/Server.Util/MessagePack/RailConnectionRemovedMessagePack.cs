using System;
using MessagePack;

namespace Server.Util.MessagePack
{
    /// <summary>
    ///     RailConnection蜿門ｾ励ゅ′繝｡繝・そ繝ｼ繧ｸ蜑･螟悶＠縺�・
    ///     Message payload representing a removed rail connection
    /// </summary>
    [MessagePackObject]
    public class RailConnectionRemovedMessagePack
    {
        [Key(0)] public int FromNodeId { get; set; }
        [Key(1)] public Guid FromGuid { get; set; }
        [Key(2)] public int ToNodeId { get; set; }
        [Key(3)] public Guid ToGuid { get; set; }

        [Obsolete("繝・す繝ｪ繧｢繝ｩ繧､繧ｺ逕ｨ繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ縺ｧ縺吶・")]
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

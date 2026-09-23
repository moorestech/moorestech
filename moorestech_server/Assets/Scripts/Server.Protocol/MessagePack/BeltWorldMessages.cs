using System;
using MessagePack;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack.BeltSegment;
namespace Server.Protocol.MessagePack
{
    [MessagePackObject]
    public sealed class GetBeltWorldRequest : ProtocolMessagePackBase
    {
        [Obsolete("For deserialization only.")] public GetBeltWorldRequest() { }
        private GetBeltWorldRequest(string tag) { Tag = tag; }
        public static GetBeltWorldRequest Create() => new(GetBeltWorldProtocol.ProtocolTag);
    }
    [MessagePackObject]
    public sealed class GetBeltWorldResponse : ProtocolMessagePackBase
    {
        [Key(2)] public BeltWorldSnapshotMessagePack Snapshot { get; set; }
        [Obsolete("For deserialization only.")] public GetBeltWorldResponse() { }
        public GetBeltWorldResponse(BeltWorldSnapshotMessagePack snapshot)
        { Tag = GetBeltWorldProtocol.ProtocolTag; Snapshot = snapshot; }
    }
}

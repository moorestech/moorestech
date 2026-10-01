using System;
using Game.Block.Blocks.BeltConveyor.Transport;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack.BeltTransport;
namespace Server.Protocol.PacketResponse
{
    public sealed class GetBeltSnapshotProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getBeltSnapshot";
        private readonly BeltWorldTransport _transport;
        public GetBeltSnapshotProtocol(ServiceProvider services) { _transport = services.GetRequiredService<BeltWorldTransport>(); }
        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            MessagePackSerializer.Deserialize<Request>(payload);
            // TickEndの途中でも最後に確定した同一境界だけを返す。
            // Return the last committed boundary even when requested midway through TickEnd.
            return new Response(new BeltSnapshotMessagePack(_transport.CaptureCommittedSnapshot()));
        }
        [MessagePackObject]
        public sealed class Request : ProtocolMessagePackBase
        {
            [Obsolete("Reserved for MessagePack.")]
            public Request() { }
            private Request(string tag) { Tag = tag; }
            public static Request Create() => new Request(ProtocolTag);
        }
        [MessagePackObject]
        public sealed class Response : ProtocolMessagePackBase
        {
            [Key(2)] public BeltSnapshotMessagePack Snapshot { get; }
            [Obsolete("Reserved for MessagePack.")]
            public Response() { }
            [SerializationConstructor]
            public Response(string tag, int sequenceId, BeltSnapshotMessagePack snapshot)
            { Tag = tag; SequenceId = sequenceId; Snapshot = snapshot; }
            public Response(BeltSnapshotMessagePack snapshot) { Tag = ProtocolTag; Snapshot = snapshot; }
        }
    }
}

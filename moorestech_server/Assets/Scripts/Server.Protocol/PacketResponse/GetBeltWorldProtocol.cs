using Game.Block.Blocks.BeltConveyor;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.MessagePack;
using Server.Util.MessagePack.BeltSegment;
namespace Server.Protocol.PacketResponse
{
    public sealed class GetBeltWorldProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getBeltWorld";
        private readonly IBeltWorldLookup world;
        public GetBeltWorldProtocol(ServiceProvider services) => world = services.GetRequiredService<IBeltWorldLookup>();
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            // 既存tick末尾の要求境界で、確定済み状態だけを読む。
            // Read completed state only at the existing tick-end request boundary.
            return new GetBeltWorldResponse(new BeltWorldSnapshotMessagePack(world.CaptureSnapshot()));
        }
    }
}

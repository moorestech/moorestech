using Core.BeltTransport;
using MessagePack;
using Server.Util.MessagePack.BeltTransport;
namespace Tests.Util.BeltTransport
{
    internal static class BeltWireRoundTrip
    {
        internal static BeltTickDifference Tick(BeltTickDifference tick)
        {
            // 全ライフサイクル検証でも実際のwire表現を通す。
            // Exercise the actual wire representation throughout lifecycle tests.
            return MessagePackSerializer.Deserialize<BeltTickMessagePack>(MessagePackSerializer.Serialize(new BeltTickMessagePack(tick, (uint)tick.Tick, 1))).ToCore();
        }
    }
}

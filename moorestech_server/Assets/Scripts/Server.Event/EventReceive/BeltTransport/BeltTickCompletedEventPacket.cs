using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Context;
using MessagePack;
using Server.Util.MessagePack.BeltTransport;
using UniRx;
namespace Server.Event.EventReceive
{
    public sealed class BeltTickCompletedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:beltTickCompleted";
        private readonly EventProtocolProvider _events;
        private readonly BeltWorldTransport _transport;
        public BeltTickCompletedEventPacket(EventProtocolProvider events, BeltWorldTransport transport)
        { _events = events; _transport = transport; }
        public void Load() => _transport.OnTickCompleted.Subscribe(Publish);
        private void Publish(BeltTickDifference difference)
        {
            // 全段階と配置変更が確定した1tickを一つの通知にする。
            // Publish all completed stages and topology mutations in a single tick bundle.
            var payload = MessagePackSerializer.Serialize(new BeltTickMessagePack(difference));
            _events.AddBroadcastEvent(EventTag, payload);
        }
    }
}

using Game.Train.Common;
using Game.Train.RailGraph;
using MessagePack;
using UniRx;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    public sealed class RailConnectionCreatedEventPacket
    {
        public const string EventTag = "va:event:railConnectionCreated";

        private readonly EventProtocolProvider _eventProtocolProvider;

        public RailConnectionCreatedEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            RailGraphDatastore.RailConnectionInitializedEvent.Subscribe(OnConnectionInitialized);
        }

        private void OnConnectionInitialized(RailConnectionInitializationNotifier.RailConnectionInitializationData data)
        {
            var tick = TrainUpdateService.CurrentTick;
            // 辺追加差分とtickを1パケットに封入
            // Attach tick metadata to edge creation diff
            var payload = MessagePackSerializer.Serialize(new RailConnectionCreatedMessagePack(
                data.FromNodeId,
                data.FromGuid,
                data.ToNodeId,
                data.ToGuid,
                data.Distance,
                tick));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}

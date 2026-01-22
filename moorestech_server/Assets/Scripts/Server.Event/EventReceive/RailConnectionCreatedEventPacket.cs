using Game.Train.Unit;
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
        private readonly TrainUpdateService _trainUpdateService;

        public RailConnectionCreatedEventPacket(EventProtocolProvider eventProtocolProvider, IRailGraphDatastore railGraphDatastore, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            railGraphDatastore.GetRailConnectionInitializedEvent().Subscribe(OnConnectionInitialized);
        }

        private void OnConnectionInitialized(RailConnectionInitializationData data)
        {
            var tick = _trainUpdateService.GetCurrentTick();
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


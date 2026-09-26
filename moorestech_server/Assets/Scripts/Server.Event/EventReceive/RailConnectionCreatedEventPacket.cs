using Game.Train.Unit;
using Game.Train.Unit.TickSynchronization;
using Game.Context;
using Game.Train.RailGraph;
using MessagePack;
using UniRx;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    public sealed class RailConnectionCreatedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:railConnectionCreated";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainTickSequenceSource _trainTickSequenceSource;

        public RailConnectionCreatedEventPacket(EventProtocolProvider eventProtocolProvider, IRailGraphDatastore railGraphDatastore, TrainTickSequenceSource trainTickSequenceSource)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _railGraphDatastore = railGraphDatastore;
            _trainTickSequenceSource = trainTickSequenceSource;
        }

        public void Load()
        {
            _railGraphDatastore.GetRailConnectionInitializedEvent().Subscribe(OnConnectionInitialized);
        }

        private void OnConnectionInitialized(RailConnectionInitializationData data)
        {
            var tick = _trainTickSequenceSource.Sequence.Tick;
            var tickSequenceId = _trainTickSequenceSource.Sequence.NextSequenceId();
            // 辺追加差分とtickを1パケットに封入
            // Attach tick metadata to edge creation diff
            var payload = MessagePackSerializer.Serialize(new RailConnectionCreatedMessagePack(
                data.FromNodeId,
                data.FromGuid,
                data.ToNodeId,
                data.ToGuid,
                data.Distance,
                tick,
                data.RailTypeGuid,
                data.IsDrawable,
                tickSequenceId));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}


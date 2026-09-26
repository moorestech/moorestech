using Game.Train.Unit;
using Game.Context;
using Game.Train.RailGraph;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     RailNode生成イベントをブロードキャストするパケット
    ///     Event packet that broadcasts newly created rail nodes
    /// </summary>
    public sealed class RailNodeCreatedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:railNodeCreated";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainTickSequenceSource _trainTickSequenceSource;

        public RailNodeCreatedEventPacket(EventProtocolProvider eventProtocolProvider, IRailGraphDatastore railGraphDatastore, TrainTickSequenceSource trainTickSequenceSource)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _railGraphDatastore = railGraphDatastore;
            _trainTickSequenceSource = trainTickSequenceSource;
        }

        public void Load()
        {
            _railGraphDatastore.GetRailNodeInitializedEvent().Subscribe(OnNodeInitialized);
        }

        private void OnNodeInitialized(RailNodeInitializationData data)
        {
            var tick = _trainTickSequenceSource.Sequence.Tick;
            var tickSequenceId = _trainTickSequenceSource.Sequence.NextSequenceId();
            // ノード生成差分と現在Tickを同時に送信
            // Include current tick alongside node creation diff
            var message = new RailNodeCreatedMessagePack(
                data.NodeId,
                data.NodeGuid,
                data.ConnectionDestination,
                data.OriginPoint,
                data.FrontControlPoint,
                data.BackControlPoint,
                tick,
                tickSequenceId);
            var payload = MessagePackSerializer.Serialize(message);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}


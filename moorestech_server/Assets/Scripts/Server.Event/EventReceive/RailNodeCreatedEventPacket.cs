using Game.Train.Common;
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
    public sealed class RailNodeCreatedEventPacket
    {
        public const string EventTag = "va:event:railNodeCreated";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;

        public RailNodeCreatedEventPacket(EventProtocolProvider eventProtocolProvider, IRailGraphDatastore railGraphDatastore, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            railGraphDatastore.GetRailNodeInitializedEvent().Subscribe(OnNodeInitialized);
        }

        private void OnNodeInitialized(RailNodeInitializationNotifier.RailNodeInitializationData data)
        {
            var tick = _trainUpdateService.GetCurrentTick();
            // ノード生成差分と現在Tickを同時に送信
            // Include current tick alongside node creation diff
            var message = new RailNodeCreatedMessagePack(
                data.NodeId,
                data.NodeGuid,
                data.ConnectionDestination,
                data.OriginPoint,
                data.FrontControlPoint,
                data.BackControlPoint,
                tick);
            var payload = MessagePackSerializer.Serialize(message);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}


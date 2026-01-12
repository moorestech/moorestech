using Game.Train.Common;
using Game.Train.RailGraph;
using MessagePack;
using Server.Event;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     RailNode削除をクライアントへ通知するイベントパケット
    ///     Event packet that broadcasts removed rail nodes
    /// </summary>
    public sealed class RailNodeRemovedEventPacket
    {
        public const string EventTag = "va:event:railNodeRemoved";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;

        public RailNodeRemovedEventPacket(EventProtocolProvider eventProtocolProvider, IRailGraphDatastore railGraphDatastore, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            railGraphDatastore.GetRailNodeRemovedEvent().Subscribe(OnNodeRemoved);
        }

        private void OnNodeRemoved(RailNodeRemovalNotifier.RailNodeRemovedData data)
        {
            // ノード削除メッセージをシリアライズ
            // Serialize node removal payload
            var tick = _trainUpdateService.GetCurrentTick();
            // 削除差分とTickを束ねて配信
            // Broadcast removal diff together with tick
            var payload = MessagePackSerializer.Serialize(new RailNodeRemovedMessagePack(data.NodeId, data.NodeGuid, tick));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}


using Game.Train.Unit;
using Game.Train.RailGraph;
using MessagePack;
using Server.Event;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     RailConnection削除をクライアントへ通知するイベントパケット
    ///     Event packet broadcasting removed rail connections
    /// </summary>
    public sealed class RailConnectionRemovedEventPacket
    {
        public const string EventTag = "va:event:railConnectionRemoved";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;

        public RailConnectionRemovedEventPacket(EventProtocolProvider eventProtocolProvider, IRailGraphDatastore railGraphDatastore, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            railGraphDatastore.GetRailConnectionRemovedEvent().Subscribe(OnConnectionRemoved);
        }

        private void OnConnectionRemoved(RailConnectionRemovalData data)
        {
            // 削除された接続情報をMessagePack化
            // Serialize the removed connection payload
            var tick = _trainUpdateService.GetCurrentTick();
            var tickSequenceId = _trainUpdateService.NextTickSequenceId();
            // 削除差分とtickをまとめてブロードキャスト
            // Broadcast removal diff paired with current tick
            var payload = MessagePackSerializer.Serialize(
                new RailConnectionRemovedMessagePack(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid, tick, tickSequenceId));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}


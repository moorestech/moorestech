using Game.Train.RailGraph;
using MessagePack;
using Server.Event;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     RailConnection蜿門ｾ励ゅ′繝｡繝・そ繝ｼ繧ｸ蜑･螟悶＠縺�・
    ///     Event packet broadcasting removed rail connections
    /// </summary>
    public sealed class RailConnectionRemovedEventPacket
    {
        public const string EventTag = "va:event:railConnectionRemoved";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly CompositeDisposable _disposable = new();

        public RailConnectionRemovedEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            RailGraphDatastore.RailConnectionRemovedEvent.Subscribe(OnConnectionRemoved).AddTo(_disposable);
        }

        private void OnConnectionRemoved(RailConnectionRemovalNotifier.RailConnectionRemovalData data)
        {
            // 削除された接続情報をMessagePack化
            // Serialize the removed connection payload
            var payload = MessagePackSerializer.Serialize(
                new RailConnectionRemovedMessagePack(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}

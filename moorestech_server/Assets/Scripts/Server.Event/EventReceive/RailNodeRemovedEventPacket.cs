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
        private readonly CompositeDisposable _disposable = new();

        public RailNodeRemovedEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            RailGraphDatastore.RailNodeRemovedEvent.Subscribe(OnNodeRemoved).AddTo(_disposable);
        }

        private void OnNodeRemoved(RailNodeRemovalNotifier.RailNodeRemovedData data)
        {
            // ノード削除メッセージをシリアライズ
            // Serialize node removal payload
            var payload = MessagePackSerializer.Serialize(new RailNodeRemovedMessagePack(data.NodeId, data.NodeGuid));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}

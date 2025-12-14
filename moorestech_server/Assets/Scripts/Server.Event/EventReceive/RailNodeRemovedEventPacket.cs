using Game.Train.RailGraph;
using MessagePack;
using Server.Event;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     RailNode蜿門ｾ励ゅ′繝｡繝・そ繝ｼ繧ｸ蜑･螟悶＠縺�・
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

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

        public RailNodeCreatedEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            RailGraphDatastore.RailNodeInitializedEvent.Subscribe(OnNodeInitialized);
        }

        private void OnNodeInitialized(RailNodeInitializationNotifier.RailNodeInitializationData data)
        {
            var message = new RailNodeCreatedMessagePack(
                data.NodeId,
                data.NodeGuid,
                data.ConnectionDestination,
                data.OriginPoint,
                data.FrontControlPoint,
                data.BackControlPoint
                );
            var payload = MessagePackSerializer.Serialize(message);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}

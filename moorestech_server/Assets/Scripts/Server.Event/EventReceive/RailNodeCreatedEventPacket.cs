using Game.Train.RailGraph;
using MessagePack;
using Server.Event;
using Server.Util.MessagePack;

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

        private void OnNodeInitialized(RailNodeInitializationData data)
        {
            var message = new RailNodeCreatedMessagePack(
                data.NodeId,
                data.NodeGuid,
                data.ControlPointOrigin,
                data.ConnectionDestination,
                data.ControlPointLength,
                data.RailDirection);
            var payload = MessagePackSerializer.Serialize(message);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}

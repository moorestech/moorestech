using Game.Train.RailGraph;
using MessagePack;
using UniRx;
using Server.Util.MessagePack;
using Server.Event;

namespace Server.Event.EventReceive
{
    public sealed class RailConnectionCreatedEventPacket
    {
        public const string EventTag = "va:event:railConnectionCreated";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly CompositeDisposable _disposable = new();

        public RailConnectionCreatedEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            RailGraphDatastore.RailConnectionInitializedEvent.Subscribe(OnConnectionInitialized).AddTo(_disposable);
        }

        private void OnConnectionInitialized(RailConnectionInitializationNotifier.RailConnectionInitializationData data)
        {
            var payload = MessagePackSerializer.Serialize(new RailConnectionCreatedMessagePack(
                data.FromNodeId,
                data.FromGuid,
                data.ToNodeId,
                data.ToGuid,
                data.Distance));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
}

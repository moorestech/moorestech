using Client.Game.InGame.Context;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using System;
using MessagePack;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    public sealed class TrainDiagramEventNetworkHandler : IInitializable, IDisposable
    {
        private readonly TrainUnitClientCache _cache;
        private readonly CompositeDisposable _subscriptions = new();

        public TrainDiagramEventNetworkHandler(TrainUnitClientCache cache)
        {
            _cache = cache;
        }

        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(
                TrainDiagramEventPacket.DockedEventTag,
                OnDocked).AddTo(_subscriptions);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(
                TrainDiagramEventPacket.DepartedEventTag,
                OnDeparted).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnDocked(byte[] payload)
        {
            HandleEvent(payload);
        }

        private void OnDeparted(byte[] payload)
        {
            HandleEvent(payload);
        }

        private void HandleEvent(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<TrainDiagramEventMessagePack>(payload);
            _cache.ApplyDiagramEvent(message);
        }
    }
}

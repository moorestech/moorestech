using Client.Game.InGame.Context;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using System;
using MessagePack;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    public sealed class TrainDiagramEventNetworkHandler : IInitializable, IDisposable
    {
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly CompositeDisposable _subscriptions = new();

        public TrainDiagramEventNetworkHandler(TrainUnitFutureMessageBuffer futureMessageBuffer)
        {
            _futureMessageBuffer = futureMessageBuffer;
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
            _futureMessageBuffer.EnqueueDiagram(message);
        }
    }
}

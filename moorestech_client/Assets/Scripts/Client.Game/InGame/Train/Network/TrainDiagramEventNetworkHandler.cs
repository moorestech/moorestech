using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
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
        private readonly TrainUnitClientCache _cache;
        private readonly CompositeDisposable _subscriptions = new();

        public TrainDiagramEventNetworkHandler(TrainUnitFutureMessageBuffer futureMessageBuffer, TrainUnitClientCache cache)
        {
            _futureMessageBuffer = futureMessageBuffer;
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
            HandleEvent(payload, TrainDiagramEventPacket.DockedEventTag);
        }

        private void OnDeparted(byte[] payload)
        {
            HandleEvent(payload, TrainDiagramEventPacket.DepartedEventTag);
        }

        private void HandleEvent(byte[] payload, string eventTag)
        {
            if (payload == null || payload.Length == 0)
            {
                return;
            }

            var message = MessagePackSerializer.Deserialize<TrainDiagramEventMessagePack>(payload);
            if (message == null)
            {
                return;
            }

            var serverTick = message.Tick;
            _futureMessageBuffer.Enqueue(serverTick, CreateBufferedEvent());

            #region Internal

            ITrainTickBufferedEvent CreateBufferedEvent()
            {
                // ダイアグラム適用処理をtickイベントとして登録する
                // Register diagram apply logic as a tick-buffered event
                return TrainTickBufferedEvent.Create(eventTag, ApplyDiagramEvent);

                void ApplyDiagramEvent()
                {
                    _cache.ApplyDiagramEvent(message);
                }
            }

            #endregion
        }
    }
}

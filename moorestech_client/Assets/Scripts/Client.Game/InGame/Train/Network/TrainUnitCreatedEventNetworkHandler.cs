using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    public sealed class TrainUnitCreatedEventNetworkHandler : IInitializable, IDisposable
    {
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _cache;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private IDisposable _subscription;

        public TrainUnitCreatedEventNetworkHandler(
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitClientCache cache,
            TrainCarObjectDatastore trainCarDatastore)
        {
            _futureMessageBuffer = futureMessageBuffer;
            _cache = cache;
            _trainCarDatastore = trainCarDatastore;
        }

        public void Initialize()
        {
            // 新規列車生成イベントを購読する
            // Subscribe to new train unit events
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(TrainUnitCreatedEventPacket.EventTag, OnEventReceived);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnEventReceived(byte[] payload)
        {
            // 受信イベントを適用する
            // Apply the incoming event
            if (payload == null || payload.Length == 0) return;

            var message = MessagePackSerializer.Deserialize<TrainUnitCreatedEventMessagePack>(payload);
            if (message?.Snapshot == null) return;

            // スナップショットを将来tickバッファへ投入する
            // Push snapshot into the future-tick buffer
            var bundle = message.Snapshot.ToModel();
            var serverTick = message.ServerTick;
            _futureMessageBuffer.EnqueueEvent(serverTick, message.TickSequenceId, CreateBufferedEvent());

            #region Internal

            ITrainTickBufferedEvent CreateBufferedEvent()
            {
                // 適用処理をイベント化してtickバッファへ渡す
                // Wrap the apply action into a buffered event for the tick queue
                return TrainTickBufferedEvent.Create(TrainUnitCreatedEventPacket.EventTag, ApplyCreatedEvent);

                void ApplyCreatedEvent()
                {
                    // 列車キャッシュと車両オブジェクトを同期更新する
                    // Update train cache and train-car objects together
                    _cache.Upsert(bundle);
                    _trainCarDatastore.OnTrainObjectUpdate(bundle.Simulation.Cars);
                }
            }

            #endregion
        }
    }
}

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
        private readonly TrainUnitClientCache _cache;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private IDisposable _subscription;

        public TrainUnitCreatedEventNetworkHandler(TrainUnitClientCache cache, TrainCarObjectDatastore trainCarDatastore)
        {
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

        #region Internal

        private void OnEventReceived(byte[] payload)
        {
            // 受信イベントを適用する
            // Apply the incoming event
            if (payload == null || payload.Length == 0) return;

            var message = MessagePackSerializer.Deserialize<TrainUnitCreatedEventMessagePack>(payload);
            if (message?.Snapshot == null) return;

            // スナップショットをキャッシュに反映し、車両オブジェクトを生成する
            // Apply snapshot to cache and create train car objects
            var bundle = message.Snapshot.ToModel();
            _cache.Upsert(bundle, message.ServerTick);
            _trainCarDatastore.OnTrainObjectUpdate(bundle.Simulation.Cars);
        }

        #endregion
    }
}

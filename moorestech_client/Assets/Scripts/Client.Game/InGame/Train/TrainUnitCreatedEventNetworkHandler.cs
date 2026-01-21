using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Entity;
using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    public sealed class TrainUnitCreatedEventNetworkHandler : IInitializable, IDisposable
    {
        private readonly TrainUnitClientCache _cache;
        private readonly TrainEntityObjectDatastore _trainEntityDatastore;
        private IDisposable _subscription;

        public TrainUnitCreatedEventNetworkHandler(TrainUnitClientCache cache, TrainEntityObjectDatastore trainEntityObjectDatastore)
        {
            _cache = cache;
            _trainEntityDatastore = trainEntityObjectDatastore;
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
            if (payload == null || payload.Length == 0)
            {
                return;
            }
            var message = MessagePackSerializer.Deserialize<TrainUnitCreatedEventMessagePack>(payload);
            if (message == null || message.Snapshot == null)
            {
                return;
            }
            var bundle = message.Snapshot.ToModel();
            _cache.Upsert(bundle, message.ServerTick);
            ApplyEntities(message.Entities);
        }

        private void ApplyEntities(EntityMessagePack[] entities)
        {
            // 生成エンティティを即時反映する
            // Apply spawned entities immediately
            if (entities == null || entities.Length == 0)
            {
                return;
            }
            var responses = new List<EntityResponse>(entities.Length);
            for (var i = 0; i < entities.Length; i++)
            {
                responses.Add(new EntityResponse(entities[i]));
            }
            _trainEntityDatastore.OnEntitiesUpdate(responses);
        }

        #endregion
    }
}

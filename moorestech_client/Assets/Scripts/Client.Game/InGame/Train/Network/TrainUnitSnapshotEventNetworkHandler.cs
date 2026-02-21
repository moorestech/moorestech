using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    // TrainUnit単機スナップショットイベントを受信してfutureバッファに積む
    // Receive per-train-unit snapshot events and enqueue them into the future buffer.
    public sealed class TrainUnitSnapshotEventNetworkHandler : IInitializable, IDisposable
    {
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _cache;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private IDisposable _subscription;

        public TrainUnitSnapshotEventNetworkHandler(
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
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(TrainUnitSnapshotEventPacket.EventTag, OnEventReceived);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnEventReceived(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return;
            }

            var message = MessagePackSerializer.Deserialize<TrainUnitSnapshotEventMessagePack>(payload);
            if (message == null)
            {
                return;
            }

            _futureMessageBuffer.EnqueueEvent(message.ServerTick, message.TickSequenceId, CreateBufferedEvent(message));

            #region Internal

            ITrainTickBufferedEvent CreateBufferedEvent(TrainUnitSnapshotEventMessagePack messagePack)
            {
                return TrainTickBufferedEvent.Create(ApplySnapshotEvent);

                void ApplySnapshotEvent()
                {
                    // 削除通知はキャッシュと描画オブジェクトを同tickで破棄する
                    // Deletion tombstones remove cache and visuals at the same tick.
                    if (messagePack.IsDeleted)
                    {
                        ApplyDelete(messagePack.TrainInstanceId);
                        return;
                    }

                    if (messagePack.Snapshot == null)
                    {
                        return;
                    }

                    // 更新通知は既存車両を再利用しつつ不足分のみ差し替える
                    // Update notifications reconcile only the changed train-car visuals.
                    ApplyUpsert(messagePack.TrainInstanceId, messagePack.Snapshot.ToModel());
                }
            }

            void ApplyDelete(TrainInstanceId trainInstanceId)
            {
                if (_cache.TryGet(trainInstanceId, out var existingUnit))
                {
                    for (var i = 0; i < existingUnit.Cars.Count; i++)
                    {
                        _trainCarDatastore.RemoveTrainEntity(existingUnit.Cars[i].TrainCarInstanceId);
                    }
                }
                _cache.Remove(trainInstanceId);
            }

            void ApplyUpsert(TrainInstanceId trainInstanceId, TrainUnitSnapshotBundle bundle)
            {
                var previousCarIds = new HashSet<TrainCarInstanceId>();
                if (_cache.TryGet(trainInstanceId, out var existingUnit))
                {
                    for (var i = 0; i < existingUnit.Cars.Count; i++)
                    {
                        previousCarIds.Add(existingUnit.Cars[i].TrainCarInstanceId);
                    }
                }

                _cache.Upsert(bundle);
                _trainCarDatastore.OnTrainObjectUpdate(bundle.Simulation.Cars);

                var nextCarIds = new HashSet<TrainCarInstanceId>();
                for (var i = 0; i < bundle.Simulation.Cars.Count; i++)
                {
                    nextCarIds.Add(bundle.Simulation.Cars[i].TrainCarInstanceId);
                }

                foreach (var previousCarId in previousCarIds)
                {
                    if (!nextCarIds.Contains(previousCarId))
                    {
                        _trainCarDatastore.RemoveTrainEntity(previousCarId);
                    }
                }
            }

            #endregion
        }
    }
}

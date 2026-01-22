using System;
using System.Collections.Generic;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Train.Unit;
using Client.Network.API;
using Game.Entity.Interface;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.View
{
    /// <summary>
    ///     列車スナップショットを初期化時にキャッシュへ流し込むアプライヤー
    ///     Applies the initial train snapshots to the local cache and can be reused for resync
    /// </summary>
    public sealed class TrainUnitSnapshotApplier : IInitializable
    {
        private readonly TrainUnitClientCache _cache;
        private readonly TrainEntityObjectDatastore _trainEntityDatastore;
        private readonly InitialHandshakeResponse _initialHandshakeResponse;

        public TrainUnitSnapshotApplier(TrainUnitClientCache cache, InitialHandshakeResponse initialHandshakeResponse, TrainEntityObjectDatastore trainEntityObjectDatastore)
        {
            _cache = cache;
            _initialHandshakeResponse = initialHandshakeResponse;
            _trainEntityDatastore = trainEntityObjectDatastore;
        }

        public void Initialize()
        {
            ApplySnapshot(_initialHandshakeResponse?.TrainUnitSnapshots);
        }

        // レスポンスに含まれる列車データをキャッシュへ適用
        // Apply the received snapshot response to the cache
        public void ApplySnapshot(TrainUnitSnapshotResponse response)
        {
            if (response == null)
            {
                return;
            }

            // スナップショットをモデルに変換して列車IDを収集する
            // Convert snapshots into models and collect train car ids
            var snapshotPacks = response.Snapshots;
            var bundles = new List<TrainUnitSnapshotBundle>(snapshotPacks?.Count ?? 0);
            var activeTrainCarIds = new HashSet<Guid>();
            var entityResponses = new List<EntityResponse>();
            if (snapshotPacks != null)
            {
                for (var i = 0; i < snapshotPacks.Count; i++)
                {
                    var pack = snapshotPacks[i];
                    if (pack == null)
                    {
                        continue;
                    }
                    var bundle = pack.ToModel();
                    bundles.Add(bundle);
                    CollectTrainCarIds(bundle, activeTrainCarIds);
                    BuildTrainEntities(bundle, entityResponses);
                }
            }

            // キャッシュ更新後に不要な列車エンティティを除去する
            // Remove stale train entities after cache update
            _cache.OverrideAll(bundles, response.ServerTick);
            if (entityResponses.Count > 0) _trainEntityDatastore.OnEntitiesUpdate(entityResponses);
            _trainEntityDatastore.RemoveTrainEntitiesNotInSnapshot(activeTrainCarIds);

            #region Internal

            void CollectTrainCarIds(TrainUnitSnapshotBundle bundle, ISet<Guid> target)
            {
                // 車両スナップショットからIDを集計する
                // Collect train car ids from the snapshot
                var cars = bundle.Simulation.Cars;
                if (cars == null) return;
                for (var i = 0; i < cars.Count; i++) target.Add(cars[i].TrainCarInstanceGuid);
            }

            void BuildTrainEntities(TrainUnitSnapshotBundle bundle, ICollection<EntityResponse> target)
            {
                // 車両スナップショットからエンティティ更新を構築する
                // Build entity updates from train car snapshots
                var cars = bundle.Simulation.Cars;
                if (cars == null || cars.Count == 0) return;
                for (var i = 0; i < cars.Count; i++)
                {
                    var car = cars[i];
                    var entityId = CreateTrainEntityInstanceId(car.TrainCarInstanceGuid);
                    var state = new TrainEntityStateMessagePack(car.TrainCarInstanceGuid, car.TrainCarMasterId);
                    var entityPack = new EntityMessagePack
                    {
                        InstanceId = entityId,
                        Type = VanillaEntityType.VanillaTrain,
                        Position = new Vector3MessagePack(Vector3.zero),
                        EntityData = MessagePackSerializer.Serialize(state)
                    };
                    target.Add(new EntityResponse(entityPack));
                }
            }

            long CreateTrainEntityInstanceId(Guid trainCarId)
            {
                // 車両Guidから安定したInstanceIdを生成する
                // Generate a stable instance id from the train car Guid
                var bytes = trainCarId.ToByteArray();
                var low = BitConverter.ToInt64(bytes, 0);
                var high = BitConverter.ToInt64(bytes, 8);
                return low ^ high;
            }

            #endregion
        }
    }
}

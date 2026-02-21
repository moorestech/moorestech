using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.Event;
using Game.Train.Unit;
using MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RemoveTrainCarProtocol : IPacketResponse
    {
        private readonly TrainUpdateService _trainUpdateService;
        private readonly ITrainUnitSnapshotNotifyEvent _trainUnitSnapshotNotifyEvent;
        public const string ProtocolTag = "va:removeTrainCar";

        public RemoveTrainCarProtocol(TrainUpdateService trainUpdateService, ITrainUnitSnapshotNotifyEvent trainUnitSnapshotNotifyEvent)
        {
            _trainUpdateService = trainUpdateService;
            _trainUnitSnapshotNotifyEvent = trainUnitSnapshotNotifyEvent;
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            // リクエストの復元
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<RemoveTrainCarRequestMessagePack>(payload);
            var beforeTrainState = CaptureTrainState(_trainUpdateService.GetRegisteredTrains());

            // 対象列車の探索
            // Resolve target train and car
            var trainsBeforeRemoval = _trainUpdateService.GetRegisteredTrains().ToList();
            var trainCarPairs = trainsBeforeRemoval.SelectMany(t => t.Cars.Select(c => (Train: t, Car: c)));
            var trainCarInstanceId = new TrainCarInstanceId(request.TrainCarInstanceId);
            var targetPair = trainCarPairs.FirstOrDefault(c => c.Car.TrainCarInstanceId == trainCarInstanceId);
            if (targetPair.Car == null)
            {
                Debug.LogWarning($"Remove train car failed. Train not found. \ncarId: {trainCarInstanceId}");
                return null;
            }

            // 削除の実行
            // Apply removal
            targetPair.Train.RemoveCar(trainCarInstanceId);
            var afterTrains = _trainUpdateService.GetRegisteredTrains().ToList();
            // 参照ではなく値スナップショット差分で通知対象を決める
            // Resolve changed trains from value snapshots (not mutable references).
            NotifyChangedTrainUnits(beforeTrainState, afterTrains);
            return null;

            #region Internal

            static Dictionary<TrainInstanceId, HashSet<TrainCarInstanceId>> CaptureTrainState(IEnumerable<TrainUnit> trains)
            {
                // 変更前後比較用に編成と車両ID集合を値で保持する
                // Capture train->car-id sets as value snapshots for before/after diff.
                var state = new Dictionary<TrainInstanceId, HashSet<TrainCarInstanceId>>();
                foreach (var train in trains)
                {
                    if (train == null || train.TrainInstanceId == TrainInstanceId.Empty)
                    {
                        continue;
                    }

                    var carIds = new HashSet<TrainCarInstanceId>();
                    for (var i = 0; i < train.Cars.Count; i++)
                    {
                        carIds.Add(train.Cars[i].TrainCarInstanceId);
                    }
                    state[train.TrainInstanceId] = carIds;
                }
                return state;
            }

            void NotifyChangedTrainUnits(Dictionary<TrainInstanceId, HashSet<TrainCarInstanceId>> previousState, IReadOnlyList<TrainUnit> latestTrains)
            {
                // 最新状態から通知対象編成を抽出し、upsert/deletedを振り分ける
                // Collect changed train ids and route them to upsert/deleted notifications.
                var latestState = CaptureTrainState(latestTrains);
                var latestTrainById = new Dictionary<TrainInstanceId, TrainUnit>();
                for (var i = 0; i < latestTrains.Count; i++)
                {
                    var train = latestTrains[i];
                    if (train == null || train.TrainInstanceId == TrainInstanceId.Empty)
                    {
                        continue;
                    }
                    latestTrainById[train.TrainInstanceId] = train;
                }

                var changedTrainIds = new HashSet<TrainInstanceId>();
                foreach (var previousEntry in previousState)
                {
                    if (!latestState.ContainsKey(previousEntry.Key))
                    {
                        changedTrainIds.Add(previousEntry.Key);
                    }
                }
                foreach (var latestEntry in latestState)
                {
                    if (!previousState.TryGetValue(latestEntry.Key, out var previousCars) || !previousCars.SetEquals(latestEntry.Value))
                    {
                        changedTrainIds.Add(latestEntry.Key);
                    }
                }

                foreach (var trainInstanceId in changedTrainIds)
                {
                    if (latestTrainById.TryGetValue(trainInstanceId, out var updatedTrain))
                    {
                        _trainUnitSnapshotNotifyEvent.NotifySnapshot(updatedTrain);
                        continue;
                    }
                    _trainUnitSnapshotNotifyEvent.NotifyDeleted(trainInstanceId);
                }
            }

            #endregion
        }

        [MessagePackObject]
        public class RemoveTrainCarRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public long TrainCarInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RemoveTrainCarRequestMessagePack()
            {
                Tag = ProtocolTag;
            }

            public RemoveTrainCarRequestMessagePack(long trainCarInstanceId)
            {
                Tag = ProtocolTag;
                TrainCarInstanceId = trainCarInstanceId;
            }
        }
    }
}

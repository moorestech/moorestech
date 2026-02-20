using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RemoveTrainCarProtocol : IPacketResponse
    {
        private readonly TrainUpdateService _trainUpdateService;
        private readonly TrainUnitSnapshotEventPacket _trainUnitSnapshotEventPacket;
        public const string ProtocolTag = "va:removeTrainCar";

        public RemoveTrainCarProtocol(TrainUpdateService trainUpdateService, TrainUnitSnapshotEventPacket trainUnitSnapshotEventPacket)
        {
            _trainUpdateService = trainUpdateService;
            _trainUnitSnapshotEventPacket = trainUnitSnapshotEventPacket;
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            // リクエストの復元
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<RemoveTrainCarRequestMessagePack>(payload);
            var beforeTrains = _trainUpdateService.GetRegisteredTrains().ToList();
            var beforeState = BuildTrainState(beforeTrains);

            // 対象列車の探索
            // Resolve target train and car
            var trainCarPairs = beforeTrains.SelectMany(t => t.Cars.Select(c => (Train: t, Car: c)));
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
            BroadcastChangedTrainUnits(beforeState, afterTrains);
            return null;

            #region Internal

            Dictionary<TrainInstanceId, HashSet<TrainCarInstanceId>> BuildTrainState(IReadOnlyList<TrainUnit> trains)
            {
                // 編成ごとの車両ID集合を記録する
                // Capture car-id sets per train unit.
                var state = new Dictionary<TrainInstanceId, HashSet<TrainCarInstanceId>>();
                for (var i = 0; i < trains.Count; i++)
                {
                    var train = trains[i];
                    if (train == null || train.TrainInstanceId == TrainInstanceId.Empty)
                    {
                        continue;
                    }

                    var carIds = new HashSet<TrainCarInstanceId>();
                    for (var j = 0; j < train.Cars.Count; j++)
                    {
                        carIds.Add(train.Cars[j].TrainCarInstanceId);
                    }
                    state[train.TrainInstanceId] = carIds;
                }
                return state;
            }

            void BroadcastChangedTrainUnits(
                Dictionary<TrainInstanceId, HashSet<TrainCarInstanceId>> previousState,
                IReadOnlyList<TrainUnit> latestTrains)
            {
                // 削除/分割後の差分編成のみスナップショット通知する
                // Broadcast snapshots only for train units changed by remove/split.
                var latestState = BuildTrainState(latestTrains);
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
                        _trainUnitSnapshotEventPacket.BroadcastSnapshot(updatedTrain);
                        continue;
                    }
                    _trainUnitSnapshotEventPacket.BroadcastDeleted(trainInstanceId);
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

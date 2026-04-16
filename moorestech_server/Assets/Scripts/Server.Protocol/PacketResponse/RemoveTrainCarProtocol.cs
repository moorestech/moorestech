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
        private readonly ITrainUnitSnapshotNotifyEvent _trainUnitSnapshotNotifyEvent;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly ITrainUnitMutationDatastore _trainUnitMutationDatastore;
        public const string ProtocolTag = "va:removeTrainCar";

        public RemoveTrainCarProtocol(
            ITrainUnitSnapshotNotifyEvent trainUnitSnapshotNotifyEvent,
            ITrainUnitLookupDatastore trainUnitLookupDatastore,
            ITrainUnitMutationDatastore trainUnitMutationDatastore)
        {
            _trainUnitSnapshotNotifyEvent = trainUnitSnapshotNotifyEvent;
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _trainUnitMutationDatastore = trainUnitMutationDatastore;
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            // Deserialize request payload.
            var request = MessagePackSerializer.Deserialize<RemoveTrainCarRequestMessagePack>(payload);
            var trainCarInstanceId = new TrainCarInstanceId(request.TrainCarInstanceId);

            if (!_trainUnitLookupDatastore.TryGetTrainUnitByCar(trainCarInstanceId, out var requestTrainUnit))
            {
                Debug.LogWarning($"Remove train car failed. Train not found. \ncarId: {trainCarInstanceId}");
                return null;
            }

            // Apply removal and collect resulting units for snapshot updates.
            var createdTrainUnit = requestTrainUnit.RemoveCar(trainCarInstanceId);

            var deletedTrainId = requestTrainUnit.TrainInstanceId;
            var createdTrainUnits = new List<TrainUnit>();

            if (requestTrainUnit.Cars.Count == 0)
            {
                _trainUnitMutationDatastore.UnregisterTrain(requestTrainUnit);
                requestTrainUnit.OnDestroy();
                requestTrainUnit = null;
            }
            else
            {
                _trainUnitMutationDatastore.RegisterTrain(requestTrainUnit);
                createdTrainUnits.Add(requestTrainUnit);
            }

            if (createdTrainUnit != null)
            {
                if (createdTrainUnit.Cars.Count == 0)
                {
                    createdTrainUnit.OnDestroy();
                    createdTrainUnit = null;
                }
                else
                {
                    _trainUnitMutationDatastore.RegisterTrain(createdTrainUnit);
                    createdTrainUnits.Add(createdTrainUnit);
                }
            }

            _trainUnitSnapshotNotifyEvent.NotifyDeleted(deletedTrainId);
            foreach (var trainUnit in createdTrainUnits)
            {
                _trainUnitSnapshotNotifyEvent.NotifySnapshot(trainUnit);
            }

            return null;
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

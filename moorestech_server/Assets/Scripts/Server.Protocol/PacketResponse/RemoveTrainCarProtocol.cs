using System;
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

        public RemoveTrainCarProtocol(ITrainUnitSnapshotNotifyEvent trainUnitSnapshotNotifyEvent, ITrainUnitLookupDatastore trainUnitLookupDatastore, ITrainUnitMutationDatastore trainUnitMutationDatastore)
        {
            _trainUnitSnapshotNotifyEvent = trainUnitSnapshotNotifyEvent;
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _trainUnitMutationDatastore = trainUnitMutationDatastore;
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            // リクエストの復元
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<RemoveTrainCarRequestMessagePack>(payload);
            var trainCarInstanceId = new TrainCarInstanceId(request.TrainCarInstanceId);
            
            if (!_trainUnitLookupDatastore.TryGetTrainUnitByCar(trainCarInstanceId, out var requestTrainUnit))
            {
                Debug.LogWarning($"Remove train car failed. Train not found. \ncarId: {trainCarInstanceId}");
                return null;
            }

            // 削除の実行
            // Apply removal
            var createdTrainUnit = requestTrainUnit.RemoveCar(trainCarInstanceId);
            
            // 実在するか
            // is exist?
            if (createdTrainUnit.Cars.Count == 0)
            {
                createdTrainUnit.OnDestroy();
                createdTrainUnit = null;
            }
            else
            {
                // datastore更新
                _trainUnitMutationDatastore.RegisterTrain(createdTrainUnit);
                _trainUnitSnapshotNotifyEvent.NotifySnapshot(createdTrainUnit);
            }
            // requestTrainUnitの中身が変更されて実在しているか
            if (requestTrainUnit.Cars.Count == 0)
            {
                // TrainUnitまるごと通知
                // Notify train unit snapshot updates
                // datastore更新上書き
                _trainUnitMutationDatastore.UnregisterTrain(requestTrainUnit);
                _trainUnitSnapshotNotifyEvent.NotifyDeleted(requestTrainUnit.TrainInstanceId);
                requestTrainUnit.OnDestroy();
                requestTrainUnit = null;
            }
            else
            {
                // datastore更新上書き
                _trainUnitMutationDatastore.RegisterTrain(requestTrainUnit);
                _trainUnitSnapshotNotifyEvent.NotifySnapshot(requestTrainUnit);
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

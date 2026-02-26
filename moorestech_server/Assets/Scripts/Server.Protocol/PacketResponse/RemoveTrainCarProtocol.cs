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
            var trainsBeforeRemoval = _trainUpdateService.GetRegisteredTrains().ToList();

            // 対象列車の探索
            // Resolve target train and car
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
            var updatedTrainInstanceIds = targetPair.Train.RemoveCar(trainCarInstanceId);
            
            if (updatedTrainInstanceIds == null) return null;
            if (updatedTrainInstanceIds.Count <= 1) return null;
            // TrainUnitまるごと通知
            // Notify train unit snapshot updates
            if (updatedTrainInstanceIds[0] != TrainInstanceId.Empty)
                _trainUnitSnapshotNotifyEvent.NotifySnapshot(updatedTrainInstanceIds[0]);
            if (updatedTrainInstanceIds[1] != TrainInstanceId.Empty)
                _trainUnitSnapshotNotifyEvent.NotifyDeleted(updatedTrainInstanceIds[1]);
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

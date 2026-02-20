using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.Unit;
using MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RemoveTrainCarProtocol : IPacketResponse
    {
        private readonly TrainUpdateService _trainUpdateService;
        public const string ProtocolTag = "va:removeTrainCar";
        
        public RemoveTrainCarProtocol(TrainUpdateService trainUpdateService)
        {
            _trainUpdateService = trainUpdateService;
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            // リクエストの復元
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<RemoveTrainCarRequestMessagePack>(payload);

            // 対象列車の探索
            // Resolve target train and car
            var trainCarPairs = _trainUpdateService.GetRegisteredTrains().SelectMany(t => t.Cars.Select(c => (Train: t, Car: c)));
            var trainCarInstanceId = new TrainCarInstanceId(request.TrainCarInstanceId);
            var targetPair = trainCarPairs.FirstOrDefault(c => c.Car.TrainCarInstanceId == trainCarInstanceId);
            if (targetPair.Car == null) { Debug.LogWarning($"Remove train car failed. Train not found. \ncarId: {trainCarInstanceId}"); return null; }
            
            // 削除の実行
            // Apply removal
            targetPair.Train.RemoveCar(trainCarInstanceId);
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

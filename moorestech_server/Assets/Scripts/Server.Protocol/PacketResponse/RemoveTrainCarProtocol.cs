using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.Common;
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
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RemoveTrainCarRequestMessagePack>(payload.ToArray());

            // TODO: オーダーがこのままだとO(n)になっているため、逆引き用の辞書等を用意してO(1)にする
            var (targetTrain, removeTargetTrainCar) = _trainUpdateService
                .GetRegisteredTrains()
                .SelectMany(t => t.Cars.Select(c => (t, c)))
                .First(c => c.c.CarId == request.TrainCarId);
            if (removeTargetTrainCar == null)
            {
                Debug.LogError($"Remove train car failed. Train not found. \ncarId: {request.TrainCarId}");
                return null;
            } 
            targetTrain.RemoveCar(request.TrainCarId);
            return null;
        }
        
        [MessagePackObject]
        public class RemoveTrainCarRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Guid TrainCarId { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RemoveTrainCarRequestMessagePack()
            {
                Tag = ProtocolTag;
            }
            
            public RemoveTrainCarRequestMessagePack(Guid trainCarId)
            {
                Tag = ProtocolTag;
                TrainCarId = trainCarId;
            }
        }
    }
}

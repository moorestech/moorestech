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
        public const string ProtocolTag = "va:removeTrainCar";
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RemoveTrainCarRequestMessagePack>(payload.ToArray());

            // TODO: オーダーがこのままだとO(n)になっているため、逆引き用の辞書等を用意してO(1)にする
            var (targetTrain, removeTargetTrainCar) = TrainUpdateService.Instance
                .GetRegisteredTrains()
                .SelectMany(t => t.Cars.Select(c => (t, c)))
                .First(c => c.c.CarId == request.TrainCarId);
            if (removeTargetTrainCar == null) throw new Exception("Remove train car failed. Train not found.");
            
            //TODO trainUnit側に特定trainCarの削除APIをはやしてそれを呼ぶようにする
            
            // 削除対象車両のインデックスを取得
            // Get index of the target car to remove
            var carIndex = targetTrain.Cars.ToList().IndexOf(removeTargetTrainCar);
            var totalCars = targetTrain.Cars.Count;
            
            // 1両のみの場合はTrainUnit全体を削除
            // If only one car, destroy the entire TrainUnit
            if (totalCars == 1)
            {
                targetTrain.OnDestroy();
                return null;
            }
            
            // 後尾からの位置を計算
            // Calculate position from rear
            var carsFromRear = totalCars - 1 - carIndex;
            
            if (carsFromRear == 0)
            {
                // 後尾車両の場合：SplitTrainで1両切り離して破棄
                // Rear car: Split 1 car and destroy it
                var detachedTrain = targetTrain.SplitTrain(1);
                detachedTrain?.OnDestroy();
            }
            else if (carIndex == 0)
            {
                // 先頭車両の場合：反転して後尾として処理、再度反転
                // Front car: Reverse, process as rear, reverse again
                targetTrain.Reverse();
                var detachedTrain = targetTrain.SplitTrain(1);
                detachedTrain?.OnDestroy();
                targetTrain.Reverse();
            }
            else
            {
                // 中間車両の場合：後ろ側を切り離し、その先頭を削除
                // Middle car: Split rear portion, then remove its front
                var rearTrain = targetTrain.SplitTrain(carsFromRear + 1);
                rearTrain.Reverse();
                var removedTrain = rearTrain.SplitTrain(1);
                removedTrain?.OnDestroy();
                rearTrain.Reverse();
            }

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
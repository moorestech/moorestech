using System;
using System.Collections.Generic;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RemoveTrainCarProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:removeTrainCar";
        
        public RemoveTrainCarProtocol(ServiceProvider serviceProvider)
        {
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RemoveTrainCarRequestMessagePack>(payload.ToArray());
            
            Debug.Log("Request remove train car.");
            
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
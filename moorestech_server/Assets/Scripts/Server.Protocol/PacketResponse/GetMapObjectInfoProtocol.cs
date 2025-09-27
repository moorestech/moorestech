using System;
using System.Collections.Generic;
using Game.Context;
using Game.Map.Interface.MapObject;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     map objectの破壊状況を送信するプロトコル
    /// </summary>
    public class GetMapObjectInfoProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:mapObjectInfo";
        
        public GetMapObjectInfoProtocol(ServiceProvider serviceProvider)
        {
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var sendMapObjects = new List<MapObjectsInfoMessagePack>();
            foreach (var mapObject in ServerContext.MapObjectDatastore.MapObjects)
                sendMapObjects.Add(new MapObjectsInfoMessagePack(mapObject.InstanceId, mapObject.IsDestroyed, mapObject.CurrentHp));
            
            var response = new ResponseMapObjectInfosMessagePack(sendMapObjects);
            
            return response;
        }
        
        
        
        [MessagePackObject]
        public class RequestMapObjectInfosMessagePack : ProtocolMessagePackBase
        {
            public RequestMapObjectInfosMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseMapObjectInfosMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<MapObjectsInfoMessagePack> MapObjects { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseMapObjectInfosMessagePack() { }
            
            public ResponseMapObjectInfosMessagePack(List<MapObjectsInfoMessagePack> mapObjects)
            {
                Tag = ProtocolTag;
                MapObjects = mapObjects;
            }
        }
        
        [MessagePackObject]
        public class MapObjectsInfoMessagePack
        {
            [Key(0)] public int InstanceId { get; set; }
            [Key(1)] public bool IsDestroyed { get; set; }
            [Key(2)] public int CurrentHp { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MapObjectsInfoMessagePack() { }
            public MapObjectsInfoMessagePack(int instanceId, bool isDestroyed, int currentHp)
            {
                InstanceId = instanceId;
                IsDestroyed = isDestroyed;
                CurrentHp = currentHp;
            }
        }
    }
}
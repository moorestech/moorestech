using System;
using System.Collections.Generic;
using System.Linq;
using Game.MapObject.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.Base;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// map objectの破壊状況を送信するプロトコル
    /// </summary>
    public class MapObjectDestructionInformationProtocol : IPacketResponse
    {
        public const string Tag = "va:mapObjectInfo";

        private readonly IMapObjectDatastore _mapObjectDatastore;
        
        public MapObjectDestructionInformationProtocol(ServiceProvider serviceProvider)
        {
            _mapObjectDatastore = serviceProvider.GetService<IMapObjectDatastore>();
        }
        
        public List<ToClientProtocolMessagePackBase> GetResponse(List<byte> payload)
        {
            var sendMapObjects = new List<MapObjectDestructionInformationData>();
            foreach (var mapObject in _mapObjectDatastore.MapObjects)
            {
                sendMapObjects.Add(new MapObjectDestructionInformationData(mapObject.InstanceId, mapObject.IsDestroyed));
            }
            
            var response = new ResponseMapObjectDestructionInformationMessagePack(sendMapObjects);
            
            return new List<ToClientProtocolMessagePackBase>(){response};
        }
    }
    
    [MessagePackObject(keyAsPropertyName:true)]
    public class RequestMapObjectDestructionInformationMessagePack : ToServerProtocolMessagePackBase
    {
        public RequestMapObjectDestructionInformationMessagePack()
        {
            ToServerTag = MapObjectDestructionInformationProtocol.Tag;
        }
    }
    
    [MessagePackObject(keyAsPropertyName:true)]
    public class ResponseMapObjectDestructionInformationMessagePack : ToClientProtocolMessagePackBase
    {
        public List<MapObjectDestructionInformationData> MapObjects { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseMapObjectDestructionInformationMessagePack() { }
        
        public ResponseMapObjectDestructionInformationMessagePack(List<MapObjectDestructionInformationData> mapObjects)
        {
            ToClientTag = MapObjectDestructionInformationProtocol.Tag;
            MapObjects = mapObjects;
        }
    }
    
    [MessagePackObject(keyAsPropertyName:true)]
    public class MapObjectDestructionInformationData
    {
        public int Instanceid { get; set; }
        public bool IsDestroyed { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MapObjectDestructionInformationData() { }
        
        public MapObjectDestructionInformationData(int instanceId, bool isDestroyed)
        {
            Instanceid = instanceId;
            IsDestroyed = isDestroyed;
        }
    }
}
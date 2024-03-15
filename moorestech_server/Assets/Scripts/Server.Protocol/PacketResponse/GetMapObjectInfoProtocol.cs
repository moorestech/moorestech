using System;
using System.Collections.Generic;
using System.Linq;
using Game.MapObject.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     map objectの破壊状況を送信するプロトコル
    /// </summary>
    public class GetMapObjectInfoProtocol : IPacketResponse
    {
        public const string Tag = "va:mapObjectInfo";

        private readonly IMapObjectDatastore _mapObjectDatastore;

        public GetMapObjectInfoProtocol(ServiceProvider serviceProvider)
        {
            _mapObjectDatastore = serviceProvider.GetService<IMapObjectDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var sendMapObjects = new List<MapObjectsInfoMessagePack>();
            foreach (var mapObject in _mapObjectDatastore.MapObjects)
                sendMapObjects.Add(new MapObjectsInfoMessagePack(mapObject.InstanceId, mapObject.IsDestroyed));

            var response = new ResponseMapObjectInfosMessagePack(sendMapObjects);

            return response;
        }
    }

    [MessagePackObject]
    public class RequestMapObjectInfosMessagePack : ProtocolMessagePackBase
    {
        public RequestMapObjectInfosMessagePack()
        {
            Tag = GetMapObjectInfoProtocol.Tag;
        }
    }

    [MessagePackObject]
    public class ResponseMapObjectInfosMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseMapObjectInfosMessagePack()
        {
        }

        public ResponseMapObjectInfosMessagePack(List<MapObjectsInfoMessagePack> mapObjects)
        {
            Tag = GetMapObjectInfoProtocol.Tag;
            MapObjects = mapObjects;
        }

        [Key(2)]
        public List<MapObjectsInfoMessagePack> MapObjects { get; set; }
    }

    [MessagePackObject]
    public class MapObjectsInfoMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MapObjectsInfoMessagePack()
        {
        }

        public MapObjectsInfoMessagePack(int instanceId, bool isDestroyed)
        {
            InstanceId = instanceId;
            IsDestroyed = isDestroyed;
        }

        [Key(0)]
        public int InstanceId { get; set; }
        [Key(1)]
        public bool IsDestroyed { get; set; }
    }
}
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
    public class MapObjectDestructionInformationProtocol : IPacketResponse
    {
        public const string Tag = "va:mapObjectInfo";

        private readonly IMapObjectDatastore _mapObjectDatastore;

        public MapObjectDestructionInformationProtocol(ServiceProvider serviceProvider)
        {
            _mapObjectDatastore = serviceProvider.GetService<IMapObjectDatastore>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var sendMapObjects = new List<MapObjectsInfoMessagePack>();
            foreach (var mapObject in _mapObjectDatastore.MapObjects)
                sendMapObjects.Add(new MapObjectsInfoMessagePack(mapObject.InstanceId, mapObject.IsDestroyed));

            var response = new ResponseMapObjectInfosMessagePack(sendMapObjects);

            return new List<List<byte>> { MessagePackSerializer.Serialize(response).ToList() };
        }
    }

    [MessagePackObject(true)]
    public class RequestMapObjectInfosMessagePack : ProtocolMessagePackBase
    {
        public RequestMapObjectInfosMessagePack()
        {
            Tag = MapObjectDestructionInformationProtocol.Tag;
        }
    }

    [MessagePackObject(true)]
    public class ResponseMapObjectInfosMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseMapObjectInfosMessagePack()
        {
        }

        public ResponseMapObjectInfosMessagePack(List<MapObjectsInfoMessagePack> mapObjects)
        {
            Tag = MapObjectDestructionInformationProtocol.Tag;
            MapObjects = mapObjects;
        }

        public List<MapObjectsInfoMessagePack> MapObjects { get; set; }
    }

    [MessagePackObject(true)]
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

        public int InstanceId { get; set; }
        public bool IsDestroyed { get; set; }
    }
}
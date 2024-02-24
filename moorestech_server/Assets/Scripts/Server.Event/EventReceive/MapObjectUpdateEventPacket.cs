using System;
using System.Linq;
using Game.MapObject.Interface;
using MessagePack;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     Mapオブジェクトが破壊など、更新されたらその情報を伝えるためのパケット
    /// </summary>
    public class MapObjectUpdateEventPacket
    {
        public const string EventTag = "va:event:mapObjectUpdate";
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IMapObjectDatastore _mapObjectDatastore;

        public MapObjectUpdateEventPacket(IMapObjectDatastore mapObjectDatastore,
            EventProtocolProvider eventProtocolProvider)
        {
            _mapObjectDatastore = mapObjectDatastore;
            _eventProtocolProvider = eventProtocolProvider;

            _mapObjectDatastore.OnDestroyMapObject += OnDestroyMapObject;
        }

        private void OnDestroyMapObject(IMapObject mapObject)
        {
            var messagePack = new MapObjectUpdateEventMessagePack(MapObjectUpdateEventMessagePack.DestroyEventType, mapObject.InstanceId);
            var data = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag,data);
        }
    }

    [MessagePackObject(true)]
    public class MapObjectUpdateEventMessagePack : EventProtocolMessagePackBase
    {
        public const string DestroyEventType = "destroy";


        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MapObjectUpdateEventMessagePack()
        {
        }

        public MapObjectUpdateEventMessagePack(string eventType, int instanceId)
        {
            EventTag = MapObjectUpdateEventPacket.EventTag;
            EventType = eventType;
            InstanceId = instanceId;
        }

        public string EventType { get; set; }
        public int InstanceId { get; set; }
    }
}
using System;
using System.Linq;
using Game.MapObject.Interface;
using MessagePack;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// Mapオブジェクトが破壊など、更新されたらその情報を伝えるためのパケット
    /// </summary>
    public class MapObjectUpdateEventPacket
    {
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IMapObjectDatastore _mapObjectDatastore;

        public const string EventTag = "va:event:mapObjectUpdate";
        
        public MapObjectUpdateEventPacket(IMapObjectDatastore mapObjectDatastore, EventProtocolProvider eventProtocolProvider)
        {
            _mapObjectDatastore = mapObjectDatastore;
            _eventProtocolProvider = eventProtocolProvider;
            
            _mapObjectDatastore.OnDestroyMapObject += OnDestroyMapObject;
        }

        private void OnDestroyMapObject(IMapObject mapObject)
        {
            _eventProtocolProvider.AddBroadcastEvent(new MapObjectUpdateEventMessagePack(
                MapObjectUpdateEventMessagePack.OnDestroyEventType, mapObject.InstanceId));
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class MapObjectUpdateEventMessagePack : EventProtocolMessagePackBase
    {
        public const string OnDestroyEventType = "destroy";

        public string EventType { get; set; }
        public int InstanceId { get; set; }
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MapObjectUpdateEventMessagePack() { }

        public MapObjectUpdateEventMessagePack(string eventType, int instanceId)
        {
            EventTag = MapObjectUpdateEventPacket.EventTag;
            EventType = eventType;
            InstanceId = instanceId;
        }

    }
}
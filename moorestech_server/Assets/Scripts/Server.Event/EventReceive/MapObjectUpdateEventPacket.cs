using System;
using Game.Context;
using Game.Map.Interface.MapObject;
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
        
        public MapObjectUpdateEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            
            _mapObjectDatastore = ServerContext.MapObjectDatastore;
            _mapObjectDatastore.OnDestroyMapObject += OnDestroyMapObject;
        }
        
        private void OnDestroyMapObject(IMapObject mapObject)
        {
            var messagePack = new MapObjectUpdateEventMessagePack(MapObjectUpdateEventMessagePack.DestroyEventType, mapObject.InstanceId);
            var data = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, data);
        }
    }
    
    [MessagePackObject]
    public class MapObjectUpdateEventMessagePack
    {
        public const string DestroyEventType = "destroy";
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MapObjectUpdateEventMessagePack()
        {
        }
        
        public MapObjectUpdateEventMessagePack(string eventType, int instanceId)
        {
            EventType = eventType;
            InstanceId = instanceId;
        }
        
        [Key(0)] public string EventType { get; set; }
        
        [Key(1)] public int InstanceId { get; set; }
    }
}
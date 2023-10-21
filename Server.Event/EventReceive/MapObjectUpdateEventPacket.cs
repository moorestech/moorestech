using System;
using System.Linq;
using Game.MapObject.Interface;
using MessagePack;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     Map
    /// </summary>
    public class MapObjectUpdateEventPacket
    {
        public const string EventTag = "va:event:mapObjectUpdate";
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IMapObjectDatastore _mapObjectDatastore;

        public MapObjectUpdateEventPacket(IMapObjectDatastore mapObjectDatastore, EventProtocolProvider eventProtocolProvider)
        {
            _mapObjectDatastore = mapObjectDatastore;
            _eventProtocolProvider = eventProtocolProvider;

            _mapObjectDatastore.OnDestroyMapObject += OnDestroyMapObject;
        }

        private void OnDestroyMapObject(IMapObject mapObject)
        {
            var data = MessagePackSerializer.Serialize(new MapObjectUpdateEventMessagePack(
                MapObjectUpdateEventMessagePack.OnDestroyEventType, mapObject.InstanceId
            )).ToList();
            _eventProtocolProvider.AddBroadcastEvent(data);
        }
    }

    [MessagePackObject(true)]
    public class MapObjectUpdateEventMessagePack : EventProtocolMessagePackBase
    {
        public const string OnDestroyEventType = "destroy";


        [Obsolete("。。")]
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
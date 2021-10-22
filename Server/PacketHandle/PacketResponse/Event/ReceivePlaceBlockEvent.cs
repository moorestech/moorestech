using System.Collections.Generic;
using Server.Util;
using World.Event;

namespace Server.PacketHandle.PacketResponse.Event
{
    public static class ReceivePlaceBlockEvent
    {
        private const int EventId = 1;
        public static void Init()
        {
            BlockPlaceEvent.Subscribe(ReceivedEvent);
        }

        private static void ReceivedEvent(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            var c = blockPlaceEventProperties.Coordinate;
            var id = blockPlaceEventProperties.Block.GetBlockId();
            var payload = new List<byte>();
            
            payload.AddRange(ByteArrayConverter.ToByteArray(EventProtocolQueProvider.EventPacketId));
            payload.AddRange(ByteArrayConverter.ToByteArray(EventId));
            payload.AddRange(ByteArrayConverter.ToByteArray(c.x));
            payload.AddRange(ByteArrayConverter.ToByteArray(c.y));
            payload.AddRange(ByteArrayConverter.ToByteArray(id));
            EventProtocolQueProvider.Instance.AddBroadcastEvent(payload.ToArray());

        }
        
    }
}
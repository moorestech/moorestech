using System.Collections.Generic;
using Server.Util;
using World.Event;

namespace Server.Event
{
    public class ReceivePlaceBlockEvent
    {
        private const short EventId = 0;

        public ReceivePlaceBlockEvent()
        {
            BlockPlaceEvent.Subscribe(ReceivedEvent);
        }

        private void ReceivedEvent(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            var c = blockPlaceEventProperties.Coordinate;
            var id = blockPlaceEventProperties.Block.GetBlockId();
            var payload = new List<byte>();
            
            payload.AddRange(ByteListConverter.ToByteArray(EventProtocolQueProvider.EventPacketId));
            payload.AddRange(ByteListConverter.ToByteArray(EventId));
            payload.AddRange(ByteListConverter.ToByteArray(c.x));
            payload.AddRange(ByteListConverter.ToByteArray(c.y));
            payload.AddRange(ByteListConverter.ToByteArray(id));
            EventProtocolQueProvider.Instance.AddBroadcastEvent(payload.ToArray());

        }
        
    }
}
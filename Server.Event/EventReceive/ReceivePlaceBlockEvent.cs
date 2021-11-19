using System.Collections.Generic;
using Server.Util;
using World.Event;

namespace Server.Event.EventReceive
{
    public class ReceivePlaceBlockEvent
    {
        private const short EventId = 0;
        readonly private EventProtocolProvider _eventProtocolProvider;

        public ReceivePlaceBlockEvent(BlockPlaceEvent blockPlaceEvent,EventProtocolProvider eventProtocolProvider)
        {
            blockPlaceEvent.Subscribe(ReceivedEvent);
            _eventProtocolProvider = eventProtocolProvider;
        }

        private void ReceivedEvent(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            var c = blockPlaceEventProperties.Coordinate;
            var id = blockPlaceEventProperties.Block.GetBlockId();
            var payload = new List<byte>();
            
            payload.AddRange(ByteListConverter.ToByteArray(EventProtocolProvider.EventPacketId));
            payload.AddRange(ByteListConverter.ToByteArray(EventId));
            payload.AddRange(ByteListConverter.ToByteArray(c.x));
            payload.AddRange(ByteListConverter.ToByteArray(c.y));
            payload.AddRange(ByteListConverter.ToByteArray(id));
            _eventProtocolProvider.AddBroadcastEvent(payload.ToArray());

        }
        
    }
}
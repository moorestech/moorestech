using System;
using System.Collections.Generic;
using Game.World.Interface.Event;
using Server.Util;

namespace Server.Event.EventReceive
{
    public class ReceivePlaceBlockEvent
    {
        private const short EventId = 0;
        private readonly EventProtocolProvider _eventProtocolProvider;

        public ReceivePlaceBlockEvent(IBlockPlaceEvent blockPlaceEvent, EventProtocolProvider eventProtocolProvider)
        {
            blockPlaceEvent.Subscribe(ReceivedEvent);
            _eventProtocolProvider = eventProtocolProvider;
        }

        private void ReceivedEvent(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            var c = blockPlaceEventProperties.Coordinate;
            var id = blockPlaceEventProperties.Block.GetBlockId();
            var payload = new List<byte>();

            payload.AddRange(ByteListConverter.ToByteArray(ServerEventConst.EventPacketId));
            payload.AddRange(ByteListConverter.ToByteArray(EventId));
            payload.AddRange(ByteListConverter.ToByteArray(c.X));
            payload.AddRange(ByteListConverter.ToByteArray(c.Y));
            payload.AddRange(ByteListConverter.ToByteArray(id));
            _eventProtocolProvider.AddBroadcastEvent(payload.ToArray());
        }
    }
}
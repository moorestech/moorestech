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

            payload.AddRange(ToByteList.Convert(ServerEventConst.EventPacketId));
            payload.AddRange(ToByteList.Convert(EventId));
            payload.AddRange(ToByteList.Convert(c.X));
            payload.AddRange(ToByteList.Convert(c.Y));
            payload.AddRange(ToByteList.Convert(id));
            payload.Add((byte)blockPlaceEventProperties.BlockDirection);
            
            _eventProtocolProvider.AddBroadcastEvent(payload.ToArray());
        }
    }
}
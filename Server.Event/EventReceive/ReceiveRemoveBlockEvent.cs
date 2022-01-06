using System.Collections.Generic;
using Game.World.Interface.Event;
using Server.Util;

namespace Server.Event.EventReceive
{
    public class ReceiveRemoveBlockEvent
    {
        private readonly EventProtocolProvider _eventProtocolProvider;
        private const short EventId = 3;

        public ReceiveRemoveBlockEvent(IBlockRemoveEvent blockRemoveEvent, EventProtocolProvider eventProtocolProvider)
        {
            blockRemoveEvent.Subscribe(ReceivedEvent);
            _eventProtocolProvider = eventProtocolProvider;
        }

        private void ReceivedEvent(BlockRemoveEventProperties blockPlaceEventProperties)
        {
            var c = blockPlaceEventProperties.Coordinate;
            var payload = new List<byte>();

            payload.AddRange(ByteListConverter.ToByteArray(ServerEventConst.EventPacketId));
            payload.AddRange(ByteListConverter.ToByteArray(EventId));
            payload.AddRange(ByteListConverter.ToByteArray(c.X));
            payload.AddRange(ByteListConverter.ToByteArray(c.Y));

            _eventProtocolProvider.AddBroadcastEvent(payload.ToArray());
        }
    }
}
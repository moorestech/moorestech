using System.Collections.Generic;
using Game.World.Interface.Event;
using Server.Util;

namespace Server.Event.EventReceive
{
    public class RemoveBlockToSetEventPacket
    {
        private readonly EventProtocolProvider _eventProtocolProvider;
        private const short EventId = 3;

        public RemoveBlockToSetEventPacket(IBlockRemoveEvent blockRemoveEvent, EventProtocolProvider eventProtocolProvider)
        {
            blockRemoveEvent.Subscribe(ReceivedEvent);
            _eventProtocolProvider = eventProtocolProvider;
        }

        private void ReceivedEvent(BlockRemoveEventProperties blockPlaceEventProperties)
        {
            var c = blockPlaceEventProperties.Coordinate;
            var payload = new List<byte>();

            payload.AddRange(ToByteList.Convert(ServerEventConst.EventPacketId));
            payload.AddRange(ToByteList.Convert(EventId));
            payload.AddRange(ToByteList.Convert(c.X));
            payload.AddRange(ToByteList.Convert(c.Y));

            _eventProtocolProvider.AddBroadcastEvent(payload);
        }
    }
}
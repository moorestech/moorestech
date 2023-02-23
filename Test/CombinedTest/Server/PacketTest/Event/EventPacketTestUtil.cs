using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Server.Event.EventReceive;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public static class EventPacketTestUtil
    {
        public static bool IsEventPacketExist(List<List<byte>> packets)
        {
            var eventPacket = packets.Where(p => 
                MessagePackSerializer.Deserialize<EventProtocolMessagePackBase>(p.ToArray()).
                    Tag == EventProtocolMessagePackBase.EventProtocolTag);
            return eventPacket.Any();
        }

        public static EventProtocolMessagePackBase GetEventPacket(List<List<byte>> packets)
        {
            var eventPacket = packets.Where(p => 
                MessagePackSerializer.Deserialize<EventProtocolMessagePackBase>(p.ToArray()).
                    Tag == EventProtocolMessagePackBase.EventProtocolTag).ToArray();
            return MessagePackSerializer.Deserialize<EventProtocolMessagePackBase>(eventPacket.ToArray()[0].ToArray()); 
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class EventTestUtil
    {
        public static List<byte> EventRequestData(int playerID)
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(playerID)).ToList();
        }
    }
}
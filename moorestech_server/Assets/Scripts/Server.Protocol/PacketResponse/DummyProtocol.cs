using System.Collections.Generic;

namespace Server.Protocol.PacketResponse
{
    public class DummyProtocol : IPacketResponse
    {
        public const string Tag = "va:dummy";

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            return null;
        }
    }
}
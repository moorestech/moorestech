using System.Collections.Generic;

namespace Server.Protocol.PacketResponse
{
    public class DummyProtocol : IPacketResponse
    {
        public List<byte[]> GetResponse(List<byte> payload)
        {
            return new List<byte[]>();
        }
    }
}
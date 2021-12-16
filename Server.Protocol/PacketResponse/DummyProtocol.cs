using System.Collections.Generic;
using Server.Protocol.PacketResponse;

namespace Server.PacketHandle.PacketResponse
{
    public class DummyProtocol : IPacketResponse
    {
        public List<byte[]> GetResponse(List<byte> payload)
        {
            return new List<byte[]>();
        }
    }
}
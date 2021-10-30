using System.Collections.Generic;

namespace Server.PacketHandle.PacketResponse
{
    public class DummyProtocol : IPacketResponse
    {
        public List<byte[]> GetResponse(byte[] payload)
        {
            return new List<byte[]>();
        }
    }
}
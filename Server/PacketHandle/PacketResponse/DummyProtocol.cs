using System.Collections.Generic;

namespace industrialization.Server.PacketHandle.PacketResponse
{
    public static class DummyProtocol
    {
        public static List<byte[]> GetResponse(byte[] payload)
        {
            return new List<byte[]>();
        }
    }
}
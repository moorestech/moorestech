using System;

namespace industrialization.Server.PacketResponse.ProtocolImplementation
{
    public class DummyProtocol
    {
        public static byte[] GetResponse(byte[] payload)
        {
            return Array.Empty<byte>();
        }
    }
}
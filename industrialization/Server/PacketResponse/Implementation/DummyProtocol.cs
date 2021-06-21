using System;

namespace industrialization.Server.PacketResponse.Implementation
{
    public class DummyProtocol : IPacketResponse
    {
        public byte[] GetResponse()
        {
            return Array.Empty<byte>();
        }

        public static IPacketResponse NewInstance(byte[] payload)
        {
            return new DummyProtocol();
        }
    }
}
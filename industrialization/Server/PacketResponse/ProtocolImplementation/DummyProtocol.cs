using System;

namespace industrialization.Server.PacketResponse.ProtocolImplementation
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
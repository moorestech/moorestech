using System;

namespace industrialization.Server.PacketResponse.Implementation
{
    public class DummyProtocol : IPacketResponse
    {
        public byte[] GetResponse()
        {
            short id = 0;
            return BitConverter.GetBytes(id);
        }

        public static IPacketResponse NewInstance(byte[] payload)
        {
            return new DummyProtocol();
        }
    }
}
using System;

namespace industrialization.Server.PacketResponse.ProtocolImplementation
{
    public static class DummyProtocol
    {
        public static byte[,] GetResponse(byte[] payload)
        {
            return new byte[0,0];
        }
    }
}
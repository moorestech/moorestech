using System;
using System.Collections.Generic;

namespace industrialization.Server.PacketResponse.ProtocolImplementation
{
    public static class DummyProtocol
    {
        public static List<byte[]> GetResponse(byte[] payload)
        {
            return new List<byte[]>();
        }
    }
}
using System.Collections.Generic;

namespace Server.Protocol.PacketResponse
{
    public class DummyProtocol : IPacketResponse
    {
        public const string Tag = "va:dummy";
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            return new List<List<byte>>();
        }
    }
}
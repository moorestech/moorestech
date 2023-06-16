using System.Collections.Generic;
using Server.Protocol.Base;

namespace Server.Protocol.PacketResponse
{
    public class DummyProtocol : IPacketResponse
    {
        public const string Tag = "va:dummy";
        public List<ToClientProtocolMessagePackBase> GetResponse(List<byte> payload)
        {
            return new List<ToClientProtocolMessagePackBase>();
        }
    }
}
using System.Collections.Generic;
using Server.Protocol.Base;

namespace Server.Protocol.PacketResponse
{
    public interface IPacketResponse
    {
        public List<ToClientProtocolMessagePackBase> GetResponse(List<byte> payload);
    }
}
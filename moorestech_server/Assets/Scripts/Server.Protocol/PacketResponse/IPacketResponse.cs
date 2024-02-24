using System.Collections.Generic;

namespace Server.Protocol.PacketResponse
{
    public interface IPacketResponse
    {
        public ProtocolMessagePackBase GetResponse(List<byte> payload);
    }
}
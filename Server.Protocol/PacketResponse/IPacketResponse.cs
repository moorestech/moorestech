using System.Collections.Generic;

namespace Server.PacketHandle.PacketResponse
{
    public interface IPacketResponse
    {
        public List<byte[]> GetResponse(List<byte> payload);
    }
}
using System.Collections.Generic;

namespace Server.Protocol.PacketResponse
{
    public class QuestProgressRequestProtocol : IPacketResponse
    {
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            throw new System.NotImplementedException();
        }
    }
}
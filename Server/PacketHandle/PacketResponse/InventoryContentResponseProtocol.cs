using System.Collections.Generic;

namespace Server.PacketHandle.PacketResponse
{
    public class InventoryContentResponseProtocol : IPacketResponse
    {
        public List<byte[]> GetResponse(List<byte>  payload)
        {
            throw new System.NotImplementedException();
        }
    }
}
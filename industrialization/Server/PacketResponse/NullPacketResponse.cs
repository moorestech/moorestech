using industrialization.Server.PacketResponse.Implementation;

namespace industrialization.Server.PacketResponse
{
    public class NullPacketResponse : IPacketResponse
    {
        public byte[] GetResponse()
        {
            return System.Array.Empty<byte>();
        }
    }
}
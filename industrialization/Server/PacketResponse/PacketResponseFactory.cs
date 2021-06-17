namespace industrialization.Server.PacketResponse
{
    public class PacketResponseFactory
    {
        public static IPacketResponse GetPacketResponse(byte[] payload)
        {
            //TODO ここ書く
            return new NullPacketResponse();
        }
    }
}
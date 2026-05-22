namespace Server.Protocol.PacketResponse
{
    public interface IConnectionAwarePacketResponse : IPacketResponse
    {
        ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context);
    }
}

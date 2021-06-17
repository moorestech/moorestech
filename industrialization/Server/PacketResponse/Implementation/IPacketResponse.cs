namespace industrialization.Server.PacketResponse.Implementation
{
    public interface IPacketResponse
    {
        byte[] GetResponse();

        IPacketResponse NewInstance(byte[] payload);
    }
}
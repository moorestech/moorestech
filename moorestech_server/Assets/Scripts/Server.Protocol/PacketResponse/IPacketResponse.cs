namespace Server.Protocol.PacketResponse
{
    public interface IPacketResponse
    {
        public ProtocolMessagePackBase GetResponse(byte[] payload);
    }
}

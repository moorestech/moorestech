namespace Server.Protocol.PacketResponse
{
    // 接続前に受ける要求は、接続へのID紐づけを行うためコンテキストを受け取る
    // Pre-binding requests receive the context so they can bind the assigned player ID
    public interface IHandshakePacketResponse
    {
        ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context);
    }
}

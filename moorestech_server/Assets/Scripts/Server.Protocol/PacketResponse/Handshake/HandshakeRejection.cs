namespace Server.Protocol.PacketResponse.Handshake
{
    // クライアントが表示文言へ変換する拒否理由
    // Rejection reasons mapped to display text by the client
    public enum HandshakeRejection
    {
        None = 0,
        InvalidIdentity = 1,
        AlreadyConnected = 2,
        ConnectionClosed = 3,
    }
}

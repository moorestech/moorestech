namespace Server.Protocol.PacketResponse.Handshake
{
    // クライアントが表示文言へ変換する拒否理由
    // Rejection reasons mapped to display text by the client
    public enum HandshakeRejection
    {
        None = 0,
        InvalidIdentity = 1,

        // 同じ身元が別の接続で先に繋がっている
        // The same identity is already bound to an earlier connection
        AlreadyConnected = 2,
        ConnectionClosed = 3,

        // この接続が既にハンドシェイク済み（身元の付け替え要求）
        // This connection already handshaked, so the request asks to switch identities
        AlreadyHandshaked = 4,
    }
}

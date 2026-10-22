namespace Client.Network.API
{
    // 応答待ちが完了した理由を明示する
    // Distinguishes how a packet wait completed
    public enum PacketWaitCompletionReason
    {
        Received,
        Timeout,
        DeserializeFailed,
    }
}

namespace Server.Protocol
{
    // 接続単位のプロトコル実行情報。ハンドシェイクで紐付いた playerId を切断処理へ渡す。
    // Per-connection protocol context. Carries the handshaken playerId to disconnect cleanup.
    public class PacketResponseContext
    {
        public int? PlayerId { get; private set; }

        public void BindPlayerId(int playerId)
        {
            PlayerId = playerId;
        }
    }
}

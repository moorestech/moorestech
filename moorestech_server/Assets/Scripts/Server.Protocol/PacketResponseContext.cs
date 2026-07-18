using Server.Event;

namespace Server.Protocol
{
    // 接続単位のプロトコル実行情報。ハンドシェイクで紐付いた playerId を切断処理へ渡す。
    // Per-connection protocol context. Carries the handshaken playerId to disconnect cleanup.
    public class PacketResponseContext
    {
        // BindPlayerId はメインスレッド、PlayerId 読み取りは受信スレッドから呼ばれるため lock で保護する。
        // BindPlayerId runs on the main thread while PlayerId is read on the receive thread, so guard with a lock.
        private readonly object _lock = new();
        private int? _playerId;

        // 接続生成時に一度だけセットされ、以後読み取り専用（受信スレッド起動前にセットされるためlock不要）
        // Set once at connection creation before the receive thread starts; read-only afterwards
        public IPlayerEventSink EventSink { get; private set; }

        public int? PlayerId
        {
            get
            {
                lock (_lock)
                {
                    return _playerId;
                }
            }
        }

        public void BindPlayerId(int playerId)
        {
            lock (_lock)
            {
                _playerId = playerId;
            }
        }

        public void SetEventSink(IPlayerEventSink eventSink)
        {
            EventSink = eventSink;
        }
    }
}

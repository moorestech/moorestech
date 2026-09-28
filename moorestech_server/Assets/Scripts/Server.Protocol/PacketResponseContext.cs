using Server.Event;

namespace Server.Protocol
{
    // 接続単位のプロトコル実行情報。ハンドシェイクで紐付いた playerId を切断処理へ渡す。
    // Per-connection protocol context. Carries the handshaken playerId to disconnect cleanup.
    public class PacketResponseContext
    {
        // 本番のバインドとcloseはtick末尾FIFOで直列化し、直接呼ぶ境界にも備えてlockを保つ
        // Production bind and close are serialized by the tick-end FIFO; keep the lock for direct boundary callers
        private readonly object _lock = new();
        private int? _playerId;
        private bool _closed;

        public IPlayerEventSink EventSink { get; }

        public PacketResponseContext(IPlayerEventSink eventSink)
        {
            EventSink = eventSink;
        }

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

        // close済みならバインドを拒否する。FIFOで先に来た操作が確定する
        // Refuse binding after close; the earlier FIFO operation wins
        public bool TryBindPlayerId(int playerId)
        {
            lock (_lock)
            {
                if (_closed) return false;
                _playerId = playerId;
                return true;
            }
        }

        // 切断確定を記録し、その時点でバインド済みのplayerIdを返す（以後のバインドは失敗する）
        // Marks the connection closed and returns the playerId bound so far; later binds fail
        public int? MarkClosedAndGetPlayerId()
        {
            lock (_lock)
            {
                _closed = true;
                return _playerId;
            }
        }
    }
}

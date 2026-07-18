using Server.Event;

namespace Server.Protocol
{
    // 接続単位のプロトコル実行情報。ハンドシェイクで紐付いた playerId を切断処理へ渡す。
    // Per-connection protocol context. Carries the handshaken playerId to disconnect cleanup.
    public class PacketResponseContext
    {
        // バインドはメインスレッド、close/読み取りは受信スレッドから呼ばれるため lock で保護する。
        // Bind runs on the main thread while close/read run on the receive thread, so guard with a lock.
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

        // close済みならバインドを拒否する。handshake処理と切断Cleanupの競合を直列化する要
        // Refuses to bind once closed; linearizes the handshake vs disconnect-cleanup race
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

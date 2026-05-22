using System;
using System.Collections.Generic;
using UniRx;

namespace Game.PlayerConnection
{
    // 接続中の playerId を管理し、乗車システムの IPlayerConnectionChecker として使う。
    // Tracks connected playerIds and serves as the riding system's IPlayerConnectionChecker.
    public class PlayerConnectionRegistry : IPlayerConnectionChecker
    {
        private readonly HashSet<int> _connectedPlayerIds = new();
        private readonly object _lock = new();
        private readonly Subject<int> _disconnectedSubject = new();

        public IObservable<int> OnPlayerDisconnected => _disconnectedSubject;

        public void Register(int playerId)
        {
            lock (_lock)
            {
                _connectedPlayerIds.Add(playerId);
            }
        }

        public void Unregister(int playerId)
        {
            // リスト更新と切断通知を 1 ロックで原子的に行う。Unregister は接続ごとの受信スレッドから並行で呼ばれ、
            // 排他ロック内の OnNext なら Subject.OnNext（並行呼び出し非対応）の競合も同時に防げる。
            // Update the set and fire the disconnect notification atomically under one lock.
            // Unregister runs concurrently on per-connection threads; OnNext inside the exclusive lock avoids the Subject race.
            lock (_lock)
            {
                // 登録済み playerId の切断だけを通知する。
                // Notify only when a registered player actually disconnects.
                if (_connectedPlayerIds.Remove(playerId))
                {
                    _disconnectedSubject.OnNext(playerId);
                }
            }
        }

        public bool IsConnected(int playerId)
        {
            lock (_lock)
            {
                return _connectedPlayerIds.Contains(playerId);
            }
        }
    }
}

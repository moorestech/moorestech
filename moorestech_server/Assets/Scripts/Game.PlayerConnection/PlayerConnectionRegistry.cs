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
            bool disconnected;
            lock (_lock)
            {
                disconnected = _connectedPlayerIds.Remove(playerId);
            }

            // 登録済み playerId の切断だけを通知する。
            // Notify only when a registered player actually disconnects.
            if (disconnected)
            {
                _disconnectedSubject.OnNext(playerId);
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

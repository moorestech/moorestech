using System;
using System.Collections.Generic;
using UniRx;

namespace Game.PlayerConnection
{
    // 接続中の playerId を管理し、乗車システムの IPlayerConnectionChecker として使う。
    // Tracks connected playerIds and serves as the riding system's IPlayerConnectionChecker.
    public class PlayerConnectionRegistry : IPlayerConnectionChecker
    {
        private readonly Dictionary<int, int> _connectionCountByPlayerId = new();
        private readonly object _lock = new();
        private readonly Subject<int> _disconnectedSubject = new();

        public IObservable<int> OnPlayerDisconnected => _disconnectedSubject;

        public void Register(int playerId)
        {
            lock (_lock)
            {
                _connectionCountByPlayerId.TryGetValue(playerId, out var count);
                _connectionCountByPlayerId[playerId] = count + 1;
            }
        }

        public void Unregister(int playerId)
        {
            var disconnected = false;
            lock (_lock)
            {
                if (!_connectionCountByPlayerId.TryGetValue(playerId, out var count))
                {
                    return;
                }

                // 同一 playerId の重複接続がある場合は最後の接続終了まで online とみなす。
                // Duplicate connections stay online until the final connection closes.
                if (1 < count)
                {
                    _connectionCountByPlayerId[playerId] = count - 1;
                    return;
                }

                _connectionCountByPlayerId.Remove(playerId);
                disconnected = true;
            }

            // 最後の接続が閉じた playerId だけ切断通知を出す。
            // Notify only when the player's final connection closes.
            if (disconnected)
            {
                _disconnectedSubject.OnNext(playerId);
            }
        }

        public bool IsConnected(int playerId)
        {
            lock (_lock)
            {
                return _connectionCountByPlayerId.ContainsKey(playerId);
            }
        }
    }
}

using System.Collections.Generic;
using Game.PlayerRiding.Interface;

namespace Game.PlayerRiding
{
    // 乗車状態の中核データストア。乗車可否・空席割当・降車の決定ロジックを集約する（仕様書セクション4.0・4.1）。
    // Core datastore for riding state. Owns ride/dismount decision logic.
    public class PlayerRidingDatastore
    {
        private readonly RidableResolver _ridableResolver;
        private readonly IPlayerConnectionChecker _connectionChecker;

        // playerId -> RidingState
        private readonly Dictionary<int, RidingState> _ridingStateByPlayerId = new();

        public PlayerRidingDatastore(RidableResolver ridableResolver, IPlayerConnectionChecker connectionChecker)
        {
            _ridableResolver = ridableResolver;
            _connectionChecker = connectionChecker;
        }

        public bool TryGetRidingState(int playerId, out RidingState ridingState)
        {
            return _ridingStateByPlayerId.TryGetValue(playerId, out ridingState);
        }

        // 乗車要求。空席を割り当てて RidingState を設定する。
        // Ride request. Assigns a free seat and sets the RidingState.
        public RideActionResult TryRide(int playerId, IRidableIdentifier identifier, out int assignedSeatIndex)
        {
            assignedSeatIndex = -1;

            // 既に乗車中なら拒否（移乗は不可、先に降車が必要）
            // Reject if already riding (no transfer; must dismount first).
            if (_ridingStateByPlayerId.ContainsKey(playerId))
            {
                return RideActionResult.AlreadyRiding;
            }

            var ridable = _ridableResolver.Resolve(identifier);
            if (ridable == null)
            {
                return RideActionResult.RidableNotFound;
            }

            var freeSeat = FindFreeSeat(identifier, ridable.SeatCount);
            if (freeSeat < 0)
            {
                return RideActionResult.NoSeatAvailable;
            }

            _ridingStateByPlayerId[playerId] = new RidingState(identifier, freeSeat);
            assignedSeatIndex = freeSeat;
            return RideActionResult.Success;

            #region Internal

            int FindFreeSeat(IRidableIdentifier target, int seatCount)
            {
                // 接続中プレイヤーが占有していない最小の座席indexを返す（仕様書セクション7）
                // Returns the smallest seat index not occupied by a connected player.
                for (var seat = 0; seat < seatCount; seat++)
                {
                    if (!IsSeatOccupiedByConnectedPlayer(target, seat))
                    {
                        return seat;
                    }
                }
                return -1;
            }

            #endregion
        }

        // 降車要求。RidingState をクリアする。
        // Dismount request. Clears the RidingState.
        public RideActionResult TryDismount(int playerId)
        {
            if (!_ridingStateByPlayerId.ContainsKey(playerId))
            {
                return RideActionResult.NotRiding;
            }

            _ridingStateByPlayerId.Remove(playerId);
            return RideActionResult.Success;
        }

        // 乗り物が破棄されたとき、その乗り物に乗っていた全プレイヤーの RidingState をクリアする。
        // 戻り値は降車させた playerId 一覧（接続中の乗員へ降車イベントを broadcast するために使う。Phase 3）。
        // Clears riding states of all players on a removed ridable. Returns the dismounted player ids.
        public IReadOnlyList<int> OnRidableRemoved(IRidableIdentifier identifier)
        {
            var dismounted = new List<int>();
            foreach (var pair in _ridingStateByPlayerId)
            {
                if (pair.Value.Identifier.Equals(identifier))
                {
                    dismounted.Add(pair.Key);
                }
            }
            // 降車処理は冪等（既に消えていれば dismounted は空。仕様書セクション4.4）
            // Idempotent: if nothing matched, dismounted is empty.
            foreach (var playerId in dismounted)
            {
                _ridingStateByPlayerId.Remove(playerId);
            }
            return dismounted;
        }

        // 同じ (identifier, seatIndex) を持つ接続中の別プレイヤーがいるか
        // Whether a connected other player occupies the same (identifier, seatIndex).
        private bool IsSeatOccupiedByConnectedPlayer(IRidableIdentifier identifier, int seatIndex, int excludePlayerId)
        {
            foreach (var pair in _ridingStateByPlayerId)
            {
                if (pair.Key == excludePlayerId) continue;
                var state = pair.Value;
                if (state.SeatIndex == seatIndex
                    && state.Identifier.Equals(identifier)
                    && _connectionChecker.IsConnected(pair.Key))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsSeatOccupiedByConnectedPlayer(IRidableIdentifier identifier, int seatIndex)
        {
            return IsSeatOccupiedByConnectedPlayer(identifier, seatIndex, -1);
        }
    }
}

using System.Collections.Generic;
using Game.PlayerConnection;
using Game.PlayerRiding.Interface;

namespace Game.PlayerRiding
{
    // 乗車状態の中核データストア。乗車可否・空席割当・降車の決定ロジックを集約する（仕様書セクション4.0・4.1）。
    // Core datastore for riding state. Owns ride/dismount decision logic.
    public class PlayerRidingDatastore
    {
        // 座席占有判定で「除外するプレイヤーなし」を表す番兵値
        // Sentinel passed to the seat-occupancy check when no player should be excluded.
        private const int NoExcludePlayerId = -1;

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
                    if (!IsSeatOccupiedByConnectedPlayer(target, seat, NoExcludePlayerId))
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

        // 乗り物が破棄されたとき、その乗り物の乗員のうち接続中のプレイヤーのみ降車させる。
        // 切断中の乗員は RidingState を残し、ログイン時に降車検知させる（仕様書セクション4.4・8）。
        // On ridable removal, dismounts only connected riders; disconnected riders keep their RidingState for login-time detection.
        public IReadOnlyList<int> OnRidableRemoved(IRidableIdentifier identifier)
        {
            var dismounted = new List<int>();
            foreach (var pair in _ridingStateByPlayerId)
            {
                if (pair.Value.Identifier.Equals(identifier) && _connectionChecker.IsConnected(pair.Key))
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

        // ログイン時の復帰判定（仕様書セクション8）。
        // 復帰可なら RidingState を維持して true、不可ならクリアして false を返す。
        // Login-time evaluation. Keeps the RidingState and returns true if restorable, else clears and returns false.
        public bool EvaluateOnLogin(int playerId)
        {
            if (!_ridingStateByPlayerId.TryGetValue(playerId, out var state))
            {
                return false;
            }

            var ridable = _ridableResolver.Resolve(state.Identifier);
            // 乗り物が消失している
            // The ridable no longer exists.
            if (ridable == null)
            {
                _ridingStateByPlayerId.Remove(playerId);
                return false;
            }
            // 記録席が範囲外（マスタ変更・セーブ手編集対策。仕様書セクション8）
            // The recorded seat is out of range (guards against master changes / hand-edited saves).
            if (state.SeatIndex < 0 || ridable.SeatCount <= state.SeatIndex)
            {
                _ridingStateByPlayerId.Remove(playerId);
                return false;
            }
            // 記録席を接続中の別プレイヤーが使用中
            // The recorded seat is taken by another connected player.
            if (IsSeatOccupiedByConnectedPlayer(state.Identifier, state.SeatIndex, playerId))
            {
                _ridingStateByPlayerId.Remove(playerId);
                return false;
            }
            return true;
        }

        // 乗車状態を全件セーブDTOに変換する（仕様書セクション10）。
        // Converts all riding states into save DTOs.
        public List<PlayerRidingSaveData> GetSaveData()
        {
            var list = new List<PlayerRidingSaveData>();
            foreach (var pair in _ridingStateByPlayerId)
            {
                // 識別子の直列化は型ごとの GetSaveState() に委譲する（乗り物種別の増加に DTO 非依存）。
                // Identifier serialization is delegated to each type's GetSaveState().
                var identifier = pair.Value.Identifier;
                list.Add(new PlayerRidingSaveData
                {
                    PlayerId = pair.Key,
                    RidableType = (byte)identifier.Type,
                    IdentifierState = identifier.GetSaveState(),
                    SeatIndex = pair.Value.SeatIndex,
                });
            }
            return list;
        }

        // セーブDTOから乗車状態を復元する。参照先の存在検証はしない（ログイン時まで遅延。仕様書セクション10）。
        // Restores riding states from save DTOs. No reference validation here (deferred to login).
        public void LoadSaveData(IReadOnlyList<PlayerRidingSaveData> saveData)
        {
            _ridingStateByPlayerId.Clear();
            if (saveData == null) return;
            foreach (var data in saveData)
            {
                // 判別子とペイロード文字列から識別子を復元する。未知の型は読み飛ばす。
                // Restore the identifier from the discriminator and payload; skip unknown types.
                var identifier = RidableIdentifierConverter.FromSaveState((RidableType)data.RidableType, data.IdentifierState);
                if (identifier == null) continue;
                _ridingStateByPlayerId[data.PlayerId] = new RidingState(identifier, data.SeatIndex);
            }
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
    }
}

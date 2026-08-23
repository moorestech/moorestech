using System.Collections.Generic;

namespace Client.Game.InGame.Map.MapObject.Pending
{
    /// <summary>
    ///     未生成宛の破壊/HPを保留する台帳
    ///     Holds destroy/HP events for not-yet-instantiated objects
    /// </summary>
    public sealed class MapObjectPendingStateLedger
    {
        private readonly Dictionary<int, MapObjectPendingState> _statesByInstanceId = new();

        public void RecordDestroy(int instanceId)
        {
            // 保留HPを保ち破壊フラグのみ立てる
            // Keep pending HP and raise only the destroyed flag
            _statesByInstanceId.TryGetValue(instanceId, out var current);
            _statesByInstanceId[instanceId] = new MapObjectPendingState(true, current.HasHp, current.Hp);
        }

        public void RecordHp(int instanceId, int hp)
        {
            // 最新HPで上書き、破壊フラグは保持
            // Overwrite with the latest HP, keep the destroyed flag
            _statesByInstanceId.TryGetValue(instanceId, out var current);
            _statesByInstanceId[instanceId] = new MapObjectPendingState(current.IsDestroyed, true, hp);
        }

        public bool TryConsume(int instanceId, out MapObjectPendingState state)
        {
            if (!_statesByInstanceId.TryGetValue(instanceId, out state)) return false;
            _statesByInstanceId.Remove(instanceId);
            return true;
        }
    }

    /// <summary>
    ///     保留された破壊/HPの合成状態
    ///     The merged pending destroy/HP state
    /// </summary>
    public readonly struct MapObjectPendingState
    {
        public readonly bool IsDestroyed;
        public readonly bool HasHp;
        public readonly int Hp;

        internal MapObjectPendingState(bool isDestroyed, bool hasHp, int hp)
        {
            IsDestroyed = isDestroyed;
            HasHp = hasHp;
            Hp = hp;
        }
    }
}

using System.Collections.Generic;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     未生成個体宛の破壊/HPイベントをinstanceId単位で保留する台帳。生成時に消費されスナップショットより優先される（ADR 0030）
    ///     Holds destroy/HP events for not-yet-instantiated objects per instanceId; consumed at instantiation and overrides the snapshot (ADR 0030)
    /// </summary>
    public sealed class MapObjectPendingStateLedger
    {
        private readonly Dictionary<int, MapObjectPendingState> _statesByInstanceId = new();

        public void RecordDestroy(int instanceId)
        {
            // 既存の保留HPを保ったまま破壊フラグだけ立てる（未記録ならdefault合成）
            // Keep any pending HP and raise only the destroyed flag (merging onto default when unrecorded)
            _statesByInstanceId.TryGetValue(instanceId, out var current);
            _statesByInstanceId[instanceId] = new MapObjectPendingState(true, current.HasHp, current.Hp);
        }

        public void RecordHp(int instanceId, int hp)
        {
            // 最新HPで上書きし、既存の破壊フラグは保つ
            // Overwrite with the latest HP while keeping any destroyed flag
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

        public MapObjectPendingState(bool isDestroyed, bool hasHp, int hp)
        {
            IsDestroyed = isDestroyed;
            HasHp = hasHp;
            Hp = hp;
        }
    }
}

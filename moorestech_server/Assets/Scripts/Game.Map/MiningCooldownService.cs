using System.Collections.Generic;
using Core.Update;
using Game.Map.Interface;
using Game.Map.Interface.Json;

namespace Game.Map
{
    /// <summary>
    ///     全手掘りの共有クールダウン
    ///     Shared cooldown for all hand mining
    /// </summary>
    public class MiningCooldownService : IMiningCooldownDatastore
    {
        // クールダウン判定の許容率。クライアントはattackSpeed間隔ちょうどで送るためジッタ余裕を持たせる
        // Cooldown tolerance; clients send at exactly attackSpeed intervals, so allow jitter
        private const double CooldownMarginRate = 0.9;

        // 1プレイヤー1振りを保証する最終打撃tick
        // Last-hit ticks enforcing one swing at a time per player
        private readonly Dictionary<int, ulong> _lastAttackTicks = new();

        public bool IsInCooldown(int playerId, double attackSpeed)
        {
            if (!_lastAttackTicks.TryGetValue(playerId, out var lastAttackTick)) return false;
            return GameUpdater.CurrentTick - lastAttackTick < GameUpdater.SecondsToTicks(attackSpeed * CooldownMarginRate);
        }

        public void RecordAttack(int playerId)
        {
            _lastAttackTicks[playerId] = GameUpdater.CurrentTick;
        }

        // Dictionaryの列挙順は削除跡の再利用で変わる。スナップショット比較が添字で突き合わせるので保存側で正準化する
        // Dictionary order shifts as removed slots get reused, so canonicalize here for the snapshot comparer that matches by index
        public List<PlayerMiningCooldownSaveJsonObject> GetSaveJsonObject()
        {
            var saveData = new List<PlayerMiningCooldownSaveJsonObject>(_lastAttackTicks.Count);
            foreach (var lastAttack in _lastAttackTicks) saveData.Add(new PlayerMiningCooldownSaveJsonObject(lastAttack.Key, lastAttack.Value));
            saveData.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return saveData;
        }

        public void LoadMiningCooldowns(List<PlayerMiningCooldownSaveJsonObject> saveData)
        {
            _lastAttackTicks.Clear();
            foreach (var entry in saveData) _lastAttackTicks[entry.PlayerId] = entry.LastAttackTick;
        }
    }
}

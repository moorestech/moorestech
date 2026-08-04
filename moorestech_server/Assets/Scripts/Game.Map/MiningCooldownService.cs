using System.Collections.Generic;
using Core.Update;

namespace Game.Map
{
    /// <summary>
    ///     手採採掘のプレイヤー単位クールダウン。mapObject採掘とvein採掘で共有し1振り制限を全採掘共通にする
    ///     Per-player cooldown for hand mining; shared by mapObject and vein mining to enforce one swing at a time
    /// </summary>
    public class MiningCooldownService
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
    }
}

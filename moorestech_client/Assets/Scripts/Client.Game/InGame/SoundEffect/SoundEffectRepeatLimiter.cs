using System.Collections.Generic;

namespace Client.Game.InGame.SoundEffect
{
    /// <summary>
    /// 同じ種類の効果音が短い間隔で重なって鳴るのを防ぐ。一括撤去のように同じ音が数百回要求されても、間隔ごとに1回だけ通す
    /// Keeps one sound type from stacking on itself; hundreds of requests for the same sound pass once per interval
    /// </summary>
    public class SoundEffectRepeatLimiter
    {
        public const float MinimumIntervalSeconds = 0.1f;

        private readonly Dictionary<SoundEffectType, float> _lastAcceptedSeconds = new();

        public bool TryAccept(SoundEffectType soundEffectType, float nowSeconds)
        {
            // 前回から間隔が空いていなければ鳴らさない
            // Reject while the interval since the last accepted play has not elapsed
            if (_lastAcceptedSeconds.TryGetValue(soundEffectType, out var lastSeconds) && nowSeconds - lastSeconds < MinimumIntervalSeconds) return false;
            _lastAcceptedSeconds[soundEffectType] = nowSeconds;
            return true;
        }
    }
}

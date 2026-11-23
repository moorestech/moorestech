namespace Client.Game.InGame.SoundEffect
{
    /// <summary>
    /// 同じ効果音が短い間隔で重なって鳴るのを防ぐ。一括撤去のように同じ音が数百回要求されても、間隔ごとに1回だけ通す
    /// Keeps one sound from stacking on itself; hundreds of requests for the same sound pass once per interval
    /// </summary>
    public class SoundEffectRepeatLimiter
    {
        private readonly float _minimumIntervalSeconds;
        private bool _hasAccepted;
        private float _lastAcceptedSeconds;

        public SoundEffectRepeatLimiter(float minimumIntervalSeconds)
        {
            _minimumIntervalSeconds = minimumIntervalSeconds;
        }

        public bool TryAccept(float nowSeconds)
        {
            // 前回から間隔が空いていなければ鳴らさない
            // Reject while the interval since the last accepted play has not elapsed
            if (_hasAccepted && nowSeconds - _lastAcceptedSeconds < _minimumIntervalSeconds) return false;
            _hasAccepted = true;
            _lastAcceptedSeconds = nowSeconds;
            return true;
        }
    }
}

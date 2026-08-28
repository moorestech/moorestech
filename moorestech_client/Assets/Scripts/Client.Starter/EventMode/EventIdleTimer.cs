namespace Client.Starter.EventMode
{
    // 無操作時間の積算。入力の「変化」だけを操作として扱う
    // Accumulates idle time, treating only input changes as activity
    public class EventIdleTimer
    {
        private readonly int _idleTimeoutSeconds;
        private float _idleSeconds;

        public EventIdleTimer(int idleTimeoutSeconds)
        {
            _idleTimeoutSeconds = idleTimeoutSeconds;
        }

        // 起動所要時間を無操作時間に数えないため、ロード完了時点から計り直す
        // Restart the measurement when loading completes so boot time is not counted as idle
        public void Reset()
        {
            _idleSeconds = 0f;
        }

        // 押しっぱなしは操作とみなさない。キーアップが失われた個体で無操作復帰が二度と起きなくなるため
        // A sustained hold is not activity: with a lost key-up the kiosk would never return to its initial state
        public bool AdvanceAndCheckTimeout(bool hasInputChanged, float deltaSeconds)
        {
            if (hasInputChanged)
            {
                _idleSeconds = 0f;
                return false;
            }

            _idleSeconds += deltaSeconds;
            return _idleTimeoutSeconds <= _idleSeconds;
        }
    }
}

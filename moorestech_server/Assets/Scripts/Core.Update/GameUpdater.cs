namespace Core.Update
{
    public static class GameUpdater
    {
        // tick設定（1tick = 50ms = 1/20秒）
        // Tick settings (1 tick = 50ms = 1/20 second)
        public const int TicksPerSecond = 20;
        public const double SecondsPerTick = 1d / TicksPerSecond;

        // 起動からの累積tick数。サーバ側の経過時間はすべてこの値の差分で測る
        // Cumulative tick count since startup; all server-side elapsed time is measured as a difference of this
        public static ulong CurrentTick { get; private set; }

        // セーブのロード時にだけ呼ぶ。tickスレッド開始前に呼ぶこと（走行中に巻き戻すと計測中の差分が負に回り込む）
        // Called only when loading a save, before the tick thread starts (rewinding mid-run wraps in-flight differences)
        public static void RestoreCurrentTick(ulong tick)
        {
            CurrentTick = tick;
        }

        // 1tickの処理順はServer.BootのMasterTickUpdaterが1ファイルで明示する。ここが持つのはその本体1本だけで、外から追加する口は無い
        // The order inside one tick is declared in Server.Boot's MasterTickUpdater; this holds only that one body, with no way to append from outside
        private static IMasterTick _masterTick = NoMasterTick.Instance;

        public static void Update()
        {
            CurrentTick++;
            _masterTick.Tick();
        }

        // サーバー生成時に1回呼ぶ。作り直しでは前のサーバーの本体を置き換え、2本並ぶことは無い
        // Called once when a server is created; re-creating replaces the previous server's body, so two never run side by side
        public static void SetMasterTick(IMasterTick tick)
        {
            _masterTick = tick;
        }

        // サーバーの破棄・作り直しの前に呼び、前のサーバーの本体を外してtickを空回りにする。CurrentTickは巻き戻さない（計測中の差分が負に回り込むため）
        // Called before a server is disposed or re-created; detaches the previous server's body so ticks idle. CurrentTick is never rewound (in-flight differences would wrap)
        public static void ResetUpdate()
        {
            _masterTick = NoMasterTick.Instance;
        }

        // 秒数をtickに変換するユーティリティ（マスターデータの秒数値を変換する用）
        // Utility to convert seconds to ticks (for converting master data values)
        public static uint SecondsToTicks(double seconds)
        {
            // 非数値や無限大、0以下の値は0tickとして扱う
            // Treat NaN, Infinity, and non-positive values as 0 ticks
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0d)
            {
                return 0u;
            }

            var ticksDouble = seconds * TicksPerSecond;

            // 非常に大きい値はuint.MaxValueにクランプ
            // Clamp extremely large values to avoid overflow
            if (ticksDouble >= uint.MaxValue)
            {
                return uint.MaxValue;
            }

            // 正の秒数だが1tick未満の場合は、最低でも1tickとする
            // For positive durations smaller than one tick, ensure at least 1 tick
            var ticks = (uint)ticksDouble;
            return ticks == 0u ? 1u : ticks;
        }

        // tickを秒数に変換するユーティリティ（表示用など）
        // Utility to convert ticks to seconds (for display purposes)
        public static double TicksToSeconds(uint ticks) => ticks * SecondsPerTick;

        // サーバーが無い間のtick本体。進めるのはtick番号だけ
        // The tick body while no server exists; only the tick number advances
        private sealed class NoMasterTick : IMasterTick
        {
            public static readonly NoMasterTick Instance = new();

            public void Tick()
            {
            }

            public void RebuildTopologiesIfDirty()
            {
            }
        }

#if UNITY_EDITOR
        public static void UpdateOneTick()
        {
            // テスト用: 1 tickずつ決定論的に進行
            // For testing: advance deterministically by 1 tick
            RunFrames(1);
        }

        // テスト用: 指定tick数だけ進行。テストはtick外で設置するので、本番の「末尾で適用→再構築→次tick」に揃えるため各tickの前に網を作り直す
        // For testing: advance by specified tick count; tests place blocks outside a tick, so networks are rebuilt before each tick to match production's apply-at-end, rebuild, next tick
        public static void RunFrames(uint frameCount)
        {
            for (var i = 0u; i < frameCount; i++)
            {
                _masterTick.RebuildTopologiesIfDirty();
                CurrentTick++;
                _masterTick.Tick();
            }
        }
#endif
    }
}

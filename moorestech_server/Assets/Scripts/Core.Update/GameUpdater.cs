using System;
using UniRx;
using Unity.Profiling;

namespace Core.Update
{
    public static class GameUpdater
    {
        // tick設定（1tick = 50ms = 1/20秒）
        // Tick settings (1 tick = 50ms = 1/20 second)
        public const int TicksPerSecond = 20;
        public const double SecondsPerTick = 1d / TicksPerSecond;

        public static IObservable<Unit> UpdateObservable => _updateSubject;
        private static Subject<Unit> _updateSubject = new();

        public static IObservable<Unit> LateUpdateObservable => _lateUpdateSubject;
        private static Subject<Unit> _lateUpdateSubject = new();

        public static void Update()
        {
            // Updateの実行
            // Execute Update
            ExecuteUpdate();

            // LateUpdateの実行
            // Execute LateUpdate
            ExecuteLateUpdate();

            #region Internal

            void ExecuteUpdate()
            {
                var updateProfilerMask = new ProfilerMarker("Update");
                updateProfilerMask.Begin();
                _updateSubject.OnNext(Unit.Default);
                updateProfilerMask.End();
            }

            void ExecuteLateUpdate()
            {
                var lateUpdateProfilerMask = new ProfilerMarker("LateUpdate");
                lateUpdateProfilerMask.Begin();
                _lateUpdateSubject.OnNext(Unit.Default);
                lateUpdateProfilerMask.End();
            }

            #endregion
        }

        public static void ResetUpdate()
        {
            _updateSubject = new Subject<Unit>();
            _lateUpdateSubject = new Subject<Unit>();
        }

        public static void Dispose()
        {
            _updateSubject.Dispose();
            _lateUpdateSubject.Dispose();
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

#if UNITY_EDITOR
        public static void UpdateOneTick()
        {
            // テスト用: 1 tickずつ決定論的に進行
            // For testing: advance deterministically by 1 tick
            AdvanceTicks(1);
        }

        // テスト用: 指定tick数だけ進行
        // For testing: advance by specified tick count
        public static void AdvanceTicks(uint tickCount)
        {
            for (var i = 0u; i < tickCount; i++)
            {
                _updateSubject.OnNext(Unit.Default);
                _lateUpdateSubject.OnNext(Unit.Default);
            }
        }
#endif
    }
}

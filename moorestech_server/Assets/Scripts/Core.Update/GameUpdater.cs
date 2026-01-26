using System;
using System.Threading;
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

        private static DateTime _lastUpdateTime = DateTime.Now;
        private static double _tickRemainderSeconds;

        // 今回のフレームで進行するtick数
        // Ticks elapsed in the current frame
        public static uint CurrentTickCount { get; private set; }

        // 今回のフレームで進行する秒数（= CurrentTickCount * SecondsPerTick）
        // Elapsed seconds in the current frame (= CurrentTickCount * SecondsPerTick)
        public static double CurrentDeltaSeconds { get; private set; }

        // 廃止予定（互換性のため残す）
        // Deprecated (kept for compatibility)
        [Obsolete("Use CurrentDeltaSeconds instead")]
        public static double UpdateSecondTime => CurrentDeltaSeconds;

        public static void Update()
        {
            // デルタタイムの更新
            // Update delta time
            UpdateDeltaTime();

            // Updateの実行
            // Execute Update
            var updateProfilerMask = new ProfilerMarker("Update");
            updateProfilerMask.Begin();
            _updateSubject.OnNext(Unit.Default);
            updateProfilerMask.End();

            // LateUpdateの実行
            // Execute LateUpdate
            var lateUpdateProfilerMask = new ProfilerMarker("LateUpdate");
            lateUpdateProfilerMask.Begin();
            _lateUpdateSubject.OnNext(Unit.Default);
            lateUpdateProfilerMask.End();
        }

        public static void UpdateDeltaTime()
        {
            var elapsedSeconds = (DateTime.Now - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = DateTime.Now;

            // 秒数をtickに換算（余りは次回に繰り越し）
            // Convert seconds to ticks (remainder carried to next frame)
            var totalSeconds = elapsedSeconds + _tickRemainderSeconds;
            CurrentTickCount = (uint)Math.Max((int)(totalSeconds * TicksPerSecond), 0);
            _tickRemainderSeconds = totalSeconds - CurrentTickCount * SecondsPerTick;

            CurrentDeltaSeconds = CurrentTickCount * SecondsPerTick;
        }

        public static void ResetUpdate()
        {
            _updateSubject = new Subject<Unit>();
            _lateUpdateSubject = new Subject<Unit>();
            CurrentTickCount = 0;
            CurrentDeltaSeconds = 0;
            _tickRemainderSeconds = 0d;
            _lastUpdateTime = DateTime.Now;
        }

        public static void Dispose()
        {
            _updateSubject.Dispose();
            _lateUpdateSubject.Dispose();
        }

#if UNITY_EDITOR
        public static void UpdateWithWait()
        {
            //TODO ゲームループ周りの修正についてはちょっと考えたい
            Update();
            Wait();
        }

        // テスト用: 指定tick数だけ進行
        // For testing: advance by specified tick count
        public static void AdvanceTicks(uint tickCount)
        {
            CurrentTickCount = tickCount;
            CurrentDeltaSeconds = tickCount * SecondsPerTick;

            _updateSubject.OnNext(Unit.Default);
            _lateUpdateSubject.OnNext(Unit.Default);
        }

        // 廃止予定（互換性のため残す）
        // Deprecated (kept for compatibility)
        [Obsolete("Use AdvanceTicks instead")]
        public static void SpecifiedDeltaTimeUpdate(double updateSecondTime)
        {
            // 秒数をtickに換算
            // Convert seconds to ticks
            var tickCount = (uint)Math.Max((int)(updateSecondTime * TicksPerSecond), 0);
            AdvanceTicks(tickCount);
        }

        public static void Wait()
        {
            Thread.Sleep(5);
        }
#endif
    }
}

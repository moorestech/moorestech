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

        // 秒数をtickに変換するユーティリティ（マスターデータの秒数値を変換する用）
        // Utility to convert seconds to ticks (for converting master data values)
        public static uint SecondsToTicks(double seconds) => (uint)Math.Max((int)(seconds * TicksPerSecond), 0);

        // tickを秒数に変換するユーティリティ（表示用など）
        // Utility to convert ticks to seconds (for display purposes)
        public static double TicksToSeconds(uint ticks) => ticks * SecondsPerTick;

        public static void Update()
        {
            // デルタタイムの更新
            // Update delta time
            UpdateDeltaTime();

            // Updateの実行
            // Execute Update
            ExecuteUpdate();

            // LateUpdateの実行
            // Execute LateUpdate
            ExecuteLateUpdate();
            
            #region Internal
            
            void UpdateDeltaTime()
            {
                var elapsedSeconds = (DateTime.Now - _lastUpdateTime).TotalSeconds;
                _lastUpdateTime = DateTime.Now;
                
                // 秒数をtickに換算（余りは次回に繰り越し）
                // Convert seconds to ticks (remainder carried to next frame)
                var totalSeconds = elapsedSeconds + _tickRemainderSeconds;
                CurrentTickCount = (uint)Math.Max((int)(totalSeconds * TicksPerSecond), 0);
                _tickRemainderSeconds = totalSeconds - CurrentTickCount * SecondsPerTick;
            }
            
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
            CurrentTickCount = 0;
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
            // テスト用: 1 tickずつ決定論的に進行
            // For testing: advance deterministically by 1 tick
            AdvanceTicks(1);
            Wait();
        }

        // テスト用: 指定tick数だけ進行
        // For testing: advance by specified tick count
        public static void AdvanceTicks(uint tickCount)
        {
            CurrentTickCount = tickCount;

            _updateSubject.OnNext(Unit.Default);
            _lateUpdateSubject.OnNext(Unit.Default);
        }

        public static void Wait()
        {
            Thread.Sleep(5);
        }
#endif
    }
}

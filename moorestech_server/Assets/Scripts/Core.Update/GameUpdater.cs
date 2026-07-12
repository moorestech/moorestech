using System;
using System.Collections.Generic;
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
        public static List<Action> AdditionalUpdates = new();

        // tick中に予約された生成・破壊とトポロジ変更を一括反映するtick末尾処理
        // Tick-end handlers applying reserved create/destroy and topology changes in one batch
        public static List<Action> TickEndUpdates = new();

        public static void Update()
        {
            // AdditionalUpdatesのやつ。テストでも呼ぶので関数化
            // Run additional tick updates first.
            ExecuteAdditionalUpdates();

            // Updateの実行
            // Execute Update
            ExecuteUpdate();

            // tick末尾処理の実行（セーブ可能な安定状態はこの後にのみ成立する）
            // Execute tick-end handlers (a save-stable state exists only after this)
            ExecuteTickEndUpdates();

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

            // 追加tick更新とtick末尾処理も初期化する
            // Reset additional tick updates and tick-end handlers as well.
            AdditionalUpdates.Clear();
            TickEndUpdates.Clear();
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

        private static void ExecuteAdditionalUpdates()
        {
            foreach (var additionalUpdate in AdditionalUpdates)
            {
                additionalUpdate();
            }
        }

        private static void ExecuteTickEndUpdates()
        {
            foreach (var tickEndUpdate in TickEndUpdates)
            {
                tickEndUpdate();
            }
        }

#if UNITY_EDITOR
        public static void UpdateOneTick()
        {
            // テスト用: 1 tickずつ決定論的に進行
            // For testing: advance deterministically by 1 tick
            RunFrames(1);
        }

        // テスト用: 指定tick数だけ進行
        // For testing: advance by specified tick count
        public static void RunFrames(uint frameCount)
        {
            for (var i = 0u; i < frameCount; i++)
            {
                ExecuteAdditionalUpdates();
                _updateSubject.OnNext(Unit.Default);
                ExecuteTickEndUpdates();
                _lateUpdateSubject.OnNext(Unit.Default);
            }
        }
#endif
    }
}

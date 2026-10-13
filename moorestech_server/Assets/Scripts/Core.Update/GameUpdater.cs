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

        // 起動からの累積tick数。サーバ側の経過時間はすべてこの値の差分で測る
        // Cumulative tick count since startup; all server-side elapsed time is measured as a difference of this
        public static ulong CurrentTick { get; private set; }

        // セーブのロード時にだけ呼ぶ。tickスレッド開始前に呼ぶこと（走行中に巻き戻すと計測中の差分が負に回り込む）
        // Called only when loading a save, before the tick thread starts (rewinding mid-run wraps in-flight differences)
        public static void RestoreCurrentTick(ulong tick)
        {
            CurrentTick = tick;
        }

        // 1tickの処理順はServer.BootのMasterTickUpdaterが1ファイルで明示する。ここには本体を1本登録するだけで、購読や末尾用のリストは持たない
        // The order inside one tick is declared in Server.Boot's MasterTickUpdater; only that one entry is registered here, with no subscription or tick-end lists
        public static List<Action> AdditionalUpdates = new();

        public static void Update()
        {
            CurrentTick++;
            ExecuteAdditionalUpdates();
        }

        // CurrentTickはここでも巻き戻さない。巻き戻すと計測中の差分が負に回り込むため
        // CurrentTick is never rewound here; rewinding would wrap in-flight elapsed differences
        public static void ResetUpdate()
        {
            AdditionalUpdates.Clear();
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
                CurrentTick++;
                ExecuteAdditionalUpdates();
            }
        }
#endif
    }
}

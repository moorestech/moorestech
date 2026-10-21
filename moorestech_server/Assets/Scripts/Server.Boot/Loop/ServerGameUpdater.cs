using System;
using System.Diagnostics;
using System.Threading;
using Core.Update;
using Unity.Profiling;

namespace Server.Boot.Loop
{
    public static class ServerGameUpdater
    {
        private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(GameUpdater.SecondsPerTick);

        // 遅れはこのtick数ぶんまで間を空けずに回して取り戻し、超えたら期限を今へ合わせ直す
        // Catch up by running back-to-back up to this many ticks of lag; beyond that, re-anchor the deadline to now
        private const int MaxCatchUpTicks = 5;
        private static readonly TimeSpan MaxCatchUpLag = TimeSpan.FromTicks(FrameInterval.Ticks * MaxCatchUpTicks);

        // Sleepは寝過ごすので期限の手前で起き、残りはYieldで詰める
        // Sleep overshoots, so wake this far before the deadline and close the rest with Yield
        private static readonly TimeSpan SpinMargin = TimeSpan.FromMilliseconds(2);

        internal static void StartUpdate(CancellationToken token, ServerThreadActionQueue threadActionQueue)
        {
            var profilerMarker = new ProfilerMarker("GameUpdate");

            // 次tickの開始時刻を絶対期限で持つ。毎回「50ms−処理時間」を数え直すと寝過ごしと重いtickの分だけ遅れが溜まるため
            // Keep the next tick's start as an absolute deadline; recounting "50ms minus work" each time accumulates oversleep and heavy-tick lag
            var stopwatch = Stopwatch.StartNew();
            var nextTickDeadline = stopwatch.Elapsed;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    WaitUntil(nextTickDeadline);

                    profilerMarker.Begin();
                    try
                    {
                        GameUpdater.Update();
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                    }
                    profilerMarker.End();

                    // 期限を1tick進め、取り戻せないほど遅れたら今を起点に刻み直す
                    // Advance the deadline by one tick; if too far behind to recover, restart the cadence from now
                    nextTickDeadline += FrameInterval;
                    var lag = stopwatch.Elapsed - nextTickDeadline;
                    if (MaxCatchUpLag < lag)
                    {
                        UnityEngine.Debug.LogWarning($"[ServerGameUpdater] tickが{lag.TotalMilliseconds:F0}ms遅れたため取り戻さず刻み直します / Tick fell {lag.TotalMilliseconds:F0}ms behind; re-anchoring instead of catching up");
                        nextTickDeadline = stopwatch.Elapsed;
                    }
                }
            }
            finally
            {
                threadActionQueue.Stop();
            }

            #region Internal

            void WaitUntil(TimeSpan deadline)
            {
                var remaining = deadline - stopwatch.Elapsed;
                if (SpinMargin < remaining)
                {
                    Thread.Sleep(remaining - SpinMargin);
                }
                while (stopwatch.Elapsed < deadline)
                {
                    Thread.Yield();
                }
            }

            #endregion
        }
    }
}

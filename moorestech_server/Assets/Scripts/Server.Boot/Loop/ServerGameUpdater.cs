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
        
        
        internal static void StartUpdate(CancellationToken token, ServerThreadActionQueue threadActionQueue)
        {
            var profilerMarker = new ProfilerMarker("GameUpdate");
            
            var stopwatch = new Stopwatch();
            
            try
            {
                while (!token.IsCancellationRequested)
                {
                    profilerMarker.Begin();

                    stopwatch.Restart();

                    try
                    {
                        GameUpdater.Update();
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                    }

                    // 経過時間を測定
                    var remaining = FrameInterval - stopwatch.Elapsed;

                    // 残りフレーム時間だけ待機
                    if (TimeSpan.Zero < remaining)
                    {
                        Thread.Sleep(remaining);
                    }

                    profilerMarker.End();
                }
            }
            finally
            {
                threadActionQueue.Stop();
            }
        }
    }
}

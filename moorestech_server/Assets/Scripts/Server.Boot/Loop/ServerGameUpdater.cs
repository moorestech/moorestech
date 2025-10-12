using System;
using System.Diagnostics;
using System.Threading;
using Core.Update;
using Unity.Profiling;

namespace Server.Boot.Loop
{
    public static class ServerGameUpdater
    {
        public const int FrameIntervalMs = 100;
        private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(FrameIntervalMs);
        
        
        public static void StartUpdate(CancellationToken token)
        {
            var profilerMarker = new ProfilerMarker("GameUpdate");
            
            var stopwatch = new Stopwatch();
            
            while (true)
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
                
                // まだフレーム時間が余っていれば、その分だけ待機
                if (remaining > TimeSpan.Zero)
                {
                    Thread.Sleep(remaining);
                }
                
                profilerMarker.End();
            }
        }
    }
}
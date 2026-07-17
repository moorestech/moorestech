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
        
        
        public static void StartUpdate(CancellationToken token)
        {
            var profilerMarker = new ProfilerMarker("GameUpdate");
            
            var stopwatch = new Stopwatch();

            // キャンセル時はtick境界で停止する（tick途中の状態を残さない安定点終了）
            // Stop at a tick boundary on cancellation so the loop never exits mid-tick
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
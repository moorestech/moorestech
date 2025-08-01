using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Core.Update;

namespace Server.Boot.Loop
{
    public static class ServerGameUpdater
    {
        public const int FrameIntervalMs = 100;
        private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(FrameIntervalMs);
        
        
        public static async Task StartUpdate(CancellationToken token)
        {
            var stopwatch = new Stopwatch();
            
            while (true)
            {
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
                    await Task.Delay(remaining, token);
                
                if (token.IsCancellationRequested) return;
            }
        }
    }
}
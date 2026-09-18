using System;
using Client.PlaytestReceiver.Upload.Attempt;

namespace Client.Tests.PlaytestReceiver
{
    // 本番と同じ回数で待ち時間だけゼロにした再試行表。テストの実時間待ちを消す
    // A retry schedule with the production count but zero waits, so tests do not sleep in real time
    public static class PlaytestNoWaitRetrySchedule
    {
        public static PlaytestUploadRetrySchedule Create()
        {
            var delays = new TimeSpan[PlaytestUploadRetrySchedule.Default.Delays.Count];
            return new PlaytestUploadRetrySchedule(delays);
        }
    }
}

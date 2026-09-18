using System;
using System.Collections.Generic;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    // 一過性の失敗を同一走行内で待って再試行する回数と間隔（ADR 0064）。テストは Immediate で待ちを消す
    // How many times and how long a transient failure is retried within one run (ADR 0064); tests use Immediate to remove the waits
    public sealed class PlaytestUploadRetrySchedule
    {
        public readonly IReadOnlyList<TimeSpan> Delays;

        public PlaytestUploadRetrySchedule(IReadOnlyList<TimeSpan> delays)
        {
            Delays = delays;
        }

        public static readonly PlaytestUploadRetrySchedule Default = new(new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60) });
        public static readonly PlaytestUploadRetrySchedule Immediate = new(new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero });
    }
}

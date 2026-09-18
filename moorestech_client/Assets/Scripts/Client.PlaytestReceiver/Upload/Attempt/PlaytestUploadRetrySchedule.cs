using System;
using System.Collections.Generic;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    // 一過性の失敗を同一走行内で待って再試行する回数と間隔（ADR 0064）
    // How many times and how long a transient failure is retried within one run (ADR 0064)
    public sealed class PlaytestUploadRetrySchedule
    {
        public readonly IReadOnlyList<TimeSpan> Delays;

        public PlaytestUploadRetrySchedule(IReadOnlyList<TimeSpan> delays)
        {
            Delays = delays;
        }

        public static readonly PlaytestUploadRetrySchedule Default = new(new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60) });
    }
}

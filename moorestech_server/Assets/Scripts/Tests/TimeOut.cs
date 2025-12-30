using System;
using Tests.Watchdog;

namespace Tests
{
    /// <summary>
    /// <see cref="TestWatchdog"/> で使うタイムアウト時間を定義します。
    /// Defines the timeout period used by <see cref="TestWatchdog"/>.
    /// </summary>
    public class TimeOut
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(1);
    }
}
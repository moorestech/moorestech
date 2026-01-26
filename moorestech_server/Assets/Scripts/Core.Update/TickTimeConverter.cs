namespace Core.Update
{
    /// <summary>
    /// tick と秒の変換ユーティリティ
    /// Utility for converting between ticks and seconds
    /// </summary>
    public static class TickTimeConverter
    {
        public static uint SecondsToTicks(double seconds)
        {
            return (uint)(seconds * GameUpdater.TicksPerSecond);
        }

        public static double TicksToSeconds(uint ticks)
        {
            return ticks * GameUpdater.SecondsPerTick;
        }
    }
}

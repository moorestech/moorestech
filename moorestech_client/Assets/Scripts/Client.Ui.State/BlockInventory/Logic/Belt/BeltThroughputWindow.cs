namespace Client.Ui.State.BlockInventory.Logic.Belt
{
    /// <summary>
    /// ベルトの搬出統計の窓 1 つ。窓が埋まったか（計測済み）／まだか（計測中）を sealed 派生で分け、計測中は個数を持たない（判断記録 D-5）
    /// One belt exit-statistics window; sealed subtypes split measured from still-measuring, and a measuring window carries no count (D-5)
    /// </summary>
    public abstract class BeltThroughputWindow
    {
        public int Ticks { get; }

        // 速度 × 窓 ÷ 360。速度によっては端数になる
        // Speed × window ÷ 360; fractional at some speeds
        public double MaxCount { get; }

        private protected BeltThroughputWindow(int ticks, double maxCount)
        {
            Ticks = ticks;
            MaxCount = maxCount;
        }

        public static BeltThroughputWindow Measured(int ticks, int exitCount, double maxCount)
        {
            return new MeasuredBeltWindow(ticks, exitCount, maxCount);
        }

        public static BeltThroughputWindow Measuring(int ticks, double maxCount)
        {
            return new MeasuringBeltWindow(ticks, maxCount);
        }
    }

    public sealed class MeasuredBeltWindow : BeltThroughputWindow
    {
        public int ExitCount { get; }

        internal MeasuredBeltWindow(int ticks, int exitCount, double maxCount) : base(ticks, maxCount)
        {
            ExitCount = exitCount;
        }
    }

    public sealed class MeasuringBeltWindow : BeltThroughputWindow
    {
        internal MeasuringBeltWindow(int ticks, double maxCount) : base(ticks, maxCount)
        {
        }
    }
}

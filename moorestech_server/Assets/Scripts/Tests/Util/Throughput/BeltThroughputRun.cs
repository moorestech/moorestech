using System.Collections.Generic;

namespace Tests.Util.Throughput
{
    // 1回の計測結果。窓の間に各受けチェストへ入った個数と、その合計
    // One measurement: how many items each sink received during the window, and their sum
    public sealed class BeltThroughputRun
    {
        public readonly int Window;
        public readonly int StartTick;
        public readonly IReadOnlyList<int> SinkCounts;
        public readonly int Total;

        public BeltThroughputRun(int window, int startTick, IReadOnlyList<int> sinkCounts)
        {
            Window = window;
            StartTick = startTick;
            SinkCounts = sinkCounts;
            var total = 0;
            foreach (var count in sinkCounts) total += count;
            Total = total;
        }
    }
}

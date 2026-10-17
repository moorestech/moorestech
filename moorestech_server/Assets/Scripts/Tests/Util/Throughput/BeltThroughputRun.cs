using System.Collections.Generic;

namespace Tests.Util.Throughput
{
    // 1回の計測結果。窓の間に各受けチェストへ入った個数とその合計、各供給チェストから出た個数
    // One measurement: how many items each sink received during the window, their sum, and how many each source emitted
    public sealed class BeltThroughputRun
    {
        public readonly int Window;
        public readonly int StartTick;
        public readonly IReadOnlyList<int> SinkCounts;
        public readonly IReadOnlyList<int> SourceCounts;
        public readonly int Total;

        public BeltThroughputRun(int window, int startTick, IReadOnlyList<int> sinkCounts, IReadOnlyList<int> sourceCounts)
        {
            Window = window;
            StartTick = startTick;
            SinkCounts = sinkCounts;
            SourceCounts = sourceCounts;
            var total = 0;
            foreach (var count in sinkCounts) total += count;
            Total = total;
        }
    }
}

using System.Collections.Generic;

namespace Tests.Util.Throughput
{
    // 1回の計測結果。窓の間に各供給チェストから出た個数(=受け入れられた速さ)、各受けチェストへ入った個数、ベルト上の増減、監視 segment の搬出数
    // One measurement: how many items each source emitted during the window (the accepted rate), how many each sink received, the change of items on the belts, and the exits of each watched segment
    public sealed class BeltThroughputRun
    {
        public readonly int Window;
        public readonly int StartTick;
        public readonly IReadOnlyList<int> SinkCounts;
        public readonly IReadOnlyList<int> SourceCounts;
        public readonly int OnBeltsDelta;
        public readonly IReadOnlyList<int> WatchedExits;
        public readonly int SinkTotal;
        public readonly int SourceTotal;

        public BeltThroughputRun(int window, int startTick, IReadOnlyList<int> sinkCounts, IReadOnlyList<int> sourceCounts, int onBeltsDelta, IReadOnlyList<int> watchedExits)
        {
            Window = window;
            StartTick = startTick;
            SinkCounts = sinkCounts;
            SourceCounts = sourceCounts;
            OnBeltsDelta = onBeltsDelta;
            WatchedExits = watchedExits;
            foreach (var count in sinkCounts) SinkTotal += count;
            foreach (var count in sourceCounts) SourceTotal += count;
        }
    }
}

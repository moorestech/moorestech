using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.Util.Throughput
{
    // スループット(合計の厳密一致)と分配の公平さ(±1)の assert。公平さは round robin の位相ぶんだけ緩める(ユーザー裁定 2026-10-18「軽くでいい」)
    // Asserts for throughput (exact total) and distribution fairness (±1); fairness allows the round-robin phase slack (user ruling 2026-10-18 "keep it light")
    public static class BeltThroughputAssert
    {
        // スループット100%: 供給が窓の間に理論値どおり受け入れられ(1個も止まらず)、出た分は受けとベルト上の増減で全部説明できる(1個も消えない)
        // 100% throughput: the sources were accepted at the theoretical rate (nothing stalled) and every emitted item is accounted for by the sinks plus the change on the belts (nothing vanished)
        public static void Throughput(int expectedSourceTotal, BeltThroughputRun run, string context)
        {
            Assert.AreEqual(expectedSourceTotal, run.SourceTotal, $"{context}: source emissions over {run.Window} ticks from tick {run.StartTick}; sources={string.Join(",", run.SourceCounts)} sinks={string.Join(",", run.SinkCounts)} onBeltsDelta={run.OnBeltsDelta}");
            Assert.AreEqual(run.SourceTotal - run.SinkTotal, run.OnBeltsDelta, $"{context}: conservation; sources={string.Join(",", run.SourceCounts)} sinks={string.Join(",", run.SinkCounts)} onBeltsDelta={run.OnBeltsDelta}");
        }

        // 監視 segment のスループット100%: 窓(周期の倍数)の間の搬出数が W/P にちょうど一致。位相に依らない
        // 100% throughput of the watched segments: exits over the window (a multiple of the period) equal W/P exactly, regardless of phase
        public static void WatchedSegmentsAtFullThroughput(BeltThroughputRun run, int periodTicks, string context)
        {
            Assert.AreEqual(0, run.Window % periodTicks, $"{context}: window {run.Window} must be a multiple of the period {periodTicks}");
            for (var i = 0; i < run.WatchedExits.Count; i++)
                Assert.AreEqual(run.Window / periodTicks, run.WatchedExits[i], $"{context}: watched segment #{i} exits over {run.Window} ticks; watched={string.Join(",", run.WatchedExits)}");
        }

        public static void EachWithinOne(IReadOnlyList<int> counts, int expectedEach, string context)
        {
            for (var i = 0; i < counts.Count; i++)
                Assert.That(counts[i], Is.InRange(expectedEach - 1, expectedEach + 1), $"{context}: share #{i} of {string.Join(",", counts)} expected about {expectedEach}");
        }

        // どの供給も窓の間に1個は出ている(永久に飢えない)
        // Every source emitted at least one item during the window (none starves forever)
        public static void NoSourceStarves(BeltThroughputRun run, string context)
        {
            for (var i = 0; i < run.SourceCounts.Count; i++)
                Assert.Less(0, run.SourceCounts[i], $"{context}: source #{i} starved; sources={string.Join(",", run.SourceCounts)}");
        }
    }
}

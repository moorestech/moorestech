using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.Util.Throughput
{
    // スループット(合計の厳密一致)と分配の公平さ(±1)の assert。公平さは round robin の位相ぶんだけ緩める(ユーザー裁定 2026-10-18「軽くでいい」)
    // Asserts for throughput (exact total) and distribution fairness (±1); fairness allows the round-robin phase slack (user ruling 2026-10-18 "keep it light")
    public static class BeltThroughputAssert
    {
        public static void Total(int expected, BeltThroughputRun run, string context)
        {
            Assert.AreEqual(expected, run.Total, $"{context}: total over {run.Window} ticks from tick {run.StartTick}; sinks={string.Join(",", run.SinkCounts)} sources={string.Join(",", run.SourceCounts)}");
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

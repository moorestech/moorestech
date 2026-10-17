using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.Throughput;
using UnityEngine;
using static Tests.Util.Throughput.BeltThroughputLayout;

namespace Tests.CombinedTest.Core.Transport.Throughput
{
    // 詰まりと独立性(ADR 0084、シナリオ案 §2 A13・A14)。詰まり中は個数が保存され、解消後は次の着荷から100%に戻る。別系統は影響を受けない
    // Blocking and independence (ADR 0084, scenario §2 A13, A14): items are conserved while blocked, 100% resumes from the next arrival, and another line is unaffected
    public class BeltThroughputBlockingTest
    {
        private const int Period = 20;
        private const int Length = 20;
        private const int BlockedTicks = 600;

        // A13: 受けを詰まらせて満杯にし、戻す
        // A13: block the sink until the line fills, then unblock
        [Test]
        public void A13_BlockThenRelease([Values] BeltPlacementOrder order)
        {
            var sink = new Vector3Int(0, 0, Length);
            var session = StraightLine(new BeltThroughputScenario(), 0, sink).Start(order);
            session.TickUntilSteady();
            var before = session.Measure(session.DefaultWindow);
            BeltThroughputAssert.Throughput(Expected(before), before, "A13 before blocking");

            // 詰まり中: 供給から出た分はすべてベルト上に残る(消えない)
            // While blocked, everything the source emitted stays on the belts (nothing vanishes)
            session.BlockSink(sink);
            var sourceBefore = session.SourceCounts()[0];
            var onBeltsBefore = session.ItemsOnBelts();
            session.TickMany(BlockedTicks);
            Assert.AreEqual(sourceBefore - session.SourceCounts()[0], session.ItemsOnBelts() - onBeltsBefore, "A13 conservation while blocked");
            Assert.AreEqual(0, session.SinkCounts()[0], "A13 nothing enters the blocked sink");

            session.UnblockSink(sink);
            session.TickUntilSteady();
            var after = session.Measure(session.DefaultWindow);
            BeltThroughputAssert.Throughput(Expected(after), after, "A13 after release");
        }

        // A14: 2系統を離して置き、片方だけ詰まらせても、もう片方は窓の間ずっと100%
        // A14: two lines apart; blocking one leaves the other at 100% for the whole window
        [Test]
        public void A14_IndependentLines([Values] BeltPlacementOrder order)
        {
            var sinkA = new Vector3Int(0, 0, Length);
            var sinkB = new Vector3Int(10, 0, Length);
            var scenario = StraightLine(new BeltThroughputScenario(), 0, sinkA);
            StraightLine(scenario, 10, sinkB);
            var session = scenario.Start(order);
            session.TickUntilSteady();

            session.BlockSink(sinkB);
            var run = session.Measure(session.DefaultWindow);
            Assert.AreEqual(Expected(run), run.SourceCounts[0], $"A14 line A accepted at full rate while B is blocked; sources={string.Join(",", run.SourceCounts)} sinks={string.Join(",", run.SinkCounts)}");
            Assert.AreEqual(run.SourceTotal - run.SinkTotal, run.OnBeltsDelta, "A14 conservation");
            Assert.AreEqual(0, run.SinkCounts[1], "A14 blocked line B receives nothing");
        }

        private static int Expected(BeltThroughputRun run)
        {
            return BeltThroughputWindow.ExpectedTotal(run.Window, Period);
        }

        // 供給(x,0,-1) → 木の歯車ベルト z=0..Length-1 → 1スロットの受け。発電機は先頭マスの東隣
        // Source at (x,0,-1) -> wood gear belt z=0..Length-1 -> one-slot sink; the generator sits east of the first cell
        private static BeltThroughputScenario StraightLine(BeltThroughputScenario scenario, int x, Vector3Int sink)
        {
            scenario.Source(new Vector3Int(x, 0, -1));
            Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(x, 0, 0), BlockDirection.North, Length);
            scenario.OneSlotSink(sink);
            Generator(scenario, new Vector3Int(x + 1, 0, 0));
            return scenario;
        }
    }
}

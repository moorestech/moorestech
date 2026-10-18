using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.Throughput;
using UnityEngine;
using static Tests.Util.Throughput.BeltThroughputLayout;

namespace Tests.CombinedTest.Core.Transport.Throughput
{
    // A16: 輪 + 横入れ合流 + 分岐出口。輪は合流と分岐で切れ、どちらも buffer を持つので、合流→分岐の区間は100%で回る(ADR 0084、シナリオ案 §2 A16 / §3 D2)
    // A16: a ring with a side-input merge and a splitter exit; the ring is cut at the merge and the splitter, both buffered, so the merge-to-splitter run turns at 100% (ADR 0084, scenario §2 A16 / §3 D2)
    // 下流から順に更新する実装は、順序を定義できない輪の切れ目で100%を下回ってここで落ちる(判別器 D2)
    // An implementation that updates downstream first cannot order the ring's seam and falls below 100% here (discriminator D2)
    public class BeltThroughputRingTest
    {
        private const int Period = 20;

        // 5×5 の輪(反時計回り)。(2,0,0) が横入れの合流、(4,0,2) が分岐。分岐の右出力が出口、前方は輪を続け、左(輪の内側)は未接続
        // A 5×5 counter-clockwise ring: (2,0,0) is the side-input merge, (4,0,2) the splitter; its right output exits, the front continues the ring, the left (inside) is unconnected
        [Test]
        public void A16_RingWithMergeAndSplitterExit([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario();
            var belt = ForUnitTestModBlockId.WoodGearBelt;
            Line(scenario, belt, new Vector3Int(0, 0, 0), BlockDirection.East, 4);
            Line(scenario, belt, new Vector3Int(4, 0, 0), BlockDirection.North, 2);
            scenario.Block(ForUnitTestModBlockId.WoodGearBeltSplitter, new Vector3Int(4, 0, 2), BlockDirection.North);
            Line(scenario, belt, new Vector3Int(4, 0, 3), BlockDirection.North, 1);
            Line(scenario, belt, new Vector3Int(4, 0, 4), BlockDirection.West, 4);
            Line(scenario, belt, new Vector3Int(0, 0, 4), BlockDirection.South, 4);

            scenario.Source(new Vector3Int(2, 0, -6));
            Line(scenario, belt, new Vector3Int(2, 0, -5), BlockDirection.North, 5);
            var exit = Line(scenario, belt, new Vector3Int(5, 0, 2), BlockDirection.East, 5);
            scenario.Sink(exit).Watch(new Vector3Int(4, 0, 0));
            Generator(scenario, new Vector3Int(1, 0, 1));

            // 合流→分岐の区間は輪の戻りと横入れを交互に受けて常に満杯で回る。供給は交互の片側ぶん(半分)しか受け入れられない
            // The merge-to-splitter run takes the ring's return and the side input in turn and stays full; the source is accepted only for its alternate half
            // 輪が何周か回って戻りの流れが落ち着くまで待ってから測る
            // Measure only after the ring has turned a few laps and the return flow has settled
            var session = scenario.Start(order);
            session.TickUntilSteady();
            session.TickMany(2000);
            var run = session.Measure(session.DefaultWindow);
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(run, Period, $"A16 merge-to-splitter run order={order}");
            BeltThroughputAssert.StageCountIsConstant("A16 (D2: ring)");
            Assert.AreEqual(run.Window / Period / 2, run.SourceTotal, $"A16 side input gets its alternate share; sources={string.Join(",", run.SourceCounts)} sinks={string.Join(",", run.SinkCounts)}");
            Assert.AreEqual(run.SourceTotal - run.SinkTotal, run.OnBeltsDelta, "A16 conservation");
        }
    }
}

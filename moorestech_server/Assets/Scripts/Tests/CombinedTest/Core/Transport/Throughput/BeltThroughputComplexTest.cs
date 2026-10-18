using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.Throughput;
using UnityEngine;
using static Tests.Util.Throughput.BeltThroughputLayout;

namespace Tests.CombinedTest.Core.Transport.Throughput
{
    // C: 複雑系。期待値は手計算せず、組から作った容量グラフの最大流で出す(ADR 0084、シナリオ案 §2 C)
    // C: complex layouts; the expectation is the max flow of a capacity graph built from the assembly rather than a hand calculation (ADR 0084, scenario §2 C)
    public class BeltThroughputComplexTest
    {
        private const int Period = 20;

        // C1: バランサー形。入力4本を2つの合流で2本にし、2つの分岐で左右に割り、たすき掛けで2つの合流へ戻して出口2本へ
        // C1: a balancer: four inputs merge into two, two splitters divide each left and right, and the halves cross over into two merges feeding two exits
        //   A(back)+B(west) -> M1(0,0,0) -> S1(0,0,3): right -> M3 back, left -> M4 west
        //   C(back)+D(west) -> M2(10,0,0) -> S2(10,0,3): left -> M3 east, right -> M4 east
        //   M3(5,0,6) -> exit east -> sink (8,0,7); M4(4,0,10) -> exit north -> sink (4,0,13)
        [Test]
        public void C1_Balancer([Values] BeltPlacementOrder order)
        {
            // ブロックIDの解決はサーバー起動後(Scenario 生成後)でないとマスタが無い
            // Block ids resolve only after the server boots (after the Scenario is created), when the master is loaded
            var scenario = new BeltThroughputScenario().Window(BeltThroughputWindow.WithSplitter);
            var belt = ForUnitTestModBlockId.WoodGearBelt;
            var splitter = ForUnitTestModBlockId.WoodGearBeltSplitter;

            // 第1段: 合流 M1・M2 とその後ろの分岐 S1・S2
            // Stage 1: merges M1 and M2 with splitters S1 and S2 after them
            Feeder(scenario, belt, new Vector3Int(0, 0, -6), BlockDirection.North, 5);
            Feeder(scenario, belt, new Vector3Int(-6, 0, 0), BlockDirection.East, 5);
            Line(scenario, belt, new Vector3Int(0, 0, 0), BlockDirection.North, 3);
            scenario.Block(splitter, new Vector3Int(0, 0, 3), BlockDirection.North);
            Feeder(scenario, belt, new Vector3Int(10, 0, -6), BlockDirection.North, 5);
            Feeder(scenario, belt, new Vector3Int(4, 0, 0), BlockDirection.East, 5);
            Line(scenario, belt, new Vector3Int(10, 0, 0), BlockDirection.North, 3);
            scenario.Block(splitter, new Vector3Int(10, 0, 3), BlockDirection.North);

            // たすき掛け: S1 右 → M3 の後ろ、S2 左 → M3 の東側。S1 左 → M4 の西側、S2 右 → M4 の東側
            // Cross-over: S1 right -> M3 back, S2 left -> M3 east; S1 left -> M4 west, S2 right -> M4 east
            Line(scenario, belt, new Vector3Int(1, 0, 3), BlockDirection.East, 4);
            Line(scenario, belt, new Vector3Int(5, 0, 3), BlockDirection.North, 3);
            scenario.Block(belt, new Vector3Int(9, 0, 3), BlockDirection.West);
            Line(scenario, belt, new Vector3Int(8, 0, 3), BlockDirection.North, 3);
            Line(scenario, belt, new Vector3Int(8, 0, 6), BlockDirection.West, 3);
            scenario.Block(belt, new Vector3Int(5, 0, 6), BlockDirection.North);
            Line(scenario, belt, new Vector3Int(-1, 0, 3), BlockDirection.North, 7);
            Line(scenario, belt, new Vector3Int(-1, 0, 10), BlockDirection.East, 5);
            Line(scenario, belt, new Vector3Int(11, 0, 3), BlockDirection.North, 7);
            Line(scenario, belt, new Vector3Int(11, 0, 10), BlockDirection.West, 7);
            scenario.Block(belt, new Vector3Int(4, 0, 10), BlockDirection.North);

            // 出口: M3 は東へ、M4 は北へ
            // Exits: M3 turns east, M4 goes north
            var exit3 = Line(scenario, belt, new Vector3Int(5, 0, 7), BlockDirection.East, 3);
            var exit4 = Line(scenario, belt, new Vector3Int(4, 0, 11), BlockDirection.North, 2);
            scenario.Sink(exit3).Sink(exit4);
            scenario.Watch(new Vector3Int(0, 0, 1)).Watch(new Vector3Int(10, 0, 1)).Watch(new Vector3Int(6, 0, 7)).Watch(new Vector3Int(4, 0, 11));
            Generator(scenario, new Vector3Int(-1, 0, -1));

            var session = scenario.Start(order);
            session.TickUntilSteady();
            var run = session.Measure(session.DefaultWindow);

            // 最大流 = 合流 M3・M4 の容量の和 = 2/20 個/tick。手計算と最大流の両方で固定する
            // Max flow = the capacities of merges M3 and M4 = 2/20 items per tick; pinned by both the hand value and the max flow
            var expected = BeltCapacityMaxFlow.ExpectedTotal(BeltWorldTestUtil.Assembly(), run.Window);
            Assert.AreEqual(2 * run.Window / Period, expected, "C1 max flow equals two merge exits");
            BeltThroughputAssert.Throughput(expected, run, $"C1 order={order}");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(run, Period, $"C1 merge exits order={order}");
            BeltThroughputAssert.EachWithinOne(run.SinkCounts, expected / 2, $"C1 exits order={order}");
            BeltThroughputAssert.EachWithinOne(run.SourceCounts, expected / 4, $"C1 inputs order={order}");
            BeltThroughputAssert.StageCountIsConstant("C1");
        }

        // 供給チェストとそこから向きの先へ伸びる列
        // A source chest and the run leading away from it along the facing
        private static void Feeder(BeltThroughputScenario scenario, BlockId belt, Vector3Int source, BlockDirection direction, int length)
        {
            scenario.Source(source);
            Line(scenario, belt, source + Step(direction), direction, length);
        }
    }
}

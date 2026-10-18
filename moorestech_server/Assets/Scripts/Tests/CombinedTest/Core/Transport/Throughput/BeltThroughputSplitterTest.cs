using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.Throughput;
using UnityEngine;
using static Tests.Util.Throughput.BeltThroughputLayout;

namespace Tests.CombinedTest.Core.Transport.Throughput
{
    // 分岐マスのスループット(ADR 0084、シナリオ案 §2 A3・A7・A8・A17)。木の歯車ベルト、周期20tick。合計は入力の W/20、分配は ±1
    // Splitter-cell throughput (ADR 0084, scenario §2 A3, A7, A8, A17): wood gear belts, 20-tick period; the total equals the input's W/20, shares within ±1
    public class BeltThroughputSplitterTest
    {
        private const int Period = 20;
        private static readonly Vector3Int Splitter = Vector3Int.zero;

        // A7: 3方向分岐、3出口とも無限受け
        // A7: a three-way splitter with all three exits open
        [Test]
        public void A7_ThreeWayAllOpen([Values] BeltPlacementOrder order)
        {
            var scenario = NewScenario();
            BackFeeder(scenario);
            scenario.Block(ForUnitTestModBlockId.WoodGearBeltSplitter, Splitter, BlockDirection.North);
            Branch(scenario, Splitter, BlockDirection.North, 5, false);
            Branch(scenario, Splitter, BlockDirection.East, 5, false);
            Branch(scenario, Splitter, BlockDirection.West, 5, false);
            Generator(scenario, new Vector3Int(1, 0, 1));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(Expected(run), run, "A7");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(run, Period, "A7 feeder");
            BeltThroughputAssert.EachWithinOne(run.SinkCounts, Expected(run) / 3, "A7 shares");
        }

        // A8a: 菱形。左右出力を迂回させて合流で戻す。前方出力は未接続
        // A8a: a diamond; the side outputs detour and rejoin at a merge, the front output unconnected
        [Test]
        public void A8a_Diamond([Values] BeltPlacementOrder order)
        {
            var scenario = NewScenario();
            BackFeeder(scenario);
            scenario.Block(ForUnitTestModBlockId.WoodGearBeltSplitter, Splitter, BlockDirection.North);
            foreach (var x in new[] { 1, -1 })
            {
                Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(x, 0, 0), BlockDirection.North, 3);
                scenario.Block(ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(x, 0, 3), x == 1 ? BlockDirection.West : BlockDirection.East);
            }
            var exit = Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 3), BlockDirection.North, 6);
            scenario.Sink(exit).Watch(new Vector3Int(0, 0, 6));
            Generator(scenario, new Vector3Int(2, 0, 0));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(Expected(run), run, "A8a");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(run, Period, "A8a feeder and exit");
        }

        // A8b: 分岐マスの直後が合流マス。前方は直結、左右は1マスで折り返して横から入る
        // A8b: a merge cell right after the splitter; the front links directly, the sides fold back in one cell and enter from the side
        [Test]
        public void A8b_SplitterThenMergeAdjacent([Values] BeltPlacementOrder order)
        {
            var scenario = NewScenario();
            BackFeeder(scenario);
            scenario.Block(ForUnitTestModBlockId.WoodGearBeltSplitter, Splitter, BlockDirection.North);
            foreach (var x in new[] { 1, -1 })
            {
                scenario.Block(ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(x, 0, 0), BlockDirection.North);
                scenario.Block(ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(x, 0, 1), x == 1 ? BlockDirection.West : BlockDirection.East);
            }
            var exit = Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 1), BlockDirection.North, 6);
            scenario.Sink(exit).Watch(new Vector3Int(0, 0, 4));
            Generator(scenario, new Vector3Int(2, 0, 0));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(Expected(run), run, "A8b");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(run, Period, "A8b feeder and exit");
        }

        // A3: 分岐マス100個の直列(残り2出力は未接続)。分岐bufferを100回通る
        // A3: one hundred splitters in series (the other two outputs unconnected); items pass through one hundred branch buffers
        // 設置順は正順だけ(ユーザー裁定 2026-10-18)。A3 の狙いは buffer 100 段と段階数一定で、設置順の差は A7/A8/A17 が見る
        // Forward placement only (user ruling 2026-10-18); A3 is about one hundred buffers and a constant stage count, and placement order is covered by A7/A8/A17
        [Test]
        public void A3_HundredSplittersInSeries()
        {
            const BeltPlacementOrder order = BeltPlacementOrder.Forward;
            var scenario = NewScenario().Source(new Vector3Int(0, 0, -1));
            var exit = Line(scenario, ForUnitTestModBlockId.WoodGearBeltSplitter, Splitter, BlockDirection.North, 100);
            scenario.Sink(exit).Watch(new Vector3Int(0, 0, 50));
            Generator(scenario, new Vector3Int(1, 0, 0));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(Expected(run), run, "A3");
            BeltThroughputAssert.StageCountIsConstant("A3 (D1: 100 segments in series)");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(run, Period, "A3 middle splitter");
        }

        // A17: 分岐マス5連。接続出力数と詰まりの組み合わせで、合計は常に入力の100%
        // A17: five splitters in a row; across output counts and blocked sinks the total always equals the input's 100%
        private static System.Collections.Generic.IEnumerable<TestCaseData> A17Cases()
        {
            foreach (var (outputs, blockedSides) in new[] { (1, 0), (2, 0), (2, 1), (3, 0), (3, 1), (3, 2) })
            foreach (BeltPlacementOrder order in System.Enum.GetValues(typeof(BeltPlacementOrder)))
                yield return new TestCaseData(outputs, blockedSides, order).SetName($"A17_FiveSplitters_Outputs{outputs}_Blocked{blockedSides}_{order}");
        }

        [TestCaseSource(nameof(A17Cases))]
        public void A17_FiveSplittersInARow(int outputs, int blockedSides, BeltPlacementOrder order)
        {
            var scenario = NewScenario();
            BackFeeder(scenario);
            for (var z = 0; z < 5; z++)
            {
                var splitter = new Vector3Int(0, 0, z);
                scenario.Block(ForUnitTestModBlockId.WoodGearBeltSplitter, splitter, BlockDirection.North);
                if (2 <= outputs) Branch(scenario, splitter, BlockDirection.East, 3, 1 <= blockedSides);
                if (3 <= outputs) Branch(scenario, splitter, BlockDirection.West, 3, 2 <= blockedSides);
            }
            var exit = Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 5), BlockDirection.North, 5);
            scenario.Sink(exit);
            Generator(scenario, outputs == 3 ? new Vector3Int(1, 0, -1) : new Vector3Int(-1, 0, 0));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(Expected(run), run, $"A17 outputs={outputs} blocked={blockedSides} order={order}");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(run, Period, $"A17 feeder outputs={outputs} blocked={blockedSides}");
        }

        private static BeltThroughputScenario NewScenario()
        {
            return new BeltThroughputScenario();
        }

        private static int Expected(BeltThroughputRun run)
        {
            return BeltThroughputWindow.ExpectedTotal(run.Window, Period);
        }

        // 供給直後の列(最初の分岐マスまで)は常に100%でなければならないので監視する
        // The feeder run up to the first splitter must always run at 100%, so it is watched
        private static void BackFeeder(BeltThroughputScenario scenario)
        {
            scenario.Source(new Vector3Int(0, 0, -6));
            Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, -5), BlockDirection.North, 5);
            scenario.Watch(new Vector3Int(0, 0, -3));
        }

        // 分岐マスの1出力の先に length マスの列と受けを置く。blocked なら最初から詰まった1スロット受け
        // Lays a length-cell line and a sink after one splitter output; blocked gives a one-slot sink that is full from the start
        private static void Branch(BeltThroughputScenario scenario, Vector3Int splitter, BlockDirection direction, int length, bool blocked)
        {
            var end = Line(scenario, ForUnitTestModBlockId.WoodGearBelt, splitter + Step(direction), direction, length);
            if (blocked) scenario.BlockedSink(end);
            else scenario.Sink(end);
        }
    }
}

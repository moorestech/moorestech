using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.Throughput;
using UnityEngine;
using static Tests.Util.Throughput.BeltThroughputLayout;

namespace Tests.CombinedTest.Core.Transport.Throughput
{
    // 合流マスのスループット(ADR 0084、シナリオ案 §2 A4〜A6・A18)。木の歯車ベルト、周期20tick、出口側が律速で合計は W/20
    // Merge-cell throughput (ADR 0084, scenario §2 A4-A6, A18): wood gear belts, 20-tick period, the exit bounds the total at W/20
    public class BeltThroughputMergeTest
    {
        private const int Period = 20;
        private static readonly Vector3Int Merge = Vector3Int.zero;

        // A4a: 左右から入る T 字。合流マスの後ろには何も無い
        // A4a: a T fed from both sides; nothing behind the merge cell
        [Test]
        public void A4a_TFromBothSides([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario();
            SideFeeder(scenario, BlockDirection.East);
            SideFeeder(scenario, BlockDirection.West);
            MergeAndExit(scenario, 10);
            Generator(scenario, new Vector3Int(0, 0, -1));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(Expected(run), run, "A4a");
            BeltThroughputAssert.EachWithinOne(run.SourceCounts, Expected(run) / 2, "A4a shares");
        }

        // A4b: 直進1本 + 横入れ1本の90度合流
        // A4b: one straight input plus one side input at 90 degrees
        [Test]
        public void A4b_StraightPlusSide([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario();
            BackFeeder(scenario);
            SideFeeder(scenario, BlockDirection.East);
            MergeAndExit(scenario, 10);
            Generator(scenario, new Vector3Int(1, 0, 0));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(Expected(run), run, "A4b");
            BeltThroughputAssert.EachWithinOne(run.SourceCounts, Expected(run) / 2, "A4b shares");
        }

        // A5a: ベルト1本 + 機械(チェスト)が横から直接搬出する合流。機械入力は内部segmentを経由する
        // A5a: one belt plus a machine (chest) emitting directly from the side; the machine input goes through an internal segment
        [Test]
        public void A5a_BeltPlusMachine([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario();
            BackFeeder(scenario);
            scenario.Source(new Vector3Int(-1, 0, 0));
            MergeAndExit(scenario, 10);
            Generator(scenario, new Vector3Int(1, 0, 0));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(Expected(run), run, "A5a");
            BeltThroughputAssert.EachWithinOne(run.SourceCounts, Expected(run) / 2, "A5a shares");
        }

        // B7(旧 A5b): 左右から入る合流の前方が直接受けチェスト。合流bufferは段階1で回収し同じtickの段階3で機械へ渡すので、1マスを19tickで通過し理論最大(144)を超える
        // B7 (was A5b): a merge fed from both sides whose front is the sink itself; the buffer collects in stage 1 and hands to the machine in stage 3 of the same tick, so a cell takes 19 ticks and the count exceeds the nominal maximum (144)
        // 特性値としてピン止め。変えるPRは理由をADRに書く(出所: ユーザー裁定 2026-10-18「F2は許容します」)
        // Pinned as a characteristic value; a PR that changes it records the reason in an ADR (user ruling 2026-10-18 "F2 is accepted")
        [Test]
        public void B7_MergeIntoMachineRunsNineteenTicksPerItem([Values] BeltPlacementOrder order)
        {
            const int pinnedTotal = 152;
            var scenario = new BeltThroughputScenario();
            SideFeeder(scenario, BlockDirection.East);
            SideFeeder(scenario, BlockDirection.West);
            scenario.Block(ForUnitTestModBlockId.WoodGearBelt, Merge, BlockDirection.North).Sink(new Vector3Int(0, 0, 1));
            Generator(scenario, new Vector3Int(0, 0, -1));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(pinnedTotal, run, "B7 pinned (2888 ticks / 19)");
        }

        // A6: 機械(チェスト)2台が左右から入る合流。ベルト入力なし
        // A6: two machines (chests) feeding from both sides; no belt input
        [Test]
        public void A6_TwoMachines([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario().Source(new Vector3Int(-1, 0, 0)).Source(new Vector3Int(1, 0, 0));
            MergeAndExit(scenario, 10);
            Generator(scenario, new Vector3Int(0, 0, -1));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(Expected(run), run, "A6");
            BeltThroughputAssert.EachWithinOne(run.SourceCounts, Expected(run) / 2, "A6 shares");
        }

        // A18: 合流マス5連。本線も横入れ5本も密。出口は本線の100%で、どの供給も飢えない
        // A18: five merge cells in a row; the main line and all five side inputs are dense; the exit runs at 100% and no source starves
        [Test]
        public void A18_FiveMergesInARow([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario();
            BackFeeder(scenario);
            for (var z = 0; z < 5; z++)
            {
                scenario.Source(new Vector3Int(-6, 0, z));
                Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(-5, 0, z), BlockDirection.East, 5);
                scenario.Block(ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, z), BlockDirection.North);
            }
            var exit = Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 5), BlockDirection.North, 10);
            scenario.Sink(exit);
            Generator(scenario, new Vector3Int(1, 0, 0));

            var run = scenario.Run(order);
            BeltThroughputAssert.Throughput(Expected(run), run, "A18");
            BeltThroughputAssert.NoSourceStarves(run, "A18");
        }

        private static int Expected(BeltThroughputRun run)
        {
            return BeltThroughputWindow.ExpectedTotal(run.Window, Period);
        }

        // 合流マスへ横から入る5マスの列。East は西側から、West は東側から入る
        // A five-cell line entering the merge cell from the side; East enters from the west, West from the east
        private static void SideFeeder(BeltThroughputScenario scenario, BlockDirection direction)
        {
            var start = Merge - Step(direction) * 5;
            scenario.Source(start - Step(direction));
            Line(scenario, ForUnitTestModBlockId.WoodGearBelt, start, direction, 5);
        }

        private static void BackFeeder(BeltThroughputScenario scenario)
        {
            scenario.Source(new Vector3Int(0, 0, -6));
            Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, -5), BlockDirection.North, 5);
        }

        private static void MergeAndExit(BeltThroughputScenario scenario, int exitLength)
        {
            var exit = Line(scenario, ForUnitTestModBlockId.WoodGearBelt, Merge, BlockDirection.North, exitLength);
            scenario.Sink(exit);
        }
    }
}

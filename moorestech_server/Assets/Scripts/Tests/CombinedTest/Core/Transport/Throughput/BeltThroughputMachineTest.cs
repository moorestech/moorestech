using Core.Master;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.Throughput;
using UnityEngine;
using static Tests.Util.Throughput.BeltThroughputLayout;

namespace Tests.CombinedTest.Core.Transport.Throughput
{
    // A9: ベルト → 素通し機(レシピ「A 1個 → A 1個、1 tick」) → ベルト。機械の受理・処理・搬出でベルトに空白が出ない(ADR 0084、シナリオ案 §2 A9)
    // A9: belt -> pass-through machine (recipe "1 A -> 1 A, one tick") -> belt; the machine's accept, process and emit leave no bubble on the belts (ADR 0084, scenario §2 A9)
    [Category("CiShardServerBeltThroughput")]
    public class BeltThroughputMachineTest
    {
        private const int Period = 20;
        private static readonly System.Guid PassThroughRecipeGuid = new("7b3e1a10-0006-4000-8000-000000000001");

        [Test]
        public void A9_PassThroughMachine([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario().Source(new Vector3Int(0, 0, -1));
            Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 0), BlockDirection.North, 5);
            scenario.Block(ForUnitTestModBlockId.GearMachine, new Vector3Int(0, 0, 5), BlockDirection.North);
            var exit = Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 6), BlockDirection.North, 5);
            scenario.Sink(exit).Watch(new Vector3Int(0, 0, 2)).Watch(new Vector3Int(0, 0, 8));
            Generator(scenario, new Vector3Int(1, 0, 0));

            // 機械はレシピを選ぶまで何も受け付けない(ADR 0042)ので、素通しレシピを選んでから流す
            // A machine accepts nothing until a recipe is selected (ADR 0042), so the pass-through recipe is selected before the flow starts
            var session = scenario.Start(order);
            var machine = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(0, 0, 5));
            MachineRecipeSelectTestUtil.SelectRecipe(machine, MasterHolder.MachineRecipesMaster.GetRecipeElement(PassThroughRecipeGuid));
            session.TickUntilSteady();

            // 機械のスロットに居るアイテムはベルト上の個数に含まれないので、保存則は機械の前後の segment の搬出数で見る
            // Items sitting in the machine's slots are not counted on the belts, so conservation is read from the segments before and after the machine
            var run = session.Measure(session.DefaultWindow);
            var expected = BeltThroughputWindow.ExpectedTotal(run.Window, Period);
            Assert.AreEqual(expected, run.SourceTotal, $"A9 source emissions order={order}; sources={string.Join(",", run.SourceCounts)} sinks={string.Join(",", run.SinkCounts)}");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(run, Period, $"A9 before and after the machine order={order}");
        }
    }
}

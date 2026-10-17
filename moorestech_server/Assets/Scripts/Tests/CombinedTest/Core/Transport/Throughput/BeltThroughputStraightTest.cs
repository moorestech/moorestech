using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.Throughput;
using UnityEngine;

namespace Tests.CombinedTest.Core.Transport.Throughput
{
    // A1: 直線100マス。供給→ベルト→受けで、窓の個数が周期からの理論値にちょうど一致する(ADR 0084、シナリオ案 §2 A1)
    // A1: a 100-cell straight line; source -> belt -> sink, and the window count equals the period-derived maximum exactly (ADR 0084, scenario §2 A1)
    public class BeltThroughputStraightTest
    {
        private const int Length = 100;

        [Test]
        public void A1_WoodGear([Values] BeltPlacementOrder order)
        {
            AssertStraight(ForUnitTestModBlockId.WoodGearBelt, 20, true, order);
        }

        [Test]
        public void A1_IronGear([Values] BeltPlacementOrder order)
        {
            AssertStraight(ForUnitTestModBlockId.IronGearBelt, 9, true, order);
        }

        [Test]
        public void A1_Electric([Values] BeltPlacementOrder order)
        {
            AssertStraight(ForUnitTestModBlockId.ElectricBelt, 3, false, order);
        }

        [Test]
        public void A1_Fast([Values] BeltPlacementOrder order)
        {
            AssertStraight(ForUnitTestModBlockId.FastBelt, 2, false, order);
        }

        // A10: 上り1段・下り1段を含む直線。坂の継ぎ目でも周期どおり
        // A10: a line with one up and one down slope; the slope seams keep the period
        [Test]
        public void A10_UpAndDownSlope([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario().Source(new Vector3Int(0, 0, -1));
            BeltThroughputLayout.Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 0), BlockDirection.North, 5);
            scenario.Block(ForUnitTestModBlockId.WoodGearBeltUp, new Vector3Int(0, 0, 5), BlockDirection.North);
            BeltThroughputLayout.Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 1, 6), BlockDirection.North, 5);
            scenario.Block(ForUnitTestModBlockId.WoodGearBeltDown, new Vector3Int(0, 0, 11), BlockDirection.North);
            var exit = BeltThroughputLayout.Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 12), BlockDirection.North, 5);
            scenario.Sink(exit);
            BeltThroughputLayout.Generator(scenario, new Vector3Int(1, 0, 0));

            var run = scenario.Run(order);
            BeltThroughputAssert.Total(BeltThroughputWindow.ExpectedTotal(run.Window, 20), run, $"A10 order={order}");
        }

        // 供給(0,0,-1) → 北向き直線 z=0..99 → 受け(0,0,100)。歯車族は先頭マスの東隣に無限トルクの発電機
        // Source at (0,0,-1) -> north-facing line z=0..99 -> sink at (0,0,100); gear families get an infinite-torque generator east of the first cell
        private static void AssertStraight(BlockId belt, int periodTicks, bool gearDriven, BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario().Source(new Vector3Int(0, 0, -1));
            for (var z = 0; z < Length; z++) scenario.Block(belt, new Vector3Int(0, 0, z), BlockDirection.North);
            scenario.Sink(new Vector3Int(0, 0, Length));
            if (gearDriven) scenario.Block(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.North);

            var run = scenario.Run(order);
            Assert.AreEqual(BeltThroughputWindow.ExpectedTotal(run.Window, periodTicks), run.Total, $"order={order} start={run.StartTick}");
        }
    }
}

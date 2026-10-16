using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Core.Transport.BeltDiffReplayTestUtil;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core.Transport
{
    // 歯車ベルトはマスタ速度(GearBeltConveyor=36)×(回転数/基準回転数)で搬送し、回転数0では止まる
    // A gear belt transports at master speed (GearBeltConveyor = 36) × (RPM / base RPM) and stops at zero RPM
    public class GearBeltConveyorTest
    {
        private static readonly ItemId ItemA = new(2);

        // 3マス・速度36: 進入距離1で出口まで1079。29tick後に残り35、30tick目に渡る。回転数2倍(速度72)なら15tick目
        // Three cells at speed 36: 1079 to the exit after entering at length 1; handed over on tick 30, or on tick 15 at double RPM (speed 72)
        private const int BaseRpmArrivalTick = 30;
        private const int DoubleRpmArrivalTick = 15;

        // 基準回転数で駆動されると、到着tickはマスタ速度どおりになる
        // Driven at the base RPM, the arrival tick follows the master speed
        [Test]
        public void GearBeltAtBaseRpmArrivesOnMasterSpeedTickTest()
        {
            CreateServer();
            var (source, output, belts) = PlaceLine();
            var generator = PlaceGenerator();
            generator.SetGenerateRpm(BaseRpm());
            generator.SetGenerateTorque(1f);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 1));

            GameUpdater.RunFrames(BaseRpmArrivalTick - 1);
            Assert.Less(0f, belts[0].GetComponent<GearEnergyTransformer>().CurrentRpm.AsPrimitive(), "belt should be driven by the generator");
            Assert.AreEqual(36, SegmentAt(Vector3Int.zero).Speed);
            Assert.AreEqual(0, CountOf(output, ItemA));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(output, ItemA));
        }

        // 回転数を2倍にすると速度も2倍になり、半分のtickで到着する
        // Doubling the RPM doubles the speed, so the item arrives in half the ticks
        [Test]
        public void DoubleRpmHalvesTheArrivalTickTest()
        {
            CreateServer();
            var (source, output, _) = PlaceLine();
            var generator = PlaceGenerator();
            generator.SetGenerateRpm(BaseRpm() * 2f);
            generator.SetGenerateTorque(1f);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 1));

            GameUpdater.RunFrames(DoubleRpmArrivalTick - 1);
            Assert.AreEqual(72, SegmentAt(Vector3Int.zero).Speed);
            Assert.AreEqual(0, CountOf(output, ItemA));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(output, ItemA));
        }

        // 回転数0(歯車ネットワーク無し)では速度0で、載ったアイテムは進まない
        // At zero RPM (no gear network) the speed is 0 and the item on the belt does not move
        [Test]
        public void UnpoweredGearBeltDoesNotTransportTest()
        {
            CreateServer();
            var (source, output, belts) = PlaceLine();
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 1));

            GameUpdater.RunFrames(BaseRpmArrivalTick * 2);
            Assert.AreEqual(0f, belts[0].GetComponent<GearEnergyTransformer>().CurrentRpm.AsPrimitive());
            Assert.AreEqual(0, SegmentAt(Vector3Int.zero).Speed);
            Assert.AreEqual(1, ItemsOnSegmentAt(Vector3Int.zero).Length, "the item stays on the belt");
            Assert.AreEqual(0, CountOf(output, ItemA));
        }

        // 回転数の変化はそのtickの差分に速度として載り、複製はサーバーと同じ速度で進み続ける
        // An RPM change rides that tick's diff as a speed, and the replica keeps advancing at the server's speed
        [Test]
        public void RpmChangesReachTheReplicaThroughTickDiffsTest()
        {
            CreateServer();
            var (source, output, _) = PlaceLine();
            var generator = PlaceGenerator();
            generator.SetGenerateRpm(BaseRpm());
            generator.SetGenerateTorque(1f);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 20));
            TickServer();
            var replica = StartReplica();

            var speedChangeTicks = 0;
            for (var tick = 1; tick <= 120; tick++)
            {
                if (tick == 20) generator.SetGenerateRpm(BaseRpm() * 2f);
                if (tick == 50) generator.SetGenerateRpm(0f);
                if (tick == 80) generator.SetGenerateRpm(BaseRpm() / 2f);
                var diff = TickServer();
                if (0 < diff.SpeedChanges.Length) speedChangeTicks++;
                ReplayAndAssert(replica, diff, tick);
            }
            Assert.AreEqual(3, speedChangeTicks, "one speed diff per RPM change");
            Assert.AreEqual(18, SegmentAt(Vector3Int.zero).Speed, "half RPM gives half speed");
            Assert.Less(0, CountOf(output, ItemA));
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static float BaseRpm()
        {
            var param = (GearBeltConveyorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockParam;
            return (float)param.GearConsumption.BaseRpm;
        }

        // 搬入チェスト→歯車ベルト3マス→搬出チェストを北向きに並べる
        // Line up input chest -> three gear belt cells -> output chest facing north
        private static (IOpenableBlockInventoryComponent source, IOpenableBlockInventoryComponent output, IBlock[] belts) PlaceLine()
        {
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            var belts = new IBlock[3];
            for (var z = 0; z < belts.Length; z++) belts[z] = Place(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, z), BlockDirection.North);
            var output = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            return (source, output, belts);
        }

        // 先頭マスの東隣に発電機を置き、歯車でベルト列を駆動する
        // Put a generator east of the head cell and drive the belt line through its gear
        private static SimpleGearGeneratorComponent PlaceGenerator()
        {
            var generator = Place(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East).GetComponent<SimpleGearGeneratorComponent>();
            Place(ForUnitTestModBlockId.SmallGear, new Vector3Int(2, 0, 0), BlockDirection.East);
            return generator;
        }
    }
}

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
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core.Transport
{
    // 歯車ベルトはマスタの固定速度(GearBeltConveyor=32)で搬送し、回転数・トルクは搬送に影響しない
    // A gear belt transports at the fixed master speed (GearBeltConveyor = 32); RPM and torque do not affect transport
    public class GearBeltConveyorTest
    {
        private static readonly ItemId ItemA = new(2);

        // 3マス・速度32: 進入距離1で出口まで767。23tick後に残り31、24tick目に渡る
        // Three cells at speed 32: 767 to the exit after entering at length 1; 31 away after 23 belt ticks, handed over on tick 24
        private const int ExpectedArrivalTick = 24;

        // 歯車ネットワークから基準回転数で駆動されても、到着tickはマスタ速度だけで決まるテスト
        // Even when driven at base RPM by a gear network, the arrival tick depends only on the master speed
        [Test]
        public void PoweredGearBeltArrivesOnMasterSpeedTickTest()
        {
            CreateServer();
            var (source, output, belts) = PlaceLine();
            var generator = Place(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East).GetComponent<SimpleGearGeneratorComponent>();
            Place(ForUnitTestModBlockId.SmallGear, new Vector3Int(2, 0, 0), BlockDirection.East);
            var param = (GearBeltConveyorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockParam;
            generator.SetGenerateRpm((float)param.GearConsumption.BaseRpm);
            generator.SetGenerateTorque(1f);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 1));

            GameUpdater.RunFrames(ExpectedArrivalTick - 1);
            Assert.Less(0f, belts[0].GetComponent<GearEnergyTransformer>().CurrentRpm.AsPrimitive(), "belt should be driven by the generator");
            Assert.AreEqual(0, CountOf(output, ItemA));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(output, ItemA));
        }

        // 回転数0(歯車ネットワーク無し)でも同じtickで搬送されるテスト
        // With zero RPM (no gear network) the item still arrives on the same tick
        [Test]
        public void UnpoweredGearBeltStillTransportsTest()
        {
            CreateServer();
            var (source, output, belts) = PlaceLine();
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 1));

            GameUpdater.RunFrames(ExpectedArrivalTick - 1);
            Assert.AreEqual(0f, belts[0].GetComponent<GearEnergyTransformer>().CurrentRpm.AsPrimitive());
            Assert.AreEqual(0, CountOf(output, ItemA));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(output, ItemA));
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        // 搬入チェスト→歯車ベルト3マス→搬出チェストを北向きに並べる
        // Line up input chest -> three gear belt cells -> output chest facing north
        private static (IBlockInventory source, IBlockInventory output, IBlock[] belts) PlaceLine()
        {
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            var belts = new IBlock[3];
            for (var z = 0; z < belts.Length; z++) belts[z] = Place(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, z), BlockDirection.North);
            var output = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            return (source, output, belts);
        }
    }
}

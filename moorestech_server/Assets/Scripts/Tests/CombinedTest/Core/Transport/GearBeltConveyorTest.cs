using Core.BeltTransport;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using static Tests.CombinedTest.Core.Transport.Segment.BeltWorldTransportTest;

namespace Tests.CombinedTest.Core.Transport
{
    public class GearBeltConveyorTest : IBeltItemDropObserver
    {
        public void OnDropped(BeltCellItemState item, string reason) => Assert.Fail(reason);

        [Test]
        public void OutputTestWhenTorqueSuppliedRateIs100()
        {
            var (transport, belt, output, generator) = CreatePoweredBelt();
            generator.SetGenerateTorque(1f);
            belt.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            for (int tick = 0; tick < 100; tick++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, output.GetItem(0).Count);
            Assert.AreEqual(0, belt.BeltConveyorItems.Count);
        }

        [Test]
        public void NoOutputWhenRpmIsZero()
        {
            var (transport, belt, output, generator) = CreatePoweredBelt();
            generator.SetGenerateTorque(0f);
            belt.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            var initial = transport.Network.CaptureItems();
            for (int tick = 0; tick < 100; tick++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, output.GetItem(0).Count);
            CollectionAssert.AreEqual(initial, transport.Network.CaptureItems());
        }

        [Test]
        public void ItemInsertedWhileStoppedShouldTransportAfterSpeedRecovery()
        {
            var (transport, belt, output, generator) = CreatePoweredBelt();
            var replay = new BeltNetworkReplay(transport.CompletedTick, transport.CaptureSnapshot(), this);
            transport.OnTickCompleted.Subscribe(replay.Apply);
            generator.SetGenerateTorque(0f);
            belt.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            for (int tick = 0; tick < 5; tick++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, belt.BeltConveyorItems.Count);

            // 速度差分を適用しても初回状態から同じCPU位置を再現する。
            // Speed differences preserve deterministic replay from the initial state.
            generator.SetGenerateTorque(1f);
            for (int tick = 0; tick < 100; tick++)
            {
                GameUpdater.UpdateOneTick();
                CollectionAssert.AreEqual(transport.Network.CaptureItems(), replay.Network.CaptureItems());
            }
            Assert.AreEqual(1, output.GetItem(0).Count);
        }

        private static (BeltWorldTransport, VanillaBeltConveyorComponent, IBlockInventory, SimpleGearGeneratorComponent) CreatePoweredBelt()
        {
            var transport = CreateWorld();
            var belt = Place(ForUnitTestModBlockId.GearBeltConveyor, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var output = Place(ForUnitTestModBlockId.ChestId, 0, 1, BlockDirection.North).GetComponent<IBlockInventory>();
            var generator = Place(ForUnitTestModBlockId.SimpleGearGenerator, 1, 0, BlockDirection.East).GetComponent<SimpleGearGeneratorComponent>();
            Place(ForUnitTestModBlockId.SmallGear, 2, 0, BlockDirection.East);
            var param = (GearBeltConveyorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockParam;
            generator.SetGenerateRpm((float)param.GearConsumption.BaseRpm);
            transport.Initialize();
            return (transport, belt, output, generator);
        }
    }
}

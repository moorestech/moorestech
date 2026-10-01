using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Core.Other;
using Tests.Util;
using UniRx;
using static Tests.CombinedTest.Core.Transport.Segment.BeltWorldTransportTest;

namespace Tests.CombinedTest.Core.Transport.Segment
{
    public class BeltSpeedBoundaryTest : IBeltItemDropObserver
    {
        public void OnDropped(BeltCellItemState item, string reason) => Assert.Fail(reason);

        [Test]
        public void StoppingGearPreservesConfiguredBoundaryWithoutStoppingFixedBeltTest()
        {
            var transport = CreateWorld();
            var fixedBelt = Place(ForUnitTestModBlockId.BeltConveyorId, 0, -1, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var gear = Place(ForUnitTestModBlockId.GearBeltConveyor, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var generator = Place(ForUnitTestModBlockId.SimpleGearGenerator, 1, 0, BlockDirection.East).GetComponent<SimpleGearGeneratorComponent>();
            Place(ForUnitTestModBlockId.SmallGear, 2, 0, BlockDirection.East);
            gear.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1));
            generator.SetGenerateRpm(2f);
            generator.SetGenerateTorque(1f);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(fixedBelt.Speed, gear.Speed);
            Assert.AreNotSame(transport.Network.GetPath(fixedBelt.CellId), transport.Network.GetPath(gear.CellId));
            var replay = new BeltNetworkReplay(transport.CompletedTick, transport.CaptureSnapshot(), this);
            transport.OnTickCompleted.Subscribe(replay.Apply);
            fixedBelt.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);

            // 一方だけ停止しても固定速セルとアイテム進行を巻き込まない。
            // Stopping one cell must not freeze the fixed-speed cell or its item.
            generator.SetGenerateTorque(0f);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, transport.Network.GetPath(gear.CellId).Segment.Speed);
            Assert.AreEqual(fixedBelt.Speed, transport.Network.GetPath(fixedBelt.CellId).Segment.Speed);
            Assert.AreNotSame(transport.Network.GetPath(fixedBelt.CellId), transport.Network.GetPath(gear.CellId));
            Assert.Greater(fixedBelt.BeltConveyorItems[0].TotalTicks - fixedBelt.BeltConveyorItems[0].RemainingTicks, 1);
            CollectionAssert.AreEqual(transport.Network.CaptureItems(), replay.Network.CaptureItems());
        }

        [Test]
        public void DifferentGearConfigurationsRepartitionTogetherAfterRecoveryTest()
        {
            var transport = CreateWorld();
            var first = Place(ForUnitTestModBlockId.GearBeltConveyor, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var second = Place(ForUnitTestModBlockId.GearBeltConveyorSplitter, 0, 1, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var generator = Place(ForUnitTestModBlockId.SimpleGearGenerator, 2, 1, BlockDirection.East).GetComponent<SimpleGearGeneratorComponent>();
            Place(ForUnitTestModBlockId.SmallGear, 1, 1, BlockDirection.East);
            Place(ForUnitTestModBlockId.SmallGear, 1, 0, BlockDirection.East);
            generator.SetGenerateRpm(10f);
            generator.SetGenerateTorque(0f);
            first.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            second.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1));
            transport.Initialize();
            Assert.AreNotSame(transport.Network.GetPath(first.CellId), transport.Network.GetPath(second.CellId));
            var replay = new BeltNetworkReplay(transport.CompletedTick, transport.CaptureSnapshot(), this);
            transport.OnTickCompleted.Subscribe(replay.Apply);
            generator.SetGenerateTorque(100f);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(32, first.Speed);
            Assert.AreEqual(128, second.Speed);
            Assert.AreNotSame(transport.Network.GetPath(first.CellId), transport.Network.GetPath(second.CellId));
            Assert.AreEqual(2, transport.Network.CaptureItems().Length);
            CollectionAssert.AreEqual(transport.Network.CaptureItems(), replay.Network.CaptureItems());
        }

        [Test]
        public void OccupiedAndEmptyGearBeltsShareShaftSpeedAndBlackoutTogetherTest()
        {
            var transport = CreateWorld();
            var first = Place(ForUnitTestModBlockId.GearBeltConveyor, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var second = Place(ForUnitTestModBlockId.GearBeltConveyor, 0, 1, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var generator = Place(ForUnitTestModBlockId.SimpleGearGenerator, 1, 0, BlockDirection.East).GetComponent<SimpleGearGeneratorComponent>();
            Place(ForUnitTestModBlockId.SmallGear, 2, 0, BlockDirection.East);
            first.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            generator.SetGenerateRpm(10f);
            generator.SetGenerateTorque(1f);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, first.BeltConveyorItems.Count);
            Assert.AreEqual(0, second.BeltConveyorItems.Count);
            Assert.AreEqual(32, first.Speed);
            Assert.AreEqual(first.Speed, second.Speed);
            Assert.AreSame(transport.Network.GetPath(first.CellId), transport.Network.GetPath(second.CellId));
            var before = transport.Network.CaptureItems();

            // 需要超過の実停止はRPMに反映され、両方の搬送を止める。
            // A real demand blackout is reflected by RPM and stops both conveyors.
            generator.SetGenerateTorque(0.000001f);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, first.Speed);
            Assert.AreEqual(0, second.Speed);
            CollectionAssert.AreEqual(before, transport.Network.CaptureItems());
        }

        [Test]
        public void SplitStackMetadataSurvivesMultipleBeltCellsTest()
        {
            var transport = CreateWorld();
            var first = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var second = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            transport.Initialize();
            var metadata = new TestMeta1();
            var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 2,
                new Dictionary<string, ItemStackMetaData> { { "quality", metadata } });
            Assert.AreEqual(1, first.InsertItem(stack, InsertItemContext.Empty).Count);
            GameUpdater.RunFrames(50);
            Assert.AreSame(metadata, second.GetItem(0).GetMeta("quality"));
        }
    }
}

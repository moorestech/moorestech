using System.Linq;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Core.Transport.Segment.BeltWorldTransportTest;

namespace Tests.CombinedTest.Core.Transport
{
    public class BeltConveyorTest
    {
        [Test]
        public void FullInsertAndChangeConnectorBeltConveyorTest()
        {
            var transport = CreateWorld();
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            transport.Initialize();
            var input = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 2);
            var remainder = belt.InsertItem(input, InsertItemContext.Empty);
            GameUpdater.RunFrames(100);
            Assert.AreEqual(1, belt.InsertItem(remainder, InsertItemContext.Empty).Count);
            IBlockInventory output = null;
            GameUpdater.TickEndUpdates.Add(AddOutput);
            GameUpdater.UpdateOneTick();
            GameUpdater.TickEndUpdates.Remove(AddOutput);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, output.GetItem(0).Count);

            #region Internal
            void AddOutput() => output = Place(ForUnitTestModBlockId.ChestId, 0, 1, BlockDirection.North).GetComponent<IBlockInventory>();
            #endregion
        }

        [Test]
        public void InsertBeltConveyorTest()
        {
            var transport = CreateWorld();
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var target = Place(ForUnitTestModBlockId.ChestId, 0, 1, BlockDirection.North).GetComponent<IBlockInventory>();
            transport.Initialize();
            var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3);
            Assert.AreEqual(2, belt.InsertItem(stack, InsertItemContext.Empty).Count);
            int ticks = (256 + belt.Speed - 1) / belt.Speed;
            GameUpdater.RunFrames((uint)ticks);
            Assert.AreEqual(1, target.GetItem(0).Count);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, target.GetItem(0).Id);
        }

        [Test]
        public void FullInsertBeltConveyorTest()
        {
            var transport = CreateWorld();
            var first = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.North);
            transport.Initialize();
            var remaining = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3);
            // 走行列全体を詰めても入口の占有判定を守る。
            // Preserve entry occupancy when the entire path becomes packed.
            for (int tick = 0; tick < 200; tick++)
            {
                remaining = first.InsertItem(remaining, InsertItemContext.Empty);
                GameUpdater.UpdateOneTick();
            }
            Assert.AreEqual(1, remaining.Count);
            Assert.AreEqual(2, transport.Network.CaptureItems().Length);
        }

        [Test]
        public void Insert2ItemBeltConveyorTest()
        {
            var transport = CreateWorld();
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            transport.Initialize();
            var first = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 2);
            var second = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 2);
            Assert.AreEqual(1, belt.InsertItem(first, InsertItemContext.Empty).Count);
            Assert.AreEqual(2, belt.InsertItem(second, InsertItemContext.Empty).Count);
            Assert.AreEqual(1, belt.BeltConveyorItems.Count);
        }

        [Test]
        public void GearBeltConveyorSplitterDistributesToTwoChestsTest()
        {
            var transport = CreateWorld();
            Place(ForUnitTestModBlockId.GearBeltConveyorSplitter, 0, 0, BlockDirection.North);
            var source = Place(ForUnitTestModBlockId.ChestId, 0, -1, BlockDirection.North).GetComponent<IBlockInventory>();
            var front = Place(ForUnitTestModBlockId.ChestId, 0, 1, BlockDirection.North).GetComponent<IBlockInventory>();
            var side = Place(ForUnitTestModBlockId.ChestId, -1, 0, BlockDirection.North).GetComponent<IBlockInventory>();
            Place(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, 1, 0, BlockDirection.East);
            Place(ForUnitTestModBlockId.SmallGear, 2, 0, BlockDirection.East);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 10));
            for (int tick = 0; tick < 2000; tick++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(5, front.GetItem(0).Count);
            Assert.AreEqual(5, side.GetItem(0).Count);
        }
    }
}

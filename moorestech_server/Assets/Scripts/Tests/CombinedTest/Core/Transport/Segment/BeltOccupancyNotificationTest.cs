using Core.BeltTransport;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.Block.Blocks.Gear;
using Mooresmaster.Model.BlocksModule;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Core.Transport.Segment.BeltWorldTransportTest;

namespace Tests.CombinedTest.Core.Transport.Segment
{
    public class BeltOccupancyNotificationTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SetItemNotifiesOnlyChangedCountTest(bool bindFirst)
        {
            var transport = CreateWorld();
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            if (bindFirst) transport.Initialize();
            int notifications = 0;
            belt.OnItemsChanged.Subscribe(_ => notifications++);
            belt.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            Assert.AreEqual(1, notifications);
            var second = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1);
            belt.SetItem(0, second);
            Assert.AreEqual(second.ItemInstanceId, belt.GetItem(0).ItemInstanceId);
            Assert.AreEqual(1, notifications, "A replacement with the same count is silent.");
            belt.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            Assert.AreEqual(2, notifications);
            belt.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            Assert.AreEqual(2, notifications);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GearTorqueChangesImmediatelyOnPendingOrBoundInsertTest(bool bindFirst)
        {
            var transport = CreateWorld();
            var block = Place(ForUnitTestModBlockId.SmallGearBeltConveyor, 0, 0, BlockDirection.North);
            var belt = block.GetComponent<VanillaBeltConveyorComponent>();
            var gear = block.GetComponent<GearBeltConveyorComponent>();
            var consumption = ((GearBeltConveyorBlockParam)block.BlockMasterElement.BlockParam).GearConsumption;
            var rpm = new RPM((float)consumption.BaseRpm);
            float full = GearConsumptionCalculator.CalcRequiredTorque(consumption, rpm).AsPrimitive();
            if (bindFirst) transport.Initialize();
            int notifications = 0;
            belt.OnItemsChanged.Subscribe(_ => notifications++);
            Assert.AreEqual(full * consumption.IdlePowerRate, gear.GetRequiredTorque(rpm, true).AsPrimitive(), 0.0001f);
            Assert.AreEqual(0, belt.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty).Count);
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(full, gear.GetRequiredTorque(rpm, true).AsPrimitive(), 0.0001f);
            belt.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1));
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(full, gear.GetRequiredTorque(rpm, true).AsPrimitive(), 0.0001f);
            belt.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            Assert.AreEqual(2, notifications);
            Assert.AreEqual(full * consumption.IdlePowerRate, gear.GetRequiredTorque(rpm, true).AsPrimitive(), 0.0001f);
        }

        [Test]
        public void InsertBindAndPhysicalCrossingsKeepNotificationBoundariesTest()
        {
            var transport = CreateWorld();
            var first = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var second = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            first.SetTicksOfItemEnterToExit(4); second.SetTicksOfItemEnterToExit(4);
            int firstChanges = 0, secondChanges = 0;
            first.OnItemsChanged.Subscribe(_ => firstChanges++);
            second.OnItemsChanged.Subscribe(_ => secondChanges++);
            Assert.AreEqual(0, first.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty).Count);
            Assert.AreEqual(1, firstChanges, "Pending insertion pushes immediately.");
            transport.Initialize();
            Assert.AreEqual(2, firstChanges, "Initial binding publishes restored occupancy.");
            GameUpdater.RunFrames(3);
            Assert.AreEqual(2, firstChanges); Assert.AreEqual(0, secondChanges);
            bool tickEndObserved = false;
            GameUpdater.TickEndUpdates.Add(ObserveCrossing);
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(tickEndObserved);
            GameUpdater.RunFrames(20);
            Assert.AreEqual(3, firstChanges); Assert.AreEqual(1, secondChanges);

            #region Internal
            void ObserveCrossing()
            {
                // Advance後かつtopology確定前に搬送の変更が見える。
                // Transport changes are visible after Advance and before topology commit.
                Assert.AreEqual(3, firstChanges); Assert.AreEqual(1, secondChanges);
                tickEndObserved = true;
            }
            #endregion
        }

        [Test]
        public void TopologyCommitBindsPendingAndPreservesSurvivorCountTest()
        {
            var transport = CreateWorld();
            var survivor = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            survivor.SetTicksOfItemEnterToExit(uint.MaxValue);
            survivor.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            transport.Initialize();
            int survivorChanges = 0, addedChanges = 0;
            survivor.OnItemsChanged.Subscribe(_ => survivorChanges++);
            var added = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            added.SetTicksOfItemEnterToExit(uint.MaxValue);
            added.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1));
            added.OnItemsChanged.Subscribe(_ => addedChanges++);
            transport.OnTickCompleted.Subscribe(_ => Assert.AreEqual(0, survivorChanges));
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, addedChanges, "New pending occupancy publishes on committed topology.");
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 1), BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, survivorChanges);
            Assert.AreEqual(1, transport.CaptureCommittedSnapshot().Snapshot.Items.Length);
        }
    }
}

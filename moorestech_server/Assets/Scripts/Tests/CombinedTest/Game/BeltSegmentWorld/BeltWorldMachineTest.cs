using System.Collections.Generic;
using Game.Block.Interface.Component;
using Core.Item.Interface;
using System.Linq;
using Core.Master;
using Game.BeltSegment;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
namespace Tests.CombinedTest.Game.BeltSegmentWorld
{
    public class BeltWorldMachineTest
    {
        [Test]
        public void RealChestsTransferCountAndRuntimePayloadWithoutPower()
        {
            var f = new BeltWorldFixture();
            var source = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.back, BlockDirection.North).GetComponent<VanillaChestComponent>();
            var belt = f.Add(ForUnitTestModBlockId.GearBeltConveyor, Vector3Int.zero, BlockDirection.North).GetComponent<SegmentBeltComponent>();
            var destination = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.forward, BlockDirection.North).GetComponent<VanillaChestComponent>();
            var marker = new TransportMetadata();
            var original = ServerContext.ItemStackFactory.Create(new ItemId(1), 3).SetMeta("transport-test", marker);
            source.SetItem(0, original);
            f.Tick(1);
            Assert.AreEqual(2, source.GetItem(0).Count);
            var payload = belt.GetItem(0);
            var guid = f.Belts.CaptureCell(belt).RunningItem.TransportGuid;
            Assert.AreSame(marker, payload.GetMeta("transport-test"));
            Assert.AreNotEqual(original.ItemInstanceId, payload.ItemInstanceId, "SubItem creates a new stack instance while preserving metadata.");
            f.Tick(16);
            Assert.AreEqual(1, destination.InventoryItems.Sum(i => i.Count));
            Assert.AreEqual(3, source.InventoryItems.Sum(i => i.Count) + destination.InventoryItems.Sum(i => i.Count) + BeltWorldFixture.Count(f.Snapshot()));
            Assert.IsFalse(f.Snapshot().Simulation.Segments.Any(s => s.Items.Any(i => i.Item.Guid == guid)));
            f.Tick(40); Assert.AreEqual(3, destination.InventoryItems.Sum(i => i.Count));
        }
        [Test]
        public void OnlyReadyLowerPriorityOfThreeMachineInputsWinsMerge()
        {
            var f = new BeltWorldFixture(); var belt = f.Belt(Vector3Int.zero, BlockDirection.North);
            var left = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.left, BlockDirection.East).GetComponent<VanillaChestComponent>();
            f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.back, BlockDirection.North);
            var right = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.right, BlockDirection.West).GetComponent<VanillaChestComponent>();
            right.SetItem(0, ServerContext.ItemStackFactory.Create(new ItemId(1), 1));
            f.Tick(1);
            Assert.AreEqual(0, right.GetItem(0).Count); Assert.AreEqual(1, belt.GetItem(0).Count);
            Assert.AreEqual(BeltDirection.Right, f.Belts.CaptureCell(belt).RunningItem.Entry);
            Assert.AreEqual(0, left.InventoryItems.Sum(i => i.Count));
        }
        [Test]
        public void BlockedReceiverKeepsPayloadAndCount()
        {
            var f = new BeltWorldFixture(); var belt = f.Belt(Vector3Int.zero, BlockDirection.North);
            var chest = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.forward, BlockDirection.North).GetComponent<VanillaChestComponent>();
            for (int i = 0; i < chest.GetSlotSize(); i++) chest.SetItem(i, new ItemId(2), 50);
            f.Seed(belt, 1); var original = f.Belts.CaptureCell(belt).RunningItem.TransportGuid;
            f.Tick(40);
            Assert.AreEqual(original, f.Belts.CaptureCell(belt).RunningItem.TransportGuid);
            Assert.AreEqual(1, BeltWorldFixture.Count(f.Snapshot()));
        }
        [Test]
        public void OneSourceHasTwoDistinctInputPortsAndOneFrameRecordsBoth()
        {
            var f = new BeltWorldFixture();
            var chest = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North).GetComponent<VanillaChestComponent>();
            f.Belt(Vector3Int.forward, BlockDirection.North); f.Belt(Vector3Int.right, BlockDirection.East);
            chest.SetItem(0, new ItemId(1), 2); f.Tick(1);
            Assert.AreEqual(2, f.Snapshot().Simulation.Inputs.Length);
            Assert.AreEqual(2, BeltWorldFixture.Count(f.Snapshot())); Assert.AreEqual(0, chest.GetItem(0).Count);
        }
        [TestCase(false)] [TestCase(true)]
        public void GearLoadIsConstantForEmptyRunningAndBufferWithAndWithoutEnergy(bool powered)
        {
            var f = new BeltWorldFixture();
            var block = f.Add(ForUnitTestModBlockId.GearBeltConveyor, Vector3Int.zero, BlockDirection.North);
            var belt = block.GetComponent<SegmentBeltComponent>(); var gear = block.GetComponent<GearBeltConveyorComponent>();
            var demand = gear.GetRequiredTorque(new global::Game.Gear.Common.RPM(10), true);
            f.Belt(Vector3Int.left, BlockDirection.East); f.Belt(Vector3Int.back, BlockDirection.North);
            if (powered) f.Add(ForUnitTestModBlockId.SimpleGearGenerator, Vector3Int.right, BlockDirection.North);
            f.Seed(belt, 1); f.Tick(1);
            Assert.AreEqual(demand, gear.GetRequiredTorque(new global::Game.Gear.Common.RPM(10), true));
            f.Tick(16); Assert.IsNotNull(f.Belts.CaptureCell(belt).BufferedItem);
            Assert.AreEqual(demand, gear.GetRequiredTorque(new global::Game.Gear.Common.RPM(10), true));
            if (powered) Assert.Greater(gear.CurrentRpm.AsPrimitive(), 0);
            else Assert.AreEqual(0, gear.CurrentRpm.AsPrimitive());
        }
        [Test]
        public void MachineInputAndModuleSlotsDoNotMakeItsOutputReady()
        {
            var f = new BeltWorldFixture();
            var inventory = f.Add(ForUnitTestModBlockId.MachineId, Vector3Int.zero, BlockDirection.North)
                .GetComponent<global::Game.Block.Blocks.Machine.Inventory.VanillaMachineBlockInventoryComponent>();
            inventory.SetItem(0, new ItemId(1), 1);
            inventory.SetItem(5, new ItemId(2), 1);
            Assert.IsFalse(inventory.HasOutputItem());
            inventory.SetItem(2, new ItemId(1), 1);
            Assert.IsTrue(inventory.HasOutputItem());
        }
        [TestCase(false, false)][TestCase(false, true)][TestCase(true, false)][TestCase(true, true)]
        public void InputOnlyGeneratorsExposeUnavailableOutputWithoutBreakingRebuild(bool gear, bool connected)
        {
            var f = new BeltWorldFixture();
            var source = f.Add(gear ? ForUnitTestModBlockId.FuelGearGeneratorId : ForUnitTestModBlockId.GeneratorId, new Vector3Int(20, 0, 20), BlockDirection.North);
            var targetBlock = f.Add(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(20, 0, 30), BlockDirection.North);
            var target = targetBlock.GetComponent<SegmentBeltComponent>();
            var output = source.GetComponent<IBlockOutputAvailability>();
            Assert.IsFalse(output.HasOutputItem());
            if (connected)
            {
                // modで指定できる出力辺をconnectorの確定集合へ与え、実topologyを構築する。
                // Supply a mod-configurable output edge to the settled connector set and build real topology.
                var connector = source.GetComponent<IBlockConnectorComponent<IBlockInventory>>();
                ((Dictionary<IBlockInventory, ConnectedInfo>)connector.ConnectedTargets).Add(target,
                    new ConnectedInfo(null, null, targetBlock, targetBlock.BlockPositionInfo.OriginalPos));
            }
            f.Tick(1);
            Assert.AreEqual(connected ? 1 : 0, f.Snapshot().Simulation.Inputs.Length);
            Assert.AreEqual(0, target.GetItem(0).Count); Assert.IsFalse(output.HasOutputItem());
        }
        private sealed class TransportMetadata : ItemStackMetaData
        {
            public override bool Equals(ItemStackMetaData target) => ReferenceEquals(this, target);
        }
    }
}

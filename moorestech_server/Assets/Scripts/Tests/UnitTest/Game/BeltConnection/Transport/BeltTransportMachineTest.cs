using Core.Master;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Transport
{
    public class BeltTransportMachineTest
    {
        private static readonly ItemId ItemA = new(1);
        private static readonly ItemId ItemB = new(2);

        [Test]
        public void MachineToStraightBeltToChestArrivesOnExactTick()
        {
            var world = NewWorld();
            InstallMachinePorts(new[] { Vector3Int.forward }, new[] { Vector3Int.back });
            var machine = Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var head = Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.North);
            var chest = Inventory(Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            var assembly = Assemble(world);
            var segment = assembly.Segments[SegmentIndexAt(assembly, Vector3Int.zero)];

            // 進入距離1で入るので出口まで767。速度6で127tick後に残り5、128tick目に渡る
            // Entering at length 1 leaves 767 to the exit; at speed 6 it is 5 away after 127 ticks and handed over on tick 128
            Assert.IsTrue(Push(assembly, head, machine, ItemA));
            Assert.AreEqual(767, segment.CaptureItems()[0].DistanceToExit);
            Tick(assembly, 127);
            Assert.AreEqual(0, TotalCount(chest), "chest is still empty after 127 ticks");
            Assert.AreEqual(5, segment.CaptureItems()[0].DistanceToExit);

            Tick(assembly, 1);
            Assert.AreEqual(1, TotalCount(chest), "chest holds exactly one item after 128 ticks");
            Assert.AreEqual(1, CountOf(chest, ItemA));
            Assert.IsEmpty(segment.CaptureItems());
        }

        [Test]
        public void RejectedItemParksAtExitUntilChestHasRoom()
        {
            var world = NewWorld();
            InstallMachinePorts(new[] { Vector3Int.forward }, new[] { Vector3Int.back });
            var machine = Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var head = Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.North);
            var chest = Inventory(Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            FillWith(chest, ItemB);
            var assembly = Assemble(world);
            var segment = assembly.Segments[SegmentIndexAt(assembly, Vector3Int.zero)];

            // 満杯のチェストに拒否され、アイテムは出口ちょうどで止まる
            // Rejected by the full chest, the item stops exactly at the exit
            Assert.IsTrue(Push(assembly, head, machine, ItemA));
            Tick(assembly, 200);
            var items = segment.CaptureItems();
            Assert.AreEqual(1, items.Length);
            Assert.AreEqual(0, items[0].DistanceToExit);
            Assert.AreEqual(ItemA, items[0].Item.ItemId);
            Assert.AreEqual(0, CountOf(chest, ItemA));

            // 1スロット空けると次のtickで渡る
            // Freeing one slot hands it over on the next tick
            chest.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            Tick(assembly, 1);
            Assert.AreEqual(1, CountOf(chest, ItemA));
            Assert.IsEmpty(segment.CaptureItems());
        }

        [Test]
        public void MachineIntoMergeGoesThroughInternalSegment()
        {
            var world = NewWorld();
            InstallMachinePorts(new[] { Vector3Int.right }, new[] { Vector3Int.back });
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
            var mergeBelt = Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.North);
            var chest = Inventory(Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            var machine = Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1), BlockDirection.North);
            var assembly = Assemble(world);
            var inner = assembly.Segments[InternalSegmentIndex(assembly)];
            var merge = assembly.Segments[SegmentIndexAt(assembly, new Vector3Int(0, 0, 1))];

            // 1マスの内部segmentは1個で満杯になり、2個目の押し込みを拒否する
            // The one-cell internal segment is full with one item and rejects a second push
            Assert.IsTrue(Push(assembly, mergeBelt, machine, ItemA));
            Assert.AreEqual(255, inner.CaptureItems()[0].DistanceToExit);
            Assert.IsFalse(Push(assembly, mergeBelt, machine, ItemA), "internal segment is full");

            // 速度128で1tick後に残り127、2tick目に合流へ進入距離1で渡る
            // At speed 128 it is 127 away after one tick and enters the merge at length 1 on tick 2
            Tick(assembly, 1);
            Assert.AreEqual(127, inner.CaptureItems()[0].DistanceToExit);
            Assert.IsFalse(Push(assembly, mergeBelt, machine, ItemA), "internal segment is still occupied");
            Tick(assembly, 1);
            Assert.IsEmpty(inner.CaptureItems());
            var mergeItems = merge.CaptureItems();
            Assert.AreEqual(1, mergeItems.Length);
            Assert.AreEqual(255, mergeItems[0].DistanceToExit);
            Assert.IsTrue(Push(assembly, mergeBelt, machine, ItemA), "internal segment accepts again once emptied");

            // 2個ともチェストへ届く
            // Both items reach the chest
            for (var tick = 0; tick < 400 && TotalCount(chest) < 2; tick++) assembly.Simulation.Tick();
            Assert.AreEqual(2, CountOf(chest, ItemA));
        }
    }
}

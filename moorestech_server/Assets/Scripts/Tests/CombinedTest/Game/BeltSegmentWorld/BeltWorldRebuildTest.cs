using Game.Block.Interface.Extension;
using Tests.Module.TestMod;
using Game.Block.Blocks.Chest;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.BeltSegment;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using UnityEngine;
namespace Tests.CombinedTest.Game.BeltSegmentWorld
{
    public class BeltWorldRebuildTest
    {
        [Test]
        public void D13Reprojects256And32ProgressWithinOwnedCells()
        {
            var f = new BeltWorldFixture(); var upstream = f.Belt(Vector3Int.zero, BlockDirection.North);
            var downstream = f.Belt(Vector3Int.forward, BlockDirection.North);
            var items = new BeltWorldItems();
            var first = items.Create(ServerContext.ItemStackFactory.Create(new ItemId(1), 1), BeltDirection.Back);
            var second = items.Create(ServerContext.ItemStackFactory.Create(new ItemId(2), 1), BeltDirection.Back);
            items.Cells.Add(upstream, new BeltCellSaveState(2, items.Save(first, 256), null));
            items.Cells.Add(downstream, new BeltCellSaveState(0, items.Save(second, 32), null));
            var topology = new BeltTopologyBuilder(new HashSet<SegmentBeltComponent> { upstream, downstream }, ServerContext.WorldBlockDatastore, items);
            var graph = BeltWorldRebuild.Build(topology, items);
            var running = graph.CaptureSnapshot().Segments.Single().Items;
            Assert.AreEqual(224, running[0].DistanceToExit); Assert.AreEqual(480, running[1].DistanceToExit);
            Assert.AreEqual(second.Guid, running[0].Item.Guid); Assert.AreEqual(first.Guid, running[1].Item.Guid);
            Assert.AreEqual(2, items.Cells[upstream].PriorityIndex);
        }
        [Test]
        public void RemovalRefundAndImmediateSaveExcludeRemovedCellWhileSurvivorKeepsIdentity()
        {
            var f = new BeltWorldFixture(); var first = f.Belt(Vector3Int.zero, BlockDirection.North);
            var second = f.Belt(Vector3Int.forward, BlockDirection.North);
            f.Seed(first, 1); f.Seed(second, 2); f.Tick(1);
            var survivor = f.Belts.CaptureCell(first).RunningItem.TransportGuid;
            var removed = f.Belts.CaptureCell(second).RunningItem.TransportGuid;
            var refund = second.GetItem(0); Assert.AreEqual(1, refund.Count);
            ServerContext.WorldBlockDatastore.RemoveBlock(Vector3Int.forward, BlockRemoveReason.ManualRemove);
            string save = f.Save(); StringAssert.DoesNotContain(removed.ToString(), save);
            Assert.AreEqual(survivor, f.Belts.CaptureCell(first).RunningItem.TransportGuid);
            f.Tick(1); Assert.AreEqual(1, BeltWorldFixture.Count(f.Snapshot()));
            var replacement = f.Belt(Vector3Int.forward, BlockDirection.North);
            Assert.AreEqual(0, f.Belts.CaptureCell(replacement).PriorityIndex); Assert.IsNull(f.Belts.CaptureCell(replacement).RunningItem);
        }
        [Test]
        public void FullLoopStopsAndPartialLoopKeepsMoving()
        {
            var f = new BeltWorldFixture(); var belts = f.Loop(false);
            foreach (var belt in belts) f.Seed(belt, 1);
            f.Tick(17); var full = f.Snapshot().Simulation.Segments[0].Items.Select(i => i.DistanceToExit).ToArray();
            f.Tick(20); CollectionAssert.AreEqual(full, f.Snapshot().Simulation.Segments[0].Items.Select(i => i.DistanceToExit).ToArray());
            Assert.AreEqual(4, BeltWorldFixture.Count(f.Snapshot()));
            belts[0].SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            f.Tick(1); var before = f.Snapshot().Simulation.Segments[0].Items.Select(i => i.DistanceToExit).ToArray();
            f.Tick(4); CollectionAssert.AreNotEqual(before, f.Snapshot().Simulation.Segments[0].Items.Select(i => i.DistanceToExit).ToArray());
            Assert.AreEqual(3, BeltWorldFixture.Count(f.Snapshot()));
        }
        [Test]
        public void CapturedSaveIsImmutableAndSharedUntilMutation()
        {
            var f = new BeltWorldFixture(); var belt = f.Belt(Vector3Int.zero, BlockDirection.North); f.Seed(belt, 1); f.Tick(1);
            var captured = f.Belts.CaptureCell(belt); Assert.AreSame(captured, f.Belts.CaptureCell(belt));
            int progress = captured.RunningItem.Progress;
            f.Tick(1); Assert.AreEqual(progress, captured.RunningItem.Progress);
            Assert.AreNotSame(captured, f.Belts.CaptureCell(belt));
            Assert.Greater(f.Belts.CaptureCell(belt).RunningItem.Progress, progress);
        }
        [Test]
        public void D22TwoToOneOutputDropsOnlyBufferAndRetainsRunningIdentityAndCellPriority()
        {
            var f = new BeltWorldFixture();
            var branch = f.Add(ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero, BlockDirection.North).GetComponent<SegmentBeltComponent>();
            var left = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.left, BlockDirection.North).GetComponent<VanillaChestComponent>();
            var right = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.right, BlockDirection.North).GetComponent<VanillaChestComponent>();
            f.Seed(branch, 1); f.Tick(17);
            Assert.AreEqual(1, f.Belts.CaptureCell(branch).PriorityIndex);
            foreach (var chest in new[] { left, right })
                for (int slot = 0; slot < chest.GetSlotSize(); slot++) chest.SetItem(slot, new ItemId(2), 50);
            f.Seed(branch, 1); f.Tick(17);
            var buffered = f.Belts.CaptureCell(branch).BufferedItem.TransportGuid;
            f.Seed(branch, 3); var running = f.Belts.CaptureCell(branch).RunningItem.TransportGuid;
            ServerContext.WorldBlockDatastore.RemoveBlock(Vector3Int.left, BlockRemoveReason.ManualRemove);
            f.Tick(1); var cell = f.Belts.CaptureCell(branch);
            Assert.AreEqual(BeltSegmentKind.Normal, f.Snapshot().Simulation.Segments.Single().Kind);
            Assert.IsNull(cell.BufferedItem); Assert.AreEqual(running, cell.RunningItem.TransportGuid);
            Assert.AreEqual(1, cell.PriorityIndex); StringAssert.DoesNotContain(buffered.ToString(), f.Save());
            f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.left, BlockDirection.North); f.Tick(1);
            Assert.AreEqual(BeltSegmentKind.Branch, f.Snapshot().Simulation.Segments.Single().Kind);
            Assert.AreEqual(1, f.Belts.CaptureCell(branch).PriorityIndex);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Core.BeltTransport;
using Core.Master;
using Core.Update;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BeltRemovalRefundTest
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AllBeltShapesRefundLiveItemWithIdentity(int shape)
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            // マスタ初期化後にテストIDを解決する
            // Resolve test IDs after initializing the master
            var blockIds = new[] { ForUnitTestModBlockId.BeltConveyorId, ForUnitTestModBlockId.GearBeltConveyor,
                ForUnitTestModBlockId.ElectricBeltUp, ForUnitTestModBlockId.ElectricBeltDown,
                ForUnitTestModBlockId.TestGearBeltConveyorUp, ForUnitTestModBlockId.TestGearBeltConveyorDown };
            world.Place(blockIds[shape], Vector3Int.zero);
            var item = BeltRemovalRefundTestWorld.Item();
            world.Restore(Vector3Int.zero, new[] { new BeltItemState(item, 100) });

            // 実プロトコルで返却し次tickも復活なし
            // Refund through the production protocol; no next-tick resurrection
            Assert.IsTrue(world.Remove(Vector3Int.zero).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
            world.AssertRefund(item);
            GameUpdater.UpdateOneTick();
            Assert.IsEmpty(Assembly().Segments);
            Assert.AreEqual(1, world.Count(item.ItemId));
        }

        [TestCase(BeltConstants.ItemWidth - 1, 1)]
        [TestCase(BeltConstants.ItemWidth, 0)]
        public void RunningItemBelongsToItsHeadCell(int distance, int expectedRefund)
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            world.Place(ForUnitTestModBlockId.GearBeltConveyor, Vector3Int.zero);
            world.Place(ForUnitTestModBlockId.GearBeltConveyor, Vector3Int.forward);
            var item = BeltRemovalRefundTestWorld.Item();
            world.Restore(Vector3Int.zero, new[] { new BeltItemState(item, distance) });
            Assert.AreSame(SegmentAt(Vector3Int.zero), SegmentAt(Vector3Int.forward));

            // 境界直前と境界上を独立した配置で検証
            // Test immediately before and on the boundary in separate fixtures
            Assert.IsTrue(world.Remove(Vector3Int.forward).Success);
            Assert.AreEqual(expectedRefund, world.Count(item.ItemId));
            GameUpdater.UpdateOneTick();
            var remaining = ItemsOnSegmentAt(Vector3Int.zero);
            Assert.AreEqual(1 - expectedRefund, remaining.Length);
            if (expectedRefund == 1) world.AssertRefund(item);
            else Assert.AreEqual(item.ItemInstanceId, remaining.Single().Item.ItemInstanceId);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BufferRefundBelongsOnlyToLastCell(bool branch)
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            if (branch)
            {
                world.Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.back);
                world.Place(ForUnitTestModBlockId.BeltConveyorSplitter, Vector3Int.zero);
                EnterMachinePushWindow();
            }
            else world.CreateMerge();
            var position = branch ? Vector3Int.zero : new Vector3Int(0, 0, 1);
            var segment = (BeltBufferedSegment)SegmentAt(position);
            var layout = Assembly().Layouts.Single(entry => entry.Index == System.Array.IndexOf(Assembly().Segments, segment));
            var owner = layout.Cells.Last().Position;
            var item = BeltRemovalRefundTestWorld.Item();
            segment.Buffer.RestoreItem(item);

            // 上流撤去後もバッファは所有マスに残る
            // After upstream removal, the buffer remains on its owning cell
            Assert.IsTrue(world.Remove(Vector3Int.back).Success);
            Assert.AreEqual(0, world.Count(item.ItemId));
            Assert.IsTrue(segment.Buffer.HasItem);
            Assert.IsTrue(world.Remove(owner).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
            world.AssertRefund(item);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, Assembly().Segments.Sum(entry => entry.Count + (entry is BeltBufferedSegment buffered && buffered.Buffer.HasItem ? 1 : 0)));
        }

        [Test]
        public void InternalItemBelongsToMergeNotUpstream()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            world.CreateMerge();
            // 分岐バッファ→合流で内部区間を生成
            // A branch buffer feeding a merge creates an internal segment
            Assert.IsTrue(world.Remove(Vector3Int.zero).Success);
            world.Place(ForUnitTestModBlockId.BeltConveyorSplitter, Vector3Int.zero);
            EnterMachinePushWindow();
            var layout = Assembly().Layouts.First(entry => entry.IsInternal);
            var item = BeltRemovalRefundTestWorld.Item();
            Assembly().Segments[layout.Index].RestoreItems(new[] { new BeltItemState(item, 100) });

            // 内部区間は合流マス撤去時のみ返却
            // Refund internal segments only when removing the merge cell
            Assert.IsTrue(world.Remove(Vector3Int.back).Success);
            Assert.AreEqual(0, world.Count(item.ItemId));
            Assert.IsTrue(world.Remove(new Vector3Int(0, 0, 1)).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
            world.AssertRefund(item);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, Assembly().Segments.Sum(entry => entry.Count));
        }
    }
}

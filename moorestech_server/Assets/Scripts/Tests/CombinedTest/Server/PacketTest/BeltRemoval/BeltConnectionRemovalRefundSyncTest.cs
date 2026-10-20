using System.Linq;
using Core.BeltTransport;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BeltConnectionRemovalRefundSyncTest
    {
        [Test]
        public void TickEndRemovalPreservesReplicaHashAndDefersPhysicalConsumption()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            var merge = world.CreateConnectionMerge(true, false);
            var sourcePosition = new Vector3Int(-1, 0, 1);
            var item = world.RestoreInternal(BeltDirection.Left);

            // 下流と合流を詰め、搬送tick後も対象を内部区間に保つ
            // Fill downstream and merge segments so the target stays internal after transport
            var blocker = new BeltItem(ForUnitTestItemId.ItemId2, ItemInstanceId.Create(), BeltEntryDirection.FromBack);
            SegmentAt(Vector3Int.forward * 2).RestoreItems(new[] { new BeltItemState(blocker, 0) });
            var mergeSegment = (BeltBufferedSegment)SegmentAt(Vector3Int.forward);
            mergeSegment.RestoreItems(new[] { new BeltItemState(blocker, 0) });
            mergeSegment.Buffer.RestoreItem(blocker);
            world.Transport.Tick();
            var replica = BeltTransportReplicaAssembler.Assemble(world.Transport.CaptureFullState());
            var report = world.Transport.Tick();
            Assert.IsFalse(report.WasRebuilt);
            Assert.IsTrue(replica.Tick(report.Diff));
            var postTransportHash = world.Transport.ComputeStateHash();
            Assert.AreEqual(postTransportHash, BeltTransportStateHash.Compute(replica.CaptureFullState()));
            Assert.AreEqual(1, world.Transport.GetConnectionRemovalRefundItems(ServerContext.WorldBlockDatastore.GetBlock(sourcePosition).BlockInstanceId).Count);
            var beforeSave = world.Transport.CreateSaveState(merge.BlockInstanceId);
            Assert.AreEqual(1, beforeSave.InternalItems.Count);

            // 搬送通知後の撤去は境界ハッシュと全量を変えない
            // Removal after transport notification leaves the boundary hash and full state unchanged
            Assert.IsTrue(world.Remove(sourcePosition).Success);
            Assert.AreEqual(postTransportHash, world.Transport.ComputeStateHash());
            Assert.AreEqual(postTransportHash, BeltTransportStateHash.Compute(world.Transport.CaptureFullState()));
            Assert.AreEqual(postTransportHash, BeltTransportStateHash.Compute(replica.CaptureFullState()));
            Assert.AreEqual(1, world.Count(item.ItemId));
            Assert.AreEqual(1, world.Transport.Assembly.Layouts.Where(layout => layout.IsInternal)
                .Sum(layout => world.Transport.Assembly.Segments[layout.Index].Count));
            Assert.IsFalse(world.Transport.GetRefundItems(merge.BlockInstanceId).Any(stack => stack.Id == item.ItemId));
            Assert.IsEmpty(world.Transport.CreateSaveState(merge.BlockInstanceId).InternalItems);
            Assert.AreEqual(1, beforeSave.InternalItems.Count);

            // 次tick先頭で消費し、そのtickの全量から複製を更新する
            // Consume at the next tick head and update the replica from that tick's full state
            world.Transport.RebuildIfDirty();
            var rebuilt = world.Transport.Tick();
            Assert.IsTrue(rebuilt.WasRebuilt);
            replica = BeltTransportReplicaAssembler.Assemble(world.Transport.CaptureFullState());
            Assert.AreEqual(world.Transport.ComputeStateHash(), BeltTransportStateHash.Compute(replica.CaptureFullState()));
            Assert.IsFalse(world.Transport.Assembly.Segments.SelectMany(segment => segment.CaptureItems())
                .Any(state => state.Item.ItemId == item.ItemId));
            Assert.IsFalse(world.Transport.GetRefundItems(merge.BlockInstanceId).Any(stack => stack.Id == item.ItemId));
            Assert.IsEmpty(world.Transport.CreateSaveState(merge.BlockInstanceId).InternalItems);
            Assert.IsTrue(world.Remove(Vector3Int.forward).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
        }

        [Test]
        public void ReplacingSourceBeforeRebuildCannotRestoreRefundedItem()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            var merge = world.CreateConnectionMerge(true, false);
            var sourcePosition = new Vector3Int(-1, 0, 1);
            var first = world.RestoreInternal(BeltDirection.Left);
            Assert.IsTrue(world.Remove(sourcePosition).Success);
            var replacement = world.Place(ForUnitTestModBlockId.ChestId, sourcePosition);

            // 同じ方向の内部区間が戻っても返却済み品は復元しない
            // Do not restore a refunded item when the same internal direction reappears
            Assert.IsEmpty(world.Transport.GetConnectionRemovalRefundItems(replacement.BlockInstanceId));
            Assert.IsEmpty(world.Transport.GetRefundItems(merge.BlockInstanceId));
            Assert.IsEmpty(world.Transport.CreateSaveState(merge.BlockInstanceId).InternalItems);
            world.Transport.RebuildIfDirty();
            Assert.AreEqual(0, world.CarriedCount());
            Assert.AreEqual(1, world.Count(first.ItemId));

            // 再構築後の新しい搬送品には除外記録を残さない
            // Do not retain exclusion records for new items after rebuilding
            var second = world.RestoreInternal(BeltDirection.Left);
            var refund = world.Transport.GetConnectionRemovalRefundItems(replacement.BlockInstanceId).Single();
            Assert.AreEqual(second.ItemInstanceId, refund.ItemInstanceId);
            Assert.IsTrue(world.Remove(sourcePosition).Success);
            world.AssertRefund(second);
            Assert.AreEqual(2, world.Count(first.ItemId));
            world.Transport.RebuildIfDirty();
            Assert.AreEqual(0, world.CarriedCount());
        }
    }
}

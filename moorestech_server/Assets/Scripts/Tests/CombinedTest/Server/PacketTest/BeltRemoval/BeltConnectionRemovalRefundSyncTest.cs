using System.Linq;
using Core.BeltTransport;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Context;
using NUnit.Framework;
using UniRx;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BeltConnectionRemovalRefundSyncTest
    {
        // 搬送tick後の撤去(tick末尾の操作適用)は組から返却品をその場で取り除き、続く再構築の全量でクライアントの複製がサーバーと一致する
        // A removal after the transport tick (tick-end input) takes the refunded item out at once, and the full state from the following rebuild brings the client replica back in line with the server
        [Test]
        public void TickEndRemovalIsCarriedToReplicaByRebuiltFullState()
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
            Assert.IsTrue(replica.Tick(report.Diff));
            var postTransportHash = world.Transport.ComputeStateHash();
            Assert.AreEqual(postTransportHash, BeltTransportStateHash.Compute(replica.CaptureFullState()));
            Assert.AreEqual(1, world.Transport.GetConnectionRemovalRefundItems(ServerContext.WorldBlockDatastore.GetBlock(sourcePosition)).Count);
            var beforeSave = world.Transport.CreateSaveState(merge.BlockInstanceId);
            Assert.AreEqual(1, beforeSave.InternalItems.Count);

            // 撤去は返却品をその場で組から取り除く。取り出し済みの保存内容は書き換えない
            // The removal takes the refunded item out of the assembly at once; an already captured save state is not rewritten
            Assert.IsTrue(world.Remove(sourcePosition).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
            Assert.AreEqual(0, world.Transport.Assembly.Layouts.Where(layout => layout.IsInternal)
                .Sum(layout => world.Transport.Assembly.Segments[layout.Index].Count));
            Assert.IsFalse(world.Transport.GetOwnedRefundItems(merge.BlockInstanceId).Any(stack => stack.Id == item.ItemId));
            Assert.IsEmpty(world.Transport.CreateSaveState(merge.BlockInstanceId).InternalItems);
            Assert.AreEqual(1, beforeSave.InternalItems.Count);

            // tick末尾の再構築は全量を流し、それで組み直した複製は境界ハッシュと一致する
            // The tick-end rebuild publishes the full state, and a replica re-assembled from it matches the boundary hash
            BeltTransportFullState rebuiltFullState = null;
            using (world.Transport.OnAssemblyRebuilt.Subscribe(_ => rebuiltFullState = world.Transport.CaptureFullState())) world.Transport.RebuildIfDirty();
            Assert.IsNotNull(rebuiltFullState, "rebuild published");
            replica = BeltTransportReplicaAssembler.Assemble(rebuiltFullState);
            Assert.AreEqual(world.Transport.ComputeStateHash(), BeltTransportStateHash.Compute(replica.CaptureFullState()));
            Assert.IsFalse(world.Transport.Assembly.Segments.SelectMany(segment => segment.CaptureItems())
                .Any(state => state.Item.ItemId == item.ItemId));
            Assert.IsFalse(world.Transport.GetOwnedRefundItems(merge.BlockInstanceId).Any(stack => stack.Id == item.ItemId));
            Assert.IsEmpty(world.Transport.CreateSaveState(merge.BlockInstanceId).InternalItems);
            Assert.IsTrue(world.Remove(Vector3Int.forward).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
        }

        // 同tickで搬出元を撤去して置き直し、同じ方向の内部segmentが作り直されても、返却済みの品は戻らない
        // Removing and re-placing a source in one tick re-creates the same internal direction, yet the refunded item never comes back
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
            Assert.IsEmpty(world.Transport.GetConnectionRemovalRefundItems(replacement));
            Assert.IsEmpty(world.Transport.GetOwnedRefundItems(merge.BlockInstanceId));
            Assert.IsEmpty(world.Transport.CreateSaveState(merge.BlockInstanceId).InternalItems);
            world.Transport.RebuildIfDirty();
            Assert.AreEqual(0, world.CarriedCount());
            Assert.AreEqual(1, world.Count(first.ItemId));

            // 再構築後に載った新しい品は、次の撤去で通常どおり返る
            // A new item carried after the rebuild is refunded as usual by the next removal
            var second = world.RestoreInternal(BeltDirection.Left);
            var refund = world.Transport.GetConnectionRemovalRefundItems(replacement).Single();
            Assert.AreEqual(second.ItemInstanceId, refund.ItemInstanceId);
            Assert.IsTrue(world.Remove(sourcePosition).Success);
            world.AssertRefund(second);
            Assert.AreEqual(2, world.Count(first.ItemId));
            world.Transport.RebuildIfDirty();
            Assert.AreEqual(0, world.CarriedCount());
        }
    }
}

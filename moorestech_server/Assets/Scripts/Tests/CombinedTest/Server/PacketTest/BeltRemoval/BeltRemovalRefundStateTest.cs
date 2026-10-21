using System.Linq;
using Core.BeltTransport;
using Core.Update;
using Core.Item.Interface;
using Game.Construction;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BeltRemovalRefundStateTest
    {
        [Test]
        public void FullInventoryPreservesMergeStateAndRetryRefundsAll()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            var block = world.CreateMerge();
            var position = new Vector3Int(0, 0, 1);
            var segment = (BeltBufferedSegment)SegmentAt(position);
            var item = BeltRemovalRefundTestWorld.Item();
            segment.Buffer.RestoreItem(item);
            var priority = segment.PriorityOrder;
            var assembly = world.Transport.Assembly;
            var hash = world.Transport.ComputeStateHash();
            world.FillInventory();
            var inventory = world.Inventory.InventoryItems.ToArray();
            var wallet = world.Wallet(block.BlockId);

            // 拒否時は搬送・手持ち・財布を全て不変にする
            // Rejection preserves transport, inventory, and wallet state
            Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull, world.Remove(position).FailureReason);
            Assert.AreSame(block, ServerContext.WorldBlockDatastore.GetBlock(position));
            Assert.AreSame(assembly, world.Transport.Assembly);
            Assert.AreEqual(hash, world.Transport.ComputeStateHash());
            Assert.AreEqual(priority, segment.PriorityOrder);
            Assert.IsTrue(segment.Buffer.TryGetItem(out var held));
            Assert.AreEqual(item.ItemInstanceId, held.ItemInstanceId);
            CollectionAssert.AreEqual(inventory, world.Inventory.InventoryItems);
            Assert.AreEqual(wallet, world.Wallet(block.BlockId));
            world.ClearInventory();
            Assert.IsTrue(world.Remove(position).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
            world.AssertRefund(item);
        }

        [Test]
        public void CarriedItemSpaceAloneCannotPayConstructionRefund()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            var block = world.Place(belt, Vector3Int.zero);
            var item = BeltRemovalRefundTestWorld.Item();
            world.Restore(Vector3Int.zero, new[] { new BeltItemState(item, 100) });
            world.Services.GetService<IRemainingPlacementCountMutation>().Refill(1, belt, 2);
            world.FillInventory();
            world.Inventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            var before = world.Inventory.InventoryItems.ToArray();
            var hash = world.Transport.ComputeStateHash();

            // 搬送1個の空きがあっても建設費全量を要求
            // Require room for all construction materials even with space for the carried item
            Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull, world.Remove(Vector3Int.zero).FailureReason);
            Assert.AreSame(block, ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero));
            Assert.AreEqual(2, world.Wallet(belt));
            Assert.AreEqual(hash, world.Transport.ComputeStateHash());
            CollectionAssert.AreEqual(before, world.Inventory.InventoryItems);
            world.ClearInventory();
            Assert.IsTrue(world.Remove(Vector3Int.zero).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
            Assert.AreEqual(0, world.Wallet(belt));
            world.AssertRefund(item);
        }

        [Test]
        public void ConsecutiveRemovalsConserveItemsWithoutDuplicates()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            for (var z = 0; z < 3; z++) world.Place(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, z));
            var states = Enumerable.Range(0, 3).Select(i => new BeltItemState(BeltRemovalRefundTestWorld.Item(), 100 + i * BeltConstants.ItemWidth)).ToArray();
            world.Restore(Vector3Int.zero, states);

            // 再構築前に2マスを消し、同じ位置を重ねて要求
            // Remove two cells before rebuilding, then repeat the same request
            Assert.IsTrue(world.Remove(new Vector3Int(0, 0, 2)).Success);
            Assert.IsTrue(world.Remove(Vector3Int.forward).Success);
            Assert.IsFalse(world.Remove(Vector3Int.forward).Success);
            Assert.AreEqual(2, world.Count(ForUnitTestItemId.ItemId1));
            GameUpdater.UpdateOneTick();
            var remaining = ItemsOnSegmentAt(Vector3Int.zero);
            Assert.AreEqual(1, remaining.Length);
            Assert.AreEqual(states[2].Item.ItemInstanceId, remaining[0].Item.ItemInstanceId);
            Assert.AreEqual(3, world.Count(ForUnitTestItemId.ItemId1) + remaining.Length);
        }

        [Test]
        public void RefundReadsCurrentItemsInsteadOfSaveCache()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            var block = world.Place(ForUnitTestModBlockId.GearBeltConveyor, Vector3Int.zero);
            var oldItem = BeltRemovalRefundTestWorld.Item();
            EnterMachinePushWindow();
            var cached = world.Transport.CreateSaveState(block.BlockInstanceId);
            var current = new BeltItem(ForUnitTestItemId.ItemId2, ItemInstanceId.Create(), BeltEntryDirection.FromBack);
            SegmentAt(Vector3Int.zero).RestoreItems(new[] { new BeltItemState(current, 100) });

            // 保存cacheを温めた後の状態注入を返却へ反映
            // Refund the live injection made after warming the save cache
            Assert.IsEmpty(cached.Items);
            Assert.IsEmpty(world.Transport.CreateSaveState(block.BlockInstanceId).Items);
            Assert.IsTrue(world.Remove(Vector3Int.zero).Success);
            Assert.AreEqual(0, world.Count(oldItem.ItemId));
            Assert.AreEqual(1, world.Count(current.ItemId));
            world.AssertRefund(current);
        }
    }
}

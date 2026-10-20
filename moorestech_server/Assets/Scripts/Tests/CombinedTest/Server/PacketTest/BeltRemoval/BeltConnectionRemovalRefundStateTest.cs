using System.Linq;
using Core.BeltTransport;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Context;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Transport;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BeltConnectionRemovalRefundStateTest
    {
        [Test]
        public void FullInventoryPreservesLiveStateAndRetryInvalidatesSaveCache()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            var merge = world.CreateConnectionMerge(true, false);
            var item = world.RestoreInternal(BeltDirection.Left);
            var sourcePosition = new Vector3Int(-1, 0, 1);
            var source = ServerContext.WorldBlockDatastore.GetBlock(sourcePosition);
            var saved = world.Transport.CreateSaveState(merge.BlockInstanceId);
            var serialized = JsonConvert.SerializeObject(saved);
            var assembly = Assembly();
            var hash = world.Transport.ComputeStateHash();
            var wallet = world.Wallet(source.BlockId);
            world.FillInventory();
            var before = world.Inventory.InventoryItems.ToArray();

            // 照会と満杯拒否では保存キャッシュも不変
            // Queries and full-inventory rejection also preserve the save cache
            Assert.AreEqual(1, world.Transport.GetConnectionRemovalRefundItems(source.BlockInstanceId).Count);
            Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull, world.Remove(sourcePosition).FailureReason);
            Assert.AreSame(source, ServerContext.WorldBlockDatastore.GetBlock(sourcePosition));
            Assert.AreSame(assembly, Assembly());
            Assert.AreEqual(hash, world.Transport.ComputeStateHash());
            Assert.AreSame(saved, world.Transport.CreateSaveState(merge.BlockInstanceId));
            Assert.AreEqual(serialized, JsonConvert.SerializeObject(saved));
            CollectionAssert.AreEqual(before, world.Inventory.InventoryItems);
            Assert.AreEqual(wallet, world.Wallet(source.BlockId));

            // 成功時も物理状態を保ち返却と保存から対象を除く
            // Preserve physical state on success while excluding the items from refunds and saves
            world.ClearInventory();
            Assert.IsTrue(world.Remove(sourcePosition).Success);
            world.AssertRefund(item);
            Assert.AreEqual(1, world.Count(item.ItemId));
            Assert.AreEqual(1, world.CarriedCount());
            Assert.AreEqual(hash, world.Transport.ComputeStateHash());
            Assert.IsEmpty(world.Transport.GetRefundItems(merge.BlockInstanceId));
            Assert.IsEmpty(world.Transport.CreateSaveState(merge.BlockInstanceId).InternalItems);
            Assert.AreEqual(serialized, JsonConvert.SerializeObject(saved));
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, world.Count(item.ItemId));
            Assert.AreEqual(0, world.CarriedCount());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PendingOverridesLiveAndRetainsUnaffectedCategories(bool staleLiveInternal)
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            BeltTransportTestUtil.InstallMachinePorts(new[] { Vector3Int.left, Vector3Int.right }, new[] { Vector3Int.back });
            var merge = world.Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.forward);
            world.Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero);
            world.Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.forward * 2);
            var left = world.Place(ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1));
            var right = world.Place(ForUnitTestModBlockId.ChestId, new Vector3Int(1, 0, 1));
            if (staleLiveInternal)
            {
                EnterMachinePushWindow();
                world.RestoreInternal(BeltDirection.Left);
                world.RestoreInternal(BeltDirection.Right);
                var oldItem = BeltRemovalRefundTestWorld.Item();
                SegmentAt(Vector3Int.forward).RestoreItems(new[] { new BeltItemState(oldItem, 100) });
                ((BeltBufferedSegment)SegmentAt(Vector3Int.forward)).Buffer.RestoreItem(oldItem);
            }
            var item = BeltRemovalRefundTestWorld.Item();
            var pending = new BeltConveyorSaveJsonObject
            {
                BufferItem = new BeltItemSaveJsonObject(item, 0),
                PriorityOrder = BeltPriority.Create(BeltDirection.Back)
            };
            pending.Items.Add(new BeltItemSaveJsonObject(item, 100));
            pending.InternalItems.Add(new BeltInternalItemSaveJsonObject(BeltDirection.Left, item, 100));
            pending.InternalItems.Add(new BeltInternalItemSaveJsonObject(BeltDirection.Right, item, 100));
            world.Transport.RegisterLoadedState(merge.BlockInstanceId, pending);
            var serialized = JsonConvert.SerializeObject(pending);
            var hash = world.Transport.ComputeStateHash();
            var assembly = Assembly();
            world.FillInventory();
            var before = world.Inventory.InventoryItems.ToArray();

            // 初回再構築前もpendingのみ計上
            // Count only pending content even before the first rebuild
            Assert.AreEqual(1, world.Transport.GetConnectionRemovalRefundItems(left.BlockInstanceId).Count);
            Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull, world.Remove(new Vector3Int(-1, 0, 1)).FailureReason);
            Assert.AreSame(assembly, Assembly());
            Assert.AreEqual(hash, world.Transport.ComputeStateHash());
            Assert.AreSame(pending, world.Transport.CreateSaveState(merge.BlockInstanceId));
            Assert.AreEqual(serialized, JsonConvert.SerializeObject(pending));
            CollectionAssert.AreEqual(before, world.Inventory.InventoryItems);
            world.ClearInventory();
            Assert.IsTrue(world.Remove(new Vector3Int(-1, 0, 1)).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
            Assert.AreEqual(staleLiveInternal ? 4 : 0, world.CarriedCount());
            Assert.AreEqual(hash, world.Transport.ComputeStateHash());

            // 撤去方向のみ消費し他状態を保持
            // Consume only the removed direction; retain running items, buffer and priority
            var remaining = world.Transport.CreateSaveState(merge.BlockInstanceId);
            Assert.AreNotSame(pending, remaining);
            Assert.AreSame(pending.Items, remaining.Items);
            Assert.AreSame(pending.BufferItem, remaining.BufferItem);
            Assert.AreEqual(pending.PriorityOrder, remaining.PriorityOrder);
            Assert.AreEqual((int)BeltDirection.Right, remaining.InternalItems.Single().InputDirection);
            Assert.AreEqual(serialized, JsonConvert.SerializeObject(pending));
            Assert.AreEqual(3, world.Transport.GetRefundItems(merge.BlockInstanceId).Count);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(4, world.Count(item.ItemId) + world.CarriedCount());
            foreach (var segment in Assembly().Segments) Assert.LessOrEqual(segment.Count, segment.Capacity);

            // 存続方向も後続撤去で一度だけ返す
            // Refund the surviving direction exactly once on its later removal
            var additional = world.Transport.GetConnectionRemovalRefundItems(right.BlockInstanceId).Count;
            Assert.IsTrue(world.Remove(new Vector3Int(1, 0, 1)).Success);
            Assert.AreEqual(1 + additional, world.Count(item.ItemId));
            Assert.IsTrue(world.Remove(Vector3Int.forward).Success);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(4, world.Count(item.ItemId) + world.CarriedCount());
        }

        [Test]
        public void OrdinaryInventoryAndInternalItemMustBothFit()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            for (var z = 0; z <= 2; z++) world.Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z));
            var sourcePosition = new Vector3Int(-1, 0, 1);
            var source = world.Place(ForUnitTestModBlockId.MachineId, sourcePosition);
            EnterMachinePushWindow();
            var item = world.RestoreInternal(BeltDirection.Left);
            var inventory = Inventory(source);
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 1));
            world.FillInventory();
            world.Inventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            var hash = world.Transport.ComputeStateHash();

            // 空き1枠では通常在庫との合計が入らない
            // One free slot cannot fit both ordinary inventory and transport contents
            Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull, world.Remove(sourcePosition).FailureReason);
            Assert.AreEqual(hash, world.Transport.ComputeStateHash());
            Assert.AreEqual(1, inventory.GetItem(0).Count);
            world.ClearInventory();
            Assert.IsTrue(world.Remove(sourcePosition).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
            Assert.AreEqual(1, world.Count(ForUnitTestItemId.ItemId3));
        }
    }
}

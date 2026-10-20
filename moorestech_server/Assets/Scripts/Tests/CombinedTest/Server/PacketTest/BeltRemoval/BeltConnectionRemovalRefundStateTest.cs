using System.Linq;
using Core.BeltTransport;
using Core.Update;
using Game.Context;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
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

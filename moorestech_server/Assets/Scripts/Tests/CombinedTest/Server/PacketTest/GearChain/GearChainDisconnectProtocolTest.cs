using System;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using UnityEngine;
using static Server.Protocol.PacketResponse.GearChainConnectionEditProtocol;
using static Tests.CombinedTest.Server.PacketTest.GearChain.GearChainEditTestWorld;

namespace Tests.CombinedTest.Server.PacketTest.GearChain
{
    // 切断と返却を一体として検証する
    // Verify disconnect and refund as one operation
    public class GearChainDisconnectProtocolTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void DisconnectRefundsOnceFromEitherEndpoint(bool reverse)
        {
            var world = new GearChainEditTestWorld(true);
            world.Connect();
            var request = GearChainConnectionEditRequest.CreateDisconnectRequest(reverse ? PosB : PosA, reverse ? PosA : PosB);
            var response = world.Send(request);

            // どちらから切っても両端が消え、一度だけ返る
            // Either direction removes both endpoints and refunds exactly once
            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.IsEmpty(response.Error);
            Assert.IsFalse(Pole(PosA).ContainsChainConnection(Pole(PosB).BlockInstanceId));
            Assert.IsFalse(Pole(PosB).ContainsChainConnection(Pole(PosA).BlockInstanceId));
            Assert.AreEqual(10, world.CountItem(world.ChainItemId));
            Assert.IsEmpty(TakeDenied(world.RequesterSink));
            Assert.IsEmpty(TakeDenied(world.OtherSink));

            world.AssertDenied(world.Send(request), "NotConnected", "denied.gearChainDisconnect.NotConnected");
            Assert.AreEqual(10, world.CountItem(world.ChainItemId));
        }

        [Test]
        public void FullInventoryPreservesBothRecordsAndAllItems()
        {
            var world = new GearChainEditTestWorld(true);
            world.Connect();
            world.FillInventory();
            var before = world.Inventory.InventoryItems.Select(stack => (stack.Id, stack.Count)).ToArray();

            // 返却拒否で記録もインベントリも不変
            // A refused refund changes neither records nor inventory
            var response = world.Send(GearChainConnectionEditRequest.CreateDisconnectRequest(PosA, PosB));
            world.AssertDenied(response, "InventoryFull", "denied.gearChainDisconnect.InventoryFull");
            Assert.IsTrue(Pole(PosA).TryGetChainConnectionRecord(Pole(PosB).BlockInstanceId, out var recordA));
            Assert.IsTrue(Pole(PosB).TryGetChainConnectionRecord(Pole(PosA).BlockInstanceId, out var recordB));
            Assert.AreEqual(ChainToolGuid, recordA.ConnectToolGuid);
            Assert.AreEqual(10, recordA.Materials.Single().Count);
            Assert.AreEqual(recordA, recordB);
            CollectionAssert.AreEqual(before, world.Inventory.InventoryItems.Select(stack => (stack.Id, stack.Count)).ToArray());
        }

        [Test]
        public void UnconnectedPairNotifiesRequester()
        {
            var world = new GearChainEditTestWorld(true);
            var response = world.Send(GearChainConnectionEditRequest.CreateDisconnectRequest(PosA, PosB));
            world.AssertDenied(response, "NotConnected", "denied.gearChainDisconnect.NotConnected");
            Assert.AreEqual(0, world.CountItem(world.ChainItemId));
        }

        [Test]
        public void MissingEndpointNotifiesRequester()
        {
            var world = new GearChainEditTestWorld(true);
            var response = world.Send(GearChainConnectionEditRequest.CreateDisconnectRequest(PosA, new Vector3Int(99, 0, 0)));
            world.AssertDenied(response, "InvalidTarget", "denied.gearChainDisconnect.InvalidTarget");
        }

        [Test]
        public void RefundUsesRecordedMaterialsInsteadOfCurrentToolCost()
        {
            var world = new GearChainEditTestWorld(true);
            // 支払額は現在のマスタ単価と独立して返す
            // Refund the saved payment independent of the current master price
            var materials = new[] { new ConnectToolMaterialCost(world.ChainItemId, 3), new ConnectToolMaterialCost(world.FillerItemId, 2) };
            var record = new ConnectionLineRecord(ChainToolGuid, materials);
            Assert.IsTrue(Pole(PosA).TryAddChainConnection(Pole(PosB).BlockInstanceId, record));
            Assert.IsTrue(Pole(PosB).TryAddChainConnection(Pole(PosA).BlockInstanceId, record));
            var response = world.Send(GearChainConnectionEditRequest.CreateDisconnectRequest(PosA, PosB));

            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.AreEqual(3, world.CountItem(world.ChainItemId));
            Assert.AreEqual(2, world.CountItem(world.FillerItemId));
            // 後続のブロック撤去でも返却を重複させない
            // Removing a block afterwards cannot duplicate the refund
            ServerContext.WorldBlockDatastore.RemoveBlock(PosA, BlockRemoveReason.ManualRemove);
            Assert.AreEqual(3, world.CountItem(world.ChainItemId));
            Assert.AreEqual(2, world.CountItem(world.FillerItemId));
        }

        [Test]
        public void RefundThatOnlyPartiallyFitsLeavesInventoryAndConnectionUntouched()
        {
            var world = new GearChainEditTestWorld(true);
            var materials = new[] { new ConnectToolMaterialCost(world.ChainItemId, 3), new ConnectToolMaterialCost(world.FillerItemId, 2) };
            var record = new ConnectionLineRecord(ChainToolGuid, materials);
            Assert.IsTrue(Pole(PosA).TryAddChainConnection(Pole(PosB).BlockInstanceId, record));
            Assert.IsTrue(Pole(PosB).TryAddChainConnection(Pole(PosA).BlockInstanceId, record));
            // 二種類の返却に対して一枠だけ空ける
            // Leave only one empty slot for two distinct refund materials
            world.FillInventory();
            world.Inventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            var before = world.Inventory.InventoryItems.Select(stack => (stack.Id, stack.Count)).ToArray();
            var response = world.Send(GearChainConnectionEditRequest.CreateDisconnectRequest(PosA, PosB));

            world.AssertDenied(response, "InventoryFull", "denied.gearChainDisconnect.InventoryFull");
            CollectionAssert.AreEqual(before, world.Inventory.InventoryItems.Select(stack => (stack.Id, stack.Count)).ToArray());
            Assert.IsTrue(Pole(PosA).ContainsChainConnection(Pole(PosB).BlockInstanceId));
            Assert.IsTrue(Pole(PosB).ContainsChainConnection(Pole(PosA).BlockInstanceId));
        }

        [Test]
        public void EmptyRecordedRefundCanDisconnectWithFullInventory()
        {
            var world = new GearChainEditTestWorld(true);
            var record = new ConnectionLineRecord(ChainToolGuid, Array.Empty<ConnectToolMaterialCost>());
            Assert.IsTrue(Pole(PosA).TryAddChainConnection(Pole(PosB).BlockInstanceId, record));
            Assert.IsTrue(Pole(PosB).TryAddChainConnection(Pole(PosA).BlockInstanceId, record));
            world.FillInventory();
            var before = world.CountItem(world.FillerItemId);

            var response = world.Send(GearChainConnectionEditRequest.CreateDisconnectRequest(PosA, PosB));
            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.AreEqual(before, world.CountItem(world.FillerItemId));
            Assert.IsFalse(Pole(PosA).ContainsChainConnection(Pole(PosB).BlockInstanceId));
            Assert.IsFalse(Pole(PosB).ContainsChainConnection(Pole(PosA).BlockInstanceId));
        }
    }
}

using Game.Context;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.GearChain;
using UnityEngine;
using static Server.Protocol.PacketResponse.GearChainConnectionEditProtocol;
using static Tests.CombinedTest.Server.PacketTest.GearChain.GearChainEditTestWorld;

namespace Tests.CombinedTest.Server.PacketTest.GearChain
{
    // 全接続拒否を要求者だけに伝え、Undoの失敗を無音にしない
    // Notify only the requester of every connect refusal so undo never fails silently
    public class GearChainConnectDeniedNotificationTest
    {
        [TestCase(GearChainPlacementFailureReason.NoItem)]
        [TestCase(GearChainPlacementFailureReason.NotUnlocked)]
        [TestCase(GearChainPlacementFailureReason.InvalidTarget)]
        [TestCase(GearChainPlacementFailureReason.TooFar)]
        [TestCase(GearChainPlacementFailureReason.AlreadyConnected)]
        [TestCase(GearChainPlacementFailureReason.ConnectionLimit)]
        public void DeniedConnectionNotifiesRequesterWithoutConsumingItems(GearChainPlacementFailureReason reason)
        {
            var world = new GearChainEditTestWorld(reason != GearChainPlacementFailureReason.NotUnlocked);
            var target = PosB;
            // 各拒否の起点となる実際の世界状態を作る
            // Build the actual world state that triggers each refusal
            switch (reason)
            {
                case GearChainPlacementFailureReason.InvalidTarget:
                    target = new Vector3Int(99, 0, 0);
                    break;
                case GearChainPlacementFailureReason.TooFar:
                    target = new Vector3Int(30, 0, 0);
                    PlacePole(target);
                    break;
                case GearChainPlacementFailureReason.AlreadyConnected:
                    world.Connect();
                    break;
                case GearChainPlacementFailureReason.ConnectionLimit:
                    world.Connect();
                    var second = new Vector3Int(-1, 0, 0);
                    PlacePole(second);
                    world.Inventory.SetItem(0, ServerContext.ItemStackFactory.Create(world.ChainItemId, 10));
                    Assert.IsTrue(world.Send(GearChainConnectionEditRequest.CreateConnectRequest(PosA, second, ChainToolGuid)).IsSuccess);
                    target = new Vector3Int(1, 0, 2);
                    PlacePole(target);
                    break;
            }

            // 在庫を持たせても拒否理由が変わらず、失敗では消費しない
            // Supplying inventory does not change the refusal or consume items on failure
            world.Inventory.SetItem(0, ServerContext.ItemStackFactory.Create(world.ChainItemId, reason == GearChainPlacementFailureReason.NoItem ? 9 : 10));
            world.ClearEvents();
            var before = world.CountItem(world.ChainItemId);
            var response = world.Send(GearChainConnectionEditRequest.CreateConnectRequest(PosA, target, ChainToolGuid));
            world.AssertDenied(response, reason.ToString(), $"denied.gearChainConnect.{reason}");
            Assert.AreEqual(before, world.CountItem(world.ChainItemId));
            if (reason != GearChainPlacementFailureReason.InvalidTarget)
            {
                var connectedBefore = reason == GearChainPlacementFailureReason.AlreadyConnected;
                Assert.AreEqual(connectedBefore, Pole(PosA).ContainsChainConnection(Pole(target).BlockInstanceId));
                Assert.AreEqual(connectedBefore, Pole(target).ContainsChainConnection(Pole(PosA).BlockInstanceId));
            }
        }

        [Test]
        public void SuccessfulConnectionProducesNoDenial()
        {
            var world = new GearChainEditTestWorld(true);
            world.Inventory.SetItem(0, ServerContext.ItemStackFactory.Create(world.ChainItemId, 10));
            var response = world.Send(GearChainConnectionEditRequest.CreateConnectRequest(PosA, PosB, ChainToolGuid));

            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.IsEmpty(response.Error);
            Assert.IsTrue(Pole(PosA).ContainsChainConnection(Pole(PosB).BlockInstanceId));
            Assert.IsTrue(Pole(PosB).ContainsChainConnection(Pole(PosA).BlockInstanceId));
            Assert.AreEqual(0, world.CountItem(world.ChainItemId));
            Assert.IsEmpty(TakeDenied(world.RequesterSink));
            Assert.IsEmpty(TakeDenied(world.OtherSink));
        }
    }
}

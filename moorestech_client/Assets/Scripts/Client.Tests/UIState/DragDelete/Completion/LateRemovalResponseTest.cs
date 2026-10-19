using System;
using System.Linq;
using System.Threading;
using Client.Network.API;
using Client.Tests.PlaceSystem.TrainCostIntegration;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete.Completion
{
    public class LateRemovalResponseTest
    {
        [SetUp]
        public void SetUp()
        {
            // 通信APIが参照するサーバー依存を先に構築する
            // Initialize server dependencies before constructing the network API
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TestCase(0, true)] [TestCase(0, false)]
        [TestCase(1, true)] [TestCase(1, false)]
        [TestCase(2, true)] [TestCase(2, false)]
        [TestCase(3, true)] [TestCase(3, false)]
        public void IndividualRequestRetainsItsResponseAfterUiDeadline(int kind, bool success)
        {
            using var capture = new PlacementPacketCapture();
            var result = Send(capture.Api, kind);
            var waiter = capture.Waiters.Single().Value;
            // 同じsequenceの待機は10秒でも一日後でも期限切れにしない
            // The same sequence remains pending past ten seconds and even one day
            Assert.IsFalse(waiter.HasExpired(waiter.SendTime + PacketExchangeManager.ResponseTimeout));
            Assert.IsFalse(waiter.HasExpired(waiter.SendTime.AddDays(1)));
            Assert.AreEqual(UniTaskStatus.Pending, result.Status);
            Respond(waiter, kind, success);
            Assert.AreEqual(success, result.GetAwaiter().GetResult());
        }

        [Test]
        public void MixedResponsesCanArriveInReverseOrderWithoutCrossCompleting()
        {
            using var capture = new PlacementPacketCapture();
            var first = Send(capture.Api, 0);
            var second = Send(capture.Api, 3);
            var waiters = capture.Waiters.OrderBy(pair => pair.Key).ToArray();
            Respond(waiters[1].Value, 3, false);
            Assert.IsFalse(second.GetAwaiter().GetResult());
            Assert.AreEqual(UniTaskStatus.Pending, first.Status);
            Respond(waiters[0].Value, 0, true);
            Assert.IsTrue(first.GetAwaiter().GetResult());
        }

        [Test]
        public void OrdinaryRequestsKeepTheirExistingDeadline()
        {
            var waiter = new ResponseWaiter(new UniRx.Subject<(byte[], PacketWaitCompletionReason)>(), 10);
            Assert.IsTrue(waiter.HasExpired(waiter.SendTime.AddSeconds(10)));
        }

        private static async UniTask<bool> Send(VanillaApi api, int kind)
        {
            // 4つのproduction要求口を直接通し、テスト専用応答口を作らない
            // Exercise all four production request APIs without a test-only response API
            switch (kind)
            {
                case 0: return (await api.Response.Block.BlockRemoveIfInstanceAwaitFinal(Vector3Int.zero, new BlockInstanceId(1), CancellationToken.None)).Success;
                case 1: return (await api.Response.Removal.DisconnectRail(0, Guid.NewGuid(), 1, Guid.NewGuid(), CancellationToken.None)).Success;
                case 2: return (await api.Response.Removal.DisconnectElectricWire(Vector3Int.zero, Vector3Int.one, CancellationToken.None)).IsSuccess;
                case 3: return (await api.Response.Removal.DisconnectGearChain(Vector3Int.zero, Vector3Int.one, CancellationToken.None)).IsSuccess;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void Respond(ResponseWaiter waiter, int kind, bool success)
        {
            // TCPの配信先と同じwaiterへ実MessagePack応答を渡す
            // Deliver real MessagePack responses to the same waiter used by TCP dispatch
            byte[] data = kind switch
            {
                0 => MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockResponseMessagePack(success, RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull)),
                1 => MessagePackSerializer.Serialize(RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack.Create(success, RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughInventorySpace, RailConnectionEditProtocol.RailEditMode.Disconnect)),
                2 => MessagePackSerializer.Serialize(new ElectricWireDisconnectProtocol.ElectricWireDisconnectResponse(success, Server.Protocol.PacketResponse.Util.ElectricWire.Placement.ElectricWirePlacementFailureReason.InventoryFull)),
                3 => MessagePackSerializer.Serialize(new GearChainConnectionEditProtocol.GearChainConnectionEditResponse(success, "InventoryFull")),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            waiter.WaitSubject.OnNext((data, PacketWaitCompletionReason.Received));
        }
    }
}

using System.Linq;
using System.Threading;
using Client.Network.API;
using Client.Tests.PlaceSystem.TrainCostIntegration;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 宇宙エレベーター接続の応答は受信の完了だけ復号し、それ以外は理由をそのまま返すことを確かめる（L0）
    /// Verifies the space elevator connect response decodes only on a received completion and passes other reasons through (L0)
    /// </summary>
    public class SpaceElevatorConnectResponseDecodeTest
    {
        // 応答の復号はサーバーのマスタの読み込みを前提にする（元は同じクラスの 1 件目が組んでいた）
        // Decoding relies on the loaded server master (formerly set up by the first test in the same class)
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TestCase(PacketWaitCompletionReason.Received, true)]
        [TestCase(PacketWaitCompletionReason.Timeout, false)]
        [TestCase(PacketWaitCompletionReason.DeserializeFailed, false)]
        [TestCase((PacketWaitCompletionReason)999, false)]
        public void PacketCompletionPreservesReasonAndDecodesOnlyReceived(PacketWaitCompletionReason reason, bool hasResponse)
        {
            using var capture = new PlacementPacketCapture();
            var pending = capture.Api.Response.Block.ConnectSpaceElevator(Vector3Int.zero, new BlockInstanceId(123), CancellationToken.None);
            capture.ReadRequest();

            // 非受信の壊れた本文を復号しない
            // Even a malformed payload on a non-Received completion must pass the reason through without decoding
            var bytes = hasResponse ? MessagePackSerializer.Serialize(new ConnectSpaceElevatorResponse(SpaceElevatorConnectResult.Connected)) : new byte[] { 0xc1 };
            capture.Waiters.Single().Value.WaitSubject.OnNext((bytes, reason));
            var actual = pending.GetAwaiter().GetResult();
            Assert.AreEqual(reason, actual.reason);
            Assert.AreEqual(hasResponse, actual.response != null);
            if (hasResponse) Assert.AreEqual(SpaceElevatorConnectResult.Connected, actual.response.Result);
        }
    }
}

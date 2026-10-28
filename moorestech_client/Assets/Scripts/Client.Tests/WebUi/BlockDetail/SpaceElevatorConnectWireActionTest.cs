using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Client.Tests.Common;
using Client.Tests.PlaceSystem.TrainCostIntegration;
using Client.WebUiHost.Game.Actions;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using MessagePack;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUi.BlockDetail
{
    public class SpaceElevatorConnectWireActionTest
    {
        private VanillaApi _previousApi;
        private GameObject _owner;
        [SetUp] public void RememberContext() => _previousApi = ClientContext.VanillaApi;
        [TearDown] public void RestoreContext()
        {
            TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", _previousApi);
            if (_owner != null) UnityEngine.Object.DestroyImmediate(_owner);
        }

        [TestCase(SpaceElevatorConnectResult.Connected, PacketWaitCompletionReason.Received, null)]
        [TestCase(SpaceElevatorConnectResult.AlreadyConnected, PacketWaitCompletionReason.Received, "already_connected")]
        [TestCase(SpaceElevatorConnectResult.InsufficientCable, PacketWaitCompletionReason.Received, "insufficient_cable")]
        [TestCase(SpaceElevatorConnectResult.NotSpaceElevatorBase, PacketWaitCompletionReason.Received, "not_space_elevator_base")]
        [TestCase(SpaceElevatorConnectResult.InvalidRequest, PacketWaitCompletionReason.Received, "connect_invalid_request")]
        [TestCase(SpaceElevatorConnectResult.BlockNotFound, PacketWaitCompletionReason.Received, "connect_block_not_found")]
        [TestCase(SpaceElevatorConnectResult.InstanceMismatch, PacketWaitCompletionReason.Received, "connect_instance_mismatch")]
        [TestCase((SpaceElevatorConnectResult)999, PacketWaitCompletionReason.Received, null)]
        [TestCase(SpaceElevatorConnectResult.Connected, (PacketWaitCompletionReason)999, null)]
        [TestCase(SpaceElevatorConnectResult.Connected, PacketWaitCompletionReason.Timeout, "connect_timeout")]
        [TestCase(SpaceElevatorConnectResult.Connected, PacketWaitCompletionReason.DeserializeFailed, "connect_invalid_response")]
        public void RequestCarriesIdentityAndPreservesEveryResult(SpaceElevatorConnectResult result, PacketWaitCompletionReason reason, string error)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            using var capture = new PlacementPacketCapture();
            _owner = new GameObject("connect-test");
            var block = _owner.AddComponent<BlockGameObject>();
            var store = _owner.AddComponent<BlockGameObjectDataStore>();
            var position = new Vector3Int(7, 0, 9);
            var instance = new BlockInstanceId(123);
            TestReflection.SetField(block, "<BlockMasterElement>k__BackingField", MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestSpaceElevatorBase));
            TestReflection.SetField(block, "<BlockInstanceId>k__BackingField", instance);
            TestReflection.SetField(store, "_blockObjectsDictionary", new Dictionary<Vector3Int, BlockGameObject> { [position] = block });
            var state = (SubInventoryState)FormatterServices.GetUninitializedObject(typeof(SubInventoryState));
            TestReflection.SetField(state, "<CurrentSubInventorySource>k__BackingField", new BlockSubInventorySource(store, position));
            TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", capture.Api);

            // 実APIの送信を復号して対象個体を確かめる
            // Decode the real API request to verify the target instance
            var pending = new SpaceElevatorConnectActionHandler(state).ExecuteAsync(new JObject());
            var request = MessagePackSerializer.Deserialize<ConnectSpaceElevatorRequest>(capture.ReadRequest());
            Assert.AreEqual(position, request.Position.Vector3Int);
            Assert.AreEqual(instance.AsPrimitive(), request.ExpectedInstanceId);
            if (reason == PacketWaitCompletionReason.DeserializeFailed)
                LogAssert.Expect(LogType.Error, new Regex("デシリアライズに失敗しました"));
            if (error != null) LogAssert.Expect(LogType.Warning, $"[SpaceElevatorConnect] 接続要求を拒否: {error}");
            var bytes = reason == PacketWaitCompletionReason.DeserializeFailed ? new byte[] { 0xc1 } : MessagePackSerializer.Serialize(new ConnectSpaceElevatorResponse(result));
            capture.Waiters.Single().Value.WaitSubject.OnNext((bytes, reason == PacketWaitCompletionReason.DeserializeFailed ? PacketWaitCompletionReason.Received : reason));
            // 未知の列挙値は契約違反として例外を保つ
            // Preserve contract violations as exceptions for unknown enum values
            if (!Enum.IsDefined(typeof(SpaceElevatorConnectResult), result) || !Enum.IsDefined(typeof(PacketWaitCompletionReason), reason))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => pending.GetAwaiter().GetResult());
                return;
            }
            var actual = pending.GetAwaiter().GetResult();
            Assert.AreEqual(error == null, actual.Ok);
            Assert.AreEqual(error, actual.Error);
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

            // 非Receivedへ壊れた本文を添えても復号せず理由だけを返す
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

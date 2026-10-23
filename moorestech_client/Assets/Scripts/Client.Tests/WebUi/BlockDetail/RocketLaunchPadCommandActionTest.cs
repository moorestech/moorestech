using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Client.Tests.Common;
using Client.Tests.PlaceSystem.TrainCostIntegration;
using Core.Master;
using Game.Block.Interface;
using MessagePack;
using Server.Boot;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol;
using System.Runtime.Serialization;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUi.BlockDetail
{
    public class RocketLaunchPadCommandActionTest
    {
        private VanillaApi _previousApi;
        private GameObject _owner;
        [SetUp] public void RememberContext() => _previousApi = ClientContext.VanillaApi;
        [TearDown] public void RestoreContext()
        {
            TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", _previousApi);
            if (_owner != null) Object.DestroyImmediate(_owner);
        }
        // 不正入力を既存エラーと警告で拒否
        // Reject invalid input with the established error code and a warning
        [UnityTest]
        public IEnumerator InvalidCommandsAreRejectedBeforeReadingGameState() => UniTask.ToCoroutine(async () =>
        {
            var handler = new RocketLaunchPadCommandActionHandler(null);
            var inputs = new[] { null, "{}", "{\"command\":null}", "{\"command\":3}", "{\"command\":{}}", "{\"command\":\"Build\"}", "{\"command\":\"other\"}" };
            foreach (var json in inputs)
            {
                LogAssert.Expect(LogType.Warning, "[RocketLaunchPadCommandAction] invalid_payload: command must be build or launch");
                var result = await handler.ExecuteAsync(json == null ? null : JObject.Parse(json));
                Assert.IsFalse(result.Ok);
                Assert.AreEqual("invalid_payload", result.Error);
            }
        });

        [UnityTest]
        public IEnumerator ValidCommandsRequireAnOpenBlock() => UniTask.ToCoroutine(async () =>
        {
            // ネットワーク購読を起動せず、未開状態だけを構成する
            // Construct only the closed state without starting a network subscription
            var state = (SubInventoryState)FormatterServices.GetUninitializedObject(typeof(SubInventoryState));
            var handler = new RocketLaunchPadCommandActionHandler(state);
            foreach (var command in new[] { "build", "launch" })
            {
                LogAssert.Expect(LogType.Warning, "[RocketLaunchPadCommandAction] block_not_open: an open RocketLaunchPad block is required");
                var result = await handler.ExecuteAsync(new JObject { ["command"] = command });
                Assert.IsFalse(result.Ok);
                Assert.AreEqual("block_not_open", result.Error);
            }
        });
        [TestCase("build", RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.None, null)]
        [TestCase("launch", RocketLaunchPadCommandOperation.Launch, RocketLaunchPadCommandFailureReason.None, null)]
        [TestCase("build", RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.MissingMaterials, "rocket_command_missing_materials")]
        [TestCase("build", RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.MissingPayload, "rocket_command_missing_payload")]
        [TestCase("build", RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.OutputFull, "rocket_command_output_full")]
        [TestCase("launch", RocketLaunchPadCommandOperation.Launch, RocketLaunchPadCommandFailureReason.NotAwaitingLaunchCommand, "rocket_command_not_awaiting_launch")]
        [TestCase("build", RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.InstanceMismatch, "rocket_command_instance_mismatch")]
        public void OpenPadCommandCrossesRealSendBoundary(string command, RocketLaunchPadCommandOperation operation, RocketLaunchPadCommandFailureReason failure, string errorCode)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            using var capture = new PlacementPacketCapture();
            var owner = _owner = new GameObject("command-test");
            var block = owner.AddComponent<BlockGameObject>();
            var store = owner.AddComponent<BlockGameObjectDataStore>();
            var position = new Vector3Int(7, 0, 9);
            var instanceId = new BlockInstanceId(123);
            TestReflection.SetField(block, "<BlockMasterElement>k__BackingField", MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.RocketLaunchPadId));
            TestReflection.SetField(block, "<BlockInstanceId>k__BackingField", instanceId);
            TestReflection.SetField(store, "_blockObjectsDictionary", new Dictionary<Vector3Int, BlockGameObject> { [position] = block });
            var state = (SubInventoryState)FormatterServices.GetUninitializedObject(typeof(SubInventoryState));
            TestReflection.SetField(state, "<CurrentSubInventorySource>k__BackingField", new BlockSubInventorySource(store, position));
            TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", capture.Api);

            // 実送信の位置・操作・個体を復号
            // Decode position, operation and identity from the real handler/API request
            var pending = new RocketLaunchPadCommandActionHandler(state).ExecuteAsync(new JObject { ["command"] = command });
            var request = MessagePackSerializer.Deserialize<RocketLaunchPadCommandRequest>(capture.ReadRequest());
            Assert.AreEqual(position, request.Position.Vector3Int);
            Assert.AreEqual(operation, request.Operation);
            Assert.AreEqual(instanceId.AsPrimitive(), request.ExpectedInstanceId);
            if (errorCode != null) LogAssert.Expect(LogType.Warning, $"[RocketLaunchPadCommandAction] {errorCode}: server rejected command: {failure}");
            capture.Waiters.Single().Value.WaitSubject.OnNext((MessagePackSerializer.Serialize(new RocketLaunchPadCommandResponse(errorCode == null, failure)), PacketWaitCompletionReason.Received));
            var result = pending.GetAwaiter().GetResult();
            Assert.AreEqual(errorCode == null, result.Ok);
            Assert.AreEqual(errorCode, result.Error);
        }
    }
}

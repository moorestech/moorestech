using System.Linq;
using Client.Game.InGame.Context;
using Client.Network.API;
using Client.Tests.Common;
using Client.Tests.PlaceSystem.TrainCostIntegration;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special.Commands;
using Game.Block.Interface;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using static Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol;

namespace Client.Tests.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 本物の送り手が発射台の合図を位置・操作・期待個体 ID 付きで送り、拒否理由を結果の種類とコードへ写すことを確かめる（L0）
    /// Verifies the production sender sends the launch pad command with position, operation and expected instance ID, and maps rejections to outcome kind and code (L0)
    /// </summary>
    public class VanillaSpecialBlockRequestSenderRocketTest
    {
        private VanillaApi _previousApi;

        [SetUp]
        public void SetUp()
        {
            _previousApi = ClientContext.VanillaApi;
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", _previousApi);
        }

        [TestCase(RocketCommand.Build, RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.None, SpecialBlockRequestOutcomeKind.Accepted, "")]
        [TestCase(RocketCommand.Launch, RocketLaunchPadCommandOperation.Launch, RocketLaunchPadCommandFailureReason.None, SpecialBlockRequestOutcomeKind.Accepted, "")]
        [TestCase(RocketCommand.Build, RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.MissingMaterials, SpecialBlockRequestOutcomeKind.Failed, "rocket_command_missing_materials")]
        [TestCase(RocketCommand.Build, RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.MissingPayload, SpecialBlockRequestOutcomeKind.Failed, "rocket_command_missing_payload")]
        [TestCase(RocketCommand.Build, RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.OutputFull, SpecialBlockRequestOutcomeKind.Failed, "rocket_command_output_full")]
        [TestCase(RocketCommand.Launch, RocketLaunchPadCommandOperation.Launch, RocketLaunchPadCommandFailureReason.NotAwaitingLaunchCommand, SpecialBlockRequestOutcomeKind.Stale, "rocket_command_not_awaiting_launch")]
        [TestCase(RocketCommand.Build, RocketLaunchPadCommandOperation.Build, RocketLaunchPadCommandFailureReason.InstanceMismatch, SpecialBlockRequestOutcomeKind.Failed, "rocket_command_instance_mismatch")]
        public void 合図は位置と操作と期待個体IDを載せて送り拒否理由を結果へ写す(RocketCommand command, RocketLaunchPadCommandOperation operation, RocketLaunchPadCommandFailureReason failure, SpecialBlockRequestOutcomeKind expectedKind, string expectedCode)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            using var capture = new PlacementPacketCapture();
            TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", capture.Api);
            var sender = new VanillaSpecialBlockRequestSender(new BlockNetworkInfoCache(), new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed)));
            var position = new Vector3Int(7, 0, 9);
            var instanceId = new BlockInstanceId(123);

            // 実送信の位置・操作・個体を復号する
            // Decode position, operation and identity from the real request
            var pending = sender.SendRocketCommand(position, command, instanceId);
            var request = MessagePackSerializer.Deserialize<RocketLaunchPadCommandRequest>(capture.ReadRequest());
            Assert.AreEqual(position, request.Position.Vector3Int);
            Assert.AreEqual(operation, request.Operation);
            Assert.AreEqual(instanceId.AsPrimitive(), request.ExpectedInstanceId);

            // 応答を返して結果の写しを見る
            // Reply and check the mapped outcome
            capture.Waiters.Single().Value.WaitSubject.OnNext((MessagePackSerializer.Serialize(new RocketLaunchPadCommandResponse(failure == RocketLaunchPadCommandFailureReason.None, failure)), PacketWaitCompletionReason.Received));
            var outcome = pending.GetAwaiter().GetResult();
            Assert.AreEqual(expectedKind, outcome.Kind);
            Assert.AreEqual(expectedCode, outcome.WireCode);
        }
    }
}

using System;
using Client.Network.API;
using Client.Ui.State.BlockInventory.Special.Commands;
using Game.Block.Interface.Component;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using FailureReason = Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 通信結果とサーバーの拒否理由を Web の橋と同じ失敗コードへ写し、Web の BENIGN_ERRORS と同じものだけを古い操作（良性）にする
    /// Maps transport results and server refusals to the Web bridge failure codes; only Web BENIGN_ERRORS become stale (benign)
    /// </summary>
    public class SpecialBlockResponseCodesTest
    {
        [Test]
        public void 届かなかった応答は時間切れと壊れた応答を分ける()
        {
            Assert.AreEqual("connect_timeout", SpecialBlockResponseCodes.FromWait(PacketWaitCompletionReason.Timeout, "connect_timeout", "connect_invalid_response").WireCode);
            Assert.AreEqual("connect_invalid_response", SpecialBlockResponseCodes.FromWait(PacketWaitCompletionReason.DeserializeFailed, "connect_timeout", "connect_invalid_response").WireCode);
            Assert.AreEqual(SpecialBlockRequestOutcomeKind.Failed, SpecialBlockResponseCodes.FromWait(PacketWaitCompletionReason.Timeout, "a", "b").Kind);
        }

        [TestCase(SpaceElevatorConnectResult.Connected, SpecialBlockRequestOutcomeKind.Accepted, "")]
        [TestCase(SpaceElevatorConnectResult.AlreadyConnected, SpecialBlockRequestOutcomeKind.Stale, "already_connected")]
        [TestCase(SpaceElevatorConnectResult.InsufficientCable, SpecialBlockRequestOutcomeKind.Failed, "insufficient_cable")]
        [TestCase(SpaceElevatorConnectResult.InstanceMismatch, SpecialBlockRequestOutcomeKind.Failed, "connect_instance_mismatch")]
        public void 接続の結果を写す(SpaceElevatorConnectResult result, SpecialBlockRequestOutcomeKind kind, string wire)
        {
            var outcome = SpecialBlockResponseCodes.FromConnectResult(result);
            Assert.AreEqual(kind, outcome.Kind);
            Assert.AreEqual(wire, outcome.WireCode);
        }

        [Test]
        public void 合図の拒否はどの理由もコードを持ち待ち以外の2つだけが古い操作()
        {
            foreach (FailureReason reason in Enum.GetValues(typeof(FailureReason)))
            {
                // None は成功の応答だけが持つ（Success=true）ので写さない
                // None only appears on a successful response (Success=true), so it is not mapped
                if (reason == FailureReason.None) continue;
                var outcome = SpecialBlockResponseCodes.FromRocketFailure(reason);
                StringAssert.StartsWith("rocket_command_", outcome.WireCode, reason.ToString());
                var stale = reason is FailureReason.NotAwaitingBuildCommand or FailureReason.NotAwaitingLaunchCommand;
                Assert.AreEqual(stale ? SpecialBlockRequestOutcomeKind.Stale : SpecialBlockRequestOutcomeKind.Failed, outcome.Kind, reason.ToString());
            }
        }
    }
}

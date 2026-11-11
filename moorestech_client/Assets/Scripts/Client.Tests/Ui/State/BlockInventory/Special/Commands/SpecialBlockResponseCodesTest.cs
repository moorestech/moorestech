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
    /// 失敗コードの写像と良性判定を確かめる
    /// Verifies failure code mapping and benign classification
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
                // None は成功応答のみなので除外
                // None only appears on success, so it is excluded
                if (reason == FailureReason.None) continue;
                var outcome = SpecialBlockResponseCodes.FromRocketFailure(reason);
                StringAssert.StartsWith("rocket_command_", outcome.WireCode, reason.ToString());
                var stale = reason is FailureReason.NotAwaitingBuildCommand or FailureReason.NotAwaitingLaunchCommand;
                Assert.AreEqual(stale ? SpecialBlockRequestOutcomeKind.Stale : SpecialBlockRequestOutcomeKind.Failed, outcome.Kind, reason.ToString());
            }
        }
    }
}

using System;
using Client.Network.API;
using Client.Ui.State.BlockInventory.Special.Commands;
using Game.Block.Interface.Component;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using FailureReason = Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason;
using CargoFailure = Server.Protocol.PacketResponse.OrbitalCargoSelectionProtocol.OrbitalCargoSelectionFailureReason;

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
        [TestCase(SpaceElevatorConnectResult.CargoNotSelected, SpecialBlockRequestOutcomeKind.Failed, "connect_cargo_not_selected")]
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

        [Test]
        public void 積荷を選んでいない合図の拒否は失敗のコードになる()
        {
            // 未選択は古い操作ではなく、選べば通る失敗として出す
            // Not-selected is a failure the player fixes by selecting, not a stale command
            var outcome = SpecialBlockResponseCodes.FromRocketFailure(FailureReason.CargoNotSelected);
            Assert.AreEqual(SpecialBlockRequestOutcomeKind.Failed, outcome.Kind);
            Assert.AreEqual("rocket_command_cargo_not_selected", outcome.WireCode);
        }

        [Test]
        public void 積荷の選択の拒否はどの理由も専用のコードを持つ失敗になる()
        {
            foreach (CargoFailure reason in Enum.GetValues(typeof(CargoFailure)))
            {
                // None は成功応答のみなので除外
                // None only appears on success, so it is excluded
                if (reason == CargoFailure.None) continue;
                var outcome = SpecialBlockResponseCodes.FromCargoSelectionFailure(reason);
                StringAssert.StartsWith("cargo_select_", outcome.WireCode, reason.ToString());
                Assert.AreEqual(SpecialBlockRequestOutcomeKind.Failed, outcome.Kind, reason.ToString());
            }
            Assert.AreEqual("cargo_select_locked", SpecialBlockResponseCodes.FromCargoSelectionFailure(CargoFailure.Locked).WireCode);
        }
    }
}

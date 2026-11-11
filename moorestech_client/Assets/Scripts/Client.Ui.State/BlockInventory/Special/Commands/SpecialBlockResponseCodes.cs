using System;
using Client.Network.API;
using Game.Block.Interface.Component;
using Server.Protocol.PacketResponse;
using FailureReason = Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 通信結果・サーバーの拒否理由を Web の橋（SpaceElevatorConnectActionHandler・RocketLaunchPadCommandActionHandler）と同じ失敗コードへ写す。Web の BENIGN_ERRORS のコードだけが古い操作
    /// Maps transport results and server refusals to the same failure codes as the Web bridge; only Web BENIGN_ERRORS codes are stale
    /// </summary>
    public static class SpecialBlockResponseCodes
    {
        public static SpecialBlockRequestOutcome FromWait(PacketWaitCompletionReason reason, string timeoutCode, string invalidResponseCode)
        {
            return reason switch
            {
                PacketWaitCompletionReason.Timeout => SpecialBlockRequestOutcome.Failed(timeoutCode),
                PacketWaitCompletionReason.DeserializeFailed => SpecialBlockRequestOutcome.Failed(invalidResponseCode),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "受信済みは応答の中身で写す"),
            };
        }

        public static SpecialBlockRequestOutcome FromConnectResult(SpaceElevatorConnectResult result)
        {
            return result switch
            {
                SpaceElevatorConnectResult.Connected => SpecialBlockRequestOutcome.Accepted,
                SpaceElevatorConnectResult.AlreadyConnected => SpecialBlockRequestOutcome.Stale("already_connected"),
                SpaceElevatorConnectResult.InsufficientCable => SpecialBlockRequestOutcome.Failed("insufficient_cable"),
                SpaceElevatorConnectResult.NotSpaceElevatorBase => SpecialBlockRequestOutcome.Failed("not_space_elevator_base"),
                SpaceElevatorConnectResult.InvalidRequest => SpecialBlockRequestOutcome.Failed("connect_invalid_request"),
                SpaceElevatorConnectResult.BlockNotFound => SpecialBlockRequestOutcome.Failed("connect_block_not_found"),
                SpaceElevatorConnectResult.InstanceMismatch => SpecialBlockRequestOutcome.Failed("connect_instance_mismatch"),
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, "未知の接続結果"),
            };
        }

        public static SpecialBlockRequestOutcome FromRocketFailure(FailureReason reason)
        {
            return reason switch
            {
                FailureReason.BlockNotFound => SpecialBlockRequestOutcome.Failed("rocket_command_block_not_found"),
                FailureReason.NotRocketLaunchPad => SpecialBlockRequestOutcome.Failed("rocket_command_not_launch_pad"),
                FailureReason.UnknownOperation => SpecialBlockRequestOutcome.Failed("rocket_command_unknown_operation"),
                FailureReason.InvalidRequest => SpecialBlockRequestOutcome.Failed("rocket_command_invalid_request"),
                FailureReason.NotAwaitingBuildCommand => SpecialBlockRequestOutcome.Stale("rocket_command_not_awaiting_build"),
                FailureReason.NotAwaitingLaunchCommand => SpecialBlockRequestOutcome.Stale("rocket_command_not_awaiting_launch"),
                FailureReason.MissingMaterials => SpecialBlockRequestOutcome.Failed("rocket_command_missing_materials"),
                FailureReason.MissingPayload => SpecialBlockRequestOutcome.Failed("rocket_command_missing_payload"),
                FailureReason.OutputFull => SpecialBlockRequestOutcome.Failed("rocket_command_output_full"),
                FailureReason.InstanceMismatch => SpecialBlockRequestOutcome.Failed("rocket_command_instance_mismatch"),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "未知の合図の拒否理由"),
            };
        }
    }
}

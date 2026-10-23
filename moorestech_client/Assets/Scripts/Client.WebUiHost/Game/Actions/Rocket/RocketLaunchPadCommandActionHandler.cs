using System;
using System.Threading;
using Client.Network.API;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 開いている発射台へ建造・発射の合図を送る
    /// Sends a build or launch command to the open rocket launch pad
    /// </summary>
    public class RocketLaunchPadCommandActionHandler : IActionHandler
    {
        public string ActionType => "rocket_launch_pad.command";

        private readonly SubInventoryState _subInventoryState;

        public RocketLaunchPadCommandActionHandler(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return Reject("invalid_payload", "command must be build or launch");
            var operation = ParseOperation(payload["command"]);
            if (operation == null) return Reject("invalid_payload", "command must be build or launch");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return Reject("block_not_open", "an open RocketLaunchPad block is required");
            if (!source.TryGetBlockIdentity(out _, out var blockTypeName) || blockTypeName != "RocketLaunchPad") return Reject("block_not_open", "an open RocketLaunchPad block is required");

            if (!source.TryGetBlockInstanceId(out var instanceId)) return Reject("block_not_open", "block instance is no longer available");

            // 表示の正本は後続のブロック状態。ここでは受理されたかだけ返す
            // The later block state is the display source of truth; only acceptance is reported here
            var (response, reason) = await ClientContext.VanillaApi.Response.Block.SendRocketLaunchPadCommand(source.BlockPosition, operation.Value, instanceId, CancellationToken.None);
            if (reason != PacketWaitCompletionReason.Received) return Reject(reason switch
            {
                PacketWaitCompletionReason.Timeout => "rocket_command_timeout",
                PacketWaitCompletionReason.DeserializeFailed => "rocket_command_invalid_response",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "未知の通信結果"),
            }, "no command response received");
            if (!response.Success) return Reject(response.FailureReason switch
            {
                RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason.BlockNotFound => "rocket_command_block_not_found",
                RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason.NotRocketLaunchPad => "rocket_command_not_launch_pad",
                RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason.UnknownOperation => "rocket_command_unknown_operation",
                RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason.InvalidRequest => "rocket_command_invalid_request",
                RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason.NotAwaitingBuildCommand => "rocket_command_not_awaiting_build",
                RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason.NotAwaitingLaunchCommand => "rocket_command_not_awaiting_launch",
                RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason.MissingMaterials => "rocket_command_missing_materials",
                RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason.MissingPayload => "rocket_command_missing_payload",
                RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason.OutputFull => "rocket_command_output_full",
                RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason.InstanceMismatch => "rocket_command_instance_mismatch",
                _ => throw new ArgumentOutOfRangeException(nameof(response.FailureReason), response.FailureReason, "未知の拒否理由"),
            }, $"server rejected command: {response.FailureReason}");
            return ActionResult.Success();

            #region Internal

            ActionResult Reject(string error, string reason)
            {
                Debug.LogWarning($"[RocketLaunchPadCommandAction] {error}: {reason}");
                return ActionResult.Fail(error);
            }

            RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation? ParseOperation(JToken token)
            {
                if (token is not JValue { Type: JTokenType.String } value) return null;
                return (string)value switch
                {
                    "build" => RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation.Build,
                    "launch" => RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation.Launch,
                    _ => null,
                };
            }

            #endregion
        }

    }
}

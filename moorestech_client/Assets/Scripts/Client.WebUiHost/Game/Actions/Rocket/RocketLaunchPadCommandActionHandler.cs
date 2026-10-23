using System.Threading;
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

            // 表示の正本は後続のブロック状態。ここでは受理されたかだけ返す
            // The later block state is the display source of truth; only acceptance is reported here
            var response = await ClientContext.VanillaApi.Response.Block.SendRocketLaunchPadCommand(source.BlockPosition, operation.Value, CancellationToken.None);
            if (response == null) return Reject("invalid_state", "no command response received");
            if (!response.Success) return Reject("invalid_state", $"server rejected command: {response.FailureReason}");
            return ActionResult.Success();
        }

        private static ActionResult Reject(string error, string reason)
        {
            Debug.LogWarning($"[RocketLaunchPadCommandAction] {error}: {reason}");
            return ActionResult.Fail(error);
        }

        private static RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation? ParseOperation(JToken token)
        {
            if (token is not JValue { Type: JTokenType.String } value) return null;
            return (string)value switch
            {
                "build" => RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation.Build,
                "launch" => RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation.Launch,
                _ => null,
            };
        }
    }
}

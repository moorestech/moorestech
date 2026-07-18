using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 開いている列車プラットフォームの転送モードを目標値で設定する
    /// Sets the open train platform's transfer mode to an explicit target
    /// </summary>
    public class TrainPlatformSetTransferModeActionHandler : IActionHandler
    {
        public string ActionType => "train_platform.set_transfer_mode";

        private readonly SubInventoryState _subInventoryState;

        public TrainPlatformSetTransferModeActionHandler(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            var mode = ParseMode(payload["mode"]);
            if (mode == null) return ActionResult.Fail("invalid_mode");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source)
                return ActionResult.Fail("block_not_open");
            if (!IsTrainPlatform(source.BlockTypeName)) return ActionResult.Fail("invalid_block_type");

            // StateDetailを表示の正本にする
            // Send the target through the existing protocol and keep the later StateDetail as the view source of truth
            var response = await ClientContext.VanillaApi.Response.SetTrainPlatformTransferMode(
                source.BlockPosition,
                mode.Value,
                CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail("mode_request_failed");
            return ActionResult.Success();
        }

        private static TrainPlatformTransferComponent.TransferMode? ParseMode(JToken token)
        {
            if (token is not JValue { Type: JTokenType.String } value) return null;
            return (string)value switch
            {
                "loadToTrain" => TrainPlatformTransferComponent.TransferMode.LoadToTrain,
                "unloadToPlatform" => TrainPlatformTransferComponent.TransferMode.UnloadToPlatform,
                _ => null,
            };
        }

        private static bool IsTrainPlatform(string blockType)
        {
            return blockType is "TrainStation" or "TrainItemPlatform" or "TrainFluidPlatform";
        }
    }
}

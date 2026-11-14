using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.Train.Platform;
using Cysharp.Threading.Tasks;
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
            if (!TrainPlatformTransferModeWire.TryParse(payload["mode"], out var mode)) return ActionResult.Fail("invalid_mode");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source)
                return ActionResult.Fail("block_not_open");
            if (!source.TryGetBlockIdentity(out _, out var blockTypeName)) return ActionResult.Fail("block_not_open");
            if (!IsTrainPlatform(blockTypeName)) return ActionResult.Fail("invalid_block_type");

            // StateDetailを表示の正本にする
            // Send the target through the existing protocol and keep the later StateDetail as the view source of truth
            var response = await ClientContext.VanillaApi.Response.Train.SetTrainPlatformTransferMode(
                source.BlockPosition,
                TrainPlatformTransferModeConversion.ToServer(mode),
                CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail("mode_request_failed");
            return ActionResult.Success();
        }

        private static bool IsTrainPlatform(string blockType)
        {
            return blockType is "TrainStation" or "TrainItemPlatform" or "TrainFluidPlatform";
        }
    }
}

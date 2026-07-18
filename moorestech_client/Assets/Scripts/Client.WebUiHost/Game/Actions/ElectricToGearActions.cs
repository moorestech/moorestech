using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 回転生成機の出力モードを設定する
    /// electric_to_gear.set_output_mode: explicitly selects the output mode of the open converter
    /// </summary>
    public class ElectricToGearSetOutputModeActionHandler : IActionHandler
    {
        public string ActionType => "electric_to_gear.set_output_mode";

        private readonly SubInventoryState _subInventoryState;

        public ElectricToGearSetOutputModeActionHandler(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["modeIndex"] is not JValue { Value: long modeIndex }) return ActionResult.Fail("invalid_mode_index");
            if (modeIndex < 0 || int.MaxValue < modeIndex) return ActionResult.Fail("invalid_mode_index");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return ActionResult.Fail("block_not_open");
            if (source.BlockTypeName != "ElectricToGearGenerator") return ActionResult.Fail("invalid_block_type");

            // 既存protocolで設定する
            // Send the target index through the existing protocol; the later StateDetail topic updates the view
            var response = await ClientContext.VanillaApi.Response.SetElectricToGearOutputMode(
                source.BlockPosition,
                (int)modeIndex,
                CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail("mode_request_failed");
            return ActionResult.Success();
        }
    }
}

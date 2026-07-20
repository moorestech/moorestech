using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.FilterSplitter;
using Newtonsoft.Json.Linq;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// filter_splitter.set_mode: 開いているフィルタ分岐器のモードを明示指定で変更する（冪等）
    /// filter_splitter.set_mode: sets the open filter splitter's mode explicitly (idempotent)
    /// </summary>
    public class FilterSplitterSetModeActionHandler : IActionHandler
    {
        public string ActionType => "filter_splitter.set_mode";

        private readonly SubInventoryState _subInventoryState;
        private readonly BlockInventoryTopic _blockInventoryTopic;

        public FilterSplitterSetModeActionHandler(SubInventoryState subInventoryState, BlockInventoryTopic blockInventoryTopic)
        {
            _subInventoryState = subInventoryState;
            _blockInventoryTopic = blockInventoryTopic;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["directionIndex"] is not JValue { Value: long dirLong }) return ActionResult.Fail("invalid_direction");
            var mode = ParseMode(payload["mode"]);
            if (mode == null) return ActionResult.Fail("invalid_payload");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return ActionResult.Fail("block_not_open");

            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(source.BlockPosition, (int)dirLong, mode.Value);
            var response = await ClientContext.VanillaApi.Response.SendFilterSplitterStateRequest(request, CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail("filter_request_failed");

            // 応答スナップショットをキャッシュへ反映し topic を再配信する（D2: state は topic 一本）
            // Apply the response snapshot to the cache and republish the topic (D2: state flows only via topics)
            _blockInventoryTopic.NetworkCache.ApplyFilterSplitterSnapshot(response);
            return ActionResult.Success();
        }

        private static FilterSplitterMode? ParseMode(JToken token)
        {
            if (token is not JValue { Type: JTokenType.String } value) return null;
            return (string)value switch
            {
                "default" => FilterSplitterMode.Default,
                "whitelist" => FilterSplitterMode.Whitelist,
                "blacklist" => FilterSplitterMode.Blacklist,
                _ => null,
            };
        }
    }

    /// <summary>
    /// filter_splitter.set_filter_item: clear=false は Grab の持ち手アイテムを設定、clear=true は解除
    /// filter_splitter.set_filter_item: clear=false assigns the grabbed item, clear=true clears the slot
    /// </summary>
    public class FilterSplitterSetFilterItemActionHandler : IActionHandler
    {
        public string ActionType => "filter_splitter.set_filter_item";

        private readonly SubInventoryState _subInventoryState;
        private readonly LocalPlayerInventoryController _controller;
        private readonly BlockInventoryTopic _blockInventoryTopic;

        public FilterSplitterSetFilterItemActionHandler(SubInventoryState subInventoryState, LocalPlayerInventoryController controller, BlockInventoryTopic blockInventoryTopic)
        {
            _subInventoryState = subInventoryState;
            _controller = controller;
            _blockInventoryTopic = blockInventoryTopic;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["directionIndex"] is not JValue { Value: long dirLong }) return ActionResult.Fail("invalid_direction");
            if (payload["slotIndex"] is not JValue { Value: long slotLong }) return ActionResult.Fail("invalid_slot");
            if (payload["clear"] is not JValue { Type: JTokenType.Boolean } clearValue) return ActionResult.Fail("invalid_payload");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return ActionResult.Fail("block_not_open");

            // uGUI FilterSplitterBlockInventoryView.cs:132-146 と同じ: 設定は Grab の現アイテム、解除は EmptyItemId
            // Same as uGUI FilterSplitterBlockInventoryView.cs:132-146: assign the grabbed item, or EmptyItemId to clear
            var itemId = (bool)clearValue.Value ? ItemMaster.EmptyItemId : _controller.GrabInventory.Id;
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(source.BlockPosition, (int)dirLong, (int)slotLong, itemId);
            var response = await ClientContext.VanillaApi.Response.SendFilterSplitterStateRequest(request, CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail("filter_request_failed");

            _blockInventoryTopic.NetworkCache.ApplyFilterSplitterSnapshot(response);
            return ActionResult.Success();
        }
    }
}

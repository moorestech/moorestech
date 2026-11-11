using System.Threading;
using Client.Game.InGame.Context;
using Client.Network.API;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Source;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;
using Operation = Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// VanillaApi で送り応答を失敗コードへ写す
    /// Sends via VanillaApi and maps responses to failure codes
    /// </summary>
    public sealed class VanillaSpecialBlockRequestSender : ISpecialBlockRequestSender
    {
        private readonly BlockNetworkInfoCache _networkCache;
        private readonly IBlockInventoryPanelSource _panels;

        public VanillaSpecialBlockRequestSender(BlockNetworkInfoCache networkCache, IBlockInventoryPanelSource panels)
        {
            _networkCache = networkCache;
            _panels = panels;
        }

        public async UniTask<SpecialBlockRequestOutcome> SetFilterMode(Vector3Int position, BlockInstanceId instanceId, int directionIndex, FilterSplitterMode mode)
        {
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(position, directionIndex, SpecialNetworkReader.ToServerMode(mode));
            return ApplyFilterResponse(await ClientContext.VanillaApi.Response.Block.SendFilterSplitterStateRequest(request, CancellationToken.None), instanceId);
        }

        public async UniTask<SpecialBlockRequestOutcome> SetFilterItem(Vector3Int position, BlockInstanceId instanceId, int directionIndex, int slotIndex, ItemId itemId)
        {
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(position, directionIndex, slotIndex, itemId);
            return ApplyFilterResponse(await ClientContext.VanillaApi.Response.Block.SendFilterSplitterStateRequest(request, CancellationToken.None), instanceId);
        }

        public async UniTask<SpecialBlockRequestOutcome> SetElectricToGearMode(Vector3Int position, int modeIndex)
        {
            var response = await ClientContext.VanillaApi.Response.Block.SetElectricToGearOutputMode(position, modeIndex, CancellationToken.None);
            return response != null && response.Success ? SpecialBlockRequestOutcome.Accepted : SpecialBlockRequestOutcome.Failed("mode_request_failed");
        }

        public async UniTask<SpecialBlockRequestOutcome> ConnectSpaceElevator(Vector3Int position, BlockInstanceId instanceId)
        {
            var (response, reason) = await ClientContext.VanillaApi.Response.Block.ConnectSpaceElevator(position, instanceId, CancellationToken.None);
            if (reason != PacketWaitCompletionReason.Received) return SpecialBlockResponseCodes.FromWait(reason, "connect_timeout", "connect_invalid_response");
            return response == null ? SpecialBlockRequestOutcome.Failed("connect_invalid_response") : SpecialBlockResponseCodes.FromConnectResult(response.Result);
        }

        public async UniTask<SpecialBlockRequestOutcome> SendRocketCommand(Vector3Int position, RocketCommand command, BlockInstanceId instanceId)
        {
            var operation = command == RocketCommand.Build ? Operation.Build : Operation.Launch;
            var (response, reason) = await ClientContext.VanillaApi.Response.Block.SendRocketLaunchPadCommand(position, operation, instanceId, CancellationToken.None);
            if (reason != PacketWaitCompletionReason.Received) return SpecialBlockResponseCodes.FromWait(reason, "rocket_command_timeout", "rocket_command_invalid_response");
            if (response == null) return SpecialBlockRequestOutcome.Failed("rocket_command_invalid_response");
            return response.Success ? SpecialBlockRequestOutcome.Accepted : SpecialBlockResponseCodes.FromRocketFailure(response.FailureReason);
        }

        internal SpecialBlockRequestOutcome ApplyFilterResponse(FilterSplitterStateProtocol.FilterSplitterStateResponse response, BlockInstanceId sentInstanceId)
        {
            // 受理した応答は、送った分配器が今も開いているときだけ集約へ当てる。別のブロックに当てると本文に他個体の設定が残る
            // Apply an accepted response only while the splitter it was sent for is still open; applying it to another block leaves foreign settings in the body
            if (response == null || !response.Success) return SpecialBlockRequestOutcome.Failed("filter_request_failed");
            if (!IsStillOpen(_panels.Current.Value, sentInstanceId))
            {
                Debug.LogWarning($"分配器の応答を捨てました（送信後に開いているブロックが変わった）/ Dropped the splitter response because the open block changed after sending: {sentInstanceId}");
                return SpecialBlockRequestOutcome.Accepted;
            }
            _networkCache.ApplyFilterSplitterSnapshot(response);
            return SpecialBlockRequestOutcome.Accepted;
        }

        internal static bool IsStillOpen(BlockInventoryPanelState current, BlockInstanceId sentInstanceId)
        {
            return current is OpenBlockInventoryPanel { Error: BlockInventoryPanelError.None } open && open.BlockInstanceId == sentInstanceId;
        }
    }
}

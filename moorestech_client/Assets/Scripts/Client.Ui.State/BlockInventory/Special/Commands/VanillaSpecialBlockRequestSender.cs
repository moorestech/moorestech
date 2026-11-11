using System.Threading;
using Client.Game.InGame.Context;
using Client.Network.API;
using Client.Ui.State.BlockDetail;
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
    /// ClientContext.VanillaApi で送り、応答を Web の橋と同じ失敗コードへ写す。分配器の応答は集約キャッシュへ当てる（Web FilterSplitter*ActionHandler と同じ）
    /// Sends through ClientContext.VanillaApi and maps responses to the Web bridge codes; splitter responses are applied to the aggregate cache (same as the Web handlers)
    /// </summary>
    public sealed class VanillaSpecialBlockRequestSender : ISpecialBlockRequestSender
    {
        private readonly BlockNetworkInfoCache _networkCache;

        public VanillaSpecialBlockRequestSender(BlockNetworkInfoCache networkCache)
        {
            _networkCache = networkCache;
        }

        public async UniTask<SpecialBlockRequestOutcome> SetFilterMode(Vector3Int position, int directionIndex, FilterSplitterMode mode)
        {
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(position, directionIndex, SpecialNetworkReader.ToServerMode(mode));
            return ApplyFilterResponse(await ClientContext.VanillaApi.Response.Block.SendFilterSplitterStateRequest(request, CancellationToken.None));
        }

        public async UniTask<SpecialBlockRequestOutcome> SetFilterItem(Vector3Int position, int directionIndex, int slotIndex, ItemId itemId)
        {
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(position, directionIndex, slotIndex, itemId);
            return ApplyFilterResponse(await ClientContext.VanillaApi.Response.Block.SendFilterSplitterStateRequest(request, CancellationToken.None));
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

        private SpecialBlockRequestOutcome ApplyFilterResponse(FilterSplitterStateProtocol.FilterSplitterStateResponse response)
        {
            // 受理した応答は最新の状態として集約へ当て、Model の組み直しを起こす
            // Apply an accepted response to the aggregate as the latest state, which triggers the model rebuild
            if (response == null || !response.Success) return SpecialBlockRequestOutcome.Failed("filter_request_failed");
            _networkCache.ApplyFilterSplitterSnapshot(response);
            return SpecialBlockRequestOutcome.Accepted;
        }
    }
}

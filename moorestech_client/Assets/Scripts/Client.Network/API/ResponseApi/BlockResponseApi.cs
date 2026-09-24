using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public static class BlockResponseApi
    {
        public static async UniTask<BlockStateMessagePack> GetBlockState(this VanillaApiWithResponse api, Vector3Int blockPos, CancellationToken ct)
        {
            var request = new BlockStateProtocol.RequestBlockStateProtocolMessagePack(blockPos);
            var response = await api.PacketExchange.GetPacketResponse<BlockStateProtocol.ResponseBlockStateProtocolMessagePack>(request, ct);

            return response.State;
        }

        public static async UniTask<RemoveBlockProtocol.RemoveBlockResponseMessagePack> BlockRemove(this VanillaApiWithResponse api, Vector3Int pos, CancellationToken ct)
        {
            var request = new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(api.ConnectionSetting.PlayerId, pos);
            return await api.PacketExchange.GetPacketResponse<RemoveBlockProtocol.RemoveBlockResponseMessagePack>(request, ct);
        }

        // 指定ブロックが属するギアネットワークの現時点の集約値を取得する
        // Fetch current aggregate info of the gear network that the given block belongs to
        public static async UniTask<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack> GetGearNetworkInfo(this VanillaApiWithResponse api, BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetGearNetworkInfoProtocol.RequestGetGearNetworkInfoMessagePack(blockInstanceId);
            return await api.PacketExchange.GetPacketResponse<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack>(request, ct);
        }

        // 指定ブロックが属する電力ネットワークの現時点の集約値を取得する
        // Fetch current aggregate info of the electric network that the given block belongs to
        public static async UniTask<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack> GetElectricNetworkInfo(this VanillaApiWithResponse api, BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetElectricNetworkInfoProtocol.RequestGetElectricNetworkInfoMessagePack(blockInstanceId);
            return await api.PacketExchange.GetPacketResponse<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack>(request, ct);
        }

        // ElectricToGear の出力モードを切り替える
        // Switch the output mode of an ElectricToGear block
        public static async UniTask<SetElectricToGearOutputModeResponse> SetElectricToGearOutputMode(
            this VanillaApiWithResponse api,
            Vector3Int position, int index, CancellationToken ct)
        {
            var request = new SetElectricToGearOutputModeRequest(position, index);
            return await api.PacketExchange.GetPacketResponse<SetElectricToGearOutputModeResponse>(request, ct);
        }

        // フィルター分岐器の状態取得・設定 (Get/SetMode/SetFilterItem を 1 メソッドで扱う)
        // Filter splitter state request (single endpoint for Get / SetMode / SetFilterItem)
        public static async UniTask<FilterSplitterStateProtocol.FilterSplitterStateResponse> SendFilterSplitterStateRequest(
            this VanillaApiWithResponse api,
            FilterSplitterStateProtocol.FilterSplitterStateRequest request, CancellationToken ct)
        {
            return await api.PacketExchange.GetPacketResponse<FilterSplitterStateProtocol.FilterSplitterStateResponse>(request, ct);
        }

        // SetRecipe/Clearを1メソッドで送信
        // Machine recipe selection request (single endpoint for SetRecipe / Clear)
        public static async UniTask<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse> SendMachineRecipeSelectionRequest(
            this VanillaApiWithResponse api,
            MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest request, CancellationToken ct)
        {
            return await api.PacketExchange.GetPacketResponse<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse>(request, ct);
        }

        // BP Create/GetAll/Deleteを1メソッドで統合
        // Blueprint request (single endpoint for Create / GetAll / Delete)
        public static async UniTask<BlueprintResponse> SendBlueprintRequest(this VanillaApiWithResponse api, BlueprintRequest request, CancellationToken ct)
        {
            return await api.PacketExchange.GetPacketResponse<BlueprintResponse>(request, ct);
        }
    }
}

using System.Threading;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public class BlockResponseApi
    {
        private readonly PacketExchangeManager _packetExchange;
        private readonly PlayerConnectionSetting _connectionSetting;

        public BlockResponseApi(PacketExchangeManager packetExchangeManager, PlayerConnectionSetting playerConnectionSetting)
        {
            _packetExchange = packetExchangeManager;
            _connectionSetting = playerConnectionSetting;
        }

        public async UniTask<BlockStateMessagePack> GetBlockState(Vector3Int blockPos, CancellationToken ct)
        {
            var request = new BlockStateProtocol.RequestBlockStateProtocolMessagePack(blockPos);
            var response = await _packetExchange.GetPacketResponse<BlockStateProtocol.ResponseBlockStateProtocolMessagePack>(request, ct);

            return response.State;
        }

        public async UniTask<RemoveBlockProtocol.RemoveBlockResponseMessagePack> BlockRemove(Vector3Int pos, CancellationToken ct)
        {
            var request = new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(_connectionSetting.PlayerId, pos);
            return await _packetExchange.GetPacketResponse<RemoveBlockProtocol.RemoveBlockResponseMessagePack>(request, ct);
        }

        // 指定ブロックが属するギアネットワークの現時点の集約値を取得する
        // Fetch current aggregate info of the gear network that the given block belongs to
        public async UniTask<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack> GetGearNetworkInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetGearNetworkInfoProtocol.RequestGetGearNetworkInfoMessagePack(blockInstanceId);
            return await _packetExchange.GetPacketResponse<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack>(request, ct);
        }

        // 指定ブロックが属する電力ネットワークの現時点の集約値を取得する
        // Fetch current aggregate info of the electric network that the given block belongs to
        public async UniTask<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack> GetElectricNetworkInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetElectricNetworkInfoProtocol.RequestGetElectricNetworkInfoMessagePack(blockInstanceId);
            return await _packetExchange.GetPacketResponse<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack>(request, ct);
        }

        // ElectricToGear の出力モードを切り替える
        // Switch the output mode of an ElectricToGear block
        public async UniTask<SetElectricToGearOutputModeResponse> SetElectricToGearOutputMode(Vector3Int position, int index, CancellationToken ct)
        {
            var request = new SetElectricToGearOutputModeRequest(position, index);
            return await _packetExchange.GetPacketResponse<SetElectricToGearOutputModeResponse>(request, ct);
        }

        // フィルター分岐器の状態取得・設定 (Get/SetMode/SetFilterItem を 1 メソッドで扱う)
        // Filter splitter state request (single endpoint for Get / SetMode / SetFilterItem)
        public async UniTask<FilterSplitterStateProtocol.FilterSplitterStateResponse> SendFilterSplitterStateRequest(
            FilterSplitterStateProtocol.FilterSplitterStateRequest request, CancellationToken ct)
        {
            return await _packetExchange.GetPacketResponse<FilterSplitterStateProtocol.FilterSplitterStateResponse>(request, ct);
        }

        // SetRecipe/Clearを1メソッドで送信
        // Machine recipe selection request (single endpoint for SetRecipe / Clear)
        public async UniTask<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse> SendMachineRecipeSelectionRequest(
            MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest request, CancellationToken ct)
        {
            return await _packetExchange.GetPacketResponse<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse>(request, ct);
        }

        // BP Create/GetAll/Deleteを1メソッドで統合
        // Blueprint request (single endpoint for Create / GetAll / Delete)
        public async UniTask<BlueprintResponse> SendBlueprintRequest(BlueprintRequest request, CancellationToken ct)
        {
            return await _packetExchange.GetPacketResponse<BlueprintResponse>(request, ct);
        }
    }
}

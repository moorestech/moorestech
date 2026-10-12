using System;
using System.Threading;
using Core.Master;
using Game.Block.Interface;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API.Requests
{
    public sealed class BlockRequestApi
    {
        private readonly PacketExchangeManager _packetExchangeManager;

        public BlockRequestApi(PacketExchangeManager packetExchangeManager)
        {
            _packetExchangeManager = packetExchangeManager;
        }

        public async UniTask<RemoveBlockProtocol.RemoveBlockResponseMessagePack> BlockRemove(Vector3Int pos, CancellationToken ct)
        {
            var request = new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(pos);
            return await _packetExchangeManager.GetPacketResponse<RemoveBlockProtocol.RemoveBlockResponseMessagePack>(request, ct);
        }

        public async UniTask<RemoveBlockProtocol.RemoveBlockResponseMessagePack> BlockRemoveIfInstance(
            Vector3Int pos, BlockInstanceId expectedInstanceId, CancellationToken ct)
        {
            var request = new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(pos, expectedInstanceId);
            return await _packetExchangeManager.GetPacketResponse<RemoveBlockProtocol.RemoveBlockResponseMessagePack>(request, ct);
        }

        // ElectricToGear の出力モードを切り替える
        // Switch the output mode of an ElectricToGear block
        public async UniTask<SetElectricToGearOutputModeResponse> SetElectricToGearOutputMode(
            Vector3Int position, int index, CancellationToken ct)
        {
            var request = new SetElectricToGearOutputModeRequest(position, index);
            return await _packetExchangeManager.GetPacketResponse<SetElectricToGearOutputModeResponse>(request, ct);
        }

        // Get/SetMode/SetFilterItemを統合
        // Filter splitter state request (single endpoint for Get/SetMode/SetFilterItem)
        public async UniTask<FilterSplitterStateProtocol.FilterSplitterStateResponse> SendFilterSplitterStateRequest(
            FilterSplitterStateProtocol.FilterSplitterStateRequest request, CancellationToken ct)
        {
            return await _packetExchangeManager.GetPacketResponse<FilterSplitterStateProtocol.FilterSplitterStateResponse>(request, ct);
        }

        // SetRecipe/Clearを1メソッドで送信
        // Machine recipe selection request (single endpoint for SetRecipe / Clear)
        public async UniTask<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse> SendMachineRecipeSelectionRequest(
            MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest request, CancellationToken ct)
        {
            return await _packetExchangeManager.GetPacketResponse<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse>(request, ct);
        }

        // BP Create/GetAll/Deleteを1メソッドで統合
        // Blueprint request (single endpoint for Create / GetAll / Delete)
        public async UniTask<BlueprintResponse> SendBlueprintRequest(BlueprintRequest request, CancellationToken ct)
        {
            return await _packetExchangeManager.GetPacketResponse<BlueprintResponse>(request, ct);
        }

        // 電線延長の唯一送信口（組立はRequest側）
        // Sole send entry for wire-extend; assembly delegated to Request
        public async UniTask<ElectricWireExtendProtocol.ElectricWireExtendResponse> SendElectricWireExtend(
            ElectricWireExtendProtocol.ElectricWireExtendRequest request,
            CancellationToken ct)
        {
            return await _packetExchangeManager.GetPacketResponse<ElectricWireExtendProtocol.ElectricWireExtendResponse>(request, ct);
        }

        // 起点ポールから新規ポールを自動設置しつつチェーン接続する
        // Place a new pole from the source pole and connect the chain
        public async UniTask<GearChainPoleExtendProtocol.GearChainPoleExtendResponse> ExtendGearChainPole(
            Vector3Int fromPolePos,
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            Guid connectToolGuid,
            CancellationToken ct)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateExtendRequest(fromPolePos, poleBlockId, polePlaceInfo, connectToolGuid);
            return await _packetExchangeManager.GetPacketResponse<GearChainPoleExtendProtocol.GearChainPoleExtendResponse>(request, ct);
        }

        // 接続なしの孤立ポールを設置する
        // Place an isolated pole without any connection
        public async UniTask<GearChainPoleExtendProtocol.GearChainPoleExtendResponse> PlaceIsolatedGearChainPole(
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            CancellationToken ct)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateIsolatedPlaceRequest(poleBlockId, polePlaceInfo);
            return await _packetExchangeManager.GetPacketResponse<GearChainPoleExtendProtocol.GearChainPoleExtendResponse>(request, ct);
        }
    }
}

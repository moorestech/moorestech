using System;
using System.Collections.Generic;
using System.Threading;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Research;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Gear.Common;
using Game.PlayerRiding.Interface;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Handshake;
using Server.Protocol.PacketResponse.MapData;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public class VanillaApiWithResponse
    {
        private readonly WorldQueryApi _worldQueryApi;
        private readonly ProgressionQueryApi _progressionQueryApi;
        private readonly TrainRequestApi _trainRequestApi;
        private readonly BlockRequestApi _blockRequestApi;
        private readonly InventoryQueryApi _inventoryQuery;
        private readonly PacketExchangeManager _packetExchangeManager;

        public VanillaApiWithResponse(PacketExchangeManager packetExchangeManager)
        {
            _inventoryQuery = new InventoryQueryApi(packetExchangeManager, ServerContext.ItemStackFactory);
            _packetExchangeManager = packetExchangeManager;
            _blockRequestApi = new BlockRequestApi(packetExchangeManager);
            _trainRequestApi = new TrainRequestApi(packetExchangeManager);
            _progressionQueryApi = new ProgressionQueryApi(packetExchangeManager);
            _worldQueryApi = new WorldQueryApi(packetExchangeManager);
        }

        // 保存を要求し、その要求番号を受け取る。書き出し完了は完了イベントと突き合わせる
        // Request a save and receive its generation; completion is matched against the completed event
        public async UniTask<SaveProtocol.SaveProtocolResponseMessagePack> Save(CancellationToken ct)
        {
            var request = new SaveProtocol.SaveProtocolMessagePack();
            return await _packetExchangeManager.GetPacketResponse<SaveProtocol.SaveProtocolResponseMessagePack>(request, ct);
        }

        // バグ報告用の即時スナップショットを要求する。完了は BugReportCaptureCompletedEventPacket.EventTag で届く
        // Requests an immediate snapshot for a bug report; completion arrives via BugReportCaptureCompletedEventPacket.EventTag
        public async UniTask<BugReportCaptureProtocol.BugReportCaptureResponse> RequestBugReportCapture(CancellationToken ct)
        {
            var request = BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest();
            return await _packetExchangeManager.GetPacketResponse<BugReportCaptureProtocol.BugReportCaptureResponse>(request, ct);
        }

        public UniTask<InitialHandshakeResponse> InitialHandShake(string playerIdentity, CancellationToken ct)
            => InitialHandshakeClient.RunAsync(this, _packetExchangeManager, playerIdentity, ct);

        public UniTask<List<GetMapObjectInfoProtocol.MapObjectsInfoMessagePack>> GetMapObjectInfo(CancellationToken ct)
            => _worldQueryApi.GetMapObjectInfo(ct);

        public UniTask<GetMapDataProtocol.ResponseMapDataMessagePack> GetMapData(CancellationToken ct)
            => _worldQueryApi.GetMapData(ct);

        public UniTask<ResponseMapDataTerrainChunkMessagePack> GetTerrainChunk(int chunkIndex, CancellationToken ct)
            => _worldQueryApi.GetTerrainChunk(chunkIndex, ct);

        public UniTask<TrainResyncProtocol.ResponseMessagePack> SendTrainResync(bool includeRailGraph, CancellationToken ct)
            => _trainRequestApi.SendTrainResync(includeRailGraph, ct);

        public UniTask<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack> PlaceTrainOnRail(RailPosition railPosition, Guid trainCarGuid, CancellationToken ct)
            => _trainRequestApi.PlaceTrainOnRail(railPosition, trainCarGuid, ct);

        public UniTask<AttachTrainCarToUnitProtocol.AttachTrainCarToUnitResponseMessagePack> AttachTrainCarToUnit(
            TrainUnitInstanceId targetTrainUnitInstanceId,
            RailPosition railPosition,
            Guid trainCarGuid,
            bool attachCarFacingForward,
            bool attachToTargetTrainHead,
            CancellationToken ct)
            => _trainRequestApi.AttachTrainCarToUnit(targetTrainUnitInstanceId, railPosition, trainCarGuid, attachCarFacingForward, attachToTargetTrainHead, ct);

        public UniTask<RideActionProtocol.ResponseRideActionMessagePack> RideAction(RideActionType action, RidableIdentifierMessagePack target, CancellationToken ct)
            => _trainRequestApi.RideAction(action, target, ct);

        public async UniTask<PlayerInventoryResponse> GetMyPlayerInventory(CancellationToken ct)
        {
            var request = new PlayerInventoryResponseProtocol.RequestPlayerInventoryProtocolMessagePack();

            var response = await _packetExchangeManager.GetPacketResponse<PlayerInventoryResponseProtocol.PlayerInventoryResponseProtocolMessagePack>(request, ct);

            // メイン・Grabだけでなく装備と選択インデックスも落とさず変換する
            // Converts equipment and the selected index as well, not just main and grab
            return new PlayerInventoryResponse(response);
        }

        public UniTask<WorldDataResponse> GetWorldData(CancellationToken ct)
            => _worldQueryApi.GetWorldData(ct);

        public UniTask<List<ChallengeCategoryResponse>> GetChallengeResponse(CancellationToken ct)
            => _progressionQueryApi.GetChallengeResponse(ct);

        public UniTask<BlockStateMessagePack> GetBlockState(Vector3Int blockPos, CancellationToken ct)
            => _worldQueryApi.GetBlockState(blockPos, ct);

        public UniTask<RemoveBlockProtocol.RemoveBlockResponseMessagePack> BlockRemove(Vector3Int pos, CancellationToken ct)
            => _blockRequestApi.BlockRemove(pos, ct);
        
        public UniTask<UnlockStateResponse> GetUnlockState(CancellationToken ct)
            => _progressionQueryApi.GetUnlockState(ct);

        public UniTask<Dictionary<Guid, ResearchNodeState>> GetResearchNodeStates(CancellationToken ct)
            => _progressionQueryApi.GetResearchNodeStates(ct);

        public UniTask<List<string>> GetPlayedSkitIds(CancellationToken ct)
            => _progressionQueryApi.GetPlayedSkitIds(ct);

        public UniTask<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack> GetWorldPlaySessionInfo(CancellationToken ct)
            => _worldQueryApi.GetWorldPlaySessionInfo(ct);

        public UniTask<CompleteResearchProtocol.ResponseCompleteResearchMessagePack> CompleteResearch(Guid researchGuid, CancellationToken ct)
            => _progressionQueryApi.CompleteResearch(researchGuid, ct);

        public UniTask<InventoryResponse> GetInventory(InventoryIdentifierMessagePack identifier, CancellationToken ct)
            => _inventoryQuery.GetInventory(identifier, ct);

        public UniTask<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack> GetGearNetworkInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
            => _worldQueryApi.GetGearNetworkInfo(blockInstanceId, ct);

        public UniTask<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack> GetElectricNetworkInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
            => _worldQueryApi.GetElectricNetworkInfo(blockInstanceId, ct);

        public UniTask<SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse> SetTrainPlatformTransferMode(
            Vector3Int position, TrainPlatformTransferComponent.TransferMode mode, CancellationToken ct)
            => _trainRequestApi.SetTrainPlatformTransferMode(position, mode, ct);

        public UniTask<SetElectricToGearOutputModeResponse> SetElectricToGearOutputMode(
            Vector3Int position, int index, CancellationToken ct)
            => _blockRequestApi.SetElectricToGearOutputMode(position, index, ct);

        public UniTask<FilterSplitterStateProtocol.FilterSplitterStateResponse> SendFilterSplitterStateRequest(
            FilterSplitterStateProtocol.FilterSplitterStateRequest request, CancellationToken ct)
            => _blockRequestApi.SendFilterSplitterStateRequest(request, ct);

        public UniTask<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse> SendMachineRecipeSelectionRequest(
            MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest request, CancellationToken ct)
            => _blockRequestApi.SendMachineRecipeSelectionRequest(request, ct);

        public UniTask<BlueprintResponse> SendBlueprintRequest(BlueprintRequest request, CancellationToken ct)
            => _blockRequestApi.SendBlueprintRequest(request, ct);

        public UniTask<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse> PlaceRailWithPier(
            int fromNodeId,
            Guid fromGuid,
            BlockId pierBlockId,
            PlaceInfo pierPlaceInfo,
            Guid railTypeGuid,
            CancellationToken ct)
            => _trainRequestApi.PlaceRailWithPier(fromNodeId, fromGuid, pierBlockId, pierPlaceInfo, railTypeGuid, ct);

        public UniTask<ElectricWireExtendProtocol.ElectricWireExtendResponse> SendElectricWireExtend(
            ElectricWireExtendProtocol.ElectricWireExtendRequest request,
            CancellationToken ct)
            => _blockRequestApi.SendElectricWireExtend(request, ct);

        public UniTask<GearChainPoleExtendProtocol.GearChainPoleExtendResponse> ExtendGearChainPole(
            Vector3Int fromPolePos,
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            Guid connectToolGuid,
            CancellationToken ct)
            => _blockRequestApi.ExtendGearChainPole(fromPolePos, poleBlockId, polePlaceInfo, connectToolGuid, ct);

        public UniTask<GearChainPoleExtendProtocol.GearChainPoleExtendResponse> PlaceIsolatedGearChainPole(
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            CancellationToken ct)
            => _blockRequestApi.PlaceIsolatedGearChainPole(poleBlockId, polePlaceInfo, ct);


    }
}

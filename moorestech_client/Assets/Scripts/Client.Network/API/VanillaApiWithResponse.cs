using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Network.Settings;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.CraftTree.Models;
using Game.Research;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Gear.Common;
using Game.PlayerRiding.Interface;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public class VanillaApiWithResponse
    {
        private readonly IItemStackFactory _itemStackFactory;
        private readonly PacketExchangeManager _packetExchangeManager;
        private readonly PlayerConnectionSetting _playerConnectionSetting;

        public VanillaApiWithResponse(PacketExchangeManager packetExchangeManager, PlayerConnectionSetting playerConnectionSetting)
        {
            _itemStackFactory = ServerContext.ItemStackFactory;
            _packetExchangeManager = packetExchangeManager;
            _playerConnectionSetting = playerConnectionSetting;
        }

        public async UniTask<InitialHandshakeResponse> InitialHandShake(int playerId, CancellationToken ct)
        {
            //最初のハンドシェイクを行う
            var request = new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(playerId, $"Player {playerId}");
            var initialHandShake = await _packetExchangeManager.GetPacketResponse<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(request, ct);

            //必要なデータを取得する
            // Fetch all required resources including research node states
            var responses = await UniTask.WhenAll(
                GetMapObjectInfo(ct),
                GetWorldData(ct),
                GetPlayerInventory(playerId, ct),
                GetChallengeResponse(ct),
                GetUnlockState(ct),
                GetCraftTree(playerId, ct),
                GetPlayedSkitIds(ct),
                GetResearchNodeStates(ct),
                GetRailGraphSnapshot(ct),
                GetTrainUnitSnapshots(ct));

            return new InitialHandshakeResponse(initialHandShake, responses);
        }

        public async UniTask<List<GetMapObjectInfoProtocol.MapObjectsInfoMessagePack>> GetMapObjectInfo(CancellationToken ct)
        {
            var request = new GetMapObjectInfoProtocol.RequestMapObjectInfosMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<GetMapObjectInfoProtocol.ResponseMapObjectInfosMessagePack>(request, ct);
            return response?.MapObjects;
        }

        public async UniTask<RailGraphSnapshotMessagePack> GetRailGraphSnapshot(CancellationToken ct)
        {
            var request = new GetRailGraphSnapshotProtocol.RequestMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<GetRailGraphSnapshotProtocol.ResponseMessagePack>(request, ct);
            return response?.Snapshot;
        }

        public async UniTask<TrainUnitSnapshotResponse> GetTrainUnitSnapshots(CancellationToken ct)
        {
            var request = new GetTrainUnitSnapshotsProtocol.RequestMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<GetTrainUnitSnapshotsProtocol.ResponseMessagePack>(request, ct);
            var snapshotPacks = response?.Snapshots;
            var snapshots = new List<TrainUnitSnapshotBundle>(snapshotPacks?.Count ?? 0);
            if (snapshotPacks != null)
            {
                for (var i = 0; i < snapshotPacks.Count; i++)
                {
                    var snapshotPack = snapshotPacks[i];
                    if (snapshotPack == null)
                    {
                        continue;
                    }
                    snapshots.Add(snapshotPack.ToModel());
                }
            }
            var tick = response?.ServerTick ?? 0u;
            var unitsHash = response?.UnitsHash ?? 0u;
            var tickSequenceId = response?.TickSequenceId ?? 0u;
            return new TrainUnitSnapshotResponse(snapshots, tick, unitsHash, tickSequenceId);
        }

        public async UniTask<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack> PlaceTrainOnRail(RailPosition railPosition, Guid trainCarGuid, CancellationToken ct)
        {
            // 列車設置のレスポンスを取得する
            // Get response for train placement
            var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition?.CreateSaveSnapshot());
            var request = new PlaceTrainCarOnRailProtocol.PlaceTrainOnRailRequestMessagePack(railPositionSnapshot, trainCarGuid, _playerConnectionSetting.PlayerId);
            return await _packetExchangeManager.GetPacketResponse<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack>(request, ct);
        }

        public async UniTask<AttachTrainCarToUnitProtocol.AttachTrainCarToUnitResponseMessagePack> AttachTrainCarToUnit(
            TrainUnitInstanceId targetTrainUnitInstanceId,
            RailPosition railPosition,
            Guid trainCarGuid,
            bool attachCarFacingForward,
            bool attachToTargetTrainHead,
            CancellationToken ct)
        {
            // 既存編成連結のレスポンスを取得する
            // Get response for attaching a car to an existing train unit
            var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition?.CreateSaveSnapshot());
            var request = new AttachTrainCarToUnitProtocol.AttachTrainCarToUnitRequestMessagePack(
                targetTrainUnitInstanceId,
                railPositionSnapshot,
                trainCarGuid,
                _playerConnectionSetting.PlayerId,
                attachCarFacingForward,
                attachToTargetTrainHead);
            return await _packetExchangeManager.GetPacketResponse<AttachTrainCarToUnitProtocol.AttachTrainCarToUnitResponseMessagePack>(request, ct);
        }

        // 乗車/降車をサーバーに要求し、結果を受け取る（仕様書セクション5.1）。
        // Requests ride/dismount from the server and returns the result.
        public async UniTask<RideActionProtocol.ResponseRideActionMessagePack> RideAction(RideActionType action, RidableIdentifierMessagePack target, CancellationToken ct)
        {
            var request = new RideActionProtocol.RequestRideActionMessagePack(_playerConnectionSetting.PlayerId, action, target);
            return await _packetExchangeManager.GetPacketResponse<RideActionProtocol.ResponseRideActionMessagePack>(request, ct);
        }

        public async UniTask<PlayerInventoryResponse> GetMyPlayerInventory(CancellationToken ct)
        {
            return await GetPlayerInventory(_playerConnectionSetting.PlayerId, ct);
        }

        public async UniTask<PlayerInventoryResponse> GetPlayerInventory(int playerId, CancellationToken ct)
        {
            var request = new PlayerInventoryResponseProtocol.RequestPlayerInventoryProtocolMessagePack(playerId);

            var response = await _packetExchangeManager.GetPacketResponse<PlayerInventoryResponseProtocol.PlayerInventoryResponseProtocolMessagePack>(request, ct);

            var mainItems = new List<IItemStack>(response.Main.Length);
            foreach (var item in response.Main)
            {
                var id = item.Id;
                var count = item.Count;
                mainItems.Add(_itemStackFactory.Create(id, count));
            }

            var grabItem = _itemStackFactory.Create(response.Grab.Id, response.Grab.Count);

            return new PlayerInventoryResponse(mainItems, grabItem);
        }

        public async UniTask<WorldDataResponse> GetWorldData(CancellationToken ct)
        {
            var request = new RequestWorldDataProtocol.RequestWorldDataMessagePack(_playerConnectionSetting.PlayerId);
            var (response, reason) = await _packetExchangeManager.GetPacketResponseWithReason<RequestWorldDataProtocol.ResponseWorldDataMessagePack>(request, ct);
            // 正常終了以外（タイムアウト等）はスキップし、呼び出し側 (WorldDataHandler) の null ガードに委ねる
            // Return null unless the exchange completed successfully so the caller (WorldDataHandler) can skip this update cycle
            if (reason != PacketWaitCompletionReason.Received) return null;

            return ParseWorldResponse(response);

            #region Internal

            WorldDataResponse ParseWorldResponse(RequestWorldDataProtocol.ResponseWorldDataMessagePack worldData)
            {
                var blocks = worldData.Blocks.Select(b => new BlockInfo(b));
                var entities = worldData.Entities.Select(e => new EntityResponse(e));

                return new WorldDataResponse(blocks.ToList(), entities.ToList());
            }

            #endregion
        }

        public async UniTask<List<ChallengeCategoryResponse>> GetChallengeResponse(CancellationToken ct)
        {
            var request = new GetChallengeInfoProtocol.RequestChallengeMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<GetChallengeInfoProtocol.ResponseChallengeInfoMessagePack>(request, ct);

            var result = new List<ChallengeCategoryResponse>();
            foreach (var category in response.Categories)
            {
                var categoryMaster = MasterHolder.ChallengeMaster.GetChallengeCategory(category.ChallengeCategoryGuid);
                var current = category.CurrentChallengeGuids.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
                var completed = category.CompletedChallengeGuids.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();

                result.Add(new ChallengeCategoryResponse(categoryMaster, category.IsUnlocked, current, completed));
            }

            return result;
        }

        public async UniTask<BlockStateMessagePack> GetBlockState(Vector3Int blockPos, CancellationToken ct)
        {
            var request = new BlockStateProtocol.RequestBlockStateProtocolMessagePack(blockPos);
            var response = await _packetExchangeManager.GetPacketResponse<BlockStateProtocol.ResponseBlockStateProtocolMessagePack>(request, ct);

            return response.State;
        }

        public async UniTask<RemoveBlockProtocol.RemoveBlockResponseMessagePack> BlockRemove(Vector3Int pos, CancellationToken ct)
        {
            var request = new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(_playerConnectionSetting.PlayerId, pos);
            return await _packetExchangeManager.GetPacketResponse<RemoveBlockProtocol.RemoveBlockResponseMessagePack>(request, ct);
        }
        
        // Renamed method to reflect its broader scope
        public async UniTask<UnlockStateResponse> GetUnlockState(CancellationToken ct)
        {
            var request = new GetGameUnlockStateProtocol.RequestGameUnlockStateProtocolMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<GetGameUnlockStateProtocol.ResponseGameUnlockStateProtocolMessagePack>(request, ct);

            // Pass challenge unlock data to the response constructor
            return new UnlockStateResponse(
                response.LockedCraftRecipeGuids, response.UnlockedCraftRecipeGuids,
                response.LockedItemIds, response.UnlockedItemIds,
                response.LockedCategoryChallengeGuids, response.UnlockedCategoryChallengeGuids,
                response.LockedMachineRecipeGuids, response.UnlockedMachineRecipeGuids,
                response.LockedBlockGuids, response.UnlockedBlockGuids,
                response.LockedTrainCarGuids, response.UnlockedTrainCarGuids);
        }

        public async UniTask<Dictionary<Guid, ResearchNodeState>> GetResearchNodeStates(CancellationToken ct)
        {
            var request = new GetResearchInfoProtocol.RequestResearchInfoMessagePack(_playerConnectionSetting.PlayerId);
            var response = await _packetExchangeManager.GetPacketResponse<GetResearchInfoProtocol.ResponseResearchInfoMessagePack>(request, ct);

            return response.ToDictionary();
        }

        public async UniTask<CraftTreeResponse> GetCraftTree(int playerId, CancellationToken ct)
        {
            var request = new GetCraftTreeProtocol.RequestGetCraftTreeMessagePack(playerId);
            var response = await _packetExchangeManager.GetPacketResponse<GetCraftTreeProtocol.ResponseGetCraftTreeMessagePack>(request, ct);

            // レスポンスからCraftTreeNodeのリストを作成
            var craftTreeNodes = new List<CraftTreeNode>();
            foreach (var tree in response.CraftTrees)
            {
                craftTreeNodes.Add(tree.CreateCraftTreeNode());
            }

            return new CraftTreeResponse(craftTreeNodes, response.CurrentTargetNode);
        }

        public async UniTask<List<string>> GetPlayedSkitIds(CancellationToken ct)
        {
            var request = new GetPlayedSkitIdsProtocol.RequestGetPlayedSkitIdsMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<GetPlayedSkitIdsProtocol.ResponseGetPlayedSkitIdsMessagePack>(request, ct);

            return response.PlayedSkitIds;
        }

        public async UniTask<CompleteResearchProtocol.ResponseCompleteResearchMessagePack> CompleteResearch(Guid researchGuid, CancellationToken ct)
        {
            var request = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(_playerConnectionSetting.PlayerId, researchGuid);
            var response = await _packetExchangeManager.GetPacketResponse<CompleteResearchProtocol.ResponseCompleteResearchMessagePack>(request, ct);

            return response;
        }

        public async UniTask<InventoryResponse> GetInventory(InventoryIdentifierMessagePack identifier, CancellationToken ct)
        {
            var request = new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier);
            var response = await _packetExchangeManager.GetPacketResponse<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(request, ct);
            return new InventoryResponse(response.Identifier, CreateStacks(response.Items), response.Result);
        }

        // 指定ブロックが属するギアネットワークの現時点の集約値を取得する
        // Fetch current aggregate info of the gear network that the given block belongs to
        public async UniTask<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack> GetGearNetworkInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetGearNetworkInfoProtocol.RequestGetGearNetworkInfoMessagePack(blockInstanceId);
            return await _packetExchangeManager.GetPacketResponse<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack>(request, ct);
        }

        // 指定ブロックが属する電力ネットワークの現時点の集約値を取得する
        // Fetch current aggregate info of the electric network that the given block belongs to
        public async UniTask<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack> GetElectricNetworkInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetElectricNetworkInfoProtocol.RequestGetElectricNetworkInfoMessagePack(blockInstanceId);
            return await _packetExchangeManager.GetPacketResponse<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack>(request, ct);
        }

        // 貨物プラットフォームのロード/アンロードモードを切り替える
        // Switch the load/unload transfer mode of a train platform block
        public async UniTask<SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse> SetTrainPlatformTransferMode(
            Vector3Int position, TrainPlatformTransferComponent.TransferMode mode, CancellationToken ct)
        {
            var request = new SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeRequest(position, mode);
            return await _packetExchangeManager.GetPacketResponse<SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse>(request, ct);
        }

        // ElectricToGear の出力モードを切り替える
        // Switch the output mode of an ElectricToGear block
        public async UniTask<SetElectricToGearOutputModeResponse> SetElectricToGearOutputMode(
            Vector3Int position, int index, CancellationToken ct)
        {
            var request = new SetElectricToGearOutputModeRequest(position, index);
            return await _packetExchangeManager.GetPacketResponse<SetElectricToGearOutputModeResponse>(request, ct);
        }

        // フィルター分岐器の状態取得・設定 (Get/SetMode/SetFilterItem を 1 メソッドで扱う)
        // Filter splitter state request (single endpoint for Get / SetMode / SetFilterItem)
        public async UniTask<FilterSplitterStateProtocol.FilterSplitterStateResponse> SendFilterSplitterStateRequest(
            FilterSplitterStateProtocol.FilterSplitterStateRequest request, CancellationToken ct)
        {
            return await _packetExchangeManager.GetPacketResponse<FilterSplitterStateProtocol.FilterSplitterStateResponse>(request, ct);
        }

        // BP Create/GetAll/Deleteを1メソッドで統合
        // Blueprint request (single endpoint for Create / GetAll / Delete)
        public async UniTask<BlueprintResponse> SendBlueprintRequest(BlueprintRequest request, CancellationToken ct)
        {
            return await _packetExchangeManager.GetPacketResponse<BlueprintResponse>(request, ct);
        }

        public async UniTask<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack> DisconnectRailAsync(
            int playerId,
            int fromNodeId,
            Guid fromGuid,
            int toNodeId,
            Guid toGuid,
            CancellationToken ct)
        {
            var request = RailConnectionEditProtocol.RailConnectionEditRequest.CreateDisconnectRequest(playerId, fromNodeId, fromGuid, toNodeId, toGuid);
            return await _packetExchangeManager.GetPacketResponse<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack>(request, ct);
        }

        public async UniTask<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse> PlaceRailWithPier(
            int fromNodeId,
            Guid fromGuid,
            BlockId pierBlockId,
            PlaceInfo pierPlaceInfo,
            Guid railTypeGuid,
            CancellationToken ct)
        {
            var request = RailConnectWithPlacePierProtocol.RailConnectWithPlacePierRequest.Create(_playerConnectionSetting.PlayerId, fromNodeId, fromGuid, pierBlockId, pierPlaceInfo, railTypeGuid);
            return await _packetExchangeManager.GetPacketResponse<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse>(request, ct);
        }

        public async UniTask<ElectricWireExtendProtocol.ElectricWireExtendResponse> ExtendElectricWire(
            Vector3Int fromPos,
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            ItemId wireItemId,
            CancellationToken ct)
        {
            // 起点あり延長として電柱設置＋接続要求を送り、設置電柱情報を受け取る
            // Send a with-origin extend request (place pole + wire) and receive the placed pole info
            var request = ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateExtendRequest(_playerConnectionSetting.PlayerId, fromPos, poleBlockId, polePlaceInfo, wireItemId);
            return await _packetExchangeManager.GetPacketResponse<ElectricWireExtendProtocol.ElectricWireExtendResponse>(request, ct);
        }

        // 起点ポールから新規ポールを自動設置しつつチェーン接続する
        // Place a new pole from the source pole and connect the chain
        public async UniTask<GearChainPoleExtendProtocol.GearChainPoleExtendResponse> ExtendGearChainPole(
            Vector3Int fromPolePos,
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            ItemId chainItemId,
            CancellationToken ct)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateExtendRequest(_playerConnectionSetting.PlayerId, fromPolePos, poleBlockId, polePlaceInfo, chainItemId);
            return await _packetExchangeManager.GetPacketResponse<GearChainPoleExtendProtocol.GearChainPoleExtendResponse>(request, ct);
        }

        // 接続なしの孤立ポールを設置する
        // Place an isolated pole without any connection
        public async UniTask<GearChainPoleExtendProtocol.GearChainPoleExtendResponse> PlaceIsolatedGearChainPole(
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            CancellationToken ct)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateIsolatedPlaceRequest(_playerConnectionSetting.PlayerId, poleBlockId, polePlaceInfo);
            return await _packetExchangeManager.GetPacketResponse<GearChainPoleExtendProtocol.GearChainPoleExtendResponse>(request, ct);
        }

        private List<IItemStack> CreateStacks(ItemMessagePack[] items)
        {
            // メッセージパックからアイテムスタックを生成
            // Create item stacks from message pack items
            var count = items?.Length ?? 0;
            var stacks = new List<IItemStack>(count);
            if (items == null) return stacks;
            foreach (var item in items)
            {
                stacks.Add(_itemStackFactory.Create(item.Id, item.Count));
            }
            return stacks;
        }
    }

    public class InventoryResponse
    {
        public InventoryIdentifierMessagePack Identifier { get; }
        public List<IItemStack> Items { get; }
        public InventoryRequestResult Result { get; }

        public InventoryResponse(InventoryIdentifierMessagePack identifier, List<IItemStack> items, InventoryRequestResult result)
        {
            Identifier = identifier;
            Items = items;
            Result = result;
        }
    }
}

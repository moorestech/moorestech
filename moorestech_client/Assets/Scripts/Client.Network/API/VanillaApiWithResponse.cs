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
using Game.Train.RailPosition;
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
            var snapshots = response?.Snapshots ?? new List<TrainUnitSnapshotBundleMessagePack>();
            var tick = response?.ServerTick ?? 0;
            var unitsHash = response?.UnitsHash ?? 0u;
            return new TrainUnitSnapshotResponse(snapshots, tick, unitsHash);
        }

        public async UniTask<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack> PlaceTrainOnRail(RailPositionSaveData railPosition, int hotBarSlot, CancellationToken ct)
        {
            // 列車設置のレスポンスを取得する
            // Get response for train placement
            var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition);
            var request = new PlaceTrainCarOnRailProtocol.PlaceTrainOnRailRequestMessagePack(railPositionSnapshot, hotBarSlot, _playerConnectionSetting.PlayerId);
            return await _packetExchangeManager.GetPacketResponse<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack>(request, ct);
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
            var request = new RequestWorldDataProtocol.RequestWorldDataMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<RequestWorldDataProtocol.ResponseWorldDataMessagePack>(request, ct);
            
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
        
        // Renamed method to reflect its broader scope
        public async UniTask<UnlockStateResponse> GetUnlockState(CancellationToken ct)
        {
            var request = new GetGameUnlockStateProtocol.RequestGameUnlockStateProtocolMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<GetGameUnlockStateProtocol.ResponseGameUnlockStateProtocolMessagePack>(request, ct);
            
            // Pass challenge unlock data to the response constructor
            return new UnlockStateResponse(
                response.LockedCraftRecipeGuids, response.UnlockedCraftRecipeGuids,
                response.LockedItemIds, response.UnlockedItemIds,
                response.LockedCategoryChallengeGuids, response.UnlockedCategoryChallengeGuids);
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
        
        public async UniTask<List<IItemStack>> GetInventory(InventoryIdentifierMessagePack identifier, CancellationToken ct)
        {
            var request = new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier);
            var response = await _packetExchangeManager.GetPacketResponse<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(request, ct);
            return CreateStacks(response.Items);
        }

        public async UniTask<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack> DisconnectRailAsync(
            int fromNodeId,
            Guid fromGuid,
            int toNodeId,
            Guid toGuid,
            CancellationToken ct)
        {
            var request = RailConnectionEditProtocol.RailConnectionEditRequest.CreateDisconnectRequest(fromNodeId, fromGuid, toNodeId, toGuid);
            return await _packetExchangeManager.GetPacketResponse<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack>(request, ct);
        }
        
        #region Internal
        
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
        
        #endregion
    }
}

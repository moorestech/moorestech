using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Network.Settings;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.CraftTree;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
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
            var responses = await UniTask.WhenAll(
                GetMapObjectInfo(ct), 
                GetWorldData(ct), 
                GetPlayerInventory(playerId, ct), 
                GetChallengeResponse(playerId, ct), 
                GetAllBlockState(ct),
                GetUnlockState(ct), 
                GetCraftTree(playerId, ct));
            
            return new InitialHandshakeResponse(initialHandShake, responses);
        }
        
        public async UniTask<List<GetMapObjectInfoProtocol.MapObjectsInfoMessagePack>> GetMapObjectInfo(CancellationToken ct)
        {
            var request = new GetMapObjectInfoProtocol.RequestMapObjectInfosMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<GetMapObjectInfoProtocol.ResponseMapObjectInfosMessagePack>(request, ct);
            return response?.MapObjects;
        }
        
        public async UniTask<List<IItemStack>> GetBlockInventory(Vector3Int blockPos, CancellationToken ct)
        {
            var request = new BlockInventoryRequestProtocol.RequestBlockInventoryRequestProtocolMessagePack(blockPos);
            
            var response = await _packetExchangeManager.GetPacketResponse<BlockInventoryRequestProtocol.BlockInventoryResponseProtocolMessagePack>(request, ct);
            
            var items = new List<IItemStack>(response.Items.Length);
            for (var i = 0; i < response.Items.Length; i++)
            {
                var id = response.Items[i].Id;
                var count = response.Items[i].Count;
                items.Add(_itemStackFactory.Create(id, count));
            }
            
            return items;
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
        
        public async UniTask<ChallengeResponse> GetChallengeResponse(int playerId, CancellationToken ct)
        {
            var request = new GetChallengeInfoProtocol.RequestChallengeMessagePack(playerId);
            var response = await _packetExchangeManager.GetPacketResponse<GetChallengeInfoProtocol.ResponseChallengeInfoMessagePack>(request, ct);
            
            var current = response.CurrentChallengeGuids.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
            var completed = response.CompletedChallengeGuids.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
            
            return new ChallengeResponse(current, completed);
        }
        
        public async UniTask<List<BlockStateMessagePack>> GetAllBlockState(CancellationToken ct)
        {
            var request = new AllBlockStateProtocol.RequestAllBlockStateProtocolMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<AllBlockStateProtocol.ResponseAllBlockStateProtocolMessagePack>(request, ct);
            
            return response.StateList;
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
                response.LockedChallengeGuids, response.UnlockedChallengeGuids);
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
    }
}
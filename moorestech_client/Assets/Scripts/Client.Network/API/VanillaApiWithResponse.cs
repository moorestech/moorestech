using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Network.Settings;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Challenge;
using Game.Context;
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
            var request = new RequestInitialHandshakeMessagePack(playerId, $"Player {playerId}");
            var response = await _packetExchangeManager.GetPacketResponse<ResponseInitialHandshakeMessagePack>(request, ct);
            
            List<MapObjectsInfoMessagePack> mapObjects = null;
            WorldDataResponse worldData = null;
            PlayerInventoryResponse inventory = null;
            ChallengeResponse challenge = null;
            List<ChangeBlockStateMessagePack> blockStates = null;
            
            //必要なデータを取得する
            await UniTask.WhenAll(GetMapObjects(), GetWorld(), GetInventory(), GetChallenge(), GetBlockStates());
            
            return new InitialHandshakeResponse(response, worldData, mapObjects, inventory, challenge, blockStates);
            
            #region Internal
            
            async UniTask GetMapObjects()
            {
                mapObjects = await GetMapObjectInfo(ct);
            }
            
            async UniTask GetWorld()
            {
                worldData = await GetWorldData(ct);
            }
            
            async UniTask GetInventory()
            {
                inventory = await GetPlayerInventory(playerId, ct);
            }
            
            async UniTask GetChallenge()
            {
                challenge = await GetChallengeResponse(playerId, ct);
            }
            
            async UniTask GetBlockStates()
            {
                blockStates = await GetCurrentBlockState(ct);
            }
            
            #endregion
        }
        
        public async UniTask<List<MapObjectsInfoMessagePack>> GetMapObjectInfo(CancellationToken ct)
        {
            var request = new RequestMapObjectInfosMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<ResponseMapObjectInfosMessagePack>(request, ct);
            return response?.MapObjects;
        }
        
        public async UniTask<List<IItemStack>> GetBlockInventory(Vector3Int blockPos, CancellationToken ct)
        {
            var request = new RequestBlockInventoryRequestProtocolMessagePack(blockPos);
            
            var response = await _packetExchangeManager.GetPacketResponse<BlockInventoryResponseProtocolMessagePack>(request, ct);
            
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
            var request = new RequestPlayerInventoryProtocolMessagePack(playerId);
            
            var response = await _packetExchangeManager.GetPacketResponse<PlayerInventoryResponseProtocolMessagePack>(request, ct);
            
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
            var request = new RequestWorldDataMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<ResponseWorldDataMessagePack>(request, ct);
            
            return ParseWorldResponse(response);
            
            #region Internal
            
            WorldDataResponse ParseWorldResponse(ResponseWorldDataMessagePack worldData)
            {
                var blocks = worldData.Blocks.Select(b => new BlockInfo(b));
                var entities = worldData.Entities.Select(e => new EntityResponse(e));
                
                return new WorldDataResponse(blocks.ToList(), entities.ToList());
            }
            
            #endregion
        }
        
        public async UniTask<ChallengeResponse> GetChallengeResponse(int playerId, CancellationToken ct)
        {
            var request = new RequestChallengeMessagePack(playerId);
            var response = await _packetExchangeManager.GetPacketResponse<ResponseChallengeInfoMessagePack>(request, ct);
            
            var current = response.CurrentChallengeGuids.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
            var completed = response.CompletedChallengeGuids.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
            
            return new ChallengeResponse(current, completed);
        }
        
        public async UniTask<List<ChangeBlockStateMessagePack>> GetCurrentBlockState(CancellationToken ct)
        {
            var request = new RequestBlockStateProtocolMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<ResponseBlockStateProtocolMessagePack>(request, ct);
            
            return response.StateList;
        }
    }
}
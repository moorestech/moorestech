using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Network.API;
using Core.Item;
using Cysharp.Threading.Tasks;
using Game.World.Interface.DataStore;
using MainGame.Network.Settings;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public class VanillaApiWithResponse
    {
        private readonly ServerConnector _serverConnector;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly PlayerConnectionSetting _playerConnectionSetting;
        
        public VanillaApiWithResponse(ServerConnector serverConnector, ItemStackFactory itemStackFactory, PlayerConnectionSetting playerConnectionSetting)
        {
            _serverConnector = serverConnector;
            _itemStackFactory = itemStackFactory;
            _playerConnectionSetting = playerConnectionSetting;
        }
        
        public async UniTask<HandshakeResponse> InitialHandShake(int playerId,CancellationToken ct)
        {
            var request = new RequestInitialHandshakeMessagePack(playerId,$"Player {playerId}");
            var response = await _serverConnector.GetInformationData<ResponseInitialHandshakeMessagePack>(request, ct);
            return new HandshakeResponse(response);
        }
        
        public async UniTask<List<MapObjectsInfoMessagePack>> GetMapObjectInfo(CancellationToken ct)
        {
            var request = new RequestMapObjectInfosMessagePack();
            var response = await _serverConnector.GetInformationData<ResponseMapObjectInfosMessagePack>(request, ct);
            return response?.MapObjects;
        }
        
        public async UniTask<List<IItemStack>> GetBlockInventory(Vector2Int blockPos, CancellationToken ct)
        {
            var request = new RequestBlockInventoryRequestProtocolMessagePack(blockPos.x, blockPos.y);

            var response = await _serverConnector.GetInformationData<BlockInventoryResponseProtocolMessagePack>(request, ct);

            var items = new List<IItemStack>(response.ItemIds.Length);
            for (int i = 0; i < response.ItemIds.Length; i++)
            {
                var id = response.ItemIds[i];
                var count = response.ItemCounts[i];
                items.Add(_itemStackFactory.Create(id, count));
            }

            return items;
        }
        
        public async UniTask<PlayerInventoryResponse> GetPlayerInventory(int playerId, CancellationToken ct)
        {
            var request = new RequestPlayerInventoryProtocolMessagePack(playerId);

            var response = await _serverConnector.GetInformationData<PlayerInventoryResponseProtocolMessagePack>(request, ct);

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

        public async UniTask<List<ChunkResponse>> GetChunkInfos(List<Vector2Int> chunks, CancellationToken ct)
        {
            var request = new RequestChunkDataMessagePack(chunks.Select(c => new Vector2IntMessagePack(c)).ToList());
            var response = await _serverConnector.GetInformationData<ResponseChunkDataMessagePack>(request, ct);
            
            var result = new List<ChunkResponse>(response.ChunkData.Length);
            foreach (var responseChunk in response.ChunkData)
            {
                result.Add(ParseChunkResponse(responseChunk));
            }
            
            return result;
            
            #region Internal

            ChunkResponse ParseChunkResponse(ChunkDataMessagePack chunk)
            {
                var blocks = new BlockInfo[chunk.BlockIds.GetLength(0), chunk.BlockIds.GetLength(1)];
                for (int x = 0; x < chunk.BlockIds.GetLength(0); x++)
                {
                    for (int y = 0; y < chunk.BlockIds.GetLength(1); y++)
                    {
                        blocks[x, y] = new BlockInfo(chunk.BlockIds[x, y], (BlockDirection) chunk.BlockDirections[x, y]);
                    }
                }
                
                var entities = chunk.Entities.
                    Select(e => new EntityResponse(e));
                
                var chunkPos = chunk.ChunkPos.Vector2Int;
                return new ChunkResponse(chunkPos, blocks, entities.ToList());
            }

            #endregion
        }
    }
}
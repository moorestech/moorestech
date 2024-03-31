using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Server.Core.Item;
using Cysharp.Threading.Tasks;
using MainGame.Network.Settings;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Const;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public class VanillaApiWithResponse
    {
        private readonly List<Vector2Int> _getChunkPoss = new();
        private readonly ItemStackFactory _itemStackFactory;
        private readonly PacketExchangeManager _packetExchangeManager;
        private readonly PlayerConnectionSetting _playerConnectionSetting;

        public VanillaApiWithResponse(PacketExchangeManager packetExchangeManager, ItemStackFactory itemStackFactory, PlayerConnectionSetting playerConnectionSetting)
        {
            _packetExchangeManager = packetExchangeManager;
            _itemStackFactory = itemStackFactory;
            _playerConnectionSetting = playerConnectionSetting;

            var getChunkSize = 5;
            for (var i = -getChunkSize; i <= getChunkSize; i++)
            for (var j = -getChunkSize; j <= getChunkSize; j++)
                _getChunkPoss.Add(new Vector2Int(i * ChunkResponseConst.ChunkSize, j * ChunkResponseConst.ChunkSize));
        }

        public async UniTask<InitialHandshakeResponse> InitialHandShake(int playerId, CancellationToken ct)
        {
            //最初のハンドシェイクを行う
            var request = new RequestInitialHandshakeMessagePack(playerId, $"Player {playerId}");
            var response = await _packetExchangeManager.GetPacketResponse<ResponseInitialHandshakeMessagePack>(request, ct);

            List<MapObjectsInfoMessagePack> mapObjects = null;
            List<ChunkResponse> chunk = null;

            //必要なデータを取得する
            await UniTask.WhenAll(GetMapObjects(), GetChunk());

            return new InitialHandshakeResponse(response, chunk, mapObjects);

            #region Internal

            async UniTask GetMapObjects()
            {
                mapObjects = await GetMapObjectInfo(ct);
            }

            async UniTask GetChunk()
            {
                chunk = await GetChunkInfos(ct);
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

            var items = new List<IItemStack>(response.ItemIds.Length);
            for (var i = 0; i < response.ItemIds.Length; i++)
            {
                var id = response.ItemIds[i];
                var count = response.ItemCounts[i];
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

        public async UniTask<List<ChunkResponse>> GetChunkInfos(CancellationToken ct)
        {
            var request = new RequestChunkDataMessagePack(_getChunkPoss.Select(c => new Vector2IntMessagePack(c)).ToList());
            var response = await _packetExchangeManager.GetPacketResponse<ResponseChunkDataMessagePack>(request, ct);

            var result = new List<ChunkResponse>(response.ChunkData.Length);
            foreach (var responseChunk in response.ChunkData)
            {
                result.Add(ParseChunkResponse(responseChunk));
            }

            return result;

            #region Internal

            ChunkResponse ParseChunkResponse(ChunkDataMessagePack chunk)
            {
                IEnumerable<BlockInfo> blocks = chunk.Blocks.Select(b => new BlockInfo(b));
                IEnumerable<EntityResponse> entities = chunk.Entities.Select(e => new EntityResponse(e));
                var chunkPos = chunk.ChunkPos;

                return new ChunkResponse(chunkPos, blocks.ToList(), entities.ToList());
            }

            #endregion
        }
    }
}
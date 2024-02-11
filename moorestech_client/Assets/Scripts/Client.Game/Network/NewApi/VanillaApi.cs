using System;
using System.Collections.Generic;
using System.Threading;
using Core.Item;
using Cysharp.Threading.Tasks;
using Game.World.Interface.DataStore;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.NewApi
{
    public class VanillaApi
    {
        private readonly ServerConnector _serverConnector;
        private readonly ItemStackFactory _itemStackFactory;

        private static VanillaApi _instance;

        public VanillaApi(ServerConnector serverConnector, ItemStackFactory itemStackFactory)
        {
            _serverConnector = serverConnector;
            _itemStackFactory = itemStackFactory;
            _instance = this;
        }

        public static async UniTask<List<MapObjectsInfoMessagePack>> GetMapObjectInfo(CancellationToken ct)
        {
            var request = new RequestMapObjectInfosMessagePack();
            var response = await _instance._serverConnector.GetInformationData<ResponseMapObjectInfosMessagePack>(request, ct);
            return response?.MapObjects;
        }


        public static async UniTask<List<IItemStack>> GetBlockInventory(Vector2Int blockPos, CancellationToken ct)
        {
            var request = new RequestBlockInventoryRequestProtocolMessagePack(blockPos.x, blockPos.y);

            var response = await _instance._serverConnector.GetInformationData<BlockInventoryResponseProtocolMessagePack>(request, ct);

            var items = new List<IItemStack>(response.ItemIds.Length);
            for (int i = 0; i < response.ItemIds.Length; i++)
            {
                var id = response.ItemIds[i];
                var count = response.ItemCounts[i];
                items.Add(_instance._itemStackFactory.Create(id, count));
            }

            return items;
        }


        public static async UniTask<PlayerInventoryResponse> GetPlayerInventory(int playerId, CancellationToken ct)
        {
            var request = new RequestPlayerInventoryProtocolMessagePack(playerId);

            var response = await _instance._serverConnector.GetInformationData<PlayerInventoryResponseProtocolMessagePack>(request, ct);

            var mainItems = new List<IItemStack>(response.Main.Length);
            foreach (var item in response.Main)
            {
                var id = item.Id;
                var count = item.Count;
                mainItems.Add(_instance._itemStackFactory.Create(id, count));
            }

            var grabItem = _instance._itemStackFactory.Create(response.Grab.Id, response.Grab.Count);

            return new PlayerInventoryResponse(mainItems, grabItem);
        }

        public static async UniTask<List<ChunkResponse>> GetChunkInfos(List<Vector2Int> chunks, CancellationToken ct)
        {
            //TODO チャンクを取得するプロトコルをつくる
            throw new NotImplementedException();
        }
    }

    public class PlayerInventoryResponse
    {
        public PlayerInventoryResponse(List<IItemStack> mainInventory, IItemStack grabItem)
        {
            MainInventory = mainInventory;
            GrabItem = grabItem;
        }

        public List<IItemStack> MainInventory { get; }
        public IItemStack GrabItem { get; }
    }

    public class ChunkResponse
    {
        public readonly Vector2Int ChunkPos;
        public readonly BlockResponse[,] Blocks;
        public readonly EntityResponse[] Entities;
        
        //TODO レスポンスの種類を増やせるようにする

        public ChunkResponse(Vector2Int chunkPos, BlockResponse[,] blocks, EntityResponse[] entities)
        {
            ChunkPos = chunkPos;
            Blocks = blocks;
            Entities = entities;
        }

    }

    public class BlockResponse
    {
        public BlockDirection BlockDirection;
        public int BlockId;
    }

    public class EntityResponse
    {
        public long InstanceId { get; set; }

        public string Type { get; set; }

        public Vector3 Position { get; set; }

        public string State { get; set; }
    }

}
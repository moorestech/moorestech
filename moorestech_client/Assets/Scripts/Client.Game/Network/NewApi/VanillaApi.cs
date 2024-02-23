using System;
using System.Collections.Generic;
using System.Linq;
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

        public static void SetOpenCloseBlock(int playerId, Vector2Int pos, bool isOpen)
        {
            var request = new BlockInventoryOpenCloseProtocolMessagePack(playerId, pos.x, pos.y, isOpen);
            _instance._serverConnector.Send(request);
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
            var request = new RequestChunkDataMessagePack(chunks.Select(c => new Vector2IntMessagePack(c)).ToList());
            var response = await _instance._serverConnector.GetInformationData<ResponseChunkDataMessagePack>(request, ct);
            
            var result = new List<ChunkResponse>(response.ChunkData.Length);
            foreach (var responseChunk in response.ChunkData)
            {
                result.Add(ParseChunkResponse(responseChunk));
            }
            
            return result;
            
            #region Internal

            ChunkResponse ParseChunkResponse(ChunkDataMessagePack chunk)
            {
                var blocks = new BlockResponse[chunk.BlockIds.GetLength(0), chunk.BlockIds.GetLength(1)];
                for (int x = 0; x < chunk.BlockIds.GetLength(0); x++)
                {
                    for (int y = 0; y < chunk.BlockIds.GetLength(1); y++)
                    {
                        blocks[x, y] = new BlockResponse(chunk.BlockIds[x, y], (BlockDirection) chunk.BlockDirections[x, y]);
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

    public class HandshakeResponse
    {
        public Vector2 PlayerPos { get; }
        
        public HandshakeResponse(Vector2 playerPos)
        {
            PlayerPos = playerPos;
        }
    }

    public class PlayerInventoryResponse
    {
        public List<IItemStack> MainInventory { get; }
        public IItemStack GrabItem { get; }
        
        public PlayerInventoryResponse(List<IItemStack> mainInventory, IItemStack grabItem)
        {
            MainInventory = mainInventory;
            GrabItem = grabItem;
        }
    }

    public class ChunkResponse
    {
        public readonly Vector2Int ChunkPos;
        public readonly BlockResponse[,] Blocks;
        public readonly List<EntityResponse> Entities;
        
        //TODO レスポンスの種類を増やせるようにする

        public ChunkResponse(Vector2Int chunkPos, BlockResponse[,] blocks, List<EntityResponse> entities)
        {
            ChunkPos = chunkPos;
            Blocks = blocks;
            Entities = entities;
        }
    }

    public class BlockResponse
    {
        public readonly BlockDirection BlockDirection;
        public readonly int BlockId;
        
        public BlockResponse(int blockId, BlockDirection blockDirection)
        {
            BlockId = blockId;
            BlockDirection = blockDirection;
        }
    }

    public class EntityResponse
    {
        public readonly long InstanceId;
        public readonly string Type;
        public readonly Vector3 Position;
        public readonly string State;

        public EntityResponse(EntityMessagePack entityMessagePack)
        {
            InstanceId = entityMessagePack.InstanceId;
            Type = entityMessagePack.Type;
            Position = entityMessagePack.Position.Vector3;
            State = entityMessagePack.State;
        }
    }

}
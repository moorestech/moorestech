using System.Collections.Generic;
using Core.Item;
using Game.World.Interface.DataStore;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public class InitialHandshakeResponse
    {
        public Vector2 PlayerPos { get; }
        public List<ChunkResponse> Chunks { get; }
        public List<MapObjectsInfoMessagePack> MapObjects { get; }
        
        public InitialHandshakeResponse(ResponseInitialHandshakeMessagePack response, List<ChunkResponse> chunks, List<MapObjectsInfoMessagePack> mapObjects)
        {
            Chunks = chunks;
            MapObjects = mapObjects;
            PlayerPos = response.PlayerPos.Vector2;
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
        public readonly BlockInfo[,] Blocks;
        public readonly List<EntityResponse> Entities;
        
        //TODO レスポンスの種類を増やせるようにする

        public ChunkResponse(Vector2Int chunkPos, BlockInfo[,] blocks, List<EntityResponse> entities)
        {
            ChunkPos = chunkPos;
            Blocks = blocks;
            Entities = entities;
        }
    }
    
    public class BlockInfo
    {
        public readonly BlockDirection BlockDirection;
        public readonly int BlockId;
        
        public BlockInfo(int blockId, BlockDirection blockDirection)
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
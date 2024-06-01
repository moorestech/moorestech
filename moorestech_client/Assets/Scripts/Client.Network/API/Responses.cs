using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Interface;
using Game.Challenge;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public class InitialHandshakeResponse
    {
        public InitialHandshakeResponse(ResponseInitialHandshakeMessagePack response, List<ChunkResponse> chunks, List<MapObjectsInfoMessagePack> mapObjects, PlayerInventoryResponse inventory, ChallengeResponse challenge, List<ChangeBlockStateMessagePack> blockStates)
        {
            PlayerPos = response.PlayerPos;
            Chunks = chunks;
            MapObjects = mapObjects;
            Inventory = inventory;
            Challenge = challenge;
            BlockStates = blockStates;
        }
        
        public Vector2 PlayerPos { get; }
        public List<ChunkResponse> Chunks { get; }
        public List<MapObjectsInfoMessagePack> MapObjects { get; }
        public PlayerInventoryResponse Inventory { get; }
        public ChallengeResponse Challenge { get; }
        public List<ChangeBlockStateMessagePack> BlockStates { get; }
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
        public readonly List<BlockInfo> Blocks;
        public readonly Vector2Int ChunkPos;
        public readonly List<EntityResponse> Entities;
        
        //TODO レスポンスの種類を増やせるようにする
        
        public ChunkResponse(Vector2Int chunkPos, List<BlockInfo> blocks, List<EntityResponse> entities)
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
        public readonly Vector3Int BlockPos;
        
        public BlockInfo(BlockDataMessagePack blockDataMessagePack)
        {
            BlockPos = blockDataMessagePack.BlockPos;
            BlockId = blockDataMessagePack.BlockId;
            BlockDirection = blockDataMessagePack.BlockDirection;
        }
    }
    
    public class EntityResponse
    {
        public readonly long InstanceId;
        public readonly Vector3 Position;
        public readonly string State;
        public readonly string Type;
        
        public EntityResponse(EntityMessagePack entityMessagePack)
        {
            InstanceId = entityMessagePack.InstanceId;
            Type = entityMessagePack.Type;
            Position = entityMessagePack.Position;
            State = entityMessagePack.State;
        }
    }
    
    public class ChallengeResponse
    {
        public readonly List<ChallengeInfo> CompletedChallenges;
        public readonly List<ChallengeInfo> CurrentChallenges;
        
        public ChallengeResponse(List<ChallengeInfo> currentChallenges, List<ChallengeInfo> completedChallenges)
        {
            CurrentChallenges = currentChallenges;
            CompletedChallenges = completedChallenges;
        }
    }
}
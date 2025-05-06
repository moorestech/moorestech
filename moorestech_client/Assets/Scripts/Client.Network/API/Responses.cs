using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Challenge;
using Game.CraftTree;
using Mooresmaster.Model.ChallengesModule;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;
using static Server.Protocol.PacketResponse.GetMapObjectInfoProtocol;
using static Server.Protocol.PacketResponse.InitialHandshakeProtocol;

namespace Client.Network.API
{
    public class InitialHandshakeResponse
    {
        public Vector3 PlayerPos { get; }
        public WorldDataResponse WorldData { get; }
        public List<MapObjectsInfoMessagePack> MapObjects { get; }
        public PlayerInventoryResponse Inventory { get; }
        public ChallengeResponse Challenge { get; }
        public List<BlockStateMessagePack> BlockStates { get; }
        public UnlockStateResponse UnlockState { get; }
        public CraftTreeResponse CraftTree { get; }
        
        public InitialHandshakeResponse(
            ResponseInitialHandshakeMessagePack initialHandshake,
            (
                List<MapObjectsInfoMessagePack> mapObjects, 
                WorldDataResponse worldData, 
                PlayerInventoryResponse inventory, 
                ChallengeResponse challenge, 
                List<BlockStateMessagePack> blockStates,
                UnlockStateResponse unlockState,
                CraftTreeResponse craftTree) responses)
        {
            PlayerPos = initialHandshake.PlayerPos;
            WorldData = responses.worldData;
            MapObjects = responses.mapObjects;
            Inventory = responses.inventory;
            Challenge = responses.challenge;
            BlockStates = responses.blockStates;
            UnlockState = responses.unlockState;
            CraftTree = responses.craftTree;
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
    
    public class WorldDataResponse
    {
        public readonly List<BlockInfo> Blocks;
        public readonly List<EntityResponse> Entities;
        
        public WorldDataResponse(List<BlockInfo> blocks, List<EntityResponse> entities)
        {
            Blocks = blocks;
            Entities = entities;
        }
    }
    
    public class BlockInfo
    {
        public readonly BlockDirection BlockDirection;
        public readonly BlockId BlockId;
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
        public readonly List<ChallengeMasterElement> CompletedChallenges;
        public readonly List<ChallengeMasterElement> CurrentChallenges;
        
        public ChallengeResponse(List<ChallengeMasterElement> currentChallenges, List<ChallengeMasterElement> completedChallenges)
        {
            CurrentChallenges = currentChallenges;
            CompletedChallenges = completedChallenges;
        }
    }
    
    public class UnlockStateResponse
    {
        public readonly List<Guid> LockedCraftRecipeGuids;
        public readonly List<Guid> UnlockedCraftRecipeGuids;
        
        public readonly List<ItemId> LockedItemIds;
        public readonly List<ItemId> UnlockedItemIds;

        public readonly List<Guid> LockedChallengeGuids; // Added for challenges
        public readonly List<Guid> UnlockedChallengeGuids; // Added for challenges
        
        public UnlockStateResponse(
            List<Guid> lockedCraftRecipeGuids, List<Guid> unlockedCraftRecipeGuids,
            List<ItemId> lockedItemIds, List<ItemId> unlockedItemIds,
            List<Guid> lockedChallengeGuids, List<Guid> unlockedChallengeGuids) // Added challenge parameters
        {
            LockedCraftRecipeGuids = lockedCraftRecipeGuids;
            UnlockedCraftRecipeGuids = unlockedCraftRecipeGuids;
            LockedItemIds = lockedItemIds;
            UnlockedItemIds = unlockedItemIds;
            LockedChallengeGuids = lockedChallengeGuids; // Added assignment
            UnlockedChallengeGuids = unlockedChallengeGuids; // Added assignment
        }
    }
    
    public class CraftTreeResponse
    {
        public List<CraftTreeNode> CraftTrees { get; }
        public Guid CurrentTargetNode { get; }
        
        public CraftTreeResponse(List<CraftTreeNode> craftTrees, Guid currentTargetNode)
        {
            CraftTrees = craftTrees;
            CurrentTargetNode = currentTargetNode;
        }
    }
}
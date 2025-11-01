using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.CraftTree.Models;
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
        public List<ChallengeCategoryResponse> Challenges { get; }
        public UnlockStateResponse UnlockState { get; }
        public CraftTreeResponse CraftTree { get; }
        public List<string> PlayedSkitIds { get; }
        public RailConnectionMessagePack.RailConnectionData[] RailConnections { get; }
        
        public InitialHandshakeResponse(
            ResponseInitialHandshakeMessagePack initialHandshake,
            (
                RailConnectionMessagePack.RailConnectionData[] railConnections,
                List<MapObjectsInfoMessagePack> mapObjects, 
                WorldDataResponse worldData, 
                PlayerInventoryResponse inventory,
                List<ChallengeCategoryResponse> challenges, 
                UnlockStateResponse unlockState,
                CraftTreeResponse craftTree,
                List<string> playedSkitIds) responses)
        {
            PlayerPos = initialHandshake.PlayerPos;
            WorldData = responses.worldData;
            MapObjects = responses.mapObjects;
            Inventory = responses.inventory;
            Challenges = responses.challenges;
            UnlockState = responses.unlockState;
            CraftTree = responses.craftTree;
            PlayedSkitIds = responses.playedSkitIds;
            
            // レール情報を初期データとして保持
            // Store rail connections for later visualization
            RailConnections = responses.railConnections ?? Array.Empty<RailConnectionMessagePack.RailConnectionData>();
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
    
    public class ChallengeCategoryResponse
    {
        public readonly ChallengeCategoryMasterElement Category;
        public readonly bool IsUnlocked;
        
        public readonly List<ChallengeMasterElement> CurrentChallenges;
        public readonly List<ChallengeMasterElement> CompletedChallenges;

        
        public ChallengeCategoryResponse(ChallengeCategoryMasterElement category, bool isUnlocked, List<ChallengeMasterElement> currentChallenges, List<ChallengeMasterElement> completedChallenges)
        {
            Category = category;
            IsUnlocked = isUnlocked;
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

        public readonly List<Guid> LockedChallengeCategoryGuids;
        public readonly List<Guid> UnlockedChallengeCategoryGuids;
        
        public UnlockStateResponse(
            List<Guid> lockedCraftRecipeGuids, List<Guid> unlockedCraftRecipeGuids,
            List<ItemId> lockedItemIds, List<ItemId> unlockedItemIds,
            List<Guid> lockedChallengeCategoryGuids, List<Guid> unlockedChallengeCategoryGuids)
        {
            LockedCraftRecipeGuids = lockedCraftRecipeGuids;
            UnlockedCraftRecipeGuids = unlockedCraftRecipeGuids;
            LockedItemIds = lockedItemIds;
            UnlockedItemIds = unlockedItemIds;
            LockedChallengeCategoryGuids = lockedChallengeCategoryGuids;
            UnlockedChallengeCategoryGuids = unlockedChallengeCategoryGuids;
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

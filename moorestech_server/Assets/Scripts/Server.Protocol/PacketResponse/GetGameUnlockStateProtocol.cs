using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class GetGameUnlockStateProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getGameUnlockState";
        
        private readonly IGameUnlockStateData gameUnlockStateData;
        
        public GetGameUnlockStateProtocol(ServiceProvider serviceProvider)
        {
            gameUnlockStateData = serviceProvider.GetService<IGameUnlockStateDataController>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var infos = gameUnlockStateData.CraftRecipeUnlockStateInfos;
            
            var lockedCraftRecipe = new List<string>();
            var unlockedCraftRecipe = new List<string>();
            foreach (var craftRecipe in gameUnlockStateData.CraftRecipeUnlockStateInfos.Values)
            {
                if (craftRecipe.IsUnlocked)
                {
                    unlockedCraftRecipe.Add(craftRecipe.CraftRecipeGuid.ToString());
                }
                else
                {
                    lockedCraftRecipe.Add(craftRecipe.CraftRecipeGuid.ToString());
                }
            }
            
            var lockedItem = new List<int>();
            var unlockedItem = new List<int>();
            foreach (var item in gameUnlockStateData.ItemUnlockStateInfos.Values)
            {
                if (item.IsUnlocked)
                {
                    unlockedItem.Add(item.ItemId.AsPrimitive());
                }
                else
                {
                    lockedItem.Add(item.ItemId.AsPrimitive());
                }
            }
            
            return new ResponseGameUnlockStateProtocolMessagePack(unlockedCraftRecipe, lockedCraftRecipe, lockedItem, unlockedItem);
        }
        
        
        [MessagePackObject]
        public class RequestGameUnlockStateProtocolMessagePack : ProtocolMessagePackBase
        {
            public RequestGameUnlockStateProtocolMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseGameUnlockStateProtocolMessagePack : ProtocolMessagePackBase
        {
            [IgnoreMember] public List<Guid> UnlockCraftRecipeGuids => UnlockCraftRecipeGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedCraftRecipeGuids => LockedCraftRecipeGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<ItemId> UnlockItemIds => UnlockedItemIdsInt.Select(i => new ItemId(i)).ToList();
            [IgnoreMember] public List<ItemId> LockedItemIds => LockedItemIdsInt.Select(i => new ItemId(i)).ToList();
            
            [Key(2)] public List<string> UnlockCraftRecipeGuidsStr { get; set; }
            [Key(3)] public List<string> LockedCraftRecipeGuidsStr { get; set; }
            
            [Key(4)] public List<int> LockedItemIdsInt { get; set; }
            [Key(5)] public List<int> UnlockedItemIdsInt { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGameUnlockStateProtocolMessagePack() { }
            public ResponseGameUnlockStateProtocolMessagePack(List<string> unlockCraftRecipeGuidsStr, List<string> lockedCraftRecipeGuidsStr, List<int> lockedItemIds, List<int> unlockedItemIds)
            {
                Tag = ProtocolTag;
                UnlockCraftRecipeGuidsStr = unlockCraftRecipeGuidsStr;
                LockedCraftRecipeGuidsStr = lockedCraftRecipeGuidsStr;
                LockedItemIdsInt = lockedItemIds;
                UnlockedItemIdsInt = unlockedItemIds;
            }
        }
    }
}
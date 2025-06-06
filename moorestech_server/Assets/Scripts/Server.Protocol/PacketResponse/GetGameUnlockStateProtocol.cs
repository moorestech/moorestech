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

            // Get challenge unlock states
            var lockedChallenge = new List<string>();
            var unlockedChallenge = new List<string>();
            foreach (var challenge in gameUnlockStateData.ChallengeUnlockStateInfos.Values)
            {
                if (challenge.IsUnlocked)
                {
                    unlockedChallenge.Add(challenge.ChallengeGuid.ToString());
                }
                else
                {
                    lockedChallenge.Add(challenge.ChallengeGuid.ToString());
                }
            }
            
            // Pass challenge unlock states to the constructor
            return new ResponseGameUnlockStateProtocolMessagePack(unlockedCraftRecipe, lockedCraftRecipe, lockedItem, unlockedItem, lockedChallenge, unlockedChallenge);
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
            [IgnoreMember] public List<Guid> UnlockedCraftRecipeGuids => UnlockedCraftRecipeGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedCraftRecipeGuids => LockedCraftRecipeGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<ItemId> UnlockedItemIds => UnlockedItemIdsInt.Select(i => new ItemId(i)).ToList();
            [IgnoreMember] public List<ItemId> LockedItemIds => LockedItemIdsInt.Select(i => new ItemId(i)).ToList();
            [IgnoreMember] public List<Guid> UnlockedChallengeGuids => UnlockedChallengeGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedChallengeGuids => LockedChallengeGuidsStr.Select(Guid.Parse).ToList();
            
            [Key(2)] public List<string> UnlockedCraftRecipeGuidsStr { get; set; }
            [Key(3)] public List<string> LockedCraftRecipeGuidsStr { get; set; }
            
            [Key(4)] public List<int> LockedItemIdsInt { get; set; }
            [Key(5)] public List<int> UnlockedItemIdsInt { get; set; }

            [Key(6)] public List<string> LockedChallengeGuidsStr { get; set; }
            [Key(7)] public List<string> UnlockedChallengeGuidsStr { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGameUnlockStateProtocolMessagePack() { }
            public ResponseGameUnlockStateProtocolMessagePack(
                List<string> unlockedCraftRecipeGuidsStr, List<string> lockedCraftRecipeGuidsStr,
                List<int> lockedItemIds, List<int> unlockedItemIds,
                List<string> lockedChallengeGuidsStr, List<string> unlockedChallengeGuidsStr) // Added challenge parameters
            {
                Tag = ProtocolTag;
                UnlockedCraftRecipeGuidsStr = unlockedCraftRecipeGuidsStr;
                LockedCraftRecipeGuidsStr = lockedCraftRecipeGuidsStr;
                LockedItemIdsInt = lockedItemIds;
                UnlockedItemIdsInt = unlockedItemIds;
                LockedChallengeGuidsStr = lockedChallengeGuidsStr; // Added assignment
                UnlockedChallengeGuidsStr = unlockedChallengeGuidsStr; // Added assignment
            }
        }
    }
}
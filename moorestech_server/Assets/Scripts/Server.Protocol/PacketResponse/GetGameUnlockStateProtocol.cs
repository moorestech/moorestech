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
        
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
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
            var lockedChallengeCategory = new List<string>();
            var unlockedChallengeCategory = new List<string>();
            foreach (var challenge in gameUnlockStateData.ChallengeCategoryUnlockStateInfos.Values)
            {
                if (challenge.IsUnlocked)
                {
                    unlockedChallengeCategory.Add(challenge.ChallengeCategoryGuid.ToString());
                }
                else
                {
                    lockedChallengeCategory.Add(challenge.ChallengeCategoryGuid.ToString());
                }
            }
            
            // 機械レシピのアンロック状態を取得
            // Get machine recipe unlock states
            var lockedMachineRecipe = new List<string>();
            var unlockedMachineRecipe = new List<string>();
            foreach (var machineRecipe in gameUnlockStateData.MachineRecipeUnlockStateInfos.Values)
            {
                if (machineRecipe.IsUnlocked)
                {
                    unlockedMachineRecipe.Add(machineRecipe.MachineRecipeGuid.ToString());
                }
                else
                {
                    lockedMachineRecipe.Add(machineRecipe.MachineRecipeGuid.ToString());
                }
            }

            // ブロックと列車車両のアンロック状態を取得
            // Get block and train car unlock states
            var lockedBlock = new List<string>();
            var unlockedBlock = new List<string>();
            foreach (var block in gameUnlockStateData.BlockUnlockStateInfos.Values)
            {
                if (block.IsUnlocked) unlockedBlock.Add(block.BlockGuid.ToString());
                else lockedBlock.Add(block.BlockGuid.ToString());
            }

            var lockedTrainCar = new List<string>();
            var unlockedTrainCar = new List<string>();
            foreach (var trainCar in gameUnlockStateData.TrainCarUnlockStateInfos.Values)
            {
                if (trainCar.IsUnlocked) unlockedTrainCar.Add(trainCar.TrainCarGuid.ToString());
                else lockedTrainCar.Add(trainCar.TrainCarGuid.ToString());
            }

            // 接続ツールのアンロック状態を取得
            // Get connect tool unlock states
            var lockedConnectTool = new List<string>();
            var unlockedConnectTool = new List<string>();
            foreach (var connectTool in gameUnlockStateData.ConnectToolUnlockStateInfos.Values)
            {
                if (connectTool.IsUnlocked) unlockedConnectTool.Add(connectTool.ConnectToolGuid.ToString());
                else lockedConnectTool.Add(connectTool.ConnectToolGuid.ToString());
            }

            return new ResponseGameUnlockStateProtocolMessagePack(
                unlockedCraftRecipe, lockedCraftRecipe,
                lockedItem, unlockedItem,
                lockedChallengeCategory, unlockedChallengeCategory,
                lockedMachineRecipe, unlockedMachineRecipe,
                lockedBlock, unlockedBlock,
                lockedTrainCar, unlockedTrainCar,
                lockedConnectTool, unlockedConnectTool);
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
            [IgnoreMember] public List<Guid> UnlockedCategoryChallengeGuids => UnlockedChallengeCategoryGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedCategoryChallengeGuids => LockedChallengeCategoryGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> UnlockedMachineRecipeGuids => UnlockedMachineRecipeGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedMachineRecipeGuids => LockedMachineRecipeGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> UnlockedBlockGuids => UnlockedBlockGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedBlockGuids => LockedBlockGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> UnlockedTrainCarGuids => UnlockedTrainCarGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedTrainCarGuids => LockedTrainCarGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> UnlockedConnectToolGuids => UnlockedConnectToolGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedConnectToolGuids => LockedConnectToolGuidsStr.Select(Guid.Parse).ToList();

            [Key(2)] public List<string> UnlockedCraftRecipeGuidsStr { get; set; }
            [Key(3)] public List<string> LockedCraftRecipeGuidsStr { get; set; }

            [Key(4)] public List<int> LockedItemIdsInt { get; set; }
            [Key(5)] public List<int> UnlockedItemIdsInt { get; set; }

            [Key(6)] public List<string> LockedChallengeCategoryGuidsStr { get; set; }
            [Key(7)] public List<string> UnlockedChallengeCategoryGuidsStr { get; set; }

            [Key(8)] public List<string> LockedMachineRecipeGuidsStr { get; set; }
            [Key(9)] public List<string> UnlockedMachineRecipeGuidsStr { get; set; }

            [Key(10)] public List<string> LockedBlockGuidsStr { get; set; }
            [Key(11)] public List<string> UnlockedBlockGuidsStr { get; set; }
            [Key(12)] public List<string> LockedTrainCarGuidsStr { get; set; }
            [Key(13)] public List<string> UnlockedTrainCarGuidsStr { get; set; }
            [Key(14)] public List<string> LockedConnectToolGuidsStr { get; set; }
            [Key(15)] public List<string> UnlockedConnectToolGuidsStr { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGameUnlockStateProtocolMessagePack() { }
            public ResponseGameUnlockStateProtocolMessagePack(
                List<string> unlockedCraftRecipeGuidsStr, List<string> lockedCraftRecipeGuidsStr,
                List<int> lockedItemIds, List<int> unlockedItemIds,
                List<string> lockedChallengeCategoryGuidsStr, List<string> unlockedChallengeCategoryGuidsStr,
                List<string> lockedMachineRecipeGuidsStr, List<string> unlockedMachineRecipeGuidsStr,
                List<string> lockedBlockGuidsStr, List<string> unlockedBlockGuidsStr,
                List<string> lockedTrainCarGuidsStr, List<string> unlockedTrainCarGuidsStr,
                List<string> lockedConnectToolGuidsStr, List<string> unlockedConnectToolGuidsStr)
            {
                Tag = ProtocolTag;
                UnlockedCraftRecipeGuidsStr = unlockedCraftRecipeGuidsStr;
                LockedCraftRecipeGuidsStr = lockedCraftRecipeGuidsStr;
                LockedItemIdsInt = lockedItemIds;
                UnlockedItemIdsInt = unlockedItemIds;
                LockedChallengeCategoryGuidsStr = lockedChallengeCategoryGuidsStr;
                UnlockedChallengeCategoryGuidsStr = unlockedChallengeCategoryGuidsStr;
                LockedMachineRecipeGuidsStr = lockedMachineRecipeGuidsStr;
                UnlockedMachineRecipeGuidsStr = unlockedMachineRecipeGuidsStr;
                LockedBlockGuidsStr = lockedBlockGuidsStr;
                UnlockedBlockGuidsStr = unlockedBlockGuidsStr;
                LockedTrainCarGuidsStr = lockedTrainCarGuidsStr;
                UnlockedTrainCarGuidsStr = unlockedTrainCarGuidsStr;
                LockedConnectToolGuidsStr = lockedConnectToolGuidsStr;
                UnlockedConnectToolGuidsStr = unlockedConnectToolGuidsStr;
            }
        }
    }
}
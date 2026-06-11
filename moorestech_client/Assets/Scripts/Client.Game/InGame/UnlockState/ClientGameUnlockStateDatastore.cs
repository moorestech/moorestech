using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Network.API;
using Core.Master;
using Game.UnlockState;
using Game.UnlockState.States;
using MessagePack;
using Server.Event.EventReceive;

namespace Client.Game.InGame.UnlockState
{
    public class ClientGameUnlockStateData : IGameUnlockStateData
    {
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _recipeUnlockStateInfos;
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos => _itemUnlockStateInfos;
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos => _challengeCategoryUnlockStateInfos;
        public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos => _machineRecipeUnlockStateInfos;


        private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _recipeUnlockStateInfos = new();
        private readonly Dictionary<ItemId, ItemUnlockStateInfo> _itemUnlockStateInfos = new();
        private readonly Dictionary<Guid, ChallengeCategoryUnlockStateInfo> _challengeCategoryUnlockStateInfos = new();
        private readonly Dictionary<Guid, MachineRecipeUnlockStateInfo> _machineRecipeUnlockStateInfos = new();
        
        public ClientGameUnlockStateData(InitialHandshakeResponse initialHandshakeResponse)
        {
            var unlockState = initialHandshakeResponse.UnlockState;
            foreach (var lockedGuid in unlockState.LockedCraftRecipeGuids)
            {
                _recipeUnlockStateInfos[lockedGuid] = new CraftRecipeUnlockStateInfo(lockedGuid, false);
            }
            foreach (var unlockedGuid in unlockState.UnlockedCraftRecipeGuids)
            {
                _recipeUnlockStateInfos[unlockedGuid] = new CraftRecipeUnlockStateInfo(unlockedGuid, true);
            }
            
            foreach (var lockedItemId in unlockState.LockedItemIds)
            {
                _itemUnlockStateInfos[lockedItemId] = new ItemUnlockStateInfo(lockedItemId, false);
            }
            foreach (var unlockedItemId in unlockState.UnlockedItemIds)
            {
                _itemUnlockStateInfos[unlockedItemId] = new ItemUnlockStateInfo(unlockedItemId, true);
            }
            
            foreach (var lockedChallengeId in unlockState.LockedChallengeCategoryGuids)
            {
                _challengeCategoryUnlockStateInfos[lockedChallengeId] = new ChallengeCategoryUnlockStateInfo(lockedChallengeId, false);
            }
            foreach (var unlockedChallengeId in unlockState.UnlockedChallengeCategoryGuids)
            {
                _challengeCategoryUnlockStateInfos[unlockedChallengeId] = new ChallengeCategoryUnlockStateInfo(unlockedChallengeId, true);
            }

            // 機械レシピのアンロック状態を初期化
            // Initialize machine recipe unlock states
            foreach (var lockedGuid in unlockState.LockedMachineRecipeGuids)
            {
                _machineRecipeUnlockStateInfos[lockedGuid] = new MachineRecipeUnlockStateInfo(lockedGuid, false);
            }
            foreach (var unlockedGuid in unlockState.UnlockedMachineRecipeGuids)
            {
                _machineRecipeUnlockStateInfos[unlockedGuid] = new MachineRecipeUnlockStateInfo(unlockedGuid, true);
            }

            ClientContext.VanillaApi.Event.SubscribeEventResponse(UnlockedEventPacket.EventTag, OnUpdateUnlock);
        }
        
        public void OnUpdateUnlock(byte[] payload)
        {
             var message = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(payload);
             
             switch (message.UnlockEventType)
             {
                 case UnlockEventType.CraftRecipe:
                     var recipeGuid = message.UnlockedCraftRecipeGuid;
                     _recipeUnlockStateInfos[recipeGuid] = new CraftRecipeUnlockStateInfo(recipeGuid, true);
                     break;
                 case UnlockEventType.Item:
                     var itemId = message.UnlockedItemId;
                     _itemUnlockStateInfos[itemId] = new ItemUnlockStateInfo(itemId, true);
                     break;
                 case UnlockEventType.ChallengeCategory:
                     var challengeId = message.UnlockedChallengeCategoryGuid;
                     _challengeCategoryUnlockStateInfos[challengeId] = new ChallengeCategoryUnlockStateInfo(challengeId, true);
                     break;
                 case UnlockEventType.MachineRecipe:
                     var machineRecipeGuid = message.UnlockedMachineRecipeGuid;
                     _machineRecipeUnlockStateInfos[machineRecipeGuid] = new MachineRecipeUnlockStateInfo(machineRecipeGuid, true);
                     break;
                 default:
                     throw new ArgumentOutOfRangeException();
             }
        }
    }
}
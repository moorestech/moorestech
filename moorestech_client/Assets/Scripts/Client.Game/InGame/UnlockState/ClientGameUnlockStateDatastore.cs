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
    public class ClientGameIiGameUnlockStateData : IGameUnlockStateData
    {
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _recipeUnlockStateInfos;
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos => _itemUnlockStateInfos;
        
        
        private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _recipeUnlockStateInfos = new();
        private readonly Dictionary<ItemId, ItemUnlockStateInfo> _itemUnlockStateInfos = new();
        
        public ClientGameIiGameUnlockStateData(InitialHandshakeResponse initialHandshakeResponse)
        {
            var unlockState = initialHandshakeResponse.UnlockCraftRecipeState;
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
                 default:
                     throw new ArgumentOutOfRangeException();
             }
        }
    }
}
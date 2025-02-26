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
            var unlockCraftRecipeState = initialHandshakeResponse.UnlockCraftRecipeState;
            foreach (var lockedGuid in unlockCraftRecipeState.LockedCraftRecipeGuids)
            {
                _recipeUnlockStateInfos[lockedGuid] = new CraftRecipeUnlockStateInfo(lockedGuid, false);
            }
            foreach (var unlockedGuid in unlockCraftRecipeState.UnlockedCraftRecipeGuids)
            {
                _recipeUnlockStateInfos[unlockedGuid] = new CraftRecipeUnlockStateInfo(unlockedGuid, true);
            }
            
            ClientContext.VanillaApi.Event.SubscribeEventResponse(UnlockedEventPacket.EventTag, OnUpdateUnlockCraftRecipe);
        }
        
        public void OnUpdateUnlockCraftRecipe(byte[] payload)
        {
             var message = MessagePackSerializer.Deserialize<UnlockCraftRecipeEventMessagePack>(payload);
             
             var recipeGuid = message.UnlockedCraftRecipeGuid;
            _recipeUnlockStateInfos[recipeGuid] = new CraftRecipeUnlockStateInfo(recipeGuid, true);
        }
    }
}
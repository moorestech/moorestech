using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Network.API;
using Game.UnlockState;
using MessagePack;
using Server.Event.EventReceive;

namespace Client.Game.InGame.UnlockState
{
    public class ClientGameUnlockStateDatastore : IUnlockCraftRecipeStateDatastore
    {
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _recipeUnlockStateInfos;
        private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _recipeUnlockStateInfos = new();
        
        public ClientGameUnlockStateDatastore(InitialHandshakeResponse initialHandshakeResponse)
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
            
            ClientContext.VanillaApi.Event.SubscribeEventResponse(UnlockedCraftRecipeEventPacket.EventTag, OnUpdateUnlockCraftRecipe);
        }
        
        public void OnUpdateUnlockCraftRecipe(byte[] payload)
        {
             var message = MessagePackSerializer.Deserialize<UnlockCraftRecipeEventMessagePack>(payload);
             
             var recipeGuid = message.UnlockedCraftRecipeGuid;
            _recipeUnlockStateInfos[recipeGuid] = new CraftRecipeUnlockStateInfo(recipeGuid, true);
        }
    }
}
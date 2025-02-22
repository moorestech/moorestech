using System;
using System.Collections.Generic;
using Game.UnlockState;

namespace Client.Game.InGame.UnlockState
{
    public class ClientGameUnlockStateDatastore : IUnlockCraftRecipeStateDatastore
    {
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _recipeUnlockStateInfos;
        private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _recipeUnlockStateInfos = new();
        
        public void SetUnlockState(Guid recipeGuid, bool isUnlocked)
        {
            _recipeUnlockStateInfos[recipeGuid] = new CraftRecipeUnlockStateInfo(recipeGuid, isUnlocked);
        }
    }
}
using System;
using System.Collections.Generic;

namespace Game.UnlockState
{
    public interface IUnlockCraftRecipeStateDatastore
    {
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; }
    }
    
    public interface IGameUnlockStateDatastore : IUnlockCraftRecipeStateDatastore
    {
        public IObservable<Guid> OnUnlockCraftRecipe { get; }
        void UnlockCraftRecipe(Guid recipeGuid);
        
        
        void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject);
        GameUnlockStateJsonObject GetSaveJsonObject();
    }
}
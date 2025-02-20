using System;
using Newtonsoft.Json;

namespace Game.UnlockState
{
    public class RecipeUnlockStateInfo
    {
        public Guid RecipeGuid { get; }
        public bool IsUnlocked { get; private set; }
        
        public RecipeUnlockStateInfo(Guid recipeGuid, bool isUnlocked)
        {
            RecipeGuid = recipeGuid;
            IsUnlocked = isUnlocked;
        }
        
        public RecipeUnlockStateInfo(RecipeUnlockStateInfoJsonObject jsonObject)
        {
            RecipeGuid = Guid.Parse(jsonObject.RecipeGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }
        
        public RecipeUnlockStateInfoJsonObject GetSaveJsonObject()
        {
            return new RecipeUnlockStateInfoJsonObject
            {
                RecipeGuid = RecipeGuid.ToString(),
                IsUnlocked = IsUnlocked
            };
        }
    }
    
    public class RecipeUnlockStateInfoJsonObject
    {
        [JsonProperty("recipeGuid")] public string RecipeGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;
    }
}
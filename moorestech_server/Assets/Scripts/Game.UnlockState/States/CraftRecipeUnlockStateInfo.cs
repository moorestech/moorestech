using System;
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class CraftRecipeUnlockStateInfo
    {
        public Guid CraftRecipeGuid { get; }
        public bool IsUnlocked { get; private set; }
        
        public CraftRecipeUnlockStateInfo(Guid craftRecipeGuid, bool isUnlocked)
        {
            CraftRecipeGuid = craftRecipeGuid;
            IsUnlocked = isUnlocked;
        }
        
        public CraftRecipeUnlockStateInfo(CraftRecipeUnlockStateInfoJsonObject jsonObject)
        {
            CraftRecipeGuid = Guid.Parse(jsonObject.CraftRecipeGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }
        
        
        public void Unlock()
        {
            IsUnlocked = true;
        }
    }
    
    public class CraftRecipeUnlockStateInfoJsonObject
    {
        [JsonProperty("guid")] public string CraftRecipeGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;
        
        public CraftRecipeUnlockStateInfoJsonObject() { }
        
        public CraftRecipeUnlockStateInfoJsonObject(CraftRecipeUnlockStateInfo craftRecipeUnlockStateInfo)
        {
            CraftRecipeGuid = craftRecipeUnlockStateInfo.CraftRecipeGuid.ToString();
            IsUnlocked = craftRecipeUnlockStateInfo.IsUnlocked;
        }
    }
}
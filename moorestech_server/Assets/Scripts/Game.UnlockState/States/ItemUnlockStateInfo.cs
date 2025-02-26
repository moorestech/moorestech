using System;
using Core.Master;
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class ItemUnlockStateInfo
    {
        public ItemId ItemId;
        public bool IsUnlocked { get; private set; }
        
        public ItemUnlockStateInfo(ItemId itemId, bool isUnlocked)
        {
            ItemId = itemId;
            IsUnlocked = isUnlocked;
        }
        
        public ItemUnlockStateInfo(ItemUnlockStateInfoJsonObject jsonObject)
        {
            ItemId = MasterHolder.ItemMaster.GetItemId(Guid.Parse(jsonObject.ItemGuid));
            IsUnlocked = jsonObject.IsUnlocked;
        }
        
        public void Unlock()
        {
            IsUnlocked = true;
        }
    }
    
    public class ItemUnlockStateInfoJsonObject
    {
        [JsonProperty("guid")] public string ItemGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;
        
        public ItemUnlockStateInfoJsonObject() { }
        
        public ItemUnlockStateInfoJsonObject(ItemUnlockStateInfo craftRecipeUnlockStateInfo)
        {
            var itemMaster = MasterHolder.ItemMaster.GetItemMaster(craftRecipeUnlockStateInfo.ItemId);
            ItemGuid = itemMaster.ItemGuid.ToString();
            IsUnlocked = craftRecipeUnlockStateInfo.IsUnlocked;
        }
    }
}
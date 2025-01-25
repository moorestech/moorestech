using System;
using Core.Master;
using Newtonsoft.Json;

namespace Core.Item.Interface
{
    public class ItemStackSaveJsonObject
    {
        [JsonProperty("itemGuid")]
        public string ItemGuidStr;
        [JsonProperty("count")]
        public int Count;
        
        [JsonIgnore] public Guid ItemGuid => Guid.Parse(ItemGuidStr);
        
            
        public ItemStackSaveJsonObject() { }
        
        public ItemStackSaveJsonObject(IItemStack itemStack)
        {
            ItemGuidStr = 
                itemStack.Id == ItemMaster.EmptyItemId ?
                    Guid.Empty.ToString() : 
                    MasterHolder.ItemMaster.GetItemMaster(itemStack.Id).ItemGuid.ToString();
            Count = itemStack.Count;
        }
    }
}
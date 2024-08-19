using System;
using Core.Master;
using Newtonsoft.Json;

namespace Core.Item.Interface
{
    public class ItemStackSaveJsonObject
    {
        [JsonProperty("itemGuid")]
        public string ItemGuidStr;
        [JsonProperty("id")]
        public int Count;
        
        [JsonIgnore]
        public Guid ItemGuid => Guid.Parse(ItemGuidStr);
            
        public ItemStackSaveJsonObject() { }
        
        public ItemStackSaveJsonObject(IItemStack itemStack)
        {
            Count = itemStack.Count;
            ItemGuidStr = ItemMaster.GetItemMaster(itemStack.Id).ItemId.ToString();
        }
    }
}
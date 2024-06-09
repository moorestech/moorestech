using Newtonsoft.Json;

namespace Core.Item.Interface
{
    public class ItemStackJsonObject
    {
        [JsonProperty("itemHash")]
        public long ItemHash;
        
        [JsonProperty("id")]
        public int Count;
        
        public ItemStackJsonObject() { }
        
        public ItemStackJsonObject(IItemStack itemStack)
        {
            ItemHash = itemStack.ItemHash;
            Count = itemStack.Count;
        }
    }
}
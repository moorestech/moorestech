using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Crafting.Config
{
    [JsonObject]
    public class CraftConfigDataElement
    {
        [JsonProperty("requiredItems")] private List<CraftItem> _requiredItems;
        [JsonProperty("resultItem")] private CraftItem _resultItem;
        
        public List<CraftItem> RequiredItems => _requiredItems;
        
        public CraftItem ResultItem => _resultItem;
    }
    
    [JsonObject]
    public class CraftItem
    {
        [JsonProperty("count")] private int _count;
        [JsonProperty("isRemain")] private bool _isRemain;
        [JsonProperty("itemName")] private string _itemName;
        [JsonProperty("modId")] private string _modId;
        
        public string ItemName => _itemName;
        public string ModId => _modId;
        
        public int Count => _count;
        
        public bool IsRemain => _isRemain;
    }
}
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Crafting.Config
{
    [JsonObject]
    public class CraftConfigDataElement
    {
        [JsonProperty("items")] private List<CraftItem> _items;
        [JsonProperty("result")] private CraftItem _result;

        public List<CraftItem> Items => _items;

        public CraftItem Result => _result;
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
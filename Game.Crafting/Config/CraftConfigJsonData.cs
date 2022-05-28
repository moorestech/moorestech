using System.Collections.Generic;
using System.Runtime.Serialization;
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
        [JsonProperty("id")] private int _id;
        [JsonProperty("count")] private int _count;

        public int Id => _id;

        public int Count => _count;
    }
}
using Newtonsoft.Json;

namespace Core.Util
{
    [JsonObject("ItemJsonData")]
    public class ItemJsonData
    {
        [JsonProperty("modId")]
        public string ModId;
        [JsonProperty("name")]
        public string Name;
        [JsonProperty("cnt")]
        public int Count;
    }
}
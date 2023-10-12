using Newtonsoft.Json;

namespace Core.Util
{
    [JsonObject("ItemJsonData")]
    public class ItemJsonData
    {
        [JsonProperty("cnt")] public int Count;

        [JsonProperty("modId")] public string ModId;

        [JsonProperty("name")] public string Name;
    }
}
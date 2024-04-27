using System;
using Newtonsoft.Json;

namespace Game.Map.Interface.Config
{
    [JsonObject("SpaceAssets")]
    public class MapObjectConfigJson
    {
        [JsonProperty("type")] public string Type;
        [JsonProperty("hp")] public int Hp;

        [JsonProperty("earnItems")] public MapObjectEarnItemConfigJson[] EarnItems;
    }

    public class MapObjectEarnItemConfigJson
    {
        [JsonProperty("itemModId")] public string ModId;
        [JsonProperty("itemName")] public string ItemName;

        [JsonProperty("minCount")] public int MinCount;
        [JsonProperty("maxCount")] public int MaxCount;
    }
}
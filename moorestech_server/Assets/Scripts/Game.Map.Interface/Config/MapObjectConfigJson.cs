using System;
using Newtonsoft.Json;

namespace Game.Map.Interface.Config
{
    [JsonObject("SpaceAssets")]
    public class MapObjectConfigJson
    {
        [JsonProperty("type")] public string Type;
        [JsonProperty("hp")] public int Hp;

        [JsonProperty("earnItemHps")] public int[] EarnItemHps;
        [JsonProperty("earnItems")] public MapObjectEarnItemConfigJson[] EarnItems;

        [JsonProperty("miningTools")] public MapObjectMiningToolConfigJson[] MiningTools;
    }

    public class MapObjectEarnItemConfigJson
    {
        [JsonProperty("itemModId")] public string ModId;
        [JsonProperty("itemName")] public string ItemName;

        [JsonProperty("minCount")] public int MinCount;
        [JsonProperty("maxCount")] public int MaxCount;
    }

    public class MapObjectMiningToolConfigJson
    {
        [JsonProperty("toolItemModId")] public string ToolItemModId;
        [JsonProperty("toolItemName")] public string ToolItemName;

        [JsonProperty("damage")] public int Damage;
        [JsonProperty("attackSpeed")] public float AttackSpeed;
    }
}
using Newtonsoft.Json;

namespace Game.Map.Interface.Config
{
    [JsonObject("SpaceAssets")]
    public class MapObjectConfigJson
    {
        [JsonProperty("earnItemHps")] public int[] EarnItemHps;
        [JsonProperty("earnItems")] public MapObjectEarnItemConfigJson[] EarnItems;
        [JsonProperty("hp")] public int Hp;
        
        [JsonProperty("miningTools")] public MapObjectMiningToolConfigJson[] MiningTools;
        [JsonProperty("type")] public string Type;
    }
    
    public class MapObjectEarnItemConfigJson
    {
        [JsonProperty("itemName")] public string ItemName;
        [JsonProperty("maxCount")] public int MaxCount;
        
        [JsonProperty("minCount")] public int MinCount;
        [JsonProperty("itemModId")] public string ModId;
    }
    
    public class MapObjectMiningToolConfigJson
    {
        [JsonProperty("attackSpeed")] public float AttackSpeed;
        
        [JsonProperty("damage")] public int Damage;
        [JsonProperty("toolItemModId")] public string ToolItemModId;
        [JsonProperty("toolItemName")] public string ToolItemName;
    }
}
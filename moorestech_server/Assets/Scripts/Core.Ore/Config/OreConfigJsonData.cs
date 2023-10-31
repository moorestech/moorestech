using Newtonsoft.Json;

namespace Core.Ore.Config
{
    [JsonObject("OreConfigJsonData")]
    public class OreConfigJsonData
    {
        [JsonProperty("itemModId")] private string _itemModId;
        [JsonProperty("itemName")] private string _itemName;
        [JsonProperty("name")] private string _name;
        [JsonProperty("priority")] private int _priority;
        [JsonProperty("veinFrequency")] private float _veinFrequency;
        [JsonProperty("veinSize")] private float _veinSize;

        public string ItemName => _itemName;

        public string ItemModId => _itemModId;

        public int Priority => _priority;

        public float VeinSize => _veinSize;

        public float VeinFrequency => _veinFrequency;

        public string Name => _name;
    }
}
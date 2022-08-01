using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Core.Ore.Config
{

    [JsonObject("OreConfigJsonData")]
    public class OreConfigJsonData
    {
        [JsonProperty("oreId")] private int _oreId;
        [JsonProperty("name")] private string _name;
        [JsonProperty("veinSize")] private float _veinSize;
        [JsonProperty("veinFrequency")] private float _veinFrequency;
        [JsonProperty("priority")] private int _priority;
        [JsonProperty("itemName")] private string _itemName;
        [JsonProperty("itemModId")] private string _itemModId;

        public string ItemName => _itemName;

        public string ItemModId => _itemModId;

        public int Priority => _priority;

        public float VeinSize => _veinSize;

        public float VeinFrequency => _veinFrequency;

        public int OreId => _oreId;

        public string Name => _name;
    }
}
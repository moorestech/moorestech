using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Core.Ore.Config
{

    [JsonObject("OreConfigJsonData")]
    public class OreConfigJsonData
    {
        [JsonProperty("oreId")] private int _oreId;
        [JsonProperty("name")] private string _name;
        [JsonProperty("miningItemId")] private int _miningItem;
        [JsonProperty("veinSize")] private float _veinSize;
        [JsonProperty("veinFrequency")] private float _veinFrequency;
        [JsonProperty("priority")] private int _priority;

        public int Priority => _priority;

        public int MiningItem => _miningItem;

        public float VeinSize => _veinSize;

        public float VeinFrequency => _veinFrequency;

        public int OreId => _oreId;

        public string Name => _name;

        public int MiningItemId => _miningItem;
    }
}
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Core.Ore.Config
{

    [JsonObject("OreConfigData")]
    public class OreConfigData
    {
        [DataMember(Name = "oreId")]
        private int _oreId;
        [DataMember(Name = "name")] private string _name;
        [DataMember(Name = "miningItemId")] private int _miningItem;
        [DataMember(Name = "veinSize")] private float _veinSize;
        [DataMember(Name = "veinFrequency")] private float _veinFrequency;
        [DataMember(Name = "priority")] private int _priority;

        public int Priority => _priority;

        public int MiningItem => _miningItem;

        public float VeinSize => _veinSize;

        public float VeinFrequency => _veinFrequency;

        public int OreId => _oreId;

        public string Name => _name;

        public int MiningItemId => _miningItem;
    }
}
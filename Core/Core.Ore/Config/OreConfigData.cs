using System.Runtime.Serialization;

namespace Core.Ore.Config
{
    [DataContract] 
    public class OreConfigData
    {
        [DataMember(Name = "oreId")]
        private int _oreId;
        [DataMember(Name = "name")]
        private string _name;
        [DataMember(Name = "miningItem")]
        private int _miningItem;

        public int OreId => _oreId;

        public string Name => _name;

        public int MiningItem => _miningItem;
    }
}
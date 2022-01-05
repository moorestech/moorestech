using System.Runtime.Serialization;

namespace Core.Ore.Config
{

    [DataContract] 
    public class OreConfigData
    {
        [DataMember(Name = "ores")]
        private OreConfigDataElement[] _oreElements;

        public OreConfigDataElement[] OreElements => _oreElements;
    }
    
    [DataContract] 
    public class OreConfigDataElement
    {
        [DataMember(Name = "oreId")]
        private int _oreId;
        [DataMember(Name = "name")]
        private string _name;
        [DataMember(Name = "miningItemId")]
        private int _miningItem;

        public int OreId => _oreId;

        public string Name => _name;

        public int MiningItemId => _miningItem;
    }
}
using System.Runtime.Serialization;

namespace Core.Ore.Config
{
    [DataContract]
    public class OreConfigData
    {
        [DataMember(Name = "ores")] private OreConfigDataElement[] _oreElements;

        public OreConfigDataElement[] OreElements => _oreElements;
    }

    [DataContract]
    public class OreConfigDataElement
    {
        [DataMember(Name = "oreId")] private int _oreId;
        [DataMember(Name = "name")] private string _name;
        [DataMember(Name = "miningItemId")] private int _miningItem;
        [DataMember(Name = "veinSize")] private int _veinSize;
        [DataMember(Name = "veinFrequency")] private int _veinFrequency;
        [DataMember(Name = "priority")] private int _priority;

        public int Priority => _priority;

        public int MiningItem => _miningItem;

        public int VeinSize => _veinSize;

        public int VeinFrequency => _veinFrequency;

        public int OreId => _oreId;

        public string Name => _name;

        public int MiningItemId => _miningItem;
    }
}
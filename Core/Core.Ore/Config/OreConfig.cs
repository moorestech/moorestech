using System.Collections.Generic;
using Core.Item.Util;

namespace Core.Ore.Config
{
    public class OreConfig : IOreConfig
    {
        private readonly Dictionary<int,OreConfigDataElement> _oreConfigData;
        
        public OreConfig()
        {
            _oreConfigData = new OreConfigJsonLoad().Load();
        }
        public int OreIdToItemId(int oreId)
        {
            return _oreConfigData.ContainsKey(oreId) ? 
                _oreConfigData[oreId].MiningItemId : 
                ItemConst.NullItemId;
        }
    }
}
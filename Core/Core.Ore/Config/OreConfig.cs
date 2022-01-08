using System.Collections.Generic;
using Core.Item.Util;

namespace Core.Ore.Config
{
    public class OreConfig : IOreConfig
    {
        private readonly Dictionary<int, OreConfigDataElement> _oreConfigData;

        public OreConfig()
        {
            _oreConfigData = new OreConfigJsonLoad().Load();
        }

        public int OreIdToItemId(int oreId)
        {
            return _oreConfigData.ContainsKey(oreId) ? _oreConfigData[oreId].MiningItemId : ItemConst.NullItemId;
        }

        public List<int> GetIds()
        {
            throw new System.NotImplementedException();
        }

        public List<int> GetSortedIdsForPriority()
        {
            throw new System.NotImplementedException();
        }

        public OreConfigDataElement Get(int oreId)
        {
            throw new System.NotImplementedException();
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using Core.Const;
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
            return _oreConfigData.ContainsKey(oreId) ? _oreConfigData[oreId].MiningItemId : ItemConst.EmptyItemId;
        }

        public List<int> GetIds()
        {
            return _oreConfigData.Keys.ToList();
        }

        public List<int> GetSortedIdsForPriority()
        {
            var values = _oreConfigData.Values.ToList();
            values.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            return values.Select(x => x.OreId).ToList();
        }

        public OreConfigDataElement Get(int oreId)
        {
            return _oreConfigData[oreId];
        }
    }
}
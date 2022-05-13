using System;
using System.Collections.Generic;
using System.Linq;
using Core.ConfigJson;
using Core.Const;
using Core.Item.Config;
using Core.Item.Util;

namespace Core.Ore.Config
{
    public class OreConfig : IOreConfig
    {
        private readonly List<OreConfigDataElement> _oreConfigData;

        public OreConfig(ConfigJsonList configJson)
        {
            _oreConfigData = new OreConfigJsonLoad().Load(configJson.SortedOreConfigJsonList);
        }

        public int OreIdToItemId(int oreId)
        {
            if (oreId < 0)
            {
                return ItemConst.EmptyItemId;
            }
            if (oreId < _oreConfigData.Count)
            {
                return _oreConfigData[oreId].MiningItemId;
            }

            return ItemConst.EmptyItemId;
        }

        public List<int> GetSortedIdsForPriority()
        {
            return _oreConfigData.
                OrderBy(x => x.Priority).
                Select(x => x.OreId).ToList();
        }

        public OreConfigDataElement Get(int oreId)
        {
            return _oreConfigData[oreId];
        }
    }
}
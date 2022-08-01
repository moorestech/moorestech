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
        private readonly List<OreConfigData> _oreConfigData;
        private readonly Dictionary<string,List<int>> _modIdToOreIds = new();
        private readonly IItemConfig _itemConfig;
        
        public OreConfig(ConfigJsonList configJson, IItemConfig itemConfig)
        {
            _itemConfig = itemConfig;
            _oreConfigData = new OreConfigJsonLoad().Load(configJson.SortedModIds, configJson.OreConfigs);
            foreach (var oreConfig in _oreConfigData)
            {                
                var oreId = oreConfig.OreId;
                if (_modIdToOreIds.TryGetValue(oreConfig.ModId, out var blockIds))
                {
                    blockIds.Add(oreId);
                }
                else
                {
                    _modIdToOreIds.Add(oreConfig.ModId, new List<int> {oreId});
                }
            }
        }

        public int OreIdToItemId(int oreId)
        {
            //0は空白なためインデックスをずらす
            oreId -= 1;
            if (oreId < 0)
            {
                return ItemConst.EmptyItemId;
            }
            if (oreId < _oreConfigData.Count)
            {
                return _itemConfig.GetItemId(_oreConfigData[oreId].ItemModId, _oreConfigData[oreId].ItemName); 
            }

            return ItemConst.EmptyItemId;
        }

        public List<int> GetSortedIdsForPriority()
        {
            return _oreConfigData.
                OrderBy(x => x.Priority).
                Select(x => x.OreId).ToList();
        }

        public OreConfigData Get(int oreId)
        {
            //0は空白なためインデックスをずらす
            oreId -= 1;
            return _oreConfigData[oreId];
        }

        public List<int> GetOreIds(string modId)
        {
            return _modIdToOreIds.TryGetValue(modId, out var oreIds) ? oreIds : new List<int>();
        }
    }
}
using System.Collections.Generic;
using Core.Item.Config;

namespace Game.Map.Interface.Config
{
    public class MapObjectConfigInfo
    {
        public readonly string Type;
        public readonly int Hp;

        public readonly List<MapObjectEarnItemConfigInfo> EarnItems;
        public MapObjectConfigInfo(MapObjectConfigJson configJson, IItemConfig itemConfig)
        {
            Type = configJson.Type;
            Hp = configJson.Hp;
            EarnItems = new List<MapObjectEarnItemConfigInfo>();
            foreach (var earnItemConfigJson in configJson.EarnItems)
            {
                EarnItems.Add(new MapObjectEarnItemConfigInfo(earnItemConfigJson, itemConfig));
            }
        }
    }

    public class MapObjectEarnItemConfigInfo
    {
        public readonly int ItemId;

        public readonly int MinCount;
        public readonly int MaxCount;
        public MapObjectEarnItemConfigInfo(MapObjectEarnItemConfigJson earnItemConfigJson, IItemConfig itemConfig)
        {
            ItemId = itemConfig.GetItemId(earnItemConfigJson.ModId, earnItemConfigJson.ItemName);
            MinCount = earnItemConfigJson.MinCount;
            MaxCount = earnItemConfigJson.MaxCount;
        }
    }
}
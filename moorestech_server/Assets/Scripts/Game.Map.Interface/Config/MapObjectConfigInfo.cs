using System.Collections.Generic;
using Core.Item.Config;

namespace Game.Map.Interface.Config
{
    public class MapObjectConfigInfo
    {
        public readonly string Type;
        public readonly int Hp;

        public readonly List<int> EarnItemHps = new();
        public readonly List<MapObjectEarnItemConfigInfo> EarnItems = new();

        public readonly List<MapObjectToolItemConfigInfo> MiningTools = new();

        public MapObjectConfigInfo(MapObjectConfigJson configJson, IItemConfig itemConfig)
        {
            Type = configJson.Type;
            Hp = configJson.Hp;

            EarnItemHps.AddRange(configJson.EarnItemHps);

            foreach (var earnItemConfigJson in configJson.EarnItems)
            {
                EarnItems.Add(new MapObjectEarnItemConfigInfo(earnItemConfigJson, itemConfig));
            }

            foreach (var tool in configJson.MiningTools)
            {
                var toolItemId = itemConfig.GetItemId(tool.ToolItemModId, tool.ToolItemName);
                MiningTools.Add(new MapObjectToolItemConfigInfo(toolItemId, tool.Damage, tool.AttackSpeed));
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

    public class MapObjectToolItemConfigInfo
    {
        public readonly int ToolItemId;

        public readonly int Damage;
        public readonly float AttackSpeed;

        public MapObjectToolItemConfigInfo(int toolItemId, int damage, float attackSpeed)
        {
            ToolItemId = toolItemId;
            Damage = damage;
            AttackSpeed = attackSpeed;
        }
    }
}
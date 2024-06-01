using System.Collections.Generic;
using Core.Item.Interface.Config;

namespace Game.Map.Interface.Config
{
    public class MapObjectConfigInfo
    {
        public readonly List<int> EarnItemHps = new();
        public readonly List<MapObjectEarnItemConfigInfo> EarnItems = new();
        public readonly int Hp;
        
        public readonly List<MapObjectToolItemConfigInfo> MiningTools = new();
        public readonly string Type;
        
        public MapObjectConfigInfo(MapObjectConfigJson configJson, IItemConfig itemConfig)
        {
            Type = configJson.Type;
            Hp = configJson.Hp;
            
            EarnItemHps.AddRange(configJson.EarnItemHps);
            
            foreach (var earnItemConfigJson in configJson.EarnItems) EarnItems.Add(new MapObjectEarnItemConfigInfo(earnItemConfigJson, itemConfig));
            
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
        public readonly int MaxCount;
        
        public readonly int MinCount;
        
        public MapObjectEarnItemConfigInfo(MapObjectEarnItemConfigJson earnItemConfigJson, IItemConfig itemConfig)
        {
            ItemId = itemConfig.GetItemId(earnItemConfigJson.ModId, earnItemConfigJson.ItemName);
            MinCount = earnItemConfigJson.MinCount;
            MaxCount = earnItemConfigJson.MaxCount;
        }
    }
    
    public class MapObjectToolItemConfigInfo
    {
        public readonly float AttackSpeed;
        
        public readonly int Damage;
        public readonly int ToolItemId;
        
        public MapObjectToolItemConfigInfo(int toolItemId, int damage, float attackSpeed)
        {
            ToolItemId = toolItemId;
            Damage = damage;
            AttackSpeed = attackSpeed;
        }
    }
}
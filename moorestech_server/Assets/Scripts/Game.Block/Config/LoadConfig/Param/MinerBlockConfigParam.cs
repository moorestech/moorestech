using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.EnergySystem;

namespace Game.Block.Config.LoadConfig.Param
{
    public class MinerBlockConfigParam : IBlockConfigParam
    {
        public readonly List<MineItemSetting> MineItemSettings;
        public readonly int OutputSlot;
        public readonly ElectricPower RequiredPower;
        
        private MinerBlockConfigParam(ElectricPower requiredPower, List<MineItemSetting> mineItemSettings, int outputSlot)
        {
            RequiredPower = requiredPower;
            MineItemSettings = mineItemSettings;
            OutputSlot = outputSlot;
        }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int requiredPower = blockParam.requiredPower;
            int outputSlot = blockParam.outputSlot;
            var oreSetting = new List<MineItemSetting>();
            foreach (var ore in blockParam.mineSettings)
            {
                int time = ore.time;
                string itemModId = ore.itemModId;
                string itemName = ore.itemName;
                
                var itemId = itemConfig.GetItemId(itemModId, itemName);
                
                oreSetting.Add(new MineItemSetting(time, itemId));
            }
            
            return new MinerBlockConfigParam(new ElectricPower(requiredPower), oreSetting, outputSlot);
        }
    }
    
    public class MineItemSetting
    {
        public readonly int ItemId;
        public readonly int MiningTime;
        
        public MineItemSetting(int miningTime, int itemId)
        {
            MiningTime = miningTime;
            ItemId = itemId;
        }
    }
}
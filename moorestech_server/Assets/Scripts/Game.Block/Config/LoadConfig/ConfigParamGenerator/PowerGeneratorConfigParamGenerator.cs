using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class PowerGeneratorConfigParamGenerator : IBlockConfigParamGenerator
    {
        private readonly IItemConfig _itemConfig;
        
        public PowerGeneratorConfigParamGenerator(IItemConfig itemConfig)
        {
            _itemConfig = itemConfig;
        }
        
        public IBlockConfigParam Generate(dynamic blockParam)
        {
            var fuelSettings = new Dictionary<int, FuelSetting>();
            foreach (var fuel in blockParam.fuel)
            {
                // TODO modパースのエラー
                
                string itemModId = fuel.itemModId;
                string idItemName = fuel.itemName;
                int time = fuel.time;
                int power = fuel.power;
                
                var itemId = _itemConfig.GetItemId(itemModId, idItemName);
                
                fuelSettings.Add(itemId, new FuelSetting(itemId, time, power));
            }
            
            int fuelSlot = blockParam.fuelSlot;
            bool isInfinityPower = blockParam.isInfinityPower;
            int infinityPower = blockParam.infinityPower;
            
            return new PowerGeneratorConfigParam(fuelSettings, fuelSlot, isInfinityPower, infinityPower);
        }
    }
}
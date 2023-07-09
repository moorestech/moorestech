using System.Collections.Generic;
using Core.Block.Config.LoadConfig.Param;
using Core.Item.Config;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
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
                
                fuelSettings.Add(itemId, new FuelSetting(itemId,time, power));
            }

            int fuelSlot = blockParam.fuelSlot;

            return new PowerGeneratorConfigParam(fuelSettings, fuelSlot);
        }
    }
}
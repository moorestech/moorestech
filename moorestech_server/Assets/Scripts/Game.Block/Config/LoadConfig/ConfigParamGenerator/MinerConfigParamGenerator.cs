using System.Collections.Generic;
using Core.Item.Config;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;
using Game.Context;

namespace Game.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class MinerConfigParamGenerator : IBlockConfigParamGenerator
    { 
        public IBlockConfigParam Generate(dynamic blockParam)
        {
            int requiredPower = blockParam.requiredPower;
            int outputSlot = blockParam.outputSlot;
            var oreSetting = new List<MineItemSetting>();
            foreach (var ore in blockParam.mineSettings)
            {
                int time = ore.time;
                string itemModId = ore.itemModId;
                string itemName = ore.itemName;

                var itemId = ServerContext.ItemConfig.GetItemId(itemModId, itemName);

                oreSetting.Add(new MineItemSetting(time, itemId));
            }

            return new MinerBlockConfigParam(requiredPower, oreSetting, outputSlot);
        }
    }
}
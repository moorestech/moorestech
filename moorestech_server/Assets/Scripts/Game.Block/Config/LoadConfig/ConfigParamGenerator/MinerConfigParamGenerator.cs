using System.Collections.Generic;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class MinerConfigParamGenerator : IBlockConfigParamGenerator
    {
        public IBlockConfigParam Generate(dynamic blockParam)
        {
            int requiredPower = blockParam.requiredPower;
            int outputSlot = blockParam.outputSlot;
            var oreSetting = new List<MineItemSetting>();
            foreach (var ore in blockParam.oreSetting)
            {
                int time = ore.time;
                string itemModId = ore.itemModId;
                string itemName = ore.itemName;
                oreSetting.Add(new MineItemSetting(time, itemModId, itemName));
            }

            return new MinerBlockConfigParam(requiredPower, oreSetting, outputSlot);
        }
    }
}
using System;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;

namespace World.Service
{
    public class MaxElectricPoleMachineConnectionRange
    {
        int maxElectricPoleMachineConnectionRange = Int32.MinValue;

        public MaxElectricPoleMachineConnectionRange(IBlockConfig blockConfig)
        {
            foreach (var id in blockConfig.GetBlockIds())
            {
                if (blockConfig.GetBlockConfig(id).Type != VanillaBlockType.ElectricPole) continue;

                var param = blockConfig.GetBlockConfig(id).Param as ElectricPoleConfigParam;
                if (maxElectricPoleMachineConnectionRange < param.machineConnectionRange)
                {
                    maxElectricPoleMachineConnectionRange = param.machineConnectionRange;
                }
            }
        }

        public int Get()
        {
            return maxElectricPoleMachineConnectionRange;
        }
    }
}
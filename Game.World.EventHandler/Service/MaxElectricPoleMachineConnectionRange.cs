using System;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;

namespace Game.World.EventHandler.Service
{
    public class MaxElectricPoleMachineConnectionRange
    {
        private readonly int _maxElectricPoleMachineConnectionRange = Int32.MinValue;

        public MaxElectricPoleMachineConnectionRange(IBlockConfig blockConfig)
        {
            for (int i = 1; i < blockConfig.GetBlockConfigCount(); i++)
            {
                if (blockConfig.GetBlockConfig(i).Type != VanillaBlockType.ElectricPole) continue;

                var param = blockConfig.GetBlockConfig(i).Param as ElectricPoleConfigParam;
                if (_maxElectricPoleMachineConnectionRange < param.machineConnectionRange)
                {
                    _maxElectricPoleMachineConnectionRange = param.machineConnectionRange;
                }
                
            }
        }

        public int Get()
        {
            return _maxElectricPoleMachineConnectionRange;
        }
    }
}
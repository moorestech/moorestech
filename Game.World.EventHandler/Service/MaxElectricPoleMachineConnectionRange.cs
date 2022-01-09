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
            foreach (var id in blockConfig.GetBlockIds())
            {
                if (blockConfig.GetBlockConfig(id).Type != VanillaBlockType.ElectricPole) continue;

                var param = blockConfig.GetBlockConfig(id).Param as ElectricPoleConfigParam;
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
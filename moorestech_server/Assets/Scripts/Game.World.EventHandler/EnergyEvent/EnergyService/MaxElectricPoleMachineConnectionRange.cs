using Game.Block;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;
using Game.Context;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public class MaxElectricPoleMachineConnectionRange
    {
        private readonly int _maxElectricPoleMachineConnectionRange = int.MinValue;

        public MaxElectricPoleMachineConnectionRange()
        {
            var blockConfig = ServerContext.BlockConfig;
            for (var i = 1; i < blockConfig.GetBlockConfigCount(); i++)
            {
                if (blockConfig.GetBlockConfig(i).Type != VanillaBlockType.ElectricPole) continue;

                var param = blockConfig.GetBlockConfig(i).Param as ElectricPoleConfigParam;
                if (_maxElectricPoleMachineConnectionRange < param.machineConnectionRange)
                    _maxElectricPoleMachineConnectionRange = param.machineConnectionRange;
            }
        }

        public int Get()
        {
            return _maxElectricPoleMachineConnectionRange;
        }
    }
}
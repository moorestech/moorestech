using Core.Master;
using Game.Block;
using Mooresmaster.Model.BlocksModule;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public class MaxElectricPoleMachineConnectionRange
    {
        private readonly int _maxElectricPoleMachineConnectionRange = int.MinValue;
        
        public MaxElectricPoleMachineConnectionRange()
        {
            foreach (var blockElement in MasterHolder.BlockMaster.Blocks.Data)
            {
                if (blockElement.BlockType != VanillaBlockType.ElectricPole) continue;
                
                var param = blockElement.BlockParam as ElectricPoleBlockParam;
                if (_maxElectricPoleMachineConnectionRange < param.MachineConnectionRange)
                    _maxElectricPoleMachineConnectionRange = param.MachineConnectionRange;
            }
        }
        
        public int Get()
        {
            return _maxElectricPoleMachineConnectionRange;
        }
    }
}
using Core.Master;
using Game.Block;
using Mooresmaster.Model.BlocksModule;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public class MaxElectricPoleMachineConnectionRange
    {
        private const int FallbackRange = 1;
        private readonly int _maxElectricPoleMachineConnectionHorizontalRange = int.MinValue;
        private readonly int _maxElectricPoleMachineConnectionHeightRange = int.MinValue;
        
        public MaxElectricPoleMachineConnectionRange()
        {
            foreach (var blockElement in MasterHolder.BlockMaster.Blocks.Data)
            {
                if (blockElement.BlockType != BlockTypeConst.ElectricPole) continue;
                
                var param = blockElement.BlockParam as ElectricPoleBlockParam;
                if (_maxElectricPoleMachineConnectionHorizontalRange < param.MachineConnectionRange)
                    _maxElectricPoleMachineConnectionHorizontalRange = param.MachineConnectionRange;

                if (_maxElectricPoleMachineConnectionHeightRange < param.MachineConnectionHeightRange)
                    _maxElectricPoleMachineConnectionHeightRange = param.MachineConnectionHeightRange;
            }
        }
        
        public int Get()
        {
            return NormalizeRange(_maxElectricPoleMachineConnectionHorizontalRange);
        }

        public int GetHorizontal()
        {
            return NormalizeRange(_maxElectricPoleMachineConnectionHorizontalRange);
        }

        public int GetHeight()
        {
            return NormalizeRange(_maxElectricPoleMachineConnectionHeightRange);
        }

        private static int NormalizeRange(int value)
        {
            return value == int.MinValue ? FallbackRange : value;
        }
    }
}

using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class ElectricPoleConfigParamGenerator : IBlockConfigParamGenerator
    {
        public IBlockConfigParam Generate(dynamic blockParam)
        {
            int poleConnectionRange = blockParam.poleConnectionRange;
            int machineConnectionRange = blockParam.machineConnectionRange;
            
            return new ElectricPoleConfigParam(poleConnectionRange, machineConnectionRange);
        }
    }
}
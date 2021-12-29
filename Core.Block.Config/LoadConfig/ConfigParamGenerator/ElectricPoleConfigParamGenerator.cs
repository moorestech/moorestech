using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class ElectricPoleConfigParamGenerator : IBlockConfigParamGenerator
    {
        public BlockConfigParamBase Generate(dynamic blockParam)
        {
            int poleConnectionRange = blockParam.poleConnectionRange;
            int machineConnectionRange = blockParam.machineConnectionRange;
            
            return new ElectricPoleConfigParam(poleConnectionRange,machineConnectionRange);
        }
    }
}
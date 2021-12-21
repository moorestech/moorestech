using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class MachineConfigParamGenerator : IBlockConfigParamGenerator
    {
        public BlockConfigParamBase Generate(dynamic blockParam)
        {
            int inputSlot = blockParam.inputSlot;
            int outputSlot = blockParam.outputSlot;
            int requiredPower = blockParam.requiredPower;
            return new MachineBlockConfigParam(inputSlot,outputSlot,requiredPower);
        }
    }
}
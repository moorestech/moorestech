using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class BeltConveyorConfigParamGenerator : IBlockConfigParamGenerator
    {
        public IBlockConfigParam Generate(dynamic blockParam)
        {
            int slot = blockParam.slot;
            int time = blockParam.time;

            return new BeltConveyorConfigParam(time, slot);
        }
    }
}
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.ConfigParamGenerator
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
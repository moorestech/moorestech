using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class ChestConfigParamGenerator : IBlockConfigParamGenerator
    {
        public IBlockConfigParam Generate(dynamic blockParam)
        {
            int slot = blockParam.slot;

            return new ChestConfigParam(slot);
        }
    }
}
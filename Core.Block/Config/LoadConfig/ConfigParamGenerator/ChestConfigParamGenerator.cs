using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
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
using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class BlockConfigParamGenerator : IBlockConfigParamGenerator
    {
        public BlockConfigParamBase Generate(dynamic blockParam)
        {
            return new NullBlockConfigParam();
        }
    }
}
using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
{
    public interface IBlockConfigParamGenerator
    {
        public BlockConfigParamBase Generate(dynamic blockParam);
    }
}
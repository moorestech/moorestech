using Core.Block.Config.Param;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
{
    public interface IBlockConfigParamGenerator
    {
        public BlockConfigParamBase Generate(dynamic blockParam);
    }
}
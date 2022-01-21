using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
{
    public interface IBlockConfigParamGenerator
    {
        public IBlockConfigParam Generate(dynamic blockParam);
    }
}
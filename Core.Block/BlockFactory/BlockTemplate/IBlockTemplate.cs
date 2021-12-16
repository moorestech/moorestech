using Core.Block.Config;
using Core.Block.Config.LoadConfig;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public interface IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int intId);
    }
}
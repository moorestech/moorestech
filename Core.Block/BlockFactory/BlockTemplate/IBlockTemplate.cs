using Core.Block.Blocks;
using Core.Block.Config;
using Core.Block.Config.LoadConfig;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public interface IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int intId);
        public IBlock Load(BlockConfigData param, int intId,string state);
    }
}
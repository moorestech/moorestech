using Core.Block.Blocks;
using Core.Block.Blocks.Miner;
using Core.Block.Config.LoadConfig;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaMinerTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int intId)
        {
            return new VanillaMiner(param.BlockId, intId);
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            return new VanillaMiner(param.BlockId, intId);
        }
    }
}
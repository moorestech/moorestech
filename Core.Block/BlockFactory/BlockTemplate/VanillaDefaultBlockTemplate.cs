using Core.Block.Config.LoadConfig;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaDefaultBlock : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int intId)
        {
            return new NormalBlock(param.BlockId,intId);
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            return new NormalBlock(param.BlockId,intId);
        }
    }
}
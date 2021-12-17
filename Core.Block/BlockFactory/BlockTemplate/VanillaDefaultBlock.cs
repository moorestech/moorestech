using Core.Block.Config.LoadConfig;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaDefaultBlock : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int intId)
        {
            return new NormalBlock(param.Id,intId);
        }
    }
}
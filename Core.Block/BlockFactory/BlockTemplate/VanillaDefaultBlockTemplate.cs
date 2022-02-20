using Core.Block.Blocks;
using Core.Block.Config.LoadConfig;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaDefaultBlock : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId)
        {
            return new VanillaBlock(param.BlockId, entityId);
        }

        public IBlock Load(BlockConfigData param, int entityId, string state)
        {
            return new VanillaBlock(param.BlockId, entityId);
        }
    }
}
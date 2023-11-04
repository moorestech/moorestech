using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaDefaultBlock : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash)
        {
            return new VanillaBlock(param.BlockId, entityId, blockHash);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state)
        {
            return new VanillaBlock(param.BlockId, entityId, blockHash);
        }
    }
}
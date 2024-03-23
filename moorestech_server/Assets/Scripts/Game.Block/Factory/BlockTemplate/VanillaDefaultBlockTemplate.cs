using Game.Block.Interface;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaDefaultBlock : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash,BlockPositionInfo blockPositionInfo)
        {
            return new VanillaBlock(param.BlockId, entityId, blockHash);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state,BlockPositionInfo blockPositionInfo)
        {
            return new VanillaBlock(param.BlockId, entityId, blockHash);
        }
    }
}
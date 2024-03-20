using Game.Block.Interface;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public interface IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash);
        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state);
    }
}
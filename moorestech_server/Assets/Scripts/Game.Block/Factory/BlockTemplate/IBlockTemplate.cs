using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public interface IBlockTemplate
    {
        public IBlock New(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo);
        public IBlock Load(string state, BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo);
    }
    
    public class BlockCreateProperties
    {
    }
}
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public interface IBlockTemplate
    {
        public IBlock New(BlockConfigData blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo);
        public IBlock Load(string state, BlockConfigData blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo);
    }
    
    public class BlockCreateProperties
    {
    }
}
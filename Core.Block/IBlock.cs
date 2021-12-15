using Core.Block.Config;

namespace Core.Block
{
    public interface IBlock
    {
        public int GetIntId();
        public int GetBlockId();
        
        public IBlock New(BlockConfigData param, int intId);
    }
}
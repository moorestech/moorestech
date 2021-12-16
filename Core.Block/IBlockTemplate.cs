using Core.Block.Config;

namespace Core.Block
{
    public interface IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int intId);
    }
}
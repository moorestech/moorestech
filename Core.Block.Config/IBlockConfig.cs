using Core.Block.Config.LoadConfig;

namespace Core.Block.Config
{
    public interface IBlockConfig
    {
        public BlockConfigData GetBlockConfig(int id);
    }
}